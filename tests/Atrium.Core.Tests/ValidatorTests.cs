using Atrium.Core.Content;
using Xunit;

namespace Atrium.Core.Tests;

/// <summary>The content pipeline must reject data that would break a design invariant, and say why.</summary>
public class ValidatorTests
{
    [Fact]
    public void MinimalContentIsValid()
    {
        var r = new MiniContent().WithStone().Report();
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void MoreThanThreePlausibleWrongClaimsIsRejected()
    {
        // Three more wrong claims on top of the one the stone already has: four in all.
        var extra = new[] { ("stony", "high"), ("leafy", "low"), ("leafy", "leafy") }
            .Select((d, i) => "{ \"id\": \"extra" + i + "\", \"kind\": \"plausibleWrong\", \"designation\": { \"what\": \"" + d.Item1 + "\", \"origin\": \"" + (i == 2 ? "low" : d.Item2) + "\" }, \"nearMiss\": \"no\" },");
        var obj = MiniContent.Stone.Replace("\"claims\": [", "\"claims\": [" + string.Concat(extra));
        var r = new MiniContent().Add("stone.json", obj).Add("library/tomes/a-book.md", MiniContent.Book).Report();
        Assert.Contains(r.Errors, e => e.Contains("cap is 3"));
    }

    [Fact]
    public void DerivationsMustHappenWhereTheyCanRun()
    {
        var obj = MiniContent.Stone.Replace("\"id\": \"crush-stone\", \"locale\": \"apothecary\"", "\"id\": \"crush-stone\", \"locale\": \"sanctum\"");
        var r = new MiniContent().Add("stone.json", obj).Add("library/tomes/a-book.md", MiniContent.Book).Report();
        Assert.Contains(r.Errors, e => e.Contains("no way to run derivations"));
        Assert.Contains(r.Errors, e => e.Contains("belongs to apothecary"));
    }

    [Fact]
    public void RecipeCyclesAreRejected()
    {
        var r = new MiniContent().Add("cycle.json", """{ "recipes": [ { "id": "r-back", "inputs": ["brick"], "output": "water" } ] }""").Report();
        Assert.Contains(r.Errors, e => e.Contains("cycle"));
    }

    [Fact]
    public void OrphanRemnantsAreRejected()
    {
        var r = new MiniContent().Add("orphan.json", """{ "items": [ { "id": "lonely", "name": "Lonely", "kind": "remnant" } ] }""").Report();
        Assert.Contains(r.Errors, e => e.Contains("orphan remnant"));
    }

    [Fact]
    public void ThriveBandOutsideSurviveBandIsRejected()
    {
        var r = new MiniContent().Add("fish.json", """
            { "lifeforms": [ { "id": "odd-fish", "name": "Odd Fish", "survive": { "light": [-1, 1], "heat": [-1, 1] }, "thrive": { "light": [0, 3], "heat": [0, 0] } } ] }
            """).Report();
        Assert.Contains(r.Errors, e => e.Contains("thrive band must sit inside"));
    }

    [Fact]
    public void UnknownTaxonomyReferencesAreRejected()
    {
        var r = new MiniContent().Add("bad.json", """{ "items": [ { "id": "thing", "name": "Thing", "traits": ["no-such-trait"] } ] }""").Report();
        Assert.Contains(r.Errors, e => e.Contains("unknown trait 'no-such-trait'"));
    }

    [Fact]
    public void TypoedKeysAreRejected()
    {
        var r = new MiniContent().Add("typo.json", """{ "items": [ { "id": "thing", "name": "Thing", "kidn": "food" } ] }""").Report();
        Assert.Contains(r.Errors, e => e.Contains("typo.json"));
    }

    [Fact]
    public void TrueClaimWithUnearnableTermIsRejected()
    {
        var obj = MiniContent.Stone.Replace("\"grants\": [\"low\"]", "\"grants\": []");
        var r = new MiniContent().Add("stone.json", obj).Add("library/tomes/a-book.md", MiniContent.Book).Report();
        Assert.Contains(r.Errors, e => e.Contains("never granted"));
    }

    [Fact]
    public void ReadingsMustUseTheirLocalesOwnMechanics()
    {
        var obj = MiniContent.Stone.Replace("\"locale\": \"sanctum\", \"trigger\": { \"verb\": \"inspect\"", "\"locale\": \"sanctum\", \"trigger\": { \"verb\": \"shelve\"");
        var r = new MiniContent().Add("stone.json", obj).Add("library/tomes/a-book.md", MiniContent.Book).Report();
        Assert.Contains(r.Errors, e => e.Contains("is not a sanctum mechanic"));
    }

    [Fact]
    public void TomesWithTooManyPagesAreRejected()
    {
        var book = MiniContent.Book + "\n::: page prose\nTwo.\n::: page prose\nThree.\n::: page prose\nFour.\n";
        var r = new MiniContent().Add("stone.json", MiniContent.Stone).Add("library/tomes/a-book.md", book).Report();
        Assert.Contains(r.Errors, e => e.Contains("page(s)"));
    }

    [Fact]
    public void TomeParserReadsFrontMatterAndPages()
    {
        var t = TomeParser.Parse("""
            ---
            id: x
            title: Title: With Colon
            year: 12
            genre: Plants
            available: beat:something
            ---
            ::: page poetry faded | The Title
              indented line
            second line

            ::: page prose
            Words.
            """, "x.md");
        Assert.Equal("Title: With Colon", t.Title);
        Assert.Equal("plants", t.Genre);
        Assert.Equal("something", t.Available.Beat);
        Assert.Equal(2, t.Pages.Count);
        Assert.True(t.Pages[0].Faded);
        Assert.Equal("The Title", t.Pages[0].Title);
        Assert.StartsWith("  indented", t.Pages[0].Text);
        Assert.Equal(2, t.Pages[1].Number);
    }
}
