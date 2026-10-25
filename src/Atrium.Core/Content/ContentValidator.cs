namespace Atrium.Core.Content;

public sealed class ValidationReport
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool Ok => Errors.Count == 0;
}

/// <summary>
/// Load-time validation of designer content: reference integrity against the shared taxonomy, the ≤3
/// plausible-wrong cap (invariant 7), recipe-tree acyclicity, derivation-graph sanity (no cycles, no orphan
/// remnants), and lifeform band sanity.
/// </summary>
public static class ContentValidator
{
    /// <summary>Invariant 7. Not a tunable: raising it needs designer sign-off and a code change.</summary>
    public const int MaxPlausibleWrongClaims = 3;

    public static readonly IReadOnlyDictionary<string, string[]> VerbsByLocale = new Dictionary<string, string[]>
    {
        ["library"] = new[] { "shelve", "compare" },
        ["apothecary"] = new[] { "work", "derive" },
        ["pools"] = new[] { "place", "transform" },
        ["sanctum"] = new[] { "inspect", "seal" },
    };

    public static ValidationReport Validate(ContentDatabase db)
    {
        var v = new Validator(db);
        v.Run();
        return v.Report;
    }

    private sealed class Validator
    {
        private readonly ContentDatabase _db;
        public ValidationReport Report { get; } = new();
        private void Err(string m) => Report.Errors.Add(m);
        private void Warn(string m) => Report.Warnings.Add(m);

        public Validator(ContentDatabase db) => _db = db;

        public void Run()
        {
            Taxonomy();
            Config();
            Items();
            Tomes();
            Recipes();
            Tools();
            Derivations();
            Pools();
            Lifeforms();
            Seals();
            Objects();
            Story();
        }

        // ───── helpers ─────

        private void Ref<T>(IReadOnlyDictionary<string, T> dict, string? id, string what, string where)
        {
            if (id is null) return;
            if (!dict.ContainsKey(id)) Err($"{where}: unknown {what} '{id}'.");
        }

        private void Trait(string id, string where) => Ref(_db.Traits, id, "trait", where);

        private void Cond(Condition? c, string where)
        {
            if (c is null) return;
            foreach (var n in c.Walk())
            {
                Ref(_db.Beats, n.Beat, "beat", where);
                Ref(_db.Objects, n.Resolved, "mystery object", where);
                Ref(_db.Objects, n.Arrived, "mystery object", where);
                Ref(_db.Readings, n.Reading, "reading", where);
                Ref(_db.Locales, n.Locale, "locale", where);
                Ref(_db.Tools, n.Tool, "tool", where);
                Ref(_db.Items, n.Discovered, "item", where);
                Ref(_db.Seals, n.Seal, "seal", where);
                Ref(_db.Lifeforms, n.Lifeform, "lifeform", where);
                if (n.Capability is not null && !_db.Capabilities.ContainsKey(n.Capability))
                    Warn($"{where}: capability '{n.Capability}' is not described in the taxonomy.");
            }
        }

        private void PayloadRefs(Payload p, string where)
        {
            foreach (var t in p.Tomes) Ref(_db.Tomes, t, "tome", where);
            foreach (var pg in p.Pages)
            {
                var (tome, num) = SplitPage(pg);
                if (tome is null || !_db.Tomes.TryGetValue(tome, out var tr)) { Err($"{where}: page '{pg}' must be 'tome-id#n' of a known tome."); continue; }
                var page = tr.Pages.FirstOrDefault(x => x.Number == num);
                if (page is null) Err($"{where}: tome '{tome}' has no page {num}.");
                else if (!page.Loose) Warn($"{where}: page '{pg}' is delivered but not marked 'loose', so it was already readable.");
            }
            foreach (var i in p.Ingredients) Ref(_db.Items, i, "item", where);
            foreach (var m in p.Minerals)
            {
                Ref(_db.Items, m, "item", where);
                if (_db.Items.TryGetValue(m, out var it) && it.Kind is not (ItemKind.Mineral or ItemKind.Aura))
                    Warn($"{where}: '{m}' is delivered as a mineral/aura but its kind is {it.Kind}.");
            }
            foreach (var g in p.Items) { Ref(_db.Items, g.Item, "item", where); Location(g.Location, where); }
        }

        private void Location(string loc, string where)
        {
            if (loc is Locations.Satchel or Locations.LibraryPile or Locations.SanctumCabinet or Locations.DruidsBag) return;
            if (Locations.IsPool(loc, out var pool)) { Ref(_db.Pools, pool, "pool", where); return; }
            Err($"{where}: unknown location '{loc}'.");
        }

        internal static (string? Tome, int Page) SplitPage(string s)
        {
            var hash = s.IndexOf('#');
            if (hash <= 0 || !int.TryParse(s[(hash + 1)..], out var n)) return (null, 0);
            return (s[..hash], n);
        }

        // ───── sections ─────

        private void Taxonomy()
        {
            if (_db.Taxonomy.Facets.Count == 0) Err("taxonomy: no designation facets defined.");
            Dupes(_db.Taxonomy.Facets.Select(f => f.Id), "facet");
            Dupes(_db.Taxonomy.Terms.Select(t => t.Id), "term");
            Dupes(_db.Taxonomy.Genres.Select(g => g.Id), "genre");
            Dupes(_db.Taxonomy.Traits.Select(t => t.Id), "trait");
            Dupes(_db.Taxonomy.Capabilities.Select(t => t.Id), "capability");
            foreach (var t in _db.Taxonomy.Terms) Ref(_db.Facets, t.Facet, "facet", $"term '{t.Id}'");
            foreach (var axis in new[] { "light", "heat" })
                if (_db.Taxonomy.Axes.All(a => a.Id != axis)) Err($"taxonomy: the Spirit Pools need an axis with id '{axis}'.");
            foreach (var a in _db.Taxonomy.Axes) if (a.Min >= a.Max) Err($"axis '{a.Id}': min must be below max.");
        }

        private void Dupes(IEnumerable<string> ids, string what)
        {
            foreach (var g in ids.GroupBy(x => x).Where(g => g.Count() > 1)) Err($"taxonomy: duplicate {what} id '{g.Key}'.");
        }

        private void Config()
        {
            var c = _db.Config;
            OneOf(c.LexiconMode, "config.lexiconMode", "global", "perObject", "globalFiltered");
            OneOf(c.Funnel, "config.funnel", "off", "gentle", "strong");
            OneOf(c.MineralDestination, "config.mineralDestination", "druids-bag", "satchel");
            OneOf(c.TickDisplay, "config.tickDisplay", "numeric", "diegetic");
            OneOf(c.OfflineProgress, "config.offlineProgress", "none", "capped");
            if (c.SecondsPerTick <= 0) Err("config.secondsPerTick must be positive.");
            if (c.NearMissMinLocales < 2) Warn("config.nearMissMinLocales below 2 lets near-misses fire with a single lens (invariant 6).");
            if (c.Synthesis.Invalid.Count == 0 || c.Synthesis.NotYet.Count == 0) Err("config.synthesis needs at least one 'invalid' and one 'notYet' line.");

            var lib = c.Library;
            OneOf(lib.ShelvingModel, "libraryConfig.shelvingModel", "tagged", "prelabeled");
            OneOf(lib.OrderBy, "libraryConfig.orderBy", "year", "title", "author", "none");
            Cond(lib.FadedRevealWhen, "libraryConfig.fadedRevealWhen");
            var shelvable = new HashSet<string>();
            if (lib.ShelvingModel == "tagged")
            {
                if (lib.ShelfCount <= 0) Err("libraryConfig.shelfCount must be positive.");
                foreach (var t in lib.Tags) { Ref(_db.Genres, t, "genre", "libraryConfig.tags"); shelvable.Add(t); }
                if (lib.Tags.Count > lib.ShelfCount) Warn("libraryConfig: more genre tags than shelves; some genres can never be shelved at once.");
            }
            else
            {
                if (lib.Shelves.Count == 0) Err("libraryConfig: the prelabeled model needs 'shelves'.");
                foreach (var s in lib.Shelves) { Ref(_db.Genres, s.Genre, "genre", $"shelf '{s.Id}'"); if (s.Genre is not null) shelvable.Add(s.Genre); }
            }
            foreach (var t in _db.Tomes.Values)
                if (!shelvable.Contains(t.Genre)) Err($"tome '{t.Id}': genre '{t.Genre}' has no shelf or tag, so it can never be shelved.");

            foreach (var i in c.Apothecary.StartingIngredients) Ref(_db.Items, i, "item", "apothecaryConfig.startingIngredients");
            if (c.Apothecary.MaxInputs < 2) Err("apothecaryConfig.maxInputs must be at least 2.");
        }

        private void OneOf(string value, string where, params string[] allowed)
        {
            if (!allowed.Contains(value)) Err($"{where}: '{value}' must be one of {string.Join(", ", allowed)}.");
        }

        private void Items()
        {
            foreach (var it in _db.Items.Values)
            {
                foreach (var t in it.Traits) Trait(t, $"item '{it.Id}'");
                if (string.IsNullOrWhiteSpace(it.Name)) Err($"item '{it.Id}' has no name.");
                if (it.FoodValue < 0) Err($"item '{it.Id}': foodValue cannot be negative.");
            }
        }

        private void Tomes()
        {
            var lib = _db.Config.Library;
            foreach (var t in _db.Tomes.Values)
            {
                var where = $"tome '{t.Id}' ({t.SourceFile})";
                if (string.IsNullOrWhiteSpace(t.Title)) Err($"{where}: missing title.");
                Ref(_db.Genres, t.Genre, "genre", where);
                var bound = t.Pages.Count(p => !p.Loose);
                if (bound < lib.MinPages || t.Pages.Count > lib.MaxPages)
                    Err($"{where}: has {t.Pages.Count} page(s); the library allows {lib.MinPages}–{lib.MaxPages}.");
                foreach (var p in t.Pages)
                {
                    if (p.Kind is not ("prose" or "poetry" or "letter" or "list" or "art" or "recipe" or "table" or "marginalia"))
                        Warn($"{where}: page {p.Number} has unusual kind '{p.Kind}'.");
                    if (string.IsNullOrWhiteSpace(p.Text)) Err($"{where}: page {p.Number} is empty.");
                }
                Cond(t.Available, where);
            }
        }

        private void Recipes()
        {
            foreach (var r in _db.Recipes.Values)
            {
                var where = $"recipe '{r.Id}'";
                if (r.Inputs.Count == 0) Err($"{where}: no inputs.");
                if (r.Inputs.Count > _db.Config.Apothecary.MaxInputs) Err($"{where}: more inputs than apothecaryConfig.maxInputs.");
                foreach (var i in r.Inputs) Ref(_db.Items, i, "item", where);
                Ref(_db.Items, r.Output, "item", where);
                Ref(_db.Tools, r.Tool, "tool", where);
                if (r.Inputs.Contains(r.Output)) Err($"{where}: output is also an input.");
            }
            foreach (var g in _db.Recipes.Values.GroupBy(RecipeKey).Where(g => g.Count() > 1))
                Err($"recipes {string.Join(", ", g.Select(r => r.Id))} share the same inputs and tool.");
            foreach (var g in _db.Recipes.Values.GroupBy(r => r.Output).Where(g => g.Count() > 1))
                Warn($"element '{g.Key}' can be made more than one way ({string.Join(", ", g.Select(r => r.Id))}).");

            // Acyclicity: no element may (transitively) be needed to make itself.
            var edges = _db.Recipes.Values.SelectMany(r => r.Inputs.Distinct().Select(i => (From: i, To: r.Output)));
            foreach (var cycle in FindCycles(edges)) Err($"recipe tree has a cycle: {string.Join(" → ", cycle)}.");

            // Reachability from the starting stock plus everything the story can deliver.
            var known = new HashSet<string>(_db.Config.Apothecary.StartingIngredients);
            foreach (var p in AllPayloads()) foreach (var i in p.Ingredients) known.Add(i);
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var r in _db.Recipes.Values)
                    if (!known.Contains(r.Output) && r.Inputs.All(known.Contains)) { known.Add(r.Output); grew = true; }
            }
            foreach (var r in _db.Recipes.Values.Where(r => !known.Contains(r.Output)))
                Warn($"recipe '{r.Id}' can never be discovered (its inputs are unreachable).");
        }

        internal static string RecipeKey(RecipeResource r) => string.Join("+", r.Inputs.OrderBy(x => x, StringComparer.Ordinal)) + "|" + (r.Tool ?? "");

        private IEnumerable<Payload> AllPayloads() =>
            _db.Beats.Values.Select(b => b.Payload)
                .Concat(_db.Objects.Values.Select(o => o.Resolution.Payload))
                .Concat(_db.Seals.Values.Select(s => s.Payload));

        private void Tools()
        {
            foreach (var t in _db.Tools.Values)
            {
                var where = $"tool '{t.Id}'";
                Ref(_db.Locales, t.Locale, "locale", where);
                Cond(t.AvailableWhen, where);
                foreach (var tr in t.Traits) Trait(tr, where);
                foreach (var g in t.Grants)
                    if (!_db.Capabilities.ContainsKey(g)) Warn($"{where}: grants undescribed capability '{g}'.");
                if (t.Kind == ToolKind.Assembled)
                {
                    if (t.Components.Count == 0) Err($"{where}: an assembled tool needs components.");
                    foreach (var c in t.Components)
                    {
                        if ((c.Item is null) == (c.Trait is null)) Err($"{where}: each component needs exactly one of 'item' or 'trait'.");
                        Ref(_db.Items, c.Item, "item", where);
                        if (c.Trait is not null) Trait(c.Trait, where);
                    }
                }
            }
        }

        private void Derivations()
        {
            foreach (var d in _db.Derivations.Values)
            {
                var where = $"derivation '{d.Id}'";
                Ref(_db.Locales, d.Locale, "locale", where);
                Ref(_db.Tools, d.Tool, "tool", where);
                // Derivations only run where a locale's mechanics include "derive" (today: the Apothecary bench).
                if (!VerbsByLocale.TryGetValue(d.Locale, out var dverbs) || !dverbs.Contains("derive"))
                    Err($"{where}: '{d.Locale}' has no way to run derivations, so this could never happen. (Crushing and mixing happen at the Apothecary.)");
                if (d.Tool is not null && _db.Tools.TryGetValue(d.Tool, out var dtool) && dtool.Locale != d.Locale)
                    Err($"{where}: tool '{d.Tool}' belongs to {dtool.Locale}, not {d.Locale}.");
                if ((d.Input is null) == (d.InputTrait is null)) Err($"{where}: needs exactly one of 'input' or 'inputTrait'.");
                Ref(_db.Items, d.Input, "item", where);
                if (d.InputTrait is not null) Trait(d.InputTrait, where);
                foreach (var w in d.With) Ref(_db.Items, w, "item", where);
                Ref(_db.Items, d.Produces, "item", where);
                Ref(_db.Items, d.Remnant, "item", where);
                if (_db.Items.TryGetValue(d.Produces, out var p) && p.Kind != ItemKind.ClueForm)
                    Err($"{where}: produces '{d.Produces}', which must be a clueForm to carry the essence.");
                if (d.Remnant is not null && _db.Items.TryGetValue(d.Remnant, out var r) && r.Kind != ItemKind.Remnant)
                    Err($"{where}: remnant '{d.Remnant}' must have kind 'remnant'.");
                if (d.Formulary is not null) Cond(d.Formulary.AvailableWhen, where);
            }

            // Orphan remnants: every remnant item must be left behind by some derivation.
            var produced = _db.Derivations.Values.Where(d => d.Remnant is not null).GroupBy(d => d.Remnant!).ToDictionary(g => g.Key, g => g.Count());
            foreach (var it in _db.Items.Values.Where(i => i.Kind == ItemKind.Remnant))
            {
                if (!produced.TryGetValue(it.Id, out var n)) Err($"remnant '{it.Id}' is never left behind by any derivation (orphan remnant).");
                else if (n > 1) Err($"remnant '{it.Id}' is left behind by {n} derivations; the Circle could not know what to restore.");
            }

            // Containment graph over forms: derivations, pool transmutations and tool assembly. Must be acyclic.
            var edges = new List<(string, string)>();
            foreach (var d in _db.Derivations.Values)
                foreach (var input in DerivationInputs(d)) edges.Add((input, d.Produces));
            foreach (var t in _db.PoolTransforms.Values.Where(t => t.TransformInto is not null)) edges.Add((t.Target, t.TransformInto!));
            foreach (var tool in _db.Tools.Values.Where(t => t.Kind == ToolKind.Assembled))
                foreach (var c in tool.Components)
                    foreach (var f in c.Item is not null ? new[] { c.Item } : FormsWithTrait(c.Trait!))
                        if (_db.Items.TryGetValue(f, out var fi) && fi.Kind == ItemKind.ClueForm) edges.Add((f, tool.Id));
            foreach (var cycle in FindCycles(edges)) Err($"derivation graph has a cycle: {string.Join(" → ", cycle)}.");

            // Every derivation input should be reachable from some object's base form.
            var reachable = new HashSet<string>(_db.Objects.Values.Select(o => o.BaseForm));
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach (var (from, to) in edges) if (reachable.Contains(from) && reachable.Add(to)) grew = true;
            }
            foreach (var d in _db.Derivations.Values)
                if (!DerivationInputs(d).Any(reachable.Contains))
                    Warn($"derivation '{d.Id}': no object's essence can ever reach its input.");

            foreach (var c in _db.Catalysis) Ref(_db.Items, c.From, "item", "catalysis");
        }

        private IEnumerable<string> DerivationInputs(DerivationResource d) =>
            d.Input is not null ? new[] { d.Input } : FormsWithTrait(d.InputTrait!);

        private IEnumerable<string> FormsWithTrait(string trait) =>
            _db.Items.Values.Where(i => i.Kind == ItemKind.ClueForm && i.Traits.Contains(trait)).Select(i => i.Id);

        private void Pools()
        {
            var light = _db.Axis("light");
            var heat = _db.Axis("heat");
            foreach (var p in _db.Pools.Values)
            {
                var where = $"pool '{p.Id}'";
                if (p.Capacity <= 0) Err($"{where}: capacity must be positive.");
                if (p.StartingClarity is < 0 or > 100) Err($"{where}: startingClarity must be 0–100.");
                if (!new IntRange(light.Min, light.Max).Contains(p.BaseEnergy.Light) || !new IntRange(heat.Min, heat.Max).Contains(p.BaseEnergy.Heat))
                    Err($"{where}: base energy is off the axes.");
                foreach (var s in p.StartingStock)
                {
                    Ref(_db.Lifeforms, s.Lifeform, "lifeform", where);
                    if (s.Sex is not (null or "" or "female" or "male")) Err($"{where}: sex must be 'female', 'male' or empty.");
                    if (_db.Lifeforms.TryGetValue(s.Lifeform, out var lf) && lf.Sexed && string.IsNullOrEmpty(s.Sex))
                        Err($"{where}: '{s.Lifeform}' is a sexed species; give each stock entry a sex.");
                }
                foreach (var f in p.SiltFinds) Ref(_db.Items, f.Item, "item", where);
            }
            foreach (var t in _db.PoolTransforms.Values)
            {
                var where = $"pool transform '{t.Id}'";
                Ref(_db.Items, t.Target, "item", where);
                Ref(_db.Items, t.TransformInto, "item", where);
                Requirement(t.Requires, where);
                foreach (var m in t.Modifiers) Ref(_db.Lifeforms, m.Lifeform, "lifeform", where);
                if (t.Ticks <= 0) Err($"{where}: ticks must be positive.");
                if (t.Ticks + t.Modifiers.Sum(m => Math.Min(0, m.DeltaTicks)) < 1) Err($"{where}: modifiers can push the cost below one tick.");
            }
            foreach (var s in _db.SpawnRules.Values)
            {
                Ref(_db.Items, s.Mineral, "item", $"spawn rule '{s.Id}'");
                Ref(_db.Lifeforms, s.Spawns, "lifeform", $"spawn rule '{s.Id}'");
                if (s.EveryTicks <= 0) Err($"spawn rule '{s.Id}': everyTicks must be positive.");
            }
            if (_db.Pools.Count > 0 && !_db.Pools.Values.Any(p => p.SpawningPond) && _db.SpawnRules.Count > 0)
                Warn("spawn rules exist but no pool is a spawning pond.");
        }

        private void Requirement(PoolRequirement r, string where)
        {
            foreach (var t in r.ReagentTraits) Trait(t, where);
            foreach (var i in r.Reagents) Ref(_db.Items, i, "item", where);
            foreach (var l in r.Lifeforms) Ref(_db.Lifeforms, l.Lifeform, "lifeform", where);
            if (r.Light.Min > r.Light.Max || r.Heat.Min > r.Heat.Max) Err($"{where}: a requirement range is inverted.");
        }

        private void Lifeforms()
        {
            var light = _db.Axis("light");
            var heat = _db.Axis("heat");
            foreach (var l in _db.Lifeforms.Values)
            {
                var where = $"lifeform '{l.Id}'";
                foreach (var b in new[] { l.Survive, l.Thrive })
                {
                    if (b.Light.Min > b.Light.Max || b.Heat.Min > b.Heat.Max) Err($"{where}: a band range is inverted.");
                    if (b.Light.Min < light.Min || b.Light.Max > light.Max || b.Heat.Min < heat.Min || b.Heat.Max > heat.Max)
                        Err($"{where}: a band leaves the energy axes ({light.Min}..{light.Max}, {heat.Min}..{heat.Max}).");
                }
                if (!l.Survive.ContainsBand(l.Thrive)) Err($"{where}: the thrive band must sit inside the survive band.");
                if (l.HungerPerTick < 0) Err($"{where}: hungerPerTick cannot be negative.");
                if (l.GestationTicks <= 0) Err($"{where}: gestationTicks must be positive.");
                foreach (var t in l.Traits) Trait(t, where);
            }
            foreach (var b in _db.Breeding.Values)
            {
                var where = $"breeding rule '{b.Id}'";
                Ref(_db.Lifeforms, b.ParentA, "lifeform", where);
                Ref(_db.Lifeforms, b.ParentB, "lifeform", where);
                if (b.Offspring.Count == 0) Err($"{where}: no offspring.");
                foreach (var o in b.Offspring)
                {
                    Ref(_db.Lifeforms, o.Lifeform, "lifeform", where);
                    if (o.Weight <= 0) Err($"{where}: offspring weights must be positive.");
                }
                if (b.Ordered && (!_db.Lifeforms.TryGetValue(b.ParentA, out var a) || !a.Sexed || !_db.Lifeforms.TryGetValue(b.ParentB, out var bb) || !bb.Sexed))
                    Err($"{where}: ordered (mother/father) rules need two sexed species.");
                if (b.Requires is not null) Requirement(b.Requires, where);
            }
        }

        private void Seals()
        {
            foreach (var s in _db.Seals.Values)
            {
                var where = $"seal '{s.Id}'";
                Ref(_db.Locales, s.Locale, "locale", where);
                foreach (var t in s.RequiresTraits) Trait(t, where);
                Ref(_db.Items, s.RequiresItem, "item", where);
                if (s.RequiresTraits.Count == 0 && s.RequiresItem is null) Err($"{where}: needs requiresTraits or requiresItem.");
                Cond(s.AvailableWhen, where);
                PayloadRefs(s.Payload, where);
            }
        }

        private void Objects()
        {
            // Which terms can ever be earned at all?
            var grantable = new HashSet<string>(_db.Objects.Values.SelectMany(o => o.Readings).SelectMany(r => r.Grants));

            foreach (var o in _db.Objects.Values)
            {
                var where = $"object '{o.Id}'";
                if (!_db.Items.TryGetValue(o.BaseForm, out var bf)) Err($"{where}: unknown base form '{o.BaseForm}'.");
                else if (bf.Kind != ItemKind.ClueForm) Err($"{where}: base form '{o.BaseForm}' must have kind 'clueForm'.");
                foreach (var f in o.Facets) Ref(_db.Facets, f, "facet", where);
                Cond(o.Arrival.When, $"{where} arrival");
                Location(o.Arrival.Location, $"{where} arrival");
                if (o.Arrival.Reading is not null && o.Readings.All(r => r.Id != o.Arrival.Reading))
                    Err($"{where}: arrival reading '{o.Arrival.Reading}' is not one of its readings.");
                foreach (var t in o.ExtraRelevantTerms) Ref(_db.Terms, t, "term", where);
                PayloadRefs(o.Resolution.Payload, $"{where} resolution");

                foreach (var r in o.Readings) Reading(o, r);

                var facets = _db.FacetsFor(o).Select(f => f.Id).ToHashSet();
                var trues = o.Claims.Count(c => c.Kind == ClaimKind.True);
                if (trues != 1) Err($"{where}: must have exactly one true claim (has {trues}).");
                var wrongs = o.Claims.Count(c => c.Kind == ClaimKind.PlausibleWrong);
                if (wrongs > MaxPlausibleWrongClaims)
                    Err($"{where}: has {wrongs} plausible-wrong claims; the cap is {MaxPlausibleWrongClaims} (invariant 7).");

                foreach (var c in o.Claims)
                {
                    var cw = $"{where} claim '{c.Id}'";
                    if (!c.Designation.Keys.ToHashSet().SetEquals(facets))
                        Err($"{cw}: designation must give exactly one term for each facet ({string.Join(", ", facets)}).");
                    foreach (var (facet, term) in c.Designation)
                    {
                        if (!_db.Terms.TryGetValue(term, out var tr)) { Err($"{cw}: unknown term '{term}'."); continue; }
                        if (tr.Facet != facet) Err($"{cw}: term '{term}' belongs to facet '{tr.Facet}', not '{facet}'.");
                        if (!grantable.Contains(term))
                        {
                            if (c.Kind == ClaimKind.True) Err($"{cw}: term '{term}' is never granted by any reading, so the truth can't be designated.");
                            else Warn($"{cw}: term '{term}' is never granted by any reading.");
                        }
                    }
                    Cond(c.Requires, cw);
                    foreach (var n in c.Requires.Walk())
                        if (n.Reading is not null && o.Readings.All(r => r.Id != n.Reading))
                            Warn($"{cw}: requires reading '{n.Reading}' from another object.");
                    if (c.Kind == ClaimKind.PlausibleWrong && string.IsNullOrWhiteSpace(c.NearMiss))
                        Err($"{cw}: plausible-wrong claims need near-miss text (invariant 6).");
                    var locales = c.Requires.Walk().Where(n => n.Reading is not null && _db.Readings.ContainsKey(n.Reading))
                        .Select(n => _db.Readings[n.Reading!].Reading.Locale).Distinct().Count();
                    if (c.Kind == ClaimKind.True && locales < 2)
                        Warn($"{cw}: the true claim's evidence spans {locales} locale(s); synthesis should need at least two.");
                }
                foreach (var g in o.Claims.GroupBy(c => string.Join("|", c.Designation.OrderBy(kv => kv.Key).Select(kv => kv.Value))).Where(g => g.Count() > 1))
                    Err($"{where}: claims {string.Join(", ", g.Select(c => c.Id))} share a designation.");
            }
            foreach (var g in _db.Objects.Values.SelectMany(o => o.Readings).GroupBy(r => r.Id).Where(g => g.Count() > 1))
                Err($"reading id '{g.Key}' is used more than once.");
        }

        private void Reading(MysteryObjectResource o, ReadingResource r)
        {
            var where = $"reading '{r.Id}'";
            Ref(_db.Locales, r.Locale, "locale", where);
            var t = r.Trigger;
            if (!VerbsByLocale.TryGetValue(r.Locale, out var verbs) || !verbs.Contains(t.Verb))
                Err($"{where}: verb '{t.Verb}' is not a {r.Locale} mechanic ({(verbs is null ? "none" : string.Join(", ", verbs))}).");
            if (t.Tool is not null and not "*") Ref(_db.Tools, t.Tool, "tool", where);
            foreach (var w in t.With) Ref(_db.Items, w, "item", where);
            if (t.Shelf is not null) Ref(_db.Genres, t.Shelf, "genre", where);
            if (t.Tome is not null)
            {
                if (!_db.Tomes.TryGetValue(t.Tome, out var tome)) Err($"{where}: unknown tome '{t.Tome}'.");
                else if (t.Page is int pg && tome.Pages.All(p => p.Number != pg)) Err($"{where}: tome '{t.Tome}' has no page {pg}.");
            }
            Ref(_db.Pools, t.Pool, "pool", where);
            Ref(_db.Seals, t.Seal, "seal", where);
            Ref(_db.PoolTransforms, t.Transform, "pool transform", where);
            Ref(_db.Derivations, t.Derivation, "derivation", where);
            Ref(_db.Items, r.Form, "item", where);
            foreach (var g in r.Grants) Ref(_db.Terms, g, "term", where);
            Cond(r.Requires, where);
            if (string.IsNullOrWhiteSpace(r.Text)) Err($"{where}: empty text.");
        }

        private void Story()
        {
            foreach (var b in _db.Beats.Values) { Cond(b.When, $"beat '{b.Id}'"); PayloadRefs(b.Payload, $"beat '{b.Id}'"); }
            foreach (var l in _db.Locales.Values)
            {
                Cond(l.UnlockWhen, $"locale '{l.Id}'");
                if (!ContentValidator.VerbsByLocale.ContainsKey(l.Id)) Warn($"locale '{l.Id}' has no engine module yet.");
            }
            if (!_db.Locales.Values.Any(l => l.UnlockWhen.Always == true)) Warn("no locale is open at the start.");
            if (_db.Hub.Art.Count == 0 && _db.Hub.Description.Length == 0) Warn("hub: no courtyard scene (art or description) is defined.");
        }

        /// <summary>Returns one representative path per cycle found (DFS, deterministic order).</summary>
        internal static List<List<string>> FindCycles(IEnumerable<(string From, string To)> edges)
        {
            var adj = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
            foreach (var (f, t) in edges)
            {
                if (!adj.TryGetValue(f, out var s)) adj[f] = s = new SortedSet<string>(StringComparer.Ordinal);
                s.Add(t);
                if (!adj.ContainsKey(t)) adj[t] = new SortedSet<string>(StringComparer.Ordinal);
            }
            var state = new Dictionary<string, int>();
            var stack = new List<string>();
            var cycles = new List<List<string>>();
            void Dfs(string n)
            {
                state[n] = 1; stack.Add(n);
                foreach (var m in adj[n])
                {
                    if (!state.TryGetValue(m, out var st)) Dfs(m);
                    else if (st == 1) cycles.Add(stack.Skip(stack.IndexOf(m)).Append(m).ToList());
                }
                stack.RemoveAt(stack.Count - 1); state[n] = 2;
            }
            foreach (var n in adj.Keys) if (!state.ContainsKey(n)) Dfs(n);
            return cycles;
        }
    }
}
