using System.Numerics;
using Atrium.Core.Content;
using Atrium.Core.Runtime;
using Atrium.Diorama.App;
using Atrium.Diorama.Overlays;
using Atrium.Diorama.Render;
using Atrium.Diorama.UI;
using Raylib_cs;
using LifeMood = Atrium.Core.Runtime.Mood;

namespace Atrium.Diorama.Scenes;

/// <summary>
/// The Spirit Pools: the Stillkeepers' greenhouse grotto. One stone-rimmed glass tank stands for each pool in the
/// content, and every lifeform in it swims, creeps, sways or hovers there; anything laid in a pool rests on its sand.
/// The water clouds as a pool gets dirty and takes on its energy (gold for Light, indigo for Dark, a warm or cold cast
/// for Heat). Click a tank to tend it on a working card; the holding crock, the druids' bag, the breeding ledger and the
/// stool are furniture too. Every rule lives in <c>PoolsLocale</c>; this file only draws and calls it.
/// </summary>
public sealed partial class PoolsScene : InteriorScene
{
    protected override string LocaleId => "pools";

    // Tank geometry, local to each tank (origin on the floor under its centre, front facing +Z).
    // the pools are natural rock basins: water from FloorY to SurfY inside a mossy rim (local to each pool)
    private const float FloorY = 0.1f, RimY = 0.78f, SurfY = 0.62f;
    // the underground river along the front, where the water leaves the grotto (the kit carves its channel)
    private const float RiverZ = 4.55f, RiverY = -0.22f, RiverW = 11.4f, RiverD = 1.45f;
    /// <summary>A pool's oval: big enough to hold the rectangle the creatures swim in.</summary>
    private static float PoolRx(float w) => w / 2 * 1.42f + 0.05f;
    private static float PoolRz(float d) => d / 2 * 1.42f + 0.05f;

    /// <summary>
    /// Where the pools lie, in content order (front-end layout data): the first under the falls' cascade on a rock
    /// terrace at the back, the second among Old Stoic's roots on the left, the third a little raised on the right.
    /// Extra pools line up along the front.
    /// </summary>
    private static readonly (Vector3 Pos, float Yaw)[] Slots =
    {
        (new Vector3(0.75f, 0.9f, -3.55f), 0f),
        (new Vector3(-2.75f, 0, -0.35f), 0.45f),
        (new Vector3(3.45f, 0.3f, -0.55f), -0.1f),
        (new Vector3(-0.2f, 0, 2.6f), 0.12f),
    };

    private static readonly Vector3 CrockPos = new(-1.75f, 0, 2.65f);
    private static readonly Vector3 BagPos = new(4.05f, 0, 2.35f);
    private static readonly Vector3 LedgerPos = new(-4.35f, 0, -3.2f);
    private static readonly Vector3 StoolPos = new(1.25f, 0, 0.55f);
    private const string SpotHolding = "holding", SpotBag = "bag", SpotLedger = "ledger", SpotStool = "stool";

    private sealed class Tank
    {
        public string Id = "";
        public int Index;
        public Vector3 Pos;
        public float Yaw, W, D;
        public Model Stone, Surface;
        public Matrix4x4 World = Matrix4x4.Identity;
        public float Murk = -1, FeedT = 99, CleanseT = 99, BubbleT, RippleT, SteamT;
        public Vector2 FeedAt;
        public Energy Energy;
        public double Clarity;
        public readonly List<Bubble> Bubbles = new();
        public readonly List<Ripple> Ripples = new();
        public string Spot => "tank:" + Id;
        public Vector3 ToWorld(Vector3 local) => Vector3.Transform(local, World);
        public Vector3 Lantern => new(PoolRx(W) * 0.82f, 1.32f, -PoolRz(D) * 0.62f);
    }

    private sealed class Bubble { public Vector3 P; public float V, Wob, Size; }
    private sealed class Ripple { public Vector2 P; public float Age, Max = 0.32f; }

    private sealed class Critter
    {
        public int Id;
        public string? Pool;       // null: resting in the holding crock
        public string Species = "";
        public LifeformKind Kind;
        public Mood Mood;
        public bool Eats, Expecting;
        public Vector3 P, Target;
        public float Yaw, Phase, Retarget, Pop = 1, Seed;
    }

    private sealed class Laid
    {
        public string Id = "", Def = "", Pool = "";
        public Vector3 P;
        public float Drop = 1;
        public int Copies = 1;
    }

    private readonly List<Tank> _tanks = new();
    private readonly Dictionary<string, Tank> _tankOf = new();
    private readonly Dictionary<int, Critter> _critters = new();
    private readonly HashSet<int> _knownLifeforms = new();
    private readonly Dictionary<string, Laid> _laid = new();
    private readonly List<(Vector3 Pos, float K)> _lamps = new();
    private readonly List<Vector3> _dust = new();
    private readonly Random _rng = new(1310);
    private bool _synced;
    private float _time, _trickleT;
    private Vector3 _trickleEnd;
    private Tank? _trickleTank;
    private Vector3 _home;
    private float _homeDist;

    private Part _decor, _ferns, _crock, _bag, _ledger, _stool, _rake;
    private Model _fishBody, _fishTail, _snailShell, _snailFoot, _plantBlades, _plantMound;
    private Model _crystal, _orb, _pebble, _jar, _crockWater, _lanternGlow, _trickle, _spill, _river;

    private AxisResource LightAxis => S.Content.Axis(nameof(Energy.Light).ToLowerInvariant());
    private AxisResource HeatAxis => S.Content.Axis(nameof(Energy.Heat).ToLowerInvariant());

    private float Rand(float a, float b) => a + _rng.NextSingle() * (b - a);

    // ───────────────────────── construction ─────────────────────────

    protected override void Build()
    {
        CamTarget = new Vector3(0.35f, 1.25f, -0.7f);
        CamYaw = 0.3f; CamPitch = 0.46f; CamDist = 13.4f; CamFov = 36;
        _home = CamTarget; _homeDist = CamDist;

        var toon = R.Shaders.Toon;
        _decor = Prop("decor", BuildDecor);
        _ferns = Prop("ferns", BuildFerns);
        _fishBody = Own(BuildFishBody().Model(toon));
        _fishTail = Own(BuildFishTail().Model(toon));
        _snailShell = Own(new MeshBuilder().Blob(new Vector3(0, 0.075f, 0), new Vector3(0.085f, 0.08f, 0.075f), 3, 7, Color.White, 7, 0.06f, Palette.Hex(0xE8E0D0)).Model(toon));
        _snailFoot = Own(new MeshBuilder().Blob(new Vector3(0.03f, 0.022f, 0), new Vector3(0.13f, 0.028f, 0.05f), 2, 6, Palette.Hex(0xE6DCC6), 8, 0.05f)
            .Box(new Vector3(0.14f, 0.07f, 0.02f), new Vector3(0.012f, 0.07f, 0.012f), Palette.Hex(0xD8CDB6))
            .Box(new Vector3(0.14f, 0.07f, -0.02f), new Vector3(0.012f, 0.07f, 0.012f), Palette.Hex(0xD8CDB6)).Model(toon));
        _plantBlades = Own(BuildPlantBlades().Model(toon));
        _plantMound = Own(new MeshBuilder().Blob(new Vector3(0, 0.03f, 0), new Vector3(0.17f, 0.07f, 0.14f), 2, 7, Color.White, 9, 0.18f).Model(toon));
        _crystal = Own(new MeshBuilder().Cylinder(Vector3.Zero, 0.03f, 0.075f, 0.06f, 6, Color.White).Cone(new Vector3(0, 0.06f, 0), 0.075f, 0.17f, 6, Color.White).Model(toon));
        _orb = Own(new MeshBuilder().Blob(new Vector3(0, 0.085f, 0), new Vector3(0.09f, 0.085f, 0.09f), 3, 8, Color.White, 2, 0.04f).Model(toon));
        _pebble = Own(new MeshBuilder().Blob(new Vector3(0, 0.045f, 0), new Vector3(0.13f, 0.05f, 0.1f), 2, 7, Color.White, 3, 0.15f).Model(toon));
        _jar = Own(new MeshBuilder().Cylinder(Vector3.Zero, 0.07f, 0.06f, 0.16f, 7, Color.White).Cylinder(new Vector3(0, 0.16f, 0), 0.04f, 0.045f, 0.05f, 6, Palette.Hex(0xC8B49A)).Model(toon));
        _crock = Prop("crock", BuildCrock);
        _crockWater = Own(WaterDisc(0.5f, 14).Model(R.Shaders.Water));
        _bag = Prop("bag", BuildBag);
        _ledger = Prop("ledger", BuildLedger);
        _stool = Prop("stool", BuildStool);
        _lanternGlow = Own(new MeshBuilder().Box(Vector3.Zero, new Vector3(0.16f, 0.22f, 0.16f), Palette.LanternHot).Model(toon));
        _rake = Prop("rake", BuildHandRake);

        LayoutTanks(toon);

        AddSpot(SpotHolding, CrockPos + new Vector3(-0.8f, 0, -0.75f), CrockPos + new Vector3(0.95f, 1.1f, 0.75f),
            Cap(S.Content.Config.Pools.HoldingName), "");
        AddSpot(SpotBag, BagPos + new Vector3(-0.55f, 0, -0.45f), BagPos + new Vector3(0.55f, 1.35f, 0.45f), "The druids' bag", "");
        AddSpot(SpotLedger, LedgerPos + new Vector3(-0.5f, 0, -0.5f), LedgerPos + new Vector3(0.55f, 1.6f, 0.55f),
            "The breeding ledger", "Every kind you've kept, and every pairing you've seen work.");
        AddSpot(SpotStool, StoolPos + new Vector3(-0.38f, 0, -0.38f), StoolPos + new Vector3(0.38f, 0.62f, 0.38f),
            "Sit and watch the water", "Let a little while pass by the tanks (Space).");

        for (int i = 0; i < 26; i++) _dust.Add(new Vector3(Rand(-4.8f, 4.8f), Rand(1.2f, 4.6f), Rand(-4.8f, 2.5f)));
        SyncState();
    }

    private void LayoutTanks(Shader toon)
    {
        var views = S.Pools.Pools();
        for (int i = 0; i < views.Count; i++)
        {
            var p = views[i].Pool;
            var slot = i < Slots.Length ? Slots[i] : (new Vector3(-3.6f + (i - Slots.Length) * 3.2f, 0, 4.2f), 0f);
            float w = Math.Clamp(1.3f + 0.34f * p.Capacity, 2.3f, 4.1f);
            float d = Math.Clamp(w * 0.4f, 1.3f, 1.65f);
            var t = new Tank { Id = p.Id, Index = i, Pos = slot.Item1, Yaw = slot.Item2, W = w, D = d };
            t.Stone = Own(BuildBasin(w, d, slot.Item1.Y, i).Model(toon));
            t.Surface = Own(WaterOval(PoolRx(w), PoolRz(d)).Model(R.Shaders.Water));
            t.World = Matrix4x4.CreateRotationY(t.Yaw) * Matrix4x4.CreateTranslation(t.Pos);
            t.FeedAt = new Vector2(0, 0);
            _tanks.Add(t);
            _tankOf[p.Id] = t;

            // a hotspot box around the whole tank (axis-aligned around its rotated corners)
            var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
            float rx = PoolRx(w) + 0.15f, rz = PoolRz(d) + 0.15f;
            foreach (var c in new[] { new Vector3(-rx, 0, -rz), new Vector3(rx, 0, -rz), new Vector3(-rx, RimY + 0.15f, rz), new Vector3(rx, RimY + 0.15f, rz) })
            {
                var wc = t.ToWorld(c);
                min = Vector3.Min(min, wc); max = Vector3.Max(max, wc);
            }
            min.Y = t.Pos.Y; max.Y = t.Pos.Y + RimY + 0.15f;
            AddSpot(t.Spot, min, max, Cap(p.Name), "");
            _lamps.Add((t.ToWorld(t.Lantern), 1f));
        }
        _lamps.Add((LedgerPos + new Vector3(0.2f, 1.75f, 0.1f), 0.55f));
        _lamps.Add((CrockPos + new Vector3(0.4f, 2.2f, 0.3f), 0.35f));

        // the trickle: a thread of the falls, piped through the back wall, feeding the first tank
        if (_tanks.Count > 0)
        {
            var t0 = _tanks[0];
            _trickleTank = t0;
            var endLocal = new Vector3(-t0.W * 0.28f, SurfY, -t0.D / 2 + 0.22f);
            _trickleEnd = t0.ToWorld(endLocal);
            var from = new Vector3(_trickleEnd.X, 5.4f, -5.3f); // the crack high in the back wall
            _trickle = Own(BuildTrickle(from, _trickleEnd).Model(R.Shaders.Falls));
        }
        // the first pool spills over its lip into the second, and the water leaves along the front as a river
        _spill = Own(new MeshBuilder().Model(toon));
        if (_tanks.Count > 1)
        {
            var (a, b) = (_tanks[0], _tanks[1]);
            var dir = Vector3.Normalize(new Vector3(b.Pos.X - a.Pos.X, 0, b.Pos.Z - a.Pos.Z));
            float edge = 1 / MathF.Sqrt(MathF.Pow(dir.X / PoolRx(a.W), 2) + MathF.Pow(dir.Z / PoolRz(a.D), 2));
            var lip = a.Pos + dir * edge + new Vector3(0, SurfY, 0);
            var land = b.Pos - dir * 0.9f + new Vector3(0, SurfY, 0);
            if (lip.Y > land.Y + 0.2f) _spill = Own(BuildTrickle(lip, land).Model(R.Shaders.Falls));
        }
        _river = Own(WaterRect(RiverW, RiverD, 24, 3).Model(R.Shaders.Water));
    }

    /// <summary>The grotto itself: flagstones, rough walls, arched greenhouse panes, ivy.</summary>
    protected override MeshBuilder BuildShell()
    {
        var m = new MeshBuilder();
        var rng = new Random(11);
        float R01() => (float)rng.NextDouble();
        m.Box(new Vector3(0, -0.13f, 0), new Vector3(11.2f, 0.2f, 11.2f), Palette.Hex(0x6E6458));
        var flag = Palette.Hex(0xBDB29C);
        for (int i = -5; i <= 5; i++)
            for (int j = -5; j <= 5; j++)
            {
                var c = Palette.Mul(Palette.Lerp(flag, Palette.Hex(0x9DAE98), R01() * 0.4f), 0.88f + R01() * 0.16f);
                m.Box(new Vector3(i + (R01() - 0.5f) * 0.05f, -0.02f, j + (R01() - 0.5f) * 0.05f), new Vector3(0.93f, 0.06f, 0.93f), c);
            }
        var wall = Palette.Lerp(Palette.Hex(0xC7BBA2), Accent, 0.07f);
        m.Box(new Vector3(0, 3.2f, -5.6f), new Vector3(11.2f, 6.6f, 0.4f), wall);
        m.Box(new Vector3(-5.6f, 3.2f, 0), new Vector3(0.4f, 6.6f, 11.2f), Palette.Mul(wall, 0.92f));
        // rough stone courses standing proud of the walls
        for (int row = 0; row < 9; row++)
        {
            float y = 0.35f + row * 0.72f;
            float x = -5.3f + (row % 2) * 0.6f;
            while (x < 5.4f)
            {
                float bw = 0.7f + R01() * 0.7f;
                if (y > 2.5f && y < 5.7f && WindowAt(x + bw / 2)) { x += bw + 0.06f; continue; }
                m.Box(new Vector3(x + bw / 2, y, -5.38f), new Vector3(bw, 0.62f, 0.06f), Palette.Mul(wall, 0.9f + R01() * 0.14f));
                x += bw + 0.06f;
            }
            float z = -5.2f + (row % 2) * 0.5f;
            while (z < 5.4f)
            {
                float bw = 0.7f + R01() * 0.7f;
                bool win = y > 2.3f && y < 5.0f && z > -2.8f && z < -0.2f;
                if (!win) m.Box(new Vector3(-5.38f, y, z + bw / 2), new Vector3(0.06f, 0.62f, bw), Palette.Mul(wall, 0.84f + R01() * 0.12f));
                z += bw + 0.06f;
            }
        }
        m.Box(new Vector3(0, 0.12f, -5.33f), new Vector3(11f, 0.24f, 0.16f), Palette.Hex(0x8E9A7E)); // a damp mossy skirting
        m.Box(new Vector3(-5.33f, 0.12f, 0), new Vector3(0.16f, 0.24f, 11f), Palette.Hex(0x8E9A7E));

        // arched greenhouse panes on the back wall
        var glass = Palette.Hex(0xEAF1DE);
        var iron = Palette.Hex(0x3F4A44);
        foreach (var cx in WindowXs)
        {
            ArchPane(m, cx, 2.7f, 1.5f, 2.2f, glass, -5.36f);
            m.Box(new Vector3(cx, 2.7f + 1.1f + 0.3f, -5.33f), new Vector3(0.06f, 3.0f, 0.05f), iron);
            for (int k = 0; k < 3; k++) m.Box(new Vector3(cx, 3.2f + k * 0.75f, -5.33f), new Vector3(1.5f, 0.05f, 0.05f), iron);
            m.Box(new Vector3(cx, 2.62f, -5.25f), new Vector3(1.75f, 0.12f, 0.3f), Palette.Mul(wall, 0.85f)); // sill
        }
        // the left window, letting in the courtyard's light
        m.Box(new Vector3(-5.37f, 3.6f, -1.5f), new Vector3(0.06f, 2.4f, 1.9f), glass);
        m.Box(new Vector3(-5.33f, 3.6f, -1.5f), new Vector3(0.05f, 2.5f, 0.08f), iron);
        m.Box(new Vector3(-5.33f, 3.6f, -1.5f), new Vector3(0.05f, 0.08f, 2.0f), iron);
        m.Box(new Vector3(-5.25f, 2.36f, -1.5f), new Vector3(0.3f, 0.12f, 2.1f), Palette.Mul(wall, 0.85f));

        // ivy trailing down from the top of the back wall
        var leaf = new[] { Palette.Hex(0x6E9F5E), Palette.Hex(0x5C8C4E), Palette.Hex(0x86B070) };
        for (int s = 0; s < 9; s++)
        {
            float x = -5.0f + s * 1.22f + R01() * 0.4f;
            int n = 5 + rng.Next(9);
            for (int k = 0; k < n; k++)
            {
                var p = new Vector3(x + MathF.Sin(k * 1.3f + s) * 0.14f, 6.4f - k * 0.26f, -5.3f);
                m.Blob(p, new Vector3(0.13f, 0.1f, 0.06f), 2, 5, leaf[(k + s) % 3], s * 31 + k, 0.2f);
            }
        }
        for (int s = 0; s < 6; s++)
        {
            float z = -4.8f + s * 1.6f + R01() * 0.4f;
            int n = 4 + rng.Next(7);
            for (int k = 0; k < n; k++)
                m.Blob(new Vector3(-5.3f, 6.4f - k * 0.26f, z + MathF.Sin(k * 1.1f + s) * 0.14f), new Vector3(0.06f, 0.1f, 0.13f), 2, 5, leaf[(k + s) % 3], 400 + s * 31 + k, 0.2f);
        }
        // moss along the damp foot of the walls
        for (int i = 0; i < 16; i++)
            m.Blob(new Vector3(-5.2f + i * 0.68f, 0.06f, -5.22f + R01() * 0.1f), new Vector3(0.3f, 0.12f, 0.16f), 2, 6, Palette.Mul(Palette.Moss, 0.85f + R01() * 0.2f), 600 + i, 0.25f);
        for (int i = 0; i < 12; i++)
            m.Blob(new Vector3(-5.22f + R01() * 0.1f, 0.06f, -4.9f + i * 0.85f), new Vector3(0.16f, 0.12f, 0.3f), 2, 6, Palette.Mul(Palette.Moss, 0.85f + R01() * 0.2f), 700 + i, 0.25f);
        return m;
    }

    private static readonly float[] WindowXs = { -3.4f, 0.75f, 4.3f };
    private static bool WindowAt(float x) => WindowXs.Any(c => MathF.Abs(x - c) < 1.0f);

    private static void ArchPane(MeshBuilder m, float cx, float y0, float w, float h, Color col, float z)
    {
        float x0 = cx - w / 2, x1 = cx + w / 2, y1 = y0 + h - w / 2;
        m.Quad(new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x0, y1, z), col);
        var c = new Vector3(cx, y1, z);
        const int n = 10;
        for (int i = 0; i < n; i++)
        {
            float a0 = MathF.PI * i / n, a1 = MathF.PI * (i + 1) / n;
            m.Tri(c, c + new Vector3(MathF.Cos(a0), MathF.Sin(a0), 0) * (w / 2), c + new Vector3(MathF.Cos(a1), MathF.Sin(a1), 0) * (w / 2), col);
        }
    }

    /// <summary>
    /// A natural pool: a dark bed, a ring of mossy rocks round the oval, a rock terrace under it if it's raised
    /// (<paramref name="lift"/>), and a lantern on an iron stake at the back (where the pool's lamp light sits).
    /// </summary>
    private static MeshBuilder BuildBasin(float w, float d, float lift, int seed)
    {
        var m = new MeshBuilder();
        var rng = new Random(40 + seed);
        float R01() => (float)rng.NextDouble();
        float rx = PoolRx(w), rz = PoolRz(d);
        var rock = Palette.Hex(0x8C8270); var rockDark = Palette.Hex(0x6E6656);
        // the bed, seen through the water
        m.Blob(new Vector3(0, FloorY - 0.08f, 0), new Vector3(rx * 0.98f, 0.1f, rz * 0.98f), 3, 14, Palette.Hex(0x3E4A3C), 300 + seed, 0.05f);
        for (int i = 0; i < 10; i++)
            m.Blob(new Vector3((R01() * 2 - 1) * rx * 0.6f, FloorY + 0.02f, (R01() * 2 - 1) * rz * 0.6f), new Vector3(0.06f + R01() * 0.05f, 0.035f, 0.05f + R01() * 0.04f),
                2, 5, Palette.Mul(Palette.Hex(0x9C9480), 0.8f + R01() * 0.3f), 320 + seed * 11 + i, 0.2f);
        // the rim: rocks round the oval, mossy on top
        int n = (int)MathF.Max(20, (rx + rz) * 9);
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * MathF.Tau + R01() * 0.15f;
            var p = new Vector3(MathF.Cos(a) * (rx + 0.06f + R01() * 0.08f), 0, MathF.Sin(a) * (rz + 0.06f + R01() * 0.08f));
            float s = 0.16f + R01() * 0.16f, h = RimY * (0.6f + R01() * 0.45f);
            m.Blob(p + new Vector3(0, h / 2, 0), new Vector3(s * (1 + R01() * 0.5f), h / 2 + 0.05f, s), 4, 7, Palette.Mul(rock, 0.88f + R01() * 0.24f), 400 + seed * 31 + i, 0.3f, rockDark);
            if (R01() < 0.35f) m.Blob(p + new Vector3(0, h + 0.03f, 0), new Vector3(s * 0.75f, 0.05f, s * 0.6f), 3, 7, Palette.Mul(Palette.Moss, 0.9f + R01() * 0.2f), 500 + seed * 31 + i, 0.3f);
        }
        // a terrace of rock under a raised pool
        if (lift > 0.05f)
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * MathF.Tau;
                m.Blob(new Vector3(MathF.Cos(a) * rx * 0.55f, -lift / 2, MathF.Sin(a) * rz * 0.55f), new Vector3(rx * 0.62f, lift / 2 + 0.12f, rz * 0.75f), 3, 7,
                    Palette.Mul(rock, 0.85f + R01() * 0.15f), 600 + seed * 7 + i, 0.2f, rockDark);
            }
        // the lantern on its stake
        var iron = Palette.Hex(0x3E3A3E);
        var lan = new Vector3(rx * 0.82f, 1.32f, -rz * 0.62f);
        m.Cylinder(new Vector3(lan.X, 0, lan.Z), 0.04f, 0.03f, lan.Y + 0.25f, 6, iron);
        m.Cone(lan + new Vector3(0, 0.14f, 0), 0.15f, 0.11f, 4, iron);
        m.Box(lan + new Vector3(0, -0.13f, 0), new Vector3(0.2f, 0.035f, 0.2f), iron);
        return m;
    }

    /// <summary>A pool's water surface: an oval of radii <paramref name="rx"/> by <paramref name="rz"/>.</summary>
    private static MeshBuilder WaterOval(float rx, float rz, int sides = 32, int rings = 5)
    {
        var m = new MeshBuilder();
        Vector3 P(float f, float a) => new(MathF.Cos(a) * rx * f, 0, MathF.Sin(a) * rz * f);
        for (int i = 0; i < rings; i++)
            for (int k = 0; k < sides; k++)
            {
                float f0 = i / (float)rings, f1 = (i + 1) / (float)rings;
                float a0 = MathF.Tau * k / sides, a1 = MathF.Tau * (k + 1) / sides;
                if (i == 0) m.Tri(Vector3.Zero, P(f1, a1), P(f1, a0), Color.White);
                else m.Quad(P(f0, a0), P(f0, a1), P(f1, a1), P(f1, a0), Color.White);
            }
        return m;
    }

    private static MeshBuilder WaterRect(float w, float d, int nx = 12, int nz = 5)
    {
        var m = new MeshBuilder();
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                float x0 = -w / 2 + w * i / nx, x1 = -w / 2 + w * (i + 1) / nx;
                float z0 = -d / 2 + d * j / nz, z1 = -d / 2 + d * (j + 1) / nz;
                m.Quad(new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0), Color.White);
            }
        return m;
    }

    private static MeshBuilder WaterDisc(float r, int sides)
    {
        var m = new MeshBuilder();
        for (int i = 0; i < 3; i++)
            for (int s = 0; s < sides; s++)
            {
                float r0 = r * i / 3, r1 = r * (i + 1) / 3;
                float a0 = MathF.Tau * s / sides, a1 = MathF.Tau * (s + 1) / sides;
                Vector3 P(float rr, float a) => new(MathF.Cos(a) * rr, 0, MathF.Sin(a) * rr);
                if (i == 0) m.Tri(Vector3.Zero, P(r1, a1), P(r1, a0), Color.White);
                else m.Quad(P(r0, a0), P(r0, a1), P(r1, a1), P(r1, a0), Color.White);
            }
        return m;
    }

    private static MeshBuilder BuildTrickle(Vector3 from, Vector3 to)
    {
        var m = new MeshBuilder();
        const int n = 12;
        Vector3 At(float t) => new(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t * t, from.Z + (to.Z - from.Z) * (1 - (1 - t) * (1 - t)));
        for (int i = 0; i < n; i++)
        {
            float t0 = i / (float)n, t1 = (i + 1) / (float)n;
            var p0 = At(t0); var p1 = At(t1);
            float w0 = 0.22f + t0 * 0.16f, w1 = 0.22f + t1 * 0.16f;
            var s = Vector3.UnitX;
            m.TriUv(p0 - s * w0 / 2, p0 + s * w0 / 2, p1 + s * w1 / 2, new Vector2(0, t0), new Vector2(1, t0), new Vector2(1, t1), Color.White);
            m.TriUv(p0 - s * w0 / 2, p1 + s * w1 / 2, p1 - s * w1 / 2, new Vector2(0, t0), new Vector2(1, t1), new Vector2(0, t1), Color.White);
        }
        return m;
    }

    private static MeshBuilder BuildFishBody()
    {
        var m = new MeshBuilder();
        m.Blob(Vector3.Zero, new Vector3(0.2f, 0.085f, 0.062f), 4, 8, Color.White, 5, 0.03f, Palette.Hex(0xF6F0E4));
        var fin = Palette.Hex(0xEDE6DA);
        m.Tri(new Vector3(0.05f, 0.07f, 0), new Vector3(-0.1f, 0.07f, 0), new Vector3(-0.05f, 0.155f, 0), fin);
        m.Tri(new Vector3(0.05f, 0.07f, 0), new Vector3(-0.05f, 0.155f, 0), new Vector3(-0.1f, 0.07f, 0), fin);
        foreach (var s in new[] { -1f, 1f })
        {
            m.Tri(new Vector3(0.06f, -0.03f, s * 0.05f), new Vector3(-0.02f, -0.05f, s * 0.1f), new Vector3(0.0f, -0.02f, s * 0.05f), fin);
            m.Tri(new Vector3(0.06f, -0.03f, s * 0.05f), new Vector3(0.0f, -0.02f, s * 0.05f), new Vector3(-0.02f, -0.05f, s * 0.1f), fin);
            m.Box(new Vector3(0.135f, 0.022f, s * 0.045f), new Vector3(0.028f, 0.028f, 0.02f), Palette.Ink);
        }
        return m;
    }

    private static MeshBuilder BuildFishTail()
    {
        var m = new MeshBuilder();
        var c = Palette.Hex(0xF2ECE0);
        var a = Vector3.Zero; var top = new Vector3(-0.16f, 0.1f, 0); var mid = new Vector3(-0.11f, 0, 0); var bot = new Vector3(-0.16f, -0.09f, 0);
        m.Tri(a, top, mid, c).Tri(a, mid, top, c).Tri(a, mid, bot, c).Tri(a, bot, mid, c);
        return m;
    }

    private static MeshBuilder BuildPlantBlades()
    {
        var m = new MeshBuilder();
        for (int k = 0; k < 6; k++)
        {
            float a = k * 2.4f;
            var b = new Vector3(MathF.Cos(a) * 0.07f, 0, MathF.Sin(a) * 0.06f);
            m.Cylinder(b, 0.022f, 0.006f, 0.34f + (k % 3) * 0.14f, 4, Color.White, null, MathF.Cos(a) * 0.07f, 0.4f);
        }
        return m;
    }

    private MeshBuilder BuildCrock()
    {
        var m = new MeshBuilder();
        var clay = Palette.Hex(0xB86E4B);
        m.Cylinder(Vector3.Zero, 0.46f, 0.5f, 0.1f, 12, Palette.Mul(clay, 0.8f));
        m.Ring(new Vector3(0, 0.08f, 0), 0.62f, 0.52f, 0.62f, 12, clay, Palette.Mul(clay, 1.12f));
        m.Disc(new Vector3(0, 0.12f, 0), 0.53f, 12, Palette.Hex(0x3E3430));
        m.Ring(new Vector3(0, 0.36f, 0), 0.635f, 0.6f, 0.06f, 12, Palette.Hex(0xE6D2B0)); // a pale painted band
        // a little landing net leaning on it (for moving creatures between pools)
        var net = new MeshBuilder();
        net.Cylinder(Vector3.Zero, 0.025f, 0.02f, 1.25f, 5, Palette.Bark);
        net.Ring(new Vector3(0, 1.25f, 0), 0.2f, 0.17f, 0.03f, 10, Palette.Hex(0x5C4A3A));
        net.Blob(new Vector3(0, 1.18f, 0), new Vector3(0.17f, 0.12f, 0.17f), 2, 8, Palette.Hex(0xD9CDB0), 4, 0.15f);
        net.Transform(Matrix4x4.CreateRotationZ(0.32f) * Matrix4x4.CreateTranslation(0.8f, 0, -0.1f));
        m.Append(net);
        // a lid resting against the far side
        var lid = new MeshBuilder().Cylinder(Vector3.Zero, 0.5f, 0.5f, 0.05f, 12, Palette.Hex(0x9E7650)).Cylinder(new Vector3(0, 0.05f, 0), 0.06f, 0.06f, 0.06f, 6, Palette.Bark);
        lid.Transform(Matrix4x4.CreateRotationX(-1.3f) * Matrix4x4.CreateTranslation(-0.2f, 0.48f, -0.7f));
        m.Append(lid);
        return m;
    }

    private MeshBuilder BuildBag()
    {
        var m = new MeshBuilder();
        var wood = Palette.Hex(0x9A7654);
        m.Box(new Vector3(0, 0.22f, 0), new Vector3(1.0f, 0.44f, 0.72f), wood, Palette.Mul(wood, 1.08f));
        for (int i = -1; i <= 1; i++) m.Box(new Vector3(i * 0.33f, 0.22f, 0.365f), new Vector3(0.03f, 0.42f, 0.01f), Palette.Mul(wood, 0.75f));
        var burlap = Palette.Hex(0xB59C6A);
        m.Blob(new Vector3(0, 0.74f, 0), new Vector3(0.36f, 0.34f, 0.3f), 3, 8, burlap, 21, 0.1f, Palette.Mul(burlap, 0.9f));
        m.Cylinder(new Vector3(0, 0.98f, 0), 0.14f, 0.12f, 0.14f, 7, burlap);
        m.Ring(new Vector3(0, 1.0f, 0), 0.16f, 0.12f, 0.04f, 8, Palette.Hex(0x6E5440));
        m.Cone(new Vector3(0, 1.1f, 0), 0.16f, 0.12f, 7, Palette.Mul(burlap, 1.05f));
        // a druid's sprig tucked into the tie
        m.Blob(new Vector3(0.12f, 1.14f, 0.05f), new Vector3(0.1f, 0.05f, 0.04f), 2, 5, Palette.Leaf, 22, 0.2f);
        m.Blob(new Vector3(0.18f, 1.2f, 0.02f), new Vector3(0.08f, 0.04f, 0.03f), 2, 5, Palette.LeafDeep, 23, 0.2f);
        m.Blob(new Vector3(0.05f, 1.22f, 0.07f), new Vector3(0.06f, 0.035f, 0.03f), 2, 5, Palette.Moss, 24, 0.2f);
        // an oak-leaf sigil stitched on the front
        m.Blob(new Vector3(0, 0.74f, 0.29f), new Vector3(0.09f, 0.11f, 0.02f), 2, 6, Palette.LeafDeep, 25, 0.1f);
        return m;
    }

    private MeshBuilder BuildLedger()
    {
        var m = new MeshBuilder();
        var wood = Palette.Hex(0x8A6648);
        m.Box(new Vector3(0, 0.04f, 0), new Vector3(0.6f, 0.08f, 0.5f), Palette.Mul(wood, 0.85f));
        m.Box(new Vector3(0, 0.55f, 0), new Vector3(0.14f, 1.0f, 0.14f), wood);
        var top = new MeshBuilder();
        top.Box(Vector3.Zero, new Vector3(0.8f, 0.06f, 0.6f), wood);
        top.Box(new Vector3(0, 0.05f, 0.28f), new Vector3(0.8f, 0.05f, 0.04f), Palette.Mul(wood, 0.8f));
        top.Box(new Vector3(0, 0.05f, 0), new Vector3(0.78f, 0.03f, 0.54f), Palette.Mul(Accent, 0.75f));       // the cover
        top.Box(new Vector3(-0.19f, 0.08f, -0.01f), new Vector3(0.36f, 0.035f, 0.5f), Palette.Hex(0xF3E8CF));   // pages
        top.Box(new Vector3(0.19f, 0.08f, -0.01f), new Vector3(0.36f, 0.035f, 0.5f), Palette.Hex(0xEFE2C4));
        for (int i = 0; i < 5; i++)
            top.Box(new Vector3(-0.19f, 0.1f, -0.16f + i * 0.08f), new Vector3(0.26f, 0.005f, 0.012f), Palette.Hex(0x8A8070));
        for (int i = 0; i < 4; i++)
            top.Box(new Vector3(0.19f, 0.1f, -0.14f + i * 0.08f), new Vector3(0.24f, 0.005f, 0.012f), Palette.Hex(0x5E8F4E)); // green ink
        top.Box(new Vector3(0.05f, 0.1f, 0.2f), new Vector3(0.02f, 0.01f, 0.28f), Palette.Hex(0x9C3A2E)); // ribbon
        top.Transform(Matrix4x4.CreateRotationX(0.42f) * Matrix4x4.CreateTranslation(0, 1.1f, 0.02f));
        m.Append(top);
        // a stub of candle on a little shelf beside it
        m.Box(new Vector3(0.38f, 0.98f, 0.05f), new Vector3(0.2f, 0.04f, 0.2f), Palette.Mul(wood, 0.9f));
        m.Cylinder(new Vector3(0.38f, 1.0f, 0.05f), 0.04f, 0.04f, 0.14f, 6, Palette.Hex(0xF3EAD2));
        return m;
    }

    private MeshBuilder BuildStool()
    {
        var m = new MeshBuilder();
        var wood = Palette.Hex(0xA07A58);
        // a little reed mat underneath, in the pools' colour
        var mat = Palette.Lerp(Accent, Palette.Parchment, 0.5f);
        m.Box(new Vector3(0, 0.03f, 0), new Vector3(1.5f, 0.03f, 1.0f), mat);
        for (int i = 0; i < 6; i++) m.Box(new Vector3(-0.62f + i * 0.25f, 0.05f, 0), new Vector3(0.03f, 0.01f, 0.96f), Palette.Mul(mat, 0.85f));
        m.Cylinder(new Vector3(0, 0.46f, 0), 0.3f, 0.3f, 0.09f, 9, wood, Palette.Mul(wood, 1.1f));
        for (int i = 0; i < 3; i++)
        {
            var leg = new MeshBuilder().Cylinder(Vector3.Zero, 0.035f, 0.03f, 0.48f, 5, Palette.Mul(wood, 0.85f), null, -0.08f);
            leg.Transform(Matrix4x4.CreateTranslation(0.24f, 0, 0) * Matrix4x4.CreateRotationY(i * MathF.Tau / 3 + 0.4f));
            m.Append(leg);
        }
        return m;
    }

    private static MeshBuilder BuildHandRake()
    {
        var m = new MeshBuilder();
        m.Cylinder(Vector3.Zero, 0.025f, 0.025f, 1.4f, 5, Palette.Bark);
        m.Box(new Vector3(0, 0, 0), new Vector3(0.5f, 0.05f, 0.06f), Palette.Hex(0x5C5658));
        for (int i = 0; i < 6; i++) m.Box(new Vector3(-0.22f + i * 0.088f, -0.07f, 0), new Vector3(0.02f, 0.12f, 0.02f), Palette.Hex(0x5C5658));
        return m;
    }

    /// <summary>The static furniture: shelves of crocks, the long rake, pots, a potting bench, a drain grate.</summary>
    private MeshBuilder BuildDecor()
    {
        var m = new MeshBuilder();
        var wood = Palette.Hex(0x8F6A4C);
        // a shelf of feed crocks on the back wall, between the panes
        m.Box(new Vector3(-1.4f, 2.05f, -5.22f), new Vector3(1.4f, 0.07f, 0.32f), wood);
        m.Box(new Vector3(-1.95f, 1.9f, -5.3f), new Vector3(0.05f, 0.25f, 0.12f), Palette.Mul(wood, 0.8f));
        m.Box(new Vector3(-0.85f, 1.9f, -5.3f), new Vector3(0.05f, 0.25f, 0.12f), Palette.Mul(wood, 0.8f));
        var jars = new[] { Palette.Hex(0xC4664A), Palette.Hex(0xA89A5A), Palette.Hex(0x8FC7D9), Palette.Hex(0xE6D2B0) };
        for (int i = 0; i < 4; i++)
        {
            float h = 0.22f + (i % 2) * 0.08f;
            m.Cylinder(new Vector3(-1.95f + i * 0.36f, 2.09f, -5.2f), 0.11f, 0.1f, h, 7, jars[i]);
            m.Cylinder(new Vector3(-1.95f + i * 0.36f, 2.09f + h, -5.2f), 0.07f, 0.07f, 0.04f, 6, Palette.Hex(0x9E7650));
        }
        // the long rake leaning on the left wall
        var rake = BuildHandRake();
        rake.Transform(Matrix4x4.CreateRotationZ(MathF.PI) * Matrix4x4.CreateTranslation(0, 1.4f, 0) * Matrix4x4.CreateRotationZ(-0.22f) * Matrix4x4.CreateTranslation(-5.15f, 0.08f, 1.6f));
        m.Append(rake);
        // potted ferns' pots (the fronds are drawn swaying)
        foreach (var p in FernPots)
        {
            m.Cylinder(p, 0.3f, 0.36f, 0.42f, 9, Palette.Hex(0xB86E4B), Palette.Hex(0x5A4232));
            m.Ring(p + new Vector3(0, 0.38f, 0), 0.38f, 0.3f, 0.07f, 9, Palette.Hex(0xC27A55));
        }
        // a brass drain grate in the floor, where the tanks are emptied
        m.Box(new Vector3(-0.9f, 0.015f, 1.0f), new Vector3(0.5f, 0.02f, 0.5f), Palette.Hex(0x7A6A4A));
        for (int i = 0; i < 4; i++) m.Box(new Vector3(-1.08f + i * 0.12f, 0.03f, 1.0f), new Vector3(0.03f, 0.01f, 0.44f), Palette.Hex(0x4A4034));
        // the trickle's spout in the back wall
        if (_trickleTank is not null)
        {
            var sp = new Vector3(_trickleEnd.X, 2.82f, -5.25f);
            m.Box(sp, new Vector3(0.36f, 0.22f, 0.3f), Palette.Hex(0xA99C83));
            m.Box(sp + new Vector3(0, -0.06f, 0.16f), new Vector3(0.14f, 0.06f, 0.16f), Palette.Hex(0x8C8270));
        }
        return m;
    }

    private static readonly Vector3[] FernPots = { new(-4.75f, 0, 4.1f), new(5.0f, 0, -4.7f), new(-4.85f, 0, -4.75f) };

    private static MeshBuilder BuildFerns()
    {
        var m = new MeshBuilder();
        int seed = 0;
        foreach (var p in FernPots)
        {
            for (int k = 0; k < 9; k++)
            {
                float a = k * MathF.Tau / 9 + seed;
                var dir = new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
                for (int j = 0; j < 4; j++)
                {
                    var q = p + new Vector3(0, 0.5f + j * 0.12f - j * j * 0.035f, 0) + dir * (0.12f + j * 0.16f);
                    m.Blob(q, new Vector3(0.13f, 0.05f, 0.13f) * (1 - j * 0.15f), 2, 5, j % 2 == 0 ? Palette.Leaf : Palette.Hex(0x7FB06A), seed * 50 + k * 5 + j, 0.2f);
                }
            }
            seed++;
        }
        return m;
    }

    // ───────────────────────── state → visuals ─────────────────────────

    /// <summary>Matches the 3D creatures and objects to the pools' real state; new arrivals hatch or drop in.</summary>
    private void SyncState()
    {
        var seen = new HashSet<int>();
        foreach (var t in _tanks)
        {
            var st = S.Pools.State(t.Id);
            t.Energy = S.Pools.Energy(t.Id);
            t.Clarity = st.Clarity;
            if (t.Murk < 0) t.Murk = TargetMurk(t);
            foreach (var lf in st.Lifeforms) SyncCritter(lf, t, seen);

            var items = S.World.Items.At(Locations.Pool(t.Id)).ToList();
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!_laid.TryGetValue(it.Id, out var l) || l.Pool != t.Id)
                {
                    l = new Laid { Id = it.Id, Def = it.Def, Pool = t.Id, Drop = _synced ? 0 : 1 };
                    _laid[it.Id] = l;
                }
                else if (l.Def != it.Def)
                {
                    l.Def = it.Def;
                    if (_synced) App.Particles.Burst(t.ToWorld(l.P + new Vector3(0, 0.15f, 0)), Palette.LanternHot, R.ReduceMotion ? 8 : 26, 1.4f, 1.2f);
                }
                l.Copies = Math.Clamp(it.Quantity, 1, 3);
                float fx = items.Count == 1 ? 0.12f : -0.62f + 1.24f * i / (items.Count - 1);
                l.P = new Vector3(fx * (t.W / 2 - 0.3f), FloorY, t.D / 2 - 0.32f - (i % 2) * 0.18f);
            }
        }
        foreach (var lf in S.Pools.Holding) SyncCritter(lf, null, seen);
        foreach (var id in _critters.Keys.Where(k => !seen.Contains(k)).ToList()) _critters.Remove(id);
        var present = S.World.Items.All.Where(i => Locations.IsPool(i.Location, out _)).Select(i => i.Id).ToHashSet();
        foreach (var id in _laid.Keys.Where(k => !present.Contains(k)).ToList()) _laid.Remove(id);
        _knownLifeforms.UnionWith(seen);
        _synced = true;
    }

    private void SyncCritter(LifeformInstance lf, Tank? t, HashSet<int> seen)
    {
        seen.Add(lf.Id);
        var sp = S.Content.Lifeforms[lf.Species];
        if (!_critters.TryGetValue(lf.Id, out var c))
        {
            c = new Critter { Id = lf.Id, Pool = t?.Id, Species = lf.Species, Kind = sp.Kind, Seed = Hash01(lf.Id * 7919 + 13) * 100, Yaw = Hash01(lf.Id) * MathF.Tau };
            _critters[lf.Id] = c;
            if (t is not null) Place(c, t, fresh: true);
            if (_synced && !_knownLifeforms.Contains(lf.Id))
            {
                c.Pop = 0; // a hatching: it pops in with a burst of bubbles
                if (t is not null)
                {
                    var at = t.ToWorld(c.P);
                    App.Particles.Burst(at, Palette.Lerp(SpeciesTint(lf.Species), Palette.Foam, 0.4f), R.ReduceMotion ? 8 : 24, 1.5f, 1.1f);
                    App.Particles.Burst(at, Palette.LanternHot, R.ReduceMotion ? 4 : 10, 0.9f, 1.4f);
                    for (int i = 0; i < (R.ReduceMotion ? 3 : 14); i++)
                        t.Bubbles.Add(new Bubble { P = c.P + new Vector3(Rand(-0.15f, 0.15f), Rand(-0.05f, 0.1f), Rand(-0.1f, 0.1f)), V = Rand(0.35f, 0.7f), Wob = Rand(0, 6), Size = Rand(0.04f, 0.08f) });
                }
                else App.Particles.Burst(CrockPos + new Vector3(0, 0.75f, 0), Palette.Foam, 14, 1.2f, 1f);
            }
        }
        c.Mood = lf.Mood;
        c.Eats = sp.HungerPerTick > 0;
        c.Expecting = lf.Expecting is not null;
        if (c.Pool != t?.Id)
        {
            c.Pool = t?.Id;
            if (t is not null) Place(c, t, fresh: false);
        }
    }

    /// <summary>Puts a creature somewhere sensible in a tank (rooted plants get a stable spot).</summary>
    private void Place(Critter c, Tank t, bool fresh)
    {
        float hx = t.W / 2 - 0.3f, hz = t.D / 2 - 0.2f;
        switch (c.Kind)
        {
            case LifeformKind.Plant:
                c.P = new Vector3((Hash01(c.Id * 31 + 1) * 2 - 1) * hx, FloorY, -t.D / 2 + 0.16f + Hash01(c.Id * 17 + 3) * 0.35f);
                break;
            case LifeformKind.Snail:
                c.P = new Vector3((Hash01(c.Id * 13 + 2) * 2 - 1) * hx, FloorY + 0.01f, (Hash01(c.Id * 7 + 5) * 2 - 1) * hz);
                break;
            case LifeformKind.Mote:
                c.P = new Vector3((Hash01(c.Id * 11 + 2) * 2 - 1) * hx, SurfY + 0.45f, (Hash01(c.Id * 5 + 9) * 2 - 1) * hz);
                break;
            default:
                c.P = fresh ? new Vector3((Hash01(c.Id * 3 + 1) * 2 - 1) * hx, FloorY + 0.25f + Hash01(c.Id * 19) * 0.55f, (Hash01(c.Id * 23 + 4) * 2 - 1) * hz)
                            : new Vector3(Rand(-0.3f, 0.3f), SurfY - 0.12f, Rand(-0.1f, 0.1f)); // tipped in from the net
                if (!fresh) Splash(t, new Vector2(c.P.X, c.P.Z), 0.7f);
                break;
        }
        c.Target = c.P;
        c.Retarget = 0;
    }

    private static float Hash01(int n)
    {
        unchecked
        {
            uint h = (uint)n * 2654435761u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    /// <summary>A species' colour: the hub's palette where it has one, otherwise a stable tint from its id.</summary>
    internal static Color SpeciesTint(string species)
    {
        var c = HubWorld.CreatureColor(species);
        if (c.R != Palette.Stone.R || c.G != Palette.Stone.G || c.B != Palette.Stone.B) return c;
        uint h = 2166136261;
        foreach (var ch in species) h = (h ^ ch) * 16777619;
        return Raylib.ColorFromHSV(h % 360, 0.42f, 0.86f);
    }

    private float TargetMurk(Tank t) => Math.Clamp(1 - (float)(t.Clarity / 100.0), 0, 0.92f);

    // ───────────────────────── per-frame ─────────────────────────

    protected override void Tick(float dt)
    {
        _time += dt;
        _cardAge += dt;
        float motion = R.ReduceMotion ? 0.3f : 1f;
        SyncState();

        foreach (var t in _tanks)
        {
            t.Murk = Ease.Approach(t.Murk, TargetMurk(t), R.ReduceMotion ? 20 : 1.6f, dt);
            t.FeedT += dt;
            t.CleanseT += dt;
            UpdateWaterLife(t, dt, motion);
        }
        foreach (var c in _critters.Values)
        {
            c.Pop = MathF.Min(1, c.Pop + dt * 1.6f);
            c.Phase += dt * motion;
            if (c.Pool is not null && _tankOf.TryGetValue(c.Pool, out var t)) Move(c, t, dt, motion);
        }
        if (!R.ReduceMotion)
            for (int i = 0; i < _dust.Count; i++)
            {
                var p = _dust[i] + new Vector3(MathF.Sin(_time * 0.21f + i) * 0.05f, 0.03f, MathF.Cos(_time * 0.17f + i * 2) * 0.04f) * dt * 3;
                if (p.Y > 5f) p.Y = 1.2f;
                _dust[i] = p;
            }

        // the trickle: a steady little splash where it lands
        if (_trickleTank is { } tt && !R.ReduceMotion)
        {
            _trickleT += dt;
            if (_trickleT > 0.12f)
            {
                _trickleT = 0;
                var p = App.Particles;
                p.Emit(_trickleEnd + new Vector3(p.R(-0.1f, 0.1f), 0.04f, p.R(-0.05f, 0.08f)), new Vector3(p.R(-0.4f, 0.4f), p.R(0.4f, 0.9f), p.R(-0.2f, 0.4f)), 0.45f, 0.06f, Palette.Alpha(Palette.Foam, 0.85f), false, 0, 0.4f, -4f);
                if (_rng.NextSingle() < 0.25f)
                {
                    var local = Vector3.Transform(_trickleEnd, Inverse(tt.World));
                    tt.Ripples.Add(new Ripple { P = new Vector2(local.X, local.Z), Max = 0.28f });
                }
            }
        }

        UpdateHints();
        UpdateFocus(dt);

        // Space sits a while; a click on empty floor puts the working card away
        if (Input.KeyPressed(KeyboardKey.Space)) Sit();
        if (_card != CardKind.None && Hovered is null && !Ui.WantsMouse && Input.Click) CloseCard();
    }

    private static Matrix4x4 Inverse(Matrix4x4 m) => Matrix4x4.Invert(m, out var inv) ? inv : Matrix4x4.Identity;

    private void UpdateWaterLife(Tank t, float dt, float motion)
    {
        // bubbles from the floor (plants and the mossy rock), more where the water is clear and alive
        t.BubbleT -= dt;
        if (t.BubbleT <= 0 && !R.ReduceMotion)
        {
            t.BubbleT = Rand(0.25f, 0.9f) * (0.6f + t.Murk);
            var src = Rand(0, 1) < 0.5f ? new Vector3(-t.W * 0.32f, FloorY + 0.3f, -t.D * 0.22f) : new Vector3(Rand(-t.W / 2 + 0.3f, t.W / 2 - 0.3f), FloorY + 0.05f, Rand(-t.D / 2 + 0.2f, 0));
            int n = 1 + _rng.Next(3);
            for (int i = 0; i < n; i++)
                t.Bubbles.Add(new Bubble { P = src + new Vector3(Rand(-0.04f, 0.04f), -i * 0.07f, Rand(-0.04f, 0.04f)), V = Rand(0.3f, 0.55f), Wob = Rand(0, 6), Size = Rand(0.03f, 0.06f) });
        }
        for (int i = t.Bubbles.Count - 1; i >= 0; i--)
        {
            var b = t.Bubbles[i];
            b.P.Y += b.V * dt * motion;
            b.P.X += MathF.Sin(_time * 5 + b.Wob) * 0.04f * dt * motion;
            if (b.P.Y >= SurfY - 0.03f)
            {
                t.Bubbles.RemoveAt(i);
                if (!R.ReduceMotion && _rng.NextSingle() < 0.5f) t.Ripples.Add(new Ripple { P = new Vector2(b.P.X, b.P.Z), Max = 0.16f });
            }
        }
        // ripples now and then where the fish touch the surface
        t.RippleT -= dt;
        if (t.RippleT <= 0 && !R.ReduceMotion)
        {
            t.RippleT = Rand(1.2f, 3.5f);
            t.Ripples.Add(new Ripple { P = new Vector2(Rand(-t.W / 2 + 0.4f, t.W / 2 - 0.4f), Rand(-t.D / 2 + 0.35f, t.D / 2 - 0.35f)), Max = 0.26f });
        }
        for (int i = t.Ripples.Count - 1; i >= 0; i--)
        {
            t.Ripples[i].Age += dt;
            if (t.Ripples[i].Age > 1.6f) t.Ripples.RemoveAt(i);
        }
        // warm water steams a little; the clock's energy is visible in the air above it
        if (t.Energy.Heat >= 2 && !R.ReduceMotion)
        {
            t.SteamT -= dt;
            if (t.SteamT <= 0)
            {
                t.SteamT = Rand(0.4f, 1.1f) / t.Energy.Heat;
                var p = App.Particles;
                var at = t.ToWorld(new Vector3(Rand(-t.W / 2 + 0.3f, t.W / 2 - 0.3f), SurfY + 0.05f, Rand(-t.D / 2 + 0.2f, t.D / 2 - 0.2f)));
                p.Emit(at, new Vector3(p.R(-0.05f, 0.05f), p.R(0.18f, 0.35f), p.R(-0.05f, 0.05f)), p.R(2.2f, 3.2f), 0.3f, Palette.Alpha(Palette.Hex(0xF6F0E6), 0.22f), false, 0.25f, 0.2f);
            }
        }
    }

    private void Move(Critter c, Tank t, float dt, float motion)
    {
        float hx = t.W / 2 - 0.24f, hz = t.D / 2 - 0.17f;
        switch (c.Kind)
        {
            case LifeformKind.Fish:
            {
                bool feeding = c.Eats && t.FeedT < 6;
                float speed = (c.Mood switch { LifeMood.Thriving => 0.42f, LifeMood.Content => 0.3f, _ => 0.12f }) * (feeding ? 1.7f : 1) * motion;
                c.Retarget -= dt;
                if (c.Retarget <= 0 || Vector2.Distance(new Vector2(c.P.X, c.P.Z), new Vector2(c.Target.X, c.Target.Z)) < 0.14f)
                {
                    c.Retarget = Rand(2.5f, 6f);
                    if (feeding) c.Target = new Vector3(t.FeedAt.X + Rand(-0.3f, 0.3f), SurfY - 0.12f, t.FeedAt.Y + Rand(-0.2f, 0.2f));
                    else
                    {
                        float low = FloorY + 0.14f, high = SurfY - 0.14f;
                        if (c.Mood == LifeMood.Sulking) high = FloorY + 0.32f;
                        c.Target = new Vector3(Rand(-hx, hx), Rand(low, high), Rand(-hz, hz));
                        // loose schools: often head toward kin
                        var kin = _critters.Values.Where(o => o != c && o.Pool == c.Pool && o.Species == c.Species && o.Kind == LifeformKind.Fish).ToList();
                        if (kin.Count > 0 && _rng.NextSingle() < 0.45f)
                        {
                            var mean = kin.Aggregate(Vector3.Zero, (a, o) => a + o.P) / kin.Count;
                            c.Target = Vector3.Lerp(c.Target, mean, 0.6f);
                        }
                    }
                    c.Target = Vector3.Clamp(c.Target, new Vector3(-hx, FloorY + 0.12f, -hz), new Vector3(hx, SurfY - 0.1f, hz));
                }
                var to = c.Target - c.P;
                float want = MathF.Atan2(-to.Z, to.X);
                c.Yaw = TurnToward(c.Yaw, want, (feeding ? 3.2f : 1.8f) * dt * MathF.Max(motion, 0.5f));
                var fwd = new Vector3(MathF.Cos(c.Yaw), 0, -MathF.Sin(c.Yaw));
                c.P += fwd * speed * dt;
                c.P.Y = Ease.Approach(c.P.Y, c.Target.Y, 0.9f * motion, dt);
                // gentle separation from neighbours
                foreach (var o in _critters.Values)
                {
                    if (o == c || o.Pool != c.Pool || o.Kind != LifeformKind.Fish) continue;
                    var d = c.P - o.P;
                    float l = d.Length();
                    if (l > 0.001f && l < 0.22f) c.P += d / l * (0.22f - l) * 0.5f * dt * 4;
                }
                var clamped = Vector3.Clamp(c.P, new Vector3(-hx, FloorY + 0.1f, -hz), new Vector3(hx, SurfY - 0.08f, hz));
                if (clamped != c.P) { c.P = clamped; c.Retarget = MathF.Min(c.Retarget, 0.2f); }
                break;
            }
            case LifeformKind.Snail:
            {
                c.Retarget -= dt;
                if (c.Retarget <= 0 || Vector2.Distance(new Vector2(c.P.X, c.P.Z), new Vector2(c.Target.X, c.Target.Z)) < 0.05f)
                {
                    c.Retarget = Rand(8, 16);
                    c.Target = new Vector3(Math.Clamp(c.P.X + Rand(-0.6f, 0.6f), -hx, hx), FloorY + 0.01f, Rand(-hz, hz));
                }
                var to = c.Target - c.P;
                c.Yaw = TurnToward(c.Yaw, MathF.Atan2(-to.Z, to.X), 0.5f * dt);
                float speed = (c.Mood == LifeMood.Sulking ? 0.012f : 0.035f) * motion;
                c.P += new Vector3(MathF.Cos(c.Yaw), 0, -MathF.Sin(c.Yaw)) * speed * dt;
                c.P = Vector3.Clamp(c.P, new Vector3(-hx, FloorY, -hz), new Vector3(hx, FloorY + 0.02f, hz));
                break;
            }
            case LifeformKind.Mote:
            {
                c.Retarget -= dt;
                if (c.Retarget <= 0)
                {
                    c.Retarget = Rand(3, 7);
                    c.Target = new Vector3(Rand(-hx, hx), SurfY + Rand(0.3f, 0.65f), Rand(-hz, hz));
                }
                c.P = Ease.Approach(c.P, c.Target, 0.35f * motion, dt);
                break;
            }
        }
    }

    private static float TurnToward(float a, float b, float maxStep)
    {
        float d = MathF.IEEERemainder(b - a, MathF.Tau);
        return a + Math.Clamp(d, -maxStep, maxStep);
    }

    private void Splash(Tank t, Vector2 local, float strength)
    {
        var at = t.ToWorld(new Vector3(local.X, SurfY + 0.04f, local.Y));
        var p = App.Particles;
        int n = (int)((R.ReduceMotion ? 5 : 16) * strength);
        for (int i = 0; i < n; i++)
            p.Emit(at, new Vector3(p.R(-0.9f, 0.9f), p.R(1.2f, 2.6f), p.R(-0.9f, 0.9f)) * strength, p.R(0.5f, 0.8f), p.R(0.06f, 0.12f), Palette.Alpha(Palette.Foam, 0.9f), false, 0, 0.4f, -7f);
        t.Ripples.Add(new Ripple { P = local, Max = 0.35f * strength + 0.1f });
        t.Ripples.Add(new Ripple { P = local, Max = 0.2f * strength + 0.06f, Age = -0.25f });
    }

    private void UpdateHints()
    {
        foreach (var t in _tanks)
        {
            if (!HasSpot(t.Spot)) continue;
            var v = S.Pools.View(t.Id);
            var lfs = v.State.Lifeforms;
            int thr = lfs.Count(l => l.Mood == LifeMood.Thriving), sulk = lfs.Count(l => l.Mood == LifeMood.Sulking);
            var hint = $"light {Signed(v.Energy.Light)} · heat {Signed(v.Energy.Heat)} · clarity {v.State.Clarity:0} · "
                       + (lfs.Count == 0 ? "empty" : $"{lfs.Count} of {v.Pool.Capacity} living, {thr} thriving" + (sulk > 0 ? $", {sulk} sulking" : ""))
                       + (lfs.Any(l => l.Expecting is not null) ? " · eggs!" : "")
                       + (v.Items.Count > 0 ? " · in the water: " + string.Join(", ", v.Items.Select(S.World.Items.Name)) : "");
            SpotOf(t.Spot).Hint = hint;
        }
        var held = S.Pools.Holding;
        SpotOf(SpotHolding).Hint = held.Count == 0 ? "Empty. Somewhere for a creature to rest between pools."
            : "Resting here: " + string.Join(", ", held.Select(l => $"{l.Name} the {S.Content.LifeformName(l.Species)}"));
        var bag = S.Pools.DruidsBag.ToList();
        SpotOf(SpotBag).Hint = bag.Count == 0 ? "Empty for now." : "In the bag: " + string.Join(", ", bag.Select(S.World.Items.Name));
    }

    private void UpdateFocus(float dt)
    {
        Vector3? subject = _card switch
        {
            CardKind.Pool when _tankOf.TryGetValue(_cardPool, out var t) => t.ToWorld(new Vector3(0, 1.0f, 0)),
            CardKind.Holding => CrockPos + new Vector3(0, 0.6f, 0),
            CardKind.Bag => BagPos + new Vector3(0, 0.7f, 0),
            _ => null,
        };
        var goal = _home; float dist = _homeDist;
        if (subject is { } s)
        {
            var right = new Vector3(MathF.Cos(CamYaw), 0, -MathF.Sin(CamYaw));
            dist = _homeDist - 2.2f;
            goal = s - right * 2.35f;
            goal.Y = MathF.Max(1.0f, s.Y);
        }
        float rate = R.ReduceMotion ? 60 : 3.2f;
        CamTarget = Ease.Approach(CamTarget, goal, rate, dt);
        CamDist = Ease.Approach(CamDist, dist, rate, dt);
    }

    protected override void Light()
    {
        base.Light();
        var l = R.Light;
        l.SunDir = Vector3.Normalize(new Vector3(0.15f, 0.85f, -0.5f)); // down through the crack above the falls
        l.Sun = Palette.Hex(0xF6DFAE);
        l.Sky = Palette.Lerp(l.Sky, Palette.Hex(0x7FA88C), 0.4f);
        l.Ground = Palette.Hex(0x4A4638);
        l.Night = 0.45f;
        int i = 0;
        foreach (var (p, k) in _lamps)
        {
            float flick = R.ReduceMotion ? 1 : 1 + 0.07f * MathF.Sin(_time * 6.3f + i * 1.7f) * MathF.Sin(_time * 2.9f + i);
            if (l.Lamps.Count < 8) l.Lamps.Add((p, 5.2f, Palette.Lantern, 0.85f * k * flick));
            i++;
        }
    }

    // ───────────────────────── drawing ─────────────────────────

    protected override void DrawRoom()
    {
        float t = _time;
        bool still = R.ReduceMotion;
        R.Draw(_decor, Matrix4x4.Identity, Color.White, outlineWidth: 1.2f);
        R.Draw(_ferns, Matrix4x4.Identity, Color.White, sway: 1.2f, outlineWidth: 1.0f);

        // furniture with hotspots
        R.Draw(_crock, Lifted(SpotHolding, CrockPos), Color.White, highlight: Rim(SpotHolding), outlineWidth: 1.4f);
        R.Draw(_bag, Lifted(SpotBag, BagPos) * Squash(SpotBag), Color.White, highlight: Rim(SpotBag), outlineWidth: 1.4f);
        R.Draw(_ledger, Matrix4x4.CreateRotationY(0.55f) * Lifted(SpotLedger, LedgerPos), Color.White, highlight: Rim(SpotLedger), outlineWidth: 1.4f);
        R.Draw(_stool, Lifted(SpotStool, StoolPos), Color.White, highlight: Rim(SpotStool), outlineWidth: 1.3f);

        foreach (var tk in _tanks)
        {
            tk.World = Matrix4x4.CreateRotationY(tk.Yaw) * Matrix4x4.CreateTranslation(tk.Pos + new Vector3(0, SpotOf(tk.Spot).Lift.Value * 0.12f, 0));
            R.Draw(tk.Stone, tk.World, Color.White, highlight: Rim(tk.Spot), outlineWidth: 1.4f);
            float flick = still ? 1 : 0.9f + 0.1f * MathF.Sin(t * 7.1f + tk.Index * 2.3f);
            R.Draw(_lanternGlow, Matrix4x4.CreateTranslation(tk.Lantern) * tk.World, Color.White, emissive: 0.9f * flick, outline: false);
            DrawLaid(tk);
            DrawCreatures(tk);
            if (tk.CleanseT < 1.4f) DrawRaking(tk);
        }
        DrawHolding();

        // inside the water (before the water is laid over it): bubbles, dappled light, the glow of laid things
        foreach (var tk in _tanks) DrawUnderwaterGlows(tk);

        // the water, far tanks first
        var cam = Camera.Position;
        foreach (var tk in _tanks.OrderByDescending(x => Vector3.DistanceSquared(cam, x.ToWorld(new Vector3(0, 1, 0)))))
            DrawWaterOf(tk);
        R.DrawWater(_crockWater, Matrix4x4.CreateTranslation(CrockPos + new Vector3(0, 0.6f, 0)), Palette.TealLight, Palette.Teal, 0.15f, 0.8f);
        R.DrawWater(_river, Matrix4x4.CreateTranslation(0, RiverY, RiverZ), Palette.Hex(0x3F8A86), Palette.Hex(0x163C44), 0.2f, 0.9f);
        if (_trickle.MeshCount > 0) R.DrawFalls(_trickle, Matrix4x4.Identity, Palette.Alpha(Palette.Hex(0xCFEDE8), 0.8f));
        if (_spill.MeshCount > 0) R.DrawFalls(_spill, Matrix4x4.Identity, Palette.Alpha(Palette.Hex(0xCFEDE8), 0.75f));

        // above the water: motes, ripples, the glow of lanterns, dust in the light
        foreach (var tk in _tanks)
        {
            DrawRipples(tk);
            DrawAbove(tk);
        }
        foreach (var (p, k) in _lamps.Take(_tanks.Count))
            R.Glow(p, 0.8f, Palette.Alpha(Palette.LanternHot, 0.55f));
        R.Glow(LedgerPos + new Vector3(0.38f * MathF.Cos(-0.55f) , 1.2f, 0.05f), 0.32f, Palette.Alpha(Palette.LanternHot, still ? 0.7f : 0.6f + 0.15f * MathF.Sin(t * 9)));
        for (int i = 0; i < _dust.Count; i++)
            R.Glow(_dust[i], 0.07f, Palette.Alpha(Palette.LanternHot, 0.22f + 0.12f * MathF.Sin(t * 0.9f + i)));
        // the bag breathes a little light when something waits in it
        if (S.Pools.DruidsBag.Any())
            R.Glow(BagPos + new Vector3(0, 1.18f + SpotOf(SpotBag).Lift.Value, 0), 0.55f, Palette.Alpha(Palette.LanternHot, 0.3f + (still ? 0 : 0.12f * MathF.Sin(t * 1.6f))));
    }

    private Matrix4x4 Lifted(string spot, Vector3 pos) => Matrix4x4.CreateTranslation(pos + new Vector3(0, SpotOf(spot).Lift.Value, 0));

    private Matrix4x4 Squash(string spot)
    {
        float l = SpotOf(spot).Lift.Value;
        return Matrix4x4.Identity * Matrix4x4.CreateScale(1 + l * 0.15f, 1 - l * 0.12f, 1 + l * 0.15f);
    }

    private void DrawLaid(Tank t)
    {
        foreach (var l in _laid.Values.Where(x => x.Pool == t.Id))
        {
            l.Drop = MathF.Min(1, l.Drop + App.Ui.Dt / 0.9f);
            float e = Ease.OutCubic(l.Drop);
            var it = S.World.Items.Get(l.Id);
            if (it is null) continue;
            var def = S.Content.Item(it.Def);
            var model = def.Kind switch
            {
                ItemKind.Mineral or ItemKind.Aura => _crystal,
                ItemKind.ClueForm => _orb,
                ItemKind.Food or ItemKind.Ingredient or ItemKind.Reagent => _jar,
                _ => _pebble,
            };
            var col = App.Art.ColorOf(def);
            if (it.Inert) col = Palette.Lerp(col, Palette.InkFaint, 0.7f);
            for (int k = 0; k < l.Copies; k++)
            {
                var pos = l.P + new Vector3(k * 0.13f - (l.Copies - 1) * 0.065f, (SurfY - FloorY) * (1 - e), (k % 2) * 0.08f);
                float tilt = (1 - e) * 2.4f;
                var w = Matrix4x4.CreateRotationZ(tilt) * Matrix4x4.CreateRotationY(Hash01(l.Id.GetHashCode() + k) * 3) * Matrix4x4.CreateTranslation(pos) * t.World;
                R.Draw(model, w, col, emissive: it.Essence is not null && !it.Inert ? 0.25f : 0.08f, outlineWidth: 1.0f);
            }
        }
    }

    private void DrawCreatures(Tank t)
    {
        float time = R.ReduceMotion ? 0 : _time;
        foreach (var c in _critters.Values.Where(x => x.Pool == t.Id))
        {
            var col = SpeciesTint(c.Species);
            if (c.Mood == LifeMood.Sulking) col = Palette.Lerp(col, Palette.StoneDark, 0.35f);
            float pop = c.Pop >= 1 ? 1 : Ease.OutBack(c.Pop, 2.2f);
            switch (c.Kind)
            {
                case LifeformKind.Fish:
                {
                    float wig = R.ReduceMotion ? 0 : MathF.Sin(time * (c.Mood == LifeMood.Sulking ? 3 : 8) + c.Seed) * 0.16f;
                    float plump = c.Expecting ? 1.28f : 1;
                    var local = Matrix4x4.CreateScale(1.15f * pop, 1.15f * pop, 1.15f * pop * plump) * Matrix4x4.CreateRotationY(c.Yaw + wig * 0.4f) * Matrix4x4.CreateTranslation(c.P) * t.World;
                    R.Draw(_fishBody, local, col, emissive: c.Mood == LifeMood.Thriving ? 0.18f : 0.08f, outlineWidth: 0.9f);
                    R.Draw(_fishTail, Matrix4x4.CreateRotationY(wig * 2.2f) * Matrix4x4.CreateTranslation(-0.17f, 0, 0) * local, col, emissive: 0.08f, outline: false);
                    break;
                }
                case LifeformKind.Snail:
                {
                    float stretch = R.ReduceMotion ? 1 : 1 + MathF.Sin(time * 1.4f + c.Seed) * 0.08f;
                    var w = Matrix4x4.CreateScale(1.2f * pop) * Matrix4x4.CreateRotationY(c.Yaw) * Matrix4x4.CreateTranslation(c.P) * t.World;
                    R.Draw(_snailFoot, Matrix4x4.CreateScale(stretch, 1, 1) * w, Color.White, outlineWidth: 0.8f);
                    R.Draw(_snailShell, w, col, emissive: 0.15f, outlineWidth: 0.9f);
                    break;
                }
                case LifeformKind.Plant:
                {
                    var w = Matrix4x4.CreateScale(1.3f * pop) * Matrix4x4.CreateRotationY(c.Seed) * Matrix4x4.CreateTranslation(c.P) * t.World;
                    R.Draw(_plantMound, w, Palette.Mul(col, 0.85f), outlineWidth: 0.9f);
                    R.Draw(_plantBlades, w, col, sway: 2.2f, outlineWidth: 0.8f);
                    break;
                }
            }
        }
    }

    private void DrawRaking(Tank t)
    {
        float k = t.CleanseT / 1.4f;
        float x = (k * 2 - 1) * (t.W / 2 - 0.3f);
        float lift = MathF.Sin(k * MathF.PI);
        var w = Matrix4x4.CreateRotationX(-0.9f) * Matrix4x4.CreateRotationY(0.15f) * Matrix4x4.CreateTranslation(x, SurfY + 0.15f + (1 - lift) * 0.3f, 0.1f) * t.World;
        R.Draw(_rake, w, Color.White, outlineWidth: 1.0f);
    }

    private void DrawHolding()
    {
        float time = R.ReduceMotion ? 0 : _time;
        var held = _critters.Values.Where(c => c.Pool is null).ToList();
        var top = CrockPos + new Vector3(0, 0.56f + SpotOf(SpotHolding).Lift.Value, 0);
        for (int i = 0; i < held.Count; i++)
        {
            var c = held[i];
            var col = SpeciesTint(c.Species);
            float a = time * 0.4f + i * MathF.Tau / held.Count;
            float rr = 0.24f + (i % 2) * 0.1f;
            var p = top + new Vector3(MathF.Cos(a) * rr, 0, MathF.Sin(a) * rr);
            float pop = c.Pop >= 1 ? 1 : Ease.OutBack(c.Pop, 2.2f);
            switch (c.Kind)
            {
                case LifeformKind.Fish:
                    var w = Matrix4x4.CreateScale(0.85f * pop) * Matrix4x4.CreateRotationY(-a - MathF.PI / 2 + MathF.PI) * Matrix4x4.CreateTranslation(p);
                    R.Draw(_fishBody, w, col, emissive: 0.1f, outlineWidth: 0.8f);
                    R.Draw(_fishTail, Matrix4x4.CreateRotationY(MathF.Sin(time * 6 + i) * 0.3f) * Matrix4x4.CreateTranslation(-0.17f, 0, 0) * w, col, outline: false);
                    break;
                case LifeformKind.Snail:
                    var sw = Matrix4x4.CreateRotationY(i) * Matrix4x4.CreateTranslation(CrockPos + new Vector3(MathF.Cos(i * 2.1f) * 0.58f, 0.66f, MathF.Sin(i * 2.1f) * 0.58f));
                    R.Draw(_snailFoot, sw, Color.White, outlineWidth: 0.8f);
                    R.Draw(_snailShell, sw, col, emissive: 0.15f, outlineWidth: 0.8f);
                    break;
                case LifeformKind.Plant:
                    R.Draw(_plantBlades, Matrix4x4.CreateTranslation(top + new Vector3(MathF.Cos(i * 2.4f) * 0.25f, -0.3f, MathF.Sin(i * 2.4f) * 0.25f)), col, sway: 2f, outlineWidth: 0.8f);
                    break;
                case LifeformKind.Mote:
                    R.Glow(top + new Vector3(MathF.Cos(a) * 0.3f, 0.45f + MathF.Sin(time * 1.3f + i) * 0.08f, MathF.Sin(a) * 0.3f), 0.32f, Palette.Alpha(col, 0.85f));
                    break;
            }
        }
    }

    private void DrawUnderwaterGlows(Tank t)
    {
        float time = R.ReduceMotion ? 0 : _time;
        float clear = 1 - t.Murk;
        foreach (var b in t.Bubbles)
            R.Glow(t.ToWorld(b.P), b.Size * 1.6f, Palette.Alpha(Palette.Foam, 0.55f * (0.4f + clear * 0.6f)));
        // dappled light on the sand, brighter in Light-leaning water
        float light = Math.Clamp((t.Energy.Light + 5) / 10f, 0, 1);
        int n = (int)(t.W * 2);
        for (int i = 0; i < n; i++)
        {
            float x = (Hash01(i * 7 + t.Index * 31) * 2 - 1) * (t.W / 2 - 0.25f) + MathF.Sin(time * 0.35f + i * 1.7f) * 0.18f;
            float z = (Hash01(i * 13 + t.Index * 17) * 2 - 1) * (t.D / 2 - 0.2f) + MathF.Cos(time * 0.3f + i) * 0.12f;
            float a = (0.1f + 0.14f * light) * clear * (0.6f + 0.4f * MathF.Sin(time * 1.1f + i * 2.3f));
            R.Glow(t.ToWorld(new Vector3(x, FloorY + 0.03f, z)), 0.42f, Palette.Alpha(Palette.Lerp(Palette.Foam, Palette.LanternHot, light * 0.5f), a));
        }
        // laid things: those with an essence glow; a soak in progress shows a ring of light filling up
        var view = S.Pools.View(t.Id);
        foreach (var l in _laid.Values.Where(x => x.Pool == t.Id))
        {
            var it = S.World.Items.Get(l.Id);
            if (it is null) continue;
            var at = t.ToWorld(l.P + new Vector3(0, 0.1f + (SurfY - FloorY) * (1 - Ease.OutCubic(l.Drop)), 0));
            if (it.Essence is not null && !it.Inert)
                R.Glow(at, 0.5f + (R.ReduceMotion ? 0 : 0.06f * MathF.Sin(_time * 2)), Palette.Alpha(Palette.LanternHot, 0.35f));
            var soak = view.Soaks.FirstOrDefault(s => s.Item == S.World.Items.Name(it));
            if (soak is null) continue;
            const int dots = 16;
            int lit = (int)MathF.Round((float)soak.Progress * dots);
            for (int k = 0; k < dots; k++)
            {
                float a = k * MathF.Tau / dots - MathF.PI / 2;
                var p = t.ToWorld(l.P + new Vector3(MathF.Cos(a) * 0.24f, 0.03f, MathF.Sin(a) * 0.17f));
                bool on = k < lit;
                float pulse = soak.ConditionsMet && !R.ReduceMotion ? 0.75f + 0.25f * MathF.Sin(_time * 3 - k * 0.4f) : 0.6f;
                R.Glow(p, on ? 0.11f : 0.06f, Palette.Alpha(on ? Palette.LanternHot : Palette.Foam, (on ? 0.85f : 0.3f) * pulse));
            }
        }
    }

    private void DrawWaterOf(Tank t)
    {
        var (shallow, deep) = WaterColours(t.Energy);
        float murk = t.Murk;
        R.DrawWater(t.Surface, Matrix4x4.CreateTranslation(0, SurfY, 0) * t.World, shallow, deep, Math.Clamp(murk, 0, 0.9f), 0.5f + murk * 0.42f);
    }

    /// <summary>The water's colour follows the pool's place on the two axes.</summary>
    private (Color Shallow, Color Deep) WaterColours(Energy e)
    {
        float l = e.Light / (float)Math.Max(1, LightAxis.Max), h = e.Heat / (float)Math.Max(1, HeatAxis.Max);
        var shallow = Palette.TealLight; var deep = Palette.Teal;
        if (l > 0) { shallow = Palette.Lerp(shallow, Palette.Hex(0xCDE8B8), l * 0.55f); deep = Palette.Lerp(deep, Palette.Hex(0x4FA88A), l * 0.45f); }
        if (l < 0) { shallow = Palette.Lerp(shallow, Palette.Hex(0x4C6A88), -l * 0.7f); deep = Palette.Lerp(deep, Palette.Hex(0x1E2A48), -l * 0.75f); }
        if (h > 0) shallow = Palette.Lerp(shallow, Palette.Hex(0xEAC98A), h * 0.35f);
        if (h < 0) shallow = Palette.Lerp(shallow, Palette.Hex(0xC4E2F2), -h * 0.45f);
        return (shallow, deep);
    }

    private void DrawRipples(Tank t)
    {
        foreach (var r in t.Ripples)
        {
            if (r.Age < 0) continue;
            float k = r.Age / 1.6f;
            float rad = 0.04f + r.Max * Ease.OutCubic(k);
            var at = t.ToWorld(new Vector3(r.P.X, SurfY + 0.035f, r.P.Y));
            var c = Palette.Alpha(Palette.Foam, 0.75f * (1 - k) * (1 - t.Murk * 0.5f));
            Raylib.DrawCircle3D(at, rad, Vector3.UnitX, 90, c);
            Raylib.DrawCircle3D(at + new Vector3(0, 0.004f, 0), rad * 0.97f, Vector3.UnitX, 90, c);
        }
        // feeding flakes float where they were scattered, thinning out as they're eaten
        if (t.FeedT < 6)
        {
            int n = (int)(14 * (1 - t.FeedT / 6));
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3(t.FeedAt.X + (Hash01(i * 3 + 1) - 0.5f) * 0.7f, SurfY + 0.04f, t.FeedAt.Y + (Hash01(i * 5 + 2) - 0.5f) * 0.4f);
                R.Puff(t.ToWorld(p), 0.06f, Palette.Alpha(Palette.Hex(0xC9A86A), 0.9f));
            }
        }
    }

    private void DrawAbove(Tank t)
    {
        float time = R.ReduceMotion ? 0 : _time;
        foreach (var c in _critters.Values.Where(x => x.Pool == t.Id && x.Kind is LifeformKind.Mote or LifeformKind.Plant))
        {
            var col = SpeciesTint(c.Species);
            float pop = c.Pop >= 1 ? 1 : Ease.OutBack(c.Pop, 2.2f);
            if (c.Kind == LifeformKind.Mote)
            {
                var p = t.ToWorld(c.P + new Vector3(0, MathF.Sin(time * 1.4f + c.Seed) * 0.07f, 0));
                R.Glow(p, 0.55f * pop, Palette.Alpha(col, 0.45f));
                R.Glow(p, 0.16f * pop, Palette.Alpha(Palette.Lerp(col, Color.White, 0.6f), 0.95f));
            }
            else if (S.Content.Lifeforms[c.Species].Emits.Light > 0)
            {
                // light-giving plants glow at their tips
                for (int k = 0; k < 3; k++)
                {
                    float a = k * 2.4f * 2 + c.Seed;
                    var tip = t.ToWorld(c.P + Vector3.Transform(new Vector3(MathF.Cos(k * 4.8f) * 0.14f, (0.34f + (k * 2 % 3) * 0.14f) * 1.3f, MathF.Sin(k * 4.8f) * 0.1f), Matrix4x4.CreateRotationY(c.Seed)));
                    R.Glow(tip, 0.16f * pop, Palette.Alpha(Palette.LanternHot, 0.55f + (R.ReduceMotion ? 0 : 0.2f * MathF.Sin(time * 2 + a))));
                }
            }
        }
        // a sunlit pool glows a little above its surface; a very dark one swallows its own sparkle
        if (t.Energy.Light >= 2)
            R.Glow(t.ToWorld(new Vector3(0, SurfY + 0.25f, 0)), t.W * 0.7f, Palette.Alpha(Palette.LanternHot, 0.05f * t.Energy.Light * (1 - t.Murk)));
        if (t.Energy.Heat <= -2)
            for (int i = 0; i < 5; i++)
            {
                var p = t.ToWorld(new Vector3((Hash01(i + t.Index * 9) * 2 - 1) * t.W / 2, RimY + 0.14f, (i % 2 == 0 ? 1 : -1) * (t.D / 2 + 0.08f)));
                R.Glow(p, 0.12f, Palette.Alpha(Palette.Hex(0xCFE8F6), 0.5f + (R.ReduceMotion ? 0 : 0.3f * MathF.Sin(time * 2 + i))));
            }
    }

    private static string Signed(int v) => v.ToString("+0;-0;0");

    internal static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    public override bool Back()
    {
        if (_card != CardKind.None) { CloseCard(); return true; }
        return base.Back();
    }
}
