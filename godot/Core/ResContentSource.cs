using Atrium.Core.Content;
using Godot;
using FileAccess = Godot.FileAccess;

namespace AtriumGodot;

/// <summary>
/// Atrium.Core's content source over Godot's virtual filesystem: every <c>.json</c> and <c>.md</c> file under
/// <c>res://content</c>, read with <see cref="DirAccess"/> and <see cref="FileAccess"/> so an exported game reads
/// them from its .pck too. Paths are relative to the root with '/' separators, in ordinal order, matching
/// <see cref="DirectoryContentSource"/>.
/// </summary>
public sealed class ResContentSource : IContentSource
{
    private readonly string _root;

    public ResContentSource(string root = "res://content") => _root = root.TrimEnd('/');

    public IEnumerable<(string Path, string Text)> Files()
    {
        if (DirAccess.Open(_root) is null)
            throw new DirectoryNotFoundException($"Content directory not found: {_root} ({DirAccess.GetOpenError()})");
        var files = new List<string>();
        Collect(_root, "", files);
        files.Sort(StringComparer.Ordinal);
        foreach (var rel in files)
        {
            var path = _root + "/" + rel;
            var text = FileAccess.GetFileAsString(path);
            var error = FileAccess.GetOpenError();
            if (error != Error.Ok) throw new IOException($"Could not read {path}: {error}");
            yield return (rel, text);
        }
    }

    private static void Collect(string dir, string prefix, List<string> into)
    {
        foreach (var file in DirAccess.GetFilesAt(dir))
        {
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".json" or ".md") into.Add(prefix + file);
        }
        foreach (var sub in DirAccess.GetDirectoriesAt(dir))
            Collect(dir + "/" + sub, prefix + sub + "/", into);
    }
}
