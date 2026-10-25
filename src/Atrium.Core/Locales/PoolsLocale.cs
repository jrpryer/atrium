using Atrium.Core.Content;
using Atrium.Core.Essence;
using Atrium.Core.Mystery;
using Atrium.Core.Runtime;

namespace Atrium.Core.Locales;

public sealed record SoakView(string Item, string Transform, double Progress, bool ConditionsMet, int EffectiveTicks, string Text, IReadOnlyList<string> Wants);

public sealed record PoolView(PoolResource Pool, PoolState State, Energy Energy, IReadOnlyList<ItemInstance> Items, IReadOnlyList<SoakView> Soaks);

/// <summary>
/// The Spirit Pools minigame, driven entirely by the world-clock: pools sit on the Light/Dark × Hot/Cold plane,
/// lifeforms survive or thrive in bands, must be fed and kept in clear water, and breed by data-driven
/// genetics. Neglect never kills: creatures sulk until things are put right. As a lens, objects laid in a pool
/// pull on its energy and are slowly transformed when a pool's conditions are met.
/// </summary>
public sealed class PoolsLocale : LocaleBase
{
    public override string Id => "pools";
    private readonly EssenceRegistry _essence;
    private PoolsConfig Cfg => W.Config.Pools;
    private PoolsState S => W.State.Pools;
    private AxisResource LightAxis => C.Axis("light");
    private AxisResource HeatAxis => C.Axis("heat");

    public PoolsLocale(World world, ILensService lens, EssenceRegistry essence) : base(world, lens)
    {
        _essence = essence;
        W.Clock.OnTick(OnTick);
        W.Bus.Subscribe<LocaleUnlocked>(e => { if (e.Locale == Id) Stock(); });
    }

    public void InitializeNewGame()
    {
        S.Pools.Clear();
        foreach (var p in C.Pools.Values) S.Pools.Add(new PoolState { Id = p.Id, Clarity = p.StartingClarity });
        if (Unlocked) Stock();
    }

    private void Stock()
    {
        if (S.Stocked) return;
        S.Stocked = true;
        foreach (var p in C.Pools.Values)
            foreach (var entry in p.StartingStock)
                for (int i = 0; i < entry.Count; i++)
                    State(p.Id).Lifeforms.Add(NewLifeform(entry.Lifeform, ParseSex(entry.Sex, entry.Lifeform)));
        foreach (var pool in S.Pools) UpdateMoods(pool);
    }

    // ───── queries ─────

    public PoolState State(string poolId) => S.Pools.First(p => p.Id == poolId);

    public IReadOnlyList<PoolView> Pools() => C.Pools.Values.Select(p => View(p.Id)).ToList();

    public PoolView View(string poolId)
    {
        var def = C.Pools[poolId];
        var st = State(poolId);
        var items = W.Items.At(Locations.Pool(poolId)).ToList();
        var soaks = new List<SoakView>();
        foreach (var it in items.Where(i => !i.Inert))
            foreach (var t in C.PoolTransforms.Values.Where(t => t.Target == it.Def))
            {
                var key = $"{t.Id}:{it.Id}";
                var progress = st.Progress.GetValueOrDefault(key);
                var met = RequirementMet(t.Requires, def, st, it);
                soaks.Add(new SoakView(W.Items.Name(it), t.Name, progress, met, EffectiveTicks(t, st), t.ProgressText,
                    met ? Array.Empty<string>() : Unmet(t.Requires, def, st, it)));
            }
        return new PoolView(def, st, Energy(poolId), items, soaks);
    }

    public IReadOnlyList<LifeformInstance> Holding => S.Holding;

    public IEnumerable<ItemInstance> DruidsBag => W.Items.At(Locations.DruidsBag);

    /// <summary>A pool's current position on the two axes: base + objects + lifeform emissions, clamped.</summary>
    public Energy Energy(string poolId)
    {
        var def = C.Pools[poolId];
        var e = def.BaseEnergy;
        foreach (var it in W.Items.At(Locations.Pool(poolId)))
            for (int q = 0; q < Math.Max(1, it.Quantity); q++) e += W.Items.Energy(it);
        foreach (var lf in State(poolId).Lifeforms) e += C.Lifeforms[lf.Species].Emits;
        return new Energy(Math.Clamp(e.Light, LightAxis.Min, LightAxis.Max), Math.Clamp(e.Heat, HeatAxis.Min, HeatAxis.Max));
    }

    /// <summary>Breeding rules the player has seen work, for the Stillkeepers' breeding ledger.</summary>
    public IReadOnlyList<BreedingResource> KnownRules() => C.Breeding.Values.Where(b => S.RulesSeen.Contains(b.Id)).ToList();

    public IReadOnlyList<string> SpeciesSeen() => S.SpeciesSeen.OrderBy(x => x, StringComparer.Ordinal).ToList();

    public LifeformInstance? FindLifeform(int id, out string? poolId)
    {
        foreach (var p in S.Pools)
        {
            var lf = p.Lifeforms.FirstOrDefault(l => l.Id == id);
            if (lf is not null) { poolId = p.Id; return lf; }
        }
        poolId = null;
        return S.Holding.FirstOrDefault(l => l.Id == id);
    }

    // ───── husbandry ─────

    public ActionResult Cleanse(string poolId)
    {
        if (!Unlocked) return ActionResult.Soft("The pools are shuttered.");
        var st = State(poolId);
        var def = C.Pools[poolId];
        var before = st.Clarity;
        st.Clarity = Math.Min(100, st.Clarity + Cfg.CleanseAmount);
        st.Cleanses++;
        W.Count("pools.cleansed");
        W.Count("pools.cleansed:" + poolId);
        var r = ActionResult.Say(before >= 95
            ? $"You draw the rake through {def.Name} anyway. It was already clear as a held breath."
            : $"You rake the silt from {def.Name} and skim the green from its edges. The water clears (clarity {before:0} → {st.Clarity:0}).");
        foreach (var find in def.SiltFinds)
        {
            if (st.FindsGiven.Contains(find.Item) || st.Cleanses < find.MinCleanses) continue;
            if (find.Capability is not null && !W.Conditions.HasCapability(find.Capability)) continue;
            st.FindsGiven.Add(find.Item);
            var dest = C.Item(find.Item).Kind is ItemKind.Mineral or ItemKind.Aura ? MineralDestination : Locations.Satchel;
            W.Items.Create(find.Item, dest);
            r.Add(LineKind.Discovery, find.Text.Length > 0 ? find.Text : $"Something glints in the silt: {C.ItemName(find.Item)}.");
            r.Add(LineKind.System, $"{C.ItemName(find.Item)} — {W.Items.DescribeLocation(dest)}.");
            W.Log.Add(LogKind.Pools, $"Found {C.ItemName(find.Item)} while cleansing {def.Name}.", locale: Id);
        }
        UpdateMoods(st);
        return Done(r);
    }

    private string MineralDestination => W.Config.MineralDestination == "satchel" ? Locations.Satchel : Locations.DruidsBag;

    /// <summary>Feeds a pool, from the Stillkeepers' crock (always there) or with a food from the satchel.</summary>
    public ActionResult Feed(string poolId, string? foodInstanceId = null)
    {
        if (!Unlocked) return ActionResult.Soft("The pools are shuttered.");
        var st = State(poolId);
        var value = Cfg.BasicFoodValue;
        var name = Cfg.BasicFoodName;
        if (foodInstanceId is not null)
        {
            var food = Carried(foodInstanceId) ?? DruidsBag.FirstOrDefault(i => i.Id == foodInstanceId);
            if (food is null) return ActionResult.Soft("You don't have that with you.");
            var def = W.Items.Def(food);
            if (def.FoodValue <= 0 || food.Inert) return ActionResult.Soft($"Nothing in the pool wants to eat {def.Name}.");
            if (food.Essence is not null) return ActionResult.Soft("That isn't food. It's far too interesting to eat.");
            value = def.FoodValue;
            name = def.Name;
            W.Items.Consume(food);
        }
        var eaters = st.Lifeforms.Where(l => C.Lifeforms[l.Species].HungerPerTick > 0).ToList();
        if (eaters.Count == 0) return Done(ActionResult.Say($"You scatter {name}, but nothing here eats it. It drifts down to the silt."));
        foreach (var l in eaters) l.Hunger = Math.Max(0, l.Hunger - value);
        UpdateMoods(st);
        W.Count("pools.fed");
        return Done(ActionResult.Say($"You scatter {name}. {eaters.Count} mouth{(eaters.Count == 1 ? "" : "s")} rise{(eaters.Count == 1 ? "s" : "")} to meet it."));
    }

    /// <summary>Lays an item in a pool (from the satchel or the druids' bag). Objects pull on the pool's energy.</summary>
    public ActionResult Place(string instanceId, string poolId)
    {
        if (!Unlocked) return ActionResult.Soft("The pools are shuttered.");
        var item = Carried(instanceId) ?? DruidsBag.FirstOrDefault(i => i.Id == instanceId);
        if (item is null) return ActionResult.Soft("You don't have that with you.");
        var def = C.Pools[poolId];
        var before = Energy(poolId);
        var moved = W.Items.Move(item, Locations.Pool(poolId), 1);
        var after = Energy(poolId);
        var r = ActionResult.Say($"You lower {W.Items.Name(moved)} into {def.Name}.");
        if (!before.Equals(after))
            r.Add(LineKind.System, $"The pool shifts: {Shift(before, after)}.");
        Observe(r, new LensEvent { Locale = Id, Verb = "place", Item = moved, Pool = poolId, Energy = after });
        UpdateMoods(State(poolId));
        return Done(r);
    }

    private string Shift(Energy a, Energy b)
    {
        var parts = new List<string>();
        if (a.Light != b.Light) parts.Add(b.Light > a.Light ? $"brighter ({a.Light:+0;-0;0} → {b.Light:+0;-0;0})" : $"darker ({a.Light:+0;-0;0} → {b.Light:+0;-0;0})");
        if (a.Heat != b.Heat) parts.Add(b.Heat > a.Heat ? $"warmer ({a.Heat:+0;-0;0} → {b.Heat:+0;-0;0})" : $"cooler ({a.Heat:+0;-0;0} → {b.Heat:+0;-0;0})");
        return string.Join(", ", parts);
    }

    public ActionResult TakeOut(string instanceId)
    {
        var item = W.Items.Get(instanceId);
        if (item is null || !Locations.IsPool(item.Location, out var poolId)) return ActionResult.Soft("It isn't in the pools.");
        var before = Energy(poolId);
        W.Items.Move(item, Locations.Satchel);
        var r = ActionResult.Say($"You lift {W.Items.Name(item)} out, dripping, and wrap it in a cloth.");
        var after = Energy(poolId);
        if (!before.Equals(after)) r.Add(LineKind.System, $"The pool shifts: {Shift(before, after)}.");
        UpdateMoods(State(poolId));
        return Done(r);
    }

    /// <summary>Moves a lifeform to another pool, or to the holding crock (<paramref name="toPool"/> null).</summary>
    public ActionResult MoveLifeform(int lifeformId, string? toPool)
    {
        var lf = FindLifeform(lifeformId, out var from);
        if (lf is null) return ActionResult.Soft("You can't find that one.");
        if (toPool == from) return ActionResult.Soft("It's already there.");
        if (toPool is not null)
        {
            var dest = State(toPool);
            if (dest.Lifeforms.Count >= C.Pools[toPool].Capacity) return ActionResult.Soft($"{C.Pools[toPool].Name} is full; there's no room for another.");
        }
        if (from is null) S.Holding.Remove(lf); else State(from).Lifeforms.Remove(lf);
        if (toPool is null) S.Holding.Add(lf); else State(toPool).Lifeforms.Add(lf);
        var name = $"{lf.Name} the {C.LifeformName(lf.Species)}";
        if (from is not null) UpdateMoods(State(from));
        if (toPool is not null) UpdateMoods(State(toPool));
        return Done(ActionResult.Say(toPool is null
            ? $"You net {name} and ease it into {Cfg.HoldingName}, where it can rest."
            : $"You net {name} and tip it gently into {C.Pools[toPool].Name}."));
    }

    // ───── the world-clock ─────

    private void OnTick(long tick)
    {
        if (!S.Stocked) return;
        foreach (var pool in S.Pools) TickPool(pool, tick);
    }

    private void TickPool(PoolState st, long tick)
    {
        var def = C.Pools[st.Id];

        foreach (var lf in st.Lifeforms)
        {
            var sp = C.Lifeforms[lf.Species];
            lf.Hunger = Math.Min(100, lf.Hunger + sp.HungerPerTick);
            st.Clarity -= lf.Mood == Mood.Sulking ? sp.WastePerTick * 0.5 : sp.WastePerTick;
            if (lf.Cooldown > 0) lf.Cooldown--;
        }
        st.Clarity = Math.Clamp(st.Clarity, 0, 100);
        UpdateMoods(st);

        // Births first (a full pool waits), then new pairings.
        foreach (var mother in st.Lifeforms.Where(l => l.Expecting is not null).ToList())
        {
            if (mother.Gestation > 0) mother.Gestation--;
            if (mother.Gestation == 0 && st.Lifeforms.Count < def.Capacity) Birth(st, def, mother, tick);
        }
        Pairings(st, def);
        Transforms(st, def);
        Spawns(st, def);
    }

    private void UpdateMoods(PoolState st)
    {
        var e = Energy(st.Id);
        foreach (var lf in st.Lifeforms)
        {
            var sp = C.Lifeforms[lf.Species];
            var eats = sp.HungerPerTick > 0;
            if (!sp.Survive.Contains(e)) { lf.Mood = Mood.Sulking; lf.MoodReason = BandReason(sp.Survive, e); }
            else if (eats && lf.Hunger > Cfg.ContentHungerMax) { lf.Mood = Mood.Sulking; lf.MoodReason = "hungry"; }
            else if (st.Clarity < Cfg.ContentClarityMin) { lf.Mood = Mood.Sulking; lf.MoodReason = "the water is murky"; }
            else if (sp.Thrive.Contains(e) && (!eats || lf.Hunger <= Cfg.ThriveHungerMax) && st.Clarity >= Cfg.ThriveClarityMin)
            { lf.Mood = Mood.Thriving; lf.MoodReason = ""; }
            else
            {
                lf.Mood = Mood.Content;
                lf.MoodReason = !sp.Thrive.Contains(e) ? "not quite its favourite water: " + BandReason(sp.Thrive, e)
                    : eats && lf.Hunger > Cfg.ThriveHungerMax ? "a little peckish"
                    : "the water could be clearer";
            }
        }
    }

    private static string BandReason(Band band, Energy e)
    {
        var parts = new List<string>();
        if (e.Light < band.Light.Min) parts.Add("too dark");
        if (e.Light > band.Light.Max) parts.Add("too bright");
        if (e.Heat < band.Heat.Min) parts.Add("too cold");
        if (e.Heat > band.Heat.Max) parts.Add("too warm");
        return parts.Count == 0 ? "comfortable" : string.Join(" and ", parts);
    }

    private void Pairings(PoolState st, PoolResource def)
    {
        var ready = st.Lifeforms.Where(l => l.Mood == Mood.Thriving && l.Expecting is null && l.Cooldown == 0).ToList();
        foreach (var mother in ready)
        {
            if (mother.Expecting is not null || mother.Cooldown > 0) continue;
            var msp = C.Lifeforms[mother.Species];
            if (msp.Sexed && mother.Sex != Sex.Female) continue;
            foreach (var partner in ready)
            {
                if (partner == mother || partner.Cooldown > 0 || partner.Expecting is not null) continue;
                var rule = FindRule(mother, partner, st, def, out var offspring);
                if (offspring is null) continue;
                mother.Expecting = offspring;
                mother.ExpectingRule = rule?.Id;
                mother.Gestation = msp.GestationTicks;
                mother.Cooldown = msp.CooldownTicks;
                partner.Cooldown = C.Lifeforms[partner.Species].CooldownTicks;
                break;
            }
        }
    }

    /// <summary>Explicit rules (environment-conditioned first) then like-breeds-like. Returns the offspring pool marker.</summary>
    private BreedingResource? FindRule(LifeformInstance mother, LifeformInstance partner, PoolState st, PoolResource def, out string? offspring)
    {
        offspring = null;
        var msp = C.Lifeforms[mother.Species];
        var psp = C.Lifeforms[partner.Species];
        var sexesOk = msp.Sexed ? psp.Sexed && partner.Sex == Sex.Male : !psp.Sexed;
        if (!sexesOk) return null;

        foreach (var b in C.Breeding.Values.OrderBy(b => b.Requires is null ? 1 : 0).ThenBy(b => b.Id, StringComparer.Ordinal))
        {
            bool match = b.Ordered
                ? mother.Species == b.ParentA && partner.Species == b.ParentB
                : (mother.Species == b.ParentA && partner.Species == b.ParentB) || (mother.Species == b.ParentB && partner.Species == b.ParentA);
            if (!match) continue;
            if (b.Requires is not null && !RequirementMet(b.Requires, def, st, null)) continue;
            offspring = "rule:" + b.Id;
            return b;
        }
        if (mother.Species == partner.Species && msp.Breeds)
        {
            offspring = "species:" + mother.Species;
            return null;
        }
        return null;
    }

    private void Birth(PoolState st, PoolResource def, LifeformInstance mother, long tick)
    {
        var marker = mother.Expecting!;
        mother.Expecting = null;
        string species;
        BreedingResource? rule = null;
        if (marker.StartsWith("rule:", StringComparison.Ordinal) && C.Breeding.TryGetValue(marker[5..], out rule))
        {
            var unseen = rule.Offspring.FirstOrDefault(o => !S.SpeciesSeen.Contains(o.Lifeform));
            if (rule.GuaranteeNewFirst && unseen is not null) species = unseen.Lifeform;
            else
            {
                var total = rule.Offspring.Sum(o => o.Weight);
                var roll = W.Rng.Next(total);
                species = rule.Offspring.First(o => (roll -= o.Weight) < 0).Lifeform;
            }
            S.RulesSeen.Add(rule.Id);
        }
        else species = marker.StartsWith("species:", StringComparison.Ordinal) ? marker[8..] : mother.Species;

        var sp = C.Lifeforms[species];
        var first = !S.SpeciesSeen.Contains(species);
        var child = NewLifeform(species, sp.Sexed ? (W.Rng.Next(2) == 0 ? Sex.Female : Sex.Male) : Sex.None);
        child.BornTick = tick;
        child.Cooldown = sp.CooldownTicks;
        st.Lifeforms.Add(child);
        W.Count("pools.born");
        W.Count("pools.born:" + species);

        var text = first && rule is not null && rule.Text.Length > 0
            ? rule.Text
            : $"{mother.Name}'s brood has hatched in {def.Name}: a little {sp.Name}, named {child.Name}.";
        W.Log.Add(LogKind.Pools, text, locale: Id);
        W.Notify(NoticeKind.Birth, first ? $"A new kind: {sp.Name}" : "A hatching", text);
        W.Bus.Publish(new LifeformBorn(st.Id, species, first));

        // Catalysis: an essence whose pull on the water made an environment-conditioned birth possible.
        if (rule?.Requires is not null)
            foreach (var it in W.Items.At(Locations.Pool(st.Id)).Where(i => i.Essence is not null && !W.Items.Energy(i).IsZero).ToList())
                _essence.RecordCatalysis(it.Essence!, $"Helped a {sp.Name} hatch in {def.Name}.");
    }

    private bool RequirementMet(PoolRequirement r, PoolResource def, PoolState st, ItemInstance? exclude)
    {
        var e = Energy(def.Id);
        if (!r.Light.Contains(e.Light) || !r.Heat.Contains(e.Heat)) return false;
        if (st.Clarity < r.ClarityMin) return false;
        var items = W.Items.At(Locations.Pool(def.Id)).Where(i => i != exclude && !i.Inert).ToList();
        if (r.ReagentTraits.Any(t => !items.Any(i => W.Items.HasTrait(i, t)))) return false;
        if (r.Reagents.Any(d => !items.Any(i => i.Def == d))) return false;
        foreach (var need in r.Lifeforms)
            if (st.Lifeforms.Count(l => l.Species == need.Lifeform && l.Mood != Mood.Sulking) < need.Min) return false;
        return true;
    }

    /// <summary>What a paused soak is waiting for, in the water's own terms (generated from the requirement data).</summary>
    private IReadOnlyList<string> Unmet(PoolRequirement r, PoolResource def, PoolState st, ItemInstance exclude)
    {
        var e = Energy(def.Id);
        var wants = new List<string>();
        if (e.Light < r.Light.Min) wants.Add("brighter water");
        if (e.Light > r.Light.Max) wants.Add("darker water");
        if (e.Heat < r.Heat.Min) wants.Add("warmer water");
        if (e.Heat > r.Heat.Max) wants.Add("cooler water");
        if (st.Clarity < r.ClarityMin) wants.Add("clearer water");
        var items = W.Items.At(Locations.Pool(def.Id)).Where(i => i != exclude && !i.Inert).ToList();
        foreach (var t in r.ReagentTraits.Where(t => !items.Any(i => W.Items.HasTrait(i, t))))
            wants.Add("something of " + (C.Traits.TryGetValue(t, out var tr) && tr.Label.Length > 0 ? tr.Label : t) + " beside it");
        foreach (var d in r.Reagents.Where(d => !items.Any(i => i.Def == d))) wants.Add(C.ItemName(d) + " beside it");
        foreach (var need in r.Lifeforms)
        {
            var have = st.Lifeforms.Count(l => l.Species == need.Lifeform && l.Mood != Mood.Sulking);
            if (have < need.Min) wants.Add($"the company of {need.Min} {C.LifeformName(need.Lifeform)}{(need.Min == 1 ? "" : "s")} (not sulking)");
        }
        return wants;
    }

    private int EffectiveTicks(PoolTransformResource t, PoolState st)
    {
        var ticks = t.Ticks;
        foreach (var m in t.Modifiers)
            if (st.Lifeforms.Count(l => l.Species == m.Lifeform && l.Mood != Mood.Sulking) >= m.Min) ticks += m.DeltaTicks;
        return Math.Max(1, ticks);
    }

    private void Transforms(PoolState st, PoolResource def)
    {
        foreach (var item in W.Items.At(Locations.Pool(def.Id)).Where(i => !i.Inert).ToList())
        {
            foreach (var t in C.PoolTransforms.Values.Where(t => t.Target == item.Def).ToList())
            {
                if (!RequirementMet(t.Requires, def, st, item)) continue; // progress pauses; it never resets
                var key = $"{t.Id}:{item.Id}";
                var progress = st.Progress.GetValueOrDefault(key) + 1.0 / EffectiveTicks(t, st);
                if (progress < 1 - 1e-9) { st.Progress[key] = progress; continue; }

                st.Progress.Remove(key);
                CompleteTransform(t, item, def, st);
                break;
            }
        }
    }

    private void CompleteTransform(PoolTransformResource t, ItemInstance item, PoolResource def, PoolState st)
    {
        // Catalysts present: essences in the water that met a reagent requirement.
        var catalysts = W.Items.At(Locations.Pool(def.Id))
            .Where(i => i != item && i.Essence is not null && !i.Inert &&
                        (t.Requires.ReagentTraits.Any(tr => W.Items.HasTrait(i, tr)) || t.Requires.Reagents.Contains(i.Def)))
            .ToList();

        if (t.TransformInto is not null)
        {
            if (item.Essence is not null) _essence.Transmute(item, t.TransformInto);
            else item.Def = t.TransformInto;
        }
        foreach (var c in catalysts) _essence.RecordCatalysis(c.Essence!, $"Lent itself to {t.Name} in {def.Name}.");

        W.Count("pools.transformed");
        W.Count("pools.transformed:" + t.Id);
        var lines = Lens.Observe(new LensEvent { Locale = Id, Verb = "transform", Item = item, Pool = def.Id, Transform = t.Id, Energy = Energy(def.Id) });
        var text = string.Join("\n\n", new[] { t.Text }.Concat(lines.Where(l => l.Kind is LineKind.NewReading or LineKind.Term).Select(l => l.Text)).Where(s => s.Length > 0));
        W.Log.Add(LogKind.Pools, t.Text.Length > 0 ? t.Text : $"{t.Name} finished in {def.Name}.", item.Essence, Id);
        W.Notify(NoticeKind.Discovery, t.Name, text);
    }

    private void Spawns(PoolState st, PoolResource def)
    {
        if (!def.SpawningPond) return;
        foreach (var item in W.Items.At(Locations.Pool(def.Id)).Where(i => !i.Inert).ToList())
        {
            foreach (var rule in C.SpawnRules.Values.Where(s => s.Mineral == item.Def))
            {
                var timer = st.SpawnTimers.GetValueOrDefault(rule.Id) + 1;
                if (timer < rule.EveryTicks) { st.SpawnTimers[rule.Id] = timer; continue; }
                if (st.Lifeforms.Count(l => l.Species == rule.Spawns) >= rule.MaxPresent || st.Lifeforms.Count >= def.Capacity)
                { st.SpawnTimers[rule.Id] = rule.EveryTicks; continue; }
                st.SpawnTimers[rule.Id] = 0;
                var sp = C.Lifeforms[rule.Spawns];
                var first = !S.SpeciesSeen.Contains(rule.Spawns);
                var lf = NewLifeform(rule.Spawns, Sex.None);
                st.Lifeforms.Add(lf);
                W.Count("pools.spawned:" + rule.Spawns);
                var text = first && rule.Text.Length > 0 ? rule.Text : $"A {sp.Name} has gathered itself out of the water of {def.Name}.";
                W.Log.Add(LogKind.Pools, text, locale: Id);
                W.Notify(NoticeKind.Birth, first ? $"A new kind: {sp.Name}" : "A spawning", text);
                W.Bus.Publish(new LifeformBorn(st.Id, rule.Spawns, first));
            }
        }
    }

    // ───── helpers ─────

    private LifeformInstance NewLifeform(string species, Sex sex)
    {
        S.SpeciesSeen.Add(species);
        var names = Cfg.Names;
        var name = names.Count == 0 ? $"No. {S.NextLifeformId}" : names[W.Rng.Next(names.Count)];
        return new LifeformInstance { Id = S.NextLifeformId++, Species = species, Sex = sex, Name = name, Hunger = 20, BornTick = W.Clock.Tick };
    }

    private Sex ParseSex(string? s, string species) => s switch
    {
        "female" => Sex.Female,
        "male" => Sex.Male,
        _ => C.Lifeforms[species].Sexed ? (W.Rng.Next(2) == 0 ? Sex.Female : Sex.Male) : Sex.None,
    };
}
