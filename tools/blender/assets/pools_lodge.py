"""RETIRED (2026-10-26): the Spirit Pools lodge, replaced by the cave mouth in pools.py at the designer's request.
The Spirit Pools lodge (the Stillkeepers' lodge, where the druids who tend the pools work): long, low and
porched. Weathered honey-brown horizontal planks between corner posts, on a low mossy stone footing; a thick teal
reed thatch with soft rounded lips and a lumpy ridge, gabled at the ends (a plank gable under a thatch hood whose
verges flare out at the eaves, so from the front it reads hipped); a deep porch along the whole front on four posts
with knee braces, a raised plank floor and stone pads; a plank door with a lintel between two shuttered windows;
a bench in front of the porch, a step at its left end and an iron-hooped rain barrel at the right front corner.
Reference: art/reference/sheets/pools-sheet.png (Higgsfield job c58861ee): a 2 x 2 grid of front (top-left),
left side (top-right), front with the left window's shutters closed (bottom-left) and back (bottom-right).

Parts the game uses: body (walls, footing, timber, porch, bench, step, barrel, door, lantern frame), roof (the
thatch), glass (every window pane and the lantern pane, lit at night), shutters (closed, shown while the lodge is
locked), shutters-open (the leaves folded back, shown once it is unlocked), and the markers lamp and door-point.
Front faces -Y in Blender (+Z in the game).

Scale: the envelope follows the brief (about 8.2 x 4.6 m, eaves about 3 m, ridge about 5.3 m), a little larger than
the sheet's own proportions so a 1.95 m door fits under the porch; swatches use each view's own metres per pixel.
"""
import math
import os

import numpy as np

SHEET = "pools-sheet.png"
PX_FRONT = 0.0080  # front view: 1025 px of wall over 8.2 m (the envelope; the door is drawn smaller than 1.95 m)
PX_SIDE = 0.0077   # side view: 600 px of wall over 4.6 m
PX_BACK = 0.0090   # back view: 915 px of wall over 8.2 m

L, D = 8.2, 4.6              # main block: length (front) and depth
Y_F, Y_B = -1.6, 3.0         # front and back wall faces (the porch sits in front of Y_F)
Y_R = (Y_F + Y_B) / 2        # ridge line
FOOT = 0.40                  # footing top = porch floor top
EAVE = 3.12                  # thatch underside at the wall line
POST_Y = -3.1                # porch posts (centre line)
POSTS_X = (-3.95, -1.38, 1.38, 3.95)
LEAN = 0.018                 # a degree of settling to the right

# the thatch cross-section, front lip to back lip: (y, underside z, top z)
PROFILE = [
    (-3.60, 2.70, 3.28),
    (-2.60, 2.91, 3.55),
    (Y_F, 3.12, 3.86),
    (-0.90, 3.63, 4.37),
    (-0.20, 4.14, 4.88),
    (0.15, 4.40, 5.12),
    (0.45, 4.62, 5.30),
    (Y_R, 4.80, 5.38),
    (0.95, 4.62, 5.30),
    (1.25, 4.40, 5.12),
    (1.60, 4.14, 4.88),
    (2.30, 3.63, 4.37),
    (Y_B, 3.12, 3.86),
    (3.55, 2.72, 3.30),
]
LIP_F = (-3.80, 3.00)  # the rounded front lip bulges out between underside and top
LIP_B = (3.75, 3.01)
NX = 20                # thatch rows along the ridge


def smoothstep(e0, e1, x):
    t = min(max((x - e0) / (e1 - e0), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def eyebrow(x):
    """The thatch lip lifts over the centre bay, in front of the door (the sheet's eyebrow)."""
    return 0.30 * smoothstep(1.4, 0.55, abs(x))


def x_end_bottom(z):
    """How far the underside reaches past the gable wall: more at the eaves (the verge flares out)."""
    return L / 2 + 0.27 + max(0.0, 4.80 - z) * 0.22


def x_end_top(z):
    """How far the top surface reaches: straight up from the eaves, then curving in to the ridge (a rounded
    shoulder from the front, a hood over the gable apex from the side)."""
    t = min(max((z - 3.25) / 2.13, 0.0), 1.0)
    return L / 2 + 0.75 - 1.6 * t ** 1.6


def lip_wave(x, k):
    return 0.05 * math.sin(2.3 * x + k) + 0.03 * math.sin(5.1 * x + 0.5 * k)


def lump(x, k, amp):
    return amp * (math.sin(1.1 * x + 1.3 * k) * 0.7 + math.sin(2.3 * x - 0.7 * k + 1.1) * 0.3)


def build(kit):
    sheet = kit.load_sheet(SHEET)

    # ── swatches (tiling) and decals, in sheet pixels from the top-left ──
    planks_box = (1880, 1135, 2250, 1233)   # back wall, between the windows, cut at plank centres
    gable_box = (1880, 265, 2025, 370)      # side gable: vertical boards, cut at board centres
    stone_box = (1700, 1316, 2400, 1362)    # back footing: slabs with moss along the bottom
    thatch_box = (1690, 942, 2420, 1010)    # a wide band of the back roof, below the ink scribbles, above the lip
    planks = kit.material("pools-planks", kit.swatch(sheet, "pools-planks", *planks_box))
    gable = kit.material("pools-gable", kit.swatch(sheet, "pools-gable", *gable_box))
    stone = kit.material("pools-stone", kit.swatch(sheet, "pools-stone", *stone_box))
    thatch = kit.material("pools-thatch", xfade_swatch(kit, sheet, "pools-thatch", *thatch_box, overlap=0.3))
    planks_tile = kit.sheet_tile(*planks_box, PX_BACK)
    gable_tile = kit.sheet_tile(*gable_box, PX_SIDE)
    stone_tile = kit.sheet_tile(*stone_box, PX_BACK)
    tt = kit.sheet_tile(*thatch_box, PX_BACK)
    # cross-faded (not mirrored) across, so 70% of the box per repeat; stretched down the slope, which the back
    # view sees foreshortened, so the brush streaks run long from ridge to lip
    thatch_tile = (tt[0] / 2 * 0.7, tt[1] * 3.0)

    lip = kit.material("pools-thatch-lip", color=kit.sample(sheet, 1950, 1060, 6))
    fringe_box = (1700, 1022, 2400, 1082)   # the back lip: tufts over a dark band, above its bottom ink line
    fringe = kit.material("pools-thatch-fringe", xfade_swatch(kit, sheet, "pools-thatch-fringe", *fringe_box, overlap=0.3, vmirror=False))
    fringe_tile = (fringe_box[2] - fringe_box[0]) * 0.7 * PX_BACK
    timber = kit.material("pools-timber", color=kit.srgb_to_linear((0.49, 0.40, 0.30)))
    iron = kit.material("pools-iron", color=kit.srgb_to_linear((0.24, 0.23, 0.22)))
    barrel_top = kit.material("pools-barrel-top", color=kit.srgb_to_linear((0.40, 0.31, 0.23)))
    lantern_glass = kit.material("pools-lantern-glass", color=kit.srgb_to_linear((0.86, 0.74, 0.48)))

    door_tex = kit.material("pools-door", kit.decal(sheet, "pools-door", 670, 459, 813, 653))
    win_fl = kit.material("pools-window-front-l", kit.decal(sheet, "pools-window-front-l", 402, 463, 490, 570))
    win_fr = kit.material("pools-window-front-r", kit.decal(sheet, "pools-window-front-r", 995, 462, 1081, 564))
    win_side = kit.material("pools-window-side", kit.decal(sheet, "pools-window-side", 1902, 474, 1996, 581))
    win_back = kit.material("pools-window-back", kit.decal(sheet, "pools-window-back", 1781, 1113, 1849, 1212))
    shut_closed = kit.material("pools-shutter-closed", kit.decal(sheet, "pools-shutter-closed", 398, 1081, 503, 1189))
    shut_leaf = kit.material("pools-shutter-leaf", kit.decal(sheet, "pools-shutter-leaf", 1840, 473, 1900, 584))
    barrel_side = kit.material("pools-barrel", hswatch(kit, sheet, "pools-barrel", 1272, 590, 1330, 720))

    root = kit.root("pools")
    body_parts = []

    def part(o, mat, tile=1.0):
        kit.assign(o, mat)
        kit.box_uv(o, tile)
        body_parts.append(o)
        return o

    def box(name, centre, size, mat, tile=1.0):
        return part(kit.box(name, centre, size, root), mat, tile)

    def strut(name, p0, p1, w, mat):
        return part(beam(kit, name, p0, p1, w, root), mat)

    # ── the main block: footing, plank walls, corner posts, wall plates, plank gables ──
    hl = L / 2
    footing = box("footing", (0, (Y_F + Y_B) / 2, 0.21), (L + 0.14, D + 0.14, 0.42), stone, stone_tile)
    shift_uv(footing, 0, 0.5)  # moss at the bottom
    box("walls", (0, (Y_F + Y_B) / 2, (FOOT + EAVE + 0.03) / 2), (L, D, EAVE + 0.03 - FOOT), planks, planks_tile)
    for sx in (-1, 1):
        for y in (Y_F + 0.08, Y_B - 0.08):
            box(f"corner{sx}{y:.1f}", (sx * (hl - 0.08), y, (FOOT + EAVE) / 2), (0.30, 0.30, EAVE - FOOT), timber)
        box(f"plate-end{sx}", (sx * (hl + 0.04), (Y_F + Y_B) / 2, 3.01), (0.16, D + 0.3, 0.18), timber)
        # the gable's top edges run up into the thatch, so no sliver of sky shows between them
        xa, xb = sx * (hl - 0.2), sx * (hl - 0.02)  # a board-thick slab just inside the wall line
        g = kit.prism_x(f"gable{sx}", [(Y_F - 0.05, 3.0), (Y_B + 0.05, 3.0), (Y_B + 0.05, 3.2), (Y_R, 4.85), (Y_F - 0.05, 3.2)],
                        min(xa, xb), max(xa, xb), root)
        part(g, gable, tile=gable_tile)
    box("plate-front", (0, Y_F - 0.06, 3.01), (L, 0.16, 0.18), timber)

    # ── the porch: stone base, plank floor, pads, posts, beams, knee braces, rafters ──
    box("porch-base", (0, (POST_Y - 0.02 + Y_F) / 2, 0.15), (L, Y_F - POST_Y + 0.02, 0.30), stone, stone_tile)
    shift_uv(body_parts[-1], 0, 0.5)
    box("porch-floor", (0, (-3.28 + Y_F) / 2, 0.35), (L + 0.16, Y_F + 3.28, 0.10), planks, planks_tile)
    beam_lo = 2.60  # underside of the outer beams; the centre one arches up under the eyebrow
    for x in POSTS_X:
        box(f"pad{x}", (x, POST_Y, 0.16), (0.44, 0.44, 0.32), stone, stone_tile)
        top = beam_lo + 0.21
        box(f"post{x}", (x, POST_Y, (0.30 + top) / 2), (0.22, 0.22, top - 0.30), timber)
        # a rafter from the post head back to the wall plate, under the porch thatch
        strut(f"rafter{x}", (x, POST_Y, 2.70), (x, Y_F - 0.05, 3.02), 0.15, timber)
    arch = [(-1.5, 2.70), (-0.55, 2.70 + 0.65 * eyebrow(0)), (0.55, 2.70 + 0.65 * eyebrow(0)), (1.5, 2.70)]
    for a, b in zip(arch, arch[1:]):
        strut(f"beam-arch{a[0]:.1f}", (a[0], POST_Y, a[1]), (b[0], POST_Y, b[1]), 0.21, timber)
    for sx in (-1, 1):
        box(f"beam{sx}", (sx * 2.77, POST_Y, beam_lo + 0.105), (2.86, 0.2, 0.21), timber)
        # knee braces: corner posts brace inward, middle posts both ways
        strut(f"brace-corner{sx}", (sx * 3.95, POST_Y, beam_lo - 0.55), (sx * 3.4, POST_Y, beam_lo + 0.03), 0.11, timber)
        strut(f"brace-mid-out{sx}", (sx * 1.38, POST_Y, beam_lo - 0.55), (sx * 1.93, POST_Y, beam_lo + 0.03), 0.11, timber)
        strut(f"brace-mid-in{sx}", (sx * 1.38, POST_Y, 2.15), (sx * 0.9, POST_Y, 2.72), 0.11, timber)

    # ── the door, with a lintel ──
    door = kit.plaque("door", door_tex, (0, Y_F - 0.012, FOOT + 0.975), (1.40, 1.95), "-y", root)
    body_parts.append(door)
    box("door-lintel", (0, Y_F - 0.06, 2.43), (1.66, 0.12, 0.16), timber)

    # ── windows: trims in body, panes in glass, shutters closed and open ──
    glass, closed, opened = [], [], []

    def window(tag, facing, c, mat, w, h, top, shutters):
        z0 = top - h
        zc = z0 + h / 2
        box(f"{tag}-lintel", wall_point(facing, c, 0.04, top + 0.07), wall_size(facing, w + 0.30, 0.10, 0.14), timber)
        box(f"{tag}-sill", wall_point(facing, c, 0.05, z0 - 0.05), wall_size(facing, w + 0.32, 0.12, 0.10), timber)
        for s in (-1, 1):
            box(f"{tag}-jamb{s}", wall_point(facing, c + s * (w / 2 + 0.04), 0.025, zc), wall_size(facing, 0.08, 0.07, h), timber)
        pane = kit.plaque(f"{tag}-glass", mat, wall_point(facing, c, 0.012, zc), (w, h), facing, root)
        glass.append(pane)
        if not shutters:
            return
        sh = kit.box(f"{tag}-shut", wall_point(facing, c, 0.08, zc), wall_size(facing, w + 0.16, 0.035, h - 0.02), root)
        kit.assign(sh, shut_closed)
        kit.fit_uv(sh)
        closed.append(sh)
        right = WALLS[facing][1][0] + WALLS[facing][1][1]  # +1 if the viewer's right is the world's + direction
        for s in (-1, 1):  # the viewer's left leaf as painted, the right one mirrored
            leaf = kit.box(f"{tag}-leaf{s}", wall_point(facing, c + right * s * (w / 2 + 0.33), 0.022, zc),
                           wall_size(facing, 0.48, 0.03, h - 0.02), root)
            kit.assign(leaf, shut_leaf)
            kit.fit_uv(leaf)
            if s > 0:
                flip_u(leaf)
            opened.append(leaf)

    top = FOOT + 1.95  # window heads line up with the door head
    window("w-front-l", "-y", -2.5, win_fl, 0.80, 0.95, top, True)
    window("w-front-r", "-y", 2.5, win_fr, 0.80, 0.95, top, True)
    window("w-left", "-x", Y_R, win_side, 0.82, 0.95, top, True)
    window("w-right", "+x", Y_R, win_side, 0.82, 0.95, top, True)
    window("w-back-l", "+y", -2.25, win_back, 0.70, 1.05, top, False)
    window("w-back-r", "+y", 2.25, win_back, 0.70, 1.05, top, False)

    # ── an iron wall lantern beside the door, under the porch roof ──
    lx, ly, lz = 1.05, Y_F - 0.30, 2.58
    g, gh = 0.20, 0.27  # the pane box: width and height
    box("lantern-arm", (lx, Y_F - 0.17, 2.885), (0.05, 0.34, 0.05), iron)
    box("lantern-hanger", (lx, ly, 2.84), (0.03, 0.03, 0.06), iron)
    box("lantern-base", (lx, ly, lz - gh / 2 - 0.015), (g + 0.06, g + 0.06, 0.03), iron)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box(f"lantern-corner{sx}{sy}", (lx + sx * g / 2, ly + sy * g / 2, lz), (0.03, 0.03, gh), iron)
    z0, z1, r0, r1 = lz + gh / 2, lz + gh / 2 + 0.1, 0.16, 0.04
    cap = kit.mesh_object("lantern-cap", [(lx - r0, ly - r0, z0), (lx + r0, ly - r0, z0), (lx + r0, ly + r0, z0), (lx - r0, ly + r0, z0),
                                          (lx - r1, ly - r1, z1), (lx + r1, ly - r1, z1), (lx + r1, ly + r1, z1), (lx - r1, ly + r1, z1)],
                          [(0, 1, 2, 3), (4, 5, 6, 7), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)], root, solid=True)
    part(cap, iron)
    pane = kit.box("lantern-glass", (lx, ly, lz), (g - 0.01, g - 0.01, gh), root)
    kit.assign(pane, lantern_glass)
    kit.box_uv(pane, 1.0)
    glass.append(pane)

    # ── in front: a bench, a step at the left end, the rain barrel at the right front corner ──
    box("bench-seat", (0, -3.88, 0.415), (1.8, 0.32, 0.07), timber)
    for sx in (-1, 1):
        box(f"bench-leg{sx}", (sx * 0.68, -3.88, 0.19), (0.08, 0.26, 0.38), timber)
    box("step", (-3.3, -3.46, 0.10), (0.75, 0.36, 0.20), timber)
    body_parts.append(barrel(kit, "barrel", (4.55, -2.7), 0.95, 0.28, 0.335, barrel_side, barrel_top, root))

    for o in body_parts:
        kit.shear(o, kx=LEAN)
    kit.join("body", body_parts)

    # ── the thatch ──
    roof = thatch_shell(kit, root, thatch, lip, fringe, thatch_tile, fringe_tile)
    kit.shear(roof, kx=LEAN)

    for o in glass + closed + opened:
        kit.shear(o, kx=LEAN)
    kit.join("glass", glass)
    kit.join("shutters", closed)
    kit.join("shutters-open", opened)

    kit.marker("lamp", (lx + LEAN * lz, ly, lz), root)
    kit.marker("door-point", (LEAN * FOOT, Y_F - 1.2, FOOT), root)
    return root, max(p[2] for p in PROFILE) + 0.1


# ───────────────────────── helpers ─────────────────────────

# each wall as a frame: where it is, which way is the viewer's right, which way is out
WALLS = {
    "-y": ((0, Y_F, 0), (1, 0, 0), (0, -1, 0)),
    "+y": ((0, Y_B, 0), (-1, 0, 0), (0, 1, 0)),
    "+x": ((L / 2, 0, 0), (0, 1, 0), (1, 0, 0)),
    "-x": ((-L / 2, 0, 0), (0, -1, 0), (-1, 0, 0)),
}


def wall_point(facing, c, out, z):
    """A point on a wall: `c` is the world x (front, back) or y (sides) along the wall, `out` the distance out
    from its face."""
    origin, along, normal = WALLS[facing]
    a = c * (along[0] + along[1])  # the world coordinate, signed into the wall's own left-to-right
    return tuple(origin[i] + along[i] * a + normal[i] * out + (z if i == 2 else 0) for i in range(3))


def wall_size(facing, across, depth, height):
    return (across, depth, height) if facing in ("-y", "+y") else (depth, across, height)


def flip_u(obj):
    for l in obj.data.uv_layers.active.data:
        l.uv.x = 1 - l.uv.x


def shift_uv(obj, du, dv):
    for l in obj.data.uv_layers.active.data:
        l.uv.x += du
        l.uv.y += dv


def hswatch(kit, sheet, name, x0, y0, x1, y1):
    """A swatch mirrored only left-to-right, for things that wrap around but must not flip upside down (barrel
    staves and hoops)."""
    region = sheet[y0:y1, x0:x1].copy()
    region[..., 3] = 1.0
    region = np.concatenate([region, region[:, ::-1]], axis=1)
    h, w = region.shape[:2]
    img = kit.bpy.data.images.new(name, width=w, height=h, alpha=False)
    img.pixels.foreach_set(region[::-1].astype(np.float32).ravel())
    os.makedirs(kit.TEXTURES, exist_ok=True)
    img.filepath_raw = os.path.join(kit.TEXTURES, name + ".png")
    img.file_format = "PNG"
    img.save()
    return img


def xfade_swatch(kit, sheet, name, x0, y0, x1, y1, overlap=0.3, vmirror=True):
    """A swatch that tiles left-to-right by cross-fading its two ends (no mirror, so no butterfly patterns in
    blotchy paint like thatch), mirrored top-to-bottom only."""
    region = sheet[y0:y1, x0:x1].copy()
    region[..., 3] = 1.0
    w = region.shape[1]
    ov = int(w * overlap)
    t = np.linspace(0, 1, ov, dtype=np.float32)[None, :, None]
    body = region[:, ov:w - ov]
    seam = region[:, w - ov:] * (1 - t) + region[:, :ov] * t
    tile = np.concatenate([seam[:, ov // 2:], body, seam[:, :ov // 2]], axis=1)  # seam split across the edge
    # the two halves of the blended seam meet at the image edge, so the texture wraps without a join
    if vmirror:
        tile = np.concatenate([tile, tile[::-1]], axis=0)
    return save_image(kit, name, tile)


def save_image(kit, name, arr):
    h, w = arr.shape[:2]
    img = kit.bpy.data.images.new(name, width=w, height=h, alpha=False)
    img.pixels.foreach_set(arr[::-1].astype(np.float32).ravel())
    os.makedirs(kit.TEXTURES, exist_ok=True)
    img.filepath_raw = os.path.join(kit.TEXTURES, name + ".png")
    img.file_format = "PNG"
    img.save()
    return img


def beam(kit, name, p0, p1, w, parent):
    """A square-section timber from p0 to p1 (braces, rafters)."""
    from mathutils import Vector
    a, b = Vector(p0), Vector(p1)
    d = (b - a).normalized()
    side = d.cross(Vector((0, 1, 0)))
    if side.length < 1e-3:
        side = d.cross(Vector((1, 0, 0)))
    side.normalize()
    up = side.cross(d).normalized()
    h = w / 2
    verts = []
    for p in (a, b):
        for s, u in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            verts.append(tuple(p + side * (s * h) + up * (u * h)))
    faces = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
    return kit.mesh_object(name, verts, faces, parent, solid=True)


def set_uvs(obj, face_uvs):
    """Per-face UVs given as {vertex index: (u, v)} for each face, in face order."""
    me = obj.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    for poly, m in zip(me.polygons, face_uvs):
        for li in poly.loop_indices:
            uv[li].uv = m[me.loops[li].vertex_index]


def barrel(kit, name, base, height, r_end, r_belly, side_mat, top_mat, parent, segments=14, repeats=3):
    """A bulging barrel: staves and hoops from the sheet wrapped round its side, plain wood on top."""
    bx, by = base
    rings = [0.0, 0.08, 0.25, 0.5, 0.75, 0.92, 1.0]
    verts, faces, face_uvs, mats = [], [], [], []
    for t in rings:
        r = r_end + (r_belly - r_end) * math.sin(math.pi * t)
        for i in range(segments):
            a = 2 * math.pi * i / segments
            verts.append((bx + r * math.cos(a), by + r * math.sin(a), t * height))
    for j in range(len(rings) - 1):
        for i in range(segments):
            i2 = (i + 1) % segments
            f = (j * segments + i, j * segments + i2, (j + 1) * segments + i2, (j + 1) * segments + i)
            faces.append(f)
            u0, u1 = i / segments * repeats, (i + 1) / segments * repeats
            face_uvs.append({f[0]: (u0, rings[j]), f[1]: (u1, rings[j]), f[2]: (u1, rings[j + 1]), f[3]: (u0, rings[j + 1])})
            mats.append(0)
    n = len(rings) - 1
    for ring, z in ((0, 0), (n, 1)):
        f = tuple(ring * segments + i for i in range(segments))
        faces.append(f)
        face_uvs.append({v: (0.5 + 0.5 * math.cos(2 * math.pi * (v % segments) / segments),
                             0.5 + 0.5 * math.sin(2 * math.pi * (v % segments) / segments)) for v in f})
        mats.append(1)
    o = kit.mesh_object(name, verts, faces, parent, solid=True)
    o.data.materials.append(side_mat)
    o.data.materials.append(top_mat)
    for p, m in zip(o.data.polygons, mats):
        p.material_index = m
    set_uvs(o, face_uvs)
    kit.smooth(o)
    return o


def thatch_shell(kit, root, thatch, lip, fringe, tile, fringe_tile):
    """The thatch as one closed shell lofted along the ridge: a cross-section (top and underside, with rounded
    lips) repeated row by row, its ends drawn in near the top (a hood over the gable apex) and flared out at the
    eaves, a lip that lifts over the door, and a lumpy, uneven top."""
    tu, tv = tile
    n = len(PROFILE)
    per = 2 * n + 2
    verts, faces, face_uvs, mats = [], [], [], []

    # arc length along the top, measured from the nearer lip (so both slopes read lip-to-ridge)
    s = [0.0]
    for k in range(1, n):
        y0, _, z0 = PROFILE[k - 1]
        y1, _, z1 = PROFILE[k]
        s.append(s[-1] + math.hypot(y1 - y0, z1 - z0))
    s_lip = [min(v, s[-1] - v) for v in s]

    def T(i, k):
        return i * per + k

    def B(i, k):
        return i * per + n + k

    def LF(i):
        return i * per + 2 * n

    def LB(i):
        return i * per + 2 * n + 1

    for i in range(NX + 1):
        u = -1 + 2 * i / NX
        xr = u * L / 2
        e = eyebrow(xr)
        row_t, row_b = [], []
        for k, (y, zb, zt) in enumerate(PROFILE):
            lift = e if k == 0 else (0.3 * e if k == 1 else 0.0)
            ridge = 1.8 if 5 <= k <= 9 else 1.0  # the ridge is lumpier than the slopes
            bump = 0.0 if i in (0, NX) else lump(xr, k, 0.022 * ridge)
            if k in (0, n - 1):  # the lips sag and swell a little along their length
                bump = lip_wave(xr, k)
            zt2 = zt + lift + bump
            if i in (0, NX):  # round the end over a little
                zt2 -= 0.05 if k in (0, n - 1) else 0.09
            zb2 = zb + lift + (bump if k in (0, n - 1) else 0.0)
            row_t.append((u * x_end_top(zt2), y, zt2))
            row_b.append((u * x_end_bottom(zb2), y, zb2))
        verts += row_t + row_b
        zf = LIP_F[1] + e + lip_wave(xr, 0)
        zbk = LIP_B[1] + lip_wave(xr, n - 1)
        verts.append((u * (x_end_bottom(zf) + 0.04), LIP_F[0], zf))
        verts.append((u * (x_end_bottom(zbk) + 0.04), LIP_B[0], zbk))

    def vx(v):
        return verts[v][0]

    def add(f, mat, uvf):
        faces.append(f)
        mats.append(mat)
        face_uvs.append({v: uvf(v) for v in f})

    def fringe_uv(v):  # the lip band runs once from the underside (v=0) up to the top surface (v=1)
        level = v % per
        return (vx(v) / fringe_tile, 0.03 if n <= level < 2 * n else (0.5 if level >= 2 * n else 0.97))

    for i in range(NX):
        j = i + 1
        for k in range(n - 1):
            add((T(i, k), T(j, k), T(j, k + 1), T(i, k + 1)), 0, lambda v: (vx(v) / tu, 0.5 + s_lip[v % per] / tv))
            add((B(i, k), B(i, k + 1), B(j, k + 1), B(j, k)), 1, lambda v: (vx(v) / tu, verts[v][1] / tu))
        # the lips: the painted fringe band (dark below, tufts above)
        add((B(i, 0), B(j, 0), LF(j), LF(i)), 2, fringe_uv)
        add((LF(i), LF(j), T(j, 0), T(i, 0)), 2, fringe_uv)
        add((T(i, n - 1), T(j, n - 1), LB(j), LB(i)), 2, fringe_uv)
        add((LB(i), LB(j), B(j, n - 1), B(i, n - 1)), 2, fringe_uv)
    # the gable ends: thatch from top to underside, plus the lip corners
    for i in (0, NX):
        for k in range(n - 1):
            add((T(i, k), T(i, k + 1), B(i, k + 1), B(i, k)), 0, lambda v: (verts[v][1] / tu, 0.5 + verts[v][2] / tv))
        add((B(i, 0), LF(i), T(i, 0)), 0, lambda v: (verts[v][1] / tu, 0.5 + verts[v][2] / tv))
        add((T(i, n - 1), LB(i), B(i, n - 1)), 0, lambda v: (verts[v][1] / tu, 0.5 + verts[v][2] / tv))

    o = kit.mesh_object("roof", verts, faces, root, solid=True)
    o.data.materials.append(thatch)
    o.data.materials.append(lip)
    o.data.materials.append(fringe)
    for p, m in zip(o.data.polygons, mats):
        p.material_index = m
    set_uvs(o, face_uvs)
    kit.smooth(o)
    return o
