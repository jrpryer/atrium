using System.Numerics;
using Atrium.Core;
using Atrium.Core.Runtime;
using Atrium.Diorama.App;
using Atrium.Diorama.Render;
using Raylib_cs;

namespace Atrium.Diorama.Scenes;

/// <summary>
/// The courtyard as a diorama, built from code and Blender models: a square stone block, the city's terraces stepping
/// up round three sides, the Slowsilver, the pools, Old Stoic (big, old and unchanging), and the four houses.
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
    public static readonly Vector3 FallsBase = new(1.6f, 0.15f, -7.7f);   // where the falls land in the pool
    /// <summary>Half the width of the square block the courtyard is cut from.</summary>
    private const float Block = 12.5f;

    private readonly Model _slab, _paving, _grass, _gorge, _props, _falls, _stream, _banks;
    /// <summary>The city (HubCity): near and far, the far part drawn in heavier haze.</summary>
    private readonly Model _cityGround, _cityStone, _cityUpper, _windows, _cityWall, _farStone, _farUpper, _farWindows, _farWall, _scarp, _corner, _baths, _poolRim;
    /// <summary>The forgotten corner's dressing (HubCorner), when the corner kit is there.</summary>
    private readonly Model? _cornerStone, _cornerFlags, _cornerUpper, _puddles, _rivulets;
    private readonly Vector3[] _lanterns;
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
        _slab = Renderer.WithTexture(BuildSlab(Houses.Values.Select(h => (h.Pos, h.PathEnd)), _course).ProjectUv(2.4f).Model(toon),
            r.Texture(Path.Combine(AppContext.BaseDirectory, "textures", "courtyard-masonry.png")));
        // painted textures (art/textures); each falls back to flat colour if it's missing
        string Tex(string name) => Path.Combine(AppContext.BaseDirectory, "textures", name);
        var rock = r.Texture(Tex("courtyard-rock.png"));
        var grassTex = r.Texture(Tex("courtyard-grass.png"));
        var paving = r.Texture(Tex("courtyard-paving.png"));
        var masonry = r.Texture(Tex("courtyard-masonry.png"));
        var (paved, grass, gorge) = BuildGround(_course);
        _paving = Renderer.WithTexture(paved.ProjectUv(3.6f).Model(toon), r.Texture(Tex("corner-paving.png")) ?? paving);
        _grass = Renderer.WithTexture(grass.ProjectUv(5.5f).Model(toon), grassTex);
        _gorge = Renderer.WithTexture(gorge.ProjectUv(1.6f).Model(toon), rock);
        var cornerKit = KitAsset.From(r.Import(Path.Combine(AppContext.BaseDirectory, "models", "corner-kit.glb")));
        var wallKit = KitAsset.From(r.Import(Path.Combine(AppContext.BaseDirectory, "models", "wall-kit.glb")));
        var city = HubCity.Build(HubCity.Kit.From(r.Import(Path.Combine(AppContext.BaseDirectory, "models", "city-kit.glb"))), cornerKit is not null, wallKit);
        // the tidy city in clean white ashlar and paving; the forgotten corner keeps its weathered masonry
        var ashlar = r.Texture(Tex("city-ashlar.png")) ?? masonry;
        var cityPaving = r.Texture(Tex("city-paving.png")) ?? paving;
        var cornerWall = r.Texture(Tex("corner-wall.png")) ?? masonry;
        var cornerPaving = r.Texture(Tex("corner-paving.png")) ?? paving;
        _corner = Renderer.WithTexture(city.Corner.ProjectUv(2.4f).Model(toon), cornerWall);
        _baths = Renderer.WithTexture(city.Baths.ProjectUv(4.2f).Model(toon), cornerWall);
        _poolRim = Renderer.WithTexture(BuildPoolRim().ProjectUv(1.6f).Model(toon), cornerWall);
        if (cornerKit is not null)
        {
            var dress = HubCorner.Build(cornerKit, CornerBlocked);
            _cornerStone = Renderer.WithTexture(dress.Stone.ProjectUv(2.4f).Model(toon), cornerWall);
            _cornerFlags = Renderer.WithTexture(dress.Flags.ProjectUv(3.6f).Model(toon), cornerPaving);
            _cornerUpper = dress.Upper.Model(toon);
            _puddles = dress.Puddles.Model(r.Shaders.Water);
            _rivulets = dress.Rivulets.Model(r.Shaders.Falls);
        }
        _cityGround = Renderer.WithTexture(city.Ground.ProjectUv(3.6f).Model(toon), cityPaving);
        _cityStone = Renderer.WithTexture(city.Stone.ProjectUv(3.0f).Model(toon), ashlar);
        _cityUpper = city.Upper.Model(toon);
        _windows = city.Windows.Model(toon);
        _cityWall = Renderer.WithTexture(city.Wall.ProjectUv(4.4f).Model(toon), ashlar);   // larger ashlar than the houses
        _farStone = Renderer.WithTexture(city.FarStone.ProjectUv(3.0f).Model(toon), ashlar);
        _farWall = Renderer.WithTexture(city.FarWall.ProjectUv(4.4f).Model(toon), ashlar);
        _farUpper = city.FarUpper.Model(toon);
        _farWindows = city.FarWindows.Model(toon);
        _scarp = Renderer.WithTexture(city.Scarp.ProjectUv(6f).Model(toon), masonry);
        _lanterns = city.Lanterns.ToArray();
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
    /// <summary>Ground the corner's dressing keeps off: the houses, the stream, the pool, the trunk, the lamp posts, the benches.</summary>
    private bool CornerBlocked(Vector2 q) =>
        Houses.Values.Any(h => Vector2.Distance(q, new Vector2(h.Pos.X, h.Pos.Z)) < h.Radius + 0.5f)
        || StreamDistance(q, _course) < 1.0f
        || MathF.Pow((q.X - 1.8f) / 3.2f, 2) + MathF.Pow((q.Y + 6.1f) / 2.5f, 2) < 1
        || Vector2.Distance(q, new Vector2(TreePos.X, TreePos.Z)) < 1.4f
        || LampSpots().Any(l => Vector2.Distance(q, new Vector2(l.X, l.Z)) < 0.6f)
        || Vector2.Distance(q, new Vector2(-1.9f, -4.3f)) < 1.1f || Vector2.Distance(q, new Vector2(3.6f, -3.1f)) < 1.1f;

    private static IEnumerable<Vector3> LampSpots()
    {
        // three on a ring round the tree; the fourth stands by the pool, not in it
        foreach (float deg in new[] { 45f, 135f, 225f })
        {
            float a = deg * MathF.PI / 180;
            yield return TreePos + new Vector3(MathF.Cos(a), 0, MathF.Sin(a)) * 5.1f;
        }
        yield return new Vector3(4.9f, 0, -3.4f);
    }

    private static MeshBuilder BuildSlab(IEnumerable<(Vector3 Pos, Vector3? PathEnd)> houses, Vector3[] stream)
    {
        var m = new MeshBuilder();
        // the square block the courtyard is cut from: stone sides, its top under the ground (BuildGround) and the
        // stream's gorge, on a dark plinth
        var side = Palette.Hex(0xE2DACB);
        m.Box(new Vector3(0, -1.65f, 0), new Vector3(Block * 2, 1.9f, Block * 2), side, Palette.Hex(0x2E3A30));
        foreach (var (c, size) in new[] { (new Vector3(0, -0.35f, Block - 0.05f), new Vector3(Block * 2, 0.7f, 0.1f)), (new Vector3(0, -0.35f, -Block + 0.05f), new Vector3(Block * 2, 0.7f, 0.1f)),
                                          (new Vector3(Block - 0.05f, -0.35f, 0), new Vector3(0.1f, 0.7f, Block * 2)), (new Vector3(-Block + 0.05f, -0.35f, 0), new Vector3(0.1f, 0.7f, Block * 2)) })
            m.Box(c, size, side);
        m.Box(new Vector3(0, -2.85f, 0), new Vector3(Block * 2 + 0.6f, 0.5f, Block * 2 + 0.6f), Palette.Hex(0x3A3532));
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
        m.Disc(new Vector3(1.8f, 0.03f, -6.1f), 3.0f, 30, Palette.Hex(0x2C5E60), 0.76f);   // the basin's floor (its rim: BuildPoolRim)
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
        Bench(new Vector3(-1.9f, 0, -4.3f), -1.12f);
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
        // the jet: out of the hole in the arch's bricks (HubCorner's brick-arch, 3.95 m up), arcing forward as it
        // drops into the pool; a curved sheet, not a flat card, narrow at the hole and spreading as it falls, with a
        // thinner, wider veil round it
        var top = new Vector3(FallsBase.X, 3.95f, HubCity.North + 0.12f);
        Vector3 Spine(float t) => new(top.X, top.Y - (top.Y - FallsBase.Y) * t * t, top.Z + (FallsBase.Z - top.Z) * t);
        void Sheet(float width0, float width1, float bulge, float out_, float vOffset)
        {
            const int Segs = 16, Across = 6;
            for (int i = 0; i < Segs; i++)
                for (int k = 0; k < Across; k++)
                {
                    Vector3 P(int si, int ki)
                    {
                        float t = si / (float)Segs, u = ki / (float)Across * 2 - 1;
                        float w = width0 + (width1 - width0) * MathF.Sqrt(t);
                        return Spine(t) + new Vector3(u * w, 0, (1 - u * u) * bulge + out_);
                    }
                    Vector2 Uv(int si, int ki) => new(ki / (float)Across, si / (float)Segs * 1.6f + vOffset);
                    var (a, b, c, d) = (P(i, k), P(i + 1, k), P(i + 1, k + 1), P(i, k + 1));
                    m.TriUv(a, b, c, Uv(i, k), Uv(i + 1, k), Uv(i + 1, k + 1), Color.White);
                    m.TriUv(a, c, d, Uv(i, k), Uv(i + 1, k + 1), Uv(i, k + 1), Color.White);
                }
        }
        Sheet(0.24f, 0.62f, 0.14f, 0, 0);
        Sheet(0.34f, 0.85f, 0.2f, 0.05f, 0.37f);
        // the churn where it lands: a ring of foam whose streaks run outward (v falls from the centre to the edge)
        const int Ring = 24, Bands = 4;
        for (int i = 0; i < Ring; i++)
            for (int b = 0; b < Bands; b++)
            {
                float a0 = MathF.Tau * i / Ring, a1 = MathF.Tau * (i + 1) / Ring;
                float r0 = 1.25f * b / Bands, r1 = 1.25f * (b + 1) / Bands;
                Vector3 P(float r, float a) => FallsBase + new Vector3(MathF.Cos(a) * r, 0.03f, MathF.Sin(a) * r * 0.8f);
                Vector2 Uv(float r, float a) => new(0.5f + 0.2f * MathF.Sin(a * 3), 0.98f - r / 1.25f * 0.45f);
                m.TriUv(P(r0, a0), P(r1, a0), P(r1, a1), Uv(r0, a0), Uv(r1, a0), Uv(r1, a1), Color.White);
                m.TriUv(P(r0, a0), P(r1, a1), P(r0, a1), Uv(r0, a0), Uv(r1, a1), Uv(r0, a1), Color.White);
            }
        return m;
    }

    /// <summary>
    /// The pool's rim: an old basin of weathered stones following its edge, each a little askew, a few gone (one
    /// tumbled into the water, one lying by the rim), and a gap on the east where the pool spills into the stream.
    /// </summary>
    private static MeshBuilder BuildPoolRim()
    {
        var m = new MeshBuilder();
        var rng = new Random(41);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        var c = new Vector3(1.8f, 0, -6.1f);
        const float Rx = 3.15f, Squash = 0.76f;
        float outlet = MathF.Atan2((-5.7f - c.Z) / Squash, 5.3f - c.X);   // where the stream leaves
        const int Stones = 26;
        for (int i = 0; i < Stones; i++)
        {
            float a = MathF.Tau * (i + R(-0.1f, 0.1f)) / Stones;
            float off = MathF.Abs(MathF.IEEERemainder(a - outlet, MathF.Tau));
            if (off < 0.32f) continue;                                          // the spill into the stream
            if (i == 7 || i == 16) continue;                                    // two long gone
            var at = c + new Vector3(MathF.Cos(a) * Rx, 0, MathF.Sin(a) * Rx * Squash);
            var tangent = Vector3.Normalize(new Vector3(-MathF.Sin(a), 0, MathF.Cos(a) * Squash));
            float yaw = MathF.Atan2(tangent.X, tangent.Z) + MathF.PI / 2 + R(-0.12f, 0.12f);
            float h = R(0.26f, 0.42f);
            var tint = Palette.Lerp(Palette.Hex(0xF2EDE4), Palette.Hex(0xD8D0C2), R(0, 1));
            var stone = new MeshBuilder().Box(new Vector3(0, h / 2 - 0.1f, 0), new Vector3(R(0.72f, 0.86f), h + 0.1f, R(0.42f, 0.52f)), tint)
                .Transform(Matrix4x4.CreateRotationZ(R(-0.06f, 0.06f)) * Matrix4x4.CreateRotationX(R(-0.05f, 0.05f))
                           * Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(at));
            m.Append(stone);
        }
        // one tumbled into the water by the falls, one lying on the grass where it fell outward
        m.Append(new MeshBuilder().Box(Vector3.Zero, new Vector3(0.8f, 0.36f, 0.48f), Palette.Hex(0xE4DDD0))
            .Transform(Matrix4x4.CreateRotationZ(0.5f) * Matrix4x4.CreateRotationY(0.4f) * Matrix4x4.CreateTranslation(c + new Vector3(-0.9f, 0.08f, -1.4f))));
        m.Append(new MeshBuilder().Box(Vector3.Zero, new Vector3(0.8f, 0.34f, 0.48f), Palette.Hex(0xE4DDD0))
            .Transform(Matrix4x4.CreateRotationX(0.25f) * Matrix4x4.CreateRotationY(1.3f) * Matrix4x4.CreateTranslation(c + new Vector3(-3.3f, 0.12f, 1.9f))));
        return m;
    }

    /// <summary>
    /// The courtyard's floor: worn paving over the whole block, grass in patches and round Old Stoic's roots, finer
    /// near the stream, where it drops into the stream's gorge (stone walls, a dark bed).
    /// </summary>
    private static (MeshBuilder Paving, MeshBuilder Grass, MeshBuilder Gorge) BuildGround(Vector3[] course)
    {
        var paving = new MeshBuilder(); var grass = new MeshBuilder(); var gorge = new MeshBuilder();
        const float Coarse = 0.5f, Bed = 0.48f, Lip = 0.74f, Depth = 0.6f;
        const int Split = 4;
        float bx0 = 0, bx1 = 0, bz0 = 0, bz1 = 0;
        if (course.Length > 1)
        {
            bx0 = MathF.Floor((course.Min(c => c.X) - 1) / Coarse) * Coarse; bx1 = MathF.Ceiling((course.Max(c => c.X) + 1) / Coarse) * Coarse;
            bz0 = MathF.Floor((course.Min(c => c.Z) - 1) / Coarse) * Coarse; bz1 = MathF.Ceiling((course.Max(c => c.Z) + 1) / Coarse) * Coarse;
        }
        bool InBox(float x, float z) => course.Length > 1 && x >= bx0 && x < bx1 && z >= bz0 && z < bz1;
        float H(float x, float z)
        {
            if (!InBox(x - 0.01f, z - 0.01f) && !InBox(x + 0.01f, z + 0.01f)) return 0;
            float d = StreamDistance(new Vector2(x, z), course);
            return d < Bed ? -Depth : d < Lip ? -Depth * (Lip - d) / (Lip - Bed) : 0;
        }
        bool Grassy(float x, float z) => Vector2.Distance(new Vector2(x, z), new Vector2(TreePos.X, TreePos.Z)) < 3.1f || Noise(x * 0.32f + 40, z * 0.32f, 21) > 0.7f;
        void Cell(float ax, float az, float size)
        {
            Vector3 P(float x, float z) => new(x, H(x, z), z);
            var (a, b, c, d) = (P(ax, az), P(ax, az + size), P(ax + size, az + size), P(ax + size, az));
            float h = (a.Y + b.Y + c.Y + d.Y) / 4;
            if (h < -0.04f)
                gorge.Quad(a, b, c, d, h < -Depth + 0.08f ? Palette.Hex(0x3A5048) : Palette.Lerp(Palette.Hex(0xE8E0D0), Palette.Hex(0x9A9080), -h / Depth));
            else if (Grassy(ax + size / 2, az + size / 2)) grass.Quad(a, b, c, d, Color.White);
            else paving.Quad(a, b, c, d, Color.White);
        }
        int n = (int)MathF.Round(Block / Coarse);
        for (int i = -n; i < n; i++)
            for (int j = -n; j < n; j++)
            {
                float ax = i * Coarse, az = j * Coarse;
                if (InBox(ax, az))
                    for (int u = 0; u < Split; u++)
                        for (int v = 0; v < Split; v++) Cell(ax + u * Coarse / Split, az + v * Coarse / Split, Coarse / Split);
                else Cell(ax, az, Coarse);
            }
        return (paving, grass, gorge);
    }

    /// <summary>How far a point (x, z) lies from the stream's course.</summary>
    private static float StreamDistance(Vector2 p, Vector3[] course)
    {
        float best = float.MaxValue;
        for (int i = 0; i < course.Length - 1; i++)
        {
            var a = new Vector2(course[i].X, course[i].Z); var b = new Vector2(course[i + 1].X, course[i + 1].Z);
            var ab = b - a;
            float t = ab.LengthSquared() < 1e-6f ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
            best = MathF.Min(best, Vector2.Distance(p, a + ab * t));
        }
        return best;
    }

    /// <summary>
    /// The stream: out of the plunge pool, an S past the tree's side of the courtyard, and into the Spirit Pools' cave
    /// mouth (the left half of its opening; the steps take the right), where it goes underground. Smoothed through a few points.
    /// </summary>
    private Vector3[] StreamCourse()
    {
        var pts = new List<Vector3> { new(5.3f, 0, -5.7f), new(6.6f, 0, -4.9f), new(8.0f, 0, -5.0f), new(9.4f, 0, -6.1f) };
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
            var dip = new Vector3(0, -0.3f, 0); // down in the gorge (BuildGround)
            water.Quad(a - sa * 0.46f + dip, b - sb * 0.46f + dip, b + sb * 0.46f + dip, a + sa * 0.46f + dip, Color.White);
            if (i % 2 == 0)
                foreach (var side in new[] { -1f, 1f })
                {
                    var p = a + sa * side * (0.86f + R01() * 0.12f); // on the gorge's lip
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
        bool TryImport(string locale, Vector3 pos, float turn = 0, float? facing = null)
        {
            if (Import(locale, out float scale) is not { } a) return false;
            var h = Imported(locale, pos, a, scale, turn);
            if (facing is { } yaw)
            {
                h.Yaw = yaw;
                if (a.Markers.TryGetValue("door-point", out var door)) h.PathEnd = Vector3.Transform(door, h.World(0));
            }
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
        // The Spirit Pools: the cave mouth set into the water gate at the foot of the city wall, where the stream goes underground
        // (models/pools.glb), turned toward the courtyard so its opening shows. The built-in shape is still iteration
        // 2's lodge, for when the model is missing.
        if (!TryImport("pools", new Vector3(12.9f, 0, HubCity.WaterGateZ), facing: -MathF.PI / 2))
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
            var at = FallsBase + new Vector3(p.R(-0.7f, 0.7f), 0.2f, p.R(-0.5f, 0.5f));
            p.Emit(at, new Vector3(p.R(-0.3f, 0.3f), p.R(0.4f, 1.1f), p.R(0.2f, 0.8f)), p.R(1.6f, 2.8f), p.R(0.5f, 1.0f), Palette.Alpha(Palette.Foam, 0.35f), false, 0.7f, 0.6f);
            if (_rng.NextDouble() < 0.4)
                p.Emit(FallsBase + new Vector3(p.R(-0.5f, 0.5f), 0.15f, p.R(-0.4f, 0.4f)), new Vector3(p.R(-0.7f, 0.7f), p.R(0.9f, 1.8f), p.R(-0.5f, 0.9f)), 0.6f, 0.14f, Palette.Alpha(Color.White, 0.8f), false, -0.05f, 0.4f, -3f);
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
        r.Draw(_paving, Matrix4x4.Identity, Color.White, outlineWidth: 1.0f);
        r.Draw(_grass, Matrix4x4.Identity, Color.White, outlineWidth: 1.0f);
        r.Draw(_gorge, Matrix4x4.Identity, Color.White, outlineWidth: 1.2f);
        // the city's windows are dark glass by day and warm lamplight after dusk, dimmer than the courtyard's own lamps
        var windowTint = Palette.Lerp(Palette.Hex(0x3A3836), Palette.Lantern, night);
        r.Draw(_cityGround, Matrix4x4.Identity, Color.White, outlineWidth: 1.0f);
        r.Draw(_corner, Matrix4x4.Identity, Color.White, outlineWidth: 1.3f);
        r.Draw(_baths, Matrix4x4.Identity, Color.White, outlineWidth: 1.3f);
        r.Draw(_poolRim, Matrix4x4.Identity, Color.White, outlineWidth: 1.2f);
        foreach (var m in new[] { _cornerStone, _cornerFlags, _cornerUpper })
            if (m is { } dress) r.Draw(dress, Matrix4x4.Identity, Color.White, outlineWidth: 1.0f);
        r.Draw(_cityStone, Matrix4x4.Identity, Color.White, outlineWidth: 1.3f);
        r.Draw(_cityWall, Matrix4x4.Identity, Color.White, outlineWidth: 1.4f);
        r.Draw(_cityUpper, Matrix4x4.Identity, Color.White, outlineWidth: 1.3f);
        r.Draw(_windows, Matrix4x4.Identity, windowTint, emissive: night * 0.6f, outline: false);
        r.Draw(_scarp, Matrix4x4.Identity, Color.White, outlineWidth: 1.2f);
        // the rows past the main street and the upper tiers fade into a cooler, lighter haze
        r.Shaders.FogRange(12, 100, Palette.Lerp(l.Fog, Palette.Hex(0xC8D2DE), 0.45f * (1 - night)));
        r.Draw(_farStone, Matrix4x4.Identity, Color.White, outlineWidth: 1.1f);
        r.Draw(_farWall, Matrix4x4.Identity, Color.White, outlineWidth: 1.2f);
        r.Draw(_farUpper, Matrix4x4.Identity, Color.White, outlineWidth: 1.1f);
        r.Draw(_farWindows, Matrix4x4.Identity, windowTint, emissive: night * 0.75f, outline: false);
        r.Shaders.FogRange(l.FogNear, l.FogFar, l.Fog);
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
        if (_puddles is { } puddles) r.DrawWater(puddles, Matrix4x4.Identity, Palette.Hex(0xCFE2E2), Palette.Hex(0x8FB2B4), 0.05f, 0.8f);
        if (_rivulets is { } rivulets) r.DrawFalls(rivulets, Matrix4x4.Identity, Palette.Alpha(Palette.Hex(0xCFEAE6), 0.7f));

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
        if (night > 0.05f) foreach (var c in _lanterns) r.Glow(c, 0.5f + night * 1.3f, Palette.Alpha(Palette.LanternHot, night * 0.75f)); // the city's street lanterns
    }

    public Camera3D Camera { get; set; }

    /// <summary>Just outside a house's door, at about head height.</summary>
    private static Vector3 Doorway(House h) => h.Pos + (h.Door - h.Pos) * (h.Radius + 0.35f) / (h.Radius + 1.6f);

    /// <summary>A species' body colour (look data in Assets/visuals.json).</summary>
    public static Color CreatureColor(string species) => UI.ItemArt.LifeformColor(species);

    public void Dispose()
    {
        foreach (var m in new[] { _slab, _paving, _grass, _gorge, _cityGround, _cityStone, _cityUpper, _windows, _cityWall, _farStone, _farUpper, _farWindows, _farWall, _scarp, _corner, _baths, _poolRim, _props, _falls, _stream, _banks }) Raylib.UnloadModel(m);
        foreach (var p in _pools) Raylib.UnloadModel(p.Water);
        foreach (var m in new[] { _cornerStone, _cornerFlags, _cornerUpper, _puddles, _rivulets }) if (m is { } model) Raylib.UnloadModel(model);
        foreach (var m in _built) Raylib.UnloadModel(m);
    }
}
