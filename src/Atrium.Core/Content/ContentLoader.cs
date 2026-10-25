using System.Text.Json;

namespace Atrium.Core.Content;

/// <summary>Where content files come from. A Godot adapter would implement this over <c>res://</c>.</summary>
public interface IContentSource
{
    /// <summary>All content files as (relative path, text). Paths use '/' separators.</summary>
    IEnumerable<(string Path, string Text)> Files();
}

public sealed class DirectoryContentSource : IContentSource
{
    private readonly string _root;
    public DirectoryContentSource(string root) => _root = Path.GetFullPath(root);

    public IEnumerable<(string Path, string Text)> Files()
    {
        if (!Directory.Exists(_root)) throw new DirectoryNotFoundException($"Content directory not found: {_root}");
        foreach (var file in Directory.EnumerateFiles(_root, "*.*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is not (".json" or ".md")) continue;
            var rel = Path.GetRelativePath(_root, file).Replace('\\', '/');
            yield return (rel, File.ReadAllText(file));
        }
    }
}

/// <summary>An in-memory source, used by tests and tools.</summary>
public sealed class MemoryContentSource : IContentSource
{
    private readonly List<(string, string)> _files = new();
    public MemoryContentSource Add(string path, string text) { _files.Add((path, text)); return this; }
    public IEnumerable<(string Path, string Text)> Files() => _files;
}

/// <summary>The shape of one content JSON file. Every section is optional; sections from all files are merged.</summary>
internal sealed class ContentPack
{
    public GameConfig? Config { get; set; }
    public LibraryConfig? LibraryConfig { get; set; }
    public ApothecaryConfig? ApothecaryConfig { get; set; }
    public PoolsConfig? PoolsConfig { get; set; }
    public TaxonomyResource? Taxonomy { get; set; }
    public List<ItemResource>? Items { get; set; }
    public List<RecipeResource>? Recipes { get; set; }
    public List<ToolResource>? Tools { get; set; }
    public List<DerivationResource>? Derivations { get; set; }
    public List<CatalysisResource>? Catalysis { get; set; }
    public List<PoolResource>? Pools { get; set; }
    public List<LifeformResource>? Lifeforms { get; set; }
    public List<BreedingResource>? Breeding { get; set; }
    public List<PoolTransformResource>? PoolTransforms { get; set; }
    public List<SpawnRuleResource>? SpawnRules { get; set; }
    public List<SealResource>? Seals { get; set; }
    public List<MysteryObjectResource>? Objects { get; set; }
    /// <summary>A file may also hold exactly one object at the top level under "object".</summary>
    public MysteryObjectResource? Object { get; set; }
    public List<StoryBeatResource>? Beats { get; set; }
    public List<LocaleResource>? Locales { get; set; }
    public HubResource? Hub { get; set; }
}

public sealed class ContentLoadException : Exception
{
    public IReadOnlyList<string> Errors { get; }
    public ContentLoadException(IReadOnlyList<string> errors)
        : base("Content failed to load:\n  " + string.Join("\n  ", errors)) => Errors = errors;
}

public static class ContentLoader
{
    /// <summary>Loads and validates; throws <see cref="ContentLoadException"/> on any error.</summary>
    public static ContentDatabase Load(IContentSource source) => Load(source, out _);

    public static ContentDatabase Load(IContentSource source, out ValidationReport report)
    {
        var db = LoadUnvalidated(source, out var errors);
        report = ContentValidator.Validate(db);
        report.Errors.InsertRange(0, errors);
        if (report.Errors.Count > 0) throw new ContentLoadException(report.Errors);
        return db;
    }

    public static ContentDatabase LoadFromDirectory(string root) => Load(new DirectoryContentSource(root));

    /// <summary>Loads without throwing on validation errors (for the content validation tool).</summary>
    public static (ContentDatabase? Db, ValidationReport Report) TryLoad(IContentSource source)
    {
        var db = LoadUnvalidated(source, out var errors);
        var report = errors.Count == 0 ? ContentValidator.Validate(db) : new ValidationReport();
        report.Errors.InsertRange(0, errors);
        return (report.Errors.Count == 0 ? db : null, report);
    }

    private static ContentDatabase LoadUnvalidated(IContentSource source, out List<string> errors)
    {
        var db = new ContentDatabase();
        errors = new List<string>();
        bool sawConfig = false, sawLib = false, sawApo = false, sawPools = false, sawHub = false;
        LibraryConfig? lib = null; ApothecaryConfig? apo = null; PoolsConfig? pools = null;

        foreach (var (path, text) in source.Files())
        {
            if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                if (!path.Contains("tomes/", StringComparison.OrdinalIgnoreCase)) continue; // README-style notes are ignored
                try
                {
                    var tome = TomeParser.Parse(text, path);
                    AddUnique(db.Tomes, tome.Id, tome, path, "tome", errors);
                }
                catch (FormatException ex) { errors.Add(ex.Message); }
                continue;
            }

            ContentPack? pack;
            try { pack = JsonSerializer.Deserialize<ContentPack>(text, ContentJson.Options); }
            catch (JsonException ex) { errors.Add($"{path}: {ex.Message}"); continue; }
            if (pack is null) continue;

            if (pack.Config is not null) { if (sawConfig) errors.Add($"{path}: 'config' defined in more than one file."); db.Config = pack.Config; sawConfig = true; }
            if (pack.LibraryConfig is not null) { if (sawLib) errors.Add($"{path}: 'libraryConfig' defined twice."); lib = pack.LibraryConfig; sawLib = true; }
            if (pack.ApothecaryConfig is not null) { if (sawApo) errors.Add($"{path}: 'apothecaryConfig' defined twice."); apo = pack.ApothecaryConfig; sawApo = true; }
            if (pack.PoolsConfig is not null) { if (sawPools) errors.Add($"{path}: 'poolsConfig' defined twice."); pools = pack.PoolsConfig; sawPools = true; }

            if (pack.Taxonomy is { } tax)
            {
                db.Taxonomy.Facets.AddRange(tax.Facets);
                db.Taxonomy.Terms.AddRange(tax.Terms);
                db.Taxonomy.Genres.AddRange(tax.Genres);
                db.Taxonomy.Traits.AddRange(tax.Traits);
                db.Taxonomy.Axes.AddRange(tax.Axes);
                db.Taxonomy.Capabilities.AddRange(tax.Capabilities);
            }
            AddAll(db.Items, pack.Items, i => i.Id, path, "item", errors);
            AddAll(db.Recipes, pack.Recipes, r => r.Id, path, "recipe", errors);
            AddAll(db.Tools, pack.Tools, t => t.Id, path, "tool", errors);
            AddAll(db.Derivations, pack.Derivations, d => d.Id, path, "derivation", errors);
            if (pack.Catalysis is not null) db.Catalysis.AddRange(pack.Catalysis);
            AddAll(db.Pools, pack.Pools, p => p.Id, path, "pool", errors);
            AddAll(db.Lifeforms, pack.Lifeforms, l => l.Id, path, "lifeform", errors);
            AddAll(db.Breeding, pack.Breeding, b => b.Id, path, "breeding rule", errors);
            AddAll(db.PoolTransforms, pack.PoolTransforms, t => t.Id, path, "pool transform", errors);
            AddAll(db.SpawnRules, pack.SpawnRules, s => s.Id, path, "spawn rule", errors);
            AddAll(db.Seals, pack.Seals, s => s.Id, path, "seal", errors);
            var objects = (pack.Objects ?? new()).Concat(pack.Object is null ? Array.Empty<MysteryObjectResource>() : new[] { pack.Object }).ToList();
            AddAll(db.Objects, objects, o => o.Id, path, "mystery object", errors);
            foreach (var o in objects)
            {
                AddAll(db.Items, o.Forms, i => i.Id, path, "item", errors);
                AddAll(db.Derivations, o.Derivations, d => d.Id, path, "derivation", errors);
                db.Catalysis.AddRange(o.Catalysis);
            }
            AddAll(db.Beats, pack.Beats, b => b.Id, path, "story beat", errors);
            AddAll(db.Locales, pack.Locales, l => l.Id, path, "locale", errors);
            if (pack.Hub is not null) { if (sawHub) errors.Add($"{path}: 'hub' defined in more than one file."); db.Hub = pack.Hub; sawHub = true; }
        }

        if (lib is not null) db.Config.Library = lib;
        if (apo is not null) db.Config.Apothecary = apo;
        if (pools is not null) db.Config.Pools = pools;

        // Loose pages are only valid when flagged; delivered pages are identified as tome#page.
        db.Index();
        return db;
    }

    private static void AddAll<T>(Dictionary<string, T> into, List<T>? items, Func<T, string> id, string path, string what, List<string> errors)
    {
        if (items is null) return;
        foreach (var item in items) AddUnique(into, id(item), item, path, what, errors);
    }

    private static void AddUnique<T>(Dictionary<string, T> into, string id, T item, string path, string what, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(id)) { errors.Add($"{path}: a {what} has no id."); return; }
        if (!into.TryAdd(id, item)) errors.Add($"{path}: duplicate {what} id '{id}'.");
    }
}
