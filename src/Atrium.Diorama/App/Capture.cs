using System.Numerics;
using Atrium.Core;
using Atrium.Core.Runtime;
using Atrium.Diorama.Overlays;
using Atrium.Diorama.Scenes;
using Raylib_cs;

namespace Atrium.Diorama.App;

/// <summary>
/// Scripted runs for visual checks: drives the game in a hidden window with simulated input and saves PNGs.
/// One command per line:
///   new [seed] · load · preset NAME · go hub|library|apothecary|pools|sanctum|title
///   wait SECONDS · ticks N · shot NAME · move X Y · click X Y · key NAME · wheel N · orbit DX
///   journal · settings · say TEXT (log) · # comment
///   press ID (click a named control; a trailing * matches by prefix) · pick N|TEXT (choose in the top list)
///   yes (answer a Confirm) · spot ID (click room furniture) · use DEF (use a satchel item here) · dump (log overlay and notices)
///   back (Esc) · close (close the top overlay) · read TOME (open the reader) · pause [off] (hold the clock)
///   ui off|on (hide the interface for clean reference frames) · view FILE (model viewer) · pose YAW PITCH (viewer angle)
///   plain [off] (viewer: the file as it is, or as the game draws it)
/// Coordinates are in the virtual 1600×900 interface space.
/// </summary>
public sealed class Capture
{
    private readonly Queue<string> _lines;
    private readonly string _out;
    private float _wait;
    private string? _shot;
    private int _releaseIn = -1;
    private int _pressIn = -1;
    public bool Done { get; private set; }

    /// <summary>Run the sound system (at zero volume) so its code paths are exercised too.</summary>
    public bool WithAudio { get; init; }

    /// <summary>Hides the interface so shots show only the 3D view (reference frames for painting over).</summary>
    public bool HideUi { get; private set; }

    public Capture(IEnumerable<string> lines, string outDir)
    {
        _lines = new Queue<string>(lines.Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')));
        _out = outDir;
        Directory.CreateDirectory(outDir);
    }

    public void BeforeFrame(GameApp app)
    {
        if (_pressIn == 0) { Input.ScriptPress(); _releaseIn = 2; }
        if (_pressIn >= 0) _pressIn--;
        if (_releaseIn == 0) Input.ScriptRelease();
        if (_releaseIn >= 0) _releaseIn--;
        if (_wait > 0) { _wait -= 1 / 60f; return; }
        while (_lines.Count > 0 && _wait <= 0 && _shot is null)
        {
            var parts = _lines.Dequeue().Split(' ', 2);
            var cmd = parts[0].ToLowerInvariant();
            var arg = parts.Length > 1 ? parts[1].Trim() : "";
            switch (cmd)
            {
                case "new":
                    app.Session = GameSession.NewGame(app.Content, arg.Length > 0 ? ulong.Parse(arg) : null);
                    app.Go(new HubScene(), instant: true);
                    break;
                case "load":
                    if (app.LoadGame()) app.Go(new HubScene(), instant: true);
                    break;
                case "preset":
                    app.Session ??= GameSession.NewGame(app.Content, 1310);
                    Presets.Apply(app.Session, arg);
                    app.Session.DrainNotices();
                    break;
                case "go":
                    app.Go(SceneFor(arg), instant: true);
                    break;
                case "wait":
                    _wait = float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case "ticks":
                    app.Session?.AdvanceTicks(int.Parse(arg));
                    break;
                case "shot":
                    _shot = arg;
                    return;
                case "move":
                    Input.ScriptMove(Vec(arg));
                    break;
                case "click":
                    // hover for a couple of frames first, as a real hand would, then press
                    Input.ScriptMove(Vec(arg));
                    _pressIn = 2;
                    _wait = 6 / 60f;
                    break;
                case "key":
                    Input.ScriptKey(Enum.Parse<KeyboardKey>(arg, ignoreCase: true));
                    _wait = 2 / 60f;
                    break;
                case "wheel":
                    Input.ScriptWheel(float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "orbit":
                    Input.ScriptOrbit(float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "dismiss":
                    app.DismissAll();
                    break;
                case "press":
                    if (app.Ui.TryGetControl(arg, out var rc))
                    {
                        Input.ScriptMove(new Vector2(rc.X + rc.Width / 2, rc.Y + rc.Height / 2));
                        _pressIn = 2;
                        _wait = 6 / 60f;
                    }
                    else Console.WriteLine($"capture: no control '{arg}' on screen");
                    break;
                case "pick":
                    if (app.TopOverlay is IChooser ch)
                    {
                        int idx = int.TryParse(arg, out var n) ? n : ch.Labels.ToList().FindIndex(l => l.Contains(arg, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0 && idx < ch.Labels.Count) { Console.WriteLine($"capture: picked '{ch.Labels[idx]}'"); ch.Choose(idx); }
                        else Console.WriteLine($"capture: no choice '{arg}' in: {string.Join(" | ", ch.Labels)}");
                    }
                    else Console.WriteLine($"capture: no list open (top: {app.TopOverlay?.Describe() ?? "none"})");
                    _wait = 4 / 60f;
                    break;
                case "yes":
                    if (app.TopOverlay is Confirm cf) cf.Yes();
                    else Console.WriteLine($"capture: no question open (top: {app.TopOverlay?.Describe() ?? "none"})");
                    _wait = 4 / 60f;
                    break;
                case "close":
                    app.TopOverlay?.Close();
                    _wait = 4 / 60f;
                    break;
                case "back":
                    Input.ScriptKey(KeyboardKey.Escape);
                    _wait = 4 / 60f;
                    break;
                case "spot":
                    if (app.Scene is InteriorScene room && !room.Activate(arg))
                        Console.WriteLine($"capture: no spot '{arg}' (have: {string.Join(", ", room.SpotIds)})");
                    _wait = 4 / 60f;
                    break;
                case "use":
                    var item = app.Session?.World.Items.Satchel.FirstOrDefault(i => i.Def == arg);
                    if (item is null) Console.WriteLine($"capture: no '{arg}' in the satchel");
                    else if (app.Scene is not UI.ISatchelAware aware || !aware.UseItem(item)) app.Open(new UI.ItemCard(item));
                    _wait = 4 / 60f;
                    break;
                case "view":
                    app.Go(new ModelViewerScene(arg), instant: true);
                    break;
                case "pose":
                    var pp = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (app.Scene is ModelViewerScene mv) mv.Pose(float.Parse(pp[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(pp[1], System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "plain":
                    if (app.Scene is ModelViewerScene pv) pv.Plain(arg != "off");
                    break;
                case "ui":
                    HideUi = arg == "off";
                    break;
                case "pause":
                    app.Session?.SetPaused(arg != "off");
                    break;
                case "read":
                    app.Open(new ReaderOverlay(arg));
                    _wait = 4 / 60f;
                    break;
                case "dump":
                    Console.WriteLine("capture: overlay: " + (app.TopOverlay?.Describe() ?? "none"));
                    foreach (var t in app.Toasts.Dump()) Console.WriteLine("capture: notice: " + t);
                    break;
                case "journal":
                    app.Open(new JournalOverlay());
                    break;
                case "settings":
                    app.Open(new SettingsCard());
                    break;
                case "say":
                    Console.WriteLine("capture: " + arg);
                    break;
                default:
                    Console.WriteLine($"capture: unknown command '{cmd}'");
                    break;
            }
        }
        if (_lines.Count == 0 && _wait <= 0 && _shot is null) Done = true;
    }

    public void AfterDraw(GameApp app)
    {
        if (_shot is null) return;
        var img = Raylib.LoadImageFromScreen();
        var path = Path.Combine(_out, _shot.EndsWith(".png") ? _shot : _shot + ".png");
        Raylib.ExportImage(img, path);
        Raylib.UnloadImage(img);
        Console.WriteLine("capture: wrote " + path);
        _shot = null;
    }

    private static Vector2 Vec(string s)
    {
        var p = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new Vector2(float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture));
    }

    public static Scene SceneFor(string id) => id switch
    {
        "title" => new TitleScene(),
        "hub" => new HubScene(),
        _ => HubScene.Interior(id),
    };
}

/// <summary>Jumps the chapter forward for capture runs and quick testing (uses the public API only).</summary>
public static class Presets
{
    public static void Apply(GameSession s, string name)
    {
        switch (name)
        {
            case "shelves": TagShelves(s); break;
            case "pools-open": SolveLeaf(s); break;
            case "midgame": Midgame(s); break;
            case "shelved": TagShelves(s); ShelveAll(s); break;
            default: Console.WriteLine($"capture: unknown preset '{name}'"); break;
        }
    }

    private static ItemInstance Carried(GameSession s, string def) => s.World.Items.Satchel.First(i => i.Def == def);

    private static void TagShelves(GameSession s)
    {
        var tags = s.Content.Config.Library.Tags;
        for (int i = 0; i < tags.Count; i++)
            if (s.Library.Shelves()[i].Genre is null) s.Library.TagShelf(i, tags[i]);
    }

    private static void ShelveAll(GameSession s)
    {
        foreach (var t in s.Library.Pile().ToList())
        {
            var shelf = s.Library.Shelves().FirstOrDefault(x => x.Genre == t.Genre);
            if (shelf is null) continue;
            s.Library.Shelve(t.Id, shelf.Index, shelf.Tomes.Count(x => x.Year <= t.Year));
        }
    }

    private static void SolveLeaf(GameSession s)
    {
        if (s.State.Evidence.Resolved.Contains("pressed-leaf")) return;
        TagShelves(s);
        var leaf = s.Library.PileItems().FirstOrDefault(i => i.Def == "pressed-leaf");
        if (leaf is not null) { s.Library.ShelveObject(leaf.Id, 0); s.Library.TakeFromPile(leaf.Id); }
        var l = Carried(s, "pressed-leaf").Id;
        s.Library.Compare(l, "trees-of-the-upper-tiers", 1);
        s.Library.Compare(l, "signs-and-sigils-of-stairfall", 2);
        s.Apothecary.Experiment(l, Array.Empty<string>(), "weighing-scales");
        s.Apothecary.Experiment(l, Array.Empty<string>(), "bunsen-burner");
        s.Sanctum.Inspect(l, "magnifying-glass");
        s.Sanctum.Designate("pressed-leaf", new Dictionary<string, string> { ["what"] = "vow-token", ["origin"] = "high-hallows", ["holds"] = "a-vow" });
    }

    private static void Discover(GameSession s, string element)
    {
        if (s.State.Apothecary.Known.Contains(element)) return;
        var recipe = s.Content.Recipes.Values.First(r => r.Output == element);
        foreach (var i in recipe.Inputs) Discover(s, i);
        s.Apothecary.Mix(recipe.Inputs, recipe.Tool);
    }

    private static void Midgame(GameSession s)
    {
        SolveLeaf(s);
        s.Pools.Cleanse("sunward-basin");
        s.Pools.Cleanse("shade-pool");
        var n = Carried(s, "gold-nugget").Id;
        s.Apothecary.Experiment(n, Array.Empty<string>(), "weighing-scales");
        s.Library.Compare(n, "tithes-and-offerings-of-the-hallows", 1);
        s.Sanctum.Inspect(n, "magnifying-glass");
        s.Apothecary.Experiment(n, Array.Empty<string>(), "mortar-and-pestle");
        s.Sanctum.Designate("gold-nugget", new Dictionary<string, string> { ["what"] = "tithe-nail", ["origin"] = "old-stoic", ["holds"] = "a-promise" });
        Discover(s, "pressed-oil");
        Discover(s, "lamp-glass");
        Discover(s, "waxed-reed-pouch");
        s.Library.Open("the-ledger-of-tides");
        s.AdvanceTicks(400);
    }
}
