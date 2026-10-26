using System.Numerics;
using Atrium.Core.Runtime;
using Atrium.Diorama.App;
using Atrium.Diorama.Overlays;
using Atrium.Diorama.Render;
using Atrium.Diorama.UI;
using Raylib_cs;

namespace Atrium.Diorama.Scenes;

/// <summary>
/// Base for the four rooms: a small 3D interior seen from a fixed three-quarter camera that sways gently with the
/// mouse. Furniture is clickable through hotspots (hover lifts and rims it, a caption names it low on the screen);
/// detailed work happens on parchment cards. Room work cards sit on the left (<see cref="Desk"/>); the right edge is
/// kept clear for toasts and the top-right for the corner buttons.
/// </summary>
public abstract class InteriorScene : Scene, ISatchelAware
{
    protected abstract string LocaleId { get; }
    public override string? Locale => LocaleId;
    public override string Mood => LocaleId;
    public override Color Backdrop => Palette.Hex(0x3A2E2A);

    protected Vector3 CamTarget = new(0, 1.5f, 0);
    protected float CamYaw = 0.32f, CamPitch = 0.36f, CamDist = 12.5f, CamFov = 36;
    private Vector2 _sway;
    private readonly List<Model> _owned = new();
    private Part _shell;
    private ModelAsset? _kit;
    private readonly Dictionary<string, Spot> _spots = new();
    private float _captionA;
    private string _caption = "", _captionSub = "";

    /// <summary>Where a room's working card goes: left of the room, clear of toasts, the satchel and the clock.</summary>
    protected static readonly Rectangle Desk = new(28, 214, 640, 572);

    /// <summary>A clickable piece of furniture. Rooms may change Name/Hint/Enabled as things change.</summary>
    protected sealed class Spot
    {
        public string Id = "", Name = "", Hint = "";
        public BoundingBox Box;
        public bool Enabled = true;
        /// <summary>0..1 hover glow, for passing to <see cref="Renderer.Draw"/> as a highlight.</summary>
        public float Glow;
        /// <summary>A little hop when clicked; add <c>Lift.Value</c> to the prop's height.</summary>
        public Spring Lift = new(0, 260, 14);
    }

    /// <summary>The hotspot under the mouse, if any.</summary>
    protected string? Hovered { get; private set; }

    protected Color Accent => Palette.Locale(LocaleId);

    public override void Enter()
    {
        _kit = ItemArt.RoomModels.Contains(LocaleId) ? R.Import(Path.Combine(AppContext.BaseDirectory, "models", LocaleId + "-room.glb")) : null;
        _shell = Prop("shell", BuildShell);
        var r = S.Enter(LocaleId);
        var intro = r.Lines.Where(l => l.Kind == LineKind.Narration).Select(l => l.Text).ToList();
        if (intro.Count > 0) App.Open(new StoryCard(App.Content.Locales[LocaleId].Name, string.Join("\n\n", intro), StoryCard.Style.Story));
        foreach (var l in r.Lines.Where(l => l.Kind != LineKind.Narration)) App.Toasts.Add("", l.Text, NoticeKind.Info, l.Kind);
        Build();
        UpdateCamera(1);
    }

    public override void Exit()
    {
        foreach (var m in _owned) Raylib.UnloadModel(m);
        _owned.Clear();
        _spots.Clear();
    }

    protected Model Own(Model m) { _owned.Add(m); return m; }

    /// <summary>
    /// One of the room's fixed models: the part named <paramref name="key"/> in the room's imported kit
    /// (models/LOCALE-room.glb, built by tools/blender in the same frame as the built-in shape it replaces), else the
    /// built-in shape. The renderer keeps kits for the session, so only built-in shapes are the room's to unload.
    /// </summary>
    protected Part Prop(string key, Func<MeshBuilder> builtIn) =>
        _kit is { } kit && kit.Has(key) ? kit.Part(key) : Own(builtIn().Model(R.Shaders.Toon));

    /// <summary>Whether the room's kit supplies a part (rooms fit anything the content sizes, like the bookcases).</summary>
    protected bool FromKit(string key) => _kit?.Has(key) == true;

    /// <summary>Registers (or replaces) a hotspot covering the box from <paramref name="min"/> to <paramref name="max"/>.</summary>
    protected Spot AddSpot(string id, Vector3 min, Vector3 max, string name, string hint)
    {
        var s = new Spot { Id = id, Box = new BoundingBox(Vector3.Min(min, max), Vector3.Max(min, max)), Name = name, Hint = hint };
        _spots[id] = s;
        return s;
    }

    protected Spot SpotOf(string id) => _spots[id];
    protected bool HasSpot(string id) => _spots.ContainsKey(id);
    protected void RemoveSpot(string id) => _spots.Remove(id);

    /// <summary>The hover rim colour for a hotspot (the locale accent, scaled by its glow), or null.</summary>
    protected Color? Rim(string id) => _spots.TryGetValue(id, out var s) && s.Glow > 0.01f ? Palette.Mul(Accent, s.Glow * 0.9f) : null;

    /// <summary>Clicked a hotspot. The click is already consumed and the spot has hopped.</summary>
    protected virtual void OnSpot(string id) { }

    /// <summary>Clicks a hotspot by id, as if by hand (scripted captures).</summary>
    public bool Activate(string id)
    {
        if (!_spots.TryGetValue(id, out var s) || !s.Enabled) return false;
        s.Lift.Kick(R.ReduceMotion ? 0 : 2.2f);
        OnSpot(id);
        return true;
    }

    /// <summary>The hotspot ids in this room (scripted captures).</summary>
    public IEnumerable<string> SpotIds => _spots.Where(kv => kv.Value.Enabled).Select(kv => kv.Key);

    private void PickSpots(float dt)
    {
        string? hov = null;
        if (!Ui.WantsMouse && !App.HasOverlay)
        {
            var ray = MouseRay();
            float best = float.MaxValue;
            foreach (var s in _spots.Values)
            {
                if (!s.Enabled) continue;
                var hit = Raylib.GetRayCollisionBox(ray, s.Box);
                if (hit.Hit && hit.Distance < best) { best = hit.Distance; hov = s.Id; }
            }
        }
        if (hov is not null && hov != Hovered) App.Audio.Play("tock", 0.3f, 1.35f);
        Hovered = hov;
        foreach (var s in _spots.Values)
        {
            s.Glow = Ease.Approach(s.Glow, s.Id == hov ? 1 : 0, 12, dt);
            s.Lift.Update(dt);
        }
        if (hov is not null)
        {
            var spot = _spots[hov];
            _caption = spot.Name; _captionSub = spot.Hint;
            if (Input.Click)
            {
                Input.ClickConsumed = true;
                spot.Lift.Kick(R.ReduceMotion ? 0 : 2.2f);
                OnSpot(hov);
            }
        }
        _captionA = Ease.Approach(_captionA, hov is null ? 0 : 1, 10, dt);
    }

    /// <summary>The handwritten name of the hovered furniture, low on the screen (never floating over the 3D).</summary>
    private void DrawCaption()
    {
        if (_captionA < 0.02f || _caption.Length == 0) return;
        float a = _captionA;
        var lines = _captionSub.Length > 0 ? Text.Wrap(Ui.F.Italic, _captionSub, 21, 560) : new List<string>();
        float h = 52 + lines.Count * 24;
        var r = new Rectangle(Ui.W / 2 - 320, Ui.H - 136 - h, 640, h);
        Raylib.DrawRectangleRounded(r, 0.3f, 8, Palette.Alpha(Palette.Parchment, 0.88f * a));
        Raylib.DrawRectangleRoundedLinesEx(r, 0.3f, 8, 1.5f, Palette.Alpha(Accent, 0.8f * a));
        Text.DrawCentered(Ui.F.Hand, _caption, new Vector2(Ui.W / 2, r.Y + 24), 32, Palette.Alpha(Palette.Ink, a));
        for (int i = 0; i < lines.Count; i++)
            Text.DrawCentered(Ui.F.Italic, lines[i], new Vector2(Ui.W / 2, r.Y + 54 + i * 24), 21, Palette.Alpha(Palette.InkSoft, a));
    }

    /// <summary>Builds the room's own furniture and props.</summary>
    protected abstract void Build();

    /// <summary>The walls, floor and window shared by every room, tinted by the locale.</summary>
    protected virtual MeshBuilder BuildShell()
    {
        var m = new MeshBuilder();
        var floor = Palette.Hex(0x9A7654);
        for (int i = -5; i <= 5; i++)
            m.Box(new Vector3(i * 1.0f, -0.1f, 0), new Vector3(0.98f, 0.2f, 11f), Palette.Mul(floor, 0.92f + (i & 1) * 0.08f));
        var wall = Palette.Lerp(Palette.Hex(0xD8C8AA), Accent, 0.08f);
        m.Box(new Vector3(0, 3.2f, -5.6f), new Vector3(11.2f, 6.6f, 0.4f), wall);              // back wall
        m.Box(new Vector3(-5.6f, 3.2f, 0), new Vector3(0.4f, 6.6f, 11.2f), Palette.Mul(wall, 0.92f)); // left wall
        m.Box(new Vector3(0, 0.35f, -5.35f), new Vector3(11f, 0.7f, 0.12f), Palette.Mul(Palette.Bark, 1.1f)); // skirting
        // a window on the left wall letting in the courtyard's light
        m.Box(new Vector3(-5.38f, 3.6f, -1.5f), new Vector3(0.1f, 2.4f, 1.8f), Palette.Hex(0xF4E2B8));
        m.Box(new Vector3(-5.33f, 3.6f, -1.5f), new Vector3(0.06f, 2.5f, 0.1f), Palette.Bark);
        m.Box(new Vector3(-5.33f, 3.6f, -1.5f), new Vector3(0.06f, 0.1f, 1.9f), Palette.Bark);
        // a rug in the locale's colour
        m.Box(new Vector3(0.6f, 0.02f, 1.2f), new Vector3(5.5f, 0.03f, 3.6f), Palette.Lerp(Accent, Palette.Parchment, 0.45f));
        return m;
    }

    protected void UpdateCamera(float dt)
    {
        var target = R.ReduceMotion ? Vector2.Zero : (Input.Mouse - new Vector2(Ui.W / 2, Ui.H / 2)) / new Vector2(Ui.W / 2, Ui.H / 2);
        _sway = Vector2.Lerp(_sway, target, MathF.Min(1, dt * 2.5f));
        Camera = Orbit(CamTarget, CamYaw - _sway.X * 0.035f, CamPitch + _sway.Y * 0.02f, CamDist, CamFov);
    }

    /// <summary>Warm interior light: the window, plus a lamp or two the room adds.</summary>
    protected virtual void Light()
    {
        var l = R.Light;
        l.SunDir = Vector3.Normalize(new Vector3(-0.9f, 0.7f, 0.3f));
        l.Sun = Palette.Hex(0xFFE3B8);
        l.Sky = Palette.Hex(0xD9C2A0);
        l.Ground = Palette.Hex(0x7A5E4A);
        l.Fog = Backdrop;
        l.FogNear = 18; l.FogFar = 46;
        l.Night = 0.3f;
        l.Lamps.Clear();
    }

    public override void Update(float dt)
    {
        Light();
        UpdateCamera(dt);
        PickSpots(dt);
        Tick(dt);
    }

    public sealed override void Draw3D()
    {
        R.Draw(_shell, Matrix4x4.Identity, Color.White, outlineWidth: 1.4f);
        DrawRoom();
    }

    /// <summary>The room's furniture, props and particles (inside the 3D pass).</summary>
    protected virtual void DrawRoom() { }

    public sealed override void DrawUI()
    {
        DrawRoomUI();
        DrawCaption();
    }

    /// <summary>The room's interface: its title and any working card. The hover caption is drawn after it.</summary>
    protected abstract void DrawRoomUI();

    /// <summary>Per-frame room logic (picking, animation).</summary>
    protected virtual void Tick(float dt) { }

    public override bool Back()
    {
        App.Go(new HubScene());
        return true;
    }

    public virtual bool UseItem(ItemInstance item) => false;

    protected Ray MouseRay() => Raylib.GetScreenToWorldRay(Input.ScreenMouse, Camera);

    /// <summary>A labelled heading in the top-left under the clock.</summary>
    protected void Title(string name, string sub)
    {
        Text.Draw(Ui.F.Hand, name, new Vector2(30, 116), 44, Palette.Parchment);
        Text.Draw(Ui.F.Italic, sub, new Vector2(32, 164), 21, Palette.Alpha(Palette.Parchment, 0.8f));
    }
}
