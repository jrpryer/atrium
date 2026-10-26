# Iteration 3: generation log

*A running record of every generation, so results can be traced and judged later. This is the first step
toward `art/manifest.json` (see the art bible's "Provenance and licensing"). Files stay in the Higgsfield
library under the job ids below; nothing generated is in the repo yet.*

## 2026-10-25: the courtyard, first pass

**Tool:** Higgsfield plugin (Plus plan). The brief is `anchors.courtyard_scene` in
[iteration-3-prompts.json](iteration-3-prompts.json), using direction A (Lamplit Ink). The plan was keyframe,
then diorama, then a 3D lift.

| Step | Model | Job id | Credits | Verdict |
|---|---|---|---|---|
| Keyframe A | Nano Banana Pro, 2K, 16:9 | `0d7ca74e-5382-49da-8198-68c9ac8520bf` | 2 | **Best keyframe.** The courtyard is truly enclosed by the city (timber-framed houses on every side, stairs, a bridge, washing lines, warm light high up and shade below). All four houses read at a glance, and the tree's roots plunge into the dark pool. Flaw: a drawn comic-panel border. |
| Keyframe B | Nano Banana Pro, 2K, 16:9 | `2787cb61-3479-489f-9f23-7dadf9a70e06` | 2 | Clean ink and flat colour, every house readable, the three pools right. More of a comic panel than a field-book plate. Same panel border. |
| Keyframe C | GPT Image 2.5 (medium), 2K, 16:9 | `a8084dba-f9ee-4c1e-a29c-3ee7ec75e7bf` | 1 | The richest "corner of a medieval city" and the best atmosphere. The lodge has its porch, the window is full of jars and the still pond mirrors the sky. But it's the generic painted-storybook look the review warned about, with little ink line. |
| Diorama 1 | Nano Banana Pro, 2K, 1:1, refs B + C | `4d488269-2a14-4624-903d-9f000d575502` | 2 | The canopy is kept, but the city behind is a flat painted card (it would lift as a flat wall), and a duplicate violet-roofed shop was added in front. |
| Diorama 2 | Nano Banana Pro, 2K, 1:1, refs B + C | `c67697a5-f03d-416e-a432-0aef00d57f8b` | 2 | A true physical miniature: stone base, cliff and waterfall, 3D city tiers. **The tree came out bare** (it breaks Old Stoic), and the duplicate shop again. |
| Diorama 2, fixed | Nano Banana Pro edit, refs diorama 2 + keyframe A | `1e94a2a7-b968-4e8f-a005-04c8fb24e210` | 2 | **Chosen for the lift.** Full, healthy canopy restored and the duplicate shop removed; everything else kept. |
| 3D lift | Tripo P2 image-to-3D (delight on, detailed texture, no PBR, UVs, 40,000-face limit) | `70e2d106-91b5-496d-8825-68bf6fe2111c` | 37 | **Strong from the front, invented behind.** One mesh, 57,398 vertices and 46,890 triangles (just under raylib's 65,535-vertex limit), 1 × 0.78 × 1 units, one 3.9 MB JPEG texture. From the front half all four houses keep their shapes and colours, the canopy is chunky and painterly, and the pools, steps, bridge and stacked city are real geometry. The back is blank dark slab walls, and the waterfall mostly flattened into the cliff. The texture keeps the painted feel but softens the ink line. |

**Total so far:** 48 credits (110 → 62).

**Lessons for the prompt pack:**

- **Nano Banana Pro adds a comic-panel border** even though the prompt asks for none. Add "full-bleed, no frame
  or panel border" to every scene prompt; a negative field alone isn't available on this model.
- **Image models invent extra buildings.** Both dioramas added a second violet-roofed shop. Prompts that list
  the four houses should add "exactly four houses, no others on the base".
- **"Leaning tree" can come back bare.** Always say "with a full, broad canopy of leaves" next to the lean.
- **A diorama prompt can produce either a painted illustration of a diorama or a photographed miniature.** The
  photographed miniature is the one that lifts well, because its forms are solid and its lighting is even.

**Lessons for the engine (feeds task E1):**

- **Tripo embeds its texture as JPEG**, and this raylib build can't decode JPEG (it reports "Data format not
  supported" and draws the model white). The import step must convert textures to PNG.
  `glb_png.py`-style conversion works but inflates the file about 5× (6 MB → 29 MB); DDS would be smaller.
- **One fused mesh with two materials and baked albedo.** As the bible predicted, nothing is separable: no lit
  windows, no shutters, no canopy sway. A single-image lift invents the back, so it suits a fixed or
  limited-orbit camera, not iteration 2's full 360° orbit.
- **To check:** the model viewer (`--view file.glb`) shows the albedo only. The cel shader and ink outline need
  E1 before the mesh can be judged in the game's own look.

## 2026-10-25: the proof slice, three assets through Blender

**The goal:** real, game-ready models instead of a scanned scene. AI makes the references and textures; Blender
scripts make the models, in the parts the game needs. The pipeline is in [tools/blender/](../../tools/blender/README.md),
and every file's origin is in [art/manifest.json](../../art/manifest.json).

| Step | Model | Job id | Credits | Verdict |
|---|---|---|---|---|
| Library sheet | Nano Banana Pro, styled from keyframe A | `cddc4cfb` | 2 | **Good.** Four consistent views: tall gable-fronted house, flared slate roof, dormer, porch, arched green door. |
| Tree sheet, first try | Nano Banana Pro, styled from keyframe A | `139bc9fd` | 2 | Discarded: it grew the whole courtyard around the tree. |
| Lamp sheet, first try | Nano Banana Pro, styled from keyframe A | `4fbd4070` | 2 | Discarded: it repainted the keyframe. |
| Tree sheet | Nano Banana Pro, styled from the Library sheet | `6f5e159b` | 2 | **Good.** Four turntable views of one oak-like tree, square holes in the bark. Faint ghost plinths at the foot (cropped). |
| Lamp sheet | Nano Banana Pro, styled from the Library sheet | `3fc9621f` | 2 | **Good.** Three side views and a top view. |
| Tree lift | Tripo H3.1 multi-view, four cropped views | `70ad8190` | 12 | Used as measurements only: trunk centreline and radius, root spread, leaf mass. Narrower crown than the sheet. |
| Leaf texture | Nano Banana Pro, styled from the tree sheet | `faea5eda` | 2 | Top half perfect; the bottom half grew trunks (cropped away). |
| Bark texture | Nano Banana Pro | `28142639` → `3b7f1253` | 0 + 2 | First job failed (refunded); the retry is clean and seamless. |

**Total for the slice:** 26 credits (62 → 36).

**The models** (`art/models/`, previews in `art/previews/`, all checked in the game's viewer):

| Asset | Script | Size | Parts the game can use |
|---|---|---|---|
| Library | `assets/library.py` | 13.8 m tall, 260 triangles | `body`, `roof`, `door`, `glass` (all windows), markers `lamp` and `door-point` |
| Lamp post | `assets/lamp_post.py` | 3.4 m, 740 triangles | `post`, `glass`, marker `light` |
| Old Stoic | `assets/old_stoic.py` | 14.4 m, 17,044 triangles | `trunk` (with roots, limbs and six nail holes at hand height), `canopy` (the part that sways). *Replaced the same day by the refined lift; see below.* |

**Lessons** (also in the pipeline README):

- **Style sheets from an isolated sheet, not a full scene.**
- **Painted sheets work for decals and single colours, not for tiling,** because their ink outlines repeat as zigzags. Generate tileable textures separately.
- **Image references leak their subject into texture prompts.** Crop the good part, or leave the reference out.
- **The multi-view lift is a good measuring tape and a poor model.** Reading numbers off it and building clean geometry gave a tree with separable parts, smooth bark and a canopy that can sway.
- **Art direction still overrides the measurements where they disagree with the bible.** The lean is set to 12°, and the crown widened by 35%.

**Not done yet:**

- **Cel shading.** Everything is shown with the viewer's default shader: no cel bands or ink until engine task E1.
- **Scale and placement.** The Library is 13.8 m tall against iteration 2's 8.3 m, and the courtyard layout hasn't been redone.
- **Curved limbs.** The tree's limbs are straight tubes; curving them is the obvious next polish.
- **Licence review (D7).** Not yet done for these tools.

## 2026-10-25: Old Stoic from the lift, and the models in the game (E1)

**Old Stoic.** The designer preferred the raw multi-view lift's look to the scripted rebuild: its stout furrowed
trunk, buttress roots and lumpy painted crown. `assets/old_stoic.py` now refines the lift itself:
- it turns the lift to face the game's front and scales it to 14 m;
- it splits the lift into `trunk` and `canopy` by painted colour (leaf where green > 0.9 × red, then three passes
  of neighbour voting);
- it saves the texture as a 2K PNG.

The result is 38,948 triangles: canopy 15,512 vertices, trunk 8,761. The scripted tree stays as
`assets/old_stoic_rebuilt.py`. The art bible records this as a designer-approved exception to "raw image-to-3D
output isn't imported as it comes".

**E1 in the game.** No credits spent.
- **Toon shader.** It samples the painted texture (white for the built-in meshes), so the cel bands, lamp light,
  rim and fog all apply to imported models.
- **Import step** (`src/Atrium.Diorama/Render/ModelAsset.cs`) fills in what raylib drops or gets wrong:
  - It finds each mesh's part by node name, matched on material and vertex count.
  - It reads the markers.
  - It converts linear base colours back to sRGB (the lamp's iron and the Library's timber were near black).
  - It puts welded smooth normals in the tangent channel for the ink.
  - It builds mipmaps.
  - It refuses parts over 65,535 vertices before raylib loads them.
- **Renderer.** It draws model by model *part*, so a house's glass glows without ink, and the canopy sways while
  the trunk stays still.
- **Caching.** The renderer keeps imported models for the session, so going in and out of rooms doesn't reload
  them.
- **The courtyard** now uses all three models, listed with their scales in the diorama's `visuals.json`:

  | Piece | Scale (courtyard units per metre) | Notes |
  |---|---|---|
  | Old Stoic | 0.85 | About 12 units tall; it now clearly outranks the houses. The crown overhangs the big pool. Clicking it uses the real mesh, not a box. |
  | Library | 0.62 | About 8.5 units, the same footprint as iteration 2's. Its painted windows light warm at night, and the house lamp sits on the file's `lamp` marker. |
  | Lamp posts | 0.8 | Each turned to the tree. The relit charter lamps glow from the `light` marker. |

- **Fallback.** With a model's file missing or its entry removed, the courtyard draws the built-in shape (checked).
- **Viewer.** `--view` now shows a model as the game draws it; Tab shows the file as it is.

**Checked:** hub captures at noon, evening and night, a Library hover, a tree click, a zoomed view and an orbit
behind the Library; all four rooms still draw as before; 35 of 35 core tests pass.

**Not done yet:**

- **The other three houses** (Apothecary, Spirit Pools, Sanctum) still use iteration 2's shapes. Each needs a
  sheet and a script. The Spirit Pools lodge also needs `shutters`.
- **The built-in courtyard** (slab, cliff, city) has no texture yet: triplanar mapping is the rest of E1.
- **Old Stoic in warm light.** At dawn and evening its olive crown reads brownish under the warm sun. The colour
  comes from the lift's own texture; whether to push it greener is the designer's call.
- **Git LFS for `art/`** (about 56 MB) before anything is committed.

## 2026-10-26: the other three houses

**Sheets** (Higgsfield, Nano Banana Pro 2K 16:9, each styled from the Library sheet for style and layout only, with
the brief from `iteration-3-prompts.json` and the bible's silhouette rules):

| Sheet | Job id | Credits | Verdict |
|---|---|---|---|
| Apothecary | `ec3919c1` | 2 | **Good.** Cream plaster, violet tiled roof, bowed bay window full of jars, violet door, blank signboard, a cracked patch, a tall brick chimney. The side views disagree about where the chimney stands, and it's drawn taller than "wide and low". |
| Spirit Pools lodge | `c58861ee` | 2 | **Good, wrong layout.** Teal thatch, porch on posts, shuttered windows, rain barrel, bench. It came back as a 2 × 2 grid with no right side, and the second front view has one pair of shutters closed: used as the closed-shutter reference. |
| Sanctum | `dea443fd` | 2 | **Good.** Sandstone round tower, crazed ochre dome, spire and ball, arched strapped door between two round windows, a round window at the back. |

All three grew a blurred strip of town along the bottom (ignored). **Total:** 6 credits (36 → 30).

**Models.** Three scripts, written in parallel by one agent per house from the Library's template. Each one was
checked against its sheet in renders and in the game's own viewer.

| House | Script | Size (m) | Triangles | Parts and markers | Judgement calls |
|---|---|---|---|---|---|
| Apothecary | `assets/apothecary.py` | 8.5 × 9.5 × 6.2 | 1,188 | `body`, `roof`, `glass`; `lamp` (an iron wall lantern), `door-point`, `chimney` | Kept wide and low (7.0 × 4.8 m, eaves 4.4 m) as the bible asks, not as tall as the sheet. The chimney is an external stack outside the right gable, toward the back. The bay window has five facets. |
| Spirit Pools lodge | `assets/pools.py` | 9.7 × 5.4 × 7.8 | 2,502 | `body`, `roof` (thatch), `glass`, `shutters`, `shutters-open`; `lamp`, `door-point` | Walls 8.2 × 4.6 m, so a 1.95 m door fits under the porch. The right side mirrors the left. The thatch is one lofted shell with a raised "eyebrow" over the door. |
| Sanctum | `assets/sanctum.py` | 5.2 × 10.9 × 5.6 | 2,523 | `body`, `glass`; `lamp` (a small lantern on the door surround), `door-point` | The sheet's proportions over the brief's numbers (4.55 m across, door 3.2 m, like the Library's). Lathed from measured profiles, with cylindrical UVs on the wall. |

**A kit bug, found by two agents independently:** `kit.fit_uv` put a decal's top at the bottom, so every
`kit.plaque` decal was upside down. That included the Library's door (its step was at the top) and its windows.
Fixed in the kit; the agents' local workarounds were removed; the Library and the lamp post were rebuilt.

**In the courtyard** (all listed in `hub.models`):
- The Apothecary and the Sanctum stand at 0.62, like the Library.
- The lodge stands at 0.5. Even at 0.5 it overlapped both basins at the built-in lodge's site. A search over
  sites, turns and scales found one fit: between the small basin and the Apothecary, turned 9° from facing the
  tree. That's where it stands now.
- The courtyard's paving paths now run from the ring to each house's `door-point`, so they follow the house.
- The lodge's closed shutters show while it's locked, and its open shutters once it's unlocked.
- The Apothecary's steam rises from the `chimney` marker.

**Checked:** fresh and midgame courtyard captures by day and evening, turned views, hovers, and entering the
lodge; 35 of 35 core tests pass.

**Not done yet:**

- **The houses' backs face the default camera** on the front side of the courtyard (the Apothecary and the
  Sanctum), as before: every house faces the tree.
- **Each agent flagged small things:**
  - the Apothecary has faint mirror seams in its roof and plaster swatches;
  - the Sanctum's round windows all use the back window's cross mullion;
  - the Sanctum's door step is tall;
  - the lodge's thatch is smoother than the sheet's brushwork.
- **Scale differs by house.** Doors range from about 1 courtyard unit (the lodge) to 2 (the Sanctum).

## 2026-10-26: the rooms, built in Blender

**Credits first.** The designer asked to spend fewer Higgsfield credits. Price checks (free, nothing generated)
found GPT Image 2.5 at low quality and 1K costs 0.25 credits, against 2 for every Nano Banana Pro sheet so far.
There is no free or unlimited allowance on the account. So each room got **one** prop sheet at that setting,
holding all its furniture plus floor and wall patches, and everything else was built in Blender. That is decision
D2: textured 3D rooms rather than painted 2D.

| Sheet | Model | Job id | Credits | Verdict |
|---|---|---|---|---|
| Apothecary room (trial) | GPT Image 2.5, low, 1K, styled from the Apothecary sheet | `e144aaa2` | 0.25 | **Good.** Every object separate and clean, in the house style with an ink line: shelving with drawers, bench, lectern, copper vessel, tray, bottle crate, blank frame, herbs, lantern, brick hearth, floorboards, plaster. 1344 × 752. |
| Library room | same, styled from the Library sheet | `ada507ad` | 0.25 | **Good.** Bookcase, shelf plaque on hooks, reading table, returns box, ladder, globe, biscuit tin, pencil cup, wall lamp, rug, arched window, floorboards, panelling. |
| Spirit Pools room | same, styled from the lodge sheet | `e1ac6fd3` | 0.25 | **Good.** Crock, canvas bag, ledger stand, stool, hand rake, lantern, ferns, ivy, arched window, stone ledge, flagstones, mossy wall. |
| Sanctum room | same, styled from the Sanctum sheet | `3bc225a4` | 0.25 | **Good.** Desk, journal, sealed blank charter, magnifier table, assembly bench, cabinet, reliquary on a plinth, balance stand, beam and pan, candle ring, round window, slate, cedar panelling. |

**Total:** 1 credit (30 → 29).

**Engine.** A room now loads its kit, `models/<locale>-room.glb`, when `visuals.json` lists it (`rooms.models`).
Each of the room's fixed models is made with `Prop("<key>", BuiltIn)`: a kit part with that name replaces the
built-in shape, drawn the same way, and anything the kit leaves out stays built-in. The Library stretches a kit
bookcase (made for 5 shelves) to the content's shelf count.

**Kits.** One agent per room, in parallel. All four hit the session limit after a first full pass; I finished the
checks myself.

| Room | Script | Triangles | Parts supplied |
|---|---|---|---|
| Library | `assets/library_room.py` | 6,957 | `shell`, `case`, `plaque`, `table`, `decor`, `returns`, `lamp-glass` |
| Apothecary | `assets/apothecary_room.py` | 7,784 | `shell`, `furniture`, `herbs`, `lantern`, `vessel`, `map-board`, `book` |
| Spirit Pools | `assets/pools_room.py` | 13,670 | `shell`, `decor`, `ferns`, `crock`, `bag`, `ledger`, `stool`, `rake` |
| Sanctum | `assets/sanctum_room.py` | 11,468 | `shell`, `decor`, `oculus-glass`, `desk`, `journal`, `board`, `glass-table`, `lens-ring`, `lens-glass`, `bench`, `sheet`, `cabinet`, `plinth`, `relic-door`, `stand`, `beam`, `pan`, `circle-candles` |

Captured in the game from a midgame save. The game's own items land where they should: jars on the Apothecary
shelves, the Discovery Map on its board, objects on the bench, books on the reading table, tinted shelf plaques,
water in the crock, the balance on the reliquary. Hover rims and lifts work on the kit parts. 35 of 35 core tests
pass.

**Fixes along the way:**
- **Kit.** `export_vertex_count` failed on a mesh with UV layers but none active.
- **The Sanctum's chalk circle** (the built-in shape, not the kit) joined its six points into a six-pointed star,
  which reads as a religious symbol. It now joins them as a plain hexagon, "so neat it reads as floor pattern"
  as the bible asks.

**Not done yet:**

- **The Spirit Pools tanks** are still the built-in pale boxes, because their size depends on each pool's
  capacity. A kit tank would need stretching like the bookcase.
- **The agents' second passes didn't happen.** Each kit had one full pass and passed my checks. The Library
  agent had planned a calmer stone wall, a larger window and moving the globe; the Pools and Apothecary agents
  were checking details close up.
- **The chalk circle stays built-in** (tinted and glowing by the game).
- **Small props stay built-in** in every room: jars, tools, books, tokens, weights and the like. The game tints
  them, or makes them per item.

## 2026-10-26: the Spirit Pools as a hidden grotto

**Designer direction.** The tank room "is a retail pet store". The pools should be a natural, almost cave-like,
almost hidden place: a stop between the waterfall and an underground river that carries the water out of the
city, with glimpses of Old Stoic's roots. The courtyard's water has to change with it.

**Concepts first.** Two GPT Image 2.5 paintings (low, 1K, 0.25 credits each; 28.5 credits left), each from a
capture of the real game for layout. The designer approved both, and asked for the cave mouth to be the entry
(gated, its lamp dark while locked) and for the lodge to go.
- **The grotto** (`7c4c00af`): a cascade through a crack, the Sunward Basin on a terrace spilling into the Shade
  Pool among the roots, a still pond ringed with flat stones, and the river slipping away under an arch.
- **The courtyard** (`d8ce380a`): the falls' plunge pool, a winding stream under a footbridge, and a cave mouth
  at the cliff's end.

**Built without agents or new images**, to save tokens and credits:
- **Grotto room.**
  - **Pools** (`PoolsScene`): the tanks became oval rock pools with mossy rims. The Sunward Basin is raised on a
    rock terrace. Fish and snails are seen from above, through the water.
  - **Water:** a cascade falls from high in the back wall, the upper pool spills into the Shade Pool, and an
    underground river runs along the front.
  - **Light:** comes down through the crack.
  - **Gameplay is unchanged:** the same pools, clicks, cards, feeding and breeding.
  - **The cave kit** (`assets/pools_grotto.py`, called from `pools_room.py`): natural rock walls with the crack,
    the river tunnel and a lit doorway at the top of worn steps; Old Stoic's roots in its own bark texture; vines,
    pale glowing mushrooms, moss and ferns. Rock and moss are painted procedurally. The crock, bag, ledger, stool
    and rake are the earlier kit's, unchanged.
- **Courtyard.**
  - **Water:** the two small basins and the courtyard fish are gone. The falls' plunge pool now feeds a winding
    stream with rocky banks, under an arched stone footbridge where the cave path crosses it, into the cave mouth.
  - **The cave mouth** (`assets/pools.py`; the lodge's script is kept as `pools_lodge.py`, unused): a mossy rock
    outcrop set into the cliff's end with steps down beside the water. Its gate is the `shutters` part, closed
    while locked, and the `shutters-open` part, swung back once open. Its lamp post's glass lights only once the
    pools are open.
  - **Paving paths** now skip the water.
- **Content.** The three locale lines that mentioned the lodge now describe the grotto, the gate and the lamp.

**Checked:** captures locked and unlocked, the grotto from a midgame save, and 35 of 35 core tests pass.

**Not done yet:** the steps' moss reads as stripes, and the moss patches are low-poly octagons. The courtyard
stream is a ribbon with rocky banks, not a gorge cut into the stone.
