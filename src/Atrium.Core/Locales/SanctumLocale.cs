using Atrium.Core.Content;
using Atrium.Core.Essence;
using Atrium.Core.Mystery;
using Atrium.Core.Runtime;
using Atrium.Core.Story;

namespace Atrium.Core.Locales;

public sealed record DesignationCard(MysteryObjectResource Object, IReadOnlyList<(FacetResource Facet, IReadOnlyList<TermResource> Terms)> Facets, bool Ready);

public sealed record BlueprintView(ToolResource Tool, IReadOnlyList<(ComponentSlot Slot, IReadOnlyList<ItemInstance> Candidates)> Slots, bool CanAssemble);

/// <summary>
/// The Sanctum: the linchpin. The magnifying glass and seals are lenses; the designation desk is where synthesis
/// happens; the assembly bench turns discoveries into tools for other locales; the Reconstitution Circle restores
/// lost forms from their remnants. Its storage cabinet extends the satchel.
/// </summary>
public sealed class SanctumLocale : LocaleBase
{
    public override string Id => "sanctum";
    private readonly EssenceRegistry _essence;
    private readonly SynthesisResolver _resolver;
    private readonly ComprehensionGate _gate;
    private readonly Lexicon _lexicon;
    private readonly SanctumRouter _router;

    public SanctumLocale(World world, ILensService lens, EssenceRegistry essence, SynthesisResolver resolver,
                         ComprehensionGate gate, Lexicon lexicon, SanctumRouter router) : base(world, lens)
    {
        _essence = essence; _resolver = resolver; _gate = gate; _lexicon = lexicon; _router = router;
    }

    public IReadOnlyList<ToolResource> Tools() => W.Conditions.FixedToolsAt(Id).ToList();

    public IEnumerable<ItemInstance> Cabinet => W.Items.At(Locations.SanctumCabinet);

    /// <summary>Everything reachable from the Sanctum bench: the satchel and the cabinet.</summary>
    public IEnumerable<ItemInstance> AtHand => W.Items.Satchel.Concat(Cabinet);

    // ───── lenses ─────

    public ActionResult Inspect(string instanceId, string toolId)
    {
        var item = Carried(instanceId);
        if (item is null) return ActionResult.Soft("You don't have that with you.");
        if (Tools().All(t => t.Id != toolId)) return ActionResult.Soft("That tool isn't here.");
        var r = new ActionResult();
        Observe(r, new LensEvent { Locale = Id, Verb = "inspect", Item = item, Tool = toolId });
        return Done(r);
    }

    public IReadOnlyList<SealResource> Seals() =>
        C.Seals.Values.Where(s => s.Locale == Id && W.Conditions.Holds(s.AvailableWhen)).ToList();

    public bool Solved(string sealId) => W.State.Sanctum.SolvedSeals.Contains(sealId);

    /// <summary>Lays an object on a seal. Offering never consumes it; the seal's response is evidence either way.</summary>
    public ActionResult Offer(string sealId, string instanceId)
    {
        var seal = Seals().FirstOrDefault(s => s.Id == sealId);
        if (seal is null) return ActionResult.Soft("There's nothing like that here.");
        var item = Carried(instanceId);
        if (item is null) return ActionResult.Soft("You don't have that with you.");
        var r = new ActionResult();
        var readings = Lens.Observe(new LensEvent { Locale = Id, Verb = "seal", Item = item, Seal = sealId });
        r.AddRange(readings);
        if (Solved(sealId)) { r.Add(LineKind.Narration, $"{seal.Name} already stands open."); return Done(r); }

        var fits = !item.Inert &&
                   (seal.RequiresItem is null || item.Def == seal.RequiresItem) &&
                   seal.RequiresTraits.All(t => W.Items.HasTrait(item, t));
        if (!fits)
        {
            if (readings.Count == 0) r.Add(LineKind.Soft, seal.RefuseText.Length > 0 ? seal.RefuseText : Fallback(item, "seal"));
            return Done(r);
        }

        W.State.Sanctum.SolvedSeals.Add(sealId);
        W.Count("sanctum.seals");
        r.Add(LineKind.Discovery, seal.SolvedText);
        if (item.Essence is not null) _essence.RecordCatalysis(item.Essence, $"Opened {seal.Name}.");
        W.Log.Add(LogKind.Story, seal.SolvedText, item.Essence, Id);
        W.Bus.Publish(new SealSolved(sealId));
        _router.Apply(seal.Payload);
        return Done(r);
    }

    // ───── synthesis ─────

    /// <summary>Objects that have arrived, open ones first.</summary>
    public IReadOnlyList<MysteryObjectResource> Objects() =>
        C.Objects.Values.Where(o => W.State.ArrivedObjects.Contains(o.Id))
            .OrderBy(o => W.State.Evidence.Resolved.Contains(o.Id) ? 1 : 0)
            .ThenBy(o => W.State.Log.FindIndex(l => l.Kind == LogKind.Arrival && l.Object == o.Id))
            .ToList();

    public DesignationCard Card(string objectId)
    {
        var obj = C.Objects[objectId];
        var facets = C.FacetsFor(obj).Select(f => (f, _lexicon.TermsFor(obj, f.Id))).ToList();
        return new DesignationCard(obj, facets, _gate.AnyAttemptable(obj));
    }

    public ActionResult Designate(string objectId, IReadOnlyDictionary<string, string> designation)
    {
        if (!W.State.ArrivedObjects.Contains(objectId)) return ActionResult.Soft("You haven't come across that.");
        var obj = C.Objects[objectId];
        foreach (var (facet, term) in designation)
            if (!W.State.Evidence.EarnedTerms.Contains(term)) return ActionResult.Soft("You haven't earned that term yet.");
        var outcome = _resolver.Attempt(objectId, designation);
        var r = new ActionResult();
        switch (outcome.Kind)
        {
            case SynthesisKind.Resolved:
                if (obj.Resolution.Title.Length > 0) r.Add(LineKind.Resolved, obj.Resolution.Title);
                r.Add(LineKind.Resolved, outcome.Text);
                break;
            case SynthesisKind.NearMiss:
                r.Add(LineKind.NearMiss, outcome.Text);
                r.Add(LineKind.System, "You write the discrepancy in your journal.");
                break;
            default:
                r.Add(LineKind.Soft, outcome.Text);
                break;
        }
        return Done(r);
    }

    public SynthesisKind? LastOutcome(string objectId) =>
        Enum.TryParse<SynthesisKind>(W.State.Evidence.Attempts.LastOrDefault(a => a.Object == objectId)?.Outcome, out var k) ? k : null;

    // ───── assembly ─────

    public IReadOnlyList<BlueprintView> Blueprints() =>
        C.Tools.Values.Where(t => t.Kind == ToolKind.Assembled && W.Conditions.Holds(t.AvailableWhen))
            .Select(t =>
            {
                var slots = t.Components.Select(c => (c, (IReadOnlyList<ItemInstance>)Candidates(c).ToList())).ToList();
                return new BlueprintView(t, slots, slots.All(s => s.Item2.Count > 0) && DistinctFill(slots));
            }).ToList();

    private static bool DistinctFill(List<(ComponentSlot, IReadOnlyList<ItemInstance>)> slots)
    {
        // Greedy check that each slot can take a different instance (or a stack with enough units).
        var used = new Dictionary<string, int>();
        foreach (var (_, cands) in slots)
        {
            var pick = cands.FirstOrDefault(i => used.GetValueOrDefault(i.Id) < i.Quantity);
            if (pick is null) return false;
            used[pick.Id] = used.GetValueOrDefault(pick.Id) + 1;
        }
        return true;
    }

    private IEnumerable<ItemInstance> Candidates(ComponentSlot slot) =>
        AtHand.Where(i => !i.Inert && (slot.Item is not null ? i.Def == slot.Item : W.Items.HasTrait(i, slot.Trait!)));

    /// <summary>Builds a tool. <paramref name="picks"/> maps component index → item instance id (defaults to the first candidate).</summary>
    public ActionResult Assemble(string toolId, IReadOnlyDictionary<int, string>? picks = null)
    {
        var bp = Blueprints().FirstOrDefault(b => b.Tool.Id == toolId);
        if (bp is null) return ActionResult.Soft("You don't know how to make that yet.");
        var chosen = new List<ItemInstance>();
        var used = new Dictionary<string, int>();
        for (int i = 0; i < bp.Slots.Count; i++)
        {
            var (slot, cands) = bp.Slots[i];
            ItemInstance? pick = picks is not null && picks.TryGetValue(i, out var id)
                ? cands.FirstOrDefault(c => c.Id == id)
                : cands.FirstOrDefault(c => used.GetValueOrDefault(c.Id) < c.Quantity);
            if (pick is null || used.GetValueOrDefault(pick.Id) >= pick.Quantity)
                return ActionResult.Soft($"You're missing {(slot.Label.Length > 0 ? slot.Label : slot.Item is not null ? C.ItemName(slot.Item) : "a component")}.");
            used[pick.Id] = used.GetValueOrDefault(pick.Id) + 1;
            chosen.Add(pick);
        }

        var carriers = chosen.Where(c => c.Essence is not null).Distinct().ToList();
        if (carriers.Count > 1) return ActionResult.Soft("Two essences won't share one tool. Choose components with only one between them.");

        var tool = W.Items.Create(toolId, Locations.Satchel);
        foreach (var c in chosen.Where(c => c.Essence is null)) W.Items.Consume(c);
        if (carriers.Count == 1) _essence.Contain(carriers[0], tool);

        W.Count("sanctum.assembled");
        W.Count("sanctum.assembled:" + toolId);
        W.Log.Add(LogKind.Tool, $"Assembled {bp.Tool.Name}.", tool.Essence, Id);
        W.Bus.Publish(new ToolAssembled(toolId, tool.Id));
        var r = ActionResult.Say(bp.Tool.AssembleText.Length > 0 ? bp.Tool.AssembleText : $"You assemble {bp.Tool.Name}.");
        foreach (var g in bp.Tool.Grants)
            r.Add(LineKind.Discovery, $"New capability — {(C.Capabilities.TryGetValue(g, out var cap) ? cap.Label : g)}.");
        r.Add(LineKind.System, $"{bp.Tool.Name} is in your satchel.");
        return Done(r);
    }

    // ───── the Reconstitution Circle ─────

    /// <summary>The Circle is whichever tool grants the engine's reconstitution capability.</summary>
    public const string ReconstituteCapability = "sanctum.reconstitute";

    public bool CircleAvailable => W.Conditions.HasCapability(ReconstituteCapability);

    public IReadOnlyList<ItemInstance> Remnants() => AtHand.Where(i => i.RemnantOf is not null).ToList();

    public ReconstitutionPreview? PreviewReconstitution(string remnantId)
    {
        var rem = AtHand.FirstOrDefault(i => i.Id == remnantId && i.RemnantOf is not null);
        return rem is null || !CircleAvailable ? null : _essence.Preview(rem);
    }

    public ActionResult Reconstitute(string remnantId)
    {
        if (!CircleAvailable) return ActionResult.Soft("The chalk circle is only chalk.");
        var rem = AtHand.FirstOrDefault(i => i.Id == remnantId && i.RemnantOf is not null);
        if (rem is null) return ActionResult.Soft("You don't have that remnant with you.");
        var preview = _essence.Preview(rem);
        var restored = _essence.Reconstitute(rem);
        var r = ActionResult.Say($"You set {preview.RemnantName} in the centre of the circle. The chalk brightens, the air goes still, and when you breathe again {C.ItemName(restored.Def)} lies there, whole.");
        foreach (var line in preview.WillGoInert) r.Add(LineKind.System, line);
        r.Add(LineKind.Narration, "Whatever it merely touched stays touched.");
        return Done(r);
    }

    // ───── storage ─────

    public ActionResult Store(string instanceId)
    {
        var i = W.Items.Get(instanceId);
        if (i is null || i.Location != Locations.Satchel) return ActionResult.Soft("That isn't in your satchel.");
        W.Items.Move(i, Locations.SanctumCabinet);
        return Done(ActionResult.Say($"You put {W.Items.Name(i)} away in the cabinet."));
    }

    public ActionResult Retrieve(string instanceId)
    {
        var i = W.Items.Get(instanceId);
        if (i is null || i.Location != Locations.SanctumCabinet) return ActionResult.Soft("That isn't in the cabinet.");
        W.Items.Move(i, Locations.Satchel);
        return Done(ActionResult.Say($"You take {W.Items.Name(i)} from the cabinet."));
    }

    /// <summary>Sweeps away an inert husk (it holds nothing; matter only).</summary>
    public ActionResult ClearHusk(string instanceId)
    {
        var i = AtHand.FirstOrDefault(x => x.Id == instanceId);
        if (i is null || !i.Inert) return ActionResult.Soft("You'd rather keep that.");
        W.State.Items.Remove(i);
        return Done(ActionResult.Say($"You sweep {W.Items.Name(i)} away. It was only matter now."));
    }
}
