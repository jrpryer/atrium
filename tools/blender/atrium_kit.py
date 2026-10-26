"""Shared helpers for building Atrium's models in Blender from AI reference sheets.

Run an asset with Blender in the background (see README.md):
    blender -b --factory-startup --python tools/blender/build.py -- library

Conventions (iteration-3 art bible, "How the art meets the engine"):
- metres; the asset's base centre sits at the origin; its front faces -Y in Blender (+Z in glTF and the game);
- one object per semantic part, named for what the game does with it (body, glass, shutters, canopy…);
- painted colour comes from swatches cut out of the asset's reference sheet, tiled seamlessly; no baked light;
- textures are PNG (the game's raylib build can't read JPEG); every part stays under 65,535 vertices.
"""
import bpy
import bmesh
import json
import math
import os
import sys
import numpy as np
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "art")
MODELS = os.path.join(ART, "models")
TEXTURES = os.path.join(ART, "models", "textures")
PREVIEWS = os.path.join(ART, "previews")
SHEETS = os.path.join(ART, "reference", "sheets")
MAX_VERTS = 65535


def script_args():
    """Arguments after Blender's `--`."""
    return sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def reset():
    """An empty scene in metres."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    s = bpy.context.scene
    s.unit_settings.system = "METRIC"
    s.unit_settings.scale_length = 1.0
    return s


# ───────────────────────── swatches from a reference sheet ─────────────────────────

def load_sheet(filename):
    """A reference sheet from art/reference/sheets, kept as a numpy array (rows from the top)."""
    path = os.path.join(SHEETS, filename)
    img = bpy.data.images.load(path, check_existing=True)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    return px.reshape(h, w, 4)[::-1]  # Blender stores rows bottom-up; flip so y=0 is the top


def swatch(sheet, name, x0, y0, x1, y1, tile=True, mirror="both"):
    """Cuts sheet[y0:y1, x0:x1] (pixels, from the top-left) into a PNG texture.
    With tile=True it is mirrored into a block so it repeats without seams: mirror="both" (2×2) suits most
    materials; mirror="v" only flips top-to-bottom, for grain that runs one way (bark furrows), where a sideways
    mirror would make chevrons."""
    region = sheet[y0:y1, x0:x1].copy()
    region[..., 3] = 1.0
    if tile:
        if mirror == "both":
            region = np.concatenate([region, region[:, ::-1]], axis=1)
        region = np.concatenate([region, region[::-1]], axis=0)
    return _image_from_array(name, region)


def texture(name, filename, mirror="both", box=None, step=1):
    """A tileable texture generated on its own (art/reference/textures/<filename>), mirrored like a swatch so any
    seam the generator left can't show. `box` (x0, y0, x1, y1, pixels from the top-left) keeps only the clean part
    of a generation; `step` 2 halves the resolution."""
    img = bpy.data.images.load(os.path.join(ART, "reference", "textures", filename), check_existing=True)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    arr = px.reshape(h, w, 4)[::-1]
    x0, y0, x1, y1 = box or (0, 0, w, h)
    arr = arr[y0:y1:step, x0:x1:step]
    return swatch(arr, name, 0, 0, arr.shape[1], arr.shape[0], tile=True, mirror=mirror)


def decal(sheet, name, x0, y0, x1, y1):
    """Cuts a single painted feature (a door, a window) to map once onto its quad."""
    return swatch(sheet, name, x0, y0, x1, y1, tile=False)


def _image_from_array(name, arr):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(name, width=w, height=h, alpha=False)
    img.pixels.foreach_set(arr[::-1].astype(np.float32).ravel())
    os.makedirs(TEXTURES, exist_ok=True)
    img.filepath_raw = os.path.join(TEXTURES, name + ".png")
    img.file_format = "PNG"
    img.save()
    return img


# ───────────────────────── materials ─────────────────────────

def material(name, image=None, color=(0.8, 0.8, 0.8), emissive=None):
    """A matte Principled material: an image texture if given, else a flat colour. No PBR maps (the cel shader
    does the light); emissive marks parts the game lights up (lamp glass, windows)."""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 1.0
    bsdf.inputs["Metallic"].default_value = 0.0
    if image is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image
        tex.interpolation = "Linear"
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    if emissive is not None:
        bsdf.inputs["Emission Color"].default_value = (*emissive, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.0
    return m


def assign(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


# ───────────────────────── geometry helpers ─────────────────────────

def mesh_object(name, verts, faces, parent=None, solid=False):
    """A mesh from vertices and faces. solid=True turns every face outward (closed shapes only): the game culls
    back faces, so a face wound inward would vanish."""
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], [tuple(f) for f in faces])
    me.validate()
    if solid:
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me)
        bm.free()
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if parent is not None:
        ob.parent = parent
    return ob


def box(name, centre, size, parent=None):
    """An axis-aligned box from its centre and size (metres)."""
    cx, cy, cz = centre
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    v = [(cx + x * hx, cy + y * hy, cz + z * hz) for x in (-1, 1) for y in (-1, 1) for z in (-1, 1)]
    f = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    return mesh_object(name, v, f, parent, solid=True)


def cylinder(name, base, r0, r1, height, segments=8, parent=None, caps=True):
    """A tapered cylinder standing on `base` (radius r0 at the bottom, r1 at the top)."""
    bx, by, bz = base
    v, f = [], []
    for i in range(segments):
        a = 2 * math.pi * i / segments
        v.append((bx + r0 * math.cos(a), by + r0 * math.sin(a), bz))
        v.append((bx + r1 * math.cos(a), by + r1 * math.sin(a), bz + height))
    for i in range(segments):
        j = (i + 1) % segments
        f.append((2 * i, 2 * j, 2 * j + 1, 2 * i + 1))
    if caps:
        f.append(tuple(2 * i for i in reversed(range(segments))))
        f.append(tuple(2 * i + 1 for i in range(segments)))
    return mesh_object(name, v, f, parent, solid=caps)


def prism(name, profile, y0, y1, parent=None):
    """Extrudes a closed XZ outline (counter-clockwise seen from -Y) along Y from y0 to y1."""
    n = len(profile)
    v = [(x, y0, z) for x, z in profile] + [(x, y1, z) for x, z in profile]
    f = [tuple(reversed(range(n))), tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        f.append((i, j, n + j, n + i))
    return mesh_object(name, v, f, parent, solid=True)


def prism_x(name, profile, x0, x1, parent=None):
    """Extrudes a closed YZ outline along X from x0 to x1 (a gable that faces ±X, like a dormer)."""
    n = len(profile)
    v = [(x0, y, z) for y, z in profile] + [(x1, y, z) for y, z in profile]
    f = [tuple(range(n)), tuple(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        f.append((i, j, n + j, n + i))
    return mesh_object(name, v, f, parent, solid=True)


def plaque(name, mat, centre, size, facing, parent=None):
    """A flat decal quad (a door, a window) facing one axis: "-y" (front), "+y", "+x" or "-x"."""
    cx, cy, cz = centre
    w, h = size
    axis = {"-y": (0, -1, 0), "+y": (0, 1, 0), "+x": (1, 0, 0), "-x": (-1, 0, 0)}[facing]
    if facing in ("-y", "+y"):
        v = [(cx - w / 2, cy, cz - h / 2), (cx + w / 2, cy, cz - h / 2), (cx + w / 2, cy, cz + h / 2), (cx - w / 2, cy, cz + h / 2)]
        flip = facing == "+y"
    else:
        v = [(cx, cy + w / 2, cz - h / 2), (cx, cy - w / 2, cz - h / 2), (cx, cy - w / 2, cz + h / 2), (cx, cy + w / 2, cz + h / 2)]
        flip = facing == "-x"
    if flip:  # mirror left-right so the decal still reads the right way round from its own side
        v = [v[1], v[0], v[3], v[2]]
    o = mesh_object(name, v, [(0, 1, 2, 3)], parent)
    n = o.data.polygons[0].normal
    if n.dot(Vector(axis)) < 0:
        o.data.flip_normals()
    assign(o, mat)
    fit_uv(o)
    return o


def srgb_to_linear(c):
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


def sample(sheet, x, y, r=6):
    """The average colour of a small patch of a sheet (pixels from the top-left), as a linear material colour."""
    c = sheet[y - r:y + r, x - r:x + r, :3].reshape(-1, 3).mean(axis=0)
    return srgb_to_linear(tuple(float(v) for v in c))


def sheet_tile(x0, y0, x1, y1, metres_per_px):
    """The real-world size of one mirrored swatch tile cut from sheet pixels (x0, y0)-(x1, y1)."""
    return (2 * (x1 - x0) * metres_per_px, 2 * (y1 - y0) * metres_per_px)


def smooth(obj):
    """Smooth shading (organic parts: bark, leaves). Hard-surface parts stay flat."""
    for p in obj.data.polygons:
        p.use_smooth = True


def join(name, objects):
    """Joins objects into one (the first keeps its name and materials merge into slots)."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    objects[0].name = name
    objects[0].data.name = name
    return objects[0]


def root(name):
    """The asset's root empty; every part is parented to it."""
    e = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(e)
    return e


def marker(name, location, parent):
    """An empty the game reads by name (where a lamp's light sits, where the door is…)."""
    e = bpy.data.objects.new(name, None)
    e.location = location
    e.empty_display_size = 0.2
    e.parent = parent
    bpy.context.scene.collection.objects.link(e)
    return e


def shear(obj, kx=0.0, ky=0.0):
    """Leans a part by moving each vertex sideways in proportion to its height (a settled, never-plumb look)."""
    for v in obj.data.vertices:
        v.co.x += kx * v.co.z
        v.co.y += ky * v.co.z


def bevel(obj, width=0.03, segments=1):
    mod = obj.modifiers.new("bevel", "BEVEL")
    mod.width = width
    mod.segments = segments
    mod.limit_method = "ANGLE"
    return mod


def apply_modifiers(obj):
    bpy.context.view_layer.objects.active = obj
    for m in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def _tile(tile):
    return (tile, tile) if isinstance(tile, (int, float)) else tile


def box_uv(obj, tile=1.0):
    """World-space box projection: each face takes the plane its normal mostly faces, at `tile` metres per
    texture repeat (a number, or (across, up) for a non-square swatch), so swatches keep one scale on every part."""
    tu, tv = _tile(tile)
    me = obj.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    mw = obj.matrix_world
    for poly in me.polygons:
        n = (mw.to_3x3() @ poly.normal).normalized()
        ax = max(range(3), key=lambda i: abs(n[i]))
        for li in poly.loop_indices:
            p = mw @ me.vertices[me.loops[li].vertex_index].co
            if ax == 0:
                u, v = p.y * (1 if n.x > 0 else -1), p.z
            elif ax == 1:
                u, v = p.x * (-1 if n.y > 0 else 1), p.z
            else:
                u, v = p.x, p.y
            uv[li].uv = (u / tu, v / tv)


def slope_uv(obj, tile=1.0):
    """For roofs: each face is mapped in its own plane (along the ridge × down the slope), so slates don't
    stretch the way a box projection stretches them on a steep pitch."""
    tu, tv = _tile(tile)
    me = obj.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    mw = obj.matrix_world
    up = Vector((0, 0, 1))
    for poly in me.polygons:
        n = (mw.to_3x3() @ poly.normal).normalized()
        along = up.cross(n)
        if along.length < 1e-4:
            along = Vector((1, 0, 0))
        along.normalize()
        down = n.cross(along).normalized()
        for li in poly.loop_indices:
            p = mw @ me.vertices[me.loops[li].vertex_index].co
            uv[li].uv = (p.dot(along) / tu, p.dot(down) / tv)


def fit_uv(obj):
    """Maps each face once, 0..1 across its own extent (decals: a door, a window), upright: v runs up the face, as
    the stored images do (their bottom row is v = 0)."""
    me = obj.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    up = Vector((0, 0, 1))
    for poly in me.polygons:
        n = poly.normal.normalized()
        along = up.cross(n)
        if along.length < 1e-4:
            along = Vector((1, 0, 0))
        along.normalize()
        vert = n.cross(along).normalized()
        pts = [me.vertices[me.loops[li].vertex_index].co for li in poly.loop_indices]
        us = [p.dot(along) for p in pts]
        vs = [p.dot(vert) for p in pts]
        u0, u1, v0, v1 = min(us), max(us), min(vs), max(vs)
        for li, u, v in zip(poly.loop_indices, us, vs):
            uv[li].uv = ((u - u0) / max(u1 - u0, 1e-6), (v - v0) / max(v1 - v0, 1e-6))


# ───────────────────────── checks, export, previews ─────────────────────────

def export_vertex_count(me):
    """How many vertices glTF will write: one per distinct (vertex, normal, UV) corner, per material."""
    layer = me.uv_layers.active or (me.uv_layers[0] if me.uv_layers else None)
    uv = layer.data if layer else None
    normals = me.corner_normals
    seen = set()
    for poly in me.polygons:
        for li in poly.loop_indices:
            n = normals[li].vector
            key = (poly.material_index, me.loops[li].vertex_index, round(n.x, 3), round(n.y, 3), round(n.z, 3),
                   (round(uv[li].uv.x, 5), round(uv[li].uv.y, 5)) if uv else None)
            seen.add(key)
    return len(seen)


def report(asset_root):
    """Counts per part, and the checks the game's import step enforces."""
    parts = {}
    ok = True
    for o in asset_root.children_recursive:
        if o.type != "MESH":
            parts[o.name] = {"type": o.type}
            continue
        me = o.data
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        verts = export_vertex_count(me)
        parts[o.name] = {"vertices": len(me.vertices), "export_vertices": verts, "triangles": tris,
                         "materials": [m.name for m in me.materials if m]}
        if verts > MAX_VERTS:
            ok = False
    return {"ok": ok, "parts": parts}


def export(asset_root, name):
    """Exports the asset to art/models/<name>.glb with embedded PNG textures."""
    os.makedirs(MODELS, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    asset_root.select_set(True)
    for o in asset_root.children_recursive:
        o.select_set(True)
    path = os.path.join(MODELS, name + ".glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True,
                              export_yup=True, export_image_format="AUTO", export_materials="EXPORT",
                              export_texcoords=True, export_normals=True, export_tangents=False)
    info = report(asset_root)
    info["file"] = os.path.relpath(path, ROOT).replace("\\", "/")
    info["bytes"] = os.path.getsize(path)
    print("ASSET " + json.dumps(info))
    return info


def previews(name, height, views=("front", "right", "back", "threequarter"), size=900):
    """Renders the asset from the front, right, back and a three-quarter angle into art/previews, for comparing
    with the reference sheet. Soft studio light, no cel shading (that's the game's job)."""
    os.makedirs(PREVIEWS, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("preview") if scene.world is None else scene.world
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.48, 0.48, 0.48, 1)
    bg.inputs["Strength"].default_value = 1.0
    sun_data = bpy.data.lights.new("preview-sun", "SUN")
    sun_data.energy = 2.5
    sun = bpy.data.objects.new("preview-sun", sun_data)
    sun.rotation_euler = (math.radians(50), 0, math.radians(30))
    scene.collection.objects.link(sun)
    cam_data = bpy.data.cameras.new("preview-cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("preview-cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    # frame the asset's real bounds (a leaning tree reaches well past its base)
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in scene.objects:
        if o.type != "MESH":
            continue
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    if lo.x > hi.x:
        lo, hi = Vector((-height / 2, -height / 2, 0)), Vector((height / 2, height / 2, height))
    target = (lo + hi) / 2
    span = max(hi.z - lo.z, hi.x - lo.x, hi.y - lo.y) * 1.12
    cam_data.ortho_scale = span
    height = span
    angles = {"front": (0, -1, 0), "right": (1, 0, 0), "back": (0, 1, 0), "left": (-1, 0, 0), "threequarter": (0.75, -1, 0.55)}
    written = []
    for v in views:
        d = Vector(angles[v]).normalized()
        cam.location = target + d * (height * 4)
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        path = os.path.join(PREVIEWS, f"{name}-{v}.png")
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        written.append(os.path.relpath(path, ROOT).replace("\\", "/"))
    for o in (sun, cam):
        bpy.data.objects.remove(o)
    print("PREVIEWS " + json.dumps(written))
    return written
