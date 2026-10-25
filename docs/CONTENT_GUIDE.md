# Content guide

Everything the player meets is data under `content/`. Adding a tome, a recipe, a fish, a tool, a story beat or
a whole clue-object needs no code. After any change, run:

```bash
dotnet run --project src/Atrium.Terminal -- --validate
```

The validator lists every problem with the file it came from: unknown ids, typo'd keys, cycles, broken
invariants. The game refuses to start on content with errors, so this check is your safety net.

## Layout and file conventions

```
content/
  config.json            game, library, apothecary and pools tunables
  taxonomy.json          facets, terms, genres, traits, energy axes, capabilities
  locales.json           the four houses: unlock conditions, intros, fallback responses
  tools.json             fixed tools and Sanctum-assembled tools
  apothecary/            elements and recipes
  pools/                 pools, lifeforms, breeding rules, minerals, spawn rules
  sanctum/seals.json     riddle-locks such as the Balance
  story/beats.json       story beats
  hub.json               the courtyard scene shown at the hub
  mystery/objects/*.json one file per clue-object (forms, derivations, readings, claims, resolution)
  library/tomes/*.md     one file per tome
```

- Organise files however you like. Every `*.json` file anywhere under `content/` is loaded, and same-named
  sections (`items`, `recipes`, `lifeforms`, …) from all files are merged. The one exception is `config`
  (and each `*Config`), which may appear once.
- JSON may contain `//` comments and trailing commas.
- Ids are lower-case-with-dashes and must be unique within their kind. Reading ids must be unique across all
  objects.
- Unknown keys are errors, so a misspelt field never silently does nothing.

## Conditions

Locale unlocks, beats, arrivals, tome availability, blueprints, formulary entries, claim gates and
reading prerequisites all use the same condition language:

```jsonc
"start"                                   // always true
"beat:unkept-vow"                         // shorthand: key:value
"resolved:gold-nugget"  "arrived:black-pearl"  "reading:leaf.script"  "locale:pools"
"tool:lamplighters-lantern"  "capability:library.read-faded"  "seal:stillkeepers-balance"
"discovered:lamp-glass"  "lifeform:shadow-guppy"  "flag:something"
{ "counter": "library.shelved", "atLeast": 15 }
{ "all": [ ... ] }   { "any": [ ... ] }   { "not": ... }
```

Every field set on one object must hold. See `docs/ARCHITECTURE.md` for the counters the engine keeps.

## Tomes (`library/tomes/<id>.md`)

```markdown
---
id: trees-of-the-upper-tiers
title: Trees of the Upper Tiers: A Field Guide for the Chronically Out of Breath
author: Wenna Holloway
year: 512
genre: horticulture
cover: moss-green cloth, silver-stamped spine
blurb: One line for the book card.
available: start
hint: Optional; shown when the player tries the wrong shelf.
---

::: page prose | Optional page title
Paragraphs separated by blank lines.

::: page poetry
Verse, art, tables, lists and recipes are shown verbatim.

::: page marginalia faded
Faded pages can only be read when libraryConfig.fadedRevealWhen holds (here: the Lamplighter's Lantern).
```

- Each page header is `::: page <kind> [faded] [loose] [| Title]`. A `loose` page isn't in the tome until a
  payload delivers it (`"pages": ["tome-id#3"]`). The tome then goes back on the pile for re-shelving.
- `genre` must be a taxonomy genre that the shelving model can hold. That means a tag in the tagged model,
  or a shelf in the prelabeled model.
- Page numbers count from 1 in file order. Readings refer to them, so keep page order stable once a reading
  points at a page.
- The page limit (`libraryConfig.minPages`/`maxPages`, default 1–3) is a tunable.
- Title style: earnest mock-academic "Title: Subtitle" that turns playful in the subtitle. See
  `docs/design/tome-manifest.md`.

## Apothecary

```jsonc
{ "items": [ { "id": "loam", "name": "Loam", "kind": "ingredient", "traits": ["earthy"],
               "poolEnergy": { "light": 0, "heat": 0 }, "portable": true, "description": "…" } ],
  "recipes": [ { "id": "r-loam", "inputs": ["hearth-ash", "patience"], "tool": null, "output": "loam", "text": "…" } ] }
```

- Inputs are an unordered set of up to `apothecaryConfig.maxInputs`. `tool` is an Apothecary fixed tool,
  or is omitted.
- The tree must be acyclic. The validator also warns about recipes nobody can reach from the starting
  elements plus everything the story delivers.
- Elements never run out. `portable` elements can be bottled into the satchel. A bottle's `poolEnergy` and
  `foodValue` apply in the Pools.
- A new tool verb is just a new fixed tool (see `spirit-still`). It can be gated on a capability that an
  assembled tool grants.

## Spirit Pools

```jsonc
{ "lifeforms": [ { "id": "grey-guppy", "name": "Grey Guppy", "kind": "fish", "sexed": true,
                   "survive": { "light": [-5, 3], "heat": [-4, 3] }, "thrive": { "light": [-5, 2], "heat": [-3, 2] },
                   "emits": { "light": 0, "heat": 0 }, "hungerPerTick": 0.07, "wastePerTick": 0.03,
                   "gestationTicks": 120, "cooldownTicks": 260 } ],
  "breeding": [ { "id": "deepest-shade", "parentA": "grey-guppy", "parentB": "grey-guppy", "ordered": false,
                  "requires": { "light": [-5, -4] },
                  "offspring": [ { "lifeform": "shadow-guppy", "weight": 2 }, { "lifeform": "grey-guppy", "weight": 1 } ],
                  "text": "Shown the first time this pairing hatches something new." } ] }
```

- Axes run −5…+5 (`taxonomy.axes`). The thrive band must sit inside the survive band.
- Creatures never die. They sulk when they are outside their survive band, hungry, or in murky water.
  Only thriving, fed creatures in clear water breed.
- `ordered: true` means `parentA` is the mother and `parentB` the father. Rules with `requires` are tried
  first. With no rule, two of a kind breed true (`breeds: false` turns that off for plants and motes).
  `guaranteeNewFirst` makes the first brood of a never-seen offspring that species.
- `poolTransforms`: `target` item, `requires` (light/heat ranges, `clarityMin`, `reagentTraits`, `reagents`,
  `lifeforms`), base `ticks`, `modifiers` (`{lifeform, min, deltaTicks}`), optional `transformInto`.
  Progress pauses when the conditions stop holding and never resets. Paused soaks tell the player what they
  want, phrased from this data.
- `spawnRules`: a mineral or aura lying in a `spawningPond` gathers a lifeform every `everyTicks`, up to
  `maxPresent`. Minerals aren't consumed.
- `siltFinds` on a pool: items a cleanse turns up once, optionally needing a capability (the sieve).

## Tools

- `fixed` tools belong to a locale and exist while `availableWhen` holds.
- `assembled` tools are built at the Sanctum from `components`. Each component is `{ "item": … }` or
  `{ "trait": … }`. The tool is an item. It `grants` capabilities, which conditions can test, and may have
  `poolEnergy`. If a component carries an essence, the tool contains it, and it goes inert (`inertName`,
  `inertDescription`) when that essence's original is restored at the Circle.

## Clue-objects (`mystery/objects/<id>.json`)

One file per object, under a top-level `"object"` key. See `pressed-leaf.json` for a complete example.

| Field | Meaning |
|---|---|
| `id`, `name`, `baseForm`, `summary` | The essence id, how it's named in prose, the original form the Circle restores, and the journal header. |
| `arrival` | `when` (a condition), `location` (`satchel`, `library.pile`, `sanctum.cabinet`, `pools.druids-bag`, `pool:<id>`), `text`, an optional first `reading`. |
| `forms` | Its items. Every form is `kind: clueForm`, and remnants are `kind: remnant`. |
| `derivations` | `locale`, `tool`, `input` or `inputTrait`, `with` (co-input elements), `produces`, `remnant`, `confirm` (shown before the player commits), `text`, and an optional `formulary` riddle. |
| `readings` | `locale`, `trigger` (the mechanic that reveals it), optional `form` restriction, optional `requires`, `text`, `grants` (terms), and `funnelHint`. |
| `claims` | Exactly one `true`, and at most 3 `plausibleWrong`, each with a unique `nearMiss`. `designation` gives one term per facet, and `requires` is the comprehension gate. |
| `resolution` | `title`, `text`, `payload` (tomes, pages, ingredients, minerals, items, flags). |
| `catalysis` | Documentation of what the essence can enable without being contained. |

### Triggers per locale (the minigames' own mechanics)

| Locale | Verb | Matches on |
|---|---|---|
| library | `shelve` | `shelf` (genre); omit for any shelf |
| library | `compare` | `tome`, `page`: holding the object against a page |
| apothecary | `work` | `tool` and `with` are exact: no tool or no co-inputs means none; `"tool": "*"` means any |
| apothecary | `derive` | `derivation`: after a derivation runs |
| pools | `place` | `pool`, `light`, `heat` ranges |
| pools | `transform` | `transform`: when a soak completes |
| sanctum | `inspect` | `tool` (the magnifying glass) |
| sanctum | `seal` | `seal`: laying the object on a riddle-lock |

A reading with no `form` matches whichever form currently carries the essence. A reading with a remnant as
its `form` matches that remnant.

### Checklist for a fair object (AGENTS.md §2.1)

1. Each locale's readings on their own should point somewhere plausible but wrong. The truth should need two
   or more locales. The validator warns when a true claim's evidence comes from fewer than two.
2. Every term in the true designation must be granted by some reading (the validator enforces this).
3. Each plausible-wrong claim's `requires` should include the readings its near-miss refers to. Near-misses
   also need evidence from two locales.
4. Every required reading must come from a repeatable action on a recoverable form, so nothing can be
   permanently missed. Derivations always leave a remnant the Circle can restore from.
5. Wrong answers must cost nothing. Don't author consequences for them.

## Story beats, locales, the courtyard

- **Beats** (`story/beats.json`) fire once, in `order`, the first time `when` holds. They show as story
  cards and can carry a `payload`. Cozy milestones (every shelf settled, a big Discovery Map) are beats too,
  so a cozy-only player is rewarded.
- **Locales** (`locales.json`) have `unlockWhen`, `intro` (shown on the first visit), `unlockText`,
  `lockedDescription`, and `fallback` responses keyed by verb or `verb:tool`.
- **The courtyard** (`hub.json`) is one fixed scene: a `caption`, a `description` and `art` lines. Old Stoic is a
  big old tree, serene and unchanging, beside a lazy waterfall, so the scene has no conditions or stages. In the
  terminal game, `~ ¦ : .` draw as water, `| / \ _` as bark, and everything else as leaves.

## Open design questions as tunables

| Question (AGENTS.md §8) | Where |
|---|---|
| 2. Funnel strength | `config.funnel`: `off`, `gentle` or `strong` (strong adds each reading's `funnelHint`) |
| 3, 11. Minerals/auras and where they go | `kind: mineral`/`aura` items, `spawnRules`, `config.mineralDestination` |
| 4. Taxonomy vocabulary scaling | `config.lexiconMode`: `global`, `perObject` or `globalFiltered` |
| 5. Tick economy | `config.secondsPerTick`, `sitTicks`, `maxTicksPerStep`, `offlineProgress`, `offlineCapTicks`, `tickDisplay`. All costs are in content. |
| 6. Tome content gating | each tome's `available`; `loose` pages; payload `tomes`/`pages` |
| 9. Creature death | not implemented: creatures sulk. The data model has room for a later state. |
| 10. Library shelving model | `libraryConfig.shelvingModel`: `tagged` (Milanote) or `prelabeled` (prototype sigil shelves) |
| 12. Economy | no currency anywhere |
| 13. Breeding parent order | `breeding[].ordered` |
| 14. Locale tool lists | `tools.json` |
