# Iteration 1: what was built, and the calls I made

*2026-10-24. For the designer's review. Anything marked **decision** was made without you and is cheap to
change. Most of it is data.*

## Scope

A complete, playable first chapter ("The Unkept Vow"):

- **Four houses**, each a full minigame on its own:
  - Library: tagging shelves, shelving the pile in year order, a reader with margin notes, check-out.
  - Apothecary: 30 discoveries from water, ash and patience, with a Discovery Map, hint-by-insight, the Old
    Formulary and bottling.
  - Spirit Pools: three pools on the Light/Dark × Hot/Cold plane, ten lifeforms, feeding, cleansing,
    sulking, breeding with ordered and environment-conditioned genetics, spawning from minerals and auras,
    and timed transformations.
  - Sanctum: the magnifying glass, the designation desk, the assembly bench, the Reconstitution Circle, the
    Balance, and a cabinet.
- **Five clue-objects** that unlock the Spirit Pools, the Circle, four assembled tools and a new Apothecary
  verb, and tell a small complete story.
- **42 tomes.**
- **A full spine:** evidence, comprehension gate, synthesis with near-misses, the essence/remnant rules,
  resurfacing, story beats, a fixed courtyard scene, and save/load.

## Platform (decision, per your request)

You asked for C# .NET that stays standalone and can be ported later. So the game is an engine-agnostic
`net8.0` library plus a terminal front end, with no Godot dependency. AGENTS.md §7 says Godot 4.6 with
`[GlobalClass]` Resources. That's still the porting path, described in `docs/ARCHITECTURE.md`. Content is JSON
and Markdown rather than `.tres` files, which also fits "make authoring cheap".

The terminal front end is a placeholder presentation layer (AGENTS.md §6: "Placeholder primitives are
acceptable"). It uses colour, the energy-plane diagram and a small drawing of the tree and falls, with no images.

## The chapter at a glance (spoilers)

```
start: Library, Apothecary, Sanctum open — a pressed leaf is misfiled in the Library pile
  │
  ├─ PRESSED LEAF   Library (shelving, two books) · Apothecary (scales, burner, water) · Sanctum (glass)
  │    true: a vow-token · the High Hallows · a vow
  │    → beat "Word Down the Stair" → SPIRIT POOLS open (also opened by a cozy milestone)
  │    → Shrine-Wax for the Apothecary · a new tome · the Ledger of Tides returned to the Library
  │
  ├─ GOLD NUGGET    found by cleansing the Sunward Basin
  │    crushing it at the mortar shows its iron core → IRON SLAG remnant → the CIRCLE wakes
  │    true: a tithe-nail · Old Stoic · a promise to return
  │    → Tamsin gives you THE COLD STONE
  │    flakes + pressed oil + patience → Gilded Lamp-Oil (Formulary: "true wealth, glittering and rare")
  │    + lamp glass → LAMPLIGHTER'S LANTERN (Library can read faded ink)
  │
  ├─ COLD STONE     needs "blessing by the sun": a bright, warm pool, the nugget WHOLE, two snails, 300 ticks
  │    (220 with a Sun-Spotted Goldfish, bred from a Spinner mother × Glory father, Glories come from
  │     Spinners bred in hot water). The nugget is in the lantern, so you restore it at the Circle,
  │    and the lantern goes dark. Matter is reclaimable; knowledge is permanent.
  │    true: a lampstone · the Crown · stored sunlight → beat "Lamps in Oil" → Sun-Mote Aura · Sun-Lens
  │
  ├─ BLACK PEARL    hidden in the hollow Ledger of Tides
  │    in the Shade Pool its darkness lets Grey Guppies hatch a Shadow Guppy (catalysis)
  │    true: a smoke-pearl · the cisterns of the Ribs · a sealed flame → Still Kit (the Apothecary can distil)
  │    crushed into a waxed reed pouch → SMOKE-IN-A-BAG + pearl husk (containment)
  │    the bag satisfies the Balance ("a thing of shadow and of flame") → the reliquary opens
  │    restoring the pearl later makes only the bag inert; the guppy and the open reliquary stay
  │
  └─ GREAT KEY      from the reliquary
       true: a sluice-key · the Gallery Reach · the falls' old course → beat "Up the Canyon" → Silver Silt
       optional: by the relit lantern, the Waterworks book's faded margin is in the same hand as the vow
       → beat "The Same Hand"
  all five → "The End of the Beginning" (chapter close; sets up the climb to the Gallery Reach)
```

The full key-by-key solution is in [chapter-one-walkthrough.md](chapter-one-walkthrough.md).

## Decisions worth your eyes

| # | Decision | Why | Where to change it |
|---|---|---|---|
| 1 | Library, Apothecary and Sanctum are open from the start; the Pools open later. | The first object needs three lenses for real synthesis and near-misses. The Sanctum is where synthesis happens. | `content/locales.json` |
| 2 | The Pools open on resolving the leaf **or** after shelving 15 tomes and making 8 discoveries. | Invariant 5 both ways: a cozy-only player still gets the Pools. | `locales.json` → `pools.unlockWhen` |
| 3 | Shelving follows the Milanote model: five empty shelves, five genre tags, "oldest nearest the lamp" (by year). | It's the most recent board. Prelabeled sigil shelves work from data alone (tested). | `config.json` → `libraryConfig` |
| 4 | Designations are three facets: *What it is / Where it came from / What it holds*. Terms are earned by readings. | Follows v2's designation card. | `taxonomy.json` |
| 5 | The lexicon is `global`: every earned term is offered on every card. | With five objects the global lexicon stays small (34 terms) and makes later objects harder in a fair way. | `config.lexiconMode` |
| 6 | Funnel is `gentle`: on entering a house, open objects with untried lenses there are named, without saying which lens. | Discoverable without hand-holding. `strong` adds hints. | `config.funnel` |
| 7 | 1 tick = 1 real second while the game is open. Idle gaps are capped at 600 ticks. No offline progress. "Sit by the water" passes 60 ticks. Time is shown diegetically. | Fish Tycoon's tempo without punishing absence, and the clock can be paused. | `config.json` |
| 8 | Creatures never die. They sulk (no breeding) when outside their band, hungry or in murky water. | Open question 9 stays open. Sulking is fully recoverable. | Pools module, `poolsConfig` thresholds |
| 9 | Crushing happens at the Apothecary mortar. | The README places state changes in the Apothecary. **Confirmed by the designer on 2026-10-24**; AGENTS.md and the overview sheet now say the same. | each `derivation.locale` |
| 10 | The Circle wakes when the first remnant ever exists. | Recoverability starts exactly when it's needed (invariant 1). | `tools.json` → `reconstitution-circle` |
| 11 | Seals (the Balance) never consume what's laid on them. | The black pearl example: the bag satisfies the puzzle and stays. | `SealResource` |
| 12 | Failed mixes build "insight". Three new failures reveal one ingredient of a nearby discovery. | "Each mix brings you closer to revealing a hidden catalyst" (README). | `apothecaryConfig` |
| 13 | Minerals and auras go to the druids' bag. In the spawning pond they spawn motes and are not used up. | Minimal semantics while you iterate (open question 3). | `config.mineralDestination`, `spawnRules` |
| 14 | Breeding: Spinner × Spinner in heat ≥ +4 gives Blazing Glories. Spinner ♀ × Glory ♂ gives Sun-Spotted (ordered; the reverse does nothing). Grey × Grey in light ≤ −4 gives Shadow Guppies. The first brood of a new kind is guaranteed. | The worked examples in AGENTS.md, with luck removed from discoveries. | `pools/lifeforms.json` |
| 15 | Tool assembly gives each locale a capability. The Lantern lets the Library read faded ink. The Sieve finds minerals while cleansing. The Sun-Lens is a pool fixture. The Still Kit adds a distil verb to the Apothecary. | "Assemble discoveries into new tools for the other locales." | `tools.json` |
| 16 | A paused soak names what it is waiting for ("brighter water", "the company of 2 Yellow-backed Snails"). | Mistakes become curiosity and point back to the evidence. | generated from transform data |
| 17 | Tome titles are written in the spirit of *Librarian: Tidy Up the Arcane Library!* but are all original. | Your request; nothing copied. | `library/tomes/*.md` |

## Placeholders

- **Characters:** Ada and Mab Quire, Tamsin and Corran Pell, Brother Alder, Sister Wend, and I.F.
- **Places:** the Hem, the Ribs, the Gallery Reach, the Long Stair.
- **Calendar:** "SR".
- **Smaller details** invented by the tome writers, for example the forty-one nail-holes and the stair counts.

The [world bible](world-bible.md) lists all of these.

## Not built yet, by design or for later

- **Economy and shop:** open question 12. There is no currency anywhere.
- **Hub as a 3D diorama, art and audio:** §6 is still to be designed. The terminal front end is a stand-in.
- **NPC request/customer system, accessibility targets, platform builds:** §5 still marks these
  "designer to expand".
- **Procedural object generation:** open question 1. The data model is generator-friendly: an object is one
  self-contained JSON document, and the validator checks it.
- **Chapter Two.** The hooks are already in place:
  - the Silver Silt has no spawn rule yet;
  - the Wardens tome mentions keys that must never leave the Reach;
  - nobody yet knows where I.F. went.
