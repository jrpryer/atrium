"""The Spirit Pools as the Travertine Well (designer's choice, 2026-11-01, finish "Travertine storybook ink"): a deep
shaft under the city whose pools step down three travertine terraces, from the cave mouth to a wet stone floor, under
a noon beam from an oculus out of sight. References: art/reference/concepts/rooms-fresh/travertine/
(finish-travertine-ink-a.jpg is the look, master-blockout-nano-a.jpg the placement, kit-travertine.jpg the forms).
Every number comes from travertine_layout.py; the parts and markers are the contract PoolsScene reads (see
tools/blender/README.md).

This module builds the shell part (terraces with banded flowstone dam faces, the landing, grey-blue drape walls,
the cave mouth and its tunnel, the low outflow arch in its mass of rock, the wet floor with its soft rocky front,
the shallow runnel and outflow channels, the right-wall alcoves, Old Stoic's wall-hugging roots and the root stair,
and the holes the bowls sit in), decor (moss strips and mounds, pebbles, boulders), ferns (wild clumps), the beam part
and every marker. The bowls, the lantern, the druids' things and
the small decor come from travertine_props.py (pools_room.py's props stand in while it is missing). Stone, moss
and floor are painted procedurally; the roots wear stoic-bark.png. Geometry stays smooth and chunky so the
inverted-hull ink stays clean at 11-16 m; the detail is painted.
Game (X, Y, Z) is Blender (X, -Z, Y).
"""
import bisect
import math
import random

import numpy as np
from mathutils import Vector

from assets import pools_grotto
from assets import pools_room as base
from assets import travertine_layout as L
from assets.pools_room import Acc, B, BoxUV, frond, mound

try:  # the bowls, the lantern, the druids' things and the small decor; the shell builds without them
    from assets import travertine_props as props
except (ImportError, SyntaxError) as err:
    print(f"pools-travertine: travertine_props unavailable ({err}); pools_room's props stand in")
    props = None

PRE = "pools-room-tv-shell-"
UP = Vector((0.0, 1.0, 0.0))
TOP_TILE, DAM_TILE, WALL_TILE, FLOOR_TILE, CHAN_TILE = 2.4, (2.2, 1.6), (3.2, 4.0), 2.6, 1.4


def G(p):
    """A game point or direction in Blender coordinates."""
    return B(p[0], p[1], p[2])


def V(x, y, z):
    return Vector((x, y, z))


def _api(name):
    return getattr(props, name, None) if props is not None else None


def _offsets(path, closed=False):
    """travertine_props.sweep's mitred horizontal normals (right of an open path), for the holes its channels cut."""
    n = len(path)
    flat = []
    for i in range(n if closed else n - 1):
        s = path[(i + 1) % n] - path[i]
        h = Vector((s.x, s.y, 0.0))
        flat.append(h.normalized() if h.length > 1e-6 else None)
    last = next((f for f in flat if f is not None), Vector((1.0, 0.0, 0.0)))
    for k, f in enumerate(flat):
        flat[k] = last = f if f is not None else last
    offs = []
    for i in range(n):
        ta, tb = (flat[i - 1], flat[i]) if closed else (flat[max(i - 1, 0)], flat[min(i, n - 2)])
        t = ta + tb
        t = t.normalized() if t.length > 1e-6 else tb
        nrm = Vector((t.y, -t.x, 0.0))
        offs.append(nrm / max(nrm.dot(Vector((tb.y, -tb.x, 0.0))), 0.5))
    return offs


def _sweep(acc, profile, path, mat, closed=True, uv_scale=1.0, smooth=True):
    """The same contract as travertine_props.sweep (used when that module is missing): a (d, h) profile, listed
    anticlockwise round the solid, set off d along the path's horizontal normal (right of an open path) and h up."""
    P = [Vector(p) for p in path]
    n, m = len(P), len(profile)
    offs = _offsets(P, closed)
    up = Vector((0.0, 0.0, 1.0))
    rings = [[P[i] + offs[i] * d + up * h for d, h in profile] for i in range(n)]
    for i in range(n if closed else n - 1):
        i1 = (i + 1) % n
        side = (offs[i] + offs[i1]).normalized()
        for k in range(m - 1):
            (d0, h0), (d1, h1) = profile[k], profile[k + 1]
            q = [rings[i][k], rings[i1][k], rings[i1][k + 1], rings[i][k + 1]]
            acc.face(q, mat, BoxUV(uv_scale), out=side * (h1 - h0) - up * (d1 - d0), smooth=smooth)


sweep = _api("sweep") or _sweep


# ───────────────────────── painted textures (sRGB, tileable: integer waves only) ─────────────────────────

def _field(rng, size, terms):
    """Smooth noise in 0..1 that tiles: `terms` are (max x waves, max y waves, amplitude); y waves 0 makes streaks."""
    y, x = np.mgrid[0:size, 0:size] / size
    n = np.zeros((size, size))
    for fx_max, fy_max, amp in terms:
        for _ in range(3):
            fx = int(rng.integers(-fx_max, fx_max + 1)) if fx_max else 0
            fy = int(rng.integers(-fy_max, fy_max + 1)) if fy_max else 0
            if fx == 0 and fy == 0:
                fx, fy = (1, 0) if fx_max else (0, 1)
            n += amp * np.sin(2 * np.pi * (fx * x + fy * y) + rng.uniform(0, 6.28))
    return (n - n.min()) / (n.max() - n.min() + 1e-9)


def _tones(n, cuts, cols):
    return np.array(cols, dtype=np.float64)[np.digitize(n, cuts)]


def _image(kit, name, rgb):
    return kit._image_from_array(PRE + name, np.dstack([np.clip(rgb, 0, 1), np.ones(rgb.shape[:2])]))


def paint_top(kit, size=256):
    """Terrace tops: cream travertine in three flat tones, peach nodules, a few faint cracks. A step darker and greyer
    than the dams: the noon sun strikes the tops head-on and would wash a light cream out to pale yellow."""
    rng = np.random.default_rng(31)
    img = _tones(_field(rng, size, [(2, 2, 1.0), (4, 4, 0.5), (8, 8, 0.25)]), [0.34, 0.7],
                 [(0.74, 0.70, 0.66), (0.80, 0.76, 0.71), (0.85, 0.82, 0.77)])
    nod = _field(rng, size, [(9, 9, 1.0), (13, 13, 0.6)]) > 0.8
    img[nod] = img[nod] * 0.55 + np.array((0.82, 0.70, 0.61)) * 0.45
    r = _field(rng, size, [(3, 3, 1.0), (6, 6, 0.4)])
    img[np.abs(r - 0.5) < 0.006] *= 0.86
    return _image(kit, "top", img)


def paint_dam(kit, size=256):
    """Flowstone dam faces: wavy horizontal strata of cream, peach and tan (v runs up the face, one period per tile),
    a darker line where bands meet, pale drip streaks running down and a scatter of light nodules."""
    rng = np.random.default_rng(32)
    U, Vv = np.meshgrid((np.arange(size) + 0.5) / size, 1.0 - (np.arange(size) + 0.5) / size)
    wob = 0.03 * np.sin(4 * np.pi * U + 1.3) + 0.018 * np.sin(10 * np.pi * U + 4.1) + 0.01 * np.sin(18 * np.pi * U + 0.4)
    b = np.mod(Vv + wob, 1.0)
    edges = np.array([0.13, 0.21, 0.38, 0.47, 0.66, 0.74, 0.9])
    pal = [(0.92, 0.86, 0.74), (0.90, 0.75, 0.60), (0.95, 0.91, 0.82), (0.86, 0.73, 0.58), (0.93, 0.88, 0.77),
           (0.91, 0.79, 0.65), (0.95, 0.90, 0.80), (0.92, 0.86, 0.74)]
    img = np.array(pal)[np.digitize(b, edges)]
    img[np.min(np.abs(b[..., None] - edges), axis=-1) < 0.006] *= 0.84
    s = _field(rng, size, [(12, 0, 1.0), (24, 1, 0.5)])
    img[s > 0.72] = img[s > 0.72] * 0.7 + 0.3 * np.array((0.97, 0.95, 0.89))
    img[s < 0.2] *= 0.95
    img[_field(rng, size, [(11, 11, 1.0), (17, 17, 0.5)]) > 0.85] *= 1.04
    return _image(kit, "dam", img)


def paint_wall(kit, size=256):
    """The shaft walls: grey-blue, streaked down the drapes, with a few dark creases between them."""
    rng = np.random.default_rng(33)
    img = _tones(_field(rng, size, [(5, 0, 1.0), (11, 0, 0.6), (2, 1, 0.2)]), [0.3, 0.66],
                 [(0.51, 0.56, 0.62), (0.58, 0.63, 0.69), (0.66, 0.70, 0.75)])
    img[np.abs(_field(rng, size, [(7, 0, 1.0), (13, 0, 0.4)]) - 0.5) < 0.01] *= 0.86
    return _image(kit, "wall", img)


def paint_floor(kit, size=256):
    """The well floor: calm wet stone, a dark blue-grey drifting softly in tone, with soft damp patches darker still
    (smooth ramps, no hard-edged blotches)."""
    rng = np.random.default_rng(34)
    n = _field(rng, size, [(2, 2, 1.0), (4, 4, 0.5), (9, 9, 0.2)])[..., None]
    img = np.array((0.34, 0.39, 0.44)) + n * np.array((0.08, 0.08, 0.08))
    damp = np.clip((_field(rng, size, [(3, 3, 1.0), (6, 6, 0.4)]) - 0.55) / 0.3, 0.0, 1.0)[..., None]
    damp = damp * damp * (3 - 2 * damp) * 0.6
    return _image(kit, "floor", img * (1 - damp) + damp * np.array((0.27, 0.33, 0.40)))


def paint_plain(kit, name, cols, seed, size=128):
    """Soft blotches in three tones (the runnel's bed, the tunnels)."""
    rng = np.random.default_rng(seed)
    return _image(kit, name, _tones(_field(rng, size, [(2, 2, 1.0), (5, 5, 0.4)]), [0.33, 0.7], cols))


class Stone:
    """The shell's materials (names 'pools-room-tv-shell-*', so nothing clashes with travertine_props)."""

    def __init__(self, kit):
        lin = kit.srgb_to_linear
        mat = lambda n, img=None, col=(0.8, 0.8, 0.8), em=None: kit.material(PRE + n, img, col, em)
        self.top = mat("top", paint_top(kit))
        self.dam = mat("dam", paint_dam(kit))
        self.wall = mat("wall", paint_wall(kit))
        self.floor = mat("floor", paint_floor(kit))
        self.channel = mat("channel", paint_plain(kit, "channel", [(0.70, 0.67, 0.59), (0.77, 0.73, 0.64),
                                                                  (0.82, 0.79, 0.70)], 35))
        self.tunnel = mat("tunnel", paint_plain(kit, "tunnel", [(0.46, 0.48, 0.51), (0.54, 0.56, 0.59),
                                                               (0.61, 0.63, 0.66)], 36))
        self.moss = mat("moss", pools_grotto.painted(kit, "tv-shell-moss", (0.45, 0.58, 0.26), (0.34, 0.46, 0.19),
                                                     (0.57, 0.68, 0.32), 37, cracks=False))
        self.bark = mat("bark", kit.texture(PRE + "bark", "stoic-bark.png", mirror="v", step=2))
        self.tread = mat("tread", col=lin((0.58, 0.43, 0.29)))
        self.pebble = mat("pebble", col=lin((0.56, 0.60, 0.64)))
        self.daylight = mat("daylight", col=lin((0.97, 0.95, 0.88)), em=lin((0.97, 0.95, 0.88)))
        self.deep = mat("deep", col=lin((0.12, 0.14, 0.15)))
        self.beam = mat("beam", col=lin((1.0, 0.93, 0.70)))


# ───────────────────────── flat tops: polygons minus holes ─────────────────────────

def _edges(polys):
    out = []
    for poly in polys:
        for a, b in zip(poly, poly[1:] + poly[:1]):
            if abs(a[0] - b[0]) > 1e-9:
                out.append((a, b) if a[0] < b[0] else (b, a))
    return out


def _z(e, x):
    (x0, z0), (x1, z1) = e
    return z0 + (z1 - z0) * min(1.0, max(0.0, (x - x0) / (x1 - x0)))


def _spans(edges, x):
    """Where the line x = const is inside the edges (even-odd), as (lower crossing, upper crossing), each (z, edge)."""
    c = sorted(((_z(e, x), e) for e in edges if e[0][0] < x < e[1][0]), key=lambda t: t[0])
    return [(c[i], c[i + 1]) for i in range(0, len(c) - 1, 2)]


def _cross_x(e, f):
    lo, hi = max(e[0][0], f[0][0]), min(e[1][0], f[1][0])
    if lo >= hi:
        return None
    d0, d1 = _z(e, lo) - _z(f, lo), _z(e, hi) - _z(f, hi)
    return lo + (hi - lo) * d0 / (d0 - d1) if d0 * d1 < 0 else None


def fill(acc, region, holes, y, mat, tile):
    """A flat terrace top at height y: the `region` outlines minus the `holes` (plan (x, z) lists). It is cut into
    strips at every vertex and every crossing, so inside a strip each piece is an exact trapezoid and the holes'
    edges are met exactly (the bowls' lips and the channels' walls close on them)."""
    groups = [_edges(region)] + [_edges([h]) for h in holes]
    xs = {p[0] for poly in region + holes for p in poly}
    for i, ga in enumerate(groups):
        for gb in groups[i + 1:]:
            for e in ga:
                for f in gb:
                    if f[1][0] > e[0][0] and f[0][0] < e[1][0]:
                        x = _cross_x(e, f)
                        if x is not None:
                            xs.add(x)
    xs = sorted(xs)
    uv = BoxUV(tile)
    for xa, xb in zip(xs, xs[1:]):
        if xb - xa < 1e-7:
            continue
        xm = (xa + xb) / 2
        cuts = sorted((c for g in groups[1:] for c in _spans(g, xm)), key=lambda c: c[0][0])
        merged = []
        for lo, hi in cuts:
            if merged and lo[0] <= merged[-1][1][0]:
                if hi[0] > merged[-1][1][0]:
                    merged[-1] = (merged[-1][0], hi)
            else:
                merged.append((lo, hi))
        for lo, hi in _spans(groups[0], xm):
            cur = lo
            for hlo, hhi in merged:
                if hhi[0] <= cur[0] or hlo[0] >= hi[0]:
                    continue
                if hlo[0] > cur[0]:
                    _trap(acc, xa, xb, cur[1], hlo[1], y, mat, uv)
                cur = hhi
            if cur[0] < hi[0]:
                _trap(acc, xa, xb, cur[1], hi[1], y, mat, uv)


def _trap(acc, xa, xb, e0, e1, y, mat, uv):
    a0, b0, a1, b1 = _z(e0, xa), _z(e0, xb), _z(e1, xa), _z(e1, xb)
    pts = [(xa, a0), (xb, b0), (xb, b1), (xa, a1)]
    if b1 - b0 < 1e-6:
        pts.pop(2)
    if a1 - a0 < 1e-6:
        pts.pop()
    if len(pts) >= 3:
        acc.face([G((x, y, z)) for x, z in pts], mat, uv, out=(0, 0, 1))


# ───────────────────────── dam faces ─────────────────────────

def _smooth01(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


class Face:
    """A dam face hanging from a terrace edge: the plan line `plan` (walked with the rock on the right, so (dz, -dx)
    points out), from y_top down to y_bot, crisp at the edge and bulging out below it in broad blunt flowstone ribs.
    `notches` [(x, z, half-width, floor y)] cut a runnel's notch through the edge (its channel closes the sides) and
    a U groove down the face below it, the chute its fall runs in."""

    def __init__(self, plan, y_top, y_bot, seed, amp=0.1, notches=()):
        self.p = [Vector((x, z)) for x, z in plan]
        self.cum = [0.0]
        for a, b in zip(self.p, self.p[1:]):
            self.cum.append(self.cum[-1] + (b - a).length)
        segs = [Vector((b.y - a.y, -(b.x - a.x))).normalized() for a, b in zip(self.p, self.p[1:])]
        self.vn = [segs[0]] + [(a + b).normalized() for a, b in zip(segs, segs[1:])] + [segs[-1]]
        self.y_top, self.y_bot, self.amp = y_top, y_bot, amp
        r = random.Random(seed)
        self.ph = [r.uniform(0, 6.28) for _ in range(4)]
        self.u0 = r.uniform(0, 4)
        self.notches = []
        for x, z, hw, fy in notches:
            s = self.s_near(x, z)
            self.notches.append((s - hw, s + hw, fy))

    def s_near(self, x, z):
        q = Vector((x, z))
        best = (1e9, 0.0)
        for i, (a, b) in enumerate(zip(self.p, self.p[1:])):
            ab = b - a
            t = min(1.0, max(0.0, (q - a).dot(ab) / max(ab.length_squared, 1e-12)))
            d = (a + ab * t - q).length
            if d < best[0]:
                best = (d, self.cum[i] + ab.length * t)
        return best[1]

    def at(self, s):
        """The edge point (x, z) and the outward normal at arclength s."""
        s = min(max(s, 0.0), self.cum[-1])
        i = max(0, min(len(self.p) - 2, bisect.bisect_left(self.cum, s) - 1))
        t = (s - self.cum[i]) / max(self.cum[i + 1] - self.cum[i], 1e-9)
        return self.p[i].lerp(self.p[i + 1], t), self.vn[i].lerp(self.vn[i + 1], t).normalized()

    def disp(self, s, y):
        a, b, c, _ = self.ph
        rib = 0.5 + 0.3 * math.sin(s * 2 * math.pi / 0.74 + a + 0.25 * math.sin(y * 1.7 + b)) + 0.2 * math.sin(s * 2 * math.pi / 1.31 + c)
        d = self.amp * (0.25 + 0.75 * max(0.0, rib)) * (1 - (1 - min(1.0, (self.y_top - y) / 0.16)) ** 2)
        for s0, s1, fy in self.notches:
            d *= _smooth01(max((s0 - s) / 0.15, (s - s1) / 0.15))
            if s0 < s < s1 and y < fy:
                t = (s - (s0 + s1) / 2) / ((s1 - s0) / 2)
                d -= 0.16 * (1 - t * t) * min(1.0, (fy - y) / 0.12)
        return d

    def point(self, s, y, off=0.0):
        e, n = self.at(s)
        k = self.disp(s, y) + off
        return V(e.x + n.x * k, y, e.y + n.y * k)

    def normal(self, s):
        n = self.at(s)[1]
        return V(n.x, 0.0, n.y)

    def build(self, acc, mat):
        ss = list(self.cum)
        for a, b in zip(self.cum, self.cum[1:]):
            k = int((b - a) / 0.1)
            ss += [a + (b - a) * i / (k + 1) for i in range(1, k + 1)]
        keep = set()
        for s0, s1, _ in self.notches:
            keep |= {s0, s1}
            ss += [s0 + (s1 - s0) * i / 6 for i in range(7)]
        ss = _merge(ss, keep, 0.012)
        ys = [self.y_top - d for d in (0.0, 0.03, 0.07, 0.13, 0.21)]
        y = ys[-1] - 0.2
        while y > self.y_bot + 0.05:
            ys.append(y)
            y -= 0.2
        ys.append(self.y_bot)
        fl = set()
        for _, _, fy in self.notches:
            fl.add(fy)
            ys += [fy, fy - 0.06, fy - 0.13]
        ys = sorted(_merge(ys, fl, 0.02), reverse=True)
        grid = [[self.point(s, y) for y in ys] for s in ss]
        for i in range(len(ss) - 1):
            out = G(self.normal((ss[i] + ss[i + 1]) / 2))
            for j in range(len(ys) - 1):
                if any(s0 - 1e-6 <= ss[i] and ss[i + 1] <= s1 + 1e-6 and ys[j + 1] >= fy - 1e-6 for s0, s1, fy in self.notches):
                    continue
                q = [grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]]
                uv = [((ss[a] + self.u0) / DAM_TILE[0], ys[b] / DAM_TILE[1]) for a, b in ((i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1))]
                acc.face([G(p) for p in q], mat, uv, out=out, smooth=True)


def _merge(vals, keep, tol):
    """Sorted values with near-duplicates dropped (the ones in `keep` win)."""
    out = []
    for v in sorted(set(vals) | set(keep)):
        if out and v - out[-1] < tol:
            if v in keep and out[-1] not in keep:
                out[-1] = v
            continue
        out.append(v)
    return out


def drape(acc, face, x, z, length, hang, back, mat, rng):
    """A moss strip along a face's edge: a thin skin lying `back` m onto the terrace, rolling over the edge and
    spilling a little way down the face (following its bulges) in soft uneven drips; flat over its length, its ends
    tuck up."""
    sc = face.s_near(x, z)
    n_s = max(4, int(length / 0.07))
    ph, ph2, lam = rng.uniform(0, 6.28), rng.uniform(0, 6.28), rng.uniform(0.55, 0.8)
    K = 5
    rings, spines = [], []
    for i in range(n_s + 1):
        t = i / n_s
        s = sc - length / 2 + length * t
        env = min(1.0, 3.0 * math.sin(math.pi * t)) ** 0.5
        w = (0.6 * (0.5 + 0.5 * math.sin(2 * math.pi * s / lam + ph))
             + 0.4 * (0.5 + 0.5 * math.sin(2 * math.pi * s / (0.43 * lam) + ph2)))
        h = max(0.05, hang * env * (0.3 + 0.7 * w * w))
        bk = back * (0.5 + 0.5 * env)
        e, n2 = face.at(s)
        n = V(n2.x, 0, n2.y)
        top = V(e.x, face.y_top, e.y)
        outer = [top - n * bk + UP * 0.012, top - n * bk * 0.45 + UP * 0.02, top + n * 0.02 + UP * 0.018]
        inner = [top - n * bk + UP * 0.003, top - n * bk * 0.45 + UP * 0.005, top + UP * 0.005]
        for k in range(1, K + 1):
            yk = face.y_top - h * k / K
            fn = face.normal(s)
            outer.append(face.point(s, yk) + fn * (0.024 if k < K else 0.014))
            inner.append(face.point(s, yk) + fn * 0.006)
        rings.append(outer + inner[::-1])
        spines.append([(a + b) / 2 for a, b in zip(outer, inner)])
    m = len(rings[0]) // 2
    for i in range(n_s):
        for k in range(2 * m):
            k1 = (k + 1) % (2 * m)
            q = [rings[i][k], rings[i + 1][k], rings[i + 1][k1], rings[i][k1]]
            c = sum(q, Vector()) / 4
            if k == m - 1:          # the tip
                o = spines[i][m - 1] - spines[i][m - 2]
            elif k == 2 * m - 1:    # the back edge on the terrace
                o = spines[i][0] - spines[i][1]
            else:
                j0, j1 = (k, k1) if k < m else (2 * m - 1 - k1, 2 * m - 1 - k)
                o = c - (spines[i][j0] + spines[i][j1] + spines[i + 1][j0] + spines[i + 1][j1]) / 4
            acc.face([G(p) for p in q], mat, BoxUV(0.8), out=G(o), smooth=True)
    for i, sgn in ((0, -1), (n_s, 1)):
        c = sum(rings[i], Vector()) / len(rings[i])
        tan = face.point(sc - length / 2 + length * i / n_s + 0.01 * sgn, face.y_top) - face.point(sc - length / 2 + length * i / n_s, face.y_top)
        for k in range(2 * m):
            acc.face([G(rings[i][k]), G(rings[i][(k + 1) % (2 * m)]), G(c)], mat, BoxUV(0.8), out=G(tan * sgn), smooth=True)


# ───────────────────────── walls ─────────────────────────

def wall(acc, mat, origin, along, normal, a_range, y_top, seed, skip=None, y_bot=None, da=0.2, dy=0.2):
    """A shaft wall: the plane through `origin` spanned by `along` and up, facing `normal` (into the room), pushed back
    into the rock in broad vertical drapes (never forward, so nothing in the room is cut). y_bot(a) gives each
    column's foot; skip(a0, a1, y0, y1) drops cells (openings, which their arch rings cover)."""
    r = random.Random(seed)
    ph = [r.uniform(0, 6.28) for _ in range(5)]
    o, al, nm = Vector(origin), Vector(along), Vector(normal)
    a0, a1 = a_range
    na = max(2, int((a1 - a0) / da))
    As = [a0 + (a1 - a0) * i / na for i in range(na + 1)]
    yb = y_bot or (lambda a: L.FLOOR - 0.8)

    def depth(a, y):
        return (0.22 * (0.5 + 0.5 * math.sin(2 * math.pi * a / 1.3 + ph[0] + 0.3 * math.sin(y * 0.5 + ph[1])))
                + 0.1 * (0.5 + 0.5 * math.sin(2 * math.pi * a / 0.55 + ph[2]))
                + 0.06 * (0.5 + 0.5 * math.sin(y * 0.9 + a * 0.3 + ph[3])))

    nv = max(2, int((y_top - min(yb(a) for a in As)) / dy))
    cols = []
    for a in As:
        y0 = yb(a)
        cols.append([(a, y0 + (y_top - y0) * j / nv) for j in range(nv + 1)])
    out = G(nm)
    for i in range(na):
        for j in range(nv):
            c = [cols[i][j], cols[i + 1][j], cols[i + 1][j + 1], cols[i][j + 1]]
            if skip is not None and skip(c):
                continue
            pts = [o + al * a + UP * y - nm * depth(a, y) for a, y in c]
            uv = [(a / WALL_TILE[0], y / WALL_TILE[1]) for a, y in c]
            acc.face([G(p) for p in pts], mat, uv, out=out, smooth=True)


def _arch_inside(cx, spring, hw, bottom):
    """A round-headed opening (centre line cx, jambs hw either side), as an inside test on (a, y)."""
    def inside(a, y):
        if y < bottom or abs(a - cx) > hw:
            return False
        return y <= spring or (a - cx) ** 2 + (y - spring) ** 2 <= hw * hw
    return inside


def _cell_hits(inside):
    def skip(c):
        pts = c + [(sum(p[0] for p in c) / 4, sum(p[1] for p in c) / 4)]
        return any(inside(a, y) for a, y in pts)
    return skip


def arch_path(cx, spring, r, foot_l, foot_r, n=14):
    """(a, y, radial a, radial y) along a round-headed arch: up the left jamb, over, down the right jamb."""
    pts = [(cx - r, foot_l, -1, 0), (cx - r, spring, -1, 0)]
    for k in range(1, n):
        t = math.pi - math.pi * k / n
        pts.append((cx + r * math.cos(t), spring + r * math.sin(t), math.cos(t), math.sin(t)))
    pts += [(cx + r, spring, 1, 0), (cx + r, foot_r, 1, 0)]
    return pts


def loft(acc, rings, centres, mat, inward=False, smooth=True, uv=None):
    """Joins rings of points (game coordinates) quad by quad; each quad faces away from (or, inward, toward) the
    centre given for its ring, which is how a ring round a path, a tunnel and a channel all come out right. `mat` may
    be a function of the ring index."""
    uv = uv or BoxUV(1.2)
    for i in range(len(rings) - 1):
        c = (centres[i] + centres[i + 1]) / 2
        m = len(rings[i])
        mi = mat(i) if callable(mat) else mat
        for k in range(m - 1):
            q = [rings[i][k], rings[i + 1][k], rings[i + 1][k + 1], rings[i][k + 1]]
            o = sum(q, Vector()) / 4 - c
            acc.face([G(p) for p in q], mi, uv, out=G(-o if inward else o), smooth=smooth)


def arch_ring(acc, mat, plane_point, along, normal, cx, spring, r, foot_l, foot_r, width=0.32, depth=0.55, proud=0.18):
    """The stone ring round an opening in a wall: a rounded slab `width` wide whose inside sits 1 cm outside the
    opening, standing `proud` out of the wall plane and reaching `depth` back into it (behind the drapes)."""
    o, al, nm = Vector(plane_point), Vector(along), Vector(normal)
    prof = [(-0.5, -depth), (-0.5, proud - 0.07), (-0.38, proud - 0.01), (0.0, proud), (0.38, proud - 0.01),
            (0.5, proud - 0.07), (0.5, -depth), (-0.5, -depth)]
    rings, centres = [], []
    for a, y, ra, ry in arch_path(cx, spring, r + 0.01 + width / 2, foot_l, foot_r):
        c = o + al * a + UP * y
        rad = al * ra + UP * ry
        rings.append([c + rad * (w * width) + nm * t for w, t in prof])
        centres.append(c + nm * ((proud - depth) / 2))
    loft(acc, rings, centres, mat)


def tunnel(acc, mat, plane_point, along, normal, cx, spring, r, bottom, length, rise=None, end_mat=None, n_back=4, knots=(),
           far=None):
    """A tunnel behind a round-headed opening: its walls and roof run `length` back from the wall plane (climbing
    rise(t) as they go, t = distance back), facing in; the far end is closed with `end_mat` (daylight, or dark), and
    far = (t, material) dresses the walls beyond t in it (the daylight spilling in at the top of the mouth's stair)."""
    o, al, nm = Vector(plane_point), Vector(along), Vector(normal)
    rise = rise or (lambda t: 0.0)
    prof = arch_path(cx, spring, r, bottom, bottom)
    ts = sorted({0.0, length} | {length * k / n_back for k in range(1, n_back)} | set(knots) | ({far[0]} if far else set()))
    rings, centres = [], []
    for t in ts:
        lift = UP * rise(t)
        rings.append([o + al * a + UP * y + lift - nm * t for a, y, _, _ in prof])
        centres.append(o + al * cx + UP * spring + lift - nm * t)
    loft(acc, rings, centres, (lambda i: far[1] if ts[i] >= far[0] - 1e-9 else mat) if far else mat, inward=True)
    if end_mat is not None:
        end = rings[-1]
        acc.face([G(p) for p in end], end_mat, BoxUV(2.0), out=G(nm))


# ───────────────────────── boxes, roots ─────────────────────────

def box(acc, lo, hi, mat, tile=1.0, top=None):
    """An axis-aligned box in game coordinates (its top may wear another material)."""
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    c = [V(x, y, z) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
    mid = V((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
    for f in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)):
        q = [c[k] for k in f]
        ctr = sum(q, Vector()) / 4
        is_top = all(abs(p.y - y1) < 1e-9 for p in q)
        acc.face([G(p) for p in q], (top or mat) if is_top else mat, BoxUV(tile), out=G(ctr - mid))


def wall_mass(acc, mat, x0, zc, half, y0, y1, skip, proud=0.08, back=0.45, step=0.1):
    """A rounded mass of rock standing out of the left wall's plane x0 round a low opening (z zc +- half, y0..y1): a
    broad face `proud` of the plane that rolls back `back` into the rock over its top and sides, behind the drapes,
    so it grows out of them. skip(cell) drops the cells over the opening; its arch ring hides the cut."""
    na, nv = int(round(2 * half / step)), int(round((y1 - y0) / step))

    def x_at(a, y):
        u = abs(a - zc) / half
        v = max(0.0, y - y0 - 0.35) / (y1 - y0 - 0.35)
        r = (u ** 4 + v ** 4) ** 0.25
        return x0 - back + (back + proud) * _smooth01((1 - r) / 0.3)

    for i in range(na):
        for j in range(nv):
            c = [(zc - half + 2 * half * (i + di) / na, y0 + (y1 - y0) * (j + dj) / nv)
                 for di, dj in ((0, 0), (1, 0), (1, 1), (0, 1))]
            if skip(c):
                continue
            acc.face([G((x_at(a, y), y, a)) for a, y in c], mat, [(a / WALL_TILE[0], y / WALL_TILE[1]) for a, y in c],
                     out=G((1, 0, 0)), smooth=True)


def root_tube(acc, ctrl, r0, r1, bark, sides=10, flare=1.9, foot=0.75):
    """A root along a cubic bezier (game points) from its thick end to its foot, bark wrapped round it, both ends
    rounded off. It tapers from r0 to r1, then over the last `foot` metres swells by up to `flare` into three broad
    buttress lobes (the foot is buried, so only its spread shows). Rings sit about 0.4 m apart, closer in the foot,
    on parallel-transported frames so a near-vertical root never twists."""
    a, b, c, d = [Vector(p) for p in ctrl]

    def bez(t):
        s = 1 - t
        return a * s ** 3 + b * 3 * s * s * t + c * 3 * s * t * t + d * t ** 3

    dense = [bez(i / 128) for i in range(129)]
    cum = [0.0]
    for p, q in zip(dense, dense[1:]):
        cum.append(cum[-1] + (q - p).length)
    length = cum[-1]
    body = max(0.0, length - foot)
    nb = max(3, int(body / 0.4))
    ss = [body * i / nb for i in range(nb)] + [body + (length - body) * k / 5 for k in range(6)]

    def at(s):
        i = max(0, min(127, bisect.bisect_left(cum, s) - 1))
        return dense[i].lerp(dense[i + 1], (s - cum[i]) / max(cum[i + 1] - cum[i], 1e-9))

    pts = [at(s) for s in ss]
    n = len(pts)
    tans = [(pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized() for i in range(n)]
    ref = Vector((0, 0, 1)) if abs(tans[0].z) < 0.9 else Vector((1, 0, 0))
    u = ref.cross(tans[0]).normalized()
    rings, rads = [], []
    for i, (p, tng) in enumerate(zip(pts, tans)):
        u = (u - tng * u.dot(tng)).normalized()
        v = tng.cross(u)
        r = r0 + (r1 - r0) * (ss[i] / length) ** 0.8
        f = (flare - 1) * _smooth01((ss[i] - body) / max(foot, 1e-6)) if flare > 1 else 0.0
        ring = []
        for k in range(sides):
            th = 2 * math.pi * k / sides
            rk = r * (1 + f * (0.75 + 0.35 * math.cos(3 * th + 0.6))) * (1 + 0.08 * math.sin(i * 1.3 + k * 2.1))
            ring.append(p + (u * math.cos(th) + v * math.sin(th)) * rk)
        rings.append(ring)
        rads.append(r * (1 + f))
    for i in range(n - 1):
        for k in range(sides):
            k1 = (k + 1) % sides
            q = [rings[i][k], rings[i][k1], rings[i + 1][k1], rings[i + 1][k]]
            v0, v1 = ss[i] / 1.6, ss[i + 1] / 1.6
            uvq = [(k / sides, v0), (k1 / sides if k1 else 1.0, v0), (k1 / sides if k1 else 1.0, v1), (k / sides, v1)]
            acc.face([G(p) for p in q], bark, uvq, out=G(sum(q, Vector()) / 4 - (pts[i] + pts[i + 1]) / 2), smooth=True)
    for i, sgn in ((0, -1), (n - 1, 1)):
        tip = pts[i] + tans[i] * (sgn * rads[i] * 0.7)
        for k in range(sides):
            acc.face([G(rings[i][k]), G(rings[i][(k + 1) % sides]), G(tip)], bark, BoxUV(1.0), out=G(tans[i] * sgn),
                     smooth=True)
    return bez


# ───────────────────────── the room ─────────────────────────

def build_shell(kit, S, root):
    acc = Acc()
    # ── the flat tops: three terraces, the landing and the alcove floors, with the bowls' holes and the channels ──
    top_hole = lambda i: L.ellipse(*L.hole(L.SLOTS[i]))
    chan = {}
    for key, path, y, w, run in (("top", L.TOP_CHANNEL, L.TOP_Y, L.CHANNEL_W, 0.12), ("mid", L.MID_CHANNEL, L.MID_Y, L.CHANNEL_W, 0.12),
                                 ("out", L.OUT_CHANNEL, L.FLOOR, L.OUT_CHANNEL_W, 0.5)):
        P = [G((x, y, z)) for x, z in path]
        offs = _offsets(P)
        right = [(p + o * (w / 2)) for p, o in zip(P, offs)]
        left = [(p - o * (w / 2)) for p, o in zip(P, offs)]
        ext = (P[-1] - P[-2]).normalized() * run           # the hole runs on past the face (or wall) it opens in
        outline = right + [right[-1] + ext, left[-1] + ext] + left[::-1]
        chan[key] = (P, [(p.x, -p.y) for p in outline])
    fill(acc, [L.top_level_poly()], [top_hole(0), chan["top"][1]], L.TOP_Y, S.top, TOP_TILE)
    fill(acc, [L.mid_level_poly()], [top_hole(i) for i in (2, 3, 4, 5)] + [chan["mid"][1]], L.MID_Y, S.top, TOP_TILE)
    fill(acc, [L.floor_poly()], [top_hole(1), chan["out"][1]], L.FLOOR, S.floor, FLOOR_TILE)
    fill(acc, [L.LANDING], [], L.LANDING_Y, S.top, TOP_TILE)

    # ── the dry channels, cut CHANNEL_D into the terrace: the runnel's two runs and the outflow's ──
    hw, d = L.CHANNEL_W / 2, L.CHANNEL_D
    for key in ("top", "mid"):
        sweep(acc, [(hw, 0.0), (hw, -d), (-hw, -d), (-hw, 0.0)], chan[key][0], S.channel, closed=False, uv_scale=CHAN_TILE)
    ow = L.OUT_CHANNEL_W / 2
    sweep(acc, [(ow, 0.0), (ow, -d), (-ow, -d), (-ow, 0.0)], chan["out"][0], S.floor, closed=False, uv_scale=FLOOR_TILE)

    # ── the dam faces: banded flowstone under each edge; the runnel notches both and runs down a groove ──
    top_dam = Face(L.top_front(), L.TOP_Y, L.MID_Y - 0.35, 41,
                   notches=[(L.TOP_CHANNEL[-1][0], L.TOP_CHANNEL[-1][1], hw, L.TOP_Y - d)])
    mid_dam = Face(L.mid_front(), L.MID_Y, L.FLOOR - 0.35, 42,
                   notches=[(L.MID_CHANNEL[-1][0], L.MID_CHANNEL[-1][1], hw, L.MID_Y - d)])
    right_face = Face([(L.RIGHT, L.BACK - 0.4), (L.RIGHT, L.top_edge(L.RIGHT))], L.TOP_Y, L.MID_Y - 0.35, 43, amp=0.06)
    landing = Face(L.LANDING[1:] + L.LANDING[:1], L.LANDING_Y, L.TOP_Y - 0.1, 44, amp=0.05)
    for f in (top_dam, mid_dam, right_face, landing):
        f.build(acc, S.dam)
    # the well floor's far front (out of every frame): a soft rocky roll down into the dark, never a box edge
    front = [G((x, L.FLOOR, z)) for x, z in L.floor_front()]
    sweep(acc, [(0.0, 0.0), (-0.1, -0.03), (-0.17, -0.11), (-0.21, -0.25)], front, S.floor, closed=False,
          uv_scale=FLOOR_TILE)
    sweep(acc, [(-0.21, -0.25), (-0.19, -0.6), (-0.27, -1.0), (-0.14, -1.6), (0.3, -2.2)], front, S.wall, closed=False,
          uv_scale=2.0)

    # ── the shaft: grey-blue drape walls round the back, left and right, rising out of the frame ──
    mouth_in = _arch_inside((L.MOUTH_X0 + L.MOUTH_X1) / 2, L.MOUTH_SPRING, (L.MOUTH_X1 - L.MOUTH_X0) / 2, L.MOUTH_BOTTOM)
    out_in = _arch_inside(L.OUT_Z, L.OUT_SPRING, L.OUT_HW, L.FLOOR - d - 0.05)
    wall(acc, S.wall, (0, 0, L.BACK), (1, 0, 0), (0, 0, 1), (L.LEFT - 0.6, L.ALC_X + 0.6), L.WALL_TOP, 51,
         skip=_cell_hits(mouth_in))
    wall(acc, S.wall, (L.LEFT, 0, 0), (0, 0, 1), (1, 0, 0), (L.BACK - 0.6, L.FRONT + 0.8), L.WALL_TOP, 52,
         skip=_cell_hits(out_in))
    # the right wall over the alcoves (their round heads cut up into it), the alcoves' backs and the pier between
    alc = [(L.BACK, L.PIER[0]), (L.PIER[1], L.ALC_FRONT)]

    def lintel(z):
        for z0, z1 in alc:
            if z0 < z < z1:
                return L.ALC_HEAD + 0.8 * math.sqrt(max(0.0, 1 - ((z - (z0 + z1) / 2) / ((z1 - z0) / 2)) ** 2))
        return L.ALC_HEAD

    wall(acc, S.wall, (L.RIGHT, 0, 0), (0, 0, -1), (-1, 0, 0), (-L.ALC_FRONT, -L.BACK + 0.6), L.WALL_TOP, 53,
         y_bot=lambda a: lintel(-a) - 0.05)
    # in front of the alcoves it comes down to the floor (the same drapes, so the seam at ALC_FRONT closes), and
    # alcove B's front side is walled
    wall(acc, S.wall, (L.RIGHT, 0, 0), (0, 0, -1), (-1, 0, 0), (-L.FRONT - 0.8, -L.ALC_FRONT), L.WALL_TOP, 53)
    wall(acc, S.wall, (0, 0, L.ALC_FRONT), (1, 0, 0), (0, 0, -1), (L.RIGHT - 0.4, L.ALC_X + 0.6), L.WALL_TOP, 58,
         y_bot=lambda a: L.MID_Y - 0.6, da=0.4, dy=0.4)
    for k, (z0, z1) in enumerate(alc):
        wall(acc, S.wall, (L.ALC_X, 0, 0), (0, 0, -1), (-1, 0, 0), (-z1 - 0.2, -z0 + 0.2), L.WALL_TOP, 54 + k,
             y_bot=lambda a: L.MID_Y - 0.6)
    box(acc, (L.RIGHT - 0.1, L.MID_Y - 0.5, L.PIER[0]), (L.ALC_X + 0.3, L.ALC_HEAD + 0.8, L.PIER[1]), S.wall, 2.0)

    # ── the cave mouth: its ring, the tunnel climbing toward the hub to a pale daylight end, treads up its left half,
    #    the brook's ramp down its right half and out over the spout lip ──
    mcx, mhw = (L.MOUTH_X0 + L.MOUTH_X1) / 2, (L.MOUTH_X1 - L.MOUTH_X0) / 2
    arch_ring(acc, S.top, (0, 0, L.BACK), (1, 0, 0), (0, 0, 1), mcx, L.MOUTH_SPRING, mhw, L.MOUTH_SILL, L.TOP_Y)
    tunnel(acc, S.tunnel, (0, 0, L.BACK + 0.02), (1, 0, 0), (0, 0, 1), mcx, L.MOUTH_SPRING, mhw, L.MOUTH_BOTTOM,
           L.BACK + 0.02 - L.TUNNEL_BACK, rise=lambda t: L.tunnel_rise(L.BACK + 0.02 - t), end_mat=S.daylight,
           knots=[L.BACK + 0.02 - L.TUNNEL_FLAT], far=(L.BACK + 0.02 - L.TUNNEL_BACK - 1.1, S.daylight))
    for k in range(6):
        z0 = L.TUNNEL_FLAT - L.TREAD_RUN * (k + 1) if k < 5 else L.TUNNEL_BACK - 0.05
        box(acc, (L.MOUTH_X0 - 0.05, L.MOUTH_BOTTOM, z0), (L.MOUTH_SPLIT, L.MOUTH_SILL + L.TREAD_RISE * (k + 1),
            L.TUNNEL_FLAT - L.TREAD_RUN * k), S.top, 1.0, top=S.daylight if k >= 4 else None)
    ramp = [(L.BACK + 0.02, 0.0), (L.TUNNEL_FLAT, 0.0), (L.TUNNEL_BACK - 0.05, L.tunnel_rise(L.TUNNEL_BACK - 0.05))]
    for (za, ra), (zb, rb) in zip(ramp, ramp[1:]):
        q = [(1.18, L.BROOK_FLOOR + ra, za), (L.MOUTH_X1 + 0.05, L.BROOK_FLOOR + ra, za),
             (L.MOUTH_X1 + 0.05, L.BROOK_FLOOR + rb, zb), (1.18, L.BROOK_FLOOR + rb, zb)]
        acc.face([G(p) for p in q], S.channel, BoxUV(CHAN_TILE), out=(0, 0, 1))
    box(acc, (L.MOUTH_SPLIT, 2.12, -5.5), (L.MOUTH_X1 + 0.06, L.BROOK_FLOOR, -5.12), S.top, 1.0)
    for x0, x1 in ((L.MOUTH_SPLIT, L.MOUTH_SPLIT + 0.12), (L.MOUTH_X1 - 0.1, L.MOUTH_X1 + 0.06)):
        box(acc, (x0, L.BROOK_FLOOR, -5.5), (x1, L.BROOK_FLOOR + 0.1, -5.12), S.top, 1.0)

    # ── the low outflow arch at the foot of the left wall, set in a rounded mass of rock (the ring stands just proud of
    #    it and hides its cut), the outflow channel running out under it into the dark ──
    wall_mass(acc, S.wall, L.LEFT, L.OUT_Z, L.OUT_MASS_HW, L.FLOOR - 0.05, L.OUT_MASS_TOP,
              _cell_hits(_arch_inside(L.OUT_Z, L.OUT_SPRING, L.OUT_HW + 0.12, L.FLOOR - 0.5)))
    arch_ring(acc, S.top, (L.LEFT, 0, 0), (0, 0, 1), (1, 0, 0), L.OUT_Z, L.OUT_SPRING, L.OUT_HW, L.FLOOR, L.FLOOR,
              width=0.26, depth=0.5, proud=0.18)
    tunnel(acc, S.tunnel, (L.LEFT, 0, 0), (0, 0, 1), (1, 0, 0), L.OUT_Z, L.OUT_SPRING, L.OUT_HW, L.FLOOR - d,
           L.LEFT - L.OUT_TUNNEL, end_mat=S.deep, n_back=2)
    q = [(L.LEFT, L.FLOOR - d, L.OUT_Z - L.OUT_HW), (L.LEFT, L.FLOOR - d, L.OUT_Z + L.OUT_HW),
         (L.OUT_TUNNEL, L.FLOOR - d, L.OUT_Z + L.OUT_HW), (L.OUT_TUNNEL, L.FLOOR - d, L.OUT_Z - L.OUT_HW)]
    acc.face([G(p) for p in q], S.tunnel, BoxUV(1.2), out=(0, 0, 1))

    # ── Old Stoic's roots: four thick ones hugging the walls, each forked once, feet buttressed and buried; the root
    #    stair's treads and its flank ──
    for ctrl, r0, r1, (tf, fork, rf) in L.ROOTS:
        bez = root_tube(acc, ctrl, r0, r1, S.bark)
        root_tube(acc, [bez(tf)] + fork, rf * 1.45, rf, S.bark, sides=8, foot=0.6)
    root_tube(acc, *L.STAIR_ROOT, S.bark, sides=8, foot=0.6)
    steps = L.stair_steps()
    for k, (x, ty, z, w, dz) in enumerate(steps):
        if k < 4:   # along the back wall, each block reaching back to the landing
            lo, hi = (x - w / 2, L.TOP_Y - 0.1, z - dz / 2 - 0.3), (-1.79, max(ty, L.TOP_Y + 0.05), z + dz / 2)
        else:       # down the left wall, each block reaching back to the top dam
            lo, hi = (x - w / 2, L.MID_Y - 0.1, L.top_edge(x) - 0.05), (x + w / 2, max(ty, L.MID_Y + 0.05), z + dz / 2)
        box(acc, lo, hi, S.bark, 1.2, top=S.tread)
    return acc.build("shell", root), (top_dam, mid_dam, landing)


DRAPES = [  # (face, x, length, hang, back): long thin moss strips along the dam edges where the terrace behind is clear
    ("top", -2.95, 2.0, 0.26, 0.14), ("top", 2.9, 1.4, 0.22, 0.02), ("top", 6.7, 1.6, 0.3, 0.16),
    ("mid", -3.0, 2.2, 0.3, 0.16), ("mid", 1.3, 1.2, 0.26, 0.16), ("mid", 4.8, 1.6, 0.26, 0.16),
    ("landing", -0.9, 1.2, 0.16, 0.1),
]
MOSS = [  # (x, y, z, rx, rz, h) low broad mounds on the terrace tops, round the roots' feet and on the floor
    (-4.7, L.TOP_Y, -4.25, 0.75, 0.45, 0.05), (-1.0, L.TOP_Y, -3.05, 0.6, 0.35, 0.04), (7.6, L.TOP_Y, -4.85, 0.7, 0.45, 0.05),
    (5.9, L.TOP_Y, -3.75, 0.55, 0.3, 0.04), (-1.5, L.LANDING_Y, -5.15, 0.35, 0.2, 0.035),
    (-5.15, L.MID_Y, 0.25, 0.55, 0.32, 0.045), (-3.0, L.MID_Y, -2.25, 0.6, 0.3, 0.04), (2.9, L.MID_Y, -2.05, 0.5, 0.25, 0.04),
    (7.8, L.MID_Y, 2.6, 0.65, 0.45, 0.05), (0.9, L.MID_Y, 0.45, 0.5, 0.3, 0.04), (5.3, L.MID_Y, 3.0, 0.55, 0.22, 0.04),
    (-5.0, L.FLOOR, 4.9, 0.8, 0.5, 0.05), (-1.0, L.FLOOR, 3.0, 0.85, 0.5, 0.045), (0.8, L.FLOOR, 1.85, 0.6, 0.4, 0.04),
]
PEBBLES = [(-1.6, 3.4), (-0.4, 2.6), (-2.6, 3.5), (0.4, 2.3), (-4.6, 4.4), (1.2, 2.9)]


def build_decor(S, faces, rng):
    acc = Acc()
    by = dict(zip(("top", "mid", "landing"), faces))
    edge = {"top": L.top_edge, "mid": lambda x: L.dam_edge(x) if x <= L.LEDGE_X else L.LEDGE_FRONT,
            "landing": lambda x: -4.47}
    for key, x, length, hang, back in DRAPES:
        drape(acc, by[key], x, edge[key](x), length, hang, back, S.moss, rng)
    for x, y, z, rx, rz, h in MOSS:
        mound(acc, G((x, y, z)), rx, rz, h, S.moss, rng, seg=18)
    for x, z in PEBBLES:
        mound(acc, G((x, L.FLOOR, z)), rng.uniform(0.14, 0.22), rng.uniform(0.1, 0.16), rng.uniform(0.07, 0.11), S.pebble, rng, seg=7)
    for x, z in L.floor_front()[4::9]:   # low boulders along the floor's far front, so its roll reads as rock
        mound(acc, G((x, L.FLOOR, z - 0.15)), rng.uniform(0.4, 0.6), rng.uniform(0.3, 0.4), rng.uniform(0.22, 0.32), S.pebble,
              rng, seg=9)
    return acc


def build_ferns(M, rng):
    acc = Acc()
    for x, y, z in L.FERN_CLUMPS:
        b = G((x, y, z))
        off = rng.uniform(0, 6.28)
        for k in range(7):
            yaw = off + k * 2 * math.pi / 7 + rng.uniform(-0.2, 0.2)
            frond(acc, b + Vector((math.cos(yaw), math.sin(yaw), 0)) * 0.05, yaw, rng.uniform(0.55, 0.75), rng.uniform(0.35, 0.5),
                  rng.uniform(0.3, 0.45), rng.uniform(0.11, 0.14), 6, M.fern[k % 3], roll=rng.uniform(-0.35, 0.35))
        for k in range(3):
            yaw = off + 0.4 + k * 2 * math.pi / 3
            frond(acc, b, yaw, rng.uniform(0.3, 0.42), rng.uniform(0.5, 0.62), rng.uniform(0.12, 0.2), rng.uniform(0.1, 0.12), 4,
                  M.fern[0], roll=rng.uniform(-0.3, 0.3))
    return acc


def build_beam(S, root):
    """The noon beam: one open tapered shaft from the oculus to the spot it lands on (the game draws it additively)."""
    acc = Acc()
    a, b = Vector(L.OCULUS), Vector(L.BEAM_SPOT)
    t = (a - b).normalized()
    u = t.cross(Vector((1, 0, 0))).normalized()
    v = t.cross(u)
    n = 24
    ring = lambda c, r: [c + (u * math.cos(2 * math.pi * k / n) + v * math.sin(2 * math.pi * k / n)) * r for k in range(n)]
    lo, hi = ring(b, L.BEAM_R_SPOT), ring(a, L.BEAM_R_TOP)
    for k in range(n):
        q = [lo[k], lo[(k + 1) % n], hi[(k + 1) % n], hi[k]]
        acc.face([G(p) for p in q], S.beam, [(k / n, 0), ((k + 1) / n, 0), ((k + 1) / n, 1), (k / n, 1)],
                 out=G(sum(q, Vector()) / 4 - (a + b) / 2), smooth=True)
    return acc.build("beam", root)


def build(kit):
    rng = random.Random(1101)
    M = base.Materials(kit, kit.load_sheet(base.SHEET), kit.load_sheet(base.HOUSE))
    S = Stone(kit)
    root = kit.root("pools-room")
    _, faces = build_shell(kit, S, root)
    decor = build_decor(S, faces, rng)
    ferns = build_ferns(M, rng)
    build_beam(S, root)

    # the bowls, the lantern, the druids' things and the small decor (travertine_props.py)
    prng = random.Random(1102)
    if _api("build_bowls"):
        props.build_bowls(kit, M, root)
    if _api("build_lantern"):
        props.build_lantern(kit, M, root)
    if _api("build_props"):
        props.build_props(kit, M, root, prng)
    else:
        base.build_crock(M, root)
        base.build_bag(M, root, prng)
        base.build_ledger(M, root)
        base.build_stool(M, root)
        base.build_rake(M, root)
    if _api("decor_items"):
        props.decor_items(kit, M, decor, ferns, prng, L.SPOTS)
    decor.build("decor", root)
    ferns.build("ferns", root)

    for name, p in L.markers().items():
        kit.marker(name, G(p), root)
    return root, 10.0
