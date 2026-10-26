using System.Numerics;
using Atrium.Diorama.Render;
using Raylib_cs;

namespace Atrium.Diorama.Scenes;

/// <summary>The Sanctum's furniture, built from code: chunky low-poly pieces in world space (or local, for the moving parts).</summary>
public sealed partial class SanctumScene
{
    private static Vector3 V(float x, float y, float z) => new(x, y, z);

    /// <summary>A box turned by <paramref name="rot"/> about its own centre.</summary>
    private static void RBox(MeshBuilder m, Vector3 c, Vector3 size, Matrix4x4 rot, Color col, Color? top = null) =>
        m.Append(new MeshBuilder().Box(Vector3.Zero, size, col, top).Transform(rot * Matrix4x4.CreateTranslation(c)));

    /// <summary>A thin square rod from <paramref name="a"/> to <paramref name="b"/>.</summary>
    private static void Strut(MeshBuilder m, Vector3 a, Vector3 b, float th, Color col)
    {
        var d = b - a;
        float len = d.Length();
        if (len < 1e-4f) return;
        var dir = d / len;
        var axis = Vector3.Cross(Vector3.UnitY, dir);
        var q = axis.LengthSquared() < 1e-8f
            ? (dir.Y >= 0 ? Quaternion.Identity : Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI))
            : Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.UnitY, dir), -1, 1)));
        RBox(m, (a + b) / 2, V(th, len, th), Matrix4x4.CreateFromQuaternion(q), col);
    }

    /// <summary>A flat chalk stroke on the floor.</summary>
    private static void FloorLine(MeshBuilder m, Vector3 a, Vector3 b, float w, Color col)
    {
        var d = b - a; d.Y = 0;
        if (d.LengthSquared() < 1e-8f) return;
        d = Vector3.Normalize(d);
        var n = new Vector3(-d.Z, 0, d.X) * (w / 2);
        m.Quad(a + n, b + n, b - n, a - n, col);
    }

    /// <summary>A brass candlestick standing on <paramref name="baseY"/>, its wick just under <paramref name="flame"/>.</summary>
    private static void Candlestick(MeshBuilder m, Vector3 flame, float baseY, float dish = 0.12f)
    {
        var b = new Vector3(flame.X, baseY, flame.Z);
        m.Cylinder(b, dish, dish * 0.85f, 0.03f, 10, BrassDark, Brass);
        m.Cylinder(b + V(0, 0.03f, 0), 0.028f, 0.028f, 0.11f, 6, Brass);
        m.Cylinder(b + V(0, 0.14f, 0), 0.07f, 0.075f, 0.03f, 8, Brass);
        float top = flame.Y - 0.05f;
        float h = MathF.Max(0.06f, top - (baseY + 0.17f));
        m.Cylinder(b + V(0, 0.17f, 0), 0.05f, 0.048f, h, 8, CandleWax, Palette.Hex(0xF8F0DC));
        // a drip of wax
        m.Box(b + V(0.045f, 0.17f + h * 0.7f, 0.0f), V(0.02f, h * 0.35f, 0.025f), CandleWax);
        m.Box(V(flame.X, top + 0.02f, flame.Z), V(0.008f, 0.04f, 0.008f), Palette.Ink);
    }

    // ───────────────────────── the room itself ─────────────────────────

    /// <summary>Flagstones, plastered walls over a stone wainscot, and the window on the left wall.</summary>
    protected override MeshBuilder BuildShell()
    {
        var m = new MeshBuilder();
        var rng = new Random(5);
        m.Box(V(0, -0.16f, 0), V(11.2f, 0.2f, 11.2f), Palette.Hex(0x5A4C42)); // the grout bed
        var flag = Palette.Hex(0xB9A88C);
        for (int row = 0; row < 10; row++)
        {
            float z = -5.4f + 0.54f + row * 1.08f;
            float x = -5.4f - (row % 2) * 0.5f;
            while (x < 5.4f)
            {
                float w = 0.8f + (float)rng.NextDouble() * 0.7f;
                float x0 = MathF.Max(x, -5.4f), x1 = MathF.Min(x + w, 5.5f);
                if (x1 - x0 > 0.12f)
                {
                    var c = Palette.Mul(flag, 0.88f + (float)rng.NextDouble() * 0.16f);
                    m.Box(V((x0 + x1) / 2, -0.04f, z), V(x1 - x0 - 0.05f, 0.08f, 1.03f), c);
                }
                x += w;
            }
        }
        var plaster = Palette.Lerp(Palette.Hex(0xD8C8AA), Accent, 0.12f);
        var stone = Palette.Hex(0xA99678);
        m.Box(V(0, 3.2f, -5.6f), V(11.2f, 6.6f, 0.4f), plaster);                       // back wall
        m.Box(V(-5.6f, 3.2f, 0), V(0.4f, 6.6f, 11.2f), Palette.Mul(plaster, 0.9f));   // left wall
        m.Box(V(0.1f, 0.55f, -5.36f), V(11f, 1.1f, 0.1f), stone, Palette.Mul(stone, 1.1f)); // wainscot
        m.Box(V(-5.36f, 0.55f, 0.1f), V(0.1f, 1.1f, 11f), Palette.Mul(stone, 0.92f), Palette.Mul(stone, 1.05f));
        m.Box(V(0.1f, 1.14f, -5.3f), V(11f, 0.08f, 0.16f), Palette.Bark);
        m.Box(V(-5.3f, 1.14f, 0.1f), V(0.16f, 0.08f, 11f), Palette.Bark);
        for (int i = 0; i < 11; i++) // stone courses on the wainscot
        {
            m.Box(V(-5.0f + i * 1.0f + (i % 2) * 0.2f, 0.55f, -5.3f), V(0.04f, 1.0f, 0.02f), Palette.Mul(stone, 0.8f));
            m.Box(V(-5.3f, 0.55f, -5.0f + i * 1.0f + (i % 2) * 0.2f), V(0.02f, 1.0f, 0.04f), Palette.Mul(stone, 0.75f));
        }
        m.Box(V(-5.25f, 3.2f, -5.25f), V(0.5f, 6.6f, 0.5f), Palette.Mul(stone, 1.05f)); // the corner pier
        m.Box(V(0.1f, 6.35f, -5.25f), V(11f, 0.35f, 0.5f), Palette.Bark);               // a beam along the top
        m.Box(V(-5.25f, 6.35f, 0.1f), V(0.5f, 0.35f, 11f), Palette.Bark);
        // the window on the left wall, letting in the courtyard's light
        m.Box(V(-5.38f, 3.6f, -1.5f), V(0.1f, 2.4f, 1.8f), Palette.Hex(0xF4E2B8));
        m.Box(V(-5.33f, 3.6f, -1.5f), V(0.06f, 2.5f, 0.1f), Palette.Bark);
        m.Box(V(-5.33f, 3.6f, -1.5f), V(0.06f, 0.1f, 1.9f), Palette.Bark);
        m.Box(V(-5.3f, 2.36f, -1.5f), V(0.22f, 0.08f, 2.1f), Palette.Mul(stone, 1.1f)); // sill
        return m;
    }

    /// <summary>The charter, the round window's frame, a bookcase, the sconce and the tall candle stand.</summary>
    private MeshBuilder BuildDecor()
    {
        var m = new MeshBuilder();
        // the charter: a hanging parchment with the red seal of the falls, the tree and four lamps
        var ch = V(-0.65f, 3.15f, -5.33f);
        m.Box(ch, V(0.95f, 1.3f, 0.02f), Palette.Parchment);
        m.Box(ch + V(0, 0.68f, 0.02f), V(1.1f, 0.06f, 0.06f), Palette.Bark);
        m.Box(ch + V(0, -0.68f, 0.02f), V(1.1f, 0.06f, 0.06f), Palette.Bark);
        for (int i = 0; i < 6; i++) m.Box(ch + V(-0.05f - (i % 2) * 0.06f, 0.42f - i * 0.11f, 0.012f), V(0.6f - (i % 3) * 0.1f, 0.018f, 0.006f), Palette.InkSoft);
        m.Append(new MeshBuilder().Cylinder(Vector3.Zero, 0.13f, 0.12f, 0.03f, 10, WaxRed).Transform(Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(ch + V(0.2f, -0.38f, 0.01f))));
        m.Box(ch + V(0.13f, -0.6f, 0.012f), V(0.05f, 0.3f, 0.006f), WaxRed);
        m.Box(ch + V(0.27f, -0.6f, 0.012f), V(0.05f, 0.26f, 0.006f), WaxRed);
        // the round window high on the back wall (its glass is drawn glowing)
        var oc = V(-0.65f, 4.6f, -5.36f);
        m.Append(new MeshBuilder().Ring(Vector3.Zero, 0.62f, 0.5f, 0.1f, 18, Palette.Mul(StoneCol, 1.1f)).Transform(Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(oc)));
        m.Box(oc + V(0, 0, 0.03f), V(1.0f, 0.05f, 0.04f), Palette.Bark);
        m.Box(oc + V(0, 0, 0.03f), V(0.05f, 1.0f, 0.04f), Palette.Bark);
        // a tall bookcase against the left wall beside the desk
        var bc = V(-5.1f, 0, -4.15f);
        m.Box(bc + V(0, 1.35f, 0), V(0.52f, 2.7f, 1.6f), Walnut);
        m.Box(bc + V(0.05f, 1.35f, 0), V(0.46f, 2.5f, 1.44f), Palette.Mul(Walnut, 0.55f));
        var spines = new[] { 0x7A3B34, 0x3D5A78, 0x5E7A4A, 0xA0763A, 0x6B4A7A, 0x8C6A4A, 0x45606A };
        var rng = new Random(12);
        for (int sh = 0; sh < 4; sh++)
        {
            float y = 0.25f + sh * 0.62f;
            m.Box(bc + V(0.05f, y - 0.03f, 0), V(0.48f, 0.05f, 1.46f), Walnut);
            float z = -0.66f;
            while (z < 0.62f)
            {
                float w = 0.06f + (float)rng.NextDouble() * 0.06f, h = 0.32f + (float)rng.NextDouble() * 0.18f;
                if (rng.NextDouble() < 0.12) { z += 0.15f; continue; }
                m.Box(bc + V(0.08f, y + h / 2, z + w / 2), V(0.34f, h, w), Palette.Hex((uint)spines[rng.Next(spines.Length)]));
                z += w + 0.01f;
            }
        }
        // a wall sconce between the bench and the cabinet
        m.Box(Sconce + V(0, -0.2f, -0.24f), V(0.16f, 0.36f, 0.04f), BrassDark);
        m.Box(Sconce + V(0, -0.33f, -0.12f), V(0.04f, 0.04f, 0.22f), Brass);
        Candlestick(m, Sconce, Sconce.Y - 0.35f, 0.09f);
        // the tall iron candle stand beside the Balance
        var st = new Vector3(StandCandle.X, 0, StandCandle.Z);
        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * MathF.Tau;
            Strut(m, st + V(0, 0.32f, 0), st + V(MathF.Cos(a) * 0.28f, 0.02f, MathF.Sin(a) * 0.28f), 0.04f, Iron);
        }
        m.Cylinder(st + V(0, 0.3f, 0), 0.03f, 0.025f, 1.2f, 6, Iron);
        m.Cylinder(st + V(0, 1.48f, 0), 0.1f, 0.13f, 0.04f, 8, Iron);
        Candlestick(m, StandCandle, 1.52f, 0.08f);
        return m;
    }

    private MeshBuilder BuildOculusGlass() =>
        new MeshBuilder().Disc(Vector3.Zero, 0.5f, 18, Palette.Lerp(Accent, Palette.LanternHot, 0.55f))
            .Transform(Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(V(-0.65f, 4.6f, -5.37f)));

    // ───────────────────────── the designation desk ─────────────────────────

    private MeshBuilder BuildDesk()
    {
        var m = new MeshBuilder();
        var p = DeskPos;
        var top = Palette.Mul(Walnut, 1.15f);
        m.Box(p + V(0, 0.98f, 0), V(2.8f, 0.1f, 1.25f), Walnut, top);
        foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            m.Box(p + V(sx * 1.27f, 0.465f, sz * 0.52f), V(0.13f, 0.93f, 0.13f), Palette.Mul(Walnut, 0.9f));
        m.Box(p + V(0, 0.66f, -0.52f), V(2.45f, 0.5f, 0.05f), Palette.Mul(Walnut, 0.82f));
        m.Box(p + V(0.78f, 0.76f, 0.02f), V(1.0f, 0.34f, 1.05f), Walnut);              // drawers under the right side
        m.Box(p + V(0.78f, 0.76f, 0.56f), V(0.86f, 0.24f, 0.03f), Palette.Mul(Walnut, 1.2f));
        m.Box(p + V(0.78f, 0.76f, 0.59f), V(0.14f, 0.04f, 0.04f), Brass);
        foreach (var c in DeskCandles) Candlestick(m, c, p.Y + 1.03f);
        // the inkwell and a quill
        var ink = p + V(0.74f, 1.03f, 0.3f);
        m.Cylinder(ink, 0.075f, 0.065f, 0.1f, 8, Palette.Hex(0x2F3A48), Palette.Ink);
        Strut(m, ink + V(0.0f, 0.05f, 0), ink + V(0.12f, 0.46f, -0.08f), 0.018f, Palette.Parchment);
        m.Append(new MeshBuilder().Blob(Vector3.Zero, V(0.04f, 0.15f, 0.012f), 3, 6, Palette.Hex(0xF2EADA), 31, 0.1f)
            .Transform(Matrix4x4.CreateRotationZ(-0.27f) * Matrix4x4.CreateTranslation(ink + V(0.09f, 0.36f, -0.06f))));
        // a stack of reference books and a loose page
        m.Box(p + V(-1.0f, 1.07f, 0.26f), V(0.5f, 0.08f, 0.36f), Palette.Hex(0x7A3B34));
        m.Box(p + V(-0.98f, 1.15f, 0.25f), V(0.46f, 0.08f, 0.33f), Palette.Hex(0x3D5A78));
        m.Box(p + V(-1.01f, 1.22f, 0.27f), V(0.42f, 0.06f, 0.3f), Palette.Hex(0x5E7A4A));
        RBox(m, p + V(0.62f, 1.035f, -0.2f), V(0.36f, 0.01f, 0.46f), Matrix4x4.CreateRotationY(0.35f), Palette.Parchment);
        // the chair, pulled out a little
        var ch = new MeshBuilder();
        var wood = Palette.Mul(Walnut, 1.05f);
        ch.Box(V(0, 0.55f, 0), V(0.66f, 0.07f, 0.62f), wood, Palette.Hex(0x8C3B37));
        foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }) ch.Box(V(sx * 0.28f, 0.27f, sz * 0.26f), V(0.07f, 0.54f, 0.07f), Palette.Mul(wood, 0.9f));
        ch.Box(V(-0.28f, 0.85f, 0.27f), V(0.07f, 0.6f, 0.07f), wood);
        ch.Box(V(0.28f, 0.85f, 0.27f), V(0.07f, 0.6f, 0.07f), wood);
        ch.Box(V(0, 1.1f, 0.27f), V(0.66f, 0.12f, 0.07f), wood);
        ch.Box(V(0, 0.86f, 0.27f), V(0.5f, 0.06f, 0.05f), wood);
        m.Append(ch.Transform(Matrix4x4.CreateRotationY(-0.55f) * Matrix4x4.CreateTranslation(p + V(1.75f, 0, 1.05f))));
        return m;
    }

    /// <summary>The open journal: a leather cover, two ruled pages and a ribbon.</summary>
    private MeshBuilder BuildJournal()
    {
        var m = new MeshBuilder();
        var j = JournalAt;
        m.Box(j + V(0, -0.012f, 0), V(1.2f, 0.03f, 0.82f), Palette.Hex(0x6B4A36));
        foreach (int side in new[] { -1, 1 })
        {
            var page = new MeshBuilder();
            page.Box(V(0, 0.025f, 0), V(0.56f, 0.05f, 0.76f), Palette.Hex(0xF2E8D2), Palette.Hex(0xF6EEDC));
            for (int i = 0; i < 7; i++)
            {
                float w = 0.4f - (i % 3) * 0.06f - (i == 6 ? 0.15f : 0);
                page.Box(V(-0.02f * side - (0.4f - w) / 2 * side, 0.052f, -0.28f + i * 0.085f), V(w, 0.004f, 0.012f), Palette.InkSoft);
            }
            m.Append(page.Transform(Matrix4x4.CreateRotationZ(-side * 0.07f) * Matrix4x4.CreateTranslation(j + V(side * 0.29f, 0, 0))));
        }
        m.Box(j + V(0.02f, 0.06f, 0.2f), V(0.03f, 0.01f, 0.6f), WaxRed);
        m.Box(j + V(0.02f, -0.06f, 0.43f), V(0.03f, 0.14f, 0.01f), WaxRed);
        return m;
    }

    /// <summary>A cork specimen board above the desk; tags are pinned to it per object.</summary>
    private MeshBuilder BuildBoard()
    {
        var m = new MeshBuilder();
        var c = V(DeskPos.X, 2.65f, -5.34f);
        m.Box(c, V(2.3f, 1.15f, 0.05f), Palette.Hex(0xB98A5A));
        m.Box(c + V(0, 0.6f, 0.02f), V(2.42f, 0.08f, 0.08f), Walnut);
        m.Box(c + V(0, -0.6f, 0.02f), V(2.42f, 0.08f, 0.08f), Walnut);
        m.Box(c + V(-1.18f, 0, 0.02f), V(0.08f, 1.26f, 0.08f), Walnut);
        m.Box(c + V(1.18f, 0, 0.02f), V(0.08f, 1.26f, 0.08f), Walnut);
        return m;
    }

    private static MeshBuilder BuildWax()
    {
        var m = new MeshBuilder();
        m.Blob(V(0, 0.02f, 0), V(0.14f, 0.03f, 0.14f), 3, 9, WaxRed, 77, 0.22f);
        m.Cylinder(V(0, 0.02f, 0), 0.09f, 0.085f, 0.035f, 10, Palette.Mul(WaxRed, 1.1f), Palette.Mul(WaxRed, 1.25f));
        m.Box(V(0, 0.058f, 0), V(0.05f, 0.008f, 0.05f), Palette.Mul(WaxRed, 1.45f));
        return m;
    }

    // ───────────────────────── the magnifying glass ─────────────────────────

    private MeshBuilder BuildGlassTable()
    {
        var m = new MeshBuilder();
        var p = GlassPos;
        m.Cylinder(p, 0.3f, 0.26f, 0.06f, 10, Palette.Mul(Walnut, 0.9f));
        m.Cylinder(p + V(0, 0.06f, 0), 0.06f, 0.05f, 0.76f, 8, Walnut);
        m.Cylinder(p + V(0, 0.8f, 0), 0.46f, 0.46f, 0.07f, 14, Walnut, Palette.Mul(Walnut, 1.15f));
        m.Cylinder(p + V(0, 0.87f, 0.08f), 0.17f, 0.17f, 0.01f, 12, Palette.Hex(0xE9DEC4)); // a felt pad for the specimen
        // the brass stand: a weighted foot, a post and an arm reaching over the pad
        var post = p + V(0, 0.87f, -0.32f);
        m.Cylinder(post, 0.11f, 0.09f, 0.05f, 10, BrassDark, Brass);
        m.Cylinder(post + V(0, 0.05f, 0), 0.025f, 0.022f, 0.72f, 6, Brass);
        m.Blob(post + V(0, 0.78f, 0), V(0.04f, 0.04f, 0.04f), 2, 6, Brass);
        Strut(m, post + V(0, 0.76f, 0), new Vector3(LensPivot.X, LensPivot.Y + 0.1f, LensPivot.Z), 0.03f, Brass);
        Strut(m, new Vector3(LensPivot.X, LensPivot.Y + 0.1f, LensPivot.Z), LensPivot, 0.025f, Brass);
        Candlestick(m, GlassCandle, p.Y + 0.87f, 0.09f);
        return m;
    }

    /// <summary>The lens hangs from <see cref="LensPivot"/>, tipped toward the room.</summary>
    private static readonly Matrix4x4 LensTilt = Matrix4x4.CreateRotationX(1.12f) * Matrix4x4.CreateTranslation(0, -0.31f, 0.03f);

    private static MeshBuilder BuildLensRing()
    {
        var m = new MeshBuilder();
        var ring = new MeshBuilder().Ring(Vector3.Zero, 0.27f, 0.225f, 0.06f, 18, Brass, Palette.Mul(Brass, 1.15f));
        ring.Box(V(0.36f, 0.03f, 0), V(0.2f, 0.05f, 0.05f), Palette.Hex(0x5E4232)); // a wooden handle
        m.Append(ring.Transform(LensTilt));
        var top = Vector3.Transform(V(0, 0.03f, -0.27f), LensTilt);
        Strut(m, Vector3.Zero, top, 0.022f, Brass);
        return m;
    }

    private static MeshBuilder BuildLensGlass()
    {
        var col = Palette.Hex(0xCFE6EE);
        var m = new MeshBuilder().Disc(V(0, 0.03f, 0), 0.23f, 18, col);
        m.Append(new MeshBuilder().Disc(Vector3.Zero, 0.23f, 18, col).Transform(Matrix4x4.CreateRotationX(MathF.PI) * Matrix4x4.CreateTranslation(0, 0.03f, 0)));
        return m.Transform(LensTilt);
    }

    // ───────────────────────── the assembly bench ─────────────────────────

    private MeshBuilder BuildBench()
    {
        var m = new MeshBuilder();
        var p = BenchPos;
        var wood = Palette.Hex(0xA27650);
        m.Box(p + V(0, 0.86f, 0), V(2.9f, 0.12f, 0.95f), wood, Palette.Hex(0xB48A62));
        foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            m.Box(p + V(sx * 1.33f, 0.4f, sz * 0.38f), V(0.14f, 0.8f, 0.14f), Palette.Mul(wood, 0.85f));
        m.Box(p + V(0, 0.22f, 0), V(2.7f, 0.06f, 0.8f), Palette.Mul(wood, 0.9f));
        m.Box(p + V(-0.75f, 0.42f, 0.02f), V(0.62f, 0.34f, 0.5f), Palette.Hex(0x8C6A4A));   // a crate of offcuts
        m.Box(p + V(0.55f, 0.37f, -0.05f), V(0.5f, 0.24f, 0.42f), Palette.Hex(0x7A5A43));
        m.Cylinder(p + V(1.0f, 0.25f, 0.05f), 0.16f, 0.15f, 0.3f, 8, Palette.Hex(0x9C7A5A)); // a little keg
        // a vice on the right end
        m.Box(p + V(1.25f, 1.0f, 0.36f), V(0.26f, 0.18f, 0.24f), Iron);
        m.Box(p + V(1.25f, 1.0f, 0.52f), V(0.26f, 0.16f, 0.06f), Palette.Mul(Iron, 1.2f));
        Strut(m, p + V(1.1f, 0.98f, 0.62f), p + V(1.4f, 0.98f, 0.62f), 0.025f, Palette.Mul(Iron, 1.4f));
        // tools: a hammer, a coil of twine, pliers, a few glass offcuts
        RBox(m, p + V(-0.35f, 0.95f, 0.22f), V(0.5f, 0.04f, 0.05f), Matrix4x4.CreateRotationY(0.3f), Palette.Hex(0x8C6A4A));
        RBox(m, p + V(-0.12f, 0.97f, 0.15f), V(0.07f, 0.08f, 0.18f), Matrix4x4.CreateRotationY(0.3f), Iron);
        m.Ring(p + V(0.32f, 0.92f, 0.12f), 0.13f, 0.07f, 0.06f, 10, Palette.Hex(0xC4A46A));
        RBox(m, p + V(0.62f, 0.935f, 0.28f), V(0.26f, 0.025f, 0.035f), Matrix4x4.CreateRotationY(-0.4f), Palette.Mul(Iron, 1.3f));
        RBox(m, p + V(0.62f, 0.935f, 0.28f), V(0.26f, 0.025f, 0.035f), Matrix4x4.CreateRotationY(-0.75f), Palette.Mul(Iron, 1.3f));
        RBox(m, p + V(0.05f, 0.94f, -0.22f), V(0.14f, 0.03f, 0.1f), Matrix4x4.CreateRotationY(0.6f), Palette.Hex(0xBFE0E6));
        RBox(m, p + V(0.22f, 0.94f, -0.26f), V(0.1f, 0.03f, 0.08f), Matrix4x4.CreateRotationY(-0.3f), Palette.Hex(0xBFE0E6));
        // the oil lamp
        var lamp = new Vector3(BenchLamp.X, p.Y + 0.92f, BenchLamp.Z);
        m.Cylinder(lamp, 0.12f, 0.1f, 0.06f, 10, BrassDark, Brass);
        m.Blob(lamp + V(0, 0.14f, 0), V(0.1f, 0.09f, 0.1f), 3, 8, Brass, 4, 0.02f);
        m.Cylinder(lamp + V(0, 0.22f, 0), 0.03f, 0.03f, 0.12f, 6, BrassDark);
        m.Cylinder(lamp + V(0, 0.32f, 0), 0.07f, 0.05f, 0.22f, 8, Palette.Hex(0xF3E3BC)); // the chimney (lit from within)
        // the pegboard and its tool silhouettes
        var pb = p + V(0, 1.85f, -0.5f);
        m.Box(pb, V(2.8f, 1.1f, 0.05f), Palette.Hex(0xC2A27A));
        m.Box(pb + V(0, 0.58f, 0.02f), V(2.9f, 0.07f, 0.08f), Palette.Mul(wood, 0.8f));
        m.Box(pb + V(-1.0f, 0.05f, 0.04f), V(0.08f, 0.7f, 0.03f), Palette.Hex(0x8C6A4A));   // a saw's handle and blade
        m.Box(pb + V(-0.85f, -0.05f, 0.035f), V(0.25f, 0.6f, 0.01f), Palette.Mul(Iron, 1.6f));
        m.Box(pb + V(-0.35f, 0.1f, 0.04f), V(0.05f, 0.75f, 0.02f), Palette.Hex(0xD9C39A));  // a rule
        m.Ring(pb + V(0.2f, 0.12f, 0.03f), 0.2f, 0.16f, 0.03f, 12, Palette.Hex(0xC4A46A));   // twine hung on a peg
        m.Box(pb + V(0.75f, 0.0f, 0.04f), V(0.4f, 0.04f, 0.03f), Palette.Mul(Iron, 1.3f));    // files
        m.Box(pb + V(0.75f, -0.12f, 0.04f), V(0.36f, 0.04f, 0.03f), Palette.Mul(Iron, 1.3f));
        m.Box(pb + V(1.15f, 0.05f, 0.04f), V(0.12f, 0.55f, 0.03f), Palette.Hex(0x8C6A4A));
        return m;
    }

    /// <summary>A pinned sketch of a tool (tinted blueprint-pale when drawn).</summary>
    private static MeshBuilder BuildSheet()
    {
        var m = new MeshBuilder();
        m.Box(Vector3.Zero, V(0.52f, 0.66f, 0.015f), Color.White);
        var ink = Palette.Hex(0x3A4E66);
        m.Ring(V(0, 0.04f, 0.009f), 0.14f, 0.12f, 0.004f, 12, ink, ink);
        m.Append(new MeshBuilder().Ring(Vector3.Zero, 0.14f, 0.125f, 0.004f, 12, ink).Transform(Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 0.04f, 0.01f)));
        m.Box(V(0, -0.2f, 0.01f), V(0.32f, 0.012f, 0.004f), ink);
        m.Box(V(-0.04f, -0.25f, 0.01f), V(0.24f, 0.012f, 0.004f), ink);
        m.Box(V(0, 0.29f, 0.014f), V(0.05f, 0.05f, 0.02f), Brass);
        return m;
    }

    // ───────────────────────── the cabinet ─────────────────────────

    private MeshBuilder BuildCabinet()
    {
        var m = new MeshBuilder();
        var p = CabinetPos;
        var dark = Palette.Mul(Cedar, 0.55f);
        m.Box(p + V(0, 1.6f, -0.36f), V(1.7f, 3.2f, 0.08f), dark);                          // back
        m.Box(p + V(-0.81f, 1.6f, 0), V(0.08f, 3.2f, 0.8f), Cedar);                        // sides
        m.Box(p + V(0.81f, 1.6f, 0), V(0.08f, 3.2f, 0.8f), Cedar);
        m.Box(p + V(0, 3.25f, 0.02f), V(1.9f, 0.12f, 0.9f), Palette.Mul(Cedar, 1.1f));       // crown
        m.Box(p + V(0, 3.34f, 0.0f), V(1.7f, 0.06f, 0.8f), Palette.Mul(Cedar, 0.95f));
        m.Box(p + V(0, 0.71f, 0.01f), V(1.62f, 1.42f, 0.78f), Cedar);                       // the drawer block
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 2; c++)
            {
                var d = p + V(-0.39f + c * 0.78f, 0.28f + r * 0.44f, 0.41f);
                m.Box(d, V(0.72f, 0.38f, 0.03f), Palette.Mul(Cedar, 1.18f));
                m.Box(d + V(0, 0, 0.03f), V(0.16f, 0.04f, 0.04f), Brass);
            }
        foreach (float y in new[] { 1.97f, 2.53f })
            m.Box(p + V(0, y, 0), V(1.62f, 0.05f, 0.72f), Palette.Mul(Cedar, 1.05f));
        m.Box(p + V(0, 1.44f, 0.38f), V(1.62f, 0.05f, 0.06f), Palette.Mul(Cedar, 1.25f));
        m.Box(p + V(0, 3.12f, 0.38f), V(1.62f, 0.1f, 0.06f), Palette.Mul(Cedar, 1.25f));
        return m;
    }

    // ───────────────────────── the Balance and its reliquary ─────────────────────────

    private static MeshBuilder BuildPlinth()
    {
        var m = new MeshBuilder();
        m.Box(V(0, 0.05f, 0), V(1.1f, 0.1f, 1.1f), Palette.Mul(StoneCol, 0.9f));
        m.Box(V(0, 0.6f, 0), V(0.95f, 1.0f, 0.95f), StoneCol);
        m.Box(V(0, 1.14f, 0), V(1.1f, 0.08f, 1.1f), Palette.Mul(StoneCol, 1.08f));
        m.Box(V(0, 0.56f, 0.47f), V(0.62f, 0.7f, 0.02f), Palette.Hex(0x2E2622));            // the reliquary's niche
        m.Box(V(0, 0.2f, 0.48f), V(0.66f, 0.04f, 0.02f), BrassDark);
        m.Box(V(0, 0.92f, 0.48f), V(0.66f, 0.04f, 0.02f), BrassDark);
        m.Box(V(0.48f, 0.6f, 0), V(0.02f, 0.06f, 0.96f), BrassDark);
        return m;
    }

    /// <summary>The reliquary door, hinged on its left edge (local x = 0).</summary>
    private static MeshBuilder BuildRelicDoor()
    {
        var m = new MeshBuilder();
        var face = Palette.Mul(StoneCol, 0.92f);
        m.Box(V(0.3f, 0, 0.0f), V(0.6f, 0.66f, 0.05f), face, face);
        m.Append(new MeshBuilder().Ring(Vector3.Zero, 0.17f, 0.13f, 0.02f, 12, BrassDark, Brass).Transform(Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(0.3f, 0.02f, 0.03f)));
        m.Box(V(0.3f, 0.02f, 0.04f), V(0.06f, 0.06f, 0.03f), Brass);
        m.Box(V(0.54f, 0, 0.04f), V(0.04f, 0.12f, 0.03f), Brass);
        return m;
    }

    private static MeshBuilder BuildBalanceStand()
    {
        var m = new MeshBuilder();
        m.Cylinder(V(0, 1.18f, 0), 0.24f, 0.18f, 0.08f, 10, BrassDark, Brass);
        m.Cylinder(V(0, 1.26f, 0), 0.05f, 0.035f, 1.22f, 8, Brass);
        m.Blob(V(0, 1.6f, 0), V(0.07f, 0.05f, 0.07f), 2, 8, Brass);
        m.Blob(V(0, 2.58f, 0), V(0.05f, 0.08f, 0.05f), 3, 6, Brass, 8, 0.02f);
        return m;
    }

    /// <summary>The beam, local to its pivot: arms along x, hooks at the ends, a needle pointing up.</summary>
    private static MeshBuilder BuildBeam()
    {
        var m = new MeshBuilder();
        m.Box(Vector3.Zero, V(BeamHalf * 2 + 0.06f, 0.055f, 0.06f), Brass);
        m.Box(V(0, 0.18f, 0), V(0.025f, 0.34f, 0.025f), BrassDark);
        m.Blob(Vector3.Zero, V(0.07f, 0.07f, 0.07f), 2, 8, Palette.Mul(Brass, 1.1f));
        foreach (int s in new[] { -1, 1 })
            m.Blob(V(s * BeamHalf, 0, 0), V(0.045f, 0.045f, 0.045f), 2, 6, Brass);
        return m;
    }

    /// <summary>A pan, local to the hook it hangs from.</summary>
    private static MeshBuilder BuildPan()
    {
        var m = new MeshBuilder();
        m.Box(V(0, -0.17f, 0), V(0.018f, 0.32f, 0.018f), BrassDark);
        for (int i = 0; i < 3; i++)
        {
            float a = i / 3f * MathF.Tau + 0.5f;
            Strut(m, V(0, -0.32f, 0), V(MathF.Cos(a) * 0.26f, -0.55f, MathF.Sin(a) * 0.26f), 0.014f, BrassDark);
        }
        m.Cylinder(V(0, -0.62f, 0), 0.2f, 0.29f, 0.07f, 12, Brass, Palette.Mul(Brass, 0.8f));
        return m;
    }

    // ───────────────────────── the Reconstitution Circle ─────────────────────────

    private static MeshBuilder BuildCircle()
    {
        var m = new MeshBuilder();
        float r = CircleR;
        m.Ring(Vector3.Zero, r, r - 0.07f, 0.008f, 64, Chalk);
        m.Ring(Vector3.Zero, r - 0.3f, r - 0.35f, 0.008f, 56, Chalk);
        m.Ring(Vector3.Zero, 0.36f, 0.31f, 0.008f, 24, Chalk);
        var pts = new Vector3[6];
        for (int k = 0; k < 6; k++)
        {
            float a = k / 6f * MathF.Tau + MathF.PI / 2;
            pts[k] = V(MathF.Cos(a) * (r - 0.38f), 0.009f, MathF.Sin(a) * (r - 0.38f));
        }
        for (int k = 0; k < 6; k++) FloorLine(m, pts[k], pts[(k + 1) % 6], 0.045f, Chalk); // a plain hexagon: floor pattern, not a symbol
        for (int k = 0; k < 6; k++) m.Ring(pts[k] with { Y = 0 }, 0.13f, 0.09f, 0.009f, 12, Chalk);
        // small marks between the rings, like someone counting
        for (int k = 0; k < 24; k++)
        {
            float a = k / 24f * MathF.Tau + 0.07f;
            var dir = V(MathF.Cos(a), 0, MathF.Sin(a));
            if (k % 3 == 0) m.Ring(dir * (r - 0.18f), 0.05f, 0.025f, 0.009f, 8, Chalk);
            else FloorLine(m, dir * (r - 0.25f) + V(0, 0.009f, 0), dir * (r - 0.12f) + V(0, 0.009f, 0), 0.03f, Chalk);
        }
        return m;
    }

    private static readonly Vector3[] CircleCandleTops = BuildCandleTops();

    private static Vector3[] BuildCandleTops()
    {
        var angles = new[] { -2.55f, -0.6f, 1.75f };
        return angles.Select(a => CirclePos + V(MathF.Cos(a) * (CircleR + 0.32f), 0.36f, MathF.Sin(a) * (CircleR + 0.32f))).ToArray();
    }

    private static MeshBuilder BuildCircleCandles()
    {
        var m = new MeshBuilder();
        foreach (var top in CircleCandleTops)
        {
            var b = top with { Y = 0 };
            m.Cylinder(b, 0.12f, 0.1f, 0.03f, 8, Palette.Mul(StoneCol, 0.9f));
            m.Cylinder(b + V(0, 0.03f, 0), 0.06f, 0.055f, top.Y - 0.08f, 8, CandleWax, Palette.Hex(0xF8F0DC));
            m.Box(V(top.X, top.Y - 0.03f, top.Z), V(0.008f, 0.04f, 0.008f), Palette.Ink);
        }
        return m;
    }
}
