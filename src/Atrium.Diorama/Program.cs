using Atrium.Core;
using Atrium.Core.Content;
using Atrium.Diorama.App;

namespace Atrium.Diorama;

/// <summary>
/// Atrium, iteration 2: a standalone 3D "lantern-lit diorama" on raylib.
///   AtriumDiorama                       play
///   AtriumDiorama --content DIR         use another content folder
///   AtriumDiorama --saves DIR --slot N  save elsewhere / another slot (shares saves with the terminal game)
///   AtriumDiorama --view FILE.glb                look at a model on a turntable, as the game draws it
///   AtriumDiorama --capture SCRIPT --out DIR [--size 1600x900] [--audio]   scripted screenshots in a hidden window
///                                       (--audio runs the sound system at zero volume, to exercise it)
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        string? content = null, saves = null, capture = null, outDir = null;
        string slot = "keeper";
        bool captureAudio = false;
        string? view = null;
        int w = 1600, h = 900;
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
            switch (args[i])
            {
                case "--content": content = Next(); break;
                case "--saves": saves = Next(); break;
                case "--slot": slot = Next(); break;
                case "--capture": capture = Next(); break;
                case "--out": outDir = Next(); break;
                case "--audio": captureAudio = true; break;
                case "--view": view = Next(); break;
                case "--size":
                    var p = Next().Split('x');
                    w = int.Parse(p[0]); h = int.Parse(p[1]);
                    break;
            }
        }

        content ??= FindContent();
        if (content is null) { Console.Error.WriteLine("Couldn't find the content folder. Pass --content <dir>."); return 2; }
        ContentDatabase db;
        try { db = ContentLoader.LoadFromDirectory(content); }
        catch (ContentLoadException ex) { Console.Error.WriteLine(ex.Message); return 2; }

        saves ??= capture is not null
            ? Path.Combine(Path.GetTempPath(), "atrium-diorama-capture")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Atrium", "saves");
        var store = new SaveStore(saves);
        var cap = capture is null ? null : new Capture(File.ReadAllLines(capture), outDir ?? Path.Combine(Path.GetTempPath(), "atrium-captures")) { WithAudio = captureAudio };
        var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
        new GameApp(db, store, slot, assets, cap) { ViewModel = view }.Run(w, h);
        return 0;
    }

    /// <summary>The live content folder of a checkout, else the copy beside the game.</summary>
    private static string? FindContent()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var c = Path.Combine(dir.FullName, "content");
            if (File.Exists(Path.Combine(c, "config.json"))) return c;
            dir = dir.Parent;
        }
        var beside = Path.Combine(AppContext.BaseDirectory, "content");
        return File.Exists(Path.Combine(beside, "config.json")) ? beside : null;
    }
}
