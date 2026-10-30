"""The outer city wall's kit: the pieces HubCity dresses the curtain wall, its towers and its gates with.

References: art/reference/concepts/city-round/city-kit-a.jpg (the wall section on relieving arches, the round tower,
the arched gate), wide-a.jpg and wide-c.jpg (the immaculate white wall above the forgotten corner), night-a.jpg
(braziers on the towers).

Conventions (read by Render/KitAsset and Scenes/HubCity), as the corner kit:
- Wall pieces have the wall's inner face at local z = 0 and their relief toward +z (into the city); x runs along the
  wall. A bay is 5.5 m long (the game stretches it to fit a run) with y = 0 at the wall-walk; the wall's body (3.6 m
  thick, outer face at z = -3.6) is the game's own.
- Tower pieces are round a vertical axis at the tower's centre, built for a 3 m radius (the game scales them); y = 0
  is the top of the tower's body (crown, roof) or the height of a band.
- Gate pieces stand on their ground (y = 0) with the wall face at z = 0.
- Parts are `<piece>_<role>`: stone and trim (the city's ashlar), dark, iron, roof (slate), lead, cloth (banners,
  tinted), moss, lamp (always lit), glass (lit at night).
"""
import math

from assets.corner_kit import Piece

PIECES = {
    "wall-bay": 0, "wall-bay-plain": 8, "tower-crown": 18, "tower-roof": 28, "tower-band": 38, "gatehouse": 48,
    "water-gate": 60,
}

COLOURS = {
    "stone": (0.93, 0.93, 0.92), "trim": (0.98, 0.98, 0.97), "dark": (0.09, 0.09, 0.09), "iron": (0.22, 0.17, 0.14),
    "roof": (0.36, 0.42, 0.50), "lead": (0.42, 0.45, 0.48), "cloth": (0.62, 0.22, 0.20), "moss": (0.36, 0.50, 0.25),
    "lamp": (1.0, 0.80, 0.45), "glass": (0.24, 0.24, 0.25), "shade": (0.76, 0.78, 0.80),
}

BAY, PIER, ARCH_R, SPRING = 5.5, 1.6, 1.95, -4.2      # bay length, pier width, arch radius, springing (below the walk)


def arch_outline(x0, x1, y0, y1, r, spring, steps=14):
    """A wall face from x0..x1, y0..y1 with a round-headed opening of radius r (springing at `spring`) cut up from
    its foot: the outline of the solid that is left."""
    pts = [(x0, y0), (x0, y1), (x1, y1), (x1, y0), (r, y0), (r, spring)]
    for i in range(1, steps):
        a = math.pi * i / steps
        pts.append((math.cos(a) * r, spring + math.sin(a) * r))
    pts += [(-r, spring), (-r, y0)]
    return pts


def voussoirs(p, cy, r0, r1, z0, z1, n, key=0.12, skip_key=False, role="trim"):
    """The wedge stones of a round arch centred at (0, cy), with a taller keystone."""
    for i in range(n):
        if skip_key and i == n // 2:
            continue
        a0, a1 = math.pi * i / n + 0.01, math.pi * (i + 1) / n - 0.01
        ro = r1 + (key if i == n // 2 else 0)
        pts = [(math.cos(a0) * r0, cy + math.sin(a0) * r0), (math.cos(a1) * r0, cy + math.sin(a1) * r0),
               (math.cos(a1) * ro, cy + math.sin(a1) * ro), (math.cos(a0) * ro, cy + math.sin(a0) * ro)]
        verts = [(x, y, z0) for x, y in pts] + [(x, y, z1) for x, y in pts]
        faces = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
        p.mesh(role, verts, faces)


def battlements(p, x0, x1, z_out, period=1.833, width=1.05):
    """The outer parapet on its corbel table (machicolations between the corbels), merlons with arrow slits and
    capstones, the outer string course below."""
    length = x1 - x0
    p.box("stone", ((x0 + x1) / 2, 0.45, z_out + 0.15), (length, 1.3, 0.9))              # the parapet, oversailing
    p.box("trim", ((x0 + x1) / 2, -1.3, z_out - 0.08), (length, 0.22, 0.16))             # the string course
    n = max(1, round(length / 0.92))
    for i in range(n):
        x = x0 + (i + 0.5) * length / n
        for k in range(3):                                                                 # a stepped corbel
            p.box("trim", (x, -0.35 - k * 0.22, z_out - 0.2 + k * 0.1), (0.36, 0.22, 0.4 - k * 0.2))
        if i < n - 1:
            p.box("dark", (x + length / n / 2, -0.21, z_out - 0.12), (length / n - 0.4, 0.04, 0.22))  # machicolation
    m = max(1, round(length / period))
    for i in range(m):
        x = x0 + (i + 0.5) * length / m
        p.box("stone", (x, 1.55, z_out + 0.3), (width, 0.9, 0.6))
        p.box("trim", (x, 2.04, z_out + 0.3), (width + 0.08, 0.08, 0.68))
        p.box("dark", (x, 1.5, z_out - 0.01), (0.09, 0.62, 0.04))                         # the arrow slit


def arch_shade(p, r, spring, z):
    """The shadowed head of an arch's opening (the game darkens `shade`), so it reads as a recess, not a line."""
    pts = [(math.cos(math.pi * i / 16) * r, spring + math.sin(math.pi * i / 16) * r) for i in range(17)]
    p.mesh("shade", [(x, y, z) for x, y in pts], [tuple(range(len(pts)))], solid=False)


def corbel_table(p, x0, x1, y, z_face, depth=1.0):
    """A row of stepped corbels on the inner face under a projecting band."""
    n = max(1, round((x1 - x0) / 0.92))
    for i in range(n):
        x = x0 + (i + 0.5) * (x1 - x0) / n
        for k in range(3):
            d = depth * (1 - k * 0.3)
            p.box("trim", (x, y - 0.11 - k * 0.22, z_face + d / 2), (0.36, 0.22, d))


def wall_bay(p, arched=True):
    """One bay of the wall's top: on the city side a band carrying the wall-walk (on a relieving arch between piers, or
    on a corbel table), a cornice and a railing; on the outer side the corbelled parapet and its merlons."""
    h = BAY / 2
    if arched:
        p.prism_z("stone", arch_outline(-h, h, SPRING, -0.24, ARCH_R, SPRING), 0.0, 1.0)
        voussoirs(p, SPRING, ARCH_R, ARCH_R + 0.55, 1.0, 1.3, 11, key=0.25)
        arch_shade(p, ARCH_R, SPRING, 0.01)
        for side in (-1, 1):
            p.box("trim", (side * (ARCH_R + 0.4), SPRING - 0.12, 0.6), (0.95, 0.24, 1.25))   # the imposts
        for x in (-h + 0.6, h - 0.6):
            p.lump("moss", (x, SPRING + 0.02, 0.9), 0.18, 0.06, 6)
    else:
        p.box("stone", (0, (SPRING - 0.24) / 2, 0.5), (BAY, -SPRING - 0.24, 1.0))           # the band, on corbels
        corbel_table(p, -h, h, SPRING, 0.0)
    p.box("trim", (0, -0.1, 0.57), (BAY, 0.24, 1.14))                                       # the cornice at the walk
    p.box("stone", (0, 0.45, 0.8), (BAY, 0.9, 0.4))                                         # the inner railing
    p.box("trim", (h - 0.3, 0.95, 0.8), (0.5, 0.12, 0.48))                                  # a cap at the joint
    battlements(p, -h, h, -3.6)


def tower_crown(p):
    """A machicolated, crenellated crown for a round tower of radius 3 (y = 0 at the top of its body)."""
    seg = 20
    for i in range(seg):
        a = 2 * math.pi * (i + 0.5) / seg
        for k in range(3):
            r = 3.0 + 0.12 + k * 0.1
            p.box("trim", (math.cos(a) * r, -0.3 - k * 0.22, math.sin(a) * r), (0.34, 0.22, 0.34))
    p.ring("stone", 2.95, 3.5, 0.0, 1.1, seg)
    p.ring("trim", 3.4, 3.58, -0.08, 0.1, seg)
    for i in range(10):
        a = 2 * math.pi * i / 10
        c = (math.cos(a) * 3.22, math.sin(a) * 3.22)
        p.box("stone", (c[0], 1.5, c[1]), (0.95, 0.8, 0.56), (0, -a, 0))
        p.box("trim", (c[0], 1.94, c[1]), (1.02, 0.08, 0.62), (0, -a, 0))
        p.box("dark", (math.cos(a) * 3.51, 1.45, math.sin(a) * 3.51), (0.08, 0.56, 0.08), (0, -a, 0))
    p.ring("lead", 0.0, 2.95, 0.02, 0.06, seg)
    p.cyl("iron", (0, 0.06, 0), 0.07, 0.07, 1.25, 6)                                          # the brazier's stand
    p.cyl("iron", (0, 1.3, 0), 0.3, 0.55, 0.35, 8)                                            # its bowl
    p.lump("lamp", (0, 1.62, 0), 0.34, 0.65, 6)                                               # and its flame
    p.lump("lamp", (0.12, 1.62, 0.08), 0.22, 0.85, 5)


def tower_roof(p):
    """A slate cone with a flared eave, a crown band and a finial (y = 0 at the top of the body)."""
    seg = 20
    p.ring("trim", 2.9, 3.25, 0.0, 0.35, seg)
    verts, faces = [], []
    rings = [(3.6, 0.25), (3.0, 0.95), (1.6, 3.0), (0.0, 5.0)]
    for r, y in rings[:-1]:
        for i in range(seg):
            a = 2 * math.pi * i / seg
            verts.append((math.cos(a) * r, y, math.sin(a) * r))
    verts.append((0, rings[-1][1], 0))
    apex = len(verts) - 1
    for k in range(len(rings) - 2):
        for i in range(seg):
            j = (i + 1) % seg
            faces.append((k * seg + i, k * seg + j, (k + 1) * seg + j, (k + 1) * seg + i))
    top = (len(rings) - 2) * seg
    for i in range(seg):
        faces.append((top + i, top + (i + 1) % seg, apex))
    faces.append(tuple(reversed(range(seg))))
    p.mesh("roof", verts, faces)
    p.cyl("iron", (0, 4.9, 0), 0.06, 0.04, 1.2, 6)
    p.lump("iron", (0, 5.6, 0), 0.14, 0.26, 6)


def tower_band(p):
    """A string course round a tower of radius 3, and arrow slits above it."""
    p.ring("trim", 2.95, 3.12, 0.0, 0.18, 20)
    for i in range(6):
        a = 2 * math.pi * (i + 0.5) / 6
        p.box("dark", (math.cos(a) * 3.0, 1.2, math.sin(a) * 3.0), (0.1, 0.9, 0.1), (0, -a, 0))
        p.box("trim", (math.cos(a) * 3.02, 0.7, math.sin(a) * 3.02), (0.36, 0.1, 0.2), (0, -a, 0))


def gatehouse(p):
    """The Low Gate's front on the city side: a tall gate tower around a round-headed passage, voussoirs, a raised
    portcullis, machicolations and battlements on top, banners, a window and two lanterns (y = 0 at the street)."""
    w, top, r, spring = 6.4, 11.0, 1.8, 3.2
    p.prism_z("stone", arch_outline(-w / 2, w / 2, 0.0, top, r, spring), 0.0, 2.2)
    p.box("dark", (0, (spring + r) / 2, 0.02), (2 * r, spring + r, 0.04))                    # the passage beyond
    voussoirs(p, spring, r, r + 0.5, 2.2, 2.3, 13)
    for side in (-1, 1):
        p.box("trim", (side * (r + 0.25), spring - 0.12, 1.1), (0.5, 0.24, 2.3))            # imposts
        p.box("trim", (side * (w / 2 - 0.2), 0.3, 1.1), (0.5, 0.6, 2.3))                    # plinth corners
    for x in [-r + 0.25 + i * 0.4 for i in range(9)]:
        p.box("iron", (x, spring + 1.2, 1.2), (0.07, 1.6, 0.07))                              # the portcullis, raised
    p.box("iron", (0, spring + 1.4, 1.2), (2 * r - 0.2, 0.08, 0.08))
    for x in [-r + 0.25 + i * 0.4 for i in range(9)]:
        p.box("iron", (x, spring + 0.35, 1.2), (0.05, 0.14, 0.05))                            # its spikes
    p.box("trim", (0, 7.2, 2.25), (w + 0.1, 0.22, 0.2))                                       # a string course
    p.box("glass", (0, 8.2, 2.22), (0.7, 1.1, 0.06))                                           # the guardroom window
    p.box("trim", (0, 7.6, 2.3), (0.95, 0.1, 0.2))
    for x in (-1.6, 1.6):
        p.box("dark", (x, 8.3, 2.22), (0.1, 0.9, 0.04))                                        # arrow slits
    # machicolations and battlements over the gate
    p.box("stone", (0, top - 0.6, 2.5), (w + 0.4, 1.2, 0.7))
    for i in range(8):
        x = -w / 2 + (i + 0.5) * w / 8
        for k in range(3):
            p.box("trim", (x, top - 1.35 - k * 0.22, 2.3 - k * 0.1), (0.36, 0.22, 0.4 - k * 0.1))
    for i in range(4):
        x = -w / 2 + 0.6 + i * (w - 1.2) / 3
        p.box("stone", (x, top + 0.45, 2.5), (1.0, 0.9, 0.7))
        p.box("trim", (x, top + 0.94, 2.5), (1.08, 0.08, 0.76))
    # banners either side of the arch, lanterns on brackets
    for x in (-2.55, 2.55):
        p.box("cloth", (x, 5.6, 2.24), (0.9, 2.6, 0.04))
        p.box("iron", (x, 6.95, 2.3), (1.1, 0.06, 0.06))
        p.box("iron", (x * 0.88, 3.3, 2.45), (0.06, 0.06, 0.5))
        p.box("lamp", (x * 0.88, 3.0, 2.65), (0.24, 0.32, 0.24))
    p.lump("moss", (-2.9, 0.0, 2.3), 0.2, 0.06, 6)


def water_gate(p):
    """A broad, round-headed water gate cut into the foot of the city wall, its portcullis rusted up near the crown:
    the Spirit Pools' cave mouth sits in it (y = 0 at the courtyard floor). The frame runs 0.5 m back into the wall,
    where the game sets the wall's body back, so the opening has jambs and a soffit; the dark is at the back of that."""
    r, spring, w, h, back = 2.8, 4.4, 7.2, 7.8, -0.5
    p.prism_z("stone", arch_outline(-w / 2, w / 2, -0.4, h, r, spring), back, 0.6)
    voussoirs(p, spring, r, r + 0.7, 0.6, 0.95, 17, key=0.3)
    p.box("trim", (0, h + 0.12, 0.3), (w + 0.3, 0.25, 0.85))                                  # a hood over the frame
    for side in (-1, 1):
        p.box("trim", (side * (r + 0.4), spring - 0.12, 0.35), (0.85, 0.24, 0.75))
    pts = [(-r, -0.4), (r, -0.4)] + [(math.cos(math.pi * i / 16) * r, spring + math.sin(math.pi * i / 16) * r) for i in range(17)]
    p.mesh("dark", [(x, y, back + 0.02) for x, y in pts], [tuple(range(len(pts)))], solid=False)
    for i in range(13):
        x = -r + 0.35 + i * (2 * r - 0.7) / 12
        top = spring + math.sqrt(max(r * r - x * x, 0.0)) - 0.15
        bottom = max(spring + 0.9, top - 1.5)
        if top - bottom > 0.2:
            p.box("iron", (x, (top + bottom) / 2, 0.25), (0.08, top - bottom, 0.08))
            p.box("iron", (x, bottom - 0.08, 0.25), (0.05, 0.16, 0.05))
    p.box("iron", (0, spring + 1.05, 0.25), (2 * r - 0.6, 0.08, 0.08))
    p.box("iron", (0, spring + 1.8, 0.25), (2 * r - 1.6, 0.08, 0.08))
    for x, y in ((-r - 0.3, 0.1), (r + 0.4, 0.05), (-r - 0.2, spring), (r + 0.3, spring + 0.2), (-1.0, spring + r + 0.6), (1.4, spring + r + 0.45)):
        p.lump("moss", (x, y, 0.62), 0.24, 0.08, 6)


def build(kit):
    root = kit.root("wall-kit")
    materials = {role: kit.material("wall-" + role, color=kit.srgb_to_linear(col), emissive=col if role == "lamp" else None) for role, col in COLOURS.items()}
    makers = {
        "wall-bay": lambda p: wall_bay(p, True), "wall-bay-plain": lambda p: wall_bay(p, False),
        "tower-crown": tower_crown, "tower-roof": tower_roof, "tower-band": tower_band,
        "gatehouse": gatehouse, "water-gate": water_gate,
    }
    for i, (name, body) in enumerate(makers.items()):
        p = Piece(kit, root, name, 100 + i, PIECES[name])
        body(p)
        p.finish(materials)
    return root, 14
