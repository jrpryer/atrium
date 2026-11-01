# Architecture

Atrium is a C# / .NET 10 solution. All the game rules live in one engine-agnostic library; front ends only
present them. The repository root is also the Godot 4.7 .NET project for the graphical front end (see
[The Godot project](#the-godot-project)).

```
Atrium.slnx
├─ src/Atrium.Core/        engine-agnostic game: schemas, loader, validation, services, minigames, saves
├─ src/Atrium.Terminal/    the standalone front end (a full-screen terminal game)
├─ tests/Atrium.Core.Tests xUnit: validator, spine, minigames, saves, a full Chapter One playthrough
└─ AtriumGodot.csproj      the Godot front end's C# (godot/**/*.cs), beside project.godot at the root
content/                   designer-authored data: JSON + one Markdown file per tome
art/models/, art/textures/ the models (.glb) and tileable textures, which Godot imports in place
```

`Atrium.Core` targets `net10.0` and references nothing outside the base class library. Godot, Unity, MonoGame,
or another front end can reference it unchanged, as long as that host targets `net10.0` too. Godot C# projects
default to `net8.0`, so `AtriumGodot.csproj` sets `net10.0`, and it runs in Godot 4.7.2 on .NET 10.0.14. (The
first proof of that, `tools/godot-smoke`, is on the `standalone-diorama` branch.) The whole solution moved from
.NET 8 to .NET 10 on 2026-11-01.

## Layers

```
 front end (Terminal today; Godot scenes next)
   │  calls locale actions, steps the clock, drains notices, renders read-models
   ▼
 GameSession  — composition root and facade; save/load
   │
   ├─ Locales (minigames)        LibraryLocale · ApothecaryLocale · PoolsLocale · SanctumLocale
   │     each knows only World + ILensService (+ the essence registry / router where sanctioned)
   │
   ├─ Mystery spine              EvidenceLedger · LensService · ComprehensionGate · SynthesisResolver
   │                             Lexicon · UnresolvedRegistry
   ├─ Essence                    EssenceRegistry (derivation, transmutation, assembly, reconstitution)
   ├─ Story                      SanctumRouter · ProgressionDirector
   │
   └─ World (shared services)    ContentDatabase · GameState · EventBus · ItemStore · Conditions
                                 WorldClock · EventLog · Rng
```

### The AGENTS.md §4 contract, as built

| Brief | Code | Notes |
|---|---|---|
| `TomeResource` | `Content/Resources.cs` + `TomeParser` | Authored as Markdown with front matter (authoring ergonomics). |
| `IngredientResource` / `RecipeResource` | `ItemResource` (kind `ingredient`) / `RecipeResource` | Elements are items, so they can be bottled and carried. |
| `LifeformResource` | `LifeformResource`, `BreedingResource` | Ordered (mother/father) and unordered rules; environment-conditioned rules. |
| `PoolTransformResource` | `PoolTransformResource` | Requirement + base ticks + lifeform tick modifiers + optional transmutation. |
| `ToolResource` | `ToolResource` | `fixed` (locale fixtures) and `assembled` (Sanctum-built items that grant capabilities). |
| `DerivationResource` | `DerivationResource`, `CatalysisResource` | Containment edges; catalysis is recorded at runtime and documented statically. |
| `TaxonomyResource` | `TaxonomyResource` | Facets, terms, genres, traits, energy axes, capabilities. |
| `MysteryObjectResource` / `ClaimResource` | same | An object file holds its forms, derivations, readings, claims and resolution. |
| `StoryBeatResource` | same | Plus `LocaleResource`, `SealResource`, and `HubResource` (the fixed courtyard scene). |
| `EvidenceLedger` | `Mystery/Evidence.cs` | Readings per object and earned terms. Everything is saved. |
| `ComprehensionGate` | `Mystery/Evidence.cs` | Only answers whether a claim may be attempted. Plausible-wrong claims need evidence from at least two locales. |
| `SynthesisResolver` | `Mystery/Synthesis.cs` | `Resolved`, `NearMiss` (once per claim), `AlreadyConsidered`, `NotYetComprehensible`, `Invalid`. |
| `WorldClock` | `Runtime/World.cs` | One pausable clock. Real-time steps are capped; "sit a while" advances it on purpose. |
| `UnresolvedRegistry` | `Mystery/Synthesis.cs` | Re-presents open objects when you enter a locale. Funnel strength is a tunable. |
| `SanctumRouter` | `Story/SanctumRouter.cs` | Routes payloads to the minigames by signal. The mineral destination is a tunable. |
| `EssenceRegistry` | `Essence/EssenceRegistry.cs` | One live form per essence, asserted after every change. The Circle preview lists the current residence and everything that will go inert. |

## How a reading surfaces (decoupling)

Locales never know about mysteries. When a locale's mechanic acts on an item, it reports a `LensEvent`
("library / compare / tome X page 1", "apothecary / work / tool weighing-scales", "pools / place / energy …",
"sanctum / seal / stillkeepers-balance") through `ILensService`. The evidence layer matches the event against
the readings authored on that item's essence, checks each reading's prerequisites, records new evidence, earns
terms, and returns lines for the front end to show. When nothing matches, the locale shows an item- or
locale-level fallback from data. Readings therefore always come through each minigame's own mechanics,
never through an inspection menu.

Cross-locale effects travel only by signal (`EventBus`) and through the router:

- `ObjectResolved` → `SanctumRouter` applies the resolution payload (`TomesDelivered` → Library pile,
  `IngredientsDelivered` → Apothecary stock, minerals → druids' bag or satchel).
- `LocaleUnlocked("pools")` → the Pools module stocks its starting lifeforms.
- `WorldChanged` → the Library checks which tomes have become available.
- Each action calls `World.Changed()`, and `ProgressionDirector` then re-evaluates locale unlocks, object
  arrivals and story beats until nothing more changes.

Adding a locale means adding a module that implements its own verbs. No existing locale has to change. Its
verbs then need registering in `ContentValidator.VerbsByLocale`, which is how the validator checks readings.

## One essence, one place

- Every clue-object's essence lives on exactly one non-inert `ItemInstance` (`Essence` field).
  `EssenceRegistry.AssertInvariant()` runs after every essence operation and throws
  `InvariantViolationException` on a violation. The game always treats a violation as a bug, never as a
  player error.
- **Containment edges** move the essence:
  - a derivation (a new form, plus an optional remnant that records which form it restores);
  - a pool transmutation (same instance, new form);
  - tool assembly (the tool becomes the carrier).
- **Catalysis** is recorded as text on the essence record ("Helped a Shadow Guppy hatch…", "Opened the
  Stillkeepers' Balance."). It is never nullified and never listed in Circle warnings.
- **Reconstitution** restores the form a remnant came from, marks the current carrier inert, halts any soak
  that carrier was part of, and crumbles remnants made later in the chain. Inert husks have no traits, no
  pool energy and grant no capabilities.

## Engine contract (the few names code relies on)

Content is data, but a handful of ids are the engine's own vocabulary:

- **Locale module ids:** `library`, `apothecary`, `pools`, `sanctum`. Their verbs are `shelve`, `compare`,
  `work`, `derive`, `place`, `transform`, `inspect` and `seal`.
- **Energy axis ids:** `light` and `heat`.
- **The reconstitution capability:** `sanctum.reconstitute`. Whichever fixed tool grants it is the Circle.
- **Counters the engine maintains,** which content may read in conditions:
  - Library: `library.shelved`, `library.read`, `library.read:<tome>`, `library.settled`, `library.misshelved`, `library.tagged`.
  - Apothecary: `apothecary.discovered`, `apothecary.tried`.
  - Pools: `pools.cleansed`, `pools.cleansed:<pool>`, `pools.fed`, `pools.born`, `pools.born:<species>`, `pools.spawned:<species>`, `pools.transformed`, `pools.transformed:<id>`.
  - Essence: `essence.remnants-created`, `essence.derivations`, `essence.reconstitutions`.
  - Sanctum: `sanctum.assembled`, `sanctum.assembled:<tool>`, `sanctum.seals`.
  - Story and navigation: `mystery.resolved`, `visits:<locale>`.

Everything else (tools, verbs on tools, shelves, genres, page counts, tick costs, pools, lifeforms,
breeding, readings, claims, beats, the courtyard scene) is data.

## Saves

`SaveFile` is versioned JSON around the whole `GameState`: items, evidence, essences, every minigame's state,
the clock, the RNG state, counters, flags and the event log. Services hold no state of their own beyond
indexes, so save-anywhere is just serialisation. The terminal game autosaves when you return to the
courtyard and when you quit. It saves to `%APPDATA%/Atrium/saves/<slot>.json` by default, or to the folder
given with `--saves`. Offline progress on load is a tunable (`offlineProgress`: `none` or `capped`). The Godot
project doesn't save or load yet.

## The Godot project

The repository root is a Godot 4.7 .NET project (Godot 4.7.2, .NET build). It is C# only: no GDScript, in files
or in scenes. The scope so far is assets and core. The core is wired in; every model, texture and content file
imports or loads cleanly; and a viewer and a headless check show it. There are no gameplay scenes yet. The
raylib diorama (iteration 2) was removed from this branch; it's kept on `standalone-diorama` as a reference.

### Layout

```
project.godot            config_version 5, features "4.7" and "C#"; main scene res://godot/Viewer/Viewer.tscn;
                         autoload Game; 1280×720; 3D MSAA ×2; assembly name AtriumGodot
AtriumGodot.csproj       Godot.NET.Sdk/4.7.2, net10.0, EnableDynamicLoading; compiles only godot/**/*.cs;
                         references src/Atrium.Core
godot/Core/              ResContentSource.cs, Game.cs (the autoload), Assets.cs
godot/Viewer/            Viewer.tscn + AssetViewer.cs: the asset viewer (the main scene)
godot/Check/             Check.tscn + Check.cs: the headless check
content/, art/models/, art/textures/   game data, imported in place (res://content, res://art/…)
```

- **Why the root.** Godot wants `project.godot` and the `.csproj` side by side. The 4.7 docs for
  `dotnet/project/solution_directory` say it moves only the solution, and that the project root should contain
  the `project.godot` and `.csproj` next to each other. A trial with the csproj in `godot/` didn't build from
  `--editor --build-solutions`, and the game then couldn't instantiate its C# scripts. So the root is the Godot
  project, and `godot/` holds only its C# and scenes.
- **One solution.** Godot finds the C# project through `Atrium.slnx`, where `AtriumGodot.csproj` sits under a
  `/godot/` folder. Don't add an `AtriumGodot.sln` beside it: with both, Godot reports *"Multiple solutions
  containing a project with assembly name 'AtriumGodot'"*. The editor builds the whole solution, so it compiles
  the terminal and the tests too. A bare `dotnet build` or `dotnet test` at the root fails with MSB1011 (more
  than one project or solution file there), so name `Atrium.slnx` or a project.
- **Kept out of Godot.** `.gdignore` files in `src/`, `tests/`, `tools/`, `docs/`, `art/raw/`, `art/reference/`,
  `art/previews/` and `art/models/textures/` (Blender's intermediate PNGs, which the `.glb` files embed) stop
  Godot scanning them. The root `Directory.Build.props` (net10.0, C# 14, nullable, implicit usings, invariant
  globalization) applies to the Godot project as to every other.
- **In git.** `.godot/` (the import cache and the C# build output) is ignored; the `*.import` sidecars and
  `*.uid` files are kept. The textures Godot extracts from `apothecary-room.glb` (`art/models/*.png` and their
  sidecars) are ignored too: the `.glb` is their source, and every import remakes them.

### The core inside Godot

- **`ResContentSource`** implements `Atrium.Core.Content.IContentSource` over `res://content` with `DirAccess`
  and `FileAccess`. It returns `.json` and `.md` files with `/` paths in ordinal order, as
  `DirectoryContentSource` does, so the loader sees the same content either way.
- **`Game`** (`godot/Core/Game.cs`, the autoload) loads the content on `_Ready` with `ContentLoader.TryLoad`. It
  sends every validation error to `GD.PushError` and every warning to `GD.PushWarning`, and keeps `Content`,
  `Report`, `LoadError`, `Ok`, a `Session` from `GameSession.NewGame` and a one-line `Summary`. Scenes reach it
  through `Game.Instance`. It doesn't step the world clock, save or load yet.
- **`Assets`** finds models and textures at run time with `ResourceLoader.ListDirectory`, which also works in an
  exported build. `ModelInfo` reads an instantiated model: its meshes (`MeshInstance3D`), its markers (plain
  `Node3D`s with no children), its world bounds, and a kit's pieces (from the `<piece>_<role>` mesh names). The
  hub and room scenes are meant to find parts and markers through it.

### Imports

| Files | Settings | Why |
|---|---|---|
| all 13 `art/models/*.glb` | `nodes/use_name_suffixes=false`; root scale 1.0; everything else default | Godot otherwise turns names ending in `-col`, `-occ`, `-navmesh`, `-rigid`, `-wheel` and the like (after `-`, `_` or `$`, in any case) into colliders, occluders and so on. Kit parts are `<piece>_<role>`, so a future role named `col` would quietly become a collider. With suffixes off, every glTF node arrives under its own name: meshes as `MeshInstance3D`, empties as `Node3D`. |
| 12 of them | `gltf/embedded_image_handling=2` (Embed as Basis Universal) | The `.glb` stays the only source, and the textures get mipmaps and VRAM compression (mean error 0.5–1.4 out of 255 against lossless). Extract would write about 150 PNG copies beside the models; Embed as Uncompressed gives no mipmaps. |
| `apothecary-room.glb` | `gltf/embedded_image_handling=1` (Extract Textures) | Godot 4.7.2 can't decode mip level 4 of two thin frame swatches (24×270 and 270×24) as Basis, and prints *"BasisUniversal cannot unpack level 4."* on every load. Its 30 extracted PNGs import lossless, with mipmaps. |
| 8 `art/textures/*.png` | `compress/mode=2` (VRAM Compressed), `compress/high_quality=true`, `mipmaps/generate=true`, `detect_3d/compress_to=0` | They're 3D surface textures, but no material uses them yet, so Godot's 3D detection would never compress them or add mipmaps. High quality picks BPTC on desktop (ASTC on mobile) over DXT1/ETC2, which would leave block artefacts on the painted ink lines. |

The check reads these options back from each `.import` file, so changing one means changing what
`godot/Check/Check.cs` expects (for image handling, its `ImageHandling` dictionary). What the import showed
about the models themselves (colours, emission, double-sided materials, scale and facing) is in
[tools/blender/README.md](../tools/blender/README.md#in-godot).

### The viewer

`godot/Viewer` is the main scene. It finds every `.glb` and `.png` at run time and shows one model at a time,
lit by a `WorldEnvironment` (colour background and ambient light) and a `DirectionalLight3D` with shadows. The
camera orbits the model and frames it by its bounds; yaw 0 looks at the +Z front. A label lists the meshes and
markers, the model's size, and the content summary. Markers are orange spheres with `Label3D` names, drawn
without a depth test. `↑`/`↓` frames one kit piece at a time. The textures page shows each texture as imported,
beside a 2×2 repeat to check that it tiles, with its import settings. The keys are in the
[README](../README.md#the-godot-project); so far they've only been exercised through the tour, not by hand.

`-- --tour [--hold=N]` shows each model from the front, three-quarter (with markers), side (+X) and back, then
every kit piece, then every texture. It holds each shot for N frames (default 2), prints a `TOUR frame N: …`
line per shot, and quits, so with Movie Maker it records a reviewable frame sequence (see the commands below).

The `.tscn` files were written by hand without `uid=` headers; the editor adds them the first time it saves
each scene.

### The check

`godot/Check` runs headless, prints a report and exits 0 (pass), 1 (failures) or 2 (the check itself threw). It
checks:

- **Content:** it loaded through the `Game` autoload with no errors (today: 42 tomes, 53 items, 30 recipes,
  10 lifeforms, 3 pools, 5 clue-objects, 0 warnings).
- **Session:** a `GameSession` plays 600 ticks, saves, loads and saves again with identical state
  (`savedAtUtc` aside).
- **Models:** every `.glb` loads and instantiates, and its `.import` options match the table above. Its glTF
  JSON chunk is read and compared node by node with the imported scene: the same count, the same names, meshes
  as `MeshInstance3D` with surfaces, empties as `Node3D`. Every material a mesh uses arrives by name, with albedo
  = `linear_to_srgb(baseColorFactor)` within 1.5/255 and emission = `linear_to_srgb(emissiveFactor)`, and its
  texture is present, mipmapped and embedded as Basis Universal (or extracted, for apothecary-room). Node and
  material names are scanned for Godot's import suffixes.
- **Each model's contract:**
  - `pools-room`: 13 parts; `pool-0`…`pool-5` with `-x` and `-z`; `at-crock`, `at-bag`, `at-ledger`, `at-stool`;
    `oculus` and `beam-spot`; `brook-`, `runnel-` and `outflow-` numbered from 0; exactly 41 markers.
  - `city-kit` (9 pieces), `corner-kit` (16) and `wall-kit` (7): each piece has an origin empty and parts, and
    every part is `<piece>_<role>` with a known piece and role. `city-kit` also needs `inn-3-lamp` and
    `stable-1-lamp`.
  - The four houses: their parts, the `lamp`, `door-point` and `chimney` markers, and `door-point` z > 0 (the
    front faces +Z).
  - The three other rooms, `old_stoic` and `lamp_post`: their named parts.
- **Textures:** every texture loads, with `compress/mode=2` and mipmaps on.
- **Engine messages:** an `EngineLog` (a `Logger` added with `OS.AddLogger`) fails the check on anything Godot
  prints while a model or texture loads. That's how it catches the Basis decode message: with apothecary-room
  set back to Basis, the check exits 1.

It passes today with no failures and no warnings.

### Commands

From Git Bash in the repository root. Set `G` to wherever the Godot 4.7.2 .NET console build is installed (the
editor, without `_console`, sits beside it):

```bash
G="/c/Program Files/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe"

dotnet build Atrium.slnx                     # build; Godot loads AtriumGodot.dll from .godot/mono/temp/bin/Debug
dotnet test tests/Atrium.Core.Tests          # the core's tests
"$G" --headless --path . --import            # (re)import models and textures
"$G" --headless --path . --scene res://godot/Check/Check.tscn; echo $?   # the check
"$G" --path .                                # the viewer (the main scene)
"$G" --headless --path . --editor --build-solutions --quit              # the editor's own build, headless
"$G" --path . --write-movie "<dir>\tour.png" --fixed-fps 10 -- --tour --hold=2   # writes <dir>\tour00000000.png on
```

Command-line runs need `dotnet build` first; the editor (open `project.godot` in the Godot 4.7.2 .NET editor,
then F5) builds on its own, through `Atrium.slnx`.

### What's next

1. **The hub and rooms as Godot scenes.** Instantiate the kits and place things by their parts and markers,
   found by name through `ModelInfo`. The hub uses `city-kit`, `wall-kit`, `corner-kit`, `old_stoic`,
   `lamp_post` and the four houses (fronts toward +Z, `door-point` where the path ends, `lamp` and `chimney`
   markers). Each room uses its `<locale>-room.glb`; the Spirit Pools place their pools at `pool-N`, `pool-N-x`
   and `pool-N-z`, the druids' things at the `at-*` spots, the water along `brook-*`, `runnel-*` and `outflow-*`,
   and the beam between `oculus` and `beam-spot`. The raylib diorama on `standalone-diorama` (in `src/Atrium.Diorama`:
   `Scenes/Hub*.cs` and `Scenes/*Scene.cs`) is the reference for placement and behaviour.
2. **The cel/ink look as Godot shaders.** The diorama's cel bands and ink outline (`Render/Shaders.cs` on
   `standalone-diorama`) become Godot shaders on the imported meshes. Godot's shading language isn't GDScript,
   so this stays within "C# only". Two things wait on this step: back-face culling (nearly every material
   imports double-sided, so culling is off) and drawing the Pools beam additively (it imports as an ordinary opaque
   material).
3. **The card UI.** The diorama's parchment working cards (`Overlays/Cards.cs`, `Scenes/*Cards.cs`) as Godot
   `Control` scenes that call the same `GameSession` methods the terminal views call. Each view in
   `src/Atrium.Terminal/Views` maps onto one card or scene.
4. **Running the game.** Step the `WorldClock` from the `Game` autoload (pausable), bridge `EventBus` events to
   Godot signals there, and save and load through `SaveFile`. Where Godot saves (`user://`, or the terminal's
   `%APPDATA%\Atrium\saves` so both share saves) is the designer's call.
5. **An export preset.** There isn't one yet. The `.md` tomes aren't Godot resources, so the preset needs a
   non-resource include filter such as `content/*` before `ResContentSource` can find them inside a `.pck`. No
   exported build has been tried.
6. **`[GlobalClass]` Resources**, only if the designer wants to author in Godot's inspector: wrappers that
   export to the same JSON. The engine keeps reading data either way. None exist yet.

## Testing

`dotnet test tests/Atrium.Core.Tests` runs:

- **Validator:** the ≤3 cap, recipe cycles, orphan remnants, band sanity, unknown references, typo'd keys,
  verbs that aren't the locale's own mechanics, page limits.
- **Spine:** two-locale near-miss gating, once-only near-misses, wrong answers costing nothing, derivation
  and remnants, containment-only nullification, the one-instance assertion, the funnel and lexicon tunables.
- **Minigames:**
  - neglect never kills;
  - ordered breeding pairs;
  - environment-conditioned breeding;
  - 300 vs 220 tick soak costs;
  - soaks pause and never reset;
  - forgiving shelving, order detection and settling;
  - prelabeled shelves driven by data;
  - Apothecary insight hints;
  - a cozy-only player opens the Pools.
- **Saves:** exact round trips, including RNG determinism, and the offline-progress tunable.
- **`PlaythroughTests.ChapterOneCanBeCompleted`:** all five objects through the public API, with the
  invariants checked along the way.

The terminal game also accepts `--script <file>` (keys from a file) for scripted UI smoke runs. The Godot side
has its own headless check ([The check](#the-check)), which `dotnet test` doesn't run.
