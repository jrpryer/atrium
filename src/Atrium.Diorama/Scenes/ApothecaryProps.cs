using System.Numerics;
using System.Text.Json;
using Atrium.Core.Content;
using Atrium.Diorama.Render;
using Raylib_cs;

namespace Atrium.Diorama.Scenes;

/// <summary>
/// The Apothecary's procedural furniture and props: the wall of shelves and little drawers, the workbench, the
/// copper vessel, one prop per kind of tool, shelf jars (and their candle, crystal, pouch, crock, lens and flask
/// cousins) and the odd objects that get set on the bench. Everything is built from code with <see cref="MeshBuilder"/>.
/// Parts that take an item's colour are built white and tinted when drawn.
/// </summary>
internal static class ApothecaryProps
{
    // ───────────────────────── layout (world units) ─────────────────────────

    public const float ShelfLeft = -2.15f, ShelfRight = 4.55f, ShelfZ = -4.95f;
    public static readonly float[] ShelfRows = { 2.92f, 2.1f, 3.74f, 1.28f }; // fill order: eye level first
    public const float BenchTop = 1.02f, BenchZ = -2.3f, BenchLeft = -2.0f, BenchRight = 4.4f;
    public static readonly Vector3 VesselPos = new(1.25f, BenchTop, -2.5f);
    public static readonly Vector3 TrayPos = new(1.25f, BenchTop, -1.86f);
    public static readonly Vector3 MapCentre = new(-3.85f, 2.75f, -5.34f);
    public const float MapW = 2.3f, MapH = 1.62f;
    public static readonly Vector3 LecternPos = new(5.05f, 0, -0.85f);
    public static readonly Vector3 CratePos = new(-2.85f, 0, -1.35f);
    public static readonly Vector3 LanternPos = new(1.25f, 4.15f, -1.6f);
    public static readonly Vector3 SconcePos = new(-2.85f, 2.95f, -5.25f);

    // ───────────────────────── palette ─────────────────────────

    public static readonly Color WoodDark = Palette.Hex(0x5E4232);
    public static readonly Color WoodMid = Palette.Hex(0x7E5A42);
    public static readonly Color WoodLight = Palette.Hex(0xA47C58);
    public static readonly Color Copper = Palette.Hex(0xC07A4A);
    public static readonly Color CopperLight = Palette.Hex(0xD9965E);
    public static readonly Color Brass = Palette.Hex(0xC9A04A);
    public static readonly Color BrassDark = Palette.Hex(0x9C7A34);
    public static readonly Color Iron = Palette.Hex(0x4A4648);
    public static readonly Color StoneGrey = Palette.Hex(0x9C968E);
    public static readonly Color Clay = Palette.Hex(0xB86E4B);
    public static readonly Color Glass = Palette.Hex(0xE4F2EE);
    public static readonly Color Cork = Palette.Hex(0xA9825A);

    // ───────────────────────── the room's fixed furniture ─────────────────────────

    /// <summary>Shelving wall with a bank of drawers, the workbench, the map board's frame, the lectern, a crate of bottles, and odds and ends.</summary>
    public static MeshBuilder Furniture(Color accent)
    {
        var m = new MeshBuilder();
        float cx = (ShelfLeft + ShelfRight) / 2, w = ShelfRight - ShelfLeft;
        // shelving wall
        m.Box(new Vector3(cx, 2.5f, -5.33f), new Vector3(w + 0.3f, 5.0f, 0.1f), WoodDark);
        foreach (var x in new[] { ShelfLeft - 0.08f, ShelfRight + 0.08f })
            m.Box(new Vector3(x, 2.5f, ShelfZ), new Vector3(0.16f, 5.0f, 0.82f), WoodMid, Palette.Mul(WoodMid, 1.1f));
        // the bank of little drawers
        m.Box(new Vector3(cx, 0.6f, ShelfZ), new Vector3(w, 1.2f, 0.8f), WoodMid, WoodLight);
        int cols = 9;
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < cols; c++)
            {
                float dx = ShelfLeft + (c + 0.5f) * w / cols, dy = 0.3f + r * 0.52f;
                m.Box(new Vector3(dx, dy, ShelfZ + 0.41f), new Vector3(w / cols - 0.07f, 0.44f, 0.04f), Palette.Mul(WoodLight, 0.92f + ((r + c) % 2) * 0.06f));
                m.Box(new Vector3(dx, dy + 0.08f, ShelfZ + 0.44f), new Vector3(0.22f, 0.1f, 0.02f), Palette.Parchment);
                m.Box(new Vector3(dx, dy - 0.08f, ShelfZ + 0.45f), new Vector3(0.07f, 0.05f, 0.05f), Brass);
            }
        // shelf boards (the lowest row stands on the drawers) and their brass rails
        foreach (var y in ShelfRows)
        {
            m.Box(new Vector3(cx, y - 0.035f, ShelfZ), new Vector3(w, 0.07f, 0.82f), WoodLight, Palette.Mul(WoodLight, 1.08f));
            m.Box(new Vector3(cx, y + 0.13f, ShelfZ + 0.39f), new Vector3(w, 0.025f, 0.025f), Brass);
        }
        // crown, in the shop's colour
        m.Box(new Vector3(cx, 4.86f, ShelfZ + 0.02f), new Vector3(w + 0.5f, 0.2f, 0.95f), Palette.Lerp(WoodDark, accent, 0.55f));
        m.Box(new Vector3(cx, 4.99f, ShelfZ + 0.04f), new Vector3(w + 0.66f, 0.08f, 1.02f), Palette.Lerp(WoodMid, accent, 0.35f));
        for (int i = 0; i < 9; i++)
            m.Box(new Vector3(ShelfLeft + 0.2f + i * (w - 0.4f) / 8, 4.72f, ShelfZ + 0.46f), new Vector3(0.05f, 0.1f, 0.03f), Brass);

        // the workbench
        float bx = (BenchLeft + BenchRight) / 2, bw = BenchRight - BenchLeft;
        m.Box(new Vector3(bx, BenchTop - 0.07f, BenchZ), new Vector3(bw, 0.14f, 1.4f), WoodLight, Palette.Hex(0xB58C64));
        m.Box(new Vector3(bx, BenchTop - 0.25f, BenchZ), new Vector3(bw - 0.2f, 0.22f, 1.28f), WoodMid);
        foreach (var x in new[] { BenchLeft + 0.14f, BenchRight - 0.14f })
            foreach (var z in new[] { BenchZ - 0.55f, BenchZ + 0.55f })
                m.Box(new Vector3(x, (BenchTop - 0.14f) / 2, z), new Vector3(0.16f, BenchTop - 0.14f, 0.16f), WoodMid);
        m.Box(new Vector3(bx, 0.24f, BenchZ), new Vector3(bw - 0.3f, 0.06f, 1.16f), WoodMid);
        // under the bench: sacks, a crate, a stoneware jug
        m.Blob(new Vector3(BenchLeft + 0.7f, 0.52f, BenchZ - 0.1f), new Vector3(0.36f, 0.26f, 0.32f), 3, 7, Palette.Hex(0xC9B48C), 11, 0.12f);
        m.Blob(new Vector3(BenchLeft + 1.4f, 0.48f, BenchZ + 0.05f), new Vector3(0.3f, 0.22f, 0.28f), 3, 7, Palette.Hex(0xBFA67C), 12, 0.12f);
        m.Box(new Vector3(BenchRight - 1.2f, 0.5f, BenchZ), new Vector3(0.6f, 0.46f, 0.6f), Palette.Hex(0x9A7454));
        m.Cylinder(new Vector3(BenchRight - 0.5f, 0.27f, BenchZ - 0.05f), 0.2f, 0.16f, 0.42f, 9, Palette.Hex(0xB8A27E), Palette.Hex(0xA08A68));
        m.Cylinder(new Vector3(bx + 0.6f, 0.27f, BenchZ + 0.1f), 0.16f, 0.13f, 0.32f, 9, Clay);
        // a brass tray for odd objects
        m.Cylinder(TrayPos, 0.3f, 0.32f, 0.04f, 14, BrassDark, Brass);

        // the Discovery Map's board and frame on the back wall
        m.Box(MapCentre - new Vector3(0, 0, 0.03f), new Vector3(MapW + 0.24f, MapH + 0.24f, 0.05f), WoodDark);

        // the lectern for the old formulary
        m.Cylinder(LecternPos, 0.32f, 0.28f, 0.08f, 8, WoodDark);
        m.Cylinder(LecternPos + new Vector3(0, 0.08f, 0), 0.08f, 0.07f, 0.95f, 6, WoodMid);
        var top = new MeshBuilder().Box(new Vector3(0, 0, 0), new Vector3(0.8f, 0.06f, 0.58f), WoodMid, WoodLight)
            .Box(new Vector3(0, 0.05f, 0.27f), new Vector3(0.8f, 0.05f, 0.04f), WoodDark);
        m.Append(top.Transform(Matrix4x4.CreateRotationX(0.42f) * Matrix4x4.CreateRotationY(-0.55f) * Matrix4x4.CreateTranslation(LecternPos + new Vector3(0, 1.06f, 0))));

        // a crate of empty bottles by the window
        m.Box(CratePos + new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.5f, 0.65f), Palette.Hex(0xA98058), Palette.Hex(0xB98E62));
        m.Box(CratePos + new Vector3(0, 0.36f, 0.33f), new Vector3(0.92f, 0.08f, 0.02f), WoodMid);
        for (int i = 0; i < 6; i++)
        {
            var p = CratePos + new Vector3(-0.3f + (i % 3) * 0.3f, 0.5f, -0.14f + (i / 3) * 0.28f);
            m.Cylinder(p, 0.1f, 0.08f, 0.2f, 7, Palette.Hex(0xBFD8CF));
            m.Cylinder(p + new Vector3(0, 0.2f, 0), 0.04f, 0.035f, 0.1f, 6, Palette.Hex(0xBFD8CF));
            m.Cylinder(p + new Vector3(0, 0.3f, 0), 0.045f, 0.045f, 0.05f, 6, Cork);
        }

        // a stool, and a potted herb under the window
        m.Cylinder(new Vector3(-3.8f, 0, 0.6f), 0.06f, 0.05f, 0.62f, 5, WoodMid, null, 0.12f);
        m.Cylinder(new Vector3(-3.5f, 0, 0.35f), 0.06f, 0.05f, 0.62f, 5, WoodMid, null, -0.1f);
        m.Cylinder(new Vector3(-3.95f, 0, 0.25f), 0.06f, 0.05f, 0.62f, 5, WoodMid, null, 0.08f);
        m.Cylinder(new Vector3(-3.7f, 0.62f, 0.4f), 0.36f, 0.36f, 0.09f, 10, WoodLight);
        m.Cylinder(new Vector3(-4.8f, 0, -0.1f), 0.28f, 0.34f, 0.5f, 9, Clay, Palette.Hex(0x5C4433));
        m.Blob(new Vector3(-4.8f, 0.78f, -0.1f), new Vector3(0.4f, 0.36f, 0.4f), 3, 8, Palette.Leaf, 31, 0.2f, Palette.LeafDeep);
        // the sconce bracket by the map
        m.Box(SconcePos + new Vector3(0, -0.16f, 0.02f), new Vector3(0.14f, 0.05f, 0.24f), Brass);
        m.Cylinder(SconcePos + new Vector3(0, -0.14f, 0.06f), 0.05f, 0.045f, 0.16f, 6, Palette.Hex(0xF3EAD2));
        return m;
    }

    /// <summary>Bunches of herbs drying on a beam (drawn with a gentle sway).</summary>
    public static MeshBuilder Herbs()
    {
        var m = new MeshBuilder();
        float y = 5.15f;
        m.Box(new Vector3(-0.9f, y, -3.7f), new Vector3(8.6f, 0.18f, 0.2f), WoodDark);
        var greens = new[] { Palette.Hex(0x7FA36B), Palette.Hex(0x9AAE6A), Palette.Hex(0x8C7AA8), Palette.Hex(0xB89A5A), Palette.Hex(0x6E9F5E) };
        for (int i = 0; i < 9; i++)
        {
            float x = -4.6f + i * 0.92f;
            float len = 0.35f + (i * 37 % 5) * 0.06f;
            m.Box(new Vector3(x, y - 0.18f, -3.7f), new Vector3(0.015f, 0.26f, 0.015f), Palette.Hex(0xC8B898));
            m.Cone(new Vector3(x, y - 0.3f - len, -3.7f), 0.13f, len, 6, greens[i % greens.Length]);
            m.Box(new Vector3(x, y - 0.32f, -3.7f), new Vector3(0.08f, 0.04f, 0.08f), Palette.Hex(0xB59C6A));
        }
        return m;
    }

    /// <summary>A hanging brass lantern (glass drawn separately as a glow).</summary>
    public static MeshBuilder Lantern()
    {
        var m = new MeshBuilder();
        m.Box(new Vector3(0, 0.9f, 0), new Vector3(0.025f, 1.5f, 0.025f), Iron);
        m.Cylinder(new Vector3(0, -0.22f, 0), 0.17f, 0.17f, 0.05f, 6, Brass);
        m.Cone(new Vector3(0, 0.12f, 0), 0.2f, 0.2f, 6, BrassDark);
        for (int i = 0; i < 6; i++)
        {
            float a = i * MathF.Tau / 6;
            m.Box(new Vector3(MathF.Cos(a) * 0.15f, -0.05f, MathF.Sin(a) * 0.15f), new Vector3(0.025f, 0.32f, 0.025f), Brass);
        }
        m.Cylinder(new Vector3(0, -0.18f, 0), 0.11f, 0.11f, 0.28f, 6, Palette.Hex(0xFFE6A8));
        return m;
    }

    // ───────────────────────── the vessel ─────────────────────────

    /// <summary>A copper pot on a brass trivet with a spirit lamp beneath. The liquid is separate.</summary>
    public static MeshBuilder Vessel()
    {
        var m = new MeshBuilder();
        for (int i = 0; i < 3; i++)
        {
            float a = i * MathF.Tau / 3 + 0.5f;
            m.Cylinder(new Vector3(MathF.Cos(a) * 0.38f, 0, MathF.Sin(a) * 0.38f), 0.03f, 0.025f, 0.22f, 5, Iron);
        }
        m.Ring(new Vector3(0, 0.2f, 0), 0.44f, 0.38f, 0.04f, 12, Iron);
        m.Cylinder(new Vector3(0, 0, 0), 0.1f, 0.08f, 0.1f, 8, Palette.Hex(0xBFD8CF), Brass);
        m.Cylinder(new Vector3(0, 0.22f, 0), 0.32f, 0.5f, 0.22f, 12, Copper);
        m.Cylinder(new Vector3(0, 0.44f, 0), 0.5f, 0.52f, 0.18f, 12, Copper);
        m.Cylinder(new Vector3(0, 0.62f, 0), 0.52f, 0.46f, 0.12f, 12, Palette.Mul(Copper, 1.05f));
        m.Ring(new Vector3(0, 0.74f, 0), 0.5f, 0.41f, 0.06f, 12, CopperLight, CopperLight);
        // a band of rivets and two handles
        for (int i = 0; i < 12; i++)
        {
            float a = i * MathF.Tau / 12 + 0.26f;
            m.Box(new Vector3(MathF.Cos(a) * 0.52f, 0.55f, MathF.Sin(a) * 0.52f), new Vector3(0.04f, 0.04f, 0.04f), Palette.Mul(Copper, 0.8f));
        }
        foreach (var s in new[] { -1f, 1f })
        {
            m.Box(new Vector3(s * 0.56f, 0.66f, 0), new Vector3(0.06f, 0.05f, 0.22f), CopperLight);
            m.Box(new Vector3(s * 0.6f, 0.6f, 0), new Vector3(0.04f, 0.14f, 0.22f), CopperLight);
        }
        // a stirring rod leaning on the rim
        m.Cylinder(new Vector3(0.2f, 0.45f, -0.18f), 0.025f, 0.02f, 0.8f, 5, WoodLight, null, 0.32f);
        return m;
    }

    /// <summary>The liquid's surface: a disc in a soft spiral of two tones (so a swirl reads when it turns).</summary>
    public static MeshBuilder Liquid()
    {
        var m = new MeshBuilder();
        int sides = 18, rings = 3;
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < sides; s++)
            {
                float r0 = 0.42f * r / rings, r1 = 0.42f * (r + 1) / rings;
                float tw = r * 0.7f;
                float a0 = MathF.Tau * s / sides, a1 = MathF.Tau * (s + 1) / sides;
                Vector3 P(float rr, float a, float t) => new(MathF.Cos(a + t) * rr, 0, MathF.Sin(a + t) * rr);
                var col = (s + r) % 3 == 0 ? Palette.Hex(0xDADADA) : Color.White;
                if (r == 0) m.Tri(Vector3.Zero, P(r1, a1, tw + 0.7f), P(r1, a0, tw + 0.7f), col);
                else m.Quad(P(r0, a0, tw), P(r0, a1, tw), P(r1, a1, tw + 0.7f), P(r1, a0, tw + 0.7f), col);
            }
        return m;
    }

    // ───────────────────────── tools ─────────────────────────

    public enum ToolLook { Mortar, Burner, Scales, Loupe, Still, Generic }

    /// <summary>Chooses a prop for a tool from its verb (then its name); anything unrecognised gets a generic instrument.</summary>
    public static ToolLook LookFor(ToolResource t)
    {
        static bool Has(string s, params string[] words) => words.Any(w => s.Contains(w, StringComparison.OrdinalIgnoreCase));
        foreach (var s in new[] { t.Verb, t.Name })
        {
            if (string.IsNullOrEmpty(s)) continue;
            if (Has(s, "grind", "crush", "pound", "mortar", "pestle")) return ToolLook.Mortar;
            if (Has(s, "warm", "heat", "burn", "boil", "roast", "flame")) return ToolLook.Burner;
            if (Has(s, "weigh", "scale", "balance")) return ToolLook.Scales;
            if (Has(s, "distil", "still")) return ToolLook.Still;
            if (Has(s, "peer", "look", "examine", "inspect", "magnif", "loupe", "glass")) return ToolLook.Loupe;
        }
        return ToolLook.Generic;
    }

    public sealed class ToolModels
    {
        public required Model Body;
        public Model? Part;   // pestle, flame, beam
        public Model? Part2;  // pan
        public Model? Glass;  // loupe lens
    }

    public static ToolModels BuildTool(ToolLook look, Shader toon)
    {
        switch (look)
        {
            case ToolLook.Mortar:
            {
                var b = new MeshBuilder();
                b.Cylinder(Vector3.Zero, 0.2f, 0.28f, 0.2f, 10, StoneGrey);
                b.Ring(new Vector3(0, 0.2f, 0), 0.28f, 0.2f, 0.04f, 10, Palette.Mul(StoneGrey, 1.1f), Palette.Mul(StoneGrey, 1.15f));
                b.Disc(new Vector3(0, 0.17f, 0), 0.21f, 10, Palette.Hex(0x5E5A55));
                var p = new MeshBuilder().Cylinder(Vector3.Zero, 0.04f, 0.065f, 0.44f, 7, Palette.Hex(0xBDB6AA));
                p.Blob(new Vector3(0, 0.44f, 0), new Vector3(0.07f, 0.05f, 0.07f), 2, 7, Palette.Hex(0xBDB6AA), 3, 0.02f);
                return new ToolModels { Body = b.Model(toon), Part = p.Model(toon) };
            }
            case ToolLook.Burner:
            {
                var b = new MeshBuilder();
                b.Cylinder(Vector3.Zero, 0.18f, 0.15f, 0.05f, 10, Brass, Palette.Mul(Brass, 1.1f));
                b.Cylinder(new Vector3(0, 0.05f, 0), 0.045f, 0.045f, 0.3f, 8, Brass);
                b.Cylinder(new Vector3(0, 0.14f, 0), 0.065f, 0.065f, 0.05f, 8, BrassDark);
                b.Box(new Vector3(0.12f, 0.06f, 0), new Vector3(0.12f, 0.03f, 0.03f), BrassDark);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * MathF.Tau / 3 + 0.3f;
                    b.Cylinder(new Vector3(MathF.Cos(a) * 0.24f, 0, MathF.Sin(a) * 0.24f), 0.015f, 0.015f, 0.56f, 4, Iron, null, -MathF.Cos(a) * 0.05f);
                }
                b.Ring(new Vector3(0, 0.55f, 0), 0.22f, 0.18f, 0.025f, 10, Iron);
                var f = new MeshBuilder().Cone(Vector3.Zero, 0.05f, 0.17f, 6, Color.White);
                return new ToolModels { Body = b.Model(toon), Part = f.Model(toon) };
            }
            case ToolLook.Scales:
            {
                var b = new MeshBuilder();
                b.Box(new Vector3(0, 0.04f, 0), new Vector3(0.7f, 0.08f, 0.26f), WoodDark, WoodMid);
                b.Cylinder(new Vector3(0, 0.08f, 0), 0.03f, 0.022f, 0.58f, 7, Brass);
                b.Blob(new Vector3(0, 0.7f, 0), new Vector3(0.04f, 0.05f, 0.04f), 2, 6, Brass, 2, 0.01f);
                for (int i = 0; i < 4; i++)
                    b.Cylinder(new Vector3(-0.2f + i * 0.08f, 0.08f, 0.08f), 0.025f + i * 0.004f, 0.025f + i * 0.004f, 0.03f + i * 0.012f, 7, Brass);
                var beam = new MeshBuilder().Box(Vector3.Zero, new Vector3(0.72f, 0.03f, 0.03f), Brass).Box(new Vector3(0, 0.03f, 0), new Vector3(0.04f, 0.08f, 0.04f), BrassDark);
                var pan = new MeshBuilder().Cylinder(Vector3.Zero, 0.1f, 0.13f, 0.03f, 10, Brass, Palette.Mul(Brass, 1.08f));
                for (int i = 0; i < 3; i++)
                {
                    float a = i * MathF.Tau / 3;
                    var foot = new Vector3(MathF.Cos(a) * 0.1f, 0.03f, MathF.Sin(a) * 0.1f);
                    var dir = new Vector3(0, 0.3f, 0) - foot;
                    pan.Append(new MeshBuilder().Box(new Vector3(0, dir.Length() / 2, 0), new Vector3(0.008f, dir.Length(), 0.008f), Iron)
                        .Transform(Align(dir) * Matrix4x4.CreateTranslation(foot)));
                }
                return new ToolModels { Body = b.Model(toon), Part = beam.Model(toon), Part2 = pan.Model(toon) };
            }
            case ToolLook.Loupe:
            {
                var b = new MeshBuilder();
                b.Cylinder(Vector3.Zero, 0.14f, 0.12f, 0.05f, 10, WoodDark, WoodMid);
                b.Cylinder(new Vector3(0, 0.05f, 0), 0.02f, 0.018f, 0.3f, 6, Brass, null, 0.06f);
                var ring = new MeshBuilder().Ring(Vector3.Zero, 0.15f, 0.12f, 0.035f, 14, Brass, Palette.Mul(Brass, 1.1f))
                    .Transform(Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(0.06f, 0.48f, 0));
                b.Append(ring);
                b.Box(new Vector3(0.06f, 0.36f, 0), new Vector3(0.03f, 0.04f, 0.03f), Brass);
                var lens = new MeshBuilder().Cylinder(new Vector3(0, -0.01f, 0), 0.125f, 0.125f, 0.02f, 14, Palette.Hex(0xCFE8EC))
                    .Transform(Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(0.06f, 0.48f, 0.015f));
                return new ToolModels { Body = b.Model(toon), Glass = lens.Model(toon) };
            }
            case ToolLook.Still:
            {
                var b = new MeshBuilder();
                b.Cylinder(Vector3.Zero, 0.2f, 0.16f, 0.26f, 8, Clay, Palette.Mul(Clay, 1.1f));
                b.Blob(new Vector3(-0.04f, 0.42f, 0), new Vector3(0.16f, 0.16f, 0.16f), 3, 8, Palette.Hex(0xBFE3DA), 5, 0.02f);
                b.Cylinder(new Vector3(-0.04f, 0.55f, 0), 0.04f, 0.03f, 0.16f, 6, Palette.Hex(0xBFE3DA), null, 0.18f);
                for (int i = 0; i < 4; i++)
                    b.Ring(new Vector3(0.26f, 0.12f + i * 0.11f, 0), 0.1f, 0.075f, 0.03f, 8, Palette.Hex(0x9CC9B3));
                b.Cylinder(new Vector3(0.26f, 0, 0), 0.03f, 0.03f, 0.56f, 5, Palette.Hex(0x9CC9B3));
                return new ToolModels { Body = b.Model(toon) };
            }
            default:
            {
                var b = new MeshBuilder();
                b.Box(new Vector3(0, 0.12f, 0), new Vector3(0.38f, 0.24f, 0.28f), Brass, Palette.Mul(Brass, 1.1f));
                b.Cylinder(new Vector3(0, 0.24f, 0), 0.1f, 0.1f, 0.03f, 10, Palette.Parchment);
                b.Box(new Vector3(0.03f, 0.275f, 0), new Vector3(0.08f, 0.01f, 0.012f), Palette.Ink);
                b.Box(new Vector3(0.21f, 0.12f, 0), new Vector3(0.04f, 0.06f, 0.06f), BrassDark);
                return new ToolModels { Body = b.Model(toon) };
            }
        }
    }

    /// <summary>A rotation taking +Y onto <paramref name="dir"/>.</summary>
    public static Matrix4x4 Align(Vector3 dir)
    {
        var d = Vector3.Normalize(dir);
        var axis = Vector3.Cross(Vector3.UnitY, d);
        float ang = MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.UnitY, d), -1, 1));
        return axis.LengthSquared() < 1e-6f ? Matrix4x4.Identity : Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(axis), ang);
    }

    // ───────────────────────── shelf jars and their cousins ─────────────────────────

    /// <summary>One way of keeping an element on a shelf: a tinted part (the stuff), a plain part (cork, dish, stand) and optional glass.</summary>
    public sealed class Keeper
    {
        public required Model Tinted;
        public Model? Plain;
        public Model? Glass;
        public float Height;
        public float Emissive = 0.12f;
        public Vector3? Flame;
        public Vector3? Glow;
    }

    public static Dictionary<string, Keeper> BuildKeepers(Shader toon)
    {
        var d = new Dictionary<string, Keeper>();
        // jar: contents, cork with a paper label, glass
        {
            var t = new MeshBuilder().Cylinder(new Vector3(0, 0.02f, 0), 0.15f, 0.15f, 0.25f, 8, Color.White, Palette.Hex(0xEEEEEE));
            var p = new MeshBuilder().Cylinder(new Vector3(0, 0.44f, 0), 0.1f, 0.11f, 0.09f, 8, Cork, Palette.Mul(Cork, 1.1f))
                .Box(new Vector3(0, 0.2f, 0.175f), new Vector3(0.18f, 0.12f, 0.012f), Palette.Parchment);
            var g = new MeshBuilder().Cylinder(Vector3.Zero, 0.18f, 0.18f, 0.34f, 8, Color.White)
                .Cylinder(new Vector3(0, 0.34f, 0), 0.18f, 0.12f, 0.06f, 8, Color.White)
                .Cylinder(new Vector3(0, 0.4f, 0), 0.1f, 0.1f, 0.05f, 8, Color.White);
            d["jar"] = new Keeper { Tinted = t.Model(toon), Plain = p.Model(toon), Glass = g.Model(toon), Height = 0.53f };
        }
        // candle on a brass dish
        {
            var t = new MeshBuilder().Cylinder(new Vector3(0, 0.04f, 0), 0.09f, 0.085f, 0.36f, 8, Color.White);
            for (int i = 0; i < 4; i++) t.Box(new Vector3(0, 0.1f + i * 0.08f, 0.088f), new Vector3(0.1f, 0.008f, 0.01f), Palette.Hex(0xC8B898));
            var p = new MeshBuilder().Cylinder(Vector3.Zero, 0.17f, 0.15f, 0.04f, 10, Brass, Palette.Mul(Brass, 1.1f))
                .Box(new Vector3(0, 0.42f, 0), new Vector3(0.012f, 0.05f, 0.012f), Palette.Ink)
                .Box(new Vector3(0.17f, 0.04f, 0), new Vector3(0.08f, 0.02f, 0.04f), Brass);
            d["candle"] = new Keeper { Tinted = t.Model(toon), Plain = p.Model(toon), Height = 0.48f, Flame = new Vector3(0, 0.49f, 0), Emissive = 0.08f };
        }
        // crystals under a bell jar
        {
            var t = new MeshBuilder();
            t.Cone(new Vector3(0, 0.05f, 0), 0.07f, 0.28f, 5, Color.White);
            t.Append(new MeshBuilder().Cone(Vector3.Zero, 0.05f, 0.2f, 5, Color.White).Transform(Matrix4x4.CreateRotationZ(0.45f) * Matrix4x4.CreateTranslation(0.06f, 0.05f, 0.02f)));
            t.Append(new MeshBuilder().Cone(Vector3.Zero, 0.045f, 0.17f, 5, Color.White).Transform(Matrix4x4.CreateRotationZ(-0.5f) * Matrix4x4.CreateRotationY(0.8f) * Matrix4x4.CreateTranslation(-0.05f, 0.05f, -0.02f)));
            var p = new MeshBuilder().Cylinder(Vector3.Zero, 0.18f, 0.17f, 0.05f, 10, WoodDark, WoodMid);
            var g = new MeshBuilder().Cylinder(new Vector3(0, 0.05f, 0), 0.15f, 0.15f, 0.26f, 9, Color.White)
                .Blob(new Vector3(0, 0.31f, 0), new Vector3(0.15f, 0.1f, 0.15f), 3, 9, Color.White, 1, 0f);
            d["crystal"] = new Keeper { Tinted = t.Model(toon), Plain = p.Model(toon), Glass = g.Model(toon), Height = 0.46f, Emissive = 0.3f, Glow = new Vector3(0, 0.18f, 0) };
        }
        // a cloth pouch tied with twine
        {
            var t = new MeshBuilder().Blob(new Vector3(0, 0.14f, 0), new Vector3(0.17f, 0.15f, 0.16f), 3, 8, Color.White, 7, 0.08f)
                .Cone(new Vector3(0, 0.25f, 0), 0.08f, 0.12f, 6, Color.White);
            var p = new MeshBuilder().Cylinder(new Vector3(0, 0.27f, 0), 0.06f, 0.06f, 0.035f, 6, Palette.Hex(0x8C6A44));
            d["pouch"] = new Keeper { Tinted = t.Model(toon), Plain = p.Model(toon), Height = 0.38f, Emissive = 0.05f };
        }
        // a crock with a heap of something in it
        {
            var t = new MeshBuilder().Blob(new Vector3(0, 0.2f, 0), new Vector3(0.15f, 0.08f, 0.15f), 2, 8, Color.White, 9, 0.15f);
            var p = new MeshBuilder().Cylinder(Vector3.Zero, 0.15f, 0.19f, 0.22f, 9, Clay, Palette.Mul(Clay, 0.7f))
                .Ring(new Vector3(0, 0.2f, 0), 0.2f, 0.16f, 0.03f, 9, Palette.Mul(Clay, 1.12f));
            d["food"] = new Keeper { Tinted = t.Model(toon), Plain = p.Model(toon), Height = 0.32f, Emissive = 0.05f };
        }
        // a lens in a little wooden stand
        {
            var t = new MeshBuilder().Cylinder(new Vector3(0, -0.012f, 0), 0.15f, 0.15f, 0.025f, 12, Color.White)
                .Transform(Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 0.24f, 0));
            var p = new MeshBuilder().Box(new Vector3(0, 0.03f, 0), new Vector3(0.26f, 0.06f, 0.12f), WoodDark, WoodMid)
                .Append(new MeshBuilder().Ring(Vector3.Zero, 0.17f, 0.145f, 0.04f, 12, Brass).Transform(Matrix4x4.CreateRotationX(MathF.PI / 2) * Matrix4x4.CreateTranslation(0, 0.24f, 0.02f)));
            d["lens"] = new Keeper { Tinted = t.Model(toon), Plain = p.Model(toon), Height = 0.42f, Emissive = 0.25f };
        }
        // a round flask
        {
            var t = new MeshBuilder().Blob(new Vector3(0, 0.13f, 0), new Vector3(0.13f, 0.1f, 0.13f), 3, 8, Color.White, 2, 0f);
            var p = new MeshBuilder().Cylinder(new Vector3(0, 0.4f, 0), 0.05f, 0.055f, 0.07f, 7, Cork);
            var g = new MeshBuilder().Blob(new Vector3(0, 0.16f, 0), new Vector3(0.17f, 0.16f, 0.17f), 4, 9, Color.White, 2, 0f)
                .Cylinder(new Vector3(0, 0.28f, 0), 0.05f, 0.045f, 0.13f, 7, Color.White);
            d["flask"] = new Keeper { Tinted = t.Model(toon), Plain = p.Model(toon), Glass = g.Model(toon), Height = 0.47f };
        }
        return d;
    }

    /// <summary>Which keeper suits an item's drawn shape (from the front end's visuals data) or, failing that, its kind.</summary>
    public static string KeeperFor(string? shape, ItemKind kind) => shape switch
    {
        "candle" => "candle",
        "crystal" or "stone" => "crystal",
        "pouch" or "bag" => "pouch",
        "food" => "food",
        "lens" => "lens",
        "flask" or "still" => "flask",
        "jar" => "jar",
        _ => kind switch
        {
            ItemKind.Mineral or ItemKind.Aura => "crystal",
            ItemKind.Food => "food",
            _ => "jar",
        },
    };

    // ───────────────────────── odd objects on the tray ─────────────────────────

    /// <summary>A small stand-in for a carried object, by its drawn shape. Built white; tinted by the item's colour.</summary>
    public static MeshBuilder ObjectMesh(string? shape)
    {
        var m = new MeshBuilder();
        switch (shape)
        {
            case "leaf":
                m.Blob(new Vector3(0, 0.03f, 0), new Vector3(0.24f, 0.025f, 0.13f), 3, 10, Color.White, 3, 0.04f);
                m.Box(new Vector3(0.27f, 0.03f, 0), new Vector3(0.1f, 0.015f, 0.015f), Palette.Hex(0xBBBBBB));
                break;
            case "nugget":
                m.Blob(new Vector3(0, 0.09f, 0), new Vector3(0.13f, 0.09f, 0.11f), 3, 7, Color.White, 5, 0.3f);
                m.Blob(new Vector3(0.08f, 0.07f, 0.04f), new Vector3(0.07f, 0.06f, 0.07f), 2, 6, Color.White, 6, 0.2f);
                break;
            case "flakes":
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 2.4f, r = 0.04f + (i % 4) * 0.035f;
                    m.Box(new Vector3(MathF.Cos(a) * r, 0.02f + (i % 3) * 0.012f, MathF.Sin(a) * r), new Vector3(0.05f, 0.012f, 0.04f), Color.White);
                }
                break;
            case "slag":
            case "husk":
                m.Blob(new Vector3(0, 0.07f, 0), new Vector3(0.14f, 0.07f, 0.11f), 3, 7, Color.White, 8, 0.35f);
                break;
            case "pearl":
                m.Blob(new Vector3(0, 0.11f, 0), new Vector3(0.11f, 0.11f, 0.11f), 5, 10, Color.White, 1, 0f);
                break;
            case "bag":
            case "pouch":
                m.Blob(new Vector3(0, 0.13f, 0), new Vector3(0.15f, 0.13f, 0.14f), 3, 8, Color.White, 7, 0.1f);
                m.Cone(new Vector3(0, 0.23f, 0), 0.07f, 0.12f, 6, Color.White);
                m.Cylinder(new Vector3(0, 0.25f, 0), 0.055f, 0.055f, 0.03f, 6, Palette.Hex(0x8C6A44));
                break;
            case "flask":
                m.Blob(new Vector3(0, 0.12f, 0), new Vector3(0.13f, 0.12f, 0.13f), 3, 8, Color.White, 2, 0f);
                m.Cylinder(new Vector3(0, 0.22f, 0), 0.04f, 0.035f, 0.12f, 6, Color.White);
                m.Cylinder(new Vector3(0, 0.34f, 0), 0.045f, 0.045f, 0.04f, 6, Cork);
                break;
            case "stone":
                m.Blob(new Vector3(0, 0.12f, 0), new Vector3(0.11f, 0.12f, 0.1f), 3, 8, Color.White, 4, 0.08f);
                break;
            case "key":
                m.Box(new Vector3(0, 0.025f, 0), new Vector3(0.36f, 0.04f, 0.05f), Color.White);
                m.Ring(new Vector3(-0.22f, 0.005f, 0), 0.09f, 0.05f, 0.04f, 8, Color.White);
                m.Box(new Vector3(0.15f, 0.025f, 0.06f), new Vector3(0.06f, 0.04f, 0.08f), Color.White);
                break;
            case "lantern":
                m.Box(new Vector3(0, 0.14f, 0), new Vector3(0.2f, 0.24f, 0.2f), Color.White);
                m.Cone(new Vector3(0, 0.26f, 0), 0.16f, 0.12f, 4, Color.White);
                break;
            case "crystal":
                m.Cone(Vector3.Zero, 0.08f, 0.3f, 5, Color.White);
                m.Append(new MeshBuilder().Cone(Vector3.Zero, 0.06f, 0.2f, 5, Color.White).Transform(Matrix4x4.CreateRotationZ(0.5f) * Matrix4x4.CreateTranslation(0.06f, 0, 0)));
                break;
            default:
                m.Blob(new Vector3(0, 0.11f, 0), new Vector3(0.11f, 0.11f, 0.11f), 3, 8, Color.White, 3, 0.06f);
                break;
        }
        return m;
    }

    /// <summary>Reads the drawn shape of each item from the front end's visuals data (Assets/visuals.json).</summary>
    public static Dictionary<string, string> LoadShapes(string assetsDir)
    {
        var shapes = new Dictionary<string, string>();
        var path = Path.Combine(assetsDir, "visuals.json");
        if (!File.Exists(path)) return shapes;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            if (!doc.RootElement.TryGetProperty("items", out var items)) return shapes;
            foreach (var p in items.EnumerateObject())
                if (p.Value.TryGetProperty("shape", out var s) && s.GetString() is { } shape) shapes[p.Name] = shape;
        }
        catch (JsonException) { }
        return shapes;
    }
}
