# Blender asset pipeline (iteration 3)

Atrium's models are built **as scripts** in Blender, from AI-generated reference sheets. Image models are good
at what a thing should look like; they're bad at clean, game-ready geometry. So the work is split:

| Step | Tool | Output |
|---|---|---|
| 1. A turnaround sheet: front, side and back views of one asset, on grey, in the chosen art style | Higgsfield (Nano Banana Pro), styled from an approved keyframe or sheet | `art/reference/sheets/<asset>-sheet.png` |
| 2. Organic assets only: a rough 3D lift from the sheet's views, used as **measurements** (Old Stoic ships its lift, refined; see below) | Higgsfield (Tripo H3.1 multi-view) | `art/raw/<asset>/` |
| 3. Tileable textures for surfaces that repeat (bark, leaves, stone) | Higgsfield | `art/reference/textures/` |
| 4. The model, built in parts by a script | Blender 5.2, run headless | `art/models/<asset>.glb` + `art/previews/` |
| 5. A check in Godot, which imports the model in place, then a place in a scene | the asset viewer and the headless check (`godot/Viewer`, `godot/Check`; see [In Godot](#in-godot)) | — |

Every generation is recorded (tool, model, job id, credits, verdict) in
[docs/design/iteration-3-log.md](../../docs/design/iteration-3-log.md).

## Running it

Blender 5.2 is used headless; nothing is installed into it. From the repository root (Windows, Steam's Blender):

```bash
"/c/Program Files (x86)/Steam/steamapps/common/Blender/blender.exe" -b --factory-startup --python tools/blender/build.py -- library
```

Swap `library` for `lamp_post` or `old_stoic`. Each run writes the model to `art/models/` and front, right, back and
three-quarter previews to `art/previews/`, and prints an `ASSET` line with each part's vertex and triangle counts.

To see a rebuilt model in Godot, reimport and run the check, then open the viewer (from the repository root,
after `dotnet build Atrium.slnx`; the viewer's keys are in the [README](../../README.md#the-godot-project)):

```bash
G="/c/Program Files/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"
"$G" --headless --path . --import
"$G" --headless --path . --scene res://godot/Check/Check.tscn; echo $?
"$G" --path .
```

The viewer shows the file as imported, under plain lights: the cel bands and ink aren't in Godot yet. The raylib
diorama's `--view` (`dotnet run --project src/Atrium.Diorama -- --view art/models/library.glb`) drew them, and is
on the `standalone-diorama` branch.

## In Godot

On the `godot` branch the repository root is a Godot 4.7 .NET project, and Godot imports `art/models/*.glb` in
place: there's no copy step. Each model's import settings live in its `.import` file beside it, which is kept in
git. What the import showed:

- **Parts and markers arrive by name.** Every part comes in as a `MeshInstance3D` and every marker (empty) as a
  plain `Node3D` with no children, each under its glTF name. The check compares every model node for node with
  its glTF and finds the same nodes. Godot names the scene's root `<stem>2` and puts the kit's own root empty,
  `<stem>`, under it.
- **Godot's name suffixes are turned off** (`nodes/use_name_suffixes=false` on every model). With them on, Godot reads
  suffixes on node names (after `-`, `_` or `$`, in any case): `-noimp` drops the node, and `-col`, `-convcol`,
  `-colonly`, `-convcolonly`, `-occ`, `-occonly`, `-navmesh`, `-rigid`, `-vehicle` and `-wheel` turn it into a
  collider, occluder, navmesh or physics body. `-loop` and `-cycle` mark animations, and `-alpha` and `-vcol` act
  on material names. Kit parts are `<piece>_<role>`, so a role named `col`, `occ` or `wheel` would have quietly
  become one. No current name ends in a suffix, and the check
  scans for them anyway; still, keep those words out of part, role, marker and material names.
- **Colours arrive as authored.** Every material shows `linear_to_srgb(baseColorFactor)` within 1.5/255, so the
  sRGB swatch colours in the scripts are the colours Godot shows (city-stone, linear 0.848, shows as 0.93).
- **Emission is linear too.** glTF's `emissiveFactor` is linear, so the kits pass `emissive=kit.srgb_to_linear(col)`
  like their base colours. The city and wall kits' lamps once passed the sRGB colour and showed paler in Godot
  ((1.000, 0.916, 0.722) instead of (1.000, 0.820, 0.480)); that was fixed on 2026-11-01 and both kits rebuilt.
  The pools room's daylight emission was already linear.
- **Textures stay in the `.glb`.** They import as Basis Universal with mipmaps (`gltf/embedded_image_handling=2`),
  except in `apothecary-room.glb`. Godot 4.7.2 can't decode mip level 4 of its two thin frame swatches,
  `apothecary-room-frame-up` (24×270) and `apothecary-room-frame-along` (270×24), so that model's textures are
  extracted instead (`=1`) into `art/models/apothecary-room_*.png`, which are git-ignored and remade on every
  import. Changing those two swatches' sizes in `assets/apothecary_room.py` and rebuilding is the other route;
  the model can then go back to `2` (its `.import` file and the `ImageHandling` dictionary in
  `godot/Check/Check.cs`).
- **Almost everything is double-sided.** Every material except Old Stoic's is exported double-sided (Blender's
  backface culling is off on them), so Godot draws them with culling disabled. That contradicts the convention
  below that the game culls back faces. Whether the kits should export with backface culling on, or the game's
  cel/ink material should set the cull mode, is undecided.
- **Scale and facing hold.** Metres, at root scale 1.0: the lamp post is 3.36 m, the library 13.75 m with its
  chimney, the rooms 6.75 to 13.34 m. Fronts face +Z: `door-point` z is positive on all four houses (+3.70 library,
  +3.60 apothecary, +2.20 pools, +3.67 sanctum), and the city kit's `inn-3` faces +Z too.
- **The pools room's beam** imports as an ordinary opaque material; drawing it additively is left to the Pools
  scene.
- **The check holds each model's contract:** the parts and markers it must have, and for the kits their piece and
  role lists (the `Contract` records in `godot/Check/Check.cs`). A kit that gains a piece, role or marker needs
  its contract updated there, or the check fails.
- `art/models/textures/` (the intermediate PNGs the `.glb` files embed), `art/previews/`, `art/raw/` and
  `art/reference/` each have a `.gdignore`, so Godot doesn't import them.

## In the game

This section describes how the raylib diorama (iteration 2, kept on the `standalone-diorama` branch) uses the
models; that code isn't on the `godot` branch. The part and marker names below are the contract the Godot scenes
will read too.

The diorama copies `art/models/*.glb` next to the game (`models/`). A courtyard piece uses its model when
`hub.models` in `src/Atrium.Diorama/Assets/visuals.json` lists it with a scale (courtyard units per metre):

```json
"models": {"old_stoic": 0.85, "library": 0.62, "apothecary": 0.62, "pools": 0.5, "sanctum": 0.62, "lamp_post": 0.8}
```

A house's model is named after its locale id (`library`, `apothecary`, `pools`, `sanctum`). The Spirit Pools lodge
is drawn smaller, at 0.5, and stands further round than the built-in lodge: it's long and low, and at 0.62 it
doesn't fit between the basins and the cliff.

Take a piece out of the list, or delete its file, and the courtyard draws its built-in shape again. The import
step (`src/Atrium.Diorama/Render/ModelAsset.cs`) does what raylib doesn't:
- it finds each mesh's part by node name (raylib drops the names);
- it reads the markers, which raylib drops;
- it turns linear base colours back to sRGB;
- it puts welded smooth normals in the tangent channel for the ink outline;
- it builds mipmaps;
- it refuses any part over 65,535 vertices.

### The city kit

`assets/city_kit.py` builds `models/city-kit.glb` with `build.py -- city-kit`. These are the white city's
buildings, which `HubCity` bakes into the hub's city in place of its code boxes:
- **Pieces:** townhouses, a shop, an inn, a stable and a tower-house.
- **Layout:** each piece is built round its own origin (on the ground, at the footprint's centre, front toward
  −Y) and laid out along X. An empty named after the piece marks the origin; `<piece>-lamp` marks its lantern.
- **Parts:** one mesh per role, named `<piece>_<role>`:
  - `stone` and `trim` go into the masonry layer;
  - `roof` and `cloth` are tinted per building;
  - `glass-a`, `glass-b` and `glass-back` are each lit or left dark as a set;
  - `lamp` is always lit;
  - the rest keep their material colour.
- **Footprints:** each piece's nominal footprint is in `PIECES`, matched by `HubCity.Kit`. The game scales each
  building to its lot.
- **Fallback:** without the file, the city falls back to code boxes.

### The wall kit

`assets/wall_kit.py` builds `models/wall-kit.glb` with `build.py -- wall-kit`. These are the outer city wall's
pieces, which `HubCity.CityWall` places:
- **Bays:** an arched bay (a relieving arch with voussoirs and imposts, a cornice, the inner railing) and a plain bay.
  Both carry a corbelled parapet with machicolations and merlons with arrow slits. Each is 5.5 m long, with y = 0 at
  the wall-walk; the game stretches bays to fit each run.
- **Towers:** a machicolated, crenellated crown with a brazier; a slate cone with a finial; a string-course band with
  arrow slits. They are built for a 3 m radius and scaled to each tower.
- **Gates:** the Low Gate's gatehouse, and the broad water gate the Spirit Pools' cave mouth sits in. The water
  gate's frame runs 0.5 m back into the wall, where the game sets the wall's body back (`Run`'s `notch`).

The wall's body, the arcade's piers and the towers' shafts stay code-built in the city's ashlar. Wall pieces have
their face at local z = 0, with relief toward +z (into the city). Kit colours are written as sRGB and converted to
Blender's linear values (all three kits do this).

### The corner kit

`assets/corner_kit.py` builds `models/corner-kit.glb` with `build.py -- corner-kit`. These are the forgotten corner's
pieces, which `Scenes/HubCorner.cs` places round the sunken courtyard:
- **Walls:** crumbling wall sections and the cracked brick arch the falls burst from.
- **Ground:** root-heaved flagstones, rubble and fallen coping.
- **Growth:** ivy curtains, moss, a fern and a grass tuft.
- **Water:** puddles and a rivulet.

It follows the city kit's conventions (`<piece>_<role>` parts and an origin empty per piece), read through
`Render/KitAsset.cs`. Wall pieces have their face at local z = 0, with relief toward +z and the foot at y = 0. Ivy
hangs down from y = 0. The `water` role is drawn as standing water, `trickle` as running water, and `stone` takes the
corner's crumbling-wall texture (or its path texture, for the flagstones). Without the file, the code's own ivy and
arch come back.

### Room kits

Each room can also draw a kit: `models/<locale>-room.glb`, built by `assets/<locale>_room.py` with
`build.py -- <locale>-room`. The rooms listed under `rooms.models` in `visuals.json` load their kit.

- **One part per built-in model.** In the room's C# code each fixed model is made with `Prop("<key>", BuiltIn)`.
  A kit node with that exact name replaces the built-in shape. The game draws it with the same transform, tint,
  hover lift, rim and outline as the built-in shape, so none of the drawing or click code changes. Keys a kit
  leaves out keep their built-in shape.
- **The same frame as the shape it replaces.** Each kit part must sit exactly where its built-in model does:
  room coordinates for the shell, `furniture` and `decor`, or around the prop's own origin where the code
  moves it. Surfaces the game fills must stay exact: shelf boards, bench and desk tops, the crock's rim, the map
  board's face.
- **Coordinates.** The game is Y-up; a game point (X, Y, Z) is Blender (X, −Z, Y).
- **The game tints a few parts:** the Library's shelf plaques, and the Sanctum's blueprint sheet and chalk circle.
  Paint those light and neutral.
- **Bookcases.** A kit's bookcase is made for 5 shelves (1.80 m wide). The Library stretches it sideways for any
  other count.

Room sheets are cheap. One GPT Image 2.5 image (low quality, 1K, 0.25 credits) holds all of a room's
furniture, plus floor and wall patches.

### The Spirit Pools room (the Travertine Well)

`build.py -- pools-room` runs `pools_room.build()`, which hands over to `assets/pools_travertine.py` (the older
grotto, `pools_grotto.py`, is kept). Every number comes from `assets/travertine_layout.py`: pool slots, terrace
edges, the mouth, the runnel and outflow paths, prop and decor spots. It is plain Python, so
`python travertine_layout.py` prints where things land on the game camera. `pools_travertine.py` builds the room and
the markers. `assets/travertine_props.py` builds the bowls, the lantern, the druids' things and the small decor. If
that module is missing, pools_room's props stand in. Stone, moss and floor are painted procedurally. The roots use
`stoic-bark.png`.

| part | frame | what |
|---|---|---|
| `shell` | room | terraces (tops 1.66 / 0.16 / −1.34) with banded flowstone dam faces, the landing (2.4), grey-blue drape walls, the cave mouth (ring, treads up its left half, the brook's ramp and spout lip on the right, daylight at the top), the low outflow arch, the wet floor, the dry runnel and outflow channels, the right-wall alcoves, roots, the root stair, and a hole in the terrace top at each slot (the lip's outer oval, scaled to the slot) |
| `decor` | room | moss drapes over the dam edges, moss mounds, pebbles, plus `decor_items`: fern pots, sieve, feed shelf, grate, second rake |
| `ferns` | room | wild fern clumps, and the potted ferns (drawn with sway) |
| `beam` | room | the noon beam, an open tapered shaft from `oculus` to `beam-spot` (drawn additively) |
| `bowl`, `bowl-dark`, `bowl-dry` | pool-local | built for a 2.96 × 1.22 water oval, origin 0.62 m below the water; the game scales them per slot |
| `lantern`, `crock`, `bag`, `ledger`, `stool`, `rake` | own origin | as in the built-in props |

Markers (empties, game coordinates): `pool-N`, `pool-N-x`, `pool-N-z` for slots 0–5 (base, long-axis end, front-axis
end, which give position, yaw, rx and rz); `at-crock`, `at-bag`, `at-ledger`, `at-stool`; the water paths
`brook-*`, `runnel-*` and `outflow-*`; and `oculus` and `beam-spot`. PoolsScene falls back to its own layout for
any part or marker that is missing.

To split a turnaround sheet into same-scale views (for a multi-view lift), describe the views in `sheets.json`
and run `split_views.py` the same way as `build.py`, passing the sheet's file name.

## Files

| File | What it does |
|---|---|
| `build.py` | Builds one asset: imports `assets/<name>.py`, calls `build(kit)`, exports and renders previews. |
| `atrium_kit.py` | Shared helpers: swatches and decals cut from sheets, generated textures, matte materials, boxes, prisms, cylinders, tubes, UV projection, checks, glTF export, previews. |
| `assets/*.py` | One script per asset. Each returns its root object and height. |
| `sheets.json` + `split_views.py` | Where each view sits on a sheet, and the tool that cuts them apart. |

## Conventions (from the iteration-3 art bible)

- **Units and pivots.** Metres; the base centre sits at the origin.
- **Facing.** The front faces −Y in Blender, which becomes +Z in glTF and in the game.
- **Parts by role.** One object per part the game treats differently, named for that role. The names are the
  contract with the game, which finds parts by name:
  - `body` and `roof` for houses;
  - `glass`, lit at night;
  - `shutters` (closed, shown while a house is locked) and `shutters-open` (shown once it's unlocked);
  - `trunk`, which stays still, and `canopy`, which sways;
  - markers (empties): `lamp` (a house's lantern light), `light` (a lamp post's flame), `door-point` (on the ground
    in front of the door, where the courtyard's paving path ends) and `chimney` (where smoke rises).
- **Decals.** `kit.plaque` maps a decal upright (the sheet's top at the face's top). Until 2026-10-26 `fit_uv` had
  it upside down; the Library's door and windows were flipped until the fix.
- **Colour.** Painted colour comes from sheet swatches, sheet decals (doors, windows) or generated tileable
  textures. Nothing is lit in the texture: the game's cel shader does the light.
- **Normals.** Every closed part faces outward (`solid=True`): the game culls back faces. (The materials are
  exported double-sided, though, so Godot doesn't yet; see [In Godot](#in-godot).)
- **Engine limits.** Under 65,535 vertices per part (raylib's index limit, from the diorama). Textures embed as
  PNG, because that raylib build can't read JPEG.
- **Lean.** Built things lean a degree or two ("never plumb"); Old Stoic leans about 12° out over the pools.

## Lessons so far

- **A sheet styled from a full scene can turn into that scene.** Style from an isolated sheet instead (the
  Library sheet styled the tree and lamp sheets).
- **Painted sheets make poor tiling swatches,** because their ink outlines repeat as zigzags. Use them for
  decals and one-off colours; generate tileable textures separately.
- **Texture prompts with an image reference can grow the reference's subject** (the leaf texture grew trunks
  in its bottom half). Crop with `box=`, or generate textures without a reference.
- **A lift is usually a measuring tape.** Read numbers off it and build clean geometry. Old Stoic is the
  exception the designer chose: a multi-view lift (four views, so the back isn't invented) whose painted crown
  beat the scripted rebuild. `old_stoic.py` refines the lift itself (turned, scaled, split into trunk and canopy
  by painted colour, PNG texture). The scripted tree survives as `old_stoic_rebuilt.py`.
- **Sheet layouts drift.** Asked for one row of four views, the lodge sheet came back as a 2 × 2 grid with no
  right-side view (and with one pair of shutters closed, which became the closed-shutter reference). Read every
  sheet's layout before measuring it.
- **The brief's sizes and the sheet's proportions can disagree.** Measuring PX from the door and from the overall
  width gives different scales. Pick one, keep the silhouette the bible asks for, and say which in the script.
