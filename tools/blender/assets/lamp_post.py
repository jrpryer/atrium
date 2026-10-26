"""An old iron lamp post: a low stepped stone plinth, a fluted tapering base, two collars, a slender post, a cup
that carries a square glass lantern, and a peaked cap with a ball finial; verdigris at the joints.
Reference: art/reference/sheets/lamp-post-sheet.png (Higgsfield job 3fc9621f).

Parts the game uses: post (iron and stone), glass (the four panes, lit at night) and the marker light (where the
flame sits). Measured off the sheet at about 3.3 mm per pixel; total height about 3.1 m.
"""
import math

SHEET = "lamp-post-sheet.png"
FLUTES = 16


def build(kit):
    sheet = kit.load_sheet(SHEET)
    iron = kit.material("lamp-iron", color=kit.sample(sheet, 399, 688, 8))
    verdigris = kit.material("lamp-verdigris", color=kit.srgb_to_linear((0.31, 0.54, 0.49)))
    pane = kit.material("lamp-glass", kit.decal(sheet, "lamp-glass", 341, 217, 461, 369))
    lib = kit.load_sheet("library-sheet.png")
    stone_box = (2202, 1287, 2656, 1369)
    stone = kit.material("lamp-stone", kit.swatch(lib, "lamp-stone", *stone_box))

    root = kit.root("lamp_post")
    iron_parts, green_parts, stone_parts = [], [], []

    # a low stepped stone plinth
    stone_parts.append(kit.box("plinth-1", (0, 0, 0.07), (0.52, 0.52, 0.14), root))
    stone_parts.append(kit.box("plinth-2", (0, 0, 0.19), (0.38, 0.38, 0.1), root))
    z = 0.24

    # the fluted base: a tapering star-section column (alternate vertices pulled in make the flutes)
    def fluted(name, z0, height, r0, r1, depth):
        verts, faces = [], []
        n = FLUTES * 2
        for i in range(n):
            a = 2 * math.pi * i / n
            k = 1 - depth if i % 2 else 1
            verts.append((r0 * k * math.cos(a), r0 * k * math.sin(a), z0))
            verts.append((r1 * k * math.cos(a), r1 * k * math.sin(a), z0 + height))
        for i in range(n):
            j = (i + 1) % n
            faces.append((2 * i, 2 * j, 2 * j + 1, 2 * i + 1))
        faces.append(tuple(2 * i for i in reversed(range(n))))
        faces.append(tuple(2 * i + 1 for i in range(n)))
        return kit.mesh_object(name, verts, faces, root, solid=True)

    iron_parts.append(fluted("base", z, 0.82, 0.115, 0.095, 0.12))
    z += 0.82
    green_parts.append(kit.cylinder("collar-low", (0, 0, z), 0.125, 0.125, 0.07, 16, root))
    z += 0.07
    iron_parts.append(fluted("shaft", z, 1.17, 0.06, 0.052, 0.1))
    z += 1.17
    green_parts.append(kit.cylinder("collar-high", (0, 0, z), 0.085, 0.085, 0.07, 16, root))
    z += 0.07
    iron_parts.append(kit.cylinder("cup", (0, 0, z), 0.06, 0.16, 0.12, 16, root))
    z += 0.12

    # the lantern: a base plate, four corner posts, glass panes, a top band and a peaked cap
    green_parts.append(kit.box("lantern-plate", (0, 0, z + 0.03), (0.36, 0.36, 0.06), root))
    z += 0.06
    glass_h, b0, b1 = 0.46, 0.15, 0.17  # height, half-width at the bottom and at the top
    for sx in (-1, 1):
        for sy in (-1, 1):
            post = kit.box(f"corner{sx}{sy}", (0, 0, z + glass_h / 2), (0.03, 0.03, glass_h), root)
            for v in post.data.vertices:  # lean each corner post out with the lantern's flare
                t = (v.co.z - z) / glass_h
                half = b0 + (b1 - b0) * t
                v.co.x += sx * half
                v.co.y += sy * half
            iron_parts.append(post)
    panes = []
    for name, facing in (("pane-front", "-y"), ("pane-back", "+y"), ("pane-left", "-x"), ("pane-right", "+x")):
        mid = (b0 + b1) / 2 - 0.005
        c = {"-y": (0, -mid, z + glass_h / 2), "+y": (0, mid, z + glass_h / 2),
             "-x": (-mid, 0, z + glass_h / 2), "+x": (mid, 0, z + glass_h / 2)}[facing]
        p = kit.plaque(name, pane, c, (b0 + b1 - 0.03, glass_h - 0.02), facing, root)  # the average width of the flared box
        panes.append(p)
    kit.join("glass", panes)
    light_z = z + glass_h / 2
    z += glass_h
    green_parts.append(kit.box("lantern-band", (0, 0, z + 0.025), (0.4, 0.4, 0.05), root))
    z += 0.05
    cap = kit.mesh_object("cap", [(-0.23, -0.23, z), (0.23, -0.23, z), (0.23, 0.23, z), (-0.23, 0.23, z),
                                  (-0.06, -0.06, z + 0.17), (0.06, -0.06, z + 0.17), (0.06, 0.06, z + 0.17), (-0.06, 0.06, z + 0.17)],
                          [(0, 1, 2, 3), (4, 5, 6, 7), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)], root, solid=True)
    iron_parts.append(cap)
    z += 0.17
    iron_parts.append(kit.cylinder("neck", (0, 0, z), 0.025, 0.025, 0.04, 8, root))
    z += 0.04
    bpy = kit.bpy
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=0.045, location=(0, 0, z + 0.04))
    ball = bpy.context.active_object
    ball.name = "finial"
    ball.parent = root
    iron_parts.append(ball)
    top = z + 0.085

    for o in iron_parts:
        kit.assign(o, iron)
        kit.box_uv(o, 1.0)
    for o in green_parts:
        kit.assign(o, verdigris)
        kit.box_uv(o, 1.0)
    for o in stone_parts:
        kit.assign(o, stone)
        kit.box_uv(o, kit.sheet_tile(*stone_box, 0.0106))
    post = kit.join("post", stone_parts + iron_parts + green_parts)
    for v in post.data.vertices:  # bent very slightly, as old posts are
        v.co.x += 0.012 * (v.co.z / top) ** 2
    kit.marker("light", (0.012 * (light_z / top) ** 2, 0, light_z), root)
    return root, top
