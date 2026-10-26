using System.Numerics;
using Atrium.Core.Runtime;
using Atrium.Diorama.App;
using Atrium.Diorama.Render;
using Atrium.Diorama.UI;
using Raylib_cs;

namespace Atrium.Diorama.Overlays;

/// <summary>A full-screen parchment card for story beats, arrivals, unlocks, the prologue and resolutions.</summary>
public sealed class StoryCard : Overlay
{
    public enum Style { Story, Prologue, Resolution, Arrival, Unlock }

    private readonly string _title, _text;
    private readonly Style _style;
    private float _reveal;
    private bool _played;

    public StoryCard(Notice n) : this(n.Title, n.Text, n.Kind switch
    {
        NoticeKind.Arrival => Style.Arrival,
        NoticeKind.Unlock => Style.Unlock,
        _ => Style.Story,
    }) { }

    public StoryCard(string title, string text, Style style) { _title = title; _text = text; _style = style; }

    public override string Describe() => $"StoryCard[{_style}] {_title}: {_text}";

    public override void Draw()
    {
        if (!_played) { _played = true; App.Audio.Play("page", 0.8f); }
        float t = Ease.OutBack(MathF.Min(1, Age / 0.45f), 1.2f);
        float a = MathF.Min(1, Age / 0.25f);
        Ui.Dim(0.5f * a);
        var bodyFont = Ui.F.Body;
        float w = 940, size = 29;
        var textH = Text.Height(bodyFont, _text, w - 140, size, 1.3f);
        float h = MathF.Min(820, 230 + textH);
        float s = 0.94f + 0.06f * t;
        var r = new Rectangle(Ui.W / 2 - w * s / 2, Ui.H / 2 - h * s / 2 + (1 - t) * 30, w * s, h * s);
        var accent = _style switch
        {
            Style.Resolution => Palette.Sanctum,
            Style.Arrival => Palette.Pools,
            Style.Unlock => Palette.Library,
            _ => Palette.InkSoft,
        };
        Ui.Panel(r, a, accent);
        float cx = r.X + r.Width / 2;
        var kicker = _style switch
        {
            Style.Prologue => "",
            Style.Resolution => "understood",
            Style.Arrival => "something turned up",
            Style.Unlock => "a door opens",
            _ => "",
        };
        if (kicker.Length > 0) Text.DrawCentered(Ui.F.Italic, kicker, new Vector2(cx, r.Y + 46), 22, Palette.Alpha(accent, a));
        Ui.HeadingCentered(_title, new Vector2(cx, r.Y + 92), 60, Palette.Alpha(Palette.Ink, a));
        Ui.Rule(cx - 160, r.Y + 134, 320, Palette.Alpha(accent, a));

        _reveal += Ui.Dt * (Ui.ReduceMotion ? 4000 : 90);
        if (Ui.InputEnabled && Input.Pressed && _reveal < _text.Length) { _reveal = _text.Length; Input.ClickConsumed = true; }
        var body = new Rectangle(r.X + 70, r.Y + 160, r.Width - 140, r.Height - 230);
        float off = Ui.BeginScroll("story", body, textH + 10);
        Text.DrawWrapped(bodyFont, _text, new Rectangle(body.X, body.Y - off, body.Width, body.Height), size, Palette.Alpha(Palette.Ink, a), 1.3f, (int)_reveal);
        Ui.EndScroll();

        if (_style == Style.Resolution) WaxSeal(new Vector2(r.X + r.Width - 70, r.Y + 70), MathF.Min(1, Age / 0.6f));
        if (_reveal >= _text.Length)
        {
            if (Ui.Button("story-continue", new Rectangle(cx - 110, r.Y + r.Height - 62, 220, 44), _style == Style.Prologue ? "Begin" : "Continue", accent))
                Close();
        }
        else Text.DrawCentered(Ui.F.Italic, "(click to read on)", new Vector2(cx, r.Y + r.Height - 40), 20, Palette.InkFaint);
    }

    private void WaxSeal(Vector2 c, float t)
    {
        float s = Ease.OutElastic(t);
        if (s <= 0.01f) return;
        var wax = Palette.Hex(0x9C3A2E);
        Raylib.DrawCircleV(c + new Vector2(2, 4), 34 * s, Palette.Alpha(Palette.Ink, 0.25f));
        for (int i = 0; i < 9; i++)
        {
            float ang = i / 9f * MathF.Tau;
            Raylib.DrawCircleV(c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 26 * s, 12 * s, wax);
        }
        Raylib.DrawCircleV(c, 30 * s, wax);
        Raylib.DrawCircleLinesV(c, 22 * s, Palette.Mul(wax, 1.35f));
        Ui.Diamond(c, 10 * s, Palette.Mul(wax, 1.5f));
    }
}

/// <summary>A smaller card for what a mechanic revealed: readings, new terms, near-misses.</summary>
public sealed class ReadingCard : Overlay
{
    private readonly string _title;
    private readonly IReadOnlyList<Line> _lines;
    private float _reveal;

    public ReadingCard(string title, IReadOnlyList<Line> lines) { _title = title; _lines = lines; }

    public override string Describe() => $"ReadingCard {_title}: " + string.Join(" / ", _lines.Select(l => $"[{l.Kind}] {l.Text}"));

    public override void Draw()
    {
        float t = Ease.OutBack(MathF.Min(1, Age / 0.35f));
        Ui.Dim(0.32f * MathF.Min(1, Age / 0.2f));
        float w = 820, size = 27, inner = w - 150;
        float textH = _lines.Sum(l => Text.Height(Font(l), Show(l), inner, size, 1.28f) + 16);
        float h = MathF.Min(800, 150 + textH + (_title.Length > 0 ? 50 : 0));
        var r = new Rectangle(Ui.W / 2 - w / 2, Ui.H / 2 - h / 2 + (1 - t) * 24, w, h);
        var nearMiss = _lines.Any(l => l.Kind == LineKind.NearMiss);
        Ui.Panel(r, MathF.Min(1, Age / 0.2f), nearMiss ? Palette.Apothecary : Palette.InkSoft);
        float y = r.Y + 36;
        if (_title.Length > 0) { Ui.HeadingCentered(_title, new Vector2(r.X + w / 2, y + 12), 42); y += 56; }
        _reveal += Ui.Dt * (Ui.ReduceMotion ? 4000 : 110);
        if (Ui.InputEnabled && Input.Pressed && _reveal < TotalChars) { _reveal = TotalChars; Input.ClickConsumed = true; }
        int budget = (int)_reveal;
        foreach (var l in _lines)
        {
            var c = l.Kind switch
            {
                LineKind.NearMiss => Palette.Apothecary,
                LineKind.Term => Palette.Sanctum,
                LineKind.Discovery => Palette.LeafDeep,
                LineKind.Recall => Palette.InkFaint,
                LineKind.Soft => Palette.Rose,
                LineKind.System => Palette.InkSoft,
                _ => Palette.Ink,
            };
            Mark(l.Kind, new Vector2(r.X + 50, y + 15), c);
            var text = Show(l);
            float used = Text.DrawWrapped(Font(l), text, new Rectangle(r.X + 80, y, inner, 400), size, c, 1.28f, budget);
            budget -= text.Length + 1;
            y += used + 16;
            if (budget <= 0) break;
        }
        if (_reveal >= TotalChars && Ui.Button("reading-ok", new Rectangle(r.X + w / 2 - 90, r.Y + h - 60, 180, 42), "I'll remember"))
            Close();
    }

    private int TotalChars => _lines.Sum(l => Show(l).Length + 1);

    private Font Font(Line l) => l.Kind is LineKind.Recall or LineKind.System or LineKind.Term ? Ui.F.Italic : Ui.F.Body;

    private static string Show(Line l) => l.Kind == LineKind.Recall ? "You recall: " + First(l.Text) : l.Text;

    private static string First(string s)
    {
        var i = s.IndexOf(". ", StringComparison.Ordinal);
        return i > 30 ? s[..(i + 1)] : s;
    }

    private static void Mark(LineKind k, Vector2 c, Color col)
    {
        switch (k)
        {
            case LineKind.NewReading: Ui.Diamond(c, 7, col); break;
            case LineKind.Term: Raylib.DrawLineEx(c + new Vector2(-8, 8), c + new Vector2(8, -8), 3, col); Raylib.DrawCircleV(c + new Vector2(-8, 8), 2.5f, col); break;
            case LineKind.NearMiss: Raylib.DrawCircleLinesV(c, 8, col); Raylib.DrawCircleLinesV(c, 5, col); break;
            case LineKind.Discovery: for (int i = 0; i < 5; i++) { float a = i * MathF.Tau / 5; Raylib.DrawCircleV(c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 6, 3.5f, col); } break;
            case LineKind.Recall: Raylib.DrawCircleV(c, 3, col); break;
            default: Raylib.DrawCircleV(c, 4, Palette.Alpha(col, 0.6f)); break;
        }
    }
}

/// <summary>Small notes that slide in at the top right and drift away.</summary>
public sealed class Toasts
{
    private sealed class T { public string Title = "", Text = ""; public NoticeKind Kind; public LineKind Line; public float Age, Life; public int Count = 1; }
    private readonly List<T> _ts = new();

    public void Add(string title, string text, NoticeKind kind, LineKind line = LineKind.Narration)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        // repeats of the same kind of news fold into one card (the journal keeps every line)
        var same = _ts.FirstOrDefault(t => (title.Length > 0 ? t.Title == title : t.Title.Length == 0 && t.Text == text) && t.Age < t.Life - 0.6f);
        if (same is not null)
        {
            if (same.Text != text || title.Length > 0) same.Count++;
            same.Text = text;
            same.Age = MathF.Min(same.Age, 0.35f);
            same.Life = Math.Clamp(3.5f + text.Length / 40f, 4, 10);
            return;
        }
        _ts.Add(new T { Title = title, Text = text, Kind = kind, Line = line, Life = Math.Clamp(3.5f + text.Length / 40f, 4, 10) });
        while (_ts.Count > 5) _ts.RemoveAt(0);
    }

    /// <summary>What the notices say right now (capture logs).</summary>
    public IEnumerable<string> Dump() => _ts.Select(t => (t.Title.Length > 0 ? t.Title + (t.Count > 1 ? $" x{t.Count}" : "") + ": " : "") + t.Text);

    public void Update(float dt)
    {
        foreach (var t in _ts) t.Age += dt;
        _ts.RemoveAll(t => t.Age > t.Life);
    }

    public void Draw(GameApp app)
    {
        var ui = app.Ui;
        float y = 120;
        foreach (var t in _ts)
        {
            float inT = Ease.OutCubic(t.Age / 0.35f), outT = Ease.OutCubic((t.Life - t.Age) / 0.5f);
            float vis = MathF.Min(inT, outT);
            var lines = Text.Wrap(ui.F.Body, t.Text, 21, 380);
            float h = 26 + lines.Count * 26 + (t.Title.Length > 0 ? 28 : 0);
            var r = new Rectangle(Ui.W - 440 + (1 - vis) * 80, y, 420, h);
            var accent = t.Kind switch
            {
                NoticeKind.Birth => Palette.Pools,
                NoticeKind.Discovery => Palette.LeafDeep,
                NoticeKind.Essence => Palette.Sanctum,
                _ => t.Line == LineKind.Soft ? Palette.Rose : Palette.InkSoft,
            };
            Raylib.DrawRectangleRounded(new Rectangle(r.X + 3, r.Y + 5, r.Width, r.Height), 0.15f, 6, Palette.Alpha(Palette.Ink, 0.2f * vis));
            Raylib.DrawRectangleRounded(r, 0.15f, 6, Palette.Alpha(Palette.Parchment, 0.96f * vis));
            Raylib.DrawRectangleRounded(new Rectangle(r.X, r.Y, 6, r.Height), 0.5f, 4, Palette.Alpha(accent, vis));
            float ty = r.Y + 12;
            if (t.Title.Length > 0)
            {
                Text.Draw(ui.F.Hand, t.Count > 1 ? $"{t.Title}  ×{t.Count}" : t.Title, new Vector2(r.X + 20, ty - 4), 26, Palette.Alpha(Palette.Ink, vis));
                ty += 28;
            }
            foreach (var l in lines) { Text.Draw(ui.F.Body, l, new Vector2(r.X + 20, ty), 21, Palette.Alpha(Palette.Ink, vis)); ty += 26; }
            y += h + 10 * vis;
        }
    }
}

/// <summary>A yes/no question on parchment.</summary>
public sealed class Confirm : Overlay
{
    private readonly string _title, _text, _yes, _no;
    private readonly Action _onYes;
    private readonly Color _accent;

    public Confirm(string title, string text, Action onYes, string yes = "Go ahead", string no = "Not yet", Color? accent = null)
    { _title = title; _text = text; _onYes = onYes; _yes = yes; _no = no; _accent = accent ?? Palette.InkSoft; }

    public override string Describe() => $"Confirm {_title}: {_text}";

    /// <summary>Answers yes (captures).</summary>
    public void Yes() { Close(); _onYes(); }

    public override void Draw()
    {
        float t = Ease.OutBack(MathF.Min(1, Age / 0.3f));
        Ui.Dim(0.4f * MathF.Min(1, Age / 0.2f));
        float w = 760, inner = w - 120;
        float th = Text.Height(Ui.F.Body, _text, inner, 26);
        float h = 200 + th;
        var r = new Rectangle(Ui.W / 2 - w / 2, Ui.H / 2 - h / 2 + (1 - t) * 20, w, h);
        Ui.Panel(r, 1, _accent);
        Ui.HeadingCentered(_title, new Vector2(r.X + w / 2, r.Y + 50), 42);
        Ui.Paragraph(_text, new Rectangle(r.X + 60, r.Y + 92, inner, th + 10), 26);
        if (Ui.Button("confirm-yes", new Rectangle(r.X + w / 2 - 230, r.Y + h - 66, 210, 46), _yes, _accent)) { Close(); _onYes(); }
        if (Ui.Button("confirm-no", new Rectangle(r.X + w / 2 + 20, r.Y + h - 66, 210, 46), _no)) Close();
    }
}

/// <summary>A scrollable list chooser.</summary>
public sealed class Picker<T> : Overlay, IChooser where T : class
{
    public sealed record Entry(T Value, string Label, string? Sub = null, Color? Swatch = null, bool Enabled = true);

    private readonly string _title;
    private readonly string? _intro;
    private readonly IReadOnlyList<Entry> _entries;
    private readonly Action<T> _onPick;
    private readonly string _id = "picker" + Guid.NewGuid().ToString("N")[..6];

    public Picker(string title, IReadOnlyList<Entry> entries, Action<T> onPick, string? intro = null)
    { _title = title; _entries = entries; _onPick = onPick; _intro = intro; }

    public IReadOnlyList<string> Labels => _entries.Select(e => e.Label + (e.Enabled ? "" : " (disabled)")).ToList();
    public void Choose(int index) { if (!_entries[index].Enabled) return; Close(); _onPick(_entries[index].Value); }
    public override string Describe() => $"Picker {_title}: " + string.Join(" | ", Labels.Select((l, i) => $"{i}:{l}"));

    public override void Draw()
    {
        float t = Ease.OutBack(MathF.Min(1, Age / 0.3f));
        Ui.Dim(0.4f * MathF.Min(1, Age / 0.2f));
        float w = 760;
        float introH = _intro is null ? 0 : Text.Height(Ui.F.Italic, _intro, w - 120, 23) + 12;
        bool subs = _entries.Any(e => e.Sub is not null);
        float rowH = subs ? 62 : 44;
        float listH = MathF.Min(_entries.Count * rowH, 560 - introH);
        float h = 150 + introH + listH;
        var r = new Rectangle(Ui.W / 2 - w / 2, Ui.H / 2 - h / 2 + (1 - t) * 20, w, h);
        Ui.Panel(r);
        Ui.HeadingCentered(_title, new Vector2(r.X + w / 2, r.Y + 46), 40);
        if (_intro is not null) Ui.Paragraph(_intro, new Rectangle(r.X + 60, r.Y + 80, w - 120, introH), 23, Palette.InkSoft, Ui.F.Italic);
        var view = new Rectangle(r.X + 40, r.Y + 88 + introH, w - 80, listH);
        float off = Ui.BeginScroll(_id, view, _entries.Count * rowH);
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            var row = new Rectangle(view.X, view.Y + i * rowH - off, view.Width - 12, rowH - 4);
            if (row.Y + row.Height < view.Y || row.Y > view.Y + view.Height) continue;
            if (Ui.Row($"{_id}-{i}", row, e.Label, e.Sub, e.Swatch, enabled: e.Enabled) && Ui.Hover(view)) { Close(); _onPick(e.Value); }
        }
        Ui.EndScroll();
        if (Ui.Button(_id + "-back", new Rectangle(r.X + w / 2 - 80, r.Y + h - 56, 160, 40), "Never mind")) Close();
    }
}

/// <summary>Volumes, reduce motion, fullscreen; save and quit.</summary>
public sealed class SettingsCard : Overlay
{
    public override void Draw()
    {
        float t = Ease.OutBack(MathF.Min(1, Age / 0.3f));
        Ui.Dim(0.45f);
        var r = new Rectangle(Ui.W / 2 - 330, Ui.H / 2 - 290 + (1 - t) * 20, 660, 580);
        Ui.Panel(r);
        Ui.HeadingCentered("Settings", new Vector2(r.X + 330, r.Y + 50), 46);
        var s = App.Settings;
        float y = r.Y + 110;
        s.Master = Slider("Master volume", s.Master, ref y, r);
        s.Music = Slider("Music", s.Music, ref y, r);
        s.Sfx = Slider("Sounds", s.Sfx, ref y, r);
        s.Ambience = Slider("Water and rooms", s.Ambience, ref y, r);
        y += 10;
        if (Ui.Button("set-motion", new Rectangle(r.X + 60, y, 260, 44), s.ReduceMotion ? "Motion: reduced" : "Motion: full")) s.ReduceMotion = !s.ReduceMotion;
        if (Ui.Button("set-full", new Rectangle(r.X + 340, y, 260, 44), "Toggle fullscreen")) { Raylib.ToggleBorderlessWindowed(); s.Fullscreen = !s.Fullscreen; }
        y += 64;
        App.ApplySettings();
        if (App.Session is not null && Ui.Button("set-quit", new Rectangle(r.X + 60, y, 260, 44), "Save and quit", Palette.Sanctum))
        { App.SaveQuietly(); App.SaveSettings(); App.Quitting = true; }
        if (Ui.Button("set-close", new Rectangle(r.X + 340, y, 260, 44), "Back to the game")) { App.SaveSettings(); Close(); }
    }

    private float Slider(string label, float v, ref float y, Rectangle r)
    {
        Ui.Label(label, new Vector2(r.X + 60, y), 25);
        var track = new Rectangle(r.X + 300, y + 14, 290, 6);
        Raylib.DrawRectangleRounded(track, 1, 4, Palette.ParchmentDark);
        Raylib.DrawRectangleRounded(new Rectangle(track.X, track.Y, track.Width * v, track.Height), 1, 4, Palette.Lantern);
        var hit = new Rectangle(track.X - 10, y, track.Width + 20, 34);
        Ui.Block(hit);
        if (Ui.Hover(hit) && Input.Down)
        {
            v = Math.Clamp((Input.Mouse.X - track.X) / track.Width, 0, 1);
            Input.ClickConsumed = true;
        }
        Raylib.DrawCircleV(new Vector2(track.X + track.Width * v, track.Y + 3), 11, Palette.Parchment);
        Raylib.DrawCircleLinesV(new Vector2(track.X + track.Width * v, track.Y + 3), 11, Palette.InkSoft);
        y += 52;
        return v;
    }
}
