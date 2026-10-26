using System.Numerics;
using Raylib_cs;

namespace Atrium.Diorama.Render;

/// <summary>"Lamplight and linen": the iteration-2 palette. Locale accents follow the overview sheet.</summary>
public static class Palette
{
    public static readonly Color Parchment = Hex(0xEFE3C8);
    public static readonly Color ParchmentDark = Hex(0xE0CFAA);
    public static readonly Color Ink = Hex(0x2B2A33);
    public static readonly Color InkSoft = Hex(0x5A5560);
    public static readonly Color InkFaint = Hex(0x9A9184);
    public static readonly Color Stone = Hex(0xD9C7A7);
    public static readonly Color StoneDark = Hex(0xA8987C);
    public static readonly Color Moss = Hex(0x7FA36B);
    public static readonly Color Leaf = Hex(0x6E9F5E);
    public static readonly Color LeafDeep = Hex(0x4E7A47);
    public static readonly Color Bark = Hex(0x7A5A43);
    public static readonly Color Teal = Hex(0x1C8A84);
    public static readonly Color TealLight = Hex(0x56A9A5);
    public static readonly Color Foam = Hex(0xE6F4F0);
    public static readonly Color Lantern = Hex(0xF2B04C);
    public static readonly Color LanternHot = Hex(0xFFD98A);
    public static readonly Color Rose = Hex(0xC98C80);
    public static readonly Color Gold = Hex(0xD9A441);

    public static readonly Color Library = Hex(0x3D5AA8);
    public static readonly Color Pools = Hex(0x1C8A84);
    public static readonly Color Apothecary = Hex(0x8A55A8);
    public static readonly Color Sanctum = Hex(0xB0700F);

    public static Color Locale(string id) => id switch
    {
        "library" => Library,
        "pools" => Pools,
        "apothecary" => Apothecary,
        "sanctum" => Sanctum,
        _ => Lantern,
    };

    public static Color Genre(string color) => color switch
    {
        "green" => Hex(0x5E8F4E),
        "oxblood" => Hex(0x8C3B37),
        "blue" => Hex(0x3D5AA8),
        "violet" => Hex(0x7A4E9A),
        "ochre" => Hex(0xC08A2E),
        _ => StoneDark,
    };

    public static Color Hex(uint rgb, byte a = 255) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, a);

    public static Color Alpha(Color c, float a) => new(c.R, c.G, c.B, (byte)Math.Clamp(a * 255, 0, 255));

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return new((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t), (byte)(a.A + (b.A - a.A) * t));
    }

    public static Color Mul(Color c, float k) =>
        new((byte)Math.Clamp(c.R * k, 0, 255), (byte)Math.Clamp(c.G * k, 0, 255), (byte)Math.Clamp(c.B * k, 0, 255), c.A);

    public static Vector3 V3(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f);
    public static Vector4 V4(Color c) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

    /// <summary>Parses "#RRGGBB" (used by front-end visuals data).</summary>
    public static Color Parse(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        var h = hex.TrimStart('#');
        return h.Length == 6 && uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var v) ? Hex(v) : fallback;
    }
}
