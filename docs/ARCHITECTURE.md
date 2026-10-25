# Architecture

Atrium is a plain C# / .NET 8 solution with no engine dependency. All the game rules live in one library;
front ends only present them.

```
Atrium.slnx
├─ src/Atrium.Core/        engine-agnostic game: schemas, loader, validation, services, minigames, saves
├─ src/Atrium.Terminal/    the standalone front end (a full-screen terminal game)
├─ tests/Atrium.Core.Tests xUnit: validator, spine, minigames, saves, a full Chapter One playthrough
└─ content/                designer-authored data: JSON + one Markdown file per tome
```

`Atrium.Core` targets `net8.0` (the runtime Godot 4.4–4.6 C# uses) and references nothing outside the base
class library. Godot, Unity, MonoGame, or a future graphical front end can reference it unchanged.

## Layers

```
 front end (Terminal today; a Godot scene tree tomorrow)
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
given with `--saves`. Offline progress on load is a tunable (`offlineProgress`: `none` or `capped`).

## Porting to Godot later

1. Reference `Atrium.Core` from the Godot C# project (both use `net8.0`).
2. Implement `IContentSource` over `res://content` with `FileAccess`/`DirAccess` (about 20 lines), or keep
   the content folder on disk.
3. Build a Godot scene per locale that calls the same `GameSession` methods the terminal views call. Each
   view in `src/Atrium.Terminal/Views` maps onto one scene.
4. Bridge `EventBus` events to Godot signals with one autoload node.
5. If the designer wants authoring in Godot's inspector, add `[GlobalClass]` Resource wrappers that export
   to the same JSON. The engine keeps reading data either way.

## Testing

`dotnet test` runs:

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

The terminal game also accepts `--script <file>` (keys from a file) for scripted UI smoke runs.
