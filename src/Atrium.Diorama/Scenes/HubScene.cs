using System.Numerics;
using Atrium.Diorama.App;
using Atrium.Diorama.Render;
using Atrium.Diorama.UI;
using Raylib_cs;

namespace Atrium.Diorama.Scenes;

/// <summary>The courtyard: orbit, zoom, hover a house to see its name in a small caption, click to go in.</summary>
public sealed class HubScene : Scene
{
    private HubWorld _world = null!;
    private float _yaw = 0.62f, _yawTarget = 0.62f, _dist = 44, _distTarget = 44, _pitch = 0.52f;
    private Vector3 _target = new(0.4f, 2.4f, -1.6f);
    private string? _hovered;
    private string? _entering;
    private float _enterT;
    private string _caption = "", _captionSub = "";
    private float _captionA;
    private float _idle;

    public override string Mood => "hub";
    public override Color Backdrop => R.Light.Fog;

    /// <summary>The interior scene for a locale.</summary>
    public static Scene Interior(string locale) => locale switch
    {
        "library" => new LibraryScene(),
        "apothecary" => new ApothecaryScene(),
        "pools" => new PoolsScene(),
        "sanctum" => new SanctumScene(),
        _ => new HubScene(),
    };

    public override void Enter()
    {
        _world = new HubWorld(R);
        App.Audio.Play("leave", 0.5f);
        if (App.Session is not null) App.SaveQuietly();
        // arrive gently from a little further out
        _dist = 52;
        UpdateCamera(0);
    }

    public override void Exit() { _world.Dispose(); R.SkyTop = null; }

    public override bool Back()
    {
        if (_entering is null) App.Open(new Overlays.KeeperMenu());
        return true;
    }

    private void UpdateCamera(float dt)
    {
        _yaw = Ease.Approach(_yaw, _yawTarget, 6, dt);
        _dist = Ease.Approach(_dist, _distTarget, 5, dt);
        var target = _target;
        float dist = _dist;
        if (_entering is not null && _world.Houses.TryGetValue(_entering, out var h))
        {
            float e = Ease.InOutSine(_enterT);
            target = Vector3.Lerp(_target, h.Door, e);
            dist = dist + (6 - dist) * e;
        }
        Camera = Orbit(target, _yaw, _pitch, dist, 31);
        _world.Camera = Camera;
    }

    public override void Update(float dt)
    {
        float phase = App.Session is null ? 0.3f : (App.Session.World.Clock.Tick % 2400) / 2400f;
        HubWorld.ApplyTimeOfDay(R.Light, phase);
        R.SkyTop = Palette.Lerp(R.Light.Sky, R.Light.Fog, 0.25f);
        _world.Update(dt, App.Particles, App.Session, R.ReduceMotion);

        if (_entering is not null)
        {
            _enterT = MathF.Min(1, _enterT + dt / 0.9f);
            UpdateCamera(dt);
            return;
        }

        // orbit and zoom
        if (!Ui.WantsMouse)
        {
            if (Input.RightDown) _yawTarget -= Input.RightDelta.X * 0.006f;
            if (Input.Wheel != 0) _distTarget = Math.Clamp(_distTarget - Input.Wheel * 2.6f, 26, 58);
        }
        if (Input.KeyDown(KeyboardKey.A) || Input.KeyDown(KeyboardKey.Left)) _yawTarget += dt * 1.1f;
        if (Input.KeyDown(KeyboardKey.D) || Input.KeyDown(KeyboardKey.Right)) _yawTarget -= dt * 1.1f;
        if (Input.KeyPressed(KeyboardKey.Space)) Sit();
        _idle += dt;
        if (!R.ReduceMotion && _idle > 25) _yawTarget += dt * 0.02f; // a slow, idle drift
        UpdateCamera(dt);

        // hover and click
        _hovered = null;
        if (!Ui.WantsMouse)
        {
            var ray = Raylib.GetScreenToWorldRay(Input.ScreenMouse, Camera);
            float best = float.MaxValue;
            foreach (var h in _world.Houses.Values)
            {
                var hit = Raylib.GetRayCollisionBox(ray, h.Box);
                if (hit.Hit && hit.Distance < best) { best = hit.Distance; _hovered = h.Locale; }
            }
            if (_hovered is not null && Input.Click) Knock(_hovered);
            else if (_hovered is null && Input.Click) ClickGround(ray);
        }
        string? prev = _caption;
        if (_hovered is not null && App.Content.Locales.TryGetValue(_hovered, out var loc))
        {
            bool open = App.Session is null || App.Session.IsUnlocked(_hovered);
            _caption = loc.Name;
            _captionSub = open ? loc.Description : loc.LockedDescription;
            if (_caption != prev) { App.Audio.Play("tock", 0.45f, 1.2f); _idle = 0; }
        }
        _captionA = Ease.Approach(_captionA, _hovered is null ? 0 : 1, 10, dt);
    }

    private void Knock(string locale)
    {
        var s = App.Session;
        if (s is not null && !s.IsUnlocked(locale))
        {
            App.Audio.Play("hm");
            if (_world.Houses.TryGetValue(locale, out var hh)) { hh.Lift.Kick(-1.5f); }
            return;
        }
        App.Audio.Play("enter");
        _entering = locale;
        _enterT = 0;
        App.Go(Interior(locale));
    }

    private void ClickGround(Ray ray)
    {
        // the tree and the water answer softly; nothing here is a button
        if (_world.HitsTree(ray))
        {
            App.Audio.Play("creak", 0.35f, 0.8f);
            App.Toasts.Add("Old Stoic", "Big, old, and perfectly still, as it has always been.", Atrium.Core.Runtime.NoticeKind.Info);
            return;
        }
        var benchHit = Raylib.GetRayCollisionBox(ray, new BoundingBox(new Vector3(-1.6f, 0, -5.8f), new Vector3(0.4f, 1.0f, -4.6f)));
        if (benchHit.Hit) { Sit(); return; }
        var ground = Raylib.GetRayCollisionBox(ray, new BoundingBox(new Vector3(-1.5f, -0.1f, -8.6f), new Vector3(5.2f, 0.25f, -3.7f)));
        if (ground.Hit)
        {
            App.Audio.Play("splash", 0.4f);
            for (int i = 0; i < 10; i++)
                App.Particles.Emit(ground.Point + new Vector3(0, 0.2f, 0), new Vector3(App.Particles.R(-1, 1), App.Particles.R(1, 2.4f), App.Particles.R(-1, 1)), 0.7f, 0.12f, Palette.Foam, false, 0, 0.4f, -6f);
        }
    }

    private void Sit()
    {
        if (App.Session is null) return;
        var r = App.Session.SitAWhile();
        App.Audio.Play("chord", 0.35f);
        App.Toasts.Add("", r.Lines[0].Text, Atrium.Core.Runtime.NoticeKind.Info);
    }

    public override void Draw3D() => _world.Draw(R, App.Session, _entering ?? _hovered);

    public override void DrawUI()
    {
        // the caption: handwritten, low on the screen, never floating over a house
        if (_captionA > 0.02f)
        {
            float a = _captionA;
            var c = new Vector2(Ui.W / 2, Ui.H - 160);
            var lines = Text.Wrap(Ui.F.Italic, _captionSub, 22, 620);
            float h = 64 + lines.Count * 26;
            var r = new Rectangle(c.X - 360, c.Y - h + 36, 720, h);
            Raylib.DrawRectangleRounded(r, 0.25f, 8, Palette.Alpha(Palette.Parchment, 0.85f * a));
            var accent = _hovered is null ? Palette.InkSoft : Palette.Locale(_hovered);
            Raylib.DrawRectangleRoundedLinesEx(r, 0.25f, 8, 1.6f, Palette.Alpha(accent, 0.8f * a));
            Text.DrawCentered(Ui.F.Hand, _caption, new Vector2(c.X, r.Y + 26), 38, Palette.Alpha(Palette.Ink, a));
            for (int i = 0; i < lines.Count; i++)
                Text.DrawCentered(Ui.F.Italic, lines[i], new Vector2(c.X, r.Y + 62 + i * 26), 22, Palette.Alpha(Palette.InkSoft, a));
        }
        if (App.Session is null) return;
        if (Ui.IconButton("hub-sit", new Vector2(Ui.W - 56 - 74 * 3, 56), 30, Icons.Bench, "Sit by the water a while (Space)")) Sit();
        var open = App.Session.Unresolved.Open.Select(o => o.Name).ToList();
        if (open.Count > 0)
        {
            var text = "still unexplained: " + string.Join(", ", open);
            var size = Text.Measure(Ui.F.Italic, text, 21);
            Raylib.DrawRectangleRounded(new Rectangle(20, 110, size.X + 22, 32), 0.5f, 8, Palette.Alpha(Palette.Parchment, 0.78f));
            Text.Draw(Ui.F.Italic, text, new Vector2(31, 114), 21, Palette.Alpha(Palette.Ink, 0.8f));
        }
        if (App.Session.ChapterComplete) Text.Draw(Ui.F.Hand, "Chapter One is complete. The Atrium is yours to keep.", new Vector2(30, 140), 26, Palette.Ink);
    }
}

/// <summary>The title: the courtyard turning slowly behind a parchment card.</summary>
public sealed class TitleScene : Scene
{
    private HubWorld _world = null!;
    private float _yaw = 0.2f;
    private bool _hasSave;

    public override string Mood => "title";
    public override bool ShowHud => false;

    public override void Enter()
    {
        _world = new HubWorld(R);
        _hasSave = App.Store.Exists(App.Slot);
    }

    public override void Exit() { _world.Dispose(); R.SkyTop = null; }

    public override void Update(float dt)
    {
        HubWorld.ApplyTimeOfDay(R.Light, 0.68f);
        R.SkyTop = Palette.Lerp(R.Light.Sky, R.Light.Fog, 0.2f);
        _world.Update(dt, App.Particles, null, R.ReduceMotion);
        _yaw += dt * (R.ReduceMotion ? 0 : 0.04f);
        Camera = Orbit(new Vector3(-2.5f, 3.2f, -1.5f), _yaw, 0.42f, 46, 30);
        _world.Camera = Camera;
    }

    public override void Draw3D() => _world.Draw(R, null, null);

    public override void DrawUI()
    {
        var r = new Rectangle(90, 170, 520, 560);
        Ui.Panel(r, 0.94f);
        Text.Draw(Ui.F.Hand, "Atrium", new Vector2(r.X + 64, r.Y + 44), 108, Palette.Ink);
        Ui.Rule(r.X + 70, r.Y + 176, 380, Palette.Sanctum);
        Ui.Paragraph("A cozy puzzle game about tending a forgotten courtyard, and slowly working out the secrets that turn up in it.",
            new Rectangle(r.X + 66, r.Y + 200, r.Width - 132, 120), 25, Palette.InkSoft, Ui.F.Italic);
        float y = r.Y + 330;
        if (Ui.Button("title-new", new Rectangle(r.X + 70, y, 380, 52), "Begin a new Atrium", Palette.Sanctum))
        {
            if (_hasSave) App.Open(new Overlays.Confirm("Start again?", "There's already an Atrium in this save slot. Starting again will replace it.", () => App.NewGame(), "Start again", "Go back"));
            else App.NewGame();
        }
        y += 66;
        if (_hasSave && Ui.Button("title-continue", new Rectangle(r.X + 70, y, 380, 52), "Continue", Palette.Library))
            if (App.LoadGame()) App.Go(new HubScene());
        if (_hasSave) y += 66;
        if (Ui.Button("title-settings", new Rectangle(r.X + 70, y, 182, 46), "Settings")) App.Open(new Overlays.SettingsCard());
        if (Ui.Button("title-quit", new Rectangle(r.X + 268, y, 182, 46), "Quit")) App.Quitting = true;
        Text.Draw(Ui.F.Italic, "Right-drag or A/D to look around · scroll to zoom · Esc to go back", new Vector2(r.X + 64, r.Y + r.Height - 46), 19, Palette.InkFaint);
    }
}
