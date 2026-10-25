using Atrium.Core.Content;
using Atrium.Core.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace Atrium.Core.Tests;

/// <summary>A scripted player: thin helpers over <see cref="GameSession"/> for writing playthrough tests.</summary>
public sealed class Player
{
    public GameSession S { get; private set; }
    private readonly ITestOutputHelper? _out;

    public Player(ITestOutputHelper? output = null, ulong seed = 1310)
    {
        _out = output;
        S = GameSession.NewGame(TestContent.Db, seed);
    }

    public ContentDatabase C => S.Content;

    public ActionResult Log(ActionResult r, string what)
    {
        _out?.WriteLine($"> {what}");
        foreach (var l in r.Lines) _out?.WriteLine($"   [{l.Kind}] {l.Text}");
        foreach (var n in S.DrainNotices()) _out?.WriteLine($"   ** {n.Title}: {n.Text}");
        return r;
    }

    public ItemInstance Carried(string def) =>
        S.World.Items.Satchel.FirstOrDefault(i => i.Def == def)
        ?? throw new Xunit.Sdk.XunitException($"Expected '{def}' in the satchel; have: {string.Join(", ", S.World.Items.Satchel.Select(i => i.Def))}");

    public bool Has(string def) => S.World.Items.Satchel.Any(i => i.Def == def);

    public bool Surfaced(string reading) => S.World.Conditions.ReadingSurfaced(reading);

    public void AssertSurfaced(params string[] readings)
    {
        foreach (var r in readings) Assert.True(Surfaced(r), $"Expected reading '{r}' to be surfaced.");
    }

    // ───── Library ─────

    public void TagAllShelves()
    {
        var tags = C.Config.Library.Tags;
        for (int i = 0; i < tags.Count; i++) Log(S.Library.TagShelf(i, tags[i]), $"tag shelf {i} {tags[i]}");
    }

    public int ShelfFor(string genre) => S.Library.Shelves().First(s => s.Genre == genre).Index;

    public void Read(string tome) => Assert.NotNull(S.Library.Open(tome));

    public ActionResult Compare(string def, string tome, int page) =>
        Log(S.Library.Compare(Carried(def).Id, tome, page), $"compare {def} with {tome} p{page}");

    /// <summary>Shelves every tome on the pile in correct order (what a tidy player eventually does).</summary>
    public void ShelveEverything()
    {
        foreach (var t in S.Library.Pile().ToList())
        {
            var shelf = ShelfFor(t.Genre);
            var tomes = S.Library.Shelves()[shelf].Tomes;
            var pos = tomes.Count(x => x.Year <= t.Year);
            Assert.True(S.Library.Shelve(t.Id, shelf, pos).Ok, $"shelving {t.Id}");
        }
    }

    // ───── Apothecary ─────

    public ActionResult Mix(string? tool, params string[] inputs) =>
        Log(S.Apothecary.Mix(inputs, tool), $"mix {string.Join("+", inputs)}{(tool is null ? "" : " with " + tool)}");

    public ActionResult Work(string def, string? tool, params string[] with) =>
        Log(S.Apothecary.Experiment(Carried(def).Id, with, tool), $"work {def} {tool} {string.Join("+", with)}");

    public void Discover(string element)
    {
        if (S.State.Apothecary.Known.Contains(element)) return;
        var recipe = C.Recipes.Values.First(r => r.Output == element);
        foreach (var i in recipe.Inputs) Discover(i);
        Assert.True(Mix(recipe.Tool, recipe.Inputs.ToArray()).Ok, $"discovering {element}");
        Assert.Contains(element, S.State.Apothecary.Known);
    }

    public void Bottle(string element)
    {
        Discover(element);
        Assert.True(Log(S.Apothecary.Bottle(element), $"bottle {element}").Ok);
    }

    // ───── Sanctum ─────

    public ActionResult Inspect(string def) => Log(S.Sanctum.Inspect(Carried(def).Id, "magnifying-glass"), $"inspect {def}");

    public ActionResult Designate(string obj, string what, string origin, string holds) =>
        Log(S.Sanctum.Designate(obj, new Dictionary<string, string> { ["what"] = what, ["origin"] = origin, ["holds"] = holds }),
            $"designate {obj}: {what} / {origin} / {holds}");

    // ───── Pools ─────

    public ActionResult Place(string def, string pool) => Log(S.Pools.Place(Carried(def).Id, pool), $"place {def} in {pool}");

    public void TakeOutAll(string def)
    {
        foreach (var i in S.World.Items.All.Where(i => i.Def == def && Locations.IsPool(i.Location, out _)).ToList())
            Log(S.Pools.TakeOut(i.Id), $"take out {def}");
    }

    /// <summary>Lets time pass in small steps, feeding and cleaning like an attentive keeper.</summary>
    public void Tend(int ticks, int step = 30)
    {
        for (int t = 0; t < ticks; t += step)
        {
            S.AdvanceTicks(Math.Min(step, ticks - t));
            foreach (var p in C.Pools.Keys)
            {
                var st = S.Pools.State(p);
                if (st.Lifeforms.Any(l => l.Hunger > 40)) S.Pools.Feed(p);
                if (st.Clarity < 60) S.Pools.Cleanse(p);
            }
            S.DrainNotices();
        }
    }

    public GameSession SaveAndReload()
    {
        var json = S.SaveJson();
        S = GameSession.LoadJson(TestContent.Db, json);
        return S;
    }
}
