using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Atrium.Terminal.Ui;

public readonly record struct Rgb(byte R, byte G, byte B);

/// <summary>The palette. Warm, low-contrast, cozy. Nothing is ever alarm-red.</summary>
public static class Palette
{
    public static readonly Rgb Text = new(226, 220, 205);
    public static readonly Rgb Dim = new(140, 136, 126);
    public static readonly Rgb Faint = new(98, 96, 92);
    public static readonly Rgb Gold = new(232, 190, 98);
    public static readonly Rgb Lamp = new(247, 216, 146);
    public static readonly Rgb Moss = new(138, 180, 112);
    public static readonly Rgb Water = new(96, 198, 192);
    public static readonly Rgb Violet = new(178, 150, 216);
    public static readonly Rgb Rose = new(214, 156, 146);
    public static readonly Rgb Copper = new(214, 144, 94);
    public static readonly Rgb Stone = new(176, 170, 156);
    public static readonly Rgb Blue = new(126, 164, 222);
    public static readonly Rgb Oxblood = new(184, 96, 92);
    public static readonly Rgb Ochre = new(208, 164, 76);
    public static readonly Rgb Cream = new(250, 240, 214);
    public static readonly Rgb Bark = new(158, 124, 92);

    public static Rgb Genre(string color) => color switch
    {
        "green" => Moss,
        "oxblood" => Oxblood,
        "blue" => Blue,
        "violet" => Violet,
        "ochre" => Ochre,
        _ => Stone,
    };

    public static Rgb Locale(string id) => id switch
    {
        "library" => Moss,
        "apothecary" => Copper,
        "pools" => Water,
        "sanctum" => Violet,
        _ => Gold,
    };
}

/// <summary>ANSI escape helpers. Colour can be switched off (--plain or redirected output).</summary>
public static class Ansi
{
    public static bool Color { get; set; } = true;

    public static string Fg(Rgb c, string s) => Color && s.Length > 0 ? $"\x1b[38;2;{c.R};{c.G};{c.B}m{s}\x1b[0m" : s;
    public static string Bold(string s) => Color ? $"\x1b[1m{s}\x1b[0m" : s;
    public static string BoldFg(Rgb c, string s) => Color && s.Length > 0 ? $"\x1b[1;38;2;{c.R};{c.G};{c.B}m{s}\x1b[0m" : s;
    public static string Italic(Rgb c, string s) => Color && s.Length > 0 ? $"\x1b[3;38;2;{c.R};{c.G};{c.B}m{s}\x1b[0m" : s;

    private static readonly Regex Escapes = new("\x1b\\[[0-9;?]*[A-Za-z]", RegexOptions.Compiled);
    public static string Strip(string s) => Escapes.Replace(s, "");
    public static int Width(string s) => Strip(s).Length;

    public static string PadRight(string s, int width)
    {
        var w = Width(s);
        return w >= width ? s : s + new string(' ', width - w);
    }
}

/// <summary>Word wrapping that respects paragraphs and explicit line breaks.</summary>
public static class Wrap
{
    public static List<string> Lines(string text, int width, string indent = "")
    {
        var result = new List<string>();
        width = Math.Max(20, width - indent.Length);
        var paragraphs = text.Replace("\r\n", "\n").Split("\n\n");
        for (int p = 0; p < paragraphs.Length; p++)
        {
            if (p > 0) result.Add("");
            foreach (var hard in paragraphs[p].Split('\n'))
            {
                var words = hard.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var line = new StringBuilder();
                foreach (var w in words)
                {
                    if (line.Length > 0 && line.Length + 1 + w.Length > width)
                    {
                        result.Add(indent + line);
                        line.Clear();
                    }
                    if (line.Length > 0) line.Append(' ');
                    line.Append(w);
                }
                result.Add(indent + line);
            }
        }
        return result;
    }

    public static string Bar(double fraction, int width, char full = '█', char empty = '░')
    {
        var n = (int)Math.Round(Math.Clamp(fraction, 0, 1) * width);
        return new string(full, n) + new string(empty, width - n);
    }

    public static string Signed(int v) => v > 0 ? "+" + v : v.ToString();

    public static string FirstSentence(string text)
    {
        var flat = text.Replace('\n', ' ');
        for (int i = 20; i < flat.Length - 1; i++)
            if ((flat[i] is '.' or '!' or '?') && flat[i + 1] == ' ') return flat[..(i + 1)];
        return flat.Length > 140 ? flat[..137] + "…" : flat;
    }
}

/// <summary>A screenful of lines, written in one go to avoid flicker.</summary>
public sealed class Frame
{
    public List<string> Lines { get; } = new();
    public int Width { get; }

    public Frame(int width) => Width = width;

    public Frame Add(string s = "") { Lines.Add(s); return this; }

    public Frame Text(string text, Rgb color, string indent = "  ")
    {
        foreach (var l in Wrap.Lines(text, Width - 2, indent)) Lines.Add(Ansi.Fg(color, l));
        return this;
    }

    /// <summary>A marked paragraph with a hanging indent.</summary>
    public Frame Bullet(string mark, string text, Rgb color, string indent = "     ")
    {
        var pad = new string(' ', Ansi.Width(mark));
        var lines = Wrap.Lines(text, Width - 2 - pad.Length, indent);
        for (int i = 0; i < lines.Count; i++)
            Lines.Add(Ansi.Fg(color, i == 0 ? indent + mark + lines[i][indent.Length..] : indent + pad + lines[i][indent.Length..]));
        return this;
    }

    public Frame Verbatim(string text, Rgb color, string indent = "    ")
    {
        foreach (var l in text.Replace("\r\n", "\n").Split('\n')) Lines.Add(Ansi.Fg(color, indent + l));
        return this;
    }

    public Frame Rule(Rgb color, string? label = null)
    {
        var w = Width - 4;
        if (label is null) { Lines.Add("  " + Ansi.Fg(color, new string('─', w))); return this; }
        var head = $"── {label} ";
        Lines.Add("  " + Ansi.Fg(color, head + new string('─', Math.Max(2, w - head.Length))));
        return this;
    }
}

/// <summary>Owns the console: alternate screen, cursor, VT processing, sizing.</summary>
public static class Screen
{
    public static bool Interactive { get; private set; }
    private static bool _alt;

    public static int Width => Interactive ? Math.Clamp(SafeWidth(), 60, 112) : 100;
    public static int Height => Interactive ? Math.Max(24, SafeHeight()) : 400;

    private static int SafeWidth() { try { return Console.WindowWidth; } catch { return 100; } }
    private static int SafeHeight() { try { return Console.WindowHeight; } catch { return 40; } }

    public static void Begin(bool interactive)
    {
        Interactive = interactive;
        Console.OutputEncoding = Encoding.UTF8;
        if (!interactive) return;
        EnableVirtualTerminal();
        Console.Write("\x1b[?1049h\x1b[?25l");
        _alt = true;
        Console.CancelKeyPress += (_, _) => End();
    }

    public static void End()
    {
        if (!_alt) return;
        _alt = false;
        Console.Write("\x1b[0m\x1b[?25h\x1b[?1049l");
    }

    public static void Draw(Frame f)
    {
        if (!Interactive)
        {
            Console.WriteLine(new string('═', 100));
            foreach (var l in f.Lines) Console.WriteLine(Ansi.Strip(l).TrimEnd());
            return;
        }
        var sb = new StringBuilder("\x1b[H");
        var max = Height - 1;
        var lines = f.Lines.Count > max ? f.Lines.Take(max - 1).Append(Ansi.Fg(Palette.Faint, "  … (more below; the window is a little small)")).ToList() : f.Lines;
        foreach (var l in lines) sb.Append(l).Append("\x1b[K\n");
        sb.Append("\x1b[J");
        Console.Write(sb.ToString());
    }

    public static void ShowCursor(bool show)
    {
        if (Interactive) Console.Write(show ? "\x1b[?25h" : "\x1b[?25l");
    }

    // ───── Windows: make sure ANSI sequences are interpreted (older consoles) ─────

    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GetStdHandle(int nStdHandle);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

    private static void EnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            var h = GetStdHandle(STD_OUTPUT_HANDLE);
            if (GetConsoleMode(h, out var mode)) SetConsoleMode(h, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        }
        catch (Exception) { /* colour is a nicety */ }
    }
}
