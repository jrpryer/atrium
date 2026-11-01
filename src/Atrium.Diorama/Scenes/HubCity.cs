using System.Numerics;
using Atrium.Diorama.Render;
using Raylib_cs;

namespace Atrium.Diorama.Scenes;

/// <summary>
/// The city round the courtyard (a code blockout, to be replaced by a Blender kit). The courtyard is a sunken pocket in
/// the corner of the city's lowest tier: tier 1's streets run 4 m above it on the west and north, round a curved
/// corner, and on along the city wall by a corbelled gallery to a shelf on the east. The outer city wall stands on the
/// east, where the stream leaves the city through a water gate behind the Spirit Pools' cave mouth. The tiers ring a
/// citadel off the map to the north-north-west, so their walls and streets curve across the view rather than round the
/// courtyard. Every building fronts its own street; the courtyard sees backs, yards and gable ends. The falls burst
/// from the cracked overflow of the old baths' cistern, which an aqueduct feeds from tier 2. The city's stone is pale
/// and cool; the courtyard's own stone stays warm.
/// </summary>
internal static class HubCity
{
    /// <summary>The citadel the tiers ring (x, z), off the map to the north-north-west.</summary>
    public static readonly Vector2 Citadel = new(-30, -110);
    /// <summary>Street levels of the first three tiers.</summary>
    public const float Tier1 = 4, Tier2 = 20, Tier3 = 34;
    /// <summary>Radii (from the citadel) of the tier walls' faces.</summary>
    public const float Tier2Wall = 82, Tier3Wall = 62;
    /// <summary>The courtyard pocket: x from West to East, z from North to the block's front edge.</summary>
    public const float West = -10.8f, North = -9.6f, East = 12.9f, Front = 12.5f;
    /// <summary>Where the water tower stands on the city wall; south of it the wall bends out.</summary>
    public const float GateZ = -3.6f;
    /// <summary>The water gate through the foot of the city wall, where the stream leaves and the Spirit Pools' cave mouth sits.</summary>
    public const float WaterGateZ = -7.4f;
    /// <summary>The pocket's north-west corner is rounded, so tier 1's rim curves round it.</summary>
    private const float Corner = 4.5f;
    internal static readonly Vector2 CornerC = new(West + Corner, North + Corner);
    /// <summary>The alley from the north stair up to the main street, under a bridge-room.</summary>
    private const float AlleyX = -5.6f;
    private const float Bend = 9.1f / 16.1f;                     // x per z along the wall's southern stretch
    private const float WallThick = 3.6f, WalkN = 12, WalkS = 10, WalkUp = 28, Base = -3.1f;
    private const float WestLimit = -52, NorthLimit = -80;      // how far the modelled city runs before haze hides it

    /// <summary>The city wall's inner face at z: straight north of the water tower, bent out along the cliff south of it.</summary>
    public static float WallX(float z) => z < GateZ ? East : East + (z - GateZ) * Bend;

    /// <summary>What the city adds to the hub, by material and by haze.</summary>
    public sealed class Parts
    {
        /// <summary>Near: tier 1's street paving, masonry, plain colour, lit windows.</summary>
        public readonly MeshBuilder Ground = new(), Stone = new(), Upper = new(), Windows = new();
        /// <summary>Far: the rows past the main street and the upper tiers, drawn in heavier haze.</summary>
        public readonly MeshBuilder FarStone = new(), FarUpper = new(), FarWindows = new();
        /// <summary>The forgotten corner's own weathered stone: the pocket's rims, stairs and gallery; and the old baths.</summary>
        public readonly MeshBuilder Corner = new(), Baths = new();
        /// <summary>The city and tier walls, in larger ashlar than the houses: near and far.</summary>
        public readonly MeshBuilder Wall = new(), FarWall = new();
        /// <summary>The rock the city wall stands on, and the section cut along the front (drawn in coarse masonry).</summary>
        public readonly MeshBuilder Scarp = new();
        /// <summary>Street lanterns, braziers and torches, for glows at night.</summary>
        public readonly List<Vector3> Lanterns = new();
        /// <summary>The Blender building kit, or null to build the code boxes.</summary>
        public Kit? Kit { get; init; }
        /// <summary>The corner kit dresses the corner (HubCorner), so the code's own ivy and arch stand down.</summary>
        public bool CornerKit { get; init; }
        /// <summary>The wall kit (art/models/wall-kit.glb): the city wall's arches, battlements, towers and gates.</summary>
        public KitAsset? WallKit { get; init; }
    }

    /// <summary>
    /// The white city's building kit (art/models/city-kit.glb, built by tools/blender/assets/city_kit.py): each piece's
    /// meshes by role, and where its origin and lantern are in the file. Buildings are baked from it into the city's
    /// layers, so they keep its haze, masonry texture and lights.
    /// </summary>
    public sealed class Kit
    {
        /// <summary>One piece: its nominal footprint, its origin and lantern in the file, its meshes as (role, mesh, colour).</summary>
        public sealed record Piece(string Name, float W, float D, Vector3 Origin, Vector3? Lamp, (string Role, Mesh Mesh, Color Colour)[] Meshes);

        // name, nominal width and depth, kind (matches PIECES in city_kit.py)
        private static readonly (string Name, float W, float D, Kind Kind)[] Table =
        {
            ("house-2a", 5.0f, 5.5f, Kind.House), ("house-3a", 5.0f, 5.5f, Kind.House), ("house-3b", 5.0f, 5.5f, Kind.House),
            ("house-4a", 4.5f, 5.5f, Kind.House), ("house-3c", 5.5f, 5.5f, Kind.House), ("shop-3", 5.5f, 5.5f, Kind.Shop),
            ("inn-3", 8.5f, 6.0f, Kind.Inn), ("stable-1", 7.0f, 5.5f, Kind.Stable), ("tower-5", 4.2f, 4.2f, Kind.Tower),
        };
        private static readonly string[] Roles =
        {
            "stone", "trim", "roof", "timber", "plaster", "dark", "glass-a", "glass-b", "glass-back", "plants", "flowers",
            "cloth", "iron", "pot", "lead", "lamp",
        };
        private readonly Dictionary<Kind, List<Piece>> _byKind = new();

        public static unsafe Kit? From(ModelAsset? asset)
        {
            if (asset is null) return null;
            var kit = new Kit();
            var model = asset.Model;
            foreach (var (name, w, d, kind) in Table)
            {
                if (!asset.Markers.TryGetValue(name, out var origin)) continue;
                var meshes = Roles.SelectMany(role => asset.Meshes($"{name}_{role}").Select(i =>
                    (role, model.Meshes[i], model.Materials[model.MeshMaterial[i]].Maps[(int)MaterialMapIndex.Albedo].Color))).ToArray();
                if (meshes.Length == 0) continue;
                var lamp = asset.Markers.TryGetValue(name + "-lamp", out var l) ? l : (Vector3?)null;
                (kit._byKind.TryGetValue(kind, out var list) ? list : kit._byKind[kind] = new List<Piece>()).Add(new Piece(name, w, d, origin, lamp, meshes));
            }
            return kit._byKind.Count > 0 ? kit : null;
        }

        internal Piece? Pick(Kind kind, Random rng) => _byKind.TryGetValue(kind, out var list) ? list[rng.Next(list.Count)] : null;
    }

    private readonly record struct Layer(MeshBuilder Stone, MeshBuilder Upper, MeshBuilder Windows);
    internal enum Kind { House, Shop, Inn, Stable, Tower }

    // one limestone for city and corner alike: the corner differs by its weathering, not by its stone
    internal static readonly Color StoneTint = Palette.Hex(0xE2DED6);
    private static readonly Color Coping = Palette.Hex(0xE9E6DF), StreetTint = Palette.Hex(0xD6D2CA);
    private static readonly Color Dark = Palette.Hex(0x23201F), Glass = Palette.Hex(0x3A3836), Timber = Palette.Hex(0x5A4330), Iron = Palette.Hex(0x34302E);
    private static readonly Color MossCol = Palette.Hex(0x6E8B4E), Street = Palette.Hex(0xCDD2D8), Lead = Palette.Hex(0x6A7078);
    private static readonly Color[] Slate = { Palette.Hex(0x5A6470), Palette.Hex(0x4E5862), Palette.Hex(0x66707A), Palette.Hex(0x545A66), Palette.Hex(0x6A6E72), Palette.Hex(0x5E6670) };
    private static readonly Color[] Tile = { Palette.Hex(0x8A5A48), Palette.Hex(0x7A5040) };
    private static readonly Color[] Plaster = { Palette.Hex(0xE8E2D4), Palette.Hex(0xDDD8CC), Palette.Hex(0xE4E2DA) };
    private static readonly Color[] Leaves = { Palette.Hex(0x5E7E44), Palette.Hex(0x6E8E4E), Palette.Hex(0x4E6E3E) };
    private static readonly Color[] Linen = { Palette.Hex(0xE8E2D6), Palette.Hex(0xB86A5A), Palette.Hex(0x6A8AA6), Palette.Hex(0xD8B86A), Palette.Hex(0x8AA66A) };

    private static float RadiusOf(float x, float z) => Vector2.Distance(new Vector2(x, z), Citadel);
    private static Vector3 OnArc(float r, float phi, float y) => new(Citadel.X + r * MathF.Sin(phi), y, Citadel.Y + r * MathF.Cos(phi));
    /// <summary>The angle at which the arc of radius r crosses x, on the courtyard's (south) side of the citadel.</summary>
    private static float AngleAt(float r, float x) => MathF.Asin(Math.Clamp((x - Citadel.X) / r, -1, 1));

    private static bool InPocket(float x, float z, float margin = 0)
    {
        if (!(x > West - margin && x < East + margin && z > North - margin)) return false;
        return !(x < CornerC.X && z < CornerC.Y && Vector2.Distance(new Vector2(x, z), CornerC) > Corner + margin);
    }

    /// <summary>A frame whose local +X points outward (to the right of the run), +Z along the run, +Y up, at origin.</summary>
    private static Matrix4x4 Frame(Vector3 origin, Vector2 run)
    {
        run = Vector2.Normalize(run);
        var o = new Vector2(run.Y, -run.X);
        return new Matrix4x4(o.X, 0, o.Y, 0, 0, 1, 0, 0, run.X, 0, run.Y, 0, origin.X, origin.Y, origin.Z, 1);
    }

    /// <summary>A quad wound to face <paramref name="facing"/>.</summary>
    private static void Face(MeshBuilder m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, Color col)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0) m.Quad(a, d, c, b, col);
        else m.Quad(a, b, c, d, col);
    }

    public static Parts Build(Kit? kit = null, bool cornerKit = false, KitAsset? wallKit = null)
    {
        var p = new Parts { Kit = kit, CornerKit = cornerKit, WallKit = wallKit };
        var near = new Layer(p.Stone, p.Upper, p.Windows);
        var far = new Layer(p.FarStone, p.FarUpper, p.FarWindows);
        var rng = new Random(31);
        Ground(p);
        Rims(p, rng);
        CityWall(p);
        TierWall(p.FarWall, far, Tier2Wall, Tier1, Tier2, rng, housesInFoot: true);
        TierWall(p.FarWall, far, Tier3Wall, Tier2, Tier3, rng, housesInFoot: false);
        Baths(p, rng);
        Landmarks(p, near, rng);
        Rows(p, near, far, rng);
        Lanterns(p, near, far);
        return p;
    }

    // ── ground: tier 1's streets (a grid whose lines fall on the pocket's edges), tiers 2 and 3 as arcs ──
    private static void Ground(Parts p)
    {
        const float Cell = (East - West) / 20;
        int i0 = -(int)MathF.Ceiling((West - WestLimit) / Cell), i1 = (int)MathF.Ceiling((26 - West) / Cell);
        int j0 = -(int)MathF.Ceiling((North - NorthLimit) / Cell), j1 = (int)MathF.Ceiling((Front - North) / Cell);
        for (int i = i0; i < i1; i++)
            for (int j = j0; j < j1; j++)
            {
                float x0 = West + i * Cell, x1 = x0 + Cell, z0 = North + j * Cell, z1 = MathF.Min(z0 + Cell, Front);
                float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
                if (z1 <= z0 || InPocket(x0, z0) || InPocket(x1, z0) || InPocket(x0, z1) || InPocket(x1, z1) || InPocket(cx, cz)
                    || RadiusOf(cx, cz) < Tier2Wall - 1.5f || cx > WallX(cz) + 1) continue;
                p.Ground.Quad(new(x0, Tier1, z0), new(x0, Tier1, z1), new(x1, Tier1, z1), new(x1, Tier1, z0), StreetTint);
            }
        Sector(p.FarUpper, Tier3Wall, Tier2Wall - 3, Tier2, Street);
        Sector(p.FarUpper, 36, Tier3Wall - 3, Tier3, Palette.Mul(Street, 0.97f));
    }

    /// <summary>A level band of ground between two radii, from the west limit to the city wall.</summary>
    private static void Sector(MeshBuilder m, float r0, float r1, float y, Color col)
    {
        for (float ra = r0; ra < r1; ra += 2.5f)
        {
            float rb = MathF.Min(ra + 2.5f, r1), rm = (ra + rb) / 2;
            float a0 = AngleAt(rm, WestLimit - 8), a1 = AngleAt(rm, East + 1);
            int n = Math.Max(1, (int)((a1 - a0) * rm / 3));
            for (int k = 0; k < n; k++)
            {
                float t0 = a0 + (a1 - a0) * k / n, t1 = a0 + (a1 - a0) * (k + 1) / n;
                Face(m, OnArc(ra, t0, y), OnArc(rb, t0, y), OnArc(rb, t1, y), OnArc(ra, t1, y), Vector3.UnitY, col);
            }
        }
    }

    // ── the pocket's rims: retaining walls round a curved corner, coping and parapets, the gallery along the city
    //    wall, stairs along the walls, a cellar door, the old well, ivy, and the front section cut ──
    private static void Rims(Parts p, Random rng)
    {
        var s = p.Corner;
        // retaining walls (west, north, the east shelf's, down to the base at the front) and their coping
        s.Box(new Vector3(West - 0.5f, (Tier1 - 0.5f) / 2, (CornerC.Y + Front) / 2), new Vector3(1, Tier1 + 0.5f, Front - CornerC.Y), StoneTint);
        foreach (var (x0, x1) in new[] { (CornerC.X, -2.6f), (6.4f, East) })   // the baths' own back wall stands between
            s.Box(new Vector3((x0 + x1) / 2, (Tier1 - 0.5f) / 2, North - 0.5f), new Vector3(x1 - x0, Tier1 + 0.5f, 1), StoneTint);
        s.Box(new Vector3(East + 0.5f, (Tier1 - 2.6f) / 2, (GateZ + Front) / 2), new Vector3(1, Tier1 + 2.6f, Front - GateZ), StoneTint);
        s.Box(new Vector3(West - 0.45f, Tier1 + 0.1f, (CornerC.Y + Front) / 2), new Vector3(1.3f, 0.2f, Front - CornerC.Y), Coping);
        s.Box(new Vector3((CornerC.X + East) / 2, Tier1 + 0.1f, North - 0.45f), new Vector3(East - CornerC.X, 0.2f, 1.3f), Coping);
        s.Box(new Vector3(East + 0.45f, Tier1 + 0.1f, (GateZ + Front) / 2), new Vector3(1.3f, 0.2f, Front - GateZ), Coping);
        // the curved north-west corner: its wall face, a broad coping (a curved pavement at the top) and a parapet
        const int Segs = 10;
        for (int k = 0; k < Segs; k++)
        {
            float t0 = MathF.PI * (1 + 0.5f * k / Segs), t1 = MathF.PI * (1 + 0.5f * (k + 1) / Segs);
            Vector3 P(float t, float r, float y) => new(CornerC.X + MathF.Cos(t) * r, y, CornerC.Y + MathF.Sin(t) * r);
            var inward = -Vector3.Normalize(P((t0 + t1) / 2, 1, 0) - new Vector3(CornerC.X, 0, CornerC.Y));
            Face(s, P(t0, Corner, -0.5f), P(t1, Corner, -0.5f), P(t1, Corner, Tier1), P(t0, Corner, Tier1), inward, StoneTint);
            Face(s, P(t0, Corner - 0.15f, Tier1 + 0.2f), P(t1, Corner - 0.15f, Tier1 + 0.2f), P(t1, Corner + 1.8f, Tier1 + 0.2f), P(t0, Corner + 1.8f, Tier1 + 0.2f), Vector3.UnitY, Coping);
            Face(s, P(t0, Corner - 0.15f, Tier1), P(t1, Corner - 0.15f, Tier1), P(t1, Corner - 0.15f, Tier1 + 0.2f), P(t0, Corner - 0.15f, Tier1 + 0.2f), inward, Coping);
            Face(s, P(t0, Corner + 0.05f, Tier1 + 0.2f), P(t1, Corner + 0.05f, Tier1 + 0.2f), P(t1, Corner + 0.05f, Tier1 + 1.1f), P(t0, Corner + 0.05f, Tier1 + 1.1f), inward, StoneTint);
            Face(s, P(t0, Corner + 0.5f, Tier1 + 0.2f), P(t1, Corner + 0.5f, Tier1 + 0.2f), P(t1, Corner + 0.5f, Tier1 + 1.1f), P(t0, Corner + 0.5f, Tier1 + 1.1f), -inward, StoneTint);
            Face(s, P(t0, Corner + 0.05f, Tier1 + 1.1f), P(t1, Corner + 0.05f, Tier1 + 1.1f), P(t1, Corner + 0.5f, Tier1 + 1.1f), P(t0, Corner + 0.5f, Tier1 + 1.1f), Vector3.UnitY, Coping);
        }
        // a footing along the city wall's foot, open where the stream leaves through the water gate
        s.Box(new Vector3(12.65f, 0.12f, (North + GateZ - 1.8f) / 2), new Vector3(0.5f, 0.6f, GateZ - 1.8f - North), Coping);
        s.Box(new Vector3(12.65f, -1.09f, (GateZ + 1.8f + Front) / 2), new Vector3(0.5f, 3.02f, Front - GateZ - 1.8f), Coping);

        // parapets along the rims, open at the stair heads, the baths and the stable (they stand on the rim itself)
        void Parapet(Vector3 a, Vector3 b)
        {
            var c = (a + b) / 2;
            s.Box(new Vector3(c.X, Tier1 + 0.65f, c.Z), new Vector3(MathF.Max(MathF.Abs(b.X - a.X), 0.45f), 0.9f, MathF.Max(MathF.Abs(b.Z - a.Z), 0.45f)), StoneTint, Coping);
        }
        Parapet(new(West - 0.5f, 0, CornerC.Y), new(West - 0.5f, 0, 4.8f));
        Parapet(new(West - 0.5f, 0, 6.6f), new(West - 0.5f, 0, Front));
        Parapet(new(AlleyX + 0.7f, 0, North - 0.5f), new(-2.8f, 0, North - 0.5f));
        Parapet(new(East + 0.5f, 0, GateZ), new(East + 0.5f, 0, 4.8f));
        Parapet(new(East + 0.5f, 0, 6.6f), new(East + 0.5f, 0, Front));

        // the gallery: corbelled out from the north rim, then bridging the water gate's arch along the city wall to the shelf
        s.Box(new Vector3(11.65f, Tier1 + 0.05f, -8.7f), new Vector3(2.5f, 0.3f, 1.8f), StoneTint, Coping);
        s.Box(new Vector3(12.0f, Tier1 + 0.05f, (-7.8f + GateZ) / 2), new Vector3(1.8f, 0.3f, GateZ + 7.8f), StoneTint, Coping);
        Parapet(new(10.52f, 0, North), new(10.52f, 0, -7.8f));
        Parapet(new(10.4f, 0, -8.02f), new(11.1f, 0, -8.02f));
        for (float z = -7.8f; z <= GateZ + 0.01f; z += 0.5f)
            p.Upper.Box(new Vector3(11.25f, Tier1 + 0.65f, z), new Vector3(0.06f, 0.9f, 0.06f), Iron);
        foreach (float y in new[] { Tier1 + 0.7f, Tier1 + 1.08f })
            p.Upper.Box(new Vector3(11.25f, y, (-7.8f + GateZ) / 2), new Vector3(0.06f, 0.06f, GateZ + 7.8f), Iron);
        for (int k = 0; k < 3; k++)
            s.Box(new Vector3(East - 0.9f + k * 0.3f, Tier1 - 0.3f - k * 0.35f, WaterGateZ + 3.2f), new Vector3(1.8f - k * 0.6f, 0.35f, 0.5f), StoneTint);
        for (float x = 10.8f; x < 11.2f; x += 1.0f)
            for (int k = 0; k < 3; k++) s.Box(new Vector3(x, Tier1 - 0.3f - k * 0.35f, North + 0.9f - k * 0.3f), new Vector3(0.4f, 0.35f, 1.8f - k * 0.6f), StoneTint);

        // stairs along the walls, about 35 degrees: (foot, head, across)
        void Flight(Vector3 foot, Vector3 head, Vector3 across, int steps)
        {
            var run = head - foot; run.Y = 0;
            for (int i = 0; i < steps; i++)
            {
                float y = foot.Y + (head.Y - foot.Y) * (i + 1) / steps;
                var c = foot + run * ((i + 0.5f) / steps) + across / 2;
                var size = Vector3.Abs(run) / steps + Vector3.Abs(across);
                s.Box(new Vector3(c.X, (y + foot.Y - 0.3f) / 2, c.Z), size with { Y = y - foot.Y + 0.3f }, StoneTint, Coping);
            }
        }
        Flight(new(West, 0, 11.0f), new(West, Tier1, 5.4f), new(1.4f, 0, 0), 14);
        Flight(new(-0.8f, 0, North), new(CornerC.X, Tier1, North), new(0, 0, 1.4f), 13);
        Flight(new(East - 1.4f, 0, 11.0f), new(East - 1.4f, Tier1, 5.4f), new(1.4f, 0, 0), 14);
        // moss on the lower treads, where the water keeps them wet
        for (int k = 0; k < 9; k++)
            p.Upper.Box(new Vector3(-0.9f - k * 0.42f, (k + 1) * Tier1 / 13 + 0.02f, North + 0.4f + (k % 3) * 0.3f), new Vector3(0.36f, 0.04f, 0.5f), Leaves[k % 3]);

        // a cellar door in the west wall
        Doorway(p.Upper, p.Corner, new Vector3(West, 0, 1.2f), Vector3.UnitX, 1.1f, 1.9f);

        // ivy and moss where the water has kept the stone wet, and ivy hanging down from the rims
        if (!p.CornerKit) RimIvy(p, rng);

        // the old well, out along the back lane W1
        p.Stone.Append(new MeshBuilder().Cylinder(new Vector3(-17.5f, Tier1, -4.7f), 0.7f, 0.7f, 0.8f, 10, StoneTint, Coping));
        p.Upper.Box(new Vector3(-17.5f, Tier1 + 0.81f, -4.7f), new Vector3(0.9f, 0.02f, 0.9f), Palette.Hex(0x2A3A38));
        p.Upper.Box(new Vector3(-17.5f, Tier1 + 1.9f, -4.7f), new Vector3(1.6f, 0.12f, 0.12f), Timber);
        foreach (float dx in new[] { -0.75f, 0.75f }) p.Upper.Box(new Vector3(-17.5f + dx, Tier1 + 1.4f, -4.7f), new Vector3(0.1f, 1.1f, 0.1f), Timber);

        // the section cut along the front, beside the courtyard's own block, and the base under it all
        var cut = Palette.Hex(0xC8CCD2);
        p.Scarp.Box(new Vector3((WestLimit - 12.5f) / 2, (Tier1 - 2.6f) / 2, Front - 0.05f), new Vector3(WestLimit * -1 - 12.5f, Tier1 + 2.6f, 0.1f), cut);
        p.Scarp.Box(new Vector3(-12.15f, Tier1 / 2, Front - 0.05f), new Vector3(0.7f, Tier1, 0.1f), cut);
        p.Scarp.Box(new Vector3((East + 1 + WallX(Front)) / 2, (Tier1 - 2.6f) / 2, Front - 0.05f), new Vector3(WallX(Front) - East - 1, Tier1 + 2.6f, 0.1f), cut);
        p.Upper.Box(new Vector3((WestLimit + 38) / 2, -2.85f, (NorthLimit + Front + 0.3f) / 2), new Vector3(38 - WestLimit, 0.5f, Front + 0.3f - NorthLimit), Palette.Hex(0x3A3532));
    }

    private static void RimIvy(Parts p, Random rng)
    {
        Ivy(p.Upper, new Vector3(-0.6f, 0.2f, North + 0.05f), Vector3.UnitX, Vector3.UnitZ, 1.4f, 3.6f, 50, rng);
        Ivy(p.Upper, new Vector3(4.0f, 0.2f, North + 0.05f), Vector3.UnitX, Vector3.UnitZ, 3.4f, 3.2f, 70, rng);
        Ivy(p.Upper, new Vector3(West + 0.05f, 0.2f, -5.0f), Vector3.UnitZ, Vector3.UnitX, 4.0f, 3.0f, 60, rng);
        Ivy(p.Upper, new Vector3(East - 0.05f, 0.2f, -9.4f), Vector3.UnitZ, -Vector3.UnitX, 3.6f, 5.5f, 70, rng);
        Ivy(p.Upper, new Vector3(East - 0.05f, 0.2f, -1.6f), Vector3.UnitZ, -Vector3.UnitX, 2.4f, 4.5f, 50, rng);
        Ivy(p.Upper, new Vector3(West + 0.05f, Tier1 + 0.1f, 6.8f), Vector3.UnitZ, Vector3.UnitX, 4.5f, 2.6f, 50, rng, hang: true);
        Ivy(p.Upper, new Vector3(West + 0.05f, Tier1 + 0.1f, -3.4f), Vector3.UnitZ, Vector3.UnitX, 3.0f, 2.2f, 40, rng, hang: true);
    }

    /// <summary>Whether a point is on one of the stairs down into the pocket (they run along its walls; see Rims).</summary>
    internal static bool OnStairs(Vector2 q) =>
        (q.X > West - 0.1f && q.X < West + 1.5f && q.Y > 5.2f && q.Y < 11.2f)
        || (q.X > CornerC.X - 0.1f && q.X < -0.6f && q.Y > North - 0.1f && q.Y < North + 1.5f)
        || (q.X > East - 1.5f && q.X < East + 0.1f && q.Y > 5.2f && q.Y < 11.2f);

    /// <summary>A dark arched doorway on a wall face (centre of its foot, the face's outward normal), with a lighter frame.</summary>
    private static void Doorway(MeshBuilder dark, MeshBuilder frame, Vector3 foot, Vector3 n, float w, float h)
    {
        var across = new Vector3(MathF.Abs(n.Z), 0, MathF.Abs(n.X));
        Vector3 Size(float a, float b, float t) => across * a + Vector3.UnitY * b + new Vector3(MathF.Abs(n.X), 0, MathF.Abs(n.Z)) * t;
        frame.Box(foot + n * 0.04f + new Vector3(0, (h + 0.3f) / 2, 0), Size(w + 0.5f, h + 0.3f, 0.08f), Coping);
        dark.Box(foot + n * 0.09f + new Vector3(0, (h - w * 0.3f) / 2, 0), Size(w, h - w * 0.3f, 0.04f), Dark);
        dark.Box(foot + n * 0.09f + new Vector3(0, h - w * 0.3f + w * 0.12f, 0), Size(w * 0.72f, w * 0.24f, 0.04f), Dark);
        dark.Box(foot + n * 0.09f + new Vector3(0, h - w * 0.06f, 0), Size(w * 0.4f, w * 0.12f, 0.04f), Dark);
    }

    /// <summary>Ivy and moss: leaves scattered over a patch of wall, thickest at the foot (or, hanging, at the top).</summary>
    private static void Ivy(MeshBuilder m, Vector3 origin, Vector3 along, Vector3 normal, float w, float h, int count, Random rng, bool hang = false)
    {
        for (int i = 0; i < count; i++)
        {
            float u = (float)rng.NextDouble(), v = MathF.Pow((float)rng.NextDouble(), 1.7f);
            float size = 0.22f + (float)rng.NextDouble() * 0.28f;
            var c = origin + along * (u * w) + Vector3.UnitY * (hang ? -v * h : v * h) + normal * (0.02f + (float)rng.NextDouble() * 0.06f);
            var ext = along * size + Vector3.UnitY * size * 0.8f;
            m.Box(c, new Vector3(MathF.Abs(ext.X), ext.Y, MathF.Abs(ext.Z)) + new Vector3(MathF.Abs(normal.X), 0, MathF.Abs(normal.Z)) * 0.05f, Leaves[rng.Next(Leaves.Length)]);
        }
    }

    // ── the outer city wall: an arcaded inner face carrying the wall-walk, battlements, towers, the water gate and the
    //    Low Gate, on a rock scarp ──
    private static void CityWall(Parts p)
    {
        var kit = p.WallKit;
        // piers of the inner arcade stand everywhere but at the gates and behind the courtyard's gallery
        static bool PierAt(Vector3 at) => !(MathF.Abs(at.Z - WaterGateZ) < 4.0f || MathF.Abs(at.Z + 24.25f) < 4.0f || (at.Z > -11 && at.Z < GateZ));

        // a straight run of wall, its inner face from a to b; outward is to the east of the run. Its body can be set back
        // (notch: from z0 to z1 along the run, up to y, by depth) for an opening with jambs; piers can be kept off a span
        // (noPier, by distance along the run); a run that ends on the front cut stops its relief short of it.
        void Run(Vector3 a, Vector3 b, float walk, MeshBuilder wall, MeshBuilder upper, MeshBuilder lit, bool arcade,
                 (float Z0, float Z1, float Y, float Depth)? notch = null, Func<float, bool>? noPier = null, bool endsOnCut = false)
        {
            var run = new Vector2(b.X - a.X, b.Z - a.Z);
            float len = run.Length();
            var f = Frame(a, run);
            var s = new MeshBuilder(); var w = new MeshBuilder(); var rock = new MeshBuilder();
            void Body(float z0, float z1, float x0, float y0, float y1) =>
                s.Box(new Vector3((x0 + WallThick) / 2, (y0 + y1) / 2, (z0 + z1) / 2), new Vector3(WallThick - x0, y1 - y0, z1 - z0), StoneTint, Coping);
            if (notch is { } n)
            {
                Body(0, n.Z0, 0, Base, walk);
                Body(n.Z0, n.Z1, n.Depth, Base, n.Y);
                Body(n.Z0, n.Z1, 0, n.Y, walk);
                Body(n.Z1, len, 0, Base, walk);
            }
            else Body(0, len, 0, Base, walk);
            s.Box(new Vector3(WallThick + 0.5f, Base + 1.5f, len / 2), new Vector3(1.0f, 3.0f, len), StoneTint);               // a battered plinth
            if (kit?["wall-bay"] is { } bay && kit["wall-bay-plain"] is { } plainBay)
            {
                // bays from the kit, scaled evenly to fit the run (so their arches stay round): arched where piers stand
                // at both ends, plain (on a corbel table) elsewhere
                float span = endsOnCut ? len - 0.7f : len;
                int count = Math.Max(1, (int)MathF.Round(span / 5.5f));
                float bayLen = span / count, sc = bayLen / 5.5f, spring = walk - 4.2f * sc;
                var pier = Enumerable.Range(0, count + 1).Select(k => arcade && PierAt(Vector3.Transform(new Vector3(-0.5f, 0, k * bayLen), f))
                                                                      && noPier?.Invoke(k * bayLen) != true && !(endsOnCut && k == count)).ToArray();
                var arched = Enumerable.Range(0, count).Select(i => pier[i] && pier[i + 1]).ToArray();
                for (int i = 0; i < count; i++)
                {
                    Place(arched[i] ? bay : plainBay, Matrix4x4.CreateScale(sc, sc, 1) * Matrix4x4.CreateRotationY(-MathF.PI / 2)
                          * Matrix4x4.CreateTranslation(0, walk, (i + 0.5f) * bayLen) * f, wall, upper, lit);
                    if (arched[i])   // the bay's back wall, in shade, so the arch reads as a recess
                        s.Box(new Vector3(-0.012f, (spring + Base) / 2, (i + 0.5f) * bayLen), new Vector3(0.02f, spring - Base, bayLen - 1.6f), Palette.Mul(StoneTint, 0.8f));
                }
                for (int k = 0; k <= count; k++)
                    if (pier[k] && ((k > 0 && arched[k - 1]) || (k < count && arched[k])))
                        s.Box(new Vector3(-0.5f, (spring + Base) / 2, k * bayLen), new Vector3(1.0f, spring - Base, 1.6f), StoneTint);
                if (endsOnCut)
                {
                    // the parapet carried on over the wedge that squares the run's end to the cut
                    s.Box(new Vector3(WallThick - 0.15f, walk + 0.45f, len + 0.6f), new Vector3(0.9f, 1.3f, 2.6f), StoneTint, Coping);
                    s.Box(new Vector3(WallThick - 0.3f, walk + 1.55f, len + 0.5f), new Vector3(0.6f, 0.9f, 1.05f), StoneTint, Coping);
                }
            }
            else
            {
                s.Box(new Vector3(WallThick + 0.08f, walk - 0.5f, len / 2), new Vector3(0.16f, 0.3f, len), Coping);                // the string course
                s.Box(new Vector3(WallThick - 0.3f, walk + 0.55f, len / 2), new Vector3(0.6f, 1.1f, len), StoneTint, Coping);      // the outer parapet
                s.Box(new Vector3(0.2f, walk + 0.45f, len / 2), new Vector3(0.4f, 0.9f, len), StoneTint, Coping);                   // the inner low wall
                for (float z = 0.5f; z + 1.0f < len; z += 1.8f)
                    s.Box(new Vector3(WallThick - 0.3f, walk + 1.5f, z + 0.5f), new Vector3(0.6f, 0.8f, 1.0f), StoneTint, Coping);  // merlons
                if (arcade)
                {
                    // relieving arches on the inner face carry the wall-walk: a band along the top, piers, and haunches
                    s.Box(new Vector3(-0.5f, walk - 1.2f, len / 2), new Vector3(1.0f, 2.4f, len), StoneTint, Coping);
                    for (float z = 0; z <= len + 0.01f; z += 5.5f)
                    {
                        var at = Vector3.Transform(new Vector3(-0.5f, 0, z), f);
                        if (!PierAt(at)) continue; // the gates, the gallery
                        s.Box(new Vector3(-0.5f, (walk - 2.4f + Base) / 2, z), new Vector3(1.0f, walk - 2.4f - Base, 1.6f), StoneTint);
                        foreach (float dz in new[] { -1.1f, 1.1f })
                            if (z + dz > 0.3f && z + dz < len - 0.3f) s.Box(new Vector3(-0.5f, walk - 2.7f, z + dz), new Vector3(1.0f, 0.6f, 0.6f), StoneTint);
                    }
                }
            }
            // torches along the wall-walk, on the inner railing (or the inner low wall, without the kit)
            float tx = kit is null ? 0.2f : -0.8f, ty = kit is null ? 1.2f : 1.05f;
            for (float z = 7; z < len - 2; z += 14)
            {
                Lantern(w, new Vector3(tx, walk + ty, z));
                p.Lanterns.Add(Vector3.Transform(new Vector3(tx, walk + ty, z), f));
            }
            rock.Box(new Vector3(WallThick + 7, (Base - 1) / 2, len / 2), new Vector3(14, -1 - Base, len + 0.5f), Palette.Hex(0xB4BAC2)); // the scarp
            wall.Append(s.Transform(f)); lit.Append(w.Transform(f)); p.Scarp.Append(rock.Transform(f));
        }
        Run(new(East, 0, NorthLimit), new(East, 0, -40.4f), WalkUp, p.FarWall, p.FarUpper, p.FarWindows, true);
        Run(new(East, 0, -40.4f), new(East, 0, -14), WalkN, p.FarWall, p.FarUpper, p.FarWindows, true);
        Run(new(East, 0, -14), new(East, 0, GateZ), WalkN, p.Wall, p.Upper, p.Windows, true,
            notch: (WaterGateZ + 14 - 3.6f, WaterGateZ + 14 + 3.6f, 8.0f, 0.5f));
        Run(new(East, 0, GateZ), new(WallX(Front), 0, Front), WalkS, p.Wall, p.Upper, p.Windows, true,
            noPier: z => z > 7.0f && z < 17.0f, endsOnCut: true);                // the shelf's stair climbs this face

        // the southern run ends on the front cut: a wedge squares its end to the cut
        var dir = Vector2.Normalize(new Vector2(Bend, 1));
        var o = new Vector2(dir.Y, -dir.X);
        var i0 = new Vector2(WallX(Front), Front);
        var o1 = i0 + o * WallThick;
        var o2 = o1 + dir * ((Front - o1.Y) / dir.Y);
        Vector3 W(Vector2 q, float y) => new(q.X, y, q.Y);
        Face(p.Wall, W(i0, WalkS), W(o1, WalkS), W(o2, WalkS), W(o2, WalkS), Vector3.UnitY, Coping);
        Face(p.Wall, W(i0, Base), W(o2, Base), W(o2, WalkS), W(i0, WalkS), Vector3.UnitZ, StoneTint);
        Face(p.Wall, W(o1, Base), W(o2, Base), W(o2, WalkS), W(o1, WalkS), new Vector3(o.X, 0, o.Y), StoneTint);

        // the stair from the east shelf up to the wall-walk, against the southern run's inner face
        var sf = Frame(new Vector3(East, 0, GateZ), new Vector2(Bend, 1));
        var stair = new MeshBuilder();
        const int Treads = 16;
        for (int i = 0; i < Treads; i++)
        {
            float y = Tier1 + (WalkS - Tier1) * (i + 1) / Treads, z = 8 + 8f * (i + 0.5f) / Treads;
            stair.Box(new Vector3(-1.6f, (y + Tier1 - 0.3f) / 2, z), new Vector3(1.2f, y - Tier1 + 0.3f, 8f / Treads), Palette.Mul(StoneTint, 0.93f), Coping);
        }
        p.Wall.Append(stair.Transform(sf));

        // towers: half-round on the outer face; a slate cone, or battlements and a brazier
        void Tower(Vector3 c, float r, float top, bool roof, Vector3 look, MeshBuilder wall, MeshBuilder upper, MeshBuilder lit)
        {
            if (kit?["tower-crown"] is { } crown && kit["tower-roof"] is { } cone && kit["tower-band"] is { } band)
            {
                // the body in ashlar; string courses with arrow slits up it; a machicolated crown with a brazier, or a slate cone
                wall.Append(new MeshBuilder().Cylinder(c with { Y = Base }, r + 0.4f, r, top - Base, 16, StoneTint, Coping));
                var radius = Matrix4x4.CreateScale(r / 3, 1, r / 3);
                for (float y = Base + 4.5f; y < top - 2.5f; y += 4.5f) Place(band, radius * Matrix4x4.CreateTranslation(c with { Y = y }), wall, upper, lit);
                Place(roof ? cone : crown, radius * Matrix4x4.CreateTranslation(c with { Y = top }), wall, upper, lit);
                if (!roof) p.Lanterns.Add(c with { Y = top + 2.0f });
                lit.Box(c + look * (r + 0.02f) + new Vector3(0, top - 1.6f, 0), MathF.Abs(look.X) > 0.5f ? new Vector3(0.06f, 0.8f, 0.45f) : new Vector3(0.45f, 0.8f, 0.06f), Color.White);
                return;
            }
            wall.Append(new MeshBuilder().Cylinder(c with { Y = Base }, r + 0.4f, r, top - Base, 12, StoneTint, Coping));
            wall.Append(new MeshBuilder().Ring(c with { Y = top }, r + 0.15f, r - 0.45f, 1.0f, 12, StoneTint, Coping));
            if (roof) upper.Append(new MeshBuilder().Cone(c with { Y = top + 0.8f }, r + 0.45f, r * 1.35f, 12, Slate[0]));
            else
            {
                for (int k = 0; k < 10; k++)
                {
                    float a = MathF.Tau * k / 10;
                    wall.Box(c + new Vector3(MathF.Cos(a) * (r - 0.15f), top + 1.4f, MathF.Sin(a) * (r - 0.15f)), new Vector3(0.6f, 0.8f, 0.6f), StoneTint, Coping);
                }
                Lantern(lit, c with { Y = top + 1.0f });
                p.Lanterns.Add(c with { Y = top + 1.0f });
            }
            // a lit guard-room window
            var size = MathF.Abs(look.X) > 0.5f ? new Vector3(0.06f, 0.8f, 0.45f) : new Vector3(0.45f, 0.8f, 0.06f);
            lit.Box(c + look * (r + 0.02f) + new Vector3(0, top - 1.6f, 0), size, Color.White);
        }
        Tower(new Vector3(East + WallThick + 0.4f, 0, GateZ), 3.4f, 15, true, -Vector3.UnitZ, p.Wall, p.Upper, p.Windows); // the water tower
        Doorway(p.Upper, p.Wall, new Vector3(14.7f, WalkN, GateZ - 2.6f), -Vector3.UnitZ, 1.1f, 2.2f);   // the walks go in through it
        Doorway(p.Upper, p.Wall, new Vector3(16.9f, WalkS, GateZ + 3.4f), Vector3.UnitZ, 1.1f, 2.2f);
        foreach (var (z, n) in new[] { (-17.86f, Vector3.UnitZ), (-30.64f, -Vector3.UnitZ), (-21.05f, Vector3.UnitZ), (-27.45f, -Vector3.UnitZ) })
            Doorway(p.FarUpper, p.FarWall, new Vector3(14.7f, WalkN, z), n, 1.1f, 2.2f);
        Tower(new Vector3(East + WallThick, 0, -20.0f), 2.8f, 16.5f, false, -Vector3.UnitX, p.FarWall, p.FarUpper, p.FarWindows); // the Low Gate's
        Tower(new Vector3(East + WallThick, 0, -28.5f), 2.8f, 16.5f, false, -Vector3.UnitX, p.FarWall, p.FarUpper, p.FarWindows);
        Tower(new Vector3(East + WallThick, 0, -41.3f), 3.0f, 31, true, -Vector3.UnitX, p.FarWall, p.FarUpper, p.FarWindows);  // tier 2 meets the wall
        Tower(new Vector3(East + WallThick, 0, -62.0f), 3.0f, 36, false, -Vector3.UnitX, p.FarWall, p.FarUpper, p.FarWindows);

        // the Low Gate: a gatehouse over the wall-walk and an arch into the city at tier 1
        p.FarWall.Box(new Vector3(14.8f, WalkN + 1.4f, -24.25f), new Vector3(4.8f, 2.8f, 6.4f), StoneTint, Coping);
        var facingCity = Matrix4x4.CreateRotationY(-MathF.PI / 2);
        if (kit?["gatehouse"] is { } gatehouse)
        {
            var at = facingCity * Matrix4x4.CreateTranslation(East, Tier1, -24.25f);
            Place(gatehouse, at, p.FarWall, p.FarUpper, p.FarWindows, cloth: Palette.Hex(0x9C3A34));
            foreach (float x in new[] { -2.244f, 2.244f }) p.Lanterns.Add(Vector3.Transform(new Vector3(x, 3.0f, 2.65f), at));
        }
        else
        {
            for (int k = -2; k <= 2; k++) p.FarWall.Box(new Vector3(12.65f, WalkN + 3.2f, -24.25f + k * 1.4f), new Vector3(0.6f, 0.8f, 0.8f), StoneTint, Coping);
            Doorway(p.FarUpper, p.FarWall, new Vector3(East, Tier1, -24.25f), -Vector3.UnitX, 3.6f, 4.8f);
        }

        // the water gate through the wall's foot, where the stream leaves the city and the Spirit Pools' cave mouth sits
        if (kit?["water-gate"] is { } waterGate)
            Place(waterGate, facingCity * Matrix4x4.CreateTranslation(East, 0, WaterGateZ), p.Wall, p.Upper, p.Windows);
        else
        {
            Doorway(p.Upper, p.Wall, new Vector3(East, -0.4f, WaterGateZ), -Vector3.UnitX, 5.6f, 5.6f);
            for (float dz = -2.4f; dz <= 2.41f; dz += 0.4f)
                p.Upper.Box(new Vector3(East - 0.16f, 4.4f, WaterGateZ + dz), new Vector3(0.07f, 1.4f, 0.07f), Iron);
        }
    }

    /// <summary>Bakes a kit piece placed by <paramref name="m"/> (its origin first taken off): stone and trim into the wall's ashlar, lamps and glass into the lit mesh, the rest into the plain one.</summary>
    private static void Place(KitAsset.Piece piece, Matrix4x4 m, MeshBuilder wall, MeshBuilder upper, MeshBuilder lit, Color? cloth = null)
    {
        var at = Matrix4x4.CreateTranslation(-piece.Origin) * m;
        foreach (var (role, mesh, colour) in piece.Meshes)
            switch (role)
            {
                case "stone": wall.AppendMesh(mesh, at, StoneTint); break;
                case "trim": wall.AppendMesh(mesh, at, Coping); break;
                case "shade": wall.AppendMesh(mesh, at, Palette.Mul(StoneTint, 0.8f)); break;
                case "lamp" or "glass": lit.AppendMesh(mesh, at, Color.White); break;
                case "cloth": upper.AppendMesh(mesh, at, cloth ?? colour); break;
                default: upper.AppendMesh(mesh, at, colour); break;
            }
    }

    // ── the tier walls: a retaining wall on an arc round the citadel, buttressed, with a parapet ──
    private static void TierWall(MeshBuilder s, Layer L, float r, float foot, float top, Random rng, bool housesInFoot)
    {
        float a0 = AngleAt(r, WestLimit - 10), a1 = AngleAt(r, East + 0.5f);
        int n = (int)MathF.Ceiling((a1 - a0) * r / 2.5f);
        for (int i = 0; i < n; i++)
        {
            float t0 = a0 + (a1 - a0) * i / n, t1 = a0 + (a1 - a0) * (i + 1) / n, tm = (t0 + t1) / 2;
            var outward = new Vector3(MathF.Sin(tm), 0, MathF.Cos(tm));
            void Band(float ro, float ri, float y0, float y1)
            {
                Face(s, OnArc(ro, t0, y0), OnArc(ro, t1, y0), OnArc(ro, t1, y1), OnArc(ro, t0, y1), outward, StoneTint);
                Face(s, OnArc(ri, t0, y0), OnArc(ri, t1, y0), OnArc(ri, t1, y1), OnArc(ri, t0, y1), -outward, StoneTint);
                Face(s, OnArc(ro, t0, y1), OnArc(ro, t1, y1), OnArc(ri, t1, y1), OnArc(ri, t0, y1), Vector3.UnitY, Coping);
            }
            Band(r, r - 3, foot - 0.5f, top);
            Band(r + 0.15f, r - 0.6f, top - 1.2f, top - 0.9f);   // a moulded band under the parapet
            Band(r, r - 0.6f, top, top + 1.1f);                   // the parapet
        }
        // buttresses every 9 m; in the bays between them, on tier 2's wall, the doors and windows of houses built into its foot
        float step = 9 / r;
        for (float t = a0 + step / 2; t < a1 - step / 3; t += step)
        {
            var f = Frame(OnArc(r, t, 0), new Vector2(-MathF.Cos(t), MathF.Sin(t)));
            var b = new MeshBuilder(); var d = new MeshBuilder(); var w = new MeshBuilder();
            b.Box(new Vector3(0.4f, (foot + top - 1.4f) / 2, 0), new Vector3(0.8f, top - 1.4f - foot, 2.0f), StoneTint, Coping);
            if (housesInFoot)
                foreach (float z in new[] { -2.25f, 2.25f })
                {
                    d.Box(new Vector3(0.03f, foot + 1.05f, z - 0.6f), new Vector3(0.06f, 2.1f, 1.0f), Dark);
                    for (float y = foot + 2.9f; y < top - 2.5f; y += 3.0f)
                    {
                        bool on = rng.NextDouble() < 0.5;
                        (on ? w : d).Box(new Vector3(0.03f, y, z + 0.5f), new Vector3(0.06f, 1.2f, 0.8f), on ? Color.White : Glass);
                    }
                }
            s.Append(b.Transform(f)); L.Upper.Append(d.Transform(f)); L.Windows.Append(w.Transform(f));
        }
    }

    // ── the old baths, built against the north rim: the cistern under them overflows through a cracked arch ──
    private static void Baths(Parts p, Random rng)
    {
        var s = p.Baths; var u = p.Upper;
        float x0 = -2.6f, x1 = 6.4f, z0 = -16.8f, z1 = North, cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
        s.Box(new Vector3(cx, 5.75f, cz), new Vector3(x1 - x0, 12.5f, z1 - z0), StoneTint, Coping);
        s.Box(new Vector3(cx, 11.8f, cz), new Vector3(x1 - x0 + 0.3f, 0.4f, z1 - z0 + 0.3f), Coping);   // the cornice
        u.Gable(new Vector3(cx, 12, cz), x1 - x0, z1 - z0, 2.6f, Slate[3]);
        // half-moon windows high up; the porch on the north, its door boarded
        foreach (float x in new[] { -0.6f, cx, 4.4f })
        {
            u.Box(new Vector3(x, 10.2f, z0 - 0.03f), new Vector3(1.4f, 0.5f, 0.06f), Dark);
            u.Box(new Vector3(x, 10.55f, z0 - 0.03f), new Vector3(0.9f, 0.25f, 0.06f), Dark);
        }
        s.Box(new Vector3(cx, Tier1 + 1.6f, z0 - 0.7f), new Vector3(2.4f, 3.2f, 1.4f), StoneTint, Coping);
        u.Gable(new Vector3(cx, Tier1 + 3.2f, z0 - 0.7f), 2.4f, 1.4f, 0.9f, Slate[3]);
        u.Box(new Vector3(cx, Tier1 + 1.1f, z0 - 1.42f), new Vector3(1.2f, 2.2f, 0.06f), Palette.Hex(0x6A4E36));
        foreach (float y in new[] { 0.7f, 1.5f }) u.Box(new Vector3(cx, Tier1 + y, z0 - 1.46f), new Vector3(1.4f, 0.14f, 0.05f), Timber);
        // the back wall, over the courtyard: blind arches, the bricked-up overflow arch and the crack the falls burst from
        foreach (float x in new[] { -0.9f, cx, 4.7f })
        {
            s.Box(new Vector3(x, 7.9f, z1 + 0.04f), new Vector3(2.0f, 4.6f, 0.08f), Palette.Mul(StoneTint, 0.86f));
            s.Box(new Vector3(x, 10.4f, z1 + 0.04f), new Vector3(1.4f, 0.4f, 0.08f), Palette.Mul(StoneTint, 0.86f));
        }
        var f = HubWorld.FallsBase;
        s.Box(new Vector3(f.X, 3.35f, z1 + 0.3f), new Vector3(1.9f, 0.22f, 0.65f), StoneTint, Coping);       // the arch's broken lip
        if (!p.CornerKit) BathsArch(p, rng);
        Aqueduct(p);
    }

    /// <summary>The bricked-up overflow arch and its crack, moss and fallen stones, in boxes (the corner kit has the real one).</summary>
    private static void BathsArch(Parts p, Random rng)
    {
        var s = p.Corner; var u = p.Upper;
        float z1 = North;
        var f = HubWorld.FallsBase;
        s.Box(new Vector3(f.X, 3.6f, z1 + 0.06f), new Vector3(3.0f, 1.6f, 0.1f), Coping);                    // the old overflow arch, bricked up
        s.Box(new Vector3(f.X, 4.55f, z1 + 0.06f), new Vector3(2.0f, 0.4f, 0.1f), Coping);
        u.Box(new Vector3(f.X, 3.85f, z1 + 0.12f), new Vector3(1.4f, 0.9f, 0.05f), Dark);                     // where the keystone fell out
        u.Box(new Vector3(f.X + 0.3f, 4.45f, z1 + 0.12f), new Vector3(0.5f, 0.35f, 0.05f), Dark);
        for (int k = 0; k < 3; k++)                                                                            // the wet, mossy fan below it
            u.Box(new Vector3(f.X, 0.6f + k * 1.0f, z1 + 0.05f), new Vector3(4.0f - k * 0.9f, 1.0f, 0.04f), Palette.Lerp(MossCol, Palette.Hex(0x4E6A44), k * 0.3f));
        foreach (var (x, z, sz) in new[] { (0.4f, -8.6f, 0.45f), (2.9f, -8.7f, 0.5f), (3.4f, -8.0f, 0.3f), (-0.1f, -7.9f, 0.32f) })
            s.Box(new Vector3(x, sz / 2, z), new Vector3(sz * 1.3f, sz, sz), StoneTint, Coping);                // fallen arch stones
        Ivy(u, new Vector3(-2.4f, 4.4f, z1 + 0.05f), Vector3.UnitX, Vector3.UnitZ, 2.6f, 4.5f, 60, rng);
        Ivy(u, new Vector3(4.2f, 4.4f, z1 + 0.05f), Vector3.UnitX, Vector3.UnitZ, 2.0f, 6.5f, 60, rng);
    }

    /// <summary>The aqueduct that feeds the baths' cistern from tier 2: an arcade, round-headed bays between its piers.</summary>
    private static void Aqueduct(Parts p)
    {
        var a = p.Stone;
        var f = HubWorld.FallsBase;
        float z0 = -16.8f;
        float wallZ = Citadel.Y + MathF.Sqrt(Tier2Wall * Tier2Wall - MathF.Pow(f.X - Citadel.X, 2));
        const float Spring = 11.4f;
        for (float z = z0 - 2.6f; z > wallZ + 1; z -= 4.6f)
        {
            a.Box(new Vector3(f.X, (Tier1 + Spring) / 2, z), new Vector3(1.6f, Spring - Tier1, 1.4f), StoneTint, Coping);
            foreach (float dz in new[] { -1.05f, 1.05f }) a.Box(new Vector3(f.X, Spring - 0.35f, z + dz), new Vector3(1.6f, 0.7f, 0.7f), StoneTint);
            a.Box(new Vector3(f.X, 8.5f, z), new Vector3(1.8f, 0.3f, 1.6f), Coping);                            // a tie course
        }
        a.Box(new Vector3(f.X, (Spring + 14.4f) / 2, (z0 + wallZ) / 2), new Vector3(1.6f, 14.4f - Spring, z0 - wallZ), StoneTint, Coping);
        foreach (float dx in new[] { -0.675f, 0.675f }) a.Box(new Vector3(f.X + dx, 14.65f, (z0 + wallZ) / 2), new Vector3(0.25f, 0.5f, z0 - wallZ), StoneTint, Coping);
        a.Box(new Vector3(f.X, 13.1f, z0 + 0.2f), new Vector3(1.0f, 2.0f, 0.8f), StoneTint, Coping);            // it drops into the cistern
    }

    // ── buildings placed by hand: the stable and the gate inn by the Low Gate; the bridge-room; market stalls; washing ──
    private static void Landmarks(Parts p, Layer near, Random rng)
    {
        Building(p, near, new Vector3(8.4f, Tier1, -12.0f), MathF.PI / 2, 4.4f, 3.6f, Kind.Stable, rng, 0.6f, 0.2f, gableFront: false);
        Building(p, near, new Vector3(8.0f, Tier1, -18.6f), MathF.Atan2(4.9f, -5.65f), 5.6f, 5.0f, Kind.Inn, rng, 0.85f, 0.3f, gableFront: false);
        // a bridge-room over the alley from the north stair to the main street, on its own piers
        p.Stone.Box(new Vector3(AlleyX, Tier1 + 4.0f, -12.6f), new Vector3(2.6f, 2.4f, 3.0f), StoneTint, Coping);
        p.Upper.Gable(new Vector3(AlleyX, Tier1 + 5.2f, -12.6f), 2.6f, 3.0f, 1.4f, Slate[2]);
        foreach (float dx in new[] { -1.1f, 1.1f }) p.Stone.Box(new Vector3(AlleyX + dx, Tier1 + 1.4f, -12.6f), new Vector3(0.4f, 2.8f, 3.0f), StoneTint);
        p.Windows.Box(new Vector3(AlleyX, Tier1 + 4.1f, -11.08f), new Vector3(0.5f, 0.7f, 0.05f), Palette.Hex(0xE8C890));
        // the trough on the east shelf
        p.Stone.Box(new Vector3(15.4f, Tier1 + 0.35f, 10.6f), new Vector3(2.0f, 0.7f, 0.8f), StoneTint, Coping);
        // market stalls along the front of W2, their backs to the cut
        for (float x = -44; x < -15; x += 3.4f)
        {
            if (rng.NextDouble() < 0.25) continue;
            float z = 11.2f;
            var canvas = Linen[1 + rng.Next(Linen.Length - 1)];
            p.Upper.Box(new Vector3(x, Tier1 + 0.5f, z - 0.3f), new Vector3(2.6f, 1.0f, 0.8f), Timber);                        // the counter
            foreach (float dx in new[] { -1.25f, 1.25f })
                p.Upper.Box(new Vector3(x + dx, Tier1 + 1.25f, z + 0.5f), new Vector3(0.1f, 2.5f, 0.1f), Timber);
            Face(p.Upper, new Vector3(x - 1.4f, Tier1 + 2.1f, z - 1.0f), new Vector3(x + 1.4f, Tier1 + 2.1f, z - 1.0f),
                 new Vector3(x + 1.4f, Tier1 + 2.6f, z + 0.7f), new Vector3(x - 1.4f, Tier1 + 2.6f, z + 0.7f), Vector3.UnitY, canvas);
            for (int k = 0; k < 3; k++) p.Upper.Box(new Vector3(x - 0.8f + k * 0.8f, Tier1 + 1.1f, z - 0.35f), new Vector3(0.35f, 0.2f, 0.35f), Linen[rng.Next(Linen.Length)]);
        }
        // washing over the back lane W1, and across the inn yard
        foreach (float x in new[] { -14.5f, -21.0f, -26.5f })
        {
            float a = AngleAt(106, x);
            Washing(p.Upper, OnArc(103.9f, a, Tier1 + 4.6f), OnArc(108.1f, a, Tier1 + 4.4f), rng);
        }
        Washing(p.Upper, new Vector3(10.6f, Tier1 + 3.6f, -14.4f), new Vector3(12.0f, Tier1 + 3.4f, -10.2f), rng);
    }

    /// <summary>A washing line from a to b, sagging, hung with linen.</summary>
    private static void Washing(MeshBuilder m, Vector3 a, Vector3 b, Random rng)
    {
        float len = Vector3.Distance(a, b);
        Vector3 P(float t) => Vector3.Lerp(a, b, t) - new Vector3(0, 0.5f * 4 * t * (1 - t), 0);
        for (float t = 0; t <= 1; t += 0.1f / len) m.Box(P(t), new Vector3(0.05f, 0.03f, 0.05f), Palette.Hex(0xD8D0C0));
        bool alongX = MathF.Abs(b.X - a.X) > MathF.Abs(b.Z - a.Z);
        for (float t = 0.12f; t < 0.9f; t += 0.55f / len)
            if (rng.NextDouble() < 0.75)
            {
                float hgt = 0.35f + (float)rng.NextDouble() * 0.3f, wid = 0.3f + (float)rng.NextDouble() * 0.25f;
                m.Box(P(t) - new Vector3(0, hgt / 2, 0), alongX ? new Vector3(wid, hgt, 0.03f) : new Vector3(0.03f, hgt, wid), Linen[rng.Next(Linen.Length)]);
            }
    }

    // ── rows of buildings along the tiers' arcs ──
    private static void Rows(Parts p, Layer near, Layer far, Random rng)
    {
        // places kept clear: the baths and their square, the stable and inn, the alley from the north stair, the gate
        // square, the aqueduct's alley, the old well
        bool Clear(Vector2 c) =>
            (c.X > -3.4f && c.X < 7.2f && c.Y > -19.5f) || (c.X > 5.0f && c.Y > -21.5f) || (c.X > AlleyX - 1.3f && c.X < AlleyX + 1.3f && c.Y > -19)
            || (c.X > 8.5f && c.Y > -31) || (MathF.Abs(c.X - HubWorld.FallsBase.X) < 1.8f && c.Y < -16) || Vector2.Distance(c, new Vector2(-17.5f, -4.7f)) < 1.2f;
        // (layer, street edge radius, faces toward the citadel?, deepest lot, how far back it may reach, level, x range, lit fronts, lit backs)
        Row(p, near, 98, true, 7.0f, 103.9f, Tier1, WestLimit, East - 0.5f, 0.4f, 0.3f, Clear, rng);          // A: backs to the courtyard
        Row(p, near, 114.75f, false, 6.5f, 108.1f, Tier1, WestLimit, West - 1.2f, 0.4f, 0.25f, Clear, rng);   // WB: fronts onto W2
        Row(p, far, 94, false, 4.5f, 89.5f, Tier1, WestLimit, East - 0.5f, 0.65f, 0.3f, Clear, rng);          // B: fronts onto the main street
        Row(p, far, 85, true, 4.5f, 89.5f, Tier1, WestLimit, East - 0.5f, 0.6f, 0.3f, Clear, rng);            // C: fronts onto Wallfoot Lane
        Row(p, far, 76, false, 6.0f, 70.2f, Tier2, WestLimit - 6, East - 0.5f, 0.7f, 0.3f, null, rng);        // tier 2, looking out over the wall
        Row(p, far, 64.5f, true, 5.5f, 70.2f, Tier2, WestLimit - 6, East - 0.5f, 0.6f, 0.3f, null, rng);
        Row(p, far, 56, false, 6.0f, 49, Tier3, WestLimit - 10, East - 0.5f, 0.7f, 0.3f, null, rng);          // tier 3
    }

    private static void Row(Parts p, Layer L, float rFront, bool inward, float depthMax, float rLimit, float y, float xMin, float xMax,
                            float litFront, float litBack, Func<Vector2, bool>? clear, Random rng)
    {
        float a0 = AngleAt(rFront, xMin), a1 = AngleAt(rFront, xMax);
        float s = 0, len = (a1 - a0) * rFront;
        int sinceAlley = 0;
        while (s < len)
        {
            var kind = rng.NextDouble() switch { < 0.6 => Kind.House, < 0.8 => Kind.Shop, < 0.86 => Kind.Inn, < 0.94 => Kind.Stable, _ => Kind.Tower };
            float w = kind switch { Kind.Inn => 8 + (float)rng.NextDouble() * 3, Kind.Stable => 6 + (float)rng.NextDouble() * 2, Kind.Tower => 4.2f, _ => 4.5f + (float)rng.NextDouble() * 2 };
            float a = a0 + (s + w / 2) / rFront;
            var radial = new Vector2(MathF.Sin(a), MathF.Cos(a));
            var front = inward ? -radial : radial;
            float yaw = MathF.Atan2(front.X, front.Y);
            // the deepest lot that fits: behind the street edge, short of rLimit, clear of the pocket, the front cut and the wall
            for (float d = depthMax; d >= 3.5f; d -= 0.5f)
            {
                float rb = inward ? rFront + d : rFront - d;
                bool fits = inward ? rb <= rLimit : rb >= rLimit;
                var c = OnArc(rFront + (inward ? d / 2 : -d / 2), a, y);
                var right = new Vector2(front.Y, -front.X);
                foreach (var (u, v) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                {
                    var k = new Vector2(c.X, c.Z) + right * (u * w / 2) + front * (v * d / 2);
                    if (InPocket(k.X, k.Y, 1.2f) || k.Y > Front - 0.4f || k.X > WallX(k.Y) - 0.5f || clear?.Invoke(k) == true) fits = false;
                }
                if (!fits) continue;
                Building(p, L, c, yaw, w, d, kind, rng, litFront, litBack, gableFront: rng.NextDouble() < 0.25);
                break;
            }
            // terraces, with now and then an alley between
            bool alley = ++sinceAlley > 3 && rng.NextDouble() < 0.3;
            if (alley) sinceAlley = 0;
            s += w + (alley ? 1.6f + (float)rng.NextDouble() : (float)rng.NextDouble() * 0.15f);
        }
    }

    /// <summary>
    /// One building, its front (local +Z) to its street: a stone ground floor, stone or jettied plaster storeys, a steep
    /// slate roof (ridge along the street, or gable-front) or a flat leaded one behind a parapet, a chimney; lit
    /// windows mostly on the front, few on the back.
    /// </summary>
    private static void Building(Parts p, Layer L, Vector3 at, float yaw, float w, float d, Kind kind, Random rng, float litFront, float litBack, bool gableFront)
    {
        if (p.Kit?.Pick(kind, rng) is { } piece)
        {
            FromKit(p, L, piece, at, yaw, w, d, rng, litFront, litBack);
            return;
        }
        var s = new MeshBuilder(); var u = new MeshBuilder(); var win = new MeshBuilder();
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        Color Warm() => Palette.Lerp(Color.White, Palette.Hex(0xA86A3A), R(0, 0.55f));
        const float G = 2.8f, U = 2.4f;
        Vector3? lamp = null;
        bool stoneUp = kind == Kind.Tower || rng.NextDouble() < 0.85;
        bool flat = kind == Kind.Tower || (stoneUp && kind != Kind.Stable && rng.NextDouble() < 0.15);
        int uppers = kind switch { Kind.Stable => 0, Kind.Tower => 3 + rng.Next(2), Kind.Inn => 2, _ => 1 + rng.Next(2) + (rng.NextDouble() < 0.15 ? 1 : 0) };
        float g = kind == Kind.Stable ? 3.2f : G, top = g + uppers * U, jetty = stoneUp || uppers == 0 ? 0 : 0.25f;
        int cols = w > 5.5f ? 3 : 2;
        var tint = Palette.Lerp(StoneTint, Palette.Hex(0xD3CEC5), (float)rng.NextDouble());
        s.Box(new Vector3(0, g / 2, 0), new Vector3(w, g, d), tint, Coping);
        if (uppers > 0)
        {
            var col = stoneUp ? tint : Plaster[rng.Next(Plaster.Length)];
            (stoneUp ? s : u).Box(new Vector3(0, g + uppers * U / 2, jetty / 2), new Vector3(w + (stoneUp ? 0 : 0.1f), uppers * U, d + jetty), col);
            if (!stoneUp)
            {
                u.Box(new Vector3(0, g + 0.07f, jetty / 2), new Vector3(w + 0.24f, 0.14f, d + jetty + 0.14f), Timber);
                for (int k = 0; k <= cols; k++) u.Box(new Vector3(-w / 2 + k * w / cols, g + uppers * U / 2, d / 2 + jetty + 0.06f), new Vector3(0.14f, uppers * U, 0.04f), Timber);
                for (int f = 1; f < uppers; f++) u.Box(new Vector3(0, g + f * U, d / 2 + jetty + 0.06f), new Vector3(w, 0.1f, 0.04f), Timber);
            }
        }
        float fz = d / 2 + jetty;
        var roofCol = rng.NextDouble() < 0.04 ? Tile[rng.Next(Tile.Length)] : Slate[rng.Next(Slate.Length)];
        float roofH;
        if (flat)
        {
            roofH = 0;
            u.Box(new Vector3(0, top + 0.05f, 0), new Vector3(w - 0.4f, 0.1f, d - 0.4f), Lead);
            s.Box(new Vector3(0, top + 0.1f, 0), new Vector3(w + 0.3f, 0.2f, d + 0.3f), Coping);
            foreach (var (x, z, sx, sz) in new[] { (0f, d / 2, w, 0.4f), (0f, -d / 2, w, 0.4f), (w / 2, 0f, 0.4f, d), (-w / 2, 0f, 0.4f, d) })
                s.Box(new Vector3(x, top + 0.6f, z), new Vector3(sx, 0.9f, sz), tint, Coping);
        }
        else if (gableFront)
        {
            roofH = R(0.45f, 0.7f) * w;
            u.Append(new MeshBuilder().Gable(Vector3.Zero, d + jetty, w, roofH, roofCol).Transform(Matrix4x4.CreateRotationY(MathF.PI / 2) * Matrix4x4.CreateTranslation(0, top, jetty / 2)));
            // the gable over the street: an attic window
            Pane(win, u, new Vector3(0, top + roofH * 0.35f, fz + 0.17f), 0.5f, 0.7f, rng.NextDouble() < litFront * 0.6f, Warm());
        }
        else
        {
            roofH = R(0.45f, 0.7f) * (d + jetty);
            u.Gable(new Vector3(0, top, jetty / 2), w, d + jetty, roofH, roofCol);
            if (rng.NextDouble() < 0.3)
                foreach (float sx in new[] { -1f, 1f }) Pane(win, u, new Vector3(sx * (w / 2 + 0.17f), top + roofH * 0.3f, jetty / 2), 0.35f, 0.5f, rng.NextDouble() < 0.2f, Warm(), side: true);
        }
        if (!flat && rng.NextDouble() < 0.6)
            s.Box(new Vector3((rng.NextDouble() < 0.5 ? -1 : 1) * (w / 2 - 0.35f), top + roofH * 0.6f, R(-d / 4, d / 4)), new Vector3(0.5f, roofH + 1.4f, 0.6f), tint, Coping);

        // the front: doors, shopfronts, windows, a balcony now and then
        switch (kind)
        {
            case Kind.Stable:
                for (float x = -w / 2 + 1.5f; x < w / 2 - 1; x += 2.6f) u.Box(new Vector3(x, 1.2f, d / 2 + 0.03f), new Vector3(1.8f, 2.4f, 0.06f), Dark);
                lamp = new Vector3(w / 2 - 0.5f, 2.6f, d / 2 + 0.2f);
                // the hayloft door under the back eave
                u.Box(new Vector3(0, g - 0.75f, -d / 2 - 0.03f), new Vector3(1.2f, 1.3f, 0.06f), Timber);
                break;
            case Kind.Shop:
                bool open = rng.NextDouble() < 0.5;
                (open ? win : u).Box(new Vector3(-w * 0.12f, 1.05f, d / 2 + 0.03f), new Vector3(w * 0.6f, 1.9f, 0.06f), open ? Warm() : Dark);
                u.Box(new Vector3(w * 0.36f, 1.0f, d / 2 + 0.03f), new Vector3(0.9f, 2.0f, 0.06f), Dark);
                u.Box(new Vector3(-w * 0.12f, 2.35f, d / 2 + 0.45f), new Vector3(w * 0.66f, 0.08f, 0.9f), Linen[1 + rng.Next(Linen.Length - 1)]);
                break;
            default:
                float door = R(-w / 4, w / 4);
                u.Box(new Vector3(door, 1.0f, d / 2 + 0.03f), new Vector3(kind == Kind.Inn ? 1.6f : 1.0f, 2.0f, 0.06f), Dark);
                Pane(win, u, new Vector3(door > 0 ? -w / 4 : w / 4, 1.5f, d / 2 + 0.02f), 0.6f, 0.8f, rng.NextDouble() < litFront * 0.6f, Warm());
                if (kind == Kind.Inn)
                {
                    u.Box(new Vector3(door + 1.3f, 2.9f, d / 2 + 0.55f), new Vector3(0.08f, 0.08f, 1.1f), Timber);
                    u.Box(new Vector3(door + 1.3f, 2.5f, d / 2 + 0.95f), new Vector3(0.06f, 0.55f, 0.7f), Palette.Hex(0x8A6A3A));
                    lamp = new Vector3(door - 1.1f, 2.4f, d / 2 + 0.2f);
                }
                break;
        }
        for (int f = 0; f < uppers; f++)
            for (int k = 0; k < cols; k++)
                Pane(win, u, new Vector3(-w / 2 + (k + 0.5f) * w / cols, g + f * U + 1.25f, fz + 0.02f), kind == Kind.Tower ? 0.4f : 0.55f, 0.9f, rng.NextDouble() < litFront, Warm());
        if (!stoneUp && uppers > 0 && rng.NextDouble() < 0.2) Balcony(u, new Vector3(0, g, fz), w * 0.5f, 1);
        // the back: a small window a storey, some with a window box; a balcony over the yard now and then
        for (int f = 0; f < uppers; f++)
        {
            var c = new Vector3(R(-w / 3, w / 3), g + f * U + 1.25f, -d / 2 - 0.02f);
            Pane(win, u, c, 0.4f, 0.55f, rng.NextDouble() < litBack, Warm(), back: true);
            if (rng.NextDouble() < 0.3)
            {
                u.Box(c + new Vector3(0, -0.4f, -0.12f), new Vector3(0.6f, 0.16f, 0.22f), Timber);
                for (int b = 0; b < 3; b++) u.Box(c + new Vector3(-0.2f + b * 0.2f, -0.26f, -0.14f), new Vector3(0.14f, 0.14f, 0.14f), b == 1 ? Palette.Hex(0xC85A4A) : Leaves[b % 3]);
            }
        }
        if (uppers > 0 && rng.NextDouble() < 0.12) Balcony(u, new Vector3(0, g, -d / 2), w * 0.45f, -1);

        var m = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(at);
        L.Stone.Append(s.Transform(m)); L.Upper.Append(u.Transform(m)); L.Windows.Append(win.Transform(m));
        if (lamp is { } lp)
        {
            Lantern(L.Windows, Vector3.Transform(lp, m));
            p.Lanterns.Add(Vector3.Transform(lp, m));
        }
    }

    /// <summary>
    /// One building baked from a kit piece, scaled to its lot and turned to its street: stone and trim into the masonry
    /// layer; the roof and any awning tinted for this building; each set of windows lit or dark; its lantern lit.
    /// </summary>
    private static void FromKit(Parts p, Layer L, Kit.Piece piece, Vector3 at, float yaw, float w, float d, Random rng, float litFront, float litBack)
    {
        var m = Matrix4x4.CreateTranslation(-piece.Origin) * Matrix4x4.CreateScale(w / piece.W, 1, d / piece.D)
              * Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateTranslation(at);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        var stone = Palette.Lerp(StoneTint, Palette.Hex(0xD3CEC5), R(0, 1));   // the city's stone, like the code buildings
        var roof = rng.NextDouble() < 0.04 ? Tile[rng.Next(Tile.Length)] : Slate[rng.Next(Slate.Length)];
        var cloth = Linen[1 + rng.Next(Linen.Length - 1)];
        var lit = new Dictionary<string, bool> { ["glass-a"] = rng.NextDouble() < litFront, ["glass-b"] = rng.NextDouble() < litFront, ["glass-back"] = rng.NextDouble() < litBack };
        var warm = Palette.Lerp(Color.White, Palette.Hex(0xA86A3A), R(0, 0.55f));
        foreach (var (role, mesh, colour) in piece.Meshes)
        {
            switch (role)
            {
                case "stone": L.Stone.AppendMesh(mesh, m, stone); break;
                case "trim": L.Stone.AppendMesh(mesh, m, Coping); break;
                case "roof": L.Upper.AppendMesh(mesh, m, roof); break;
                case "cloth": L.Upper.AppendMesh(mesh, m, cloth); break;
                case "lamp": L.Windows.AppendMesh(mesh, m, Color.White); break;
                case "glass-a" or "glass-b" or "glass-back":
                    if (lit[role]) L.Windows.AppendMesh(mesh, m, warm);
                    else L.Upper.AppendMesh(mesh, m, Glass);
                    break;
                default: L.Upper.AppendMesh(mesh, m, colour); break;
            }
        }
        if (piece.Lamp is { } lamp) p.Lanterns.Add(Vector3.Transform(lamp, m));
    }

    /// <summary>A window on a face: lit (the emissive mesh, in its own warmth) or dark glass, with a sill.</summary>
    private static void Pane(MeshBuilder lit, MeshBuilder dark, Vector3 c, float w, float h, bool on, Color warm, bool back = false, bool side = false)
    {
        var size = side ? new Vector3(0.05f, h, w) : new Vector3(w, h, 0.05f);
        (on ? lit : dark).Box(c, size, on ? warm : Glass);
        var n = side ? new Vector3(MathF.Sign(c.X), 0, 0) : new Vector3(0, 0, back ? -1 : 1);
        dark.Box(c + n * 0.06f - new Vector3(0, h / 2 + 0.04f, 0), side ? new Vector3(0.14f, 0.08f, w + 0.14f) : new Vector3(w + 0.14f, 0.08f, 0.14f), Coping);
    }

    /// <summary>A timber balcony on a storey floor, out from the front (dir 1) or back (dir -1) face at z.</summary>
    private static void Balcony(MeshBuilder m, Vector3 floor, float w, int dir)
    {
        m.Box(floor + new Vector3(0, 0.1f, dir * 0.4f), new Vector3(w, 0.12f, 0.8f), Timber);
        m.Box(floor + new Vector3(0, 0.6f, dir * 0.78f), new Vector3(w, 0.06f, 0.06f), Timber);
        for (float x = -w / 2; x <= w / 2 + 0.01f; x += w / 4) m.Box(floor + new Vector3(x, 0.38f, dir * 0.78f), new Vector3(0.06f, 0.5f, 0.06f), Timber);
    }

    private static void Lantern(MeshBuilder lit, Vector3 c) => lit.Box(c, new Vector3(0.22f, 0.3f, 0.22f), Color.White);

    // ── street lanterns on brackets: the main street, the west lanes, Wallfoot Lane, tier 2's parapet walk, the gate ──
    private static void Lanterns(Parts p, Layer near, Layer far)
    {
        void Along(Layer L, float r, float y, float xMin, float xMax, float every)
        {
            float a0 = AngleAt(r, xMin), a1 = AngleAt(r, xMax);
            for (float a = a0 + every / 2 / r; a < a1; a += every / r)
            {
                var c = OnArc(r, a, y);
                if (InPocket(c.X, c.Z, 1.5f)) continue;
                var outward = new Vector3(MathF.Sin(a), 0, MathF.Cos(a));
                L.Upper.Box(c - outward * 0.3f + new Vector3(0, 0.25f, 0), new Vector3(0.08f, 0.08f, 0.08f) + new Vector3(MathF.Abs(outward.X), 0, MathF.Abs(outward.Z)) * 0.6f, Iron);
                Lantern(L.Windows, c);
                p.Lanterns.Add(c);
            }
        }
        Along(near, 94.4f, Tier1 + 3.2f, WestLimit, East - 1, 10);
        Along(near, 104.6f, Tier1 + 3.2f, WestLimit, West - 2, 11);
        Along(near, 115.1f, Tier1 + 3.2f, WestLimit, West - 2, 11);
        Along(far, 82.4f, Tier1 + 3.2f, WestLimit, East - 1, 12);
        Along(far, 76.4f, Tier2 + 3.0f, WestLimit, East - 1, 14);
        if (p.WallKit?["gatehouse"] is null)
            foreach (float z in new[] { -22.4f, -26.1f })
        {
            var c = new Vector3(12.3f, Tier1 + 1.2f, z);
            Lantern(near.Windows, c); p.Lanterns.Add(c);   // braziers inside the Low Gate
        }
    }
}
