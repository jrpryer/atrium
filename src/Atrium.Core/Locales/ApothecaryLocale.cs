using Atrium.Core.Content;
using Atrium.Core.Essence;
using Atrium.Core.Mystery;
using Atrium.Core.Runtime;

namespace Atrium.Core.Locales;

public sealed record MapNode(string? Element, IReadOnlyList<string?> Inputs, string? Tool, bool NeedsTool, bool Discovered);

public sealed record DiscoveryMapView(IReadOnlyList<ItemResource> Known, IReadOnlyList<MapNode> Frontier, int Discovered, int Total);

public sealed record DerivationPreview(DerivationResource Derivation, string ProducesName, string? RemnantName);

/// <summary>
/// The Apothecary minigame: combine known elements (plus a tool) to discover new ones on a growing Discovery Map.
/// Elements, once known, are never used up. As a lens, clue-objects go on the bench with tools and elements;
/// sanctioned derivations change their form (leaving a remnant), everything else only reveals.
/// </summary>
public sealed class ApothecaryLocale : LocaleBase
{
    public override string Id => "apothecary";
    private readonly EssenceRegistry _essence;
    private ApothecaryConfig Cfg => W.Config.Apothecary;
    private ApothecaryState S => W.State.Apothecary;

    public ApothecaryLocale(World world, ILensService lens, EssenceRegistry essence) : base(world, lens)
    {
        _essence = essence;
        W.Bus.Subscribe<IngredientsDelivered>(e =>
        {
            foreach (var i in e.Ingredients.Where(i => S.Known.Add(i)))
            {
                S.DiscoveryOrder.Add(i);
                W.Notify(NoticeKind.Discovery, "New for the Apothecary", $"{C.ItemName(i)} has joined Tamsin's shelves.");
                W.Log.Add(LogKind.Discovery, $"{C.ItemName(i)} was added to the Apothecary's stock.", locale: Id);
            }
        });
    }

    public void InitializeNewGame()
    {
        foreach (var i in Cfg.StartingIngredients) if (S.Known.Add(i)) S.DiscoveryOrder.Add(i);
    }

    public IReadOnlyList<ItemResource> Known() => S.DiscoveryOrder.Select(C.Item).ToList();

    public IReadOnlyList<ToolResource> Tools() => W.Conditions.FixedToolsAt(Id).ToList();

    public int MaxInputs => Cfg.MaxInputs;

    // ───── the cozy loop ─────

    public ActionResult Mix(IReadOnlyList<string> inputs, string? tool)
    {
        if (inputs.Count == 0) return ActionResult.Soft("The bench is empty.");
        if (inputs.Count > Cfg.MaxInputs) return ActionResult.Soft($"The bench holds {Cfg.MaxInputs} things at once.");
        if (inputs.Any(i => !S.Known.Contains(i))) return ActionResult.Soft("You don't have that on the shelves.");
        if (tool is not null && !Tools().Any(t => t.Id == tool)) return ActionResult.Soft("That tool isn't on the bench.");

        var key = Key(inputs, tool);
        var recipe = C.Recipes.Values.FirstOrDefault(r => ContentValidatorKey(r) == key);
        var r = new ActionResult { Ok = recipe is not null };
        if (recipe is not null)
        {
            if (S.Known.Add(recipe.Output))
            {
                S.DiscoveryOrder.Add(recipe.Output);
                W.Count("apothecary.discovered");
                r.Add(LineKind.Narration, recipe.Text);
                r.Add(LineKind.Discovery, $"New discovery: {C.ItemName(recipe.Output)}. A new node blooms on the Discovery Map.");
                W.Log.Add(LogKind.Discovery, $"Discovered {C.ItemName(recipe.Output)} ({Describe(inputs, tool)}).", locale: Id);
                W.Bus.Publish(new DiscoveryMade(recipe.Output));
            }
            else r.Add(LineKind.Narration, $"You make a little more {C.ItemName(recipe.Output)}. You know this one by heart.");
            return Done(r);
        }

        if (!S.Tried.Add(key))
            return Done(ActionResult.Soft("You've tried this before. It settles into nothing much, just as it did then."));

        W.Count("apothecary.tried");
        r.Add(LineKind.Soft, "The mixture fizzes politely and settles into nothing much. Still, you learn a little from it.");
        S.Insight += Cfg.InsightPerTry;
        if (S.Insight >= Cfg.InsightToReveal && RevealHint() is { } hint)
        {
            S.Insight = 0;
            r.Add(LineKind.Discovery, hint);
        }
        return Done(r);
    }

    private static string Key(IEnumerable<string> inputs, string? tool) =>
        string.Join("+", inputs.OrderBy(x => x, StringComparer.Ordinal)) + "|" + (tool ?? "");

    private static string ContentValidatorKey(RecipeResource r) => Key(r.Inputs, r.Tool);

    private string Describe(IEnumerable<string> inputs, string? tool) =>
        string.Join(" + ", inputs.Select(C.ItemName)) + (tool is null ? "" : $", with the {C.ToolName(tool)}");

    /// <summary>Recipes whose every input is known and whose tool is to hand, but which are not yet discovered.</summary>
    private IEnumerable<RecipeResource> Frontier() =>
        C.Recipes.Values
            .Where(r => !S.Known.Contains(r.Output) && r.Inputs.All(S.Known.Contains) && (r.Tool is null || Tools().Any(t => t.Id == r.Tool)))
            .OrderBy(r => r.Id, StringComparer.Ordinal);

    private string? RevealHint()
    {
        foreach (var rec in Frontier().OrderBy(r => HintCount(r)))
        {
            var slots = rec.Inputs.Count + (rec.Tool is null ? 0 : 1);
            for (int i = 0; i < slots; i++)
            {
                if (!S.Hints.Add($"{rec.Id}:{i}")) continue;
                var what = i < rec.Inputs.Count ? C.ItemName(rec.Inputs[i]) : $"the {C.ToolName(rec.Tool!)}";
                return $"Something clicks: there's a discovery waiting that wants {what}. You mark it on the Discovery Map.";
            }
        }
        return null;
    }

    private int HintCount(RecipeResource r) => S.Hints.Count(h => h.StartsWith(r.Id + ":", StringComparison.Ordinal));

    public DiscoveryMapView Map()
    {
        var frontier = Frontier().Select(r =>
        {
            var inputs = r.Inputs.Select((x, i) => S.Hints.Contains($"{r.Id}:{i}") ? C.ItemName(x) : null).ToList();
            var tool = r.Tool is not null && S.Hints.Contains($"{r.Id}:{r.Inputs.Count}") ? C.ToolName(r.Tool) : null;
            return new MapNode(null, inputs, tool, r.Tool is not null, false);
        }).ToList();
        var total = C.Recipes.Values.Select(r => r.Output).Distinct().Count();
        var found = C.Recipes.Values.Select(r => r.Output).Distinct().Count(S.Known.Contains);
        return new DiscoveryMapView(Known(), frontier, found, total);
    }

    /// <summary>Recipes the player has made, for the map's "how did I make this?" view.</summary>
    public IReadOnlyList<RecipeResource> KnownRecipes() =>
        C.Recipes.Values.Where(r => S.Known.Contains(r.Output) && S.DiscoveryOrder.Contains(r.Output)).OrderBy(r => S.DiscoveryOrder.IndexOf(r.Output)).ToList();

    /// <summary>Takes a measure of a known element in a stoppered jar, for use in other places.</summary>
    public ActionResult Bottle(string element)
    {
        if (!S.Known.Contains(element)) return ActionResult.Soft("You don't have that on the shelves.");
        var def = C.Item(element);
        if (!def.Portable) return ActionResult.Soft($"{def.Name} won't keep in a jar. It only exists here, on the bench.");
        W.Items.Create(element, Locations.Satchel);
        return Done(ActionResult.Say($"You stopper a measure of {def.Name} and tuck it into your satchel. There's always more on the shelf."));
    }

    public IReadOnlyList<FormularyEntry> Formulary() =>
        C.Derivations.Values.Where(d => d.Formulary is not null && W.Conditions.Holds(d.Formulary.AvailableWhen))
            .Select(d => d.Formulary!).ToList();

    // ───── the mystery loop: objects on the bench ─────

    /// <summary>If this combination would change the object's form, describes it so the player can confirm first.</summary>
    public DerivationPreview? PreviewExperiment(string instanceId, IReadOnlyList<string> with, string? tool)
    {
        var item = Carried(instanceId);
        if (item is null) return null;
        var d = _essence.FindDerivation(Id, item, tool, with);
        return d is null ? null : new DerivationPreview(d, C.ItemName(d.Produces), d.Remnant is null ? null : C.ItemName(d.Remnant));
    }

    /// <summary>
    /// Works an object on the bench with a tool and/or elements. If a sanctioned derivation matches it runs
    /// (callers should confirm via <see cref="PreviewExperiment"/> first); otherwise the object only reveals.
    /// </summary>
    public ActionResult Experiment(string instanceId, IReadOnlyList<string> with, string? tool)
    {
        var item = Carried(instanceId);
        if (item is null) return ActionResult.Soft("You don't have that with you.");
        if (with.Count > Cfg.MaxInputs - 1) return ActionResult.Soft($"The bench holds {Cfg.MaxInputs} things at once.");
        if (with.Any(i => !S.Known.Contains(i))) return ActionResult.Soft("You don't have that on the shelves.");
        if (tool is not null && !Tools().Any(t => t.Id == tool)) return ActionResult.Soft("That tool isn't on the bench.");

        var r = new ActionResult();
        var d = _essence.FindDerivation(Id, item, tool, with);
        if (d is not null)
        {
            var (produced, remnant) = _essence.Derive(item, d);
            r.Add(LineKind.Narration, d.Text);
            r.Add(LineKind.System, remnant is null
                ? $"You now carry {C.ItemName(produced.Def)}."
                : $"You now carry {C.ItemName(produced.Def)}, and {C.ItemName(remnant.Def)} is left behind (also in your satchel).");
            r.AddRange(Lens.Observe(new LensEvent { Locale = Id, Verb = "derive", Item = produced, Derivation = d.Id }));
            return Done(r);
        }

        Observe(r, new LensEvent { Locale = Id, Verb = "work", Item = item, Tool = tool, With = with.ToList() });
        return Done(r);
    }
}
