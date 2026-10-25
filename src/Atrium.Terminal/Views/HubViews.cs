using Atrium.Core.Runtime;
using Atrium.Terminal.Ui;

namespace Atrium.Terminal.Views;

/// <summary>The courtyard: the tree, the four houses, the clock.</summary>
public sealed class HubView : View
{
    public override string Title => "The Atrium";
    public override bool Live => true;

    public override void OnEnter() => A.Save(quiet: true);

    public override void Render(Frame f)
    {
        // The courtyard: one fixed scene. The tree and the falls never change.
        var hub = S.Hub;
        var artWidth = hub.Art.Count == 0 ? 0 : hub.Art.Max(a => a.Length) + 3;
        var desc = Wrap.Lines(hub.Description, Math.Max(30, A.Width - artWidth - 6));
        var rows = Math.Max(hub.Art.Count, desc.Count + 2);
        for (int i = 0; i < rows; i++)
        {
            var left = i < hub.Art.Count ? Scenery(hub.Art[i]) : "";
            var right = i == 0 ? Ansi.BoldFg(Palette.Moss, hub.Caption)
                : i - 1 < desc.Count ? Ansi.Fg(Palette.Dim, desc[i - 1]) : "";
            f.Add("  " + Ansi.PadRight(left, artWidth) + right);
        }
        f.Add();
        foreach (var l in C.LocalesInOrder)
        {
            var isOpen = S.IsUnlocked(l.Id);
            var name = Ansi.PadRight(isOpen ? Ansi.BoldFg(Palette.Locale(l.Id), l.Name) : Ansi.Fg(Palette.Faint, l.Name), 22);
            var text = isOpen ? l.Description : l.LockedDescription;
            var lines = Wrap.Lines(text, A.Width - 30);
            for (int i = 0; i < lines.Count; i++)
                f.Add("  " + (i == 0 ? name : new string(' ', 22)) + Ansi.Fg(isOpen ? Palette.Dim : Palette.Faint, lines[i]));
        }
        var open = S.Unresolved.Open.ToList();
        if (open.Count > 0)
        {
            f.Add();
            f.Add("  " + Ansi.Fg(Palette.Gold, "Still unexplained: ") + Ansi.Fg(Palette.Text, string.Join(", ", open.Select(o => o.Name))));
        }
        if (S.ChapterComplete) f.Add("  " + Ansi.Fg(Palette.Lamp, "Chapter One is complete. The Atrium is yours to keep."));
    }

    public override IEnumerable<MenuItem> Menu()
    {
        var i = 1;
        foreach (var l in C.LocalesInOrder)
        {
            var id = l.Id;
            yield return new MenuItem((i++).ToString(), l.Name.Replace("The ", ""), () => Go(id), S.IsUnlocked(id));
        }
        yield return new MenuItem("j", "Journal", () => A.Run(new JournalView()));
        yield return new MenuItem("s", "Satchel", () => A.Run(new SatchelView()));
        yield return new MenuItem("w", "Sit by the water", () => A.Say(S.SitAWhile()));
        yield return new MenuItem("p", S.World.Clock.Paused ? "Resume the clock" : "Pause the clock", TogglePause);
        yield return new MenuItem("h", "How to play", () => A.Run(new HelpView()));
        yield return new MenuItem("o", "Save", () => A.Save());
        yield return new MenuItem("q", "Save and quit", () => { A.Save(quiet: true); A.Quit(); });
    }

    /// <summary>Colours the courtyard art: water and spray, bark, leaves.</summary>
    private static string Scenery(string line)
    {
        static Rgb ColorOf(char c) => c switch
        {
            '~' or '¦' or ':' or '.' => Palette.Water,
            '|' or '/' or '\\' or '_' => Palette.Bark,
            _ => Palette.Moss,
        };
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < line.Length)
        {
            if (line[i] == ' ') { sb.Append(' '); i++; continue; }
            var color = ColorOf(line[i]);
            int j = i;
            while (j < line.Length && line[j] != ' ' && ColorOf(line[j]).Equals(color)) j++;
            sb.Append(Ansi.Fg(color, line[i..j]));
            i = j;
        }
        return sb.ToString();
    }

    private void TogglePause()
    {
        S.SetPaused(!S.World.Clock.Paused);
        A.Say(S.World.Clock.Paused ? "You let the world hold its breath. Nothing will change until you resume." : "The falls take up their falling again.", LineKind.System);
    }

    private void Go(string locale)
    {
        View v = locale switch
        {
            "library" => new LibraryView(),
            "apothecary" => new ApothecaryView(),
            "pools" => new PoolsView(),
            "sanctum" => new SanctumView(),
            _ => throw new InvalidOperationException($"No view for locale '{locale}'."),
        };
        A.Run(v);
        A.Save(quiet: true);
    }
}

/// <summary>Base for locale screens: shows the entry text and re-presents open objects on arrival.</summary>
public abstract class LocaleView : View
{
    public override void OnEnter()
    {
        var r = S.Enter(Locale);
        var intro = r.Lines.Where(l => l.Kind == LineKind.Narration).ToList();
        if (intro.Count > 0) A.Card(Title, intro);
        foreach (var l in r.Lines.Where(l => l.Kind != LineKind.Narration)) A.Say(l.Text, l.Kind);
    }

    protected IEnumerable<MenuItem> Common()
    {
        yield return new MenuItem("j", "Journal", () => A.Run(new JournalView()));
        yield return new MenuItem("s", "Satchel", () => A.Run(new SatchelView()));
        yield return new MenuItem("x", "Back to the courtyard", Close);
    }
}

public sealed class HelpView : View
{
    public override string Title => "How to play";

    public override void Render(Frame f)
    {
        f.Text("Atrium has no timers, no failure and no hurry. Wrong guesses cost only a moment, and nothing you find is ever lost for good.", Palette.Text);
        f.Add();
        f.Text("The four houses are each a small game of their own:", Palette.Gold);
        f.Text("• The Library: hang genre tags on the shelves, then shelve the pile, oldest nearest the lamp. Read anything. Write notes in the margins. Check one book out to carry.", Palette.Text);
        f.Text("• The Apothecary: put up to three elements on the bench, pick a tool or none, and mix. Discoveries are yours for good and never run out. Failed mixes still teach you something, and the Discovery Map shows what you're near. Bottle elements to carry them to other places.", Palette.Text);
        f.Text("• The Spirit Pools: every pool sits somewhere between Light and Dark, Hot and Cold. Keep creatures fed and the water clear, in water they like, and they'll breed. Things laid in a pool change its energy, and sometimes the water slowly changes them.", Palette.Text);
        f.Text("• The Sanctum: examine things under the glass, assemble tools from what you've made, and use the chalk circle to remake what you've taken apart.", Palette.Text);
        f.Add();
        f.Text("Odd objects turn up now and then. Carry them in your satchel and try them in each house's own way: on a shelf, against a page, on the scales, in the burner, in the water, under the glass. What you notice goes in your journal, and some of it earns words to describe the object.", Palette.Text);
        f.Text("When your notes feel full enough, go to the Sanctum's designation desk and write down what you believe the thing truly is. If you're wrong in an interesting way, you'll learn exactly why.", Palette.Text);
        f.Add();
        f.Text("The world-clock runs while the game is open. Sit by the water to let time pass, or pause the clock from the courtyard.", Palette.Dim);
    }

    public override IEnumerable<MenuItem> Menu()
    {
        yield return new MenuItem("x", "Back", Close);
    }
}
