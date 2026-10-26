using System.Numerics;
using System.Text.Json;
using Atrium.Core.Content;
using Atrium.Core.Runtime;
using Atrium.Diorama.Render;
using Raylib_cs;

namespace Atrium.Diorama.UI;

/// <summary>Draws little ink-and-wash icons for items, from Assets/visuals.json (front-end look data, not game content).</summary>
public sealed class ItemArt
{
    private sealed record Look(string Shape, Color Color);
    private readonly Dictionary<string, Look> _looks = new();

    private static readonly Dictionary<string, (Color Body, Color? Glow)> Lifeforms = new();

    /// <summary>The story beat that relights the courtyard's old charter lamps (look data, from visuals.json).</summary>
    public static string? CharterLampsBeat { get; private set; }

    /// <summary>
    /// Courtyard pieces drawn from imported models (models/NAME.glb, built by tools/blender) and the scale of each in
    /// courtyard units per metre (look data, from visuals.json). A piece that isn't listed keeps its built-in shape.
    /// </summary>
    public static IReadOnlyDictionary<string, float> HubModels => _hubModels;
    private static readonly Dictionary<string, float> _hubModels = new();

    /// <summary>
    /// Rooms drawn from an imported model kit (models/LOCALE-room.glb, built by tools/blender; look data, from
    /// visuals.json). A room that isn't listed keeps its built-in furniture.
    /// </summary>
    public static IReadOnlySet<string> RoomModels => _roomModels;
    private static readonly HashSet<string> _roomModels = new();

    public ItemArt(string path)
    {
        if (!File.Exists(path)) return;
        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        if (doc.RootElement.TryGetProperty("items", out var items))
            foreach (var p in items.EnumerateObject())
                _looks[p.Name] = new Look(p.Value.GetProperty("shape").GetString() ?? "orb", Palette.Parse(p.Value.GetProperty("color").GetString(), Palette.Stone));
        if (doc.RootElement.TryGetProperty("lifeforms", out var lifeforms))
            foreach (var p in lifeforms.EnumerateObject())
            {
                Color? glow = p.Value.TryGetProperty("glow", out var g) ? Palette.Parse(g.GetString(), Palette.LanternHot) : null;
                Lifeforms[p.Name] = (Palette.Parse(p.Value.GetProperty("color").GetString(), Palette.Stone), glow);
            }
        if (doc.RootElement.TryGetProperty("hub", out var hub))
        {
            if (hub.TryGetProperty("charterLampsBeat", out var beat)) CharterLampsBeat = beat.GetString();
            if (hub.TryGetProperty("models", out var models))
                foreach (var p in models.EnumerateObject()) _hubModels[p.Name] = p.Value.GetSingle();
        }
        if (doc.RootElement.TryGetProperty("rooms", out var rooms) && rooms.TryGetProperty("models", out var roomModels))
            foreach (var e in roomModels.EnumerateArray())
                if (e.GetString() is { } id) _roomModels.Add(id);
    }

    /// <summary>A lifeform's body colour; species without a listed look get a stable tint from their id.</summary>
    public static Color LifeformColor(string species)
    {
        if (Lifeforms.TryGetValue(species, out var l)) return l.Body;
        uint h = 2166136261;
        foreach (var ch in species) h = (h ^ ch) * 16777619;
        return Palette.Lerp(Palette.Hex(0x6FA7B8), Palette.Hex(0xE0A060), (h % 1000) / 1000f);
    }

    /// <summary>The colour a lifeform shines with, if it shines at all.</summary>
    public static Color? LifeformGlow(string species) => Lifeforms.TryGetValue(species, out var l) ? l.Glow : null;

    public Color ColorOf(ItemResource def) => LookOf(def).Color;

    private Look LookOf(ItemResource def)
    {
        if (_looks.TryGetValue(def.Id, out var l)) return l;
        var hue = (uint)def.Id.GetHashCode();
        var c = Palette.Lerp(Palette.Hex(0x8AA6B8), Palette.Hex(0xC9A27A), (hue % 100) / 100f);
        return def.Kind switch
        {
            ItemKind.Mineral or ItemKind.Aura => new Look("crystal", c),
            ItemKind.Food => new Look("food", c),
            ItemKind.ClueForm => new Look("orb", c),
            ItemKind.Remnant => new Look("slag", Palette.Hex(0x55505A)),
            ItemKind.Tool => new Look("lantern", Palette.Gold),
            _ => new Look("jar", c),
        };
    }

    /// <summary>Draws an item's icon centred at <paramref name="c"/>, roughly <paramref name="s"/> pixels across.</summary>
    public void Draw(ItemResource def, Vector2 c, float s, bool inert = false, float time = 0)
    {
        var look = LookOf(def);
        var col = inert ? Palette.Lerp(look.Color, Palette.InkFaint, 0.7f) : look.Color;
        float r = s / 2;
        var ink = Palette.Alpha(Palette.Ink, 0.85f);
        switch (look.Shape)
        {
            case "jar":
                Raylib.DrawRectangleRounded(new Rectangle(c.X - r * 0.55f, c.Y - r * 0.45f, r * 1.1f, r * 1.3f), 0.35f, 6, Palette.Alpha(Palette.Foam, 0.65f));
                Raylib.DrawRectangleRounded(new Rectangle(c.X - r * 0.48f, c.Y - r * 0.05f, r * 0.96f, r * 0.85f), 0.35f, 6, col);
                Raylib.DrawRectangleRoundedLinesEx(new Rectangle(c.X - r * 0.55f, c.Y - r * 0.45f, r * 1.1f, r * 1.3f), 0.35f, 6, 1.5f, ink);
                Raylib.DrawRectangleRounded(new Rectangle(c.X - r * 0.3f, c.Y - r * 0.72f, r * 0.6f, r * 0.3f), 0.3f, 4, Palette.Hex(0xA9825A));
                Raylib.DrawLineEx(c + new Vector2(-r * 0.32f, -r * 0.3f), c + new Vector2(-r * 0.32f, r * 0.55f), 2, Palette.Alpha(Color.White, 0.55f));
                break;
            case "candle":
                Raylib.DrawRectangleRounded(new Rectangle(c.X - r * 0.25f, c.Y - r * 0.3f, r * 0.5f, r * 1.0f), 0.3f, 4, col);
                Raylib.DrawRectangleRoundedLinesEx(new Rectangle(c.X - r * 0.25f, c.Y - r * 0.3f, r * 0.5f, r * 1.0f), 0.3f, 4, 1.5f, ink);
                Raylib.DrawCircleGradient(c + new Vector2(0, -r * 0.55f), r * 0.45f, Palette.Alpha(Palette.LanternHot, 0.8f), Palette.Alpha(Palette.Lantern, 0));
                Raylib.DrawEllipse((int)c.X, (int)(c.Y - r * 0.52f), r * 0.1f, r * 0.2f, Palette.Lantern);
                break;
            case "pouch":
            case "bag":
                Raylib.DrawCircleV(c + new Vector2(0, r * 0.2f), r * 0.6f, col);
                Raylib.DrawCircleLinesV(c + new Vector2(0, r * 0.2f), r * 0.6f, ink);
                Raylib.DrawTriangle(c + new Vector2(-r * 0.3f, -r * 0.35f), c + new Vector2(0, -r * 0.05f), c + new Vector2(r * 0.3f, -r * 0.35f), col);
                Raylib.DrawLineEx(c + new Vector2(-r * 0.3f, -r * 0.2f), c + new Vector2(r * 0.3f, -r * 0.2f), 2.5f, Palette.Bark);
                if (look.Shape == "bag" && !inert)
                    for (int i = 0; i < 3; i++)
                        Raylib.DrawCircleV(c + new Vector2(MathF.Sin(time * 1.5f + i * 2) * r * 0.25f, -r * (0.55f + i * 0.2f)), r * (0.14f - i * 0.03f), Palette.Alpha(Palette.InkSoft, 0.5f));
                break;
            case "leaf":
                for (int i = -1; i <= 1; i += 2)
                    Raylib.DrawCircleSector(c + new Vector2(i * r * 0.05f, 0), r * 0.75f, i < 0 ? 200 : 20, i < 0 ? 340 : 160, 10, col);
                Raylib.DrawEllipse((int)c.X, (int)c.Y, r * 0.38f, r * 0.85f, col);
                Raylib.DrawLineEx(c + new Vector2(0, r * 0.95f), c + new Vector2(0, -r * 0.8f), 1.8f, Palette.Hex(0xA8B4BC));
                for (int i = 0; i < 4; i++)
                {
                    float y = -r * 0.5f + i * r * 0.32f;
                    Raylib.DrawLineEx(c + new Vector2(0, y + r * 0.1f), c + new Vector2(-r * 0.3f, y - r * 0.08f), 1.2f, Palette.Hex(0xC4D0D8));
                    Raylib.DrawLineEx(c + new Vector2(0, y + r * 0.1f), c + new Vector2(r * 0.3f, y - r * 0.08f), 1.2f, Palette.Hex(0xC4D0D8));
                }
                Raylib.DrawEllipseLines((int)c.X, (int)c.Y, r * 0.38f, r * 0.85f, ink);
                break;
            case "nugget":
                Raylib.DrawCircleV(c + new Vector2(-r * 0.2f, r * 0.05f), r * 0.45f, col);
                Raylib.DrawCircleV(c + new Vector2(r * 0.22f, -r * 0.05f), r * 0.4f, col);
                Raylib.DrawCircleV(c + new Vector2(0, r * 0.2f), r * 0.38f, Palette.Mul(col, 0.85f));
                Raylib.DrawCircleV(c + new Vector2(-r * 0.15f, -r * 0.15f), r * 0.12f, Palette.Alpha(Color.White, 0.75f));
                if (!inert) Raylib.DrawCircleGradient(c, r * 0.9f, Palette.Alpha(Palette.LanternHot, 0.25f), Palette.Alpha(Palette.LanternHot, 0));
                break;
            case "flakes":
                for (int i = 0; i < 6; i++)
                {
                    var p = c + new Vector2(MathF.Cos(i * 1.9f) * r * 0.45f, MathF.Sin(i * 2.7f) * r * 0.4f);
                    Ui.Diamond(p, r * 0.18f, i % 2 == 0 ? col : Palette.Mul(col, 1.15f));
                }
                break;
            case "flask":
                Raylib.DrawCircleV(c + new Vector2(0, r * 0.25f), r * 0.55f, Palette.Alpha(Palette.Foam, 0.6f));
                Raylib.DrawCircleSector(c + new Vector2(0, r * 0.25f), r * 0.5f, 0, 180, 12, col);
                Raylib.DrawRectangle((int)(c.X - r * 0.15f), (int)(c.Y - r * 0.65f), (int)(r * 0.3f), (int)(r * 0.5f), Palette.Alpha(Palette.Foam, 0.7f));
                Raylib.DrawCircleLinesV(c + new Vector2(0, r * 0.25f), r * 0.55f, ink);
                if (!inert) for (int i = 0; i < 3; i++) Raylib.DrawCircleV(c + new Vector2(MathF.Sin(time + i * 2) * r * 0.25f, r * (0.3f + 0.1f * i)), 2, Palette.LanternHot);
                break;
            case "slag":
                Raylib.DrawTriangle(c + new Vector2(-r * 0.12f, -r * 0.7f), c + new Vector2(0, r * 0.85f), c + new Vector2(r * 0.12f, -r * 0.7f), col);
                Raylib.DrawRectangle((int)(c.X - r * 0.3f), (int)(c.Y - r * 0.85f), (int)(r * 0.6f), (int)(r * 0.18f), col);
                break;
            case "pearl":
                Raylib.DrawCircleV(c, r * 0.6f, col);
                Raylib.DrawCircleGradient(c, r * 0.3f, Palette.Alpha(Palette.Lantern, inert ? 0 : 0.35f), Palette.Alpha(Palette.Lantern, 0));
                Raylib.DrawCircleV(c + new Vector2(-r * 0.2f, -r * 0.22f), r * 0.14f, Palette.Alpha(Color.White, 0.7f));
                Raylib.DrawCircleLinesV(c, r * 0.6f, ink);
                break;
            case "husk":
                Raylib.DrawCircleSector(c, r * 0.6f, 180, 360, 14, col);
                Raylib.DrawRing(c, r * 0.4f, r * 0.6f, 180, 360, 14, Palette.Mul(col, 0.8f));
                break;
            case "stone":
                if (def.Id.StartsWith("waking", StringComparison.Ordinal) && !inert)
                    Raylib.DrawCircleGradient(c, r * 1.0f, Palette.Alpha(Palette.LanternHot, 0.45f + 0.15f * MathF.Sin(time * 2)), Palette.Alpha(Palette.Lantern, 0));
                Raylib.DrawEllipse((int)c.X, (int)c.Y, r * 0.5f, r * 0.68f, col);
                Raylib.DrawEllipseLines((int)c.X, (int)c.Y, r * 0.5f, r * 0.68f, ink);
                Raylib.DrawEllipse((int)(c.X - r * 0.15f), (int)(c.Y - r * 0.25f), r * 0.12f, r * 0.2f, Palette.Alpha(Color.White, 0.6f));
                break;
            case "key":
                Raylib.DrawRing(c + new Vector2(-r * 0.55f, 0), r * 0.15f, r * 0.3f, 0, 360, 16, col);
                Raylib.DrawLineEx(c + new Vector2(-r * 0.25f, 0), c + new Vector2(r * 0.7f, 0), r * 0.16f, col);
                Raylib.DrawRectangle((int)(c.X + r * 0.4f), (int)c.Y, (int)(r * 0.3f), (int)(r * 0.35f), Palette.Hex(0xD8DEE2));
                break;
            case "lantern":
                if (!inert) Raylib.DrawCircleGradient(c, r * 0.95f, Palette.Alpha(Palette.LanternHot, 0.5f), Palette.Alpha(Palette.Lantern, 0));
                Raylib.DrawRectangleRounded(new Rectangle(c.X - r * 0.35f, c.Y - r * 0.4f, r * 0.7f, r * 0.9f), 0.2f, 4, Palette.Alpha(inert ? Palette.InkFaint : Palette.LanternHot, 0.85f));
                Raylib.DrawRectangleRoundedLinesEx(new Rectangle(c.X - r * 0.35f, c.Y - r * 0.4f, r * 0.7f, r * 0.9f), 0.2f, 4, 2, col);
                Raylib.DrawTriangle(c + new Vector2(-r * 0.42f, -r * 0.4f), c + new Vector2(r * 0.42f, -r * 0.4f), c + new Vector2(0, -r * 0.72f), col);
                Raylib.DrawRingLines(c + new Vector2(0, -r * 0.8f), r * 0.08f, r * 0.12f, 0, 360, 10, col);
                break;
            case "sieve":
                Raylib.DrawCircleV(c, r * 0.65f, Palette.Alpha(col, 0.35f));
                for (int i = -2; i <= 2; i++)
                {
                    Raylib.DrawLineEx(c + new Vector2(i * r * 0.22f, -r * 0.6f), c + new Vector2(i * r * 0.22f, r * 0.6f), 1, Palette.Alpha(Palette.Bark, 0.7f));
                    Raylib.DrawLineEx(c + new Vector2(-r * 0.6f, i * r * 0.22f), c + new Vector2(r * 0.6f, i * r * 0.22f), 1, Palette.Alpha(Palette.Bark, 0.7f));
                }
                Raylib.DrawRing(c, r * 0.6f, r * 0.72f, 0, 360, 24, Palette.Hex(0xC4664A));
                break;
            case "lens":
                Raylib.DrawCircleV(c, r * 0.55f, Palette.Alpha(col, 0.55f));
                Raylib.DrawRing(c, r * 0.55f, r * 0.68f, 0, 360, 24, Palette.Hex(0xB8875A));
                Raylib.DrawCircleV(c + new Vector2(-r * 0.18f, -r * 0.18f), r * 0.12f, Palette.Alpha(Color.White, 0.7f));
                break;
            case "still":
                Raylib.DrawRectangle((int)(c.X - r * 0.5f), (int)(c.Y + r * 0.35f), (int)r, (int)(r * 0.25f), Palette.Hex(0xC4664A));
                for (int i = 0; i < 3; i++) Raylib.DrawRingLines(c + new Vector2(0, -r * 0.15f + i * r * 0.18f), r * 0.25f, r * 0.32f, 0, 360, 16, col);
                break;
            case "crystal":
                if (!inert) Raylib.DrawCircleGradient(c, r * 0.85f, Palette.Alpha(col, 0.35f), Palette.Alpha(col, 0));
                Raylib.DrawTriangle(c + new Vector2(0, -r * 0.7f), c + new Vector2(-r * 0.35f, 0), c + new Vector2(r * 0.35f, 0), Palette.Mul(col, 1.1f));
                Raylib.DrawTriangle(c + new Vector2(-r * 0.35f, 0), c + new Vector2(0, r * 0.7f), c + new Vector2(r * 0.35f, 0), Palette.Mul(col, 0.8f));
                break;
            case "food":
                Raylib.DrawRectangleRounded(new Rectangle(c.X - r * 0.5f, c.Y - r * 0.3f, r, r * 0.8f), 0.4f, 6, col);
                Raylib.DrawRectangleRoundedLinesEx(new Rectangle(c.X - r * 0.5f, c.Y - r * 0.3f, r, r * 0.8f), 0.4f, 6, 1.5f, ink);
                for (int i = 0; i < 5; i++) Raylib.DrawCircleV(c + new Vector2(-r * 0.3f + i * r * 0.15f, -r * 0.45f + (i % 2) * 4), 2.5f, Palette.Mul(col, 0.8f));
                break;
            default:
                Raylib.DrawCircleV(c, r * 0.55f, col);
                Raylib.DrawCircleLinesV(c, r * 0.55f, ink);
                break;
        }
        if (inert) Raylib.DrawCircleV(c, r * 0.9f, Palette.Alpha(Palette.Parchment, 0.25f));
    }

    public void Draw(ContentDatabase content, ItemInstance item, Vector2 c, float s, float time = 0) =>
        Draw(content.Item(item.Def), c, s, item.Inert, time);
}
