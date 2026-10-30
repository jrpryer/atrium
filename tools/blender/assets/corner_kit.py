"""The forgotten corner's kit: the pieces HubCorner places round the sunken courtyard the tidy city let go.

References: art/reference/concepts/city-round/corner-kit-a.jpg and corner-kit-b.jpg (the pieces), corner-a.jpg (how
they sit together): crumbling limestone with brick showing through, broken coping, flagstones heaved by roots, the
cracked bricked-up arch the falls burst from, rubble, ivy curtains, moss, ferns, grass tufts, puddles, rivulets.

Conventions (read by Render/KitAsset and Scenes/HubCorner):
- Each piece is built round its own origin, laid out along X at PIECES[name]; an empty named after the piece marks
  the origin. Wall pieces have the wall face at local z = 0 with their relief toward +z (out of the wall) and their
  foot at y = 0; hanging pieces (ivy) hang down from y = 0; ground pieces sit on y = 0.
- Parts are one mesh per role, named `<piece>_<role>`: stone (the corner's weathered masonry layer), brick, dark,
  moss, root, leaf, leaf-b, fern, grass, flower, flower-b, water (puddles, flat on the ground) and trickle (rivulets
  running down walls).
- Game-local coordinates here (x across, y up, z out of the wall or toward the front); Blender is (x, -z, y).
"""
import math
import random

PIECES = {
    "wall-crumble-a": 0, "wall-crumble-b": 6, "brick-arch": 12, "flags-roots-a": 18, "flags-roots-b": 23,
    "rubble-a": 28, "coping-fallen": 31, "ivy-curtain-a": 34, "ivy-curtain-b": 38, "moss-a": 42, "moss-b": 44,
    "fern-a": 46, "tuft-a": 48, "puddle-a": 50, "puddle-b": 53, "rivulet-a": 56,
}

COLOURS = {
    "stone": (0.86, 0.84, 0.78), "brick": (0.60, 0.34, 0.26), "dark": (0.10, 0.10, 0.10), "moss": (0.36, 0.50, 0.25),
    "root": (0.30, 0.20, 0.14), "leaf": (0.28, 0.44, 0.22), "leaf-b": (0.40, 0.56, 0.28), "fern": (0.32, 0.50, 0.26),
    "grass": (0.46, 0.62, 0.30), "flower": (0.96, 0.93, 0.74), "flower-b": (0.94, 0.74, 0.30), "water": (0.62, 0.80, 0.84),
    "trickle": (0.70, 0.86, 0.88),
}


class Piece:
    """One piece's geometry by role, in game-local coordinates round the piece's origin."""

    def __init__(self, kit, root, name, seed, ox=None):
        self.kit, self.root, self.name = kit, root, name
        self.ox = PIECES[name] if ox is None else ox
        self.parts = {}
        self.n = 0
        self.rng = random.Random(seed)

    def r(self, a, b):
        return self.rng.uniform(a, b)

    def _id(self):
        self.n += 1
        return f"{self.name}-{self.n}"

    def _to(self, p):
        x, y, z = p
        return (self.ox + x, -z, y)

    def mesh(self, role, verts, faces, solid=True):
        ob = self.kit.mesh_object(self._id(), [self._to(v) for v in verts], faces, solid=solid)
        self.parts.setdefault(role, []).append(ob)

    def prism_z(self, role, outline, z0, z1):
        """Extrudes a closed x-y outline (it may be concave: an arch cut into a wall) along z from z0 to z1."""
        ob = self.kit.prism(self._id(), [(self.ox + x, y) for x, y in outline], -z1, -z0)
        self.parts.setdefault(role, []).append(ob)

    def cyl(self, role, base, r0, r1, h, seg=8):
        """A tapered cylinder standing on `base`."""
        x, y, z = base
        self.parts.setdefault(role, []).append(self.kit.cylinder(self._id(), (self.ox + x, -z, y), r0, r1, h, seg))

    def ring(self, role, r0, r1, y0, y1, seg=16, centre=(0.0, 0.0)):
        """A closed annular band round a vertical axis (a tower's parapet, a string course)."""
        cx, cz = centre
        verts = []
        for r, y in ((r1, y0), (r1, y1), (r0, y1), (r0, y0)):
            for i in range(seg):
                a = 2 * math.pi * i / seg
                verts.append((cx + math.cos(a) * r, y, cz + math.sin(a) * r))
        faces = []
        for k in range(4):
            for i in range(seg):
                j = (i + 1) % seg
                faces.append((k * seg + i, k * seg + j, ((k + 1) % 4) * seg + j, ((k + 1) % 4) * seg + i))
        self.mesh(role, verts, faces)

    def box(self, role, c, s, rot=(0.0, 0.0, 0.0)):
        """A box from its centre and size, turned by small angles (about x, y, z in radians)."""
        hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
        ax, ay, az = rot
        verts = []
        for x in (-hx, hx):
            for y in (-hy, hy):
                for z in (-hz, hz):
                    # rotate about z (in the x-y plane), then x (y-z), then y (x-z)
                    x1, y1 = x * math.cos(az) - y * math.sin(az), x * math.sin(az) + y * math.cos(az)
                    y2, z1 = y1 * math.cos(ax) - z * math.sin(ax), y1 * math.sin(ax) + z * math.cos(ax)
                    x2, z2 = x1 * math.cos(ay) + z1 * math.sin(ay), -x1 * math.sin(ay) + z1 * math.cos(ay)
                    verts.append((c[0] + x2, c[1] + y2, c[2] + z2))
        faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
        self.mesh(role, verts, faces)

    def flat(self, role, centre, radius, y, n=14, wobble=0.3, squash=1.0):
        """A flat, irregular disc (a puddle, a patch), facing up."""
        cx, cz = centre
        ring = []
        for i in range(n):
            a = 2 * math.pi * i / n
            rr = radius * (1 + self.r(-wobble, wobble))
            ring.append((cx + math.cos(a) * rr, y, cz + math.sin(a) * rr * squash))
        verts = [(cx, y, cz)] + ring
        faces = [(0, 1 + (i + 1) % n, 1 + i) for i in range(n)]
        self.mesh(role, verts, faces, solid=False)
        self._face_up(role)

    def _face_up(self, role):
        """Turns a flat piece's faces to point up (+Blender Z)."""
        me = self.parts[role][-1].data
        for poly in me.polygons:
            if poly.normal.z < 0:
                poly.flip()
        me.update()

    def lump(self, role, c, r, h, seg=7):
        """A low, jittered dome: a clump of moss, a stone."""
        x, y, z = c
        ring0 = [(x + math.cos(2 * math.pi * i / seg) * r * self.r(0.8, 1.15), y, z + math.sin(2 * math.pi * i / seg) * r * self.r(0.8, 1.15)) for i in range(seg)]
        ring1 = [(x + (p[0] - x) * 0.6, y + h * self.r(0.6, 0.9), z + (p[2] - z) * 0.6) for p in ring0]
        top = (x + self.r(-0.05, 0.05), y + h, z + self.r(-0.05, 0.05))
        verts = ring0 + ring1 + [top]
        faces = [(i, (i + 1) % seg, seg + (i + 1) % seg, seg + i) for i in range(seg)]
        faces += [(seg + i, seg + (i + 1) % seg, 2 * seg) for i in range(seg)]
        faces.append(tuple(reversed(range(seg))))
        self.mesh(role, verts, faces)

    def blade(self, role, base, direction, length, width, droop, segs=4):
        """A tapered, arching blade (a fern frond, a grass blade): a thin double-sided strip."""
        bx, by, bz = base
        dx, dz = direction
        side = (-dz, dx)
        verts = []
        for i in range(segs + 1):
            t = i / segs
            w = width * (1 - t) * (0.6 + 0.8 * t * (1 - t) * 2)
            px, pz = bx + dx * length * t, bz + dz * length * t
            py = by + math.sin(t * math.pi * 0.6) * length * 0.6 - droop * t * t
            verts.append((px + side[0] * w / 2, py, pz + side[1] * w / 2))
            verts.append((px - side[0] * w / 2, py, pz - side[1] * w / 2))
        faces = [(2 * i, 2 * i + 1, 2 * i + 3, 2 * i + 2) for i in range(segs)]
        faces += [(2 * i + 2, 2 * i + 3, 2 * i + 1, 2 * i) for i in range(segs)]   # both sides (the game culls backs)
        self.mesh(role, verts, faces, solid=False)

    def tube(self, role, points, r0, r1, seg=6):
        """A tapered tube along a polyline (a root)."""
        verts, faces = [], []
        n = len(points)
        for i, (x, y, z) in enumerate(points):
            a, b = points[max(i - 1, 0)], points[min(i + 1, n - 1)]
            tx, ty, tz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
            ln = math.sqrt(tx * tx + ty * ty + tz * tz) or 1
            tx, ty, tz = tx / ln, ty / ln, tz / ln
            # two axes across the tube
            ux, uy, uz = -tz, 0.0, tx
            ul = math.sqrt(ux * ux + uz * uz) or 1
            ux, uz = ux / ul, uz / ul
            vx, vy, vz = ty * uz - tz * uy, tz * ux - tx * uz, tx * uy - ty * ux
            rr = r0 + (r1 - r0) * i / (n - 1)
            for k in range(seg):
                a2 = 2 * math.pi * k / seg
                verts.append((x + (ux * math.cos(a2) + vx * math.sin(a2)) * rr, y + (uy * math.cos(a2) + vy * math.sin(a2)) * rr,
                              z + (uz * math.cos(a2) + vz * math.sin(a2)) * rr))
        for i in range(n - 1):
            for k in range(seg):
                j = (k + 1) % seg
                faces.append((i * seg + k, i * seg + j, (i + 1) * seg + j, (i + 1) * seg + k))
        faces.append(tuple(range(seg)))
        faces.append(tuple(range((n - 1) * seg, n * seg))[::-1])
        self.mesh(role, verts, faces)

    def finish(self, materials):
        for role, obs in self.parts.items():
            ob = self.kit.join(f"{self.name}_{role}", obs)
            self.kit.assign(ob, materials[role])
            ob.parent = self.root
        self.kit.marker(self.name, (self.ox, 0, 0), self.root)


def bricks(p, x0, x1, y0, y1, z, skip=None):
    """Courses of small bricks filling a rectangle on a face; skip(x, y) leaves gaps."""
    bh, bw = 0.11, 0.26
    row = 0
    y = y0
    while y + bh <= y1 + 1e-6:
        x = x0 - (bw / 2 if row % 2 else 0)
        while x < x1:
            xa, xb = max(x, x0), min(x + bw - 0.02, x1)
            cx, cy = (xa + xb) / 2, y + bh / 2
            if xb - xa > 0.06 and not (skip and skip(cx, cy)):
                p.box("brick", (cx, cy, z + p.r(0.0, 0.025)), (xb - xa, bh - 0.02, 0.06))
            x += bw
        y += bh
        row += 1


def crumbling_wall(p, w=4.0, h=4.0, crumble=0.18):
    """A section of the pocket's retaining wall, in relief on its face: worn ashlar blocks, some fallen out to the
    brick behind, a crack, broken coping on top, moss in the ledges and at the foot."""
    y = -0.2
    holes = []
    while y < h - 0.3:
        ch = p.r(0.42, 0.62)
        x = -w / 2
        while x < w / 2 - 0.1:
            bw = min(p.r(0.7, 1.4), w / 2 - x)
            cx, cy = x + bw / 2, y + ch / 2
            if p.rng.random() < crumble and 0.3 < cy < h - 0.6:
                holes.append((x, x + bw, y, y + ch))            # fallen out: brick behind, a dark gap at the edge
                bricks(p, x + 0.03, x + bw - 0.03, y + 0.03, y + ch - 0.03, 0.0)
                p.box("dark", (cx, y + 0.03, 0.01), (bw - 0.06, 0.05, 0.03))
            else:
                d = p.r(0.04, 0.16)
                p.box("stone", (cx, cy, d / 2), (bw - 0.04, ch - 0.04, d), (0, 0, p.r(-0.015, 0.015)))
                if p.rng.random() < 0.35:
                    p.lump("moss", (cx + p.r(-bw / 3, bw / 3), y + ch - 0.02, d * 0.7), p.r(0.12, 0.22), 0.06, 6)
            x += bw
        y += ch
    # a crack zig-zagging down through a few courses
    cx, cy = p.r(-w / 3, w / 3), h - 0.5
    for _ in range(6):
        nx, ny = cx + p.r(-0.25, 0.25), cy - p.r(0.3, 0.5)
        p.box("dark", ((cx + nx) / 2, (cy + ny) / 2, 0.17), (0.04, abs(cy - ny) + 0.05, 0.02), (0, 0, math.atan2(nx - cx, cy - ny)))
        cx, cy = nx, ny
    # coping: stones along the top, one missing and one knocked askew
    x = -w / 2
    k = 0
    gap = p.rng.randrange(1, 4)
    while x < w / 2 - 0.1:
        cw = min(p.r(0.8, 1.1), w / 2 - x)
        if k != gap:
            tilt = (p.r(-0.06, 0.06), 0, p.r(-0.04, 0.04)) if k == gap + 1 else (0, 0, 0)
            p.box("stone", (x + cw / 2, h + 0.1, 0.12), (cw - 0.03, 0.22, 0.55), tilt)
        else:
            p.lump("moss", (x + cw / 2, h - 0.02, 0.1), 0.3, 0.12, 6)
        x += cw
        k += 1
    # moss and grass along the foot
    for i in range(5):
        p.lump("moss", (p.r(-w / 2 + 0.3, w / 2 - 0.3), 0.0, p.r(0.15, 0.35)), p.r(0.2, 0.38), p.r(0.08, 0.14), 7)


def flags_roots(p, size=3.2, roots=3):
    """A patch of old flagstones heaved, tilted and split by roots that splay out from the trunk (the -z edge)
    across it, thick where they leave the trunk, diving under the stones and arching out again."""
    paths = []
    for k in range(roots):
        x0, x1 = p.r(-0.5, 0.5), p.r(-size / 2, size / 2)
        bulge = p.r(1.4, 2.6)
        pts = []
        for i in range(9):
            t = i / 8
            x = x0 + (x1 - x0) * t + math.sin(t * 4 + k * 2) * 0.16
            z = -size / 2 - 0.7 + t * (size + 0.7)
            y = math.sin(t * math.pi * bulge) * 0.16 + max(0.0, 0.3 - t) * 0.6 - 0.04
            pts.append((x, y, z))
        paths.append(pts)
        p.tube("root", pts, p.r(0.24, 0.32), 0.05, 7)
    n = 5
    cell = size / n
    for i in range(n):
        for j in range(n):
            cx, cz = -size / 2 + (i + 0.5) * cell, -size / 2 + (j + 0.5) * cell
            near = min(math.hypot(cx - x, cz - z) for pts in paths for x, _, z in pts)
            if near < 0.3 and p.rng.random() < 0.65:
                continue                                         # broken away where a root breaks through
            lift = max(0.0, 0.55 - near)
            tilt = (p.r(-0.04, 0.04) + lift * p.r(-0.7, 0.7), p.r(-0.1, 0.1), p.r(-0.04, 0.04) + lift * p.r(-0.7, 0.7))
            p.box("stone", (cx + p.r(-0.04, 0.04), 0.05 + lift * 0.35, cz + p.r(-0.04, 0.04)), (cell - 0.07, 0.1, cell - 0.07), tilt)
            if p.rng.random() < 0.4:
                p.lump("moss", (cx + p.r(-0.15, 0.15), 0.05, cz + p.r(-0.15, 0.15)), p.r(0.08, 0.14), 0.05, 6)
    for k in range(5):
        p.blade("grass", (p.r(-1.2, 1.2), 0.02, p.r(-1.2, 1.2)), (p.r(-1, 1), p.r(-1, 1)), p.r(0.3, 0.5), 0.08, 0.1, 3)


def brick_arch(p):
    """The old overflow arch of the baths' cistern, bricked up; the keystone and a hole of bricks have fallen out,
    and that is where the falls burst from (the hole is at local (0, 1.2))."""
    r0, r1, spring = 1.2, 1.65, 0.9
    n = 11
    for i in range(n):
        if i == n // 2:
            continue                                             # the keystone is gone
        a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
        a0 += 0.012
        a1 -= 0.012
        pts = [(math.cos(a0) * r0, spring + math.sin(a0) * r0), (math.cos(a1) * r0, spring + math.sin(a1) * r0),
               (math.cos(a1) * r1, spring + math.sin(a1) * r1), (math.cos(a0) * r1, spring + math.sin(a0) * r1)]
        d = 0.24
        verts = [(x, y, 0.0) for x, y in pts] + [(x, y, d) for x, y in pts]
        faces = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
        p.mesh("stone", verts, faces)
    for side in (-1, 1):
        y = 0.0
        while y < spring - 0.05:
            ch = min(0.32, spring - y)
            p.box("stone", (side * (r0 + r1) / 2, y + ch / 2, 0.12), (r1 - r0 - 0.03, ch - 0.03, 0.24))
            y += ch
    p.box("dark", (0, spring + r0 + 0.12, 0.05), (0.28, 0.3, 0.06))                   # where the keystone was

    def outside(x, y):
        inside_arch = y < spring and abs(x) < r0 or (y >= spring and x * x + (y - spring) ** 2 < r0 * r0)
        hole = (x / 0.55) ** 2 + ((y - 1.2) / 0.42) ** 2 < 1
        return not inside_arch or hole
    bricks(p, -r0, r0, 0.0, spring + r0, 0.0, outside)
    hole = []
    for i in range(14):
        a = 2 * math.pi * i / 14
        hole.append((math.cos(a) * 0.55 * p.r(0.85, 1.1), 1.2 + math.sin(a) * 0.42 * p.r(0.85, 1.1), 0.005))
    p.mesh("dark", [(0, 1.2, 0.005)] + hole, [(0, 1 + i, 1 + (i + 1) % 14) for i in range(14)], solid=False)
    for x, y in ((-0.9, 0.05), (0.8, 0.0), (1.9, 0.0), (-1.95, 1.0)):
        p.lump("moss", (x, y, 0.2), p.r(0.18, 0.3), 0.1, 6)
    for k in range(3):
        p.box("brick", (p.r(-0.8, 0.8), 0.06, p.r(0.4, 1.0)), (0.26, 0.1, 0.12), (0, p.r(0, 3), p.r(-0.3, 0.3)))   # fallen bricks


def rubble(p):
    for k in range(6):
        s = p.r(0.25, 0.5)
        p.box("stone", (p.r(-0.5, 0.5), s * 0.4, p.r(-0.4, 0.4)), (s * 1.3, s * 0.8, s), (p.r(-0.4, 0.4), p.r(0, 3), p.r(-0.4, 0.4)))
    for k in range(5):
        p.box("brick", (p.r(-0.6, 0.6), 0.05, p.r(-0.5, 0.5)), (0.26, 0.1, 0.12), (0, p.r(0, 3), p.r(-0.2, 0.2)))
    p.lump("moss", (0.2, 0.1, 0.1), 0.3, 0.12, 6)


def coping_fallen(p):
    p.box("stone", (0, 0.2, 0), (1.0, 0.22, 0.55), (0.35, 0.4, 0.15))
    p.box("stone", (0.8, 0.11, 0.4), (0.7, 0.22, 0.5), (0, -0.5, 0.05))
    p.lump("moss", (0.4, 0.15, 0.2), 0.25, 0.1, 6)
    p.blade("grass", (-0.3, 0.0, 0.4), (-0.4, 0.6), 0.45, 0.1, 0.1, 3)


def ivy_curtain(p, w=2.4, h=2.6, strands=11):
    """Ivy hanging from a wall top (local y = 0) down the face (z just in front of it)."""
    for s in range(strands):
        x = -w / 2 + (s + p.r(0.2, 0.8)) * w / strands
        length = h * p.r(0.4, 1.0)
        y = 0.05
        z = 0.06
        while y > -length:
            role = "leaf" if p.rng.random() < 0.6 else "leaf-b"
            size = p.r(0.16, 0.28)
            p.box(role, (x + p.r(-0.08, 0.08), y, z + p.r(0.0, 0.06)), (size, size * 0.8, 0.03), (p.r(-0.3, 0.3), 0, p.r(-0.8, 0.8)))
            y -= p.r(0.12, 0.2)
    for k in range(6):
        p.box("leaf", (p.r(-w / 2, w / 2), 0.08, p.r(-0.25, 0.1)), (0.3, 0.06, 0.3), (0, p.r(0, 3), 0))     # spilling over the top


def moss(p, big):
    for k in range(4 if big else 3):
        p.lump("moss", (p.r(-0.35, 0.35), 0, p.r(-0.35, 0.35)), p.r(0.18, 0.34) * (1.3 if big else 1), p.r(0.08, 0.16), 7)


def fern(p):
    for k in range(8):
        a = 2 * math.pi * k / 8 + p.r(-0.2, 0.2)
        p.blade("fern", (0, 0.02, 0), (math.cos(a), math.sin(a)), p.r(0.55, 0.85), 0.2, 0.35, 5)
    p.lump("moss", (0, 0, 0), 0.15, 0.08, 6)


def tuft(p):
    for k in range(9):
        a = 2 * math.pi * k / 9 + p.r(-0.3, 0.3)
        p.blade("grass", (math.cos(a) * 0.05, 0.0, math.sin(a) * 0.05), (math.cos(a) * 0.4, math.sin(a) * 0.4), p.r(0.3, 0.55), 0.07, 0.05, 3)
    for k in range(3):
        role = "flower" if k % 2 == 0 else "flower-b"
        p.box(role, (p.r(-0.15, 0.15), p.r(0.3, 0.45), p.r(-0.15, 0.15)), (0.08, 0.06, 0.08))


def rivulet(p, h=4.0):
    """A thin trickle down a wall face (z = 0.03), a wet streak either side, a splash puddle at the foot."""
    verts, faces = [], []
    n = 10
    for i in range(n + 1):
        y = h - h * i / n
        x = math.sin(i * 1.3) * 0.06
        w = 0.07 + 0.05 * (i / n)
        verts += [(x - w, y, 0.035), (x + w, y, 0.035)]
    for i in range(n):
        faces.append((2 * i, 2 * i + 2, 2 * i + 3, 2 * i + 1))
    p.mesh("trickle", verts, faces, solid=False)
    p.flat("water", (0.05, 0.35), 0.45, 0.015, 12, 0.35, 0.7)
    p.lump("moss", (-0.35, 0, 0.15), 0.2, 0.08, 6)
    p.lump("moss", (0.4, 0, 0.12), 0.16, 0.06, 6)


def build(kit):
    root = kit.root("corner-kit")
    materials = {role: kit.material("corner-" + role, color=kit.srgb_to_linear(col)) for role, col in COLOURS.items()}
    makers = {
        "wall-crumble-a": (11, lambda p: crumbling_wall(p, crumble=0.14)),
        "wall-crumble-b": (23, lambda p: crumbling_wall(p, crumble=0.26)),
        "brick-arch": (5, brick_arch),
        "flags-roots-a": (7, lambda p: flags_roots(p, roots=2)),
        "flags-roots-b": (19, lambda p: flags_roots(p, roots=3)),
        "rubble-a": (3, rubble),
        "coping-fallen": (4, coping_fallen),
        "ivy-curtain-a": (8, lambda p: ivy_curtain(p)),
        "ivy-curtain-b": (13, lambda p: ivy_curtain(p, 1.8, 3.4, 8)),
        "moss-a": (2, lambda p: moss(p, False)),
        "moss-b": (9, lambda p: moss(p, True)),
        "fern-a": (6, fern),
        "tuft-a": (12, tuft),
        "puddle-a": (14, lambda p: p.flat("water", (0, 0), 0.7, 0.015, 16, 0.3, 0.7)),
        "puddle-b": (15, lambda p: p.flat("water", (0, 0), 1.0, 0.015, 18, 0.35, 0.55)),
        "rivulet-a": (16, rivulet),
    }
    for name, (seed, body) in makers.items():
        p = Piece(kit, root, name, seed)
        body(p)
        p.finish(materials)
    return root, 4
