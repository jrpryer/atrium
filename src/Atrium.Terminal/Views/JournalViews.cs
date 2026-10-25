using Atrium.Core;
using Atrium.Core.Runtime;
using Atrium.Terminal.Ui;

namespace Atrium.Terminal.Views;

/// <summary>The Player Journal (kept at the Sanctum; your copy goes everywhere). Nothing written here is ever lost.</summary>
public sealed class JournalView : View
{
    private int _tab = 1;
    private int _logPage;
    public override string Title => "Your journal";
    public override string Locale => "sanctum";

    private Journal J => new(S);

    public override void Render(Frame f)
    {
        string[] tabs = { "Mysteries", "Lexicon", "Story so far", "Margin notes", "Event log" };
        f.Add("  " + string.Join("   ", tabs.Select((t, i) => i + 1 == _tab ? Ansi.BoldFg(Palette.Violet, $"{i + 1} {t}") : Ansi.Fg(Palette.Dim, $"{i + 1} {t}"))));
        f.Add();
        switch (_tab)
        {
            case 1:
                var objs = J.Objects();
                if (objs.Count == 0) f.Text("Nothing odd has turned up yet.", Palette.Dim);
                foreach (var o in objs)
                {
                    f.Add("  " + (o.Resolved ? Ansi.Fg(Palette.Lamp, "★ ") : Ansi.Fg(Palette.Gold, "? ")) + Ansi.BoldFg(Palette.Text, PoolsView.Cap(o.Object.Name))
                          + Ansi.Fg(Palette.Dim, $"  — {o.Readings.Count} note{(o.Readings.Count == 1 ? "" : "s")}; now {o.Where}"));
                    if (o.Designation is not null) f.Add("     " + Ansi.Fg(Palette.Lamp, o.Designation));
                }
                f.Add();
                f.Text("Choose an entry to read your notes in full.", Palette.Faint);
                break;
            case 2:
                foreach (var (facet, terms) in J.Lexicon())
                {
                    f.Add("  " + Ansi.Fg(Palette.Gold, facet.Label));
                    if (terms.Count == 0) f.Add("     " + Ansi.Fg(Palette.Faint, "(no words yet)"));
                    foreach (var t in terms) f.Add("     " + Ansi.Fg(Palette.Cream, t.Label) + Ansi.Fg(Palette.Dim, "  " + t.Gloss));
                }
                break;
            case 3:
                var story = J.Story();
                if (story.Count == 0) f.Text("The story hasn't really started. Or it has, and you haven't noticed yet.", Palette.Dim);
                foreach (var b in story) { f.Add("  " + Ansi.BoldFg(Palette.Lamp, b.Title)); f.Text(b.Text, Palette.Text, "     "); f.Add(); }
                break;
            case 4:
                var notes = J.Notes();
                if (notes.Count == 0) f.Text("You haven't written in any margins yet. (Open a book in the Library and press N.)", Palette.Dim);
                foreach (var n in notes) { f.Add("  " + Ansi.Fg(Palette.Gold, $"{n.Tome}, p.{n.Page}")); f.Bullet("✎ ", n.Text, Palette.Cream); }
                break;
            case 5:
                var log = J.Log().Reverse().ToList();
                var size = Math.Max(6, Screen.Height - 16);
                var pages = Math.Max(1, (log.Count + size - 1) / size);
                _logPage = Math.Clamp(_logPage, 0, pages - 1);
                foreach (var e in log.Skip(_logPage * size).Take(size))
                    f.Add("  " + Ansi.Fg(Palette.Faint, $"day {e.Tick / 2400 + 1} ") + Ansi.Fg(Color(e.Kind), Trim(e.Text, A.Width - 14)));
                if (pages > 1) f.Add("  " + Ansi.Fg(Palette.Faint, $"(page {_logPage + 1} of {pages}; newest first)"));
                break;
        }
    }

    private static string Trim(string s, int w)
    {
        s = s.Replace('\n', ' ');
        return s.Length > w ? s[..(w - 1)] + "…" : s;
    }

    private static Rgb Color(LogKind k) => k switch
    {
        LogKind.Story => Palette.Lamp, LogKind.Reading => Palette.Cream, LogKind.NearMiss => Palette.Violet,
        LogKind.Resolved => Palette.Lamp, LogKind.Discovery => Palette.Moss, LogKind.Pools => Palette.Water,
        LogKind.Library => Palette.Moss, LogKind.Essence => Palette.Gold, LogKind.Tool => Palette.Violet, _ => Palette.Dim,
    };

    public override IEnumerable<MenuItem> Menu()
    {
        for (int i = 1; i <= 5; i++) { var t = i; yield return new MenuItem(t.ToString(), new[] { "", "Mysteries", "Lexicon", "Story", "Notes", "Log" }[t], () => _tab = t); }
        if (_tab == 1) yield return new MenuItem("r", "Read an entry", ReadEntry, J.Objects().Count > 0);
        if (_tab == 5) { yield return new MenuItem(",", "Newer", () => _logPage--); yield return new MenuItem(".", "Older", () => _logPage++); }
        yield return new MenuItem("x", "Close the journal", Close);
    }

    private void ReadEntry()
    {
        var o = A.Pick("Which entry?", J.Objects(), x => (x.Resolved ? "★ " : "? ") + PoolsView.Cap(x.Object.Name));
        if (o is not null) A.Run(new JournalObjectView(o.Object.Id));
    }
}

public sealed class JournalObjectView : View
{
    private readonly string _obj;
    public JournalObjectView(string obj) => _obj = obj;

    public override string Title => "Journal: " + C.Objects[_obj].Name;
    public override string Locale => "sanctum";

    public override void Render(Frame f)
    {
        var o = new Journal(S).Objects().First(x => x.Object.Id == _obj);
        f.Text(o.Object.Summary, Palette.Dim);
        f.Add("  " + Ansi.Fg(Palette.Faint, "Now: " + o.Where));
        if (o.Designation is not null) f.Add("  " + Ansi.Fg(Palette.Lamp, "★ " + o.Designation));
        f.Add();
        foreach (var g in o.Readings.GroupBy(r => r.Locale))
        {
            f.Add("  " + Ansi.Fg(Palette.Gold, "At " + g.Key.Replace("The ", "the ")));
            foreach (var r in g) f.Bullet("✦ ", r.Text, Palette.Cream);
        }
        if (o.NearMisses.Count > 0)
        {
            f.Add("  " + Ansi.Fg(Palette.Violet, "Ruled out"));
            foreach (var m in o.NearMisses) f.Bullet("◇ ", m, Palette.Violet);
        }
        if (o.Considered.Count > 0)
        {
            f.Add("  " + Ansi.Fg(Palette.Dim, "Considered, didn't hold"));
            foreach (var m in o.Considered) f.Bullet("· ", m, Palette.Dim);
        }
        if (o.Touched.Count > 0)
        {
            f.Add("  " + Ansi.Fg(Palette.Moss, "What it has touched (these stay, whatever happens to it)"));
            foreach (var t in o.Touched) f.Bullet("❀ ", t, Palette.Moss);
        }
    }

    public override IEnumerable<MenuItem> Menu()
    {
        yield return new MenuItem("x", "Back", Close);
    }
}

/// <summary>What you're carrying. The checked-out book can be read here, anywhere.</summary>
public sealed class SatchelView : View
{
    public override string Title => "Your satchel";

    public override void Render(Frame f)
    {
        var items = S.World.Items.Satchel.ToList();
        if (items.Count == 0) f.Text("Empty, apart from a pencil and a slightly squashed apple.", Palette.Dim);
        foreach (var i in items)
        {
            var color = i.Inert ? Palette.Faint : i.Essence is not null ? Palette.Lamp : i.RemnantOf is not null ? Palette.Stone : Palette.Text;
            f.Add("  " + Ansi.Fg(color, "· " + S.World.Items.Name(i)));
            f.Text(S.World.Items.Description(i), Palette.Dim, "      ");
        }
        foreach (var t in S.Library.CheckedOut()) f.Add("  " + Ansi.Fg(Palette.Moss, "· a library book: " + t.Title));
    }

    public override IEnumerable<MenuItem> Menu()
    {
        yield return new MenuItem("r", "Read your library book", () =>
        {
            var t = A.Pick("Read which?", S.Library.CheckedOut(), x => x.Title);
            if (t is not null) A.Run(new ReaderView(t.Id));
        }, S.Library.CheckedOut().Count > 0);
        yield return new MenuItem("x", "Back", Close);
    }
}
