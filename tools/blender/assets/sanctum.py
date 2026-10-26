"""The Sanctum: the Keeper's own lodge. A round tower of pale warm sandstone in courses on a slightly wider round
plinth, a projecting stone cornice ring, an ochre dome of crazed, patched render, and a small pale stone spire on a
collar with a ball finial. On the front: a heavy arched plank door with black iron straps and a ring pull, in a
stone arch surround that stands proud of the curved wall, over one stone step; a small round window with a stone
rim on each side of the door; one round window high on the back.
Reference: art/reference/sheets/sanctum-sheet.png (Higgsfield job dea443fd), four views: front, right, back, left.

Parts the game uses: body (everything solid, the door decal and the window rims included), glass (the three round
window panes and the lantern's panes, lit at night), and the markers lamp and door-point. Front faces -Y in
Blender (+Z in the game).

Measured off the sheet (front view, ground line at sheet y 1292) at PX metres per pixel. The brief's numbers
(5.6 m across, a 2.5 m door) disagree with each other on the sheet by a factor of 1.5, so the sheet's own
proportions are kept and PX sits between them: wall 4.55 m across, door 1.5 x 3.2 m, 10.9 m to the finial.
"""
import math

SHEET = "sanctum-sheet.png"
PX = 0.0098                 # metres per sheet pixel
GROUND = 1292               # sheet y of the ground line (bottom of the plinth)
SEG = 32                    # segments around the tower
LEAN = 0.02                 # settled a little over to the right (about 1.1 degrees)


def zy(y):
    """Height in metres of a sheet row (front view)."""
    return (GROUND - y) * PX


R = 232 * PX                # wall radius (2.27 m)
R_PLINTH = 248 * PX         # plinth radius (2.43 m)
Z_PLINTH = zy(1215)         # top of the plinth's upright face
Z_PLINTH_TOP = zy(1182)     # top of its sloped weathering
Z_WALL = zy(652)            # where the cornice begins
DOME_A, DOME_B = 228 * PX, 207 * PX     # dome: radius and rise (a squashed half-ellipsoid)
DOME_Z = zy(590)                        # dome springing line

# the door and its arch surround (front view, centred on sheet x 405)
SURROUND_HW = 125 * PX      # half width of the surround (1.23 m)
SPRING = zy(990)            # where the surround's outer arch springs
PROUD = 0.2                 # how far the surround's face stands in front of the wall's foremost point
DOOR_SILL = zy(1250)        # door bottom = top of the step
DOOR_TOP = zy(865)          # top of the surround (and of the decal)

# round windows: (angle from the front towards +X in degrees, centre height)
WINDOWS = [(-47.0, zy(1025)), (47.0, zy(1025)), (180.0, zy(806))]
RIM_R, GLASS_R = 58 * PX, 42 * PX
RIM_PROUD = 0.1


# ───────────────────────── helpers (local to this asset) ─────────────────────────

def lathe(kit, name, profile, root, segments=SEG, u_tiles=1, tile_v=1.0, v_mode="z", cap_tile=1.0, v0=0.0,
          twist=0.0):
    """A surface of revolution from a (radius, height) profile, bottom to top; radius 0 makes a pole.
    Cylindrical UVs: u runs around in a whole number of tiles (u_tiles, so the seam can't show), v is height
    (v_mode "z", from height v0) or arc length up the profile ("s", for the dome), over tile_v metres; twist turns
    u a little with v (a spiral) so a mirrored swatch's axes don't run straight up. Flat caps map in plan."""
    verts, meta, rings = [], [], []
    for i, (r, z) in enumerate(profile):
        if r <= 1e-6:
            rings.append([len(verts)])
            verts.append((0.0, 0.0, z))
            meta.append((i, None))
            continue
        ring = []
        for j in range(segments):
            a = 2 * math.pi * j / segments
            ring.append(len(verts))
            verts.append((r * math.cos(a), r * math.sin(a), z))
            meta.append((i, j))
        rings.append(ring)
    faces = []
    for i in range(len(rings) - 1):
        a_ring, b_ring = rings[i], rings[i + 1]
        for j in range(segments):
            k = (j + 1) % segments
            if len(a_ring) == 1:
                faces.append((a_ring[0], b_ring[k], b_ring[j]))
            elif len(b_ring) == 1:
                faces.append((a_ring[j], a_ring[k], b_ring[0]))
            else:
                faces.append((a_ring[j], a_ring[k], b_ring[k], b_ring[j]))
    if len(rings[0]) > 1:
        faces.append(tuple(reversed(rings[0])))
    if len(rings[-1]) > 1:
        faces.append(tuple(rings[-1]))
    o = kit.mesh_object(name, verts, faces, root, solid=True)

    # arc length up the profile, for v_mode "s"
    s = [0.0]
    for i in range(1, len(profile)):
        (r0, z0), (r1, z1) = profile[i - 1], profile[i]
        s.append(s[-1] + math.hypot(r1 - r0, z1 - z0))
    me = o.data
    uvl = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        loops = list(poly.loop_indices)
        vids = [me.loops[li].vertex_index for li in loops]
        if len(vids) > 4:  # a flat cap: plan projection
            for li, vi in zip(loops, vids):
                uvl.data[li].uv = (verts[vi][0] / cap_tile, verts[vi][1] / cap_tile)
            continue
        js = [meta[vi][1] for vi in vids]
        known = [j for j in js if j is not None]
        if max(known) - min(known) > segments / 2:  # the face that closes the circle
            js = [None if j is None else (j + segments if j < segments / 2 else j) for j in js]
            known = [j for j in js if j is not None]
        pole_j = sum(known) / len(known)
        for li, vi, j in zip(loops, vids, js):
            i = meta[vi][0]
            jj = pole_j if j is None else j
            v = (profile[i][1] - v0 if v_mode == "z" else s[i]) / tile_v
            uvl.data[li].uv = (jj / segments * u_tiles + twist * v, v)
    return o


def tiles_around(radius, tile_u):
    """How many whole swatch tiles fit around a circle of this radius (at least one)."""
    return max(1, round(2 * math.pi * radius / tile_u))


def smooth(obj, sharp_deg=25.0):
    """Smooth shading for the round parts, keeping hard creases (caps, the cornice's steps) sharp."""
    for p in obj.data.polygons:
        p.use_smooth = True
    try:
        obj.data.set_sharp_from_angle(angle=math.radians(sharp_deg))
    except AttributeError:
        pass


def facing_frame(deg):
    """Outward direction and the 'right' direction (as seen from outside) of a point on the wall at angle deg
    from the front, turning towards +X."""
    a = math.radians(deg)
    d = (math.sin(a), -math.cos(a), 0.0)
    t = (math.cos(a), math.sin(a), 0.0)
    return d, t


def disc(kit, name, mat, centre, d, t, radius, root, segments=16):
    """A flat round decal facing d: the decal image maps once across the disc's square."""
    cx, cy, cz = centre
    verts, uvs = [], []
    for k in range(segments):
        a = 2 * math.pi * k / segments
        ca, sa = math.cos(a), math.sin(a)
        verts.append((cx + radius * ca * t[0], cy + radius * ca * t[1], cz + radius * sa))
        uvs.append((0.5 + 0.5 * ca, 0.5 + 0.5 * sa))
    o = kit.mesh_object(name, verts, [tuple(range(segments))], root)
    me = o.data
    if me.polygons[0].normal.dot(kit.Vector(d)) < 0:
        me.flip_normals()
    uvl = me.uv_layers.new(name="UVMap")
    for li in me.polygons[0].loop_indices:
        uvl.data[li].uv = uvs[me.loops[li].vertex_index]
    kit.assign(o, mat)
    return o


def rim(kit, name, z, deg, root, segments=16):
    """A short stone cylinder pushed out through the wall along its normal: a round window's rim."""
    d, t = facing_frame(deg)
    back, front = R - 0.3, R + RIM_PROUD
    verts = []
    for dist in (back, front):
        for k in range(segments):
            a = 2 * math.pi * k / segments
            ca, sa = math.cos(a), math.sin(a)
            verts.append((d[0] * dist + RIM_R * ca * t[0], d[1] * dist + RIM_R * ca * t[1], z + RIM_R * sa))
    faces = [(k, (k + 1) % segments, segments + (k + 1) % segments, segments + k) for k in range(segments)]
    faces.append(tuple(reversed(range(segments))))
    faces.append(tuple(range(segments, 2 * segments)))
    return kit.mesh_object(name, verts, faces, root, solid=True)


def arch(half_w, z0, z_spring, n_arc=14):
    """A round-headed outline in XZ, counter-clockwise seen from -Y."""
    pts = [(-half_w, z0), (half_w, z0)]
    for k in range(n_arc + 1):
        a = math.pi * k / n_arc
        pts.append((half_w * math.cos(a), z_spring + half_w * math.sin(a)))
    return pts


# ───────────────────────── the house ─────────────────────────

def build(kit):
    sheet = kit.load_sheet(SHEET)
    wall_box = (1642, 926, 1866, 1132)       # back view, below the round window: courses, cut mid-course
    plinth_box = (1510, 1225, 1830, 1278)    # back view, the plinth's upright face
    dome_box = (300, 425, 545, 566)          # front view, the crazed render on the dome
    wall = kit.material("sanctum-wall", kit.swatch(sheet, "sanctum-wall", *wall_box))
    plinth = kit.material("sanctum-plinth", kit.swatch(sheet, "sanctum-plinth", *plinth_box))
    dome = kit.material("sanctum-dome", kit.swatch(sheet, "sanctum-dome", *dome_box))
    dressed = kit.material("sanctum-dressed", color=kit.sample(sheet, 1700, 598, 3))   # cornice top, spire
    iron = kit.material("sanctum-iron", color=kit.sample(sheet, 400, 1024, 2))         # the door's straps
    door_mat = kit.material("sanctum-door", kit.decal(sheet, "sanctum-door", 280, 865, 530, 1250))
    rim_mat = kit.material("sanctum-window-rim", kit.decal(sheet, "sanctum-window-rim", 1665, 748, 1781, 865))
    pane_mat = kit.material("sanctum-window-glass", kit.decal(sheet, "sanctum-window-glass", 1679, 763, 1763, 847))
    lantern_glass = kit.material("sanctum-lantern-glass", color=kit.srgb_to_linear((0.86, 0.76, 0.55)))
    wall_tile = kit.sheet_tile(*wall_box, PX)
    plinth_tile = kit.sheet_tile(*plinth_box, PX)
    dome_tile = kit.sheet_tile(*dome_box, PX)

    root = kit.root("sanctum")
    body_parts = []

    # ── plinth: an upright band, then a sloped weathering up to the wall ──
    base = lathe(kit, "plinth", [(R_PLINTH, 0.0), (R_PLINTH, Z_PLINTH), (R + 0.005, Z_PLINTH_TOP)], root,
                 u_tiles=tiles_around(R_PLINTH, plinth_tile[0]), tile_v=plinth_tile[1], cap_tile=plinth_tile[0])
    kit.assign(base, plinth)
    body_parts.append(base)

    # ── the wall: sandstone courses ──
    tower = lathe(kit, "wall", [(R, Z_PLINTH - 0.1), (R, Z_WALL + 0.12)], root,
                  u_tiles=tiles_around(R, wall_tile[0]), tile_v=wall_tile[1], cap_tile=wall_tile[0],
                  v0=Z_PLINTH_TOP - 24 * PX)  # the box's first bed joint (sheet y 950) sits on the plinth
    kit.assign(tower, wall)
    body_parts.append(tower)

    # ── cornice: a band, a projecting slab, and a sloped top up to the dome ──
    band_r, slab_r = 244 * PX, 267 * PX
    cornice = lathe(kit, "cornice", [(R - 0.06, Z_WALL - 0.02), (band_r, Z_WALL + 0.02), (band_r, zy(632)),
                                     (slab_r, zy(629)), (slab_r, zy(600)), (DOME_A + 0.06, zy(586))], root,
                    u_tiles=6, tile_v=1.0)
    kit.assign(cornice, dressed)
    body_parts.append(cornice)

    # ── the dome: a squashed half-ellipsoid, rendered ochre ──
    rings = 10
    prof = []
    for k in range(rings + 1):
        phi = (math.pi / 2) * k / rings
        prof.append((DOME_A * math.cos(phi) if k < rings else 0.0, DOME_Z - 0.03 + DOME_B * math.sin(phi)))
    cupola = lathe(kit, "dome", prof, root, u_tiles=tiles_around(DOME_A, dome_tile[0]), tile_v=dome_tile[1],
                   v_mode="s", twist=0.45)  # a slight spiral, so the mirrored cracks don't line up as lace
    kit.assign(cupola, dome)
    body_parts.append(cupola)

    # ── collar, spire and ball finial (profile read off the front view, radii in sheet pixels) ──
    spire_prof = [(57, 398), (57, 373), (52, 367), (42, 365), (30, 359), (21, 349), (16, 336), (15, 326),
                  (19, 321), (24, 313), (23, 305), (19, 301), (15, 289), (11, 270), (7, 248), (4, 226),
                  (2.5, 217), (0, 214)]
    spire = lathe(kit, "spire", [(r * PX, zy(y)) for r, y in spire_prof], root, segments=16, u_tiles=1)
    kit.assign(spire, dressed)
    ball_c, ball_r = zy(199), 16.5 * PX
    ball_prof = [(ball_r * math.sin(math.pi * k / 8), ball_c - ball_r * math.cos(math.pi * k / 8)) for k in range(9)]
    ball_prof[0] = (0.0, ball_prof[0][1])
    ball_prof[-1] = (0.0, ball_prof[-1][1])
    ball = lathe(kit, "finial", ball_prof, root, segments=12, u_tiles=1)
    kit.assign(ball, dressed)
    body_parts += [spire, ball]

    for o in body_parts:
        smooth(o)

    # ── the door's arch surround: a flat stone block standing proud of the curve ──
    y_face = -(R + PROUD)
    surround = kit.prism("surround", arch(SURROUND_HW, 0.0, SPRING), y_face, -(R - 0.75), root)
    kit.assign(surround, dressed)
    kit.box_uv(surround, 1.0)
    step = kit.box("step", (0, y_face - 0.25, DOOR_SILL / 2), (2.15, 0.6, DOOR_SILL), root)
    kit.assign(step, plinth)
    kit.box_uv(step, plinth_tile)
    body_parts += [surround, step]

    # the painted door and surround stones, mapped once across the arched face
    outline = arch(SURROUND_HW, DOOR_SILL, SPRING)
    door = kit.mesh_object("door", [(x, y_face - 0.012, z) for x, z in outline], [tuple(range(len(outline)))], root)
    if door.data.polygons[0].normal.y > 0:
        door.data.flip_normals()
    uvl = door.data.uv_layers.new(name="UVMap")
    for li in door.data.polygons[0].loop_indices:  # sheet box x 280-530, y 865-1250: the surround's full width
        x, _, z = door.data.vertices[door.data.loops[li].vertex_index].co
        uvl.data[li].uv = ((x + SURROUND_HW) / (2 * SURROUND_HW), (z - DOOR_SILL) / (DOOR_TOP - DOOR_SILL))
    kit.assign(door, door_mat)
    body_parts.append(door)

    # ── round windows: a stone rim pushed out through the wall, its painted face, and the pane ──
    panes = []
    for n, (deg, z) in enumerate(WINDOWS):
        d, t = facing_frame(deg)
        stone = rim(kit, f"rim{n}", z, deg, root)
        kit.assign(stone, dressed)
        kit.box_uv(stone, 1.0)
        smooth(stone, 40.0)
        f = R + RIM_PROUD
        face = disc(kit, f"rim-face{n}", rim_mat, (d[0] * (f + 0.012), d[1] * (f + 0.012), z), d, t, RIM_R, root)
        pane = disc(kit, f"pane{n}", pane_mat, (d[0] * (f + 0.024), d[1] * (f + 0.024), z), d, t, GLASS_R, root)
        body_parts += [stone, face]
        panes.append(pane)

    # ── a small iron lantern on a bracket, on the face of the surround's right jamb (below the arch's springing) ──
    lx, ly, lz = 1.0, y_face - 0.3, 2.41      # lz: the lantern's base plate
    gz0, gz1, half = lz + 0.022, lz + 0.298, 0.1
    lantern_iron = [
        kit.box("bracket-plate", (lx, y_face - 0.015, lz + 0.455), (0.1, 0.03, 0.24), root),
        kit.box("bracket-arm", (lx, y_face - 0.17, lz + 0.515), (0.035, 0.34, 0.035), root),
        kit.box("bracket-hook", (lx, ly, lz + 0.455), (0.025, 0.025, 0.1), root),
        kit.box("lantern-band", (lx, ly, lz + 0.315), (0.25, 0.25, 0.035), root),
        kit.box("lantern-base", (lx, ly, lz), (0.23, 0.23, 0.045), root),
    ]
    cap_z0, cap_z1 = lz + 0.33, lz + 0.415
    cap = kit.mesh_object("lantern-cap", [(lx - 0.14, ly - 0.14, cap_z0), (lx + 0.14, ly - 0.14, cap_z0),
                                          (lx + 0.14, ly + 0.14, cap_z0), (lx - 0.14, ly + 0.14, cap_z0),
                                          (lx - 0.03, ly - 0.03, cap_z1), (lx + 0.03, ly - 0.03, cap_z1),
                                          (lx + 0.03, ly + 0.03, cap_z1), (lx - 0.03, ly + 0.03, cap_z1)],
                          [(0, 1, 2, 3), (4, 5, 6, 7), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)],
                          root, solid=True)
    lantern_iron.append(cap)
    for sx in (-1, 1):
        for sy in (-1, 1):
            lantern_iron.append(kit.box(f"lantern-post{sx}{sy}", (lx + sx * half, ly + sy * half, (gz0 + gz1) / 2),
                                        (0.026, 0.026, gz1 - gz0), root))
    for o in lantern_iron:
        kit.assign(o, iron)
        kit.box_uv(o, 1.0)
    body_parts += lantern_iron
    for nm, facing, c in (("lp-front", "-y", (lx, ly - half, 0)), ("lp-back", "+y", (lx, ly + half, 0)),
                          ("lp-left", "-x", (lx - half, ly, 0)), ("lp-right", "+x", (lx + half, ly, 0))):
        panes.append(kit.plaque(nm, lantern_glass, (c[0], c[1], (gz0 + gz1) / 2), (2 * half, gz1 - gz0), facing, root))
    lamp_z = (gz0 + gz1) / 2

    for o in body_parts + panes:
        kit.shear(o, kx=LEAN)
    kit.join("body", body_parts)
    kit.join("glass", panes)

    kit.marker("lamp", (lx + LEAN * lamp_z, ly, lamp_z), root)
    kit.marker("door-point", (0, y_face - 1.2, 0), root)
    return root, zy(182)
