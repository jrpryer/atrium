"""Old Stoic, rebuilt from measurements (an alternative; not the chosen model, see old_stoic.py).

An enormous ancient tree, leaning about fifteen degrees, with buttress roots, a few great limbs and a
broad canopy in heavy rounded clumps. Big, old, serene and unchanging.

How it's made: the Tripo multi-view lift (art/raw/old-stoic/, from the four views of the sheet) is used only as a
measured reference. This script reads its trunk centreline and radius slice by slice, where its roots spread
and where its leaf mass sits, then builds a clean model from those numbers: a lofted trunk, tapered roots and
limbs, and canopy clumps. The lift itself never ships.

Parts the game uses: trunk (trunk, roots, limbs and the old square nail-holes, which stay still) and canopy (the
only part that sways). Reference: art/reference/sheets/old-stoic-sheet.png (Higgsfield job 6f5e159b).
"""
import math
import os

import numpy as np
from mathutils import Vector

LIFT = os.path.join("raw", "old-stoic", "lift-tripo-h3.1-multiview.glb")
TREE_H = 14.0       # metres, ground to the top of the canopy
SIDES = 20          # around the trunk
CLUMPS = 11         # canopy clumps
LEAN = 0.21         # tan(12°): the lean out over the pools (the bible asks for about 15°; the lift barely leans)
SPREAD = 1.35       # widen the crown: the multi-view lift came out narrower than the sheet's broad oak crown
rng = np.random.default_rng(1310)


# ───────────────────────── reading the lift ─────────────────────────

def read_lift(kit):
    """The lift's vertices (metres, base at the origin, front turned to face -Y) and a leaf/bark mask."""
    bpy = kit.bpy
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(kit.ART, LIFT))
    new = [o for o in bpy.data.objects if o not in before]
    mesh_obj = next(o for o in new if o.type == "MESH")
    me, mw = mesh_obj.data, mesh_obj.matrix_world
    co = np.array([tuple(mw @ v.co) for v in me.vertices])
    img = next(n.image for n in me.materials[0].node_tree.nodes if n.type == "TEX_IMAGE")
    w, h = img.size
    px = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(h, w, 4)
    uv = np.zeros((len(me.vertices), 2))
    loops_uv = me.uv_layers.active.data
    for loop in me.loops:
        uv[loop.vertex_index] = loops_uv[loop.index].uv
    col = px[np.clip((uv[:, 1] % 1) * h, 0, h - 1).astype(int), np.clip((uv[:, 0] % 1) * w, 0, w - 1).astype(int), :3]
    leaf = col[:, 1] > 0.9 * col[:, 0]  # olive leaves are as green as they are red; bark is redder
    # the lift faces +X with its lean toward +Y; turn it so the front faces -Y and the lean goes to +X
    co = np.stack([co[:, 1], -co[:, 0], co[:, 2]], axis=1)
    co[:, 2] -= co[:, 2].min()
    co *= TREE_H / co[:, 2].max()
    base = co[co[:, 2] < 0.6][:, :2].mean(axis=0)
    co[:, :2] -= base
    return co, leaf, new


def trunk_line(co, leaf, top):
    """The trunk's centre and radius every 0.6 m, from bark points near the previous centre."""
    zs = np.arange(0.4, top, 0.6)
    centre = np.zeros(2)
    line = []
    for z in zs:
        band = (~leaf) & (np.abs(co[:, 2] - z) < 0.35)
        pts = co[band][:, :2]
        if len(pts) < 20:
            break
        near = pts[np.linalg.norm(pts - centre, axis=1) < (2.4 if z < 3 else 1.8)]
        if len(near) < 12:
            near = pts
        centre = np.median(near, axis=0)
        r = float(np.percentile(np.linalg.norm(near - centre, axis=1), 55))
        line.append((centre[0], centre[1], z, r))
    line = np.array(line)
    # smooth the centreline and let the radius only ever shrink going up (no bulges from stray points)
    for k in range(2):
        line[1:-1, :2] = (line[:-2, :2] + 2 * line[1:-1, :2] + line[2:, :2]) / 4
    line[:, 3] = np.minimum.accumulate(np.convolve(line[:, 3], np.ones(3) / 3, mode="same"))
    line[-1, 3] = line[-2, 3] * 0.8
    return line


def root_directions(co, leaf, base_r, count=6):
    """Angles where bark spreads furthest beyond the trunk near the ground."""
    low = co[(~leaf) & (co[:, 2] < 1.0)]
    d = np.linalg.norm(low[:, :2], axis=1)
    far = low[d > base_r * 1.15]
    if len(far) < 10:
        return np.linspace(0, 2 * math.pi, count, endpoint=False)
    ang = np.arctan2(far[:, 1], far[:, 0])
    hist, edges = np.histogram(ang, bins=24, range=(-math.pi, math.pi), weights=np.linalg.norm(far[:, :2], axis=1))
    picks = []
    for i in np.argsort(hist)[::-1]:
        a = (edges[i] + edges[i + 1]) / 2
        if all(abs((a - p + math.pi) % (2 * math.pi) - math.pi) > 0.7 for p in picks):
            picks.append(a)
        if len(picks) == count:
            break
    return np.array(picks)


def canopy_clumps(co, leaf, k):
    """k-means over the leaf points: each cluster becomes one clump (centre and size)."""
    pts = co[leaf]
    pts = pts[pts[:, 2] > np.percentile(pts[:, 2], 3)]
    cent = pts[rng.choice(len(pts), k, replace=False)]
    for _ in range(30):
        lab = np.argmin(((pts[:, None, :] - cent[None, :, :]) ** 2).sum(-1), axis=1)
        cent = np.array([pts[lab == i].mean(0) if (lab == i).any() else cent[i] for i in range(k)])
    clumps = []
    for i in range(k):
        p = pts[lab == i]
        if len(p) < 15:
            continue
        size = np.clip(p.std(0) * 3.0, 2.8, 5.4)
        clumps.append((cent[i], size))
    # spread the clumps out from the crown's centre (wider, like the sheet) and pull them down a touch
    mid = np.mean([c for c, _ in clumps], axis=0)
    clumps = [(np.array([mid[0] + (c[0] - mid[0]) * SPREAD, mid[1] + (c[1] - mid[1]) * SPREAD, c[2] - 0.3]), sz) for c, sz in clumps]
    return clumps


# ───────────────────────── building ─────────────────────────

def tube(kit, name, points, radii, sides, parent, furrows=0.0, seed=0):
    """A lofted tube through `points` (with a radius at each), capped at both ends. `furrows` cuts gentle
    vertical grooves (bark)."""
    pts = [Vector(p) for p in points]
    verts, faces = [], []
    prev_side = None
    for i, (p, r) in enumerate(zip(pts, radii)):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        side = (prev_side if prev_side is not None else (Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))).cross(t))
        side = (side - t * side.dot(t)).normalized()
        prev_side = side
        up = t.cross(side).normalized()
        for s in range(sides):
            a = 2 * math.pi * s / sides
            groove = 1 - furrows * (0.5 + 0.5 * math.sin(a * 7 + seed + i * 0.35)) ** 3
            verts.append(tuple(p + (side * math.cos(a) + up * math.sin(a)) * r * groove))
    n = len(pts)
    for i in range(n - 1):
        for s in range(sides):
            a0, a1 = i * sides + s, i * sides + (s + 1) % sides
            faces.append((a0, a1, a1 + sides, a0 + sides))
    faces.append(tuple(reversed(range(sides))))
    faces.append(tuple(range((n - 1) * sides, n * sides)))
    return kit.mesh_object(name, verts, faces, parent, solid=True)


def clump(kit, name, centre, size, parent, seed):
    """A lumpy, cloud-like clump of leaves: an icosphere pushed out in a few big bulges, its underside flattened."""
    bpy = kit.bpy
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=4, radius=1.0, location=(0, 0, 0))
    o = bpy.context.active_object
    o.name = name
    r = np.random.default_rng(seed)
    bumps = r.normal(size=(11, 3))
    bumps /= np.linalg.norm(bumps, axis=1, keepdims=True)
    for v in o.data.vertices:
        d = np.array(v.co)
        lump = 0.92 + 0.2 * sum(max(0.0, float(d @ b)) ** 6 for b in bumps)  # scalloped, like the sheet's clumps
        p = d * lump
        if p[2] < -0.25:  # a flatter underside, like the clumps on the sheet
            p[2] = -0.25 + (p[2] + 0.25) * 0.35
        v.co = (p[0] * size[0] / 2 + centre[0], p[1] * size[1] / 2 + centre[1], p[2] * size[2] / 2 + centre[2])
    o.parent = parent
    return o


def build(kit):
    co, leaf, lift_objects = read_lift(kit)
    leaf_z = co[leaf][:, 2]
    canopy_base = float(np.percentile(leaf_z, 4)) if len(leaf_z) else TREE_H * 0.45
    line = trunk_line(co, leaf, canopy_base + 2.0)
    clumps = canopy_clumps(co, leaf, CLUMPS)
    base_r = float(line[0, 3])
    roots = root_directions(co, leaf, base_r)
    print("TREE " + str({"leaf_points": int(leaf.sum()), "canopy_base": round(canopy_base, 2), "trunk_slices": len(line),
                         "base_radius": round(base_r, 2), "top": [round(float(x), 2) for x in line[-1]],
                         "clumps": len(clumps), "roots": len(roots)}))

    # a look at the lift itself from the same cameras, for comparison, then it's removed
    for o in lift_objects:
        if o.type == "MESH":
            o.rotation_euler = (0, 0, -math.pi / 2)
            o.scale = (TREE_H,) * 3
            o.location = (0, 0, TREE_H / 2)
    kit.previews("old_stoic_rebuilt-lift", TREE_H, views=("front", "threequarter"))
    for o in lift_objects:
        kit.bpy.data.objects.remove(o, do_unlink=True)

    # bark and leaves are tileable textures generated in the sheet's style (painted sheets tile badly: their
    # ink outlines repeat as zigzags)
    bark = kit.material("stoic-bark", kit.texture("stoic-bark", "stoic-bark.png", mirror="v", step=2))
    leaves = kit.material("stoic-leaves", kit.texture("stoic-leaves", "stoic-leaves.png", mirror="v", box=(0, 0, 2048, 980), step=2))  # the top band: the rest grew trunks
    BARK_TILE = (2.4, 4.8)   # metres per repeat (mirrored top to bottom, so twice as tall)
    LEAF_TILE = (6.0, 5.8)   # big repeats, so the pattern doesn't read as a pattern
    hole = kit.material("stoic-nail-hole", color=kit.srgb_to_linear((0.16, 0.11, 0.09)))
    root = kit.root("old_stoic_rebuilt")

    def lean(o):
        """The deliberate lean out over the pools: everything shifts toward +X with height."""
        for v in o.data.vertices:
            v.co.x += LEAN * max(0.0, v.co.z)

    # ── trunk: lofted along the measured centreline, flaring at the foot ──
    pts = [(x, y, z) for x, y, z, _ in line]
    radii = [r for *_, r in line]
    pts[0:0] = [(pts[0][0], pts[0][1], -0.3), (pts[0][0], pts[0][1], 0.1)]
    radii[0:0] = [radii[0] * 1.55, radii[0] * 1.3]  # the foot flares into the ground
    trunk = tube(kit, "trunk-core", pts, radii, SIDES, root, furrows=0.12)
    parts = [trunk]

    # ── roots: tapered, arching out and down where the lift's roots spread ──
    for i, a in enumerate(roots):
        d = Vector((math.cos(a), math.sin(a), 0))
        reach = base_r * (1.6 + 0.25 * (i % 3))
        c = [Vector((0, 0, 2.0)) + d * base_r * 0.45, Vector((0, 0, 0.9)) + d * base_r * 1.05,
             Vector((0, 0, 0.2)) + d * base_r * 1.45, Vector((0, 0, -0.45)) + d * reach]  # the tips dive into the ground
        parts.append(tube(kit, f"root{i}", c, [base_r * 0.55, base_r * 0.42, base_r * 0.26, base_r * 0.14], 12, root, furrows=0.1, seed=i))

    # ── limbs: from the upper trunk out into each clump ──
    top = Vector(pts[-1])
    for i, (c, size) in enumerate(clumps):
        target = Vector(c) - Vector((0, 0, size[2] * 0.15))
        start_i = max(1, len(pts) - 1 - (i % 3))
        start = Vector(pts[start_i])
        mid = start.lerp(target, 0.55) + Vector((0, 0, 0.4))
        r0 = radii[start_i] * 0.45
        parts.append(tube(kit, f"limb{i}", [start, mid, target], [r0, r0 * 0.6, r0 * 0.3], 8, root, seed=i + 20))
    _ = top

    for o in parts:
        kit.assign(o, bark)
        lean(o)
        kit.box_uv(o, BARK_TILE)
        kit.smooth(o)
    trunk = kit.join("trunk", parts)

    # ── the old square nail-holes, empty and dark, at a raised hand's height on the front ──
    holes = []
    bvh_obj = trunk
    for i, (dx, z) in enumerate(((-0.35, 1.95), (0.1, 2.15), (0.45, 1.9), (-0.1, 2.4), (0.3, 2.45), (-0.5, 2.25))):
        hit, loc, normal, _ = bvh_obj.ray_cast(Vector((dx + LEAN * z, -20, z)), Vector((0, 1, 0)))
        if not hit:
            continue
        n = normal.normalized()
        u = Vector((0, 0, 1)).cross(n).normalized()
        v = n.cross(u).normalized()
        c = loc + n * 0.01
        s = 0.06
        quad = [c - u * s - v * s, c + u * s - v * s, c + u * s + v * s, c - u * s + v * s]
        q = kit.mesh_object(f"hole{i}", [tuple(p) for p in quad], [(0, 1, 2, 3)], root)
        if q.data.polygons[0].normal.dot(n) < 0:
            q.data.flip_normals()
        kit.assign(q, hole)
        kit.fit_uv(q)
        holes.append(q)
    if holes:
        trunk = kit.join("trunk", [trunk, *holes])

    # ── canopy: the clumps, one swaying part ──
    leaves_parts = [clump(kit, f"clump{i}", c, size, root, 100 + i) for i, (c, size) in enumerate(clumps)]
    # a hidden core so the crown never shows daylight between clumps
    leaf_pts = co[leaf]
    core_c = leaf_pts.mean(0)
    core_s = (leaf_pts.max(0) - leaf_pts.min(0)) * np.array([0.42, 0.42, 0.36])
    leaves_parts.append(clump(kit, "core", core_c, core_s, root, 99))
    for o in leaves_parts:
        kit.assign(o, leaves)
        lean(o)
        kit.box_uv(o, LEAF_TILE)
        kit.smooth(o)
    kit.join("canopy", leaves_parts)
    return root, TREE_H + 0.5
