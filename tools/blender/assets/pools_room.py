"""The Spirit Pools room kit: the Stillkeepers' wet stone room beside the falls. Cool green-grey and damp: wet
flagstones with moss in the joints, rough mossy stone walls, arched barred windows overgrown with ferns, ivy
trailing from the wall tops, a long stone ledge along the back wall, a carved spout feeding the trickle, and the
druids' things (a stoneware holding crock, a patched green canvas bag, the breeding ledger on its stand, a
three-legged stool on a reed mat, a hand rake, a hanging lantern).
Reference: art/reference/sheets/pools-room-sheet.png (props, 1344 x 752) and pools-sheet.png (the lodge: its grey
mossy footing stone and honey timber tie the room to the house).

Parts (each named for the key the game asks for in PoolsScene: Prop("<key>", ...)):
- shell   room coordinates (drawn at identity): flagstones, walls, the ledge, windows, sills, ivy, moss.
- decor   room coordinates: fern pots, the jars, cloth and bucket on the ledge, the wall rake, the drain, the trickle's
          spout and its wet streak, the hanging lantern.
- ferns   room coordinates, drawn with sway 1.2: the fronds over the three pots in decor.
- crock   its own origin (drawn at CrockPos, lifted): rim top at 0.70 m, opening 0.52 m (the game's water disc is
          r 0.5 at 0.6 m), a landing net leaning on it, the lid behind.
- bag     its own origin (drawn at BagPos, lifted and squashed): mouth near 1.1 m, where the game puts its glow.
- ledger  its own origin (drawn rotated 0.55 rad about Y at LedgerPos): the reading slope faces +Z (game); the candle
          stays at the built-in's (0.38-0.45, ~1.0-1.2, 0.05) where the game draws its flame glow.
- stool   its own origin (drawn at StoolPos): seat top at 0.55 m, on a reed mat.
- rake    its own origin: the head at the origin, the handle up +Y (game), the tines down (the game turns it).
The tanks, water, creatures, plants, crystals, jars and the trickle are the game's; nothing here enters the tank
slots (PoolsScene.Slots) or the trickle's path.

Game (X, Y, Z) is Blender (X, -Z, Y); the game's front (+Z) is Blender -Y.
"""
import math
import random

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector

SHEET = "pools-room-sheet.png"
HOUSE = "pools-sheet.png"
PRE = "pools-room-"

# ── boxes on the prop sheet (real pixels from the top-left) ──
FLAG_A = (772, 500, 877, 585)       # four wet flagstones: each slab's painted face inside its ink edge
FLAG_B = (897, 500, 1005, 585)
FLAG_C = (772, 608, 877, 695)
FLAG_W = (895, 608, 1005, 695)      # the one with the puddle sheen
WALL = (1080, 493, 1302, 703)       # mossy stone wall patch, four courses, inside its outer ink line
WALL_COURSES = [493, 543, 598, 651, 703]   # the mortar lines between its courses
PX_WALL = 0.0068                    # a course (~52 px) is ~0.35 m
COPING = (815, 375, 1035, 398)      # the long ledge: its coping band
LEDGE_FACE = (835, 403, 1035, 455)  # the ledge's mossy face
WINDOW = (482, 355, 698, 703)       # the arched window with its stone surround and iron bars
CROCK_WRAP = (95, 115, 185, 300)    # the crock's body, rim to foot (moss at the bottom)
BAG_FRONT = (348, 163, 482, 296)    # the bag's front panel: flap, buckled straps, patch
CANVAS = (356, 267, 426, 294)       # plain green canvas below the flap
BOOK = (568, 40, 734, 116)          # the ledger's open pages
STOOL_TOP = (820, 143, 967, 173)    # the stool's seat top (an ellipse; mapped back onto a circle)
STOOL_SIDE = (855, 178, 935, 189)   # the seat's edge band
WOOD = (585, 262, 715, 279)         # the ledger stand's lower stretcher: honey wood, grain along its length
LANTERN_GLASS = (1206, 198, 1240, 272)
# ── on the lodge sheet ──
FOOTING = (1700, 1316, 2400, 1362)  # the lodge's back footing: grey slabs with moss along the bottom
PX_HOUSE = 0.0090

# ── room layout (game coordinates, from PoolsScene / InteriorScene) ──
WALL_IN = 5.4                       # the back and left walls' inner faces (game z = -5.4, x = -5.4)
EDGE = 5.6                          # the open cutaway edges (+X, +Z)
TANK0_W = min(max(1.3 + 0.34 * 8, 2.3), 4.1)   # the first pool (capacity 8 in content/pools/pools.json)
TRICKLE_X = 0.75 - 0.28 * TANK0_W   # where the trickle leaves the back wall (PoolsScene.LayoutTanks)
TANKS = [(0.75, -3.55), (-2.75, -0.35), (3.45, -0.55)]
FERN_POTS = [(-4.75, 4.1), (5.0, -4.7), (-4.85, -4.75)]
DRAIN = (-0.9, 1.0)
CROCK_POS = (-1.75, 2.65)
BACK_WINDOWS = [-3.4, 0.85, 4.25]
WIN_W = 2.0
WIN_H = WIN_W * (WINDOW[3] - WINDOW[1]) / (WINDOW[2] - WINDOW[0])
WIN_Z0 = 1.55                       # back windows' decal bottom
WIN_SILL = (WINDOW[3] - 662) / (WINDOW[3] - WINDOW[1]) * WIN_H   # the painted sill's top above the decal bottom
LEFT_WIN_Z = -1.5
LEFT_WIN_Z0 = 1.8
LEDGE_X = (-4.15, 4.45)
LEDGE_TOP = 0.64


def B(x, y, z):
    """A game point (Y up, front +Z) in Blender coordinates."""
    return Vector((x, -z, y))


# ───────────────────────── mesh building ─────────────────────────

def _normal(pts):
    n = Vector((0.0, 0.0, 0.0))
    for a, b in zip(pts, pts[1:] + pts[:1]):
        n.x += (a.y - b.y) * (a.z + b.z)
        n.y += (a.z - b.z) * (a.x + b.x)
        n.z += (a.x - b.x) * (a.y + b.y)
    return n


class BoxUV:
    """World box projection (like kit.box_uv) at `tile` metres per repeat, plus an offset in repeats."""

    def __init__(self, tile, du=0.0, dv=0.0):
        self.tu, self.tv = (tile, tile) if isinstance(tile, (int, float)) else tile
        self.du, self.dv = du, dv

    def __call__(self, pts, n):
        ax = max(range(3), key=lambda i: abs(n[i]))
        out = []
        for p in pts:
            if ax == 0:
                u, v = p.y * (1 if n.x > 0 else -1), p.z
            elif ax == 1:
                u, v = p.x * (-1 if n.y > 0 else 1), p.z
            else:
                u, v = p.x, p.y
            out.append((u / self.tu + self.du, v / self.tv + self.dv))
        return out


class Acc:
    """One part: faces with their own corners, UVs and materials, each turned to face `out`."""

    def __init__(self):
        self.verts, self.faces, self.uvs, self.mats, self.smooth = [], [], [], [], []
        self.materials = []

    def _mi(self, mat):
        if mat not in self.materials:
            self.materials.append(mat)
        return self.materials.index(mat)

    def face(self, pts, mat, uvs=None, out=None, smooth=False):
        pts = [Vector(p) for p in pts]
        if callable(uvs):
            uvs_f = uvs
            uvs = None
        else:
            uvs_f = None
        uvs = list(uvs) if uvs is not None else [(0.0, 0.0)] * len(pts)
        n = _normal(pts)
        if out is not None and n.dot(Vector(out)) < 0:
            pts, uvs, n = pts[::-1], uvs[::-1], -n
        if uvs_f is not None:
            uvs = uvs_f(pts, n.normalized() if n.length > 1e-9 else Vector((0, 0, 1)))
        k = len(self.verts)
        self.verts += pts
        self.faces.append(tuple(range(k, k + len(pts))))
        self.uvs.append(uvs)
        self.mats.append(self._mi(mat))
        self.smooth.append(smooth)

    def build(self, name, parent):
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(v) for v in self.verts], [], self.faces)
        layer = me.uv_layers.new(name="UVMap")
        for poly, uvs, m, s in zip(me.polygons, self.uvs, self.mats, self.smooth):
            poly.material_index = m
            poly.use_smooth = s
            for li, uv in zip(poly.loop_indices, uvs):
                layer.data[li].uv = uv
        for m in self.materials:
            me.materials.append(m)
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        bm.to_mesh(me)
        bm.free()
        me.update()
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        ob.parent = parent
        return ob


def cuboid(acc, lo, hi, mat, uv=None, M=None):
    """A box from lo to hi (in a local frame M, if given)."""
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    sides = [
        ([(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)], (0, 0, -1)),
        ([(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], (0, 0, 1)),
        ([(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)], (0, -1, 0)),
        ([(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)], (0, 1, 0)),
        ([(x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1)], (-1, 0, 0)),
        ([(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)], (1, 0, 0)),
    ]
    for pts, n in sides:
        pts = [Vector(p) for p in pts]
        n = Vector(n)
        if M is not None:
            pts = [M @ p for p in pts]
            n = M.to_3x3() @ n
        acc.face(pts, mat, uv, out=n)


def beam(acc, a, b, w, mat, uv=None, d=None):
    """A square timber (or iron bar) from a to b, w across (d deep, if given)."""
    a, b = Vector(a), Vector(b)
    t = (b - a).normalized()
    ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
    s = ref.cross(t).normalized()
    u = t.cross(s).normalized()
    hw, hd = w / 2, (d if d is not None else w) / 2
    ring = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    A = [a + s * (i * hw) + u * (j * hd) for i, j in ring]
    Bp = [b + s * (i * hw) + u * (j * hd) for i, j in ring]
    c = (a + b) / 2
    acc.face(A, mat, uv, out=-t)
    acc.face(Bp, mat, uv, out=t)
    for k in range(4):
        q = [A[k], A[(k + 1) % 4], Bp[(k + 1) % 4], Bp[k]]
        acc.face(q, mat, uv, out=sum(q, Vector()) / 4 - c)


def lathe(acc, centre, profile, seg, mat, uv=None, smooth=False, M=None, scale=(1.0, 1.0), jitter=None, phase=0.0):
    """Revolves an (r, z) profile, listed from the bottom of the axis round the outside to the top of the axis (or
    round a closed loop), about +Z at `centre`. `mat` and `smooth` may be functions of the profile segment;
    `uv(i, j, k)` gives the UV of profile point k at angle step j for segment i, or is a BoxUV."""
    c = Vector(centre)
    grid = []
    for k, (r, z) in enumerate(profile):
        row = []
        for j in range(seg):
            a = phase + 2 * math.pi * j / seg
            f = jitter(k, j) if (jitter is not None and r > 0) else 1.0
            p = Vector((r * f * math.cos(a) * scale[0], r * f * math.sin(a) * scale[1], z))
            row.append(p)
        grid.append(row)
    for i in range(len(profile) - 1):
        (r0, z0), (r1, z1) = profile[i], profile[i + 1]
        if r0 == 0 and r1 == 0:
            continue
        nr, nz = (z1 - z0), -(r1 - r0)
        m = mat(i) if callable(mat) else mat
        sm = smooth(i) if callable(smooth) else smooth
        for j in range(seg):
            j1 = (j + 1) % seg
            am = phase + 2 * math.pi * (j + 0.5) / seg
            corners = [(i, j, j), (i + 1, j, j), (i + 1, j1, j + 1), (i, j1, j + 1)]
            if r1 == 0:
                corners = [corners[0], corners[1], corners[3]]
            elif r0 == 0:
                corners = [corners[0], corners[1], corners[2]]
            pts = [grid[k][jj] for k, jj, _ in corners]
            out = Vector((nr * math.cos(am) * scale[1], nr * math.sin(am) * scale[0], nz))
            if M is not None:
                pts = [M @ (c + p) for p in pts]
                out = M.to_3x3() @ out
            else:
                pts = [c + p for p in pts]
            if isinstance(uv, BoxUV):
                acc.face(pts, m, uv, out=out, smooth=sm)
            else:
                uvs = [uv(i, ju, k) for k, _, ju in corners] if uv is not None else None
                acc.face(pts, m, uvs, out=out, smooth=sm)


def tube(acc, pts, r, mat, sides=3, closed=False):
    """A thin tube through points (vines, tines, rings); open ends."""
    pts = [Vector(p) for p in pts]
    n = len(pts)
    rings = []
    for i, p in enumerate(pts):
        if closed:
            t = (pts[(i + 1) % n] - pts[(i - 1) % n]).normalized()
        else:
            t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        a = ref.cross(t).normalized()
        b = t.cross(a).normalized()
        rings.append([p + (a * math.cos(2 * math.pi * k / sides) + b * math.sin(2 * math.pi * k / sides)) * r for k in range(sides)])
    for i in range(n if closed else n - 1):
        i1 = (i + 1) % n
        mid = (pts[i] + pts[i1]) / 2
        for k in range(sides):
            k1 = (k + 1) % sides
            q = [rings[i][k], rings[i][k1], rings[i1][k1], rings[i1][k]]
            acc.face(q, mat, out=sum(q, Vector()) / 4 - mid)


def strap(acc, pts, w, t, mat, across=Vector((0, 1, 0))):
    """A flat band (a strap) through points, w wide along `across`, t thick."""
    pts = [Vector(p) for p in pts]
    n = len(pts)
    rings = []
    for i, p in enumerate(pts):
        tg = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        a = (across - tg * across.dot(tg)).normalized()
        b = tg.cross(a).normalized()
        rings.append([p + a * sa * w / 2 + b * sb * t / 2 for sa, sb in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
    for i in range(n - 1):
        mid = (pts[i] + pts[i + 1]) / 2
        for k in range(4):
            q = [rings[i][k], rings[i][(k + 1) % 4], rings[i + 1][(k + 1) % 4], rings[i + 1][k]]
            acc.face(q, mat, out=sum(q, Vector()) / 4 - mid)
    for i, sgn in ((0, -1), (n - 1, 1)):
        tg = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        acc.face(rings[i], mat, out=tg * sgn)


def prism_x(acc, profile, x0, x1, mat, uv=None):
    """A closed (y, z) outline extruded along X."""
    n = len(profile)
    A = [Vector((x0, y, z)) for y, z in profile]
    Bp = [Vector((x1, y, z)) for y, z in profile]
    c = (sum(A, Vector()) + sum(Bp, Vector())) / (2 * n)
    acc.face(A, mat, uv, out=(-1, 0, 0))
    acc.face(Bp, mat, uv, out=(1, 0, 0))
    for k in range(n):
        q = [A[k], A[(k + 1) % n], Bp[(k + 1) % n], Bp[k]]
        acc.face(q, mat, uv, out=sum(q, Vector()) / 4 - c)


def quad(acc, centre, right, up, w, h, mat, normal, uvs=((0, 0), (1, 0), (1, 1), (0, 1))):
    """A flat decal: `right` and `up` span it (u runs along right, v along up)."""
    c, r, u = Vector(centre), Vector(right).normalized(), Vector(up).normalized()
    pts = [c - r * w / 2 - u * h / 2, c + r * w / 2 - u * h / 2, c + r * w / 2 + u * h / 2, c - r * w / 2 + u * h / 2]
    acc.face(pts, mat, list(uvs), out=normal)


def frond(acc, base, yaw, length, rise, droop, width, teeth, mat, roll=0.0, thick=0.012):
    """A fern frond: a serrated blade (one leaflet per tooth on each side) along an arching spine, thick at the
    spine and sharp at its edges, so it closes on itself and takes the ink outline."""
    base = Vector(base)
    d = Vector((math.cos(yaw), math.sin(yaw), 0.0))
    side0 = Vector((-math.sin(yaw), math.cos(yaw), 0.0)) * math.cos(roll) + Vector((0, 0, 1)) * math.sin(roll)
    ns = 2 * teeth + 1

    def spine(t):
        return base + d * (length * t) + Vector((0, 0, rise * (1 - (1 - t) ** 2) - droop * t ** 3))

    P = [spine(i / ns) for i in range(ns + 1)]
    L, R, St, Sb, N = [], [], [], [], []
    for i, p in enumerate(P):
        t = i / ns
        tan = (P[min(i + 1, ns)] - P[max(i - 1, 0)]).normalized()
        side = (side0 - tan * side0.dot(tan)).normalized()
        nrm = tan.cross(side).normalized()
        if nrm.z < 0:
            nrm, side = -nrm, -side
        env = math.sin(math.pi * min(t * 1.08, 1.0)) ** 0.6 * (1 - 0.3 * t)
        w = width * env * (1.0 if i % 2 == 1 else 0.13)
        fwd = tan * (0.5 * length / ns if i % 2 == 1 else 0.0)
        th = thick * max(0.0, 1 - t) ** 0.5
        L.append(p + side * w + fwd)
        R.append(p - side * w + fwd)
        St.append(p + nrm * th)
        Sb.append(p - nrm * th)
        N.append(nrm)
    for i in range(ns):
        n = N[i]
        if i == ns - 1:  # the tip closes to a point
            tip = P[ns]
            acc.face([St[i], L[i], tip], mat, out=n)
            acc.face([St[i], tip, R[i]], mat, out=n)
            acc.face([Sb[i], tip, L[i]], mat, out=-n)
            acc.face([Sb[i], R[i], tip], mat, out=-n)
            continue
        acc.face([St[i], L[i], L[i + 1], St[i + 1]], mat, out=n)
        acc.face([St[i], St[i + 1], R[i + 1], R[i]], mat, out=n)
        acc.face([Sb[i], Sb[i + 1], L[i + 1], L[i]], mat, out=-n)
        acc.face([Sb[i], R[i], R[i + 1], Sb[i + 1]], mat, out=-n)


def leaf(acc, c, normal, tipdir, size, mat):
    """An ivy leaf: a pointed blade with two side lobes, domed a little toward `normal` (a closed bipyramid)."""
    c, n = Vector(c), Vector(normal).normalized()
    u = (Vector(tipdir) - n * Vector(tipdir).dot(n)).normalized()
    s = n.cross(u)
    outline = [(0.0, 1.0), (0.78, 0.1), (0.0, -0.6), (-0.78, 0.1)]
    rim = [c + s * (x * size) + u * (y * size) for x, y in outline]
    top, bot = c + n * (0.2 * size), c - n * (0.06 * size)
    for i in range(4):
        a, b = rim[i], rim[(i + 1) % 4]
        acc.face([a, b, top], mat, out=n)
        acc.face([b, a, bot], mat, out=-n)


def mound(acc, centre, rx, ry, h, mat, rng, seg=7):
    """A low lumpy mound (moss, a cloth bundle)."""
    prof = [(0, 0), (1, 0), (0.92, 0.35), (0.66, 0.74), (0.3, 0.95), (0, 1)]
    jit = {(k, j): 1 + (rng.random() - 0.5) * 0.3 for k in range(len(prof)) for j in range(seg)}
    lathe(acc, centre, [(r, z * h) for r, z in prof], seg, mat, scale=(rx, ry), jitter=lambda k, j: jit[(k, j)],
          phase=rng.random() * 6.28)


# ───────────────────────── textures ─────────────────────────

def _region(sheet, box):
    x0, y0, x1, y1 = box
    r = sheet[y0:y1, x0:x1].copy()
    r[..., 3] = 1.0
    return r


def hswatch(kit, sheet, name, box):
    """Mirrored left-to-right only, to wrap round a pot without turning it upside down."""
    r = _region(sheet, box)
    return kit._image_from_array(PRE + name, np.concatenate([r, r[:, ::-1]], axis=1))


def mirrored(arr):
    """The 2 x 2 mirror block kit.swatch builds (rows from the top)."""
    a = np.concatenate([arr, arr[:, ::-1]], axis=1)
    return np.concatenate([a, a[::-1]], axis=0)


def wall_courses(sheet):
    """The mossy wall rebuilt course by course: each painted course is mirrored end to end (so it tiles across) and
    slid along by its own amount, and the courses are stacked in a shuffled order. A plain 2 x 2 mirror prints a
    kaleidoscope on an 11 m wall; this keeps every course upright (moss hangs down) with no seam lining up."""
    x0, _, x1, _ = WALL
    rows = []
    order = [(0, 0.0), (1, 0.37), (2, 0.71), (3, 0.18), (2, 0.52), (0, 0.86), (3, 0.63), (1, 0.05)]
    for k, shift in order:
        strip = sheet[WALL_COURSES[k]:WALL_COURSES[k + 1], x0:x1].copy()
        strip = np.concatenate([strip, strip[:, ::-1]], axis=1)
        rows.append(np.roll(strip, int(shift * strip.shape[1]), axis=1))
    out = np.concatenate(rows, axis=0)
    out[..., 3] = 1.0
    return out


def sample_wall(wall_px, tile, off, P, axis):
    """The wall texture's pixels at world points P (..., 3) on a wall whose u runs along `axis` (v up)."""
    th, tw = wall_px.shape[:2]
    U = np.mod(P @ np.array(axis, dtype=np.float64) / tile[0] + off[0], 1.0)
    V = np.mod(P[..., 2] / tile[1] + off[1], 1.0)
    rows = np.clip(((1.0 - V) * th).astype(int), 0, th - 1)
    cols = np.clip((U * tw).astype(int), 0, tw - 1)
    return wall_px[rows, cols]


def window_decal(kit, name, sheet, wall_px, tile, off, centre, right, axis, w, h):
    """The painted window with the sheet's grey background round its arch and stones replaced by the wall itself,
    exactly as the wall maps behind it, so the decal's rectangle never shows."""
    win = _region(sheet, WINDOW)
    H, W = win.shape[:2]
    bg = win[:4, :4, :3].reshape(-1, 3).mean(axis=0)
    near = np.abs(win[..., :3] - bg).max(axis=-1) < 0.07
    keyed = np.zeros((H, W), dtype=bool)
    stack = [(r, c) for r in range(H) for c in (0, W - 1)] + [(r, c) for c in range(W) for r in (0, H - 1)]
    while stack:  # flood the background in from the border
        r, c = stack.pop()
        if r < 0 or c < 0 or r >= H or c >= W or keyed[r, c] or not near[r, c]:
            continue
        keyed[r, c] = True
        stack += [(r + 1, c), (r - 1, c), (r, c + 1), (r, c - 1)]
    m = (~keyed).astype(np.float32)
    pad = np.pad(m, 1, mode="edge")
    m = sum(pad[1 + dy:1 + dy + H, 1 + dx:1 + dx + W] for dy in (-1, 0, 1) for dx in (-1, 0, 1)) / 9.0
    s = (np.arange(W) + 0.5) / W * w - w / 2
    t = h / 2 - (np.arange(H) + 0.5) / H * h
    c, rt = np.array(centre, dtype=np.float64), np.array(right, dtype=np.float64)
    P = c[None, None, :] + rt[None, None, :] * s[None, :, None] + np.array([0, 0, 1.0])[None, None, :] * t[:, None, None]
    under = sample_wall(wall_px, tile, off, P, axis)
    out = win.copy()
    out[..., :3] = win[..., :3] * m[..., None] + under[..., :3] * (1 - m[..., None])
    out[..., 3] = 1.0
    return kit.decal(out, PRE + name, 0, 0, W, H)


def streak_decal(kit, name, wall_tile_px, tile, off, x0, z0, w, h, res=100):
    """A dark wet streak down the wall under the spout, painted over the wall swatch exactly as the wall maps it
    (u = x / tile + du, v = z / tile + dv), its edges fading into the wall so no rectangle shows."""
    ww, hh = max(8, int(w * res)), max(8, int(h * res))
    X = x0 + (np.arange(ww) + 0.5) / ww * w
    Z = z0 + h - (np.arange(hh) + 0.5) / hh * h
    P = np.stack(np.broadcast_arrays(X[None, :], np.zeros((hh, ww)), Z[:, None]), axis=-1)
    under = sample_wall(wall_tile_px, tile, off, P, (1, 0, 0)).copy()
    k = (Z - z0) / h                                   # 0 at the bottom, 1 under the spout
    xc = x0 + w / 2 + 0.03 * np.sin(Z * 4.1)
    half = 0.07 + 0.11 * (1 - k)                      # it spreads as it runs down
    across = np.exp(-((X[None, :] - xc[:, None]) / half[:, None]) ** 2)
    along = np.clip(k / 0.35, 0, 1)[:, None] * np.clip((1 - k) / 0.04, 0, 1)[:, None]
    ry = np.minimum(np.arange(hh), hh - 1 - np.arange(hh))[:, None] / (0.06 * hh)
    rx = np.minimum(np.arange(ww), ww - 1 - np.arange(ww))[None, :] / (0.2 * ww)
    edge = np.clip(np.minimum(ry, rx), 0, 1)
    m = (across * along * edge)[..., None]
    wet = under[..., :3] * 0.5 + np.array([0.10, 0.15, 0.12]) * 0.5
    sheen = np.exp(-((X[None, :] - xc[:, None]) / 0.018) ** 2)[..., None] * 0.10 * m
    out = under.copy()
    out[..., :3] = under[..., :3] * (1 - m) + wet * m + sheen
    out[..., 3] = 1.0
    return kit.decal(out, PRE + name, 0, 0, ww, hh)


class Materials:
    def __init__(self, kit, sheet, house):
        k = kit
        mat = lambda n, img=None, col=(0.8, 0.8, 0.8): k.material(PRE + n, img, col)
        lin = k.srgb_to_linear
        self.flags = [mat("flag-" + n, k.decal(sheet, PRE + "flag-" + n, *b)) for n, b in
                      (("a", FLAG_A), ("b", FLAG_B), ("c", FLAG_C), ("wet", FLAG_W))]
        self.flag_side = mat("flag-side", col=lin((0.33, 0.36, 0.35)))
        self.grout = mat("grout", col=lin((0.24, 0.29, 0.21)))
        self.wall_px = wall_courses(sheet)
        self.wall = mat("wall", k._image_from_array(PRE + "wall", self.wall_px))
        self.wall_tile = (self.wall_px.shape[1] * PX_WALL, self.wall_px.shape[0] * PX_WALL)
        self.wall_off = (0.0, 0.0)
        self.coping = mat("coping", hswatch(kit, sheet, "coping", COPING))
        self.ledge = mat("ledge", hswatch(kit, sheet, "ledge", LEDGE_FACE))
        self.sheet = sheet
        self.footing = mat("footing", k.swatch(house, PRE + "footing", *FOOTING))
        self.sill = mat("sill", col=k.sample(sheet, 625, 685, 3))
        lift = lambda c, f: tuple(min(1.0, v * f) for v in c)
        self.ivy = [mat("ivy-" + str(i), col=lift(k.sample(sheet, x, y, 3), 1.3)) for i, (x, y) in enumerate(((390, 565), (350, 450), (355, 525)))]
        self.vine = mat("vine", col=k.sample(sheet, 372, 625, 1))
        self.fern = [mat("fern-" + n, col=lift(k.sample(sheet, x, y, 3), 1.45)) for n, (x, y) in
                     (("light", (155, 530)), ("mid", (185, 470)), ("dark", (50, 500)))]
        self.moss = [mat("moss", col=k.sample(sheet, 1170, 547, 3)), mat("moss-deep", col=lin((0.30, 0.38, 0.22)))]
        self.stoneware = mat("stoneware", hswatch(kit, sheet, "stoneware", CROCK_WRAP))
        self.rim = mat("stoneware-rim", col=k.sample(sheet, 75, 97, 3))
        self.inside = mat("crock-inside", col=lin((0.20, 0.23, 0.22)))
        self.soil = mat("soil", col=lin((0.21, 0.17, 0.13)))
        wood = _region(sheet, WOOD)
        self.wood = mat("wood", k.swatch(wood, PRE + "wood", 0, 0, wood.shape[1], wood.shape[0]))
        wv = np.ascontiguousarray(np.rot90(wood))
        self.wood_v = mat("wood-v", k.swatch(wv, PRE + "wood-v", 0, 0, wv.shape[1], wv.shape[0]))
        self.wood_tile = (2 * (WOOD[2] - WOOD[0]) * 0.004, 2 * (WOOD[3] - WOOD[1]) * 0.004)
        self.wood_dark = mat("wood-dark", col=lin((0.33, 0.24, 0.16)))
        self.timber = mat("timber", col=k.sample(house, 1950, 1180, 4))
        self.leather = mat("leather", col=k.sample(sheet, 392, 205, 2))
        self.cover = mat("book-cover", col=lin((0.25, 0.30, 0.24)))
        self.page = mat("page-edge", col=k.sample(sheet, 650, 100, 3))
        self.book = mat("book", k.decal(sheet, PRE + "book", *BOOK))
        self.ribbon = mat("ribbon", col=lin((0.55, 0.20, 0.16)))
        self.candle = mat("candle", col=k.sample(sheet, 1245, 260, 2))
        self.flame = mat("flame", col=lin((1.0, 0.82, 0.45)))
        self.stool_top = mat("stool-top", k.decal(sheet, PRE + "stool-top", *STOOL_TOP))
        self.stool_side = mat("stool-side", k.swatch(sheet, PRE + "stool-side", *STOOL_SIDE))
        self.reed = mat("reed", col=lin((0.72, 0.71, 0.53)))
        self.reed_bind = mat("reed-binding", col=lin((0.42, 0.42, 0.30)))
        self.iron = mat("iron", col=k.sample(sheet, 1080, 275, 2))
        self.rake_wood = mat("rake-wood", col=k.sample(sheet, 1080, 175, 2))
        self.canvas = mat("canvas", k.swatch(sheet, PRE + "canvas", *CANVAS))
        self.bag_front = mat("bag-front", k.decal(sheet, PRE + "bag-front", *BAG_FRONT))
        self.bag_inside = mat("bag-inside", col=lin((0.17, 0.22, 0.13)))
        self.parchment = mat("parchment", col=lin((0.85, 0.80, 0.67)))     # the scroll's lit side on the sheet
        self.cloth_blue = mat("cloth-blue", col=lin((0.47, 0.57, 0.66)))   # the bundled cloth's lit blue
        self.bottle = mat("bottle", col=k.sample(sheet, 437, 140, 3))
        self.cork = mat("cork", col=k.sample(sheet, 447, 110, 2))
        self.net = mat("net", col=lin((0.62, 0.62, 0.52)))
        self.linen = mat("linen", col=lin((0.62, 0.66, 0.58)))
        self.glaze = mat("glaze", col=lin((0.36, 0.56, 0.52)))
        self.cream = mat("cream-glaze", col=lin((0.84, 0.80, 0.68)))
        self.lantern_glass = mat("lantern-glass", k.decal(sheet, PRE + "lantern-glass", *LANTERN_GLASS))
        self.hole = mat("drain-hole", col=lin((0.10, 0.12, 0.11)))


# ───────────────────────── the shell ─────────────────────────

def build_shell(kit, M, root, rng):
    acc = Acc()
    # ── the floor: irregular courses of flagstones over a mossy grout bed; each slab wears one of the sheet's
    #    four painted slabs, turned and flipped at random, wetter near the drain, the crock and the tanks ──
    cuboid(acc, (-EDGE, -EDGE, -0.25), (EDGE, EDGE, -0.025), M.grout, BoxUV(1.0))
    g = 0.03
    wet_spots = [(B(*DRAIN[:1], 0, DRAIN[1]), 1.1, 0.75), (B(CROCK_POS[0], 0, CROCK_POS[1]), 1.3, 0.5)] + \
                [(B(x, 0, z), 2.6, 0.3) for x, z in TANKS]
    y = -EDGE
    while y < WALL_IN + 0.02:
        y1 = min(y + rng.uniform(0.82, 1.12), WALL_IN + 0.02)
        if WALL_IN + 0.02 - y1 < 0.4:
            y1 = WALL_IN + 0.02
        x = -WALL_IN - 0.02 - rng.uniform(0.0, 0.7)
        while x < EDGE:
            x1 = x + rng.uniform(0.72, 1.32)
            if EDGE - x1 < 0.35:
                x1 = EDGE
            xa, xb = max(x, -WALL_IN - 0.02), min(x1, EDGE)
            ga = 0.0 if xa <= -WALL_IN else g
            gb = 0.0 if xb >= EDGE else g
            gy0 = 0.0 if y <= -EDGE else g
            gy1 = g
            j = lambda: rng.uniform(0.0, 0.03)
            c = [Vector((xa + ga + j(), y + gy0 + j(), 0)), Vector((xb - gb - j(), y + gy0 + j(), 0)),
                 Vector((xb - gb - j(), y1 - gy1 - j(), 0)), Vector((xa + ga + j(), y1 - gy1 - j(), 0))]
            if xb >= EDGE:
                c[1].x = c[2].x = EDGE
            if xa <= -WALL_IN:
                c[0].x = c[3].x = xa
            if y <= -EDGE:
                c[0].y = c[1].y = -EDGE
            top = -rng.uniform(0.0, 0.007)
            mid = Vector(((xa + xb) / 2, (y + y1) / 2, 0))
            p_wet = 0.1
            for p, rad, pw in wet_spots:
                if (Vector((p.x, p.y, 0)) - mid).length < rad:
                    p_wet = max(p_wet, pw)
            flag = M.flags[3] if rng.random() < p_wet else M.flags[rng.randrange(3)]
            uv = [(0, 0), (1, 0), (1, 1), (0, 1)]
            r = rng.randrange(4)
            uv = uv[r:] + uv[:r]
            if rng.random() < 0.5:
                uv = [(1 - u, v) for u, v in uv]
            T = [Vector((p.x, p.y, top)) for p in c]
            Bt = [Vector((p.x, p.y, -0.06)) for p in c]
            acc.face(T, flag, uv, out=(0, 0, 1))
            for k in range(4):
                q = [Bt[k], Bt[(k + 1) % 4], T[(k + 1) % 4], T[k]]
                acc.face(q, M.flag_side, out=sum(q, Vector()) / 4 - mid)
            x = x1
        y = y1

    # ── walls: back and left, their inner faces toward the room; mossy stone, upright from the floor ──
    wall_uv = BoxUV(M.wall_tile, *M.wall_off)
    cuboid(acc, (-5.8, WALL_IN, -0.25), (EDGE, 5.8, 6.6), M.wall, wall_uv)
    cuboid(acc, (-5.8, -EDGE, -0.25), (-WALL_IN, 5.8, 6.6), M.wall, wall_uv)

    # the lodge's mossy footing as a skirting course along the walls' damp feet
    foot_tile = kit.sheet_tile(*FOOTING, PX_HOUSE)
    foot_uv = BoxUV(foot_tile, 0.0, 0.5)
    cuboid(acc, (-WALL_IN - 0.02, -EDGE, -0.02), (-5.25, WALL_IN + 0.02, 0.42), M.footing, foot_uv)
    cuboid(acc, (-WALL_IN - 0.02, 5.25, -0.02), (LEDGE_X[0] + 0.05, WALL_IN + 0.02, 0.42), M.footing, foot_uv)
    cuboid(acc, (LEDGE_X[1] - 0.05, 5.25, -0.02), (EDGE, WALL_IN + 0.02, 0.42), M.footing, foot_uv)

    # ── the long stone ledge along the back wall, its coping in three slabs ──
    ledge_u = 2 * (LEDGE_FACE[2] - LEDGE_FACE[0]) * 0.0100   # horizontally mirrored only: v runs foot to coping
    yf = -(-4.95)  # the ledge face (game z = -4.95)
    cuboid(acc, (LEDGE_X[0], yf, -0.02), (LEDGE_X[1], WALL_IN + 0.02, LEDGE_TOP - 0.12), M.ledge,
           BoxUV((ledge_u, LEDGE_TOP - 0.1), 0.0, 0.02 / (LEDGE_TOP - 0.1)))
    cop_u = 2 * (COPING[2] - COPING[0]) * 0.0052

    def coping_uv(pts, n):
        out = []
        for p in pts:
            if abs(n.z) > 0.5:
                u, v = p.x / cop_u, 0.1 + 0.8 * (p.y - (yf - 0.06)) / (WALL_IN + 0.08 - yf)
            elif abs(n.y) > 0.5:
                u, v = p.x / cop_u, (p.z - (LEDGE_TOP - 0.12)) / 0.12
            else:
                u, v = p.y / cop_u, (p.z - (LEDGE_TOP - 0.12)) / 0.12
            out.append((u, v))
        return out

    cuts = [LEDGE_X[0] - 0.06, -1.3, 1.6, LEDGE_X[1] + 0.06]
    for a, b in zip(cuts, cuts[1:]):
        cuboid(acc, (a + 0.012, yf - 0.06, LEDGE_TOP - 0.12), (b - 0.012, WALL_IN + 0.02, LEDGE_TOP), M.coping, coping_uv)

    # ── windows: the painted arched window on the back wall (three) and the left wall (one), each over a real
    #    stone sill that stands proud where the painted sill is ──
    e = 0.012
    for i, cx in enumerate(BACK_WINDOWS):
        c = (cx, WALL_IN - e, WIN_Z0 + WIN_H / 2)
        win = kit.material(PRE + f"window-{i}", window_decal(kit, f"window-{i}", M.sheet, M.wall_px, M.wall_tile, M.wall_off,
                                                            c, (1, 0, 0), (1, 0, 0), WIN_W, WIN_H))
        quad(acc, c, (1, 0, 0), (0, 0, 1), WIN_W, WIN_H, win, (0, -1, 0))
        cuboid(acc, (cx - 0.8, WALL_IN - 0.2, WIN_Z0 + WIN_SILL - 0.13), (cx + 0.8, WALL_IN + 0.01, WIN_Z0 + WIN_SILL + 0.015),
               M.sill, BoxUV(1.0))
    ly = -LEFT_WIN_Z
    c = (-WALL_IN + e, ly, LEFT_WIN_Z0 + WIN_H / 2)
    win = kit.material(PRE + "window-left", window_decal(kit, "window-left", M.sheet, M.wall_px, M.wall_tile, M.wall_off,
                                                         c, (0, -1, 0), (0, 1, 0), WIN_W, WIN_H))
    quad(acc, c, (0, -1, 0), (0, 0, 1), WIN_W, WIN_H, win, (1, 0, 0))
    cuboid(acc, (-WALL_IN - 0.01, ly - 0.8, LEFT_WIN_Z0 + WIN_SILL - 0.13), (-WALL_IN + 0.2, ly + 0.8, LEFT_WIN_Z0 + WIN_SILL + 0.015),
           M.sill, BoxUV(1.0))

    # ferns sprouting on the sills (the shell doesn't sway; the potted ferns do)
    def sill_fern(base, facing, n=5):
        for i in range(n):
            yaw = facing + (i - (n - 1) / 2) * 0.55 + rng.uniform(-0.15, 0.15)
            frond(acc, base, yaw, rng.uniform(0.4, 0.6), rng.uniform(0.22, 0.36), rng.uniform(0.2, 0.34),
                  rng.uniform(0.09, 0.11), 4, M.fern[rng.randrange(3)], roll=rng.uniform(-0.3, 0.3), thick=0.01)

    sz = WIN_Z0 + WIN_SILL + 0.015
    sill_fern((BACK_WINDOWS[0] - 0.55, WALL_IN - 0.1, sz), -math.pi / 2)
    sill_fern((BACK_WINDOWS[1] + 0.55, WALL_IN - 0.1, sz), -math.pi / 2)
    sill_fern((BACK_WINDOWS[2] + 0.5, WALL_IN - 0.1, sz), -math.pi / 2, 4)
    sill_fern((-WALL_IN + 0.1, ly - 0.55, LEFT_WIN_Z0 + WIN_SILL + 0.015), 0.0, 4)

    # ── ivy trailing down from the wall tops and the window heads ──
    def ivy(top, normal, along, length, leaves=True):
        top, normal, along = Vector(top), Vector(normal), Vector(along)
        n = max(3, int(length / 0.24) + 1)
        ph, amp = rng.uniform(0, 6.28), rng.uniform(0.04, 0.1)
        pts = []
        for k in range(n):
            d = length * k / (n - 1)
            pts.append(top + Vector((0, 0, -d)) + along * (amp * math.sin(d * 3.1 + ph)) + normal * (0.025 + 0.012 * math.sin(d * 5 + ph)))
        tube(acc, pts, 0.012, M.vine, 3)
        for k in range(1, n):
            p = pts[k]
            side = 1 if k % 2 else -1
            size = rng.uniform(0.11, 0.16) * (1.0 - 0.3 * k / n)
            nrm = normal + along * rng.uniform(-0.35, 0.35) + Vector((0, 0, rng.uniform(-0.1, 0.4)))
            tip = Vector((0, 0, -1)) + along * (side * rng.uniform(0.2, 0.9))
            leaf(acc, p + along * (side * 0.07) + normal * 0.02, nrm, tip, size, M.ivy[rng.randrange(3)])
            if rng.random() < 0.45:
                leaf(acc, p - along * (side * 0.05) + normal * 0.035, nrm, Vector((0, 0, -1)) - along * side * 0.5,
                     size * 0.8, M.ivy[rng.randrange(3)])

    back_n, back_a = (0, -1, 0), (1, 0, 0)
    for x, L in ((-5.0, 3.0), (-2.1, 2.5), (-1.5, 3.2), (2.3, 2.8), (5.15, 3.3)):
        ivy((x, WALL_IN, 6.55), back_n, back_a, L)
    for cx, dx, L in ((BACK_WINDOWS[0], -0.62, 1.0), (BACK_WINDOWS[1], 0.6, 0.9), (BACK_WINDOWS[2], -0.58, 1.3)):
        ivy((cx + dx, WALL_IN - 0.01, WIN_Z0 + WIN_H - 0.45), back_n, back_a, L)
    left_n, left_a = (1, 0, 0), (0, 1, 0)
    for z, L in ((-4.4, 3.0), (0.25, 2.4), (1.2, 3.4), (3.0, 2.6), (4.6, 3.1)):
        ivy((-WALL_IN, -z, 6.55), left_n, left_a, L)
    for z, L in ((LEFT_WIN_Z - 0.62, 1.0), (LEFT_WIN_Z + 0.65, 1.4)):
        ivy((-WALL_IN + 0.01, -z, LEFT_WIN_Z0 + WIN_H - 0.45), left_n, left_a, L)

    # ── moss: along the damp foot of the left wall, on the ledge and the sills ──
    for z, r in ((-2.3, 0.32), (-0.9, 0.25), (0.55, 0.3), (2.6, 0.28), (3.35, 0.22), (5.1, 0.3)):
        mound(acc, B(-5.12, 0.0, z), 0.14, r, rng.uniform(0.08, 0.13), M.moss[rng.randrange(2)], rng)
    for x, r in ((-4.0, 0.16), (-1.75, 0.22), (TRICKLE_X, 0.2), (4.3, 0.15)):
        mound(acc, (x, 5.12, LEDGE_TOP - 0.01), r, 0.12, 0.06, M.moss[rng.randrange(2)], rng)
    for cx in BACK_WINDOWS:
        mound(acc, (cx - 0.5 if cx != BACK_WINDOWS[0] else cx + 0.5, WALL_IN - 0.1, sz - 0.01), 0.16, 0.08, 0.05, M.moss[0], rng)
    for x, z, r in ((-4.6, -1.9, 0.16), (-3.95, 5.15, 0.2), (5.2, 5.0, 0.18)):
        mound(acc, B(x, -0.01, z), r, r * 0.8, 0.07, M.moss[1], rng)
    return acc.build("shell", root)


# ───────────────────────── decor (room coordinates) ─────────────────────────

POT = [(0, 0), (0.27, 0), (0.30, 0.03), (0.35, 0.32), (0.385, 0.40), (0.39, 0.45), (0.36, 0.465), (0.33, 0.44),
       (0.32, 0.41), (0, 0.41)]


def rake_parts(acc, M, X=None):
    """The hand rake in its own frame: the head at the origin, the handle up +Z, the tines down, curling to -Y."""
    def xf(p):
        return X @ Vector(p) if X is not None else Vector(p)
    R = X if X is not None else Matrix.Identity(4)
    lathe(acc, (0, 0, 0.06), [(0, 0), (0.022, 0), (0.024, 1.3), (0.02, 1.34), (0, 1.34)], 6, M.rake_wood, M=R,
          smooth=lambda i: i == 1)
    lathe(acc, (0, 0, -0.005), [(0, 0), (0.031, 0), (0.031, 0.09), (0, 0.09)], 6, M.iron, M=R)
    ring = [xf((0.045 * math.cos(a), 0, 1.45 + 0.045 * math.sin(a))) for a in np.linspace(0, 2 * math.pi, 9)[:-1]]
    tube(acc, ring, 0.009, M.iron, 3, closed=True)
    cuboid(acc, (-0.22, -0.018, -0.035), (0.22, 0.018, 0.0), M.iron, M=R)
    for x in (-0.2, -0.1, 0.0, 0.1, 0.2):
        tube(acc, [xf(p) for p in ((x, 0, -0.02), (x, 0, -0.1), (x, -0.025, -0.15), (x, -0.06, -0.18))], 0.011, M.iron, 4)


def build_decor(kit, M, root, rng):
    acc = Acc()
    ware_uv = lambda i, j, k: (j / 12 * 3, POT[k][1] / 0.45)
    # ── the fern pots: grey stoneware, the fronds (in "ferns") rise from their soil ──
    for x, z in FERN_POTS:
        lathe(acc, B(x, 0, z), POT, 12, lambda i: M.soil if i == 8 else (M.rim if i in (4, 5, 6, 7) else M.stoneware),
              ware_uv, smooth=lambda i: i in (2, 3))

    # ── on the ledge: feed jars with wooden lids, a damp cloth over the edge, a bucket ──
    for x, h, r, glaze in ((-3.92, 0.30, 0.12, M.stoneware), (-3.6, 0.22, 0.10, M.glaze), (-3.27, 0.34, 0.13, M.cream)):
        prof = [(0, 0), (r * 0.8, 0), (r, 0.04), (r * 1.06, h * 0.5), (r * 0.86, h * 0.86), (r * 0.62, h * 0.93),
                (r * 0.62, h), (0, h)]
        lathe(acc, B(x, LEDGE_TOP, -5.17), prof, 10, glaze, lambda i, j, k: (j / 10 * 2, prof[k][1] / h),
              smooth=lambda i: i in (2, 3, 4))
        lathe(acc, B(x, LEDGE_TOP + h, -5.17), [(0, 0), (r * 0.7, 0), (r * 0.7, 0.035), (r * 0.25, 0.05), (r * 0.18, 0.09), (0, 0.09)],
              10, M.timber)
    cuboid(acc, (-2.68, 4.9, LEDGE_TOP - 0.005), (-2.22, 5.14, LEDGE_TOP + 0.018), M.linen)
    cuboid(acc, (-2.68, 4.875, LEDGE_TOP - 0.3), (-2.22, 4.902, LEDGE_TOP + 0.018), M.linen)
    bucket = [(0, 0.04), (0.135, 0.04), (0.165, 0.29), (0.185, 0.29), (0.15, 0.0), (0, 0.0)]
    lathe(acc, B(3.95, LEDGE_TOP, -5.15), list(reversed(bucket)), 10, M.timber)
    for zb in (0.05, 0.22):
        rb = 0.152 + (0.183 - 0.152) * zb / 0.29
        lathe(acc, B(3.95, LEDGE_TOP + zb, -5.15), [(rb, 0), (rb + 0.012, 0), (rb + 0.012, 0.03), (rb, 0.03), (rb, 0)], 10, M.iron)

    # ── the long rake leaning on the left wall: its foot on the floor, its head up against the wall, the tines to
    #    the room ──
    X = (Matrix.Translation((-4.98, -1.6, 0.0)) @ Matrix.Rotation(-0.28, 4, 'Y') @ Matrix.Translation((0, 0, 1.46))
         @ Matrix.Rotation(math.pi, 4, 'X') @ Matrix.Rotation(math.pi / 2, 4, 'Z'))
    rake_parts(acc, M, X)

    # ── the drain where the tanks are emptied: an iron grate on a dark hole ──
    dx, dy = DRAIN[0], -DRAIN[1]
    cuboid(acc, (dx - 0.27, dy - 0.27, -0.01), (dx + 0.27, dy + 0.27, 0.008), M.hole)
    for a, b in (((-0.27, -0.27), (0.27, -0.22)), ((-0.27, 0.22), (0.27, 0.27)), ((-0.27, -0.22), (-0.22, 0.22)), ((0.22, -0.22), (0.27, 0.22))):
        cuboid(acc, (dx + a[0], dy + a[1], 0.0), (dx + b[0], dy + b[1], 0.022), M.iron)
    for i in range(5):
        x = dx - 0.16 + i * 0.08
        cuboid(acc, (x - 0.014, dy - 0.22, 0.0), (x + 0.014, dy + 0.22, 0.018), M.iron)

    # ── the trickle's spout: a carved stone channel out of the back wall, the water's thread leaves its lip at
    #    (TRICKLE_X, 2.75); a wet streak runs down the wall below it ──
    tx = TRICKLE_X
    stone = BoxUV((2 * (LEDGE_FACE[2] - LEDGE_FACE[0]) * 0.006, 0.5))
    cuboid(acc, (tx - 0.25, 5.3, 2.56), (tx + 0.25, WALL_IN + 0.01, 3.02), M.ledge, stone)
    cuboid(acc, (tx - 0.12, 5.08, 2.64), (tx + 0.12, 5.31, 2.72), M.ledge, stone)
    for s in (-1, 1):
        a, b = sorted((tx + s * 0.075, tx + s * 0.12))
        cuboid(acc, (a, 5.08, 2.72), (b, 5.31, 2.82), M.ledge, stone)
    cuboid(acc, (tx - 0.18, 5.2, 2.95), (tx + 0.18, 5.31, 3.02), M.ledge, stone)
    sx0, sz0, sw, shh = tx - 0.32, LEDGE_TOP, 0.64, 2.6 - LEDGE_TOP
    streak = kit.material(PRE + "streak", streak_decal(kit, "streak", M.wall_px, M.wall_tile, M.wall_off, sx0, sz0, sw, shh))
    quad(acc, (tx, WALL_IN - 0.006, sz0 + shh / 2), (1, 0, 0), (0, 0, 1), sw, shh, streak, (0, -1, 0))

    # ── a lantern hung from an iron bracket on the left wall, over the ledger ──
    lx, ly, lz = -4.98, 3.75, 2.36
    cuboid(acc, (-WALL_IN - 0.01, ly - 0.04, 2.96), (lx + 0.03, ly + 0.04, 3.0), M.iron)
    beam(acc, (-WALL_IN, ly, 2.66), (-5.12, ly, 2.97), 0.03, M.iron)
    cuboid(acc, (-WALL_IN - 0.01, ly - 0.06, 2.6), (-WALL_IN + 0.02, ly + 0.06, 3.04), M.iron)
    tube(acc, [(lx, ly, 2.96), (lx, ly, 2.86)], 0.008, M.iron, 3)
    ring = [(lx, ly + 0.03 * math.cos(a), lz + 0.47 + 0.03 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 8)[:-1]]
    tube(acc, ring, 0.008, M.iron, 3, closed=True)
    lathe(acc, (lx, ly, lz), [(0, 0), (0.135, 0), (0.135, 0.03), (0, 0.03)], 6, M.iron)
    for j in range(6):
        a = 2 * math.pi * j / 6
        p = (lx + 0.112 * math.cos(a), ly + 0.112 * math.sin(a))
        beam(acc, (p[0], p[1], lz + 0.02), (p[0], p[1], lz + 0.28), 0.022, M.iron)
    lathe(acc, (lx, ly, lz + 0.27), [(0, 0), (0.155, 0), (0.15, 0.025), (0.05, 0.13), (0.03, 0.15), (0, 0.15)], 6, M.iron)
    return acc


def lantern_panes(acc, M):
    """The lantern's glass: the painted pane once on each of its six faces (u across the face, v up)."""
    lx, ly, lz = -4.98, 3.75, 2.36
    for j in range(6):
        a0, a1 = 2 * math.pi * j / 6, 2 * math.pi * (j + 1) / 6
        p0 = Vector((lx + 0.107 * math.cos(a0), ly + 0.107 * math.sin(a0), lz + 0.03))
        p1 = Vector((lx + 0.107 * math.cos(a1), ly + 0.107 * math.sin(a1), lz + 0.03))
        h = Vector((0, 0, 0.24))
        mid = (a0 + a1) / 2
        acc.face([p0, p1, p1 + h, p0 + h], M.lantern_glass, [(0, 0), (1, 0), (1, 1), (0, 1)],
                 out=(math.cos(mid), math.sin(mid), 0))


# ───────────────────────── ferns (room coordinates, swaying) ─────────────────────────

def build_ferns(M, root, rng):
    acc = Acc()
    for pi_, (x, z) in enumerate(FERN_POTS):
        base = B(x, 0.41, z)
        off = rng.uniform(0, 6.28)
        for k in range(8):  # the outer ring of long arching fronds, dark and mid
            yaw = off + k * 2 * math.pi / 8 + rng.uniform(-0.15, 0.15)
            frond(acc, base + Vector((math.cos(yaw), math.sin(yaw), 0)) * 0.05, yaw, rng.uniform(0.62, 0.8),
                  rng.uniform(0.38, 0.5), rng.uniform(0.3, 0.45), rng.uniform(0.12, 0.145), 6,
                  M.fern[2] if k % 2 else M.fern[1], roll=rng.uniform(-0.35, 0.35))
        for k in range(3):  # young fronds standing up in the middle, light
            yaw = off + 0.4 + k * 2 * math.pi / 3 + rng.uniform(-0.2, 0.2)
            frond(acc, base, yaw, rng.uniform(0.3, 0.42), rng.uniform(0.5, 0.62), rng.uniform(0.12, 0.2),
                  rng.uniform(0.1, 0.12), 4, M.fern[0], roll=rng.uniform(-0.3, 0.3))
    return acc.build("ferns", root)


# ───────────────────────── furniture (each in its own frame) ─────────────────────────

CROCK = [(0, 0), (0.44, 0), (0.48, 0.02), (0.52, 0.06), (0.60, 0.15), (0.655, 0.27), (0.675, 0.38), (0.665, 0.48),
         (0.63, 0.57), (0.60, 0.62), (0.625, 0.64), (0.645, 0.665), (0.635, 0.69), (0.60, 0.70), (0.56, 0.70),
         (0.53, 0.69), (0.52, 0.66), (0.52, 0.22), (0.47, 0.14), (0, 0.14)]


def build_crock(M, root):
    """The holding crock: squat grey stoneware (rim at 0.70 m like the built-in, so the game's water disc at 0.6 m and
    its resting snails on the rim at r 0.58 sit right), a landing net leaning on it, its wooden lid behind."""
    acc = Acc()

    def mat(i):
        if 1 <= i <= 8:
            return M.stoneware
        if 9 <= i <= 14:
            return M.rim
        return M.inside if i >= 15 else M.stoneware

    lathe(acc, (0, 0, 0), CROCK, 20, mat, lambda i, j, k: (j / 20 * 4, min(CROCK[k][1], 0.62) / 0.62),
          smooth=lambda i: 1 <= i <= 8 or 16 <= i <= 17)
    # the landing net: handle on the floor, resting on the rim bead, its hoop over the water
    a, b = Vector((0.95, 0.05, 0.0)), Vector((0.488, 0.05, 1.05))
    lathe(acc, (0, 0, 0), [(0, 0), (0.021, 0), (0.021, (b - a).length), (0, (b - a).length)], 6, M.rake_wood,
          M=Matrix.Translation(a) @ Vector((0, 0, 1)).rotation_difference((b - a).normalized()).to_matrix().to_4x4())
    hc = b + (b - a).normalized() * 0.165
    hoop = [hc + Vector((0.16 * math.cos(t), 0.16 * math.sin(t), 0.03 * math.cos(t))) for t in np.linspace(0, 2 * math.pi, 11)[:-1]]
    tube(acc, hoop, 0.014, M.wood_dark, 4, closed=True)
    lathe(acc, (hc.x, hc.y, hc.z - 0.3), [(0, 0), (0.05, 0.01), (0.1, 0.07), (0.14, 0.17), (0.152, 0.27), (0.15, 0.29), (0, 0.29)], 10,
          M.net, smooth=True)
    # the lid, standing behind on its edge and leaning in
    n = Vector((0, 0.95, 0.3)).normalized()
    L = Matrix.Translation((-0.15, 0.72, 0.47)) @ Vector((0, 0, 1)).rotation_difference(n).to_matrix().to_4x4()
    lathe(acc, (0, 0, 0), [(0, 0), (0.45, 0), (0.45, 0.05), (0, 0.05)], 16, M.timber, M=L)
    cuboid(acc, (-0.22, -0.035, 0.05), (0.22, 0.035, 0.1), M.wood_dark, M=L)
    return acc.build("crock", root)


def build_bag(M, root, rng):
    """The druids' bag: patched green canvas, its painted front (flap, buckled straps, patch) on the side the game
    turns to the room (+Z), a shoulder strap looped down its left side, a scroll, a bottle and a bundle of blue cloth
    poking out of its mouth."""
    acc = Acc()
    levels = [(0.0, 0.36, 0.20), (0.04, 0.42, 0.245), (0.25, 0.46, 0.27), (0.55, 0.46, 0.265), (0.80, 0.43, 0.245),
              (0.90, 0.41, 0.225)]
    nseg = 20

    def ring(z, a, b):
        pts = []
        for j in range(nseg):
            t = 2 * math.pi * (j + 0.5) / nseg - math.pi / 2
            c, s = math.cos(t), math.sin(t)
            pts.append(Vector((a * math.copysign(abs(c) ** (2 / 3.2), c), b * math.copysign(abs(s) ** (2 / 3.2), s), z)))
        return pts

    rings = [ring(*l) for l in levels]
    canvas_uv = BoxUV(kit_tile(CANVAS, 0.007))
    zf0, zf1, xf = 0.02, 0.9, 0.43
    for i in range(len(rings) - 1):
        for j in range(nseg):
            j1 = (j + 1) % nseg
            q = [rings[i][j], rings[i][j1], rings[i + 1][j1], rings[i + 1][j]]
            c = sum(q, Vector()) / 4
            n = _normal(q)
            out = Vector((c.x, c.y, 0))
            if n.dot(out) < 0:
                n = -n
            n.normalize()
            if n.y < -0.55 and abs(c.x) < 0.42:
                uvs = [((p.x + xf) / (2 * xf), (p.z - zf0) / (zf1 - zf0)) for p in q]
                acc.face(q, M.bag_front, uvs, out=out)
            else:
                acc.face(q, M.canvas, canvas_uv, out=out)
    acc.face(rings[0], M.canvas, canvas_uv, out=(0, 0, -1))
    mouth = Vector((0, 0, 0.83))
    for j in range(nseg):
        acc.face([rings[-1][j], rings[-1][(j + 1) % nseg], mouth], M.bag_inside, out=(0, 0, 1))
    # the shoulder strap, looping out from the left side, with a leather buckle near its top
    sp = [Vector((-0.41 - 0.2 * math.sin(math.pi * s), 0.05, 0.84 - 0.68 * s)) for s in np.linspace(0, 1, 9)]
    strap(acc, sp, 0.075, 0.018, M.canvas, Vector((0, 1, 0)))
    bk = sp[2]
    cuboid(acc, (bk.x - 0.02, 0.0, bk.z - 0.07), (bk.x + 0.02, 0.1, bk.z + 0.05), M.leather)
    # what pokes out of the mouth
    mound(acc, (0.03, -0.01, 0.8), 0.2, 0.15, 0.2, M.cloth_blue, rng, seg=9)
    mound(acc, (-0.08, 0.05, 0.84), 0.13, 0.11, 0.17, M.cloth_blue, rng, seg=7)
    S = Matrix.Translation((-0.2, 0.06, 0.62)) @ Matrix.Rotation(-0.42, 4, 'Y') @ Matrix.Rotation(-0.12, 4, 'X')
    lathe(acc, (0, 0, 0), [(0, 0), (0.065, 0), (0.068, 0.44), (0, 0.44)], 9, M.parchment, M=S, smooth=lambda i: i == 1)
    Bt = Matrix.Translation((0.2, 0.05, 0.6)) @ Matrix.Rotation(0.18, 4, 'Y')
    lathe(acc, (0, 0, 0), [(0, 0), (0.1, 0), (0.105, 0.28), (0.08, 0.34), (0.04, 0.38), (0.035, 0.46), (0.043, 0.47), (0, 0.47)],
          10, M.bottle, M=Bt, smooth=lambda i: i in (1, 2, 3))
    lathe(acc, (0, 0, 0.47), [(0, 0), (0.04, 0), (0.046, 0.065), (0, 0.065)], 8, M.cork, M=Bt)
    return acc.build("bag", root)


def kit_tile(box, px):
    return (2 * (box[2] - box[0]) * px, 2 * (box[3] - box[1]) * px)


def build_ledger(M, root):
    """The breeding ledger open on a wooden lectern stand: two trestle legs on feet, a stretcher and braces, a
    sloped top (tilted 0.42 rad toward the front, like the built-in) with a lip, the book, and the candle on a little
    side shelf at the built-in's place."""
    acc = Acc()
    wh = BoxUV(M.wood_tile)
    wv = BoxUV((M.wood_tile[1], M.wood_tile[0]))
    for sx in (-1, 1):
        x = sx * 0.3
        cuboid(acc, (x - 0.05, -0.22, 0.0), (x + 0.05, 0.22, 0.08), M.wood, wh)
        cuboid(acc, (x - 0.035, -0.035, 0.08), (x + 0.035, 0.035, 0.93), M.wood_v, wv)
        beam(acc, (x - sx * 0.02, 0, 0.66), (sx * 0.13, 0, 0.92), 0.045, M.wood, wh)
    cuboid(acc, (-0.3, -0.03, 0.22), (0.3, 0.03, 0.3), M.wood, wh)
    for y0, y1 in ((-0.2, -0.15), (0.15, 0.2)):
        cuboid(acc, (-0.37, y0, 0.9), (0.37, y1, 0.97), M.wood, wh)
    for sx in (-1, 1):
        a, b = sorted((sx * 0.32, sx * 0.37))
        cuboid(acc, (a, -0.2, 0.9), (b, 0.2, 0.97), M.wood, wh)
        # a cheek under each end of the slope
        prism_x(acc, [(-0.2, 0.97), (0.2, 0.97), (0.2, 1.17), (-0.2, 1.0)], a, b, M.wood, wh)
    T = Matrix.Translation((0, -0.02, 1.08)) @ Matrix.Rotation(0.42, 4, 'X')
    cuboid(acc, (-0.4, -0.29, -0.02), (0.4, 0.29, 0.02), M.wood, wh, M=T)
    cuboid(acc, (-0.4, -0.32, -0.02), (0.4, -0.285, 0.07), M.wood_dark, M=T)
    cuboid(acc, (-0.37, -0.255, 0.02), (0.37, 0.255, 0.035), M.cover, M=T)
    cuboid(acc, (-0.355, -0.235, 0.035), (-0.008, 0.235, 0.064), M.page, M=T)
    cuboid(acc, (0.008, -0.235, 0.035), (0.355, 0.235, 0.064), M.page, M=T)
    n = T.to_3x3() @ Vector((0, 0, 1))
    quad(acc, T @ Vector((0, 0, 0.0645)), T.to_3x3() @ Vector((1, 0, 0)), T.to_3x3() @ Vector((0, 1, 0)), 0.71, 0.47, M.book, n)
    cuboid(acc, (0.05, -0.30, 0.064), (0.075, -0.1, 0.069), M.ribbon, M=T)
    cuboid(acc, (0.05, -0.335, -0.1), (0.075, -0.322, 0.066), M.ribbon, M=T)
    # the candle on its shelf
    cuboid(acc, (0.37, -0.13, 0.93), (0.53, 0.03, 0.97), M.wood_dark)
    beam(acc, (0.37, -0.05, 0.86), (0.47, -0.05, 0.93), 0.03, M.wood_dark)
    lathe(acc, (0.45, -0.05, 0.97), [(0, 0), (0.036, 0), (0.036, 0.14), (0.022, 0.15), (0, 0.15)], 8, M.candle,
          smooth=lambda i: i == 1)
    lathe(acc, (0.45, -0.05, 1.12), [(0, 0), (0.019, 0.02), (0.013, 0.05), (0, 0.075)], 6, M.flame)
    return acc.build("ledger", root)


def build_stool(M, root):
    """A three-legged stool with a thick round seat (its painted end grain on top) on a little reed mat."""
    acc = Acc()
    seat = [(0, 0), (0.29, 0), (0.30, 0.012), (0.30, 0.088), (0.29, 0.10), (0, 0.10)]

    def uv(i, j, k):
        if i == 4:  # the top: the painted seat, mapped back from its ellipse onto the circle
            r, z = seat[k]
            a = 2 * math.pi * j / 16
            return (0.5 + 0.5 * r / 0.3 * math.cos(a), 0.5 + 0.5 * r / 0.3 * math.sin(a))
        return (j / 16 * 3, 0.5 + seat[k][1] / 0.2)

    lathe(acc, (0, 0, 0.45), seat, 16, lambda i: M.stool_top if i == 4 else M.stool_side, uv)
    wv = BoxUV((M.wood_tile[1], M.wood_tile[0]))
    for i in range(3):
        a = 0.4 + i * 2 * math.pi / 3
        c, s = math.cos(a), math.sin(a)
        top, foot = Vector((0.17 * c, 0.17 * s, 0.46)), Vector((0.29 * c, 0.29 * s, 0.0))
        beam(acc, foot, top, 0.055, M.wood_v, wv)
        k = 0.17 / 0.46
        p = foot + (top - foot) * k
        beam(acc, p, Vector((0, 0, p.z)), 0.038, M.wood_v, wv)
    lathe(acc, (0, 0, 0.145), [(0, 0), (0.045, 0), (0.045, 0.05), (0, 0.05)], 6, M.wood_dark)
    # the reed mat
    cuboid(acc, (-0.62, -0.42, 0.0), (0.62, 0.42, 0.022), M.reed)
    for x in (-0.42, -0.14, 0.14, 0.42):
        cuboid(acc, (x - 0.014, -0.425, 0.0), (x + 0.014, 0.425, 0.027), M.reed_bind)
    for sx in (-1, 1):
        a, b = sorted((sx * 0.6, sx * 0.66))
        cuboid(acc, (a, -0.42, 0.0), (b, 0.42, 0.034), M.reed_bind)
    return acc.build("stool", root)


def build_rake(M, root):
    acc = Acc()
    rake_parts(acc, M)
    return acc.build("rake", root)


def build(kit):
    """The room the game uses: the hidden grotto (pools_grotto.py), with this file's crock, bag, ledger, stool and rake."""
    from assets import pools_grotto
    return pools_grotto.build(kit)


def build_stone_room(kit):
    """The first version: a wet stone room with tanks (replaced by the grotto, 2026-10-26)."""
    rng = random.Random(1310)
    sheet = kit.load_sheet(SHEET)
    house = kit.load_sheet(HOUSE)
    M = Materials(kit, sheet, house)
    root = kit.root("pools-room")
    build_shell(kit, M, root, rng)
    decor = build_decor(kit, M, root, rng)
    lantern_panes(decor, M)
    decor.build("decor", root)
    build_ferns(M, root, rng)
    build_crock(M, root)
    build_bag(M, root, rng)
    build_ledger(M, root)
    build_stool(M, root)
    build_rake(M, root)
    return root, 6.6
