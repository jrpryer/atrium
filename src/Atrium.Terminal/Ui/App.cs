using System.Diagnostics;
using Atrium.Core;
using Atrium.Core.Content;
using Atrium.Core.Runtime;

namespace Atrium.Terminal.Ui;

public sealed record MenuItem(string Key, string Label, Action Run, bool Enabled = true)
{
    public bool Matches(Key k) =>
        Key.Length == 1 ? k.Is(Key[0])
        : Key == "enter" ? k.Kind == KeyKind.Enter
        : Key == "esc" ? k.Kind == KeyKind.Escape
        : Key == "left" ? k.Kind == KeyKind.Left
        : Key == "right" ? k.Kind == KeyKind.Right
        : false;
}

/// <summary>One screen of the game. Views render state and offer a menu; all rules live in Atrium.Core.</summary>
public abstract class View
{
    protected App A { get; private set; } = null!;
    protected GameSession S => A.Session;
    protected ContentDatabase C => A.Session.Content;
    public bool Closed { get; protected set; }

    public abstract string Title { get; }
    public virtual string Locale => "hub";
    /// <summary>Redraw once a second (for the world-clock).</summary>
    public virtual bool Live => false;

    internal void Attach(App app) => A = app;
    public void Close() => Closed = true;

    public abstract void Render(Frame f);
    public abstract IEnumerable<MenuItem> Menu();
    public virtual void OnEnter() { }
}

/// <summary>Runs views, the world-clock, notices and modal widgets.</summary>
public sealed class App
{
    public GameSession Session { get; set; }
    public SaveStore Store { get; }
    public IInput In { get; }
    public string Slot { get; }
    public bool QuitRequested { get; private set; }

    private readonly List<Line> _messages = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _last;

    public App(GameSession session, SaveStore store, IInput input, string slot)
    {
        Session = session; Store = store; In = input; Slot = slot;
    }

    public int Width => Screen.Width;

    // ───── the loop ─────

    public void Run(View v)
    {
        v.Attach(this);
        v.OnEnter();
        while (!v.Closed && !QuitRequested)
        {
            StepClock();
            ShowNotices();
            if (v.Closed || QuitRequested) break;

            var menu = v.Menu().ToList();
            var f = new Frame(Width);
            Header(f, v);
            v.Render(f);
            Messages(f);
            var m = new Frame(Width);
            MenuLines(m, menu, Palette.Locale(v.Locale));
            // Never lose the menu off the bottom of a short window: trim the body instead.
            var room = Screen.Height - 1 - m.Lines.Count;
            if (Screen.Interactive && f.Lines.Count > room && room > 6)
            {
                var keep = f.Lines.Take(room - 1).ToList();
                f.Lines.Clear();
                f.Lines.AddRange(keep);
                f.Add("  " + Ansi.Fg(Palette.Faint, "… (a taller window shows the rest)"));
            }
            f.Lines.AddRange(m.Lines);
            Screen.Draw(f);

            var key = In.Read(v.Live && !In.Scripted ? 1000 : 0);
            if (key.Kind == KeyKind.Timeout) continue;
            var item = menu.FirstOrDefault(m => m.Enabled && m.Matches(key));
            if (item is null)
            {
                if (key.Kind == KeyKind.Escape || key.Is('x') && menu.All(m => m.Key != "x")) v.Close();
                continue;
            }
            _messages.Clear();
            item.Run();
        }
    }

    public void Quit() => QuitRequested = true;

    private void StepClock()
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = now - _last;
        _last = now;
        if (!In.Scripted) Session.StepRealTime(dt);
    }

    private void Header(Frame f, View v)
    {
        var w = Session.World;
        var paused = w.Clock.Paused ? Ansi.Fg(Palette.Rose, "  ⏸ the clock is paused") : "";
        var time = Session.Content.Config.TickDisplay == "numeric" ? $" · tick {w.Clock.Tick}" : "";
        var accent = Palette.Locale(v.Locale);
        f.Add();
        f.Add("  " + Ansi.BoldFg(Palette.Gold, "ATRIUM") + Ansi.Fg(Palette.Dim, $"  ·  Day {w.Clock.Day}, {w.Clock.TimeOfDay()}{time}") + paused);
        f.Add("  " + Ansi.BoldFg(accent, v.Title));
        f.Rule(accent);
    }

    private void Messages(Frame f)
    {
        if (_messages.Count == 0) return;
        f.Rule(Palette.Faint);
        foreach (var l in _messages) RenderLine(f, l);
    }

    public static void RenderLine(Frame f, Line l)
    {
        switch (l.Kind)
        {
            case LineKind.NewReading: Bullet(f, "✦ ", l.Text, Palette.Cream); break;
            case LineKind.Recall: Bullet(f, "· ", "(You recall) " + Wrap.FirstSentence(l.Text), Palette.Faint); break;
            case LineKind.Term: Bullet(f, "✎ ", l.Text, Palette.Gold); break;
            case LineKind.NearMiss: Bullet(f, "◇ ", l.Text, Palette.Violet); break;
            case LineKind.Resolved: Bullet(f, "★ ", l.Text, Palette.Lamp); break;
            case LineKind.Discovery: Bullet(f, "❀ ", l.Text, Palette.Moss); break;
            case LineKind.Soft: Bullet(f, "~ ", l.Text, Palette.Rose); break;
            case LineKind.System: Bullet(f, "› ", l.Text, Palette.Stone); break;
            default: Bullet(f, "  ", l.Text, Palette.Text); break;
        }
    }

    private static void Bullet(Frame f, string mark, string text, Rgb c)
    {
        var lines = Wrap.Lines(text, f.Width - 6);
        for (int i = 0; i < lines.Count; i++)
            f.Add("  " + Ansi.Fg(c, (i == 0 ? mark : "  ") + lines[i]));
    }

    private void MenuLines(Frame f, List<MenuItem> menu, Rgb accent)
    {
        f.Rule(accent);
        var cells = menu.Select(m =>
        {
            var key = m.Key.Length == 1 ? m.Key.ToUpperInvariant() : m.Key switch { "esc" => "Esc", "enter" => "⏎", "left" => "←", "right" => "→", _ => m.Key };
            return m.Enabled
                ? Ansi.BoldFg(accent, $"[{key}]") + " " + Ansi.Fg(Palette.Text, m.Label)
                : Ansi.Color ? Ansi.Fg(Palette.Faint, $"[{key}] {m.Label}") : $"[-] ({m.Label})";
        }).ToList();
        // Lay the menu out in as many columns as fit.
        var colW = Math.Min(38, cells.Select(Ansi.Width).DefaultIfEmpty(10).Max() + 3);
        var cols = Math.Max(1, (Width - 4) / colW);
        for (int i = 0; i < cells.Count; i += cols)
            f.Add("  " + string.Concat(cells.Skip(i).Take(cols).Select(c => Ansi.PadRight(c, colW))).TrimEnd());
    }

    // ───── results and notices ─────

    /// <summary>Shows an action's result: inline when short, as a card when long.</summary>
    public void Say(ActionResult r, string? title = null)
    {
        var length = r.Lines.Sum(l => l.Kind == LineKind.Recall ? 1 : Wrap.Lines(l.Text, Width - 6).Count);
        if (r.Lines.Any(l => l.Kind == LineKind.Resolved))
            Card(r.Lines.First(l => l.Kind == LineKind.Resolved).Text, r.Lines.Skip(1).ToList(), story: true);
        else if (length > 14) Card(title ?? "", r.Lines);
        else _messages.AddRange(r.Lines);
    }

    public void Say(string text, LineKind kind = LineKind.Narration) => _messages.Add(new Line(kind, text));

    private void ShowNotices()
    {
        foreach (var n in Session.DrainNotices())
        {
            var big = n.Kind is NoticeKind.Story or NoticeKind.Unlock or NoticeKind.Arrival || n.Text.Length > 220;
            if (big) Card(n.Title, new[] { new Line(n.Kind == NoticeKind.Story ? LineKind.Narration : LineKind.NewReading, n.Text) }, n.Kind == NoticeKind.Story);
            else _messages.Add(new Line(n.Kind is NoticeKind.Birth or NoticeKind.Discovery ? LineKind.Discovery : LineKind.System, $"{n.Title}: {n.Text}"));
        }
    }

    // ───── modal widgets ─────

    public void Card(string title, IEnumerable<Line> lines, bool story = false)
    {
        var f = new Frame(Width);
        f.Add();
        f.Add();
        if (title.Length > 0)
        {
            f.Add("     " + Ansi.BoldFg(story ? Palette.Lamp : Palette.Gold, story ? $"❦  {title}  ❦" : title));
            f.Add();
        }
        foreach (var l in lines)
        {
            var c = l.Kind switch
            {
                LineKind.NearMiss => Palette.Violet, LineKind.Resolved => Palette.Lamp, LineKind.Term => Palette.Gold,
                LineKind.Soft => Palette.Rose, LineKind.System => Palette.Stone, LineKind.Recall => Palette.Faint,
                LineKind.Discovery => Palette.Moss, _ => Palette.Text,
            };
            foreach (var w in Wrap.Lines(l.Kind == LineKind.Recall ? "(You recall) " + Wrap.FirstSentence(l.Text) : l.Text, Math.Min(Width - 10, 84), "     "))
                f.Add(Ansi.Fg(c, w));
            f.Add();
        }
        f.Add("     " + Ansi.Fg(Palette.Faint, "(any key)"));
        Screen.Draw(f);
        In.Read();
    }

    public bool Confirm(string title, string text, string yes = "Yes", string no = "Not yet")
    {
        var f = new Frame(Width);
        f.Add(); f.Add();
        f.Add("     " + Ansi.BoldFg(Palette.Gold, title));
        f.Add();
        foreach (var w in Wrap.Lines(text, Math.Min(Width - 10, 84), "     ")) f.Add(Ansi.Fg(Palette.Text, w));
        f.Add();
        f.Add("     " + Ansi.BoldFg(Palette.Moss, "[Y]") + " " + yes + "     " + Ansi.BoldFg(Palette.Rose, "[N]") + " " + no);
        Screen.Draw(f);
        while (true)
        {
            var k = In.Read();
            if (k.Is('y')) return true;
            if (k.Is('n') || k.Kind == KeyKind.Escape) return false;
        }
    }

    public string Prompt(string title, string text)
    {
        var f = new Frame(Width);
        f.Add(); f.Add();
        f.Add("     " + Ansi.BoldFg(Palette.Gold, title));
        f.Add();
        foreach (var w in Wrap.Lines(text, Math.Min(Width - 10, 84), "     ")) f.Add(Ansi.Fg(Palette.Dim, w));
        f.Add();
        Screen.Draw(f);
        Console.Write("     > ");
        return In.ReadLine().Trim();
    }

    private const string PickKeys = "123456789abcdefghijklmnopqrstuvwyz";

    /// <summary>A paged list picker. Returns null when the player backs out.</summary>
    public T? Pick<T>(string title, IReadOnlyList<T> items, Func<T, string> label, Func<T, string?>? detail = null, string? intro = null) where T : class
    {
        if (items.Count == 0) { Say("There's nothing to choose from.", LineKind.Soft); return null; }
        var pageSize = Math.Clamp(Screen.Height - 12 - (intro is null ? 0 : 3), 5, PickKeys.Length);
        if (detail is not null) pageSize = Math.Clamp((Screen.Height - 12) / 2, 4, PickKeys.Length);
        var page = 0;
        var pages = (items.Count + pageSize - 1) / pageSize;
        while (true)
        {
            var f = new Frame(Width);
            f.Add();
            f.Add("  " + Ansi.BoldFg(Palette.Gold, title) + (pages > 1 ? Ansi.Fg(Palette.Faint, $"   (page {page + 1} of {pages})") : ""));
            if (intro is not null) { f.Add(); f.Text(intro, Palette.Dim); }
            f.Rule(Palette.Faint);
            var slice = items.Skip(page * pageSize).Take(pageSize).ToList();
            for (int i = 0; i < slice.Count; i++)
            {
                f.Add("  " + Ansi.BoldFg(Palette.Gold, $"[{PickKeys[i]}]") + " " + Ansi.Fg(Palette.Text, label(slice[i])));
                var d = detail?.Invoke(slice[i]);
                if (!string.IsNullOrEmpty(d)) foreach (var l in Wrap.Lines(d, Width - 10, "       ")) f.Add(Ansi.Fg(Palette.Dim, l));
            }
            f.Rule(Palette.Faint);
            var nav = Ansi.BoldFg(Palette.Gold, "[Esc/X]") + " back";
            if (pages > 1) nav = Ansi.BoldFg(Palette.Gold, "[,]") + " previous page   " + Ansi.BoldFg(Palette.Gold, "[.]") + " next page   " + nav;
            f.Add("  " + nav);
            Screen.Draw(f);

            var k = In.Read();
            if (k.Kind == KeyKind.Escape || k.Is('x')) return null;
            if ((k.Is('.') || k.Kind == KeyKind.Right) && page < pages - 1) { page++; continue; }
            if ((k.Is(',') || k.Kind == KeyKind.Left) && page > 0) { page--; continue; }
            if (k.Kind != KeyKind.Char) continue;
            var idx = PickKeys.IndexOf(char.ToLowerInvariant(k.Char));
            if (idx >= 0 && idx < slice.Count) return slice[idx];
        }
    }

    // ───── saving ─────

    public void Save(bool quiet = false)
    {
        try
        {
            Store.Save(Slot, Session);
            if (!quiet) Say("Saved. The Atrium will keep.", LineKind.System);
        }
        catch (Exception ex)
        {
            Say($"Couldn't save: {ex.Message}", LineKind.Soft);
        }
    }
}
