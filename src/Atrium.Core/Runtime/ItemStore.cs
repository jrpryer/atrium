using Atrium.Core.Content;

namespace Atrium.Core.Runtime;

/// <summary>Every item instance in the world and where it is. Shared by all locales (the satchel lives here).</summary>
public sealed class ItemStore
{
    private readonly World _w;
    public ItemStore(World w) => _w = w;

    public IEnumerable<ItemInstance> All => _w.State.Items;

    public ItemInstance? Get(string id) => _w.State.Items.FirstOrDefault(i => i.Id == id);

    public ItemInstance Require(string id) =>
        Get(id) ?? throw new InvalidOperationException($"No item instance '{id}'.");

    public IEnumerable<ItemInstance> At(string location) => _w.State.Items.Where(i => i.Location == location);

    public ItemResource Def(ItemInstance i) => _w.Content.Item(i.Def);

    /// <summary>Creates an instance. Stackable (non-unique, non-essence) items merge into an existing stack.</summary>
    public ItemInstance Create(string defId, string location, int quantity = 1, string? essence = null)
    {
        var def = _w.Content.Item(defId);
        if (!def.IsUnique && essence is null)
        {
            var stack = _w.State.Items.FirstOrDefault(i => i.Def == defId && i.Location == location && i.Essence is null && !i.Inert);
            if (stack is not null) { stack.Quantity += quantity; return stack; }
        }
        var inst = new ItemInstance
        {
            Id = "it" + _w.State.NextItemId++,
            Def = defId,
            Location = location,
            Quantity = def.IsUnique ? 1 : quantity,
            Essence = essence,
        };
        _w.State.Items.Add(inst);
        return inst;
    }

    /// <summary>Removes one unit (or the whole stack).</summary>
    public void Consume(ItemInstance inst, int quantity = 1)
    {
        if (inst.Essence is not null)
            throw new InvariantViolationException($"Essence-carrying item '{inst.Def}' cannot be consumed directly; use a derivation.");
        inst.Quantity -= quantity;
        if (inst.Quantity <= 0) _w.State.Items.Remove(inst);
    }

    /// <summary>Removes an instance outright (used by the essence registry when a form changes).</summary>
    internal void Delete(ItemInstance inst) => _w.State.Items.Remove(inst);

    /// <summary>Moves an instance (or part of a stack) to a new location. Returns the moved instance.</summary>
    public ItemInstance Move(ItemInstance inst, string location, int quantity = int.MaxValue)
    {
        if (inst.Quantity <= quantity || Def(inst).IsUnique || inst.Essence is not null)
        {
            var def = Def(inst);
            if (!def.IsUnique && inst.Essence is null && !inst.Inert)
            {
                var stack = _w.State.Items.FirstOrDefault(i => i != inst && i.Def == inst.Def && i.Location == location && i.Essence is null && !i.Inert);
                if (stack is not null) { stack.Quantity += inst.Quantity; _w.State.Items.Remove(inst); return stack; }
            }
            inst.Location = location;
            return inst;
        }
        inst.Quantity -= quantity;
        return Create(inst.Def, location, quantity);
    }

    public string Name(ItemInstance i)
    {
        var def = Def(i);
        var name = i.Inert ? def.InertName ?? $"{def.Name} (inert)" : def.Name;
        return i.Quantity > 1 ? $"{name} ×{i.Quantity}" : name;
    }

    public string Description(ItemInstance i)
    {
        var def = Def(i);
        return i.Inert ? def.InertDescription ?? "Whatever was in it has gone quiet. It is only matter now." : def.Description;
    }

    /// <summary>Traits an instance currently has. Inert husks have none.</summary>
    public IReadOnlyList<string> Traits(ItemInstance i) => i.Inert ? Array.Empty<string>() : Def(i).Traits;

    public bool HasTrait(ItemInstance i, string trait) => Traits(i).Contains(trait);

    /// <summary>What an instance contributes to a pool. Inert husks contribute nothing.</summary>
    public Energy Energy(ItemInstance i) => i.Inert ? default : Def(i).PoolEnergy ?? default;

    /// <summary>The single live form carrying an essence, if the essence is in the world.</summary>
    public ItemInstance? Carrier(string essence) => _w.State.Items.FirstOrDefault(i => i.Essence == essence && !i.Inert);

    /// <summary>Player-carried items plus those stored at the Sanctum (both reachable from the Sanctum).</summary>
    public IEnumerable<ItemInstance> Satchel => At(Locations.Satchel);

    public string DescribeLocation(string location)
    {
        if (location == Locations.Satchel) return "in your satchel";
        if (location == Locations.LibraryPile) return "on the Library's pile";
        if (location == Locations.SanctumCabinet) return "in the Sanctum cabinet";
        if (location == Locations.DruidsBag) return "in the druids' bag";
        if (Locations.IsPool(location, out var p)) return $"in {(_w.Content.Pools.TryGetValue(p, out var pr) ? pr.Name : p)}";
        return location;
    }
}

/// <summary>Thrown when a design invariant (e.g. one essence, one place) is violated. Always a bug, never player error.</summary>
public sealed class InvariantViolationException : Exception
{
    public InvariantViolationException(string message) : base(message) { }
}
