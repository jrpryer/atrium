using System.Numerics;
using Atrium.Core.Content;
using Atrium.Core.Runtime;
using Atrium.Diorama.App;
using Atrium.Diorama.Overlays;
using Atrium.Diorama.Render;
using Atrium.Diorama.UI;
using Raylib_cs;
using static Atrium.Diorama.Scenes.ApothecaryProps;

namespace Atrium.Diorama.Scenes;

/// <summary>
/// The Apothecary: a wall of shelves with a jar for every element the player knows, a workbench with the shop's
/// tools, and a copper vessel to mix in. Click jars to drop elements in, click a tool to pick it up, then mix from
/// the bench card. Odd objects from the satchel go on the brass tray to be worked with the same tools; anything
/// that would change an object's form is confirmed first, in the core's own words. The Discovery Map is pinned on
/// the wall (and blooms as discoveries are made), the old formulary sits on its lectern, and a crate of empty
/// bottles by the window stoppers measures to carry. Every rule is the core's; this is presentation only.
/// </summary>
public sealed partial class ApothecaryScene : InteriorScene
{
    protected override string LocaleId => "apothecary";

    // ── the bench (what the player has set out; the core sees it only when they mix or work) ──
    private readonly List<string> _bench = new();
    private string? _tool;
    private string? _object;

    // ── models ──
    private Part _furniture, _herbs, _lantern, _vessel, _mapBoard, _book;
    private Model _liquid, _bit, _node;
    private Dictionary<string, Keeper> _keepers = new();
    private readonly Dictionary<ToolLook, ToolModels> _toolModels = new();
    private readonly Dictionary<string, Model> _objectModels = new();
    private Dictionary<string, string> _shapes = new();

    private sealed class Jar
    {
        public required string Id;
        public required ItemResource Def;
        public required Keeper Keeper;
        public Color Color;
        public Vector3 Pos, Target;
        public float Scale = 1, Appear = 1;
        public bool Waiting;
        public Spring Wobble = new(0, 210, 5);
        public Spring Hop = new(0, 240, 11);
    }

    private readonly List<Jar> _jars = new();
    private readonly Dictionary<string, Jar> _jarById = new();

    private sealed class ToolProp
    {
        public required ToolResource Def;
        public ToolLook Look;
        public Vector3 Pos;
        public float Height;
        public Spring Tilt = new(0, 40, 2.6f);
        public float Busy, Spin, Sel;
    }

    private readonly List<ToolProp> _tools = new();
    private string _toolKey = "";

    // ── the vessel ──
    private static readonly Color EmptyLiquid = Palette.Hex(0x4E5A60);
    private Color _liquidColor = EmptyLiquid;
    private float _level = 0.46f, _swirl, _swirlSpeed = 0.4f, _murk, _flash;
    private Color _flashColor = Palette.LanternHot;
    private Spring _vesselHop = new(0, 260, 12);
    private int _landed; // inputs that have actually dropped in (the rest are still flying)

    // ── things in flight ──
    private sealed class Flight
    {
        public Vector3 From, To;
        public float T, Dur = 0.55f, Arc = 0.9f, Size = 0.14f;
        public Color Color;
        public Action? Land;
    }

    private readonly List<Flight> _flights = new();

    private sealed class Pocket { public required ItemResource Def; public Vector2 From; public float T, Delay; }
    private readonly List<Pocket> _pockets = new();

    // ── cached counts (refreshed after actions and now and then) ──
    private int _discovered, _total, _frontier;
    private float _refresh;
    private float _steamT, _bubbleT;
    private string? _prevHover;
    private float _shift;

    private int MaxHere => S.Apothecary.MaxInputs - (_object is null ? 0 : 1);

    // ───────────────────────── building ─────────────────────────

    protected override void Build()
    {
        CamTarget = new Vector3(1.05f, 2.15f, -2.6f);
        CamYaw = 0.34f; CamPitch = 0.3f; CamDist = 11.6f; CamFov = 38;

        _shapes = LoadShapes(App.AssetsDir);
        var toon = R.Shaders.Toon;
        _furniture = Prop("furniture", () => Furniture(Accent));
        _herbs = Prop("herbs", Herbs);
        _lantern = Prop("lantern", Lantern);
        _vessel = Prop("vessel", Vessel);
        _liquid = Own(Liquid().Model(toon));
        _mapBoard = Prop("map-board", MapBoard);
        _book = Prop("book", OpenBook);
        _bit = Own(new MeshBuilder().Blob(Vector3.Zero, new Vector3(0.07f, 0.05f, 0.07f), 2, 6, Color.White, 4, 0.2f).Model(toon));
        _node = Own(new MeshBuilder().Cylinder(Vector3.Zero, 0.055f, 0.055f, 0.02f, 8, Color.White)
            .Transform(Matrix4x4.CreateRotationX(MathF.PI / 2)).Model(toon));
        _keepers = BuildKeepers(toon);
        foreach (var k in _keepers.Values) { Own(k.Tinted); if (k.Plain is { } p) Own(p); if (k.Glass is { } g) Own(g); }

        AddSpot("vessel", VesselPos + new Vector3(-0.6f, 0, -0.6f), VesselPos + new Vector3(0.6f, 0.82f, 0.45f), "The copper vessel", "");
        AddSpot("tray", TrayPos + new Vector3(-0.34f, 0, -0.26f), TrayPos + new Vector3(0.34f, 0.3f, 0.3f), "The brass tray", "");
        AddSpot("map", MapCentre - new Vector3(MapW / 2, MapH / 2, 0.06f), MapCentre + new Vector3(MapW / 2, MapH / 2, 0.08f), "The Discovery Map",
            "Every discovery blooms here. Click to read it.");
        AddSpot("formulary", LecternPos + new Vector3(-0.5f, 0.75f, -0.45f), LecternPos + new Vector3(0.5f, 1.4f, 0.45f), "The old formulary",
            "Half recipes, half riddles, cracked along the spine. Click to read it.");
        AddSpot("bottles", CratePos + new Vector3(-0.5f, 0, -0.38f), CratePos + new Vector3(0.5f, 0.85f, 0.38f), "A crate of empty bottles",
            "Stopper a measure of an element to carry with you. There's always more on the shelf.");

        SyncJars(fromVessel: false);
        SyncTools();
        RefreshCounts();
    }

    private Model ObjectModel(string def)
    {
        var shape = _shapes.GetValueOrDefault(def) ?? "orb";
        if (!_objectModels.TryGetValue(shape, out var m))
            _objectModels[shape] = m = Own(ObjectMesh(shape).Model(R.Shaders.Toon));
        return m;
    }

    private MeshBuilder MapBoard()
    {
        var m = new MeshBuilder();
        var c = MapCentre;
        m.Box(c, new Vector3(MapW, MapH, 0.03f), Palette.Hex(0xEADBB8));
        var ink = Palette.Hex(0x6A5A4A);
        // a hand-ruled border, a compass rose and a few routes between the nodes
        m.Box(c + new Vector3(0, MapH / 2 - 0.08f, 0.02f), new Vector3(MapW - 0.12f, 0.015f, 0.01f), ink);
        m.Box(c + new Vector3(0, -MapH / 2 + 0.08f, 0.02f), new Vector3(MapW - 0.12f, 0.015f, 0.01f), ink);
        m.Box(c + new Vector3(MapW / 2 - 0.08f, 0, 0.02f), new Vector3(0.015f, MapH - 0.12f, 0.01f), ink);
        m.Box(c + new Vector3(-MapW / 2 + 0.08f, 0, 0.02f), new Vector3(0.015f, MapH - 0.12f, 0.01f), ink);
        var rose = c + new Vector3(MapW / 2 - 0.3f, -MapH / 2 + 0.3f, 0.02f);
        m.Box(rose, new Vector3(0.02f, 0.3f, 0.01f), Palette.Apothecary);
        m.Box(rose, new Vector3(0.3f, 0.02f, 0.01f), Palette.Apothecary);
        m.Cylinder(rose - new Vector3(0, 0, 0.005f), 0.06f, 0.06f, 0.01f, 8, Palette.Gold);
        foreach (var pin in new[] { new Vector3(-MapW / 2 + 0.1f, MapH / 2 - 0.1f, 0.03f), new Vector3(MapW / 2 - 0.1f, MapH / 2 - 0.1f, 0.03f) })
            m.Box(c + pin, new Vector3(0.06f, 0.06f, 0.05f), Brass);
        var nodes = MapNodes(30);
        for (int i = 1; i < nodes.Count; i++)
        {
            var a = nodes[(i - 1) / 2] + new Vector3(0, 0, 0.015f);
            var b = nodes[i] + new Vector3(0, 0, 0.015f);
            var d = b - a;
            float len = d.Length();
            m.Append(new MeshBuilder().Box(new Vector3(0, len / 2, 0), new Vector3(0.012f, len, 0.004f), Palette.Alpha(ink, 255))
                .Transform(Align(d) * Matrix4x4.CreateTranslation(a)));
        }
        return m;
    }

    /// <summary>Where the map's nodes sit: a branching bloom from the left edge of the parchment.</summary>
    private static List<Vector3> MapNodes(int count)
    {
        var rng = new Random(13);
        var list = new List<Vector3>();
        float w = MapW - 0.5f, h = MapH - 0.45f;
        for (int i = 0; i < count; i++)
        {
            int depth = (int)MathF.Floor(MathF.Log2(i + 1));
            float across = (i + 1 - (1 << depth)) / (float)(1 << depth);
            float x = -w / 2 + Math.Min(depth, 5) / 5f * w + ((float)rng.NextDouble() - 0.5f) * 0.12f;
            float y = (across - 0.5f + 0.5f / (1 << depth)) * h + ((float)rng.NextDouble() - 0.5f) * 0.1f;
            if (depth > 5) { x -= (float)rng.NextDouble() * 0.4f; y = ((float)rng.NextDouble() - 0.5f) * h; }
            list.Add(MapCentre + new Vector3(x, y, 0.025f));
        }
        return list;
    }

    private static MeshBuilder OpenBook()
    {
        var m = new MeshBuilder();
        m.Box(new Vector3(0, 0.0f, 0), new Vector3(0.66f, 0.03f, 0.46f), Palette.Hex(0x6B3F2E));
        var page = Palette.Hex(0xF2E6CA);
        m.Append(new MeshBuilder().Box(new Vector3(-0.155f, 0, 0), new Vector3(0.31f, 0.05f, 0.42f), page).Transform(Matrix4x4.CreateRotationZ(-0.08f) * Matrix4x4.CreateTranslation(0, 0.03f, 0)));
        m.Append(new MeshBuilder().Box(new Vector3(0.155f, 0, 0), new Vector3(0.31f, 0.05f, 0.42f), page).Transform(Matrix4x4.CreateRotationZ(0.08f) * Matrix4x4.CreateTranslation(0, 0.03f, 0)));
        for (int i = 0; i < 5; i++)
        {
            m.Box(new Vector3(-0.16f, 0.07f, -0.14f + i * 0.06f), new Vector3(0.22f, 0.006f, 0.012f), Palette.Hex(0x8A7A6A));
            m.Box(new Vector3(0.16f, 0.07f, -0.14f + i * 0.06f), new Vector3(0.2f - (i % 2) * 0.06f, 0.006f, 0.012f), Palette.Hex(0x8A7A6A));
        }
        m.Box(new Vector3(0.02f, 0.06f, 0.24f), new Vector3(0.03f, 0.01f, 0.12f), Palette.Apothecary);
        return m;
    }

    private static Matrix4x4 LecternTop => Matrix4x4.CreateRotationX(0.42f) * Matrix4x4.CreateRotationY(-0.55f) * Matrix4x4.CreateTranslation(LecternPos + new Vector3(0, 1.06f, 0));

    // ───────────────────────── keeping the room in step with the core ─────────────────────────

    /// <summary>Adds a jar for every newly known element (in discovery order) and lays the shelves out again.</summary>
    private List<Jar> SyncJars(bool fromVessel)
    {
        var fresh = new List<Jar>();
        var known = S.Apothecary.Known();
        foreach (var def in known)
        {
            if (_jarById.ContainsKey(def.Id)) continue;
            var keeper = _keepers[KeeperFor(_shapes.GetValueOrDefault(def.Id), def.Kind)];
            var jar = new Jar { Id = def.Id, Def = def, Keeper = keeper, Color = App.Art.ColorOf(def) };
            _jars.Add(jar);
            _jarById[def.Id] = jar;
            AddSpot("jar:" + def.Id, Vector3.Zero, Vector3.One, def.Name, "");
            fresh.Add(jar);
        }
        if (fresh.Count == 0) return fresh;
        Layout(snap: !fromVessel);
        bool initial = _jars.Count == fresh.Count;
        int k = 0;
        foreach (var jar in fresh)
        {
            jar.Pos = jar.Target;
            if (initial) continue;
            jar.Appear = 0;
            if (fromVessel)
            {
                jar.Waiting = true;
                var j = jar;
                _flights.Add(new Flight
                {
                    From = VesselPos + new Vector3(0, 0.9f, 0), To = jar.Target + new Vector3(0, 0.25f, 0), Dur = 0.9f + k * 0.15f, Arc = 1.4f, Size = 0.3f,
                    Color = jar.Color, Land = () => { j.Waiting = false; Arrive(j); },
                });
            }
            else Arrive(jar);
            k++;
        }
        return fresh;
    }

    /// <summary>A new jar lands on its shelf: a pop, a sparkle and a clink.</summary>
    private void Arrive(Jar jar)
    {
        jar.Appear = 0;
        jar.Hop.Kick(R.ReduceMotion ? 0 : 3f);
        App.Audio.Play("clink", 0.55f, 1.15f);
        Burst(jar.Target + new Vector3(0, 0.3f, 0), jar.Color, 22);
        Burst(jar.Target + new Vector3(0, 0.3f, 0), Palette.LanternHot, 10);
    }

    private void Layout(bool snap)
    {
        int n = _jars.Count;
        int perRow = Math.Max(11, (int)MathF.Ceiling(n / (float)ShelfRows.Length));
        float width = ShelfRight - ShelfLeft - 0.3f, cx = (ShelfLeft + ShelfRight) / 2;
        float spacing = width / perRow;
        float scale = MathF.Min(1, spacing / 0.56f);
        for (int i = 0; i < n; i++)
        {
            int row = i / perRow, col = i % perRow;
            int inRow = Math.Min(perRow, n - row * perRow);
            var jar = _jars[i];
            jar.Target = new Vector3(cx + (col - (inRow - 1) / 2f) * spacing, ShelfRows[Math.Min(row, ShelfRows.Length - 1)], ShelfZ + 0.06f);
            jar.Scale = scale;
            if (snap) jar.Pos = jar.Target;
        }
    }

    /// <summary>Lays the tools along the bench, either side of the vessel; the set can grow (a still, say).</summary>
    private void SyncTools()
    {
        var tools = S.Apothecary.Tools();
        var key = string.Join("|", tools.Select(t => t.Id));
        if (key == _toolKey) return;
        _toolKey = key;
        foreach (var t in _tools) RemoveSpot("tool:" + t.Def.Id);
        var old = _tools.ToDictionary(t => t.Def.Id);
        _tools.Clear();
        int n = tools.Count, left = n / 2, right = n - left;
        float l0 = BenchLeft + 0.45f, l1 = VesselPos.X - 0.95f, r0 = VesselPos.X + 0.95f, r1 = BenchRight - 0.4f;
        for (int i = 0; i < n; i++)
        {
            var def = tools[i];
            var look = LookFor(def);
            if (!_toolModels.ContainsKey(look))
            {
                var tm = BuildTool(look, R.Shaders.Toon);
                Own(tm.Body); if (tm.Part is { } a) Own(a); if (tm.Part2 is { } b) Own(b); if (tm.Glass is { } g) Own(g);
                _toolModels[look] = tm;
            }
            bool onLeft = i < left;
            int k = onLeft ? i : i - left, count = onLeft ? left : right;
            float t = count <= 1 ? 0.5f : k / (float)(count - 1);
            float x = onLeft ? l0 + (l1 - l0) * t : r0 + (r1 - r0) * t;
            float z = BenchZ - 0.12f + (k % 2) * 0.22f;
            var prop = old.TryGetValue(def.Id, out var o) ? o : new ToolProp { Def = def };
            prop.Def = def;
            prop.Look = look;
            prop.Pos = new Vector3(x, BenchTop, z);
            prop.Height = look switch { ToolLook.Mortar => 0.5f, ToolLook.Burner => 0.62f, ToolLook.Scales => 0.78f, ToolLook.Loupe => 0.66f, ToolLook.Still => 0.75f, _ => 0.36f };
            _tools.Add(prop);
            AddSpot("tool:" + def.Id, prop.Pos + new Vector3(-0.36f, 0, -0.3f), prop.Pos + new Vector3(0.36f, prop.Height, 0.3f), def.Name, "");
        }
        if (_tool is not null && _tools.All(t => t.Def.Id != _tool)) _tool = null;
    }

    private void RefreshCounts()
    {
        var map = S.Apothecary.Map();
        _discovered = map.Discovered;
        _total = map.Total;
        _frontier = map.Frontier.Count;
        _refresh = 0.6f;
    }

    // ───────────────────────── per frame ─────────────────────────

    protected override void Light()
    {
        base.Light();
        var l = R.Light;
        float t = R.ReduceMotion ? 0 : R.Time;
        float flick = 0.04f * MathF.Sin(t * 9.1f) + 0.03f * MathF.Sin(t * 13.7f + 1);
        l.Lamps.Add((LanternPos - new Vector3(0, 0.1f, 0), 7.5f, Palette.LanternHot, 0.85f + flick));
        l.Lamps.Add((SconcePos + new Vector3(0, 0.1f, 0.2f), 3.6f, Palette.Lantern, 0.65f + flick * 2));
        l.Lamps.Add((VesselPos + new Vector3(0, 1.0f, 0.35f), 2.6f, Palette.Lerp(_liquidColor, Palette.LanternHot, 0.3f), 0.3f + _flash * 1.4f));
        foreach (var tp in _tools)
            if (tp.Look == ToolLook.Burner)
            {
                l.Lamps.Add((tp.Pos + new Vector3(0, 0.5f, 0.1f), 2.0f + tp.Busy, Palette.Hex(0xFFB060), 0.35f + tp.Busy * 0.6f + flick * 3));
                break;
            }
        l.Lamps.Add((new Vector3(-4.6f, 3.4f, -1.5f), 5.5f, Palette.Hex(0xFFE3B8), 0.35f)); // the window's glow
    }

    protected override void Tick(float dt)
    {
        SyncJars(fromVessel: false); // elements can arrive from elsewhere (the Sanctum sends some)
        SyncTools();
        if (_object is not null && S.World.Items.Get(_object) is not { Location: Locations.Satchel }) _object = null;
        if ((_refresh -= dt) <= 0) RefreshCounts();

        // the camera eases aside so a card never hides the furniture you're working with
        _shift = Ease.Approach(_shift, _card == Card.None ? 0 : 1, 4, dt);
        var right = new Vector3(MathF.Cos(CamYaw), 0, -MathF.Sin(CamYaw));
        CamTarget = new Vector3(1.05f, 2.15f, -2.6f) - right * (1.7f * _shift);

        UpdateHints();
        if (Hovered != _prevHover && Hovered is not null && Hovered.StartsWith("jar:", StringComparison.Ordinal) && _jarById.TryGetValue(Hovered[4..], out var hj))
            hj.Wobble.Kick(R.ReduceMotion ? 0 : (_jars.IndexOf(hj) % 2 == 0 ? 2.4f : -2.4f));
        _prevHover = Hovered;

        foreach (var j in _jars)
        {
            j.Pos = R.ReduceMotion ? j.Target : Ease.Approach(j.Pos, j.Target, 6, dt);
            j.Wobble.Update(dt);
            j.Hop.Update(dt);
            if (!j.Waiting && j.Appear < 1) j.Appear = MathF.Min(1, j.Appear + dt / 0.45f);
            float s = j.Scale;
            var box = SpotOf("jar:" + j.Id);
            box.Box = new BoundingBox(j.Pos + new Vector3(-0.21f * s, 0, -0.25f), j.Pos + new Vector3(0.21f * s, (j.Keeper.Height + 0.04f) * s, 0.25f));
            box.Enabled = !j.Waiting;
        }

        foreach (var tp in _tools)
        {
            bool sel = tp.Def.Id == _tool;
            tp.Sel = Ease.Approach(tp.Sel, sel ? 1 : 0, 10, dt);
            tp.Busy = MathF.Max(0, tp.Busy - dt / 1.4f);
            tp.Tilt.Update(dt);
            if (!R.ReduceMotion) tp.Spin += dt * (0.4f + tp.Busy * 9f) * (tp.Look == ToolLook.Mortar ? 1 : 0);
        }

        // the vessel: colour follows what's in it, the level rises with each drop, swirls settle
        var target = LiquidTarget();
        _liquidColor = LerpColor(_liquidColor, target, 1 - MathF.Exp(-3f * dt));
        _level = Ease.Approach(_level, 0.46f + 0.08f * _landed, 5, dt);
        _swirlSpeed = Ease.Approach(_swirlSpeed, _landed > 0 ? 0.8f : 0.3f, 1.2f, dt);
        if (!R.ReduceMotion) _swirl += _swirlSpeed * dt;
        _murk = MathF.Max(0, _murk - dt / 1.6f);
        _flash = MathF.Max(0, _flash - dt / 1.2f);
        _vesselHop.Update(dt);

        UpdateFlights(dt);
        foreach (var p in _pockets) { if (p.Delay > 0) p.Delay -= dt; else p.T += dt / 0.8f; }
        _pockets.RemoveAll(p => p.T >= 1);
        Ambient(dt);
    }

    private void UpdateHints()
    {
        int max = MaxHere;
        foreach (var j in _jars)
        {
            var s = SpotOf("jar:" + j.Id);
            s.Hint = _bench.Count >= max
                ? $"{j.Def.Description} (The vessel is full: mix, or take something out.)"
                : $"{j.Def.Description} Click to add it to the vessel.";
        }
        foreach (var tp in _tools)
        {
            var s = SpotOf("tool:" + tp.Def.Id);
            s.Hint = tp.Def.Id == _tool ? $"{tp.Def.Description} It's in your hand; click to set it down." : $"{tp.Def.Description} Click to take it up ({tp.Def.Verb}).";
        }
        var v = SpotOf("vessel");
        v.Hint = _bench.Count == 0 ? "Empty for now. Click a jar on the shelves to put something in." : $"Holding {string.Join(", ", _bench.Select(S.Content.ItemName))}. Click to look in and mix.";
        var tray = SpotOf("tray");
        if (_object is not null && S.World.Items.Get(_object) is { } o)
        {
            tray.Name = S.World.Items.Name(o);
            tray.Hint = "On the tray, ready to be worked with a tool. Click to take it off.";
        }
        else
        {
            tray.Name = "The brass tray";
            tray.Hint = "Set an odd object here to work it with the tools: click it in your satchel, or click here.";
        }
    }

    private Color LiquidTarget()
    {
        if (_landed == 0) return EmptyLiquid;
        float r = 0, g = 0, b = 0;
        int n = Math.Min(_landed, _bench.Count);
        for (int i = 0; i < n; i++)
        {
            var c = App.Art.ColorOf(S.Content.Item(_bench[i]));
            r += c.R; g += c.G; b += c.B;
        }
        if (n == 0) return EmptyLiquid;
        var mix = new Color((byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)255);
        return Palette.Lerp(mix, Palette.Hex(0x6A7A80), _murk * 0.7f);
    }

    private static Color LerpColor(Color a, Color b, float t) => Palette.Lerp(a, b, t);

    private void UpdateFlights(float dt)
    {
        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            var f = _flights[i];
            f.T += dt / f.Dur;
            if (f.T < 1) continue;
            _flights.RemoveAt(i);
            f.Land?.Invoke();
        }
    }

    private static Vector3 FlightPos(Flight f)
    {
        float t = Ease.InOutSine(f.T);
        return Vector3.Lerp(f.From, f.To, t) + new Vector3(0, MathF.Sin(MathF.PI * MathF.Min(1, f.T)) * f.Arc, 0);
    }

    private void Ambient(float dt)
    {
        if (R.ReduceMotion) return;
        var p = App.Particles;
        var mouth = VesselPos + new Vector3(0, 0.78f + _vesselHop.Value * 0.02f, 0);
        _steamT -= dt;
        if (_steamT <= 0)
        {
            _steamT = _landed > 0 ? 0.18f : 0.4f;
            p.Emit(mouth + new Vector3(p.R(-0.2f, 0.2f), 0.05f, p.R(-0.2f, 0.2f)), new Vector3(p.R(-0.08f, 0.08f), p.R(0.35f, 0.6f), p.R(-0.05f, 0.08f)),
                p.R(2.2f, 3.2f), 0.22f, Palette.Alpha(Palette.Lerp(Palette.Foam, _liquidColor, 0.25f), 0.3f), false, 0.38f, 0.25f);
        }
        _bubbleT -= dt;
        if (_bubbleT <= 0)
        {
            _bubbleT = _landed > 0 ? 0.09f : 0.35f;
            var at = VesselPos + new Vector3(p.R(-0.3f, 0.3f), _level + 0.02f, p.R(-0.3f, 0.3f));
            p.Emit(at, new Vector3(0, p.R(0.15f, 0.4f), 0), p.R(0.3f, 0.6f), p.R(0.05f, 0.09f), Palette.Alpha(Palette.Lerp(_liquidColor, Color.White, 0.55f), 0.8f), true, -0.05f, 1f);
            if (_landed > 0 && p.R(0, 1) < 0.08f) App.Audio.Play("bubble", 0.12f, p.R(0.8f, 1.2f));
        }
        // the burner's flame breathes a few sparks when it's working
        foreach (var tp in _tools)
            if (tp.Look == ToolLook.Burner && tp.Busy > 0.2f && p.R(0, 1) < 0.5f)
                p.Emit(tp.Pos + new Vector3(0, 0.55f, 0), new Vector3(p.R(-0.2f, 0.2f), p.R(0.4f, 0.9f), p.R(-0.2f, 0.2f)), 0.5f, 0.06f, Palette.LanternHot, true, -0.05f, 1f);
    }

    private void Burst(Vector3 at, Color c, int count, float speed = 2f)
    {
        if (R.ReduceMotion) { count /= 3; speed *= 0.4f; }
        App.Particles.Burst(at, c, count, speed, 1.0f);
    }

    // ───────────────────────── drawing the room ─────────────────────────

    protected override void DrawRoom()
    {
        float t = R.ReduceMotion ? 0 : R.Time;
        R.Draw(_furniture, Matrix4x4.Identity, Color.White, outlineWidth: 1.4f);
        R.Draw(_herbs, Matrix4x4.Identity, Color.White, sway: 0.6f, outlineWidth: 1.2f);
        float swing = MathF.Sin(t * 0.7f) * 0.03f;
        var lanternWorld = Matrix4x4.CreateTranslation(0, -1.65f, 0) * Matrix4x4.CreateRotationZ(swing) * Matrix4x4.CreateTranslation(LanternPos + new Vector3(0, 1.65f, 0));
        R.Draw(_lantern, lanternWorld, Color.White, emissive: 0.15f, outlineWidth: 1.2f);

        // the Discovery Map, blooming one node per discovery
        R.Draw(_mapBoard, Matrix4x4.Identity, Color.White, highlight: Rim("map"), outlineWidth: 1.2f);
        DrawMapNodes(t);
        // the formulary on its lectern
        R.Draw(_book, LecternTop * Matrix4x4.CreateTranslation(0, Lift("formulary"), 0), Color.White, highlight: Rim("formulary"), outlineWidth: 1.2f);

        // the vessel and what's in it
        float hop = _vesselHop.Value * 0.03f + Lift("vessel");
        var vw = Matrix4x4.CreateTranslation(VesselPos + new Vector3(0, hop, 0));
        R.Draw(_vessel, vw, Color.White, highlight: Rim("vessel"), outlineWidth: 1.6f);
        var liquidCol = Palette.Lerp(_liquidColor, _flashColor, _flash * 0.6f);
        R.Draw(_liquid, Matrix4x4.CreateRotationY(-_swirl) * Matrix4x4.CreateTranslation(VesselPos + new Vector3(0, _level + hop, 0)), liquidCol,
            emissive: 0.3f + _flash * 0.9f, outline: false);
        for (int i = 0; i < Math.Min(_landed, _bench.Count); i++)
        {
            float a = _swirl * 1.3f + i * MathF.Tau / Math.Max(1, _bench.Count);
            var bp = VesselPos + new Vector3(MathF.Cos(a) * 0.22f, _level + hop + 0.02f + MathF.Sin(t * 2.2f + i) * 0.015f, MathF.Sin(a) * 0.22f);
            R.Draw(_bit, Matrix4x4.CreateRotationY(a * 2) * Matrix4x4.CreateTranslation(bp), App.Art.ColorOf(S.Content.Item(_bench[i])), emissive: 0.2f, outlineWidth: 1);
        }

        foreach (var tp in _tools) DrawTool(tp, t);
        DrawObject(t);

        // jars: the opaque parts first, then all the glass
        foreach (var j in _jars) DrawJar(j, t, glass: false);
        foreach (var j in _jars) DrawJar(j, t, glass: true);

        // flights (drops into the vessel, discoveries up to the shelves)
        foreach (var f in _flights)
        {
            var p = FlightPos(f);
            R.Glow(p, f.Size * 2.2f, Palette.Alpha(f.Color, 0.85f));
            R.Glow(p, f.Size, Palette.Alpha(Color.White, 0.9f));
        }

        // lights you can see
        R.Glow(Vector3.Transform(new Vector3(0, -0.04f, 0), lanternWorld), 1.3f, Palette.Alpha(Palette.LanternHot, 0.55f));
        float fl = 1 + 0.12f * MathF.Sin(t * 11) + 0.08f * MathF.Sin(t * 6.3f);
        R.Glow(SconcePos + new Vector3(0, 0.08f, 0.06f), 0.42f * fl, Palette.Alpha(Palette.LanternHot, 0.8f));
        R.Glow(SconcePos + new Vector3(0, 0.08f, 0.06f), 0.13f * fl, Palette.Alpha(Color.White, 0.9f));
        R.Glow(VesselPos + new Vector3(0, 0.12f, 0.1f), 0.32f * fl, Palette.Alpha(Palette.Hex(0x9CC0FF), 0.55f)); // the spirit lamp under the pot
        if (_flash > 0.01f) R.Glow(VesselPos + new Vector3(0, _level + 0.2f, 0), 2.4f * _flash, Palette.Alpha(_flashColor, 0.6f * _flash));
        DrawMotes(t);
    }

    private float Lift(string spot) => HasSpot(spot) ? SpotOf(spot).Lift.Value * 0.03f : 0;

    private void DrawMapNodes(float t)
    {
        if (_total <= 0) return;
        var nodes = MapNodes(30);
        var recipes = S.Apothecary.KnownRecipes();
        int n = Math.Min(_total, nodes.Count);
        for (int i = 0; i < n; i++)
        {
            bool lit = i < _discovered;
            var col = lit ? (i < recipes.Count ? App.Art.ColorOf(S.Content.Item(recipes[i].Output)) : Palette.Lantern) : Palette.Hex(0xB8A88E);
            float pulse = lit ? 1 + 0.06f * MathF.Sin(t * 1.5f + i) : 0.8f;
            R.Draw(_node, Matrix4x4.CreateScale(pulse) * Matrix4x4.CreateTranslation(nodes[i]), col, emissive: lit ? 0.35f : 0, outline: lit, outlineWidth: 1);
        }
    }

    private void DrawJar(Jar j, float t, bool glass)
    {
        float appear = j.Waiting ? 0 : Ease.OutBack(j.Appear, 2.2f);
        if (appear <= 0.01f) return;
        var hl = Rim("jar:" + j.Id);
        float s = j.Scale * appear;
        float wob = j.Wobble.Value * 0.12f;
        float lift = MathF.Max(0, j.Hop.Value) * 0.04f + Lift("jar:" + j.Id) + (Hovered == "jar:" + j.Id ? 0.04f : 0);
        var world = Matrix4x4.CreateScale(s) * Matrix4x4.CreateRotationZ(wob) * Matrix4x4.CreateRotationY(0.25f) * Matrix4x4.CreateTranslation(j.Pos + new Vector3(0, lift, 0));
        var k = j.Keeper;
        if (!glass)
        {
            R.Draw(k.Tinted, world, j.Color, emissive: k.Emissive, highlight: hl, outline: k.Glass is null, outlineWidth: 1.2f);
            if (k.Plain is { } p) R.Draw(p, world, Color.White, highlight: hl, outlineWidth: 1.0f);
            return;
        }
        if (k.Glass is { } g) R.Draw(g, world, Palette.Alpha(Glass, 0.3f), highlight: hl, outlineWidth: 1.3f);
        if (k.Flame is { } f)
        {
            float fl = 1 + 0.15f * MathF.Sin(t * 12 + j.Id.Length) + 0.1f * MathF.Sin(t * 7.1f);
            var at = Vector3.Transform(f, world);
            R.Glow(at, 0.28f * fl * s, Palette.Alpha(Palette.LanternHot, 0.85f));
            R.Glow(at, 0.09f * fl * s, Palette.Alpha(Color.White, 0.9f));
        }
        if (k.Glow is { } gl) R.Glow(Vector3.Transform(gl, world), 0.5f * s, Palette.Alpha(j.Color, 0.35f));
    }

    private void DrawTool(ToolProp tp, float t)
    {
        var models = _toolModels[tp.Look];
        var id = "tool:" + tp.Def.Id;
        var hl = Rim(id);
        float lift = Lift(id) + tp.Sel * 0.06f;
        var basePos = tp.Pos + new Vector3(0, lift, 0);
        var world = Matrix4x4.CreateRotationY(-0.35f) * Matrix4x4.CreateTranslation(basePos);
        float em = tp.Sel * 0.12f;
        var ink = tp.Sel > 0.5f ? Palette.Lerp(Palette.Ink, Accent, 0.5f) : (Color?)null;
        if (tp.Sel > 0.02f) R.Glow(tp.Pos + new Vector3(0, 0.05f, 0.05f), 1.1f * tp.Sel, Palette.Alpha(Palette.LanternHot, 0.3f * tp.Sel));
        R.Draw(models.Body, world, Color.White, emissive: em, highlight: hl, outlineWidth: 1.3f + tp.Sel, ink: ink);
        switch (tp.Look)
        {
            case ToolLook.Mortar when models.Part is { } pestle:
            {
                float a = tp.Spin;
                var pw = Matrix4x4.CreateRotationZ(0.55f + MathF.Sin(t * 8) * 0.05f * tp.Busy) * Matrix4x4.CreateTranslation(0.06f, 0.1f, 0) * Matrix4x4.CreateRotationY(a) * world;
                R.Draw(pestle, pw, Color.White, emissive: em, highlight: hl, outlineWidth: 1.2f, ink: ink);
                break;
            }
            case ToolLook.Burner when models.Part is { } flame:
            {
                float fl = 1 + 0.15f * MathF.Sin(t * 13) + 0.1f * MathF.Sin(t * 7.3f + 1);
                float size = (0.8f + tp.Busy * 1.4f + tp.Sel * 0.3f) * fl;
                var fpos = basePos + new Vector3(0, 0.36f, 0);
                var fw = Matrix4x4.CreateScale(0.9f, size, 0.9f) * Matrix4x4.CreateTranslation(fpos);
                R.Draw(flame, fw, Palette.Lerp(Palette.Hex(0x6FA8FF), Palette.Hex(0xFFB060), tp.Busy), emissive: 1.1f, outline: false);
                R.Glow(fpos + new Vector3(0, 0.08f * size, 0), 0.5f * size, Palette.Alpha(Palette.Lerp(Palette.Hex(0x7FB0FF), Palette.LanternHot, 0.4f + tp.Busy * 0.6f), 0.6f));
                break;
            }
            case ToolLook.Scales when models.Part is { } beam && models.Part2 is { } pan:
            {
                float tilt = tp.Tilt.Value * 0.18f + (R.ReduceMotion ? 0 : MathF.Sin(t * 0.8f) * 0.015f);
                var pivot = new Vector3(0, 0.66f, 0);
                var bw = Matrix4x4.CreateRotationZ(tilt) * Matrix4x4.CreateTranslation(pivot) * world;
                R.Draw(beam, bw, Color.White, emissive: em, highlight: hl, outlineWidth: 1.1f, ink: ink);
                foreach (var side in new[] { -1f, 1f })
                {
                    var end = Vector3.Transform(new Vector3(side * 0.33f, 0, 0), Matrix4x4.CreateRotationZ(tilt)) + pivot;
                    var pw = Matrix4x4.CreateTranslation(end - new Vector3(0, 0.3f, 0)) * world;
                    R.Draw(pan, pw, Color.White, emissive: em, highlight: hl, outlineWidth: 1.0f, ink: ink);
                }
                break;
            }
            case ToolLook.Loupe when models.Glass is { } lens:
            {
                float glint = tp.Busy;
                R.Draw(lens, world, Palette.Alpha(Palette.Hex(0xD6EEF2), 0.55f), emissive: 0.2f + glint, outline: false);
                if (glint > 0.05f) R.Glow(Vector3.Transform(new Vector3(0.06f, 0.48f, 0.05f), world), 0.6f * glint, Palette.Alpha(Color.White, 0.8f * glint));
                break;
            }
        }
    }

    private void DrawObject(float t)
    {
        if (_object is null || S.World.Items.Get(_object) is not { } o) return;
        var def = S.Content.Item(o.Def);
        var col = App.Art.ColorOf(def);
        if (o.Inert) col = Palette.Lerp(col, Palette.InkFaint, 0.7f);
        float bob = R.ReduceMotion ? 0 : MathF.Sin(t * 1.6f) * 0.015f;
        var at = TrayPos + new Vector3(0, 0.04f + Lift("tray") + bob, 0);
        var world = Matrix4x4.CreateScale(1.25f) * Matrix4x4.CreateRotationY(t * 0.25f + 0.4f) * Matrix4x4.CreateTranslation(at);
        R.Draw(ObjectModel(o.Def), world, col, emissive: o.Essence is not null ? 0.18f : 0.04f, highlight: Rim("tray"), outlineWidth: 1.4f);
        if (o.Essence is not null && !o.Inert)
            R.Glow(at + new Vector3(0, 0.15f, 0), 0.75f + 0.08f * MathF.Sin(t * 2), Palette.Alpha(Palette.Lerp(col, Palette.LanternHot, 0.5f), 0.35f));
    }

    private void DrawMotes(float t)
    {
        // dust turning slowly in the light from the window
        for (int i = 0; i < 26; i++)
        {
            float u = i * 0.618f % 1, v = i * 0.371f % 1;
            float y = 0.6f + (v * 3.6f + t * 0.05f * (0.5f + u)) % 3.6f;
            var p = new Vector3(-4.9f + u * 3.4f + MathF.Sin(t * 0.3f + i) * 0.2f, y, -2.6f + v * 2.4f + MathF.Cos(t * 0.23f + i * 1.7f) * 0.2f);
            float a = 0.25f + 0.2f * MathF.Sin(t * 0.9f + i * 2.1f);
            R.Glow(p, 0.06f, Palette.Alpha(Palette.LanternHot, MathF.Max(0.05f, a)));
        }
    }

    // ───────────────────────── clicking furniture ─────────────────────────

    protected override void OnSpot(string id)
    {
        if (id.StartsWith("jar:", StringComparison.Ordinal)) { AddElement(id[4..]); return; }
        if (id.StartsWith("tool:", StringComparison.Ordinal)) { ToggleTool(id[5..]); return; }
        switch (id)
        {
            case "vessel":
                App.Audio.Play("bubble", 0.5f);
                _vesselHop.Kick(R.ReduceMotion ? 0 : 1.5f);
                if (_card == Card.Bench) CloseCard(); else OpenCard(Card.Bench);
                break;
            case "tray":
                if (_object is not null) TakeObjectOff();
                else ChooseObject();
                break;
            case "map": OpenCard(Card.Map); break;
            case "formulary": OpenCard(Card.Formulary); break;
            case "bottles": ChooseBottle(); break;
        }
    }

    private void AddElement(string element)
    {
        if (!_jarById.TryGetValue(element, out var jar)) return;
        if (_bench.Count >= MaxHere)
        {
            App.Audio.Play("hm", 0.5f);
            _vesselHop.Kick(R.ReduceMotion ? 0 : -1.5f);
            jar.Wobble.Kick(R.ReduceMotion ? 0 : 3);
            App.Toasts.Add("", $"The vessel holds {MaxHere} things at once{(_object is null ? "" : " beside the object on the tray")}. Mix, or take something out.", NoticeKind.Info, LineKind.Soft);
            return;
        }
        _bench.Add(element);
        App.Audio.Play("clink", 0.45f, 1.05f);
        jar.Hop.Kick(R.ReduceMotion ? 0 : 2.4f);
        int count = _bench.Count;
        _flights.Add(new Flight
        {
            From = jar.Pos + new Vector3(0, 0.55f * jar.Scale, 0), To = VesselPos + new Vector3(0, _level + 0.1f, 0), Color = jar.Color, Dur = 0.55f, Arc = 0.8f,
            Land = () =>
            {
                _landed = Math.Min(_bench.Count, _landed + 1);
                Splash(jar.Color);
            },
        });
        if (_card != Card.Bench) OpenCard(Card.Bench);
    }

    private void Splash(Color c)
    {
        App.Audio.Play("bubble", 0.5f);
        App.Audio.Play("splash", 0.18f, 1.4f);
        _vesselHop.Kick(R.ReduceMotion ? 0 : 1.2f);
        var p = App.Particles;
        var at = VesselPos + new Vector3(0, _level + 0.08f, 0);
        int n = R.ReduceMotion ? 3 : 10;
        for (int i = 0; i < n; i++)
            p.Emit(at, new Vector3(p.R(-0.6f, 0.6f), p.R(0.9f, 1.8f), p.R(-0.6f, 0.6f)), 0.55f, p.R(0.05f, 0.09f), Palette.Lerp(c, Color.White, 0.3f), false, 0, 0.5f, -5f);
    }

    private void RemoveElementAt(int i)
    {
        if (i < 0 || i >= _bench.Count) return;
        _bench.RemoveAt(i);
        _landed = Math.Min(_landed, _bench.Count);
        App.Audio.Play("slide", 0.35f, 1.3f);
        _vesselHop.Kick(R.ReduceMotion ? 0 : -0.8f);
    }

    private void ToggleTool(string id)
    {
        var tp = _tools.FirstOrDefault(t => t.Def.Id == id);
        if (tp is null) return;
        if (_tool == id) { _tool = null; App.Audio.Play("tock", 0.5f, 0.9f); }
        else
        {
            _tool = id;
            ToolCue(tp, 0.45f);
            if (tp.Look == ToolLook.Scales) tp.Tilt.Kick(R.ReduceMotion ? 0 : 3);
        }
        if (_card != Card.Bench) OpenCard(Card.Bench);
    }

    private void SetTool(string? id)
    {
        if (id is null) { _tool = null; return; }
        ToggleTool(id);
        _tool = id;
    }

    private void ToolCue(ToolProp tp, float vol)
    {
        switch (tp.Look)
        {
            case ToolLook.Mortar: App.Audio.Play("grind", vol); break;
            case ToolLook.Burner: App.Audio.Play("whoosh", vol); break;
            case ToolLook.Scales: App.Audio.Play("creak", vol * 0.8f); break;
            case ToolLook.Loupe: App.Audio.Play("glass", vol); break;
            case ToolLook.Still: App.Audio.Play("bubble", vol * 1.2f, 0.8f); break;
            default: App.Audio.Play("clink", vol); break;
        }
    }

    /// <summary>Sets the chosen tool to work: the pestle grinds, the flame flares, the scales tip, the glass glints.</summary>
    private void WorkTool()
    {
        var tp = _tools.FirstOrDefault(t => t.Def.Id == _tool);
        if (tp is null) return;
        ToolCue(tp, 0.6f);
        tp.Busy = 1;
        if (tp.Look == ToolLook.Scales) tp.Tilt.Kick(R.ReduceMotion ? 0 : -4);
    }

    // ───────────────────────── the satchel, the tray and the bottles ─────────────────────────

    private static bool Objectish(ItemInstance i) => i.Essence is not null || i.RemnantOf is not null;

    public override bool UseItem(ItemInstance item)
    {
        if (!Objectish(item)) return false;
        if (_object == item.Id) TakeObjectOff();
        else PlaceObject(item);
        return true;
    }

    private void PlaceObject(ItemInstance item)
    {
        _object = item.Id;
        while (_bench.Count > S.Apothecary.MaxInputs - 1) _bench.RemoveAt(_bench.Count - 1);
        _landed = Math.Min(_landed, _bench.Count);
        App.Audio.Play("slide", 0.5f);
        SpotOf("tray").Lift.Kick(R.ReduceMotion ? 0 : 3f);
        var p = App.Particles;
        if (!R.ReduceMotion)
            for (int i = 0; i < 8; i++)
                p.Emit(TrayPos + new Vector3(0, 0.06f, 0), new Vector3(p.R(-0.7f, 0.7f), p.R(0.1f, 0.3f), p.R(-0.7f, 0.7f)), 0.7f, 0.12f, Palette.Alpha(Palette.Parchment, 0.5f), false, 0.2f, 2f);
        if (_card != Card.Bench) OpenCard(Card.Bench);
    }

    private void TakeObjectOff()
    {
        if (_object is null) return;
        if (S.World.Items.Get(_object) is { } o) FlyToSatchel(S.Content.Item(o.Def), TrayPos + new Vector3(0, 0.2f, 0));
        _object = null;
        App.Audio.Play("slide", 0.4f, 1.2f);
    }

    private void ChooseObject()
    {
        var items = S.World.Items.Satchel.Where(Objectish).ToList();
        if (items.Count == 0)
        {
            App.Audio.Play("hm", 0.4f);
            App.Toasts.Add("", "You're not carrying anything odd just now. Things that turn up can be set here and worked with the tools.", NoticeKind.Info, LineKind.Soft);
            return;
        }
        var entries = items.Select(i => new Picker<ItemInstance>.Entry(i, S.World.Items.Name(i), S.World.Items.Description(i), App.Art.ColorOf(S.Content.Item(i.Def)))).ToList();
        App.Open(new Picker<ItemInstance>("Put which object on the bench?", entries, PlaceObject));
    }

    private void ChooseBottle()
    {
        var portable = S.Apothecary.Known().Where(k => k.Portable).ToList();
        if (portable.Count == 0)
        {
            App.Audio.Play("hm", 0.4f);
            App.Toasts.Add("", "Nothing on the shelves will keep in a jar just yet.", NoticeKind.Info, LineKind.Soft);
            return;
        }
        App.Audio.Play("clink", 0.35f, 0.9f);
        var entries = portable.Select(k => new Picker<ItemResource>.Entry(k, k.Name, BottleNote(k), App.Art.ColorOf(k))).ToList();
        App.Open(new Picker<ItemResource>("Bottle which element?", entries, Bottle, "A stoppered measure to carry with you. There's always more on the shelf."));
    }

    private static string? BottleNote(ItemResource k) =>
        k.PoolEnergy is { } e && !e.IsZero ? $"In a pool: light {Signed(e.Light)}, heat {Signed(e.Heat)}" : k.FoodValue > 0 ? "Pond creatures will eat this." : null;

    private static string Signed(int n) => n > 0 ? "+" + n : n.ToString();

    private void Bottle(ItemResource k)
    {
        var r = S.Apothecary.Bottle(k.Id);
        App.Audio.Play("clink", 0.6f, 1.2f);
        if (r.Ok && _jarById.TryGetValue(k.Id, out var jar))
        {
            jar.Hop.Kick(R.ReduceMotion ? 0 : 3);
            Burst(jar.Pos + new Vector3(0, 0.4f, 0), jar.Color, 10, 1.2f);
            FlyToSatchel(k, jar.Pos + new Vector3(0, 0.3f, 0));
        }
        App.Show(r, "At the bench");
    }

    private void FlyToSatchel(ItemResource def, Vector3 from, float delay = 0)
    {
        var s = Raylib.GetWorldToScreen(from, Camera);
        _pockets.Add(new Pocket { Def = def, From = (s - Input.UiOffset) / Input.UiScale, Delay = delay });
    }

    // ───────────────────────── mixing and working ─────────────────────────

    private void Work()
    {
        if (_object is null) { Mix(); return; }
        var preview = S.Apothecary.PreviewExperiment(_object, _bench.ToList(), _tool);
        if (preview is not null)
        {
            var text = preview.Derivation.Confirm.Length > 0 ? preview.Derivation.Confirm
                : $"This will change it into {preview.ProducesName}" + (preview.RemnantName is null ? "." : $", leaving {preview.RemnantName} behind.");
            App.Audio.Play("hm", 0.35f, 1.2f);
            App.Open(new Confirm("This will change it", text, () => Experiment(preview), "Go ahead", "Not yet", Accent));
            return;
        }
        Experiment(null);
    }

    private void Mix()
    {
        if (_bench.Count == 0) return;
        int before = _jars.Count;
        var r = S.Apothecary.Mix(_bench.ToList(), _tool);
        WorkTool();
        _swirlSpeed += R.ReduceMotion ? 0 : 10;
        _vesselHop.Kick(R.ReduceMotion ? 0 : 2.5f);
        var fresh = SyncJars(fromVessel: true);
        var mouth = VesselPos + new Vector3(0, 0.95f, 0);
        if (fresh.Count > 0)
        {
            _flash = 1;
            _flashColor = fresh[0].Color;
            Burst(mouth, fresh[0].Color, 30, 2.4f);
            Burst(mouth, Palette.LanternHot, 16, 1.6f);
        }
        else if (r.Ok)
        {
            App.Audio.Play("bubble", 0.6f, 1.2f);
            _flash = 0.5f;
            _flashColor = _liquidColor;
            Burst(mouth, Palette.Lerp(_liquidColor, Color.White, 0.4f), 10, 1.2f);
        }
        else
        {
            // a dud: a polite fizz and a little grey puff, nothing lost
            App.Audio.Play("fizz", 0.7f);
            _murk = 1;
            var p = App.Particles;
            int n = R.ReduceMotion ? 3 : 12;
            for (int i = 0; i < n; i++)
                p.Emit(mouth + new Vector3(p.R(-0.2f, 0.2f), 0, p.R(-0.2f, 0.2f)), new Vector3(p.R(-0.3f, 0.3f), p.R(0.4f, 1.0f), p.R(-0.3f, 0.3f)),
                    p.R(0.8f, 1.4f), p.R(0.15f, 0.3f), Palette.Alpha(Palette.Hex(0xB8B4AC), 0.45f), false, 0.3f, 1.2f);
        }
        App.Show(r, "At the bench");
        _bench.Clear();
        _landed = 0;
        RefreshCounts();
    }

    private void Experiment(Atrium.Core.Locales.DerivationPreview? preview)
    {
        if (_object is null || S.World.Items.Get(_object) is not { } obj) return;
        var col = App.Art.ColorOf(S.Content.Item(obj.Def));
        var r = S.Apothecary.Experiment(_object, _bench.ToList(), _tool);
        WorkTool();
        SpotOf("tray").Lift.Kick(R.ReduceMotion ? 0 : 2f);
        if (preview is not null && S.World.Items.Get(_object) is null)
        {
            // the essence moved: the new form (and any remnant) go into the satchel
            App.Audio.Play("shimmer", 0.45f);
            Burst(TrayPos + new Vector3(0, 0.25f, 0), col, 26, 1.8f);
            Burst(TrayPos + new Vector3(0, 0.25f, 0), Palette.LanternHot, 12, 1.2f);
            FlyToSatchel(S.Content.Item(preview.Derivation.Produces), TrayPos + new Vector3(0, 0.2f, 0));
            if (preview.Derivation.Remnant is { } rem) FlyToSatchel(S.Content.Item(rem), TrayPos + new Vector3(0.1f, 0.15f, 0), 0.25f);
            _object = null;
            _bench.Clear();
            _landed = 0;
        }
        else Burst(TrayPos + new Vector3(0, 0.25f, 0), Palette.Lerp(col, Palette.LanternHot, 0.5f), 8, 0.8f);
        App.Show(r, "At the bench");
        RefreshCounts();
    }

    private void ClearBench()
    {
        bool had = _bench.Count > 0;
        _bench.Clear();
        _landed = 0;
        _tool = null;
        if (_object is not null) TakeObjectOff();
        if (had)
        {
            App.Audio.Play("splash", 0.3f, 0.8f);
            _vesselHop.Kick(R.ReduceMotion ? 0 : -1.2f);
        }
        else App.Audio.Play("tock", 0.4f);
    }

    public override bool Back()
    {
        if (_card != Card.None) { CloseCard(); return true; }
        return base.Back();
    }
}
