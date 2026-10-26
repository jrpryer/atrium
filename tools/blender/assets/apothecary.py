"""The Apothecary: a wide, low cottage of cream lime plaster framed by dark timber corner posts over a grey stone
base course, under a steep violet tiled roof whose long slope faces the front (gables at the sides, a bell-cast
kick at the eaves, small timber brackets under them). On the front: a bowed five-light bay window full of jars and
bottles under its own little hipped violet roof, a narrow violet door with a small window, an iron wall lantern,
a blank signboard hanging off the right corner from an iron bracket, and a cracked patch of plaster showing the
stones behind. A tall red-brown brick chimney stands outside the right (+X) gable, toward the back.
Reference: art/reference/sheets/apothecary-sheet.png (Higgsfield job ec3919c1).

Parts the game uses: body (walls, base, timber, chimney, bay window frame, door, lantern and sign iron), roof
(the main roof and the bay's roof), glass (every window pane, lit at night) and the markers lamp, door-point and
chimney. Front faces -Y in Blender (+Z in the game).

The sheet draws the house taller and narrower than the art bible wants ("wide, with a chimney and a bowed
window"), so the plan is widened to 7.0 x 4.8 m and the heights squashed (eaves 4.4 m, ridge 8.0 m); painted
features keep their real sizes, measured at PX metres per sheet pixel (the door leaf, ~288 px, is ~2.25 m).
"""
import math

import numpy as np

SHEET = "apothecary-sheet.png"
PX = 0.0078  # metres per sheet pixel

# ── proportions (metres) ──
W, D = 7.0, 4.8
HW, HD = W / 2, D / 2
H = 4.4                      # wall eave line
RIDGE = 8.0                  # top of the roof slabs
T = 0.22                     # roof slab thickness (vertical)
YK, ZK = 1.9, 4.965          # the knee of the bell-cast kick (top surface)
YE, ZE = 2.9, 4.215          # the eave tip (top surface)
VERGE = (0.22, 0.32, 0.42)   # gable overhang beyond the walls at the ridge, the knee and the eave (flares out)
BASE_H = 0.74                # stone base course
POST = 0.24                  # timber corner posts
LEAN = -0.014                # a degree of settling to the left (never plumb)

# the bowed bay window (plan offsets from its centre and the wall face; -y is out from the wall)
BAY_X = -1.85
BAY_PTS = [(-1.2, 0.0), (-0.826, -0.45), (-0.30, -0.70), (0.278, -0.70), (0.816, -0.45), (1.2, 0.0)]
SILL0, SILL1 = 0.70, 0.85    # sill board
HEAD0, HEAD1 = 2.50, 2.68    # head band over the lights
BAYROOF_TOP = 3.5            # where the bay roof meets the wall

DOOR_X, DOOR_W, DOOR_H = 0.55, 1.28, 2.62   # the decal with its frame, lintel and step (leaf ≈ 0.93 x 2.2 m)
LAMP = (1.62, -HD - 0.32, 2.72)             # the iron wall lantern right of the door
CHIM_X0, CHIM_X1 = HW - 0.05, HW + 0.70     # the brick stack outside the +X gable
CHIM_Y0, CHIM_Y1 = 0.85, 1.65
CHIM_TOP = 9.5

# ── boxes on the sheet (real pixels, from the top-left) ──
PLASTER = (1545, 800, 2000, 1252)    # the whole back wall between its posts, eave shadow and base course
PLASTER_WINDOW = (1686, 824, 1782, 972)  # the back window inside it, painted out with the plaster beside it
PLASTER_CALM = 0.45                  # brush-stroke contrast kept (mirrored strokes shouldn't read as a pattern)
PLASTER_V0 = 0.62                    # the wall height where the swatch starts, so no mirror line crosses a wall
STONE = (1492, 1268, 2002, 1360)     # back base course, inside its ink outline
TILES = (1480, 310, 2055, 718)       # back roof slope, inside its ridge, eave and verge outlines
BRICK = (1238, 235, 1322, 500)       # chimney (right view) above the roof line
DOOR = (479, 1033, 615, 1370)
DOOR_WIN = (514, 1077, 581, 1193)
BAY_GLASS = (195, 1036, 458, 1262)
CRACK = (455, 775, 640, 1010)
SIGN = (674, 1023, 770, 1095)
WIN_HIGH = (1066, 513, 1148, 638)
WIN_LOW_R = (1048, 1083, 1135, 1223)
WIN_LOW_L = (2359, 1083, 2446, 1223)
WIN_BACK = (1692, 832, 1774, 956)


def _xsweep(kit, name, profile, lo, hi, parent):
    """A closed YZ outline swept along X, each outline point from its own x lo to x hi (a flared verge)."""
    n = len(profile)
    v = [(lo[i], y, z) for i, (y, z) in enumerate(profile)] + [(hi[i], y, z) for i, (y, z) in enumerate(profile)]
    f = [tuple(reversed(range(n))), tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        f.append((i, j, n + j, n + i))
    return kit.mesh_object(name, v, f, parent, solid=True)


def _zprism(kit, name, outline, z0, z1, parent):
    """A closed XY outline extruded up from z0 to z1."""
    n = len(outline)
    v = [(x, y, z0) for x, y in outline] + [(x, y, z1) for x, y in outline]
    f = [tuple(reversed(range(n))), tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        f.append((i, j, n + j, n + i))
    return kit.mesh_object(name, v, f, parent, solid=True)


def _offset(pts, d, wall_y):
    """Offsets the bay's open outline outward by d (mitred); its two ends slide along their edges to wall_y."""
    normals = []
    for i in range(len(pts) - 1):
        ex, ey = pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]
        L = math.hypot(ex, ey)
        normals.append((ey / L, -ex / L))
    out = []
    last = len(pts) - 1
    for i, (x, y) in enumerate(pts):
        if i in (0, last):
            k = 0 if i == 0 else last - 1
            nx, ny = normals[k]
            ex, ey = pts[k + 1][0] - pts[k][0], pts[k + 1][1] - pts[k][1]
            px, py = x + d * nx, y + d * ny
            t = (wall_y - py) / ey
            out.append((px + t * ex, wall_y))
        else:
            (ax, ay), (bx, by) = normals[i - 1], normals[i]
            s = d / (1 + ax * bx + ay * by)
            out.append((x + (ax + bx) * s, y + (ay + by) * s))
    return out


def _plaster(sheet):
    """The back wall's plaster with its window painted out (from the plaster just right of it, edges feathered)
    and the brush-stroke contrast softened, so the mirrored tile doesn't print a pattern across a 7 m wall."""
    x0, y0, x1, y1 = PLASTER
    p = sheet[y0:y1, x0:x1].copy()
    wx0, wy0, wx1, wy1 = PLASTER_WINDOW
    shift = wx1 - wx0 + 14
    patch = sheet[wy0:wy1, wx0 + shift:wx1 + shift]
    hh, ww = patch.shape[:2]
    f = 10.0
    ry = np.minimum(np.arange(hh), hh - 1 - np.arange(hh))[:, None] / f
    rx = np.minimum(np.arange(ww), ww - 1 - np.arange(ww))[None, :] / f
    m = np.clip(np.minimum(ry, rx), 0.0, 1.0)[..., None]
    region = p[wy0 - y0:wy1 - y0, wx0 - x0:wx1 - x0]
    p[wy0 - y0:wy1 - y0, wx0 - x0:wx1 - x0] = patch * m + region * (1 - m)
    mean = p[..., :3].reshape(-1, 3).mean(axis=0)
    p[..., :3] = mean + (p[..., :3] - mean) * PLASTER_CALM
    p[..., 3] = 1.0
    return p


def _tiles(sheet, keep=0.6):
    """The back roof's tiles with the paint's light and dark blotches softened (the ink lines between tiles are
    left alone), so the one mirror line across each slope doesn't print a symmetrical blotch."""
    x0, y0, x1, y1 = TILES
    p = sheet[y0:y1, x0:x1].copy()
    lum = p[..., :3] @ np.array([0.3, 0.59, 0.11])
    paint = np.clip((lum - 0.26) / 0.1, 0.0, 1.0)[..., None]  # 0 on ink, 1 on painted tile
    mean = (p[..., :3] * paint).reshape(-1, 3).sum(axis=0) / paint.sum()
    calm = mean + (p[..., :3] - mean) * keep
    p[..., :3] = p[..., :3] * (1 - paint) + calm * paint
    p[..., 3] = 1.0
    return p


def _offset_uv(obj, du=0.0, dv=0.0):
    for d in obj.data.uv_layers.active.data:
        d.uv = (d.uv[0] + du, d.uv[1] + dv)


def _roof_uv(obj, tile, u_start=0.004):
    """Like kit.slope_uv (along the ridge x up the slope), but each face starts its own repeat inside the
    upright quarter of the mirrored swatch: a long face hangs from the swatch's top row, a short one starts at its
    bottom row. So no horizontal mirror line crosses a slope, and the one vertical mirror line sits off centre."""
    from mathutils import Vector
    tu, tv = tile
    me = obj.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    up = Vector((0, 0, 1))
    for poly in me.polygons:
        n = poly.normal.normalized()
        along = up.cross(n)
        if along.length < 1e-4:
            along = Vector((1, 0, 0))
        along.normalize()
        rise = n.cross(along).normalized()
        pts = [me.vertices[me.loops[li].vertex_index].co for li in poly.loop_indices]
        a = [p.dot(along) for p in pts]
        b = [p.dot(rise) for p in pts]
        a0, b0, b1 = min(a), min(b), max(b)
        long = (b1 - b0) > 2.0
        for li, ai, bi in zip(poly.loop_indices, a, b):
            v = (0.995 - (b1 - bi) / tv) if long else (0.505 + (bi - b0) / tv)
            uv[li].uv = (u_start + (ai - a0) / tu, v)


def _blended_decal(kit, under, name, dec, under_tile, under_off, x0w, z0w, w, h, feather=0.24):
    """A decal whose border fades into the wall swatch exactly as the wall maps it underneath (front faces:
    u = x / tile + du, v = z / tile + dv), so a one-off painted patch has no visible rectangle around it."""
    dec = dec.copy()
    hh, ww = dec.shape[:2]
    p = np.concatenate([under, under[:, ::-1]], axis=1)  # the same 2x2 mirror kit.swatch builds
    p = np.concatenate([p, p[::-1]], axis=0)
    th, tw = p.shape[:2]
    X = x0w + (np.arange(ww) + 0.5) / ww * w
    Z = z0w + h - (np.arange(hh) + 0.5) / hh * h
    U = np.mod(X / under_tile[0] + under_off[0], 1.0)
    V = np.mod(Z / under_tile[1] + under_off[1], 1.0)
    rows = np.clip(((1.0 - V) * th).astype(int), 0, th - 1)  # v=0 is the bottom row of the stored image
    cols = np.clip((U * tw).astype(int), 0, tw - 1)
    under = p[rows[:, None], cols[None, :]]
    ry = np.minimum(np.arange(hh), hh - 1 - np.arange(hh))[:, None] / (feather * hh)
    rx = np.minimum(np.arange(ww), ww - 1 - np.arange(ww))[None, :] / (feather * ww)
    m = np.clip(np.minimum(ry, rx), 0.0, 1.0)
    m = (m * m * (3 - 2 * m))[..., None]
    out = dec * m + under * (1 - m)
    out[..., 3] = 1.0
    return kit.decal(out, name, 0, 0, ww, hh)


def build(kit):
    sheet = kit.load_sheet(SHEET)

    # ── materials: tiling swatches, decals and sampled flat colours ──
    plaster_tile = kit.sheet_tile(*PLASTER, PX)
    plaster_off = (0.0, 0.5 - PLASTER_V0 / plaster_tile[1])  # mirror lines at x = 0 (behind the door), ±3.55 (posts)
    stone_tile = (2 * (STONE[2] - STONE[0]) * PX, 2 * BASE_H)  # one course of stones exactly fills the base
    roof_tile = (2 * (TILES[2] - TILES[0]) * PX, 2 * (TILES[3] - TILES[1]) * PX * 1.15)  # the sheet's slope is foreshortened
    brick_tile = kit.sheet_tile(*BRICK, PX)
    plaster_px = _plaster(sheet)
    plaster = kit.material("apothecary-plaster", kit.swatch(plaster_px, "apothecary-plaster", 0, 0, plaster_px.shape[1], plaster_px.shape[0]))
    stone = kit.material("apothecary-stone", kit.swatch(sheet, "apothecary-stone", *STONE))
    tiles_px = _tiles(sheet)
    tiles = kit.material("apothecary-tiles", kit.swatch(tiles_px, "apothecary-tiles", 0, 0, tiles_px.shape[1], tiles_px.shape[0]))
    brick = kit.material("apothecary-brick", kit.swatch(sheet, "apothecary-brick", *BRICK))
    timber = kit.material("apothecary-timber", color=kit.sample(sheet, 130, 1000, 5))
    violet = kit.material("apothecary-violet", color=kit.sample(sheet, 330, 1012, 4))
    iron = kit.material("apothecary-iron", color=kit.sample(sheet, 717, 1001, 3))
    wood = kit.material("apothecary-sign-wood", color=kit.sample(sheet, 720, 1060, 6))
    soot = kit.material("apothecary-soot", color=kit.srgb_to_linear((0.17, 0.12, 0.10)))
    sill_stone = kit.material("apothecary-sill", color=kit.sample(sheet, 1105, 524, 4))
    door_mat = kit.material("apothecary-door", kit.decal(sheet, "apothecary-door", *DOOR))
    sign_mat = kit.material("apothecary-sign", kit.decal(sheet, "apothecary-sign", *SIGN))
    door_win = kit.material("apothecary-door-window", kit.decal(sheet, "apothecary-door-window", *DOOR_WIN))
    bay_glass = kit.material("apothecary-bay-glass", kit.decal(sheet, "apothecary-bay-glass", *BAY_GLASS))
    win_high = kit.material("apothecary-window-high", kit.decal(sheet, "apothecary-window-high", *WIN_HIGH))
    win_low_r = kit.material("apothecary-window-low-r", kit.decal(sheet, "apothecary-window-low-r", *WIN_LOW_R))
    win_low_l = kit.material("apothecary-window-low-l", kit.decal(sheet, "apothecary-window-low-l", *WIN_LOW_L))
    win_back = kit.material("apothecary-window-back", kit.decal(sheet, "apothecary-window-back", *WIN_BACK))
    lantern_glass = kit.material("apothecary-lantern-glass", color=kit.sample(sheet, 1108, 580, 8))

    root = kit.root("apothecary")
    body_parts, roof_parts, glass_parts = [], [], []

    def part(obj, mat, tile=None, into=body_parts):
        kit.assign(obj, mat)
        if tile is not None:
            kit.box_uv(obj, tile)
        into.append(obj)
        return obj

    def box(name, lo, hi, mat, tile=1.0):
        c = tuple((a + b) / 2 for a, b in zip(lo, hi))
        s = tuple(b - a for a, b in zip(lo, hi))
        return part(kit.box(name, c, s, root), mat, tile)

    def plaque(name, mat, centre, size, facing, into):
        o = kit.plaque(name, mat, centre, size, facing, root)
        into.append(o)
        return o

    # ── walls: a box whose side walls run up into the gables, just inside the roof's underside ──
    e = 0.05  # how far the gable tops sit up inside the roof slab
    walls = kit.prism_x("walls", [(-HD, 0), (HD, 0), (HD, H), (YK, ZK - T + e), (0, RIDGE - T + e),
                                  (-YK, ZK - T + e), (-HD, H)], -HW, HW, root)
    part(walls, plaster, plaster_tile)
    _offset_uv(walls, *plaster_off)

    base = box("base", (-HW - 0.02, -HD - 0.02, 0), (HW + 0.02, HD + 0.02, BASE_H), stone, stone_tile)
    for d in base.data.uv_layers.active.data:  # show the crop the right way up: its upper half of the mirrored tile
        d.uv = (d.uv[0], d.uv[1] + 0.5)

    # timber: corner posts, an eave plate front and back, and the little brackets under both eaves
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(f"post{sx}{sy}", (sx * HW - POST / 2, sy * HD - POST / 2, BASE_H), (sx * HW + POST / 2, sy * HD + POST / 2, H), timber)
    for sy in (-1, 1):
        y_in, y_out = sy * HD, sy * (HD + 0.08)
        box(f"plate{sy}", (-HW, min(y_in, y_out), 4.1), (HW, max(y_in, y_out), H), timber)
    for sx in (-1, 1):  # the tie beam across each gable at the eave line
        x_in, x_out = sx * (HW - 0.02), sx * (HW + 0.08)
        box(f"tie{sx}", (min(x_in, x_out), -HD, 4.1), (max(x_in, x_out), HD, H), timber)
    for sy in (-1, 1):
        for i in range(10):
            x = -3.15 + i * 0.7
            y_in, y_out = sy * HD, sy * (HD + 0.32)
            box(f"bracket{sy}{i}", (x - 0.06, min(y_in, y_out), 3.96), (x + 0.06, max(y_in, y_out), 4.16), timber)

    # bargeboards under each verge, following the roof's underside
    def verge_l(y, sx):
        """How far the roof reaches past the gable at x = sx * L: it flares toward the eaves, except behind the
        chimney, where the +X verge draws in nearly flush so no flap of roof sticks out past the stack."""
        a = abs(y)
        if sx > 0 and y > 0:
            return HW + VERGE[0] + (0.06 - VERGE[0]) * min(a / YK, 1.0)
        if a <= YK:
            return HW + VERGE[0] + (VERGE[1] - VERGE[0]) * a / YK
        return HW + VERGE[1] + (VERGE[2] - VERGE[1]) * (a - YK) / (YE - YK)

    under = [(-YE, ZE - T), (-YK, ZK - T), (0, RIDGE - T), (YK, ZK - T), (YE, ZE - T)]
    for sx in (-1, 1):
        for s, pts in (("l", under[:3]), ("r", under[2:])):
            top = [(y, z + 0.03) for y, z in pts]
            bot = [(y, z - 0.2) for y, z in reversed(pts)]
            prof = top + bot
            outer = [sx * (verge_l(y, sx) - 0.04) for y, _ in prof]
            inner = [sx * max(verge_l(y, sx) - 0.15, HW - 0.05) for y, _ in prof]
            lo, hi = (inner, outer) if sx > 0 else (outer, inner)
            part(_xsweep(kit, f"barge{sx}{s}", prof, lo, hi, root), timber)

    # ── the chimney: a stepped brick stack outside the +X gable, toward the back ──
    box("chimney-base", (CHIM_X0, CHIM_Y0 - 0.2, 0), (CHIM_X1 + 0.1, CHIM_Y1 + 0.2, 2.9), brick, brick_tile)
    shoulder = kit.mesh_object("chimney-shoulder", [
        (CHIM_X0, CHIM_Y0 - 0.2, 2.9), (CHIM_X1 + 0.1, CHIM_Y0 - 0.2, 2.9), (CHIM_X1 + 0.1, CHIM_Y1 + 0.2, 2.9), (CHIM_X0, CHIM_Y1 + 0.2, 2.9),
        (CHIM_X0, CHIM_Y0, 3.5), (CHIM_X1, CHIM_Y0, 3.5), (CHIM_X1, CHIM_Y1, 3.5), (CHIM_X0, CHIM_Y1, 3.5)],
        [(0, 1, 2, 3), (4, 5, 6, 7), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)], root, solid=True)
    part(shoulder, brick, brick_tile)
    box("chimney-stack", (CHIM_X0, CHIM_Y0, 3.5), (CHIM_X1, CHIM_Y1, CHIM_TOP - 0.3), brick, brick_tile)
    # the cap: a corbelled rim round a sooty flue
    cx0, cx1, cy0, cy1 = CHIM_X0 - 0.07, CHIM_X1 + 0.07, CHIM_Y0 - 0.07, CHIM_Y1 + 0.07
    fx0, fx1, fy0, fy1 = CHIM_X0 + 0.16, CHIM_X1 - 0.16, CHIM_Y0 + 0.17, CHIM_Y1 - 0.17
    z0, z1 = CHIM_TOP - 0.3, CHIM_TOP
    box("cap-f", (cx0, cy0, z0), (cx1, fy0, z1), brick, brick_tile)
    box("cap-b", (cx0, fy1, z0), (cx1, cy1, z1), brick, brick_tile)
    box("cap-l", (cx0, fy0, z0), (fx0, fy1, z1), brick, brick_tile)
    box("cap-r", (fx1, fy0, z0), (cx1, fy1, z1), brick, brick_tile)
    box("flue", (fx0, fy0, z0 - 0.05), (fx1, fy1, z1 - 0.12), soot)

    # ── the bowed bay window ──
    yw = -HD
    P = [(BAY_X + x, yw + y) for x, y in BAY_PTS]
    into_wall = yw + 0.04

    def closed(pts):
        return [(pts[0][0], into_wall)] + pts + [(pts[-1][0], into_wall)]

    _offset_uv(part(_zprism(kit, "bay-riser", closed(P), 0.0, SILL0, root), plaster, plaster_tile), *plaster_off)
    part(_zprism(kit, "bay-sill", closed(_offset(P, 0.07, yw)), SILL0, SILL1, root), violet)
    part(_zprism(kit, "bay-head", closed(_offset(P, 0.04, yw)), HEAD0, HEAD1, root), violet)
    for i, (x, y) in enumerate(P):
        part(kit.cylinder(f"bay-post{i}", (x, y, SILL1), 0.045, 0.045, HEAD0 - SILL1, 6, root), violet)

    # the lights: one quad per facet, the painted bay mapped across them as the sheet's front view sees it
    verts, faces = [], []
    for i in range(len(P) - 1):
        (ax, ay), (bx, by) = P[i], P[i + 1]
        k = len(verts)
        verts += [(ax, ay, SILL1), (bx, by, SILL1), (bx, by, HEAD0), (ax, ay, HEAD0)]
        faces.append((k, k + 1, k + 2, k + 3))
    lights = kit.mesh_object("bay-lights", verts, faces, root)
    kit.assign(lights, bay_glass)
    me = lights.data
    me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    for poly in me.polygons:
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv[li].uv = ((co.x - (BAY_X - 1.2)) / 2.4, (co.z - SILL1) / (HEAD0 - SILL1))
    glass_parts.append(lights)

    # the bay's own hipped roof: a fan of slopes from its eave up to a line on the wall
    wall_in = yw + 0.03
    Q = _offset(P, 0.16, wall_in)
    zb, ze = HEAD1 - 0.03, HEAD1 + 0.11
    Tl = [(BAY_X + (x - BAY_X) * 0.52, wall_in) for x, _ in Q]
    n = len(Q)
    v = [(x, y, ze) for x, y in Q] + [(x, y, zb) for x, y in Q] + [(x, y, BAYROOF_TOP) for x, y in Tl]
    A, B, Tt = 0, n, 2 * n
    f = []
    for i in range(n - 1):
        f.append((A + i, A + i + 1, Tt + i + 1, Tt + i))       # slopes
        f.append((B + i, B + i + 1, A + i + 1, A + i))         # fascia
    f.append(tuple(B + i for i in range(n)))                  # soffit
    # the back, against the wall, as a fan of triangles (its top edge has collinear points)
    f.append((B + 0, A + 0, Tt + 0))
    for i in range(n - 1):
        f.append((B + 0, Tt + i, Tt + i + 1))
    f.append((B + 0, Tt + n - 1, A + n - 1))
    f.append((B + 0, A + n - 1, B + n - 1))
    bay_roof = kit.mesh_object("bay-roof", v, f, root, solid=True)
    kit.assign(bay_roof, tiles)
    _roof_uv(bay_roof, roof_tile)
    roof_parts.append(bay_roof)

    # ── the door: a timber doorcase standing proud of the base course, with the painted door on it ──
    dx0, dx1 = DOOR_X - DOOR_W / 2, DOOR_X + DOOR_W / 2
    box("doorcase", (dx0, -HD - 0.1, 0), (dx1, -HD + 0.02, DOOR_H), timber)
    plaque("door", door_mat, (DOOR_X, -HD - 0.105, DOOR_H / 2), (DOOR_W, DOOR_H), "-y", body_parts)
    # its little window, lit at night with the rest of the glass
    dw_w = (DOOR_WIN[2] - DOOR_WIN[0]) / (DOOR[2] - DOOR[0]) * DOOR_W
    dw_h = (DOOR_WIN[3] - DOOR_WIN[1]) / (DOOR[3] - DOOR[1]) * DOOR_H
    dw_x = DOOR_X + (((DOOR_WIN[0] + DOOR_WIN[2]) / 2 - (DOOR[0] + DOOR[2]) / 2) / (DOOR[2] - DOOR[0])) * DOOR_W
    dw_z = DOOR_H - (((DOOR_WIN[1] + DOOR_WIN[3]) / 2 - DOOR[1]) / (DOOR[3] - DOOR[1])) * DOOR_H
    plaque("door-window", door_win, (dw_x, -HD - 0.11, dw_z), (dw_w, dw_h), "-y", glass_parts)
    box("step", (dx0 - 0.12, -HD - 0.42, 0), (dx1 + 0.12, -HD - 0.06, 0.13), stone, stone_tile)

    # the cracked patch of plaster above, its border faded into the wall swatch underneath
    ck_w = 1.2
    ck_h = ck_w * (CRACK[3] - CRACK[1]) / (CRACK[2] - CRACK[0])
    ck_x, ck_z = 2.45, 3.15
    crack_px = sheet[CRACK[1]:CRACK[3], CRACK[0]:CRACK[2]]
    crack_mat = kit.material("apothecary-crack", _blended_decal(kit, plaster_px, "apothecary-crack", crack_px, plaster_tile, plaster_off,
                                                               ck_x - ck_w / 2, ck_z - ck_h / 2, ck_w, ck_h))
    plaque("crack", crack_mat, (ck_x, -HD - 0.012, ck_z), (ck_w, ck_h), "-y", body_parts)

    # ── the iron wall lantern right of the door: a little glazed box hung from an arm ──
    lx, ly, lz = LAMP
    s = 1.15
    zc = lz + 0.195 * s           # its cap, rising to the arm
    za = zc + 0.12 * s
    box("lamp-plate", (lx - 0.06, -HD - 0.03, za - 0.15), (lx + 0.06, -HD + 0.01, za + 0.07), iron)
    box("lamp-arm", (lx - 0.02, ly - 0.02, za - 0.03), (lx + 0.02, -HD - 0.02, za + 0.01), iron)
    box("lamp-floor", (lx - 0.11 * s, ly - 0.11 * s, lz - 0.185 * s), (lx + 0.11 * s, ly + 0.11 * s, lz - 0.145 * s), iron)
    box("lamp-ceiling", (lx - 0.12 * s, ly - 0.12 * s, lz + 0.155 * s), (lx + 0.12 * s, ly + 0.12 * s, zc), iron)
    for sx in (-1, 1):
        for sy in (-1, 1):
            cx, cy = lx + sx * 0.095 * s, ly + sy * 0.095 * s
            box(f"lamp-post{sx}{sy}", (cx - 0.0125 * s, cy - 0.0125 * s, lz - 0.15 * s), (cx + 0.0125 * s, cy + 0.0125 * s, lz + 0.16 * s), iron)
    r = 0.12 * s
    cap = kit.mesh_object("lamp-cap", [(lx - r, ly - r, zc), (lx + r, ly - r, zc), (lx + r, ly + r, zc), (lx - r, ly + r, zc), (lx, ly, za)],
                          [(0, 1, 2, 3), (0, 4, 1), (1, 4, 2), (2, 4, 3), (3, 4, 0)], root, solid=True)
    part(cap, iron)
    o = 0.09 * s
    for name, facing, c in (("lamp-front", "-y", (lx, ly - o, lz)), ("lamp-back", "+y", (lx, ly + o, lz)),
                            ("lamp-left", "-x", (lx - o, ly, lz)), ("lamp-right", "+x", (lx + o, ly, lz))):
        plaque(name, lantern_glass, c, (0.17 * s, 0.29 * s), facing, glass_parts)

    # ── the blank signboard, hung off the right front corner post from an iron bracket ──
    sy = -HD
    px = HW + POST / 2
    box("sign-plate", (px, sy - 0.08, 2.76), (px + 0.03, sy + 0.08, 2.99), iron)
    box("sign-arm", (px, sy - 0.025, 2.86), (HW + 1.05, sy + 0.025, 2.91), iron)
    box("sign-hook", (HW + 1.0, sy - 0.025, 2.91), (HW + 1.05, sy + 0.025, 3.0), iron)
    bx0, bx1, bz0, bz1 = HW + 0.22, HW + 0.97, 2.13, 2.69
    for hx in (bx0 + 0.14, bx1 - 0.14):
        box(f"sign-hanger{hx:.2f}", (hx - 0.015, sy - 0.015, bz1 - 0.02), (hx + 0.015, sy + 0.015, 2.87), iron)
    box("sign-board", (bx0, sy - 0.025, bz0), (bx1, sy + 0.025, bz1), wood)
    bc = ((bx0 + bx1) / 2, (bz0 + bz1) / 2)
    plaque("sign-front", sign_mat, (bc[0], sy - 0.028, bc[1]), (bx1 - bx0, bz1 - bz0), "-y", body_parts)
    plaque("sign-back", sign_mat, (bc[0], sy + 0.028, bc[1]), (bx1 - bx0, bz1 - bz0), "+y", body_parts)

    # ── windows: high and low in each gable, one at the back; each painted window gets a real stone lintel and
    #    sill over its painted ones, so the cel light and the ink outline give it some depth ──
    e = 0.015

    def window(name, mat, along, z, size, facing):
        w, h = size
        sgn = 1 if facing[0] == "+" else -1
        if facing[1] == "x":
            plaque(name, mat, (sgn * (HW + e), along, z), size, facing, glass_parts)
            for tag, z0, z1, depth, k in (("lintel", z + h / 2 - 0.16 * h, z + h / 2 + 0.01, 0.05, 1.0),
                                          ("sill", z - h / 2 - 0.01, z - h / 2 + 0.15 * h, 0.07, 0.94)):
                xs = sorted((sgn * (HW - 0.02), sgn * (HW + depth)))
                box(f"{name}-{tag}", (xs[0], along - k * w / 2, z0), (xs[1], along + k * w / 2, z1), sill_stone)
        else:
            plaque(name, mat, (along, sgn * (HD + e), z), size, facing, glass_parts)
            for tag, z0, z1, depth, k in (("lintel", z + h / 2 - 0.16 * h, z + h / 2 + 0.01, 0.05, 1.0),
                                          ("sill", z - h / 2 - 0.01, z - h / 2 + 0.15 * h, 0.07, 0.94)):
                ys = sorted((sgn * (HD - 0.02), sgn * (HD + depth)))
                box(f"{name}-{tag}", (along - k * w / 2, ys[0], z0), (along + k * w / 2, ys[1], z1), sill_stone)

    window("w-right-high", win_high, -0.25, 5.6, (0.64, 0.98), "+x")
    window("w-right-low", win_low_r, -0.25, 1.6, (0.68, 1.09), "+x")
    window("w-left-high", win_high, 0.0, 5.6, (0.64, 0.98), "-x")
    window("w-left-low", win_low_l, 0.0, 1.6, (0.68, 1.09), "-x")
    window("w-back", win_back, 0.12, 3.1, (0.64, 0.97), "+y")

    # ── the main roof: two slabs with a bell-cast kick at the eaves and a flared verge, and a ridge roll ──
    for sgn in (-1, 1):
        top = [(0.0, RIDGE), (sgn * YK, ZK), (sgn * YE, ZE)]
        prof = top + [(y, z - T) for y, z in reversed(top)]
        slab = _xsweep(kit, f"slope{sgn}", prof, [-verge_l(y, -1) for y, _ in prof], [verge_l(y, 1) for y, _ in prof], root)
        kit.assign(slab, tiles)
        _roof_uv(slab, roof_tile)
        roof_parts.append(slab)
    ridge = kit.prism_x("ridge", [(-0.16, RIDGE - 0.27), (0.16, RIDGE - 0.27), (0.0, RIDGE + 0.1)],
                        -HW - VERGE[0] - 0.02, HW + VERGE[0] + 0.02, root)
    kit.assign(ridge, tiles)
    _roof_uv(ridge, roof_tile)
    roof_parts.append(ridge)

    # ── lean, join into the parts the game reads, markers ──
    for o in body_parts + roof_parts + glass_parts:
        kit.shear(o, kx=LEAN)
    kit.join("body", body_parts)
    kit.join("roof", roof_parts)
    kit.join("glass", glass_parts)

    kit.marker("lamp", (lx + LEAN * lz, ly, lz), root)
    kit.marker("door-point", (DOOR_X, -HD - 1.2, 0), root)
    cxm, cym = (CHIM_X0 + CHIM_X1) / 2, (CHIM_Y0 + CHIM_Y1) / 2
    kit.marker("chimney", (cxm + LEAN * CHIM_TOP, cym, CHIM_TOP), root)
    return root, CHIM_TOP
