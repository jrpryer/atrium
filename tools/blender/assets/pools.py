"""The Spirit Pools' cave mouth (designer direction, 2026-10-26; it replaces the Stillkeepers' lodge): a mossy rock
outcrop at the end of the cliff where the courtyard's stream goes underground, with worn steps going down into the
dark beside the water, a wooden gate across them while the pools are locked, and a lamp post at the top of the steps
that is lit only once they're open.
Concept: art/reference/concepts/courtyard-stream-concept.png.

Parts: body (rock, moss, steps, the lamp post's iron), glass (the lantern's panes: the game lights them only when the
pools are open), shutters (the gate, closed: shown while locked), shutters-open (the gate swung back: shown once
open); markers lamp (the lantern) and door-point (the top of the steps). Front faces -Y (the courtyard's tree).
"""
import math
import random

import bmesh
import bpy
from mathutils import Vector

from assets.pools_grotto import painted

PRE = "pools-"
W, D, H = 4.6, 3.4, 3.4          # the outcrop: width, depth, height
ARCH_W, ARCH_H = 1.9, 2.1        # the opening


def rock_lump(kit, name, centre, size, mat, seed, parent):
    """A lumpy rock: an icosphere pushed about by a little noise, flat-shaded."""
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=(0, 0, 0))
    o = bpy.context.active_object
    o.name = name
    rng = random.Random(seed)
    for v in o.data.vertices:
        p = v.co
        k = 1 + 0.18 * math.sin(p.x * 3.1 + seed) * math.sin(p.y * 2.7 + seed * 0.3) + rng.uniform(-0.06, 0.06)
        v.co = Vector((p.x * size[0] * k + centre[0], p.y * size[1] * k + centre[1], max(p.z * size[2] * k, -0.05) + centre[2]))
    o.parent = parent
    kit.assign(o, mat)
    kit.box_uv(o, 2.0)
    return o


def build(kit):
    lin = kit.srgb_to_linear
    rock = kit.material(PRE + "rock", painted(kit, "pools-rock", (0.52, 0.48, 0.41), (0.45, 0.41, 0.35), (0.58, 0.54, 0.46), 11))
    moss = kit.material(PRE + "moss", painted(kit, "pools-moss", (0.42, 0.52, 0.25), (0.33, 0.42, 0.20), (0.52, 0.61, 0.30), 12, cracks=False))
    deep = kit.material(PRE + "deep", color=lin((0.05, 0.07, 0.07)))
    step = kit.material(PRE + "step", color=lin((0.55, 0.52, 0.45)))
    plank = kit.material(PRE + "plank", color=lin((0.45, 0.33, 0.22)))
    iron = kit.material(PRE + "iron", color=lin((0.20, 0.20, 0.22)))
    glass = kit.material(PRE + "lantern-glass", color=lin((0.95, 0.80, 0.50)))
    root = kit.root("pools")
    hw = W / 2
    body = []

    # the outcrop: big lumps either side of the arch and over it, smaller ones round the foot
    for i, (c, s) in enumerate([((-1.75, 0.3, 1.2), (1.2, 1.4, 1.5)), ((1.8, 0.2, 1.1), (1.1, 1.3, 1.4)), ((0, 0.6, 2.6), (2.1, 1.3, 0.9)),
                                ((-0.9, 1.1, 1.6), (1.3, 1.0, 1.6)), ((1.0, 1.1, 1.5), (1.2, 1.0, 1.5)), ((-2.3, -0.6, 0.35), (0.6, 0.5, 0.4)),
                                ((2.4, -0.5, 0.3), (0.55, 0.45, 0.35)), ((0, 1.4, 0.9), (1.6, 0.6, 1.0))]):
        body.append(rock_lump(kit, f"rock{i}", c, s, rock, 30 + i, root))
    # moss over the top, like the concept's green cap
    for i, (c, s) in enumerate([((0, 0.5, 3.35), (1.7, 1.1, 0.28)), ((-1.6, 0.2, 2.55), (0.9, 0.9, 0.22)), ((1.7, 0.2, 2.4), (0.8, 0.8, 0.2))]):
        body.append(rock_lump(kit, f"moss{i}", c, s, moss, 60 + i, root))

    # the dark opening (an arched face set into the rock) and the steps going down into it
    seg = 12
    pts = [(-ARCH_W / 2, 0.0)] + [(ARCH_W / 2 * math.cos(math.pi * k / seg) * -1, ARCH_H - ARCH_W / 2 + ARCH_W / 2 * math.sin(math.pi * k / seg)) for k in range(seg + 1)] + [(ARCH_W / 2, 0.0)]
    verts = [(x, -0.45, z) for x, z in pts]
    hole = kit.mesh_object("opening", verts, [tuple(range(len(verts)))[::-1]], root)
    if hole.data.polygons[0].normal.y > 0:
        hole.data.flip_normals()
    kit.assign(hole, deep)
    body.append(hole)
    for i in range(4):  # worn steps going down into the dark
        body.append(kit.box(f"step{i}", (0.45, -0.95 + i * 0.32, 0.2 - i * 0.07), (0.9, 0.34, 0.12), root))  # the stream takes the left half
        kit.assign(body[-1], step)

    # the lamp post at the top of the steps (the game lights its panes only when the pools are open)
    lx, ly = 1.55, -1.45
    body.append(kit.cylinder("post", (lx, ly, 0), 0.07, 0.05, 2.3, 8, root))
    kit.assign(body[-1], iron)
    body.append(kit.box("lantern-base", (lx, ly, 2.33), (0.3, 0.3, 0.05), root))
    body.append(kit.cylinder("lantern-cap", (lx, ly, 2.75), 0.2, 0.03, 0.2, 4, root))
    for o in body[-2:]:
        kit.assign(o, iron)
    panes = []
    for name, facing, off in (("pane-f", "-y", (0, -0.12)), ("pane-b", "+y", (0, 0.12)), ("pane-l", "-x", (-0.12, 0)), ("pane-r", "+x", (0.12, 0))):
        panes.append(kit.plaque(name, glass, (lx + off[0], ly + off[1], 2.54), (0.22, 0.36), facing, root))
    kit.join("glass", panes)
    kit.marker("lamp", (lx, ly - 0.3, 2.54), root)

    # the gate: closed across the opening (locked) and swung back against the rock (open)
    def gate(name, x0, y0, angle):
        """Planks and two iron bands across the opening, hinged at (x0, y0) and turned by `angle` about the hinge."""
        parts = []
        for k in range(6):
            o = kit.box(f"{name}-plank{k}", (x0 + (k + 0.5) * (ARCH_W - 0.1) / 6, y0, 0.85), (0.28, 0.05, 1.7 - abs(k - 2.5) * 0.06), root)
            kit.assign(o, plank)
            parts.append(o)
        for z in (0.35, 1.35):
            o = kit.box(f"{name}-band{z}", (x0 + (ARCH_W - 0.1) / 2, y0 - 0.04, z), (ARCH_W - 0.1, 0.03, 0.07), root)
            kit.assign(o, iron)
            parts.append(o)
        ca, sa = math.cos(angle), math.sin(angle)
        for o in parts:
            for v in o.data.vertices:
                dx, dy = v.co.x - x0, v.co.y - y0
                v.co.x, v.co.y = x0 + dx * ca - dy * sa, y0 + dx * sa + dy * ca
        return kit.join(name, parts)

    gate("shutters", -(ARCH_W - 0.1) / 2, -0.6, 0.0)
    gate("shutters-open", -(ARCH_W - 0.1) / 2, -0.6, -math.pi * 0.55)

    kit.join("body", body)
    kit.marker("door-point", (0, -2.2, 0), root)
    return root, H
