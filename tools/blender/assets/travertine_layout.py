"""The Travertine Well's layout, in game room units (metres, Y up, +Z toward the camera): the single source of the
Spirit Pools kit's numbers. pools_travertine.py builds the room from it and places the markers PoolsScene reads
(the kit contract is summarised in tools/blender/README.md). Plain Python, no bpy: `python travertine_layout.py`
prints where the pools, props and openings land on the game camera."""
import math

# ── the Spirit Pools' fixed camera (PoolsScene.Build, Scene.Orbit) ──
TARGET = (0.35, 1.25, -0.7)
YAW, PITCH, DIST, FOV = 0.3, 0.46, 13.4, 36.0
W, H = 1600, 900


def orbit_eye(target, yaw, pitch, dist):
    d = (math.sin(yaw) * math.cos(pitch), math.sin(pitch), math.cos(yaw) * math.cos(pitch))
    return tuple(t + dd * dist for t, dd in zip(target, d))


EYE = orbit_eye(TARGET, YAW, PITCH, DIST)


def project(p, eye=EYE, target=TARGET, fov=FOV):
    f = [t - e for t, e in zip(target, eye)]
    n = math.sqrt(sum(x * x for x in f)); f = [x / n for x in f]
    r = (f[1] * 0 - f[2] * 1, f[2] * 0 - f[0] * 0, f[0] * 1 - f[1] * 0)
    rn = math.sqrt(sum(x * x for x in r)); r = [x / rn for x in r]
    u = (r[1] * f[2] - r[2] * f[1], r[2] * f[0] - r[0] * f[2], r[0] * f[1] - r[1] * f[0])
    v = [a - b for a, b in zip(p, eye)]
    z = sum(a * b for a, b in zip(v, f)); x = sum(a * b for a, b in zip(v, r)); y = sum(a * b for a, b in zip(v, u))
    t = math.tan(math.radians(fov / 2))
    return (W / 2 + x / (z * t * W / H) * W / 2, H / 2 - y / (z * t) * H / 2)


def view_angle(p):
    h = math.hypot(p[0] - EYE[0], p[2] - EYE[2])
    return math.degrees(math.atan2(EYE[1] - p[1], h))


# ── the shaft ──
BACK, LEFT, RIGHT = -5.4, -6.2, 8.4  # inner planes of the back, left and right walls (drapes only go back from them)
WALL_TOP = 9.5                       # well above the top of the frame
FRONT = 10.6                         # the well floor's far front (out of every frame, focus close-ups included)
FLOOR = -1.34                        # the well floor = the bottom pool's rim
LIP = 0.16                           # rim above water (RimY 0.78 - SurfY 0.62)
BASE = 0.62                          # a slot's base (the bowl's origin) below its water
BOWL_RX, BOWL_RZ, LIP_W = 2.96, 1.22, 0.28   # the bowl part's water oval and its rolled lip's width
SKIN = 0.03                          # terrace top kept in front of a lip that runs into its dam face

TOP_Y, MID_Y, LANDING_Y = 1.66, 0.16, 2.4   # terrace tops (= rims) and the landing under the mouth


def slot(name, c, y, rx, rz, yaw=0.0):
    """A pool slot: centre (x, z) of its water oval, water level, half-axes and turn (0 or pi/2)."""
    return {"name": name, "c": c, "y": y, "rx": rx, "rz": rz, "yaw": yaw}


SLOTS = [
    slot("Sunward Basin", (2.4, -3.6), 1.5, 2.90, 1.19),             # 0: top terrace, under the beam
    slot("Shade Pool", (-3.0, 1.1), -1.5, 2.90, 1.19),               # 1: the well floor
    slot("Spawning Pond", (0.0, -1.2), 0.0, 2.42, 1.00),             # 2: the middle terrace, small and still
    slot("spare", (6.4, -0.1), 0.0, 2.96, 1.22, math.pi / 2),        # 3: the right ledge, in frame
    slot("spare (alcove A)", (10.0, -1.9), 0.0, 2.96, 1.22, math.pi / 2),   # 4, 5: alcoves beyond x 8.4
    slot("spare (alcove B)", (10.0, 5.0), 0.0, 2.96, 1.22, math.pi / 2),
]
TOP, BOT, MID = SLOTS[0], SLOTS[1], SLOTS[2]


def hole(s):
    """The bowl lip's outer oval at slot s (the bowl is built for BOWL_RX x BOWL_RZ and scaled to the slot), as
    (cx, cz, half-axis along x, half-axis along z): the hole in the terrace top the bowl sits in."""
    a = (BOWL_RX + LIP_W) * s["rx"] / BOWL_RX
    b = (BOWL_RZ + LIP_W) * s["rz"] / BOWL_RZ
    return (s["c"][0], s["c"][1], b, a) if s["yaw"] else (s["c"][0], s["c"][1], a, b)


def ellipse(cx, cz, ax, az, seg=96):
    return [(cx + ax * math.cos(2 * math.pi * k / seg), cz + az * math.sin(2 * math.pi * k / seg)) for k in range(seg)]


def _half(s, x):
    cx, cz, ax, az = hole(s)
    d = (x - cx) / ax
    return None if abs(d) >= 1 else az * math.sqrt(1 - d * d)


def _front(s, x):
    h = _half(s, x)
    return None if h is None else s["c"][1] + h


def _back(s, x):
    h = _half(s, x)
    return None if h is None else s["c"][1] - h


def top_edge(x):
    """The top terrace's front edge (its dam over the middle level): SKIN in front of the Sunward Basin's lip, else
    -2.65 on the left (room for the runnel) and -3.35 on the right; always just behind the lips below it, so where
    the two disagree (x 0.4-1.9) the top lip overhangs its dam by up to 0.13 m, as rimstone does."""
    e = -2.65 if x < TOP["c"][0] else -3.35
    f = _front(TOP, x)
    if f is not None:
        e = max(e, f + SKIN)
    for s in (MID, SLOTS[3]):
        b = _back(s, x)
        if b is not None:
            e = min(e, b - 0.02)
    return e


def dam_edge(x):
    """The middle terrace's front edge over the well floor, left of LEDGE_X: 0.6, but always in front of the
    Spawning Pond's lip and just behind the Shade Pool's."""
    e = 0.6
    f = _front(MID, x)
    if f is not None:
        e = max(e, f + SKIN)
    b = _back(BOT, x)
    if b is not None:
        e = min(e, b - 0.02)
    return e


LEDGE_X, LEDGE_FRONT, LEDGE_FRONT_X = 0.45, 3.3, 2.3   # the ledge's left side runs diagonally, clear of the Shade Pool

# the right-wall alcoves for slots 4 and 5: floors at the middle level, x RIGHT..ALC_X, split by a pier
ALC_X, PIER, ALC_FRONT, ALC_HEAD = 11.9, (1.40, 1.70), 8.4, 2.9

STEP = 0.05


def _xs(a, b, step=STEP):
    n = max(1, int(math.ceil(abs(b - a) / step)))
    return [a + (b - a) * k / n for k in range(n + 1)]


def top_front():
    """The top dam's plan line, walked right to left (the rock on the right)."""
    return [(x, top_edge(x)) for x in _xs(RIGHT, LEFT - 0.4)]


def mid_front():
    """The middle terrace's outer faces, walked with the rock on the right: alcove B's front, its side down to the
    ledge, the ledge front, the diagonal, then the dam over the Shade Pool to the left wall."""
    return ([(ALC_X + 0.4, ALC_FRONT), (RIGHT, ALC_FRONT), (RIGHT, LEDGE_FRONT), (LEDGE_FRONT_X, LEDGE_FRONT)] +
            [(x, dam_edge(x)) for x in _xs(LEDGE_X, LEFT - 0.4)])


def top_level_poly():
    return [(LEFT - 0.4, BACK - 0.4), (RIGHT, BACK - 0.4)] + top_front()


def mid_level_poly():
    back = [(x, top_edge(x)) for x in _xs(LEFT - 0.4, RIGHT)]
    alcoves = [(RIGHT, BACK - 0.4), (ALC_X + 0.4, BACK - 0.4), (ALC_X + 0.4, PIER[0]), (RIGHT, PIER[0]), (RIGHT, PIER[1]),
               (ALC_X + 0.4, PIER[1])]
    return back + alcoves + mid_front()


def floor_front():
    """The well floor's far front edge, walked right to left (the rock on the right): FRONT, wavering about 0.35 m
    so the soft rocky roll that drops from it never reads as a box edge."""
    return [(x, FRONT + 0.25 * math.sin(1.1 * x + 0.7) + 0.12 * math.sin(2.7 * x + 2.1)) for x in _xs(RIGHT, LEFT - 0.4, 0.25)]


def floor_poly():
    edge = [(x, dam_edge(x)) for x in _xs(LEFT - 0.4, LEDGE_X)]
    return edge + [(LEDGE_FRONT_X, LEDGE_FRONT), (RIGHT, LEDGE_FRONT)] + floor_front()


# ── the cave mouth on the back wall: door-sized, round-headed; steps rise into the tunnel on the left half, the brook
#    runs down a ramp on the right half and leaves over a spout lip ──
MOUTH_X0, MOUTH_X1, MOUTH_SILL = 0.5, 1.9, LANDING_Y
MOUTH_SPRING = MOUTH_SILL + 0.9
MOUTH_CROWN = MOUTH_SPRING + (MOUTH_X1 - MOUTH_X0) / 2
MOUTH_SPLIT = 1.22                  # steps left of it, the brook right of it
MOUTH_BOTTOM = 2.15                 # the opening's foot (the spout lip and the landing fill below the sill)
TUNNEL_FLAT, TUNNEL_BACK = -5.6, -8.6
TREAD_RUN, TREAD_RISE = 0.45, 0.2
BROOK_FLOOR = 2.36                  # the brook's bed in the mouth (and the spout lip's groove)


def tunnel_rise(z):
    """How far the tunnel (floor and roof) has climbed at z: level to TUNNEL_FLAT, then up with the treads."""
    return max(0.0, TUNNEL_FLAT - z) * TREAD_RISE / TREAD_RUN


# the landing block under the mouth (sill height), for the bag; kept clear of the Sunward Basin's lip
LANDING = [(-1.8, BACK - 0.4), (1.2, BACK - 0.4), (1.2, -5.05), (0.4, -4.85), (-0.4, -4.5), (-1.8, -4.4)]

# the low outflow arch in the left wall, at floor level: the outflow channel runs out under it; the arch is set in a
# rounded mass of rock standing out of the wall (OUT_Z +- OUT_MASS_HW, from the floor to 0.6 above the crown)
OUT_Z, OUT_HW, OUT_SPRING = 2.3, 0.3, -1.05
OUT_TUNNEL = LEFT - 1.6
OUT_MASS_HW, OUT_MASS_TOP = 1.0, OUT_SPRING + OUT_HW + 0.6

# ── water paths (markers; the game draws the water along them). The runnel and the outflow ride high in shallow
#    channels, RUN_DROP under the terrace they cross, so the stream shows from the camera; drops are falls ──
RUN_DROP = 0.06
TOP_RUN, MID_RUN, FLOOR_RUN = (round(y - RUN_DROP, 3) for y in (TOP_Y, MID_Y, FLOOR))   # 1.60, 0.10, -1.40
BROOK = [(1.6, 2.42, -5.6), (1.6, 2.42, -5.25), (1.62, 2.2, -5.05), (1.66, 1.5, -4.55)]
RUNNEL = [(-0.2, TOP["y"], -3.6), (-0.62, TOP_RUN, -3.55), (-2.5, TOP_RUN, -3.3), (-4.4, TOP_RUN, -2.95),
          (-4.4, TOP_RUN, -2.7), (-4.4, MID_RUN, -2.55), (-4.45, MID_RUN, -0.35), (-4.45, BOT["y"], 0.15)]
OUTFLOW = [(-4.8, BOT["y"], 1.6), (-5.4, FLOOR_RUN, 1.85), (-5.95, FLOOR_RUN, 2.3), (-6.6, FLOOR_RUN, 2.3),
           (-7.3, FLOOR_RUN, 2.3)]

# the shallow U-channels the runnel and the outflow run in (plan paths; cut CHANNEL_D into the terrace they cross)
CHANNEL_W, CHANNEL_D = 0.5, 0.14
TOP_CHANNEL = [(-0.62, -3.55), (-2.5, -3.3), (-4.4, -2.95), (-4.4, top_edge(-4.4))]
_n = (0.233, 0.972)                 # the middle dam's outward normal at the runnel's drop (x -4.45)
MID_CHANNEL = [(-4.4, top_edge(-4.4) - 0.2), (-4.4, -2.55), (-4.45 - 0.4 * _n[0], dam_edge(-4.45) - 0.4 * _n[1]),
               (-4.45, dam_edge(-4.45))]
OUT_CHANNEL_W = 2 * OUT_HW
OUT_CHANNEL = [(-5.55, 1.94), (-5.95, OUT_Z), (LEFT, OUT_Z)]

# light: an oculus above the top-right, out of frame; its beam lands on the Sunward Basin
OCULUS = (5.0, 6.5, -4.5)
BEAM_SPOT = (2.6, 1.5, -3.7)
BEAM_R_SPOT, BEAM_R_TOP = 0.75, 1.0

# ── furniture: where each prop's origin stands (its floor point, with height) ──
BAG = (-0.9, LANDING_Y, -4.85)
LEDGER = (3.8, MID_Y, -0.4)
STOOL = (2.7, MID_Y, 0.7)
CROCK = (4.0, MID_Y, 1.45)


def _shelf_yaw(x):
    """The turn whose +Z front faces straight out of the top dam where it follows the Sunward Basin, at x."""
    cx, cz, ax, az = hole(TOP)
    z = _front(TOP, x)
    nx, nz = (x - cx) / ax ** 2, (z - cz) / az ** 2
    return math.atan2(nx, nz)


# the small decor travertine_props.decor_items places (floor points with height; yaw turns local +Z, the front, the
# way PoolsScene's CreateRotationY does): fern pots, the sieve, the feed shelf (the foot of the dam it hangs on), the
# drain grate by the outflow, the second rake (leaning on the back wall)
SPOTS = {
    "pots": [(-2.45, TOP_Y, -3.98), (6.95, TOP_Y, -4.4), (1.6, MID_Y, 1.0)],
    "sieve": (2.0, MID_Y, 2.2),
    "feed_shelf": (4.6, MID_Y, top_edge(4.6), _shelf_yaw(4.6)),
    "grate": (-5.75, FLOOR, 3.05),
    "rake": (7.4, TOP_Y, -5.15, 0.0),
}
FERN_CLUMPS = [(-4.95, TOP_Y, -4.0), (4.6, TOP_Y, -5.1), (-3.5, MID_Y, -2.0), (-4.6, FLOOR, 3.6), (7.9, TOP_Y, -3.75)]


def lantern(s):
    """PoolsScene: a pool's lantern cage at local (0.82 rx, 1.32, -0.62 rz) from its base."""
    return (s["c"][0] + 0.82 * s["rx"], s["y"] - BASE + 1.32, s["c"][1] - 0.62 * s["rz"])


def stair_steps():
    """(x, top y, z, width x, depth z) of each tread of the root stair."""
    s = []
    for k in range(4):   # off the landing, down to the top terrace, along the back wall
        s.append((-2.0 - 0.4 * k, LANDING_Y - 0.185 * (k + 1), -4.9, 0.42, 1.0))
    for k in range(6):   # down the left wall to the middle terrace
        s.append((-5.6, TOP_Y - 0.25 * (k + 1), -2.5 + 0.3 * k, 1.0, 0.32))
    return s


# Old Stoic's roots come down the shaft from the tree above and hug its walls: each runs LW / BW out from the left or
# back wall's plane (its back half sunk in the drapes), in a gentle S, tapering from its thick end above the frame to
# a buttressed foot buried 0.15 in a terrace or the floor, and forks once. (bezier control points, thick end first;
# radius at the thick end and at the foot; the fork: (t on the root where it leaves, its other three control
# points, the last its buried foot), its foot radius.) Few and thick: thin strands would be ink noise.
LW, BW, SINK = LEFT + 0.18, BACK + 0.18, 0.15
ROOTS = [
    # the back wall, left of the stair: two feet on the top terrace
    ([(-3.5, WALL_TOP, BW), (-2.5, 6.6, BW), (-5.0, 3.6, BW), (-4.3, TOP_Y - SINK, BW + 0.06)], 0.38, 0.17,
     (0.62, [(-4.7, 3.4, BW), (-5.3, 2.6, BW), (-5.25, TOP_Y - SINK, BW + 0.04)], 0.13)),
    # the left wall at the back: two feet on the top terrace, either side of the corner fern
    ([(LW, WALL_TOP, -2.2), (LW, 6.8, -1.0), (LW, 3.8, -4.5), (LW + 0.06, TOP_Y - SINK, -3.55)], 0.4, 0.17,
     (0.6, [(LW, 3.6, -3.9), (LW, 2.6, -4.7), (LW + 0.04, TOP_Y - SINK, -4.55)], 0.13)),
    # the left wall over the stair's foot: onto the middle terrace; low down its fork reaches along the wall above
    # the Shade Pool, over the outflow arch, and bends down to the floor beyond it
    ([(LW, WALL_TOP, 1.4), (LW, 6.6, -1.6), (LW, 3.4, 0.9), (LW + 0.06, MID_Y - SINK, -0.3)], 0.42, 0.18,
     (0.7, [(LW, 0.9, 1.5), (LW, 1.5, 4.6), (LW + 0.06, FLOOR - SINK, 4.6)], 0.17)),
    # the back wall right of the Sunward Basin, framing the beam
    ([(6.9, WALL_TOP, BW), (7.7, 6.6, BW), (5.4, 3.6, BW), (6.4, TOP_Y - SINK, BW + 0.06)], 0.36, 0.16,
     (0.6, [(5.9, 3.4, BW), (5.4, 2.5, BW), (5.4, TOP_Y - SINK, BW + 0.04)], 0.12)),
]
# the root stair's flank: rising out of the top terrace beside the runnel (its thick end buried), over the dam's
# edge, down beside the treads
STAIR_ROOT = ([(-5.0, 1.2, -3.45), (-4.85, 2.0, -2.45), (-5.35, 0.8, -1.75), (-5.15, MID_Y - SINK, -0.85)], 0.22, 0.12)


def markers():
    """Every marker the game reads, in game coordinates."""
    m = {}
    for i, s in enumerate(SLOTS):
        b = (s["c"][0], s["y"] - BASE, s["c"][1])
        cy, sy = math.cos(s["yaw"]), math.sin(s["yaw"])
        m[f"pool-{i}"] = b
        m[f"pool-{i}-x"] = (b[0] + s["rx"] * cy, b[1], b[2] - s["rx"] * sy)
        m[f"pool-{i}-z"] = (b[0] + s["rz"] * sy, b[1], b[2] + s["rz"] * cy)
    m.update({"at-crock": CROCK, "at-bag": BAG, "at-ledger": LEDGER, "at-stool": STOOL, "oculus": OCULUS,
              "beam-spot": BEAM_SPOT})
    for name, pts in (("brook", BROOK), ("runnel", RUNNEL), ("outflow", OUTFLOW)):
        for i, p in enumerate(pts):
            m[f"{name}-{i}"] = p
    return m


# where this sits in the world (for the section): room z -> world x, room y -> world y
WORLD_X_OF_BACK = 18.0             # the tunnel runs 5.1 m east from the hub mouth (x 12.9) under the wall (12.9-16.5)
WORLD_Y_OF_ROOM0 = -7.5            # the oculus rim (room y 6.5) sits on the scarp shelf (world y -1)


def to_world(p):
    x, y, z = p
    return (z - BACK + WORLD_X_OF_BACK, y + WORLD_Y_OF_ROOM0, -7.4 - (x - (MOUTH_X0 + MOUTH_X1) / 2))


if __name__ == "__main__":
    print("eye", [round(c, 2) for c in EYE])
    for i, s in enumerate(SLOTS):
        cx, cz, ax, az = hole(s)
        c = (cx, s["y"], cz)
        pts = [project((x, s["y"], z)) for x, z in ellipse(cx, cz, ax, az, 32)]
        print(f"{i} {s['name']:18s} centre {tuple(round(v) for v in project(c))}  angle {view_angle(c):.1f}"
              f"  bbox x {min(q[0] for q in pts):.0f}-{max(q[0] for q in pts):.0f} y {min(q[1] for q in pts):.0f}-{max(q[1] for q in pts):.0f}")
    for nm, p in {"mouth sill L": (MOUTH_X0, MOUTH_SILL, BACK), "mouth crown": ((MOUTH_X0 + MOUTH_X1) / 2, MOUTH_CROWN, BACK),
                  "bag": (BAG[0], BAG[1] + 0.5, BAG[2]), "ledger": (LEDGER[0], 1.1, LEDGER[2]), "stool": (STOOL[0], 0.7, STOOL[2]),
                  "crock": (CROCK[0], 0.8, CROCK[2]), "outflow arch": (LEFT, FLOOR, OUT_Z), "oculus": OCULUS,
                  "feed shelf": SPOTS["feed_shelf"][:3], "grate": SPOTS["grate"], "sieve": SPOTS["sieve"]}.items():
        print(f"{nm:14s}", tuple(round(v) for v in project(p)), "world", tuple(round(v, 1) for v in to_world(p)))
