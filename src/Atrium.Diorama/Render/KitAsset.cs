using System.Numerics;
using Raylib_cs;

namespace Atrium.Diorama.Render;

/// <summary>
/// A kit of pieces from the Blender pipeline (tools/blender/assets/*_kit.py), for baking copies into the game's own
/// meshes: each piece's meshes by role (parts named <c>&lt;piece&gt;_&lt;role&gt;</c>), its origin (an empty named after the
/// piece) and its other markers (<c>&lt;piece&gt;-&lt;marker&gt;</c>). Vertices are in file space; subtract the origin.
/// </summary>
public sealed class KitAsset
{
    public sealed record Piece(string Name, Vector3 Origin, (string Role, Mesh Mesh, Color Colour)[] Meshes, IReadOnlyDictionary<string, Vector3> Markers);

    private readonly Dictionary<string, Piece> _pieces = new();

    public Piece? this[string name] => _pieces.GetValueOrDefault(name);
    public IEnumerable<Piece> Pieces => _pieces.Values;

    public static unsafe KitAsset? From(ModelAsset? asset)
    {
        if (asset is null) return null;
        var kit = new KitAsset();
        var model = asset.Model;
        foreach (var group in asset.PartNames.Where(n => n.Contains('_')).GroupBy(n => n[..n.IndexOf('_')]))
        {
            string name = group.Key;
            if (!asset.Markers.TryGetValue(name, out var origin)) continue;
            var meshes = group.SelectMany(part => asset.Meshes(part).Select(i =>
                (part[(name.Length + 1)..], model.Meshes[i], model.Materials[model.MeshMaterial[i]].Maps[(int)MaterialMapIndex.Albedo].Color))).ToArray();
            var markers = asset.Markers.Where(m => m.Key.StartsWith(name + "-")).ToDictionary(m => m.Key[(name.Length + 1)..], m => m.Value);
            kit._pieces[name] = new Piece(name, origin, meshes, markers);
        }
        return kit._pieces.Count > 0 ? kit : null;
    }
}
