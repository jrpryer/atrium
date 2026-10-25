using Xunit;

namespace Atrium.Core.Tests;

/// <summary>
/// Plays to the middle of Chapter One and, when ATRIUM_SAMPLE_SAVES is set, writes the save there
/// (handy for trying late-game screens in the terminal UI without replaying).
/// </summary>
public class SampleSaves
{
    [Fact]
    public void MidChapterSaveLoads()
    {
        var p = new Player();
        var s = p.S;
        p.TagAllShelves();
        var leaf = s.Library.PileItems().Single();
        s.Library.ShelveObject(leaf.Id, 0);
        s.Library.TakeFromPile(leaf.Id);
        p.Compare("pressed-leaf", "trees-of-the-upper-tiers", 1);
        p.Compare("pressed-leaf", "signs-and-sigils-of-stairfall", 2);
        p.Work("pressed-leaf", "bunsen-burner");
        p.Designate("pressed-leaf", "vow-token", "high-hallows", "a-vow");
        s.Pools.Cleanse("sunward-basin");
        p.Work("gold-nugget", "weighing-scales");
        p.Compare("gold-nugget", "tithes-and-offerings-of-the-hallows", 1);
        p.Inspect("gold-nugget");
        p.Work("gold-nugget", "mortar-and-pestle");
        p.Designate("gold-nugget", "tithe-nail", "old-stoic", "a-promise");
        p.Discover("pressed-oil");
        p.Work("gold-flakes", "bunsen-burner", "pressed-oil", "patience");
        p.Bottle("lamp-glass");
        p.Read("the-lamps-of-the-charter");
        s.Sanctum.Assemble("lamplighters-lantern");
        p.Read("the-ledger-of-tides");
        p.Discover("waxed-reed-pouch");
        p.Bottle("reed-twine");
        p.Bottle("fired-clay");
        s.DrainNotices();
        Assert.True(p.Has("black-pearl"));

        var json = s.SaveJson();
        var dir = Environment.GetEnvironmentVariable("ATRIUM_SAMPLE_SAVES");
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "keeper.json"), json);
        }
        Assert.NotNull(GameSession.LoadJson(TestContent.Db, json));
    }
}
