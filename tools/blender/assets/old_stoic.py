"""Old Stoic: the Tripo multi-view lift itself, refined for the game (chosen by the designer, 2026-10-25, over the
scripted rebuild in old_stoic_rebuilt.py).

The lift (art/raw/old-stoic/, made from the four views of art/reference/sheets/old-stoic-sheet.png) keeps the
sheet's character: a stout furrowed trunk, buttress roots, the square nail-holes and a lumpy painted crown. This
script only makes it fit the game:
- turns it so its front faces -Y and scales it to TREE_H, base at the origin;
- splits it by its painted colour into `trunk` (bark, roots, limbs) and `canopy` (leaves), so the crown can sway on
  its own; a couple of passes of neighbour voting tidy the boundary;
- converts the 4K JPEG texture to a 2K PNG (the game's raylib build can't read JPEG) and makes the material matte.
"""
import os

import bmesh
import numpy as np

LIFT = os.path.join("raw", "old-stoic", "lift-tripo-h3.1-multiview.glb")
TREE_H = 14.0       # metres, ground to the top of the crown
LEAN = 0.0          # extra lean toward +X (tan of the angle); 0 keeps the lift's own gentle lean
TEXTURE_SIZE = 2048


def build(kit):
    bpy = kit.bpy
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(kit.ART, LIFT))
    new = [o for o in bpy.data.objects if o not in before]
    lift = next(o for o in new if o.type == "MESH")
    for o in new:
        if o is not lift:
            bpy.data.objects.remove(o, do_unlink=True)

    # bake the importer's transform into the mesh, then turn the front to -Y, scale and stand it on the origin
    me = lift.data
    me.transform(lift.matrix_world)
    lift.location, lift.rotation_euler, lift.scale = (0, 0, 0), (0, 0, 0), (1, 1, 1)
    co = np.array([tuple(v.co) for v in me.vertices])
    co = np.stack([co[:, 1], -co[:, 0], co[:, 2]], axis=1)  # the lift faced +X; the game's front is -Y
    co[:, 2] -= co[:, 2].min()
    co *= TREE_H / co[:, 2].max()
    base = co[co[:, 2] < 0.6][:, :2].mean(axis=0)
    co[:, :2] -= base
    co[:, 0] += LEAN * co[:, 2]
    me.vertices.foreach_set("co", co.ravel())
    me.update()

    # the texture: 4K JPEG → 2K PNG, and a matte material
    mat = me.materials[0]
    mat.name = "stoic"
    nodes = mat.node_tree.nodes
    tex = next(n for n in nodes if n.type == "TEX_IMAGE")
    img = tex.image
    w, h = img.size
    px = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(h, w, 4)
    img.scale(TEXTURE_SIZE, TEXTURE_SIZE)
    os.makedirs(kit.TEXTURES, exist_ok=True)
    img.name = "stoic"
    img.filepath_raw = os.path.join(kit.TEXTURES, "stoic.png")
    img.file_format = "PNG"
    img.save()
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 1.0
    bsdf.inputs["Metallic"].default_value = 0.0
    for name in ("Metallic", "Roughness", "Normal"):  # drop any PBR maps: the cel shader does the light
        for link in list(bsdf.inputs[name].links):
            mat.node_tree.links.remove(link)

    # classify each face as leaf or bark by the paint at its centre (olive leaves are about as green as red)
    uv_layer = me.uv_layers.active.data
    leaf = np.zeros(len(me.polygons), bool)
    for f in me.polygons:
        uv = np.mean([uv_layer[li].uv for li in f.loop_indices], axis=0)
        r, g, _ = px[int(np.clip((uv[1] % 1) * h, 0, h - 1)), int(np.clip((uv[0] % 1) * w, 0, w - 1)), :3]
        leaf[f.index] = g > 0.9 * r
    # neighbour voting: a face mostly surrounded by the other kind joins it (no speckles along the boundary)
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    for _ in range(3):
        votes = leaf.copy()
        for f in bm.faces:
            nb = {g.index for e in f.edges for g in e.link_faces if g.index != f.index}
            if nb:
                share = np.mean([leaf[i] for i in nb])
                if share > 0.66:
                    votes[f.index] = True
                elif share < 0.34:
                    votes[f.index] = False
        leaf = votes
    bm.free()

    root = kit.root("old_stoic")

    def part(name, keep_leaf):
        o = lift.copy()
        o.data = lift.data.copy()
        o.name = o.data.name = name
        bpy.context.scene.collection.objects.link(o)
        b = bmesh.new()
        b.from_mesh(o.data)
        b.faces.ensure_lookup_table()
        bmesh.ops.delete(b, geom=[f for f in b.faces if leaf[f.index] != keep_leaf], context="FACES")
        bmesh.ops.delete(b, geom=[v for v in b.verts if not v.link_faces], context="VERTS")
        b.to_mesh(o.data)
        b.free()
        o.parent = root
        return o

    trunk = part("trunk", False)
    canopy = part("canopy", True)
    bpy.data.objects.remove(lift, do_unlink=True)
    print("TREE " + str({"faces": len(leaf), "canopy_faces": int(leaf.sum()), "trunk_faces": int((~leaf).sum()),
                         "trunk_verts": len(trunk.data.vertices), "canopy_verts": len(canopy.data.vertices)}))
    return root, TREE_H
