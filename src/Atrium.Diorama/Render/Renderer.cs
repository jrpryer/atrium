using System.Numerics;
using Raylib_cs;

namespace Atrium.Diorama.Render;

/// <summary>Draws cel-shaded, ink-outlined models, water, the falls, glows and particles; owns the post pass.</summary>
public sealed class Renderer : IDisposable
{
    public Shaders Shaders { get; } = new();
    public Lighting Light { get; } = new();
    public Texture2D Dot { get; }
    public Texture2D Grain { get; }
    public float Time { get; set; }
    public bool ReduceMotion { get; set; }
    /// <summary>When set, the backdrop is a vertical gradient from this colour down to the fog colour.</summary>
    public Color? SkyTop { get; set; }

    private RenderTexture2D _target;
    private int _w, _h;
    private Camera3D _cam;
    private readonly Dictionary<string, ModelAsset?> _imported = new();
    private readonly Dictionary<string, Texture2D?> _textures = new();

    public Renderer()
    {
        var img = Raylib.GenImageGradientRadial(64, 64, 0f, new Color(255, 255, 255, 255), new Color(255, 255, 255, 0));
        Dot = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        Raylib.SetTextureFilter(Dot, TextureFilter.Bilinear);
        var noise = Raylib.GenImagePerlinNoise(256, 256, 0, 0, 6f);
        Grain = Raylib.LoadTextureFromImage(noise);
        Raylib.UnloadImage(noise);
        Raylib.SetTextureFilter(Grain, TextureFilter.Bilinear);
    }

    /// <summary>Starts the 3D pass into the off-screen target (recreated when the window resizes).</summary>
    public void Begin3D(Camera3D cam, Color backdrop)
    {
        int w = Raylib.GetRenderWidth(), h = Raylib.GetRenderHeight();
        if (w != _w || h != _h || _target.Id == 0)
        {
            if (_target.Id != 0) Raylib.UnloadRenderTexture(_target);
            _target = Raylib.LoadRenderTexture(w, h);
            Raylib.SetTextureFilter(_target.Texture, TextureFilter.Bilinear);
            _w = w; _h = h;
        }
        _cam = cam;
        Shaders.Apply(Light, cam, Time, new Vector2(w, h));
        Raylib.BeginTextureMode(_target);
        Raylib.ClearBackground(backdrop);
        if (SkyTop is { } top) Raylib.DrawRectangleGradientV(0, 0, w, h, top, backdrop);
        // raylib's default near plane (0.01 m) leaves about a centimetre of depth precision 45 m away, so decals,
        // puddles and window glass flickered against their surfaces; nothing is ever drawn closer than this
        Rlgl.SetClipPlanes(NearPlane, FarPlane);
        Raylib.BeginMode3D(cam);
    }

    /// <summary>The camera's clip planes (metres).</summary>
    public const double NearPlane = 0.4, FarPlane = 450;

    public void End3D()
    {
        Raylib.EndMode3D();
        Raylib.EndTextureMode();
    }

    /// <summary>Composites the 3D target with the warm grade, vignette and grain.</summary>
    public void Present(float grain = 0.035f)
    {
        var post = Shaders.Post;
        Shaders.Set(post, "resolution", new Vector2(_w, _h));
        Shaders.Set(post, "time", ReduceMotion ? 0f : Time);
        Shaders.Set(post, "grain", grain);
        Raylib.BeginShaderMode(post);
        Raylib.DrawTexturePro(_target.Texture, new Rectangle(0, 0, _w, -_h), new Rectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight()), Vector2.Zero, 0, Color.White);
        Raylib.EndShaderMode();
    }

    /// <summary>Draws a model (or some of its meshes) with the toon shader and, optionally, its ink outline.</summary>
    public void Draw(Part p, Matrix4x4 world, Color tint, float emissive = 0, Color? highlight = null,
                     bool outline = true, float sway = 0, float outlineWidth = 2.0f, Color? ink = null)
    {
        var s = Shaders;
        var transform = Matrix4x4.Transpose(world); // raylib's matrices are column-major
        var toon = s.Toon;
        s.Set(toon, "emissive", emissive);
        s.Set(toon, "highlight", highlight is { } h ? Palette.V3(h) : Vector3.Zero);
        s.Set(toon, "rimStrength", 1f);
        s.Set(toon, "sway", ReduceMotion ? 0f : sway);
        DrawMeshes(p, toon, transform, tint);
        if (!outline) return;
        var ol = s.Outline;
        s.Set(ol, "width", outlineWidth);
        s.Set(ol, "sway", ReduceMotion ? 0f : sway);
        Rlgl.SetCullFace(0); // cull front faces: draw the inverted hull
        DrawMeshes(p, ol, transform, ink ?? Palette.Ink);
        Rlgl.SetCullFace(1);
    }

    /// <summary>Draws a part's meshes with one shader, each in its own material's colour times the tint.</summary>
    private static unsafe void DrawMeshes(Part p, Shader shader, Matrix4x4 transform, Color tint)
    {
        var m = p.Model;
        int count = p.Meshes?.Length ?? m.MeshCount;
        for (int k = 0; k < count; k++)
        {
            int i = p.Meshes?[k] ?? k;
            var material = m.Materials[m.MeshMaterial[i]]; // a copy, so the shader swap stays here; its maps are shared
            material.Shader = shader;
            ref var albedo = ref material.Maps[(int)MaterialMapIndex.Albedo];
            var own = albedo.Color;
            albedo.Color = new Color(own.R * tint.R / 255, own.G * tint.G / 255, own.B * tint.B / 255, own.A * tint.A / 255);
            Raylib.DrawMesh(m.Meshes[i], material, transform);
            albedo.Color = own;
        }
    }

    public unsafe void DrawWater(Model m, Matrix4x4 world, Color shallow, Color deep, float murk = 0, float alpha = 0.92f, float swell = 1)
    {
        var s = Shaders;
        m.Transform = Matrix4x4.Transpose(world);
        m.Materials[0].Shader = s.Water;
        s.Set(s.Water, "deepColor", Palette.V4(deep));
        s.Set(s.Water, "murk", murk);
        s.Set(s.Water, "sparkle", ReduceMotion ? 0.3f : 1f);
        s.Set(s.Water, "swell", (ReduceMotion ? 0.2f : 1f) * swell);
        Raylib.DrawModel(m, Vector3.Zero, 1, Palette.Alpha(shallow, alpha));
    }

    public unsafe void DrawFalls(Model m, Matrix4x4 world, Color tint)
    {
        m.Transform = Matrix4x4.Transpose(world);
        m.Materials[0].Shader = Shaders.Falls;
        Rlgl.DisableBackfaceCulling();
        Raylib.DrawModel(m, Vector3.Zero, 1, tint);
        Rlgl.EnableBackfaceCulling();
    }

    /// <summary>A soft additive glow sprite (lanterns, motes, sparkles).</summary>
    public void Glow(Vector3 pos, float size, Color c)
    {
        Raylib.BeginBlendMode(BlendMode.Additive);
        Rlgl.DisableDepthMask();
        Raylib.DrawBillboard(_cam, Dot, pos, size, c);
        Rlgl.EnableDepthMask();
        Raylib.EndBlendMode();
    }

    /// <summary>A soft alpha-blended puff (mist, steam, dust).</summary>
    public void Puff(Vector3 pos, float size, Color c)
    {
        Rlgl.DisableDepthMask();
        Raylib.DrawBillboard(_cam, Dot, pos, size, c);
        Rlgl.EnableDepthMask();
    }

    /// <summary>
    /// An imported model, loaded once and kept for the session (scenes are rebuilt each visit; their models aren't).
    /// Null if the file is missing or won't import.
    /// </summary>
    public ModelAsset? Import(string path)
    {
        if (!_imported.TryGetValue(path, out var a)) _imported[path] = a = ModelAsset.TryLoad(path);
        return a;
    }

    /// <summary>
    /// A painted texture (art/textures, copied beside the game), loaded once for the session with mipmaps and
    /// trilinear filtering; null if the file is missing.
    /// </summary>
    public Texture2D? Texture(string path)
    {
        if (_textures.TryGetValue(path, out var t)) return t;
        Texture2D? loaded = null;
        if (File.Exists(path))
        {
            var tex = Raylib.LoadTexture(path);
            Raylib.GenTextureMipmaps(ref tex);
            Raylib.SetTextureFilter(tex, TextureFilter.Trilinear);
            Raylib.SetTextureWrap(tex, TextureWrap.Repeat);
            loaded = tex;
        }
        else Console.WriteLine($"textures: {path} not found; using flat colour");
        return _textures[path] = loaded;
    }

    /// <summary>Gives a built model a texture (raylib's UnloadModel leaves textures alone, so cached ones can be shared).</summary>
    public static unsafe Model WithTexture(Model m, Texture2D? t)
    {
        if (t is { } tex) m.Materials[0].Maps[(int)MaterialMapIndex.Albedo].Texture = tex;
        return m;
    }

    /// <summary>Adds a texture's light over the scene (unlit, no depth write), scaled by the tint.</summary>
    public void DrawAdditive(Part p, Matrix4x4 world, Color tint)
    {
        Raylib.BeginBlendMode(BlendMode.Additive);
        Rlgl.DisableDepthMask();
        DrawMeshes(p, Shaders.Unlit, Matrix4x4.Transpose(world), tint);
        Rlgl.EnableDepthMask();
        Raylib.EndBlendMode();
    }

    public void Dispose()
    {
        foreach (var a in _imported.Values) a?.Dispose();
        foreach (var t in _textures.Values) if (t is { } tex) Raylib.UnloadTexture(tex);
        Raylib.UnloadTexture(Dot);
        Raylib.UnloadTexture(Grain);
        if (_target.Id != 0) Raylib.UnloadRenderTexture(_target);
        Shaders.Dispose();
    }
}

/// <summary>Simple CPU particles drawn as soft sprites.</summary>
public sealed class Particles
{
    private struct P
    {
        public Vector3 Pos, Vel;
        public float Life, Max, Size, Grow, Drag, Gravity;
        public Color Color;
        public bool Additive;
    }

    private readonly List<P> _ps = new();
    private readonly Random _rng = new(7);
    public int Count => _ps.Count;

    public float R(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

    public void Emit(Vector3 pos, Vector3 vel, float life, float size, Color color, bool additive = false, float grow = 0, float drag = 0.5f, float gravity = 0)
    {
        if (_ps.Count > 1500) return;
        _ps.Add(new P { Pos = pos, Vel = vel, Life = life, Max = life, Size = size, Grow = grow, Drag = drag, Gravity = gravity, Color = color, Additive = additive });
    }

    /// <summary>A small celebratory burst (discoveries, hatchings, a settled shelf).</summary>
    public void Burst(Vector3 pos, Color color, int count = 24, float speed = 2.2f, float life = 1.1f)
    {
        for (int i = 0; i < count; i++)
        {
            var d = Vector3.Normalize(new Vector3(R(-1, 1), R(0.2f, 1.4f), R(-1, 1)));
            Emit(pos, d * R(speed * 0.4f, speed), R(life * 0.6f, life), R(0.08f, 0.18f), color, true, -0.05f, 1.6f, -1.4f);
        }
    }

    public void Update(float dt)
    {
        for (int i = _ps.Count - 1; i >= 0; i--)
        {
            var p = _ps[i];
            p.Life -= dt;
            if (p.Life <= 0) { _ps.RemoveAt(i); continue; }
            p.Vel *= MathF.Max(0, 1 - p.Drag * dt);
            p.Vel.Y += p.Gravity * dt;
            p.Pos += p.Vel * dt;
            p.Size = MathF.Max(0.01f, p.Size + p.Grow * dt);
            _ps[i] = p;
        }
    }

    public void Draw(Renderer r)
    {
        foreach (var p in _ps)
        {
            float t = p.Life / p.Max;
            float fade = MathF.Min(1, t * 3) * MathF.Min(1, (1 - t) * 6 + 0.2f);
            var c = Palette.Alpha(p.Color, p.Color.A / 255f * fade);
            if (p.Additive) r.Glow(p.Pos, p.Size, c);
            else r.Puff(p.Pos, p.Size, c);
        }
    }

    public void Clear() => _ps.Clear();
}
