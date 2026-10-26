using System.Numerics;
using Raylib_cs;

namespace Atrium.Diorama.UI;

/// <summary>
/// Book text in EB Garamond (OFL, bundled under Assets/fonts) and headings in a handwritten face from the system
/// (Ink Free or Segoe Print), each with sensible fallbacks. Fonts are rasterised large and drawn scaled.
/// </summary>
public sealed class Fonts : IDisposable
{
    public Font Body, Italic, Bold, Hand;
    private readonly List<Font> _owned = new();

    private static readonly int[] Codepoints = BuildCodepoints();

    private static int[] BuildCodepoints()
    {
        var cps = new List<int>();
        for (int c = 32; c < 127; c++) cps.Add(c);
        for (int c = 160; c < 256; c++) cps.Add(c);
        cps.AddRange(new[] { 0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2026, 0x2022, 0x00B7, 0x2190, 0x2192, 0x00D7, 0x2032, 0x2033, 0x2009, 0x00A0, 0x2039, 0x203A, 0x2194 });
        return cps.Distinct().ToArray();
    }

    public Fonts(string assetsDir)
    {
        var fontsDir = Path.Combine(assetsDir, "fonts");
        var user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
        var win = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string? Find(params string[] candidates) =>
            candidates.SelectMany(c => new[] { Path.Combine(fontsDir, c), Path.Combine(user, c), Path.Combine(win, c) }).FirstOrDefault(File.Exists);

        Body = Load(Find("EBGaramond-Regular.ttf", "EBGaramond-Medium.ttf", "georgia.ttf", "pala.ttf"), 56);
        Italic = Load(Find("EBGaramond-Italic.ttf", "georgiai.ttf", "palai.ttf"), 56, Body);
        Bold = Load(Find("EBGaramond-SemiBold.ttf", "EBGaramond-Bold.ttf", "georgiab.ttf", "palab.ttf"), 56, Body);
        Hand = Load(Find("Inkfree.ttf", "segoepr.ttf", "EBGaramond-Italic.ttf"), 64, Italic);
    }

    private Font Load(string? path, int size, Font? fallback = null)
    {
        if (path is null) return fallback ?? Raylib.GetFontDefault();
        var f = Raylib.LoadFontEx(path, size, Codepoints, Codepoints.Length);
        if (f.Texture.Id == 0) return fallback ?? Raylib.GetFontDefault();
        Raylib.GenTextureMipmaps(ref f.Texture);
        Raylib.SetTextureFilter(f.Texture, TextureFilter.Trilinear);
        _owned.Add(f);
        return f;
    }

    public void Dispose()
    {
        foreach (var f in _owned) Raylib.UnloadFont(f);
    }
}

/// <summary>Word-wrapped text drawing and measuring.</summary>
public static class Text
{
    public static Vector2 Measure(Font f, string s, float size) => Raylib.MeasureTextEx(f, s, size, size * 0.02f);

    public static void Draw(Font f, string s, Vector2 pos, float size, Color c) => Raylib.DrawTextEx(f, s, pos, size, size * 0.02f, c);

    public static void DrawCentered(Font f, string s, Vector2 centre, float size, Color c)
    {
        var m = Measure(f, s, size);
        Draw(f, s, centre - m / 2, size, c);
    }

    /// <summary>Wraps paragraphs to a width. Blank lines separate paragraphs; single newlines are kept.</summary>
    public static List<string> Wrap(Font f, string text, float size, float width)
    {
        var lines = new List<string>();
        var paras = text.Replace("\r\n", "\n").Split("\n\n");
        for (int p = 0; p < paras.Length; p++)
        {
            if (p > 0) lines.Add("");
            foreach (var hard in paras[p].Split('\n'))
            {
                var line = "";
                foreach (var w in hard.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trial = line.Length == 0 ? w : line + " " + w;
                    if (line.Length > 0 && Measure(f, trial, size).X > width) { lines.Add(line); line = w; }
                    else line = trial;
                }
                lines.Add(line);
            }
        }
        return lines;
    }

    /// <summary>Draws wrapped text; returns the height used. <paramref name="reveal"/> limits visible characters (typewriter).</summary>
    public static float DrawWrapped(Font f, string text, Rectangle r, float size, Color c, float lineGap = 1.28f, int reveal = int.MaxValue, bool verbatim = false)
    {
        var lines = verbatim ? text.Replace("\r\n", "\n").Split('\n').ToList() : Wrap(f, text, size, r.Width);
        float y = r.Y;
        int shown = 0;
        foreach (var l in lines)
        {
            if (shown >= reveal) break;
            var vis = l.Length <= reveal - shown ? l : l[..(reveal - shown)];
            Draw(f, vis, new Vector2(r.X, y), size, c);
            shown += l.Length + 1;
            y += size * lineGap;
        }
        return y - r.Y;
    }

    public static float Height(Font f, string text, float width, float size, float lineGap = 1.28f, bool verbatim = false) =>
        (verbatim ? text.Split('\n').Length : Wrap(f, text, size, width).Count) * size * lineGap;
}
