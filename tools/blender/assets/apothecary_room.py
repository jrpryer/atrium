"""The Apothecary's room kit: the inside of the low plaster shop, built part by part in the same frame as the
game's built-in shapes it replaces (see InteriorScene.Prop). Reference: art/reference/sheets/apothecary-room-sheet.png
(props) and art/reference/sheets/apothecary-sheet.png (the house: its plaster, violet and timber).

Parts (each named for the key the game asks for):
- shell: floorboards, the cream plaster back and left walls with dark timber posts and skirting, the bowed window
  (a bay out through the left wall with its seat of jars), a brick chimney breast with an open hearth, ash on the
  boards in front of it, and a woven violet rug;
- furniture: the wall of shelves above two rows of little drawers, the long bench with sacks and pots under it, the
  brass tray, the map board's frame, the lectern, the crate of bottles, a stool, a potted herb and the sconce;
- herbs: the drying beam and its bundles (drawn with sway);
- lantern: the hanging iron lantern, round its own origin (drawn at a swinging transform);
- vessel: the copper pot on its iron trivet, round its own origin, open so the game's liquid shows inside;
- map-board: the Discovery Map's paper, with the routes between the game's map nodes painted on it;
- book: the formulary, in the lectern top's own frame.

Coordinates. Everything is laid out in the game's frame (Y up, metres; the back wall at z = -5.4, the left wall at
x = -5.4) and converted to Blender's (Z up) as game (x, y, z) -> Blender (x, -z, y); the glTF exporter converts back.
Every surface the game sets things on (shelf boards, the bench top, the tray, the lectern top, the map paper, the
vessel's bowl) sits exactly where ApothecaryProps.cs / ApothecaryScene.cs put the built-in one.
"""
import math

import numpy as np
from mathutils import Vector

ROOM_SHEET = "apothecary-room-sheet.png"
HOUSE_SHEET = "apothecary-sheet.png"
P = "apothecary-room-"

# ── the game's layout (ApothecaryProps.cs) ──
SHELF_L, SHELF_R, SHELF_Z = -2.15, 4.55, -4.95
SHELF_ROWS = (1.28, 2.1, 2.92, 3.74)
BENCH_TOP, BENCH_Z, BENCH_L, BENCH_R = 1.02, -2.3, -2.0, 4.4
VESSEL = (1.25, BENCH_TOP, -2.5)
TRAY = (1.25, BENCH_TOP, -1.86)
MAP_C = (-3.85, 2.75, -5.34)
MAP_W, MAP_H = 2.3, 1.62
LECTERN = (5.05, 0.0, -0.85)
CRATE = (-2.85, 0.0, -1.35)
SCONCE = (-2.85, 2.95, -5.25)

# ── the shell (InteriorScene.BuildShell: the back wall's face at z = -5.4, the left wall's at x = -5.4) ──
WALL_IN, WALL_OUT, WALL_TOP = -5.4, -5.8, 6.5
ROOM_END = 5.6                       # the open (cutaway) ends of the back and left walls
# the chimney breast on the left wall, between the back corner and the bowed window
CH_Z0, CH_Z1 = -4.6, -2.8            # the breast's width
CH_FRONT = -4.8                      # its face
CH_BASE = 0.2                        # the raised stone hearth
JAMB = 0.38                          # each jamb's width
CH_MANTEL0, CH_MANTEL1 = 1.3, 1.54   # the mantel beam
CH_HOOD_TOP = 3.0
STACK_Z0, STACK_Z1, STACK_FRONT = -4.15, -3.25, -5.0
# the bowed window: a five-light bay out through the left wall, seen from inside
BAY_Z = -1.25                        # its centre along the wall
BAY_SILL, BAY_HEAD = 0.95, 2.85
BAY_PLAN = [(-1.0, 0.0), (-0.69, 0.36), (-0.25, 0.55), (0.23, 0.55), (0.68, 0.36), (1.0, 0.0)]  # (along, out)

# ── sheet boxes (real pixels, from the top-left) ──
FLOOR = (51, 522, 229, 714)          # floorboard patch, cut on the plank seams
PX_FLOOR = 0.0099                    # a painted plank (~22 px) is ~0.22 m
PLASTER = (1560, 985, 1990, 1245)    # house sheet: the back wall's plaster below its window
PX_PLASTER = 0.0078                  # the house's scale
PLASTER_CALM = 0.5
PLANKS = (45, 131, 275, 161)         # the shelving's back planks between two shelves
PX_PLANKS = 0.0075
SHELF_EDGE = (45, 76, 275, 84)       # a shelf board's front edge, ink lines and all
DRAWER = (88, 226, 130, 255)         # one drawer front with its brass cup pull
BENCH_WOOD = (345, 113, 660, 125)    # the bench top's planks (grain along x)
BENCH_EDGE = (345, 128, 660, 137)
LEG_WOOD = (344, 150, 362, 250)      # a bench leg (grain up)
FRAME_WOOD = (336, 330, 348, 465)    # the blank frame's side member (grain up)
MANTEL = (1040, 486, 1310, 510)      # the fireplace's mantel beam
BRICK = (1115, 276, 1235, 345)       # the fireplace hood
PX_BRICK = 0.0069                    # the fireplace is ~1.8 m across its jambs (262 px)
FIREBOX = (1104, 518, 1247, 672)     # the hearth's opening: sooty back bricks and the fire basket
SOOT = (1112, 524, 1240, 600)
HEARTH = (1040, 682, 1310, 708)      # the stone base course
COPPER = (955, 112, 1035, 160)       # the copper pot's belly
PX_COPPER = 0.0096
TRAY_BRASS = (1135, 158, 1265, 176)
CRATE_SIDE = (42, 397, 242, 487)
LANTERN_PANE = (884, 402, 928, 470)
MAP_PAPER = (352, 328, 456, 466)     # the blank frame's paper (portrait; turned for the landscape map)
PAPER = (340, 535, 510, 700)         # the paper patch
SAGE = (572, 385, 608, 440)          # herb bundles: sage, tansy, rosemary
TANSY = (664, 414, 700, 446)
ROSEMARY = (748, 378, 786, 425)
BAY_GLASS = (195, 1036, 458, 1262)   # house sheet: the bay's lights, jars and all


# ───────────────────────── small helpers ─────────────────────────

def g2b(p):
    """A game point (Y up) in Blender (Z up)."""
    x, y, z = p
    return (x, -z, y)


def hexc(h):
    return ((h >> 16 & 255) / 255, (h >> 8 & 255) / 255, (h & 255) / 255)


def calm(arr, k):
    a = arr.copy()
    mean = a[..., :3].reshape(-1, 3).mean(axis=0)
    a[..., :3] = mean + (a[..., :3] - mean) * k
    a[..., 3] = 1.0
    return a


def crop(sheet, box):
    x0, y0, x1, y1 = box
    a = sheet[y0:y1, x0:x1].copy()
    a[..., 3] = 1.0
    return a


def hmirror(a):
    """Mirrored left-to-right into a block twice as wide, so it repeats sideways without a seam."""
    return np.concatenate([a, a[:, ::-1]], axis=1)


def resize(a, w, h):
    """Bilinear resize of an image array (rows from the top)."""
    H, W = a.shape[:2]
    ys = np.clip((np.arange(h) + 0.5) * H / h - 0.5, 0, H - 1)
    xs = np.clip((np.arange(w) + 0.5) * W / w - 0.5, 0, W - 1)
    y0 = np.floor(ys).astype(int)
    x0 = np.floor(xs).astype(int)
    y1 = np.minimum(y0 + 1, H - 1)
    x1 = np.minimum(x0 + 1, W - 1)
    fy = (ys - y0)[:, None, None]
    fx = (xs - x0)[None, :, None]
    top = a[y0][:, x0] * (1 - fx) + a[y0][:, x1] * fx
    bot = a[y1][:, x0] * (1 - fx) + a[y1][:, x1] * fx
    return top * (1 - fy) + bot * fy


def blur(a, passes=1):
    for _ in range(passes):
        a = (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5
    return a


def paint_line(img, p0, p1, width, color, alpha=1.0, soft=1.0):
    """Paints an anti-aliased segment (pixel coordinates, x right, y down) onto an image array."""
    H, W = img.shape[:2]
    x0 = int(max(0, min(p0[0], p1[0]) - width - 2))
    x1 = int(min(W, max(p0[0], p1[0]) + width + 3))
    y0 = int(max(0, min(p0[1], p1[1]) - width - 2))
    y1 = int(min(H, max(p0[1], p1[1]) + width + 3))
    if x0 >= x1 or y0 >= y1:
        return
    ys, xs = np.mgrid[y0:y1, x0:x1].astype(np.float32) + 0.5
    ax, ay = p0
    dx, dy = p1[0] - ax, p1[1] - ay
    L2 = max(dx * dx + dy * dy, 1e-6)
    t = np.clip(((xs - ax) * dx + (ys - ay) * dy) / L2, 0, 1)
    d = np.hypot(xs - (ax + t * dx), ys - (ay + t * dy))
    m = np.clip((width / 2 - d) / soft + 0.5, 0, 1)[..., None] * alpha
    img[y0:y1, x0:x1, :3] = img[y0:y1, x0:x1, :3] * (1 - m) + np.array(color) * m


def paint_disc(img, c, r, color, alpha=1.0):
    H, W = img.shape[:2]
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32) + 0.5
    d = np.hypot(xs - c[0], ys - c[1])
    m = np.clip(r - d + 0.5, 0, 1)[..., None] * alpha
    img[..., :3] = img[..., :3] * (1 - m) + np.array(color) * m


class DotNetRandom:
    """System.Random(seed) as .NET runs it (the seeded, subtractive generator), so the map's routes can be painted
    exactly where ApothecaryScene.MapNodes puts the nodes."""
    MBIG, MSEED = 2147483647, 161803398

    def __init__(self, seed):
        sa = [0] * 56
        sub = 2147483647 if seed == -2147483648 else abs(seed)
        mj = self.MSEED - sub
        sa[55] = mj
        mk = 1
        for i in range(1, 55):
            ii = (21 * i) % 55
            sa[ii] = mk
            mk = mj - mk
            if mk < 0:
                mk += self.MBIG
            mj = sa[ii]
        for _ in range(1, 5):
            for i in range(1, 56):
                sa[i] -= sa[1 + (i + 30) % 55]
                if sa[i] < 0:
                    sa[i] += self.MBIG
        self.sa, self.inext, self.inextp = sa, 0, 21

    def next_double(self):
        a = self.inext + 1 if self.inext + 1 < 56 else 1
        b = self.inextp + 1 if self.inextp + 1 < 56 else 1
        r = self.sa[a] - self.sa[b]
        if r == self.MBIG:
            r -= 1
        if r < 0:
            r += self.MBIG
        self.sa[a] = r
        self.inext, self.inextp = a, b
        return r * (1.0 / self.MBIG)


def map_nodes(count=30):
    """ApothecaryScene.MapNodes, relative to the map's centre (x right, y up)."""
    rng = DotNetRandom(13)
    w, h = MAP_W - 0.5, MAP_H - 0.45
    out = []
    for i in range(count):
        depth = int(math.floor(math.log2(i + 1)))
        across = (i + 1 - (1 << depth)) / float(1 << depth)
        x = -w / 2 + min(depth, 5) / 5 * w + (rng.next_double() - 0.5) * 0.12
        y = (across - 0.5 + 0.5 / (1 << depth)) * h + (rng.next_double() - 0.5) * 0.1
        if depth > 5:
            x -= rng.next_double() * 0.4
            y = (rng.next_double() - 0.5) * h
        out.append((x, y))
    return out


# ───────────────────────── geometry in the game's frame ─────────────────────────

class Kit:
    """Builds one part (a key) from many small objects, all in the game's frame, then joins them."""

    def __init__(self, kit, root):
        self.kit, self.root, self.objs = kit, root, []
        self.n = 0

    def _name(self, s):
        self.n += 1
        return f"{s}-{self.n}"

    def add(self, o, mat, tile=None):
        self.kit.assign(o, mat)
        if tile is not None:
            self.kit.box_uv(o, tile)
        self.objs.append(o)
        return o

    def box(self, lo, hi, mat, tile=1.0, name="box"):
        (x0, y0, z0), (x1, y1, z1) = lo, hi
        o = self.kit.box(self._name(name), ((x0 + x1) / 2, -(z0 + z1) / 2, (y0 + y1) / 2),
                         (abs(x1 - x0), abs(z1 - z0), abs(y1 - y0)), self.root)
        return self.add(o, mat, tile)

    def mesh(self, verts_g, faces, mat, tile=1.0, solid=True, name="mesh"):
        o = self.kit.mesh_object(self._name(name), [g2b(v) for v in verts_g], faces, self.root, solid=solid)
        return self.add(o, mat, tile)

    def prism_y(self, outline_xz, y0, y1, mat, tile=1.0, name="prism"):
        """A closed plan outline (game x, z) extruded up from y0 to y1."""
        n = len(outline_xz)
        v = [(x, y0, z) for x, z in outline_xz] + [(x, y1, z) for x, z in outline_xz]
        f = [tuple(reversed(range(n))), tuple(range(n, 2 * n))]
        for i in range(n):
            j = (i + 1) % n
            f.append((i, j, n + j, n + i))
        return self.mesh(v, f, mat, tile, True, name)

    def plaque(self, mat, centre_g, size, facing_g, name="plaque"):
        """A decal quad facing a game axis: '+x', '-x', '+z' (toward the camera) or '-z'."""
        facing = {"+x": "+x", "-x": "-x", "+z": "-y", "-z": "+y"}[facing_g]
        o = self.kit.plaque(self._name(name), mat, g2b(centre_g), size, facing, self.root)
        self.objs.append(o)
        return o

    def floor_quad(self, mat, x0, x1, z0, z1, y, name="floor-quad"):
        """An upward quad over game x0..x1, z0..z1 at height y, mapped once (u along +x, v toward -z)."""
        v = [(x0, -z1, y), (x1, -z1, y), (x1, -z0, y), (x0, -z0, y)]
        o = self.kit.mesh_object(self._name(name), v, [(0, 1, 2, 3)], self.root)
        if o.data.polygons[0].normal.z < 0:
            o.data.flip_normals()
        self.kit.assign(o, mat)
        self.kit.fit_uv(o)
        self.objs.append(o)
        return o

    def lathe(self, centre_g, profile, mat, n=12, tile=(1.0, 1.0), smooth=True, sharp=(), phase=0.0, scale=(1.0, 1.0),
              v_by_height=False, star=1.0, name="lathe"):
        """A surface of revolution round a vertical axis through centre_g: profile [(r, y)] from the bottom pole up
        (and back down an inner wall, if any) to the top pole, y relative to centre_g. Mapped round (u, at about
        tile[0] metres a repeat) and along the profile (v; or up it, with v_by_height); smooth normals except at
        the `sharp` profile points (smooth=False: flat facets)."""
        bx, by, bz = g2b(centre_g)
        m = len(profile)
        seg_len = [math.hypot(profile[i + 1][0] - profile[i][0], profile[i + 1][1] - profile[i][1]) for i in range(m - 1)]
        s = [0.0]
        for L in seg_len:
            s.append(s[-1] + L)
        rmax = max(r for r, _ in profile)
        U = max(1, round(2 * math.pi * rmax / tile[0])) if tile[0] > 0 else 1
        seg_n = []
        for i in range(m - 1):
            dr = profile[i + 1][0] - profile[i][0]
            dy = profile[i + 1][1] - profile[i][1]
            L = max(math.hypot(dr, dy), 1e-9)
            seg_n.append((dy / L, -dr / L))
        pt_n = []
        for i in range(m):
            if i == 0:
                pt_n.append(seg_n[0])
            elif i == m - 1:
                pt_n.append(seg_n[-1])
            else:
                a, b = seg_n[i - 1], seg_n[i]
                nr, ny = a[0] + b[0], a[1] + b[1]
                L = max(math.hypot(nr, ny), 1e-9)
                pt_n.append((nr / L, ny / L))
        verts, uvs_v = [], []
        for i, (r, y) in enumerate(profile):
            for k in range(n + 1):
                a = phase + 2 * math.pi * k / n
                rk = r * (star if k % 2 else 1.0)   # star < 1 ridges the surface (a bundle of leaves)
                verts.append((bx + rk * math.cos(a) * scale[0], by + rk * math.sin(a) * scale[1], bz + y))
        faces, corner = [], []  # corner: (profile index, k, segment index) per face corner
        for i in range(m - 1):
            r0, r1 = profile[i][0], profile[i + 1][0]
            for k in range(n):
                a, b = i * (n + 1) + k, i * (n + 1) + k + 1
                c, d = (i + 1) * (n + 1) + k + 1, (i + 1) * (n + 1) + k
                if r0 == 0:
                    faces.append((a, c, d))
                    corner.append([(i, k, i), (i + 1, k + 1, i), (i + 1, k, i)])
                elif r1 == 0:
                    faces.append((a, b, d))
                    corner.append([(i, k, i), (i, k + 1, i), (i + 1, k, i)])
                else:
                    faces.append((a, b, c, d))
                    corner.append([(i, k, i), (i, k + 1, i), (i + 1, k + 1, i), (i + 1, k, i)])
        o = self.kit.mesh_object(self._name(name), verts, faces, self.root)
        me = o.data
        me.uv_layers.new(name="UVMap")
        uv = me.uv_layers.active.data
        ymin = min(y for _, y in profile)
        normals = []
        for poly, cs in zip(me.polygons, corner):
            for li, (pi, k, si) in zip(poly.loop_indices, cs):
                v = (profile[pi][1] - ymin) / tile[1] if v_by_height else s[pi] / tile[1]
                uv[li].uv = (k / n * U, v)
                nr, ny = seg_n[si] if pi in sharp else pt_n[pi]
                a = phase + 2 * math.pi * k / n
                normals.append((nr * math.cos(a), nr * math.sin(a), ny))
        if smooth:
            for poly in me.polygons:
                poly.use_smooth = True
            me.normals_split_custom_set(normals)
        self.kit.assign(o, mat)
        self.objs.append(o)
        return o

    def rod(self, a_g, b_g, r, mat, n=6, name="rod"):
        """A capped round rod from game point a to b."""
        a, b = Vector(g2b(a_g)), Vector(g2b(b_g))
        d = b - a
        L = d.length
        q = Vector((0, 0, 1)).rotation_difference(d.normalized())
        verts = []
        for z in (0.0, L):
            for k in range(n):
                t = 2 * math.pi * k / n
                verts.append(a + q @ Vector((r * math.cos(t), r * math.sin(t), z)))
        f = [tuple(reversed(range(n))), tuple(range(n, 2 * n))]
        for k in range(n):
            j = (k + 1) % n
            f.append((k, j, n + j, n + k))
        o = self.kit.mesh_object(self._name(name), [tuple(v) for v in verts], f, self.root, solid=True)
        return self.add(o, mat, 1.0)

    def torus(self, centre_g, R, r, axis, mat, n=10, m=5, name="ring"):
        """A ring (a handle) round `axis` ('x' or 'z', game) through centre_g."""
        cx, cy, cz = centre_g
        verts = []
        for i in range(n):
            a = 2 * math.pi * i / n
            for j in range(m):
                b = 2 * math.pi * j / m
                rr = R + r * math.cos(b)
                u, w, h = rr * math.cos(a), rr * math.sin(a), r * math.sin(b)
                if axis == "z":   # the ring lies in the game's x-y plane
                    verts.append((cx + u, cy + w, cz + h))
                else:             # in the y-z plane
                    verts.append((cx + h, cy + w, cz + u))
        f = []
        for i in range(n):
            for j in range(m):
                i2, j2 = (i + 1) % n, (j + 1) % m
                f.append((i * m + j, i2 * m + j, i2 * m + j2, i * m + j2))
        return self.mesh(verts, f, mat, 1.0, True, name)

    def join(self, key, lean=None):
        if lean:
            for o in self.objs:
                self.kit.shear(o, *lean)
        return self.kit.join(key, self.objs)


def uv_by(obj, fn):
    """Sets every corner's UV from fn(world point (Blender), face normal (Blender))."""
    me = obj.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    for poly in me.polygons:
        n = poly.normal
        for li in poly.loop_indices:
            uv[li].uv = fn(me.vertices[me.loops[li].vertex_index].co, n)


def transform_g(v, rot_x=0.0, rot_y=0.0, move=(0, 0, 0)):
    """System.Numerics: CreateRotationX(rot_x) * CreateRotationY(rot_y) * CreateTranslation(move), applied to a
    game point (row vectors, so the X rotation goes first)."""
    x, y, z = v
    c, s = math.cos(rot_x), math.sin(rot_x)
    y, z = y * c - z * s, y * s + z * c
    c, s = math.cos(rot_y), math.sin(rot_y)
    x, z = x * c + z * s, -x * s + z * c
    return (x + move[0], y + move[1], z + move[2])


# lectern top: CreateRotationX(0.42) * CreateRotationY(-0.55) * CreateTranslation(LecternPos + (0, 1.06, 0))
LECTERN_RX, LECTERN_RY = 0.42, -0.55
LECTERN_TOP = (LECTERN[0], LECTERN[1] + 1.06, LECTERN[2])


def oriented_box(k, lo, hi, mat, tile, rx, ry, move, name="obox"):
    """A box given in a local game frame, turned and moved like MeshBuilder.Transform."""
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    v = [transform_g((x, y, z), rx, ry, move) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
    f = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    return k.mesh(v, f, mat, tile, True, name)


# ───────────────────────── materials ─────────────────────────

def make_materials(kit):
    room = kit.load_sheet(ROOM_SHEET)
    house = kit.load_sheet(HOUSE_SHEET)
    M = {}

    def mat(name, img=None, color=None):
        M[name] = kit.material(P + name, img, color if color is not None else (0.8, 0.8, 0.8))
        return M[name]

    def tex(name, arr, tile=True):
        a = arr.copy()
        a[..., 3] = 1.0
        return kit.swatch(a, P + name, 0, 0, a.shape[1], a.shape[0], tile=tile)

    lin = kit.srgb_to_linear
    # walls, floor
    plaster = calm(crop(house, PLASTER), PLASTER_CALM)
    plaster[..., :3] = np.clip(plaster[..., :3] * 1.04, 0, 1)
    mat("plaster", tex("plaster", plaster))
    mat("floor", kit.swatch(room, P + "floor", *FLOOR))
    # wood
    mat("planks", tex("planks", hmirror(crop(room, PLANKS)), tile=False))
    mat("shelf-edge", tex("shelf-edge", hmirror(crop(room, SHELF_EDGE)), tile=False))
    mat("drawer", kit.decal(room, P + "drawer", *DRAWER))
    mat("wood", kit.swatch(room, P + "wood", *BENCH_WOOD))
    mat("wood-edge", tex("wood-edge", hmirror(crop(room, BENCH_EDGE)), tile=False))
    mat("wood-up", kit.swatch(room, P + "wood-up", *LEG_WOOD))
    frame = crop(room, FRAME_WOOD)
    mat("frame-up", tex("frame-up", frame))
    mat("frame-along", tex("frame-along", np.ascontiguousarray(np.rot90(frame))))
    mantel = crop(room, MANTEL)
    mat("mantel", tex("mantel", mantel))
    mat("timber-up", tex("timber-up", np.ascontiguousarray(np.rot90(mantel))))
    violet = np.array(hexc(0x7A4A93))
    crown = crop(room, BENCH_WOOD)
    lum = crown[..., :3].mean(axis=2, keepdims=True)
    crown[..., :3] = np.clip(violet * (0.55 + 1.1 * (lum - lum.mean())) + crown[..., :3] * 0.18, 0, 1)
    mat("crown", tex("crown", crown))
    # brick, stone, the hearth
    mat("brick", kit.swatch(room, P + "brick", *BRICK))
    soot = crop(room, SOOT)
    mat("soot", tex("soot", soot))
    mat("firebox", kit.decal(room, P + "firebox", *FIREBOX))
    mat("hearth", kit.swatch(room, P + "hearth", *HEARTH))
    # metals and glass
    mat("copper", kit.swatch(room, P + "copper", *COPPER))
    mat("copper-dark", color=kit.sample(room, 975, 150, 3))
    mat("brass-tray", kit.swatch(room, P + "brass-tray", *TRAY_BRASS))
    mat("brass", color=kit.sample(room, 1200, 150, 3))
    mat("iron", color=kit.sample(room, 905, 377, 2))
    mat("glass", color=kit.sample(room, 86, 385, 3))
    mat("glass-amber", color=lin(hexc(0xC89A5A)))
    mat("glass-violet", color=lin(hexc(0xA88CC0)))
    mat("cork", color=lin(hexc(0xA9825A)))
    mat("parchment", color=kit.sample(room, 420, 600, 6))
    mat("candle", color=lin(hexc(0xF3EAD2)))
    mat("stoneware", color=lin(hexc(0xC9B48C)))
    mat("stoneware-dark", color=lin(hexc(0x7A5A43)))
    mat("clay", color=lin(hexc(0xB86E4B)))
    mat("sack", color=lin(hexc(0xC9B48C)))
    mat("sack-dark", color=lin(hexc(0xB09870)))
    mat("leather", color=lin(hexc(0x6B3F2E)))
    mat("ribbon", color=lin(hexc(0x8A55A8)))
    mat("violet", color=kit.sample(house, 330, 1012, 4))
    mat("char", color=lin(hexc(0x3A2E2A)))
    mat("ash", color=lin(hexc(0x8A847C)))
    mat("ember", color=lin(hexc(0xC0623A)))
    mat("leaf", color=lin(hexc(0x6E9F5E)))
    mat("leaf-deep", color=lin(hexc(0x4E7A47)))
    mat("twine", color=lin(hexc(0xB59C6A)))
    mat("stem", color=lin(hexc(0x8C8A55)))
    mat("lavender", color=lin(hexc(0x8C7AA8)))
    # herbs
    mat("sage", tex("sage", crop(room, SAGE)))
    mat("tansy", tex("tansy", crop(room, TANSY)))
    mat("rosemary", tex("rosemary", crop(room, ROSEMARY)))
    # decals
    mat("crate", kit.decal(room, P + "crate", *CRATE_SIDE))
    pane = crop(room, LANTERN_PANE)
    pane[..., :3] = np.clip(pane[..., :3] * 1.15 + np.array(hexc(0xFFE6A8)) * 0.12, 0, 1)
    mat("lantern-pane", tex("lantern-pane", pane, tile=False))
    mat("bay-glass", tex("bay-glass", _bay_glass(house), tile=False))
    mat("map", tex("map", _map_paper(room), tile=False))
    mat("page-l", tex("page-l", _page(room, 0), tile=False))
    mat("page-r", tex("page-r", _page(room, 1), tile=False))
    mat("rug", tex("rug", _rug(room), tile=False))
    return room, house, M


def _bay_glass(house):
    """The house's bowed lights seen from inside in daylight: the dark panes brightened to a warm glow behind the
    jars and shelves, so the jars read backlit; the violet glazing bars and the ink stay."""
    a = crop(house, BAY_GLASS)
    rgb = a[..., :3]
    lum = rgb @ np.array([0.3, 0.59, 0.11])
    sat = rgb.max(axis=2) - rgb.min(axis=2)
    grey = np.clip((0.14 - sat) / 0.08, 0, 1)
    pane = grey * np.clip((lum - 0.16) / 0.06, 0, 1) * np.clip((0.46 - lum) / 0.06, 0, 1)
    jar = grey * np.clip((lum - 0.44) / 0.06, 0, 1)
    light = np.array(hexc(0xFFF6DE))
    jar_col = np.array(hexc(0x7FA79A)) * (0.55 + 0.6 * (lum[..., None] - 0.5))
    out = rgb * (1 - pane[..., None]) + light * pane[..., None]
    out = out * (1 - jar[..., None]) + jar_col * jar[..., None]
    a[..., :3] = np.clip(out, 0, 1)
    return a


def _map_paper(room):
    """The blank frame's paper, turned to landscape and enlarged, with the map's ink border, compass rose and the
    routes between the game's nodes painted where ApothecaryScene.MapBoard draws them."""
    paper = np.ascontiguousarray(np.rot90(crop(room, MAP_PAPER)))
    ppm = 400  # pixels per metre
    W, H = int(MAP_W * ppm), int(MAP_H * ppm)
    img = resize(paper, W, H)
    img = calm(img, 0.8)

    def px(x, y):
        return ((x + MAP_W / 2) * ppm, (MAP_H / 2 - y) * ppm)

    ink = hexc(0x6A5A4A)
    hw, hh = MAP_W / 2 - 0.08, MAP_H / 2 - 0.08
    for a, b in (((-hw, hh), (hw, hh)), ((-hw, -hh), (hw, -hh)), ((hw, -hh), (hw, hh)), ((-hw, -hh), (-hw, hh))):
        paint_line(img, px(*a), px(*b), 0.015 * ppm, ink, 0.9)
    nodes = map_nodes(30)
    for i in range(1, len(nodes)):
        paint_line(img, px(*nodes[(i - 1) // 2]), px(*nodes[i]), 0.012 * ppm, ink, 0.85)
    rx, ry = MAP_W / 2 - 0.3, -MAP_H / 2 + 0.3
    violet = hexc(0x8A55A8)
    paint_line(img, px(rx, ry - 0.15), px(rx, ry + 0.15), 0.02 * ppm, violet)
    paint_line(img, px(rx - 0.15, ry), px(rx + 0.15, ry), 0.02 * ppm, violet)
    paint_disc(img, px(rx, ry), 0.06 * ppm, hexc(0xD9A441))
    paint_disc(img, px(rx, ry), 0.025 * ppm, ink, 0.6)
    img[..., 3] = 1.0
    return img


def _page(room, side):
    """A page of the formulary: the sheet's paper with a few hand-ruled lines of writing and, on the right-hand
    page, a little drawing of a vessel (half recipes, half riddles)."""
    paper = crop(room, PAPER)
    W, H = 160, 220
    img = calm(resize(paper, W, H), 0.7)
    ink = hexc(0x5A4A3E)
    rng = np.random.default_rng(7 + side)
    rows = range(24, H - 18, 13)
    for j, y in enumerate(rows):
        if side == 1 and 70 < y < 150:
            continue
        x = 16 + (8 if j % 5 == 0 else 0)
        end = W - 16 - (rng.integers(0, 50) if j % 4 == 3 else 0)
        while x < end:
            w = rng.integers(10, 30)
            paint_line(img, (x, y + rng.uniform(-0.6, 0.6)), (min(x + w, end), y + rng.uniform(-0.6, 0.6)), 1.8, ink, 0.75)
            x += w + rng.integers(4, 8)
    if side == 1:  # a sketch: a round flask over a flame
        cx, cy = W / 2, 108
        for t in range(28):
            a0, a1 = 2 * math.pi * t / 28, 2 * math.pi * (t + 1) / 28
            paint_line(img, (cx + 22 * math.cos(a0), cy + 22 * math.sin(a0)), (cx + 22 * math.cos(a1), cy + 22 * math.sin(a1)), 1.8, ink, 0.7)
        paint_line(img, (cx - 6, cy - 21), (cx - 6, cy - 40), 1.8, ink, 0.7)
        paint_line(img, (cx + 6, cy - 21), (cx + 6, cy - 40), 1.8, ink, 0.7)
        paint_line(img, (cx - 12, cy + 34), (cx, cy + 26), 1.6, hexc(0x5A7AB0), 0.7)
        paint_line(img, (cx, cy + 26), (cx + 12, cy + 34), 1.6, hexc(0x5A7AB0), 0.7)
    # the crack along the spine
    gx = W - 3 if side == 0 else 2
    for y in range(0, H, 9):
        paint_line(img, (gx + rng.uniform(-2, 2), y), (gx + rng.uniform(-2, 2), y + 9), 1.4, hexc(0x8A7A6A), 0.6)
    img[..., 3] = 1.0
    return img


def _rug(room):
    """A woven rug in the shop's violet with a cream stripe inside a darker border and a fringe at each end, its
    paint mottled by the sheet's paper patch (no lighting in it)."""
    W, H = 400, 280
    paper = resize(crop(room, PAPER), W, H)
    mott = (paper[..., :3].mean(axis=2) - paper[..., :3].mean())[..., None]
    field = np.array(hexc(0x7C5A78))     # dusty plum: the shop's violet, worn and walked on
    border = np.array(hexc(0x56384F))
    ochre = np.array(hexc(0xC2A26A))
    cream = np.array(hexc(0xD9C8A2))
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    d = np.minimum.reduce([xs - 10, W - 11 - xs, ys, H - 1 - ys])
    col = np.where((d < 24)[..., None], border, field)
    col = np.where(((d >= 9) & (d < 14))[..., None], ochre, col)
    col = np.where(((d >= 34) & (d < 38))[..., None], ochre, col)
    # a row of small lozenges in the border and a large one in the middle of the field
    cx, cy = W / 2, H / 2
    dia = np.abs(xs - cx) / 120 + np.abs(ys - cy) / 70
    col = np.where(((dia > 0.82) & (dia < 0.9) | (dia < 0.16))[..., None], ochre, col)
    col = np.where(((dia >= 0.16) & (dia < 0.3))[..., None], border, col)
    edge_band = (d >= 14) & (d < 24)
    lz = (np.abs((xs % 36) - 18) / 6 + np.abs(np.where(ys < H / 2, ys - 19, ys - (H - 20))) / 4) < 1
    col = np.where((edge_band & lz)[..., None], cream, col)
    # a soft weave across the field
    col = col + (np.sin(ys * 0.9) * 0.02)[..., None] * (d >= 38)[..., None]
    # fringe at the short ends
    fr = (xs < 10) | (xs > W - 11)
    col = np.where(fr[..., None], cream * (0.85 + 0.15 * (ys % 4 < 2))[..., None], col)
    img = np.empty((H, W, 4), np.float32)
    img[..., :3] = np.clip(col + mott * 1.2, 0, 1)
    img[..., 3] = 1.0
    return img


def blended_floor_decal(kit, floor_arr, tile, name, x0, x1, z0, z1, paint, ppm=160):
    """A decal for the floor over game x0..x1, z0..z1: the floorboards exactly as the floor maps them underneath
    (box_uv: u = x / tile, v = blender y / tile), with `paint(under, X, Z)` laid over them; it fades to nothing at
    its border, so no rectangle shows."""
    W, H = int((x1 - x0) * ppm), int((z1 - z0) * ppm)
    blk = np.concatenate([floor_arr, floor_arr[:, ::-1]], axis=1)  # the 2x2 mirrored block kit.swatch builds
    blk = np.concatenate([blk, blk[::-1]], axis=0)
    th, tw = blk.shape[:2]
    X = x0 + (np.arange(W) + 0.5) / W * (x1 - x0)
    BY = -z0 - (np.arange(H) + 0.5) / H * (z1 - z0)   # row 0 is the decal's top (v = 1): blender y = -z0
    U = np.mod(X / tile[0], 1.0)
    V = np.mod(BY / tile[1], 1.0)
    rows = np.clip(((1.0 - V) * th).astype(int), 0, th - 1)
    cols = np.clip((U * tw).astype(int), 0, tw - 1)
    under = blk[rows[:, None], cols[None, :]].copy()
    Z = -BY
    out = paint(under, X[None, :].repeat(H, 0), Z[:, None].repeat(W, 1))
    out[..., 3] = 1.0
    return kit.decal(out, name, 0, 0, W, H)


# ───────────────────────── the parts ─────────────────────────

def build_shell(kit, root, M, room):
    k = Kit(kit, root)
    plaster_tile = kit.sheet_tile(*PLASTER, PX_PLASTER)
    floor_tile = kit.sheet_tile(*FLOOR, PX_FLOOR)
    brick_tile = kit.sheet_tile(*BRICK, PX_BRICK)
    hearth_tile = kit.sheet_tile(*HEARTH, PX_BRICK)
    mantel_tile = (2 * (MANTEL[2] - MANTEL[0]) * PX_BRICK, 2 * (MANTEL[3] - MANTEL[1]) * PX_BRICK)
    timber_tile = (mantel_tile[1], mantel_tile[0])

    # ── floor: boards running along z ──
    k.box((-5.6, -0.2, -5.6), (ROOM_END, 0.0, ROOM_END), M["floor"], floor_tile, "floor")

    # ── walls: plaster, the left one round the bay's opening ──
    k.box((WALL_OUT, -0.1, WALL_OUT), (ROOM_END, WALL_TOP, WALL_IN), M["plaster"], plaster_tile, "back-wall")
    bz0, bz1 = BAY_Z - 1.0, BAY_Z + 1.0
    # the left wall: one closed slab with the bay's opening through it (separate boxes would ink their seams)
    zs = [WALL_IN, bz0, bz1, ROOM_END]
    ys = [-0.1, BAY_SILL - 0.1, BAY_HEAD + 0.12, WALL_TOP]
    v = [(x, ys[j], zs[i]) for x in (WALL_IN, WALL_OUT) for i in range(4) for j in range(4)]

    def vi(side, i, j):
        return side * 16 + i * 4 + j

    f = []
    for side in (0, 1):
        for i in range(3):
            for j in range(3):
                if (i, j) != (1, 1):
                    f.append((vi(side, i, j), vi(side, i + 1, j), vi(side, i + 1, j + 1), vi(side, i, j + 1)))
    rim = [(i, 0) for i in range(4)] + [(3, j) for j in range(1, 4)] + [(i, 3) for i in (2, 1, 0)] + [(0, j) for j in (2, 1)]
    hole = [(1, 1), (2, 1), (2, 2), (1, 2)]
    for loop in (rim, hole):
        for a, b in zip(loop, loop[1:] + loop[:1]):
            f.append((vi(0, *a), vi(0, *b), vi(1, *b), vi(1, *a)))
    k.mesh(v, f, M["plaster"], plaster_tile, True, "left-wall")

    # ── timber: corner posts, a post to frame each open end, skirting ──
    t = 0.24
    k.box((WALL_IN, 0, WALL_IN), (WALL_IN + t, WALL_TOP, WALL_IN + t), M["timber-up"], timber_tile, "post")
    k.box((ROOM_END - t, 0, WALL_IN), (ROOM_END, WALL_TOP, WALL_IN + 0.2), M["timber-up"], timber_tile, "post")
    k.box((WALL_IN, 0, 1.55), (WALL_IN + 0.2, WALL_TOP, 1.55 + t), M["timber-up"], timber_tile, "post")
    k.box((WALL_IN, 0, ROOM_END - t), (WALL_IN + 0.2, WALL_TOP, ROOM_END), M["timber-up"], timber_tile, "post")
    k.box((WALL_IN, 0, WALL_IN), (ROOM_END, 0.2, WALL_IN + 0.04), M["mantel"], mantel_tile, "skirting")
    k.box((WALL_IN, 0, WALL_IN), (WALL_IN + 0.04, 0.2, ROOM_END), M["mantel"], mantel_tile, "skirting")

    # ── the chimney breast with its open hearth ──
    breast = Kit(kit, root)
    zc = (CH_Z0 + CH_Z1) / 2
    oz0, oz1 = CH_Z0 + JAMB, CH_Z1 - JAMB          # the opening
    breast.box((WALL_IN, 0, CH_Z0 - 0.12), (CH_FRONT + 0.42, CH_BASE, CH_Z1 + 0.12), M["hearth"], hearth_tile, "hearth")
    for z0, z1 in ((CH_Z0, oz0), (oz1, CH_Z1)):
        breast.box((WALL_IN, CH_BASE, z0), (CH_FRONT, CH_MANTEL0, z1), M["brick"], brick_tile, "jamb")
    # the firebox: sooty linings and back, the painted fire basket on the back
    lin = 0.06
    breast.box((WALL_IN, CH_BASE, oz0), (CH_FRONT - 0.02, CH_MANTEL0, oz0 + lin), M["soot"], brick_tile, "lining")
    breast.box((WALL_IN, CH_BASE, oz1 - lin), (CH_FRONT - 0.02, CH_MANTEL0, oz1), M["soot"], brick_tile, "lining")
    back_x = WALL_IN + 0.12
    breast.box((WALL_IN, CH_BASE, oz0), (back_x, CH_MANTEL0, oz1), M["soot"], brick_tile, "fireback")
    breast.plaque(M["firebox"], (back_x + 0.005, (CH_BASE + CH_MANTEL0) / 2, zc), (oz1 - oz0 - 2 * lin, CH_MANTEL0 - CH_BASE), "+x", "fireback-decal")
    # a heap of ash and two charred logs in the opening
    breast.lathe((-5.02, CH_BASE, zc), [(0, 0), (0.3, 0.0), (0.22, 0.035), (0.1, 0.07), (0, 0.075)], M["ash"], 9, smooth=True,
                 scale=(0.7, 1.0), name="ash-heap")
    breast.rod((-5.12, CH_BASE + 0.08, zc - 0.32), (-4.98, CH_BASE + 0.1, zc + 0.3), 0.055, M["char"], 6, "log")
    breast.rod((-4.95, CH_BASE + 0.07, zc - 0.25), (-5.1, CH_BASE + 0.16, zc + 0.24), 0.05, M["char"], 6, "log")
    breast.lathe((-5.02, CH_BASE + 0.05, zc + 0.05), [(0, 0), (0.09, 0.0), (0.06, 0.05), (0, 0.06)], M["ember"], 6, smooth=True, name="embers")
    # mantel, hood and stack
    breast.box((WALL_IN, CH_MANTEL0, CH_Z0 - 0.1), (CH_FRONT + 0.1, CH_MANTEL1, CH_Z1 + 0.1), M["mantel"], mantel_tile, "mantel")
    hz0, hz1, hf = CH_Z0 + 0.05, CH_Z1 - 0.05, CH_FRONT - 0.05
    hood = [(WALL_IN, CH_MANTEL1, hz0), (hf, CH_MANTEL1, hz0), (hf, CH_MANTEL1, hz1), (WALL_IN, CH_MANTEL1, hz1),
            (WALL_IN, CH_HOOD_TOP, STACK_Z0), (STACK_FRONT, CH_HOOD_TOP, STACK_Z0), (STACK_FRONT, CH_HOOD_TOP, STACK_Z1),
            (WALL_IN, CH_HOOD_TOP, STACK_Z1)]
    breast.mesh(hood, [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)], M["brick"], brick_tile, True, "hood")
    breast.box((WALL_IN, CH_HOOD_TOP, STACK_Z0), (STACK_FRONT, WALL_TOP, STACK_Z1), M["brick"], brick_tile, "stack")
    # a few things on the mantel: two stoneware jars and a candle stub
    breast.lathe((CH_FRONT + 0.03, CH_MANTEL1, CH_Z0 + 0.08), [(0, 0), (0.06, 0), (0.07, 0.08), (0.04, 0.15), (0.045, 0.17), (0, 0.17)],
                 M["stoneware-dark"], 8, name="mantel-jar")
    breast.lathe((CH_FRONT + 0.03, CH_MANTEL1, CH_Z1 - 0.12), [(0, 0), (0.05, 0), (0.055, 0.12), (0.03, 0.14), (0, 0.14)],
                 M["stoneware"], 8, name="mantel-jar")
    breast.lathe((CH_FRONT + 0.03, CH_MANTEL1, CH_Z1 - 0.3), [(0, 0), (0.035, 0), (0.035, 0.1), (0, 0.1)], M["candle"], 6, name="mantel-candle")
    k.objs += breast.objs

    # ── the bowed window: an opening in the left wall, the bay beyond it, a seat of jars ──
    def bay_pt(s, d):
        return (WALL_OUT - d, BAY_Z + s)    # game (x, z)

    glass_pts = [bay_pt(s, d) for s, d in BAY_PLAN]
    seat_pts = [bay_pt(s * 1.0, d + 0.06) for s, d in BAY_PLAN]
    outline = [(WALL_IN + 0.06, bz0 - 0.06), (WALL_IN + 0.06, bz1 + 0.06), (WALL_IN, bz1 + 0.06), (WALL_IN, bz1)] + \
              list(reversed(seat_pts)) + [(WALL_IN, bz0), (WALL_IN, bz0 - 0.06)]
    k.prism_y(outline, BAY_SILL - 0.1, BAY_SILL, M["wood"], kit.sheet_tile(*BENCH_WOOD, 0.012), "bay-seat")
    head = [(WALL_IN, bz1)] + list(reversed(seat_pts)) + [(WALL_IN, bz0)]
    k.prism_y(head, BAY_HEAD, BAY_HEAD + 0.12, M["plaster"], plaster_tile, "bay-head")
    k.box((WALL_IN, BAY_HEAD, bz0 - 0.16), (WALL_IN + 0.07, BAY_HEAD + 0.24, bz1 + 0.16), M["mantel"], mantel_tile, "bay-lintel")
    # the lights: one quad per facet, facing in, the painted bay mapped across them as seen from inside
    verts, faces = [], []
    for i in range(len(glass_pts) - 1):
        (ax, az), (bx, bz) = glass_pts[i], glass_pts[i + 1]
        n0 = len(verts)
        verts += [(ax, BAY_SILL, az), (bx, BAY_SILL, bz), (bx, BAY_HEAD, bz), (ax, BAY_HEAD, az)]
        faces.append((n0, n0 + 1, n0 + 2, n0 + 3))
    lights = k.mesh(verts, faces, M["bay-glass"], None, False, "bay-lights")
    for poly in lights.data.polygons:  # face into the room
        c = poly.center
        if poly.normal.dot(Vector((WALL_IN, -BAY_Z, c.z)) - c) < 0:
            poly.flip()
    lights.data.update()
    y_lo, y_hi = -bz1, -bz0
    uv_by(lights, lambda p, n: ((p.y - y_lo) / (y_hi - y_lo), (p.z - BAY_SILL) / (BAY_HEAD - BAY_SILL)))
    # daylight through the glass: the panes face into the shadowed left wall, so their normals are turned up and
    # toward the room's light (game (0, 0.9, 0.44)), and the cel shader paints them in full light behind the jars
    for poly in lights.data.polygons:
        poly.use_smooth = True
    lights.data.normals_split_custom_set([g2b((0.0, 0.9, 0.44))] * len(lights.data.loops))
    for i, (x, z) in enumerate(glass_pts):
        k.lathe((x, BAY_SILL, z), [(0, 0), (0.05, 0), (0.05, BAY_HEAD - BAY_SILL), (0, BAY_HEAD - BAY_SILL)], M["violet"], 6,
                smooth=False, name="bay-post")
    # jars and bottles on the seat, against the light
    jars = [(-0.62, 0.3, "glass", "bottle"), (-0.35, 0.42, "glass-amber", "jar"), (-0.05, 0.43, "glass", "flask"),
            (0.22, 0.44, "glass-violet", "bottle"), (0.5, 0.33, "stoneware", "crock"), (0.72, 0.22, "glass", "jar")]
    for s, d, m, shape in jars:
        x, z = bay_pt(s, d - 0.05)
        _jar(k, (x, BAY_SILL, z), shape, M[m], M)

    # ── the rug and the ash on the boards ──
    rug = k.box((-2.0, 0.0, -0.9), (3.0, 0.02, 2.6), M["rug"], None, "rug")
    uv_by(rug, lambda p, n: ((p.x + 2.0) / 5.0, (p.y + 2.6) / 3.5) if n.z > 0.5 else (0.004, 0.5))

    floor_px = crop(room, FLOOR)
    ash_rng = np.random.default_rng(3)
    blobs = [(-4.15, -3.7, 0.55, 0.9), (-3.75, -3.4, 0.35, 0.45), (-3.85, -4.2, 0.3, 0.3), (-3.5, -3.05, 0.22, 0.25)]
    ash_col = np.array(hexc(0x9A948C))

    def ash_paint(under, X, Z):
        m = np.zeros(X.shape, np.float32)
        for cx, cz, rx, rz in blobs:
            d = np.hypot((X - cx) / rx, (Z - cz) / rz)
            m = np.maximum(m, np.clip(1.25 - d, 0, 1))
        noise = blur(ash_rng.random(X.shape).astype(np.float32), 3)
        noise = (noise - noise.mean()) / max(noise.std(), 1e-6)   # unit spread, so the patch is ragged
        m = np.clip(m * 1.5 + noise * 0.25 - 0.15, 0, 0.92)
        specks = (ash_rng.random(X.shape) > 0.98) & (m > 0.15)
        out = under.copy()
        out[..., :3] = under[..., :3] * (1 - m[..., None]) + ash_col * m[..., None]
        out[specks, :3] = np.array(hexc(0x4A403A))
        return out

    ash_mat = kit.material(P + "floor-ash", blended_floor_decal(kit, floor_px, floor_tile, P + "floor-ash",
                                                               -4.75, -3.0, -4.95, -2.45, ash_paint))
    k.floor_quad(ash_mat, -4.75, -3.0, -4.95, -2.45, 0.004, "floor-ash")
    return k.join("shell")


def _jar(k, base, shape, glass, M):
    """A small decor jar or bottle standing at `base` (game)."""
    if shape == "bottle":
        prof = [(0, 0), (0.09, 0), (0.09, 0.2), (0.04, 0.27), (0.035, 0.36), (0, 0.36)]
        k.lathe(base, prof, glass, 8, name="bottle")
        k.lathe((base[0], base[1] + 0.36, base[2]), [(0, 0), (0.04, 0), (0.04, 0.05), (0, 0.05)], M["cork"], 6, name="cork")
    elif shape == "flask":
        prof = [(0, 0), (0.08, 0.01), (0.13, 0.1), (0.1, 0.2), (0.035, 0.25), (0.03, 0.34), (0, 0.34)]
        k.lathe(base, prof, glass, 8, name="flask")
        k.lathe((base[0], base[1] + 0.34, base[2]), [(0, 0), (0.035, 0), (0.035, 0.04), (0, 0.04)], M["cork"], 6, name="cork")
    elif shape == "crock":
        prof = [(0, 0), (0.1, 0), (0.12, 0.12), (0.09, 0.2), (0.095, 0.22), (0, 0.22)]
        k.lathe(base, prof, glass, 8, name="crock")
    else:
        prof = [(0, 0), (0.1, 0), (0.1, 0.2), (0.07, 0.24), (0.07, 0.26), (0, 0.26)]
        k.lathe(base, prof, glass, 8, name="jar")
        k.lathe((base[0], base[1] + 0.26, base[2]), [(0, 0), (0.06, 0), (0.06, 0.04), (0, 0.04)], M["cork"], 6, name="cork")


def build_furniture(kit, root, M):
    k = Kit(kit, root)
    wood_tile = kit.sheet_tile(*BENCH_WOOD, 0.012)
    up_tile = kit.sheet_tile(*LEG_WOOD, 0.008)
    planks_u = 2 * (PLANKS[2] - PLANKS[0]) * PX_PLANKS

    # ── the shelving wall ──
    cx, w = (SHELF_L + SHELF_R) / 2, SHELF_R - SHELF_L
    # the planked back, one panel per gap so each starts under its board
    edges = [0.0] + sorted(SHELF_ROWS) + [4.76]
    for i in range(len(edges) - 1):
        y0 = edges[i]
        y1 = edges[i + 1] - (0.07 if i + 1 < len(edges) - 1 else 0.0)
        if y1 - y0 < 0.05:
            continue
        panel = k.box((cx - w / 2 - 0.15, y0, -5.38), (cx + w / 2 + 0.15, y1, -5.28), M["planks"], None, "back-panel")
        uv_by(panel, lambda p, n, y0=y0, y1=y1: (p.x / planks_u, (p.z - y0) / (y1 - y0)))
    for x in (SHELF_L - 0.08, SHELF_R + 0.08):
        k.box((x - 0.08, 0, SHELF_Z - 0.41), (x + 0.08, 4.78, SHELF_Z + 0.41), M["wood-up"], up_tile, "side")
    # the bank of little drawers
    k.box((SHELF_L, 0.0, -5.35), (SHELF_R, 1.2, SHELF_Z + 0.4), M["wood"], wood_tile, "drawer-bank")
    k.box((SHELF_L - 0.02, 0.0, SHELF_Z + 0.38), (SHELF_R + 0.02, 0.07, SHELF_Z + 0.45), M["mantel"], 1.0, "plinth")
    cols = 9
    for r in range(2):
        for c in range(cols):
            dx, dy = SHELF_L + (c + 0.5) * w / cols, 0.33 + r * 0.52
            dw, dh = w / cols - 0.07, 0.44
            k.box((dx - dw / 2, dy - dh / 2, SHELF_Z + 0.4), (dx + dw / 2, dy + dh / 2, SHELF_Z + 0.43), M["wood"], wood_tile, "drawer")
            k.plaque(M["drawer"], (dx, dy, SHELF_Z + 0.433), (dw, dh), "+z", "drawer-front")
            k.box((dx - 0.09, dy + 0.1, SHELF_Z + 0.433), (dx + 0.09, dy + 0.17, SHELF_Z + 0.45), M["parchment"], 1.0, "label")
    # shelf boards (exactly where the jars stand) with their painted front edges and brass rails
    for y in SHELF_ROWS:
        b = k.box((SHELF_L, y - 0.07, SHELF_Z - 0.41), (SHELF_R, y, SHELF_Z + 0.41), M["wood"], wood_tile, "shelf")
        edge = k.plaque(M["shelf-edge"], (cx, y - 0.035, SHELF_Z + 0.412), (w, 0.07), "+z", "shelf-edge")
        uv_by(edge, lambda p, n, y=y: (p.x / planks_u, (p.z - (y - 0.07)) / 0.07))
        k.box((SHELF_L, y + 0.1175, SHELF_Z + 0.3775), (SHELF_R, y + 0.1425, SHELF_Z + 0.4025), M["brass"], 1.0, "rail")
        for x in (SHELF_L + 0.02, SHELF_R - 0.02):
            k.box((x - 0.015, y, SHELF_Z + 0.375), (x + 0.015, y + 0.145, SHELF_Z + 0.405), M["brass"], 1.0, "rail-post")
    # the crown, painted in the shop's violet, with brass studs
    k.box((cx - w / 2 - 0.25, 4.76, SHELF_Z - 0.455), (cx + w / 2 + 0.25, 4.96, SHELF_Z + 0.495), M["crown"], wood_tile, "crown")
    k.box((cx - w / 2 - 0.33, 4.95, SHELF_Z - 0.47), (cx + w / 2 + 0.33, 5.03, SHELF_Z + 0.55), M["mantel"], 1.0, "cornice")
    for i in range(9):
        x = SHELF_L + 0.2 + i * (w - 0.4) / 8
        k.box((x - 0.03, 4.82, SHELF_Z + 0.495), (x + 0.03, 4.9, SHELF_Z + 0.515), M["brass"], 1.0, "stud")

    # ── the workbench ──
    bx, bw = (BENCH_L + BENCH_R) / 2, BENCH_R - BENCH_L
    top = k.box((BENCH_L, BENCH_TOP - 0.14, BENCH_Z - 0.7), (BENCH_R, BENCH_TOP, BENCH_Z + 0.7), M["wood"], wood_tile, "bench-top")
    for z in (BENCH_Z + 0.7,):
        e = k.plaque(M["wood-edge"], (bx, BENCH_TOP - 0.07, z + 0.002), (bw, 0.14), "+z", "bench-edge")
        uv_by(e, lambda p, n: (p.x / (2 * (BENCH_EDGE[2] - BENCH_EDGE[0]) * 0.012), (p.z - (BENCH_TOP - 0.14)) / 0.14))
    k.box((BENCH_L + 0.1, BENCH_TOP - 0.36, BENCH_Z - 0.64), (BENCH_R - 0.1, BENCH_TOP - 0.14, BENCH_Z + 0.64), M["wood"], wood_tile, "apron")
    for x in (BENCH_L + 0.14, BENCH_R - 0.14):
        for z in (BENCH_Z - 0.55, BENCH_Z + 0.55):
            k.box((x - 0.09, 0, z - 0.09), (x + 0.09, BENCH_TOP - 0.14, z + 0.09), M["wood-up"], up_tile, "leg")
    k.box((BENCH_L + 0.15, 0.21, BENCH_Z - 0.58), (BENCH_R - 0.15, 0.27, BENCH_Z + 0.58), M["wood"], wood_tile, "low-shelf")
    for x in (BENCH_L + 0.14, BENCH_R - 0.14):  # the end stretchers, as on the sheet's bench
        k.box((x - 0.06, 0.3, BENCH_Z - 0.55), (x + 0.06, 0.4, BENCH_Z + 0.55), M["wood"], wood_tile, "stretcher")
    # under the bench: two sacks, a crate, a stoneware jug and a clay pot
    k.lathe((BENCH_L + 0.7, 0.27, BENCH_Z - 0.1), [(0, 0), (0.3, 0.0), (0.36, 0.13), (0.3, 0.27), (0.14, 0.33), (0.06, 0.37), (0, 0.38)],
            M["sack"], 9, phase=0.3, scale=(1.0, 0.85), name="sack")
    k.lathe((BENCH_L + 1.42, 0.27, BENCH_Z + 0.05), [(0, 0), (0.25, 0.0), (0.3, 0.12), (0.25, 0.25), (0.1, 0.32), (0.05, 0.35), (0, 0.36)],
            M["sack-dark"], 9, phase=0.7, scale=(1.0, 0.9), name="sack")
    k.box((BENCH_R - 1.5, 0.27, BENCH_Z - 0.3), (BENCH_R - 0.9, 0.62, BENCH_Z + 0.3), M["wood-up"], up_tile, "under-crate")
    k.lathe((BENCH_R - 0.5, 0.27, BENCH_Z - 0.05), [(0, 0), (0.16, 0), (0.19, 0.13), (0.14, 0.27), (0.06, 0.32), (0.065, 0.37), (0, 0.37)],
            M["stoneware"], 10, name="jug")
    k.lathe((bx + 0.6, 0.27, BENCH_Z + 0.1), [(0, 0), (0.13, 0), (0.16, 0.2), (0.15, 0.3), (0.165, 0.32), (0, 0.32)], M["clay"], 9, name="pot")
    # the brass tray (its floor at the built-in tray's top, where the game sets objects)
    tx, ty, tz = TRAY
    tray = k.lathe(TRAY, [(0, 0), (0.29, 0.0), (0.31, 0.04), (0.325, 0.06), (0.305, 0.06), (0.29, 0.04), (0, 0.04)], M["brass-tray"], 16,
                   sharp=(1, 2, 3, 4, 5), name="tray")
    kit.box_uv(tray, (0.6, 0.3))   # planar: a lathe's own mapping fans the floor into slices
    for s in (-1, 1):
        k.torus((tx + s * 0.35, ty + 0.045, tz), 0.045, 0.012, "x", M["brass"], 8, 4, "tray-handle")

    # ── the map board's frame on the back wall ──
    mx, my, mz = MAP_C
    fw, fh, t = MAP_W / 2 + 0.12, MAP_H / 2 + 0.12, 0.12
    up_frame = kit.sheet_tile(*FRAME_WOOD, 0.009)
    k.box((mx - fw, my - fh, -5.395), (mx + fw, my + fh, -5.355), M["frame-up"], up_frame, "map-back")
    for s in (-1, 1):
        k.box((mx + s * fw - (t if s > 0 else 0), my - fh, -5.395), (mx + s * fw + (0 if s > 0 else t), my + fh, -5.3), M["frame-up"], up_frame, "map-side")
        k.box((mx - fw + t, my + s * fh - (t if s > 0 else 0), -5.395), (mx + fw - t, my + s * fh + (0 if s > 0 else t), -5.3),
              M["frame-along"], (up_frame[1], up_frame[0]), "map-rail")
    k.torus((mx, my + fh + 0.05, -5.37), 0.05, 0.012, "z", M["iron"], 8, 4, "map-ring")

    # ── the lectern: crossed feet, a square post, the tilted top (the book sits in its frame) ──
    lx, ly, lz = LECTERN
    k.box((lx - 0.36, 0, lz - 0.08), (lx + 0.36, 0.1, lz + 0.08), M["wood"], wood_tile, "lectern-foot")
    k.box((lx - 0.08, 0, lz - 0.36), (lx + 0.08, 0.1, lz + 0.36), M["wood"], wood_tile, "lectern-foot")
    k.box((lx - 0.075, 0.1, lz - 0.075), (lx + 0.075, 1.04, lz + 0.075), M["wood-up"], up_tile, "lectern-post")
    oriented_box(k, (-0.4, -0.03, -0.29), (0.4, 0.03, 0.29), M["wood"], wood_tile, LECTERN_RX, LECTERN_RY, LECTERN_TOP, "lectern-top")
    oriented_box(k, (-0.4, 0.025, 0.25), (0.4, 0.075, 0.29), M["mantel"], 1.0, LECTERN_RX, LECTERN_RY, LECTERN_TOP, "lectern-lip")

    # ── the crate of bottles by the window ──
    ox, oy, oz = CRATE
    cw, cd, ch, tk = 0.45, 0.325, 0.5, 0.04
    k.box((ox - cw, 0, oz - cd), (ox + cw, 0.06, oz + cd), M["wood"], wood_tile, "crate-floor")
    for s in (-1, 1):
        k.box((ox - cw, 0, oz + s * cd - (tk if s > 0 else 0)), (ox + cw, ch, oz + s * cd + (0 if s > 0 else tk)), M["wood"], wood_tile, "crate-side")
        k.box((ox + s * cw - (tk if s > 0 else 0), 0, oz - cd), (ox + s * cw + (0 if s > 0 else tk), ch, oz + cd), M["wood"], wood_tile, "crate-end")
    k.plaque(M["crate"], (ox, ch / 2, oz + cd + 0.003), (2 * cw, ch), "+z", "crate-front")
    k.plaque(M["crate"], (ox + cw + 0.003, ch / 2, oz), (2 * cd, ch), "+x", "crate-end-face")
    for i in range(6):
        px_, pz_ = ox - 0.3 + (i % 3) * 0.3, oz - 0.14 + (i // 3) * 0.28
        prof = [(0, 0), (0.1, 0), (0.1, 0.42), (0.05, 0.54), (0.04, 0.7), (0, 0.7)] if i % 2 == 0 else \
               [(0, 0), (0.11, 0), (0.115, 0.36), (0.045, 0.5), (0.04, 0.64), (0, 0.64)]
        k.lathe((px_, 0.06, pz_), prof, M["glass"], 8, name="crate-bottle")
        top = prof[-1][1]
        k.lathe((px_, 0.06 + top, pz_), [(0, 0), (0.045, 0), (0.045, 0.06), (0, 0.06)], M["cork"], 6, name="crate-cork")

    # ── a stool, a potted herb by the window, the sconce by the map ──
    sx, sz = -3.7, 0.4
    for i, a in enumerate((0.3, 2.4, 4.5)):
        fx, fz = sx + math.cos(a) * 0.26, sz + math.sin(a) * 0.26
        k.rod((fx, 0, fz), (sx + math.cos(a) * 0.16, 0.63, sz + math.sin(a) * 0.16), 0.04, M["wood-up"], 5, "stool-leg")
    k.lathe((sx, 0.62, sz), [(0, 0), (0.34, 0), (0.36, 0.05), (0.33, 0.09), (0, 0.09)], M["wood"], 10, tile=(1.0, 0.3), name="stool-seat")
    px_, pz_ = -4.78, 0.55
    k.lathe((px_, 0, pz_), [(0, 0), (0.26, 0), (0.33, 0.44), (0.36, 0.5), (0.3, 0.5), (0, 0.48)], M["clay"], 10, sharp=(2, 3, 4), name="plant-pot")
    k.lathe((px_, 0.42, pz_), [(0, 0), (0.36, 0.05), (0.44, 0.25), (0.36, 0.5), (0.16, 0.66), (0, 0.7)], M["rosemary"], 9,
            tile=(0.5, 0.5), phase=0.2, name="plant")
    sx_, sy_, sz_ = SCONCE
    k.box((sx_ - 0.07, sy_ - 0.3, -5.4), (sx_ + 0.07, sy_ - 0.05, -5.37), M["brass"], 1.0, "sconce-plate")
    k.box((sx_ - 0.07, sy_ - 0.185, sz_ - 0.1), (sx_ + 0.07, sy_ - 0.135, sz_ + 0.14), M["brass"], 1.0, "sconce-arm")
    k.lathe((sx_, sy_ - 0.14, sz_ + 0.06), [(0, 0), (0.05, 0), (0.045, 0.16), (0, 0.16)], M["candle"], 6, name="sconce-candle")
    k.box((sx_ - 0.006, sy_ + 0.02, sz_ + 0.054), (sx_ + 0.006, sy_ + 0.05, sz_ + 0.066), M["char"], 1.0, "wick")
    return k.join("furniture")


def build_herbs(kit, root, M):
    k = Kit(kit, root)
    y, z = 5.15, -3.7
    beam_tile = (2 * (MANTEL[2] - MANTEL[0]) * PX_BRICK, 2 * (MANTEL[3] - MANTEL[1]) * PX_BRICK)
    k.box((-5.25, y - 0.09, z - 0.1), (3.45, y + 0.09, z + 0.1), M["mantel"], beam_tile, "beam")
    kinds = ("sage", "tansy", "rosemary")
    for i in range(9):
        x = -4.45 + i * 0.9
        L = 0.5 + (i * 37 % 5) * 0.06
        kind = kinds[i % 3]
        tie = y - 0.09 - 0.2
        k.box((x - 0.008, tie + 0.1, z - 0.008), (x + 0.008, y - 0.09, z + 0.008), M["twine"], 1.0, "string")
        k.lathe((x, tie, z), [(0, 0), (0.03, 0), (0.04, 0.1), (0, 0.1)], M["stem"], 6, star=0.6, smooth=False, name="stems")
        k.lathe((x, tie - 0.015, z), [(0, 0), (0.048, 0), (0.048, 0.035), (0, 0.035)], M["twine"], 7, name="tie")
        # leaves fanning down from the tie, widest low down, ridged so the facets catch the light like leaves
        prof = [(0, -L), (0.05, -0.92 * L), (0.12, -0.74 * L), (0.15, -0.5 * L), (0.12, -0.27 * L), (0.07, -0.1 * L), (0.035, 0.0), (0, 0.0)]
        k.lathe((x, tie - 0.01, z), prof, M[kind], 10, tile=(0.5, 0.55), phase=0.4 * i, scale=(1.0, 0.6), star=0.55, smooth=False,
                name="bundle")
        if kind != "tansy":  # lavender heads hanging below
            for j, (dx, dz, l) in enumerate(((-0.06, 0.02, 0.17), (0.05, -0.02, 0.21), (0.0, 0.04, 0.14))):
                top_y = tie - 0.01 - 0.8 * L
                k.lathe((x + dx, top_y - l, z + dz), [(0, 0), (0.024, 0.35 * l), (0.02, l), (0, l)], M["lavender"], 6, star=0.6,
                        smooth=False, name="lavender")
    return k.join("herbs")


def build_lantern(kit, root, M):
    """The hanging lantern round its own origin (LanternPos); the game swings it about a point 1.65 above."""
    k = Kit(kit, root)
    hexs = 6
    k.lathe((0, 0, 0), [(0, -0.245), (0.175, -0.245), (0.175, -0.2), (0.15, -0.185), (0, -0.185)], M["iron"], hexs, smooth=False, phase=math.pi / 6,
            name="lantern-base")
    pane_tile = (2 * math.pi * 0.13 / hexs, 0.29)
    k.lathe((0, 0, 0), [(0, -0.19), (0.13, -0.19), (0.13, 0.1), (0, 0.1)], M["lantern-pane"], hexs, tile=pane_tile, smooth=False,
            phase=math.pi / 6, v_by_height=True, name="lantern-glass")
    for i in range(hexs):
        a = math.pi / 6 + 2 * math.pi * i / hexs
        cx, cz = 0.14 * math.cos(a), -0.14 * math.sin(a)
        k.box((cx - 0.018, -0.2, cz - 0.018), (cx + 0.018, 0.11, cz + 0.018), M["iron"], 1.0, "lantern-post")
    k.lathe((0, 0, 0), [(0, 0.09), (0.2, 0.09), (0.2, 0.12), (0.06, 0.3), (0.035, 0.31), (0, 0.31)], M["iron"], hexs, smooth=False,
            phase=math.pi / 6, name="lantern-roof")
    k.lathe((0, 0.31, 0), [(0, 0), (0.03, 0.0), (0.035, 0.03), (0, 0.05)], M["iron"], 6, name="finial")
    k.torus((0, 0.42, 0), 0.055, 0.014, "z", M["iron"], 8, 4, "lantern-ring")
    # the chain up to where it hangs from (1.65 above, as the built-in rod)
    yy, i = 0.48, 0
    while yy < 1.66:
        h = 0.11
        if i % 2 == 0:
            k.box((-0.028, yy, -0.008), (0.028, yy + h, 0.008), M["iron"], 1.0, "link")
        else:
            k.box((-0.008, yy, -0.028), (0.008, yy + h, 0.028), M["iron"], 1.0, "link")
        yy += h - 0.015
        i += 1
    return k.join("lantern")


def build_vessel(kit, root, M):
    """The copper pot on its iron trivet, round its own origin (VesselPos on the bench). The bowl is open: the
    game draws the liquid as a 0.42 m disc at 0.46-0.78 m, which the inner wall (0.39-0.41 m) just swallows."""
    k = Kit(kit, root)
    copper_tile = (2 * (COPPER[2] - COPPER[0]) * PX_COPPER, 2 * (COPPER[3] - COPPER[1]) * PX_COPPER)
    prof = [(0, 0.235), (0.22, 0.24), (0.34, 0.28), (0.45, 0.36), (0.515, 0.46), (0.535, 0.56), (0.515, 0.65), (0.475, 0.72),
            (0.465, 0.745), (0.5, 0.765), (0.505, 0.785), (0.48, 0.805), (0.43, 0.805), (0.41, 0.785), (0.41, 0.52),
            (0.385, 0.45), (0.3, 0.395), (0.15, 0.375), (0, 0.37)]
    k.lathe((0, 0, 0), prof, M["copper"], 18, tile=copper_tile, sharp=(8, 11, 12, 13), name="pot")
    for s in (-1, 1):
        k.torus((s * 0.55, 0.66, 0), 0.075, 0.018, "z", M["copper-dark"], 10, 5, "handle")
    # the trivet: a ring holding the pot by its belly, three splayed legs with curled feet
    k.lathe((0, 0, 0), [(0.355, 0.26), (0.43, 0.26), (0.43, 0.3), (0.355, 0.3), (0.355, 0.26)], M["iron"], 14, smooth=False,
            name="trivet-ring")
    for i in range(3):
        a = i * 2 * math.pi / 3 + 0.5
        top = (math.cos(a) * 0.4, 0.27, math.sin(a) * 0.4)
        foot = (math.cos(a) * 0.47, 0.02, math.sin(a) * 0.47)
        k.rod(top, foot, 0.022, M["iron"], 5, "trivet-leg")
        k.rod(foot, (math.cos(a) * 0.53, 0.035, math.sin(a) * 0.53), 0.02, M["iron"], 5, "trivet-foot")
    # the spirit lamp under the pot (the game draws its blue flame)
    k.lathe((0, 0, 0), [(0, 0), (0.1, 0), (0.1, 0.07), (0.07, 0.1), (0, 0.1)], M["glass"], 8, name="spirit-lamp")
    k.lathe((0, 0.1, 0), [(0, 0), (0.035, 0), (0.035, 0.03), (0, 0.03)], M["brass"], 6, name="lamp-collar")
    # a stirring paddle leaning on the rim
    k.rod((0.2, 0.42, -0.18), (0.5, 1.22, -0.18), 0.022, M["wood-up"], 5, "paddle")
    return k.join("vessel")


def build_map_board(kit, root, M):
    """The Discovery Map's paper, in room coordinates, its face where the built-in face is (z = -5.325)."""
    k = Kit(kit, root)
    mx, my, mz = MAP_C
    k.box((mx - MAP_W / 2, my - MAP_H / 2, mz - 0.015), (mx + MAP_W / 2, my + MAP_H / 2, mz + 0.015), M["parchment"], 1.0, "paper")
    k.plaque(M["map"], (mx, my, mz + 0.0155), (MAP_W, MAP_H), "+z", "map-face")
    for sx in (-1, 1):
        px_ = mx + sx * (MAP_W / 2 - 0.1)
        k.box((px_ - 0.03, my + MAP_H / 2 - 0.13, mz + 0.005), (px_ + 0.03, my + MAP_H / 2 - 0.07, mz + 0.055), M["brass"], 1.0, "pin")
    return k.join("map-board")


def build_book(kit, root, M):
    """The formulary, in the lectern top's own frame (the game draws it at LecternTop): its cover lies on the top's
    surface (y = 0.03) and its lower edge rests against the lip (+z)."""
    k = Kit(kit, root)
    base = 0.03
    k.box((-0.34, base, -0.24), (0.34, base + 0.025, 0.24), M["leather"], 1.0, "cover")
    for side, s in ((0, -1), (1, 1)):
        x0, x1 = (-0.315, -0.008) if s < 0 else (0.008, 0.315)
        lo_h, hi_h = base + 0.045, base + 0.065
        inner, outer = (x1, x0) if s < 0 else (x0, x1)
        # each page block droops toward the gutter
        v = []
        for (x, yt) in ((inner, lo_h), (outer, hi_h)):
            for z in (-0.215, 0.215):
                v.append((x, base + 0.02, z))
                v.append((x, yt, z))
        # v: inner-back-bottom 0, inner-back-top 1, inner-front-bottom 2, inner-front-top 3, outer... 4..7
        f = [(0, 2, 6, 4), (1, 5, 7, 3), (0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6)]
        block = k.mesh(v, f, M["parchment"], None, True, "pages")
        top = k.mesh([(inner, lo_h + 0.0015, -0.215), (outer, hi_h + 0.0015, -0.215), (outer, hi_h + 0.0015, 0.215), (inner, lo_h + 0.0015, 0.215)],
                     [(0, 1, 2, 3)], M["page-l" if s < 0 else "page-r"], None, False, "page")
        if top.data.polygons[0].normal.z < 0:
            top.data.flip_normals()
        xa, xb = min(inner, outer), max(inner, outer)
        uv_by(top, lambda p, n, xa=xa, xb=xb: ((p.x - xa) / (xb - xa), (p.y + 0.215) / 0.43))
        uv_by(block, lambda p, n: (0.5, 0.5))
    # the ribbon, hanging over the lower edge
    k.box((0.02, base + 0.04, 0.2), (0.05, base + 0.05, 0.3), M["ribbon"], 1.0, "ribbon")
    return k.join("book")


def build(kit):
    room, house, M = make_materials(kit)
    root = kit.root("apothecary-room")
    build_shell(kit, root, M, room)
    build_furniture(kit, root, M)
    build_herbs(kit, root, M)
    build_lantern(kit, root, M)
    build_vessel(kit, root, M)
    build_map_board(kit, root, M)
    build_book(kit, root, M)
    return root, 7.0
