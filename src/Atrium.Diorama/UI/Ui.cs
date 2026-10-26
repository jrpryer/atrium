using System.Numerics;
using Atrium.Diorama.App;
using Atrium.Diorama.Audio;
using Atrium.Diorama.Render;
using Raylib_cs;

namespace Atrium.Diorama.UI;

/// <summary>
/// An immediate-mode interface in a virtual 1600×900 space (scaled to the window). Parchment and ink;
/// every control lifts on hover, squashes on press and makes a small sound.
/// </summary>
public sealed class Ui
{
    public const float W = 1600, H = 900;

    public Fonts F { get; }
    public AudioKit Audio { get; }
    public float Time { get; private set; }
    public float Dt { get; private set; }
    public bool ReduceMotion { get; set; }

    /// <summary>False while drawing layers under a modal: controls draw but don't respond.</summary>
    public bool InputEnabled { get; set; } = true;

    /// <summary>True when the mouse was over the interface last frame (world picking should ignore it).</summary>
    public bool WantsMouse { get; private set; }

    private readonly Dictionary<string, float> _anim = new();
    private readonly Dictionary<string, Spring> _springs = new();
    private readonly HashSet<string> _hoverNow = new(), _hoverPrev = new();
    private readonly List<Rectangle> _hits = new();
    private readonly Dictionary<string, float> _scroll = new();
    private string? _tooltip;
    private bool _modalThisFrame;

    public Ui(Fonts fonts, AudioKit audio) { F = fonts; Audio = audio; }

    // where each named control was drawn, last frame and this one (scripted captures press controls by name)
    private Dictionary<string, Rectangle> _rects = new(), _rectsPrev = new();

    /// <summary>Where the control with this id was last drawn. A trailing * matches by prefix.</summary>
    public bool TryGetControl(string id, out Rectangle r)
    {
        foreach (var d in new[] { _rectsPrev, _rects })
        {
            if (id.EndsWith('*'))
            {
                var key = d.Keys.FirstOrDefault(k => k.StartsWith(id[..^1], StringComparison.Ordinal));
                if (key is not null) { r = d[key]; return true; }
            }
            else if (d.TryGetValue(id, out r)) return true;
        }
        r = default;
        return false;
    }

    public void BeginFrame(float dt)
    {
        (_rectsPrev, _rects) = (_rects, _rectsPrev);
        _rects.Clear();
        Dt = dt;
        Time += dt;
        _hits.Clear();
        _hoverNow.Clear();
        _tooltip = null;
        _modalThisFrame = false;
    }

    public void EndFrame()
    {
        if (_tooltip is not null) DrawTooltip(_tooltip);
        var m = Input.Mouse;
        WantsMouse = _modalThisFrame || _hits.Any(r => Raylib.CheckCollisionPointRec(m, r));
        _hoverPrev.Clear();
        foreach (var h in _hoverNow) _hoverPrev.Add(h);
    }

    /// <summary>Registers an opaque region (the world won't receive clicks under it).</summary>
    public void Block(Rectangle r) => _hits.Add(r);

    /// <summary>Marks that a modal layer is open this frame.</summary>
    public void Modal() => _modalThisFrame = true;

    public bool Hover(Rectangle r) => InputEnabled && Raylib.CheckCollisionPointRec(Input.Mouse, r);

    /// <summary>Animated 0..1 value toward <paramref name="on"/>; for hover glows and reveals.</summary>
    public float Anim(string id, bool on, float rate = 14)
    {
        var v = _anim.TryGetValue(id, out var a) ? a : 0;
        v = Ease.Approach(v, on ? 1 : 0, rate, Dt);
        _anim[id] = v;
        return v;
    }

    public Spring SpringOf(string id, float initial = 0)
    {
        if (!_springs.TryGetValue(id, out var s)) _springs[id] = s = new Spring(initial);
        return s;
    }

    public void SetSpring(string id, Spring s) => _springs[id] = s;

    /// <summary>Tracks hover for sound: returns true on the frame the pointer enters.</summary>
    private bool Entered(string id, bool hovering)
    {
        if (!hovering) return false;
        _hoverNow.Add(id);
        return !_hoverPrev.Contains(id);
    }

    // ───────────────────────── drawing primitives ─────────────────────────

    /// <summary>A parchment card with a soft shadow and a double ink rule.</summary>
    public void Panel(Rectangle r, float alpha = 1, Color? accent = null, bool block = true)
    {
        if (block) Block(r);
        var shadow = new Rectangle(r.X + 5, r.Y + 8, r.Width, r.Height);
        Raylib.DrawRectangleRounded(shadow, Round(r, 14), 10, Palette.Alpha(Palette.Ink, 0.22f * alpha));
        Raylib.DrawRectangleRounded(r, Round(r, 14), 10, Palette.Alpha(Palette.Parchment, alpha));
        var inner = new Rectangle(r.X, r.Y + r.Height * 0.55f, r.Width, r.Height * 0.45f);
        Raylib.DrawRectangleGradientV((int)inner.X + 6, (int)inner.Y, (int)inner.Width - 12, (int)inner.Height - 6,
            Palette.Alpha(Palette.ParchmentDark, 0), Palette.Alpha(Palette.ParchmentDark, 0.55f * alpha));
        var ink = accent ?? Palette.InkSoft;
        Raylib.DrawRectangleRoundedLinesEx(r, Round(r, 14), 10, 2.2f, Palette.Alpha(ink, 0.85f * alpha));
        var r2 = new Rectangle(r.X + 7, r.Y + 7, r.Width - 14, r.Height - 14);
        Raylib.DrawRectangleRoundedLinesEx(r2, Round(r2, 10), 10, 1f, Palette.Alpha(ink, 0.35f * alpha));
    }

    private static float Round(Rectangle r, float px) => Math.Clamp(px / MathF.Min(r.Width, r.Height) * 2, 0.02f, 1f);

    /// <summary>A small fleuron rule under headings.</summary>
    public void Rule(float x, float y, float w, Color c)
    {
        Raylib.DrawLineEx(new Vector2(x, y), new Vector2(x + w * 0.44f, y), 1.4f, Palette.Alpha(c, 0.6f));
        Raylib.DrawLineEx(new Vector2(x + w * 0.56f, y), new Vector2(x + w, y), 1.4f, Palette.Alpha(c, 0.6f));
        Diamond(new Vector2(x + w / 2, y), 5, c);
    }

    public static void Diamond(Vector2 c, float r, Color col)
    {
        Raylib.DrawTriangle(c + new Vector2(0, -r), c + new Vector2(-r, 0), c + new Vector2(0, r), col);
        Raylib.DrawTriangle(c + new Vector2(0, -r), c + new Vector2(0, r), c + new Vector2(r, 0), col);
    }

    public void Heading(string text, Vector2 pos, float size = 46, Color? c = null) =>
        Text.Draw(F.Hand, text, pos, size, c ?? Palette.Ink);

    public void HeadingCentered(string text, Vector2 centre, float size = 46, Color? c = null) =>
        Text.DrawCentered(F.Hand, text, centre, size, c ?? Palette.Ink);

    public void Label(string text, Vector2 pos, float size = 26, Color? c = null, Font? font = null) =>
        Text.Draw(font ?? F.Body, text, pos, size, c ?? Palette.Ink);

    public float Paragraph(string text, Rectangle r, float size = 26, Color? c = null, Font? font = null, int reveal = int.MaxValue, bool verbatim = false) =>
        Text.DrawWrapped(font ?? F.Body, text, r, size, c ?? Palette.Ink, 1.3f, reveal, verbatim);

    // ───────────────────────── controls ─────────────────────────

    /// <summary>A parchment button. Returns true when clicked.</summary>
    public bool Button(string id, Rectangle r, string label, Color? accent = null, bool enabled = true, float size = 26, string? tooltip = null)
    {
        Block(r);
        if (enabled) _rects[id] = r;
        bool hov = enabled && Hover(r);
        if (Entered(id, hov)) Audio.Play("tock", 0.5f);
        float h = Anim(id, hov);
        var sp = SpringOf(id + "#p", 1);
        sp.Target = 1;
        sp.Update(Dt);
        bool clicked = hov && Input.Pressed;
        if (clicked)
        {
            sp.Value = 0.92f;
            Input.ClickConsumed = true;
            Audio.Play("paper", 0.7f);
        }
        SetSpring(id + "#p", sp);
        float s = sp.Value + h * 0.025f;
        var c = new Vector2(r.X + r.Width / 2, r.Y + r.Height / 2 - h * 2);
        var rr = new Rectangle(c.X - r.Width * s / 2, c.Y - r.Height * s / 2, r.Width * s, r.Height * s);
        var acc = accent ?? Palette.InkSoft;
        Raylib.DrawRectangleRounded(new Rectangle(rr.X + 2, rr.Y + 4, rr.Width, rr.Height), 0.35f, 8, Palette.Alpha(Palette.Ink, 0.18f));
        var fill = Palette.Lerp(Palette.Parchment, Palette.Lerp(Palette.Parchment, acc, 0.18f), h);
        Raylib.DrawRectangleRounded(rr, 0.35f, 8, enabled ? fill : Palette.Alpha(Palette.ParchmentDark, 0.8f));
        Raylib.DrawRectangleRoundedLinesEx(rr, 0.35f, 8, 1.8f + h, Palette.Alpha(enabled ? acc : Palette.InkFaint, 0.9f));
        Text.DrawCentered(F.Body, label, c + new Vector2(0, 1), size * s, enabled ? Palette.Ink : Palette.InkFaint);
        if (hov && tooltip is not null) _tooltip = tooltip;
        return clicked && enabled;
    }

    /// <summary>A round icon button with a drawn icon and a tooltip.</summary>
    public bool IconButton(string id, Vector2 centre, float radius, Action<Vector2, float, Color> icon, string tooltip, Color? accent = null, bool enabled = true)
    {
        var r = new Rectangle(centre.X - radius, centre.Y - radius, radius * 2, radius * 2);
        Block(r);
        if (enabled) _rects[id] = r;
        bool hov = enabled && Hover(r);
        if (Entered(id, hov)) Audio.Play("tock", 0.5f);
        float h = Anim(id, hov);
        bool clicked = hov && Input.Pressed;
        if (clicked) { Input.ClickConsumed = true; Audio.Play("paper", 0.6f); }
        var c = centre - new Vector2(0, h * 3);
        Raylib.DrawCircleV(centre + new Vector2(1, 4), radius, Palette.Alpha(Palette.Ink, 0.22f));
        Raylib.DrawCircleV(c, radius, Palette.Lerp(Palette.Parchment, Palette.LanternHot, h * 0.5f));
        Raylib.DrawCircleLinesV(c, radius, accent ?? Palette.InkSoft);
        Raylib.DrawCircleLinesV(c, radius - 4, Palette.Alpha(accent ?? Palette.InkSoft, 0.35f));
        icon(c, radius * 0.55f, enabled ? Palette.Ink : Palette.InkFaint);
        if (hov) _tooltip = tooltip;
        return clicked && enabled;
    }

    /// <summary>A clickable row in a list (books, elements, terms). Returns true when clicked.</summary>
    public bool Row(string id, Rectangle r, string text, string? sub = null, Color? swatch = null, bool selected = false, bool enabled = true, float size = 24)
    {
        Block(r);
        if (enabled) _rects[id] = r;
        bool hov = enabled && Hover(r);
        if (Entered(id, hov)) Audio.Play("tock", 0.35f);
        float h = Anim(id, hov || selected);
        bool clicked = hov && Input.Pressed;
        if (clicked) { Input.ClickConsumed = true; Audio.Play("paper", 0.45f); }
        if (h > 0.01f)
            Raylib.DrawRectangleRounded(r, 0.25f, 6, Palette.Alpha(selected ? Palette.LanternHot : Palette.ParchmentDark, 0.65f * h));
        float x = r.X + 12 + h * 4;
        if (swatch is { } sw)
        {
            Raylib.DrawRectangleRounded(new Rectangle(x, r.Y + r.Height / 2 - 11, 14, 22), 0.4f, 4, sw);
            x += 26;
        }
        Text.Draw(F.Body, text, new Vector2(x, r.Y + (sub is null ? (r.Height - size) / 2 : 4)), size, enabled ? Palette.Ink : Palette.InkFaint);
        if (sub is not null) Text.Draw(F.Italic, sub, new Vector2(x, r.Y + size + 6), size * 0.78f, Palette.InkSoft);
        return clicked && enabled;
    }

    /// <summary>Begins a vertical scroll region; returns the y offset to apply. Call <see cref="EndScroll"/> after.</summary>
    public float BeginScroll(string id, Rectangle view, float contentHeight)
    {
        float off = _scroll.TryGetValue(id, out var o) ? o : 0;
        if (Hover(view)) off -= Input.Wheel * 48;
        off = Math.Clamp(off, 0, MathF.Max(0, contentHeight - view.Height));
        _scroll[id] = off;
        var sr = Input.ToScreen(view);
        Raylib.BeginScissorMode((int)sr.X, (int)sr.Y, (int)sr.Width, (int)sr.Height);
        if (contentHeight > view.Height)
        {
            float bar = view.Height * view.Height / contentHeight;
            float y = view.Y + (view.Height - bar) * (off / (contentHeight - view.Height));
            Raylib.DrawRectangleRounded(new Rectangle(view.X + view.Width - 6, y, 4, bar), 1, 4, Palette.Alpha(Palette.InkSoft, 0.35f));
        }
        return off;
    }

    public void EndScroll() => Raylib.EndScissorMode();

    public void ResetScroll(string id) => _scroll.Remove(id);

    public void Tooltip(string text) => _tooltip = text;

    private void DrawTooltip(string text)
    {
        var m = Input.Mouse;
        var lines = Text.Wrap(F.Body, text, 22, 380);
        float w = lines.Max(l => Text.Measure(F.Body, l, 22).X) + 28, h = lines.Count * 28 + 18;
        var r = new Rectangle(Math.Min(m.X + 18, W - w - 8), Math.Min(m.Y + 22, H - h - 8), w, h);
        Raylib.DrawRectangleRounded(new Rectangle(r.X + 3, r.Y + 5, r.Width, r.Height), 0.2f, 6, Palette.Alpha(Palette.Ink, 0.25f));
        Raylib.DrawRectangleRounded(r, 0.2f, 6, Palette.Ink);
        for (int i = 0; i < lines.Count; i++) Text.Draw(F.Body, lines[i], new Vector2(r.X + 14, r.Y + 9 + i * 28), 22, Palette.Parchment);
    }

    /// <summary>Full-screen dim behind a modal.</summary>
    public void Dim(float a = 0.45f)
    {
        Modal();
        Raylib.DrawRectangle(0, 0, (int)W, (int)H, Palette.Alpha(Palette.Ink, a));
    }
}
