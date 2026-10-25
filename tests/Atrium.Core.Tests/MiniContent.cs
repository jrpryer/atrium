using Atrium.Core.Content;

namespace Atrium.Core.Tests;

/// <summary>Builds small in-memory content packs for focused tests (and for checking the validator rejects bad data).</summary>
public sealed class MiniContent
{
    private readonly MemoryContentSource _src = new();

    public const string Base = """
    {
      "config": { "seed": 7, "synthesis": { "invalid": ["no"], "notYet": ["not yet"] } },
      "libraryConfig": { "shelvingModel": "tagged", "shelfCount": 2, "tags": ["plants", "sums"], "maxPages": 3 },
      "apothecaryConfig": { "startingIngredients": ["water", "ash"], "insightToReveal": 2 },
      "taxonomy": {
        "facets": [ { "id": "what", "label": "What" }, { "id": "origin", "label": "Origin" } ],
        "terms": [
          { "id": "leafy", "facet": "what", "label": "leafy" }, { "id": "stony", "facet": "what", "label": "stony" },
          { "id": "high", "facet": "origin", "label": "high" }, { "id": "low", "facet": "origin", "label": "low" }
        ],
        "genres": [ { "id": "plants", "label": "Plants" }, { "id": "sums", "label": "Sums" } ],
        "traits": [ { "id": "shiny" }, { "id": "dark" }, { "id": "hot" } ],
        "axes": [ { "id": "light", "min": -5, "max": 5 }, { "id": "heat", "min": -5, "max": 5 } ],
        "capabilities": [ { "id": "sanctum.reconstitute", "label": "the circle" } ]
      },
      "locales": [
        { "id": "library", "name": "Library", "unlockWhen": "start" },
        { "id": "apothecary", "name": "Apothecary", "unlockWhen": "start" },
        { "id": "pools", "name": "Pools", "unlockWhen": "start" },
        { "id": "sanctum", "name": "Sanctum", "unlockWhen": "start" }
      ],
      "tools": [
        { "id": "mortar", "name": "Mortar", "locale": "apothecary" },
        { "id": "burner", "name": "Burner", "locale": "apothecary" },
        { "id": "glass", "name": "Glass", "locale": "sanctum" },
        { "id": "circle", "name": "Circle", "locale": "sanctum", "grants": ["sanctum.reconstitute"],
          "availableWhen": { "counter": "essence.remnants-created" } }
      ],
      "items": [
        { "id": "water", "name": "Water", "kind": "ingredient" },
        { "id": "ash", "name": "Ash", "kind": "ingredient" },
        { "id": "mud", "name": "Mud", "kind": "ingredient" },
        { "id": "brick", "name": "Brick", "kind": "ingredient" },
        { "id": "flakes", "name": "Flakes", "kind": "food", "foodValue": 50 }
      ],
      "recipes": [
        { "id": "r-mud", "inputs": ["water", "ash"], "output": "mud" },
        { "id": "r-brick", "inputs": ["mud"], "tool": "burner", "output": "brick" }
      ]
    }
    """;

    public const string Stone = """
    {
      "object": {
        "id": "stone", "name": "the stone", "baseForm": "stone",
        "arrival": { "when": "start", "location": "satchel" },
        "forms": [
          { "id": "stone", "name": "Stone", "kind": "clueForm", "traits": ["shiny"], "poolEnergy": { "light": -4, "heat": 0 } },
          { "id": "dust", "name": "Dust", "kind": "clueForm", "traits": ["dark", "hot"] },
          { "id": "core", "name": "Core", "kind": "remnant" }
        ],
        "derivations": [
          { "id": "crush-stone", "locale": "apothecary", "tool": "mortar", "input": "stone", "produces": "dust", "remnant": "core" }
        ],
        "readings": [
          { "id": "s.lib", "locale": "library", "trigger": { "verb": "compare", "tome": "a-book", "page": 1 }, "grants": ["leafy", "high", "stony"], "text": "Library says leafy, high." },
          { "id": "s.apo", "locale": "apothecary", "trigger": { "verb": "work", "tool": "burner" }, "grants": ["low"], "text": "The burner says low." },
          { "id": "s.san", "locale": "sanctum", "trigger": { "verb": "inspect", "tool": "glass" }, "text": "The glass says stony." }
        ],
        "claims": [
          { "id": "s.true", "kind": "true", "designation": { "what": "stony", "origin": "low" }, "requires": { "all": ["reading:s.apo", "reading:s.san"] } },
          { "id": "s.wrong", "kind": "plausibleWrong", "designation": { "what": "leafy", "origin": "high" }, "requires": "reading:s.lib",
            "nearMiss": "Not leafy at all, as the burner showed." }
        ]
      }
    }
    """;

    public const string Book = """
    ---
    id: a-book
    title: A Book
    author: Someone
    year: 100
    genre: plants
    ---
    ::: page prose
    A page about leaves and stones.
    """;

    public MiniContent(bool withBase = true)
    {
        if (withBase) _src.Add("base.json", Base);
    }

    public MiniContent Add(string path, string text) { _src.Add(path, text); return this; }
    public MiniContent WithStone() => Add("stone.json", Stone).Add("library/tomes/a-book.md", Book);

    public IContentSource Source => _src;

    public ContentDatabase Load() => ContentLoader.Load(_src);

    public ValidationReport Report() => ContentLoader.TryLoad(_src).Report;
}
