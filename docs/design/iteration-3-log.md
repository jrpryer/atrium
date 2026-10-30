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

**Follow-up (2026-10-29):**
- **The gorge.** The courtyard stream now runs in a gorge cut into the ground. A new ground grid
  (`HubWorld.BuildGround`) drops 0.6 m along the stream's course, with stone walls and a dark bed. The water
  runs down in it, the bank rocks sit on its lip, and the footbridge spans it. The slab's cap moved below the
  gorge, with a capless band carrying its side up to the ground.
- **The grotto's moss.** The steps lost their moss stripes, and the moss patches are rounder (18 segments).
- **Credits.** The designer now has a Higgsfield subscription with plenty of credits, so art can iterate for
  quality.

## 2026-10-29: the courtyard's cliff, ground and city backdrop

The designer now has a Higgsfield subscription, so these are Nano Banana Pro, for quality: 12 credits.

| Image | Model | Job id | Credits | Verdict |
|---|---|---|---|---|
| Cliff rock | Nano Banana Pro, 2K, styled from the Library sheet | `419a6a72` | 2 | **Good.** Warm sandstone strata with ink edges; tiles cleanly. |
| Grass | same | `5707e36c` | 2 | **Good.** Soft turf, clover, a few daisies. |
| City backdrop | Nano Banana Pro, 4K 21:9, from keyframe A | `781728d2` | 4 | **Very good.** Tier on tier of timber-framed houses, stairs, arched bridges, washing lines and chimney smoke, with a waterfall from the top tier, a mossy cliff edge along the bottom, and a clean flat green background. |
| City at night | Nano Banana Pro edit of the backdrop | `42715b94` | 4 | **Good.** The same painting at night with warm lit windows; used only for its lights. |

**Processing** (`tools/blender/courtyard_textures.py`, headless Blender, no new tools):
- The textures are resized to 1024.
- The backdrop's green is keyed out to transparency, with the green spill taken off the edges, so the game's own
  sky shows through.
- The backdrop is cropped to the window the cliff can hide, with its waterfall kept over the real falls, and both
  ends are feathered into haze.
- The night repaint gives a lit-window mask on black.

**Engine:**
- **Projection.** `MeshBuilder.ProjectUv` maps the cliff onto a cylinder, so its strata run round the arc, and the
  ground onto its plane.
- **Textures.** The renderer keeps the painted textures for the session (mipmapped, trilinear).
- **Ground.** It splits into grass (turf) and the gorge (rock).
- **City.** The backdrop is a card on the arc, lit by the cel shader. Its night lights are added through a small
  unlit shader, scaled by how dark it is. When the files are missing, iteration 2's flat shapes come back.

**Checked:** captures by day and night, close and wide; 35 of 35 core tests pass.

**Follow-up, the cliff sculpted (same day):**
- **Mesh.** The cliff is now 140 × 40 instead of 56 × 9. It leans back as it rises, swells into buttresses
  along the arc, and ripples with 1.25 m strata that echo the painted ones.
- **Shading.** The wall is smooth-shaded (`MeshBuilder.SmoothNormals`), so the cel bands follow its forms instead
  of drawing triangles. Ledges that face up take a moss tint.
- **Ends.** Both ends sink smoothly into the slab, so there are no tall end caps.
- **Mapping.** `ProjectUv` now chooses each triangle's projection from its true face, and gives faces that turn
  across the arc a flat projection, so nothing streaks.
- **Checked:** captures by day and from the side; 35 of 35 core tests pass.

**Not done yet:** from far round the orbit the backdrop card is seen edge-on, and its painted waterfall only lines
up with the real falls from near the courtyard's centre.

## 2026-10-29: the courtyard rebuilt as a corner of a stone city (blockout)

The designer turned down the painted backdrop and the sandstone cliff: the city should be modelled in 3D and
carved from stone, with the water breaking out of it. Both are gone (`BuildCliff`, `BuildBackdrop`, the old
`BuildCity`, `art/textures/city-backdrop*.png`). The raw paintings stay in `art/raw/courtyard` as provenance. This
is step 1 of 3: a code blockout. Next is a concept paint-over of a capture, then a Blender city kit.

**New art (Nano Banana Pro, 2K, 4 credits):**
- `paving.png`: worn flagstones and cobbles with moss in the joints.
- `masonry.png`: retaining-wall masonry with damp streaks.

`tools/blender/courtyard_textures.py` makes all four tileable textures at 1024 and no longer handles a backdrop.

**Blockout (`HubWorld`):**
- **Block.** A square stone block (±12.5) on a dark plinth replaces the round floating slab.
- **Ground.** Paving everywhere, with grass in noise patches and round Old Stoic, and the stream's gorge as before.
- **Terraces.** Retaining walls step up in three tiers at the back (y 5, 9.5, 14) and two on each side. They use
  masonry and have stairs up, dark doorways at the square's level, and rows of houses facing in. Each house has a
  stone ground floor, a jettied plastered storey or two, a gable or gable-front roof, and a chimney. Its windows
  light at night through the existing window mechanism. The side rows stop short of the front, so the city steps
  down toward the camera.
- **Falls.** A stone cistern stands on the back terrace. The falls pour from its broken culvert in the tier-1
  wall, past fallen blocks, into the plunge pool.
- **Camera (`HubScene`).** It stays in the front arc (yaw ±0.95 rad). The idle drift turns back at the ends, and
  the title view sways across the arc.

**Checked:** captures by day, by night, from both ends of the arc and close up; 35 of 35 core tests pass.

**Not done yet:**
- The grass patches are blocky (one per ground cell).
- The houses are uniform boxes, and there are no arches, bridges, balconies, washing lines or ivy yet.
- The cave mouth still stands in front of the east wall rather than being an opening in it.

These are for the paint-over and the Blender kit.

## 2026-10-30: the courtyard as a corner of a walled, tiered city (blockout, round 2)

**Designer notes on round 1.**
- "Much more medieval, think like Minas Tirith."
- The far right, against the grotto, should be a full city wall.
- The left should be the raised section, curving along the city wall too, on our tier.
- At the back, a few more rows of houses on our level, then up a whole tier, fading into the background.
- The city should not focus in on this corner: its lights shouldn't all point inward.
- A see-through camera shader is planned, so some occlusion is acceptable.

**Process.**
- Three design passes (fortification, urban fabric, camera composition) were merged into one layout.
- After the first build, three critique passes (notes, realism, composition) reviewed captures. Their high and medium findings were fixed.

**Layout (`Scenes/HubCity.cs`, which replaces `BuildCity`):**
- **The pocket.** The courtyard is a sunken pocket in the corner of the lowest tier. Tier 1's streets run 4 m above it on the west and north. Its north-west corner is rounded. A corbelled gallery carries the rim along the city wall, over the water gate, to a shelf on the east, with stairs down along the walls.
- **The tiers.** Tier walls and streets are arcs around a citadel off the map to the north-north-west, so they curve across the view rather than around the courtyard. Tier 2's wall (20 m) and tier 3's (34 m) rise behind three rows of tier-1 houses.
- **The city wall.** It is on the east, with an arcade of relieving arches carrying the wall-walk, battlements and torches. It has a slate-roofed water tower behind the cave mouth, with the water gate and a half-raised grille at its foot. Also the Low Gate with its towers and gatehouse, a stair from the shelf, and a rock scarp. South of the water tower it bends out along the cliff, so the default camera never stands outside it. The wall ends square on the front cut.
- **Buildings.** Every building fronts its own street: houses, shops, inns, stables, tower-houses. The courtyard sees backs, yards, window boxes and gable ends. There is an inn and a stable by the Low Gate, a bridge-room over the alley from the north stair, market stalls along W2, washing lines over the back lane, and the old well out in W1.
- **The falls.** The old baths stand on the north rim, with blind arches over the courtyard. The falls burst from the cracked, bricked-up overflow arch of their cistern. A two-stage arcade aqueduct feeds the cistern from tier 2.

**Look:**
- The city's stone is pale and cool (Minas Tirith), and its walls use larger ashlar than its houses. The courtyard keeps its warm stone, so it stands apart.
- Roofs are slate, with a few tile and some flat leaded roofs behind parapets.
- By day the windows are dark glass; at dusk they warm to lamplight, each a little different and dimmer than the courtyard's own lamps. More of the lit windows are on street fronts than on backs.
- Rows past the main street and the upper tiers are drawn in a shorter, cooler haze (`Shaders.FogRange`).

**Camera (`HubScene`):**
- Yaw is limited to -0.95 to 0.62. The east end is the default view, short of looking in from outside the wall.
- Zooming out now looks flatter and higher (pitch -0.2, target +6 up and 3 back at full zoom), so the tiers rise into view. The default pitch is unchanged.

**Removed:**
- The cliff, the painted backdrop and the round-1 city.
- The east-end tower (it filled the foreground) and the lean-to livery on the east shelf (it faced into the courtyard).
- Two blob trees (they read as toys; untended greenery waits for the Blender kit).

**Checked:** captures by day and night, at default, both ends of the arc, and zoomed out; 35 of 35 core tests pass.

**Not done yet / open:**
- The default pitch is still 0.52. The critics suggest about 0.47 so the skyline clears the frame; that is a designer call on the locked framing.
- There is no "world beyond" outside the wall yet.
- Night light doesn't pool on the streets: the shader's lamps are capped at 8.
- Building backs are all alike (inn wings, hayloft hoists and outside stairs are for the kit).
- The cave mouth still stands in front of the wall rather than in it.
- Next: the concept paint-over of a capture, then the Blender city kit.

**Follow-up, the concept paint-over (same day):**
- **Generated.** Nano Banana Pro, 2K 16:9, 2 credits, preflighted, from the zoomed-out blockout capture with keyframe A for style. Saved as `art/reference/concepts/courtyard-city-paintover.png`, beside its source, `courtyard-city-blockout.png`.
- **Layout.** It keeps the layout. Every wall, tower, row, stair and street is where the blockout put it, so the paint-over can serve as the Blender kit's target directly.
- **Detail for the kit:**
  - **Houses:** slate roofs with dormers; window boxes and lanterns on brackets; barrels and planters at doors; washing between gables.
  - **Walls:** damp streaks and trailing ivy down the walls; the tier wall's buttresses with windows between them and moss below its band.
  - **Towers:** a round tower with tall crenellations on the city wall.
- **Drift to correct in the kit:**
  - The Apothecary's purple roof came back slate.
  - The Sanctum's dome lost its banding.
  - The cave mouth is hidden behind boulders.
  - The courtyard is a little less overgrown than asked.

**Follow-up, a fuller concept round (same day):**
- **Direction.** The designer asked to use Higgsfield more if it gives better art. The city should feel alive, lived in, busy and dense: tidy, almost marble-white. Our corner has fallen by the wayside: exposed stone-brick paths, grass, roots, moss, trickling water, crumbling limestone.
- **The round.** 12 images at 4K, 48 credits, preflighted. Stored as JPG in `art/reference/concepts/city-round/`, 31 MB.
  - Four zoomed-out day variants: `wide-a` is the layout target; `wide-c` is the mood target, with the strongest contrast between the white city and the green corner, and a glimpse of land beyond the wall, though its layout drifts.
  - Two close-ups of the corner; `corner-a` is the texture and prop target: bare brick under the baths' failing render, cracks, rivulets, puddles, roots across the flags.
  - Two night views; `night-a` is the lighting target: braziers on the towers, warm street fronts, a dim corner.
  - Two corner kit sheets, both usable as the Blender brief.
  - One white-city kit sheet. Its second variant was discarded: smeared fog, painted text, and a banner too close to a film emblem.

## 2026-10-30: the Blender city kit, step 1 (the white city's buildings)

**Targets (designer's picks).** Concept `wide-a` for layout, `wide-c` for mood, `corner-a` for the forgotten corner, and `night-a` for lighting.

**The kit (`tools/blender/assets/city_kit.py` → `art/models/city-kit.glb`).** Nine pieces, from the city kit sheet:
- five townhouses: two, three and four storeys; one gable-front; one with a jettied plaster storey and a timber frame;
- a shop with an awning, shutters and a hanging sign;
- an inn with three dormers, a sign, a lantern and barrels;
- a stable with a hayloft, a hoist beam and a hay rack;
- a tower-house with a parapet.

Each has a plinth, string courses, a cornice and a steep slate roof with dormers and chimneys with pots. Windows have sills, lintels, shutters and window boxes. There are doors with steps and back windows.

**In the game.** `HubCity.Kit` reads each piece's meshes by role, plus its origin and lantern markers. `MeshBuilder.AppendMesh` bakes a copy into the city's own layers for every lot the row generator places, scaled to the lot and turned to its street. That way the buildings keep the near and far haze, the masonry projection and the lights. Each building gets:
- its own slate (or rare tile) roof and awning colour;
- a lit or dark state for each of its three window sets;
- a lantern glow if the piece has one.

Without the file, the code boxes come back.

**Two stones.** The city now uses new clean textures (Nano Banana Pro, 2K, 4 credits, preflighted): `city-ashlar` (white limestone) for houses, walls and towers, and `city-paving` for tier 1's streets. The courtyard's weathered masonry stays only on the forgotten corner: the pocket's rims, stairs and gallery, and the old baths.

**Checked:** captures by day and night at the default view, zoomed out, and at the west end of the arc; 35 of 35 core tests pass.

**Next kit steps:**
- the corner kit (crumbling walls, root-heaved flags, the cracked brick arch, rivulets, puddles, moss, ivy curtains), from `corner-kit-a`/`-b` and `corner-a`;
- the city wall and towers, with real arches;
- busier streets (stalls, carts, banners, laundry);
- the cave mouth set into the wall.

## 2026-10-30: the Blender city kit, step 2 (the forgotten corner)

**Textures (Nano Banana Pro, 2K, 4 credits).**
- `corner-wall`: crumbling limestone with brick showing through, damp streaks and moss in the joints. It is now on the pocket's rims, stairs and gallery, and on the old baths, which take it at a larger scale so the brick patches don't repeat in a grid.
- `corner-paving`: an overgrown old brick path with moss, grass and small flowers in the gaps. It is now the courtyard's floor.

**The kit (`tools/blender/assets/corner_kit.py` → `art/models/corner-kit.glb`, 16 pieces, from `corner-kit-a`/`-b`).**
- two crumbling wall sections: worn blocks in relief, some fallen out to the brick behind, a crack, broken and askew coping, moss on the ledges and at the foot;
- the cracked brick arch: voussoirs, the keystone gone, brick infill with a hole where the falls burst out, fallen bricks;
- two patches of flagstones heaved by roots, with the roots splaying out from the trunk;
- rubble and fallen coping;
- two ivy curtains;
- moss, a fern and a grass tuft;
- two puddles;
- a rivulet with its splash.

**In the game (`Scenes/HubCorner.cs`, `Render/KitAsset.cs`).**
- **Generic kit loading.** `KitAsset` reads any kit's pieces by role (`ModelAsset.PartNames`), and `MeshBuilder.AppendMesh` can now project UVs, for running water.
- **Placement.** `HubCorner` faces the pocket's walls with crumbling sections, fitted run by run, and sets the arch in the baths' back wall, where its hole meets the falls. It lays root-heaved flags over the paving ring at three angles round Old Stoic.
- **Dressing.** It adds rubble and fallen coping at the wall feet; nine ivy curtains on the rims, the gallery, the baths and the foot of the city wall; five rivulets, drawn as falling water; six puddles, drawn as water; and moss, ferns and grass along the wall feet, on the rim tops, round the trunk and round the pool.
- **Clearance.** It keeps off the houses, stream, pool, trunk, lamps, benches and stairs.
- **Fallback.** The code's box ivy and bricked arch stand down when the kit is there.

**Checked:** captures by day and night, at the default view, at the west end of the arc and zoomed out; 35 of 35 core tests pass.

**Not done yet:**
- The puddles read as pale patches rather than reflections.
- The NW corner's curve has no crumbling facade (the wall pieces are flat).
- The water gate and the cave mouth are still the code's.
- Next: the city wall and towers with real arches.

## 2026-10-30: the Blender city kit, step 3 (the city wall, its towers and gates)

**The kit (`tools/blender/assets/wall_kit.py` → `art/models/wall-kit.glb`, from `city-kit-a`, `wide-a`/`-c` and `night-a`).**
- **Bays:** an arched bay, with a relieving arch on thick voussoirs and imposts, its head in shade. A plain bay, whose band rides a corbel table. Both share one cornice and inner railing, so the top line runs unbroken. Both carry a parapet on corbels, with machicolations and merlons with arrow slits and capstones.
- **Towers:** a machicolated, crenellated crown with a brazier raised on a stand so its flame clears the merlons; a slate cone with a flared eave and a finial; string-course bands with arrow slits.
- **The Low Gate's gatehouse:** a round-headed passage, voussoirs, a raised portcullis, a guardroom window, banners and lanterns.
- **The water gate:** a broad arch with a hood, its portcullis rusted up near the crown.

**In the game (`HubCity.CityWall`).**
- **Bays.** Bays are placed along each run, scaled evenly so their arches stay round. They are arched where piers can stand and plain at the gates, behind the courtyard's gallery and where the shelf's stair climbs.
- **Recesses.** Arched bays get a shaded back wall between their code-built piers.
- **Towers.** Towers keep their code-built ashlar shafts and get the kit's bands and crowns or cones. The water tower moved 0.4 m out so its battered base stays clear of the gate.
- **Doorways.** Doors were added where the wall-walks meet the water tower, the Low Gate's towers and the gatehouse.
- **Front cut.** The southern run's relief stops short of the front cut, and its parapet carries on over the end wedge.
- **Colours.** All three kits now write their colours as sRGB, converted to Blender's linear values. Before, iron, brick, moss, roots and slate all came out too light.

**The cave mouth, set into the wall.**
- The water gate is cut through the foot of the wall at z = -7.4, and the wall's body is set back 0.5 m behind its frame, so the opening has jambs and a soffit.
- The Spirit Pools' cave mouth (`pools.glb`, now at scale 0.75) sits inside it, facing the courtyard, its back buried in the wall.
- The stream is re-routed straight from the pool into it; its gorge, banks and footbridge follow.
- The courtyard's gallery crosses in front of the gate below the springing, on an open iron railing with a corbel stack at the jamb, so the arch and portcullis read above it.

**Process.** Two critique passes (fortification fidelity; integration and artifacts) reviewed the first build. Their high and medium findings were fixed:
- the gate had been painted onto a solid wall;
- the gallery's parapet hid the arch;
- the tower's base bulged into the opening;
- the stair crossed two arches;
- plain bays were blank and the cornice jogged;
- the arches were shallow, the walks ran blind into towers, the braziers were hidden, and the run's end poked past the cut.

**Checked:** captures by day and night, at the default view, at the west end of the arc and zoomed out; 35 of 35 core tests pass.

**Not done yet:** the critique's low items (coplanar seams on the walk; the stair is still separate boxes) and a world beyond the wall.

**Follow-up, the falls and the pool (same day).** The designer felt both broke the art style and believability.
- **What was wrong:**
  - The falls were a flat, straight card wider than the hole they came from, and the shader drew hard-edged columns that read as a pixelated curtain.
  - The pool was a perfect ellipse with a clean cream rim, a formal fountain in a forgotten corner.
  - One of Old Stoic's lamp posts stood in the water, and a bench sat inside the rim.
- **The falls:**
  - The water is now a jet that arcs out of the brick arch's hole at 3.95 m and drops forward into the pool, landing 1.8 m out at `FallsBase` (now z -7.7).
  - It is a curved sheet with a wider veil round it, narrow at the hole and spreading as it falls.
  - A ring of foam churns outward where it lands, and the mist and spray rise from that spot.
  - The falls shader's streaks are now soft value noise that varies across the sheet, whitening toward the foot (the corner's rivulets share it).
- **The pool:**
  - The rim is an old basin of 26 weathered stones in the corner's crumbling texture, each a little askew; two are gone, one has tumbled into the water and one lies on the grass.
  - A gap on the east lets the pool spill into the stream.
- **Placement:** the fourth lamp post stands by the pool at (4.9, -3.4); the bench sits outside the rim, facing the water.

