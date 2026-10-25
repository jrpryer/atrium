using Atrium.Core.Content;
using Atrium.Core.Runtime;
using Xunit;

namespace Atrium.Core.Tests;

/// <summary>Each minigame stands alone (invariant 3): these exercise them with no mystery progress at all.</summary>
public class MinigameTests
{
    private static GameSession WithPoolsOpen()
    {
        var s = GameSession.NewGame(TestContent.Db, 99);
        s.State.UnlockedLocales.Add("pools");
        s.World.Bus.Publish(new LocaleUnlocked("pools"));
        return s;
    }

    // ───── Spirit Pools ─────

    [Fact]
    public void NeglectedCreaturesSulkButNeverDie()
    {
        var s = WithPoolsOpen();
        var before = s.State.Pools.Pools.SelectMany(p => p.Lifeforms).Select(l => l.Id).ToList();
        s.AdvanceTicks(20000);
        var after = s.State.Pools.Pools.SelectMany(p => p.Lifeforms).Select(l => l.Id).ToHashSet();
        Assert.All(before, id => Assert.Contains(id, after)); // some may have hatched; none are ever lost
        var spinner = s.Pools.State("sunward-basin").Lifeforms.First(l => l.Species == "spotted-spinner");
        Assert.Equal(Mood.Sulking, spinner.Mood);

        s.Pools.Feed("sunward-basin");
        s.Pools.Feed("sunward-basin");
        s.Pools.Cleanse("sunward-basin");
        s.Pools.Cleanse("sunward-basin");
        Assert.NotEqual(Mood.Sulking, spinner.Mood);
    }

    [Fact]
    public void OrderedPairsCareWhichParentIsWhich()
    {
        var s = WithPoolsOpen();
        var pool = s.Pools.State("sunward-basin");
        s.World.Items.Create("cinder", Locations.Pool("sunward-basin")); // warm enough for a Glory to thrive
        pool.Lifeforms.RemoveAll(l => l.Species == "spotted-spinner");
        pool.Clarity = 100;
        pool.Lifeforms.Add(new LifeformInstance { Id = 9001, Species = "blazing-glory", Sex = Sex.Female, Name = "Ember", Hunger = 0 });
        pool.Lifeforms.Add(new LifeformInstance { Id = 9002, Species = "spotted-spinner", Sex = Sex.Male, Name = "Pip", Hunger = 0 });

        Tend(s, 1500);
        Assert.DoesNotContain("sun-spotted-goldfish", s.State.Pools.SpeciesSeen);

        pool.Lifeforms.RemoveAll(l => l.Id is 9001 or 9002);
        pool.Lifeforms.Add(new LifeformInstance { Id = 9003, Species = "spotted-spinner", Sex = Sex.Female, Name = "Marigold", Hunger = 0 });
        pool.Lifeforms.Add(new LifeformInstance { Id = 9004, Species = "blazing-glory", Sex = Sex.Male, Name = "Flint", Hunger = 0 });
        Tend(s, 600);
        Assert.Contains("sun-spotted-goldfish", s.State.Pools.SpeciesSeen);
        Assert.Contains("sun-spotted-cross", s.State.Pools.RulesSeen);
    }

    [Fact]
    public void HeatBringsOutTheBlaze()
    {
        var s = WithPoolsOpen();
        s.World.Items.Create("cinder", Locations.Pool("sunward-basin"));
        s.World.Items.Create("sun-grit", Locations.Pool("sunward-basin"));
        Assert.Equal(4, s.Pools.Energy("sunward-basin").Heat); // hot enough for the blaze, not too hot to thrive
        Tend(s, 900);
        Assert.Contains("blazing-glory", s.State.Pools.SpeciesSeen);
    }

    [Fact]
    public void TheSunBlessingCosts300TicksOr220WithSunSpottedGoldfish()
    {
        var s = WithPoolsOpen();
        var pool = s.Pools.State("sunward-basin");
        pool.Clarity = 100;
        var nugget = s.World.Items.Create("gold-nugget", Locations.Pool("sunward-basin"));
        s.Essence.Register("gold-nugget", nugget);
        var stone = s.World.Items.Create("cold-sunstone", Locations.Pool("sunward-basin"));
        s.Essence.Register("cold-sunstone", stone);
        s.AdvanceTicks(1);

        var soak = s.Pools.View("sunward-basin").Soaks.Single();
        Assert.True(soak.ConditionsMet);
        Assert.Equal(300, soak.EffectiveTicks);

        pool.Lifeforms.Add(new LifeformInstance { Id = 9100, Species = "sun-spotted-goldfish", Sex = Sex.Male, Name = "Sol", Hunger = 0 });
        s.AdvanceTicks(1);
        Assert.Equal(220, s.Pools.View("sunward-basin").Soaks.Single().EffectiveTicks);

        // Take the nugget out: the soak pauses but never resets.
        var mid = 100;
        s.AdvanceTicks(mid);
        var progress = s.Pools.View("sunward-basin").Soaks.Single().Progress;
        s.Pools.TakeOut(nugget.Id);
        s.AdvanceTicks(300);
        var paused = s.Pools.View("sunward-basin").Soaks.Single();
        Assert.False(paused.ConditionsMet);
        Assert.Equal(progress, paused.Progress, 6);
        Assert.Contains("something of the sun's own metal beside it", paused.Wants);

        s.Pools.Place(nugget.Id, "sunward-basin");
        Tend(s, 240, feedOnly: true);
        Assert.Equal("waking-sunstone", stone.Def);
        Assert.Contains(s.Essence.Record("gold-nugget")!.Catalyzed, c => c.Contains("Blessed by the Sun"));
    }

    private static void Tend(GameSession s, int ticks, bool feedOnly = false)
    {
        for (int t = 0; t < ticks; t += 20)
        {
            s.AdvanceTicks(20);
            foreach (var p in s.State.Pools.Pools)
            {
                if (p.Lifeforms.Any(l => l.Hunger > 30)) s.Pools.Feed(p.Id);
                if (!feedOnly && p.Clarity < 80) s.Pools.Cleanse(p.Id);
            }
        }
    }

    // ───── Library ─────

    [Fact]
    public void ShelvingIsForgivingAndOrderIsVisible()
    {
        var s = GameSession.NewGame(TestContent.Db, 5);
        var tags = s.Content.Config.Library.Tags;
        for (int i = 0; i < tags.Count; i++) s.Library.TagShelf(i, tags[i]);
        int Shelf(string g) => s.Library.Shelves().First(x => x.Genre == g).Index;

        var trees = s.Content.Tomes["trees-of-the-upper-tiers"];
        var wrong = s.Library.Shelve(trees.Id, Shelf("history"));
        Assert.False(wrong.Ok);
        Assert.Contains(trees.Id, s.Library.Pile().Select(t => t.Id)); // back in your hands, nothing lost

        Assert.True(s.Library.Shelve(trees.Id, Shelf("horticulture")).Ok);
        var older = s.Content.Tomes["the-reed-and-its-uses"]; // SR 377, older than 512
        var r = s.Library.Shelve(older.Id, Shelf("horticulture")); // at the far end: out of order
        Assert.True(r.Ok);
        Assert.False(s.Library.Shelves()[Shelf("horticulture")].InOrder);
        s.Library.Reorder(Shelf("horticulture"), 1, 0);
        Assert.True(s.Library.Shelves()[Shelf("horticulture")].InOrder);
    }

    [Fact]
    public void AShelfSettlesWhenEveryBookOfItsGenreIsHomeInOrder()
    {
        var s = GameSession.NewGame(TestContent.Db, 5);
        var tags = s.Content.Config.Library.Tags;
        for (int i = 0; i < tags.Count; i++) s.Library.TagShelf(i, tags[i]);
        var shelf = s.Library.Shelves().First(x => x.Genre == "mathematics").Index;
        foreach (var t in s.Library.Pile().Where(t => t.Genre == "mathematics").OrderBy(t => t.Year).ToList())
            s.Library.Shelve(t.Id, shelf);
        Assert.True(s.Library.Shelves()[shelf].Settled);
        Assert.Equal(1, s.World.Counter("library.settled"));
    }

    [Fact]
    public void FadedPagesNeedTheRightLightAndStayReadOnceRead()
    {
        var s = GameSession.NewGame(TestContent.Db, 5);
        var tome = s.Content.Tomes["the-waterworks-of-stairfall"];
        var faded = tome.Pages.Single(p => p.Faded);
        Assert.False(s.Library.PageReadable(tome, faded));
        Assert.Null(s.Library.Open(tome.Id)!.Pages.Single(p => p.Faded).Text);
    }

    [Fact]
    public void ThePrelabeledShelvingModelWorksFromDataAlone()
    {
        var config = MiniContent.Base.Replace(
            "\"libraryConfig\": { \"shelvingModel\": \"tagged\", \"shelfCount\": 2, \"tags\": [\"plants\", \"sums\"], \"maxPages\": 3 }",
            "\"libraryConfig\": { \"shelvingModel\": \"prelabeled\", \"shelves\": [ { \"id\": \"p\", \"label\": \"Sigil of the Seed\", \"genre\": \"plants\" }, { \"id\": \"s\", \"label\": \"Sigil of the Square\", \"genre\": \"sums\" } ] }");
        var db = new MiniContent(withBase: false).Add("base.json", config).WithStone().Load();
        var s = GameSession.NewGame(db);
        Assert.False(s.Library.Tagged);
        Assert.Equal("Sigil of the Seed", s.Library.Shelves()[0].Label);
        Assert.True(s.Library.Shelve("a-book", 0).Ok);
    }

    // ───── Apothecary ─────

    [Fact]
    public void FailedMixesTeachAndDiscoveriesNeverRunOut()
    {
        var s = GameSession.NewGame(new MiniContent().WithStone().Load());
        var first = s.Apothecary.Mix(new[] { "water" }, null);
        Assert.False(first.Ok);
        Assert.Equal(1, s.State.Apothecary.Insight);
        s.Apothecary.Mix(new[] { "water" }, null); // tried before: nothing new, no insight
        Assert.Equal(1, s.State.Apothecary.Insight);
        var second = s.Apothecary.Mix(new[] { "ash" }, null);
        Assert.Contains(second.Lines, l => l.Kind == LineKind.Discovery && l.Text.Contains("Discovery Map"));
        Assert.NotEmpty(s.State.Apothecary.Hints);

        Assert.True(s.Apothecary.Mix(new[] { "ash", "water" }, null).Ok);
        Assert.Contains("mud", s.State.Apothecary.Known);
        Assert.True(s.Apothecary.Mix(new[] { "mud" }, "burner").Ok);
        Assert.True(s.Apothecary.Mix(new[] { "mud" }, "burner").Ok); // making more never uses anything up
        Assert.Contains("brick", s.State.Apothecary.Known);
        Assert.Contains("mud", s.State.Apothecary.Known);
    }

    [Fact]
    public void ACozyOnlyPlayerStillOpensTheSpiritPools()
    {
        var s = GameSession.NewGame(TestContent.Db, 21);
        var tags = s.Content.Config.Library.Tags;
        for (int i = 0; i < tags.Count; i++) s.Library.TagShelf(i, tags[i]);
        foreach (var t in s.Library.Pile().ToList())
        {
            var shelf = s.Library.Shelves().First(x => x.Genre == t.Genre).Index;
            s.Library.Shelve(t.Id, shelf, s.Library.Shelves()[shelf].Tomes.Count(x => x.Year <= t.Year));
        }
        foreach (var r in s.Content.Recipes.Values.Where(r => r.Tool != "spirit-still" && !r.Inputs.Contains("shrine-wax") && !r.Inputs.Contains("waxed-reed-pouch")).OrderBy(r => r.Id))
            Discover(s, r.Output);
        Assert.True(s.IsUnlocked("pools"), "shelving and alchemy alone should bring the Stillkeepers back");
        Assert.DoesNotContain("pressed-leaf", s.State.Evidence.Resolved);
        // The Stillkeepers return a book when they come back; shelve it and the Library is complete.
        foreach (var t in s.Library.Pile().ToList())
        {
            var shelf = s.Library.Shelves().First(x => x.Genre == t.Genre).Index;
            s.Library.Shelve(t.Id, shelf, s.Library.Shelves()[shelf].Tomes.Count(x => x.Year <= t.Year));
        }
        Assert.True(s.Library.Shelves().All(sh => sh.Settled));
        Assert.Contains("library-settled", s.State.FiredBeats);
    }

    private static void Discover(GameSession s, string element)
    {
        if (s.State.Apothecary.Known.Contains(element)) return;
        var recipe = s.Content.Recipes.Values.First(r => r.Output == element);
        foreach (var i in recipe.Inputs) Discover(s, i);
        s.Apothecary.Mix(recipe.Inputs, recipe.Tool);
    }

    // ───── Save / load ─────

    [Fact]
    public void SavesRoundTripExactly()
    {
        var s = WithPoolsOpen();
        s.AdvanceTicks(500);
        var json = s.SaveJson();
        var loaded = GameSession.LoadJson(TestContent.Db, json);
        Assert.Equal(Strip(json), Strip(loaded.SaveJson()));
        loaded.AdvanceTicks(10);
        s.AdvanceTicks(10);
        Assert.Equal(Strip(s.SaveJson()), Strip(loaded.SaveJson())); // deterministic RNG survives the save
    }

    [Fact]
    public void OfflineProgressIsATunable()
    {
        var capped = MiniContent.Base.Replace("\"seed\": 7", "\"seed\": 7, \"offlineProgress\": \"capped\", \"offlineCapTicks\": 100");
        var db = new MiniContent(withBase: false).Add("base.json", capped).WithStone().Load();
        var s = GameSession.NewGame(db);
        var json = s.SaveJson();
        var later = GameSession.LoadJson(db, json, DateTimeOffset.UtcNow.AddHours(5));
        Assert.Equal(100, later.World.Clock.Tick);

        var none = GameSession.LoadJson(new MiniContent().WithStone().Load(), GameSession.NewGame(new MiniContent().WithStone().Load()).SaveJson(), DateTimeOffset.UtcNow.AddHours(5));
        Assert.Equal(0, none.World.Clock.Tick);
    }

    private static string Strip(string json) =>
        System.Text.RegularExpressions.Regex.Replace(json, "\"(savedAtUtc|summary)\": \"[^\"]*\"", "");
}
