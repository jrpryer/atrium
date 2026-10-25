using Atrium.Core.Content;
using Atrium.Core.Runtime;

namespace Atrium.Core.Essence;

/// <summary>What the Reconstitution Circle will do, shown to the player before they commit.</summary>
public sealed class ReconstitutionPreview
{
    public required string Essence { get; init; }
    public required string ObjectName { get; init; }
    public required string RemnantName { get; init; }
    public required string RestoresName { get; init; }
    /// <summary>Where the essence lives now ("Gilded Lamp-Oil, in your satchel"); null if it is in no live form.</summary>
    public string? CurrentResidence { get; init; }
    /// <summary>Every form that materially carries the essence and will go inert, with consequences.</summary>
    public List<string> WillGoInert { get; } = new();
    /// <summary>The forms the player would need to retrace to make the current carrier again.</summary>
    public List<string> Retrace { get; } = new();
    public List<string> RemnantsCrumble { get; } = new();
}

/// <summary>
/// Invariant 2 — one essence, one place. Tracks the single live form of every clue-object essence, moves it along
/// containment edges (derivation, transmutation, assembly), records catalysis, and executes reconstitution:
/// restore the original from its remnant, then nullify only the containment chain.
/// </summary>
public sealed class EssenceRegistry
{
    private readonly World _w;
    public EssenceRegistry(World w) => _w = w;

    public EssenceRecord? Record(string essence) => _w.State.Essences.TryGetValue(essence, out var r) ? r : null;

    public void Register(string essence, ItemInstance carrier)
    {
        carrier.Essence = essence;
        _w.State.Essences[essence] = new EssenceRecord { Essence = essence, CarrierId = carrier.Id, History = { carrier.Def } };
        AssertInvariant();
    }

    // ───── derivations ─────

    /// <summary>Finds the sanctioned derivation (if any) for this form, locale, tool and co-inputs.</summary>
    public DerivationResource? FindDerivation(string locale, ItemInstance item, string? tool, IReadOnlyCollection<string> with)
    {
        if (item.Essence is null || item.Inert) return null;
        return _w.Content.Derivations.Values.FirstOrDefault(d =>
            d.Locale == locale &&
            d.Tool == tool &&
            Mystery.LensService.SameMultiset(d.With, with) &&
            (d.Input == item.Def || (d.InputTrait is not null && _w.Items.HasTrait(item, d.InputTrait))));
    }

    /// <summary>Moves the essence into the derived form, leaving the remnant behind (both land in the satchel).</summary>
    public (ItemInstance Produced, ItemInstance? Remnant) Derive(ItemInstance input, DerivationResource d)
    {
        var essence = input.Essence ?? throw new InvariantViolationException("Derivation input carries no essence.");
        var rec = Record(essence)!;
        const string location = Locations.Satchel;
        var fromForm = input.Def;

        _w.Items.Delete(input);
        var produced = _w.Items.Create(d.Produces, location, 1, essence);
        rec.CarrierId = produced.Id;
        rec.History.Add(d.Produces);

        ItemInstance? remnant = null;
        if (d.Remnant is not null)
        {
            remnant = _w.Items.Create(d.Remnant, location);
            remnant.RemnantOf = essence;
            remnant.Restores = fromForm;
            remnant.RemnantStep = rec.History.Count - 2; // index of the form it restores
            rec.RemnantIds.Add(remnant.Id);
            _w.Count("essence.remnants-created");
        }
        _w.Count("essence.derivations");
        _w.Log.Add(LogKind.Essence, $"{_w.Content.ItemName(fromForm)} became {_w.Content.ItemName(d.Produces)}" +
                                    (d.Remnant is null ? "." : $", leaving {_w.Content.ItemName(d.Remnant)}."), essence, d.Locale);
        _w.Bus.Publish(new EssenceMoved(essence, fromForm, d.Produces, "containment"));
        AssertInvariant();
        return (produced, remnant);
    }

    /// <summary>The same essence takes a new form in place (e.g. a pool's blessing). No remnant.</summary>
    public void Transmute(ItemInstance carrier, string newForm)
    {
        var essence = carrier.Essence ?? throw new InvariantViolationException("Only essence carriers transmute.");
        var from = carrier.Def;
        carrier.Def = newForm;
        Record(essence)!.History.Add(newForm);
        _w.Bus.Publish(new EssenceMoved(essence, from, newForm, "containment"));
        AssertInvariant();
    }

    /// <summary>A component's essence moves into an assembled tool; the component is used up in the making.</summary>
    public void Contain(ItemInstance component, ItemInstance tool)
    {
        var essence = component.Essence ?? throw new InvariantViolationException("Component carries no essence.");
        var rec = Record(essence)!;
        _w.Items.Delete(component);
        tool.Essence = essence;
        rec.CarrierId = tool.Id;
        rec.History.Add(tool.Def);
        _w.Bus.Publish(new EssenceMoved(essence, component.Def, tool.Def, "containment"));
        AssertInvariant();
    }

    /// <summary>Records an outcome the essence enabled without inhabiting. Catalysed outcomes are never nullified.</summary>
    public void RecordCatalysis(string essence, string outcome)
    {
        var rec = Record(essence);
        if (rec is null || rec.Catalyzed.Contains(outcome)) return;
        rec.Catalyzed.Add(outcome);
        _w.Log.Add(LogKind.Essence, outcome, essence);
    }

    // ───── reconstitution ─────

    public ReconstitutionPreview Preview(ItemInstance remnant)
    {
        var essence = remnant.RemnantOf ?? throw new InvalidOperationException("Only remnants can be reconstituted.");
        var rec = Record(essence)!;
        var obj = _w.Content.Objects[essence];
        var carrier = _w.Items.Carrier(essence);

        var p = new ReconstitutionPreview
        {
            Essence = essence,
            ObjectName = obj.Name,
            RemnantName = _w.Items.Name(remnant),
            RestoresName = _w.Content.ItemName(remnant.Restores!),
            CurrentResidence = carrier is null ? null : $"{_w.Items.Name(carrier)}, {_w.Items.DescribeLocation(carrier.Location)}",
        };
        if (carrier is not null)
        {
            var line = $"{_w.Items.Name(carrier)} will go inert";
            var consequences = Consequences(carrier).ToList();
            p.WillGoInert.Add(consequences.Count == 0 ? line + "." : line + ": " + string.Join("; ", consequences) + ".");
        }
        // The path from the restored form to the current carrier, to be retraced if the player wants it back.
        for (int i = remnant.RemnantStep + 1; i < rec.History.Count; i++) p.Retrace.Add(_w.Content.ItemName(rec.History[i]));
        foreach (var other in rec.RemnantIds.Select(_w.Items.Get).Where(r => r is not null && r.Id != remnant.Id && r.RemnantStep > remnant.RemnantStep))
            p.RemnantsCrumble.Add(_w.Items.Name(other!));
        return p;
    }

    private IEnumerable<string> Consequences(ItemInstance carrier)
    {
        if (_w.Content.Tools.TryGetValue(carrier.Def, out var tool))
            foreach (var g in tool.Grants)
                yield return "you will lose " + (_w.Content.Capabilities.TryGetValue(g, out var c) ? c.Label : g);
        if (Locations.IsPool(carrier.Location, out _))
        {
            var e = _w.Items.Energy(carrier);
            if (!e.IsZero) yield return $"its pull on the pool's energy ({e}) will stop";
            var soaking = _w.State.Pools.Pools.SelectMany(pp => pp.Progress.Where(kv => kv.Key.EndsWith(":" + carrier.Id) && kv.Value > 0)).Any();
            if (soaking) yield return "the soak it is part of will halt";
        }
    }

    /// <summary>Restores the original form from its remnant; nullifies whatever currently carries the essence.</summary>
    public ItemInstance Reconstitute(ItemInstance remnant)
    {
        var preview = Preview(remnant);
        var essence = preview.Essence;
        var rec = Record(essence)!;

        var carrier = _w.Items.Carrier(essence);
        if (carrier is not null)
        {
            carrier.Inert = true;
            carrier.FormerEssence = essence;
            carrier.Essence = null;
            foreach (var pool in _w.State.Pools.Pools)
                foreach (var key in pool.Progress.Keys.Where(k => k.EndsWith(":" + carrier.Id)).ToList()) pool.Progress.Remove(key);
            _w.Bus.Publish(new EssenceNullified(essence, carrier.Id));
        }

        // Remnants made after the restored form no longer describe anything; they crumble.
        foreach (var later in rec.RemnantIds.Select(_w.Items.Get).Where(r => r is not null && r.RemnantStep > remnant.RemnantStep).ToList())
            _w.Items.Delete(later!);

        var restored = _w.Items.Create(remnant.Restores!, Locations.Satchel, 1, essence);
        rec.History.RemoveRange(remnant.RemnantStep + 1, rec.History.Count - remnant.RemnantStep - 1);
        rec.RemnantIds.RemoveAll(id => _w.Items.Get(id) is null || id == remnant.Id || _w.Items.Get(id)!.RemnantStep > remnant.RemnantStep);
        rec.CarrierId = restored.Id;
        _w.Items.Delete(remnant);

        _w.Count("essence.reconstitutions");
        _w.Log.Add(LogKind.Essence, $"The Circle restored {preview.RestoresName} from {preview.RemnantName}." +
                                    (carrier is null ? "" : $" {_w.Items.Name(carrier)} went quiet."), essence, "sanctum");
        _w.Bus.Publish(new Reconstituted(essence, restored.Def));
        AssertInvariant();
        return restored;
    }

    // ───── invariant ─────

    /// <summary>Every essence in the world lives in exactly one usable form (runtime assertion, invariant 2).</summary>
    public void AssertInvariant()
    {
        foreach (var rec in _w.State.Essences.Values)
        {
            if (_w.Content.Objects.TryGetValue(rec.Essence, out var obj) && obj.ExemptFromOneInstance) continue;
            var live = _w.State.Items.Count(i => i.Essence == rec.Essence && !i.Inert);
            if (live != 1)
                throw new InvariantViolationException($"Essence '{rec.Essence}' has {live} live forms; it must have exactly one.");
            var carrier = _w.Items.Carrier(rec.Essence)!;
            if (carrier.Id != rec.CarrierId)
                throw new InvariantViolationException($"Essence '{rec.Essence}' registry points at {rec.CarrierId} but lives in {carrier.Id}.");
            if (_w.State.Items.Any(i => i.Inert && i.Essence is not null))
                throw new InvariantViolationException("An inert husk still claims an essence.");
        }
    }
}
