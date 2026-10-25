# Chapter One walkthrough (full spoilers)

This is one intended route through the chapter, for playtesting. Other orders work too: every required
reading comes from a repeatable action, and every derivation can be undone at the Circle. The same route is
automated in `tests/Atrium.Core.Tests/PlaythroughTests.cs`.

Menu letters in brackets refer to the terminal game.

## 1. The pressed leaf

1. **Library.** Tag all five shelves `[T]`. Use *Something odd in the pile* `[O]` to try the leaf on a
   shelf, then take it.
   - *Reading:* a sigil of a closed hand around a flame.
2. **Library.** Open *Trees of the Upper Tiers* and hold the leaf against **page 1** `[H]`.
   - *Reading:* Lantern Birch, which grows only in the High Hallows.
   - *Terms:* a botanist's specimen, the High Hallows, a record of where it grew.
3. **Library.** Open *Signs and Sigils of Stairfall* and hold the leaf against **page 2**.
   - *Reading:* things given to be kept.
   - *Term:* a vow-token.
4. **Apothecary.** Put the leaf on the bench `[O]` and work it three times:
   - with the **Weighing Scales:** shrine-wax;
   - with the **Bunsen Burner:** the vow beneath the wax (*term:* a vow);
   - with **Spring Water**, no tool: it drank mountain water.
5. **Sanctum.** Examine the leaf under the glass `[G]`.
   - *Reading:* pinholes and Hem clay.
   - *Terms:* a keepsake, the Hem, a memory.
6. **Designation desk** `[D]`. Commit **a vow-token · the High Hallows · a vow**.
   - **Near-misses:** *specimen · High Hallows · record*; *keepsake · Hem · memory*; *vow-token · Hem · vow*.
   - **Result:** Word Down the Stair. The Spirit Pools open, and Shrine-Wax goes to the Apothecary.

## 2. The gold nugget

1. **Pools → Sunward Basin.** Cleanse `[C]`. The nugget turns up in the silt.
2. Lay the nugget in any pool `[P]`, then take it out.
   - *Reading:* it glows, so it holds stored sunlight. This points to a wrong answer.
3. **Apothecary.** Put it on the **Weighing Scales**.
   - *Reading:* too light for gold.
4. **Library.** Hold it against *Tithes and Offerings of the Hallows*, **page 1**.
   - *Terms:* a tithe-nail, a promise to return.
5. **Sanctum.** Examine it under the glass.
   - *Reading:* bark and sap.
   - *Term:* Old Stoic.
6. **Apothecary.** Work it with the **Mortar & Pestle** and confirm.
   - You get Gold Flakes, plus Iron Slag as a remnant.
   - *Reading:* a hand-forged nail.
   - The Circle wakes.
7. **Designation desk.** Commit **a tithe-nail · Old Stoic · a promise to return**.
   - **Near-misses:** *river gold · the Atrium's own waters · stored sunlight*; *counterfeit gilt · Atrium's waters · a deception*.
   - **Result:** Tamsin hands you the cold stone.

## 3. The Lamplighter's Lantern (optional now, needed for the epilogue reading)

1. **Apothecary.** Discover Pressed Oil by this chain:
   - ash + patience → loam;
   - water + patience → stillwater;
   - loam + stillwater → green shoot;
   - shoot + patience → reed;
   - reed + patience → seed;
   - seed with the mortar → oil.
2. Work the Gold Flakes with Pressed Oil and Patience on the **Burner**. You get Gilded Lamp-Oil.
3. Discover Lamp Glass by this chain, then bottle it `[B]`:
   - water + ash → lye;
   - lye with the burner → potash;
   - loam + water → mud;
   - mud + patience → clay;
   - clay with the burner → fired clay;
   - fired clay with the mortar → grog sand;
   - grog sand + potash with the burner → lamp glass.
4. **Library.** Read *The Lamps of the Charter*. The blueprint appears.
5. **Sanctum → assembly bench.** Assemble the **Lamplighter's Lantern**. Faded pages in the Library become
   readable.

## 4. The cold stone

1. **Apothecary.** Work it with the **Burner**: it won't warm.
2. **Library.** Hold it against *The Lamps of the Charter*, **page 1**.
   - *Terms:* a lampstone, stored sunlight.
   - Optionally, also hold it against *Amber and Its Imitations*, page 1, which points to a wrong answer.
3. **Sanctum.** Examine it under the glass.
   - *Reading:* the charter-seal.
   - *Term:* the Crown.
4. **Sanctum → Circle.** Restore the nugget from the Iron Slag.
   - The warning says the essence is in the Lantern, which will go inert.
5. **Pools → Sunward Basin.** Keep the water clear and the snails content. Lay in the **nugget** and the
   **cold stone**.
   - The light reaches +5 and the heat +2.
   - Wait about 300 ticks (sit by the water five times), or about 220 if a Sun-Spotted Goldfish is in the pool.
   - Result: the Waking Sunstone.
6. **Designation desk.** Commit **a lampstone · the Crown · stored sunlight**.
   - **Near-misses:** *amber · the Gallery markets · a sleeping life*; *a salamander's egg · the Atrium's own waters · a sleeping life*.
   - **Result:** Lamps in Oil. A Sun-Mote Aura goes into the druids' bag, and the Sun-Lens blueprint appears.

**Optional genetics:**

1. Add a bottled Cinder (or Sun-Grit) to the Sunward Basin so the heat reaches +4. Spinners then hatch a
   Blazing Glory.
2. A Spinner mother with a Glory father hatches Sun-Spotted Goldfish. The reverse pairing hatches nothing.

## 5. The black pearl

1. **Library.** Read *The Ledger of Tides*, which joins the pile when the Pools open. It's hollow, and the
   pearl is inside.
2. **Library.** Hold the pearl against *Holding Your Breath for a Living*, **page 1**.
   - *Terms:* a smoke-pearl, the cisterns of the Ribs.
   - Optionally, also *Mourning Customs of the Hem*, page 1, which points to a wrong answer.
3. **Sanctum.** Examine it under the glass.
   - *Reading:* seamless, with an ember inside.
4. **Apothecary.** Work it with the **Mortar** and nothing else.
   - *Reading:* something pushes back.
   - *Term:* a sealed flame.
   - Or lay it on the Balance, which tips toward shadow and gives the same term.
5. **Optional catalysis.** Cleanse the Shade Pool and lay the pearl in it (light −5). The Grey Guppies hatch
   a Shadow Guppy.
6. **Designation desk.** Commit **a smoke-pearl · the cisterns of the Ribs · a sealed flame**.
   - **Near-misses:** *a mourning bead · the Hem · grief*; *a river pearl · the Atrium's own waters · the dark*.
   - **Result:** Not Yet. The Still Kit blueprint appears.
7. **Apothecary.** Discover a Waxed Reed Pouch by this chain:
   - reed with the mortar → fibre;
   - fibre + patience → twine;
   - twine + shrine-wax → pouch.
8. **Apothecary.** Work the pearl with the **Mortar** and the pouch. You get Smoke-In-A-Bag, plus a Pearl
   Husk as a remnant.
9. **Sanctum.** Lay the Smoke-In-A-Bag on the Balance. The reliquary opens, and inside is the great key.
10. **Optional containment check.** Restore the pearl from its husk at the Circle. Only the bag goes inert.
    The guppy and the open reliquary stay.

## 6. The great key

1. **Sanctum.** Examine it under the glass.
   - *Reading:* SLUICE III, the Crown's mark, and I.F.
   - *Terms:* a sluice-key, the Crown.
2. **Library.** Hold it against *The Waterworks of Stairfall*, **page 1**.
   - *Terms:* the Gallery Reach, the falls' old course.
3. **Pools.** Lay it in any pool.
   - *Reading:* it turns to point up the canyon.
4. **Designation desk.** Commit **a sluice-key · the Gallery Reach · the falls' old course**.
   - **Near-misses:** *a sluice-key · the Crown · the falls' old course*; *a reliquary key · the Crown · a secret*.
   - **Result:** Up the Canyon. Silver Silt arrives, and with all five settled, *The End of the Beginning*.
5. **Epilogue reading.** With the Lantern lit, which may mean crushing the nugget again, hold the key or the
   leaf against the Waterworks book's faded **page 3**. This triggers *The Same Hand*.

## Cozy-only milestones

- **Pools without the leaf:** shelve 15 tomes and make 8 discoveries.
- **Every Shelf in Its Place:** settle all five shelves.
- **A Map in the Window:** make 15 discoveries.
- **A Brood of Broods:** four hatchings.
