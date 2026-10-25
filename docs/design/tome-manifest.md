# Tome manifest (iteration 1)

Every tome is one Markdown file in `content/library/tomes/<id>.md`. The engine reads these directly. This list is
the authoring brief; the files are the source of truth once written.

## File format

```markdown
---
id: trees-of-the-upper-tiers
title: Trees of the Upper Tiers
author: Wenna Holloway
year: 512
genre: horticulture
cover: moss-green cloth, silver-stamped spine
blurb: A field guide to the trees that grow above the Gallery.
available: start
hint: Optional. Shown when the player tries the wrong shelf.
---

::: page prose | The Lantern Birch
Paragraphs of text. Blank lines separate paragraphs.

::: page poetry
Lines of verse are kept
exactly as written.

::: page marginalia faded
Faded pages can only be read by gilded lamplight.
```

- Front matter keys: `id`, `title`, `author`, `year` (integer, SR), `genre` (one of `horticulture`, `history`,
  `husbandry`, `mysticism`, `mathematics`), `cover` (short physical description; the colour usually but not always
  hints at the genre), `blurb` (one line shown on the spine card), `available` (`start`, or a condition such as
  `beat:unkept-vow` or `locale:pools`), optional `hint`.
- Each page starts with `::: page <kind>`, optionally followed by `faded`, optionally followed by `| Page title`.
  Kinds: `prose`, `poetry`, `letter`, `list`, `art`, `recipe`, `table`, `marginalia`.
- `poetry`, `art`, `table` and `list` pages are shown verbatim (keep lines ≤ 64 characters). Other kinds are
  word-wrapped.
- 1–3 pages per tome. 60–180 words per page.
- Pages are numbered from 1 in file order. **Page numbers matter**: clue-objects are compared against specific
  pages.

Cover colours by genre (default; a few tomes are deliberately re-bound in the "wrong" colour):
horticulture → green, history → oxblood/brown, husbandry → blue, mysticism → violet, mathematics → ochre/yellow.

## Title style

Titles follow the spirit of *Librarian: Tidy Up the Arcane Library!*: earnest mock-academic **"Title: Subtitle"**
phrasing that turns playful or self-deprecating in the subtitle, the occasional edition or rank suffix
("Beginner Edition", "– Adept", "Volume II"), and everyday self-help framing applied to a fantasy subject. Titles
are original to Atrium (none are copied from that game). The `title` front-matter value is the full title.

## The list

`★` marks a page a clue-object reading depends on. Its content must say what the note says (in-world words, not
these words).

### Horticulture

| id | title | author | year | avail | pages | notes |
|---|---|---|---|---|---|---|
| trees-of-the-upper-tiers | Trees of the Upper Tiers: A Field Guide for the Chronically Out of Breath | Wenna Holloway | 512 | start | 2 | ★p1 Lantern Birch: pale leaf, veins like poured silver; grows only in the High Hallows, will not root below the Crown; Gallery botanists press its leaves for herbaria. p2 another high tree. |
| amber-and-its-imitations | Amber and Its Imitations: How Not to Be Swindled in the Gallery | Corwin Dask | 588 | start | 2 | ★p1 Amber: resin turned stone, light as pumice, warms quickly in the hand, smells of resin near a flame, Gallery gem-sellers, insects asleep inside. p2 imitations (copal, glass, "ember-glass"). |
| a-year-in-the-hem-gardens | A Year in the Hem Gardens (and Most of the Mud) | Posy Wendle | 601 | start | 3 | Seasonal; one poetry page. Mentions the red Hem clay that gets on everything. |
| mosses-of-damp-places | Mosses of Damp Places, Volume II: The Damper Places | Ansel Brook | 455 | start | 2 | Hush-moss: grows in shaded cool pools and dims the water around it. |
| the-reed-and-its-uses | The Reed and Its Uses: 101 Things That Are Not Baskets | anonymous | 377 | start | 2 | Lantern-reeds; reed beaten to twine; a tight reed pouch, waxed, "will hold even smoke". |
| old-stoic-a-biography | Old Stoic: An Unauthorised Biography | Hollis Quire | 590 | start | 3 | The tree's great age; it was the last waystation of the Long Stair; old square nail-holes in its bark at the height of a raised hand (no explanation of why). One poetry page. |
| on-the-keeping-of-herbaria | On the Keeping of Herbaria: Pressing Matters | Dr. Fenwick Lisle | 530 | start | 2 | ★p1 How botanists press specimens: flat between papers, labelled with place and date; *never* sealed in wax (wax yellows and spoils the specimen); never strung or worn. |
| kitchen-herbs-of-the-ribs | Kitchen Herbs of the Ribs: Cooking on a Ledge | Marta Ossery | 622 | start | 2 | Ledge gardens of the Ribs; a recipe page. Cover deliberately oxblood (re-bound). |

### History

| id | title | author | year | avail | pages | notes |
|---|---|---|---|---|---|---|
| the-charter-of-the-atrium | The Charter of the Atrium (A Fair Copy, Mostly) | the Crown Clerks | 233 | start | 2 | p1 the charter, with the four duties verbatim. p2 the four houses it names. |
| on-the-slowsilver | On the Slowsilver: An Oral History of a Waterfall | Ide Marrow | 560 | start | 2 | The falls once ran silver; since the Year of Quiet Water (SR 541) a lazy turquoise; people's memories of the shine. Does NOT explain sluices. |
| the-lamps-of-the-charter | The Lamps of the Charter: A Lamplighter's Handbook | Brannoc Vell | 548 | start | 3 | ★p1 the lampstone: cold, pale, oval; carried down from the Crown each midsummer; drank the sun at the top, gave it back at the bottom; Crown charter-seal on its base. ★p2 the Lamplighter's Lantern and gilded lamp-oil (pressed oil, patience, "something of true wealth, glittering and rare", warmed gently) reveal lamplit ink. p3 poetry: a lamplighter's song. |
| mourning-customs-of-the-hem | Mourning Customs of the Hem: A Polite Guide to Grief | Ottoline Gray | 597 | start | 2 | ★p1 jet beads for grief: black stone, drilled, strung on black cord, warm to the touch, take a high polish. p2 other customs. |
| the-lamp-divers-of-the-ribs | Holding Your Breath for a Living: The Lamp-Divers of the Ribs | Sefton Hale | 615 | start | 3 | ★p1 smoke-pearls (see world bible): black, seamless, warm at the heart, lighter than they look; crack one and smoke pours out that will not disperse. p2 a divers' work-song (poetry). p3 guild life. Cover deliberately blue (re-bound). |
| the-four-houses | The Four Houses of the Atrium: Who Still Owes Whom | Mab Quire | 630 | start | 2 | The Library, Apothecary, Spirit Pools (the Stillkeepers left about SR 610 when the pools clouded), Sanctum. Gossipy. |
| the-year-of-quiet-water | The Year of Quiet Water: Minutes of a Meeting Nobody Attended | Jory Tallow | 545 | start | 2 | SR 541: Sluice III at the Gallery Reach closed for repairs that never came; the city barely noticed; the warden of Sluice III was "relieved of duty" — the record keeps only the initials I.F. |
| letters-from-the-gallery | Letters from the Gallery: Collected Complaints | various | 580 | start | 2 | Two letters, `letter` kind. Market gossip, glassworks, a botanist complaining. |
| customs-of-the-hallows-pilgrims | Customs of the Hallows Pilgrims: Packing Light for the Long Stair | Mother Aud of the Hallows | 520 | beat:unkept-vow | 3 | p1 the Long Stair; vow-tokens (see world bible); "a token found is a vow still open". p2 the waystation trees, Old Stoic the last of them. p3 a pilgrim's leave-taking prayer (poetry). |
| a-census-of-nails | A Census of Nails: Forty-One Holes and What I Think About Them | Mab Quire | 640 | beat:gilded-nails | 2 | Mab, freshly inspired, counts the nail-holes in Old Stoic (forty-one) and lists rumours. Light, funny. |
| an-account-of-the-lamps-relit | An Account of the Lamps Relit, Corrected Twice | Ada Quire | 640 | beat:lamps-relit | 2 | Ada's very precise account of the night the Atrium's lamps were relit. Warm. |
| the-wardens-of-the-reach | The Wardens of the Reach: Duties, Oaths, and Approved Hats | Engineer Oriel Banes | 534 | beat:silver-course | 2 | The duties of the three sluice-wardens; a warden "keeps the key as a priest keeps a vow". Mentions that the keys were never to leave the Reach. |

### Husbandry

| id | title | author | year | avail | pages | notes |
|---|---|---|---|---|---|---|
| a-stillkeepers-primer | A Stillkeeper's Primer: Pond Husbandry for the Restless | Sister Wend | 598 | start | 3 | p1 the two breaths of water (Light↔Dark, Hot↔Cold); survive vs thrive. p2 feeding, cleansing; creatures sulk, never die. p3 breeding lore (see world bible: like breeds like; heat brings out the blaze; Spinner mother + Glory father = Sun-Spotted, not the other way round; guppies darken in the deepest shade; motes spawn, not breed). |
| the-ledger-of-tides | The Ledger of Tides | unknown | 571 | locale:pools | 1 | Returned by the Stillkeepers. p1: the pages have been cut out to make a hollow; a scrap of note in the hollow: "for safekeeping — not for the reliquary, not yet." (The black pearl is found inside; the engine handles that.) |
| a-keepers-bestiary | A Keeper's Bestiary: Small Fire-Beasts and How to Apologise to Them | Tobiah Wick | 502 | start | 2 | ★p1 salamander eggs: smooth, oval, cold as river stone until laid in sunlit water, where they warm, glow, and hatch; snails gather on them. p2 other small fire-beasts (whimsical). |
| snails-a-love-letter | Snails: A Love Letter in Seven Slow Parts | Pim Arden | 619 | start | 2 | Yellow-backed snails graze the green off pool walls (keep water clear), glow faintly in warm light, like warm and bright water. |
| the-guppy-fancier | The Guppy Fancier's Companion: Grey Is a Colour Too | Lettice Mole | 604 | start | 2 | Grey guppies are hardy and forgiving; shadow guppies come of the deepest shade. |
| motes-and-their-moods | Motes and Their Moods: A Practical Guide to Things Too Small to Argue With | Sister Calla | 590 | start | 2 | Motes spawn from minerals/auras in a spawning pond: sun-motes (bright), frost-motes (cold). Their moods. |
| feeding-the-small | Feeding the Small: Recipes for Mouths You Can Barely See | Brother Alder | 606 | start | 2 | p1 pond flakes; p2 a `recipe` page for reed meal (reed and loam, ground). |
| goldfish-of-the-gallery-fountains | Fountain Society: The Goldfish of the Gallery | Amabel Fyfe | 579 | start | 2 | Spotted Spinners and Blazing Glories in the Gallery fountains; a fountain-keeper's anecdote. |

### Mysticism

| id | title | author | year | avail | pages | notes |
|---|---|---|---|---|---|---|
| signs-and-sigils-of-stairfall | Signs and Sigils of Stairfall: A Beginner's Guide to Pointing at Symbols | Archivist Penhallow | 470 | start | 3 | p1 the five shelving sigils of the libraries (one per genre). ★p2 the closed hand around a flame — "things given to be kept": offerings, tokens, vows; shrines mark them; libraries never shelve them. p3 the Crown's seal (falls, tree, four lamps) and other civic sigils. |
| tithes-and-offerings-of-the-hallows | Tithes and Offerings of the Hallows: What to Leave and Where | Brother Esk | 498 | start | 2 | ★p1 tithe-nails (see world bible) driven into "waystation trees along the descent" (do not name Old Stoic); Hallows-blessed gold glows faintly in water. p2 other offerings. |
| locks-and-reliquaries | Locks and Reliquaries: Keeping Things Shut, Spiritually | Sister Wend | 566 | start | 2 | ★p1 the reliquaries kept "keys to things that should stay closed": small, finely warded keys. p2 the Balance and its riddle ("Lay upon me a thing of shadow and of flame…"; neither shadow alone nor fire alone will move it). |
| chalk-and-remnant | Chalk and Remnant: Remaking Things – Adept | anonymous | 410 | start | 2 | The Circle; "matter is reclaimable; knowledge is permanent"; what is made *from* a thing goes quiet when the thing is made whole; what it only *touched* stays touched. |
| dreams-of-the-stair | Dreams of the Stair: Poems for Tired Knees | Lio Marsh | 612 | start | 2 | Two poetry pages. Dreams about the Long Stair and the falls. |
| the-keepers-vigil | The Keeper's Vigil: On Doing Nothing Properly | unnamed Keeper | 402 | start | 2 | Short meditations on stillness by a long-ago Keeper. |
| water-ash-and-patience | Water, Ash, and Patience: A Beginner's Guide to Alchemy Without Explosions | Old Corran Pell | 560 | start | 3 | A first alchemy. Hints, as riddling advice: warm water rises (steam); ash given patience becomes earth (loam); water and ash make a biting liquor (lye); patient water is stillwater; things that are ground give up their secrets. Cover ochre (re-bound). |

### Mathematics

| id | title | author | year | avail | pages | notes |
|---|---|---|---|---|---|---|
| weights-and-measures-of-the-exchange | Weights and Measures of the Exchange: Heavy Truths | the Guild of Assayers | 540 | start | 2 | ★p1 gold is the heaviest common metal; iron much lighter; gilded iron weighs light for its size; forgers gild *lead* to fake gold's weight. p2 a `table` of weights. |
| the-waterworks-of-stairfall | The Waterworks of Stairfall: Sluices, Sums, and Silver | Engineer Oriel Banes | 530 | start | 3 | ★p1 the three sluices at the Gallery Reach; when all three stand open the falls run silver with Hallows silt; each opened by a great key with a silver bit, held by a sluice-warden. p2 a `table` or `art` page of flows. ★p3 `marginalia faded`: in a different hand: "I hold III. When they open the others, I'll open mine, and the falls will run silver, and I will come home. — I.F." |
| an-almanac-of-shadow-lengths | An Almanac of Shadow Lengths: Telling Time by Tree | Perrin Gale | 600 | start | 2 | Telling time in the Atrium by Old Stoic's shadow ("a hand's width", "a pool's length"). A `table` page. |
| proofs-for-idle-afternoons | Proofs for Idle Afternoons: Sums to Do in a Hammock | Ada Quire | 611 | start | 2 | Two gentle puzzles with answers. |
| the-geometry-of-shelving | The Geometry of Shelving: Oldest Nearest the Lamp | Ada Quire | 625 | start | 2 | The Quire rule: *oldest nearest the lamp* — each shelf runs from the oldest year to the newest. p2 Mab's rebuttal. |
| a-primer-of-tier-reckoning | A Primer of Tier Reckoning: Counting Stairs So You Don't Have To | Clerk Hobb | 488 | start | 2 | How the tiers are counted and measured; stair counts; a `list` page. |
| the-arithmetic-of-tides | The Arithmetic of Tides: Cisterns, Sums, and Damp Socks | Senna Lowe | 593 | start | 2 | The cisterns of the Ribs filling and emptying; a Stillkeeper's tally. Cover blue (re-bound). |
