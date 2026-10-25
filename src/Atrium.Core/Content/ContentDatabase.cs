namespace Atrium.Core.Content;

/// <summary>All loaded content, indexed by id. Immutable after loading.</summary>
public sealed class ContentDatabase
{
    public GameConfig Config { get; internal set; } = new();
    public TaxonomyResource Taxonomy { get; } = new();

    public Dictionary<string, ItemResource> Items { get; } = new();
    public Dictionary<string, TomeResource> Tomes { get; } = new();
    public Dictionary<string, RecipeResource> Recipes { get; } = new();
    public Dictionary<string, ToolResource> Tools { get; } = new();
    public Dictionary<string, DerivationResource> Derivations { get; } = new();
    public List<CatalysisResource> Catalysis { get; } = new();
    public Dictionary<string, PoolResource> Pools { get; } = new();
    public Dictionary<string, LifeformResource> Lifeforms { get; } = new();
    public Dictionary<string, BreedingResource> Breeding { get; } = new();
    public Dictionary<string, PoolTransformResource> PoolTransforms { get; } = new();
    public Dictionary<string, SpawnRuleResource> SpawnRules { get; } = new();
    public Dictionary<string, SealResource> Seals { get; } = new();
    public Dictionary<string, MysteryObjectResource> Objects { get; } = new();
    public Dictionary<string, StoryBeatResource> Beats { get; } = new();
    public Dictionary<string, LocaleResource> Locales { get; } = new();
    public HubResource Hub { get; internal set; } = new();

    public Dictionary<string, TermResource> Terms { get; } = new();
    public Dictionary<string, FacetResource> Facets { get; } = new();
    public Dictionary<string, GenreResource> Genres { get; } = new();
    public Dictionary<string, TraitResource> Traits { get; } = new();
    public Dictionary<string, CapabilityResource> Capabilities { get; } = new();

    /// <summary>Reading id → (object, reading).</summary>
    public Dictionary<string, (MysteryObjectResource Obj, ReadingResource Reading)> Readings { get; } = new();

    public IEnumerable<LocaleResource> LocalesInOrder => Locales.Values.OrderBy(l => l.Order).ThenBy(l => l.Id, StringComparer.Ordinal);
    public IEnumerable<StoryBeatResource> BeatsInOrder => Beats.Values.OrderBy(b => b.Order).ThenBy(b => b.Id, StringComparer.Ordinal);

    public ItemResource Item(string id) =>
        Items.TryGetValue(id, out var it) ? it : throw new KeyNotFoundException($"Unknown item '{id}'.");

    public string ItemName(string id) => Items.TryGetValue(id, out var it) ? it.Name : id;
    public string TermLabel(string id) => Terms.TryGetValue(id, out var t) ? t.Label : id;
    public string LocaleName(string id) => Locales.TryGetValue(id, out var l) ? l.Name : id;
    public string LifeformName(string id) => Lifeforms.TryGetValue(id, out var l) ? l.Name : id;
    public string ToolName(string id) => Tools.TryGetValue(id, out var t) ? t.Name : id;

    public AxisResource Axis(string id) =>
        Taxonomy.Axes.FirstOrDefault(a => a.Id == id) ?? new AxisResource { Id = id, Label = id };

    /// <summary>Facets an object's designation card uses.</summary>
    public IReadOnlyList<FacetResource> FacetsFor(MysteryObjectResource obj) =>
        (obj.Facets.Count > 0 ? obj.Facets.Where(Facets.ContainsKey).Select(f => Facets[f]) : Taxonomy.Facets).ToList();

    internal void Index()
    {
        foreach (var f in Taxonomy.Facets) Facets[f.Id] = f;
        foreach (var t in Taxonomy.Terms) Terms[t.Id] = t;
        foreach (var g in Taxonomy.Genres) Genres[g.Id] = g;
        foreach (var t in Taxonomy.Traits) Traits[t.Id] = t;
        foreach (var c in Taxonomy.Capabilities) Capabilities[c.Id] = c;
        foreach (var o in Objects.Values)
            foreach (var r in o.Readings)
                Readings[r.Id] = (o, r);

        // Assembled tools exist as items so they can carry an essence, sit in a pool, or go inert.
        foreach (var tool in Tools.Values.Where(t => t.Kind == ToolKind.Assembled))
        {
            if (Items.ContainsKey(tool.Id)) continue;
            Items[tool.Id] = new ItemResource
            {
                Id = tool.Id, Name = tool.Name, Description = tool.Description, Kind = ItemKind.Tool,
                Traits = tool.Traits.ToList(), PoolEnergy = tool.PoolEnergy,
                InertName = tool.InertName, InertDescription = tool.InertDescription,
            };
        }
    }
}
