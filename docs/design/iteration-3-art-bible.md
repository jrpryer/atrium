# Iteration 3: art bible (draft for the designer)

*Drafted 2026-10-25 and revised the same day after two independent reviews: one for identity and lore, one for
production and the engine. It plans an iteration that uses generated and hand-finished art instead of
iteration 2's all-procedural look. It's tool-agnostic: the same brief works with Higgsfield, another image
model, or a human artist. Nothing here is decided until the designer picks a direction (see
[Decisions for the designer](#decisions-for-the-designer)). Companion files:
- the asset list, [iteration-3-assets.md](iteration-3-assets.md);
- the prompt pack, [iteration-3-prompts.json](iteration-3-prompts.json);
- the paste-ready direction tests, [iteration-3-direction-tests.md](iteration-3-direction-tests.md).*

## Why a new look

Iteration 2 builds every mesh, texture and sound in code. That kept it standalone and easy to remove, but it
capped the look at clean low-poly primitives: pleasant, and interchangeable with a hundred other cozy games.
Atrium's lore has much more specific material than that. This bible turns the world's own rules into visual
rules, so the art could only belong to this game.

## The idea in one line

**The shaded floor at the very bottom of a city built down a cliff, where everything arrives from above, and
warmth and gold appear only where someone carried a light down or learned something.**

## Five pillars

### 1. The well of the city

The Atrium is the canyon floor under five tiers of city. The two kinds of view show that differently.

**Interior plates, story vignettes and key art** look *up* at least a little: a sliver of bright, busy city at
the top of the frame, and the courtyard or room in soft shade below.

**The courtyard camera** looks *down* at a fixed pitch (about 30°, AGENTS.md §6), so there the well shows in
other ways:
- the canyon walls rise out of the top of the frame in stacked strata, windows and stairs;
- things fall into frame: leaves, spray, ropes, the noon shaft;
- the Spawning Pond, "perfectly still", mirrors the lit tiers overhead. *The Keeper's Vigil*: "A still pool
  shows you the sky."

On the cliffs, each tier has its waystation tree, small and old, joined by the zigzag of the Long Stair coming
down. Old Stoic was the last of them.

**A corner of a large medieval city, not an island** (designer direction, 2026-10-25). The courtyard should feel
like a natural corner of a big medieval city:
- the city crowds its edges and climbs the cliff behind it, in timber-framed and stone houses stacked tier on
  tier;
- stairs zigzag down, with arched passages, little bridges, chimneys and washing lines;
- the four houses belong to that city, and the tree and pools sit in a quiet pocket of it.

Iteration 2's floating slab is the opposite of this. The Higgsfield courtyard brief
(`anchors.courtyard_scene` in the prompt pack) starts from it.

### 2. Light is carried down

Warm light in the Atrium always has a visible source and a route down:
- a lamp someone lit;
- a carried lantern;
- the noon shaft through the gap in the cliffs;
- a stone that drank the sun at the top of the city.

There is never an ambient warm fill. Warmth looks *brought*, the way the lampstone was carried down from the
Crown each midsummer, and the way news in Stairfall travels "downward, mostly, like water". Everything that
matters in chapter one came down too: the leaf, the gold, the pearl, the key and the silt in the falls.

The light at the canyon floor is the bright sky overhead: cool, soft and nearly shadowless. The noon shaft
reaches the Sunward Basin and the tree's crown for a few hours. The Shade Pool stays in the roots' shade at
every hour.

### 3. Gilt means known

Gilt is a **flat leaf, used only in the interface**, and only for knowledge. One gilt rule or edge goes on
paper the player has settled:

| What the player has done | Where gilt appears |
|---|---|
| Earned a term | The term in the journal |
| Settled a shelf | The shelf's genre tag |
| Made the newest discovery | That jar's label |
| Resolved a mystery | The resolution card |

**Gold matter is not gilt.** The nugget, the flakes, the gilded oil, sun-grit and the goldfish are painted as
warm, soft-edged colour with an inner glow: never as gilt line, and never with a gilt treatment. Any gilt stamp
in a book's cover description is painted as soot-dulled brass. This keeps the rule honest even though the
mystery's own matter is gold.

### 4. Made from, or only touched

A clue-object's essence has one visual tell, and the tell travels with it. The nugget's warmth moves into the
flakes, then drifts in the oil; the remnant left behind is dull.

When the Reconstitution Circle remakes an original, whatever was *made from* it goes quiet: the same shape, its
colour drained to grey, no glow. It is like the candle in *Chalk and Remnant* that "goes cold and will not light
again". Things it only *touched*, such as a Shadow Guppy or an opened Balance, keep their colour. This is the
game's thesis (matter is reclaimable, knowledge is permanent), made visible:
- every carrier has a **live** look and a **quiet** look;
- every catalysed outcome has only one look.

Elsewhere, *made* things show they've been used and mended: re-bindings, tea rings, string, a patch. Clue-objects
never get invented marks (see the review checklist).

### 5. Old Stoic tells the time

The tree is big, old, serene and unchanging (designer decision, 2026-10-24). Nothing about it ever changes.
**Its shadow moves.** *An Almanac of Shadow Lengths* opens: "There are no clocks in the Atrium that anyone has
wound." It tells the hour by where the tip of the tree's shadow falls, crossing the courtyard's landmarks in
order:

1. the far canyon wall;
2. the reed beds;
3. the nearest pool;
4. its own roots at noon;
5. the Library step;
6. the Apothecary door;
7. then up the cliff.

So:
- **The courtyard:** nothing is added to the ground. The hub layout puts those landmarks on the shadow's path,
  and the engine sweeps a soft shadow across them. The renderer casts no shadows today, so this is a projected
  decal driven by the clock (engine task E4).
- **The interface:** the clock medallion is a tiny plan of the courtyard with the tree's shadow laid across it,
  read in the Almanac's units: a hand's width, a pool's length. The core already uses those words.

The square nail-holes are part of the bark from the first frame, empty and dark inside, at the height of a
raised hand. What once filled them is a mystery the art must not answer.

## Reserved colours

| Colour | Reserved for | Never used for |
|---|---|---|
| **Gilt** `#D4A63A` (flat leaf, interface only) | Knowledge: earned terms, settled shelves, the newest discovery, resolutions | Objects in the world, clue-objects, book covers, decoration |
| **Slowsilver silver** `#C9D3D6`, warm-lit "like a spoon in candlelight" | What the Slowsilver once carried or touched: Lantern Birch veins, the sluice key's bit, silver silt, and the falls if they ever run silver again | Today's water, decoration, book stamps (painted as tarnished grey) |
| **Hem clay** `#B5553A` | Anything that passed through the Hem on its way down: red footprints, thumbprints, the Hem's gardens | Warnings or errors. There are none, and red never means wrong. |
| **Lamp amber** `#F2B04C` | Light someone carried down or lit | Ambient light, which is pale and cool |

## Palette

Starting values to test against the direction frames, not final.

**The shade (about 70% of any frame)**

| Name | Hex | Use |
|---|---|---|
| Cistern slate | `#2F3B44` | Deepest shadows, line in dark areas |
| Verdigris | `#4F8A7E` | Stone in shade, brass edges |
| Deep pool | `#1F5E63` | Water depth, pool rims in shadow |
| Canyon moss | `#6E8B4E` | Moss, foliage in shade |
| Damp stone | `#8C8F86` | Paving, walls |
| Fall mist | `#B9C4BC` | Atmosphere, distance, sky through spray |

**Paper and stone (about 20%)**

| Name | Hex | Use |
|---|---|---|
| Parchment | `#EFE3C8` | Cards, pages, the interface |
| Vellum | `#E3D6B4` | Old paper |
| Sandstone | `#C7B08A` | Sunlit cliff strata |
| Ink | `#2B2A33` | Line and text (never pure black) |

**Warm and water accents (about 10%)**

| Name | Hex | Use |
|---|---|---|
| Lamp amber | `#F2B04C` | Carried and lit light |
| Ember | `#D9653B` | Burners, cinders, the Blazing Glory |
| Hem clay | `#B5553A` | See reserved colours |
| Slowsilver duck-egg | `#A7D8CC` (to test) | The falls today, "the colour of a duck's egg held up to a window": pale, backlit, thin |
| Gilt | `#D4A63A` | See reserved colours (under 2% of a frame) |

**Locale accents** (kept from the overview sheet so the sheet and the game agree)

| House | Accent |
|---|---|
| Library | Library blue `#3D5AA8` |
| Spirit Pools | Pools teal `#1C8A84` |
| Apothecary | Apothecary violet `#8A55A8` |
| Sanctum | Sanctum ochre `#B0700F` |

## Shape language

- **Leaning, never plumb.** The city has settled for six centuries. Walls lean a degree or two, doors are out
  of square and stairs sag. Old Stoic's lean (about 15°) is the strongest diagonal in the courtyard.
- **Cupped shapes.** The votive sigil is a closed hand around a flame. Bowls, alcoves, the falls' lip and the
  round Sanctum repeat that cupping.
- **Stacked steps.** Tiers, stairs, shelves and cliff strata are stepped horizontals. The courtyard reads as the
  bottom stair.
- **Silhouette first.** Each house must be recognisable as a solid shape at thumbnail size, because there are no
  floating labels (AGENTS.md §6):
  - Library: tall, narrow and steep;
  - Pools: low, long and porched;
  - Apothecary: wide, with a chimney and a bowed window;
  - Sanctum: round and domed.
- **Used, not new.** Made things carry one sign of use or repair.

## Line, paint and texture

- **Technique.** Each direction defines its own medium (see below). They share:
  - flat, opaque colour shapes;
  - two or three value steps per material;
  - a coloured shadow (verdigris or cistern slate) instead of a darkened copy of the base colour;
  - no airbrushed gradients.
- **The line** is a warm brown-black (`#2B2A33`), heavier on silhouettes and lighter with distance.
- **Materials worth their own texture:**
  - sandstone strata;
  - wet stone with moss lines;
  - square-holed bark;
  - cloth and buckram;
  - verdigris brass;
  - glazed stoneware;
  - reed weave;
  - Hem clay;
  - chalk on slate;
  - water that is glassy in shade and pale duck-egg where the sun reaches it.
- **Shrine-wax is hard and glass-clear, never yellow.** Soft yellow wax is Hem tallow, and the pressed leaf's
  near-miss depends on the difference. It shows as a thin glassy highlight and a crisp edge, with whatever it
  seals fully visible beneath.
- **Detail goes where the eye should go:** hands' height, doors and work surfaces. Distant tiers are simple
  shapes with lit windows.

## Light through the day

The core has eight shadow-hours. The look keys to them. There are two kinds of lamp:
- **The iron lamp posts** are the Keeper's, lit every evening from the first night.
- **The four charter lamps** over the four doors stay dark (cold, clean wicks under glass) until the
  lamps-relit story beat. No art made for the time before that beat shows them lit. Relighting them is the
  courtyard's one sanctioned story change; the tree never changes.

| Hours | Sky above | Courtyard | Lamps |
|---|---|---|---|
| Early morning | Peach above the rim | Blue shade, mist on the pools | Lamp posts guttering |
| Mid to late morning | Pale gold | Shade; a bright band creeps down the cliff | Out |
| **Noon** | White-gold | **The noon shaft:** direct sun on the Sunward Basin and the tree's crown; the Shade Pool stays shaded | Out |
| Afternoon | Gold | The shaft leaves; warm bounce off the sunlit cliff | Out |
| Evening | Amber to rose | Deep shade; windows warm up | Lamp posts lit; charter lamps dark until relit |
| Night | Indigo; the city above like a second sky | Lamplight and lit windows | Lamp posts; charter lamps once relit |

Night is never gloomy. The city above becomes a wall of warm windows, the brightest thing in view. The painted
art carries no time of day of its own: the engine tints it from the clock (see "How the art meets the engine").

## The four houses (inside)

| House | Light | Materials | Signature detail |
|---|---|---|---|
| **Library** | One lamp at the end of the shelf wall (books run "oldest nearest the lamp"); dusty beams from a high window | Dark wood, cloth spines, brass rails, paper drifts | Ada's pencils, Mab's biscuit crumbs; the pile as a landscape of its own |
| **Apothecary** | The burner's blue flame; a bowed window full of backlit jars | Glazed stoneware, copper, string, ash | Corran's cracked formulary; blank paper jar labels (the engine writes the names) |
| **Spirit Pools** | Each tank wears its own pool's breath (below) | Wet stone, glass, reed, moss, damp cloth | Brother Alder's wet handprints on the glass; motes like slow sparks |
| **Sanctum** | Candles and the Circle's faint glow; a round window above | Cedar, chalk, brass, slate | The Balance over its reliquary; the framed charter on the wall, its seal in red wax; a chalk circle so neat it reads as floor pattern |

**The Spirit Pools show the two breaths.** Each tank stands for its own pool and wears that pool's light. That
way Light/Dark and Hot/Cold can be read at a glance:
- **Sunward tank:** warm, broken morning light and falls-spray, with a branch shadow across it.
- **Shade tank:** green, tired light filtered through roots, with a root breaking through the wall behind it.
- **Spawning Pond tank:** glass-still, ringed with flat stones, with no caustics.
- **Light and Dark** show in the light on the water. **Hot and Cold** show in the glass: a steam haze for hot,
  frost feathering at the rim for cold. The engine tints these from each pool's live state.

**People.** The Keeper is never shown: never named, never gendered, with no hands, body, cast shadow or
reflection. The Keeper acts off-frame: things arrive, lift and settle. (The designer may choose a presence
instead; see D4.) Whether the other people (Ada and Mab Quire, Tamsin Pell, Brother Alder) get portraits is the
designer's call (D3). If they do, they're portrait cards, not figures placed in scenes.

## Interface

- **Paper you could hold.** Parchment cards with deckled edges, faint grain, ink-ruled lines and a
  locale-coloured edge. There's no glassy panel and no pure white anywhere. Cards are built from tileable deckle
  strips and corners, so the edges stay crisp at every size (from a 420-pixel notice to the 1,260-pixel reader).
- **The journal** is a physical book: cloth cover, ribbon tabs, pencil for notes, gilt for what's settled.
- **Sigils and civic marks are drawn by hand as vectors, never generated** (image models garble small symbols):
  - the five shelving sigils and the votive hand around a flame;
  - the Crown's seal (the falls, the tree and four lamps), drawn from the hub's own silhouette so the seal is
    visibly the Atrium;
  - the civic marks *Signs and Sigils of Stairfall* already defines: the Assayers' level scales, the waterworks'
    three bars over a wave, and the glassmakers' teardrop.

  The seal is evidence (it's read off the sunstone's base and the key's bow), so it must be the same drawing
  everywhere. It is stamped onto covers, the charter and tags in paint-over.
- **No generated lettering anywhere.** Every title, label, sign and page of text is set by the engine.
  Text-bearing surfaces (journal pages, labels, the shop sign, the map) are generated blank and described
  positively ("blank cream pages", "an unpainted signboard"). Every keeper passes an automatic text check.
- **Fonts (SIL Open Font Licence, so they can ship):**

  | Role | Font | Status |
  |---|---|---|
  | Body and book text | EB Garamond | Already bundled |
  | Book titles | IM Fell English | To add |
  | The Keeper's handwriting | Caveat | To add |

  Adding Caveat replaces iteration 2's reliance on system fonts that can't be redistributed.

## How the art meets the engine

The art can't simply be dropped into iteration 2. These engine tasks come first, and each is small and
testable:

| Task | What it does | Why it's needed |
|---|---|---|
| **E1. Textured cel shading and mesh import** | Adds a texture sampler to the toon shader that keeps the cel bands. World-space triplanar mapping covers the slab, cliff and walls (no UVs needed); UVs are added in MeshBuilder only where a texture must follow the surface (roofs, bark, doors). An import step for glTF (`.glb`): rejects any part over 65,535 vertices (raylib meshes have 16-bit indices); sets the toon and outline shaders on every material (raylib leaves material 0 as an unused default); rebuilds smooth normals into the tangent channel for the ink outline; checks scale (1 unit = 1 m), pivot and facing. **Status (2026-10-25): the sampler and the import step are in** (`Render/ModelAsset.cs`); the import also names each mesh after its part, reads the markers, turns linear base colours back to sRGB and builds mipmaps. Scale, pivot and facing are checked by the Blender kit, not at import. **Still to do:** triplanar mapping and MeshBuilder UVs for the built-in courtyard. | Before E1 the toon shader read vertex colour only and MeshBuilder made no UVs, so no surface could take a texture, and imported models bypassed the cel shading and ink entirely. |
| **E2. A 2D stage for each room** | Sprite layers, 2D hotspot outlines, a hover outline from each sprite's alpha, and the hop as a 2D offset. One task per room. | The rules and working cards carry over unchanged, but the staging doesn't: hotspots today are 3D boxes picked by ray, and books, jars, fish and the Balance are 3D draws. |
| **E3. Courtyard backdrop** | Long painted strips mapped onto curved cards that follow the canyon's arc. The engine tints them from the clock's sky and fog colours and lights a hand-painted window mask at night. | The courtyard camera orbits a full 360°, so flat cards would be seen edge-on. Baked lighting states can't follow the continuous day cycle. |
| **E4. The tree's shadow** | A soft projected shadow decal, swept by the clock across the Almanac's landmarks. | The renderer casts no shadows. |
| **E5. Art loading** | Looks up files by content id and falls back to iteration 2's procedural art when a file is missing, so content never waits on art (AGENTS.md invariant 8). Bleeds edge colour into transparent areas, builds mipmaps with trilinear filtering, and loads compressed DDS textures for plates and backdrops. Keeps only the current room's plate in memory. | Without mipmaps, icons drawn small shimmer; uncompressed plates cost about 32 MB each. |
| **E6. Living layers and quiet states** | Separate painted layers that the engine animates and switches off: glow, smoke and drifting flecks. A "quiet" shader for inert items (desaturate and wash toward parchment), with painted inert icons for the four forms content names. A still frame for every animated layer when reduce-motion is on. | Iteration 2 already animates these and greys inert things. A flat painting would lose both cues. |

**Ids are filenames.** Content assets are named by their content id (`items/gold-nugget.png`,
`spines/the-ledger-of-tides.png`, `lifeforms/shadow-guppy-side.png`). Tools that live in the satchel are items
too, so they go under `items/`.

**Formats.** Painted art is unlit albedo: the shader does the lighting. Other formats:
- textures: PNG with mipmaps;
- plates and backdrops: DDS (DXT1 opaque, DXT5 with alpha), which raylib loads natively;
- props and icons: PNG with alpha, cut with a matting model;
- meshes: optional, glTF through the E1 import step.

**The courtyard keeps iteration 2's geometry and gets it textured.** Replacement meshes are optional (P3). They
have to come in the parts the hub draws separately:
- body, window glass (lit at night) and shutters (shown while a house is locked);
- for the tree, trunk and canopy apart (only the canopy sways);
- a marker for each lamp.

All four houses, Old Stoic and the four charter-lamp posts are in. The diorama's `Assets/visuals.json` lists each
one under `hub.models` with its scale in courtyard units per metre. A piece that isn't listed, or whose file is
missing, keeps its built-in shape. The Spirit Pools lodge stands further round than its built-in shape, between
the small basin and the Apothecary, because the long, low lodge doesn't fit its old slot.

They're built as scripts in Blender (`tools/blender/`). Raw image-to-3D output isn't imported as it comes: it
fuses those parts, invents the backs the orbiting camera will see, and breaks the ink outline. **One exception,
approved by the designer (2026-10-25):** Old Stoic is the Tripo multi-view lift itself, refined by a script that
splits it into trunk and canopy, turns, scales and re-textures it. It was lifted from four turnaround views, so
its back isn't invented from a single image, and the designer preferred it to the scripted rebuild.

## Three directions to test

All three follow the pillars above. They differ in medium. Generate the eight direction tests in each
([iteration-3-direction-tests.md](iteration-3-direction-tests.md) has every prompt ready to paste), then pick.

| | A. Lamplit Ink (recommended) | B. The Pop-up Stair | C. Kiln and Glaze |
|---|---|---|---|
| **Pitch** | Gouache and ink, like plates from an old naturalist's field book: observed, not whimsical | The city as a paper theatre: layered cut card, folds, string, glue | A tabletop museum model: glazed ceramic, carved and painted wood |
| **Strong because** | It's the medium the lore already uses: Hollis Quire's sketches, Dr. Lisle's herbarium plates, the breeding ledger. It holds up from icon size to a full scene. | It ties the whole world to the Library, and it's very distinctive | It's closest to iteration 2's 3D; the courtyard geometry carries over almost as is |
| **Risks** | Painted illustration is what image models produce by default. Without the lore-specific rules it will look like every other generated storybook. | Small items and creatures can lose readability as paper; paper-craft has been done before | It can slide back toward a generic "cozy toy" look; glaze highlights fight the gilt rule |
| **Engine fit** | Textured 3D courtyard, 2D painted rooms | 2D layered courtyard and rooms (the 3D hub would become parallax planes) | Fully 3D; the most mesh work |

## Process

1. **Direction tests.** Eight frames per direction. The two courtyard frames are painted over an engine capture
   from the courtyard's real camera, so we judge the view the game actually shows.
   - **The line-up test:** show each direction's courtyard and object frames, interface removed, beside
     screenshots of four other cozy games. A direction passes only if someone outside the team picks out
     Atrium and can say why.
   - The designer picks a direction, or a blend.
2. **Lock the style.** A style sheet for the chosen direction, plus three or four approved anchor images for each
   template (icons, plates, textures, the interface). Each kind of asset needs its own anchors. Anchors live in
   `art/reference/`.
3. **Engine tasks E1, E5 and E6**, so the art can be judged on screen rather than as loose images.
4. **Anchors in the engine.** The courtyard textures and backdrop, and one complete room, judged on screen.
   Revise this bible from what we learn.
5. **Train a style adapter** (a LoRA, or the tool's own style feature) on 20 to 40 approved keepers, if the tool
   allows it. Every later batch runs through it. It's retrained only at a review gate, never mid-batch.
6. **Content batches, by template:** item icons, lifeforms, spines, then story vignettes. Each batch passes
   the automated gate, then a contact-sheet review at in-game size beside its anchors, before the next starts.
7. **Polish:** page vignettes, key art, and a trailer captured in the engine.

**How consistency is kept across about 350 images:**
- **Fixed anchors.** Every generation in a template uses the same fixed anchor set. It never chains from the
  previous keeper, because chaining compounds drift.
- **Engine structure references.** Anything with geometry (plates, props, the courtyard) also gets a structure
  reference captured from the engine (`--capture`), so the camera and light are the game's own.
- **One image per room.** Each room is generated *fully furnished*. Each piece of furniture is then cut out to
  its own layer and the wall behind it repainted, so every prop shares the room's perspective and light.
- **Seeds.** They're recorded for the audit trail. Hosted models change underneath you, so seeds aren't relied
  on for consistency.

## Review checklist (every asset)

- [ ] **Shows nothing a reading would surface.** Art may show what a form's own description says, never what a
  reading says, and never assumes which derivation the player did first. Check the object's readings in
  `content/mystery/objects/*.json` and the beat's `when` in `content/story/beats.json` before approving.
- [ ] **Clue-objects and remnants are drawn exactly as described.** No added holes, cords, chips, stains,
  engravings or repairs: on a clue-object an invented mark is invented evidence.
- [ ] **Shrine-wax is clear.** Anything soft and yellow is tallow.
- [ ] **Readable at in-game size,** checked on a contact sheet at that size: icons around 40 px, spines around
  20 px wide.
- [ ] **Palette:** about 70% shade family, warm only where light was carried or lit. Checked automatically by a
  palette histogram.
- [ ] **Clean:** no lettering (automatic text check), no logos, no modern objects; no gilt outside the
  interface's knowledge marks; no silver outside what the Slowsilver carried.
- [ ] **No shadows or lighting baked into isolated assets or textures.** The engine draws them.
- [ ] **The Keeper isn't shown** (D4).
- [ ] **Provenance recorded** (below).

## Provenance and licensing

For every shipped file, `art/manifest.json` records:
- the file path and its sha256;
- the template and content id;
- the tool, the model name and exact version as reported, and the account plan;
- the prompt, negative and seed;
- each reference image's path, sha256 and origin: an own keeper, an engine capture or a hand drawing. Never
  third-party art, and never an artist's or studio's name in a prompt.
- the post-processing chain: upscaler, matting model, inpaint passes, and minutes of paint-over and by whom;
- who approved it, and when.

Raw outputs are archived under `art/raw/<id>/`, with a snapshot of each tool's terms on the day of use under
`art/licences/`. That makes the art auditable. It doesn't make it regenerable, because hosted models change, so
the raw outputs and layered sources are what we keep.

**Licensing comes before step 1,** because the first test frames become references for everything after them.
- **Check the terms for each tool and plan tier.** An aggregator such as Higgsfield routes to third-party
  models, so both the platform's terms and the underlying model's can apply.
- **Record human edits.** Unedited AI output generally can't be copyrighted (US Copyright Office guidance), so
  edits are worth recording.
- **Steam disclosure.** Steam requires a disclosure of AI-generated content; draft it from the manifest.
- **Trailer.** Shoot it in the engine with a scripted camera, which the game already supports. Generated video
  is for internal mood boards only: a video model would morph Old Stoic and show things the game can't do.

## Decisions for the designer

| # | Question | Default if you'd rather not decide yet |
|---|---|---|
| D1 | Direction A, B, C or a blend? | A, Lamplit Ink, if it passes the line-up test |
| D2 | Rooms as painted 2D scenes (as AGENTS.md §6 says) or iteration 2's 3D rooms, textured? | **Decided (designer, 2026-10-26): textured 3D rooms**, built in Blender from one cheap prop sheet per room, to keep generation credits low. Painted 2D stays an option for later. |
| D3 | Do the people (Ada and Mab, Tamsin, Brother Alder) get portraits? | Not yet. Their voices carry them. |
| D4 | Does the Keeper ever appear? | No hands, no shadow; the Keeper acts off-frame. The alternative is gloved hands at the frame's edge, always the same worn, patched gardening gloves (the Keeper already wears gloves: shade-salt feels cold "even through your gloves"). |
| D5 | Is silver held back for what the Slowsilver carried, including its return? | Yes |
| D6 | Which tools, and what generation budget? | Test frames first (about 100 generations), then decide |
| D7 | Are the tools' licence terms acceptable for a shipped build? | Check before step 1 |
| D8 | Should cover colour hint at genre? Today 38 of 42 cover descriptions track their genre's colour (with four deliberate exceptions), and the reader and satchel also show genre colour. | Leave the content as it is, and ask before generating spines |
| D9 | Is lamplit ink brown or gold? The leaf and key readings and *The Same Hand* say the vow and the margin note are in "the same brown ink"; page 3 of *The Waterworks of Stairfall* says "gold writing comes up". | Resolve in the content first. Either way, the vow and the note must use the identical ink so the comparison can be seen. |
| D10 | The Spirit Pools room: tanks that stand for the courtyard pools (as in iteration 2), or the pools themselves seen at water level? | **Decided (designer, 2026-10-26): the pools themselves, in a hidden grotto.** The falls' water breaks in high in the back wall, rests in three natural rock pools among Old Stoic's roots, and leaves as an underground river. In the courtyard, the stream runs from the plunge pool into a cave mouth (the Spirit Pools' entry, gated and its lamp dark while locked); the lodge is gone. Concepts: `art/reference/concepts/`. |
