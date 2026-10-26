# Iteration 2: the lantern-lit diorama

*Branch `standalone-diorama`, started 2026-10-25 from `standalone`. This is one interpretation of Atrium as a
standalone 3D game. Everything specific to it lives in `src/Atrium.Diorama/` and this file. To start again with a
different graphics style, branch from `standalone` again. The game rules, content and tests are unchanged.*

## Library choice: raylib (Raylib-cs 8.1, Zlib licence)

| Option | Verdict |
|---|---|
| **raylib (Raylib-cs)** | **Chosen.** One small package with its native library bundled, and no content pipeline. Fonts, shaders, sounds and meshes all load from code or memory. It has real 3D (cameras, procedural meshes, custom GLSL, ray picking) and fast 2D for the interface. Its immediate-mode style suits a small team that iterates a lot. It runs on Windows, macOS and Linux. |
| MonoGame | Mature and excellent for 2D and XNA-style games, but fonts and shaders go through the MGCB content pipeline and HLSL→MojoShader effects. That's real friction for a procedural 3D diorama with no art assets yet. |
| Stride | A full engine with an editor. You asked for a library, and this iteration is a stepping stone toward Godot, so a second engine would be a detour. |
| Silk.NET | Raw OpenGL/Vulkan bindings. Too low-level for the scope. |

There are no external art or audio files. Every mesh, texture and sound is generated in code at startup. That
keeps the build standalone and means a new art direction can replace the visuals without leftover assets.

## Art direction: "lamplight and linen"

**The inspirations, and what I take from each:**

- *Strange Horticulture*: lamplit, a little mysterious, the satisfaction of a correct label.
- *Librarian: Tidy Up the Arcane Library*: chunky stylised 3D and colourful spines that click into place.
- *Potion Craft*: tactile tools and an illuminated-manuscript map.
- *Fish Tycoon*: tanks you can watch for minutes.
- *Townscaper* and *Tiny Glade*: a toy-like diorama floating in soft light, no pressure.

**The look**

- **A floating diorama.** The courtyard is a round slab of canyon floor floating in a warm, hazy void. Behind it
  rise silhouettes of the city's tiers, with tiny lit windows. It is a quiet corner of a city, seen like a model on
  a table.
- **Cel shading with ink.** Three soft bands of light, an ink outline (inverted hull), hemispheric ambient light
  (warm sky above, cool bounce below), a rim highlight, and depth fog that fades into the backdrop colour.
- **Old Stoic.** A big old tree, serene and unchanging, with a thick leaning trunk and a broad, faceted canopy.
  It never changes with progress (designer decision, 2026-10-24).
- **The Slowsilver.** A tall, lazy ribbon of water with scrolling bands, foam where it lands, and mist drifting off.
- **Water.** Turquoise with gentle vertex swell, soft bands of light and slow sparkles.
- **Houses read by silhouette, colour and lantern-light, with no floating labels.** The accent colours come from the
  overview sheet:
  - **Library:** a tall, narrow grey-stone house with a steep blue roof and a green door.
  - **Spirit Pools:** the Stillkeepers' low lodge with a teal roof, beside the pools.
  - **Apothecary:** a low shop with a purple roof, a bowed window and a chimney that steams.
  - **Sanctum:** a round stone lodge with an ochre dome.

  Hovering a house lifts it slightly and outlines it in its colour. Its name appears in a small handwritten caption
  at the bottom of the screen, never above the house.
- **Time of day.** The world clock's time of day tints the light: dawn rose, day gold, dusk amber, night blue.
  Lanterns glow brighter as it gets darker. Nothing ever gets dark enough to be gloomy.
- **Interiors.** Each house is entered and is its own small 3D room, seen from a fixed three-quarter camera that
  sways a little with the mouse. Detailed work happens on parchment cards laid over the room: the reader, the
  Discovery Map, the designation card and so on.
- **Post-processing.** A warm grade, a soft vignette and a faint paper grain over the 3D view. The interface stays
  crisp.
- **Palette.** Parchment `#EFE3C8`, ink `#2B2A33`, warm stone `#D9C7A7`, moss `#7FA36B`, teal `#1C8A84` /
  `#56A9A5`, lantern amber `#F2B04C`.
  - Locale accents: Library blue `#3D5AA8`, Pools teal `#1C8A84`, Apothecary purple `#8A55A8`, Sanctum ochre
    `#B0700F`.
- **Type.** Book text in EB Garamond (OFL, bundled). Headings in a handwritten face (Ink Free or Segoe Print when
  the system has them, otherwise EB Garamond Italic).

## Audio: all synthesised, all soft

Nothing is a buzzer. Wrong answers get a curious "hm?", never an error tone.

| Kind | Sound |
|---|---|
| Ambience | Waterfall (pink noise, slow swell, low rumble); faint lantern wind-chimes now and then; each interior has its own room tone (Library: quiet hush and page rustles; Apothecary: bubbling; Pools: drips; Sanctum: low warm drone) |
| Music | Generative and sparse: pentatonic phrases on a soft kalimba/music-box voice with long pauses. Each locale has its own scale and timbre, and every note lands softly. |
| Interface | Hover: a soft wooden "tock". Click: paper. Panel open: a page turn. Back: a soft paper slide. |
| Library | Book slide plus a soft thump; the shelf "pulses" with a warm two-note chord; a refused book gets a gentle low "hm?"; a settled shelf rings a rising chord. |
| Apothecary | Mortar grind (noise grains), burner whoosh, bubbling, a glass clink, discovery (a rising arpeggio and sparkle), a failed mix (a gentle bubble pop). |
| Pools | Splash, the rake, feeding sprinkle, a hatching (bubbly pop and chime), a soak finishing (bell). |
| Sanctum | The magnifying glass (a glassy ping), the Balance (wood creak and brass clink), the Circle (a shimmering riser), designation outcomes (resolved: a full warm chord; near-miss: a soft two-note "ah"; not yet: a single muted tone). |
| Story | Story card: a page turn and a sustained pad; a new term: a pen scratch and a small chime. |

## Juice (cozy, never noisy)

- **Hover.** Objects and houses lift slightly on a spring, glow at the rim, and give a soft tock.
- **Press.** Squash and stretch on buttons and objects. Cards overshoot slightly as they arrive and ease away when
  they leave.
- **Library.** Books slide into their gap and the shelf breathes light in its genre colour. A refused book wobbles
  and springs back into your hand. A settled shelf sends a ripple of light along its spines.
- **Apothecary.** Ingredients drop into the mortar. Mixing swirls and bubbles. A discovery pops in a new jar with
  an overshoot and sparkles; a failure fizzes politely.
- **Pools.** Fish swim with simple flocking and a tail wiggle, and drift up to the surface for feeding flakes.
  Cleansing clears the murk you can see in the water. Hatchings burst into bubbles. Motes bob and glow.
- **Sanctum.** The Balance tips with damped-spring physics. The Circle draws itself in light before a restoration.
  A resolved designation stamps the card with wax.
- **Text.** Readings appear word by word. New terms ink in with a bleed. Story cards unfold.
- **Camera.** Entering a house dollies toward its door and fades through warm light. Leaving pulls back out.
- **No screen shake.** "Reduce motion" turns off sway, bobbing and parallax.

## Controls

- **Mouse first:** left-click to act, right-drag or A/D to orbit the courtyard, scroll to zoom.
- **Keyboard:** `Esc` goes back. `J` opens the journal. `Space` sits by the water. `P` pauses the clock.
- **Settings:** volumes, reduce motion and fullscreen.

## Engineering notes

- The project is `src/Atrium.Diorama` (`dotnet run --project src/Atrium.Diorama`). It uses `GameSession` exactly
  as the terminal front end does. No rule lives in the front end.
- Look-specific data, such as item icon shapes and colours and the house layout, lives in the front-end project,
  not in `content/`. A different graphics style can therefore replace it without touching the game's content.
- `--capture <script>` runs scripted steps in a hidden window and writes PNG screenshots. This is how the visuals
  were checked during development, and it doubles as a visual regression aid.
- Rooms share one kit (`Scenes/InteriorScene.cs`). Furniture is clickable through hotspots: hovering lifts and rims
  a prop, and a handwritten caption names it low on the screen. Working cards sit on the left of the room, so the
  right edge stays clear for notices and the bottom for the satchel.
- `Input.Blocked` keeps the world animating under an open card without taking its clicks.

## The rooms

Each house is a small furnished 3D room. You click furniture to work, and a parchment card opens on the left
while the camera leans in on what you're using.

| Room | Furniture | What happens there |
|---|---|---|
| **Library** | One bookcase per shelf; hanging plaques for genre tags; the lamp the order runs from; the reading table with the pile; a returns box | Hang tags; flip through the pile and shelve each book, choosing its slot along the shelf; browse a shelf to read, reorder, take down or borrow; try objects on shelves or hold them against pages. Spines take their colour from each cover's description rather than from its genre (in the current content most covers echo their genre's colour, with four deliberate exceptions; whether they should is decision D8 in the iteration-3 art bible). Books slide in and wobble, and a settled shelf glows in its genre colour, chimes and sparkles. |
| **Apothecary** | A wall of jars (one per discovery); a bench with a prop for each tool; the copper vessel; the brass tray; the Discovery Map on the wall; the formulary lectern; a crate of bottles | Click jars to drop elements into the vessel, pick a tool and mix. New jars pop onto the shelf with a sparkle, and a dud fizzes politely. Objects go on the tray to be worked, with a confirmation whenever the essence will move. |
| **Spirit Pools** | One glass tank per pool, with swimming, creeping and swaying residents; the holding crock; the druids' bag; the breeding ledger; a stool | Each tank's card shows its place on the Light/Dark × Hot/Cold plane. Hovering a resident draws where it survives and thrives. From the card you cleanse, feed, lay things in or take them out, move creatures, and watch soaks progress. The water clouds as clarity falls and tints with the pool's energy. |
| **Sanctum** | The designation desk and journal; the glass on its stand; the assembly bench; the cedar cabinet; the chalk Reconstitution Circle; the Balance over its reliquary | Designate with earned words, one facet at a time. A near-miss lifts the journal's pages, a "not yet" sends a breath through the candles, and a resolution stamps wax. The Circle always shows its full warning (where the essence is, what goes inert, what you would have to remake) before it draws itself in light. |

Satchel items have a use in every room: click one and the room offers what it can do with it.

## Testing it without a person at the keyboard

`--capture script.txt --out dir` plays a script in a hidden window and writes screenshots. Scripts can press
named controls (`press lib-shelve`), choose from lists (`pick Horticulture`), answer questions (`yes`),
click furniture (`spot tank:sunward-basin`), use satchel items (`use gold-nugget`), open a book (`read <tome>`),
pause the clock (`pause`), hide the interface for clean reference frames (`ui off`) and log what the game said
(`dump`). The first two mysteries of Chapter One were played this way, end to end,
through the 3D rooms. `--audio` runs the sound system at zero volume so its code paths are exercised too.

## Going back, or starting again with a new style

Everything specific to this iteration is in `src/Atrium.Diorama/`, this file, one line in `Atrium.slnx` and the
"Play it" paragraph of the README. The core, content and tests are untouched, so the 3D game can be removed or
replaced cleanly.

- **Back to the state before the 3D game:** `git switch standalone`.
- **A new graphics style from scratch:** `git switch -c standalone-<style> standalone`, then add a new front-end
  project beside `Atrium.Core`. `GameSession` is the whole API a front end needs. `App/GameApp.cs` and
  `App/Capture.cs` here show the wiring (scenes, overlays, saves, scripted screenshots) and can be copied.
- **Keep the 3D game and restyle it:** branch from `standalone-diorama` instead. The look is concentrated in
  `Render/` (shaders, palette, mesh kit), `Scenes/HubWorld.cs` (the courtyard model), the room `Build()`/`DrawRoom()`
  methods, `UI/ItemArt.cs` with `Assets/visuals.json` (item icons) and `Audio/` (every sound is a few lines of
  synthesis). The rules never live in any of these.
