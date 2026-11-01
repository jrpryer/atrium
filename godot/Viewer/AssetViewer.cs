using Godot;
using Environment = Godot.Environment;

namespace AtriumGodot;

/// <summary>
/// The asset viewer (the project's main scene for now). It finds every .glb under res://art/models and every .png
/// under res://art/textures at run time, shows one model at a time with an orbit camera, and lists the model's
/// meshes (parts) and markers (empties). A kit made of "piece_role" parts can be focused piece by piece.
///
/// Keys: Left/Right model (or texture) · Up/Down focus a kit piece · M markers · Tab models/textures page ·
/// R reset view · Esc quit. Mouse: left-drag orbit, wheel zoom.
///
/// <c>-- --tour [--hold=N]</c> shows every model from the front, three-quarter (with markers), side and back, then
/// each kit piece, then every texture, N frames each (default 2), and quits: run it with Godot's
/// <c>--write-movie frames/tour.png</c> to record the frames.
/// </summary>
public partial class AssetViewer : Node3D
{
    private const float DefaultPitch = 20f;

    private readonly List<string> _models = new();
    private readonly List<string> _textures = new();

    private Camera3D _camera = null!;
    private Node3D _stage = null!;
    private Node3D _gizmos = null!;
    private Label _text = null!;
    private Control _texturePage = null!;
    private TextureRect _textureSingle = null!;
    private TextureRect _textureTiled = null!;
    private StandardMaterial3D _gizmoMaterial = null!;

    private int _model, _texture, _focus = -1;
    private bool _showMarkers, _onTextures;
    private Node? _instance;
    private ModelInfo? _info;
    private List<string> _pieces = new();

    private float _yaw, _pitch = DefaultPitch, _zoom = 1f, _radius = 1f;
    private Vector3 _target;

    private List<(string Label, Action Apply)>? _tour;
    private int _tourStep, _tourWait, _hold = 2, _frame;

    public override void _Ready()
    {
        BuildWorld();
        BuildUi();
        _models.AddRange(Assets.Find(Assets.ModelsDir, ".glb"));
        _textures.AddRange(Assets.Find(Assets.TexturesDir, ".png"));
        GD.Print($"Viewer: {_models.Count} models, {_textures.Count} textures. {Game.Instance?.Summary}");

        var args = OS.GetCmdlineUserArgs();
        foreach (var a in args)
            if (a.StartsWith("--hold=", StringComparison.Ordinal) && int.TryParse(a["--hold=".Length..], out var h)) _hold = Math.Max(1, h);
        if (args.Contains("--tour")) _tour = BuildTour();

        if (_models.Count > 0) ShowModel(0);
        else _text.Text = $"No .glb files under {Assets.ModelsDir}.";
    }

    // ───── scene ─────

    private void BuildWorld()
    {
        var env = new Environment
        {
            BackgroundMode = Environment.BGMode.Color,
            BackgroundColor = new Color(0.20f, 0.22f, 0.26f),
            AmbientLightSource = Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.80f, 0.82f, 0.90f),
            AmbientLightEnergy = 0.5f,
        };
        AddChild(new WorldEnvironment { Environment = env });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, 35, 0), ShadowEnabled = true, LightEnergy = 1.0f });
        _camera = new Camera3D { Fov = 40f, Current = true };
        AddChild(_camera);
        _stage = new Node3D { Name = "Stage" };
        AddChild(_stage);
        _gizmos = new Node3D { Name = "Markers", Visible = false };
        AddChild(_gizmos);
        _gizmoMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1f, 0.55f, 0.15f),
            NoDepthTest = true,
        };
    }

    private void BuildUi()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        _texturePage = new Control { Visible = false };
        layer.AddChild(_texturePage);
        _texturePage.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var bg = new ColorRect { Color = new Color(0.12f, 0.12f, 0.14f) };
        _texturePage.AddChild(bg);
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _textureSingle = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        _textureTiled = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Tile };
        _texturePage.AddChild(_textureSingle);
        _texturePage.AddChild(_textureTiled);

        _text = new Label { Position = new Vector2(12, 8), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _text.AddThemeFontSizeOverride("font_size", 14);
        _text.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
        _text.AddThemeConstantOverride("outline_size", 4);
        layer.AddChild(_text);
    }

    private Vector2 ViewSize => GetViewport().GetVisibleRect().Size;

    // ───── models ─────

    private void ShowModel(int index)
    {
        _model = (index % _models.Count + _models.Count) % _models.Count;
        if (_instance is not null)
        {
            _stage.RemoveChild(_instance);
            _instance.QueueFree();
        }
        var scene = GD.Load<PackedScene>(_models[_model]);
        _instance = scene?.Instantiate();
        if (_instance is null)
        {
            _info = null;
            _text.Text = $"Could not load {_models[_model]}.";
            return;
        }
        _stage.AddChild(_instance);
        _info = ModelInfo.Of(_instance);
        _pieces = _info.Pieces();
        _focus = -1;
        BuildGizmos();
        ResetView();
    }

    private void Focus(int focus)
    {
        if (_info is null) return;
        _focus = _pieces.Count == 0 ? -1 : Math.Clamp(focus, -1, _pieces.Count - 1);
        Frame();
        UpdateCamera();
        UpdateText();
    }

    private void ResetView()
    {
        _yaw = 0f;
        _pitch = DefaultPitch;
        _zoom = 1f;
        Frame();
        UpdateCamera();
        UpdateText();
    }

    private void Frame()
    {
        if (_info is null) return;
        var box = (_focus < 0 ? _info.Bounds() : _info.Bounds(_info.PieceMeshes(_pieces[_focus]))) ?? new Aabb(new Vector3(-0.5f, 0, -0.5f), Vector3.One);
        _target = box.GetCenter();
        _radius = Math.Max(box.Size.Length() / 2f, 0.1f);
    }

    private void UpdateCamera()
    {
        var dist = _radius / Mathf.Sin(Mathf.DegToRad(_camera.Fov / 2f)) * _zoom;
        float yaw = Mathf.DegToRad(_yaw), pitch = Mathf.DegToRad(_pitch);
        var offset = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * dist;
        _camera.Near = Math.Max(0.01f, dist * 0.005f);
        _camera.Far = dist * 4f + _radius * 8f;
        _camera.Position = _target + offset;
        _camera.LookAt(_target, Vector3.Up);
    }

    private void BuildGizmos()
    {
        foreach (var child in _gizmos.GetChildren()) child.QueueFree();
        if (_info is null) return;
        var whole = _info.Bounds();
        var size = Math.Clamp((whole?.Size.Length() ?? 1f) * 0.006f, 0.02f, 0.4f);
        var sphere = new SphereMesh { Radius = size, Height = size * 2f, RadialSegments = 12, Rings = 6, Material = _gizmoMaterial };
        foreach (var marker in _info.Markers)
        {
            var at = marker.GlobalPosition;
            _gizmos.AddChild(new MeshInstance3D { Mesh = sphere, Position = at });
            _gizmos.AddChild(new Label3D
            {
                Text = marker.Name,
                Position = at + new Vector3(0, size * 2.5f, 0),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                FontSize = 32,
                PixelSize = size * 2.5f / 32f,
                OutlineSize = 8,
                Modulate = new Color(1f, 0.85f, 0.6f),
            });
        }
    }

    private void UpdateText()
    {
        if (_onTextures) { UpdateTextureText(); return; }
        if (_info is null) return;
        var file = System.IO.Path.GetFileName(_models[_model]);
        var piece = _focus < 0 ? null : _pieces[_focus];
        var meshes = piece is null ? _info.Meshes : _info.PieceMeshes(piece).ToList();
        var markers = piece is null ? _info.Markers : _info.Markers.Where(m => m.Name.ToString().StartsWith(piece, StringComparison.Ordinal)).ToList();
        var box = piece is null ? _info.Bounds() : _info.Bounds(meshes);
        var size = box is { } b ? $"{b.Size.X:0.##} × {b.Size.Y:0.##} × {b.Size.Z:0.##} m" : "no meshes";
        var focus = piece is null ? (_pieces.Count > 0 ? $"whole kit, {_pieces.Count} pieces (Up/Down to focus one)" : "whole model") : $"piece {_focus + 1}/{_pieces.Count}: {piece}";
        _text.Size = new Vector2(ViewSize.X - 24, 0);
        _text.Text =
            $"Model {_model + 1}/{_models.Count}: {file}  —  {focus}\n" +
            $"{size} · {meshes.Count} meshes · {markers.Count} markers{(_showMarkers ? " (shown)" : "")} · {Game.Instance?.Summary ?? "no Game autoload"}\n" +
            $"Meshes: {string.Join(", ", meshes.Select(m => m.Name))}\n" +
            $"Markers: {(markers.Count == 0 ? "none" : string.Join(", ", markers.Select(m => m.Name)))}\n" +
            "Left/Right model · Up/Down piece · M markers · Tab textures · R reset · Esc quit · drag to orbit · wheel to zoom";
    }

    // ───── textures ─────

    private void ShowTextures(bool on)
    {
        _onTextures = on && _textures.Count > 0;
        _texturePage.Visible = _onTextures;
        _stage.Visible = !_onTextures;
        _gizmos.Visible = !_onTextures && _showMarkers;
        if (_onTextures) ShowTexture(_texture);
        else UpdateText();
    }

    private void ShowTexture(int index)
    {
        _texture = (index % _textures.Count + _textures.Count) % _textures.Count;
        var tex = GD.Load<Texture2D>(_textures[_texture]);
        _textureSingle.Texture = tex;
        _textureTiled.Texture = tex;
        var view = ViewSize;
        const float top = 96f;
        var side = Math.Min(view.X / 2f - 24f, view.Y - top - 16f);
        _textureSingle.Position = new Vector2(16, top);
        _textureSingle.Size = new Vector2(side, side);
        if (tex is not null)
        {
            // Two-by-two repeats, scaled to the same box, to show whether the texture tiles without seams.
            var twice = tex.GetSize() * 2f;
            _textureTiled.Size = twice;
            _textureTiled.Scale = Vector2.One * (side / Math.Max(twice.X, twice.Y));
            _textureTiled.Position = new Vector2(view.X / 2f + 8f, top);
        }
        UpdateTextureText();
    }

    private void UpdateTextureText()
    {
        var path = _textures[_texture];
        var tex = GD.Load<Texture2D>(path);
        var import = new ConfigFile();
        var settings = import.Load(path + ".import") == Error.Ok
            ? $"import: compress/mode={import.GetValue("params", "compress/mode", -1)}, high_quality={import.GetValue("params", "compress/high_quality", false)}, mipmaps={import.GetValue("params", "mipmaps/generate", false)}"
            : "import settings not readable (exported build)";
        _text.Size = new Vector2(ViewSize.X - 24, 0);
        _text.Text =
            $"Texture {_texture + 1}/{_textures.Count}: {System.IO.Path.GetFileName(path)}  —  {tex?.GetWidth()} × {tex?.GetHeight()} px · {settings}\n" +
            "Left: as imported · Right: repeated 2 × 2\n" +
            "Left/Right texture · Tab models · Esc quit";
    }

    // ───── input ─────

    public override void _UnhandledInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey { Pressed: true, Echo: false } key:
                switch (key.Keycode)
                {
                    case Key.Right or Key.Pagedown:
                        if (_onTextures) ShowTexture(_texture + 1); else if (_models.Count > 0) ShowModel(_model + 1);
                        break;
                    case Key.Left or Key.Pageup:
                        if (_onTextures) ShowTexture(_texture - 1); else if (_models.Count > 0) ShowModel(_model - 1);
                        break;
                    case Key.Up: if (!_onTextures) Focus(_focus + 1); break;
                    case Key.Down: if (!_onTextures) Focus(_focus - 1); break;
                    case Key.M:
                        _showMarkers = !_showMarkers;
                        _gizmos.Visible = _showMarkers && !_onTextures;
                        UpdateText();
                        break;
                    case Key.Tab or Key.T: ShowTextures(!_onTextures); break;
                    case Key.R: if (!_onTextures) ResetView(); break;
                    case Key.Escape: GetTree().Quit(); break;
                }
                break;
            case InputEventMouseButton { Pressed: true } button when !_onTextures:
                if (button.ButtonIndex == MouseButton.WheelUp) _zoom = Math.Max(0.05f, _zoom * 0.9f);
                else if (button.ButtonIndex == MouseButton.WheelDown) _zoom = Math.Min(6f, _zoom * 1.1f);
                UpdateCamera();
                break;
            case InputEventMouseMotion motion when !_onTextures && (motion.ButtonMask & MouseButtonMask.Left) != 0:
                _yaw -= motion.Relative.X * 0.3f;
                _pitch = Math.Clamp(_pitch + motion.Relative.Y * 0.3f, -85f, 85f);
                UpdateCamera();
                break;
        }
    }

    // ───── tour (for --write-movie) ─────

    private List<(string, Action)> BuildTour()
    {
        var steps = new List<(string, Action)>();
        for (int i = 0; i < _models.Count; i++)
        {
            int m = i;
            var name = Assets.FileStem(_models[m]);
            steps.Add(($"{name} front", () => { ShowModel(m); SetMarkers(false); Orbit(0f, DefaultPitch); }));
            steps.Add(($"{name} three-quarter, markers", () => { SetMarkers(true); Orbit(45f, 28f); }));
            steps.Add(($"{name} side (+X)", () => { SetMarkers(false); Orbit(90f, DefaultPitch); }));
            steps.Add(($"{name} back", () => Orbit(180f, DefaultPitch)));
            steps.Add(($"{name} pieces", () => { }));
            int pieceSlot = steps.Count - 1;
            // Pieces are only known once the model is loaded, so this step expands itself when it runs.
            steps[pieceSlot] = ($"{name} pieces", () =>
            {
                var extra = new List<(string, Action)>();
                for (int p = 0; p < _pieces.Count; p++)
                {
                    int piece = p;
                    extra.Add(($"{name} piece {_pieces[piece]}", () => { Focus(piece); Orbit(35f, 24f); }));
                }
                _tour!.InsertRange(_tourStep, extra);
            });
        }
        for (int t = 0; t < _textures.Count; t++)
        {
            int tex = t;
            steps.Add(($"texture {Assets.FileStem(_textures[tex])}", () => { ShowTextures(true); ShowTexture(tex); }));
        }
        return steps;
    }

    private void SetMarkers(bool on)
    {
        _showMarkers = on;
        _gizmos.Visible = on && !_onTextures;
        UpdateText();
    }

    private void Orbit(float yaw, float pitch)
    {
        _yaw = yaw;
        _pitch = pitch;
        UpdateCamera();
        UpdateText();
    }

    public override void _Process(double delta)
    {
        if (_tour is not null && --_tourWait <= 0)
        {
            // Steps that only expand the tour (the pieces placeholder) run without taking frames.
            while (true)
            {
                if (_tourStep >= _tour.Count)
                {
                    GD.Print($"TOUR done after {_frame} frames");
                    GetTree().Quit();
                    break;
                }
                var (label, apply) = _tour[_tourStep++];
                apply();
                if (label.EndsWith(" pieces", StringComparison.Ordinal)) continue;
                GD.Print($"TOUR frame {_frame:D5}: {label}");
                _tourWait = _hold;
                break;
            }
        }
        _frame++;
    }
}
