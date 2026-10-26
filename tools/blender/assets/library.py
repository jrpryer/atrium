"""The Library: a tall, narrow grey stone house, gable to the front, with a flared blue slate roof, a dormer, a
chimney on the back gable, a timber band at the first floor, a slate-roofed porch on posts along its right side
and an arched green door. Reference: art/reference/sheets/library-sheet.png (Higgsfield job cddc4cfb).

Parts the game uses: body (walls, plinth, band, chimney, porch), roof, door, glass (every window, lit at night),
and the markers lamp and door. Front faces -Y in Blender (+Z in the game).
"""
import numpy as np

SHEET = "library-sheet.png"
PX = 0.0106  # metres per sheet pixel (wall height 6.4 m ≈ 605 px on the 2752-px sheet)

W, D = 4.6, 5.0            # walls: width (gable end) and depth
H, R = 6.4, 6.3            # eave height and roof rise
BAND_Z = 3.6               # the first-floor timber band
LEAN = 0.018               # a degree of settling to the right


def build(kit):
    sheet = kit.load_sheet(SHEET)
    stone_box = (1541, 1046, 1844, 1238)
    plinth_box = (2202, 1287, 2656, 1369)
    slate_box = (2174, 206, 2614, 716)
    stone = kit.material("library-stone", kit.swatch(sheet, "library-stone", *stone_box))
    plinth = kit.material("library-plinth", kit.swatch(sheet, "library-plinth", *plinth_box))
    slate = kit.material("library-slate", kit.swatch(sheet, "library-slate", *slate_box))
    timber = kit.material("library-timber", color=kit.srgb_to_linear((0.43, 0.36, 0.29)))
    door_tex = kit.material("library-door", kit.decal(sheet, "library-door", 383, 1094, 526, 1380))
    win_tall = kit.material("library-window", kit.decal(sheet, "library-window", 325, 743, 383, 908))
    win_wide = kit.material("library-window-wide", kit.decal(sheet, "library-window-wide", 2370, 1087, 2463, 1225))
    win_dormer = kit.material("library-window-dormer", kit.decal(sheet, "library-window-dormer", 1025, 475, 1139, 647))
    stone_tile = kit.sheet_tile(*stone_box, PX)
    plinth_tile = kit.sheet_tile(*plinth_box, PX)
    slate_tile = kit.sheet_tile(*slate_box, PX)

    root = kit.root("library")

    # ── body: a gable-ended prism (walls run up into the front and back gables) ──
    hw, hd = W / 2, D / 2
    # the gables follow the underside of the flared roof, so no wall pokes through it
    body = kit.prism("walls", [(-hw, 0), (hw, 0), (hw, H), (hw - 0.5, H + 0.38), (0, H + R - 0.22),
                               (-(hw - 0.5), H + 0.38), (-hw, H)], -hd, hd, root)
    kit.assign(body, stone)
    kit.box_uv(body, stone_tile)

    base = kit.box("plinth", (0, 0, 0.55), (W + 0.18, D + 0.18, 1.1), root)
    kit.assign(base, plinth)
    kit.box_uv(base, plinth_tile)

    band = kit.box("band", (0, 0, BAND_Z), (W + 0.14, D + 0.14, 0.22), root)
    kit.assign(band, timber)
    kit.box_uv(band, 1.0)

    chimney_top = H + R + 0.6
    chimney = kit.box("chimney", (-1.25, hd - 0.45, (chimney_top + H) / 2), (0.75, 0.7, chimney_top - H), root)
    kit.assign(chimney, stone)
    kit.box_uv(chimney, stone_tile)
    pots = [kit.cylinder(f"pot{i}", (-1.25 + dx, hd - 0.45, chimney_top), 0.14, 0.11, 0.45, 8, root) for i, dx in enumerate((-0.16, 0.18))]
    for p in pots:
        kit.assign(p, plinth)
        kit.box_uv(p, plinth_tile)

    # ── porch on posts along the right side ──
    porch_parts = []
    for y in (-1.3, 1.0):
        post = kit.box(f"post{y}", (hw + 1.05, y, 1.5), (0.16, 0.16, 3.0), root)
        kit.assign(post, timber)
        kit.box_uv(post, 1.0)
        porch_parts.append(post)
    beam = kit.box("porch-beam", (hw + 1.05, -0.15, 3.0), (0.18, 2.7, 0.18), root)
    kit.assign(beam, timber)
    kit.box_uv(beam, 1.0)
    porch_parts.append(beam)

    body_parts = [body, base, band, chimney, *pots, *porch_parts]
    for o in body_parts:
        kit.shear(o, kx=LEAN)
    body = kit.join("body", body_parts)

    # ── roof: two slopes with a flared eave (the swept look on the sheet) ──
    def slope(sign):
        t = 0.2
        top = [(0.0, H + R), (sign * (hw - 0.5), H + 0.6), (sign * (hw + 0.5), H - 0.15)]
        bottom = [(x, z - t) for x, z in reversed(top)]
        outline = top + bottom if sign > 0 else list(reversed(top + bottom))
        return kit.prism(f"slope{sign}", outline, -hd - 0.35, hd + 0.35, root)

    slopes = [slope(1), slope(-1)]
    porch_roof = kit.prism("porch-roof", [(hw - 0.05, 3.55), (hw + 1.35, 2.95), (hw + 1.35, 2.82), (hw - 0.05, 3.42)], -1.55, 1.25, root)

    # the dormer on the right slope: its gable faces +X, its back is buried in the roof
    dormer = kit.prism_x("dormer", [(-0.85, 8.3), (0.35, 8.3), (0.35, 9.7), (-0.25, 10.35), (-0.85, 9.7)], 0.35, 1.3, root)
    kit.assign(dormer, stone)
    kit.box_uv(dormer, stone_tile)
    dormer_roof = kit.prism_x("dormer-roof", [(-0.95, 9.62), (-0.25, 10.55), (0.45, 9.62), (0.45, 9.46), (-0.25, 10.39), (-0.95, 9.46)], 0.3, 1.45, root)

    roof_parts = [*slopes, porch_roof, dormer_roof]
    for o in roof_parts:
        kit.assign(o, slate)
        kit.slope_uv(o, slate_tile)
    for o in roof_parts + [dormer]:
        kit.shear(o, kx=LEAN)
    roof = kit.join("roof", roof_parts)
    body = kit.join("body", [body, dormer])

    # ── decals: the door, and every window as one "glass" part the game can light ──
    def plaque(name, mat, centre, size, facing):
        o = kit.plaque(name, mat, centre, size, facing, root)
        kit.shear(o, kx=LEAN)
        return o

    e = 0.015
    door = plaque("door", door_tex, (0, -hd - e, 1.5), (1.5, 3.0), "-y")
    windows = [
        plaque("w-front-l", win_tall, (-0.95, -hd - e, 4.9), (0.62, 1.75), "-y"),
        plaque("w-front-r", win_tall, (0.95, -hd - e, 4.9), (0.62, 1.75), "-y"),
        plaque("w-front-gable", win_tall, (0, -hd - e, 8.0), (0.62, 1.75), "-y"),
        plaque("w-back-gable", win_tall, (0.25, hd + e, 8.0), (0.62, 1.75), "+y"),
        plaque("w-left-1", win_tall, (-hw - e, -1.2, 4.9), (0.62, 1.75), "-x"),
        plaque("w-left-2", win_tall, (-hw - e, 1.2, 4.9), (0.62, 1.75), "-x"),
        plaque("w-left-ground", win_wide, (-hw - e, 0.6, 2.1), (1.0, 1.45), "-x"),
        plaque("w-right-ground", win_tall, (hw + e, -0.15, 2.1), (0.62, 1.6), "+x"),
        plaque("w-dormer", win_dormer, (1.3 + e, -0.25, 9.0), (0.75, 1.05), "+x"),
    ]
    kit.join("glass", windows)

    kit.marker("lamp", (0.95, -hd - 0.3, 2.7), root)
    kit.marker("door-point", (0, -hd - 1.2, 0), root)
    return root, H + R + 2.1
