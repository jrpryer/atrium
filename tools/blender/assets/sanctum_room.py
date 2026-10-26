"""The Sanctum room kit: the Keeper's round stone study, built to replace the room's MeshBuilder shapes part by part.
References: art/reference/sheets/sanctum-room-sheet.png (props, 1344 x 752) and sanctum-sheet.png (the tower's
sandstone, so the walls inside match the walls outside).

The game (SanctumScene.Build) asks for each part by name with Prop("<key>", BuiltIn) and draws the kit's part with
exactly the transform it would draw the built-in shape with, so every part here is built in the same frame as the
C# builder it replaces (SanctumProps.cs):
- room coordinates (drawn at Identity or lifted straight up by the hover hop): shell, decor, oculus-glass, desk,
  journal, board, glass-table, bench, cabinet, circle-candles;
- local frames (the draw call moves them): sheet (a pinned sketch, centred), plinth and stand (the seal's base
  centre), relic-door (hinged on its left edge), beam (its pivot), pan (the hook it hangs from), lens-ring and
  lens-glass (the lens pivot, then the game's swing about X).
"circle" is left to the built-in chalk rings: it is tinted and glows, drawn without ink, and lines up with the
spark that runs round it.

Everything is written in GAME coordinates (Y up, metres; the back wall's face at z = -5.4, the left wall's at
x = -5.4) and converted once on the way into Blender: a game point (x, y, z) is Blender (x, -z, y).

The room: a slate floor laid square to the chalk circle (two slabs to its radius, a grout cross at its centre, a
brass ring set round it), cedar panelling to dado height with a cap rail, the tower's sandstone above, the back
corner rounded into an apse (the study is round; the furniture keeps the two flat walls it was placed against),
a round leaded window high on the back wall and a larger one on the left wall (their glass is the emissive
"oculus-glass" part), a corner bookcase, the framed charter with its red seal, a brass sconce and an iron candle
stand (their wicks sit exactly under the flames the game draws).
"""
import math
import random

import bmesh
import numpy as np

ROOM = "sanctum-room-sheet.png"
HOUSE = "sanctum-sheet.png"
N = "sanctum-room-"

# ───────────────────────── where things stand (SanctumScene.cs) ─────────────────────────
DESK = np.array((-2.75, 0.0, -4.15))
GLASS = np.array((-4.35, 0.0, -1.25))
BENCH = np.array((1.55, 0.0, -4.85))
CABINET = np.array((4.4, 0.0, -4.9))
CIRCLE = np.array((-0.2, 0.0, 0.6))
CIRCLE_R = 1.75
BALANCE = np.array((3.85, 0.0, -1.7))
JOURNAL_AT = DESK + (-0.05, 1.06, 0.12)
LENS_PIVOT = GLASS + (0.0, 1.52, -0.02)
BEAM_HALF = 0.84
DESK_CANDLES = [DESK + (-1.12, 1.42, -0.34), DESK + (1.16, 1.34, -0.38)]
GLASS_CANDLE = GLASS + (-0.26, 1.2, 0.22)
BENCH_LAMP = BENCH + (-1.1, 1.32, -0.18)
SCONCE = np.array((3.2, 2.78, -5.12))
STAND_CANDLE = BALANCE + (-0.85, 1.78, 0.62)
CIRCLE_CANDLES = [CIRCLE + (math.cos(a) * (CIRCLE_R + 0.32), 0.36, math.sin(a) * (CIRCLE_R + 0.32)) for a in (-2.55, -0.6, 1.75)]

# ───────────────────────── the room's walls ─────────────────────────
WALL = -5.4                 # the faces of the back and left walls
RC = 1.5                    # radius of the rounded back-left corner
CX = CZ = WALL + RC         # its centre
L1 = 5.6 - CX               # wall line, measured from the back wall's right end: the back wall straight,
LA = math.pi / 2 * RC       # the corner arc,
L2 = 5.6 - CZ               # and the left wall straight
LT = L1 + LA + L2
ARC_N = 16
WALL_H = 6.6
DADO = 1.1                  # panelling up to here, then its rail and cap (top at 1.235: the bench's tool board starts at 1.3)

# sheet boxes (pixels from the top-left of the room sheet unless noted)
SLATE = (536, 610, 612, 686)        # slab centre to slab centre, so the grout cross sits mid-swatch
BOARDS = (718, 600, 797, 700)       # cedar panelling patch: board centre to board centre, between the rails
RAIL = (700, 575, 840, 588)         # its top rail
WALL_STONE = (1642, 926, 1866, 1132)  # sanctum-sheet.png: the tower's back wall below its round window
DESK_WOOD = (60, 130, 330, 140)     # the desk top's front edge
BENCH_WOOD = (1030, 134, 1210, 150)  # the bench top's front face
BENCH_LEG = (998, 160, 1019, 262)
PLANKS = (75, 345, 118, 550)        # the cabinet's left door panel, left of its ring pull
TABLE_WOOD = (795, 135, 860, 147)   # the round table's top, left of the stand's foot
PLINTH_STONE = (332, 415, 420, 538)
LEATHER = (453, 80, 538, 140)
PARCHMENT = (622, 72, 722, 204)     # the charter's blank parchment, above the seal
PED_L, PED_R = (35, 143, 135, 252), (253, 143, 353, 252)
FRIEZE, KNEE = (137, 143, 253, 161), (137, 163, 253, 201)
HUTCH = (75, 63, 318, 117)
DRAWER = (51, 183, 116, 213)
DOOR_L, DOOR_R = (57, 328, 143, 520), (145, 328, 231, 520)
CHARTER = (603, 53, 741, 260)
RIBBON = (662, 259, 680, 271)
WINDOW = (1110, 359, 1314, 563)     # the round leaded window: centre (1212, 461), stone ring r 101 px, glass r 67 px
LENS = (829, 42, 871, 84)
RELIC = (350, 350, 385, 383)        # the reliquary's middle arch with its cross
WAX = (972, 402, 991, 436)          # one candle's lit side with its drips

K = None
ROOT = None


# ───────────────────────── helpers: game coordinates in, Blender meshes out ─────────────────────────

def g2b(p):
    return (float(p[0]), -float(p[2]), float(p[1]))


def RX(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, s], [0, -s, c]])


def RY(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, -s], [0, 1, 0], [s, 0, c]])


def RZ(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, s, 0], [-s, c, 0], [0, 0, 1]])
# (row vectors, as System.Numerics: p @ RY(a) is Vector3.Transform(p, Matrix4x4.CreateRotationY(a)))


def xf(pts, M=None, t=(0, 0, 0)):
    P = np.asarray(pts, dtype=float).reshape(-1, 3)
    if M is not None:
        P = P @ M
    return P + np.asarray(t, dtype=float)


def mk(name, verts, faces, mat=None, uvs=None, solid=True, smooth=None, fmat=None):
    """A mesh from game-space vertices. uvs: per face, a UV per corner. mat: a material, or a list with fmat giving
    each face's slot. smooth: shade smooth, keeping edges sharper than this many degrees."""
    o = K.mesh_object(name, [g2b(v) for v in verts], faces, ROOT, solid=solid)
    me = o.data
    assert len(me.polygons) == len(faces), f"{name}: {len(me.polygons)} faces after validate, {len(faces)} given"
    if uvs is not None:
        me.uv_layers.new(name="UVMap")
        uvl = me.uv_layers.active.data
        for fi, poly in enumerate(me.polygons):
            corner = dict(zip(faces[fi], uvs[fi]))
            for li in poly.loop_indices:
                uvl[li].uv = corner[me.loops[li].vertex_index]
    if isinstance(mat, (list, tuple)):
        me.materials.clear()
        for m in mat:
            me.materials.append(m)
        for fi, poly in enumerate(me.polygons):
            poly.material_index = fmat[fi]
    elif mat is not None:
        K.assign(o, mat)
    if smooth is not None:
        soften(o, smooth)
    return o


def soften(o, deg=35):
    bm = bmesh.new()
    bm.from_mesh(o.data)
    thr = math.radians(deg)
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        e.smooth = len(e.link_faces) == 2 and e.calc_face_angle(math.pi) <= thr
    bm.to_mesh(o.data)
    bm.free()


def facing(o, n):
    """Turns every face of an open mesh (a decal, a disc) toward game direction n."""
    want = np.array(g2b(n))
    for p in o.data.polygons:
        if np.dot(np.array(p.normal), want) < 0:
            p.flip()
    o.data.update()
    return o


BOX_F = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]


def box(name, lo, hi, mat=None, tile=None, M=None, t=(0, 0, 0), pivot=(0, 0, 0), off=None):
    """An axis-aligned box lo..hi, optionally turned by M about pivot, then moved by t."""
    lo, hi = np.asarray(lo, float), np.asarray(hi, float)
    v = np.array([(hi[0] if ix else lo[0], hi[1] if iy else lo[1], hi[2] if iz else lo[2])
                  for ix in (0, 1) for iy in (0, 1) for iz in (0, 1)])
    if M is not None:
        v = (v - pivot) @ M + pivot
    v = v + np.asarray(t, float)
    o = mk(name, v, BOX_F, mat)
    if tile is not None:
        K.box_uv(o, tile)
        if off is not None:
            shift_uv(o, *off)
    return o


def boxc(name, c, s, mat=None, tile=None, **kw):
    c, s = np.asarray(c, float), np.asarray(s, float)
    return box(name, c - s / 2, c + s / 2, mat, tile, **kw)


def shift_uv(o, du=0.0, dv=0.0):
    for d in o.data.uv_layers.active.data:
        d.uv = (d.uv[0] + du, d.uv[1] + dv)


def frustum(name, y0, y1, w0, w1, d0=None, d1=None, mat=None, tile=None, t=(0, 0, 0)):
    """A square-ish block whose footprint shrinks or grows from w0 x d0 at y0 to w1 x d1 at y1 (chamfers)."""
    d0 = w0 if d0 is None else d0
    d1 = w1 if d1 is None else d1
    v = []
    for ix in (0, 1):
        for iy in (0, 1):
            for iz in (0, 1):
                w, d = (w1, d1) if iy else (w0, d0)
                v.append(((ix - 0.5) * w, y1 if iy else y0, (iz - 0.5) * d))
    o = mk(name, xf(v, t=t), BOX_F, mat)
    if tile is not None:
        K.box_uv(o, tile)
    return o


def strut(name, a, b, th, mat=None):
    """A thin square rod from a to b."""
    a, b = np.asarray(a, float), np.asarray(b, float)
    d = b - a
    d /= np.linalg.norm(d)
    up = np.array((0, 1, 0)) if abs(d[1]) < 0.9 else np.array((1, 0, 0))
    u = np.cross(d, up)
    u /= np.linalg.norm(u)
    w = np.cross(d, u)
    h = th / 2
    ring = [(1, 1), (-1, 1), (-1, -1), (1, -1)]
    v = [a + (su * u + sw * w) * h for su, sw in ring] + [b + (su * u + sw * w) * h for su, sw in ring]
    f = [(0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    return mk(name, v, f, mat)


def lathe(name, prof, mat=None, segs=12, M=None, t=(0, 0, 0), smooth=None, uv=None, loop=False, phase=0.0):
    """A turned shape from a (radius, height) profile, bottom to top, about local Y; radius 0 makes a pole, an open
    end gets a flat cap; loop=True joins the last ring back to the first (a ring of any cross-section). M turns the
    local shape, t places it. uv: ("cyl", repeats, y0, y1) wraps u around and runs v up, or ("plan", size) maps the
    local XZ plane once across a square of that size."""
    verts, rings, lv = [], [], []
    for r, y in prof:
        if r <= 1e-7:
            rings.append([len(verts)])
            verts.append((0.0, y, 0.0))
            continue
        ring = []
        for j in range(segs):
            a = 2 * math.pi * j / segs + phase
            ring.append(len(verts))
            verts.append((r * math.cos(a), y, r * math.sin(a)))
        rings.append(ring)
    local = np.array(verts)

    def uv_of(vi, j):
        if uv is None:
            return (0.0, 0.0)
        x, y, z = local[vi]
        if uv[0] == "cyl":
            _, rep, y0, y1 = uv
            return (j / segs * rep, (y - y0) / (y1 - y0))
        size = uv[1]
        return (0.5 + x / size, 0.5 - z / size)

    faces, uvs = [], []
    pairs = list(zip(rings[:-1], rings[1:]))
    if loop:
        pairs.append((rings[-1], rings[0]))
    for A, B in pairs:
        for j in range(segs):
            k = (j + 1) % segs
            if len(A) == 1:
                faces.append((A[0], B[k], B[j]))
                uvs.append((uv_of(A[0], j + 0.5), uv_of(B[k], j + 1), uv_of(B[j], j)))
            elif len(B) == 1:
                faces.append((A[j], A[k], B[0]))
                uvs.append((uv_of(A[j], j), uv_of(A[k], j + 1), uv_of(B[0], j + 0.5)))
            else:
                faces.append((A[j], A[k], B[k], B[j]))
                uvs.append((uv_of(A[j], j), uv_of(A[k], j + 1), uv_of(B[k], j + 1), uv_of(B[j], j)))
    if not loop:
        for R in (rings[0], rings[-1]):
            if len(R) > 1:
                faces.append(tuple(R))
                if uv is not None and uv[0] == "cyl":
                    uvs.append(tuple((0.5, 0.98) for _ in R))  # caps take one texel near the top edge
                else:
                    uvs.append(tuple(uv_of(vi, 0) for vi in R))
    o = mk(name, xf(local, M, t), faces, mat, uvs if uv is not None else None, solid=True, smooth=smooth)
    return o


def quad(name, mat, p0, p1, p2, p3, uv=(0.0, 0.0, 1.0, 1.0)):
    """A decal quad: corners bottom-left, bottom-right, top-right, top-left as seen from its front."""
    u0, v0, u1, v1 = uv
    o = mk(name, [p0, p1, p2, p3], [(0, 1, 2, 3)], mat, [((u0, v0), (u1, v0), (u1, v1), (u0, v1))], solid=False)
    n = np.cross(np.subtract(p1, p0), np.subtract(p3, p0))
    return facing(o, n)


def decal(name, mat, centre, right, up, w, h, uv=(0.0, 0.0, 1.0, 1.0)):
    c, r, u = (np.asarray(x, float) for x in (centre, right, up))
    r, u = r * w / 2, u * h / 2
    return quad(name, mat, c - r - u, c + r - u, c + r + u, c - r + u, uv)


def join(name, objs):
    objs = [o for o in objs if o is not None]
    if len(objs) == 1:
        objs[0].name = name
        objs[0].data.name = name
        return objs[0]
    return K.join(name, objs)


# ───────────────────────── the wall line: back wall, corner arc, left wall ─────────────────────────

def wall_pt(s, d):
    """The point d metres into the room from the wall line, s metres along it (from the back wall's right end)."""
    if s <= L1:
        return 5.6 - s, WALL + d
    if s <= L1 + LA:
        th = -math.pi / 2 - (s - L1) / RC
        r = RC - d
        return CX + r * math.cos(th), CZ + r * math.sin(th)
    return WALL + d, CZ + (s - L1 - LA)


def wall_samples(sa, sb):
    pts = [sa, sb] + [L1 + LA * k / ARC_N for k in range(ARC_N + 1)]
    return sorted({round(s, 6) for s in pts if sa - 1e-9 <= s <= sb + 1e-9})


def ribbon(name, sa, sb, y0, y1, d0, d1, mat=None, tile=(1.0, 1.0), off=(0.0, 0.0)):
    """A solid strip that follows the wall line from s = sa to sb, y0..y1 high, d0..d1 out from the wall (the
    walls themselves, the panelling, rails). Its room face is mapped flat along the wall (u = s, v = y), so the
    rounded corner carries the texture round without a seam."""
    ss = wall_samples(sa, sb)
    tu, tv = tile
    du, dv = off
    verts = []
    for s in ss:
        xf_, zf = wall_pt(s, d1)
        xb, zb = wall_pt(s, d0)
        verts += [(xf_, y0, zf), (xf_, y1, zf), (xb, y0, zb), (xb, y1, zb)]
    U = lambda s: s / tu + du
    Vy = lambda y: y / tv + dv
    faces, uvs = [], []
    for i in range(len(ss) - 1):
        a, b = 4 * i, 4 * (i + 1)
        s0, s1 = ss[i], ss[i + 1]
        faces += [(a, b, b + 1, a + 1), (a + 2, a + 3, b + 3, b + 2), (a + 1, b + 1, b + 3, a + 3), (a, a + 2, b + 2, b)]
        uvs += [((U(s0), Vy(y0)), (U(s1), Vy(y0)), (U(s1), Vy(y1)), (U(s0), Vy(y1))),
                ((U(s0), Vy(y0)), (U(s0), Vy(y1)), (U(s1), Vy(y1)), (U(s1), Vy(y0))),
                ((U(s0), d1 / tv), (U(s1), d1 / tv), (U(s1), d0 / tv), (U(s0), d0 / tv)),
                ((U(s0), d1 / tv), (U(s0), d0 / tv), (U(s1), d0 / tv), (U(s1), d1 / tv))]
    for i in (0, 4 * (len(ss) - 1)):
        faces.append((i, i + 1, i + 3, i + 2))
        uvs.append(((d1 / tu, Vy(y0)), (d1 / tu, Vy(y1)), (d0 / tu, Vy(y1)), (d0 / tu, Vy(y0))))
    return mk(name, verts, faces, mat, uvs, solid=True, smooth=25)


# ───────────────────────── painted textures made from sheet paint ─────────────────────────

def _mask_segment(h, w, p0, p1, width):
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    (x0, y0), (x1, y1) = p0, p1
    dx, dy = x1 - x0, y1 - y0
    L2_ = dx * dx + dy * dy or 1.0
    t = np.clip(((xx - x0) * dx + (yy - y0) * dy) / L2_, 0, 1)
    dist = np.hypot(xx - (x0 + t * dx), yy - (y0 + t * dy))
    return np.clip(width / 2 + 0.5 - dist, 0, 1)


def _mask_ring(h, w, cx, cy, r, width, sy=1.0):
    yy, xx = np.mgrid[0:h, 0:w].astype(float)
    dist = np.abs(np.hypot(xx - cx, (yy - cy) / sy) - r)
    return np.clip(width / 2 + 0.5 - dist, 0, 1)


def _ink(arr, mask, col, alpha=0.85):
    m = (mask * alpha)[..., None]
    arr[..., :3] = arr[..., :3] * (1 - m) + np.asarray(col)[None, None, :] * m


def parchment(sheet, scale=3):
    x0, y0, x1, y1 = PARCHMENT
    p = sheet[y0:y1, x0:x1].copy()
    return np.repeat(np.repeat(p, scale, axis=0), scale, axis=1)


def page_texture(sheet, left):
    """A journal page: hand-ruled lines of writing in the brown-black ink; the left page has a little sketch."""
    p = parchment(sheet)
    h, w = p.shape[:2]
    ink = (0.17, 0.16, 0.2)
    rng = random.Random(7 if left else 11)
    top = 36
    if left:
        # a sketched leaf with its midrib, and a ruled label under it
        _ink(p, _mask_ring(h, w, w * 0.5, 96, 44, 2.6, sy=1.9), ink, 0.75)
        _ink(p, _mask_segment(h, w, (w * 0.5, 20), (w * 0.5, 180), 2.2), ink, 0.7)
        for k in range(4):
            yy = 70 + k * 26
            _ink(p, _mask_segment(h, w, (w * 0.5, yy), (w * 0.5 - 30, yy - 18), 1.6), ink, 0.6)
            _ink(p, _mask_segment(h, w, (w * 0.5, yy), (w * 0.5 + 30, yy - 18), 1.6), ink, 0.6)
        top = 214
    y = top
    while y < h - 24:
        x0 = 26 + rng.randint(0, 6)
        x1 = w - 26 - (rng.randint(30, 120) if rng.random() < 0.25 else rng.randint(0, 12))
        x = x0
        while x < x1:  # words: short dashes with gaps
            wl = rng.randint(14, 46)
            _ink(p, _mask_segment(h, w, (x, y + rng.uniform(-0.6, 0.6)), (min(x + wl, x1), y + rng.uniform(-0.6, 0.6)), 2.4), ink, 0.72)
            x += wl + rng.randint(7, 12)
        y += 30
    return p


def sketch_texture(sheet):
    """A pinned tool sketch: a ringed lens drawn in plan and section, with a few lines of notes. The game tints the
    whole sheet blueprint-pale, so the paper stays light."""
    p = parchment(sheet)
    h, w = p.shape[:2]
    ink = (0.23, 0.31, 0.40)
    cx, cy = w * 0.5, h * 0.36
    _ink(p, _mask_ring(h, w, cx, cy, 88, 4.0), ink)
    _ink(p, _mask_ring(h, w, cx, cy, 72, 2.6), ink)
    _ink(p, _mask_segment(h, w, (cx - 120, cy), (cx + 120, cy), 1.8), ink, 0.6)
    _ink(p, _mask_segment(h, w, (cx, cy - 120), (cx, cy + 120), 1.8), ink, 0.6)
    _ink(p, _mask_ring(h, w, cx, cy + 150, 80, 3.0, sy=0.18), ink, 0.8)   # the section, edge on
    for k, (a, b) in enumerate(((40, 250), (40, 200), (40, 230))):
        yy = h - 92 + k * 26
        _ink(p, _mask_segment(h, w, (a, yy), (b, yy), 3.0), ink, 0.75)
    return p


def wax_texture(sheet):
    x0, y0, x1, y1 = WAX
    r = sheet[y0:y1, x0:x1].copy()
    return np.concatenate([r, r[:, ::-1]], axis=1)


# ───────────────────────── shared small things ─────────────────────────

class Mats:
    pass


def candle(prefix, M_, x, z, y0, y1, r=0.05, segs=10):
    """A pillar candle with the sheet's drips round its top, and a wick; returns its parts."""
    body = lathe(prefix + "-wax", [(r, y0), (r, y1)], M_.wax, segs, t=(x, 0, z), uv=("cyl", 2, y0, y1), smooth=40)
    wick = boxc(prefix + "-wick", (x, y1 + 0.02, z), (0.012, 0.04, 0.012), M_.ink)
    return [body, wick]


def candlestick(prefix, M_, flame, base_y, dish=0.12):
    """A brass candlestick on base_y whose candle top sits just under the game's flame (as Candlestick() in C#)."""
    x, z = float(flame[0]), float(flame[2])
    top = float(flame[1]) - 0.05
    stick = lathe(prefix + "-stick", [(0, base_y), (dish, base_y), (dish * 0.95, base_y + 0.022), (dish * 0.55, base_y + 0.035),
                                      (0.028, base_y + 0.045), (0.024, base_y + 0.12), (0.075, base_y + 0.135),
                                      (0.08, base_y + 0.165), (0, base_y + 0.165)], M_.brass, 12, t=(x, 0, z), smooth=40)
    y0 = base_y + 0.165
    return [stick] + candle(prefix, M_, x, z, y0, max(top, y0 + 0.05))


def ring_xy(name, centre, r_out, r_in, th, mat, segs=12, M=None):
    """A flat ring (washer) standing in the local XY plane, a little torus for hooks and hangers."""
    prof = [(r_in, -th / 2), (r_out, -th / 2), (r_out, th / 2), (r_in, th / 2)]
    return lathe(name, prof, mat, segs, M=RX(math.pi / 2) if M is None else RX(math.pi / 2) @ M, t=centre, loop=True, smooth=50)


# ───────────────────────── the parts ─────────────────────────

def build_shell(M_):
    parts = []
    # the floor: one slab of slate, its slabs laid square to the circle (a grout cross under the circle's centre,
    # two slabs to its radius), so the chalk reads as part of the pattern
    slab = CIRCLE_R / 2
    tile = 2 * slab
    floor = box("_floor", (-5.8, -0.25, -5.8), (5.6, 0.0, 5.6), M_.slate, tile,
                off=(0.25 - CIRCLE[0] / tile, 0.25 + CIRCLE[2] / tile))
    parts.append(floor)
    # a brass ring let into the slate round the circle (flush; a flat face has no ink of its own)
    seg = 72
    v, f = [], []
    for j in range(seg):
        a = 2 * math.pi * j / seg
        for r in (1.86, 1.94):
            v.append((CIRCLE[0] + r * math.cos(a), 0.003, CIRCLE[2] + r * math.sin(a)))
    for j in range(seg):
        k = (j + 1) % seg
        f.append((2 * j, 2 * j + 1, 2 * k + 1, 2 * k))
    parts.append(facing(mk("_inlay", v, f, M_.brass_dark, solid=False), (0, 1, 0)))

    # the walls: the tower's sandstone, carried round the corner; then the cedar panelling and its rails
    parts.append(ribbon("_walls", 0, LT, 0.0, WALL_H, -0.4, 0.0, M_.stone, M_.stone_tile, (0.0, 0.12)))
    parts.append(ribbon("_skirting", 0, LT, 0.0, 0.14, -0.01, 0.045, M_.rail, M_.rail_tile))
    parts.append(ribbon("_boards", 0, LT, 0.14, DADO, -0.01, 0.018, M_.boards, M_.boards_tile, (0.0, 0.5 - 0.62 / M_.boards_tile[1])))
    s = 0.55
    while s < LT - 0.2:
        parts.append(ribbon(f"_stile{s:.2f}", s - 0.045, s + 0.045, 0.14, DADO, -0.01, 0.034, M_.rail, M_.rail_tile))
        s += 1.25
    parts.append(ribbon("_rail", 0, LT, DADO, DADO + 0.08, -0.01, 0.034, M_.rail, M_.rail_tile))
    parts.append(ribbon("_cap", 0, LT, DADO + 0.08, DADO + 0.135, -0.01, 0.06, M_.rail, M_.rail_tile))
    # a cedar cornice where the walls meet the dome (mostly above the camera's view)
    parts.append(ribbon("_cornice", 0, LT, WALL_H - 0.36, WALL_H, -0.01, 0.22, M_.rail, M_.rail_tile))
    parts.append(ribbon("_cornice2", 0, LT, WALL_H - 0.46, WALL_H - 0.36, -0.01, 0.1, M_.rail, M_.rail_tile))
    return join("shell", parts)


def round_window(prefix, M_, centre, right, up, r_out, glass_parts, parts, depth=0.09, segs=28):
    """A round leaded window: the painted stone ring stands proud of the wall (decor), the painted glass and its
    leading sit back in the bore (oculus-glass, which the game draws glowing). One decal maps across both."""
    c, rt, upv = (np.asarray(x, float) for x in (centre, right, up))
    nrm = np.cross(rt, upv)
    r_in = r_out * 67 / 101
    half = r_out * 102 / 101
    uvof = lambda lx, ly: (0.5 + lx / (2 * half), 0.5 + ly / (2 * half))
    stone_uv = (0.5, 0.965)
    v, f, uvs = [], [], []
    for j in range(segs):
        a = 2 * math.pi * j / segs
        for r in (r_out, r_in):
            for d in (depth, -0.02):
                v.append(c + rt * r * math.cos(a) + upv * r * math.sin(a) + nrm * d)
    I = lambda j, ri, di: 4 * (j % segs) + 2 * ri + di
    for j in range(segs):
        a0, a1 = 2 * math.pi * j / segs, 2 * math.pi * (j + 1) / segs
        f.append((I(j, 0, 0), I(j + 1, 0, 0), I(j + 1, 1, 0), I(j, 1, 0)))
        uvs.append((uvof(r_out * math.cos(a0), r_out * math.sin(a0)), uvof(r_out * math.cos(a1), r_out * math.sin(a1)),
                    uvof(r_in * math.cos(a1), r_in * math.sin(a1)), uvof(r_in * math.cos(a0), r_in * math.sin(a0))))
        for q in ((I(j, 0, 0), I(j, 0, 1), I(j + 1, 0, 1), I(j + 1, 0, 0)), (I(j, 1, 0), I(j + 1, 1, 0), I(j + 1, 1, 1), I(j, 1, 1)),
                  (I(j, 0, 1), I(j, 1, 1), I(j + 1, 1, 1), I(j + 1, 0, 1))):
            f.append(q)
            uvs.append((stone_uv,) * 4)
    parts.append(mk(prefix + "-ring", v, f, M_.window_rim, uvs, solid=True))
    gv = [c + nrm * 0.012] + [c + nrm * 0.012 + rt * r_in * math.cos(2 * math.pi * j / segs) + upv * r_in * math.sin(2 * math.pi * j / segs)
                              for j in range(segs)]
    gf = [(0, 1 + j, 1 + (j + 1) % segs) for j in range(segs)]
    guv = [((0.5, 0.5), uvof(r_in * math.cos(2 * math.pi * j / segs), r_in * math.sin(2 * math.pi * j / segs)),
            uvof(r_in * math.cos(2 * math.pi * (j + 1) / segs), r_in * math.sin(2 * math.pi * (j + 1) / segs))) for j in range(segs)]
    glass_parts.append(facing(mk(prefix + "-glass", gv, gf, M_.window_glass, guv, solid=False), nrm))


def build_decor(M_, glass_parts):
    parts = []
    # ── the round window high on the back wall, and the bigger one on the left wall (where the dust falls from) ──
    round_window("_oculus", M_, (-0.65, 4.7, WALL), (1, 0, 0), (0, 1, 0), 0.68, glass_parts, parts)
    round_window("_leftwin", M_, (WALL, 3.6, -1.5), (0, 0, -1), (0, 1, 0), 0.92, glass_parts, parts)

    # ── the framed charter, its seal in red wax, hung a hair askew ──
    cc = np.array((-0.65, 3.02, WALL))
    w, h = 0.9, 0.9 * (CHARTER[3] - CHARTER[1]) / (CHARTER[2] - CHARTER[0])
    tilt = RZ(0.012)
    fr = box("_charter-frame", (-w / 2, -h / 2, 0.0), (w / 2, h / 2, 0.035), M_.brass_dark, M=tilt, t=cc)
    face = decal("_charter-face", M_.charter, (0, 0, 0.037), (1, 0, 0), (0, 1, 0), w, h)
    rib_h = 0.11
    ribbon_ = decal("_charter-ribbon", M_.ribbon, (0.0, -h / 2 - rib_h / 2 + 0.02, 0.039), (1, 0, 0), (0, 1, 0), 0.118, rib_h)
    for o in (face, ribbon_):
        for vtx in o.data.vertices:  # turn and place the decals with the frame (they were made at the origin)
            g = np.array((vtx.co.x, vtx.co.z, -vtx.co.y)) @ tilt + cc
            vtx.co = g2b(g)
    hook = ring_xy("_charter-ring", cc + (0.0, h / 2 + 0.05, 0.02), 0.045, 0.03, 0.012, M_.brass)
    nail = boxc("_charter-nail", cc + (0.0, h / 2 + 0.085, 0.01), (0.03, 0.03, 0.02), M_.iron)
    parts += [fr, face, ribbon_, hook, nail]

    # ── the corner bookcase, standing across the rounded corner ──
    parts += bookcase(M_)

    # ── the brass sconce between the bench and the cabinet (its wick under the game's flame) ──
    sx, sy, sz = SCONCE
    parts.append(boxc("_sconce-plate", (sx, sy - 0.2, WALL + 0.015), (0.16, 0.38, 0.03), M_.brass_dark))
    parts.append(lathe("_sconce-boss", [(0, 0), (0.05, 0), (0.035, 0.03), (0, 0.04)], M_.brass, 10, M=RX(math.pi / 2), t=(sx, sy - 0.12, WALL + 0.03)))
    parts.append(box("_sconce-arm", (sx - 0.02, sy - 0.39, WALL + 0.03), (sx + 0.02, sy - 0.35, sz), M_.brass))
    parts += candlestick("_sconce", M_, SCONCE, sy - 0.35, 0.09)

    # ── the tall iron candle stand beside the Balance ──
    st = np.array((STAND_CANDLE[0], 0.0, STAND_CANDLE[2]))
    for i in range(3):
        a = i / 3 * 2 * math.pi + 0.3
        foot = st + (math.cos(a) * 0.3, 0.0, math.sin(a) * 0.3)
        parts.append(strut(f"_stand-leg{i}", st + (0, 0.34, 0), foot + (0, 0.03, 0), 0.04, M_.iron))
        parts.append(lathe(f"_stand-foot{i}", [(0, 0), (0.04, 0), (0.035, 0.035), (0, 0.035)], M_.iron, 6, t=foot))
    parts.append(lathe("_stand-shaft", [(0, 0.3), (0.045, 0.3), (0.045, 0.36), (0.028, 0.4), (0.024, 1.3), (0.04, 1.33), (0.022, 1.36),
                                         (0.022, 1.46), (0.13, 1.47), (0.13, 1.5), (0.06, 1.52), (0, 1.52)], M_.iron, 10, t=st, smooth=40))
    parts.append(lathe("_stand-cup", [(0, 1.52), (0.075, 1.52), (0.08, 1.56), (0, 1.56)], M_.iron, 10, t=st))
    parts += candle("_stand", M_, st[0], st[2], 1.54, STAND_CANDLE[1] - 0.05, r=0.055)
    return join("decor", parts)


def bookcase(M_):
    """A cedar bookcase across the rounded corner, facing the room on the diagonal, its back clear of the dado cap."""
    f = np.array((1.0, 0.0, 1.0)) / math.sqrt(2)          # facing (into the room)
    centre = np.array((CX, 0.0, CZ)) - f * 1.1             # its plan centre
    M = RY(math.pi / 4)                                     # local +z (front) onto f
    W, D, H = 1.1, 0.4, 2.55
    hw, hd = W / 2, D / 2
    out = []

    def b(name, lo, hi, mat, tile=None):
        o = box(name, lo, hi, mat, None, M=M, t=centre)
        if tile is not None:
            K.box_uv(o, tile)
        out.append(o)
        return o

    b("_bc-left", (-hw, 0.0, -hd), (-hw + 0.05, H, hd), M_.rail, M_.rail_tile)
    b("_bc-right", (hw - 0.05, 0.0, -hd), (hw, H, hd), M_.rail, M_.rail_tile)
    b("_bc-back", (-hw + 0.05, 0.0, -hd), (hw - 0.05, H, -hd + 0.03), M_.boards, M_.boards_tile)
    b("_bc-plinth", (-hw - 0.02, 0.0, -hd), (hw + 0.02, 0.1, hd + 0.03), M_.rail, M_.rail_tile)
    b("_bc-top", (-hw - 0.04, H - 0.06, -hd), (hw + 0.04, H, hd + 0.04), M_.rail, M_.rail_tile)
    b("_bc-crown", (-hw - 0.07, H, -hd), (hw + 0.07, H + 0.07, hd + 0.07), M_.rail, M_.rail_tile)
    shelves = [0.1, 0.72, 1.32, 1.92]
    for i, y in enumerate(shelves[1:]):
        b(f"_bc-shelf{i}", (-hw + 0.05, y - 0.03, -hd + 0.03), (hw - 0.05, y, hd), M_.rail, M_.rail_tile)
    # the books: sheet colours, a few leaning, one lying flat
    rng = random.Random(4)
    for si, y in enumerate(shelves):
        room = (0.55 if si < 3 else 0.5)
        x = -hw + 0.07
        while x < hw - 0.1:
            if rng.random() < 0.08:
                x += 0.1
                continue
            bw = rng.uniform(0.045, 0.085)
            bh = rng.uniform(0.3, 0.46) if room > 0.5 else rng.uniform(0.28, 0.42)
            bd = rng.uniform(0.24, 0.3)
            if x + bw > hw - 0.07:
                break
            col = M_.books[rng.randrange(len(M_.books))]
            lean = 0.0
            if x + bw + 0.12 > hw - 0.07 and rng.random() < 0.6:
                lean = -0.22  # the last book of a row leans on its neighbour
            o = box(f"_bk{si}-{x:.3f}", (x, y, hd - 0.02 - bd), (x + bw, y + bh, hd - 0.02), col, M=RZ(lean) @ M if lean else M,
                    t=centre, pivot=(x + bw, y, 0) if lean else (0, 0, 0))
            out.append(o)
            x += bw + 0.004
            if lean:
                break
    flat = box("_bk-flat", (-0.32, 1.92, -0.08), (0.05, 1.98, 0.15), M_.books[1], M=M, t=centre)
    out.append(flat)
    for o in out:
        K.shear(o, kx=0.006)
    return out


def build_desk(M_):
    p = DESK
    parts = []
    dt = M_.desk_tile

    def b(name, lo, hi, mat=None, tile=None):
        o = box(name, p + lo, p + hi, mat or M_.desk, dt if tile is None and mat is None else tile)
        parts.append(o)
        return o

    # the top (its surface at y = 1.03, where the journal and candlesticks stand)
    b("_top", (-1.4, 0.93, -0.625), (1.4, 1.03, 0.625))
    # two pedestals of three drawers each, on plinths and bun feet, with the kneehole between them
    for side, (x0, x1), dec in ((-1, (-1.33, -0.53), M_.ped_l), (1, (0.53, 1.33), M_.ped_r)):
        b(f"_ped{side}", (x0, 0.12, -0.57), (x1, 0.93, 0.55))
        b(f"_pedbase{side}", (x0 - 0.03, 0.04, -0.6), (x1 + 0.03, 0.12, 0.58))
        parts.append(decal(f"_pedface{side}", dec, p + ((x0 + x1) / 2, 0.525, 0.554), (1, 0, 0), (0, 1, 0), x1 - x0, 0.81))
        for fx in (x0 + 0.06, x1 - 0.06):
            for fz in (-0.54, 0.52):
                parts.append(lathe(f"_foot{side}{fx:.2f}{fz:.2f}", [(0, 0), (0.04, 0), (0.05, 0.02), (0.04, 0.04), (0, 0.04)],
                                   M_.desk_dark, 8, t=p + (fx, 0, fz)))
    b("_kneeback", (-0.53, 0.28, -0.47), (0.53, 0.93, -0.43))
    parts.append(decal("_kneeface", M_.knee, p + (0, 0.58, -0.426), (1, 0, 0), (0, 1, 0), 1.06, 0.6))
    b("_frieze", (-0.53, 0.8, 0.51), (0.53, 0.93, 0.55))
    parts.append(decal("_friezeface", M_.frieze, p + (0, 0.865, 0.554), (1, 0, 0), (0, 1, 0), 1.06, 0.13))

    # the gallery at the back, between the two candlesticks: posts, a painted back (books, ink, a quill), a shelf,
    # a top rail, the arched crest and two brass finials
    gx0, gx1 = -0.91, 0.95            # its inside, local to the desk (posts outside these)
    gz0, gz1 = -0.625, -0.425
    gy0, gy1 = 1.03, 1.45
    b("_post-l", (gx0 - 0.08, gy0, gz0), (gx0, gy1, gz1))
    b("_post-r", (gx1, gy0, gz0), (gx1 + 0.08, gy1, gz1))
    b("_gallery-back", (gx0, gy0, gz0), (gx1, gy1, gz0 + 0.03))
    parts.append(decal("_gallery-face", M_.hutch, p + ((gx0 + gx1) / 2, (gy0 + gy1) / 2, gz0 + 0.034), (1, 0, 0), (0, 1, 0), gx1 - gx0, gy1 - gy0))
    shelf_y = gy1 - (87 - HUTCH[1]) / (HUTCH[3] - HUTCH[1]) * (gy1 - gy0)
    b("_gallery-shelf", (gx0, shelf_y - 0.02, gz0 + 0.03), (gx1, shelf_y, gz0 + 0.15))
    b("_gallery-rail", (gx0 - 0.12, gy1, gz0 - 0.01), (gx1 + 0.12, gy1 + 0.06, gz1 + 0.02))
    crest = [(gx0, gy1 + 0.06)]
    n = 14
    for i in range(n + 1):
        t = i / n
        xx = gx0 + 0.04 + t * (gx1 - gx0 - 0.08)
        yy = gy1 + 0.06 + 0.05 + 0.15 * math.sin(math.pi * t) ** 1.6
        crest.append((xx, yy))
    crest.append((gx1, gy1 + 0.06))
    nv = len(crest)
    cv = [p + (x, y, gz0) for x, y in crest] + [p + (x, y, gz0 + 0.04) for x, y in crest]
    cf = [tuple(range(nv)), tuple(range(nv, 2 * nv))] + [(i, (i + 1) % nv, nv + (i + 1) % nv, nv + i) for i in range(nv)]
    crest_o = mk("_crest", cv, cf, M_.desk)
    K.box_uv(crest_o, dt)
    parts.append(crest_o)
    for fx in (gx0 - 0.04, gx1 + 0.04):
        parts.append(lathe(f"_finial{fx:.2f}", [(0, 0), (0.035, 0), (0.035, 0.015), (0.014, 0.03), (0.042, 0.065), (0.03, 0.1), (0.008, 0.115), (0, 0.13)],
                           M_.brass, 10, t=p + (fx, gy1 + 0.06, (gz0 + gz1) / 2), smooth=50))

    # the two candlesticks, an inkwell with its quill, a stack of books and a loose page
    for i, c in enumerate(DESK_CANDLES):
        parts += candlestick(f"_dc{i}", M_, c, 1.03)
    ink = p + (0.74, 1.03, 0.3)
    parts.append(lathe("_inkwell", [(0, 0), (0.07, 0), (0.078, 0.04), (0.06, 0.085), (0.032, 0.095), (0.032, 0.115), (0, 0.115)],
                       M_.inkwell, 10, t=ink, smooth=50))
    qa, qb = ink + (0.0, 0.1, 0.0), ink + (0.12, 0.47, -0.08)
    parts.append(strut("_quill", qa, qb, 0.014, M_.feather))
    d = (qb - qa) / np.linalg.norm(qb - qa)
    ex = np.cross(d, (0, 0, 1.0))
    ex /= np.linalg.norm(ex)
    ez = np.cross(ex, d)
    Mq = np.array([ex, d, ez]) * np.array([[1.0], [1.0], [0.25]])
    parts.append(lathe("_feather", [(0, 0), (0.022, 0.05), (0.04, 0.14), (0.034, 0.25), (0, 0.33)], M_.feather, 8, M=Mq,
                       t=ink + (0.035, 0.2, -0.023), smooth=50))
    stack = [((-1.0, 1.07, 0.26), (0.5, 0.08, 0.36), 0.05, 0), ((-0.98, 1.15, 0.25), (0.46, 0.08, 0.33), -0.08, 3),
             ((-1.01, 1.22, 0.27), (0.42, 0.06, 0.3), 0.12, 5)]
    for i, (c, s, rot, ci) in enumerate(stack):
        c = np.asarray(c)
        parts.append(boxc(f"_book{i}", (0, 0, 0), s, M_.books[ci], M=RY(rot), t=p + c))
    page = decal("_page", M_.page_r, (0, 0, 0), (1, 0, 0), (0, 0, -1), 0.36, 0.46)
    for vtx in page.data.vertices:
        g = np.array((vtx.co.x, vtx.co.z, -vtx.co.y)) @ RY(0.35) + p + (0.78, 1.032, -0.17)
        vtx.co = g2b(g)
    parts.append(page)

    # the chair, pulled out a little (local, then turned and placed as in C#)
    Mc, tc = RY(-0.55), p + (1.75, 0, 1.05)
    ch = [boxc("_seat", (0, 0.55, 0), (0.66, 0.07, 0.62), M_.desk, M=Mc, t=tc),
          boxc("_cushion", (0, 0.6, -0.01), (0.58, 0.04, 0.54), M_.red, M=Mc, t=tc),
          boxc("_back", (0, 1.1, 0.27), (0.66, 0.12, 0.07), M_.desk, M=Mc, t=tc),
          boxc("_backrail", (0, 0.86, 0.27), (0.5, 0.06, 0.05), M_.desk, M=Mc, t=tc)]
    for sx in (-1, 1):
        for sz in (-1, 1):
            ch.append(boxc(f"_cleg{sx}{sz}", (sx * 0.28, 0.27, sz * 0.26), (0.07, 0.54, 0.07), M_.desk_dark, M=Mc, t=tc))
        ch.append(boxc(f"_cpost{sx}", (sx * 0.28, 0.85, 0.27), (0.07, 0.6, 0.07), M_.desk, M=Mc, t=tc))
    for o in ch:
        if o.data.materials[0] == M_.desk:
            K.box_uv(o, dt)
    parts += ch
    return join("desk", parts)


def build_journal(M_):
    j = JOURNAL_AT
    parts = [boxc("_cover", j + (0, -0.012, 0), (1.2, 0.03, 0.82), M_.leather, M_.leather_tile)]
    for side, tex in ((-1, M_.page_l), (1, M_.page_r)):
        R, t = RZ(-side * 0.07), j + (side * 0.29, 0, 0)
        parts.append(box(f"_pages{side}", (-0.28, 0.0, -0.38), (0.28, 0.05, 0.38), M_.page_edge, M=R, t=t))
        q = decal(f"_pageface{side}", tex, (0, 0.0515, 0), (1, 0, 0), (0, 0, -1), 0.54, 0.74)
        for vtx in q.data.vertices:
            g = np.array((vtx.co.x, vtx.co.z, -vtx.co.y)) @ R + t
            vtx.co = g2b(g)
        parts.append(q)
    parts.append(boxc("_ribbon", j + (0.02, 0.06, 0.2), (0.03, 0.01, 0.6), M_.red))
    parts.append(boxc("_ribbon-tail", j + (0.02, -0.06, 0.43), (0.03, 0.14, 0.01), M_.red))
    return join("journal", parts)


def build_board(M_):
    c = np.array((DESK[0], 2.65, -5.34))
    parts = [boxc("_cork", c, (2.3, 1.15, 0.05), M_.leather, M_.leather_tile)]
    for name, off, size in (("_bt", (0, 0.6, 0.02), (2.42, 0.08, 0.08)), ("_bb", (0, -0.6, 0.02), (2.42, 0.08, 0.08)),
                            ("_bl", (-1.18, 0, 0.02), (0.08, 1.26, 0.08)), ("_br", (1.18, 0, 0.02), (0.08, 1.26, 0.08))):
        parts.append(boxc(name, c + off, size, M_.desk, M_.desk_tile))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(lathe(f"_stud{sx}{sy}", [(0, 0), (0.03, 0), (0.025, 0.015), (0, 0.02)], M_.brass, 8, M=RX(math.pi / 2),
                               t=c + (sx * 1.18, sy * 0.6, 0.06)))
    return join("board", parts)


def build_glass_table(M_):
    p = GLASS
    tt = M_.table_tile
    parts = []
    top = lathe("_tabletop", [(0, 0.8), (0.42, 0.8), (0.46, 0.815), (0.46, 0.855), (0.445, 0.87), (0, 0.87)], M_.table, 20, t=p, smooth=30)
    K.box_uv(top, tt)
    parts.append(top)
    ped = lathe("_pedestal", [(0, 0.12), (0.09, 0.12), (0.09, 0.17), (0.06, 0.2), (0.075, 0.3), (0.088, 0.44), (0.06, 0.57), (0.044, 0.6),
                              (0.062, 0.64), (0.044, 0.68), (0.05, 0.75), (0.12, 0.775), (0.12, 0.8), (0, 0.8)], M_.table, 10, t=p, smooth=40)
    K.box_uv(ped, tt)
    parts.append(ped)
    for i in range(3):
        a = math.pi / 2 + i * 2 * math.pi / 3 + 0.35
        cdir = np.array((math.cos(a), 0, math.sin(a)))
        k1, k2, k3 = p + cdir * 0.07 + (0, 0.22, 0), p + cdir * 0.26 + (0, 0.07, 0), p + cdir * 0.34 + (0, 0.025, 0)
        for nm, a_, b_, th in (("a", k1, k2, 0.065), ("b", k2, k3, 0.055)):
            o = strut(f"_leg{i}{nm}", a_, b_, th, M_.table)
            K.box_uv(o, tt)
            parts.append(o)
        parts.append(lathe(f"_toe{i}", [(0, 0), (0.045, 0), (0.045, 0.04), (0, 0.05)], M_.table_dark, 8, t=p + cdir * 0.35))
    parts.append(lathe("_felt", [(0, 0.87), (0.17, 0.87), (0.17, 0.88), (0, 0.88)], M_.felt, 16, t=p + (0, 0, 0.08)))
    # the brass stand: a turned column on a weighted foot, its arm reaching over the pad to the lens's pivot
    post = p + (0, 0.87, -0.32)
    parts.append(lathe("_lens-post", [(0, 0), (0.11, 0), (0.11, 0.018), (0.085, 0.035), (0.045, 0.05), (0.03, 0.08), (0.042, 0.1),
                                      (0.026, 0.12), (0.022, 0.62), (0.036, 0.64), (0.022, 0.66), (0.022, 0.72), (0.042, 0.75),
                                      (0.042, 0.78), (0.018, 0.8), (0.026, 0.83), (0.008, 0.88), (0, 0.9)], M_.brass, 10, t=post, smooth=40))
    tip = np.array((LENS_PIVOT[0], LENS_PIVOT[1] + 0.1, LENS_PIVOT[2]))
    parts.append(strut("_lens-arm", post + (0, 0.76, 0), tip, 0.03, M_.brass))
    parts.append(strut("_lens-drop", tip, LENS_PIVOT + (0, 0.02, 0), 0.024, M_.brass))
    parts.append(lathe("_lens-knuckle", [(0, -0.03), (0.026, -0.03), (0.03, 0), (0.026, 0.03), (0, 0.03)], M_.brass_light, 8, t=tip, smooth=50))
    parts += candlestick("_gc", M_, GLASS_CANDLE, 0.87, 0.09)
    return join("glass-table", parts)


LENS_TILT_M = RX(1.12)
LENS_TILT_T = np.array((0.0, -0.31, 0.03))


def _tilt(o):
    """Applies the C# LensTilt (rotate 1.12 about X, then drop 0.31 and bring forward 0.03) to a part built flat."""
    for vtx in o.data.vertices:
        g = np.array((vtx.co.x, vtx.co.z, -vtx.co.y)) @ LENS_TILT_M + LENS_TILT_T
        vtx.co = g2b(g)
    o.data.update()
    return o


def build_lens_ring(M_):
    parts = []
    ring = lathe("_lens-rim", [(0.222, 0.0), (0.27, 0.0), (0.285, 0.03), (0.27, 0.06), (0.222, 0.06), (0.214, 0.03)], M_.brass, 20, loop=True, smooth=40)
    ferrule = lathe("_ferrule", [(0, 0.255), (0.032, 0.255), (0.036, 0.285), (0.03, 0.31), (0, 0.31)], M_.brass_dark, 8, M=RZ(-math.pi / 2), t=(0, 0.03, 0), smooth=50)
    handle = lathe("_handle", [(0, 0.31), (0.024, 0.31), (0.031, 0.36), (0.028, 0.43), (0.018, 0.47), (0, 0.48)], M_.handle, 8, M=RZ(-math.pi / 2), t=(0, 0.03, 0), smooth=50)
    clevis = lathe("_clevis", [(0, -0.02), (0.022, -0.02), (0.022, 0.02), (0, 0.02)], M_.brass_dark, 8, M=RX(math.pi / 2), t=(0, 0.03, -0.29))
    for o in (ring, ferrule, handle, clevis):
        parts.append(_tilt(o))
    top = np.array((0.0, 0.03, -0.29)) @ LENS_TILT_M + LENS_TILT_T
    parts.append(strut("_hanger", (0, 0.015, 0), top, 0.022, M_.brass))
    parts.append(lathe("_pin", [(0, -0.05), (0.018, -0.05), (0.018, 0.05), (0, 0.05)], M_.brass_light, 8, M=RZ(-math.pi / 2)))
    return join("lens-ring", parts)


def build_lens_glass(M_):
    segs, r = 18, 0.23
    parts = []
    for name, y, n in (("_glass-top", 0.03, (0, 1, 0)), ("_glass-under", 0.0, (0, -1, 0))):
        v = [(0, y, 0)] + [(r * math.cos(2 * math.pi * j / segs), y, r * math.sin(2 * math.pi * j / segs)) for j in range(segs)]
        f = [(0, 1 + j, 1 + (j + 1) % segs) for j in range(segs)]
        uvp = lambda q: (0.5 + q[0] / (2 * r), 0.5 - q[2] / (2 * r))
        uv = [(uvp(v[0]), uvp(v[1 + j]), uvp(v[1 + (j + 1) % segs])) for j in range(segs)]
        o = facing(mk(name, v, f, M_.lens, uv, solid=False), n)
        parts.append(_tilt(o))
    return join("lens-glass", parts)


def build_bench(M_):
    p = BENCH
    parts = []
    bt, lt = M_.bench_tile, M_.leg_tile

    def b(name, lo, hi, mat=None, tile="wood"):
        mat = mat or M_.bench
        o = box(name, p + lo, p + hi, mat, bt if tile == "wood" else tile)
        parts.append(o)
        return o

    b("_top", (-1.45, 0.8, -0.475), (1.45, 0.92, 0.475))                      # its surface at y = 0.92
    for sx in (-1, 1):
        for sz in (-1, 1):
            c = np.array((sx * 1.33, 0, sz * 0.38))
            b(f"_leg{sx}{sz}", c + (-0.07, 0.07, -0.07), c + (0.07, 0.8, 0.07), M_.bench_leg, lt)
            parts.append(frustum(f"_legfoot{sx}{sz}", 0.0, 0.08, 0.2, 0.14, mat=M_.bench_leg, tile=lt, t=p + c))
        b(f"_endrail{sx}", (sx * 1.33 - 0.05, 0.3, -0.38), (sx * 1.33 + 0.05, 0.4, 0.38))
    b("_shelf", (-1.35, 0.19, -0.4), (1.35, 0.25, 0.4))                         # the crates stand on it at y = 0.25
    b("_apron", (-1.26, 0.7, 0.4), (1.26, 0.8, 0.44))
    # a crate of offcuts, a box and a hooped keg underneath
    b("_crate", (-1.06, 0.25, -0.23), (-0.44, 0.59, 0.27), M_.planks, M_.planks_tile)
    for k in range(3):
        b(f"_offcut{k}", (-0.98 + k * 0.17, 0.59, -0.15 + k * 0.05), (-0.9 + k * 0.17, 0.69 - k * 0.03, 0.15 + k * 0.03))
    b("_box", (0.3, 0.25, -0.26), (0.8, 0.49, 0.16), M_.desk, M_.desk_tile)
    keg = lathe("_keg", [(0, 0.25), (0.15, 0.25), (0.17, 0.4), (0.15, 0.55), (0, 0.55)], M_.planks, 10, t=p + (1.0, 0, 0.05), smooth=40)
    K.box_uv(keg, M_.planks_tile)
    parts.append(keg)
    for y in (0.3, 0.5):
        parts.append(lathe(f"_hoop{y}", [(0.155, y - 0.015), (0.172, y - 0.015), (0.172, y + 0.015), (0.155, y + 0.015)], M_.iron, 10,
                           t=p + (1.0, 0, 0.05), loop=True))
    # the vice at the right end of the front: a wooden jaw, an iron screw and a tommy bar with brass knobs
    b("_jaw", (0.92, 0.6, 0.475), (1.4, 0.92, 0.545))
    parts.append(lathe("_screw", [(0, 0), (0.03, 0), (0.03, 0.2), (0.045, 0.21), (0.045, 0.25), (0, 0.25)], M_.iron, 8, M=RX(math.pi / 2),
                       t=p + (1.16, 0.76, 0.545)))
    parts.append(lathe("_tommy", [(0, -0.2), (0.014, -0.2), (0.014, 0.2), (0, 0.2)], M_.iron, 6, M=RZ(-math.pi / 2) @ RZ(0.0), t=p + (1.16, 0.76, 0.77)))
    for sx in (-1, 1):
        parts.append(lathe(f"_tommyknob{sx}", [(0, -0.03), (0.03, -0.02), (0.03, 0.02), (0, 0.03)], M_.brass, 8, t=p + (1.16 + sx * 0.21, 0.76, 0.77), smooth=50))
    for sx in (-1, 1):
        parts.append(boxc(f"_guide{sx}", p + (1.16 + sx * 0.17, 0.76, 0.5), (0.025, 0.025, 0.12), M_.iron))
    # tools on the top: a hammer, a coil of twine, pliers, glass offcuts
    parts.append(boxc("_hammer", (0, 0, 0), (0.5, 0.04, 0.05), M_.handle, M=RY(0.3), t=p + (-0.35, 0.945, 0.22)))
    parts.append(boxc("_hammerhead", (0, 0, 0), (0.07, 0.08, 0.18), M_.iron, M=RY(0.3), t=p + (-0.12, 0.96, 0.15)))
    parts.append(lathe("_twine", [(0.07, 0.92), (0.13, 0.92), (0.13, 0.98), (0.07, 0.98)], M_.twine, 12, t=p + (0.32, 0, 0.12), loop=True, smooth=40))
    for k, a in enumerate((-0.4, -0.75)):
        parts.append(boxc(f"_plier{k}", (0, 0, 0), (0.26, 0.025, 0.035), M_.iron, M=RY(a), t=p + (0.62, 0.935, 0.28)))
    for k, (c, s, a) in enumerate((((0.05, 0.935, -0.22), (0.14, 0.03, 0.1), 0.6), ((0.22, 0.935, -0.26), (0.1, 0.03, 0.08), -0.3))):
        parts.append(boxc(f"_glassbit{k}", (0, 0, 0), s, M_.glass_bit, M=RY(a), t=p + c))
    # the oil lamp at the back left (its flame, drawn by the game, burns inside the chimney)
    lamp = np.array((BENCH_LAMP[0], 0.0, BENCH_LAMP[2]))
    parts.append(lathe("_lamp", [(0, 0.92), (0.12, 0.92), (0.12, 0.94), (0.1, 0.97), (0.05, 0.99), (0.1, 1.04), (0.11, 1.09), (0.08, 1.14),
                                 (0.035, 1.16), (0.035, 1.22), (0.06, 1.24), (0, 1.24)], M_.brass, 12, t=lamp, smooth=40))
    parts.append(lathe("_chimney", [(0, 1.24), (0.065, 1.24), (0.075, 1.3), (0.062, 1.38), (0.048, 1.46), (0, 1.46)], M_.chimney, 12, t=lamp, smooth=40))
    # the tool board on the wall behind, its tools hung on pegs (the sketches pin above it, on the wall)
    pb = p + (0, 1.85, -0.5)
    tb = box("_toolboard", pb + (-1.4, -0.55, -0.025), pb + (1.4, 0.55, 0.025), M_.boards, M_.boards_tile)
    parts.append(tb)
    parts.append(box("_toolrail", pb + (-1.45, 0.545, -0.02), pb + (1.45, 0.615, 0.06), M_.rail, M_.rail_tile))
    parts.append(box("_toolrail2", pb + (-1.45, -0.6, -0.02), pb + (1.45, -0.54, 0.05), M_.rail, M_.rail_tile))
    parts.append(boxc("_saw-handle", pb + (-1.0, 0.25, 0.045), (0.12, 0.2, 0.03), M_.handle))
    parts.append(boxc("_saw-blade", pb + (-0.95, -0.12, 0.04), (0.22, 0.56, 0.01), M_.steel))
    parts.append(boxc("_rule", pb + (-0.5, 0.05, 0.04), (0.05, 0.8, 0.02), M_.rule))
    parts.append(lathe("_twine-hung", [(0.12, -0.015), (0.18, -0.015), (0.18, 0.015), (0.12, 0.015)], M_.twine, 12, M=RX(math.pi / 2), t=pb + (0.1, 0.1, 0.04), loop=True))
    for k in range(3):
        parts.append(boxc(f"_file{k}", pb + (0.6 + k * 0.12, 0.0, 0.04), (0.035, 0.42 - k * 0.04, 0.025), M_.iron))
        parts.append(boxc(f"_filehandle{k}", pb + (0.6 + k * 0.12, 0.26 - k * 0.02, 0.04), (0.045, 0.12, 0.035), M_.handle))
    parts.append(boxc("_square-a", pb + (1.1, 0.05, 0.04), (0.05, 0.5, 0.02), M_.handle))
    parts.append(boxc("_square-b", pb + (1.21, -0.18, 0.04), (0.27, 0.05, 0.015), M_.steel))
    for k, x in enumerate((-1.0, -0.5, 0.1, 0.72, 1.1)):
        parts.append(lathe(f"_peg{k}", [(0, 0), (0.016, 0), (0.016, 0.06), (0, 0.06)], M_.handle, 6, M=RX(math.pi / 2), t=pb + (x, 0.42, 0.025)))
    return join("bench", parts)


def build_sheet(M_):
    parts = [boxc("_paper", (0, 0, 0), (0.52, 0.66, 0.015), M_.paper),
             decal("_sketch", M_.sketch, (0, 0, 0.0085), (1, 0, 0), (0, 1, 0), 0.52, 0.66),
             lathe("_tack", [(0, 0), (0.03, 0), (0.026, 0.012), (0.01, 0.02), (0, 0.03)], M_.brass, 8, M=RX(math.pi / 2), t=(0, 0.29, 0.008))]
    return join("sheet", parts)


def build_cabinet(M_):
    p = CABINET
    parts = []
    pt = M_.planks_tile

    def b(name, lo, hi, mat=None, tile=None):
        o = box(name, p + lo, p + hi, mat or M_.planks, tile if mat is not None else pt)
        parts.append(o)
        return o

    for sx in (-1, 1):
        for sz in (-1, 1):
            parts.append(lathe(f"_foot{sx}{sz}", [(0, 0), (0.05, 0), (0.06, 0.03), (0.05, 0.06), (0, 0.06)], M_.desk_dark, 8,
                               t=p + (sx * 0.74, 0, -0.02 + sz * 0.34)))
    b("_plinth", (-0.85, 0.06, -0.4), (0.85, 0.18, 0.46))
    b("_side-l", (-0.81, 0.18, -0.4), (-0.73, 3.19, 0.42))
    b("_side-r", (0.73, 0.18, -0.4), (0.81, 3.19, 0.42))
    b("_back", (-0.73, 0.18, -0.4), (0.73, 3.19, -0.34), M_.planks_dark, pt)
    b("_drawer-block", (-0.73, 0.18, -0.34), (0.73, 1.44, 0.4))
    b("_lip", (-0.73, 1.44, 0.37), (0.73, 1.47, 0.43), M_.rail, M_.rail_tile)
    for y in (2.01, 2.57):        # shelf tops (the game sets things on them at 2.02 and 2.58)
        b(f"_shelf{y}", (-0.73, y - 0.05, -0.34), (0.73, y, 0.36))
        b(f"_shelflip{y}", (-0.73, y - 0.06, 0.34), (0.73, y + 0.01, 0.38), M_.rail, M_.rail_tile)
    b("_toprail", (-0.73, 3.06, 0.36), (0.73, 3.19, 0.43))
    b("_crown", (-0.95, 3.19, -0.43), (0.95, 3.31, 0.47), M_.rail, M_.rail_tile)
    b("_crown2", (-0.86, 3.31, -0.4), (0.86, 3.37, 0.42), M_.rail, M_.rail_tile)
    # six drawers in the block, painted like the desk's, brass knobs and all
    for r, (y0, y1) in enumerate(((0.21, 0.58), (0.62, 0.99), (1.03, 1.4))):
        for c in range(2):
            cx = -0.37 + c * 0.74
            b(f"_drawer{r}{c}", (cx - 0.34, y0, 0.4), (cx + 0.34, y1, 0.43), M_.desk, M_.desk_tile)
            parts.append(decal(f"_drawerface{r}{c}", M_.drawer, p + (cx, (y0 + y1) / 2, 0.434), (1, 0, 0), (0, 1, 0), 0.68, y1 - y0))
    # the two doors of the upper case, standing open on their hinges
    hinge_z = 0.43
    for side, dec in ((-1, M_.door_l), (1, M_.door_r)):
        hx = side * 0.81
        Md = RY(-side * math.radians(104))
        lo, hi = (0.0, 1.49, 0.0), (0.78, 3.15, 0.035)
        if side > 0:
            lo, hi = (-0.78, 1.49, 0.0), (0.0, 3.15, 0.035)
        door = box(f"_door{side}", lo, hi, M_.planks, None, M=Md, t=p + (hx, 0, hinge_z))
        K.box_uv(door, pt)
        parts.append(door)
        x0, x1 = (lo[0], hi[0])
        q = quad(f"_doorface{side}", dec, (x0, 1.49, 0.037), (x1, 1.49, 0.037), (x1, 3.15, 0.037), (x0, 3.15, 0.037))
        for vtx in q.data.vertices:
            g = np.array((vtx.co.x, vtx.co.z, -vtx.co.y)) @ Md + p + (hx, 0, hinge_z)
            vtx.co = g2b(g)
        parts.append(q)
        for y in (1.75, 2.9):
            parts.append(boxc(f"_hinge{side}{y}", p + (hx, y, hinge_z), (0.03, 0.12, 0.03), M_.iron))
    return join("cabinet", parts)


def build_plinth(M_):
    """The reliquary's body: a sandstone pedestal with a real niche in its front, lined in dark red, framed in brass.
    Local to the seal's base centre; its cap's top is at 1.18, where the Balance's foot stands."""
    st = M_.plinth_tile
    parts = [frustum("_base", 0.0, 0.12, 1.12, 1.12, mat=M_.plinth, tile=st),
             frustum("_base-chamfer", 0.12, 0.18, 1.12, 0.95, mat=M_.plinth, tile=st),
             frustum("_cap-chamfer", 1.0, 1.07, 0.95, 1.12, mat=M_.plinth, tile=st),
             frustum("_cap", 1.07, 1.18, 1.12, 1.12, mat=M_.plinth, tile=st)]
    W, Y0, Y1, F = 0.475, 0.18, 1.0, 0.475
    hw, h0, h1, back = 0.31, 0.22, 0.9, 0.17
    v = [(-W, Y0, F), (W, Y0, F), (W, Y1, F), (-W, Y1, F),            # 0-3 front outer corners
         (-hw, h0, F), (hw, h0, F), (hw, h1, F), (-hw, h1, F),        # 4-7 the niche's mouth
         (-hw, h0, back), (hw, h0, back), (hw, h1, back), (-hw, h1, back),  # 8-11 its back
         (-W, Y0, -W), (W, Y0, -W), (W, Y1, -W), (-W, Y1, -W)]        # 12-15 the back of the block
    f = [(0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7),       # front, round the mouth
         (4, 5, 9, 8), (5, 6, 10, 9), (6, 7, 11, 10), (7, 4, 8, 11), (8, 9, 10, 11),  # the niche
         (12, 15, 14, 13), (0, 3, 15, 12), (1, 13, 14, 2), (3, 2, 14, 15), (0, 12, 13, 1)]
    shaft = mk("_shaft", v, f, [M_.plinth, M_.velvet], fmat=[0] * 4 + [1] * 5 + [0] * 5)
    K.box_uv(shaft, st)
    parts.append(shaft)
    for name, lo, hi in (("_fl", (-0.39, 0.18, F - 0.005), (-0.345, 0.95, F + 0.02)), ("_fr", (0.345, 0.18, F - 0.005), (0.39, 0.95, F + 0.02)),
                         ("_ft", (-0.39, 0.91, F - 0.005), (0.39, 0.955, F + 0.02)), ("_fb", (-0.39, 0.18, F - 0.005), (0.39, 0.215, F + 0.02))):
        parts.append(box(name, lo, hi, M_.brass_dark))
    for k, off in enumerate((0.01, 0.0)):
        pass
    for o in parts:
        if o.data.uv_layers:
            shift_uv(o, 0.27, 0.0)
    return join("plinth", parts)


def build_relic_door(M_):
    """The reliquary's door, hinged on its left edge at local x = 0 (the game swings it open about Y)."""
    parts = [box("_leaf", (0.0, -0.33, -0.025), (0.6, 0.33, 0.025), M_.brass_dark),
             decal("_face", M_.relic, (0.3, 0.0, 0.0265), (1, 0, 0), (0, 1, 0), 0.58, 0.64),
             lathe("_pull", [(0, 0), (0.028, 0), (0.028, 0.012), (0.012, 0.022), (0.03, 0.045), (0.02, 0.06), (0, 0.065)], M_.brass_light, 8,
                   M=RX(math.pi / 2), t=(0.54, 0.0, 0.025), smooth=50)]
    for y in (-0.22, 0.22):
        parts.append(lathe(f"_knuckle{y}", [(0, -0.045), (0.018, -0.045), (0.018, 0.045), (0, 0.045)], M_.iron, 8, t=(0.0, y, 0.03)))
    return join("relic-door", parts)


def build_stand(M_):
    """The Balance's column, local to the seal's base: a foot on the plinth's cap (y 1.18), a turned column and a
    fork holding the pin the beam turns on (y 2.46)."""
    parts = [lathe("_column", [(0, 1.18), (0.26, 1.18), (0.26, 1.205), (0.23, 1.22), (0.17, 1.245), (0.09, 1.268), (0.06, 1.3), (0.068, 1.33),
                               (0.048, 1.36), (0.046, 1.55), (0.062, 1.57), (0.062, 1.6), (0.045, 1.625), (0.034, 2.2), (0.05, 2.22),
                               (0.05, 2.25), (0.034, 2.27), (0.034, 2.33), (0.07, 2.35), (0.07, 2.38), (0, 2.38)], M_.brass, 14, smooth=40)]
    parts.append(box("_yoke", (-0.035, 2.37, -0.11), (0.035, 2.4, 0.11), M_.brass_dark))
    for sz in (-1, 1):
        parts.append(box(f"_prong{sz}", (-0.028, 2.38, min(sz * 0.078, sz * 0.104)), (0.028, 2.52, max(sz * 0.078, sz * 0.104)), M_.brass_dark))
        parts.append(lathe(f"_pinhead{sz}", [(0, 0), (0.024, 0), (0.024, 0.02), (0, 0.025)], M_.brass_light, 8,
                           M=RX(sz * math.pi / 2), t=(0, 2.46, sz * 0.104), smooth=50))
    parts.append(lathe("_pin", [(0, -0.105), (0.013, -0.105), (0.013, 0.105), (0, 0.105)], M_.brass_light, 6, M=RX(math.pi / 2), t=(0, 2.46, 0)))
    return join("stand", parts)


def build_beam(M_):
    """The beam, local to its pivot: tapered arms with knobbed ends, a boss on the pin, a loop above, and a ring at
    each end (x = +-0.84) where the pans hang."""
    parts = [lathe("_arms", [(0, -0.905), (0.016, -0.895), (0.026, -0.875), (0.018, -0.855), (0.015, -0.83), (0.03, -0.12), (0.036, -0.07),
                             (0.036, 0.07), (0.03, 0.12), (0.015, 0.83), (0.018, 0.855), (0.026, 0.875), (0.016, 0.895), (0, 0.905)],
                   M_.brass, 8, M=RZ(-math.pi / 2), smooth=40),
             lathe("_boss", [(0, -0.05), (0.05, -0.05), (0.064, -0.035), (0.064, 0.035), (0.05, 0.05), (0, 0.05)], M_.brass_light, 12,
                   M=RX(math.pi / 2), smooth=50),
             lathe("_stem", [(0, 0.05), (0.016, 0.05), (0.022, 0.09), (0.014, 0.11), (0, 0.11)], M_.brass, 8, smooth=50),
             ring_xy("_loop", (0, 0.155, 0), 0.055, 0.035, 0.016, M_.brass)]
    for sx in (-1, 1):
        parts.append(ring_xy(f"_eye{sx}", (sx * BEAM_HALF, -0.02, 0), 0.034, 0.02, 0.012, M_.brass_dark, segs=10))
    return join("beam", parts)


def build_pan(M_):
    """A pan, local to the hook it hangs from: a short rod and ring, three chains and a brass bowl whose inside
    floor (-0.57) is where the game sets the weighed thing and the counterweight (-0.565)."""
    parts = [strut("_rod", (0, 0.0, 0), (0, -0.3, 0), 0.014, M_.brass_dark),
             ring_xy("_gather", (0, -0.32, 0), 0.03, 0.017, 0.012, M_.brass_dark, segs=10)]
    for i in range(3):
        a = i / 3 * 2 * math.pi + 0.5
        rim = np.array((math.cos(a) * 0.27, -0.552, math.sin(a) * 0.27))
        parts.append(strut(f"_chain{i}", (0, -0.335, 0), rim, 0.013, M_.brass_dark))
    parts.append(lathe("_bowl", [(0, -0.625), (0.19, -0.622), (0.25, -0.6), (0.285, -0.567), (0.296, -0.548), (0.28, -0.546),
                                 (0.25, -0.555), (0.18, -0.566), (0, -0.57)], M_.brass, 16, smooth=45))
    return join("pan", parts)


def build_circle_candles(M_):
    parts = []
    for i, top in enumerate(CIRCLE_CANDLES):
        x, z = float(top[0]), float(top[2])
        parts.append(lathe(f"_dish{i}", [(0, 0), (0.135, 0), (0.14, 0.015), (0.125, 0.03), (0.08, 0.035), (0.075, 0.075), (0.088, 0.085),
                                         (0.072, 0.09), (0, 0.09)], M_.iron, 12, t=(x, 0, z), smooth=40))
        parts += candle(f"_cc{i}", M_, x, z, 0.06, float(top[1]) - 0.05, r=0.058)
    return join("circle-candles", parts)


# ───────────────────────── materials and the build ─────────────────────────

def materials(room, house):
    M_ = Mats()

    def sw(name, sheet, bx, mirror="both"):
        return K.material(N + name, K.swatch(sheet, N + name, *bx, mirror=mirror))

    def dc(name, sheet, bx):
        return K.material(N + name, K.decal(sheet, N + name, *bx))

    def arr(name, a):
        return K.material(N + name, K.decal(a, N + name, 0, 0, a.shape[1], a.shape[0]))

    def flat(name, col):
        return K.material(N + name, color=col)

    def scaled(c, k):
        return tuple(min(1.0, x * k) for x in c)

    M_.slate = sw("slate", room, SLATE)
    M_.stone = sw("stone", house, WALL_STONE)
    M_.stone_tile = K.sheet_tile(*WALL_STONE, 0.0085)
    M_.boards = sw("cedar-boards", room, BOARDS)
    M_.boards_tile = K.sheet_tile(*BOARDS, 0.006)
    M_.rail = sw("cedar-rail", room, RAIL)
    M_.rail_tile = K.sheet_tile(*RAIL, 0.006)
    M_.desk = sw("desk-wood", room, DESK_WOOD)
    M_.desk_tile = K.sheet_tile(*DESK_WOOD, 0.0084)
    M_.bench = sw("bench-wood", room, BENCH_WOOD)
    M_.bench_tile = K.sheet_tile(*BENCH_WOOD, 0.0083)
    M_.bench_leg = sw("bench-leg", room, BENCH_LEG)
    M_.leg_tile = K.sheet_tile(*BENCH_LEG, 0.0083)
    M_.planks = sw("cabinet-planks", room, PLANKS)
    M_.planks_tile = K.sheet_tile(*PLANKS, 0.0085)
    M_.table = sw("table-wood", room, TABLE_WOOD)
    M_.table_tile = K.sheet_tile(*TABLE_WOOD, 0.0072)
    M_.plinth = sw("plinth-stone", room, PLINTH_STONE)
    M_.plinth_tile = K.sheet_tile(*PLINTH_STONE, 0.0108)
    M_.leather = sw("leather", room, LEATHER)
    M_.leather_tile = K.sheet_tile(*LEATHER, 0.0045)

    M_.ped_l, M_.ped_r = dc("desk-pedestal-l", room, PED_L), dc("desk-pedestal-r", room, PED_R)
    M_.frieze, M_.knee = dc("desk-frieze", room, FRIEZE), dc("desk-kneehole", room, KNEE)
    M_.hutch = dc("desk-gallery", room, HUTCH)
    M_.drawer = dc("drawer", room, DRAWER)
    M_.door_l, M_.door_r = dc("cabinet-door-l", room, DOOR_L), dc("cabinet-door-r", room, DOOR_R)
    M_.charter = dc("charter", room, CHARTER)
    M_.ribbon = dc("charter-ribbon", room, RIBBON)
    win = K.decal(room, N + "window", *WINDOW)
    M_.window_rim = K.material(N + "window-rim", win)
    M_.window_glass = K.material(N + "window-glass", win)
    M_.lens = dc("lens", room, LENS)
    M_.relic = dc("reliquary-door", room, RELIC)
    M_.wax = arr("candle", wax_texture(room))
    M_.page_l = arr("page-left", page_texture(room, True))
    M_.page_r = arr("page-right", page_texture(room, False))
    M_.sketch = arr("sketch", sketch_texture(room))

    M_.brass = flat("brass", K.sample(room, 800, 506, 3))
    M_.brass_dark = flat("brass-dark", K.sample(room, 800, 519, 2))
    M_.brass_light = flat("brass-light", K.sample(room, 521, 450, 2))
    M_.iron = flat("iron", K.sample(room, 985, 505, 2))
    M_.steel = flat("steel", scaled(K.sample(room, 985, 505, 2), 2.2))
    M_.ink = flat("ink", K.srgb_to_linear((0.17, 0.165, 0.2)))
    M_.red = flat("red", K.sample(room, 671, 226, 3))
    M_.velvet = flat("velvet", scaled(K.sample(room, 671, 226, 3), 0.45))
    M_.felt = flat("felt", K.sample(room, 668, 150, 6))
    M_.paper = flat("paper", K.sample(room, 668, 150, 6))
    M_.page_edge = flat("page-edge", K.sample(room, 490, 243, 1))
    M_.inkwell = flat("inkwell", K.sample(room, 290, 106, 2))
    M_.feather = flat("feather", K.sample(room, 650, 100, 4))
    M_.handle = flat("handle", scaled(K.sample(room, 857, 200, 2), 0.8))
    M_.desk_dark = flat("desk-dark", scaled(K.sample(room, 230, 205, 3), 0.75))
    M_.table_dark = flat("table-dark", scaled(K.sample(room, 857, 200, 2), 0.7))
    M_.planks_dark = M_.planks
    M_.twine = flat("twine", K.sample(room, 143, 92, 1))
    M_.rule = flat("rule", K.sample(room, 668, 150, 6))
    M_.glass_bit = flat("glass-bit", K.sample(room, 840, 55, 3))
    M_.chimney = flat("chimney", K.sample(room, 982, 407, 2))
    book_pts = [(91, 95), (101, 95), (113, 82), (95, 111), (153, 110), (122, 78), (106, 108)]
    M_.books = [flat(f"book{i}", K.sample(room, x, y, 1)) for i, (x, y) in enumerate(book_pts)] + [M_.red, flat("book-leather", K.sample(room, 500, 120, 3))]
    for nm in ("brass", "brass_dark", "brass_light", "iron", "red", "felt", "inkwell", "handle", "desk_dark", "twine", "glass_bit", "chimney", "page_edge"):
        m = getattr(M_, nm)
        c = m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value
        print(f"COLOUR {nm}: " + " ".join(f"{x:.3f}" for x in c[:3]))
    return M_


def build(kit):
    global K, ROOT
    K = kit
    room = kit.load_sheet(ROOM)
    house = kit.load_sheet(HOUSE)
    M_ = materials(room, house)
    ROOT = kit.root("sanctum-room")

    glass_parts = []
    build_shell(M_)
    build_decor(M_, glass_parts)
    join("oculus-glass", glass_parts)
    build_desk(M_)
    build_journal(M_)
    build_board(M_)
    build_glass_table(M_)
    build_lens_ring(M_)
    build_lens_glass(M_)
    build_bench(M_)
    build_sheet(M_)
    build_cabinet(M_)
    build_plinth(M_)
    build_relic_door(M_)
    build_stand(M_)
    build_beam(M_)
    build_pan(M_)
    build_circle_candles(M_)
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in ROOT.children if o.type == "MESH")
    print(f"KIT triangles: {tris}")
    for o in ROOT.children:
        if o.type == "MESH":
            print(f"KIT part {o.name}: {sum(len(p.vertices) - 2 for p in o.data.polygons)} tris")
    return ROOT, WALL_H
