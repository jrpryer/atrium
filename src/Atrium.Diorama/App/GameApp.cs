using System.Numerics;
using System.Text.Json;
using Atrium.Core;
using Atrium.Core.Content;
using Atrium.Core.Runtime;
using Atrium.Diorama.Audio;
using Atrium.Diorama.Overlays;
using Atrium.Diorama.Render;
using Atrium.Diorama.Scenes;
using Atrium.Diorama.UI;
using Raylib_cs;

namespace Atrium.Diorama.App;

public sealed class Settings
{
    public float Master { get; set; } = 0.8f;
    public float Music { get; set; } = 0.55f;
    public float Sfx { get; set; } = 0.75f;
    public float Ambience { get; set; } = 0.6f;
    public bool ReduceMotion { get; set; }
    public bool Fullscreen { get; set; }
}

/// <summary>Owns the window, the frame loop, the session, scenes, overlays, notices and saving.</summary>
public sealed class GameApp
{
    public ContentDatabase Content { get; }
    public SaveStore Store { get; }
    public string Slot { get; }
    public GameSession? Session { get; set; }
    public Renderer Renderer { get; private set; } = null!;
    public Ui Ui { get; private set; } = null!;
    public AudioKit Audio { get; private set; } = null!;
    public Fonts Fonts { get; private set; } = null!;
    public Particles Particles { get; } = new();
    public ItemArt Art { get; private set; } = null!;
    public Settings Settings { get; private set; } = new();
    public string AssetsDir { get; }
    public Capture? Capture { get; }

    /// <summary>Open the model viewer on this file instead of the title (`--view file.glb`).</summary>
    public string? ViewModel { get; init; }
    public bool Quitting { get; set; }

    public Scene Scene { get; private set; } = null!;
    private Scene? _pending;
    private float _fade = 1;     // 1 = fully covered by warm light, 0 = clear
    private bool _fadingOut;
    private readonly List<Overlay> _overlays = new();
    private readonly Queue<Notice> _cards = new();
    public Toasts Toasts { get; } = new();
    private float _autosave;

    public GameApp(ContentDatabase content, SaveStore store, string slot, string assetsDir, Capture? capture)
    {
        Content = content; Store = store; Slot = slot; AssetsDir = assetsDir; Capture = capture;
    }

    public void Run(int width, int height)
    {
        LoadSettings();
        var flags = ConfigFlags.Msaa4xHint | ConfigFlags.ResizableWindow | ConfigFlags.VSyncHint;
        if (Capture is not null) flags = ConfigFlags.Msaa4xHint | ConfigFlags.HiddenWindow;
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        Raylib.SetConfigFlags(flags);
        Raylib.InitWindow(width, height, "Atrium");
        Raylib.SetExitKey(KeyboardKey.Null);
        Raylib.SetTargetFPS(60);
        if (Settings.Fullscreen && Capture is null) Raylib.ToggleBorderlessWindowed();
        Input.Scripted = Capture is not null;

        Renderer = new Renderer { ReduceMotion = Settings.ReduceMotion };
        Fonts = new Fonts(AssetsDir);
        Audio = new AudioKit(enable: Capture is null || Capture.WithAudio)
        {
            Master = Capture is null ? Settings.Master : 0, MusicVolume = Settings.Music, SfxVolume = Settings.Sfx, AmbienceVolume = Settings.Ambience,
        };
        Ui = new Ui(Fonts, Audio) { ReduceMotion = Settings.ReduceMotion };
        Art = new ItemArt(Path.Combine(AssetsDir, "visuals.json"));

        Go(ViewModel is null ? new TitleScene() : new ModelViewerScene(ViewModel), instant: true);
        while (!Quitting && !Raylib.WindowShouldClose())
        {
            float dt = Capture is not null ? 1 / 60f : MathF.Min(Raylib.GetFrameTime(), 1 / 15f);
            Capture?.BeforeFrame(this);
            Frame(dt);
            if (Capture?.Done == true) break;
        }
        if (Session is not null && Capture is null) SaveQuietly();
        SaveSettings();
        Audio.Dispose();
        Fonts.Dispose();
        Renderer.Dispose();
        Raylib.CloseWindow();
    }

    private void Frame(float dt)
    {
        Input.UiScale = Raylib.GetScreenHeight() / Ui.H;
        if (Raylib.GetScreenWidth() / (float)Raylib.GetScreenHeight() < Ui.W / Ui.H)
            Input.UiScale = Raylib.GetScreenWidth() / Ui.W;
        Input.UiOffset = new Vector2((Raylib.GetScreenWidth() - Ui.W * Input.UiScale) / 2, (Raylib.GetScreenHeight() - Ui.H * Input.UiScale) / 2);
        Renderer.Time += dt;
        Audio.SetMood(Scene.Mood);
        Audio.Update(dt);

        // the world-clock runs while the game is open (the core caps long gaps)
        if (Session is not null && Scene.ShowHud) Session.StepRealTime(dt);
        if (Session is not null) DrainNotices();

        // overlays and Esc
        _overlays.RemoveAll(o => o.Closed);
        if (_cards.Count > 0 && _overlays.All(o => o is not StoryCard)) Open(new StoryCard(_cards.Dequeue()));
        if (Input.KeyPressed(KeyboardKey.Escape))
        {
            if (_overlays.Count > 0) _overlays[^1].Back();
            else Scene.Back();
        }
        foreach (var o in _overlays) o.Age += dt;

        Input.Blocked = _overlays.Count > 0;
        Scene.Update(dt);
        Input.Blocked = false;
        Particles.Update(dt);
        Toasts.Update(dt);
        UpdateTransition(dt);

        _autosave += dt;
        if (_autosave > 90 && Session is not null && Capture is null) { _autosave = 0; SaveQuietly(); }

        // ── draw ──
        Renderer.Begin3D(Scene.Camera, Scene.Backdrop);
        Scene.Draw3D();
        Particles.Draw(Renderer);
        Renderer.End3D();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(Palette.Ink);
        Renderer.Present(Settings.ReduceMotion ? 0.02f : 0.035f);

        var uiCam = new Camera2D(Input.UiOffset, Vector2.Zero, 0, Input.UiScale);
        Raylib.BeginMode2D(uiCam);
        Ui.BeginFrame(dt);
        Ui.InputEnabled = _overlays.Count == 0 && _fade < 0.5f;
        if (Capture?.HideUi != true)
        {
            Scene.DrawUI();
            if (Session is not null && Scene.ShowHud) Hud.Draw(this);
            Toasts.Draw(this);
        }
        for (int i = 0; i < _overlays.Count; i++)
        {
            Ui.InputEnabled = i == _overlays.Count - 1 && _fade < 0.5f;
            _overlays[i].Draw();
        }
        Ui.InputEnabled = true;
        Ui.EndFrame();
        DrawFade();
        Raylib.EndMode2D();
        Capture?.AfterDraw(this);
        Raylib.EndDrawing();
        Input.EndFrame();
    }

    // ───────────────────────── scenes ─────────────────────────

    public void Go(Scene next, bool instant = false)
    {
        next.Attach(this);
        if (instant || Scene is null)
        {
            Scene?.Exit();
            Scene = next;
            Scene.Enter();
            _fade = 1;
            _fadingOut = false;
            return;
        }
        _pending = next;
        _fadingOut = true;
    }

    private void UpdateTransition(float dt)
    {
        if (_fadingOut)
        {
            _fade = MathF.Min(1, _fade + dt / 0.38f);
            if (_fade >= 1 && _pending is not null)
            {
                Scene.Exit();
                Scene = _pending;
                _pending = null;
                Particles.Clear();
                Scene.Enter();
                _fadingOut = false;
            }
        }
        else _fade = MathF.Max(0, _fade - dt / 0.55f);
    }

    private void DrawFade()
    {
        if (_fade <= 0.001f) return;
        float a = Ease.InOutSine(_fade);
        var warm = Palette.Lerp(Palette.Parchment, Palette.LanternHot, 0.35f);
        Raylib.DrawRectangle(-200, -200, (int)Ui.W + 400, (int)Ui.H + 400, Palette.Alpha(warm, a));
    }

    // ───────────────────────── overlays & notices ─────────────────────────

    public T Open<T>(T overlay) where T : Overlay
    {
        overlay.Attach(this);
        _overlays.Add(overlay);
        return overlay;
    }

    public bool HasOverlay => _overlays.Count > 0;

    /// <summary>The overlay on top, if any.</summary>
    public Overlay? TopOverlay => _overlays.LastOrDefault(o => !o.Closed);

    /// <summary>Closes every overlay and pending card (capture runs).</summary>
    public void DismissAll() { _overlays.Clear(); _cards.Clear(); }
    public bool HasOverlayOf<T>() => _overlays.Any(o => o is T && !o.Closed);

    private void DrainNotices()
    {
        foreach (var n in Session!.DrainNotices())
        {
            if (n.Kind is NoticeKind.Story or NoticeKind.Unlock or NoticeKind.Arrival || n.Text.Length > 260) _cards.Enqueue(n);
            else Toasts.Add(n.Title, n.Text, n.Kind);
            Audio.Play(n.Kind switch
            {
                NoticeKind.Birth => "hatch",
                NoticeKind.Discovery => "discover",
                NoticeKind.Essence => "shimmer",
                NoticeKind.Story or NoticeKind.Unlock or NoticeKind.Arrival => "story",
                _ => "reading",
            }, 0.7f);
        }
    }

    /// <summary>Shows an action's result: a story card when it's a moment, otherwise toasts.</summary>
    public void Show(ActionResult r, string title = "")
    {
        if (r.Lines.Count == 0) return;
        if (r.Lines.Any(l => l.Kind == LineKind.Resolved))
        {
            var lines = r.Lines.Where(l => l.Kind == LineKind.Resolved).Select(l => l.Text).ToList();
            Audio.Play("resolved");
            Open(new StoryCard(lines[0], string.Join("\n\n", lines.Skip(1)), StoryCard.Style.Resolution));
            return;
        }
        if (r.Lines.Any(l => l.Kind == LineKind.NearMiss)) Audio.Play("nearmiss");
        else if (r.Lines.Any(l => l.Kind == LineKind.NewReading)) Audio.Play("reading");
        else if (r.Lines.Any(l => l.Kind == LineKind.Discovery)) Audio.Play("discover", 0.8f);
        else if (r.Lines.All(l => l.Kind == LineKind.Soft)) Audio.Play("hm");
        if (r.Lines.Any(l => l.Kind == LineKind.Term)) Audio.Play("term", 0.8f);
        var important = r.Lines.Where(l => l.Kind is LineKind.NewReading or LineKind.NearMiss).ToList();
        if (important.Count > 0)
        {
            Open(new ReadingCard(title, r.Lines));
            return;
        }
        foreach (var l in r.Lines) Toasts.Add("", l.Text, NoticeKind.Info, l.Kind);
    }

    // ───────────────────────── game lifecycle ─────────────────────────

    public void NewGame(ulong? seed = null)
    {
        Session = GameSession.NewGame(Content, seed);
        Open(new HelpCard()); // waits underneath the prologue
        Open(new StoryCard(Content.Config.Title, Content.Config.Prologue, StoryCard.Style.Prologue));
        Go(new HubScene());
    }

    public bool LoadGame()
    {
        try { Session = Store.Load(Slot, Content); return true; }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or InvariantViolationException)
        {
            Toasts.Add("That save couldn't be read", ex.Message, NoticeKind.Info);
            return false;
        }
    }

    public void SaveQuietly()
    {
        if (Session is null) return;
        try { Store.Save(Slot, Session); }
        catch (IOException ex) { Toasts.Add("Couldn't save", ex.Message, NoticeKind.Info); }
    }

    private string SettingsPath => Path.Combine(Store.Directory, "settings.json");

    private void LoadSettings()
    {
        try { if (File.Exists(SettingsPath)) Settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new(); }
        catch (JsonException) { Settings = new(); }
    }

    public void SaveSettings()
    {
        if (Capture is not null) return;
        try { File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true })); }
        catch (IOException) { }
    }

    public void ApplySettings()
    {
        Audio.Master = Capture is null ? Settings.Master : 0; Audio.MusicVolume = Settings.Music; Audio.SfxVolume = Settings.Sfx; Audio.AmbienceVolume = Settings.Ambience;
        Renderer.ReduceMotion = Settings.ReduceMotion;
        Ui.ReduceMotion = Settings.ReduceMotion;
    }
}
