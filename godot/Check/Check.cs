using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Atrium.Core;
using Atrium.Core.Content;
using Godot;
using FileAccess = Godot.FileAccess;

namespace AtriumGodot;

/// <summary>
/// Headless project check. Run from the repository root:
/// <c>Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --scene res://godot/Check/Check.tscn</c>
/// It checks that the content loads and validates (through the Game autoload), that a GameSession plays 600
/// ticks and round-trips a save, that every .glb under res://art/models imports exactly as its glTF says (every
/// node, its type, every material's colours and textures) and carries what the game needs (the kit contracts in
/// tools/blender/README.md), and that every texture under res://art/textures loads with mipmaps. It prints a
/// report and quits with 0 (pass), 1 (failures) or 2 (the check itself threw).
/// </summary>
public partial class Check : Node
{
    private readonly List<string> _failures = new();
    private readonly List<string> _warnings = new();
    private readonly StringBuilder _log = new();
    private readonly EngineLog _engine = new();

    public override void _Ready()
    {
        int code;
        OS.AddLogger(_engine);
        try
        {
            RunAll();
            code = _failures.Count == 0 ? 0 : 1;
        }
        catch (Exception e)
        {
            Fail("the check itself threw: " + e);
            code = 2;
        }
        OS.RemoveLogger(_engine);
        Line("");
        Line($"== {(code == 0 ? "PASS" : "FAIL")}: {_failures.Count} failures, {_warnings.Count} warnings");
        foreach (var f in _failures) Line("  FAIL " + f);
        foreach (var w in _warnings) Line("  warn " + w);
        GD.Print(_log.ToString());
        GetTree().Quit(code);
    }

    private void Line(string s) => _log.AppendLine(s);
    private void Fail(string s) => _failures.Add(s);
    private void Warn(string s) => _warnings.Add(s);

    private void RunAll()
    {
        Line($"Atrium check: Godot {Engine.GetVersionInfo()["string"]} on {RuntimeInformation.FrameworkDescription}");
        var db = CheckContent();
        if (db is not null) CheckSession(db);
        CheckModels();
        CheckTextures();
    }

    // ───── content and session ─────

    private ContentDatabase? CheckContent()
    {
        Line("");
        Line("== content (res://content through the Game autoload)");
        var game = Game.Instance;
        if (game is null)
        {
            Fail("the Game autoload is not running (project.godot [autoload])");
            return null;
        }
        Line("  " + game.Summary);
        if (game.LoadError is not null) Fail("content: " + game.LoadError);
        foreach (var e in game.Report?.Errors ?? []) Fail("content: " + e);
        foreach (var w in (game.Report?.Warnings ?? []).Take(8)) Line("  content warning: " + w);
        if (game.Session is null && game.Ok) Fail("content loaded but the Game autoload made no GameSession");
        return game.Content;
    }

    private void CheckSession(ContentDatabase db)
    {
        Line("");
        Line("== session");
        var s = GameSession.NewGame(db, 1310);
        var start = s.World.Clock.Tick;
        s.AdvanceTicks(600);
        if (s.World.Clock.Tick != start + 600) Fail($"session: 600 ticks moved the clock from {start} to {s.World.Clock.Tick}");
        var json = s.SaveJson();
        var back = GameSession.LoadJson(db, json, SavedAt(json));
        var again = back.SaveJson();
        if (back.World.Clock.Tick != s.World.Clock.Tick) Fail($"session: saved at tick {s.World.Clock.Tick}, loaded at {back.World.Clock.Tick}");
        if (WithoutSaveTime(json) != WithoutSaveTime(again)) Fail("session: saving the loaded game gave different state than the save it was loaded from");
        Line($"  played 600 ticks (tick {s.World.Clock.Tick}); saved {json.Length:N0} characters; loaded and saved again: {(WithoutSaveTime(json) == WithoutSaveTime(again) ? "identical" : "DIFFERENT")}");
    }

    private static DateTimeOffset? SavedAt(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject o) return null;
        var key = o.Select(p => p.Key).FirstOrDefault(k => k.Equals("savedAtUtc", StringComparison.OrdinalIgnoreCase));
        return key is not null && o[key] is JsonValue v && v.TryGetValue<string>(out var text) ? DateTimeOffset.Parse(text, System.Globalization.CultureInfo.InvariantCulture) : null;
    }

    private static string WithoutSaveTime(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject o) return json;
        foreach (var key in o.Select(p => p.Key).Where(k => k.Equals("savedAtUtc", StringComparison.OrdinalIgnoreCase)).ToList()) o.Remove(key);
        return o.ToJsonString();
    }

    // ───── models ─────

    /// <summary>Godot's node-name suffixes (any of '-', '_', '$' before them, any case) and material suffixes.</summary>
    private static readonly Regex NodeSuffix = new(@"[-_$](noimp|col|convcol|colonly|convcolonly|occ|occonly|navmesh|rigid|vehicle|wheel)$", RegexOptions.IgnoreCase);
    private static readonly Regex MaterialSuffix = new(@"[-_$](alpha|vcol)$", RegexOptions.IgnoreCase);

    private sealed record Contract(string[] Meshes, string[] Markers, string[]? Pieces = null, string[]? Roles = null,
        string[]? MarkerFamilies = null, int? MarkerCount = null, bool FrontMarker = false);

    /// <summary>What the game needs from each known model (tools/blender/README.md and the kit scripts).</summary>
    private static readonly Dictionary<string, Contract> Contracts = new()
    {
        ["library"] = new(["body", "roof", "glass", "door"], ["lamp", "door-point"], FrontMarker: true),
        ["apothecary"] = new(["body", "roof", "glass"], ["lamp", "door-point", "chimney"], FrontMarker: true),
        ["pools"] = new(["body", "glass", "shutters", "shutters-open"], ["lamp", "door-point"], FrontMarker: true),
        ["sanctum"] = new(["body", "glass"], ["lamp", "door-point"], FrontMarker: true),
        ["old_stoic"] = new(["trunk", "canopy"], []),
        ["lamp_post"] = new(["post", "glass"], ["light"]),
        ["library-room"] = new(["shell", "case", "plaque", "table", "decor", "returns", "lamp-glass"], []),
        ["apothecary-room"] = new(["shell", "furniture", "herbs", "lantern", "vessel", "map-board", "book"], []),
        ["sanctum-room"] = new(["shell", "decor", "oculus-glass", "desk", "journal", "board", "glass-table", "bench", "cabinet",
            "circle-candles", "sheet", "plinth", "stand", "relic-door", "beam", "pan", "lens-ring", "lens-glass"], []),
        ["pools-room"] = new(["shell", "decor", "ferns", "beam", "bowl", "bowl-dark", "bowl-dry", "lantern", "crock", "bag", "ledger", "stool", "rake"],
            [.. Enumerable.Range(0, 6).SelectMany(i => new[] { $"pool-{i}", $"pool-{i}-x", $"pool-{i}-z" }),
             "at-crock", "at-bag", "at-ledger", "at-stool", "oculus", "beam-spot"],
            MarkerFamilies: ["brook", "runnel", "outflow"], MarkerCount: 41),
        ["city-kit"] = new([], ["inn-3-lamp", "stable-1-lamp"],
            Pieces: ["house-2a", "house-3a", "house-3b", "house-4a", "house-3c", "shop-3", "inn-3", "stable-1", "tower-5"],
            Roles: ["stone", "trim", "roof", "timber", "plaster", "dark", "glass-a", "glass-b", "glass-back", "plants", "flowers", "cloth", "iron", "pot", "lead", "lamp"]),
        ["corner-kit"] = new([], [],
            Pieces: ["wall-crumble-a", "wall-crumble-b", "brick-arch", "flags-roots-a", "flags-roots-b", "rubble-a", "coping-fallen", "ivy-curtain-a",
                "ivy-curtain-b", "moss-a", "moss-b", "fern-a", "tuft-a", "puddle-a", "puddle-b", "rivulet-a"],
            Roles: ["stone", "brick", "dark", "moss", "root", "leaf", "leaf-b", "fern", "grass", "flower", "flower-b", "water", "trickle"]),
        ["wall-kit"] = new([], [],
            Pieces: ["wall-bay", "wall-bay-plain", "tower-crown", "tower-roof", "tower-band", "gatehouse", "water-gate"],
            Roles: ["stone", "trim", "dark", "iron", "roof", "lead", "cloth", "moss", "lamp", "glass", "shade"]),
    };

    private void CheckModels()
    {
        Line("");
        Line($"== models ({Assets.ModelsDir})");
        var models = Assets.Find(Assets.ModelsDir, ".glb");
        if (models.Count == 0) Fail($"no .glb files under {Assets.ModelsDir}");
        foreach (var path in models) CheckModel(path);
        foreach (var known in Contracts.Keys.Where(k => models.All(m => Assets.FileStem(m) != k)))
            Fail($"{known}.glb is missing from {Assets.ModelsDir}");
    }

    /// <summary>
    /// How each model's embedded images are imported (gltf/embedded_image_handling). 2, Embed as Basis Universal, keeps
    /// the .glb the only source and gives mipmaps and VRAM compression. apothecary-room.glb uses 1, Extract Textures
    /// (lossless PNGs beside it, mipmapped, git-ignored): two of its 24-px trim textures (frame-up 24x270 and
    /// frame-along 270x24) fail Basis decoding at mip level 4 in Godot 4.7.2 ("BasisUniversal cannot unpack level 4").
    /// </summary>
    private static readonly Dictionary<string, int> ImageHandling = new() { ["apothecary-room"] = 1 };

    private static int ImageHandlingFor(string stem) => ImageHandling.TryGetValue(stem, out var mode) ? mode : 2;

    private void CheckModel(string path)
    {
        var stem = Assets.FileStem(path);
        _engine.Take();
        var scene = ResourceLoader.Load<PackedScene>(path);
        var root = scene?.Instantiate();
        if (root is null)
        {
            Fail($"{stem}: {path} did not load or instantiate");
            return;
        }
        AddChild(root);
        try
        {
            var info = ModelInfo.Of(root);
            var box = info.Bounds();
            Line($"  {stem}: {info.Meshes.Count} meshes, {info.Markers.Count} markers, " +
                 (box is { } b ? $"{b.Size.X:0.00} x {b.Size.Y:0.00} x {b.Size.Z:0.00} m, min y {b.Position.Y:0.00}" : "no bounds") +
                 $"; tree {root.GetType().Name} '{root.Name}' > {string.Join(", ", root.GetChildren().Select(c => $"{c.GetType().Name} '{c.Name}'"))}");
            if (box is { } size && (size.Size.Y < 0.05f || size.Size.Y > 100f)) Warn($"{stem}: height {size.Size.Y:0.00} m looks wrong for metres");
            CheckImportOptions(stem, path);
            if (ReadGltf(path) is { } gltf) CompareWithGltf(stem, gltf, root, info);
            else Warn($"{stem}: could not read the glTF source to compare against (exported build?)");
            if (Contracts.TryGetValue(stem, out var contract)) CheckContract(stem, info, contract);
            else Warn($"{stem}: no contract in Check.cs; only checked that it imports faithfully");
            foreach (var e in _engine.Take()) Fail($"{stem}: Godot reported while loading it: {e}");
        }
        finally
        {
            RemoveChild(root);
            root.Free();
        }
    }

    private void CheckImportOptions(string stem, string path)
    {
        var cfg = new ConfigFile();
        if (cfg.Load(path + ".import") != Error.Ok)
        {
            Warn($"{stem}: {path}.import not readable");
            return;
        }
        // Images as ImageHandling says; name suffixes off, so a part is never made a collider, occluder or dropped by its name.
        var mode = ImageHandlingFor(stem);
        if ((int)cfg.GetValue("params", "gltf/embedded_image_handling", -1) != mode)
            Fail($"{stem}: gltf/embedded_image_handling should be {mode} in {path}.import");
        if ((bool)cfg.GetValue("params", "nodes/use_name_suffixes", true))
            Fail($"{stem}: nodes/use_name_suffixes should be false in {path}.import");
    }

    private static JsonObject? ReadGltf(string path)
    {
        var bytes = FileAccess.GetFileAsBytes(path);
        if (bytes.Length < 20 || BitConverter.ToUInt32(bytes, 0) != 0x46546C67u || BitConverter.ToUInt32(bytes, 16) != 0x4E4F534Au) return null;
        var length = BitConverter.ToInt32(bytes, 12);
        return JsonNode.Parse(Encoding.UTF8.GetString(bytes, 20, length)) as JsonObject;
    }

    private void CompareWithGltf(string stem, JsonObject gltf, Node root, ModelInfo info)
    {
        var nodes = gltf["nodes"]?.AsArray() ?? new JsonArray();
        var imported = root.FindChildren("*", "", true, false);
        var byName = new Dictionary<string, Node>();
        foreach (var n in imported)
            if (!byName.TryAdd(n.Name, n)) Warn($"{stem}: two imported nodes are named '{n.Name}'");
        if (imported.Count != nodes.Count)
            Fail($"{stem}: the glTF has {nodes.Count} nodes but {imported.Count} were imported (an importer option or name suffix changed the tree)");

        foreach (var n in nodes)
        {
            var name = (string?)n?["name"] ?? "";
            if (NodeSuffix.IsMatch(name)) Warn($"{stem}: node '{name}' ends in a Godot import suffix (ignored, as nodes/use_name_suffixes is off)");
            if (!byName.TryGetValue(name, out var node))
            {
                Fail($"{stem}: glTF node '{name}' is missing after import");
                continue;
            }
            if (n!["mesh"] is not null)
            {
                if (node is not MeshInstance3D mi || mi.Mesh is null || mi.Mesh.GetSurfaceCount() == 0)
                    Fail($"{stem}: mesh node '{name}' imported as {node.GetType().Name} without a usable mesh");
                else if (mi.GetAabb().Size.Length() <= 0f)
                    Fail($"{stem}: mesh '{name}' has empty bounds");
            }
            else if (n["camera"] is null && n["extensions"] is null && node.GetType() != typeof(Node3D))
                Fail($"{stem}: empty '{name}' imported as {node.GetType().Name}, not Node3D");
        }

        // Materials: every material a mesh uses arrives by name, with its colours in sRGB and its textures embedded.
        var materials = gltf["materials"]?.AsArray() ?? new JsonArray();
        var used = new SortedSet<int>();
        foreach (var mesh in gltf["meshes"]?.AsArray() ?? new JsonArray())
            foreach (var prim in mesh?["primitives"]?.AsArray() ?? new JsonArray())
                if (prim?["material"] is JsonValue mv) used.Add(mv.GetValue<int>());
        var godot = new Dictionary<string, BaseMaterial3D>();
        foreach (var mi in info.Meshes)
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                if (mi.Mesh.SurfaceGetMaterial(s) is BaseMaterial3D bm) godot.TryAdd(bm.ResourceName, bm);
                else Fail($"{stem}: '{mi.Name}' surface {s} has no StandardMaterial3D");
            }

        int textured = 0, embedded = 0, extracted = 0, doubleSided = 0;
        foreach (var index in used)
        {
            var m = materials[index]!;
            var name = (string?)m["name"] ?? $"#{index}";
            if (MaterialSuffix.IsMatch(name)) Warn($"{stem}: material '{name}' ends in a Godot material suffix");
            if (!godot.TryGetValue(name, out var gm))
            {
                Fail($"{stem}: material '{name}' is missing after import");
                continue;
            }
            if ((bool?)m["doubleSided"] == true) doubleSided++;
            var pbr = m["pbrMetallicRoughness"];
            var baseLinear = ColorOf(pbr?["baseColorFactor"], Colors.White);
            var expect = baseLinear.LinearToSrgb();
            if (!Near(gm.AlbedoColor, expect))
                Fail($"{stem}: material '{name}' albedo {Show(gm.AlbedoColor)} but the glTF's linear {Show(baseLinear)} is {Show(expect)} in sRGB");
            if (pbr?["baseColorTexture"] is not null)
            {
                textured++;
                if (gm.AlbedoTexture is not { } tex) Fail($"{stem}: material '{name}' lost its base colour texture");
                else
                {
                    if (tex is PortableCompressedTexture2D) embedded++;
                    else if (tex is CompressedTexture2D && tex.ResourcePath.StartsWith(Assets.ModelsDir + "/" + stem + "_", StringComparison.Ordinal)) extracted++;
                    if (tex.GetImage() is not { } img || !img.HasMipmaps()) Fail($"{stem}: material '{name}' texture has no mipmaps");
                }
            }
            if (m["emissiveFactor"] is JsonArray ef && ColorOf(ef, Colors.Black) is var em && (em.R + em.G + em.B) > 0f)
            {
                if (!gm.EmissionEnabled || !Near(gm.Emission, em.LinearToSrgb()))
                    Fail($"{stem}: material '{name}' emission {(gm.EmissionEnabled ? Show(gm.Emission) : "off")}, expected {Show(em.LinearToSrgb())} (sRGB of the glTF's linear {Show(em)})");
                else
                    Line($"    emissive '{name}': {Show(gm.Emission)} sRGB, energy {gm.EmissionEnergyMultiplier}");
                if (Near(em, baseLinear.LinearToSrgb()) && !Near(em, baseLinear))
                    Warn($"{stem}: material '{name}' emissive {Show(em)} equals its base colour in sRGB: the kit wrote an sRGB colour where glTF " +
                         $"expects linear, so it shows as {Show(em.LinearToSrgb())} instead of {Show(em)} (fix in the kit script: pass the linear colour)");
            }
        }
        Line($"    {used.Count} materials ({doubleSided} double-sided), {textured} textured, all mipmapped ({embedded} embedded as Basis Universal, " +
             $"{extracted} extracted), {gltf["images"]?.AsArray().Count ?? 0} images; all {nodes.Count} nodes match the glTF");
        var expected = ImageHandlingFor(stem) == 1 ? extracted : embedded;
        if (textured != expected) Fail($"{stem}: {textured - expected} textured materials don't use {(ImageHandlingFor(stem) == 1 ? "an extracted" : "an embedded Basis Universal")} texture");
    }

    private static Color ColorOf(JsonNode? node, Color fallback)
    {
        if (node is not JsonArray a || a.Count < 3) return fallback;
        return new Color((float)a[0]!, (float)a[1]!, (float)a[2]!, a.Count > 3 ? (float)a[3]! : 1f);
    }

    private static bool Near(Color a, Color b) =>
        Math.Abs(a.R - b.R) <= 1.5f / 255f && Math.Abs(a.G - b.G) <= 1.5f / 255f && Math.Abs(a.B - b.B) <= 1.5f / 255f;

    private static string Show(Color c) => $"({c.R:0.000}, {c.G:0.000}, {c.B:0.000})";

    private void CheckContract(string stem, ModelInfo info, Contract c)
    {
        foreach (var mesh in c.Meshes)
            if (info.Mesh(mesh) is null) Fail($"{stem}: part '{mesh}' is missing");
        foreach (var marker in c.Markers)
            if (info.Marker(marker) is null) Fail($"{stem}: marker '{marker}' is missing");
        foreach (var family in c.MarkerFamilies ?? [])
        {
            var members = info.Markers.Where(m => Regex.IsMatch(m.Name, $"^{Regex.Escape(family)}-\\d+$")).ToList();
            if (members.Count == 0) Fail($"{stem}: no '{family}-N' markers");
            for (int i = 0; i < members.Count; i++)
                if (info.Marker($"{family}-{i}") is null) Fail($"{stem}: '{family}' markers are not numbered 0..{members.Count - 1} (no '{family}-{i}')");
        }
        if (c.MarkerCount is { } count && info.Markers.Count != count)
            Fail($"{stem}: {info.Markers.Count} markers, expected {count}");
        if (c.Pieces is { } pieces)
        {
            foreach (var piece in pieces)
            {
                if (info.Marker(piece) is null) Fail($"{stem}: piece '{piece}' has no origin empty");
                if (!info.PieceMeshes(piece).Any()) Fail($"{stem}: piece '{piece}' has no parts");
            }
            foreach (var mesh in info.Meshes)
            {
                var parts = mesh.Name.ToString().Split('_', 2);
                if (parts.Length != 2 || !pieces.Contains(parts[0]) || !(c.Roles ?? []).Contains(parts[1]))
                    Fail($"{stem}: part '{mesh.Name}' is not <piece>_<role> with a known piece and role");
            }
        }
        if (c.FrontMarker && info.Marker("door-point") is { } door && door.GlobalPosition.Z <= 0f)
            Fail($"{stem}: door-point is at z {door.GlobalPosition.Z:0.00}; the front should face +Z");
        var markerNote = c.MarkerCount is { } n ? $", all {n} markers" : "";
        Line($"    contract: {c.Meshes.Length} named parts, {c.Markers.Length} named markers{markerNote}" +
             (c.Pieces is { } p ? $", {p.Length} pieces" : "") + (c.FrontMarker && info.Marker("door-point") is { } d ? $", door-point z {d.GlobalPosition.Z:0.00} (front +Z)" : ""));
    }

    // ───── textures ─────

    private void CheckTextures()
    {
        Line("");
        Line($"== textures ({Assets.TexturesDir})");
        var textures = Assets.Find(Assets.TexturesDir, ".png");
        if (textures.Count == 0) Fail($"no .png files under {Assets.TexturesDir}");
        foreach (var path in textures)
        {
            var stem = Assets.FileStem(path);
            _engine.Take();
            var tex = ResourceLoader.Load<Texture2D>(path);
            if (tex is null || tex.GetWidth() == 0)
            {
                Fail($"{stem}: {path} did not load");
                continue;
            }
            var cfg = new ConfigFile();
            string settings;
            if (cfg.Load(path + ".import") == Error.Ok)
            {
                var mode = (int)cfg.GetValue("params", "compress/mode", -1);
                var mips = (bool)cfg.GetValue("params", "mipmaps/generate", false);
                settings = $"compress/mode={mode}, high_quality={cfg.GetValue("params", "compress/high_quality", false)}, mipmaps={mips}";
                if (!mips) Fail($"{stem}: a 3D surface texture needs mipmaps/generate=true");
                if (mode != 2) Fail($"{stem}: a 3D surface texture should be VRAM compressed (compress/mode=2)");
            }
            else
            {
                settings = "import settings not readable";
                Warn($"{stem}: {path}.import not readable");
            }
            Line($"  {stem}: {tex.GetWidth()} x {tex.GetHeight()} {tex.GetType().Name}; {settings}");
            foreach (var e in _engine.Take()) Fail($"{stem}: Godot reported while loading it: {e}");
        }
    }
}

/// <summary>Collects what the engine logs (errors, warnings and plain messages) while the check runs.</summary>
public partial class EngineLog : Logger
{
    private readonly object _gate = new();
    private readonly List<string> _entries = new();

    /// <summary>Everything logged since the last call.</summary>
    public List<string> Take()
    {
        lock (_gate)
        {
            var copy = _entries.ToList();
            _entries.Clear();
            return copy;
        }
    }

    public override void _LogError(string function, string file, int line, string code, string rationale, bool editorNotify, int errorType,
        Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
    {
        var kind = errorType == (int)ErrorType.Warning ? "warning" : "error";
        lock (_gate) _entries.Add($"{kind}: {(string.IsNullOrEmpty(rationale) ? code : rationale)} ({file}:{line})");
    }

    /// <summary>Plain messages count too: loading a resource prints nothing when it is well, and some decoders report
    /// problems as plain prints (Basis Universal's "cannot unpack level").</summary>
    public override void _LogMessage(string message, bool error)
    {
        lock (_gate) _entries.Add((error ? "error: " : "printed: ") + message.Trim());
    }
}
