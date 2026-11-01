using Godot;

namespace AtriumGodot;

/// <summary>Finds the game's imported art at run time (no hard-coded lists) and reads a model's parts and markers.</summary>
public static class Assets
{
    public const string ModelsDir = "res://art/models";
    public const string TexturesDir = "res://art/textures";

    /// <summary>
    /// Every resource in <paramref name="dir"/> with the extension, as res:// paths in ordinal order.
    /// <see cref="ResourceLoader.ListDirectory"/> lists resources by their editor names, in the editor and after export.
    /// </summary>
    public static List<string> Find(string dir, string extension)
    {
        var list = new List<string>();
        foreach (var entry in ResourceLoader.ListDirectory(dir))
            if (!entry.EndsWith('/') && entry.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                list.Add(dir + "/" + entry);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    public static string FileStem(string path) => System.IO.Path.GetFileNameWithoutExtension(path);
}

/// <summary>
/// What an instantiated kit offers: its meshes (parts, by node name) and its markers (empties: plain, childless
/// <see cref="Node3D"/>s). Kits are flat (one root empty holding every part and marker), but nothing here assumes it.
/// </summary>
public sealed class ModelInfo
{
    public Node Root { get; }
    public List<MeshInstance3D> Meshes { get; } = new();
    public List<Node3D> Markers { get; } = new();

    private ModelInfo(Node root) => Root = root;

    public static ModelInfo Of(Node root)
    {
        var info = new ModelInfo(root);
        foreach (var node in root.FindChildren("*", "", true, false))
        {
            if (node is MeshInstance3D mesh) info.Meshes.Add(mesh);
            else if (node.GetType() == typeof(Node3D) && node.GetChildCount() == 0) info.Markers.Add((Node3D)node);
        }
        info.Meshes.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        info.Markers.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return info;
    }

    public MeshInstance3D? Mesh(string name) => Meshes.FirstOrDefault(m => m.Name == name);
    public Node3D? Marker(string name) => Markers.FirstOrDefault(m => m.Name == name);

    /// <summary>A kit's pieces: the prefixes of "piece_role" mesh names, when every mesh is named that way.</summary>
    public List<string> Pieces()
    {
        if (Meshes.Count == 0 || Meshes.Any(m => !m.Name.ToString().Contains('_'))) return new();
        return Meshes.Select(m => m.Name.ToString().Split('_')[0]).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();
    }

    public IEnumerable<MeshInstance3D> PieceMeshes(string piece) => Meshes.Where(m => m.Name.ToString().StartsWith(piece + "_", StringComparison.Ordinal));

    /// <summary>World-space bounds of the given meshes (all by default). The model must be in the scene tree.</summary>
    public Aabb? Bounds(IEnumerable<MeshInstance3D>? meshes = null)
    {
        Aabb? box = null;
        foreach (var m in meshes ?? Meshes)
        {
            if (m.Mesh is null) continue;
            var b = m.GlobalTransform * m.GetAabb();
            box = box is { } a ? a.Merge(b) : b;
        }
        return box;
    }
}
