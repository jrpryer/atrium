using System.Numerics;
using Atrium.Core;
using Atrium.Core.Runtime;
using Atrium.Diorama.App;
using Atrium.Diorama.Render;
using Raylib_cs;

namespace Atrium.Diorama.Scenes;

/// <summary>
/// The courtyard as a floating diorama, built entirely from code: the slab, the cliff and the city's tiers above,
/// the Slowsilver, the pools, Old Stoic (big, old and unchanging), and the four houses.
/// </summary>
public sealed class HubWorld : IDisposable
{
    public sealed class House
    {
        public required string Locale;
        public required Part Body;
        public required Part Glass;
        /// <summary>Closed shutters, shown while the house is locked.</summary>
        public Part? Shutters;
        /// <summary>Shutters folded open, shown once the house is unlocked (imported models only).</summary>
        public Part? OpenShutters;
        /// <summary>Where chimney smoke rises, in the house's own units (before its scale and turn).</summary>
        public Vector3? Smoke;
        /// <summary>Imported from a model with painted windows (drawn lighter than the built-in plain glass).</summary>
        public bool PaintedGlass;
        public Vector3 Pos;
        public float Yaw, Scale = 1;
        public float Radius, Height;
        /// <summary>The house lamp, relative to the house's base and in courtyard units (already scaled).</summary>
        public Vector3 LampLocal;
        /// <summary>Where the paved path from the tree ends: the model's door-point, or null to stop short of a built-in house.</summary>
        public Vector3? PathEnd;
        public Spring Lift = new(0, 120, 12);
        public float Hover;

        public Matrix4x4 World(float lift) => Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateRotationY(Yaw) * Matrix4x4.CreateTranslation(Pos + new Vector3(0, lift, 0));
        public Vector3 Lamp => Vector3.Transform(LampLocal, Matrix4x4.CreateRotationY(Yaw)) + Pos;
        public Vector3 Door => Vector3.Transform(new Vector3(0, 1.1f, Radius + 1.6f), Matrix4x4.CreateRotationY(Yaw)) + Pos;
        public BoundingBox Box => new(Pos - new Vector3(Radius, 0, Radius), Pos + new Vector3(Radius, Height, Radius));
    }

    public readonly Dictionary<string, House> Houses = new();
    public static readonly Vector3 TreePos = new(-0.8f, 0, -1.4f);
    public static readonly Vector3 FallsBase = new(1.6f, 0.15f, -7.4f);

    private readonly Model _slab, _cliff, _city, _windows, _props, _falls, _stream, _banks;
    /// <summary>The stream's course, from the plunge pool under the falls to the Spirit Pools' cave mouth.</summary>
    private Vector3[] _course = Array.Empty<Vector3>();
    /// <summary>The courtyard's three water basins. Content pools fill them in content order (largest, under the falls, first).</summary>
    private readonly (Model Water, Vector3 Centre, float R, float Squash)[] _pools;
    // Old Stoic, the lamp posts and the houses: imported models where visuals.json lists one, else built in code.
    private Part _trunk, _canopy;
    private Matrix4x4 _treeAt = Matrix4x4.Identity;
    private ModelAsset? _stoic;
    private Part _lampPost;
    private Part? _lampGlass;
    private Matrix4x4[] _lampAt = { Matrix4x4.Identity };
    private Vector3[] _charterLamps = Array.Empty<Vector3>();
    private readonly List<Model> _built = new();
    private readonly Renderer _renderer;
    private readonly Random _rng = new(3);
    private float _inviteTimer;
    private float _mistTimer, _steamTimer;

    public HubWorld(Renderer r)
    {
        _renderer = r;
        var toon = r.Shaders.Toon;
        AddHouses(toon); // first, so the paving can lead to each door and the stream can find the cave mouth
        _course = StreamCourse();
        _slab = BuildSlab(Houses.Values.Select(h => (h.Pos, h.PathEnd)), _course).Model(toon);
        _cliff = BuildCliff().Model(toon);
        (_city, _windows) = BuildCity(toon);
        AddTree(toon);
        _props = BuildProps().Model(toon);
        AddLampPosts(toon);
        _falls = BuildFalls().Model(r.Shaders.Falls);
        // the plunge pool under the falls; the Spirit Pools themselves lie below, in the grotto
        _pools = new[] { (WaterDisc(2.95f, 0.76f).Model(r.Shaders.Water), new Vector3(1.8f, 0.14f, -6.1f), 2.95f, 0.76f) };
        var (water, banks) = BuildStream(_course);
        _stream = water.Model(r.Shaders.Water);
        _banks = banks.Model(toon);
    }

    // ───────────────────────── construction ─────────────────────────

    /// <summary>
    /// The imported model for a courtyard piece, if visuals.json lists one (with its scale) and the file loads. The
    /// renderer keeps it for the session, so the courtyard doesn't unload it.
    /// </summary>
    private ModelAsset? Import(string name, out float scale) =>
        UI.ItemArt.HubModels.TryGetValue(name, out scale) ? _renderer.Import(Path.Combine(AppContext.BaseDirectory, "models", name + ".glb")) : null;

    /// <summary>A model built in code, kept to unload with the courtyard.</summary>
    private Part Built(MeshBuilder m, Shader toon)
    {
        var model = m.Model(toon);
        _built.Add(model);
        return model;
    }

    private void AddTree(Shader toon)
    {
        if (Import("old_stoic", out float scale) is { } stoic)
        {
            // the lift's crown overhangs its +X side, which TreeLean turns out over the big pool
            _stoic = stoic;
            _treeAt = Matrix4x4.CreateScale(scale) * TreeLean();
            _trunk = stoic.PartExcept("canopy");
            _canopy = stoic.Part("canopy");
            return;
        }
        _trunk = Built(BuildTrunk(), toon);
        _canopy = Built(BuildCanopy(), toon);
    }

    /// <summary>Whether a ray meets Old Stoic: its imported mesh, or a box around the built-in shape.</summary>
    public bool HitsTree(Ray ray) => _stoic is { } stoic
        ? stoic.Hit(ray, _treeAt).Hit
        : Raylib.GetRayCollisionBox(ray, new BoundingBox(TreePos + new Vector3(-1.4f, 0, -1.4f), TreePos + new Vector3(2.5f, 9.5f, 1.4f))).Hit;

    private void AddLampPosts(Shader toon)
    {
        if (Import("lamp_post", out float scale) is { } lamp)
        {
            _lampPost = lamp.PartExcept("glass");
            _lampGlass = lamp.Part("glass");
            _lampAt = LampSpots().Select(p =>
            {
                var d = TreePos - p;
                return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateRotationY(MathF.Atan2(d.X, d.Z)) * Matrix4x4.CreateTranslation(p);
            }).ToArray();
            var flame = lamp.Markers.TryGetValue("light", out var f) ? f : new Vector3(0, lamp.Bounds.Max.Y * 0.85f, 0);
            _charterLamps = _lampAt.Select(at => Vector3.Transform(flame, at)).ToArray();
            return;
        }
        var (posts, lamps) = BuildLampPosts();
        _lampPost = Built(posts, toon);
        _charterLamps = lamps;
    }

    /// <summary>The four old charter lamps, on the diagonals around the tree.</summary>
    private static IEnumerable<Vector3> LampSpots()
    {
        for (int i = 0; i < 4; i++)
        {
            float a = MathF.PI / 4 + i * MathF.PI / 2;
            yield return TreePos + new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * 5.1f;
        }
    }

    private static MeshBuilder BuildSlab(IEnumerable<(Vector3 Pos, Vector3? PathEnd)> houses, Vector3[] stream)
    {
        var m = new MeshBuilder();
        var grass = Palette.Hex(0x86A86E);
        m.Cylinder(new Vector3(0, -1.4f, 0), 12.5f, 12.6f, 1.4f, 44, Palette.Hex(0x9C8C74), grass);
        m.Cylinder(new Vector3(0, -6.8f, 0), 3.2f, 12.3f, 5.4f, 22, Palette.Hex(0x7D6E5C), null, 0, 0.12f);
        var under = new Random(17);
        for (int i = 0; i < 9; i++)
        {
            float a = i / 9f * MathF.Tau, d = 4 + (float)under.NextDouble() * 5;
            m.Blob(new Vector3(MathF.Cos(a) * d, -5.2f - (float)under.NextDouble() * 1.8f, MathF.Sin(a) * d), new Vector3(1.6f, 1.4f, 1.6f), 3, 6, Palette.Hex(0x6E604F), 200 + i, 0.25f);
        }
        // paving ring around the tree and paths to the doors
        var stone = Palette.Hex(0xD7C9AE);
        m.Ring(TreePos + new Vector3(0, 0, 0), 4.7f, 3.2f, 0.06f, 28, stone, stone);
        for (int i = 0; i < 36; i++)
        {
            float a = i / 36f * MathF.Tau;
            var p = TreePos + new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * 3.95f;
            m.Box(p + new Vector3(0, 0.07f, 0), new Vector3(0.55f, 0.04f, 0.4f), Palette.Mul(stone, 0.93f + (i % 3) * 0.04f));
        }
        foreach (var (target, end) in houses)
        {
            // from the paving ring to the door (an imported house's door-point), or most of the way to a built-in house
            var from = TreePos + Vector3.Normalize(target - TreePos) * 4.6f;
            var to = end is { } e ? new Vector3(e.X, 0, e.Z) : Vector3.Lerp(from, target, 0.62f);
            if (Vector3.Distance(to, TreePos) <= 4.6f) continue; // the door opens onto the ring itself
            int steps = Math.Max(1, (int)MathF.Round(Vector3.Distance(from, to) / 0.45f));
            for (int i = 0; i <= steps; i++)
            {
                var p = Vector3.Lerp(from, to, i / (float)steps);
                if (stream.Any(c => Vector2.Distance(new Vector2(c.X, c.Z), new Vector2(p.X, p.Z)) < 0.75f)) continue; // the footbridge carries it over
                m.Box(p + new Vector3(0, 0.05f, 0), new Vector3(0.75f, 0.06f, 0.55f), Palette.Mul(stone, 0.95f + (i % 2) * 0.05f));
            }
        }
        // rocks and moss tufts scattered near the edge
        var rng = new Random(4);
        for (int i = 0; i < 26; i++)
        {
            float a = (float)rng.NextDouble() * MathF.Tau, d = 9.5f + (float)rng.NextDouble() * 2.6f;
            var p = new Vector3(MathF.Cos(a) * d, 0.1f, MathF.Sin(a) * d);
            if (p.Z < -6) continue;
            m.Blob(p, new Vector3(0.35f, 0.22f, 0.3f) * (0.6f + (float)rng.NextDouble()), 3, 6, i % 3 == 0 ? Palette.Moss : Palette.Hex(0xA89A86), i, 0.25f);
        }
        return m;
    }

    private static float Noise(float x, float y, int seed)
    {
        static float H(int x, int y, int s) { unchecked { int n = x * 374761393 + y * 668265263 + s * 2147483; n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 0xFFFF) / 65535f; } }
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = H(xi, yi, seed), b = H(xi + 1, yi, seed), c = H(xi, yi + 1, seed), d = H(xi + 1, yi + 1, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    /// <summary>One continuous, weathered canyon wall around the back of the slab, banded in sandstone strata.</summary>
    private static MeshBuilder BuildCliff()
    {
        var m = new MeshBuilder();
        var strata = new[] { Palette.Hex(0xB59B78), Palette.Hex(0xC7B08A), Palette.Hex(0xA88E6C), Palette.Hex(0xBFA581), Palette.Hex(0xAD936F) };
        const int cols = 56, rows = 9;
        const float Base = -1.4f;
        float a0 = MathF.PI * 1.04f, a1 = MathF.PI * 1.96f;
        float fallsA = MathF.Atan2(FallsBase.Z - 4.6f, FallsBase.X);
        if (fallsA < 0) fallsA += MathF.Tau;
        var grid = new Vector3[cols + 1, rows + 1];
        var heights = new float[cols + 1];
        for (int i = 0; i <= cols; i++)
        {
            float a = a0 + (a1 - a0) * i / cols;
            float edge = MathF.Min(i, cols - i) / (float)cols;              // lower toward the ends
            float h = 6.5f + Noise(i * 0.35f, 0, 3) * 4f + MathF.Min(edge * 14, 1) * 2.5f;
            h *= 0.55f + 0.45f * MathF.Min(1, edge * 5);
            if (MathF.Abs(a - fallsA) < 0.2f) h = MathF.Max(h, 11.4f);       // the lip the Slowsilver pours from
            heights[i] = h;
            for (int j = 0; j <= rows; j++)
            {
                float t = j / (float)rows;
                // the foot sits just inside the slab's rim and below the turf, so no seam shows
                float r = 12.1f + t * 1.6f + (Noise(i * 0.6f, j * 0.8f, 7) - 0.5f) * 1.1f * MathF.Min(1, t * 4) + (j % 2) * 0.12f;
                grid[i, j] = new Vector3(MathF.Cos(a) * r, Base + (h - Base) * t, MathF.Sin(a) * r);
            }
        }
        // every face is wound by the direction it should face, so the hull outline never shows through
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, Color col)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0) m.Quad(a, d, c, b, col);
            else m.Quad(a, b, c, d, col);
        }
        static Vector3 Out(Vector3 p) => Vector3.Normalize(new Vector3(p.X, 0, p.Z));
        Vector3 Back(Vector3 p) => p + Out(p) * 2.4f;
        for (int i = 0; i < cols; i++)
            for (int j = 0; j < rows; j++)
            {
                var col = strata[(j + (int)(Noise(i * 0.2f, j, 11) * 2)) % strata.Length];
                var mid = (grid[i, j] + grid[i + 1, j + 1]) / 2;
                Face(grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1], -Out(mid), col);   // the courtyard-facing wall
            }
        // a mossy top, a plain back and an underside, so it reads as solid from any angle
        var moss = Palette.Hex(0x8DA86A); var backCol = Palette.Hex(0x9C8466); var under = Palette.Hex(0x7D6E5C);
        for (int i = 0; i < cols; i++)
        {
            var t0 = grid[i, rows]; var t1 = grid[i + 1, rows];
            var f0 = grid[i, 0]; var f1 = grid[i + 1, 0];
            Face(t0, t1, Back(t1), Back(t0), Vector3.UnitY, moss);
            Face(Back(t0), Back(t1), Back(t1) with { Y = Base }, Back(t0) with { Y = Base }, Out(t0), backCol);
            Face(f0, f1, Back(f1) with { Y = Base }, Back(f0) with { Y = Base }, -Vector3.UnitY, under);
        }
        // end caps where the wall steps down at either side
        foreach (var (i, sign) in new[] { (0, -1f), (cols, 1f) })
        {
            float a = a0 + (a1 - a0) * i / cols;
            var along = new Vector3(-MathF.Sin(a), 0, MathF.Cos(a)) * sign;
            for (int j = 0; j < rows; j++)
            {
                var p0 = grid[i, j]; var p1 = grid[i, j + 1];
                var q0 = Back(grid[i, rows]) with { Y = p0.Y }; var q1 = Back(grid[i, rows]) with { Y = p1.Y };
                Face(p0, p1, q1, q0, along, strata[j % strata.Length]);
            }
        }
        // switchback stairs cut into the left end, toward the Hem
        for (int i = 0; i < 12; i++)
        {
            float t = i / 11f;
            var p = new Vector3(-11.3f + (i % 6 < 3 ? i % 6 : 6 - i % 6) * 0.45f, 0.2f + i * 0.45f, -4.4f - t * 2.6f);
            m.Box(p, new Vector3(1.1f, 0.2f, 0.65f), Palette.Hex(0xD3C4A6));
        }
        return m;
    }

    private static (Model City, Model Windows) BuildCity(Shader toon)
    {
        var m = new MeshBuilder();
        var w = new MeshBuilder();
        var rng = new Random(21);
        var tiers = new[] { (y: 10.5f, z: -18f, n: 13, spread: 15f), (y: 15.5f, z: -24f, n: 15, spread: 19f), (y: 21f, z: -30f, n: 13, spread: 23f) };
        foreach (var (y, z, n, spread) in tiers)
        {
            m.Box(new Vector3(0, y - 1.5f, z + 1.5f), new Vector3(spread * 2.2f, 3f, 5f), Palette.Hex(0xAE9F86));
            for (int i = 0; i < n; i++)
            {
                float x = -spread + i * (spread * 2 / (n - 1)) + (float)rng.NextDouble() * 0.6f;
                float bw = 1.6f + (float)rng.NextDouble() * 1.4f, bh = 2.2f + (float)rng.NextDouble() * 3.4f, bd = 1.8f;
                var col = Palette.Lerp(Palette.Hex(0xCFC0A4), Palette.Hex(0xB8A88E), (float)rng.NextDouble());
                var roof = Palette.Lerp(Palette.Hex(0x8C6A5A), Palette.Hex(0x6E7A8A), (float)rng.NextDouble());
                var bc = new Vector3(x, y + bh / 2, z + (float)rng.NextDouble() * 1.2f);
                m.Box(bc, new Vector3(bw, bh, bd), col);
                m.Gable(bc + new Vector3(0, bh / 2, 0), bw, bd, 0.9f + (float)rng.NextDouble() * 0.6f, roof);
                for (int k = 0; k < 2; k++)
                    if (rng.NextDouble() < 0.7)
                        w.Box(bc + new Vector3(-bw * 0.22f + k * bw * 0.44f, bh * 0.1f, bd / 2 + 0.02f), new Vector3(0.28f, 0.38f, 0.04f), Color.White);
            }
        }
        return (m.Model(toon), w.Model(toon));
    }

    private static Matrix4x4 TreeLean()
    {
        var dir = Vector3.Normalize(new Vector3(1.8f, 0, -6.1f) - TreePos);
        return Matrix4x4.CreateRotationY(MathF.Atan2(-dir.Z, dir.X)) * Matrix4x4.CreateTranslation(TreePos);
    }

    private static MeshBuilder BuildTrunk()
    {
        var bark = Palette.Hex(0x7A5A43);
        var m = new MeshBuilder();
        m.Cylinder(new Vector3(0, 0, 0), 1.05f, 0.82f, 2.3f, 9, bark, null, 0.3f, 0.2f);
        m.Cylinder(new Vector3(0.3f, 2.25f, 0), 0.82f, 0.62f, 1.9f, 9, Palette.Mul(bark, 1.04f), null, 0.5f, 0.35f);
        m.Cylinder(new Vector3(0.8f, 4.1f, 0), 0.62f, 0.42f, 1.5f, 8, bark, null, 0.55f, 0.5f);
        // great limbs: one reaching out over the water, two climbing
        void Limb(Vector3 from, Vector3 to, float r0, float r1)
        {
            var d = to - from;
            float len = d.Length();
            var b = new MeshBuilder().Cylinder(Vector3.Zero, r0, r1, len, 7, Palette.Mul(bark, 0.96f));
            var up = Vector3.UnitY;
            var axis = Vector3.Cross(up, Vector3.Normalize(d));
            float ang = MathF.Acos(Math.Clamp(Vector3.Dot(up, Vector3.Normalize(d)), -1, 1));
            var rot = axis.LengthSquared() < 1e-6f ? Matrix4x4.Identity : Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(axis), ang);
            m.Append(b.Transform(rot * Matrix4x4.CreateTranslation(from)));
        }
        Limb(new Vector3(1.2f, 5.3f, 0), new Vector3(4.6f, 6.6f, 0.4f), 0.42f, 0.2f);
        Limb(new Vector3(1.0f, 5.2f, 0), new Vector3(-1.2f, 7.4f, 1.2f), 0.38f, 0.18f);
        Limb(new Vector3(1.1f, 5.2f, 0), new Vector3(1.4f, 7.8f, -1.8f), 0.36f, 0.16f);
        // roots spreading across the paving
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * MathF.Tau + 0.3f;
            var p = new Vector3(MathF.Cos(a) * 1.05f, 0.12f, MathF.Sin(a) * 1.05f);
            m.Blob(p, new Vector3(0.75f, 0.28f, 0.42f), 3, 6, Palette.Mul(bark, 0.92f), 50 + i, 0.15f);
        }
        // the old nail-holes, at the height of a raised hand
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * MathF.Tau + (i % 3) * 0.1f;
            m.Box(new Vector3(MathF.Cos(a) * 0.96f + 0.13f, 1.45f + (i * 7 % 5) * 0.07f, MathF.Sin(a) * 0.96f), new Vector3(0.05f, 0.05f, 0.05f), Palette.Hex(0x5A4232));
        }
        return m.Transform(TreeLean());
    }

    private static MeshBuilder BuildCanopy()
    {
        var m = new MeshBuilder();
        var greens = new[] { Palette.Hex(0x6E9F5E), Palette.Hex(0x7FA36B), Palette.Hex(0x5F914F), Palette.Hex(0x86AC6E) };
        var under = Palette.Hex(0x4E7A47);
        var spots = new (Vector3 P, Vector3 R)[]
        {
            (new(1.6f, 7.2f, 0), new(2.6f, 1.9f, 2.4f)), (new(4.4f, 6.6f, 0.3f), new(2.0f, 1.5f, 1.9f)),
            (new(-1.2f, 7.4f, 1.0f), new(2.0f, 1.6f, 1.8f)), (new(1.5f, 7.6f, -2.2f), new(2.0f, 1.6f, 1.8f)),
            (new(0.4f, 8.6f, 0.2f), new(1.8f, 1.3f, 1.7f)), (new(3.0f, 8.0f, -1.0f), new(1.6f, 1.2f, 1.5f)),
            (new(2.6f, 6.6f, 2.2f), new(1.9f, 1.4f, 1.6f)), (new(-0.4f, 6.5f, -1.6f), new(1.6f, 1.2f, 1.4f)),
            (new(5.9f, 6.0f, -0.4f), new(1.3f, 1.0f, 1.3f)),
        };
        for (int i = 0; i < spots.Length; i++) m.Blob(spots[i].P, spots[i].R, 4, 8, greens[i % greens.Length], 70 + i, 0.14f, under);
        return m.Transform(TreeLean());
    }

    private static MeshBuilder BuildProps()
    {
        var m = new MeshBuilder();
        var rim = Palette.Hex(0xC8B898);
        m.Ring(new Vector3(1.8f, 0, -6.1f), 3.3f, 2.95f, 0.34f, 30, rim, Palette.Mul(rim, 1.05f), 0.76f);
        m.Disc(new Vector3(1.8f, 0.03f, -6.1f), 2.95f, 30, Palette.Hex(0x2C5E60), 0.76f);
        // benches by the water, where the Keeper sits
        void Bench(Vector3 p, float yaw)
        {
            var b = new MeshBuilder();
            var wood = Palette.Hex(0x9A7454);
            b.Box(new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.12f, 0.5f), wood);
            b.Box(new Vector3(-0.7f, 0.2f, 0), new Vector3(0.14f, 0.4f, 0.42f), Palette.Mul(wood, 0.8f));
            b.Box(new Vector3(0.7f, 0.2f, 0), new Vector3(0.14f, 0.4f, 0.42f), Palette.Mul(wood, 0.8f));
            m.Append(b.Transform(Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(p)));
        }
        Bench(new Vector3(-0.6f, 0, -5.2f), 0.4f);
        Bench(new Vector3(3.6f, 0, -3.1f), -0.6f);
        // crates and barrels by the Apothecary
        m.Box(new Vector3(8.4f, 0.35f, 3.0f), new Vector3(0.7f, 0.7f, 0.7f), Palette.Hex(0xA98058));
        m.Box(new Vector3(8.6f, 0.95f, 3.1f), new Vector3(0.5f, 0.5f, 0.5f), Palette.Hex(0xB98E62));
        m.Cylinder(new Vector3(8.1f, 0, 5.4f), 0.4f, 0.36f, 0.9f, 10, Palette.Hex(0x8C6644), Palette.Hex(0x7A5A3C));
        // lily pads
        var rng = new Random(9);
        for (int i = 0; i < 9; i++)
        {
            var p = new Vector3(1.8f, 0.17f, -6.1f) + new Vector3(((float)rng.NextDouble() - 0.5f) * 4.2f, 0, ((float)rng.NextDouble() - 0.5f) * 2.6f);
            if (Vector3.Distance(p, FallsBase) < 1.6f) continue;
            m.Disc(p, 0.22f + (float)rng.NextDouble() * 0.12f, 7, Palette.Hex(0x6E9F5E));
        }
        return m;
    }

    private static (MeshBuilder, Vector3[]) BuildLampPosts()
    {
        var m = new MeshBuilder();
        var lamps = new List<Vector3>();
        foreach (var p in LampSpots())
        {
            m.Cylinder(p, 0.09f, 0.07f, 2.1f, 6, Palette.Hex(0x3E3A3E));
            m.Box(p + new Vector3(0, 2.25f, 0), new Vector3(0.34f, 0.36f, 0.34f), Palette.Hex(0xF0E2C0));
            m.Cone(p + new Vector3(0, 2.43f, 0), 0.28f, 0.28f, 4, Palette.Hex(0x3E3A3E));
            lamps.Add(p + new Vector3(0, 2.25f, 0));
        }
        return (m, lamps.ToArray());
    }

    private static MeshBuilder BuildFalls()
    {
        var m = new MeshBuilder();
        int segs = 14;
        Vector3 Spine(float t) => new(FallsBase.X, FallsBase.Y + (1 - t) * 11.2f, FallsBase.Z - 4.6f + MathF.Pow(t, 0.55f) * 4.6f - MathF.Sin(t * MathF.PI) * 0.4f);
        for (int i = 0; i < segs; i++)
        {
            float t0 = i / (float)segs, t1 = (i + 1) / (float)segs;
            float w0 = 1.0f + t0 * 0.45f, w1 = 1.0f + t1 * 0.45f;
            var a = Spine(t0); var b = Spine(t1);
            var a0 = a - new Vector3(w0, 0, 0); var a1 = a + new Vector3(w0, 0, 0);
            var b0 = b - new Vector3(w1, 0, 0); var b1 = b + new Vector3(w1, 0, 0);
            m.TriUv(a0, b0, b1, new Vector2(0, t0), new Vector2(0, t1), new Vector2(1, t1), Color.White);
            m.TriUv(a0, b1, a1, new Vector2(0, t0), new Vector2(1, t1), new Vector2(1, t0), Color.White);
        }
        return m;
    }

    /// <summary>
    /// The stream: out of the plunge pool, an S past the tree's side of the courtyard, and into the Spirit Pools' cave
    /// mouth (the left half of its opening; the steps take the right), where it goes underground. Smoothed through a few points.
    /// </summary>
    private Vector3[] StreamCourse()
    {
        var pts = new List<Vector3> { new(5.3f, 0, -5.7f), new(6.0f, 0, -4.4f), new(6.3f, 0, -3.0f), new(6.9f, 0, -1.6f), new(8.0f, 0, -1.0f) };
        if (Houses.TryGetValue("pools", out var cave))
        {
            var w = cave.World(0);
            pts.Add(Vector3.Transform(new Vector3(-0.45f, 0, 2.5f), w) with { Y = 0 });
            pts.Add(Vector3.Transform(new Vector3(-0.45f, 0, 0.1f), w) with { Y = 0 });
        }
        var course = new List<Vector3>();
        for (int i = 0; i < pts.Count - 1; i++)
        {
            var (p0, p1, p2, p3) = (pts[Math.Max(i - 1, 0)], pts[i], pts[i + 1], pts[Math.Min(i + 2, pts.Count - 1)]);
            int n = Math.Max(2, (int)(Vector3.Distance(p1, p2) / 0.25f));
            for (int k = 0; k < n; k++)
            {
                float t = k / (float)n;
                course.Add(0.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (3 * p1 - p0 - 3 * p2 + p3) * t * t * t));
            }
        }
        course.Add(pts[^1]);
        return course.ToArray();
    }

    /// <summary>The stream's water, and its bed, rocky banks and the footbridge where the cave's path crosses it.</summary>
    private (MeshBuilder Water, MeshBuilder Banks) BuildStream(Vector3[] course)
    {
        var water = new MeshBuilder();
        var m = new MeshBuilder();
        if (course.Length < 2) return (water, m);
        var rng = new Random(23);
        float R01() => (float)rng.NextDouble();
        Vector3 Side(int i) => Vector3.Normalize(Vector3.Cross(Vector3.UnitY, course[Math.Min(i + 1, course.Length - 1)] - course[Math.Max(i - 1, 0)]));
        for (int i = 0; i < course.Length - 1; i++)
        {
            var (a, b) = (course[i], course[i + 1]);
            var (sa, sb) = (Side(i), Side(i + 1));
            water.Quad(a - sa * 0.42f + new Vector3(0, 0.08f, 0), b - sb * 0.42f + new Vector3(0, 0.08f, 0), b + sb * 0.42f + new Vector3(0, 0.08f, 0), a + sa * 0.42f + new Vector3(0, 0.08f, 0), Color.White);
            m.Quad(a - sa * 0.62f + new Vector3(0, 0.03f, 0), b - sb * 0.62f + new Vector3(0, 0.03f, 0), b + sb * 0.62f + new Vector3(0, 0.03f, 0), a + sa * 0.62f + new Vector3(0, 0.03f, 0), Palette.Hex(0x2A4A44));
            if (i % 2 == 0)
                foreach (var side in new[] { -1f, 1f })
                {
                    var p = a + sa * side * (0.66f + R01() * 0.12f);
                    float s = 0.14f + R01() * 0.14f;
                    m.Blob(p + new Vector3(0, s * 0.4f, 0), new Vector3(s * 1.3f, s * 0.75f, s), 3, 6, Palette.Mul(Palette.Hex(0x9A8E78), 0.85f + R01() * 0.25f), 700 + i * 2 + (side > 0 ? 1 : 0), 0.25f);
                    if (R01() < 0.35f) m.Blob(p + new Vector3(0, s * 0.95f, 0), new Vector3(s, 0.05f, s * 0.8f), 2, 6, Palette.Moss, 900 + i, 0.25f);
                }
        }
        // the footbridge, where the path to the cave mouth crosses the stream
        if (Houses.TryGetValue("pools", out var cave) && cave.PathEnd is { } door)
        {
            var from = TreePos + Vector3.Normalize(cave.Pos - TreePos) * 4.6f;
            int best = -1; float bestD = 0.75f;
            for (int i = 0; i < course.Length * 7 / 10; i++)
            {
                var c = course[i];
                var ab = door - from; float t = Math.Clamp(Vector3.Dot(c - from, ab) / ab.LengthSquared(), 0, 1);
                float d = Vector2.Distance(new Vector2(c.X, c.Z), new Vector2(from.X + ab.X * t, from.Z + ab.Z * t));
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best >= 0)
            {
                var u = Vector3.Normalize(new Vector3(door.X - from.X, 0, door.Z - from.Z));
                var bridge = new MeshBuilder();
                var stone = Palette.Hex(0xD7C9AE);
                for (int k = 0; k < 8; k++)
                {
                    float t = (k + 0.5f) / 8, x = -1.2f + 2.4f * t, y = 0.1f + 0.38f * MathF.Sin(MathF.PI * t);
                    bridge.Box(new Vector3(x, y, 0), new Vector3(0.31f, 0.12f, 1.0f), Palette.Mul(stone, 0.94f + (k % 2) * 0.06f));
                    foreach (var z in new[] { -0.47f, 0.47f }) bridge.Box(new Vector3(x, y + 0.13f, z), new Vector3(0.31f, 0.16f, 0.08f), Palette.Mul(stone, 0.85f));
                }
                bridge.Box(new Vector3(0, 0.22f, 0), new Vector3(0.9f, 0.22f, 0.86f), Palette.Mul(stone, 0.7f)); // the arch's spandrel
                m.Append(bridge.Transform(Matrix4x4.CreateRotationY(MathF.Atan2(-u.Z, u.X)) * Matrix4x4.CreateTranslation(course[best] with { Y = 0 })));
            }
        }
        return (water, m);
    }

    private static MeshBuilder WaterDisc(float r, float squash)
    {
        var m = new MeshBuilder();
        int rings = 6, sides = 32;
        for (int i = 0; i < rings; i++)
            for (int s = 0; s < sides; s++)
            {
                float r0 = r * i / rings, r1 = r * (i + 1) / rings;
                float a0 = MathF.Tau * s / sides, a1 = MathF.Tau * (s + 1) / sides;
                Vector3 P(float rr, float a) => new(MathF.Cos(a) * rr, 0, MathF.Sin(a) * rr * squash);
                if (i == 0) m.Tri(Vector3.Zero, P(r1, a1), P(r1, a0), Color.White);
                else m.Quad(P(r0, a0), P(r0, a1), P(r1, a1), P(r1, a0), Color.White);
            }
        return m;
    }

    private void AddHouses(Shader toon)
    {
        Vector3 face = TreePos;
        House Make(string locale, Vector3 pos, MeshBuilder body, MeshBuilder glass, float radius, float height, Vector3 lamp, MeshBuilder? shutters = null)
        {
            var d = face - pos;
            return Houses[locale] = new House
            {
                Locale = locale, Body = Built(body, toon), Glass = Built(glass, toon), Shutters = shutters is null ? null : Built(shutters, toon),
                Pos = pos, Yaw = MathF.Atan2(d.X, d.Z), Radius = radius, Height = height, LampLocal = lamp,
            };
        }
        // a house from an imported model: its parts, size and lamp come from the file
        House Imported(string locale, Vector3 pos, ModelAsset a, float scale, float turn)
        {
            var d = face - pos;
            var b = a.Bounds;
            float radius = MathF.Max(MathF.Max(-b.Min.X, b.Max.X), MathF.Max(-b.Min.Z, b.Max.Z)) * scale;
            var lamp = a.Markers.TryGetValue("lamp", out var l) ? l : new Vector3(0, b.Max.Y * 0.25f, b.Max.Z);
            var h = Houses[locale] = new House
            {
                Locale = locale, Body = a.PartExcept("glass", "shutters", "shutters-open"), Glass = a.Part("glass"), PaintedGlass = true,
                Shutters = a.Has("shutters") ? a.Part("shutters") : null,
                OpenShutters = a.Has("shutters-open") ? a.Part("shutters-open") : null,
                Smoke = a.Markers.TryGetValue("chimney", out var c) ? c : null,
                Pos = pos, Yaw = MathF.Atan2(d.X, d.Z) + turn, Scale = scale, Radius = radius, Height = b.Max.Y * scale, LampLocal = lamp * scale,
            };
            if (a.Markers.TryGetValue("door-point", out var door)) h.PathEnd = Vector3.Transform(door, h.World(0));
            return h;
        }
        // each house uses its imported model if visuals.json lists one, else its built-in shape (turn: radians away
        // from facing the tree, where a model needs a slightly different site than its built-in shape)
        bool TryImport(string locale, Vector3 pos, float turn = 0)
        {
            if (Import(locale, out float scale) is not { } a) return false;
            Imported(locale, pos, a, scale, turn);
            return true;
        }

        // The Library: tall, narrow, grey stone, steep blue roof, green door, a lamp that never quite goes out.
        if (!TryImport("library", new Vector3(-7.2f, 0, -3.2f)))
        {
            var b = new MeshBuilder(); var g = new MeshBuilder();
            var stone = Palette.Hex(0xBDB6AA);
            b.Box(new Vector3(0, 2.8f, 0), new Vector3(2.8f, 5.6f, 2.6f), stone, Palette.Mul(stone, 1.05f));
            b.Box(new Vector3(0, 0.25f, 0), new Vector3(3.0f, 0.5f, 2.8f), Palette.Mul(stone, 0.88f));
            b.Gable(new Vector3(0, 5.6f, 0), 2.8f, 2.6f, 2.5f, Palette.Library);
            b.Box(new Vector3(0, 0.85f, 1.32f), new Vector3(0.95f, 1.7f, 0.1f), Palette.Hex(0x4E7F4A));
            b.Box(new Vector3(0, 1.85f, 1.36f), new Vector3(0.24f, 0.2f, 0.2f), Palette.Hex(0x3E3A3E));
            b.Box(new Vector3(0.9f, 6.2f, -0.6f), new Vector3(0.4f, 1.2f, 0.4f), Palette.Mul(stone, 0.9f));
            for (int i = 0; i < 2; i++)
            {
                g.Box(new Vector3(-0.75f + i * 1.5f, 3.4f, 1.31f), new Vector3(0.45f, 1.1f, 0.06f), Color.White);
                g.Box(new Vector3(-0.75f + i * 1.5f, 1.4f, 1.31f), new Vector3(0.38f, 0.7f, 0.06f), Color.White);
            }
            g.Box(new Vector3(0, 4.6f, 1.31f), new Vector3(0.5f, 0.5f, 0.06f), Color.White);
            Make("library", new Vector3(-7.2f, 0, -3.2f), b, g, 1.9f, 8.3f, new Vector3(0, 2.0f, 1.55f));
        }
        // The Spirit Pools: a cave mouth set into the cliff's end where the stream goes underground, down to the grotto
        // (models/pools.glb), turned toward the courtyard so its opening shows. The built-in shape is still iteration
        // 2's lodge, for when the model is missing.
        if (!TryImport("pools", new Vector3(10.4f, 0, -3.2f), 55 * MathF.PI / 180))
        {
            var b = new MeshBuilder(); var g = new MeshBuilder(); var sh = new MeshBuilder();
            var wood = Palette.Hex(0xA07A58);
            b.Box(new Vector3(0, 1.0f, 0), new Vector3(2.9f, 2.0f, 2.4f), wood, Palette.Mul(wood, 1.05f));
            b.Gable(new Vector3(0, 2.0f, 0), 2.9f, 2.4f, 1.4f, Palette.Pools, 0.35f);
            b.Box(new Vector3(-1.2f, 0.8f, 1.6f), new Vector3(0.14f, 1.6f, 0.14f), Palette.Mul(wood, 0.8f));
            b.Box(new Vector3(1.2f, 0.8f, 1.6f), new Vector3(0.14f, 1.6f, 0.14f), Palette.Mul(wood, 0.8f));
            b.Box(new Vector3(0, 0.08f, 1.5f), new Vector3(2.8f, 0.16f, 0.9f), Palette.Mul(wood, 0.9f));
            b.Box(new Vector3(0, 0.75f, 1.22f), new Vector3(0.8f, 1.5f, 0.08f), Palette.Mul(wood, 0.7f));
            g.Box(new Vector3(-0.95f, 1.15f, 1.21f), new Vector3(0.6f, 0.55f, 0.05f), Color.White);
            g.Box(new Vector3(0.95f, 1.15f, 1.21f), new Vector3(0.6f, 0.55f, 0.05f), Color.White);
            sh.Box(new Vector3(-0.95f, 1.15f, 1.25f), new Vector3(0.66f, 0.6f, 0.06f), Palette.Hex(0x6E5440));
            sh.Box(new Vector3(0.95f, 1.15f, 1.25f), new Vector3(0.66f, 0.6f, 0.06f), Palette.Hex(0x6E5440));
            Make("pools", new Vector3(7.4f, 0, -4.6f), b, g, 1.9f, 3.6f, new Vector3(0, 1.85f, 1.65f), sh);
        }
        // The Apothecary: a low shop, purple roof, a bowed window full of jars, a chimney that steams.
        if (!TryImport("apothecary", new Vector3(5.8f, 0, 4.4f)))
        {
            var b = new MeshBuilder(); var g = new MeshBuilder();
            var plaster = Palette.Hex(0xDCC8A6);
            b.Box(new Vector3(0, 1.25f, 0), new Vector3(3.8f, 2.5f, 3.0f), plaster, Palette.Mul(plaster, 1.04f));
            b.Gable(new Vector3(0, 2.5f, 0), 3.8f, 3.0f, 1.6f, Palette.Apothecary, 0.25f);
            b.Box(new Vector3(1.2f, 3.6f, -0.5f), new Vector3(0.55f, 1.6f, 0.55f), Palette.Hex(0x9C6B54));
            b.Box(new Vector3(-1.0f, 0.95f, 1.52f), new Vector3(0.9f, 1.9f, 0.1f), Palette.Hex(0x6E4E7E));
            b.Box(new Vector3(0, 2.05f, 1.75f), new Vector3(1.0f, 0.35f, 0.08f), Palette.Apothecary);
            g.Cylinder(new Vector3(0.75f, 0.55f, 1.5f), 0.7f, 0.7f, 1.2f, 10, Color.White);
            Make("apothecary", new Vector3(5.8f, 0, 4.4f), b, g, 2.4f, 4.6f, new Vector3(-1.0f, 2.1f, 1.75f)).Smoke = new Vector3(1.2f, 4.5f, -0.5f);
        }
        // The Sanctum: a round stone lodge with an ochre dome and a little spire.
        if (!TryImport("sanctum", new Vector3(-5.6f, 0, 4.6f)))
        {
            var b = new MeshBuilder(); var g = new MeshBuilder();
            var stone = Palette.Hex(0xCDBFA2);
            b.Cylinder(Vector3.Zero, 2.0f, 1.9f, 3.0f, 14, stone, Palette.Mul(stone, 1.05f));
            b.Blob(new Vector3(0, 3.0f, 0), new Vector3(2.1f, 1.7f, 2.1f), 5, 14, Palette.Sanctum, 91, 0.02f);
            b.Cone(new Vector3(0, 4.55f, 0), 0.22f, 1.1f, 6, Palette.Mul(Palette.Sanctum, 0.85f));
            b.Box(new Vector3(0, 0.95f, 1.92f), new Vector3(0.9f, 1.9f, 0.2f), Palette.Hex(0x7A5A43));
            for (int i = -1; i <= 1; i += 2)
                g.Cylinder(new Vector3(i * 1.25f, 1.75f, 1.48f), 0.26f, 0.26f, 0.06f, 10, Color.White);
            Make("sanctum", new Vector3(-5.6f, 0, 4.6f), b, g, 2.3f, 5.7f, new Vector3(0, 2.2f, 2.15f));
        }
    }

    // ───────────────────────── per-frame ─────────────────────────

    /// <summary>Sets the sun, sky and fog from the world clock's time of day (never gloomy).</summary>
    public static void ApplyTimeOfDay(Lighting l, float phase)
    {
        (float P, uint Sun, uint Sky, uint Ground, uint Fog, float Night)[] keys =
        {
            // aligned with the core's eight shadow-hours: dawn at 0, noon at 3/8, evening at 6/8, night the last eighth
            (0.00f, 0xFFC9A0, 0xC8B8D8, 0x8A7466, 0xE9CDB8, 0.25f),
            (0.10f, 0xFFE2BE, 0xC2CADA, 0x8E7C6C, 0xECD8C0, 0.05f),
            (0.40f, 0xFFF2DC, 0xBCD2E2, 0x8E8070, 0xEBDFC6, 0.0f),
            (0.64f, 0xFFDDA6, 0xD6C6A6, 0x8C7866, 0xEDD2A8, 0.05f),
            (0.77f, 0xF5AE7C, 0xA894B8, 0x725E66, 0xD2AE9C, 0.45f),
            (0.87f, 0x8A9CCC, 0x4C5A86, 0x3A3A50, 0x5A6690, 0.95f),
            (0.97f, 0x8A9CCC, 0x4C5A86, 0x3A3A50, 0x5A6690, 0.95f),
            (1.00f, 0xFFC9A0, 0xC8B8D8, 0x8A7466, 0xE9CDB8, 0.25f),
        };
        int k = 0;
        while (k < keys.Length - 2 && phase > keys[k + 1].P) k++;
        var a = keys[k]; var b = keys[k + 1];
        float t = Ease.InOutSine((phase - a.P) / (b.P - a.P));
        l.Sun = Palette.Lerp(Palette.Hex(a.Sun), Palette.Hex(b.Sun), t);
        l.Sky = Palette.Lerp(Palette.Hex(a.Sky), Palette.Hex(b.Sky), t);
        l.Ground = Palette.Lerp(Palette.Hex(a.Ground), Palette.Hex(b.Ground), t);
        l.Fog = Palette.Lerp(Palette.Hex(a.Fog), Palette.Hex(b.Fog), t);
        l.Night = a.Night + (b.Night - a.Night) * t;
        float az = phase * MathF.Tau * 0.9f + 0.6f;
        float el = 0.35f + 0.45f * MathF.Sin(MathF.Min(phase / 0.82f, 1) * MathF.PI);
        l.SunDir = Vector3.Normalize(new Vector3(MathF.Cos(az), el, MathF.Sin(az) * 0.6f + 0.5f));
        l.FogNear = 52; l.FogFar = 150;
    }

    public void Update(float dt, Particles p, GameSession? s, bool reduceMotion)
    {
        // an open door you haven't been through yet lets a few warm motes drift out to meet you
        _inviteTimer -= dt;
        if (s is not null && _inviteTimer <= 0)
        {
            _inviteTimer = reduceMotion ? 0.9f : 0.35f;
            foreach (var h in Houses.Values)
                if (s.IsUnlocked(h.Locale) && !s.State.VisitedLocales.Contains(h.Locale))
                    p.Emit(Doorway(h) + new Vector3(p.R(-0.4f, 0.4f), p.R(-0.2f, 0.4f), p.R(-0.4f, 0.4f)),
                           new Vector3(p.R(-0.15f, 0.15f), p.R(0.25f, 0.55f), p.R(-0.15f, 0.15f)), p.R(2.2f, 3.4f), p.R(0.18f, 0.3f),
                           Palette.Alpha(Palette.LanternHot, 0.85f), true, -0.04f, 0.3f);
        }
        _mistTimer -= dt;
        if (_mistTimer <= 0 && !reduceMotion)
        {
            _mistTimer = 0.05f;
            var at = FallsBase + new Vector3(p.R(-1.2f, 1.2f), 0.2f, p.R(-0.3f, 0.5f));
            p.Emit(at, new Vector3(p.R(-0.3f, 0.3f), p.R(0.4f, 1.1f), p.R(0.2f, 0.8f)), p.R(1.6f, 2.8f), p.R(0.5f, 1.0f), Palette.Alpha(Palette.Foam, 0.35f), false, 0.7f, 0.6f);
            if (_rng.NextDouble() < 0.4)
                p.Emit(FallsBase + new Vector3(p.R(-1f, 1f), 0.15f, p.R(-0.2f, 0.4f)), new Vector3(p.R(-0.6f, 0.6f), p.R(0.8f, 1.6f), p.R(0.1f, 0.9f)), 0.6f, 0.14f, Palette.Alpha(Color.White, 0.8f), false, -0.05f, 0.4f, -3f);
        }
        _steamTimer -= dt;
        if (_steamTimer <= 0 && !reduceMotion && Houses.TryGetValue("apothecary", out var apo) && apo.Smoke is { } smoke)
        {
            _steamTimer = 0.35f;
            var chimney = Vector3.Transform(smoke, apo.World(0));
            p.Emit(chimney, new Vector3(p.R(-0.1f, 0.1f), p.R(0.5f, 0.8f), p.R(-0.1f, 0.1f)), p.R(2.5f, 3.5f), 0.35f, Palette.Alpha(Palette.Hex(0xF4EEE6), 0.4f), false, 0.45f, 0.2f);
        }
        foreach (var h in Houses.Values) { h.Lift.Update(dt); }
    }

    public void Draw(Renderer r, GameSession? s, string? hovered)
    {
        var l = r.Light;
        l.Lamps.Clear();
        bool lampsRelit = UI.ItemArt.CharterLampsBeat is { } beat && s?.State.FiredBeats.Contains(beat) == true;
        float night = l.Night;
        foreach (var h in Houses.Values)
        {
            bool open = s is null || s.IsUnlocked(h.Locale);
            if (open) l.Lamps.Add((h.Lamp, 5.5f, Palette.Lantern, 0.35f + night * 1.1f));
        }
        foreach (var c in _charterLamps)
            if (lampsRelit) l.Lamps.Add((c, 6f, Palette.LanternHot, 0.3f + night * 1.2f));
        // Shaders.Apply ran in Begin3D before lamps were known; re-upload this frame's lights.
        r.Shaders.Apply(l, Camera, r.Time, new Vector2(Raylib.GetRenderWidth(), Raylib.GetRenderHeight()));

        r.Draw(_slab, Matrix4x4.Identity, Color.White, outlineWidth: 1.6f);
        r.Draw(_cliff, Matrix4x4.Identity, Color.White, outlineWidth: 1.4f);
        r.Draw(_city, Matrix4x4.Identity, Color.White, outline: false);
        r.Draw(_windows, Matrix4x4.Identity, Palette.Lerp(Palette.Hex(0x6A6460), Palette.LanternHot, 0.35f + night * 0.65f), emissive: 0.4f + night * 1.4f, outline: false);
        r.Draw(_props, Matrix4x4.Identity, Color.White, outlineWidth: 1.4f);
        r.Draw(_banks, Matrix4x4.Identity, Color.White, outlineWidth: 1.2f);
        r.Draw(_trunk, _treeAt, Color.White, outlineWidth: 2.2f);
        r.Draw(_canopy, _treeAt, Color.White, sway: 0.25f, outlineWidth: 2.2f);
        foreach (var at in _lampAt)
        {
            r.Draw(_lampPost, at, Color.White, emissive: lampsRelit && _lampGlass is null ? 0.2f + night * 0.6f : 0, outlineWidth: 1.4f);
            if (_lampGlass is { } lampGlass)
                r.Draw(lampGlass, at, lampsRelit ? Palette.Lerp(Color.White, Palette.LanternHot, 0.4f + night * 0.6f) : Palette.Hex(0x8C8880),
                       emissive: lampsRelit ? 0.35f + night * 1.2f : 0, outline: false);
        }

        foreach (var h in Houses.Values)
        {
            bool open = s is null || s.IsUnlocked(h.Locale);
            h.Lift.Target = hovered == h.Locale ? 0.18f : 0;
            h.Hover = Ease.Approach(h.Hover, hovered == h.Locale ? 1 : 0, 10, 1 / 60f);
            var world = h.World(h.Lift.Value);
            var accent = Palette.Locale(h.Locale);
            r.Draw(h.Body, world, open ? Color.White : Palette.Hex(0xB8B0A8), highlight: Palette.Mul(accent, h.Hover * 0.9f), outlineWidth: 2.0f + h.Hover * 1.2f,
                   ink: Palette.Lerp(Palette.Ink, accent, h.Hover));
            var glassTint = !open ? Palette.Hex(0x4A4642)
                : h.PaintedGlass ? Palette.Lerp(Color.White, Palette.LanternHot, 0.25f + night * 0.6f)
                : Palette.Lerp(Palette.Hex(0x8A8478), Palette.LanternHot, 0.45f + night * 0.55f);
            r.Draw(h.Glass, world, glassTint, emissive: open ? 0.35f + night * 1.2f : 0, outline: false);
            if (!open && h.Shutters is { } sh) r.Draw(sh, world, Color.White, outlineWidth: 1.2f);
            if (open && h.OpenShutters is { } osh) r.Draw(osh, world, Color.White, outlineWidth: 1.2f);
        }

        // water and falls (transparent) last
        foreach (var (water, centre, _, _) in _pools)
            r.DrawWater(water, Matrix4x4.CreateTranslation(centre), Palette.TealLight, Palette.Teal, 0.12f);
        r.DrawWater(_stream, Matrix4x4.Identity, Palette.TealLight, Palette.Teal, 0.1f);
        r.DrawFalls(_falls, Matrix4x4.Identity, Palette.Alpha(Palette.Hex(0xBFE6E0), 0.85f));

        foreach (var h in Houses.Values)
            if (s is null || s.IsUnlocked(h.Locale)) r.Glow(h.Lamp, 0.9f + night * 0.9f, Palette.Alpha(Palette.LanternHot, 0.55f + night * 0.4f));
        if (s is not null)
            foreach (var h in Houses.Values)
                if (s.IsUnlocked(h.Locale) && !s.State.VisitedLocales.Contains(h.Locale))
                {
                    float pulse = r.ReduceMotion ? 0.75f : 0.6f + 0.3f * MathF.Sin(r.Time * 2.1f);
                    r.Glow(Doorway(h), 2.6f * pulse, Palette.Alpha(Palette.LanternHot, 0.45f * pulse));
                }
        if (lampsRelit) foreach (var c in _charterLamps) r.Glow(c, 1.1f + night * 1.1f, Palette.Alpha(Palette.LanternHot, 0.6f + night * 0.4f));
    }

    public Camera3D Camera { get; set; }

    /// <summary>Just outside a house's door, at about head height.</summary>
    private static Vector3 Doorway(House h) => h.Pos + (h.Door - h.Pos) * (h.Radius + 0.35f) / (h.Radius + 1.6f);

    /// <summary>A species' body colour (look data in Assets/visuals.json).</summary>
    public static Color CreatureColor(string species) => UI.ItemArt.LifeformColor(species);

    public void Dispose()
    {
        foreach (var m in new[] { _slab, _cliff, _city, _windows, _props, _falls, _stream, _banks }) Raylib.UnloadModel(m);
        foreach (var p in _pools) Raylib.UnloadModel(p.Water);
        foreach (var m in _built) Raylib.UnloadModel(m);
    }
}
