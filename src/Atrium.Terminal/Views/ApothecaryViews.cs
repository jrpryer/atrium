using Atrium.Core.Content;
using Atrium.Core.Runtime;
using Atrium.Terminal.Ui;

namespace Atrium.Terminal.Views;

public sealed class ApothecaryView : LocaleView
{
    public override string Title => "The Apothecary";
    public override string Locale => "apothecary";

    private readonly List<string> _bench = new();
    private string? _tool;
    private string? _object;

    public override void Render(Frame f)
    {
        var apo = S.Apothecary;
        var known = apo.Known();
        var map = apo.Map();
        f.Add("  " + Ansi.Fg(Palette.Gold, "On Tamsin's shelves ") + Ansi.Fg(Palette.Dim, $"({known.Count} elements · {map.Discovered} of {map.Total} discoveries)"));
        var names = known.Select(k => k.Name).ToList();
        var line = "   ";
        foreach (var n in names)
        {
            if (Ansi.Width(line) + n.Length + 3 > A.Width - 4) { f.Add(Ansi.Fg(Palette.Copper, line)); line = "   "; }
            line += n + " · ";
        }
        if (line.Trim().Length > 0) f.Add(Ansi.Fg(Palette.Copper, line.TrimEnd(' ', '·')));
        f.Add("  " + Ansi.Fg(Palette.Gold, "Tools: ") + Ansi.Fg(Palette.Text, string.Join(", ", apo.Tools().Select(t => t.Name))));
        f.Add();

        f.Add("  " + Ansi.BoldFg(Palette.Copper, "The bench"));
        var obj = _object is null ? null : S.World.Items.Get(_object);
        if (obj is null || obj.Location != Locations.Satchel) _object = null;
        if (_object is not null) f.Add("     " + Ansi.Fg(Palette.Lamp, "object: " + S.World.Items.Name(obj!)));
        f.Add("     " + Ansi.Fg(Palette.Text, _bench.Count == 0 ? (_object is null ? "(empty)" : "(nothing alongside it)") : string.Join("  +  ", _bench.Select(C.ItemName))));
        f.Add("     " + Ansi.Fg(Palette.Dim, "tool: " + (_tool is null ? "none" : C.ToolName(_tool))));
        if (map.Frontier.Count > 0)
            f.Add("  " + Ansi.Fg(Palette.Moss, $"The Discovery Map shows {map.Frontier.Count} discover{(map.Frontier.Count == 1 ? "y" : "ies")} within reach of what you know."));
    }

    public override IEnumerable<MenuItem> Menu()
    {
        var max = S.Apothecary.MaxInputs - (_object is null ? 0 : 1);
        yield return new MenuItem("a", "Add an element to the bench", AddElement, _bench.Count < max);
        yield return new MenuItem("t", "Choose a tool", ChooseTool);
        yield return new MenuItem("o", _object is null ? "Put an object on the bench" : "Take the object off the bench", ToggleObject,
            _object is not null || S.World.Items.Satchel.Any(LibraryView.Objectish));
        yield return new MenuItem("m", _object is null ? "Mix" : "Work the object", Work, _bench.Count > 0 || _object is not null);
        yield return new MenuItem("c", "Clear the bench", () => { _bench.Clear(); _tool = null; _object = null; }, _bench.Count > 0 || _tool is not null || _object is not null);
        yield return new MenuItem("d", "Discovery Map", () => A.Run(new DiscoveryMapView()));
        yield return new MenuItem("f", "Old Formulary", () => A.Run(new FormularyView()));
        yield return new MenuItem("b", "Bottle an element to carry", Bottle);
        foreach (var m in Common()) yield return m;
    }

    private void AddElement()
    {
        var el = A.Pick("Add which element?", S.Apothecary.Known(), k => k.Name, k => k.Description);
        if (el is not null) _bench.Add(el.Id);
    }

    private void ChooseTool()
    {
        var tools = new List<ToolResource> { new() { Id = "", Name = "No tool" } }.Concat(S.Apothecary.Tools()).ToList();
        var t = A.Pick("Which tool?", tools, t => t.Id.Length == 0 ? "No tool, just hands and time" : $"{t.Name} ({t.Verb})", t => t.Description);
        if (t is not null) _tool = t.Id.Length == 0 ? null : t.Id;
    }

    private void ToggleObject()
    {
        if (_object is not null) { _object = null; return; }
        var item = A.Pick("Put which object on the bench?", S.World.Items.Satchel.Where(LibraryView.Objectish).ToList(), i => S.World.Items.Name(i), i => S.World.Items.Description(i));
        if (item is null) return;
        _object = item.Id;
        while (_bench.Count > S.Apothecary.MaxInputs - 1) _bench.RemoveAt(_bench.Count - 1);
    }

    private void Work()
    {
        if (_object is null)
        {
            A.Say(S.Apothecary.Mix(_bench, _tool));
            _bench.Clear();
            return;
        }
        var preview = S.Apothecary.PreviewExperiment(_object, _bench, _tool);
        if (preview is not null)
        {
            var text = preview.Derivation.Confirm.Length > 0 ? preview.Derivation.Confirm
                : $"This will change it into {preview.ProducesName}" + (preview.RemnantName is null ? "." : $", leaving {preview.RemnantName} behind.");
            if (!A.Confirm("This will change it", text, "Go ahead", "Not yet")) return;
        }
        var r = S.Apothecary.Experiment(_object, _bench, _tool);
        A.Say(r, "At the bench");
        if (preview is not null) { _object = null; _bench.Clear(); }
    }

    private void Bottle()
    {
        var el = A.Pick("Bottle which element?", S.Apothecary.Known().Where(k => k.Portable).ToList(), k => k.Name,
            k => k.PoolEnergy is { } e && !e.IsZero ? $"In a pool: light {Wrap.Signed(e.Light)}, heat {Wrap.Signed(e.Heat)}" : k.FoodValue > 0 ? "Pond creatures will eat this." : null);
        if (el is not null) A.Say(S.Apothecary.Bottle(el.Id));
    }
}

public sealed class DiscoveryMapView : View
{
    public override string Title => "The Discovery Map";
    public override string Locale => "apothecary";

    public override void Render(Frame f)
    {
        var map = S.Apothecary.Map();
        f.Text($"Discovered {map.Discovered} of {map.Total}. Each new thing you find blooms on the map; every failed mix teaches you a little, and enough of those reveal part of something still hidden.", Palette.Dim);
        f.Add();
        if (map.Frontier.Count > 0)
        {
            f.Add("  " + Ansi.Fg(Palette.Moss, "Within reach of what you know:"));
            foreach (var n in map.Frontier)
            {
                var parts = n.Inputs.Select(i => i ?? "?").ToList();
                var tool = n.Tool is not null ? $", with the {n.Tool}" : n.NeedsTool ? ", and a tool" : "";
                f.Add("     " + Ansi.Fg(Palette.Moss, "❀ ??? ") + Ansi.Fg(Palette.Dim, "= " + string.Join(" + ", parts) + tool));
            }
            f.Add();
        }
        f.Add("  " + Ansi.Fg(Palette.Gold, "How you made what you know:"));
        foreach (var r in S.Apothecary.KnownRecipes())
            f.Add("     " + Ansi.Fg(Palette.Copper, C.ItemName(r.Output)) + Ansi.Fg(Palette.Dim, " = " + string.Join(" + ", r.Inputs.Select(C.ItemName)) + (r.Tool is null ? "" : $" ({C.ToolName(r.Tool)})")));
    }

    public override IEnumerable<MenuItem> Menu()
    {
        yield return new MenuItem("x", "Back", Close);
    }
}

public sealed class FormularyView : View
{
    public override string Title => "The Old Formulary";
    public override string Locale => "apothecary";

    public override void Render(Frame f)
    {
        f.Text("Corran Pell's book: half recipes, half riddles, cracked along the spine.", Palette.Dim);
        f.Add();
        foreach (var e in S.Apothecary.Formulary())
        {
            f.Add("  " + Ansi.BoldFg(Palette.Copper, e.Title));
            f.Text(e.Text, Palette.Cream, "    ");
            f.Add();
        }
    }

    public override IEnumerable<MenuItem> Menu()
    {
        yield return new MenuItem("x", "Back", Close);
    }
}
