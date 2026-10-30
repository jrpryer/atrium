using System.Numerics;
using Raylib_cs;

namespace Atrium.Diorama.Render;

/// <summary>
/// Builds faceted, vertex-coloured meshes (one normal per face, for the cel look) and stores a smooth
/// normal per vertex in the tangent channel so the ink outline extrudes without cracks.
/// </summary>
public sealed class MeshBuilder
{
    private readonly List<Vector3> _pos = new();
    private readonly List<Vector3> _nrm = new();
    private readonly List<Color> _col = new();
    private readonly List<Vector2> _uv = new();

    public int TriangleCount => _pos.Count / 3;

    public MeshBuilder Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
    {
        var n = Vector3.Cross(b - a, c - a);
        n = n.LengthSquared() < 1e-12f ? Vector3.UnitY : Vector3.Normalize(n);
        _pos.Add(a); _pos.Add(b); _pos.Add(c);
        _nrm.Add(n); _nrm.Add(n); _nrm.Add(n);
        _col.Add(col); _col.Add(col); _col.Add(col);
        _uv.Add(Vector2.Zero); _uv.Add(Vector2.UnitX); _uv.Add(Vector2.One);
        return this;
    }

    public MeshBuilder TriUv(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Color col)
    {
        Tri(a, b, c, col);
        _uv[^3] = ua; _uv[^2] = ub; _uv[^1] = uc;
        return this;
    }

    public MeshBuilder Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col) => Tri(a, b, c, col).Tri(a, c, d, col);

    /// <summary>An axis-aligned box from its centre and size, optionally with a different top colour.</summary>
    public MeshBuilder Box(Vector3 c, Vector3 size, Color col, Color? top = null)
    {
        var h = size / 2;
        Vector3 P(float x, float y, float z) => c + new Vector3(x * h.X, y * h.Y, z * h.Z);
        var t = top ?? col;
        Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), t);   // top
        Quad(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), Palette.Mul(col, 0.8f)); // bottom
        Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), col);  // +z
        Quad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), col); // -z
        Quad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), col);  // +x
        Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), col); // -x
        return this;
    }

    /// <summary>A gabled roof (triangular prism) over a box footprint, ridge along X.</summary>
    public MeshBuilder Gable(Vector3 baseCentre, float w, float d, float h, Color col, float overhang = 0.15f)
    {
        float x = w / 2 + overhang, z = d / 2 + overhang;
        var a = baseCentre + new Vector3(-x, 0, -z); var b = baseCentre + new Vector3(x, 0, -z);
        var c = baseCentre + new Vector3(x, 0, z); var e = baseCentre + new Vector3(-x, 0, z);
        var r0 = baseCentre + new Vector3(-x, h, 0); var r1 = baseCentre + new Vector3(x, h, 0);
        Quad(e, c, r1, r0, col);           // front slope
        Quad(b, a, r0, r1, col);           // back slope
        Tri(a, e, r0, Palette.Mul(col, 0.85f)); // gable ends
        Tri(c, b, r1, Palette.Mul(col, 0.85f));
        Quad(a, b, c, e, Palette.Mul(col, 0.6f)); // underside
        return this;
    }

    /// <summary>A capped cylinder or frustum along Y, with n sides (low counts look hand-cut).</summary>
    public MeshBuilder Cylinder(Vector3 bottom, float rBottom, float rTop, float height, int sides, Color col, Color? cap = null, float lean = 0, float twist = 0)
    {
        var top = bottom + new Vector3(lean, height, 0);
        for (int i = 0; i < sides; i++)
        {
            float a0 = MathF.Tau * i / sides, a1 = MathF.Tau * (i + 1) / sides;
            Vector3 B(float a) => bottom + new Vector3(MathF.Cos(a) * rBottom, 0, MathF.Sin(a) * rBottom);
            Vector3 T(float a) => top + new Vector3(MathF.Cos(a + twist) * rTop, 0, MathF.Sin(a + twist) * rTop);
            Quad(B(a1), B(a0), T(a0), T(a1), col);
            Tri(top, T(a1), T(a0), cap ?? col);
            Tri(bottom, B(a0), B(a1), Palette.Mul(col, 0.7f));
        }
        return this;
    }

    public MeshBuilder Cone(Vector3 bottom, float r, float height, int sides, Color col)
    {
        var apex = bottom + new Vector3(0, height, 0);
        for (int i = 0; i < sides; i++)
        {
            float a0 = MathF.Tau * i / sides, a1 = MathF.Tau * (i + 1) / sides;
            var p0 = bottom + new Vector3(MathF.Cos(a0) * r, 0, MathF.Sin(a0) * r);
            var p1 = bottom + new Vector3(MathF.Cos(a1) * r, 0, MathF.Sin(a1) * r);
            Tri(p1, p0, apex, col);
            Tri(bottom, p0, p1, Palette.Mul(col, 0.7f));
        }
        return this;
    }

    /// <summary>A faceted blob: a UV sphere with few segments, jittered for a hand-made canopy or rock.</summary>
    public MeshBuilder Blob(Vector3 c, Vector3 radii, int rings, int segs, Color col, int seed = 0, float jitter = 0.12f, Color? under = null)
    {
        var rng = new Random(seed);
        var grid = new Vector3[rings + 1, segs];
        for (int r = 0; r <= rings; r++)
            for (int s = 0; s < segs; s++)
            {
                float phi = MathF.PI * r / rings, th = MathF.Tau * s / segs + (r % 2) * 0.5f * MathF.Tau / segs;
                var d = new Vector3(MathF.Sin(phi) * MathF.Cos(th), MathF.Cos(phi), MathF.Sin(phi) * MathF.Sin(th));
                float j = (r == 0 || r == rings) ? 1 : 1 + ((float)rng.NextDouble() - 0.5f) * 2 * jitter;
                grid[r, s] = c + d * radii * j;
            }
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < segs; s++)
            {
                int s1 = (s + 1) % segs;
                var shade = r >= rings * 0.6f && under is not null ? under.Value : col;
                if (r > 0) Tri(grid[r, s], grid[r, s1], grid[r + 1, s], shade);          // skip the degenerate top pole
                if (r < rings - 1) Tri(grid[r, s1], grid[r + 1, s1], grid[r + 1, s], shade); // and the bottom pole
            }
        return this;
    }

    /// <summary>A flat disc (n-gon) facing up.</summary>
    public MeshBuilder Disc(Vector3 c, float r, int sides, Color col, float squash = 1)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = MathF.Tau * i / sides, a1 = MathF.Tau * (i + 1) / sides;
            Tri(c, c + new Vector3(MathF.Cos(a1) * r, 0, MathF.Sin(a1) * r * squash), c + new Vector3(MathF.Cos(a0) * r, 0, MathF.Sin(a0) * r * squash), col);
        }
        return this;
    }

    /// <summary>A ring wall (open cylinder band) — pool rims, the slab's edge.</summary>
    public MeshBuilder Ring(Vector3 c, float rOuter, float rInner, float height, int sides, Color col, Color? top = null, float squash = 1)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = MathF.Tau * i / sides, a1 = MathF.Tau * (i + 1) / sides;
            Vector3 O(float a, float y) => c + new Vector3(MathF.Cos(a) * rOuter, y, MathF.Sin(a) * rOuter * squash);
            Vector3 I(float a, float y) => c + new Vector3(MathF.Cos(a) * rInner, y, MathF.Sin(a) * rInner * squash);
            Quad(O(a1, 0), O(a0, 0), O(a0, height), O(a1, height), col);   // outside
            Quad(I(a0, 0), I(a1, 0), I(a1, height), I(a0, height), Palette.Mul(col, 0.85f)); // inside
            Quad(O(a1, height), O(a0, height), I(a0, height), I(a1, height), top ?? col);
        }
        return this;
    }

    /// <summary>
    /// Appends a loaded mesh's triangles (a piece of a Blender kit), transformed, in one colour, flat-shaded; with
    /// <paramref name="uv"/>, texture coordinates come from each vertex's world position (running water).
    /// </summary>
    public unsafe MeshBuilder AppendMesh(Mesh mesh, Matrix4x4 m, Color col, Func<Vector3, Vector2>? uv = null)
    {
        if (mesh.Vertices == null) return this;
        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            int a = t * 3, b = a + 1, c = a + 2;
            if (mesh.Indices != null) (a, b, c) = (mesh.Indices[a], mesh.Indices[b], mesh.Indices[c]);
            Vector3 P(int i) => Vector3.Transform(new Vector3(mesh.Vertices[i * 3], mesh.Vertices[i * 3 + 1], mesh.Vertices[i * 3 + 2]), m);
            var (pa, pb, pc) = (P(a), P(b), P(c));
            if (uv is null) Tri(pa, pb, pc, col);
            else TriUv(pa, pb, pc, uv(pa), uv(pb), uv(pc), col);
        }
        return this;
    }

    public MeshBuilder Append(MeshBuilder other)
    {
        _pos.AddRange(other._pos); _nrm.AddRange(other._nrm); _col.AddRange(other._col); _uv.AddRange(other._uv);
        return this;
    }

    public MeshBuilder Transform(Matrix4x4 m)
    {
        Matrix4x4.Invert(m, out var inv);
        var nm = Matrix4x4.Transpose(inv);
        for (int i = 0; i < _pos.Count; i++)
        {
            _pos[i] = Vector3.Transform(_pos[i], m);
            _nrm[i] = Vector3.Normalize(Vector3.TransformNormal(_nrm[i], nm));
        }
        return this;
    }

    /// <summary>
    /// Texture coordinates by projection, at <paramref name="tile"/> units per repeat: faces looking up or down take
    /// the ground plane; the rest take a cylinder round the origin when <paramref name="cylinder"/> is set (a curved
    /// wall's texture then runs unbroken round its arc), else the side plane they mostly face. Faces that turn
    /// across the cylinder (a wall's end caps) take their side plane too, so the texture never streaks.
    /// </summary>
    public MeshBuilder ProjectUv(float tile, bool cylinder = false)
    {
        for (int i = 0; i + 2 < _pos.Count; i += 3)
        {
            var (p0, p1, p2) = (_pos[i], _pos[i + 1], _pos[i + 2]);
            var n = Vector3.Cross(p1 - p0, p2 - p0);
            n = n.LengthSquared() < 1e-12f ? Vector3.UnitY : Vector3.Normalize(n);
            var centre = (p0 + p1 + p2) / 3;
            var radial = new Vector2(centre.X, centre.Z);
            bool across = cylinder && radial.LengthSquared() > 1e-6f
                && MathF.Abs(Vector2.Dot(new Vector2(n.X, n.Z), Vector2.Normalize(radial))) < 0.45f * new Vector2(n.X, n.Z).Length();
            float ax = MathF.Abs(n.X), ay = MathF.Abs(n.Y), az = MathF.Abs(n.Z);
            for (int k = i; k < i + 3; k++)
            {
                var p = _pos[k];
                _uv[k] = ay >= 0.7f ? new Vector2(p.X, p.Z) / tile
                    : cylinder && !across ? new Vector2(MathF.Atan2(p.Z, p.X) * new Vector2(p.X, p.Z).Length(), -p.Y) / tile
                    : ax >= az ? new Vector2(p.Z, -p.Y) / tile : new Vector2(p.X, -p.Y) / tile;
            }
        }
        return this;
    }

    /// <summary>Smooth shading: each vertex takes the average normal of the faces that share its (millimetre-rounded) position.</summary>
    public MeshBuilder SmoothNormals()
    {
        var sum = new Dictionary<(int, int, int), Vector3>();
        (int, int, int) Key(Vector3 p) => ((int)MathF.Round(p.X * 1000), (int)MathF.Round(p.Y * 1000), (int)MathF.Round(p.Z * 1000));
        for (int i = 0; i < _pos.Count; i++)
        {
            var k = Key(_pos[i]);
            sum[k] = sum.TryGetValue(k, out var acc) ? acc + _nrm[i] : _nrm[i];
        }
        for (int i = 0; i < _pos.Count; i++)
        {
            var n = sum[Key(_pos[i])];
            if (n.LengthSquared() > 1e-8f) _nrm[i] = Vector3.Normalize(n);
        }
        return this;
    }

    /// <summary>Uploads to the GPU as a raylib mesh (with smooth normals in the tangent channel).</summary>
    public unsafe Mesh Build()
    {
        int n = _pos.Count;
        var mesh = new Mesh(n, n / 3);
        mesh.AllocVertices();
        mesh.AllocNormals();
        mesh.AllocColors();
        mesh.AllocTexCoords();
        mesh.AllocTangents();
        var v = mesh.VerticesAs<Vector3>();
        var nr = mesh.NormalsAs<Vector3>();
        var cl = mesh.ColorsAs<Color>();
        var uv = mesh.TexCoordsAs<Vector2>();
        var tg = mesh.TangentsAs<Vector4>();

        // smooth normals: average face normals sharing a (quantised) position
        var smooth = new Dictionary<(int, int, int), Vector3>();
        (int, int, int) Key(Vector3 p) => ((int)MathF.Round(p.X * 1000), (int)MathF.Round(p.Y * 1000), (int)MathF.Round(p.Z * 1000));
        for (int i = 0; i < n; i++)
        {
            var k = Key(_pos[i]);
            smooth[k] = smooth.TryGetValue(k, out var acc) ? acc + _nrm[i] : _nrm[i];
        }
        for (int i = 0; i < n; i++)
        {
            v[i] = _pos[i]; nr[i] = _nrm[i]; cl[i] = _col[i]; uv[i] = _uv[i];
            var s = smooth[Key(_pos[i])];
            s = s.LengthSquared() < 1e-8f ? _nrm[i] : Vector3.Normalize(s);
            tg[i] = new Vector4(s, 1);
        }
        Raylib.UploadMesh(ref mesh, false);
        return mesh;
    }

    /// <summary>Builds and wraps in a model that uses the given shader.</summary>
    public unsafe Model Model(Shader shader)
    {
        var model = Raylib.LoadModelFromMesh(Build());
        model.Materials[0].Shader = shader;
        return model;
    }
}
