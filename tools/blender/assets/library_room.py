"""The Library's room kit: the inside of the tall, narrow grey stone house, as the parts LibraryScene draws.
Reference: art/reference/sheets/library-room-sheet.png (props: bookcase, blank plaque on two brass hooks, reading
table with turned legs, returns box with a slot, rolling ladder, globe, biscuit tin, pencil cup, brass wall lamp, faded
rug, arched window, floorboards patch, panelled wall patch) and art/reference/sheets/library-sheet.png (the house's
grey stone, so the room's upper walls match the outside).

Every part sits in the same frame as the MeshBuilder shape it replaces (game point (X, Y, Z) is Blender (X, -Z, Y)):
- shell     room coordinates (drawn at Identity): worn floorboards, a panelled back wall up to a long timber rail
            above the bookcases, grey stone above, a panelled dado and stone on the left wall, a high arched window
            at the built-in window's place (the dust drifts out of it), corner posts and the faded rug.
- case      ONE bookcase around its own origin for 5 shelves (CaseW(5) = 1.80 m), drawn at (CaseX, lift, CaseZ); the
            game stretches it in X for other counts. Plinth, both boards, the brass rail, the crown and the sides are
            exactly where BuildCase puts them (boards' tops at 1.25 and 2.25: the game slides books onto the first).
- plaque    the tag plaque around its hook point (TINTED with the shelf's genre colour): the sheet's notched board,
            repainted light so the tint carries, on two pale brass hooks.
- table     room coordinates: the turned-leg reading table (top at 1.02, where the pile lies), the candle under the
            game's flame glow, an open ledger moved clear of the pile and the odd items, Ada's pencil cup.
- decor     room coordinates: the brass wall lamp at LampPos (its glass is the lamp-glass part), the rolling ladder
            hooked over the crowns between the 4th and 5th cases (clear of both tag plaques), Mab's stool with her
            biscuit tin, and a floor globe by the window.
- returns   room coordinates (drawn lifted): the slotted box at ReturnsPos; the loan card rises out of its slot.
- lamp-glass  the lamp's chimney around its own origin (drawn emissive at LampPos - 0.2 Y).
"""
import math

import numpy as np
from mathutils import Vector

ROOM_SHEET = "library-room-sheet.png"
HOUSE_SHEET = "library-sheet.png"
HOUSE_PX = 0.0106            # the house's metres per sheet pixel (library.py), so the stone matches outside

# ── the game's layout (LibraryScene.cs, InteriorScene.cs) ──
CASE_Z, CASE_D, BOARD_Y, CASE_H = -5.0, 0.62, 1.25, 3.15
CASE_W = (5.05 - -4.55) / 5 - 0.12          # 1.80: the kit's case is made for five shelves
LAMP = (-5.08, 2.35, -4.85)
GLASS = (LAMP[0], LAMP[1] - 0.2, LAMP[2])   # where the game draws lamp-glass
TABLE = (0.4, 0.0, 1.15)
CANDLE = (TABLE[0] + 0.95, 1.02, TABLE[2] - 0.3)
RETURNS = (4.1, 0.0, 2.7)
CAM = (3.73, 5.98, 10.53)                    # the room camera at rest (CamTarget + orbit), for painted round things

# ── the room's own dressing ──
WALL = -5.4                  # the faces of the back wall (z) and the left wall (x)
PANEL_TOP = 3.40             # the back wall's panelling runs up to the long rail over the cases
DADO = 1.75                  # the left wall's panelling
LADDER_X = 3.13              # between the 4th and 5th cases: clear of both tag plaques
WIN_Z, WIN_Y, WIN_W = -1.5, 3.6, 1.75       # the high window, centred where the built-in window was
GLOBE = (-4.5, 0.0, -1.55)                   # under the window
STONE_CALM = 0.55            # how much of the house stone's ink and wash contrast the inside keeps
# the upper walls are timber-framed bays (posts at the gaps between the cases, and either side of the window);
# each bay shows its own part of the stone tile, so the sheet's few brick marks never repeat as a motif
BACK_BAYS = [-5.8, -2.63, -0.71, 1.21, 3.13, 5.6]
LEFT_BAYS = [-5.8, -3.3, 0.35, 3.0, 5.6]
BAY_OFFSETS = [(0.13, 0.05), (0.61, 0.42), (0.37, 0.71), (0.86, 0.18), (0.24, 0.57), (0.72, 0.9), (0.49, 0.3)]
STOOL = (-3.2, 0.0, 2.0)

# ── boxes on the room sheet (real pixels, from the top-left) ──
PLANK_ROWS = [515, 549, 585, 622, 658, 693]  # the top of each floorboard, inside its ink line (30 px kept)
PLANK_X = (497, 712)
WOOD = (500, 586, 710, 616)                  # one clean floorboard: the grain for every piece of furniture
PANEL = (833, 521, 935, 612)                 # mid-stile to mid-stile, mid-rail to mid-rail: tiles seamlessly
CASE = (30, 25, 246, 292)                    # the whole bookcase (its parts are mapped from sub-boxes)
PLAQUE = (290, 84, 442, 157)                 # the notched board
WINDOW = (230, 502, 359, 725)                # the arched window with its stone surround and sill
RUG = (825, 329, 1116, 489)                  # the rug with both fringes
RETURNS_BOX = (903, 90, 1070, 268)
TABLE_BOX = (470, 98, 860, 145)
GLOBE_BALL = (77, 334, 185, 441)
TIN = (276, 397, 388, 462)
CUP = (477, 393, 534, 463)
HOUSE_STONE = (1541, 1046, 1844, 1238)       # library.py's wall swatch


# ───────────────────────── frames and shapes in game coordinates ─────────────────────────

def B(p):
    """A game point (Y up, front +Z) in Blender (Z up, front -Y)."""
    return (p[0], -p[2], p[1])


def G(p):
    """A Blender point or direction in game coordinates."""
    return Vector((p[0], p[2], -p[1]))


def gmesh(kit, name, verts, faces, root):
    return kit.mesh_object(name, [B(v) for v in verts], faces, root, solid=True)


def gbox(kit, name, lo, hi, root):
    """An axis-aligned box from its game-space corners."""
    c = B([(a + b) / 2 for a, b in zip(lo, hi)])
    s = (hi[0] - lo[0], hi[2] - lo[2], hi[1] - lo[1])
    return kit.box(name, c, s, root)


BOX_FACES = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]


def obox(kit, name, centre, size, root, yaw=0.0):
    """A box turned about the game's Y axis by `yaw` radians."""
    cx, cy, cz = centre
    c, s = math.cos(yaw), math.sin(yaw)
    v = []
    for x in (-1, 1):
        for y in (-1, 1):
            for z in (-1, 1):
                lx, ly, lz = x * size[0] / 2, y * size[1] / 2, z * size[2] / 2
                v.append((cx + lx * c + lz * s, cy + ly, cz - lx * s + lz * c))
    return gmesh(kit, name, v, BOX_FACES, root)


def beam(kit, name, a, b, w, d, root):
    """A square-ended beam from game point a to b, w across (horizontal) and d deep."""
    a, b = Vector(a), Vector(b)
    ax = (b - a).normalized()
    side = ax.cross(Vector((0, 1, 0)))
    side = side.normalized() if side.length > 1e-4 else Vector((1, 0, 0))
    other = side.cross(ax).normalized()
    v = []
    for t in (a, b):
        for i, j in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            v.append(tuple(t + side * (i * w / 2) + other * (j * d / 2)))
    f = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    return gmesh(kit, name, v, f, root)


def lathe(kit, name, base, profile, root, seg=10, axis=(0, 1, 0), ry=None):
    """A turned shape: rings of (radius, height) along `axis` from `base` (game). A zero radius at either end is a
    single point. `ry` squashes the rings across (an oval wall plate)."""
    ax = Vector(axis).normalized()
    t = Vector((1, 0, 0)) if abs(ax.x) < 0.9 else Vector((0, 0, 1))
    s = ax.cross(t).normalized()
    u = ax.cross(s).normalized()
    b = Vector(base)
    v, f, rings = [], [], []
    for r, h in profile:
        if r < 1e-5:
            rings.append([len(v)])
            v.append(tuple(b + ax * h))
            continue
        ring = []
        for i in range(seg):
            a = 2 * math.pi * (i + 0.5) / seg
            ring.append(len(v))
            v.append(tuple(b + ax * h + s * (r * math.cos(a)) + u * (r * (ry or 1.0) * math.sin(a))))
        rings.append(ring)
    for k in range(len(rings) - 1):
        r0, r1 = rings[k], rings[k + 1]
        if len(r0) == 1 and len(r1) == 1:
            continue
        if len(r0) == 1:
            f += [(r0[0], r1[(i + 1) % seg], r1[i]) for i in range(seg)]
        elif len(r1) == 1:
            f += [(r0[i], r0[(i + 1) % seg], r1[0]) for i in range(seg)]
        else:
            f += [(r0[i], r0[(i + 1) % seg], r1[(i + 1) % seg], r1[i]) for i in range(seg)]
    if len(rings[0]) > 1:
        f.append(tuple(reversed(rings[0])))
    if len(rings[-1]) > 1:
        f.append(tuple(rings[-1]))
    return gmesh(kit, name, v, f, root)


def sphere(kit, name, c, r, root, seg=16, rings=10):
    v = [(c[0], c[1] - r, c[2])]
    for k in range(1, rings):
        phi = -math.pi / 2 + math.pi * k / rings
        for i in range(seg):
            a = 2 * math.pi * i / seg
            v.append((c[0] + r * math.cos(phi) * math.cos(a), c[1] + r * math.sin(phi), c[2] + r * math.cos(phi) * math.sin(a)))
    v.append((c[0], c[1] + r, c[2]))
    top = len(v) - 1
    f = [(0, 1 + (i + 1) % seg, 1 + i) for i in range(seg)]
    for k in range(rings - 2):
        a0, a1 = 1 + k * seg, 1 + (k + 1) * seg
        f += [(a0 + i, a0 + (i + 1) % seg, a1 + (i + 1) % seg, a1 + i) for i in range(seg)]
    last = 1 + (rings - 2) * seg
    f += [(last + i, last + (i + 1) % seg, top) for i in range(seg)]
    return gmesh(kit, name, v, f, root)


def tube(kit, name, pts, r, root, sides=4):
    """A thin open-ended path swept along game points (a meridian ring, a hook), capped at both ends."""
    pts = [Vector(p) for p in pts]
    v, f = [], []
    n = len(pts)
    prev = None
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        a = t.cross(ref).normalized() if prev is None else (prev - t * prev.dot(t)).normalized()
        b = t.cross(a).normalized()
        prev = a
        for k in range(sides):
            ang = 2 * math.pi * (k + 0.5) / sides
            v.append(tuple(p + a * (r * math.cos(ang)) + b * (r * math.sin(ang))))
    for i in range(n - 1):
        for k in range(sides):
            j = (k + 1) % sides
            f.append((i * sides + k, i * sides + j, (i + 1) * sides + j, (i + 1) * sides + k))
    f.append(tuple(reversed(range(sides))))
    f.append(tuple(range((n - 1) * sides, n * sides)))
    return gmesh(kit, name, v, f, root)


# ───────────────────────── painting faces ─────────────────────────

FRONT = lambda n: n.y < -0.7                 # noqa: E731  game +Z, toward the camera
BACK = lambda n: n.y > 0.7                   # noqa: E731
UPDOWN = lambda n: abs(n.z) > 0.7            # noqa: E731
TOP = lambda n: n.z > 0.7                    # noqa: E731
RIGHT = lambda n: n.x > 0.7                  # noqa: E731  game +X (the left wall's face)
SIDE = lambda n: abs(n.x) > 0.7              # noqa: E731
ANY = lambda n: True                         # noqa: E731


def sub(crop, box):
    """A sheet box as a UV rectangle (u0, v0, u1, v1) inside the decal cut from `crop` (v = 0 at the bottom)."""
    X0, Y0, X1, Y1 = crop
    x0, y0, x1, y1 = box
    W, H = X1 - X0, Y1 - Y0
    return ((x0 - X0) / W, 1 - (y1 - Y0) / H, (x1 - X0) / W, 1 - (y0 - Y0) / H)


def _plane(n):
    up = Vector((0, 0, 1))
    along = up.cross(n)
    along = along.normalized() if along.length > 1e-4 else Vector((1, 0, 0))
    return along, n.cross(along).normalized()


def paint(obj, rules):
    """Gives each face the first rule whose test passes on its (Blender) normal: (test, material, uv), where uv is
    ("box", tile[, offset]) for a world-space box projection like kit.box_uv, ("rect", (u0, v0, u1, v1)) to fit the
    face into a rectangle of a decal, ("fn", f) for f(game point, game normal) -> (u, v), or ("flat",)."""
    me = obj.data
    mats = []
    for _, m, _ in rules:
        if m not in mats:
            mats.append(m)
    me.materials.clear()
    for m in mats:
        me.materials.append(m)
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    mw = obj.matrix_world
    for poly in me.polygons:
        n = (mw.to_3x3() @ poly.normal).normalized()
        test, mat, spec = next(r for r in rules if r[0](n))
        poly.material_index = mats.index(mat)
        pts = [mw @ me.vertices[me.loops[li].vertex_index].co for li in poly.loop_indices]
        kind = spec[0]
        if kind == "box":
            tu, tv = spec[1] if isinstance(spec[1], tuple) else (spec[1], spec[1])
            du, dv = spec[2] if len(spec) > 2 else (0.0, 0.0)
            ax = max(range(3), key=lambda i: abs(n[i]))
            for li, p in zip(poly.loop_indices, pts):
                if ax == 0:
                    u, v = p.y * (1 if n.x > 0 else -1), p.z
                elif ax == 1:
                    u, v = p.x * (-1 if n.y > 0 else 1), p.z
                else:
                    u, v = p.x, p.y
                uv[li].uv = (u / tu + du, v / tv + dv)
        elif kind == "rect":
            u0, v0, u1, v1 = spec[1]
            along, vert = _plane(n)
            a = [p.dot(along) for p in pts]
            b = [p.dot(vert) for p in pts]
            a0, a1, b0, b1 = min(a), max(a), min(b), max(b)
            for li, ai, bi in zip(poly.loop_indices, a, b):
                uv[li].uv = (u0 + (ai - a0) / max(a1 - a0, 1e-6) * (u1 - u0), v0 + (bi - b0) / max(b1 - b0, 1e-6) * (v1 - v0))
        elif kind == "fn":
            gn = G(n)
            for li, p in zip(poly.loop_indices, pts):
                uv[li].uv = spec[1](G(p), gn)
        else:
            for li in poly.loop_indices:
                uv[li].uv = (0.5, 0.5)
    return obj


def view_uv(centre, rect, half_w, half_h=None, y0=None, height=None):
    """A painted front view projected onto a round thing as the room camera sees it: u across the view, v up the
    view (or up the height for an upright cylinder), into a decal rectangle. The back gets the mirror image."""
    c = Vector(centre)
    view = (Vector(CAM) - c).normalized()
    right = Vector((view.z, 0, -view.x)).normalized()
    up = right.cross(-view).normalized()
    u0, v0, u1, v1 = rect

    def f(p, n):
        d = p - c
        u = 0.5 + d.dot(right) / (2 * half_w)
        if height is not None:
            v = (p.y - y0) / height
        else:
            v = 0.5 + d.dot(up) / (2 * (half_h or half_w))
        u, v = min(max(u, 0.0), 1.0), min(max(v, 0.0), 1.0)
        return (u0 + u * (u1 - u0), v0 + v * (v1 - v0))
    return f


# ───────────────────────── materials ─────────────────────────

def _cut(sheet, box):
    x0, y0, x1, y1 = box
    r = sheet[y0:y1, x0:x1].copy()
    r[..., 3] = 1.0
    return r


def _lum(p):
    return p[..., :3] @ np.array([0.3, 0.59, 0.11])


def materials(kit, room, house):
    M = {}
    flat = lambda name, c: kit.material(name, color=c)  # noqa: E731

    def tex(name, arr, tile=True, mirror="both"):
        if tile:
            return kit.material(name, kit.swatch(arr, name, 0, 0, arr.shape[1], arr.shape[0], mirror=mirror))
        return kit.material(name, kit.decal(arr, name, 0, 0, arr.shape[1], arr.shape[0]))

    # the floorboards: each board of the sheet's patch, inside its ink lines, beside its own mirror image so it
    # repeats along its length; one row per board, so every board of the floor can take its own
    rows = []
    for y0 in PLANK_ROWS:
        r = _cut(room, (PLANK_X[0], y0 + 1, PLANK_X[1], y0 + 31))
        rows.append(np.concatenate([r, r[:, ::-1]], axis=1))
    M["planks"] = tex("library-room-planks", np.concatenate(rows, axis=0), tile=False)

    wood = _cut(room, WOOD)
    M["wood"] = tex("library-room-wood", wood)                               # grain across
    M["wood_v"] = tex("library-room-wood-v", np.ascontiguousarray(np.rot90(wood)))  # grain up
    dark = wood.copy()
    dark[..., :3] *= np.array([0.66, 0.6, 0.55])                             # old oak timber, a shade warmer
    M["timber"] = tex("library-room-timber", dark)
    M["timber_v"] = tex("library-room-timber-v", np.ascontiguousarray(np.rot90(dark)))
    M["panel"] = tex("library-room-panel", _cut(room, PANEL))
    # the house's stone, inside: the same wash with its ink joints faded, so the mirrored tile doesn't print the
    # sheet's few brick marks as a motif across a 10 m wall
    st = _cut(house, HOUSE_STONE)
    mean = st[..., :3].reshape(-1, 3).mean(axis=0)
    st[..., :3] = mean + (st[..., :3] - mean) * STONE_CALM
    M["stone"] = tex("library-room-stone", st)

    M["case"] = tex("library-room-case", _cut(room, CASE), tile=False)
    M["table"] = tex("library-room-table", _cut(room, TABLE_BOX), tile=False)
    M["returns"] = tex("library-room-returns", _cut(room, RETURNS_BOX), tile=False)

    # the plaque is tinted by the game with the shelf's genre colour, so its paint keeps only light and shade:
    # the board's grain and ink lifted to pale parchment
    pl = _cut(room, PLAQUE)
    L = _lum(pl)
    t = np.clip((L - 0.1) / 0.36, 0.0, 1.0) ** 0.7
    val = 0.3 + 0.7 * t
    pl[..., :3] = val[..., None] * np.array([0.97, 0.94, 0.87])
    M["plaque"] = tex("library-room-plaque", pl, tile=False)
    M["plaque_edge"] = flat("library-room-plaque-edge", kit.srgb_to_linear((0.86, 0.82, 0.74)))
    M["hook"] = flat("library-room-plaque-hook", kit.srgb_to_linear((0.8, 0.72, 0.52)))

    # the window: its panes lifted toward the courtyard's light, since the left wall is in the sun's shadow
    win = _cut(room, WINDOW)
    gx0, gy0, gx1, gy1 = 262 - WINDOW[0], 545 - WINDOW[1], 322 - WINDOW[0], 688 - WINDOW[1]
    g = win[gy0:gy1, gx0:gx1]
    lg = _lum(g)
    k = np.clip((lg - 0.38) / 0.1, 0.0, 1.0)[..., None] * 0.85
    g[..., :3] = g[..., :3] * (1 - k) + np.array([1.0, 0.95, 0.82]) * k
    win[gy0:gy1, gx0:gx1] = g
    M["window"] = tex("library-room-window", win, tile=False)
    M["sill"] = flat("library-room-sill", tuple(c * 0.55 for c in kit.sample(room, 300, 712, 3)))
    M["paper"] = flat("library-room-paper", kit.srgb_to_linear((0.86, 0.81, 0.7)))

    # the rug, its fringes filled where the sheet's grey shows between the threads
    rug = _cut(room, RUG)
    fringe = np.array(kit.sample(room, 831, 410, 3))
    fringe_srgb = np.array([(c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055) for c in fringe])
    for xs in (slice(0, 12), slice(rug.shape[1] - 13, rug.shape[1])):
        part = rug[:, xs]
        grey = (np.abs(part[..., 0] - part[..., 2]) < 0.05) & (_lum(part) > 0.45)
        part[grey, :3] = fringe_srgb
        rug[:, xs] = part
    M["rug"] = tex("library-room-rug", rug, tile=False)
    M["rug_edge"] = flat("library-room-rug-edge", kit.sample(room, 845, 410, 3))

    M["globe"] = tex("library-room-globe", _cut(room, GLOBE_BALL), tile=False)
    M["tin"] = tex("library-room-tin", _cut(room, TIN), tile=False)
    M["cup"] = tex("library-room-cup", _cut(room, CUP), tile=False)

    M["brass"] = flat("library-room-brass", kit.sample(room, 700, 445, 3))
    M["brass_dark"] = flat("library-room-brass-dark", kit.sample(room, 122, 500, 3))
    M["iron"] = flat("library-room-iron", kit.sample(room, 1165, 350, 2))
    M["tin_lid"] = flat("library-room-tin-lid", kit.sample(room, 330, 375, 4))
    M["glass"] = flat("library-room-lamp-glass", kit.srgb_to_linear((1.0, 0.86, 0.6)))
    M["wax"] = flat("library-room-wax", kit.srgb_to_linear((0.95, 0.9, 0.79)))
    M["wick"] = flat("library-room-wick", kit.srgb_to_linear((0.2, 0.15, 0.12)))
    M["pages"] = flat("library-room-pages", kit.srgb_to_linear((0.95, 0.91, 0.82)))
    M["ledger"] = flat("library-room-ledger", kit.srgb_to_linear((0.42, 0.23, 0.18)))
    M["pencil_a"] = flat("library-room-pencil-a", kit.sample(room, 477, 362, 2))
    M["pencil_b"] = flat("library-room-pencil-b", kit.sample(room, 494, 357, 2))
    M["graphite"] = flat("library-room-graphite", kit.srgb_to_linear((0.25, 0.24, 0.24)))
    M["soot"] = flat("library-room-soot", kit.srgb_to_linear((0.16, 0.13, 0.11)))
    return M


# ───────────────────────── shell (room coordinates) ─────────────────────────

def build_shell(kit, M, root):
    parts = []
    T_WOOD = (2.4, 0.34)
    T_WOOD_V = (0.34, 2.4)
    stone_tile = kit.sheet_tile(*HOUSE_STONE, HOUSE_PX)

    def add(o):
        parts.append(o)
        return o

    # ── the floor: boards running toward the camera, each cut into lengths at staggered joints; each length
    #    takes one board of the sheet's patch at its own offset, and the game's ink outlines the joints ──
    rng = np.random.RandomState(1310)
    x0, x1, z0, z1 = -5.42, 5.5, -5.42, 5.5
    n = 34
    pw = (x1 - x0) / n
    period = 2 * (PLANK_X[1] - PLANK_X[0]) * pw / 30   # metres per mirrored row (30 px across a board)
    nrows = len(PLANK_ROWS)
    for i in range(n):
        xa, xb = x0 + i * pw, x0 + (i + 1) * pw
        z = z0
        cut = rng.uniform(0.4, 3.2)
        while z < z1 - 1e-6:
            zb = min(z + cut, z1)
            if z1 - zb < 0.6:
                zb = z1
            row = rng.randint(nrows)
            off = rng.uniform(0, 1)
            vt = 1 - row / nrows - 1.2 / (30 * nrows)
            vb = 1 - (row + 1) / nrows + 1.2 / (30 * nrows)
            seg = gbox(kit, f"board{i}-{z:.2f}", (xa, -0.2, z), (xb, 0.0, zb), root)

            def f(p, nn, xa=xa, zb=zb, off=off, vt=vt, vb=vb):
                along = (zb - p.z) / period + off
                if abs(nn.y) > 0.7:
                    return (along, vb + (p.x - xa) / pw * (vt - vb))
                if abs(nn.x) > 0.7:
                    return (along, vb + (p.y + 0.2) / 0.2 * (vt - vb) * 0.3)
                return (off + (p.x - xa) / pw * 0.05, vb + (p.y + 0.2) / 0.2 * (vt - vb))
            paint(seg, [(ANY, M["planks"], ("fn", f))])
            add(seg)
            z = zb
            cut = rng.uniform(1.6, 3.8)

    # ── walls: grey stone (the house's own), one box per timber-framed bay ──
    k = 0
    for a, b in zip(BACK_BAYS, BACK_BAYS[1:]):
        w = gbox(kit, f"back-wall{k}", (a, -0.2, -5.8), (b, 6.5, WALL), root)
        add(paint(w, [(ANY, M["stone"], ("box", stone_tile, BAY_OFFSETS[k % len(BAY_OFFSETS)]))]))
        k += 1
    for a, b in zip(LEFT_BAYS, LEFT_BAYS[1:]):
        w = gbox(kit, f"left-wall{k}", (-5.8, -0.2, a), (WALL, 6.5, b), root)
        add(paint(w, [(ANY, M["stone"], ("box", stone_tile, BAY_OFFSETS[k % len(BAY_OFFSETS)]))]))
        k += 1

    # panelling: the back wall's runs up to the long rail over the cases; the left wall's is a dado under the
    # window and the lamp (four and two rows of the sheet's panels)
    e = 0.035
    bp = gbox(kit, "back-panelling", (WALL, 0.0, WALL), (5.6, PANEL_TOP, WALL + e), root)
    add(paint(bp, [(FRONT, M["panel"], ("box", (1.96, PANEL_TOP / 2))), (ANY, M["timber"], ("box", T_WOOD))]))
    lp = gbox(kit, "left-panelling", (WALL, 0.0, WALL), (WALL + e, DADO, 5.6), root)
    add(paint(lp, [(RIGHT, M["panel"], ("box", (1.96, DADO))), (ANY, M["timber"], ("box", T_WOOD))]))

    timber = [(UPDOWN, M["timber"], ("box", T_WOOD)), (ANY, M["timber"], ("box", T_WOOD))]
    timber_v = [(UPDOWN, M["timber"], ("box", T_WOOD)), (ANY, M["timber_v"], ("box", T_WOOD_V))]
    for name, lo, hi in (
        ("skirting-back", (WALL, 0.0, WALL), (5.6, 0.2, WALL + 0.07)),
        ("skirting-left", (WALL, 0.0, WALL), (WALL + 0.07, 0.2, 5.6)),
        ("dado-rail", (WALL, DADO - 0.02, WALL), (WALL + 0.075, DADO + 0.08, 5.6)),
        ("case-rail", (WALL, PANEL_TOP, WALL), (5.6, PANEL_TOP + 0.18, WALL + 0.13)),
        ("case-rail-cap", (WALL, PANEL_TOP + 0.18, WALL), (5.6, PANEL_TOP + 0.22, WALL + 0.16)),
    ):
        add(paint(gbox(kit, name, lo, hi, root), timber))
    for name, lo, hi in (
        ("post-corner", (WALL, 0.0, WALL), (WALL + 0.24, 6.5, WALL + 0.24)),
        ("post-end", (5.36, 0.0, WALL), (5.6, 6.5, WALL + 0.24)),
    ):
        o = paint(gbox(kit, name, lo, hi, root), timber_v)
        add(o)
    # the posts of the upper storey's framing, over the long rail and over the dado
    for x in BACK_BAYS[1:-1]:
        add(paint(gbox(kit, f"post-back{x}", (x - 0.1, PANEL_TOP + 0.22, WALL), (x + 0.1, 6.5, WALL + 0.1), root), timber_v))
    for z in LEFT_BAYS[1:-1]:
        add(paint(gbox(kit, f"post-left{z}", (WALL, DADO + 0.08, z - 0.1), (WALL + 0.1, 6.5, z + 0.1), root), timber_v))

    # ── the high window: the sheet's arched window cut to its outline, on the left wall, with a real stone sill ──
    X0, Y0, X1, Y1 = WINDOW
    W = WIN_W
    H = W * (Y1 - Y0) / (X1 - X0)
    yb = WIN_Y - H / 2
    xw = WALL + 0.012

    def wpt(px, py):  # sheet pixel -> game point on the wall (the sheet's right is toward the back wall)
        u = (px - X0) / (X1 - X0)
        v = 1 - (py - Y0) / (Y1 - Y0)
        return (xw, yb + v * H, WIN_Z - (u - 0.5) * W)

    cx, cy, r = (X0 + X1) / 2, 563.0, (353 - 235) / 2   # the arch: a half circle over the jambs
    outline = [(230, 725), (359, 725), (359, 700), (353, 700)]
    outline += [(cx + r * math.cos(a), cy - r * math.sin(a)) for a in np.linspace(0, math.pi, 13)]
    outline += [(235, 700), (230, 700)]
    verts = [wpt(*p) for p in outline]
    win = gmesh(kit, "window", verts, [tuple(range(len(verts)))], root)
    win.data.flip_normals() if win.data.polygons[0].normal.x < 0 else None
    me = win.data
    me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            px, py = outline[me.loops[li].vertex_index]
            me.uv_layers.active.data[li].uv = ((px - X0) / (X1 - X0), 1 - (py - Y0) / (Y1 - Y0))
    kit.assign(win, M["window"])
    add(win)
    # the sill: stands out over the painted one (its front is the painted sill's front)
    sill_top = yb + (725 - 702) / (Y1 - Y0) * H
    sill = gbox(kit, "window-sill", (WALL, yb - 0.01, WIN_Z - W / 2), (WALL + 0.07, sill_top, WIN_Z + W / 2), root)
    add(paint(sill, [(RIGHT, M["window"], ("rect", sub(WINDOW, (230, 702, 359, 723)))), (ANY, M["sill"], ("flat",))]))

    # ── the faded rug, under the table ──
    rw = 5.4 * (RUG[2] - RUG[0]) / (1103 - 837)
    rd = 5.4 * (RUG[3] - RUG[1]) / (1103 - 837)
    rc = (0.6, 1.2)
    rug = gbox(kit, "rug", (rc[0] - rw / 2, 0.0, rc[1] - rd / 2), (rc[0] + rw / 2, 0.03, rc[1] + rd / 2), root)
    add(paint(rug, [(TOP, M["rug"], ("rect", (0, 0, 1, 1))), (ANY, M["rug_edge"], ("flat",))]))

    return kit.join("shell", parts)


# ───────────────────────── one bookcase (around its own origin) ─────────────────────────

def build_case(kit, M, root):
    w, d = CASE_W, CASE_D
    hw = w / 2
    parts = []
    T_WOOD = (2.4, 0.34)
    T_WOOD_V = (0.34, 2.4)

    def piece(name, lo, hi, front=None, top="wood"):
        o = gbox(kit, name, lo, hi, root)
        rules = []
        if front is not None:
            rules.append((FRONT, M["case"], ("rect", sub(CASE, front))))
        rules.append((UPDOWN, M[top], ("box", T_WOOD)))
        rules.append((ANY, M["wood_v"], ("box", T_WOOD_V)))
        parts.append(paint(o, rules))

    # BuildCase: plinth (0..0.28), sides (x ±(hw-0.1..hw), full height), back (z -d/2 .. -d/2+0.06), boards with
    # their tops at 1.25 and 2.25, a brass rail under the first, the crown (3.08..3.22 + 0.06 = 3.14..3.28)
    piece("plinth", (-hw - 0.03, 0.0, -d / 2), (hw + 0.03, 0.28, d / 2 + 0.04), (37, 245, 240, 287))
    # the sides run the full height; plinth and frieze stand a little proud of them, so the painted post (sheet
    # 58..261, between the plinth and the frieze) is mapped onto the face's whole height accordingly
    piece("side-l", (-hw, 0.0, -d / 2), (-hw + 0.1, CASE_H, d / 2), (42, 45, 57, 282))
    piece("side-r", (hw - 0.1, 0.0, -d / 2), (hw, CASE_H, d / 2), (218, 45, 233, 282))
    bz0, bz1 = -d / 2, -d / 2 + 0.06
    piece("back-low", (-hw + 0.1, 0.28, bz0), (hw - 0.1, BOARD_Y - 0.04, bz1), (59, 206, 211, 260), top="wood_v")
    piece("back-mid", (-hw + 0.1, BOARD_Y - 0.04, bz0), (hw - 0.1, 2.21, bz1), (59, 131, 211, 189), top="wood_v")
    piece("back-top", (-hw + 0.1, 2.21, bz0), (hw - 0.1, 2.98, bz1), (59, 58, 211, 117), top="wood_v")
    for i, (y, front) in enumerate(((BOARD_Y, (59, 120, 211, 130)), (2.25, (59, 190, 211, 205)))):
        piece(f"board{i}", (-(w - 0.12) / 2, y - 0.08, -(d - 0.04) / 2), ((w - 0.12) / 2, y, (d - 0.04) / 2), front)
    piece("frieze", (-hw, 2.98, -d / 2), (hw, CASE_H - 0.01, d / 2 + 0.012), (41, 45, 234, 57))
    piece("crown", (-hw - 0.08, CASE_H - 0.01, -d / 2 - 0.03), (hw + 0.08, CASE_H + 0.13, d / 2 + 0.07), (31, 27, 244, 45))
    rail = lathe(kit, "brass-rail", (-(w - 0.12) / 2, BOARD_Y - 0.12, d / 2 - 0.02), [(0.024, 0.0), (0.024, w - 0.12)], root, seg=6, axis=(1, 0, 0))
    parts.append(paint(rail, [(ANY, M["brass"], ("flat",))]))
    for sx in (-1, 1):
        stud = gbox(kit, f"rail-stud{sx}", (sx * (hw - 0.1) - 0.03, BOARD_Y - 0.15, d / 2 - 0.05), (sx * (hw - 0.1) + 0.03, BOARD_Y - 0.09, d / 2 - 0.01), root)
        parts.append(paint(stud, [(ANY, M["brass"], ("flat",))]))
    return kit.join("case", parts)


# ───────────────────────── the tag plaque (around its hook point) ─────────────────────────

def build_plaque(kit, M, root):
    hw, top, bot, r = 0.36, -0.03, -0.37, 0.06
    pts = []  # game (x, y), clockwise from the top-left notch as seen from the front

    def arc(cx, cy, a0, a1):
        for a in np.linspace(a0, a1, 5):
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    arc(-hw, top, 0.0, -math.pi / 2)              # top-left notch (concave)
    arc(-hw, bot, math.pi / 2, 0.0)               # bottom-left
    arc(hw, bot, math.pi, math.pi / 2)            # bottom-right
    arc(hw, top, -math.pi / 2, -math.pi)          # top-right
    board = kit.prism("board", [(x, y) for x, y in reversed(pts)], -0.03, 0.03, root)
    parts = [paint(board, [(FRONT, M["plaque"], ("rect", (0, 0, 1, 1))), (BACK, M["plaque"], ("rect", (0, 0, 1, 1))),
                           (ANY, M["plaque_edge"], ("flat",))])]
    # two hooks: a small plate screwed to the board, a link up to a ring that catches the crown
    for sx in (-0.22, 0.22):
        strap = gbox(kit, f"strap{sx}", (sx - 0.026, -0.085, 0.028), (sx + 0.026, -0.04, 0.04), root)
        link = gbox(kit, f"link{sx}", (sx - 0.008, -0.07, 0.012), (sx + 0.008, 0.048, 0.03), root)
        ring = [(sx + 0.03 * math.sin(a), 0.075 - 0.03 * math.cos(a), 0.015) for a in np.linspace(0, 2 * math.pi, 9)]
        loop = tube(kit, f"ring{sx}", ring, 0.009, root, sides=4)
        for o in (strap, link, loop):
            parts.append(paint(o, [(ANY, M["hook"], ("flat",))]))
    return kit.join("plaque", parts)


# ───────────────────────── the reading table (room coordinates) ─────────────────────────

def build_table(kit, M, root):
    tx, _, tz = TABLE
    parts = []
    T_WOOD = (2.4, 0.34)
    T_WOOD_V = (0.34, 2.4)
    wood = [(UPDOWN, M["wood"], ("box", T_WOOD)), (ANY, M["wood_v"], ("box", T_WOOD_V))]

    # the top: 2.9 x 1.5, its surface at 1.02 (the pile lies on it); its edge is the sheet's
    top = gbox(kit, "top", (tx - 1.45, 0.90, tz - 0.75), (tx + 1.45, 1.02, tz + 0.75), root)
    edge = sub(TABLE_BOX, (474, 112, 857, 126))
    parts.append(paint(top, [(TOP, M["wood"], ("box", (2.9, 0.5))), (UPDOWN, M["wood"], ("box", T_WOOD)),
                             (ANY, M["table"], ("rect", edge))]))
    # aprons between the legs
    apron = sub(TABLE_BOX, (536, 127, 792, 142))
    for name, lo, hi in (("apron-f", (tx - 1.24, 0.76, tz + 0.56), (tx + 1.24, 0.90, tz + 0.6)),
                         ("apron-b", (tx - 1.24, 0.76, tz - 0.6), (tx + 1.24, 0.90, tz - 0.56)),
                         ("apron-l", (tx - 1.32, 0.76, tz - 0.56), (tx - 1.28, 0.90, tz + 0.56)),
                         ("apron-r", (tx + 1.28, 0.76, tz - 0.56), (tx + 1.32, 0.90, tz + 0.56))):
        parts.append(paint(gbox(kit, name, lo, hi, root), [(UPDOWN, M["wood"], ("box", T_WOOD)),
                                                           (ANY, M["table"], ("rect", apron))]))
    # turned legs: a square block under the top, the vase turning, a square block for the stretchers, a bun foot
    turn = [(0.04, 0.26), (0.058, 0.285), (0.04, 0.31), (0.036, 0.34), (0.06, 0.42), (0.072, 0.48), (0.064, 0.55),
            (0.04, 0.62), (0.03, 0.66), (0.046, 0.7), (0.04, 0.73), (0.05, 0.765)]
    bun = [(0.0, 0.0), (0.045, 0.004), (0.065, 0.03), (0.06, 0.07), (0.045, 0.095)]
    for sx in (-1.3, 1.3):
        for sz in (-0.62, 0.62):
            x, z = tx + sx, tz + sz
            parts.append(paint(gbox(kit, f"leg-top{sx}{sz}", (x - 0.06, 0.76, z - 0.06), (x + 0.06, 0.9, z + 0.06), root), wood))
            parts.append(paint(lathe(kit, f"leg{sx}{sz}", (x, 0, z), turn, root, seg=8), [(ANY, M["wood_v"], ("box", T_WOOD_V))]))
            parts.append(paint(gbox(kit, f"leg-block{sx}{sz}", (x - 0.055, 0.09, z - 0.055), (x + 0.055, 0.26, z + 0.055), root), wood))
            parts.append(paint(lathe(kit, f"foot{sx}{sz}", (x, 0, z), bun, root, seg=8), [(ANY, M["wood_v"], ("box", T_WOOD_V))]))
    # the H stretcher
    for sx in (-1.3, 1.3):
        parts.append(paint(gbox(kit, f"stretch{sx}", (tx + sx - 0.03, 0.14, tz - 0.6), (tx + sx + 0.03, 0.21, tz + 0.6), root), wood))
    parts.append(paint(gbox(kit, "stretch-long", (tx - 1.27, 0.15, tz - 0.03), (tx + 1.27, 0.21, tz + 0.03), root), wood))

    # the candle in its brass dish, under the game's flame glow
    cx, cy, cz = CANDLE
    dish = lathe(kit, "dish", (cx, cy, cz), [(0.0, 0.0), (0.15, 0.0), (0.165, 0.025), (0.15, 0.03), (0.06, 0.02), (0.055, 0.045), (0.0, 0.045)], root, seg=12)
    parts.append(paint(dish, [(ANY, M["brass"], ("flat",))]))
    wax = lathe(kit, "candle", (cx, cy + 0.02, cz), [(0.0, 0.0), (0.048, 0.0), (0.05, 0.27), (0.04, 0.29), (0.0, 0.29)], root, seg=8)
    parts.append(paint(wax, [(ANY, M["wax"], ("flat",))]))
    wick = gbox(kit, "wick", (cx - 0.006, cy + 0.3, cz - 0.006), (cx + 0.006, cy + 0.34, cz + 0.006), root)
    parts.append(paint(wick, [(ANY, M["wick"], ("flat",))]))

    # an open ledger at the right end, clear of the pile and of anything odd set beside it
    lx, lz, yaw = tx + 1.07, tz + 0.33, 0.18
    parts.append(paint(obox(kit, "ledger-cover", (lx, 1.03, lz), (0.66, 0.02, 0.47), root, yaw), [(ANY, M["ledger"], ("flat",))]))
    for s in (-1, 1):
        c, sn = math.cos(yaw), math.sin(yaw)
        ox = s * 0.155
        pg = obox(kit, f"ledger-pages{s}", (lx + ox * c, 1.055, lz - ox * sn), (0.3, 0.035, 0.43), root, yaw)
        parts.append(paint(pg, [(ANY, M["pages"], ("flat",))]))

    # Ada's pencil cup, at the back right corner
    px, pz = tx + 1.3, tz - 0.55
    cup = lathe(kit, "cup", (px, 1.02, pz), [(0.0, 0.0), (0.06, 0.0), (0.064, 0.15), (0.0, 0.15)], root, seg=10)
    parts.append(paint(cup, [(TOP, M["soot"], ("flat",)), (ANY, M["cup"], ("fn", view_uv((px, 1.09, pz), (0, 0, 1, 1), 0.064, y0=1.02, height=0.15)))]))
    for i, (dx, dz, tilt, mat) in enumerate(((-0.025, 0.01, -0.22, "pencil_a"), (0.0, -0.02, 0.05, "pencil_b"),
                                             (0.022, 0.012, 0.2, "pencil_b"), (0.03, -0.015, 0.3, "pencil_a"))):
        axis = (math.sin(tilt) * 0.8, math.cos(tilt), math.sin(tilt) * 0.3)
        pen = lathe(kit, f"pencil{i}", (px + dx, 1.05, pz + dz), [(0.011, 0.0), (0.011, 0.19), (0.0045, 0.22), (0.0, 0.23)], root, seg=6, axis=axis)
        parts.append(paint(pen, [(ANY, M[mat], ("flat",))]))
    return kit.join("table", parts)


# ───────────────────────── decor (room coordinates) ─────────────────────────

def build_decor(kit, M, root):
    parts = []
    T_WOOD = (2.4, 0.34)
    T_WOOD_V = (0.34, 2.4)
    wood = [(UPDOWN, M["wood"], ("box", T_WOOD)), (ANY, M["wood_v"], ("box", T_WOOD_V))]
    brass = [(ANY, M["brass"], ("flat",))]

    # ── the brass wall lamp on the left wall at the back: oval plate, arm, the font under the glass, a finial ──
    gx, gy, gz = GLASS
    arm_y = gy - 0.13
    plate = lathe(kit, "lamp-plate", (WALL, arm_y, gz), [(0.0, 0.0), (0.13, 0.0), (0.13, 0.022), (0.105, 0.035), (0.0, 0.035)], root, seg=12, axis=(1, 0, 0), ry=0.69)
    parts.append(paint(plate, brass))
    arm = lathe(kit, "lamp-arm", (WALL + 0.03, arm_y, gz), [(0.0, 0.0), (0.022, 0.0), (0.02, 0.2), (0.03, 0.205), (0.03, 0.225), (0.0, 0.23)], root, seg=6, axis=(1, 0, 0))
    parts.append(paint(arm, brass))
    font = [(0.0, -0.31), (0.012, -0.30), (0.032, -0.26), (0.03, -0.235), (0.06, -0.215), (0.112, -0.18), (0.124, -0.135),
            (0.108, -0.095), (0.06, -0.075), (0.048, -0.05), (0.086, -0.035), (0.086, 0.0), (0.0, 0.0)]
    parts.append(paint(lathe(kit, "lamp-font", (gx, gy, gz), font, root, seg=12), brass))
    key = lathe(kit, "lamp-key", (gx, gy - 0.06, gz), [(0.0, 0.0), (0.008, 0.0), (0.008, 0.08), (0.022, 0.085), (0.022, 0.1), (0.0, 0.1)], root, seg=6, axis=(0.6, 0, 0.8))
    parts.append(paint(key, brass))

    # ── the rolling ladder, hooked over the crowns of the 4th and 5th cases ──
    foot_z, top_z, top_y = -4.08, -4.565, 3.2
    for s in (-1, 1):
        x = LADDER_X + s * 0.26
        parts.append(paint(beam(kit, f"rail{s}", (x, 0.1, foot_z), (x, top_y, top_z), 0.065, 0.05, root), wood))
        bracket = gbox(kit, f"wheel-bracket{s}", (x - 0.035, 0.05, foot_z - 0.035), (x + 0.035, 0.16, foot_z + 0.035), root)
        parts.append(paint(bracket, brass))
        wheel = lathe(kit, f"wheel{s}", (x - 0.02, 0.055, foot_z), [(0.0, 0.0), (0.05, 0.0), (0.055, 0.02), (0.05, 0.04), (0.0, 0.04)], root, seg=10, axis=(1, 0, 0))
        parts.append(paint(wheel, [(ANY, M["iron"], ("flat",))]))
        # the hook: up from the rail, over the crown's top, and a lip down its back edge
        hook = [(x, top_y - 0.12, top_z - 0.02), (x, CASE_H + 0.17, top_z - 0.035), (x, CASE_H + 0.19, top_z - 0.1),
                (x, CASE_H + 0.17, top_z - 0.2), (x, CASE_H + 0.15, top_z - 0.22)]
        parts.append(paint(tube(kit, f"hook{s}", hook, 0.016, root, sides=4), brass))
    bar = lathe(kit, "ladder-bar", (LADDER_X - 0.32, top_y - 0.08, top_z + 0.0), [(0.02, 0.0), (0.02, 0.64)], root, seg=6, axis=(1, 0, 0))
    parts.append(paint(bar, brass))
    for i in range(1, 7):
        t = i / 7
        y = 0.1 + (top_y - 0.1) * t
        z = foot_z + (top_z - foot_z) * t
        parts.append(paint(gbox(kit, f"tread{i}", (LADDER_X - 0.25, y - 0.02, z - 0.065), (LADDER_X + 0.25, y + 0.02, z + 0.065), root), wood))

    # ── Mab's stool, and her biscuit tin on it ──
    sx, _, sz = STOOL
    seat = lathe(kit, "stool-seat", (sx, 0.6, sz), [(0.0, 0.0), (0.27, 0.0), (0.305, 0.03), (0.3, 0.07), (0.0, 0.07)], root, seg=12)
    stool = [paint(seat, wood)]
    for k, a in enumerate((0.4, 2.5, 4.6)):
        top = (sx + math.cos(a) * 0.17, 0.62, sz + math.sin(a) * 0.17)
        low = (sx + math.cos(a) * 0.25, 0.0, sz + math.sin(a) * 0.25)
        stool.append(paint(beam(kit, f"stool-leg{k}", low, top, 0.055, 0.055, root), wood))
        a2 = a + 2.1
        r0 = (sx + math.cos(a) * 0.215, 0.24, sz + math.sin(a) * 0.215)
        r1 = (sx + math.cos(a2) * 0.215, 0.24, sz + math.sin(a2) * 0.215)
        stool.append(paint(beam(kit, f"stool-rung{k}", r0, r1, 0.03, 0.03, root), wood))
    tin_c = (sx + 0.03, 0.67, sz - 0.02)
    body = lathe(kit, "tin", tin_c, [(0.0, 0.0), (0.145, 0.0), (0.15, 0.01), (0.15, 0.12), (0.0, 0.12)], root, seg=14)
    stool.append(paint(body, [(UPDOWN, M["tin_lid"], ("flat",)),
                              (ANY, M["tin"], ("fn", view_uv((tin_c[0], tin_c[1] + 0.06, tin_c[2]), sub(TIN, (276, 408, 388, 462)), 0.15, y0=tin_c[1], height=0.12)))]))
    lid = lathe(kit, "tin-lid", (tin_c[0], tin_c[1] + 0.115, tin_c[2]), [(0.0, 0.0), (0.156, 0.0), (0.158, 0.03), (0.14, 0.042), (0.03, 0.045),
                                                                       (0.028, 0.06), (0.04, 0.07), (0.03, 0.08), (0.0, 0.08)], root, seg=14)
    stool.append(paint(lid, [(ANY, M["tin_lid"], ("flat",))]))
    for o in stool:
        kit.shear(o, kx=0.012)
    parts += stool

    # ── a floor globe by the window: a turned stand, the painted globe, a brass meridian ──
    gx, _, gz = GLOBE
    stand = [(0.0, 0.0), (0.23, 0.0), (0.245, 0.025), (0.2, 0.055), (0.09, 0.075), (0.05, 0.11), (0.075, 0.18), (0.075, 0.22),
             (0.045, 0.3), (0.038, 0.44), (0.06, 0.49), (0.044, 0.53), (0.07, 0.56), (0.05, 0.6), (0.0, 0.6)]
    globe = [paint(lathe(kit, "globe-stand", (gx, 0, gz), stand, root, seg=10), [(ANY, M["wood_v"], ("box", T_WOOD_V))])]
    R, cy = 0.3, 0.6 + 0.045 + 0.3
    ball = sphere(kit, "globe", (gx, cy, gz), R, root, seg=24, rings=14)
    globe.append(paint(ball, [(ANY, M["globe"], ("fn", view_uv((gx, cy, gz), (0, 0, 1, 1), R)))]))
    c = Vector((gx, cy, gz))
    view = (Vector(CAM) - c).normalized()
    right = Vector((view.z, 0, -view.x)).normalized()
    up = Vector((0, 1, 0))
    tilt = math.radians(18)
    axis = (up * math.cos(tilt) + right * math.sin(tilt)).normalized()
    left = (-right * math.cos(tilt) + up * math.sin(tilt)).normalized()
    Rm = R + 0.035
    ring = [tuple(c + left * (Rm * math.cos(a)) + axis * (Rm * math.sin(a))) for a in np.linspace(-math.pi / 2, math.pi / 2, 15)]
    globe.append(paint(tube(kit, "meridian", ring, 0.022, root, sides=6), [(ANY, M["brass"], ("flat",))]))
    for k, s in enumerate((-1, 1)):
        pole = c + axis * (s * (Rm + 0.01))
        globe.append(paint(lathe(kit, f"pivot{k}", tuple(pole - axis * 0.04), [(0.0, 0.0), (0.022, 0.0), (0.022, 0.06), (0.0, 0.07)], root, seg=6, axis=tuple(axis)), [(ANY, M["brass_dark"], ("flat",))]))
    low = c - axis * Rm
    globe.append(paint(beam(kit, "globe-stem", (gx, 0.58, gz), tuple(low), 0.035, 0.035, root), [(ANY, M["brass_dark"], ("flat",))]))
    for o in globe:
        kit.shear(o, kx=-0.01)
    parts += globe

    # ── paper drifts: loose sheets that got away from the pile ──
    for i, (x, z, yaw) in enumerate(((4.05, 1.35, 0.4), (4.4, 1.7, -0.3), (3.95, 1.85, 1.1), (-2.75, 1.25, 0.7), (-0.2, 3.25, -0.5))):
        sheet = obox(kit, f"paper{i}", (x, 0.003 + 0.002 * (i % 2), z), (0.24, 0.006, 0.32), root, yaw)
        parts.append(paint(sheet, [(ANY, M["paper"], ("flat",))]))
    return kit.join("decor", parts)


# ───────────────────────── the returns box (room coordinates, drawn lifted) ─────────────────────────

def build_returns(kit, M, root):
    rx, _, rz = RETURNS
    T_WOOD = (2.4, 0.34)
    T_WOOD_V = (0.34, 2.4)
    parts = []

    def piece(name, lo, hi, front, side=None):
        o = gbox(kit, name, (rx + lo[0], lo[1], rz + lo[2]), (rx + hi[0], hi[1], rz + hi[2]), root)
        rules = [(FRONT, M["returns"], ("rect", sub(RETURNS_BOX, front)))]
        if side is not None:
            rules.append((SIDE, M["returns"], ("rect", sub(RETURNS_BOX, side))))
        rules += [(UPDOWN, M["wood"], ("box", T_WOOD)), (ANY, M["wood_v"], ("box", T_WOOD_V))]
        parts.append(paint(o, rules))

    piece("plinth", (-0.48, 0.0, -0.36), (0.48, 0.12, 0.36), (905, 245, 1069, 267))
    piece("body", (-0.45, 0.12, -0.33), (0.45, 0.6, 0.33), (934, 140, 1063, 245), side=(911, 141, 934, 244))
    piece("eave", (-0.49, 0.6, -0.37), (0.49, 0.65, 0.37), (907, 129, 1068, 141))
    # the lid: a long front slope with the painted slot (where the loan card stands at z +0.05), a short back one
    ridge_z, ridge_y = -0.22, 0.84
    lid = kit.prism_x("lid", [(-0.37, 0.65), (0.37, 0.65), (-ridge_z, ridge_y)], rx - 0.47, rx + 0.47, root)
    # prism_x takes (y, z) in Blender: y = -game z
    for v in lid.data.vertices:
        v.co.y += -rz
    parts.append(paint(lid, [(lambda n: n.y < -0.1 and n.z > 0.3, M["returns"], ("rect", sub(RETURNS_BOX, (917, 93, 1058, 129)))),
                             (UPDOWN, M["wood"], ("box", T_WOOD)), (ANY, M["wood_v"], ("box", T_WOOD_V))]))
    for o in parts:
        kit.shear(o, kx=0.01)
    return kit.join("returns", parts)


# ───────────────────────── the lamp's glass (around its own origin) ─────────────────────────

def build_glass(kit, M, root):
    chimney = [(0.0, 0.0), (0.075, 0.0), (0.106, 0.05), (0.12, 0.12), (0.106, 0.2), (0.072, 0.26), (0.056, 0.31),
               (0.054, 0.44), (0.062, 0.46), (0.0, 0.46)]
    g = lathe(kit, "lamp-glass", (0, 0, 0), chimney, root, seg=12)
    paint(g, [(ANY, M["glass"], ("flat",))])
    return g


def build(kit):
    room = kit.load_sheet(ROOM_SHEET)
    house = kit.load_sheet(HOUSE_SHEET)
    M = materials(kit, room, house)
    root = kit.root("library-room")
    build_shell(kit, M, root)
    build_case(kit, M, root)
    build_plaque(kit, M, root)
    build_table(kit, M, root)
    build_decor(kit, M, root)
    build_returns(kit, M, root)
    build_glass(kit, M, root)
    return root, 6.5
