"""The Spirit Pools as a hidden grotto (designer direction, 2026-10-26): the falls' water breaks in through a crack
high in the back wall, rests in three natural pools and leaves along the front as an underground river, out of the
city. Old Stoic's roots come down the left wall; worn steps climb the back-right corner to a doorway (the cave mouth
in the courtyard); moss, vines, ferns and pale glowing mushrooms everywhere.
Concept: art/reference/concepts/pools-grotto-concept.png. The pools, their water, the cascade, the spill between
the pools and the river's water are the game's (PoolsScene); this kit is the cave around them, plus the druids'
things from pools_room.py (crock, bag, ledger, stool, rake), unchanged.

Parts: shell (rock, roots, steps), decor (vines, mushrooms, moss, pebbles), ferns (sway), crock, bag, ledger, stool,
rake. Rock and moss are painted procedurally (no new images); the roots use Old Stoic's bark texture.
Game (X, Y, Z) is Blender (X, -Z, Y).
"""
import math
import random

import numpy as np
from mathutils import Vector

from assets import pools_room as base
from assets.pools_room import Acc, BoxUV, B, frond, leaf, mound, tube

PRE = "pools-room-"
WALL_IN = 5.4                      # the back and left walls (game z = -5.4, x = -5.4)
EDGE = 5.6                         # the open cutaway edges
CRACK_X = 0.75 - 0.28 * min(max(1.3 + 0.34 * 8, 2.3), 4.1)   # where the cascade leaves the back wall (PoolsScene)
RIVER = (3.83, 5.28, -0.6)         # the channel under PoolsScene's river water (z from, z to, bed depth)
DOOR = (3.95, 5.25, 3.0, 5.0)      # the doorway at the top of the steps (x from, x to, sill y, head y)
STEPS = (3.95, 5.25, -2.3, 10)     # x from, x to, the first step's front z, number of 0.3 m steps


def painted(kit, name, base_rgb, dark_rgb, light_rgb, seed, size=256, cracks=True):
    """A tileable painted texture: soft blotches in a few flat tones, plus dark cracks (sRGB 0..1)."""
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:size, 0:size] / size

    def noise(octaves):
        n = np.zeros((size, size))
        for f, a in octaves:
            for _ in range(3):
                fx, fy = rng.integers(-f, f + 1, 2)
                n += a * np.sin(2 * np.pi * (fx * x + fy * y) + rng.uniform(0, 6.28))
        return (n - n.min()) / (n.max() - n.min())

    n = noise([(2, 1.0), (4, 0.5), (8, 0.25)])
    tone = np.digitize(n, [0.35, 0.68])                          # three flat tones, like gouache
    cols = np.array([dark_rgb, base_rgb, light_rgb])
    img = cols[tone]
    if cracks:
        r = noise([(3, 1.0), (6, 0.4)])
        img[np.abs(r - 0.5) < 0.012] *= 0.55
    img = np.dstack([img, np.ones((size, size))])
    return kit._image_from_array(PRE + name, img)


def surface(acc, P, nu, nv, mat_of, out, uv):
    """A grid surface: P(u, v) -> game point; each quad's material from mat_of(game point, game normal)."""
    g = [[P(i / nu, j / nv) for j in range(nv + 1)] for i in range(nu + 1)]
    for i in range(nu):
        for j in range(nv):
            q = [g[i][j], g[i + 1][j], g[i + 1][j + 1], g[i][j + 1]]
            c = sum(q, Vector()) / 4
            n = (q[1] - q[0]).cross(q[3] - q[0])
            if n.dot(out(c)) < 0:
                n = -n
            n = n.normalized() if n.length > 1e-9 else Vector((0, 1, 0))
            acc.face([B(*p) for p in q], mat_of(c, n), uvs=uv, out=B(*n) - B(0, 0, 0))


def build(kit):
    rng = random.Random(2610)
    M = base.Materials(kit, kit.load_sheet(base.SHEET), kit.load_sheet(base.HOUSE))
    lin = kit.srgb_to_linear
    rock = kit.material(PRE + "rock", painted(kit, "rock", (0.50, 0.46, 0.39), (0.44, 0.40, 0.34), (0.55, 0.51, 0.43), 1))
    rock_wet = kit.material(PRE + "rock-wet", painted(kit, "rock-wet", (0.36, 0.38, 0.34), (0.31, 0.33, 0.30), (0.41, 0.43, 0.38), 2))
    moss = kit.material(PRE + "moss", painted(kit, "moss", (0.42, 0.52, 0.25), (0.31, 0.40, 0.19), (0.55, 0.63, 0.32), 3, cracks=False))
    deep = kit.material(PRE + "deep", color=lin((0.06, 0.08, 0.07)))
    warm = kit.material(PRE + "doorway-light", color=lin((0.93, 0.78, 0.50)))
    bark = kit.material(PRE + "bark", kit.texture(PRE + "bark", "stoic-bark.png", mirror="v", step=2))
    cap_mats = [kit.material(PRE + "mushroom-" + n, color=lin(c)) for n, c in (("teal", (0.62, 0.92, 0.86)), ("cream", (0.93, 0.90, 0.74)))]
    stem = kit.material(PRE + "mushroom-stem", color=lin((0.86, 0.84, 0.74)))
    root = kit.root("pools-room")
    uv = BoxUV(2.4)

    def bump(a, b, k=1.0):
        return (math.sin(a * 2.3 + b * 0.7) * 0.5 + math.sin(a * 5.1 - b * 3.3) * 0.25 + math.sin(b * 4.4 + a) * 0.25) * k

    def rocky(c, n):
        return rock  # moss comes as organic mounds (decor), never per grid square

    shell = Acc()
    # ── the floor, with the river's channel carved along the front ──
    z0, z1, bed = RIVER

    def floor(u, v):
        x, z = -EDGE + 2 * EDGE * u, -EDGE + 2 * EDGE * v
        y = bump(x, z, 0.035)
        if z > z0 - 0.3:
            t = min(1.0, (z - (z0 - 0.3)) / 0.35) if z < z1 else max(0.0, 1 - (z - z1) / 0.3)
            y = y * (1 - t) + bed * t
        return Vector((x, y, z))

    surface(shell, floor, 34, 40, lambda c, n: rock_wet if c.y < -0.2 else rocky(c, n), lambda c: Vector((0, 1, 0)), uv)

    # ── the back wall: rough rock leaning in at the top; the crack for the falls; the doorway ──
    def back(u, v):
        x, y = -EDGE + 2 * EDGE * u, -0.7 + 7.9 * v
        z = -WALL_IN + bump(x, y, 0.32) + max(0.0, y - 5.0) * 0.55
        if abs(x - CRACK_X) < 0.42 and 4.7 < y < 6.6:
            z -= 0.7 * (1 - abs(x - CRACK_X) / 0.42)
        if DOOR[0] < x < DOOR[1] and DOOR[2] < y < DOOR[3]:
            z -= 1.4
        return Vector((x, y, z))

    def back_mat(c, n):
        if c.z < -WALL_IN - 0.45:
            return deep
        return rock_wet if abs(c.x - CRACK_X) < 1.0 and c.y < 5.6 else rock

    surface(shell, back, 46, 34, back_mat, lambda c: Vector((0, 0, 1)), uv)
    # light through the doorway, at the back of its recess
    shell.face([B(DOOR[0], DOOR[2], -WALL_IN - 1.3), B(DOOR[1], DOOR[2], -WALL_IN - 1.3), B(DOOR[1], DOOR[3], -WALL_IN - 1.3),
                B(DOOR[0], DOOR[3], -WALL_IN - 1.3)], warm, out=B(0, 0, 1) - B(0, 0, 0))

    # ── the left wall, with the tunnel where the river leaves ──
    def left(u, v):
        z, y = -EDGE + 2 * EDGE * u, -0.7 + 7.9 * v
        x = -WALL_IN + bump(z, y, 0.32) + max(0.0, y - 5.0) * 0.55
        if z > z0 - 0.1 and y < 1.3:
            x -= 1.5 * min(1.0, (1.3 - y) / 0.5)
        return Vector((x, y, z))

    surface(shell, left, 40, 34, lambda c, n: deep if c.x < -WALL_IN - 1.0 else rock,
            lambda c: Vector((1, 0, 0)), uv)

    # ── worn steps up the back-right corner to the doorway ──
    sx0, sx1, sz, n = STEPS
    for i in range(n):
        za, zb = sz - (i + 1) * 0.29, sz - i * 0.29
        top = (i + 1) * DOOR[2] / n
        lo, hi = Vector((sx0, -0.6, za)), Vector((sx1, top, zb))
        corners = [Vector((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]
        for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
            q = [corners[k] for k in f]
            c = sum(q, Vector()) / 4
            out = c - (lo + hi) / 2
            shell.face([B(*p) for p in q], moss if out.y > 0 and rng.random() < 0.35 else rock, uvs=uv, out=B(*out) - B(0, 0, 0))

    # ── Old Stoic's roots: down the left wall, over the floor around the second pool ──
    def root_tube(ctrl, r0, r1, seg=14, sides=8):
        def bez(t):
            a, b, c, d = ctrl
            s = 1 - t
            return a * s ** 3 + b * 3 * s * s * t + c * 3 * s * t * t + d * t ** 3
        pts = [bez(i / seg) for i in range(seg + 1)]
        rings = []
        for i, p in enumerate(pts):
            tng = (pts[min(i + 1, seg)] - pts[max(i - 1, 0)]).normalized()
            ref = Vector((0, 1, 0)) if abs(tng.y) < 0.9 else Vector((1, 0, 0))
            a = ref.cross(tng).normalized()
            b = tng.cross(a).normalized()
            r = r0 + (r1 - r0) * (i / seg)
            rings.append([p + (a * math.cos(2 * math.pi * k / sides) + b * math.sin(2 * math.pi * k / sides)) * r * (1 + 0.12 * math.sin(i * 1.7 + k))
                          for k in range(sides)])
        length = sum((pts[i + 1] - pts[i]).length for i in range(seg))
        for i in range(seg):
            for k in range(sides):
                k1 = (k + 1) % sides
                q = [rings[i][k], rings[i][k1], rings[i + 1][k1], rings[i + 1][k]]
                uvq = [(k / sides, i / seg * length / 1.6), (k1 / sides if k1 else 1.0, i / seg * length / 1.6),
                       (k1 / sides if k1 else 1.0, (i + 1) / seg * length / 1.6), (k / sides, (i + 1) / seg * length / 1.6)]
                c = sum(q, Vector()) / 4
                shell.face([B(*p) for p in q], bark, uvs=uvq, out=B(*(c - (pts[i] + pts[i + 1]) / 2)) - B(0, 0, 0), smooth=True)

    V = Vector
    roots = [
        ((V((-5.2, 7.1, -2.6)), V((-4.9, 4.0, -2.4)), V((-4.9, 0.6, -2.0)), V((-3.9, 0.1, -1.7))), 0.42, 0.12),
        ((V((-5.2, 7.1, -0.6)), V((-4.7, 4.6, -0.4)), V((-5.0, 1.2, 0.3)), V((-4.3, 0.1, 1.2))), 0.5, 0.14),
        ((V((-5.2, 7.1, 1.4)), V((-4.9, 4.4, 1.6)), V((-5.0, 1.0, 2.0)), V((-3.6, 0.05, 2.3))), 0.38, 0.1),
        ((V((-4.6, 7.0, -4.9)), V((-3.2, 6.4, -4.9)), V((-2.2, 5.4, -5.0)), V((-1.6, 4.4, -5.1))), 0.3, 0.08),
        ((V((-5.1, 4.0, -1.0)), V((-4.4, 1.6, -1.2)), V((-4.6, 0.4, -0.8)), V((-4.3, 0.1, -0.1))), 0.22, 0.07),
    ]
    for ctrl, r0, r1 in roots:
        root_tube(ctrl, r0, r1)
    shell.build("shell", root)

    # ── decor: vines from the wall tops, glowing mushrooms, moss and pebbles ──
    decor = Acc()
    for i in range(16):
        on_back = i < 10
        x = -4.6 + i * 0.95 if on_back else -5.15
        z = -5.15 if on_back else -4.4 + (i - 10) * 1.3
        if on_back and (abs(x - CRACK_X) < 0.7 or DOOR[0] - 0.3 < x < DOOR[1] + 0.3):
            continue
        top, length = rng.uniform(5.6, 6.6), rng.uniform(1.8, 3.6)
        pts = [Vector((x + math.sin(k * 0.9 + i) * 0.08, top - length * k / 8, z)) for k in range(9)]
        tube(decor, [B(*p) for p in pts], 0.025, M.vine, sides=3)
        for k in range(1, 9):
            p = pts[k]
            nrm = B(0, 0, 1) - B(0, 0, 0) if on_back else B(1, 0, 0) - B(0, 0, 0)
            leaf(decor, B(p.x + (0.08 if k % 2 else -0.08), p.y, p.z + (0.04 if on_back else 0)), nrm,
                 B(0, -1, 0) - B(0, 0, 0), 0.13 + rng.random() * 0.05, M.ivy[k % len(M.ivy)])
    shrooms = [(-3.8, 0.12, -1.4), (-4.1, 0.1, 1.5), (-3.3, 0.06, 2.5), (-4.6, 0.6, -2.9), (2.9, 0.05, 1.5), (5.0, 0.05, -1.9),
               (-1.2, 0.05, -2.0), (4.2, 0.05, 2.9)]
    for cx, cy, cz in shrooms:
        for k in range(rng.randint(3, 5)):
            a = rng.uniform(0, 6.28)
            p = Vector((cx + math.cos(a) * rng.uniform(0, 0.25), cy, cz + math.sin(a) * rng.uniform(0, 0.25)))
            h, r = rng.uniform(0.08, 0.22), rng.uniform(0.04, 0.09)
            tube(decor, [B(p.x, p.y, p.z), B(p.x, p.y + h, p.z)], r * 0.3, stem, sides=5)
            mound(decor, B(p.x, p.y + h, p.z), r, r, r * 0.6, cap_mats[k % 2], rng, seg=7)
    for cx, cz, rx, rz in ((-4.6, 3.1, 0.7, 0.45), (4.7, 1.9, 0.8, 0.5), (-0.6, -1.3, 0.6, 0.4), (2.5, 3.0, 0.7, 0.4), (-2.6, 2.9, 0.5, 0.35),
                           (-4.4, -3.9, 0.7, 0.5), (0.6, 1.9, 0.5, 0.35), (-3.9, 1.0, 0.6, 0.4), (3.6, 2.2, 0.45, 0.3), (-1.0, 3.2, 0.5, 0.3)):
        mound(decor, B(cx, 0.0, cz), rx, rz, 0.1, moss, rng, seg=9)
    for i in range(26):
        x, z = rng.uniform(-4.8, 5.2), rng.uniform(1.4, 3.6)
        mound(decor, B(x, 0.0, z), rng.uniform(0.05, 0.12), rng.uniform(0.04, 0.1), rng.uniform(0.03, 0.07), rock, rng, seg=6)
    decor.build("decor", root)

    # ── ferns growing out of the rock (sway) ──
    ferns = Acc()
    for (x, y, z) in ((-4.7, 0.0, 3.2), (5.1, 0.0, -1.95), (-1.4, 1.0, -5.0), (2.4, 1.2, -5.05), (-4.9, 2.6, -3.6), (5.0, 0.0, 3.2)):
        b = B(x, y, z)
        off = rng.uniform(0, 6.28)
        for k in range(7):
            yaw = off + k * 2 * math.pi / 7 + rng.uniform(-0.2, 0.2)
            frond(ferns, b + Vector((math.cos(yaw), math.sin(yaw), 0)) * 0.05, yaw, rng.uniform(0.55, 0.75), rng.uniform(0.35, 0.5),
                  rng.uniform(0.3, 0.45), rng.uniform(0.11, 0.14), 6, M.fern[k % 3], roll=rng.uniform(-0.35, 0.35))
    ferns.build("ferns", root)

    # ── the druids' things, as in the stone room ──
    base.build_crock(M, root)
    base.build_bag(M, root, rng)
    base.build_ledger(M, root)
    base.build_stool(M, root)
    base.build_rake(M, root)
    return root, 7.2
