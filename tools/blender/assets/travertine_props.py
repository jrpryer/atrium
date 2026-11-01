"""The Travertine Well's props: the travertine bowls the pools sit in, the pool lantern, the druids' things and the
room's small furniture. pools_travertine.py builds the room round them and calls this module.
Reference: art/reference/concepts/rooms-fresh/travertine/kit-travertine.jpg (the bowl's lip and bed),
kit-druid-props.jpg (everything else; sizes come from the game, never the sheet) and finish-travertine-ink-a.jpg
(the colour).

Parts (glTF node names, the keys PoolsScene asks for):
- bowl, bowl-dark, bowl-dry   the pool-local bowl frame: origin at the water's centre, 0.62 m below its surface;
          long axis +X, front +Z. Built for the largest oval (BOWL_RX x BOWL_RZ, the water's edge = the lip's inner
          edge); the game scales each to its slot by (rx / BOWL_RX, 1, rz / BOWL_RZ). Lip top +0.82 (4 cm proud of
          the terrace at +0.78, its outer face dropping straight to the hole's edge), mineral ring 0.50-0.60, bed
          +0.10; the front strip of the bed (z > 0.4 rz) stays clear.
- lantern  its own origin at the terrace's height (the game stands it at +0.78), its foot on the lip 4 cm up: an
          iron crook stake behind a small cage centred at (0, 0.54, 0), open round the game's 0.16 x 0.22 x 0.16
          glow box.
- crock, bag, ledger, stool, rake   their own origins, as in pools_room.py's docstring.
decor_items() adds the fern pots (fronds into "ferns"), the sieve, the feed shelf, the drain grate and a leaning
rake to the shell's decor, at the spots the shell chooses.

Shapes stay smooth and chunky so the inverted-hull ink is clean at 11-16 m; small detail (nodules, stitching,
embroidery, net and sieve mesh, grate slots) is painted into procedural textures. Colours are sRGB.
Game (X, Y, Z) is Blender (X, -Z, Y); the game's front (+Z) is Blender -Y.
"""
import math
import random

import numpy as np
from mathutils import Matrix, Vector

from assets.pools_room import CROCK, Acc, B, BoxUV, beam, cuboid, frond, lathe, leaf, prism_x, quad, strap, tube

TV = "pools-room-tv-prop-"
BOWL_RX, BOWL_RZ = 2.96, 1.22       # the oval the bowls are built for (the game scales them to each slot's)
TERRACE, RIM, BED = 0.78, 0.82, 0.10   # the terrace top, the lip's top (4 cm proud of it) and the bed, above the
                                       # bowl's origin
UP = Vector((0.0, 0.0, 1.0))

# ── painted colours (sRGB): (base, dark, light) ──
CREAM = ((0.90, 0.84, 0.71), (0.85, 0.78, 0.64), (0.94, 0.89, 0.78))
STRATA = [(0.90, 0.83, 0.70), (0.90, 0.78, 0.64), (0.84, 0.74, 0.60), (0.94, 0.89, 0.79)]
SLATE = ((0.55, 0.60, 0.63), (0.47, 0.52, 0.56), (0.62, 0.67, 0.69))
MOSS = [(0.36, 0.48, 0.26), (0.47, 0.59, 0.31)]


# ───────────────────────── the sweep ─────────────────────────

def _offsets(path, closed):
    """Per path point: the horizontal unit normal (out of a closed path, to the right of an open one), mitred so a
    profile set off along it keeps its width round bends. Vertical runs borrow their neighbours' heading."""
    n = len(path)
    flat = []
    for i in range(n if closed else n - 1):
        s = path[(i + 1) % n] - path[i]
        h = Vector((s.x, s.y, 0.0))
        flat.append(h.normalized() if h.length > 1e-6 else None)
    last = next((f for f in flat if f is not None), Vector((1.0, 0.0, 0.0)))
    for k, f in enumerate(flat):
        if f is None:
            flat[k] = last
        else:
            last = f
    offs = []
    for i in range(n):
        ta, tb = (flat[i - 1], flat[i]) if closed else (flat[max(i - 1, 0)], flat[min(i, n - 2)])
        t = ta + tb
        t = t.normalized() if t.length > 1e-6 else tb
        nrm = Vector((t.y, -t.x, 0.0))
        offs.append(nrm / max(nrm.dot(Vector((tb.y, -tb.x, 0.0))), 0.5))
    if closed and sum(a.x * b.y - b.x * a.y for a, b in zip(path, path[1:] + path[:1])) < 0:
        offs = [-o for o in offs]   # a clockwise loop: +d still points out
    return offs


def sweep(acc, profile, path, mat, closed=True, uv_scale=1.0, smooth=True):
    """A constant-width (d, h) profile swept along a path (Blender coordinates, Z up): each profile point is set off
    d along the path's horizontal normal and h straight up, so a rolled lip keeps one width all the way round an
    oval (lathe(scale=...) stretches it 2.4x at the ends). The profile is listed like lathe's, anticlockwise round
    the solid with d in lathe's r: +d points out of a closed path (whichever way it runs) and to the right of an
    open one. `mat` and `smooth` may be functions of the profile segment. UVs run by arc length at `uv_scale`
    metres per repeat: u along the path (a closed path fits a whole number of repeats), v along the profile."""
    P = [Vector(p) for p in path]
    n, m = len(P), len(profile)
    offs = _offsets(P, closed)
    rings = [[P[i] + offs[i] * d + UP * h for d, h in profile] for i in range(n)]
    s = [0.0]
    for i in range(n if closed else n - 1):
        s.append(s[-1] + (P[(i + 1) % n] - P[i]).length)
    su = s[-1] / max(1, round(s[-1] / uv_scale)) if closed else uv_scale
    t = [0.0]
    for (d0, h0), (d1, h1) in zip(profile, profile[1:]):
        t.append(t[-1] + math.hypot(d1 - d0, h1 - h0))
    for i in range(n if closed else n - 1):
        i1 = (i + 1) % n
        side = (offs[i] + offs[i1]).normalized()
        for k in range(m - 1):
            (d0, h0), (d1, h1) = profile[k], profile[k + 1]
            q = [rings[i][k], rings[i1][k], rings[i1][k + 1], rings[i][k + 1]]
            uv = [(s[i] / su, t[k] / uv_scale), (s[i + 1] / su, t[k] / uv_scale),
                  (s[i + 1] / su, t[k + 1] / uv_scale), (s[i] / su, t[k + 1] / uv_scale)]
            acc.face(q, mat(k) if callable(mat) else mat, uv, out=side * (h1 - h0) - UP * (d1 - d0),
                     smooth=smooth(k) if callable(smooth) else smooth)


# ───────────────────────── painted textures ─────────────────────────

def _field(rng, h, w, octaves):
    """Seamless noise in 0..1: sines with whole-number frequencies across the tile."""
    y, x = np.mgrid[0:h, 0:w]
    y, x = y / h, x / w
    n = np.zeros((h, w))
    for f, a in octaves:
        for _ in range(3):
            fx, fy = rng.integers(-f, f + 1, 2)
            n += a * np.sin(2 * np.pi * (fx * x + fy * y) + rng.uniform(0, 6.28))
    return (n - n.min()) / max(n.max() - n.min(), 1e-9)


def _tones(field, cols, cuts=(0.33, 0.7)):
    """A field posterised into flat gouache tones, cols (dark, base, light)."""
    return np.array(cols, dtype=np.float64)[np.digitize(field, cuts)]


def _stone(rng, c, h=256, w=256):
    return _tones(_field(rng, h, w, ((2, 1.0), (4, 0.5), (8, 0.25))), [c[1], c[0], c[2]])


def _wrap(d, n):
    """The signed distance across a seamless tile."""
    return (d + n / 2) % n - n / 2


def _nodules(img, rng, n, r0, r1, light, shade):
    """Round travertine nodules on a seamless tile: a lit dome over a shaded crescent each, the later ones
    overlapping the earlier, like the cauliflower beads on a rimstone lip."""
    h, w = img.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    for _ in range(n):
        cy, cx, r = rng.uniform(0, h), rng.uniform(0, w), rng.uniform(r0, r1)
        dy, dx = _wrap(yy - cy, h), _wrap(xx - cx, w)
        img[np.hypot(dx, dy) < r] = shade
        img[np.hypot(dx, dy + 0.22 * r) < 0.8 * r] = light
    return img


def _bands(rng, h, w, cols, n):
    """Wavy horizontal strata (flowstone), seamless both ways."""
    y, x = np.mgrid[0:h, 0:w]
    v = y / h + 0.035 * np.sin(2 * np.pi * 2 * x / w + rng.uniform(0, 6.28)) \
        + 0.015 * np.sin(2 * np.pi * 5 * x / w + rng.uniform(0, 6.28))
    edges = np.sort(rng.uniform(0, 1, n))
    order = [0]
    while len(order) < n:   # a run of colours, never the same twice in a row
        c = int(rng.integers(len(cols)))
        if c != order[-1] and (len(order) < n - 1 or c != order[0]):
            order.append(c)
    return np.array(cols, dtype=np.float64)[np.array(order)[np.digitize(v % 1.0, edges) % n]]


def _moss(img, rng, amount):
    """Soft moss patches over a seamless tile (two greens)."""
    f = _field(rng, img.shape[0], img.shape[1], ((2, 1.0), (4, 0.5), (7, 0.15)))
    img[f > 1 - amount] = MOSS[0]
    img[f > 1 - amount * 0.55] = MOSS[1]
    return img


def _oak(img, base, ang, length, width, fill, lit, rib):
    """An embroidered oak leaf: a lobed blade round its midrib, from its stalk at `base` (row, col) along `ang`."""
    h, w = img.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    dy, dx = yy - base[0], xx - base[1]
    c, s = math.cos(ang), math.sin(ang)
    a = (dx * c + dy * s) / length
    q = -dx * s + dy * c
    half = width * np.sin(np.pi * np.clip(a, 0, 1)) ** 0.75 * (0.66 + 0.34 * np.abs(np.cos(np.pi * 4.5 * a)))
    inside = (a > 0.05) & (a < 1) & (np.abs(q) < half)
    img[inside] = fill
    img[inside & (q > 0)] = lit
    img[(inside & (np.abs(q) > half - 2.5)) | ((a > -0.12) & (a < 0.9) & (np.abs(q) < 1.6))] = rib


def _image(kit, name, rgb):
    return kit._image_from_array(TV + name, np.dstack([np.clip(rgb, 0.0, 1.0), np.ones(rgb.shape[:2])]))


class _Tex:
    """This module's own materials (names "pools-room-tv-prop-..."), painted once per build."""

    def __init__(self, kit):
        rng = np.random.default_rng(1101)
        lin = kit.srgb_to_linear

        def mat(name, rgb=None, col=(0.8, 0.8, 0.8)):
            return kit.material(TV + name, _image(kit, name, rgb) if rgb is not None else None, lin(col))

        # ── the bowls: cream lip with painted nodules, banded flowstone outside, wet tan under the water down to a
        #    mid-dark bed (the water draws over it; a pale bed would make every pool milky) ──
        self.lip = mat("lip", _nodules(_stone(rng, CREAM), rng, 70, 7, 15, (0.96, 0.92, 0.82), (0.80, 0.72, 0.58)))
        self.flowstone = mat("flowstone", _bands(rng, 256, 256, STRATA, 5))
        self.wet = mat("wet", _stone(rng, ((0.55, 0.47, 0.35), (0.47, 0.40, 0.30), (0.61, 0.53, 0.40))))
        self.ring = mat("ring", col=(0.88, 0.85, 0.75))
        self.bed = mat("bed", _stone(rng, ((0.38, 0.35, 0.27), (0.32, 0.30, 0.23), (0.44, 0.40, 0.31))))
        # the shaded pool's: wet blue-grey, mossy
        self.lip_dark = mat("lip-dark", _moss(_stone(rng, SLATE), rng, 0.3))
        self.wall_dark = mat("wall-dark", _bands(rng, 256, 256, [SLATE[0], SLATE[1], SLATE[2], (0.51, 0.56, 0.60)], 6))
        self.wet_dark = mat("wet-dark", _stone(rng, ((0.36, 0.40, 0.42), (0.31, 0.35, 0.37), (0.41, 0.45, 0.47))))
        self.ring_dark = mat("ring-dark", col=(0.68, 0.72, 0.72))
        self.bed_dark = mat("bed-dark", _stone(rng, ((0.24, 0.27, 0.29), (0.20, 0.23, 0.25), (0.29, 0.32, 0.34))))
        self.boulder = mat("boulder", _moss(_stone(rng, ((0.52, 0.58, 0.62), (0.43, 0.49, 0.54), (0.60, 0.66, 0.69))), rng, 0.38))
        # the empty slot's: dry and pale, a white ring where the water stood, a crazed bed
        self.wet_dry = mat("wet-dry", _stone(rng, ((0.80, 0.73, 0.60), (0.75, 0.68, 0.55), (0.85, 0.79, 0.66))))
        self.ring_dry = mat("ring-dry", col=(0.96, 0.95, 0.91))
        bed = _stone(rng, ((0.80, 0.74, 0.62), (0.74, 0.67, 0.55), (0.85, 0.80, 0.68)))
        r = _field(rng, 256, 256, ((3, 1.0), (6, 0.4)))
        bed[np.abs(r - 0.5) < 0.012] *= 0.72
        self.bed_dry = mat("bed-dry", bed)
        self.litter = [mat("litter-" + str(i), col=c) for i, c in enumerate(((0.70, 0.50, 0.28), (0.56, 0.36, 0.22), (0.80, 0.62, 0.30)))]

        # ── iron, wood ──
        self.iron = mat("iron", col=(0.22, 0.23, 0.25))
        self.handle = mat("handle", col=(0.66, 0.50, 0.33))
        self.head = mat("rake-head", col=(0.55, 0.40, 0.26))
        self.hoop = mat("hoop", col=(0.42, 0.30, 0.20))
        self.bentwood = mat("bentwood", col=(0.74, 0.58, 0.40))
        y, x = np.mgrid[0:256, 0:256]
        grain = _field(rng, 256, 256, ((1, 1.0), (2, 0.5)))
        grain = 0.6 * grain + 0.4 * (0.5 + 0.5 * np.sin(2 * np.pi * (11 * x / 256 + 1.5 * grain)))
        planks = _tones(grain, [(0.63, 0.46, 0.28), (0.71, 0.53, 0.33), (0.78, 0.60, 0.39)], (0.3, 0.72))
        planks[(x % 64 < 3) | (x % 64 > 61)] = (0.38, 0.26, 0.16)   # four boards, their seams
        self.planks = mat("lid-planks", planks)

        # ── the crock: slate glaze over raw biscuit, the glaze dribbling past its line (v 0.643 = 0.45 m) ──
        self.glaze = mat("glaze", _stone(rng, ((0.47, 0.55, 0.58), (0.40, 0.47, 0.50), (0.55, 0.63, 0.66)), 128, 128))
        raw = _stone(rng, ((0.80, 0.72, 0.60), (0.73, 0.65, 0.53), (0.85, 0.78, 0.66)), 128, 128)
        yy, xx = np.mgrid[0:128, 0:128]
        line = (1 - 0.45 / 0.70) * 128
        for _ in range(9):
            cx, ln, wd = rng.uniform(0, 128), rng.uniform(6, 16), rng.uniform(3, 4.5)
            dx = _wrap(xx - cx, 128)
            tip = line + ln
            raw[(np.abs(dx) < wd) & (yy < tip) & (yy > line - 2)] = (0.47, 0.55, 0.58)
            raw[np.hypot(dx, yy - tip) < wd] = (0.47, 0.55, 0.58)
        raw[yy < line] = (0.47, 0.55, 0.58)
        self.raw = mat("biscuit", raw)
        self.inside = mat("crock-inside", col=(0.20, 0.23, 0.22))
        net = np.full((128, 128, 3), (0.36, 0.35, 0.30))
        net[((xx + yy) % 16 < 3) | ((xx - yy) % 16 < 3)] = (0.84, 0.80, 0.66)
        self.net = mat("net", net)

        # ── the bag: oiled canvas (the embroidery, eyelets and drawstring painted on), leather base band ──
        H, W = 512, 1024
        img = _tones(_field(rng, H, W, ((2, 1.0), (5, 0.5), (11, 0.25))), [(0.53, 0.51, 0.40), (0.60, 0.58, 0.46), (0.66, 0.64, 0.52)], (0.3, 0.72))
        yy, xx = np.mgrid[0:H, 0:W]
        row = lambda z: (1 - z / BAG_TOP) * H
        img[np.abs(_wrap(xx - 0.25 * W, W)) < 2.5] = (0.42, 0.40, 0.31)   # the back seam
        r0 = row(0.9)
        for k in range(8):   # eyelets round the neck, the cord threaded in and out between them
            d = np.hypot(_wrap(xx - (k + 0.5) / 8 * W, W), yy - r0)
            img[d < 9] = (0.70, 0.60, 0.38)
            img[d < 5] = (0.15, 0.13, 0.10)
        f = ((xx / W * 8 - 0.5) % 1.0)
        span = np.floor(xx / W * 8 - 0.5).astype(int) % 2 == 0
        img[span & (np.abs(yy - r0 - 7 * np.sin(np.pi * f)) < 2.6) & (f > 0.12) & (f < 0.88)] = (0.40, 0.30, 0.22)
        for sgn in (-1, 1):   # its two tails down the front, knotted
            zt = BAG_TOP * (1 - yy / H)
            cx = 0.75 * W + sgn * (6 + 40 * np.clip(0.9 - zt, 0, 1))
            img[(np.abs(xx - cx) < 2.6) & (zt < 0.9) & (zt > 0.66 + 0.02 * sgn)] = (0.40, 0.30, 0.22)
            img[np.hypot(xx - (0.75 * W + sgn * (6 + 40 * (0.24 - 0.02 * sgn))), yy - row(0.66 + 0.02 * sgn)) < 8] = (0.33, 0.24, 0.17)
        cx = 0.75 * W   # the oak sprig embroidered on the front, clear of the band: a stem, leaves at three heights, acorns
        img[(np.abs(xx - cx - 4 * np.sin((yy - row(0.37)) / 30)) < 2) & (yy > row(0.51)) & (yy < row(0.37))] = (0.25, 0.38, 0.18)
        for z, ang, ln, wd in ((0.51, -math.pi / 2, 104, 24), (0.42, -math.pi / 2 - 1.15, 88, 21), (0.46, -math.pi / 2 + 1.15, 88, 21)):
            _oak(img, (row(z), cx), ang, ln, wd, (0.45, 0.62, 0.30), (0.55, 0.70, 0.36), (0.25, 0.38, 0.18))
        for dxa in (-13, 12):
            ay = row(0.355)
            img[((xx - cx - dxa) / 9) ** 2 + ((yy - ay) / 12) ** 2 < 1] = (0.66, 0.48, 0.26)
            img[(((xx - cx - dxa) / 10) ** 2 + ((yy - ay + 7) / 7) ** 2 < 1) & (yy < ay - 3)] = (0.42, 0.31, 0.19)
        self.canvas = mat("canvas", img)
        band = _tones(_field(rng, 128, 512, ((2, 1.0), (6, 0.4))), [(0.44, 0.28, 0.18), (0.52, 0.34, 0.22), (0.58, 0.40, 0.26)])
        y, x = np.mgrid[0:128, 0:512]
        band[y < 5] = (0.36, 0.23, 0.15)
        band[(y > 13) & (y < 18) & (x % 12 < 7)] = (0.86, 0.78, 0.60)   # the stitching along its top
        self.band = mat("leather-band", band)
        self.cord = mat("cord", col=(0.42, 0.31, 0.22))
        self.strap = mat("strap", col=(0.48, 0.32, 0.21))
        self.opening = mat("bag-opening", col=(0.12, 0.11, 0.09))

        # ── the ledger, the stool's mat ──
        self.cover = mat("book-cover", col=(0.32, 0.45, 0.32))
        self.ink = mat("ink-pot", col=(0.20, 0.42, 0.30))
        y, x = np.mgrid[0:256, 0:256]
        rr, th = np.hypot(x - 127.5, y - 127.5), np.arctan2(y - 127.5, x - 127.5)
        ring = (rr // 7).astype(int)
        coil = np.where((ring % 2 == 0)[..., None], np.array((0.86, 0.77, 0.52)), np.array((0.78, 0.68, 0.44)))
        coil[(ring % 2 == 0) & (((th / (2 * np.pi)) * (8 + ring)) % 1.0 < 0.07)] = (0.58, 0.47, 0.29)
        coil[rr > 121] = (0.60, 0.50, 0.32)
        self.coil = mat("reed-coil", coil)

        # ── decor: terracotta, the sieve's brass mesh, the drain grate's slots ──
        pot = _stone(rng, ((0.76, 0.44, 0.28), (0.68, 0.38, 0.24), (0.82, 0.52, 0.34)), 128, 256)
        y, x = np.mgrid[0:128, 0:256]
        pot[y > 108 + 5 * np.sin(2 * np.pi * 3 * x / 256)] = (0.62, 0.37, 0.25)   # a damp stain round the foot
        self.terracotta = mat("terracotta", pot)
        y, x = np.mgrid[0:256, 0:256]
        mesh = np.full((256, 256, 3), (0.32, 0.27, 0.20))
        mesh[(x % 18 < 4) | (y % 18 < 4)] = (0.80, 0.66, 0.38)
        self.mesh = mat("sieve-mesh", mesh)
        grate = np.full((256, 256, 3), (0.28, 0.29, 0.31))
        edge = np.minimum(np.minimum(x, 255 - x), np.minimum(y, 255 - y))
        grate[edge < 12] = (0.40, 0.41, 0.43)
        for cx in (76, 180):
            for cy in (50, 102, 154, 206):
                grate[(np.abs(x - cx) / 40) ** 4 + (np.abs(y - cy) / 14) ** 4 < 1] = (0.07, 0.07, 0.08)
        self.grate = mat("grate", grate)
        self.jars = [mat("jar-" + n, col=c) for n, c in (("cream", (0.86, 0.81, 0.68)), ("sage", (0.56, 0.64, 0.55)),
                                                          ("brown", (0.55, 0.40, 0.28)), ("blue", (0.52, 0.60, 0.66)))]


def _tex(kit, M):
    """The module's materials, painted on first use and kept on the shell's Materials."""
    if getattr(M, "tv_props", None) is None:
        M.tv_props = _Tex(kit)
    return M.tv_props


# ───────────────────────── the bowls (pool-local) ─────────────────────────

# the bowl's cross-section (d out from the water's edge, h up from the bowl's origin), listed like lathe's: straight
# up the outer wall at the hole's edge (d 0.28) from below the bed to 2 cm above the terrace, round a small shoulder
# onto the flat top 4 cm proud of the terrace (one raised rim, one ink line), round the roll that leans a little over
# the water, then down under it past the mineral ring to the bed's edge. The offset oval runs a hair outside the
# hole's at the diagonals, so the terrace never shows a gap round the lip.
LIP = [(0.28, -0.2), (0.28, 0.70), (0.28, 0.80), (0.262, 0.815), (0.225, RIM), (0.07, RIM), (0.025, 0.81),
       (0.0, 0.79), (-0.025, 0.75), (-0.025, 0.68), (0.0, 0.62), (0.0, 0.60), (-0.03, 0.50), (-0.12, 0.32),
       (-0.26, 0.17), (-0.36, BED)]


def _oval(n=72):
    """The water's edge, anticlockwise from above (uniform in angle: densest at the tight ends)."""
    return [Vector((BOWL_RX * math.cos(2 * math.pi * i / n), BOWL_RZ * math.sin(2 * math.pi * i / n), 0.0)) for i in range(n)]


def _bowl(lip, wall, wet, ring, bed):
    """One bowl: the swept lip and walls, then the flat bed filling the oval."""
    acc = Acc()
    path = _oval()
    sweep(acc, LIP, path, lambda k: wall if k < 1 else lip if k < 10 else ring if k == 11 else wet, uv_scale=0.8)
    d, h = LIP[-1]
    edge = [p + o * d for p, o in zip(path, _offsets(path, True))]
    rings = [[Vector((e.x * f, e.y * f, h)) for e in edge] for f in (1.0, 0.68, 0.36)]
    uv = BoxUV(1.6)
    n = len(path)
    for a, b in zip(rings, rings[1:]):
        for i in range(n):
            acc.face([a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]], bed, uv, out=UP)
    for i in range(n):
        acc.face([rings[-1][i], rings[-1][(i + 1) % n], Vector((0, 0, h))], bed, uv, out=UP)
    return acc


def _boulders(acc, T, rng):
    """A ring of low rounded boulders set into the lip: at most 0.4 m above the rim at the front (the bed's front
    strip must show at 35 degrees), taller at the back and the ends; gaps at the back-left, where the runnel falls
    in, and the front-left, where the outflow leaves."""
    prof = [(0, 0), (0.9, 0), (1.0, 0.2), (0.93, 0.52), (0.7, 0.82), (0.36, 0.97), (0, 1.0)]
    n = 15
    for k in range(n):
        t = 2 * math.pi * (k + rng.uniform(-0.2, 0.2)) / n
        deg = math.degrees(t) % 360
        # (the front-left gap starts at the end itself: a boulder there would stand in the low outflow arch)
        if 100 < deg < 128 or 186 < deg < 234:
            continue
        back = (1 + math.sin(t)) / 2                       # 0 at the front (Blender -Y), 1 at the back
        r = 0.36 + 0.2 * back + rng.uniform(0, 0.12)        # broad, low slabs, never balls
        rise = 0.2 + 0.42 * back + rng.uniform(0, 0.1)      # its top above the terrace
        nrm = Vector((BOWL_RZ * math.cos(t), BOWL_RX * math.sin(t), 0)).normalized()
        c = Vector((BOWL_RX * math.cos(t), BOWL_RZ * math.sin(t), TERRACE - 0.12)) + nrm * (0.22 + 0.8 * r)
        X = Matrix.Translation(c) @ Matrix.Rotation(math.atan2(nrm.y, nrm.x) + math.pi / 2, 4, 'Z')
        jit = {(kk, j): 1 + (rng.random() - 0.5) * 0.18 for kk in range(len(prof)) for j in range(10)}
        lathe(acc, (0, 0, 0), [(rr * r, z * (TERRACE - c.z + rise)) for rr, z in prof], 10, T.boulder, BoxUV(1.0),
              smooth=True, M=X, scale=(rng.uniform(1.2, 1.5), 1.0), jitter=lambda kk, j: jit[(kk, j)], phase=rng.uniform(0, 6.28))


def build_bowls(kit, M, root):
    """bowl (a lit pool's), bowl-dark (a pool whose base Light < 0) and bowl-dry (a slot with no pool)."""
    T = _tex(kit, M)
    rng = random.Random(1101)
    a = _bowl(T.lip, T.flowstone, T.wet, T.ring, T.bed)
    bowl = a.build("bowl", root)
    a = _bowl(T.lip_dark, T.wall_dark, T.wet_dark, T.ring_dark, T.bed_dark)
    _boulders(a, T, rng)
    dark = a.build("bowl-dark", root)
    # the dry bowl's few dead leaves lie on the back of the bed, clear of its front strip
    a = _bowl(T.lip, T.flowstone, T.wet_dry, T.ring_dry, T.bed_dry)
    placed = 0
    while placed < 10:
        x, y = rng.uniform(-0.8, 0.8) * BOWL_RX, rng.uniform(-0.05, 0.75) * BOWL_RZ
        if (x / (BOWL_RX - 0.45)) ** 2 + (y / (BOWL_RZ - 0.45)) ** 2 > 0.85:
            continue
        ang = rng.uniform(0, 6.28)
        leaf(a, (x, y, BED + 0.012), (rng.uniform(-0.2, 0.2), rng.uniform(-0.2, 0.2), 1.0), (math.cos(ang), math.sin(ang), 0.0),
             rng.uniform(0.14, 0.2), T.litter[placed % 3])
        placed += 1
    dry = a.build("bowl-dry", root)
    return bowl, dark, dry


# ───────────────────────── the pool lantern ─────────────────────────

def build_lantern(kit, M, root):
    """An iron shepherd's-crook stake standing on the lip behind (-Z) its cage; the crook comes over and hangs a
    small square iron cage centred at (0, 0.54, 0), its sides open round the game's glow box. The origin is the
    terrace's height; the staff is driven into the lip and its foot collar sits on the lip's top 4 cm up."""
    T = _tex(kit, M)
    acc = Acc()
    # the staff behind the cage (close enough that its foot stays on the narrowest slot's lip), where it bends, the
    # crook, the cage
    sy, top, rc, c = 0.16, 0.9, 0.08, 0.54
    crook = [Vector((0, sy, z)) for z in (0.0, 0.3, 0.6, top)]
    crook += [Vector((0, sy - rc + rc * math.cos(a), top + rc * math.sin(a))) for a in np.linspace(0, math.pi, 9)[1:]]
    crook.append(Vector((0, sy - 2 * rc, top - 0.06)))
    tube(acc, crook, 0.02, T.iron, sides=6)
    f = RIM - TERRACE - 0.005
    lathe(acc, (0, sy, f), [(0, 0), (0.038, 0), (0.038, 0.06), (0.022, 0.08), (0, 0.08)], 6, T.iron)
    # the cage: floor and top plates, four corner posts clear of the 0.16 x 0.22 x 0.16 glow, a pyramid roof
    cuboid(acc, (-0.12, -0.12, c - 0.14), (0.12, 0.12, c - 0.11), T.iron)
    for px in (-0.105, 0.105):
        for py in (-0.105, 0.105):
            cuboid(acc, (px - 0.016, py - 0.016, c - 0.11), (px + 0.016, py + 0.016, c + 0.12), T.iron)
    cuboid(acc, (-0.125, -0.125, c + 0.12), (0.125, 0.125, c + 0.15), T.iron)
    lathe(acc, (0, 0, c + 0.15), [(0, 0), (0.177, 0), (0.03, 0.09), (0, 0.1)], 4, T.iron, phase=math.pi / 4)
    beam(acc, (0, 0, c + 0.23), (0, 0, top - 0.04), 0.03, T.iron)
    return acc.build("lantern", root)


# ───────────────────────── the druids' things (each in its own frame) ─────────────────────────

CROCK_G = CROCK[:7] + [(0.668, 0.45)] + CROCK[7:]   # pools_room's squat crock, with a point where the glaze stops


def _crock(T, M, root):
    """The holding crock: slate glaze over the raw biscuit below 0.45 m (rim 0.70, opening r 0.52, belly r 0.675),
    its cleated wooden lid (r 0.62) leaning on the rim behind it, a landing net leaning on its right."""
    acc = Acc()
    lathe(acc, (0, 0, 0), CROCK_G, 20, lambda i: T.raw if i <= 6 else T.glaze if i <= 16 else T.inside,
          lambda i, j, k: (j / 20 * 3, min(CROCK_G[k][1], 0.70) / 0.70), smooth=lambda i: 1 <= i <= 9 or 17 <= i <= 18)
    # the lid stands behind on its edge, 12 degrees off upright, its face resting on the rim bead
    n = Vector((0.0, -math.cos(0.21), -math.sin(0.21)))
    L = Matrix.Translation((0.0, 0.657, 0.61)) @ Vector((0, 0, 1)).rotation_difference(n).to_matrix().to_4x4()

    def planar(i, j, k, r=0.62):
        rad, a = [(0, 0), (r, 0), (r, 0), (0, 0)][k][0], 2 * math.pi * j / 24
        return (0.5 + 0.5 * rad / r * math.cos(a), 0.5 + 0.5 * rad / r * math.sin(a))

    lathe(acc, (0, 0, 0), [(0, -0.06), (0.62, -0.06), (0.62, 0.0), (0, 0.0)], 24, T.planks, planar, M=L)
    for y in (-0.3, 0.3):
        cuboid(acc, (-0.42, y - 0.045, 0.0), (0.42, y + 0.045, 0.035), T.hoop, M=L)
    # the landing net: its handle's foot on the floor at the front right, the hoop resting by the rim, the net
    # hanging from it (a textured shell, never strands)
    H = Vector((0.80, -0.20, 0.86))
    ax = Vector((0.45, -0.1, -0.88)).normalized()
    F = Vector((0.93, -0.62, 0.0))
    side = (F - H) - ax * (F - H).dot(ax)
    d = H + side.normalized() * 0.17 - F
    R = Matrix.Translation(F) @ Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    lathe(acc, (0, 0, 0), [(0, 0), (0.021, 0), (0.021, d.length), (0, d.length)], 6, T.handle, M=R, smooth=lambda i: i == 1)
    lathe(acc, (0, 0, 0.02), [(0, 0), (0.026, 0), (0.026, 0.3), (0, 0.3)], 6, M.leather, M=R)
    N = Matrix.Translation(H) @ Vector((0, 0, 1)).rotation_difference(ax).to_matrix().to_4x4()
    tube(acc, [N @ Vector((0.17 * math.cos(a), 0.17 * math.sin(a), 0)) for a in np.linspace(0, 2 * math.pi, 13)[:-1]],
         0.016, T.hoop, 5, closed=True)
    net = [(0.155, 0), (0.15, 0.1), (0.12, 0.2), (0.06, 0.28), (0, 0.3), (0, 0.27), (0.05, 0.25), (0.11, 0.18),
           (0.14, 0.09), (0.145, 0), (0.155, 0)]
    lathe(acc, (0, 0, 0), net, 12, T.net, lambda i, j, k: (j / 12 * 3, net[k][1] / 0.15), M=N, smooth=True)
    return acc.build("crock", root)


BAG_TOP = 1.13
# the bucket bag, listed like lathe's: a flat base rounding up into the leather band (to 0.30), the soft canvas body
# tapering a little to its shoulder, gathered hard in folds to the drawstring cinch at 0.965-0.985, the puckered
# frill standing up above it to 1.13, and down inside to the small dark opening
BAG = [(0, 0.0), (0.40, 0.0), (0.435, 0.025), (0.45, 0.08), (0.445, 0.30), (0.43, 0.50), (0.41, 0.68), (0.395, 0.80),
       (0.36, 0.87), (0.27, 0.925), (0.17, 0.955), (0.13, 0.965), (0.13, 0.985), (0.15, 1.02), (0.175, 1.07),
       (0.18, 1.11), (0.15, BAG_TOP), (0.09, 1.115), (0.07, 1.08), (0, 1.07)]
# how deep the gathered folds run at each profile point (they start on the shoulder and are deepest in the frill)
BAG_FOLD = {6: 0.03, 7: 0.08, 8: 0.15, 9: 0.18, 10: 0.08, 11: 0.05, 12: 0.12, 13: 0.15, 14: 0.16, 15: 0.15, 16: 0.12, 17: 0.08}


def _bag(T, M, root, rng):
    """The druids' bag: a drawstring bucket bag of oiled canvas on a flat leather-banded base, the oak sprig
    embroidered on its front (+Z), slumping a little; its mouth drawn tight in chunky folds into a puckered frill
    round a small dark opening (the game's glow sits at 1.18 m); a long shoulder strap from the back of its left
    shoulder slung over the top against the back of the frill and down its right side to the band, all inside the
    1.1 x 0.9 m footprint."""
    acc = Acc()
    ph = rng.uniform(0, 6.28)
    seg = 20
    depth = [rng.uniform(0.55, 1.25) for _ in range(seg)]     # each fold its own depth, so the frill never reads as a star

    def jit(k, j):
        if k in BAG_FOLD:
            return 1 + BAG_FOLD[k] * depth[j] * (1 if j % 2 else -1)   # ten pleats, running down from the cinch
        return 1 + 0.022 * math.sin(1.3 * j + 1.7 * k + ph) if 3 <= k <= 5 else 1.0

    def mat(i):
        return T.band if i <= 3 else T.cord if i == 11 else T.opening if i >= 17 else T.canvas

    def uv(i, j, k):
        return (j / seg * 2, BAG[k][1] / 0.30) if i <= 3 else (j / seg, BAG[k][1] / BAG_TOP)

    lathe(acc, (0, 0, 0), BAG, seg, mat, uv, smooth=lambda i: 1 <= i <= 16, scale=(1.0, 0.86), jitter=jit)
    # the strap: sewn on at the back of the left shoulder, slung up over the top against the back of the frill
    # (behind the glow; it lies on the bag, never stands up like a handle), over the right shoulder and down the
    # right side bowed a little off the body by its slack, into the band at the right front
    pts = [(-0.31, 0.10, 0.88), (-0.25, 0.17, 0.97), (-0.12, 0.205, 1.06), (0.03, 0.21, 1.10), (0.16, 0.19, 1.065),
           (0.27, 0.14, 0.98), (0.37, 0.06, 0.885), (0.44, -0.01, 0.74), (0.48, -0.05, 0.54), (0.485, -0.09, 0.33),
           (0.455, -0.13, 0.17)]
    strap(acc, pts, 0.09, 0.03, T.strap, Vector((0, 1, 0)))
    for p, ang in ((pts[0], 2.6), (pts[-1], -0.3)):              # the leather tabs it is stitched to
        X = Matrix.Translation(p) @ Matrix.Rotation(ang, 4, 'Z')
        cuboid(acc, (-0.02, -0.06, -0.07), (0.02, 0.06, 0.05), T.strap, M=X)
    # the canvas slumps a little to the back left as it rises (the band and base stay square on the floor)
    for v in acc.verts:
        k = min(1.0, max(0.0, (v.z - 0.3) / (BAG_TOP - 0.3)))
        k = k * k * (3 - 2 * k)
        v.x -= 0.04 * k
        v.y += 0.015 * k
    return acc.build("bag", root)


def _ledger(T, M, root):
    """The breeding ledger on pools_room's trestle (two legs on feet, braces, a stretcher, rails), carrying the
    sheet's slanted book box: a wedge whose top is the reading slope (tilt 0.42 about its 1.08 m centre, facing +Z)
    with a book stop and the open book; the candle on an iron bracket off its right side, the ink pot on a little
    shelf off the left leg."""
    acc = Acc()
    wh = BoxUV(M.wood_tile)
    wv = BoxUV((M.wood_tile[1], M.wood_tile[0]))
    for sx in (-1, 1):
        x = sx * 0.3
        cuboid(acc, (x - 0.05, -0.22, 0.0), (x + 0.05, 0.22, 0.08), M.wood, wh)
        cuboid(acc, (x - 0.035, -0.035, 0.08), (x + 0.035, 0.035, 0.93), M.wood_v, wv)
        beam(acc, (x - sx * 0.02, 0, 0.66), (sx * 0.13, 0, 0.92), 0.045, M.wood, wh)
        a, b = sorted((sx * 0.32, sx * 0.37))
        cuboid(acc, (a, -0.2, 0.9), (b, 0.2, 0.97), M.wood, wh)
    cuboid(acc, (-0.3, -0.03, 0.22), (0.3, 0.03, 0.3), M.wood, wh)
    for y0, y1 in ((-0.2, -0.15), (0.15, 0.2)):
        cuboid(acc, (-0.37, y0, 0.9), (0.37, y1, 0.97), M.wood, wh)
    S = Matrix.Translation((0, -0.02, 1.08)) @ Matrix.Rotation(0.42, 4, 'X')
    cs, sn = math.cos(0.42), math.sin(0.42)

    def under(y):   # the slope board's underside at world y
        return 1.08 + (y + 0.02 - 0.02 * sn) / cs * sn - 0.02 * cs

    prism_x(acc, [(-0.25, 0.93), (0.25, 0.93), (0.25, under(0.25)), (-0.25, under(-0.25))], -0.36, 0.36, M.wood, wh)
    cuboid(acc, (-0.38, -0.29, -0.02), (0.38, 0.29, 0.02), M.wood, wh, M=S)
    cuboid(acc, (-0.38, -0.32, -0.02), (0.38, -0.285, 0.07), M.wood_dark, M=S)
    cuboid(acc, (-0.36, -0.255, 0.02), (0.36, 0.255, 0.035), T.cover, M=S)
    cuboid(acc, (-0.345, -0.235, 0.035), (-0.008, 0.235, 0.064), M.page, M=S)
    cuboid(acc, (0.008, -0.235, 0.035), (0.345, 0.235, 0.064), M.page, M=S)
    R = S.to_3x3()
    quad(acc, S @ Vector((0, 0, 0.0645)), R @ Vector((1, 0, 0)), R @ Vector((0, 1, 0)), 0.69, 0.47, M.book, R @ Vector((0, 0, 1)))
    cuboid(acc, (0.05, -0.30, 0.064), (0.075, -0.1, 0.069), M.ribbon, M=S)
    cuboid(acc, (0.05, -0.335, -0.1), (0.075, -0.322, 0.066), M.ribbon, M=S)
    # the candle on its bracket at the box's side midpoint: (0.45, 0.99-1.2, 0.05) in the game's frame
    cuboid(acc, (0.36, -0.09, 0.94), (0.375, -0.01, 1.02), T.iron)
    cuboid(acc, (0.375, -0.065, 0.955), (0.43, -0.035, 0.975), T.iron)
    lathe(acc, (0.45, -0.05, 0.955), [(0, 0), (0.058, 0), (0.06, 0.035), (0, 0.035)], 8, T.iron)
    lathe(acc, (0.45, -0.05, 0.99), [(0, 0), (0.032, 0), (0.032, 0.14), (0.02, 0.15), (0, 0.15)], 8, M.candle,
          smooth=lambda i: i == 1)
    lathe(acc, (0.45, -0.05, 1.13), [(0, 0), (0.018, 0.02), (0.012, 0.05), (0, 0.075)], 6, M.flame)
    # the ink pot on its little shelf off the left leg (top 0.94)
    cuboid(acc, (-0.58, -0.12, 0.905), (-0.335, 0.06, 0.94), M.wood_dark)
    beam(acc, (-0.335, -0.03, 0.78), (-0.52, -0.03, 0.905), 0.03, M.wood_dark)
    lathe(acc, (-0.47, -0.03, 0.94), [(0, 0), (0.052, 0), (0.06, 0.035), (0.052, 0.07), (0.026, 0.08), (0.026, 0.105), (0, 0.105)],
          10, T.ink, smooth=lambda i: i in (1, 2))
    lathe(acc, (-0.47, -0.03, 1.045), [(0, 0), (0.03, 0), (0.032, 0.03), (0, 0.03)], 8, M.cork)
    return acc.build("ledger", root)


def _stool(T, M, root):
    """A three-legged stool (seat top 0.55, r 0.28, its painted end grain on top) on a round coiled reed mat."""
    acc = Acc()
    seat = [(0, 0), (0.27, 0), (0.28, 0.012), (0.28, 0.088), (0.27, 0.10), (0, 0.10)]

    def uv(i, j, k):
        if i == 4:  # the top: the painted seat, mapped back from its ellipse onto the circle
            r, a = seat[k][0], 2 * math.pi * j / 16
            return (0.5 + 0.5 * r / 0.28 * math.cos(a), 0.5 + 0.5 * r / 0.28 * math.sin(a))
        return (j / 16 * 3, 0.5 + seat[k][1] / 0.2)

    lathe(acc, (0, 0, 0.45), seat, 16, lambda i: M.stool_top if i == 4 else M.stool_side, uv)
    wv = BoxUV((M.wood_tile[1], M.wood_tile[0]))
    for i in range(3):
        a = 0.4 + i * 2 * math.pi / 3
        c, s = math.cos(a), math.sin(a)
        top, foot = Vector((0.16 * c, 0.16 * s, 0.46)), Vector((0.27 * c, 0.27 * s, 0.0))
        beam(acc, foot, top, 0.055, M.wood_v, wv)
        p = foot + (top - foot) * (0.17 / 0.46)
        beam(acc, p, Vector((0, 0, p.z)), 0.038, M.wood_v, wv)
    lathe(acc, (0, 0, 0.145), [(0, 0), (0.045, 0), (0.045, 0.05), (0, 0.05)], 6, M.wood_dark)
    mat_prof = [(0, 0), (0.55, 0), (0.56, 0.014), (0.55, 0.028), (0, 0.028)]
    lathe(acc, (0, 0, 0), mat_prof, 28, T.coil,
          lambda i, j, k: (0.5 + 0.5 * mat_prof[k][0] / 0.56 * math.cos(2 * math.pi * j / 28),
                           0.5 + 0.5 * mat_prof[k][0] / 0.56 * math.sin(2 * math.pi * j / 28)))
    return acc.build("stool", root)


def _rake(acc, T, X=None):
    """The wooden hand rake in its own frame (X places it): the head bar at the origin, the handle up +Z with an iron
    ferrule, six dowel tines down."""
    X = X if X is not None else Matrix.Identity(4)
    lathe(acc, (0, 0, 0.05), [(0, 0), (0.024, 0), (0.024, 1.3), (0.02, 1.34), (0, 1.34)], 6, T.handle, M=X,
          smooth=lambda i: i == 1)
    lathe(acc, (0, 0, 0.02), [(0, 0), (0.034, 0), (0.034, 0.11), (0, 0.11)], 6, T.iron, M=X)
    cuboid(acc, (-0.26, -0.035, -0.03), (0.26, 0.035, 0.035), T.head, M=X)
    for x in (-0.22, -0.132, -0.044, 0.044, 0.132, 0.22):
        lathe(acc, (x, 0, -0.165), [(0, 0), (0.011, 0), (0.017, 0.04), (0.017, 0.14), (0, 0.14)], 6, T.head, M=X)


def build_props(kit, M, root, rng):
    """crock, bag, ledger, stool and rake, each at its own origin."""
    T = _tex(kit, M)
    acc = Acc()
    _rake(acc, T)
    return [_crock(T, M, root), _bag(T, M, root, rng), _ledger(T, M, root), _stool(T, M, root), acc.build("rake", root)]


# ───────────────────────── decor (room coordinates) ─────────────────────────

POT = [(0, 0), (0.26, 0), (0.28, 0.03), (0.33, 0.30), (0.385, 0.315), (0.39, 0.45), (0.37, 0.465), (0.345, 0.45),
       (0.335, 0.41), (0, 0.41)]                     # a terracotta pot with a thick collar, soil at 0.41


def decor_items(kit, M, decor, ferns, rng, spots):
    """The room's small furniture, added to the shell's decor (and the potted fronds to its ferns), at `spots` in
    game room coordinates; a yaw turns the piece as the game's CreateRotationY does (local +X to (cos, 0, -sin)):
    - pots: [(x, y, z), ...]   each pot's foot;
    - sieve: (x, y, z)          lying flat;
    - feed_shelf: (x, y, z, yaw)   the floor point under the middle of the shelf's back edge (against a wall); the
                                   shelf stands in front of it (local +Z), 1.3 m long, its plank at 0.46;
    - grate: (x, y, z)          a 0.64 m iron grate, its top 0.03 above the point;
    - rake: (x, y, z, yaw)      the foot of the wall it leans on: its tines rest on the floor 0.5 m in front (local
                                +Z) and the handle's top end lies against the wall, about 1.48 m up."""
    T = _tex(kit, M)
    pot_mat = lambda i: M.soil if i == 8 else T.terracotta
    for x, y, z in spots.get("pots", []):
        lathe(decor, B(x, y, z), POT, 14, pot_mat, lambda i, j, k: (j / 14 * 2, POT[k][1] / 0.465), smooth=lambda i: i == 2)
        base = B(x, y + 0.41, z)
        off = rng.uniform(0, 6.28)
        for k in range(8):  # the outer ring of long arching fronds, dark and mid
            yaw = off + k * 2 * math.pi / 8 + rng.uniform(-0.15, 0.15)
            frond(ferns, base + Vector((math.cos(yaw), math.sin(yaw), 0)) * 0.05, yaw, rng.uniform(0.62, 0.8),
                  rng.uniform(0.38, 0.5), rng.uniform(0.3, 0.45), rng.uniform(0.12, 0.145), 6,
                  M.fern[2] if k % 2 else M.fern[1], roll=rng.uniform(-0.35, 0.35))
        for k in range(3):  # young fronds standing up in the middle, light
            yaw = off + 0.4 + k * 2 * math.pi / 3 + rng.uniform(-0.2, 0.2)
            frond(ferns, base, yaw, rng.uniform(0.3, 0.42), rng.uniform(0.5, 0.62), rng.uniform(0.12, 0.2),
                  rng.uniform(0.1, 0.12), 4, M.fern[0], roll=rng.uniform(-0.3, 0.3))

    if "sieve" in spots:  # the Stillkeeper's sieve: a bentwood hoop round a brass mesh
        W = Matrix.Translation(B(*spots["sieve"]))
        lathe(decor, (0, 0, 0), [(0.30, 0.0), (0.345, 0.0), (0.35, 0.02), (0.35, 0.12), (0.345, 0.14), (0.30, 0.14), (0.30, 0.0)],
              20, T.bentwood, M=W, smooth=lambda i: i in (2, 4))
        lathe(decor, (0, 0, 0.03), [(0, 0), (0.30, 0), (0.30, 0.012), (0, 0.012)], 20, T.mesh,
              lambda i, j, k: (0.5 + 0.5 * (0.3 if k in (1, 2) else 0) / 0.3 * math.cos(2 * math.pi * j / 20),
                               0.5 + 0.5 * (0.3 if k in (1, 2) else 0) / 0.3 * math.sin(2 * math.pi * j / 20)), M=W)

    if "feed_shelf" in spots:  # a plank shelf on two end boards: five small lidded crocks on top, two squat below
        x, y, z, yaw = spots["feed_shelf"]
        W = Matrix.Translation(B(x, y, z)) @ Matrix.Rotation(yaw, 4, 'Z')
        wh, wv = BoxUV(M.wood_tile), BoxUV((M.wood_tile[1], M.wood_tile[0]))
        for sx in (-1, 1):
            a, b = sorted((sx * 0.5, sx * 0.58))
            cuboid(decor, (a, -0.32, 0.0), (b, -0.02, 0.40), M.wood_v, wv, M=W)
        cuboid(decor, (-0.66, -0.34, 0.40), (0.66, 0.0, 0.46), M.wood, wh, M=W)
        cuboid(decor, (-0.5, -0.30, 0.06), (0.5, -0.04, 0.10), M.wood, wh, M=W)
        jars = [(-0.5, 0.46, 0.20, 0.10, 0), (-0.25, 0.46, 0.28, 0.11, 1), (0.0, 0.46, 0.22, 0.12, 2), (0.25, 0.46, 0.30, 0.11, 0),
                (0.5, 0.46, 0.18, 0.09, 3), (-0.22, 0.10, 0.22, 0.14, 2), (0.2, 0.10, 0.2, 0.13, 1)]
        for jx, jz, h, r, c in jars:
            prof = [(0, 0), (r * 0.8, 0), (r, 0.04), (r * 1.06, h * 0.5), (r * 0.86, h * 0.86), (r * 0.62, h * 0.93),
                    (r * 0.62, h), (0, h)]
            J = W @ Matrix.Translation((jx, -0.17, jz))
            lathe(decor, (0, 0, 0), prof, 10, T.jars[c], M=J, smooth=lambda i: i in (2, 3, 4))
            lathe(decor, (0, 0, h), [(0, 0), (r * 0.72, 0), (r * 0.72, 0.03), (r * 0.32, 0.05), (r * 0.32, 0.085), (0, 0.085)],
                  10, M.timber, M=J)

    if "grate" in spots:  # the drain's iron grate, its slots painted on its top
        gx, gy, gz = B(*spots["grate"])
        s, h = 0.32, 0.03
        top = [Vector((gx - s, gy - s, gz + h)), Vector((gx + s, gy - s, gz + h)), Vector((gx + s, gy + s, gz + h)),
               Vector((gx - s, gy + s, gz + h))]
        decor.face(top, T.grate, [(0, 0), (1, 0), (1, 1), (0, 1)], out=UP)
        for k in range(4):
            a, b = top[k], top[(k + 1) % 4]
            decor.face([a, b, b - UP * 0.06, a - UP * 0.06], T.iron, out=(a + b) / 2 - Vector((gx, gy, a.z)))

    if "rake" in spots:  # the second rake, leaning on a wall
        x, y, z, yaw = spots["rake"]
        _rake(decor, T, Matrix.Translation(B(x, y, z)) @ Matrix.Rotation(yaw, 4, 'Z')
              @ Matrix.Translation((0, -0.45, 0.155)) @ Matrix.Rotation(-0.31, 4, 'X'))
