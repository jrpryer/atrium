using Atrium.Core.Content;
using Atrium.Core.Mystery;
using Atrium.Core.Runtime;
using Atrium.Terminal.Ui;

namespace Atrium.Terminal.Views;

public sealed class SanctumView : LocaleView
{
    public override string Title => "The Sanctum";
    public override string Locale => "sanctum";

    public override void Render(Frame f)
    {
        var san = S.Sanctum;
        var open = S.Unresolved.Open.ToList();
        f.Add("  " + Ansi.Fg(Palette.Gold, "The journal desk: ") + Ansi.Fg(Palette.Text, open.Count == 0 ? "nothing unexplained, for now." : $"{open.Count} thing{(open.Count == 1 ? "" : "s")} still unexplained"));
        foreach (var o in open)
        {
            var ready = S.Gate.AnyAttemptable(o);
            f.Add("     " + Ansi.Fg(Palette.Text, "· " + o.Name) + (ready ? Ansi.Fg(Palette.Moss, "   your notes feel full enough to try") : Ansi.Fg(Palette.Faint, "   you'd want to know more first")));
        }
        foreach (var seal in san.Seals())
        {
            f.Add();
            f.Add("  " + Ansi.Fg(Palette.Gold, PoolsView.Cap(seal.Name)));
            f.Text(san.Solved(seal.Id) ? "It stands open above an empty reliquary." : seal.Inscription, Palette.Dim, "     ");
        }
        f.Add();
        f.Add("  " + Ansi.Fg(Palette.Gold, "The chalk circle: ") + Ansi.Fg(Palette.Dim, san.CircleAvailable ? "glowing faintly, like a hearth after the fire's gone out." : "just chalk, as far as you can tell."));
        var cab = san.Cabinet.ToList();
        if (cab.Count > 0) f.Add("  " + Ansi.Fg(Palette.Gold, "In the cabinet: ") + Ansi.Fg(Palette.Text, string.Join(", ", cab.Select(S.World.Items.Name))));
    }

    public override IEnumerable<MenuItem> Menu()
    {
        var san = S.Sanctum;
        var atHand = san.AtHand.ToList();
        yield return new MenuItem("d", "The designation desk", Desk, san.Objects().Count > 0);
        yield return new MenuItem("g", "Examine something under the glass", Examine, atHand.Any(LibraryView.Objectish));
        yield return new MenuItem("a", "The assembly bench", () => A.Run(new AssemblyScreen()));
        yield return new MenuItem("r", "The Reconstitution Circle", Circle, san.CircleAvailable);
        yield return new MenuItem("l", "Lay something on the Balance", Balance, san.Seals().Any(s => !san.Solved(s.Id)) && atHand.Count > 0);
        yield return new MenuItem("k", "The cabinet", () => A.Run(new CabinetScreen()));
        foreach (var m in Common()) yield return m;
    }

    private void Desk()
    {
        var obj = A.Pick("Which thing?", S.Sanctum.Objects(), o => (S.Ledger.IsResolved(o.Id) ? "✓ " : "  ") + o.Name, o => o.Summary);
        if (obj is not null) A.Run(new DesignationScreen(obj.Id));
    }

    private void Examine()
    {
        var item = A.Pick("Examine what?", S.Sanctum.AtHand.Where(LibraryView.Objectish).ToList(), i => S.World.Items.Name(i), i => S.World.Items.DescribeLocation(i.Location));
        if (item is null) return;
        A.Say(S.Sanctum.Inspect(item.Id, "magnifying-glass"), "Under the glass");
    }

    private void Circle()
    {
        var rem = A.Pick("Which remnant?", S.Sanctum.Remnants(), i => S.World.Items.Name(i), i => S.World.Items.Description(i));
        if (rem is null) return;
        var p = S.Sanctum.PreviewReconstitution(rem.Id)!;
        var text = $"Set {p.RemnantName} in the circle and {p.RestoresName} will be made whole again.\n\n";
        text += p.CurrentResidence is null ? "Its essence isn't in anything right now." : $"Its essence is in {p.CurrentResidence}.";
        if (p.WillGoInert.Count > 0) text += "\n\n" + string.Join("\n", p.WillGoInert.Select(w => "• " + w));
        if (p.Retrace.Count > 0) text += $"\n\nTo have that back afterwards you would need to make it again: {string.Join(" → ", p.Retrace)}.";
        if (p.RemnantsCrumble.Count > 0) text += $"\n\nThese remnants would crumble: {string.Join(", ", p.RemnantsCrumble)}.";
        text += "\n\nAnything it only touched will stay as it is. What you've learned stays in your journal.";
        if (A.Confirm("The Reconstitution Circle", text, "Restore it", "Not yet")) A.Say(S.Sanctum.Reconstitute(rem.Id), "The Circle");
    }

    private void Balance()
    {
        var seal = S.Sanctum.Seals().FirstOrDefault(s => !S.Sanctum.Solved(s.Id));
        if (seal is null) return;
        var item = A.Pick($"Lay what on {seal.Name}?", S.Sanctum.AtHand.ToList(), i => S.World.Items.Name(i), intro: seal.Inscription);
        if (item is not null) A.Say(S.Sanctum.Offer(seal.Id, item.Id), PoolsView.Cap(seal.Name));
    }
}

/// <summary>The designation card: one earned term per facet, then commit. Wrong is allowed; it costs nothing.</summary>
public sealed class DesignationScreen : View
{
    private readonly string _obj;
    private readonly Dictionary<string, string> _choice = new();
    public DesignationScreen(string obj) => _obj = obj;

    public override string Title => "Designation: " + C.Objects[_obj].Name;
    public override string Locale => "sanctum";

    public override void Render(Frame f)
    {
        var card = S.Sanctum.Card(_obj);
        var resolved = S.Ledger.IsResolved(_obj);
        f.Text(card.Object.Summary, Palette.Dim);
        f.Add();
        if (resolved)
        {
            var j = new Atrium.Core.Journal(S).Objects().First(o => o.Object.Id == _obj);
            f.Add("  " + Ansi.Fg(Palette.Lamp, "★ " + j.Designation));
            f.Text("Settled. It's written in your journal in your best hand.", Palette.Dim);
            return;
        }
        var i = 1;
        foreach (var (facet, terms) in card.Facets)
        {
            var chosen = _choice.TryGetValue(facet.Id, out var t) ? C.TermLabel(t) : "—";
            f.Add($"  {Ansi.Fg(Palette.Dim, (i++).ToString())}  " + Ansi.PadRight(Ansi.Fg(Palette.Gold, facet.Label), 22) + Ansi.Fg(Palette.Cream, chosen)
                  + Ansi.Fg(Palette.Faint, $"   ({terms.Count} term{(terms.Count == 1 ? "" : "s")} known)"));
        }
        f.Add();
        f.Text(card.Ready ? "Your notes feel full enough to try." : "You'd want to know more before committing to anything. Try it in each house's own way.", card.Ready ? Palette.Moss : Palette.Faint);
        var misses = new Atrium.Core.Journal(S).Objects().First(o => o.Object.Id == _obj).NearMisses;
        if (misses.Count > 0)
        {
            f.Add();
            f.Add("  " + Ansi.Fg(Palette.Violet, "Already ruled out:"));
            foreach (var m in misses) f.Bullet("◇ ", m, Palette.Violet);
        }
    }

    public override IEnumerable<MenuItem> Menu()
    {
        if (S.Ledger.IsResolved(_obj)) { yield return new MenuItem("x", "Back", Close); yield break; }
        var card = S.Sanctum.Card(_obj);
        var i = 1;
        foreach (var (facet, terms) in card.Facets)
        {
            var fid = facet.Id;
            var list = terms;
            yield return new MenuItem((i++).ToString(), facet.Label, () => Choose(fid, list), list.Count > 0);
        }
        yield return new MenuItem("c", "Commit this designation", Commit, card.Facets.All(x => _choice.ContainsKey(x.Facet.Id)));
        yield return new MenuItem("n", "Read your notes on it", () => A.Run(new JournalObjectView(_obj)));
        yield return new MenuItem("x", "Back", Close);
    }

    private void Choose(string facet, IReadOnlyList<TermResource> terms)
    {
        var t = A.Pick(C.Facets[facet].Prompt, terms, t => t.Label, t => t.Gloss);
        if (t is not null) _choice[facet] = t.Id;
    }

    private void Commit()
    {
        var r = S.Sanctum.Designate(_obj, _choice);
        A.Say(r, C.Objects[_obj].Name);
    }
}

public sealed class AssemblyScreen : View
{
    public override string Title => "The assembly bench";
    public override string Locale => "sanctum";

    public override void Render(Frame f)
    {
        var bps = S.Sanctum.Blueprints();
        if (bps.Count == 0) { f.Text("You don't know how to make anything here yet. Ideas for tools turn up in books, and in what people tell you.", Palette.Dim); return; }
        f.Text("Components come from your satchel or the cabinet. Bottle elements at the Apothecary to bring them here.", Palette.Dim);
        f.Add();
        foreach (var b in bps)
        {
            var owned = S.World.Items.All.Any(i => i.Def == b.Tool.Id && !i.Inert);
            f.Add("  " + Ansi.BoldFg(Palette.Violet, b.Tool.Name) + (owned ? Ansi.Fg(Palette.Moss, "  (you have one)") : ""));
            f.Text(b.Tool.Description, Palette.Dim, "     ");
            foreach (var (slot, cands) in b.Slots)
                f.Add("     " + (cands.Count > 0 ? Ansi.Fg(Palette.Moss, "✓ ") : Ansi.Fg(Palette.Faint, "· ")) + Ansi.Fg(cands.Count > 0 ? Palette.Text : Palette.Faint, slot.Label.Length > 0 ? slot.Label : C.ItemName(slot.Item ?? slot.Trait!)));
            f.Add();
        }
    }

    public override IEnumerable<MenuItem> Menu()
    {
        yield return new MenuItem("a", "Assemble something", Assemble, S.Sanctum.Blueprints().Any(b => b.CanAssemble));
        yield return new MenuItem("x", "Back", Close);
    }

    private void Assemble()
    {
        var b = A.Pick("Assemble which?", S.Sanctum.Blueprints().Where(b => b.CanAssemble).ToList(), b => b.Tool.Name);
        if (b is null) return;
        var picks = new Dictionary<int, string>();
        for (int i = 0; i < b.Slots.Count; i++)
        {
            var (slot, cands) = b.Slots[i];
            if (cands.Count <= 1) continue;
            var c = A.Pick($"Use what for {slot.Label}?", cands, x => S.World.Items.Name(x));
            if (c is null) return;
            picks[i] = c.Id;
        }
        var carrier = b.Slots.SelectMany(s => s.Candidates).FirstOrDefault(c => c.Essence is not null && (picks.ContainsValue(c.Id) || b.Slots.Any(s => s.Candidates.Count == 1 && s.Candidates[0] == c)));
        if (carrier is not null && !A.Confirm("This will change it", $"{S.World.Items.Name(carrier)} will go into the {b.Tool.Name} and stay there. (If its original is ever made whole again at the Circle, the tool will go quiet.)", "Assemble", "Not yet"))
            return;
        A.Say(S.Sanctum.Assemble(b.Tool.Id, picks), b.Tool.Name);
    }
}

public sealed class CabinetScreen : View
{
    public override string Title => "The cabinet";
    public override string Locale => "sanctum";

    public override void Render(Frame f)
    {
        f.Text("A tall cedar cabinet with deep drawers. Anything kept here is at hand whenever you're in the Sanctum.", Palette.Dim);
        f.Add();
        var cab = S.Sanctum.Cabinet.ToList();
        if (cab.Count == 0) f.Add("  " + Ansi.Fg(Palette.Faint, "Empty."));
        foreach (var i in cab) f.Add("  " + Ansi.Fg(i.Inert ? Palette.Faint : Palette.Text, "· " + S.World.Items.Name(i)));
    }

    public override IEnumerable<MenuItem> Menu()
    {
        yield return new MenuItem("p", "Put something away", () =>
        {
            var i = A.Pick("Put what away?", S.World.Items.Satchel.ToList(), x => S.World.Items.Name(x));
            if (i is not null) A.Say(S.Sanctum.Store(i.Id));
        }, S.World.Items.Satchel.Any());
        yield return new MenuItem("t", "Take something out", () =>
        {
            var i = A.Pick("Take what?", S.Sanctum.Cabinet.ToList(), x => S.World.Items.Name(x));
            if (i is not null) A.Say(S.Sanctum.Retrieve(i.Id));
        }, S.Sanctum.Cabinet.Any());
        yield return new MenuItem("w", "Sweep away an inert husk", () =>
        {
            var i = A.Pick("Sweep away what?", S.Sanctum.AtHand.Where(x => x.Inert).ToList(), x => S.World.Items.Name(x), x => S.World.Items.Description(x));
            if (i is not null) A.Say(S.Sanctum.ClearHusk(i.Id));
        }, S.Sanctum.AtHand.Any(x => x.Inert));
        yield return new MenuItem("x", "Back", Close);
    }
}
