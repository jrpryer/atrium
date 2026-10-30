using System.Numerics;
using System.Text.Json;
using Raylib_cs;

namespace Atrium.Diorama.Render;

/// <summary>A model, or just some of its meshes: one part of an imported model (a house's glass, a tree's canopy).</summary>
public readonly record struct Part(Model Model, int[]? Meshes = null)
{
    public static implicit operator Part(Model m) => new(m);
}

/// <summary>
/// A model from the Blender pipeline (tools/blender → art/models/*.glb), made ready for the cel shader: the import
/// step of engine task E1. raylib loads the file; this adds what raylib leaves out or gets wrong for this look.
/// - Parts. raylib drops node names, so each mesh is matched back to its glTF node (by material and vertex count)
///   and the game asks for meshes by part name: <c>glass</c>, <c>canopy</c>, <c>shutters</c>, <c>shutters-open</c>,
///   anything else is body.
/// - Markers. raylib drops empties; their positions (<c>lamp</c>, <c>light</c>, <c>door-point</c>, <c>chimney</c>)
///   are read from the file's node tree.
/// - Colour. raylib takes glTF's linear base colour as if it were sRGB, which darkens it; it is converted back.
/// - Ink. Welded smooth normals go in the tangent channel, which the outline pass pushes along, and meshes without
///   vertex colours get white ones (the toon shader multiplies by them).
/// - Textures get mipmaps and trilinear filtering, so painted surfaces don't shimmer in the distance.
/// </summary>
public sealed class ModelAsset : IDisposable
{
    public Model Model { get; }
    public string Name { get; }
    /// <summary>Every mesh, in the file's units (metres; glTF Y-up, front toward +Z).</summary>
    public BoundingBox Bounds { get; }
    public IReadOnlyDictionary<string, Vector3> Markers => _markers;

    private readonly string[] _partOf;
    private readonly Dictionary<string, Vector3> _markers = new();

    private ModelAsset(string path)
    {
        Name = Path.GetFileNameWithoutExtension(path);
        using var json = ReadJson(path);
        var root = json.RootElement;
        var primitives = ReadNodes(root, _markers); // checks the file before raylib touches it
        var model = Raylib.LoadModel(path);
        if (!Raylib.IsModelValid(model)) throw new InvalidDataException($"raylib couldn't load {path}");
        try
        {
            _partOf = MatchParts(primitives, model);
            FixColours(root, model);
            for (int i = 0; i < model.MeshCount; i++) Prepare(model, i);
            Mipmaps(model);
        }
        catch
        {
            Raylib.UnloadModel(model);
            throw;
        }
        Model = model;
        Bounds = Raylib.GetModelBoundingBox(model);
    }

    /// <summary>Loads a model, or returns null (with a note on the console) if the file is missing or unreadable.</summary>
    public static ModelAsset? TryLoad(string path)
    {
        if (!File.Exists(path)) { Console.WriteLine($"models: {path} not found; using the built-in shape"); return null; }
        try { return new ModelAsset(path); }
        catch (Exception e) { Console.WriteLine($"models: can't import {path}: {e.Message}"); return null; }
    }

    public bool Has(string part) => _partOf.Contains(part);
    /// <summary>Every part's name (a kit's pieces and roles).</summary>
    public IEnumerable<string> PartNames => _partOf.Where(n => n != "").Distinct();

    /// <summary>The meshes of the named parts.</summary>
    public int[] Meshes(params string[] parts) => Enumerable.Range(0, _partOf.Length).Where(i => parts.Contains(_partOf[i])).ToArray();

    /// <summary>Every mesh not in the named parts.</summary>
    public int[] MeshesExcept(params string[] parts) => Enumerable.Range(0, _partOf.Length).Where(i => !parts.Contains(_partOf[i])).ToArray();

    public Part Part(params string[] parts) => new(Model, Meshes(parts));
    public Part PartExcept(params string[] parts) => new(Model, MeshesExcept(parts));

    /// <summary>The bounds of the named parts (every part if none are named), in the file's units.</summary>
    public unsafe BoundingBox BoundsOf(params string[] parts)
    {
        var box = new BoundingBox(new Vector3(float.MaxValue), new Vector3(float.MinValue));
        foreach (int i in parts.Length == 0 ? MeshesExcept() : Meshes(parts))
        {
            var b = Raylib.GetMeshBoundingBox(Model.Meshes[i]);
            box = new BoundingBox(Vector3.Min(box.Min, b.Min), Vector3.Max(box.Max, b.Max));
        }
        return box;
    }

    /// <summary>Where a ray first meets the named parts (every part if none are named) drawn at <paramref name="world"/>.</summary>
    public unsafe RayCollision Hit(Ray ray, Matrix4x4 world, params string[] parts)
    {
        var best = new RayCollision { Distance = float.MaxValue };
        var transform = Matrix4x4.Transpose(world); // raylib's matrices are column-major
        foreach (int i in parts.Length == 0 ? MeshesExcept() : Meshes(parts))
        {
            var h = Raylib.GetRayCollisionMesh(ray, Model.Meshes[i], transform);
            if (h.Hit && h.Distance < best.Distance) best = h;
        }
        return best;
    }

    public void Dispose() => Raylib.UnloadModel(Model);

    // ───────────────────────── import ─────────────────────────

    /// <summary>The JSON chunk of a glTF binary file.</summary>
    private static JsonDocument ReadJson(string path)
    {
        using var r = new BinaryReader(File.OpenRead(path));
        if (r.ReadUInt32() != 0x46546C67) throw new InvalidDataException("not a glTF binary (.glb) file"); // "glTF"
        r.ReadUInt32(); // version
        r.ReadUInt32(); // total length
        int length = (int)r.ReadUInt32();
        if (r.ReadUInt32() != 0x4E4F534A) throw new InvalidDataException("the first chunk isn't JSON"); // "JSON"
        return JsonDocument.Parse(r.ReadBytes(length));
    }

    /// <summary>
    /// The file's triangle primitives, each with the part (node) it belongs to, in node order; the nodes without
    /// meshes go in <paramref name="markers"/>. Refuses a part too big for raylib's 16-bit mesh indices.
    /// </summary>
    private static List<(string Part, int Material, int Vertices)> ReadNodes(JsonElement root, Dictionary<string, Vector3> markers)
    {
        var nodes = root.GetProperty("nodes");
        int n = nodes.GetArrayLength();
        var parent = Enumerable.Repeat(-1, n).ToArray();
        for (int i = 0; i < n; i++)
            if (nodes[i].TryGetProperty("children", out var children))
                foreach (var c in children.EnumerateArray()) parent[c.GetInt32()] = i;
        var world = new Matrix4x4?[n];
        Matrix4x4 World(int i) => world[i] ??= Local(nodes[i]) * (parent[i] >= 0 ? World(parent[i]) : Matrix4x4.Identity);

        var accessors = root.GetProperty("accessors");
        var primitives = new List<(string Part, int Material, int Vertices)>();
        for (int i = 0; i < n; i++)
        {
            var node = nodes[i];
            string name = node.TryGetProperty("name", out var nm) ? PartName(nm.GetString()) : "";
            if (!node.TryGetProperty("mesh", out var mesh))
            {
                if (name != "") markers[name] = World(i).Translation;
                continue;
            }
            foreach (var p in root.GetProperty("meshes")[mesh.GetInt32()].GetProperty("primitives").EnumerateArray())
            {
                if (p.TryGetProperty("mode", out var mode) && mode.GetInt32() != 4) continue; // triangles only, like raylib
                int material = p.TryGetProperty("material", out var mt) ? mt.GetInt32() : -1;
                int count = accessors[p.GetProperty("attributes").GetProperty("POSITION").GetInt32()].GetProperty("count").GetInt32();
                if (count > 65535) throw new InvalidDataException($"part '{name}' has {count:N0} vertices; raylib meshes stop at 65,535 (16-bit indices)");
                primitives.Add((name, material, count));
            }
        }
        return primitives;
    }

    /// <summary>
    /// Names each raylib mesh after the glTF node it came from. raylib makes one mesh per triangle primitive and
    /// numbers materials from 1 (0 is its default), so a mesh is matched to the first unused primitive with the same
    /// material and vertex count.
    /// </summary>
    private static unsafe string[] MatchParts(List<(string Part, int Material, int Vertices)> primitives, Model model)
    {
        var used = new bool[primitives.Count];
        var parts = new string[model.MeshCount];
        for (int i = 0; i < model.MeshCount; i++)
        {
            int material = model.MeshMaterial[i] - 1, count = model.Meshes[i].VertexCount;
            int k = Enumerable.Range(0, primitives.Count).FirstOrDefault(j => !used[j] && primitives[j].Material == material && primitives[j].Vertices == count, -1);
            if (k < 0) k = Enumerable.Range(0, primitives.Count).FirstOrDefault(j => !used[j] && primitives[j].Vertices == count, -1);
            if (k >= 0) used[k] = true;
            parts[i] = k >= 0 ? primitives[k].Part : "";
        }
        return parts;
    }

    /// <summary>A node's name without Blender's ".001" suffix.</summary>
    private static string PartName(string? s) => s is null ? "" : s.Split('.')[0];

    private static Matrix4x4 Local(JsonElement node)
    {
        if (node.TryGetProperty("matrix", out var m))
        {
            var a = m.EnumerateArray().Select(e => e.GetSingle()).ToArray(); // column-major, which is System.Numerics' row-vector layout
            return new Matrix4x4(a[0], a[1], a[2], a[3], a[4], a[5], a[6], a[7], a[8], a[9], a[10], a[11], a[12], a[13], a[14], a[15]);
        }
        var t = node.TryGetProperty("translation", out var tr) ? Vec(tr) : Vector3.Zero;
        var s = node.TryGetProperty("scale", out var sc) ? Vec(sc) : Vector3.One;
        var r = Quaternion.Identity;
        if (node.TryGetProperty("rotation", out var ro))
        {
            var q = ro.EnumerateArray().Select(e => e.GetSingle()).ToArray();
            r = new Quaternion(q[0], q[1], q[2], q[3]);
        }
        return Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t);
    }

    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());

    /// <summary>Base colours as the shader expects them: sRGB, like vertex colours and painted textures.</summary>
    private static unsafe void FixColours(JsonElement root, Model model)
    {
        if (!root.TryGetProperty("materials", out var materials)) return;
        for (int i = 0; i < materials.GetArrayLength() && i + 1 < model.MaterialCount; i++)
        {
            var f = new[] { 1f, 1f, 1f, 1f };
            if (materials[i].TryGetProperty("pbrMetallicRoughness", out var pbr) && pbr.TryGetProperty("baseColorFactor", out var bc))
                f = bc.EnumerateArray().Select(e => e.GetSingle()).ToArray();
            static byte Srgb(float c)
            {
                c = Math.Clamp(c, 0, 1);
                float s = c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1 / 2.4f) - 0.055f;
                return (byte)MathF.Round(s * 255);
            }
            model.Materials[i + 1].Maps[(int)MaterialMapIndex.Albedo].Color = new Color(Srgb(f[0]), Srgb(f[1]), Srgb(f[2]), (byte)MathF.Round(Math.Clamp(f[3], 0, 1) * 255));
        }
    }

    /// <summary>
    /// Re-uploads a mesh with white vertex colours where it has none and welded smooth normals in the tangent
    /// channel (what the outline pass pushes along; the lighting keeps the file's own normals).
    /// </summary>
    private static unsafe void Prepare(Model model, int index)
    {
        var src = model.Meshes[index];
        int n = src.VertexCount;
        var mesh = new Mesh(n, src.TriangleCount);
        mesh.AllocVertices();
        mesh.AllocNormals();
        mesh.AllocTexCoords();
        mesh.AllocColors();
        mesh.AllocTangents();
        var pos = mesh.VerticesAs<Vector3>();
        var nrm = mesh.NormalsAs<Vector3>();
        new Span<Vector3>(src.Vertices, n).CopyTo(pos);
        if (src.Normals != null) new Span<Vector3>(src.Normals, n).CopyTo(nrm);
        if (src.TexCoords != null) new Span<Vector2>(src.TexCoords, n).CopyTo(mesh.TexCoordsAs<Vector2>());
        if (src.Colors != null) new Span<Color>(src.Colors, n).CopyTo(mesh.ColorsAs<Color>());
        else mesh.ColorsAs<Color>().Fill(Color.White);
        if (src.Indices != null)
        {
            mesh.AllocIndices();
            new Span<ushort>(src.Indices, src.TriangleCount * 3).CopyTo(mesh.IndicesAs<ushort>());
        }

        // smooth normals: average the normals of every vertex at the same (millimetre-rounded) position
        var smooth = new Dictionary<(int, int, int), Vector3>();
        (int, int, int) Key(Vector3 p) => ((int)MathF.Round(p.X * 1000), (int)MathF.Round(p.Y * 1000), (int)MathF.Round(p.Z * 1000));
        for (int i = 0; i < n; i++)
        {
            var k = Key(pos[i]);
            smooth[k] = smooth.TryGetValue(k, out var acc) ? acc + nrm[i] : nrm[i];
        }
        var tan = mesh.TangentsAs<Vector4>();
        for (int i = 0; i < n; i++)
        {
            var s = smooth[Key(pos[i])];
            tan[i] = new Vector4(s.LengthSquared() < 1e-8f ? nrm[i] : Vector3.Normalize(s), 1);
        }

        Raylib.UploadMesh(ref mesh, false);
        Raylib.UnloadMesh(src);
        model.Meshes[index] = mesh;
    }

    private static unsafe void Mipmaps(Model model)
    {
        var done = new HashSet<uint>();
        uint fallback = Rlgl.GetTextureIdDefault();
        for (int i = 0; i < model.MaterialCount; i++)
        {
            ref var map = ref model.Materials[i].Maps[(int)MaterialMapIndex.Albedo];
            var tex = map.Texture;
            if (tex.Id == 0 || tex.Id == fallback || !done.Add(tex.Id)) continue;
            Raylib.GenTextureMipmaps(ref tex);
            Raylib.SetTextureFilter(tex, TextureFilter.Trilinear);
            map.Texture = tex;
        }
    }
}
