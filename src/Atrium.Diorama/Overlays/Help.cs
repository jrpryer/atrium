using System.Numerics;
using Atrium.Diorama.App;
using Atrium.Diorama.Render;
using Atrium.Diorama.UI;
using Raylib_cs;

namespace Atrium.Diorama.Overlays;

/// <summary>How to play: a two-page spread, the houses on the left and the mystery on the right.</summary>
public sealed class HelpCard : Overlay
{
    private static readonly (string Name, Color Accent, string Text)[] Houses =
    {
        ("The Library", Palette.Library, "Hang a genre tag on each shelf, then shelve the pile with the oldest book nearest the lamp. A shelf in perfect order settles with a chime. Read anything, and write in the margins."),
        ("The Apothecary", Palette.Apothecary, "Set up to three elements on the bench, pick a tool or none, and mix. Discoveries are yours for good and never run out. A failed mix still teaches you something, and the Discovery Map shows what you're close to."),
        ("The Spirit Pools", Palette.Pools, "Every pool sits somewhere between Light and Dark, Hot and Cold. Keep the water clear and the creatures fed, in water they like, and they'll breed. Things laid in a pool change it, and the water slowly changes them."),
        ("The Sanctum", Palette.Sanctum, "Look at things under the glass, assemble tools from what you've made, and use the chalk circle to remake what you took apart. The designation desk is where you write down what you believe something truly is."),
    };

    public override void Draw()
    {
        if (Age < 0.02f) App.Audio.Play("page", 0.7f);
        var (left, right) = BookArt.Spread(Ui, Age, Palette.Hex(0x5B4636));
        Ui.Heading("How the Atrium works", new Vector2(left.X, left.Y), 38);
        Ui.Rule(left.X, left.Y + 50, 260, Palette.Sanctum);
        float y = left.Y + 72;
        foreach (var (name, accent, text) in Houses)
        {
            Raylib.DrawRectangleRounded(new Rectangle(left.X, y + 6, 6, 26), 1, 4, accent);
            Text.Draw(Ui.F.Hand, name, new Vector2(left.X + 18, y), 28, Palette.Ink);
            y += 36;
            y += Text.DrawWrapped(Ui.F.Body, text, new Rectangle(left.X + 18, y, left.Width - 30, 200), 21, Palette.InkSoft, 1.25f) + 14;
        }

        Ui.Heading("Curiosities", new Vector2(right.X, right.Y), 38);
        Ui.Rule(right.X, right.Y + 50, 200, Palette.Sanctum);
        float ry = right.Y + 72;
        ry += Text.DrawWrapped(Ui.F.Body,
            "Odd things turn up now and then. Carry them in your satchel and try them in each house's own way: on a shelf or against a page, on the scales or in the burner, in the water, or under the glass. What you notice goes into your journal, and some of it earns you words to describe the thing.",
            new Rectangle(right.X, ry, right.Width - 10, 220), 21, Palette.InkSoft, 1.25f) + 14;
        ry += Text.DrawWrapped(Ui.F.Body,
            "When your notes feel full enough, write down what you believe it truly is at the Sanctum. If you're wrong in an interesting way, you'll learn exactly why. Nothing is ever lost for good, and nothing here is in a hurry.",
            new Rectangle(right.X, ry, right.Width - 10, 220), 21, Palette.InkSoft, 1.25f) + 22;

        Text.Draw(Ui.F.Hand, "Hands and keys", new Vector2(right.X, ry), 28, Palette.Ink);
        ry += 38;
        foreach (var line in new[]
                 {
                     "Click a house to go in, and click furniture inside.",
                     "Right-drag or A / D to turn the courtyard; scroll to zoom.",
                     "Click things in your satchel to use them where you are.",
                     "J  the journal      P  pause the clock      Space  sit by the water",
                     "Esc  go back, or open the Keeper's menu in the courtyard",
                 })
        {
            Text.Draw(Ui.F.Italic, line, new Vector2(right.X + 6, ry), 20, Palette.InkSoft);
            ry += 27;
        }
        if (Ui.Button("help-close", new Rectangle(right.X + right.Width - 210, right.Y + right.Height + 12, 210, 40), "Back to the Atrium")) Close();
    }
}

/// <summary>The Keeper's menu (Esc in the courtyard): carry on, how to play, settings, save.</summary>
public sealed class KeeperMenu : Overlay
{
    public override void Draw()
    {
        float t = Ease.OutBack(MathF.Min(1, Age / 0.3f));
        Ui.Dim(0.4f);
        var r = new Rectangle(Ui.W / 2 - 230, Ui.H / 2 - 230 + (1 - t) * 20, 460, 460);
        Ui.Panel(r, 1, Palette.Sanctum);
        Ui.HeadingCentered("The Keeper's rest", new Vector2(r.X + r.Width / 2, r.Y + 52), 42);
        Ui.Rule(r.X + 110, r.Y + 92, 240, Palette.Sanctum);
        var summary = App.Session is null ? "" : $"Day {App.Session.World.Clock.Day}, {App.Session.World.Clock.TimeOfDay()}";
        Text.DrawCentered(Ui.F.Italic, summary, new Vector2(r.X + r.Width / 2, r.Y + 116), 21, Palette.InkFaint);
        float y = r.Y + 146, x = r.X + 60, w = r.Width - 120;
        if (Ui.Button("km-continue", new Rectangle(x, y, w, 48), "Carry on", Palette.Pools)) Close();
        y += 60;
        if (Ui.Button("km-help", new Rectangle(x, y, w, 48), "How the Atrium works")) { Close(); App.Open(new HelpCard()); }
        y += 60;
        if (Ui.Button("km-settings", new Rectangle(x, y, w, 48), "Settings")) { Close(); App.Open(new SettingsCard()); }
        y += 60;
        if (Ui.Button("km-title", new Rectangle(x, y, w, 48), "Save and return to the title"))
        {
            App.SaveQuietly();
            Close();
            App.Go(new Scenes.TitleScene());
        }
        y += 60;
        if (Ui.Button("km-quit", new Rectangle(x, y, w, 48), "Save and quit", Palette.Sanctum))
        {
            App.SaveQuietly();
            App.Quitting = true;
        }
    }
}
