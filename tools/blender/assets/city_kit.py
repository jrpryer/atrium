"""The white city's building kit: the pieces HubCity bakes into the courtyard's city in place of its code boxes.

References: art/reference/concepts/city-round/wide-a.jpg (layout), wide-c.jpg (mood), city-kit-a.jpg (the pieces).
A tidy, prosperous city of crisp, almost marble-white limestone: stone townhouses with string courses and cornices,
steep blue-grey slate roofs with dormers and chimneys, shutters and window boxes, a shop with an awning and a hanging
sign, an inn, a stable with a hayloft, a tower-house with a parapet.

Conventions (read by HubCity.Kit):
- Each piece is built round its own origin (on the ground, at the footprint's centre), front toward -Y in Blender
  (+Z in the game), and laid out along X at PIECES[name].x; an empty named after the piece marks that origin, and
  `<piece>-lamp` marks its lantern if it has one. The game subtracts the origin, so vertices sit in file space.
- Each piece's parts are one mesh per role, named `<piece>_<role>`: stone and trim (the game's masonry layer), roof
  (tinted per building), timber, plaster, dark (door and shop openings), plants, flowers, cloth (tinted per
  building), iron, pot, lead, lamp (always lit), and glass-a / glass-b (front windows, lit or dark as two sets) and
  glass-back (back windows, mostly dark).
- Nominal footprints (w across the front, d deep) are in PIECES; the game scales each building to its lot.
"""
import math

G, U = 2.8, 2.4   # ground storey and upper storey heights (match HubCity.Building)

PIECES = {
    # name: (x position in the file, w, d, kind)
    "house-2a": (0, 5.0, 5.5, "house"),
    "house-3a": (14, 5.0, 5.5, "house"),
    "house-3b": (28, 5.0, 5.5, "house"),
    "house-4a": (42, 4.5, 5.5, "house"),
    "house-3c": (56, 5.5, 5.5, "house"),
    "shop-3": (70, 5.5, 5.5, "shop"),
    "inn-3": (86, 8.5, 6.0, "inn"),
    "stable-1": (102, 7.0, 5.5, "stable"),
    "tower-5": (116, 4.2, 4.2, "tower"),
}

COLOURS = {
    "stone": (0.93, 0.93, 0.92), "trim": (0.98, 0.98, 0.97), "roof": (0.36, 0.42, 0.50), "timber": (0.36, 0.26, 0.19),
    "plaster": (0.93, 0.90, 0.84), "dark": (0.11, 0.10, 0.10), "glass-a": (0.24, 0.24, 0.25), "glass-b": (0.24, 0.24, 0.25),
    "glass-back": (0.24, 0.24, 0.25), "plants": (0.30, 0.44, 0.24), "flowers": (0.80, 0.34, 0.30), "cloth": (0.74, 0.30, 0.26),
    "iron": (0.20, 0.19, 0.18), "pot": (0.62, 0.36, 0.26), "lead": (0.42, 0.45, 0.48), "lamp": (1.0, 0.82, 0.48),
}


class Piece:
    """Collects one piece's boxes and prisms by role, in game-local coordinates (x across, y up, z toward the front)."""

    def __init__(self, kit, root, name):
        self.kit, self.root, self.name = kit, root, name
        self.ox = PIECES[name][0]
        self.parts = {}
        self.n = 0

    def _add(self, role, ob):
        self.parts.setdefault(role, []).append(ob)

    def _id(self):
        self.n += 1
        return f"{self.name}-{self.n}"

    def box(self, role, c, s):
        """A box from its centre and size in game-local coordinates."""
        x, y, z = c
        sx, sy, sz = s
        self._add(role, self.kit.box(self._id(), (self.ox + x, -z, y), (sx, sz, sy)))

    def ridge_x(self, role, x0, x1, profile):
        """A prism along x from a (z, y) outline: roofs and gables whose ridge runs across the front."""
        self._add(role, self.kit.prism_x(self._id(), [(-z, y) for z, y in profile], self.ox + x0, self.ox + x1))

    def ridge_z(self, role, z0, z1, profile):
        """A prism along z from an (x, y) outline: roofs and gables whose ridge runs front to back."""
        self._add(role, self.kit.prism(self._id(), [(self.ox + x, y) for x, y in profile], -z1, -z0))

    def cyl(self, role, base, r0, r1, h, seg=8):
        x, y, z = base
        self._add(role, self.kit.cylinder(self._id(), (self.ox + x, -z, y), r0, r1, h, seg))

    def finish(self, materials):
        for role, obs in self.parts.items():
            ob = self.kit.join(f"{self.name}_{role}", obs)
            self.kit.assign(ob, materials[role])
            ob.parent = self.root
        self.kit.marker(self.name, (self.ox, 0, 0), self.root)


def gable_roof(p, w, d, eave, pitch=0.62, over=0.25, t=0.22, along_x=True):
    """A slate roof as a thick inverted V over a w by d footprint, and the stone gable triangles under it.
    Returns the ridge height."""
    span = d if along_x else w
    rise = span / 2 * pitch * 2
    ridge = eave + rise
    h = span / 2 + over
    shell = [(h, eave - over * pitch * 2), (0, ridge + t), (-h, eave - over * pitch * 2),
             (-h, eave - over * pitch * 2 - t), (0, ridge), (h, eave - over * pitch * 2 - t)]
    gable = [(span / 2, eave), (0, ridge - 0.05), (-span / 2, eave)]
    if along_x:
        p.ridge_x("roof", -w / 2 - 0.18, w / 2 + 0.18, shell)
        p.ridge_x("stone", -w / 2, w / 2, gable)
    else:
        p.ridge_z("roof", -d / 2 - 0.18, d / 2 + 0.18, shell)
        p.ridge_z("stone", -d / 2, d / 2, gable)
    return ridge


def window(p, x, y, z, w=0.62, h=1.0, glass="glass-a", shutters=False, box=False, back=False):
    """A window on the front (or back) face at z: glass, a sill and lintel in trim, shutters, a window box."""
    s = -1 if back else 1
    p.box(glass, (x, y, z + s * 0.02), (w, h, 0.06))
    p.box("trim", (x, y - h / 2 - 0.06, z + s * 0.08), (w + 0.2, 0.1, 0.18))
    p.box("trim", (x, y + h / 2 + 0.07, z + s * 0.04), (w + 0.16, 0.14, 0.1))
    if shutters:
        for sx in (-1, 1):
            p.box("timber", (x + sx * (w / 2 + 0.2), y, z + s * 0.04), (0.34, h, 0.05))
    if box:
        p.box("timber", (x, y - h / 2 - 0.2, z + s * 0.18), (w + 0.1, 0.2, 0.24))
        p.box("plants", (x, y - h / 2 - 0.06, z + s * 0.18), (w, 0.14, 0.2))
        for k in (-1, 0, 1):
            p.box("flowers", (x + k * w / 3, y - h / 2 + 0.04, z + s * 0.22), (0.12, 0.1, 0.1))


def door(p, x, z, w=1.1, h=2.1, back=False):
    s = -1 if back else 1
    p.box("dark", (x, h / 2, z + s * 0.03), (w, h, 0.06))
    p.box("trim", (x, h + 0.12, z + s * 0.06), (w + 0.3, 0.24, 0.12))           # the lintel
    p.box("trim", (x, 0.08, z + s * 0.3), (w + 0.3, 0.16, 0.5))                 # the step
    p.box("iron", (x + w * 0.3, h * 0.5, z + s * 0.07), (0.06, 0.06, 0.04))     # the latch


def dormer(p, x, d, eave, pitch=0.62, glass="glass-a"):
    """A dormer on the front slope: stone cheeks, a little gable roof, a window."""
    z0 = d / 2 - 0.4
    p.box("stone", (x, eave + 0.65, z0 - 0.7), (1.3, 1.3, 1.6))
    p.ridge_z("roof", z0 - 1.6, z0 + 0.15, [(x + 0.85, eave + 1.2), (x, eave + 1.85), (x - 0.85, eave + 1.2),
                                            (x - 0.85, eave + 1.05), (x, eave + 1.7), (x + 0.85, eave + 1.05)])
    p.box(glass, (x, eave + 0.7, z0 + 0.12), (0.6, 0.8, 0.06))
    p.box("trim", (x, eave + 0.25, z0 + 0.16), (0.8, 0.08, 0.14))


def chimney(p, x, z, base, top):
    p.box("stone", (x, (base + top) / 2, z), (0.7, top - base, 0.9))
    p.box("trim", (x, top + 0.08, z), (0.85, 0.16, 1.05))
    for k in (-0.18, 0.18):
        p.cyl("pot", (x, top + 0.16, z + k), 0.12, 0.1, 0.4)


def townhouse(p, w, d, uppers, dormers=(), chimney_x=None, shutters=False, gable_front=False, plaster=False,
              boxes=0.4, door_x=None):
    """The common townhouse: a plinth, a stone ground floor, upper storeys (stone, or plaster with a timber frame
    and a jetty), string courses, a cornice, a steep roof, windows front and back."""
    jetty = 0.25 if plaster else 0.0
    top = G + uppers * U
    p.box("trim", (0, 0.15, 0), (w + 0.12, 0.3, d + 0.12))                      # the plinth
    p.box("stone", (0, G / 2, 0), (w, G, d))
    if plaster:
        p.box("plaster", (0, G + uppers * U / 2, jetty / 2), (w + 0.08, uppers * U, d + jetty))
        p.box("timber", (0, G + 0.08, jetty / 2), (w + 0.24, 0.16, d + jetty + 0.16))
        for k in range(4):
            p.box("timber", (-w / 2 + k * w / 3, G + uppers * U / 2, d / 2 + jetty + 0.03), (0.16, uppers * U, 0.06))
        for f in range(1, uppers):
            p.box("timber", (0, G + f * U, d / 2 + jetty + 0.03), (w, 0.12, 0.06))
    else:
        p.box("stone", (0, G + uppers * U / 2, 0), (w, uppers * U, d))
        for f in range(uppers):
            p.box("trim", (0, G + f * U, 0), (w + 0.14, 0.14, d + 0.14))       # string courses
    p.box("trim", (0, top - 0.1, jetty / 2), (w + 0.3, 0.2, d + jetty + 0.3))   # the cornice
    fz, bz = d / 2 + jetty, -d / 2
    if gable_front:
        ridge = gable_roof(p, w, d + jetty, top, along_x=False)
        window(p, 0, top + (ridge - top) * 0.35, fz + 0.02, 0.5, 0.7, "glass-b")
    else:
        ridge = gable_roof(p, w, d + jetty, top)
        for i, x in enumerate(dormers):
            dormer(p, x, d + jetty, top, glass="glass-a" if i % 2 == 0 else "glass-b")
    # front: a door and a window on the ground floor, two or three windows a storey above
    cols = 3 if w > 5.2 else 2
    dx = door_x if door_x is not None else (-w / 4 if cols == 2 else -w / 3)
    door(p, dx, d / 2)
    for x in ((-w / 3, w / 3) if abs(dx) < 0.5 else (-dx,)):   # a central door gets a window either side
        window(p, x, 1.55, d / 2, 0.7, 1.1, "glass-b", shutters=shutters)
    for f in range(uppers):
        for k in range(cols):
            x = -w / 2 + (k + 0.5) * w / cols
            window(p, x, G + f * U + 1.2, fz, glass="glass-a" if (k + f) % 2 == 0 else "glass-b",
                   shutters=shutters, box=(f == 0 and (k * 7 + uppers) % 10 < boxes * 10))
    # back: a small window a storey (a window box on some), a back door
    for f in range(uppers):
        window(p, (-1 if f % 2 else 1) * w / 5, G + f * U + 1.2, bz, 0.45, 0.65, "glass-back", back=True,
               box=(f == 0))
    door(p, w / 4, bz, 0.9, 1.9, back=True)
    if chimney_x is not None:
        chimney(p, chimney_x * (w / 2 - 0.45), -d / 6, top - 0.5, ridge + 0.9)
    return top, ridge


def build_pieces(kit, root, materials):
    def make(name, body):
        p = Piece(kit, root, name)
        body(p)
        p.finish(materials)

    make("house-2a", lambda p: townhouse(p, 5.0, 5.5, 1, dormers=(-1.2, 1.2), chimney_x=1, boxes=0.8))
    make("house-3a", lambda p: townhouse(p, 5.0, 5.5, 2, dormers=(0.0,), chimney_x=-1, shutters=True))
    make("house-3b", lambda p: townhouse(p, 5.0, 5.5, 2, chimney_x=1, gable_front=True, boxes=0.6))
    make("house-4a", lambda p: townhouse(p, 4.5, 5.5, 3, dormers=(0.0,), chimney_x=1, boxes=0.5))
    make("house-3c", lambda p: townhouse(p, 5.5, 5.5, 2, dormers=(-1.3, 1.3), chimney_x=-1, plaster=True, boxes=0.7))

    def shop(p):
        w, d = 5.5, 5.5
        townhouse(p, w, d, 2, dormers=(0.0,), chimney_x=1, shutters=True)
        # the shopfront: a wide opening in the ground floor, a counter, an awning, a hanging sign
        p.box("dark", (-0.6, 1.25, d / 2 + 0.04), (3.0, 1.9, 0.06))
        p.box("timber", (-0.6, 0.55, d / 2 + 0.25), (3.0, 0.9, 0.4))
        p.box("cloth", (-0.6, 2.5, d / 2 + 0.6), (3.4, 0.08, 1.2))
        for k in range(5):
            p.box("trim" if k % 2 else "cloth", (-0.6 - 1.36 + k * 0.68, 2.36, d / 2 + 1.18), (0.68, 0.24, 0.05))
        p.box("iron", (1.9, 2.95, d / 2 + 0.45), (0.06, 0.06, 0.9))
        p.box("timber", (1.9, 2.6, d / 2 + 0.75), (0.06, 0.55, 0.6))
    make("shop-3", shop)

    def inn(p):
        w, d = 8.5, 6.0
        top, ridge = townhouse(p, w, d, 2, dormers=(-2.6, 0.0, 2.6), chimney_x=-1, shutters=True, boxes=0.7, door_x=0.0)
        chimney(p, w / 2 - 0.45, -d / 6, top - 0.5, ridge + 0.9)
        p.box("dark", (0, 1.15, d / 2 + 0.035), (1.8, 2.3, 0.06))                   # the wide door
        p.box("iron", (1.6, 3.0, d / 2 + 0.55), (0.06, 0.06, 1.0))
        p.box("timber", (1.6, 2.55, d / 2 + 0.9), (0.06, 0.7, 0.85))                # the sign
        p.box("iron", (-1.3, 2.75, d / 2 + 0.2), (0.06, 0.06, 0.3))
        p.box("lamp", (-1.3, 2.45, d / 2 + 0.35), (0.24, 0.32, 0.24))
        kit.marker("inn-3-lamp", (PIECES["inn-3"][0] - 1.3, -(d / 2 + 0.35), 2.45), root)
        for x in (-3.2, 3.2):
            p.cyl("timber", (x, 0, d / 2 + 0.6), 0.3, 0.3, 0.8, 8)                  # barrels by the door
    make("inn-3", inn)

    def stable(p):
        w, d = 7.0, 5.5
        g = 3.2
        p.box("trim", (0, 0.15, 0), (w + 0.12, 0.3, d + 0.12))
        p.box("stone", (0, g / 2, 0), (w, g, d))
        p.box("trim", (0, g - 0.1, 0), (w + 0.3, 0.2, d + 0.3))
        ridge = gable_roof(p, w, d, g, pitch=0.55)
        for x in (-1.7, 1.7):
            p.box("timber", (x, 1.25, d / 2 + 0.04), (2.0, 2.5, 0.08))
            p.box("trim", (x, 2.65, d / 2 + 0.06), (2.3, 0.22, 0.12))
            p.box("dark", (x, 2.2, d / 2 + 0.09), (1.6, 0.25, 0.04))
        dormer(p, 0, d, g, glass="glass-b")
        p.box("timber", (0, g + 0.7, d / 2 - 0.22), (0.75, 0.85, 0.06))           # the hayloft door over the dormer glass
        p.box("timber", (0, g + 1.55, d / 2 + 0.3), (0.12, 0.12, 1.2))            # the hoist beam
        p.box("iron", (0, 2.75, d / 2 + 0.2), (0.06, 0.06, 0.3))
        p.box("lamp", (0, 2.45, d / 2 + 0.35), (0.24, 0.32, 0.24))
        kit.marker("stable-1-lamp", (PIECES["stable-1"][0], -(d / 2 + 0.35), 2.45), root)
        p.box("timber", (-w / 2 - 0.5, 0.45, d / 2 - 1.0), (0.8, 0.9, 1.6))        # a hay rack
        p.box("plants", (-w / 2 - 0.5, 0.95, d / 2 - 1.0), (0.7, 0.2, 1.5))
        for f in range(1):
            window(p, w / 4, 1.8, -d / 2, 0.5, 0.6, "glass-back", back=True)
    make("stable-1", stable)

    def tower(p):
        w, d, uppers = 4.2, 4.2, 4
        top = G + uppers * U
        p.box("trim", (0, 0.15, 0), (w + 0.12, 0.3, d + 0.12))
        p.box("stone", (0, top / 2, 0), (w, top, d))
        for f in range(uppers + 1):
            p.box("trim", (0, G + f * U - (0.1 if f == uppers else 0), 0), (w + 0.14, 0.14, d + 0.14))
        p.box("lead", (0, top + 0.05, 0), (w - 0.4, 0.1, d - 0.4))
        for sx, sz, lx, lz in ((0, d / 2 - 0.2, w, 0.4), (0, -d / 2 + 0.2, w, 0.4), (w / 2 - 0.2, 0, 0.4, d), (-w / 2 + 0.2, 0, 0.4, d)):
            p.box("stone", (sx, top + 0.5, sz), (lx, 1.0, lz))
        for k in range(4):
            for side in (-1, 1):
                p.box("stone", (-w / 2 + 0.35 + k * (w - 0.7) / 3, top + 1.2, side * (d / 2 - 0.2)), (0.5, 0.45, 0.4))
        door(p, 0, d / 2)
        for f in range(uppers):
            window(p, 0, G + f * U + 1.2, d / 2, 0.5, 0.9, "glass-a" if f % 2 else "glass-b", box=(f == 1))
            window(p, 0, G + f * U + 1.2, -d / 2, 0.4, 0.6, "glass-back", back=True)
    make("tower-5", tower)


def build(kit):
    root = kit.root("city-kit")
    materials = {role: kit.material("city-" + role, color=kit.srgb_to_linear(col), emissive=col if role == "lamp" else None)
                 for role, col in COLOURS.items()}
    build_pieces(kit, root, materials)
    return root, 16
