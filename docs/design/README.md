# Design sheets

One-page design sheets for Atrium, in the style of Stone Librande's GDC 2010 talk *One-Page Designs*. Each sheet explains one system as a picture with labels. The Milanote board is the archive behind them, and [AGENTS.md](../../AGENTS.md) is the engineering contract.

## Conventions

- **One file pair per sheet:** a printable PDF (`spirit-pools.pdf`) and the source file it was drawn in (`spirit-pools.ai`, `.fig` or `.afdesign`).
- **Every sheet is dated** in its corner. When a decision changes, redraw that part and change the date. The date is how people tell printed copies apart.
- **Red means not designed yet.** Mark every open question in red on the sheet itself.
- **If a sheet and AGENTS.md disagree,** ask the designer before building either way.

## Sheets

| Sheet | Diagram types | Status |
|---|---|---|
| Overview | Isometric courtyard with locale cards; clue × locale grid | Rough mockup: [overview-draft.png](overview-draft.png) |
| Spirit Pools | Player action and world response loop; Light/Dark × Hot/Cold plane with lifeform ranges | Not started. Draw this first, since the Pools have no play flow yet. |
| Pacing | Timing diagram with minutes, hours and days lanes | Not started |
| Library | Player action and world response loop | Not started. Waits on the shelving model (AGENTS.md §8, question 10). |
| Apothecary | Player action and world response loop | Not started |
| Sanctum | Player action and world response loop | Not started |
| One per clue | Level map with an acts sidebar | When the second clue is written |
| Verb dictionary | List of verbs, then recipes written with them | When the tool verbs are chosen |

## Iteration notes and art direction

| Document | What it is |
|---|---|
| [iteration-1.md](iteration-1.md) | What the first playable (the core and the terminal game) decided and left open |
| [iteration-2-diorama.md](iteration-2-diorama.md) | The standalone 3D front end: library choice, procedural look, audio, juice, rooms |
| [iteration-3-art-bible.md](iteration-3-art-bible.md) | **Draft.** A visual identity for generated and hand-finished art: pillars, palette, reserved colours, three directions to test, and the decisions waiting on the designer |
| [iteration-3-assets.md](iteration-3-assets.md) | Every asset that identity needs, with counts generated from `content/` |
| [iteration-3-prompts.json](iteration-3-prompts.json) | The tool-agnostic prompt pack: direction tests, templates filled from content, look-only briefs per item, and hand-written anchors |
| [iteration-3-direction-tests.md](iteration-3-direction-tests.md) | The 24 direction-test prompts, assembled and ready to paste into an image tool, with how to capture the courtyard reference frames |
| [iteration-3-log.md](iteration-3-log.md) | Every generation so far: model, job id, credits, verdict and lessons |
| [../../tools/blender/README.md](../../tools/blender/README.md) | The asset pipeline: AI reference sheets and textures in, Blender-scripted models out |

`overview-draft.png` is a thumbnail drawn with Claude on Oct 11, 2026, from the Milanote board, AGENTS.md and prototype v4. It's meant to be redrawn by hand or in Illustrator, not used as finished art.

**v0.2, Oct 24, 2026:** in the clue × locale grid, "crush → slag" moved to the Apothecary, and the Sanctum cell for the gold nugget now reads "driven into wood" (what the magnifying glass shows in the build). The tree panel now shows Old Stoic as one big old tree beside the falls, serene and unchanging.
