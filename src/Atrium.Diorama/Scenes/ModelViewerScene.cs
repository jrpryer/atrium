using System.Numerics;
using Atrium.Diorama.App;
using Atrium.Diorama.Render;
using Atrium.Diorama.UI;
using Raylib_cs;

namespace Atrium.Diorama.Scenes;

/// <summary>
/// A turntable for inspecting a model (`--view file.glb`, or `view` in a capture script). By default it shows the
/// model as the game does: through the import step, with cel bands and ink. Tab switches to the file as it is
/// (raylib's default shader, no import fixes), the look test for raw generated meshes.
/// </summary>
public sealed class ModelViewerScene : Scene
{
    private readonly string _path;
    private Model _model;
    private ModelAsset? _asset;
    private bool _loaded, _plain;
    private Vector3 _centre;
    private float _radius = 5, _yaw = 0.7f, _pitch = 0.5f, _dist = 12;
    private string _info = "";

    public ModelViewerScene(string path) => _path = path;

    public override bool ShowHud => false;
    public override string Mood => "title";
    public override Color Backdrop => Palette.Hex(0x7A7A7A);

    public override unsafe void Enter()
    {
        if (!File.Exists(_path)) { _info = $"Can't find {_path}"; Console.WriteLine("viewer: " + _info); return; }
        _asset = ModelAsset.TryLoad(_path);
        _model = _asset?.Model ?? Raylib.LoadModel(_path);
        _plain = _asset is null;
        _loaded = true;
        var bb = Raylib.GetModelBoundingBox(_model);
        _centre = (bb.Min + bb.Max) / 2;
        _radius = MathF.Max(0.001f, Vector3.Distance(bb.Min, bb.Max) / 2);
        _dist = _radius / MathF.Sin(35 * MathF.PI / 360) * 1.1f; // the whole bounding sphere fits the 35° view
        long verts = 0, tris = 0;
        for (int i = 0; i < _model.MeshCount; i++) { verts += _model.Meshes[i].VertexCount; tris += _model.Meshes[i].TriangleCount; }
        var size = bb.Max - bb.Min;
        _info = $"{Path.GetFileName(_path)}  ·  {_model.MeshCount} mesh{(_model.MeshCount == 1 ? "" : "es")}  ·  {_model.MaterialCount} materials  ·  "
                + $"{verts:N0} vertices  ·  {tris:N0} triangles  ·  size {size.X:0.##} × {size.Y:0.##} × {size.Z:0.##}";
        Console.WriteLine("viewer: " + _info);
    }

    public override void Exit()
    {
        if (_asset is not null) _asset.Dispose();
        else if (_loaded) Raylib.UnloadModel(_model);
    }

    public override void Update(float dt)
    {
        if (Input.RightDown) _yaw -= Input.RightDelta.X * 0.006f;
        else if (!R.ReduceMotion) _yaw += dt * 0.25f;
        if (Input.KeyDown(KeyboardKey.A) || Input.KeyDown(KeyboardKey.Left)) _yaw += dt * 1.1f;
        if (Input.KeyDown(KeyboardKey.D) || Input.KeyDown(KeyboardKey.Right)) _yaw -= dt * 1.1f;
        if (Input.KeyDown(KeyboardKey.W) || Input.KeyDown(KeyboardKey.Up)) _pitch = MathF.Min(1.4f, _pitch + dt * 0.8f);
        if (Input.KeyDown(KeyboardKey.S) || Input.KeyDown(KeyboardKey.Down)) _pitch = MathF.Max(-0.2f, _pitch - dt * 0.8f);
        if (Input.KeyPressed(KeyboardKey.Tab) && _asset is not null) _plain = !_plain;
        if (Input.Wheel != 0) _dist = Math.Clamp(_dist - Input.Wheel * _radius * 0.15f, _radius * 0.6f, _radius * 6);
        Camera = Orbit(_centre, _yaw, _pitch, _dist, 35);
    }

    /// <summary>Sets the turntable angle directly (capture scripts).</summary>
    public void Pose(float yaw, float pitch) { _yaw = yaw; _pitch = pitch; }

    /// <summary>Shows the file as it is (true) or as the game draws it (false); capture scripts.</summary>
    public void Plain(bool plain) => _plain = plain || _asset is null;

    public override void Draw3D()
    {
        if (!_loaded) return;
        if (_plain || _asset is null) { Raylib.DrawModel(_model, Vector3.Zero, 1, Color.White); return; }
        float width = Math.Clamp(1.6f * 12 / _dist, 0.8f, 2.2f); // ink thins as the model fills the view less
        R.Draw(_asset.PartExcept("glass", "canopy"), Matrix4x4.Identity, Color.White, outlineWidth: width);
        R.Draw(_asset.Part("canopy"), Matrix4x4.Identity, Color.White, sway: 0.25f, outlineWidth: width);
        R.Draw(_asset.Part("glass"), Matrix4x4.Identity, Color.White, emissive: 0.2f, outline: false);
    }

    public override void DrawUI()
    {
        Text.Draw(Ui.F.Italic, _info, new Vector2(24, Ui.H - 40), 20, Palette.Parchment);
        string look = _asset is null ? "the file as it is (it didn't import)" : _plain ? "the file as it is" : "as the game draws it";
        Text.Draw(Ui.F.Italic, $"Showing {look} · Tab to switch · right-drag or A/D to turn · W/S to tilt · scroll to zoom · Esc to quit", new Vector2(24, 20), 19, Palette.Alpha(Palette.Parchment, 0.8f));
    }

    public override bool Back()
    {
        App.Quitting = true;
        return true;
    }
}
