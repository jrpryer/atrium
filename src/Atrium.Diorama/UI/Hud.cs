using System.Numerics;
using Atrium.Core.Content;
using Atrium.Core.Runtime;
using Atrium.Diorama.App;
using Atrium.Diorama.Overlays;
using Atrium.Diorama.Render;
using Raylib_cs;

namespace Atrium.Diorama.UI;

/// <summary>The clock medallion, the corner buttons and the leather satchel bar.</summary>
public static class Hud
{
    public static void Draw(GameApp app)
    {
        var ui = app.Ui;
        var s = app.Session!;
        Clock(app);

        // corner buttons
        float x = Ui.W - 56;
        if (ui.IconButton("hud-settings", new Vector2(x, 56), 30, Icons.Gear, "Settings")) app.Open(new SettingsCard());
        x -= 74;
        bool paused = s.World.Clock.Paused;
        if (ui.IconButton("hud-pause", new Vector2(x, 56), 30, Icons.Hourglass, paused ? "Let the world go on (P)" : "Let the world hold its breath (P)"))
            TogglePause(app);
        x -= 74;
        if (ui.IconButton("hud-journal", new Vector2(x, 56), 30, Icons.Book, "Your journal (J)")) app.Open(new JournalOverlay());
        if (app.Scene.Locale is not null)
        {
            x -= 74;
            if (ui.IconButton("hud-back", new Vector2(x, 56), 30, Icons.Door, "Back to the courtyard (Esc)")) app.Scene.Back();
        }
        if (!app.HasOverlay)
        {
            if (Input.KeyPressed(KeyboardKey.J)) app.Open(new JournalOverlay());
            if (Input.KeyPressed(KeyboardKey.P)) TogglePause(app);
        }
        Satchel(app);
    }

    private static void TogglePause(GameApp app)
    {
        var s = app.Session!;
        s.SetPaused(!s.World.Clock.Paused);
        app.Toasts.Add("", s.World.Clock.Paused ? "You let the world hold its breath. Nothing will change until you let it go on." : "The falls take up their falling again.", NoticeKind.Info);
    }

    private static void Clock(GameApp app)
    {
        var ui = app.Ui;
        var w = app.Session!.World;
        var c = new Vector2(64, 60);
        float phase = (w.Clock.Tick % 2400) / 2400f;
        ui.Block(new Rectangle(20, 16, 320, 90));
        // a parchment tab behind the words, so they read against night and day alike
        string when = w.Clock.Paused ? "the clock is resting" : w.Clock.TimeOfDay();
        float tabW = MathF.Max(Text.Measure(ui.F.Italic, when, 22).X, 90) + 90;
        Raylib.DrawRectangleRounded(new Rectangle(66, 24, tabW, 72), 0.45f, 8, Palette.Alpha(Palette.Parchment, 0.82f));
        Raylib.DrawCircleV(c + new Vector2(2, 5), 40, Palette.Alpha(Palette.Ink, 0.22f));
        Raylib.DrawCircleV(c, 40, Palette.Parchment);
        // sky disc: day above, night below; it turns with the day
        float ang = (phase - 0.375f) * MathF.Tau - MathF.PI / 2; // the sun stands highest at noon (3/8 of the day)
        var sun = c + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 24;
        var moon = c - new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 24;
        var sky = Input.ToScreen(new Rectangle(c.X - 38, c.Y - 38, 76, 38));
        Raylib.BeginScissorMode((int)sky.X, (int)sky.Y, (int)sky.Width, (int)sky.Height);
        Raylib.DrawCircleV(c, 38, Palette.Hex(0xF4E2BC));
        Raylib.DrawCircleV(sun, 9, Palette.Lantern);
        Raylib.DrawCircleV(moon, 7, Palette.Hex(0xC9D3E0));
        Raylib.EndScissorMode();
        Raylib.DrawLineEx(c - new Vector2(38, 0), c + new Vector2(38, 0), 2, Palette.InkSoft);
        Raylib.DrawCircleLinesV(c, 40, Palette.InkSoft);
        Raylib.DrawCircleLinesV(c, 34, Palette.Alpha(Palette.InkSoft, 0.35f));
        Text.Draw(ui.F.Hand, $"Day {w.Clock.Day}", new Vector2(116, 26), 30, Palette.Ink);
        Text.Draw(ui.F.Italic, when, new Vector2(118, 62), 22, Palette.InkSoft);
    }

    private static void Satchel(GameApp app)
    {
        var ui = app.Ui;
        var s = app.Session!;
        var items = s.World.Items.Satchel.ToList();
        var books = s.Library.CheckedOut();
        int n = items.Count + books.Count;
        float tile = 66, gap = 8;
        float w = Math.Max(260, n * (tile + gap) + 40);
        var bar = new Rectangle(Ui.W / 2 - w / 2, Ui.H - tile - 26, w, tile + 18);
        ui.Block(bar);
        // leather strap with stitching
        Raylib.DrawRectangleRounded(new Rectangle(bar.X + 3, bar.Y + 6, bar.Width, bar.Height), 0.3f, 8, Palette.Alpha(Palette.Ink, 0.25f));
        Raylib.DrawRectangleRounded(bar, 0.3f, 8, Palette.Hex(0x7A5136));
        var inner = new Rectangle(bar.X + 6, bar.Y + 6, bar.Width - 12, bar.Height - 12);
        Raylib.DrawRectangleRoundedLinesEx(inner, 0.3f, 8, 1.2f, Palette.Alpha(Palette.Hex(0xE6C89A), 0.55f));
        Text.DrawCentered(ui.F.Hand, "satchel", new Vector2(bar.X + 56, bar.Y - 12), 22, Palette.Alpha(Palette.Parchment, 0.9f));
        if (n == 0) Text.DrawCentered(ui.F.Italic, "a pencil and a slightly squashed apple", new Vector2(Ui.W / 2, bar.Y + bar.Height / 2), 21, Palette.Alpha(Palette.Parchment, 0.8f));

        float x = bar.X + 20 + gap / 2;
        foreach (var it in items)
        {
            var r = new Rectangle(x, bar.Y + 9, tile, tile);
            var id = "sat-" + it.Id;
            bool hov = ui.Hover(r);
            float h = ui.Anim(id, hov);
            Raylib.DrawRectangleRounded(new Rectangle(r.X, r.Y - h * 6, r.Width, r.Height), 0.25f, 6, Palette.Lerp(Palette.Hex(0xE9D7B4), Palette.LanternHot, h * 0.4f));
            var def = s.Content.Item(it.Def);
            app.Art.Draw(def, new Vector2(r.X + tile / 2, r.Y + tile / 2 - h * 6), tile * 0.78f, it.Inert, ui.Time);
            if (it.Quantity > 1) Text.Draw(ui.F.Bold, $"×{it.Quantity}", new Vector2(r.X + tile - 26, r.Y + tile - 26 - h * 6), 20, Palette.Ink);
            if (it.Essence is not null) Raylib.DrawCircleV(new Vector2(r.X + 9, r.Y + 9 - h * 6), 4, Palette.Lantern);
            if (hov)
            {
                ui.Tooltip($"{s.World.Items.Name(it)}: {s.World.Items.Description(it)}");
                if (Input.Pressed)
                {
                    Input.ClickConsumed = true;
                    app.Audio.Play("paper", 0.5f);
                    if (app.Scene is not ISatchelAware aware || !aware.UseItem(it)) app.Open(new ItemCard(it));
                }
            }
            x += tile + gap;
        }
        foreach (var b in books)
        {
            var r = new Rectangle(x, bar.Y + 9, tile, tile);
            bool hov = ui.Hover(r);
            float h = ui.Anim("sat-book-" + b.Id, hov);
            Raylib.DrawRectangleRounded(new Rectangle(r.X, r.Y - h * 6, r.Width, r.Height), 0.25f, 6, Palette.Hex(0xE9D7B4));
            var g = s.Content.Genres.TryGetValue(b.Genre, out var gg) ? Palette.Genre(gg.Color) : Palette.StoneDark;
            Raylib.DrawRectangleRounded(new Rectangle(r.X + 18, r.Y + 10 - h * 6, 30, 46), 0.15f, 4, g);
            Raylib.DrawLineEx(new Vector2(r.X + 22, r.Y + 16 - h * 6), new Vector2(r.X + 22, r.Y + 50 - h * 6), 2, Palette.Alpha(Palette.Parchment, 0.7f));
            if (hov)
            {
                ui.Tooltip($"A library book: {b.Title}");
                if (Input.Pressed) { Input.ClickConsumed = true; app.Open(new ReaderOverlay(b.Id)); }
            }
            x += tile + gap;
        }
    }
}

/// <summary>Scenes that give satchel items a use (put on the bench, lay in a pool…).</summary>
public interface ISatchelAware
{
    bool UseItem(ItemInstance item);
}

/// <summary>A close look at one carried thing.</summary>
public sealed class ItemCard : Overlay
{
    private readonly ItemInstance _item;
    public ItemCard(ItemInstance item) => _item = item;

    public override void Draw()
    {
        float t = Ease.OutBack(MathF.Min(1, Age / 0.3f));
        Ui.Dim(0.35f);
        var def = S.Content.Item(_item.Def);
        var desc = S.World.Items.Description(_item);
        float th = Text.Height(Ui.F.Body, desc, 420, 26);
        var r = new Rectangle(Ui.W / 2 - 340, Ui.H / 2 - (180 + th) / 2 + (1 - t) * 20, 680, 200 + th);
        Ui.Panel(r);
        Raylib.DrawCircleV(new Vector2(r.X + 120, r.Y + 120), 78, Palette.Alpha(Palette.ParchmentDark, 0.8f));
        App.Art.Draw(def, new Vector2(r.X + 120, r.Y + 120), 130, _item.Inert, Ui.Time);
        Ui.Heading(S.World.Items.Name(_item), new Vector2(r.X + 220, r.Y + 34), 40);
        Ui.Paragraph(desc, new Rectangle(r.X + 222, r.Y + 92, 420, th + 10), 26);
        var notes = new List<string>();
        if (_item.Essence is not null) notes.Add("It has something of its own in it. You'll want to understand it.");
        if (_item.RemnantOf is not null) notes.Add("A remnant. The chalk circle in the Sanctum could make its original whole again.");
        if (_item.Inert) notes.Add("Inert: only matter now.");
        float y = r.Y + 100 + th;
        foreach (var n in notes) { Ui.Paragraph(n, new Rectangle(r.X + 222, y, 420, 60), 22, Palette.InkSoft, Ui.F.Italic); y += 30; }
        if (Ui.Button("itemcard-ok", new Rectangle(r.X + r.Width / 2 - 70, r.Y + r.Height - 58, 140, 40), "Put it away")) Close();
    }
}

/// <summary>Small vector icons for the round buttons.</summary>
public static class Icons
{
    public static void Gear(Vector2 c, float r, Color col)
    {
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8;
            Raylib.DrawLineEx(c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r * 0.6f, c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r * 1.05f, r * 0.32f, col);
        }
        Raylib.DrawCircleV(c, r * 0.72f, col);
        Raylib.DrawCircleV(c, r * 0.3f, Palette.Parchment);
    }

    public static void Hourglass(Vector2 c, float r, Color col)
    {
        Raylib.DrawTriangle(c + new Vector2(-r * 0.6f, -r * 0.8f), c, c + new Vector2(r * 0.6f, -r * 0.8f), col);
        Raylib.DrawTriangle(c, c + new Vector2(-r * 0.6f, r * 0.8f), c + new Vector2(r * 0.6f, r * 0.8f), col);
        Raylib.DrawLineEx(c + new Vector2(-r * 0.75f, -r * 0.85f), c + new Vector2(r * 0.75f, -r * 0.85f), 3, col);
        Raylib.DrawLineEx(c + new Vector2(-r * 0.75f, r * 0.85f), c + new Vector2(r * 0.75f, r * 0.85f), 3, col);
    }

    public static void Book(Vector2 c, float r, Color col)
    {
        Raylib.DrawRectangleRounded(new Rectangle(c.X - r * 0.9f, c.Y - r * 0.65f, r * 0.85f, r * 1.3f), 0.15f, 4, col);
        Raylib.DrawRectangleRounded(new Rectangle(c.X + r * 0.05f, c.Y - r * 0.65f, r * 0.85f, r * 1.3f), 0.15f, 4, col);
        Raylib.DrawLineEx(c + new Vector2(0, -r * 0.65f), c + new Vector2(0, r * 0.65f), 2, Palette.Parchment);
    }

    public static void Door(Vector2 c, float r, Color col)
    {
        Raylib.DrawRectangle((int)(c.X - r * 0.5f), (int)(c.Y - r * 0.4f), (int)r, (int)(r * 1.2f), col);
        Raylib.DrawCircleSector(c + new Vector2(0, -r * 0.4f), r * 0.5f, 180, 360, 12, col);
        Raylib.DrawCircleV(c + new Vector2(r * 0.25f, r * 0.25f), r * 0.1f, Palette.Parchment);
    }

    public static void Bench(Vector2 c, float r, Color col)
    {
        Raylib.DrawRectangle((int)(c.X - r), (int)(c.Y - r * 0.15f), (int)(r * 2), (int)(r * 0.3f), col);
        Raylib.DrawRectangle((int)(c.X - r * 0.8f), (int)c.Y, (int)(r * 0.2f), (int)(r * 0.7f), col);
        Raylib.DrawRectangle((int)(c.X + r * 0.6f), (int)c.Y, (int)(r * 0.2f), (int)(r * 0.7f), col);
        Raylib.DrawRectangle((int)(c.X - r), (int)(c.Y - r * 0.7f), (int)(r * 2), (int)(r * 0.2f), col);
    }
}
