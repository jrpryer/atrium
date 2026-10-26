# Iteration 3: asset list

*Companion to the [art bible](iteration-3-art-bible.md). Generated on 2026-10-25 from `content/` and [iteration-3-prompts.json](iteration-3-prompts.json), so it covers every item, creature, tool, tome and story beat in the current build. When content changes, the iteration-3 pipeline regenerates it.*

Priorities: **P0** decides the look, **P1** puts the identity in place, **P2** fills in content, **P3** is polish and optional.

**Sizes.**

- **Native** is what a model generates: most produce about 2 megapixels, so larger sizes are upscales.
- **Shipped** is what the game loads. It is set by the 1600×900 interface scaled up to 2.4× at 4K.
- **Formats:** plates and backdrops ship as DDS (compressed, which raylib loads natively). Everything else ships as PNG with mipmaps.
- **Memory:** only the current room's plate is held in memory.

## Summary

| Group | Count | Priority | Shipped size | Template |
|---|---|---|---|---|
| Direction test frames | 8 per direction × 3 | P0 | 1920×1080 | `direction_tests` |
| Style sheet and per-template anchors | 1 + 3–4 per template | P0 | varies | `style_sheet` |
| Courtyard backdrop strips (curved, wrap the canyon) | 4 | P1 | 4096×1024 DXT5 | `hub_backdrop` |
| Window masks for the city layers | 3 | P1 | 4096×1024 | `window_mask` |
| Courtyard textures (unlit albedo) | 15 | P1 | 1024² + mipmaps | `texture_tile` |
| Room masters, then clean plates | 4 | P1 | 3840×2160 DXT1 | `room_master` |
| Room cut-outs (from the masters) | 48 | P1 | as cut | `room_master` (cut) |
| Interface pieces | 15 | P1 | as listed | `ui_piece` |
| Sigils and civic marks | 10 | P1 | vector | drawn by hand, never generated |
| Item icons | 38 | P2 | 320² in a 2048 atlas | `item_icon` |
| Clue icons, live | 11 | P2 | 320² | `clue_icon` |
| Clue icons, quiet (inert) | 4 | P2 | 320² | `clue_icon` |
| Tool icons (saved as items) | 11 | P2 | 320² | `tool_icon` |
| Lifeforms: ledger portrait, side sprite, top sprite | 10 × 3 | P2 | 512² / 256² / 256² | `lifeform_*` |
| Book spines | 42 | P2 | 96×768 | `book_spine` |
| Pile tops (the book seen from above, small) | 42 | P2 | 256×180 | `pile_top` |
| Story vignettes | 12 | P2 | 1600×1000 | `story_vignette` |
| Page vignettes (poems, letters, recipes, tables, lists, marginalia) | 30 | P3 | 900×600 | `page_vignette` |
| Book covers, face-on | 42 | P3, only if a view shows them | 768×1024 | `book_cover` |
| Replacement meshes (tree, houses, lamp post) | 0 at P1: iteration 2's geometry stays and gets textured | P3, optional | — | `hero_turnaround` (reference only) |
| Key art | 2 | P3 | 3840×2160 | `key_art` |
| Trailer | 6 shots | P3 | captured in the engine | none (generated video is for mood boards only) |

## Budget

| Template | Keepers | Attempts each | Generations |
|---|---|---|---|
| Direction tests (P0) | 24 | 4 | 96 |
| Style sheet and template anchors | 10 | 1 | 10 |
| Room masters (fully furnished) | 4 | 12 | 48 |
| Room cut-outs and inpainting | 48 | 2 | 96 |
| Backdrop strips | 4 | 10 | 40 |
| Window masks | 3 | 3 | 9 |
| Textures | 15 | 6 | 90 |
| Item icons | 38 | 5 | 190 |
| Clue icons, live and quiet | 15 | 5 | 75 |
| Tool icons | 11 | 5 | 55 |
| Lifeforms (portrait, side, top) | 30 | 5 | 150 |
| Spines | 42 | 4 | 168 |
| Pile tops | 42 | 3 | 126 |
| Story vignettes | 12 | 10 | 120 |
| Page vignettes | 30 | 4 | 120 |
| Interface pieces | 15 | 4 | 60 |
| Key art | 2 | 20 | 40 |
| **Total** | **345** | | **1493** |

Plan for **1400–1800 generations** with retries after the review gates. P0 alone is about 100.

On top of generation, allow:

- text-removal inpainting on text-bearing pieces;
- tile seam fixes, upscales and matting;
- roughly **70–100 hours of human cleanup** (edge checks, paint-over, stamping the vector sigils, and import and QA in the engine).

## States the game already draws

Each state is either **art** (a painted file or layer) or **engine** (a shader, tint or transform over the painting).

| Where | State | Art or engine |
|---|---|---|
| Items | Quiet (inert) look for the four forms content names | Art: a painted quiet icon |
| Items | Quiet look for any other inert item | Engine: desaturate and wash toward parchment |
| Items | Glow, smoke and drifting flecks (nugget, pearl, sunstones, gilded oil, smoke bag, lantern, crystals) | Art layers, animated and switched off by the engine; a still frame for reduce-motion |
| Courtyard | A house locked (shutters closed, greyed) | Art: a shutters part; engine: the grey tint |
| Courtyard | Windows lit at night; lamp posts lit at dusk | Engine: emissive on the glass part |
| Courtyard | Charter lamps dark, then relit at lamps-relit | Engine: switched by the beat |
| Courtyard | Time of day | Engine: tints the backdrop and lights the window masks |
| Courtyard | Old Stoic's shadow sweeping the landmarks | Engine: projected decal (E4) |
| Courtyard | Creatures in the pools | Art: top sprites; engine: swim paths |
| Courtyard | Basin rims, benches, crates, barrels, lily pads, stairs, the falls | Engine: existing geometry, textured; the falls scroll a strip texture |
| Library | A shelf tagged with a genre; a settled shelf | Art: one blank plaque with a tint mask; engine: genre colour and the settled glow |
| Library | A book on loan | Art: a loan card in the returns box |
| Apothecary | A jar per known element | Art: one jar shell and a substance mask; engine: tints the mask only |
| Apothecary | The newest discovery | Engine: a gilt edge on that one label |
| Apothecary | The vessel filling and swirling | Art: a liquid layer; engine: tint and swirl |
| Spirit Pools | One tank per pool | Art: one tank in parts; engine: placed per pool |
| Spirit Pools | Light/Dark, Hot/Cold and murk in each tank | Engine: water tint, steam haze, frost at the rim, murk |
| Spirit Pools | Sulking, expecting, a hatching | Engine: tint, scale and a pop (sprites painted in neutral light) |
| Sanctum | One Balance and reliquary per seal; shut or open | Art: beam, pans and lid as parts; engine: tilt and swing |
| Sanctum | The Circle dormant, then awake | Art: a glow layer; engine: fades it in on circle-wakes |
| Sanctum | The cabinet door | Art: a door part |
| Interface | Buttons: plain, hover, pressed and disabled | Art: four states |
| Interface | Panels from a 420 px notice to the 1,260 px reader | Art: tileable deckle strips and corners, drawn tiled (not 9-sliced) |

## Courtyard

| Asset | Notes |
|---|---|
| Backdrop: `backdrop-hallows` | the highest, farthest layer: the snowy mountain shrines of the High Hallows and the top of the Long Stair, tiny and pale |
| Backdrop: `backdrop-crown-gallery` | the upper tiers: the grand, slightly forgetful Crown with its charter office, and below it the Gallery's markets and glassworks; at one side three great sluices of oak and iron, water pouring through the two nearest the cliff while the outermost stands shut and dry, moss on its gate; each tier's small, old waystation tree, joined by the zigzag of the Long Stair |
| Backdrop: `backdrop-ribs-hem` | the lower tiers: cistern arches carved into the rock with tiny sealed lamps glowing deep in their mouths, kitchen gardens on ledges, washing lines, red clay paths, small mourning houses, each tier's waystation tree, and the Long Stair zigzagging down toward the courtyard |
| Backdrop: `backdrop-near-cliff` | the nearest cliff edges framing the canyon: stair landings, rope bridges, and pilgrim cords and lanterns hanging down into view |
| Lighting | None baked. The engine tints each layer from the clock and lights the window masks at dusk. |
| Textures | `tex-sandstone`, `tex-wet-stone`, `tex-moss-stone`, `tex-hem-clay`, `tex-paving`, `tex-stoic-bark`, `tex-canopy`, `tex-roof-slate`, `tex-roof-thatch`, `tex-roof-violet`, `tex-dome-ochre`, `tex-plaster`, `tex-timber`, `tex-water-flow`, `tex-falls-strip` |
| Effects | Foam, mist puffs, the noon light shaft, lamp glow, dust mote |
| Replacement meshes (optional, P3) | Each ships as the separate parts the hub draws: body; window glass (lit at night); shutters (shown while locked); for the tree, trunk and canopy apart (only the canopy sways); a `lamp` marker. One material per part, at most 5,000 triangles per house and 15,000 for the tree, no lighting baked in, 1 unit = 1 m, pivot at the base centre, door facing +Z. Modelled or retopologised by hand; raw image-to-3D output is never imported. |

## Rooms

Each room is generated once, fully furnished, from an engine capture of its real camera. The furniture is then cut out and the plate behind it repainted.

| Room | Clean plate | Cut-outs |
|---|---|---|
| Library | Inside the Library, a tall narrow stone house: a bare panelled back wall with a long rail for bookcases, a brass lamp on the wall at the left end, a high window with dusty beams, worn floorboards and a faded rug. | bookcase, tag plaque (blank, with a tint mask), reading table, book pile (small, medium, tall), returns box, loan card, rolling ladder, globe, biscuit tin, pencil cup, candle |
| Apothecary | Inside the Apothecary, a low plaster shop: a wall of shelves above a row of small drawers, a long wooden bench, a bowed front window, a chimney breast, ash on the floorboards. | jar shell, jar substance mask, copper vessel, vessel liquid surface, brass tray, mortar and pestle, spirit burner, burner flame, balance body, balance pans, reading glass, still, map frame (blank paper; the engine draws the map), formulary on lectern, crate of bottles |
| Spirit Pools | Inside the Stillkeepers' wet stone room beside the falls: arched windows overgrown with ferns, wet flagstones, a trickle of water running down the wall, a long stone ledge where tanks can stand. | tank rim, tank glass, tank water (neutral; the engine tints it per pool), holding crock, druids' bag, breeding ledger on its stand, stool, lantern, hand rake |
| Sanctum | Inside the Sanctum, a round stone study: a round window above, panelled walls, a slate floor with a chalk circle worked so neatly into its pattern it could be decoration, wall sconces. | desk and journal, framed charter, reading glass on stand, assembly bench, cabinet body, cabinet door, balance stand, balance beam, balance pans, reliquary body, reliquary lid, circle glow layer, candle |

## Interface

| Id | Piece |
|---|---|
| `ui-deckle-edge` | a long strip of cream parchment edge, softly deckled, tileable along its length |
| `ui-deckle-corner` | one corner of a sheet of cream parchment with a softly deckled edge |
| `ui-paper` | a plain sheet of cream parchment with faint grain, tileable |
| `ui-button` | a small rounded tab of thick cream card with a fine inked edge, in four states: plain, lifted, pressed and greyed-out |
| `ui-journal` | an open hand-bound journal spread, cloth cover re-bound along the spine, blank cream pages |
| `ui-tab` | one cloth ribbon tab in undyed linen (the engine tints and repeats it) |
| `ui-satchel` | a long worn leather satchel strap with stitching and a brass buckle, seen face-on |
| `ui-satchel-tile` | a small square pocket of soft leather, seen face-on |
| `ui-medallion` | a plain round brass medallion with a recessed blank face (the engine draws the courtyard plan and the shadow) |
| `ui-wax` | a blank round wax seal in deep red, glossy, with a soft drip |
| `ui-slip` | a narrow torn slip of parchment |
| `ui-tooltip` | a small dark ink-blue card with a cream edge |
| `ui-close` | a small round brass button with a simple cross |
| `ui-cursor` | a short sharpened pencil, pointing up and to the left |
| `ui-reader` | an open old book spread, deckled pages, a ribbon marker, blank pages |

Sigils and civic marks, drawn by hand as vectors:

- the five shelving sigils;
- the votive hand around a flame;
- the Crown's seal (the falls, the tree and four lamps, drawn from the hub's own silhouette);
- the Assayers' level scales, the waterworks' three bars over a wave, and the glassmakers' teardrop.

## Item icons (38)

| Id | Name | Kind | Brief |
|---|---|---|---|
| `brine` | Brine | Ingredient | a jar of clear water with a white rim of salt crust |
| `clay` | Clay | Ingredient | a lump of smooth red-brown clay with thumb marks |
| `dew` | Dew | Ingredient | a small jar with a few bright, cold beads of water clinging inside its glaze window |
| `fired-clay` | Fired Clay | Ingredient | a small lopsided terracotta pot |
| `green-shoot` | Green Shoot | Ingredient | a small clay pot with a single bright green shoot |
| `grog-sand` | Grog Sand | Ingredient | a wooden scoop of fine red sand |
| `hearth-ash` | Hearth Ash | Ingredient | a glazed stoneware jar of soft grey ash, a little spilled over the lip |
| `lamp-glass` | Lamp Glass | Ingredient | a curved piece of greenish, slightly bubbled lamp-chimney glass catching the light |
| `loam` | Loam | Ingredient | a jar of dark crumbly earth with a clod beside it |
| `lye` | Lye | Ingredient | a stoneware jar of pale yellow-green liquor, its cork eaten ragged |
| `morning-mist` | Morning Mist | Ingredient | a jar holding a soft white haze, stoppered with a cloth |
| `mud` | Mud | Ingredient | a jar of wet brown mud with a smear down its side |
| `patience` | Patience | Ingredient | an hour-candle marked in thumb-widths, burning in a small dish |
| `potash` | Potash | Ingredient | a jar of white crusted salt-like powder, a flake of crust on the lid |
| `pressed-oil` | Pressed Oil | Ingredient | a small stoppered bottle of golden oil |
| `reed` | Reed | Ingredient | a bundle of tall hollow reeds tied with twine |
| `reed-fibre` | Reed Fibre | Ingredient | a loose skein of long pale reed threads |
| `reed-seed` | Reed Seed | Ingredient | a little heap of small dark oily seeds on a scrap of paper |
| `reed-twine` | Reed Twine | Ingredient | a coil of strong grey reed cord |
| `shrine-wax` | Shrine-Wax | Ingredient | a few hard, glass-clear flakes of wax on a scrap of paper (clear, never yellow) |
| `soap` | Soap | Ingredient | a rough cake of pale soap tied with string |
| `spring-water` | Spring Water | Ingredient | a stoppered glazed jar with a window showing clear cold water, a drop beaded on the cork |
| `steam` | Steam | Ingredient | a small stoppered flask fogged inside with warm vapour, a curl escaping past the cork |
| `stillwater` | Stillwater | Ingredient | a jar of dark, glass-still water, perfectly level, with a cool sheen |
| `waxed-reed-pouch` | Waxed Reed Pouch | Ingredient | an empty pouch of tightly woven reed twine sealed with clear wax, drawn closed |
| `charcoal` | Charcoal | Reagent | a few sticks of matte black charcoal |
| `cinder` | Cinder | Reagent | a jar with a glowing coal inside, warm light showing through its glaze window |
| `cold-brine` | Cold Brine | Reagent | a jar of brine with frost feathering its outside |
| `distilled-dew` | Distilled Dew | Reagent | a tiny stoppered vial of perfectly clear water with frost at its neck |
| `lampblack-ink` | Lampblack Ink | Reagent | a small bottle of deep black ink |
| `spirit-of-cinders` | Spirit of Cinders | Reagent | a small stoppered vial with a warm orange shimmer inside, wrapped in a cloth |
| `votive-candle` | Votive Candle | Reagent | a small clear candle burning in a bowl of water |
| `clear-spirit` | Clear Spirit | Pond food | a small stoppered vial of very clear water |
| `reed-meal` | Reed Meal | Pond food | a wooden scoop of coarse brown-green meal |
| `shade-salt` | Shade-Salt | Mineral | a few blue-grey crystals in a twist of paper |
| `silver-silt` | Silver Silt | Mineral | a pinch of fine silver silt in a twist of paper, softly glittering |
| `sun-grit` | Sun-Grit | Mineral | a pinch of warm golden grit in a twist of paper |
| `sun-mote-aura` | Sun-Mote Aura | Aura | a stoppered jar of warm bright air glowing softly from within |

## Clue icons (11 live, 4 quiet)

Drawn exactly as briefed: no added holes, cords, chips, stains, engravings or repairs.

| Id | Name | Kind | Brief |
|---|---|---|---|
| `black-pearl` | Black Pearl | Clue-object form | a black pearl the size of a thumbnail, perfectly smooth, resting on a fold of dark cloth |
| `cold-sunstone` | Cold Stone | Clue-object form | a smooth, pale oval stone, a little translucent at the edges, with a cold blue-white sheen |
| `gilded-lamp-oil` | Gilded Lamp-Oil | Clue-object form | a small stoppered flask of honey-coloured oil with gold flecks drifting in it |
| `gold-flakes` | Gold Flakes | Clue-object form | soft, bright flakes of beaten gold in a twist of paper |
| `gold-nugget` | Gold Nugget | Clue-object form | a lump of gold the size of a thumb-joint, softly folded, as if pressed rather than found |
| `pressed-leaf` | Pressed Leaf | Clue-object form | a pale, slender leaf sealed flat under something clear and hard, its veins catching the light like threads of silver |
| `sluice-key` | Great Key | Clue-object form | a heavy iron key as long as a forearm, its bow plain and its bit bright silver, worn smooth |
| `smoke-in-a-bag` | Smoke-In-A-Bag | Clue-object form | a small waxed reed pouch drawn tight, bulging with slow grey smoke that shows darkly through the weave |
| `waking-sunstone` | Waking Sunstone | Clue-object form | the same smooth pale oval stone, warm now and faintly glowing from within with soft gold-white light |
| `iron-slag` | Iron Slag | Remnant | a square-shanked, tapering, hand-forged iron nail, dull black, with a flattened head |
| `pearl-husk` | Pearl Husk | Remnant | the dull outer half of a pearl, hollow as an eggshell |
| `gilded-lamp-oil:inert` | quiet form | Inert | the same small flask holding ordinary pale oil, a few dull specks at the bottom |
| `gold-flakes:inert` | quiet form | Inert | the same twist of paper with dull gold-coloured foil flakes, no glitter at all |
| `lamplighters-lantern:inert` | quiet form | Inert | the same brass lantern with a plain small yellow flame |
| `smoke-in-a-bag:inert` | quiet form | Inert | the same reed pouch gone slack, thin ordinary grey smoke leaking from the weave |

## Tools (11)

| Id | Name | Where | Brief |
|---|---|---|---|
| `mortar-and-pestle` | Mortar & Pestle | apothecary (fixed) | a heavy grey stone mortar and pestle, worn smooth by generations of hands |
| `bunsen-burner` | Bunsen Burner | apothecary (fixed) | a small brass spirit burner with a cloth wick and a blue flame, a brass snuffer cap beside it, no tubing |
| `weighing-scales` | Weighing Scales | apothecary (fixed) | an assayer's balance with two brass pans and a row of tiny brass weights |
| `loupe` | Magnifying Glass | apothecary (fixed) | a jeweller's loupe in worn horn |
| `spirit-still` | Stillkeeper's Still | apothecary (fixed) | a coil of greenish glass tubing on a clay stand |
| `magnifying-glass` | Magnifying Glass | sanctum (fixed) | a round reading glass on a long brass chain |
| `reconstitution-circle` | Reconstitution Circle | sanctum (fixed) | a ring of chalk drawn on a dark slate tile |
| `lamplighters-lantern` | Lamplighter's Lantern | sanctum (assembled) | a small brass lantern with a greenish glass chimney, burning a low honeyed flame |
| `stillkeepers-sieve` | Stillkeeper's Sieve | sanctum (assembled) | a round sieve of woven reed mesh in a fired-clay rim |
| `sun-lens` | Sun-Lens | sanctum (assembled) | a thick lens of greenish lamp glass set in a clay ring |
| `still-kit` | Still Kit | sanctum (assembled) | the parts of a small still bundled together: a glass coil, a clay pot and a cork, tied with twine |

## Lifeforms (10)

| Id | Name | Kind | Brief |
|---|---|---|---|
| `spotted-spinner` | Spotted Spinner | fish | a small round-bodied pond fish, orange with darker spots, fins like little fans |
| `blazing-glory` | Blazing Glory | fish | a small pond fish of flame red and orange with long trailing fins |
| `sun-spotted-goldfish` | Sun-Spotted Goldfish | fish | a small golden pond fish dappled with bright sun-coloured spots |
| `yellow-backed-snail` | Yellow-backed Snail | snail | a slow pond snail with a smooth butter-yellow shell |
| `grey-guppy` | Grey Guppy | fish | a small plain grey guppy with a neat fan tail |
| `shadow-guppy` | Shadow Guppy | fish | a small guppy so dark it is nearly black, with a faint blue sheen |
| `hush-moss` | Hush-Moss | plant | a soft cushion of dark green moss that grows underwater |
| `lantern-reed` | Lantern Reed | plant | a clump of slender reeds with tiny warm glowing tips |
| `sun-mote` | Sun-Mote | mote | a small warm golden spark of light with a soft halo |
| `frost-mote` | Frost-Mote | mote | a small pale blue spark of cold light with a soft halo |

## Spines and pile tops (42 each)

The cover description is the brief. The colour column is the word the game already picks out of the same text for the spine (LibraryScene.CoverColor), and QA holds each generated spine to it.

The genre column is here for decision D8. Most cover colours echo their genre; the four deliberate exceptions must survive generation.

| Id | Title | Year | Genre | Colour word | Cover |
|---|---|---|---|---|---|
| `a-census-of-nails` | A Census of Nails: Forty-One Holes and What I Think About T… | SR 640 | history | brown | brown cloth, so new the glue still smells of fish, with an inky thumbprint on the flyleaf |
| `a-keepers-bestiary` | A Keeper's Bestiary: Small Fire-Beasts and How to Apologise… | SR 502 | husbandry | smoke-blue | smoke-blue cloth, scorched along the bottom edge |
| `a-primer-of-tier-reckoning` | A Primer of Tier Reckoning: Counting Stairs So You Don't Ha… | SR 488 | mathematics | ochre | ochre buckram, a faded charter-office ribbon still marking the second page |
| `a-stillkeepers-primer` | A Stillkeeper's Primer: Pond Husbandry for the Restless | SR 598 | husbandry | river-blue | river-blue cloth worn pale at the corners, a flattened reed tucked inside the front board |
| `a-year-in-the-hem-gardens` | A Year in the Hem Gardens (and Most of the Mud) | SR 601 | horticulture | grass-green | grass-green cloth with rust-red thumbprints on every edge |
| `amber-and-its-imitations` | Amber and Its Imitations: How Not to Be Swindled in the Gal… | SR 588 | horticulture | bottle-green | bottle-green leather, a honey-coloured bead set into the spine |
| `an-account-of-the-lamps-relit` | An Account of the Lamps Relit, Corrected Twice | SR 640 | history | oxblood | oxblood cloth with perfectly square corners and a ruled paper bookmark |
| `an-almanac-of-shadow-lengths` | An Almanac of Shadow Lengths: Telling Time by Tree | SR 600 | mathematics | ochre | ochre cloth, sun-faded paler along one edge |
| `chalk-and-remnant` | Chalk and Remnant: Remaking Things – Adept | SR 410 | mysticism | violet | violet cloth gone grey with chalk dust, no lettering on the spine at all |
| `customs-of-the-hallows-pilgrims` | Customs of the Hallows Pilgrims: Packing Light for the Long… | SR 520 | history | oxblood | oxblood cloth, the boards stiff with mountain cold, a knotted cord for a bookmark |
| `dreams-of-the-stair` | Dreams of the Stair: Poems for Tired Knees | SR 612 | mysticism | violet | dusk-violet paper boards, the corners soft from years in a coat pocket |
| `feeding-the-small` | Feeding the Small: Recipes for Mouths You Can Barely See | SR 606 | husbandry | blue | faded blue cloth, flour-dusted, a dry reed stalk for a bookmark |
| `goldfish-of-the-gallery-fountains` | Fountain Society: The Goldfish of the Gallery | SR 579 | husbandry | blue | bright blue silk-finish cloth, a little fountain stamped in white on the front board |
| `kitchen-herbs-of-the-ribs` | Kitchen Herbs of the Ribs: Cooking on a Ledge | SR 622 | horticulture | oxblood | oxblood leather, re-bound after an accident with a stockpot; smells faintly of thyme |
| `letters-from-the-gallery` | Letters from the Gallery: Collected Complaints | SR 580 | history | brown | brown cloth, much thumbed, with a tea-ring on the front board |
| `locks-and-reliquaries` | Locks and Reliquaries: Keeping Things Shut, Spiritually | SR 566 | mysticism | violet | violet cloth, a small brass keyhole plate set into the front board, purely for show |
| `mosses-of-damp-places` | Mosses of Damp Places, Volume II: The Damper Places | SR 455 | horticulture | green | dark green buckram, softly speckled with mildew (on purpose, the author insists) |
| `motes-and-their-moods` | Motes and Their Moods: A Practical Guide to Things Too Smal… | SR 590 | husbandry | blue | deep blue cloth flecked with tiny pale dots, like dust in a sunbeam |
| `mourning-customs-of-the-hem` | Mourning Customs of the Hem: A Polite Guide to Grief | SR 597 | history | oxblood | oxblood cloth faded nearly to black, with a black ribbon marker |
| `old-stoic-a-biography` | Old Stoic: An Unauthorised Biography | SR 590 | horticulture | green | deep green morocco, worn bark-brown at the corners |
| `on-the-keeping-of-herbaria` | On the Keeping of Herbaria: Pressing Matters | SR 530 | horticulture | green | sage-green boards with brass corners, alarmingly clean |
| `on-the-slowsilver` | On the Slowsilver: An Oral History of a Waterfall | SR 560 | history | grey | brown cloth with a thin silver line stamped down the spine, gone grey with tarnish |
| `proofs-for-idle-afternoons` | Proofs for Idle Afternoons: Sums to Do in a Hammock | SR 611 | mathematics | mustard | mustard-yellow cloth with a tea ring on the back board |
| `signs-and-sigils-of-stairfall` | Signs and Sigils of Stairfall: A Beginner's Guide to Pointi… | SR 470 | mysticism | violet | violet cloth, the five shelving sigils pressed in a row down the spine |
| `snails-a-love-letter` | Snails: A Love Letter in Seven Slow Parts | SR 619 | husbandry | blue | pale blue cloth with a glossy trail looping across the front board, which the author insists is del… |
| `the-arithmetic-of-tides` | The Arithmetic of Tides: Cisterns, Sums, and Damp Socks | SR 593 | mathematics | blue | pool-blue cloth, re-bound after the original fell into a cistern |
| `the-charter-of-the-atrium` | The Charter of the Atrium (A Fair Copy, Mostly) | SR 233 | history | oxblood | oxblood leather, cracked at the hinge, the Crown's seal pressed faintly into the front board |
| `the-four-houses` | The Four Houses of the Atrium: Who Still Owes Whom | SR 630 | history | brown | brown cloth with chewed corners and a daisy pressed into the spine |
| `the-geometry-of-shelving` | The Geometry of Shelving: Oldest Nearest the Lamp | SR 625 | mathematics | ochre | ochre cloth ruled in fine brown lines, like a ledger |
| `the-guppy-fancier` | The Guppy Fancier's Companion: Grey Is a Colour Too | SR 604 | husbandry | blue | slate-blue cloth, quite plain, a small grey fish stamped on the spine |
| `the-keepers-vigil` | The Keeper's Vigil: On Doing Nothing Properly | SR 402 | mysticism | violet | plain violet cloth, very worn, a teacup ring on the back board |
| `the-lamp-divers-of-the-ribs` | Holding Your Breath for a Living: The Lamp-Divers of the Ri… | SR 615 | history | blue | blue oilcloth, water-stained, re-bound by someone who took it for a pond book |
| `the-lamps-of-the-charter` | The Lamps of the Charter: A Lamplighter's Handbook | SR 548 | history | oxblood | oxblood cloth, soot-dark at the corners, a small lamp stamped in gilt on the spine |
| `the-ledger-of-tides` | The Ledger of Tides | SR 571 | husbandry | blue | faded blue boards, water-stained, the brass clasp still fastened |
| `the-reed-and-its-uses` | The Reed and Its Uses: 101 Things That Are Not Baskets | SR 377 | horticulture | green | faded green cloth, the spine bound round with real plaited reed |
| `the-wardens-of-the-reach` | The Wardens of the Reach: Duties, Oaths, and Approved Hats | SR 534 | history | oxblood | oxblood boards with brass corners and a sluice-gate tooled on the front |
| `the-waterworks-of-stairfall` | The Waterworks of Stairfall: Sluices, Sums, and Silver | SR 530 | mathematics | ochre | ochre cloth, water-stained, a sluice-wheel stamped in brass on the front board |
| `the-year-of-quiet-water` | The Year of Quiet Water: Minutes of a Meeting Nobody Attend… | SR 545 | history | brown | brown boards gone soft at the edges, tied with a faded clerk's ribbon |
| `tithes-and-offerings-of-the-hallows` | Tithes and Offerings of the Hallows: What to Leave and Where | SR 498 | mysticism | violet | violet cloth spotted with old frost-marks, tied shut with a pilgrim's cord |
| `trees-of-the-upper-tiers` | Trees of the Upper Tiers: A Field Guide for the Chronically… | SR 512 | horticulture | moss | moss-green cloth, silver-stamped spine, a bootprint on the back board |
| `water-ash-and-patience` | Water, Ash, and Patience: A Beginner's Guide to Alchemy Wit… | SR 560 | mysticism | ochre | ochre buckram, re-bound after a small fire, the old violet cloth peeping out at one corner |
| `weights-and-measures-of-the-exchange` | Weights and Measures of the Exchange: Heavy Truths | SR 540 | mathematics | ochre | ochre buckram stamped with a pair of scales, heavier than it looks |

## Story vignettes (12)

Each is painted from the moment *after* its beat, and never shows what a reading would surface.

| Id | Title | Scene |
|---|---|---|
| `unkept-vow` | Word Down the Stair | evening on a long stone stair: a small wax-sealed note passed from hand to hand down the tiers of the city, from one lit doorway to the next, toward a dark cou… |
| `circle-wakes` | The Chalk Circle | a slate floor at night: a closed chalk circle worked so neatly into the floor's pattern it could be decoration, glowing faintly the way a hearth glows after th… |
| `gilded-nails` | Promises in the Bark | close on the bark of a great tree at a raised hand's height: a column of empty square nail-holes ringed with smooth dark bark; a ladder leaning on the trunk wi… |
| `smoke-pearl` | Not Yet | candlelight on an old brass balance standing over a sealed wooden reliquary; a torn scrap of old paper covered in faded, unreadable handwriting lies beside a b… |
| `reliquary-opened` | The Reliquary Opens | a brass balance level at last over an open cedar reliquary lined with worn velvet, a long empty hollow pressed into the velvet, dust turning in the candlelight. |
| `lamps-relit` | Lamps in Oil | night in the courtyard with the four old lamps over the four doors lit for the first time in eighty years; small figures on the last stairs from the tier above… |
| `silver-course` | Up the Canyon | a library table covered by an unrolled map of waterworks and sluices (drawn shapes, no writing), its corners weighted with teacups; an old book open beside it;… |
| `pilgrims-hand` | The Same Hand | two old pages side by side under a honeyed lantern, both covered in the same small handwriting drawn as unreadable flourishes, in identical ink; a pale pressed… |
| `chapter-one` | The End of the Beginning | a warm evening picnic under the great leaning tree, seen from the Keeper's own place on the blanket: a good teacup set down in the foreground, far too much foo… |
| `library-settled` | Every Shelf in Its Place | a long run of bookcases full and in perfect order, the lamp at the end of the run glowing, a single fingertip trail in the dust along the spines; quiet. |
| `apothecary-map` | A Map in the Window | a bowed shop window seen from inside in morning sun: a hand-drawn map of connected circles (no writing) pinned to the glass, sunlight shining through it onto j… |
| `pools-thriving` | A Brood of Broods | the edge of a sunlit pool: a crowd of small bright fish turning circles, butter-yellow snails on the stones, a druid's wet boots at the water's edge. |
