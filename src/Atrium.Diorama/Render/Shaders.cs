using System.Numerics;
using Raylib_cs;

namespace Atrium.Diorama.Render;

/// <summary>The scene's lighting for one frame: sun, sky, fog and up to eight lanterns.</summary>
public sealed class Lighting
{
    public Vector3 SunDir = Vector3.Normalize(new Vector3(-0.4f, 0.8f, 0.45f)); // toward the sun
    public Color Sun = Palette.Hex(0xFFE9C4);
    public Color Sky = Palette.Hex(0xB9C7D6);
    public Color Ground = Palette.Hex(0x8A7A66);
    public Color Fog = Palette.Hex(0xE8D3B0);
    public float FogNear = 18, FogFar = 70;
    public float Night; // 0 day … 1 night, drives lantern strength
    public readonly List<(Vector3 Pos, float Radius, Color Color, float Intensity)> Lamps = new();
}

/// <summary>Loads and drives the iteration's GLSL shaders (GLSL 330, raylib's desktop default).</summary>
public sealed class Shaders : IDisposable
{
    public Shader Toon, Outline, Water, Falls, Post;
    private readonly Dictionary<(uint, string), int> _locs = new();

    public Shaders()
    {
        Toon = Load(ToonVs, ToonFs);
        Outline = Load(OutlineVs, OutlineFs);
        Water = Load(WaterVs, WaterFs);
        Falls = Load(FallsVs, FallsFs);
        Post = Raylib.LoadShaderFromMemory(PostVs, PostFs);
    }

    private static unsafe Shader Load(string vs, string fs)
    {
        var s = Raylib.LoadShaderFromMemory(vs, fs);
        s.Locs[(int)ShaderLocationIndex.MatrixModel] = Raylib.GetShaderLocation(s, "matModel");
        s.Locs[(int)ShaderLocationIndex.MatrixNormal] = Raylib.GetShaderLocation(s, "matNormal");
        s.Locs[(int)ShaderLocationIndex.VectorView] = Raylib.GetShaderLocation(s, "viewPos");
        return s;
    }

    public int Loc(Shader s, string name)
    {
        if (!_locs.TryGetValue((s.Id, name), out var l)) _locs[(s.Id, name)] = l = Raylib.GetShaderLocation(s, name);
        return l;
    }

    public void Set(Shader s, string name, float v) => Raylib.SetShaderValue(s, Loc(s, name), v, ShaderUniformDataType.Float);
    public void Set(Shader s, string name, int v) => Raylib.SetShaderValue(s, Loc(s, name), v, ShaderUniformDataType.Int);
    public void Set(Shader s, string name, Vector2 v) => Raylib.SetShaderValue(s, Loc(s, name), v, ShaderUniformDataType.Vec2);
    public void Set(Shader s, string name, Vector3 v) => Raylib.SetShaderValue(s, Loc(s, name), v, ShaderUniformDataType.Vec3);
    public void Set(Shader s, string name, Vector4 v) => Raylib.SetShaderValue(s, Loc(s, name), v, ShaderUniformDataType.Vec4);

    public unsafe void SetArray(Shader s, string name, Vector4[] v, int count)
    {
        fixed (Vector4* p = v) Raylib.SetShaderValueV(s, Loc(s, name), p, ShaderUniformDataType.Vec4, Math.Max(1, count));
    }

    private readonly Vector4[] _lampPos = new Vector4[8];
    private readonly Vector4[] _lampCol = new Vector4[8];

    /// <summary>Uploads the frame's lighting to every lit shader.</summary>
    public void Apply(Lighting l, Camera3D cam, float time, Vector2 viewport)
    {
        int n = Math.Min(8, l.Lamps.Count);
        for (int i = 0; i < n; i++)
        {
            var (p, r, c, k) = l.Lamps[i];
            _lampPos[i] = new Vector4(p, r);
            _lampCol[i] = new Vector4(Palette.V3(c), k);
        }
        foreach (var s in new[] { Toon, Water, Falls })
        {
            Set(s, "sunDir", Vector3.Normalize(l.SunDir));
            Set(s, "sunColor", Palette.V3(l.Sun));
            Set(s, "skyColor", Palette.V3(l.Sky));
            Set(s, "groundColor", Palette.V3(l.Ground));
            Set(s, "fogColor", Palette.V3(l.Fog));
            Set(s, "fogNear", l.FogNear);
            Set(s, "fogFar", l.FogFar);
            Set(s, "viewPos", cam.Position);
            Set(s, "time", time);
            Set(s, "lampCount", n);
            SetArray(s, "lampPos", _lampPos, n);
            SetArray(s, "lampCol", _lampCol, n);
        }
        Set(Outline, "viewport", viewport);
        Set(Outline, "fogColor", Palette.V3(l.Fog));
        Set(Outline, "fogNear", l.FogNear);
        Set(Outline, "fogFar", l.FogFar);
        Set(Outline, "viewPos", cam.Position);
        Set(Outline, "time", time);
    }

    public void Dispose()
    {
        foreach (var s in new[] { Toon, Outline, Water, Falls, Post }) Raylib.UnloadShader(s);
    }

    // ───────────────────────── GLSL ─────────────────────────

    private const string Common = """
        uniform vec3 sunDir; uniform vec3 sunColor; uniform vec3 skyColor; uniform vec3 groundColor;
        uniform vec3 fogColor; uniform float fogNear; uniform float fogFar; uniform vec3 viewPos; uniform float time;
        uniform vec4 lampPos[8]; uniform vec4 lampCol[8]; uniform int lampCount;
        vec3 lamps(vec3 base, vec3 n, vec3 p) {
            vec3 add = vec3(0.0);
            for (int i = 0; i < 8; i++) {
                if (i >= lampCount) break;
                vec3 d = lampPos[i].xyz - p; float dist = length(d);
                float att = clamp(1.0 - dist / lampPos[i].w, 0.0, 1.0); att *= att;
                float wrap = max(dot(n, d / max(dist, 0.001)), 0.0) * 0.6 + 0.4;
                add += base * lampCol[i].rgb * lampCol[i].a * att * wrap;
            }
            return add;
        }
        vec3 fog(vec3 c, vec3 p) {
            float f = clamp((length(viewPos - p) - fogNear) / (fogFar - fogNear), 0.0, 1.0);
            return mix(c, fogColor, f * f);
        }
        """;

    private const string ToonVs = """
        #version 330
        in vec3 vertexPosition; in vec3 vertexNormal; in vec4 vertexColor; in vec2 vertexTexCoord;
        uniform mat4 mvp; uniform mat4 matModel; uniform mat4 matNormal;
        uniform float time; uniform float sway;
        out vec3 fragPos; out vec3 fragNormal; out vec4 fragColor; out vec2 fragTexCoord;
        void main() {
            vec3 p = vertexPosition;
            // optional gentle sway for foliage and cloth (sway = 0 for stone)
            p.x += sin(time * 0.9 + vertexPosition.y * 1.7 + vertexPosition.z) * 0.035 * sway * max(vertexPosition.y, 0.0);
            vec4 wp = matModel * vec4(p, 1.0);
            fragPos = wp.xyz;
            fragNormal = normalize((matNormal * vec4(vertexNormal, 0.0)).xyz);
            fragColor = vertexColor;
            fragTexCoord = vertexTexCoord;
            gl_Position = mvp * vec4(p, 1.0);
        }
        """;

    private const string ToonFs = "#version 330\n" + Common + """
        in vec3 fragPos; in vec3 fragNormal; in vec4 fragColor; in vec2 fragTexCoord;
        uniform sampler2D texture0;
        uniform vec4 colDiffuse; uniform float emissive; uniform vec3 highlight; uniform float rimStrength;
        out vec4 finalColor;
        void main() {
            // painted colour: the texture (white for vertex-coloured meshes) x vertex colour x material colour
            vec4 texel = texture(texture0, fragTexCoord);
            if (texel.a < 0.05) discard;
            vec3 base = texel.rgb * fragColor.rgb * colDiffuse.rgb;
            vec3 n = normalize(fragNormal);
            float ndl = dot(n, normalize(sunDir));
            float band = smoothstep(-0.08, 0.06, ndl) * 0.6 + smoothstep(0.42, 0.56, ndl) * 0.4;
            vec3 amb = mix(groundColor, skyColor, n.y * 0.5 + 0.5) * 0.62;
            vec3 col = base * (amb + sunColor * band * 0.78);
            col += lamps(base, n, fragPos);
            vec3 v = normalize(viewPos - fragPos);
            float rim = pow(1.0 - max(dot(n, v), 0.0), 3.0);
            col += rim * (skyColor * 0.18 * rimStrength + highlight * 1.4);
            col += base * emissive;
            finalColor = vec4(fog(col, fragPos), colDiffuse.a * fragColor.a * texel.a);
        }
        """;

    // Inverted hull: extrude along the smooth normal (stored in the tangent) by a constant number of pixels.
    private const string OutlineVs = """
        #version 330
        in vec3 vertexPosition; in vec3 vertexNormal; in vec4 vertexTangent;
        uniform mat4 mvp; uniform mat4 matModel; uniform vec2 viewport; uniform float width; uniform float time; uniform float sway;
        out vec3 fragPos;
        void main() {
            vec3 p = vertexPosition;
            p.x += sin(time * 0.9 + vertexPosition.y * 1.7 + vertexPosition.z) * 0.035 * sway * max(vertexPosition.y, 0.0);
            vec3 sn = length(vertexTangent.xyz) > 0.01 ? vertexTangent.xyz : vertexNormal;
            vec4 clip = mvp * vec4(p, 1.0);
            vec4 clipN = mvp * vec4(p + sn * 0.05, 1.0);
            vec2 dir = normalize(clipN.xy / clipN.w - clip.xy / clip.w + vec2(1e-6));
            clip.xy += dir * width * 2.0 / viewport * clip.w;
            fragPos = (matModel * vec4(p, 1.0)).xyz;
            gl_Position = clip;
        }
        """;

    private const string OutlineFs = """
        #version 330
        in vec3 fragPos;
        uniform vec4 colDiffuse; uniform vec3 fogColor; uniform float fogNear; uniform float fogFar; uniform vec3 viewPos;
        out vec4 finalColor;
        void main() {
            float f = clamp((length(viewPos - fragPos) - fogNear) / (fogFar - fogNear), 0.0, 1.0);
            finalColor = vec4(mix(colDiffuse.rgb, fogColor, f * f * 0.9), colDiffuse.a);
        }
        """;

    private const string WaterVs = """
        #version 330
        in vec3 vertexPosition; in vec3 vertexNormal;
        uniform mat4 mvp; uniform mat4 matModel; uniform float time; uniform float swell;
        out vec3 fragPos; out vec3 localPos;
        void main() {
            vec3 p = vertexPosition;
            vec4 w = matModel * vec4(p, 1.0);
            p.y += (sin(w.x * 1.3 + time * 1.1) + sin(w.z * 1.7 - time * 0.8)) * 0.03 * swell;
            localPos = vertexPosition;
            fragPos = (matModel * vec4(p, 1.0)).xyz;
            gl_Position = mvp * vec4(p, 1.0);
        }
        """;

    private const string WaterFs = "#version 330\n" + Common + """
        in vec3 fragPos; in vec3 localPos;
        uniform vec4 colDiffuse; uniform vec4 deepColor; uniform float murk; uniform float sparkle;
        out vec4 finalColor;
        float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
        void main() {
            vec3 v = normalize(viewPos - fragPos);
            float fres = pow(1.0 - clamp(v.y, 0.0, 1.0), 2.0);
            vec2 q = fragPos.xz;
            float bands = sin(q.x * 2.1 + sin(q.y * 1.3 + time * 0.6) * 1.4 + time * 0.5) * 0.5 + 0.5;
            bands = smoothstep(0.78, 0.95, bands) * 0.25;
            vec3 col = mix(deepColor.rgb, colDiffuse.rgb, 0.35 + fres * 0.5);
            col += vec3(bands) * (1.0 - murk);
            vec2 cell = floor(q * 6.0);
            float s = hash(cell + floor(time * 1.5));
            float glint = step(0.985, s) * smoothstep(0.5, 0.0, length(fract(q * 6.0) - 0.5)) * sparkle;
            col += vec3(glint) * 0.8 * (1.0 - murk);
            col = mix(col, vec3(0.42, 0.45, 0.33), murk * 0.75);
            col *= mix(groundColor, skyColor, 0.7) * 0.55 + sunColor * 0.55;
            col += lamps(col, vec3(0.0, 1.0, 0.0), fragPos) * 0.6;
            finalColor = vec4(fog(col, fragPos), colDiffuse.a);
        }
        """;

    private const string FallsVs = """
        #version 330
        in vec3 vertexPosition; in vec2 vertexTexCoord;
        uniform mat4 mvp; uniform mat4 matModel;
        out vec2 uv; out vec3 fragPos;
        void main() { uv = vertexTexCoord; fragPos = (matModel * vec4(vertexPosition, 1.0)).xyz; gl_Position = mvp * vec4(vertexPosition, 1.0); }
        """;

    private const string FallsFs = "#version 330\n" + Common + """
        in vec2 uv; in vec3 fragPos;
        uniform vec4 colDiffuse;
        out vec4 finalColor;
        float hash(float n) { return fract(sin(n) * 43758.5453); }
        void main() {
            float col = floor(uv.x * 9.0);
            float speed = 0.35 + hash(col) * 0.2;
            float s = fract(uv.y * 5.0 - time * speed + hash(col + 3.0));
            float streak = smoothstep(0.0, 0.25, s) * smoothstep(0.9, 0.55, s);
            float edge = smoothstep(0.0, 0.18, uv.x) * smoothstep(1.0, 0.82, uv.x);
            vec3 c = mix(colDiffuse.rgb, vec3(0.93, 0.98, 0.96), streak * 0.6);
            c *= mix(groundColor, skyColor, 0.6) * 0.5 + sunColor * 0.6;
            float a = edge * (0.55 + streak * 0.35) * colDiffuse.a;
            finalColor = vec4(fog(c, fragPos), a);
        }
        """;

    private const string PostVs = """
        #version 330
        in vec3 vertexPosition; in vec2 vertexTexCoord; in vec4 vertexColor;
        uniform mat4 mvp;
        out vec2 fragTexCoord; out vec4 fragColor;
        void main() { fragTexCoord = vertexTexCoord; fragColor = vertexColor; gl_Position = mvp * vec4(vertexPosition, 1.0); }
        """;

    private const string PostFs = """
        #version 330
        in vec2 fragTexCoord; in vec4 fragColor;
        uniform sampler2D texture0; uniform vec2 resolution; uniform float time; uniform float grain;
        out vec4 finalColor;
        float hash(vec2 p) { return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }
        void main() {
            vec2 uv = fragTexCoord;
            vec3 c = texture(texture0, uv).rgb;
            // warm grade: lift shadows toward plum, highlights toward cream
            float l = dot(c, vec3(0.299, 0.587, 0.114));
            c = mix(c, c * vec3(1.04, 1.0, 0.94), 0.6);
            c += vec3(0.035, 0.02, 0.05) * (1.0 - l);
            // soft vignette
            vec2 d = uv - 0.5;
            float vig = smoothstep(0.85, 0.25, length(d * vec2(1.0, 1.15)));
            c *= mix(0.72, 1.0, vig);
            // paper grain
            float g = hash(floor(uv * resolution) + floor(time * 12.0)) - 0.5;
            c += g * grain;
            finalColor = vec4(c, 1.0);
        }
        """;
}
