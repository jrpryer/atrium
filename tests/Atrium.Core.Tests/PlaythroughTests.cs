using Atrium.Core.Content;
using Atrium.Core.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace Atrium.Core.Tests;

/// <summary>
/// Chapter One, start to finish, through the public API only. Doubles as a walkthrough of the intended
/// synthesis for each clue-object, and checks the invariants along the way.
/// </summary>
public class PlaythroughTests
{
    private readonly ITestOutputHelper _out;
    public PlaythroughTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void ChapterOneCanBeCompleted()
    {
        var p = new Player(_out);
        var s = p.S;
        var courtyardAtStart = (p.S.Hub.Caption, p.S.Hub.Description, string.Join("|", p.S.Hub.Art));

        // ── Opening: Library, Apothecary and Sanctum open; Pools shut. ──
        Assert.True(s.IsUnlocked("library"));
        Assert.True(s.IsUnlocked("apothecary"));
        Assert.True(s.IsUnlocked("sanctum"));
        Assert.False(s.IsUnlocked("pools"));
        p.Log(s.Enter("library"), "enter library");

        // ── 1. The pressed leaf ──
        var leaf = s.Library.PileItems().Single();
        Assert.Equal("pressed-leaf", leaf.Def);
        p.TagAllShelves();
        p.Log(s.Library.ShelveObject(leaf.Id, 0), "try to shelve the leaf");
        p.Log(s.Library.TakeFromPile(leaf.Id), "take leaf");
        p.Compare("pressed-leaf", "trees-of-the-upper-tiers", 1);
        p.Compare("pressed-leaf", "signs-and-sigils-of-stairfall", 2);
        p.Work("pressed-leaf", "weighing-scales");
        p.Work("pressed-leaf", "bunsen-burner");
        p.Work("pressed-leaf", null, "spring-water");
        p.Inspect("pressed-leaf");
        p.AssertSurfaced("leaf.misfiled", "leaf.lantern-birch", "leaf.votive", "leaf.wax", "leaf.script", "leaf.spring", "leaf.pinholes");

        // A plausible-wrong answer earns a unique near-miss once; a repeat earns nothing new.
        var miss = p.Designate("pressed-leaf", "specimen", "high-hallows", "a-record");
        Assert.Contains(miss.Lines, l => l.Kind == LineKind.NearMiss);
        var again = p.Designate("pressed-leaf", "specimen", "high-hallows", "a-record");
        Assert.DoesNotContain(again.Lines, l => l.Kind == LineKind.NearMiss);
        var nonsense = p.Designate("pressed-leaf", "keepsake", "high-hallows", "a-record");
        Assert.All(nonsense.Lines, l => Assert.Equal(LineKind.Soft, l.Kind));

        var solved = p.Designate("pressed-leaf", "vow-token", "high-hallows", "a-vow");
        Assert.Contains(solved.Lines, l => l.Kind == LineKind.Resolved);
        Assert.True(s.IsUnlocked("pools"), "resolving the leaf opens the Spirit Pools");
        Assert.Contains("shrine-wax", s.State.Apothecary.Known);
        Assert.Contains("unkept-vow", s.State.FiredBeats);

        // ── 2. The gold nugget: found by cleansing the Sunward Basin. ──
        p.Log(s.Enter("pools"), "enter pools");
        p.Log(s.Pools.Cleanse("sunward-basin"), "cleanse sunward");
        Assert.True(p.Has("gold-nugget"));
        p.AssertSurfaced("nugget.silt");
        p.Place("gold-nugget", "sunward-basin");
        p.TakeOutAll("gold-nugget");
        p.Work("gold-nugget", "weighing-scales");
        p.Compare("gold-nugget", "tithes-and-offerings-of-the-hallows", 1);
        p.Inspect("gold-nugget");

        // Crushing reveals the truth and leaves a remnant; the Circle wakes.
        Assert.NotNull(s.Apothecary.PreviewExperiment(p.Carried("gold-nugget").Id, Array.Empty<string>(), "mortar-and-pestle"));
        Assert.False(s.Sanctum.CircleAvailable);
        p.Work("gold-nugget", "mortar-and-pestle");
        Assert.True(p.Has("gold-flakes"));
        Assert.True(p.Has("iron-slag"));
        Assert.False(p.Has("gold-nugget"));
        Assert.True(s.Sanctum.CircleAvailable);
        p.AssertSurfaced("nugget.glow", "nugget.light", "nugget.tithes", "nugget.bark", "nugget.iron-nail");

        var nearMiss = p.Designate("gold-nugget", "river-gold", "atrium-waters", "stored-sunlight");
        Assert.Contains(nearMiss.Lines, l => l.Kind == LineKind.NearMiss);
        p.Designate("gold-nugget", "tithe-nail", "old-stoic", "a-promise");
        Assert.Contains("gold-nugget", s.State.Evidence.Resolved);
        Assert.True(p.Has("cold-sunstone"), "Tamsin hands over the cold stone after the nugget is understood");

        // ── The Lamplighter's Lantern: gilded oil from the flakes (true wealth, glittering and rare). ──
        p.Discover("pressed-oil");
        p.Work("gold-flakes", "bunsen-burner", "pressed-oil", "patience");
        Assert.True(p.Has("gilded-lamp-oil"));
        p.Bottle("lamp-glass");
        p.Read("the-lamps-of-the-charter");
        Assert.Contains(s.Sanctum.Blueprints(), b => b.Tool.Id == "lamplighters-lantern" && b.CanAssemble);
        p.Log(s.Sanctum.Assemble("lamplighters-lantern"), "assemble lantern");
        Assert.True(s.World.Conditions.HasCapability("library.read-faded"));
        Assert.Equal(p.Carried("lamplighters-lantern").Id, s.World.Items.Carrier("gold-nugget")!.Id);

        // ── 3. The cold stone needs the nugget whole: the Circle warns, restores, and the lantern goes dark. ──
        p.Work("cold-sunstone", "bunsen-burner");
        p.Compare("cold-sunstone", "the-lamps-of-the-charter", 1);
        p.Compare("cold-sunstone", "amber-and-its-imitations", 1);
        p.Inspect("cold-sunstone");

        var slag = p.Carried("iron-slag");
        var preview = s.Sanctum.PreviewReconstitution(slag.Id)!;
        _out.WriteLine($"Circle preview: resides {preview.CurrentResidence}; inert: {string.Join(" | ", preview.WillGoInert)}; retrace: {string.Join(" → ", preview.Retrace)}");
        Assert.Contains("Lamplighter's Lantern", preview.CurrentResidence);
        Assert.Single(preview.WillGoInert);
        Assert.Equal(new[] { "Gold Flakes", "Gilded Lamp-Oil", "Lamplighter's Lantern" }, preview.Retrace);
        p.Log(s.Sanctum.Reconstitute(slag.Id), "reconstitute nugget");
        Assert.True(p.Has("gold-nugget"));
        Assert.False(p.Has("iron-slag"));
        Assert.True(s.World.Items.All.Single(i => i.Def == "lamplighters-lantern").Inert);
        Assert.False(s.World.Conditions.HasCapability("library.read-faded"));
        Assert.Contains("gold-nugget", s.State.Evidence.Resolved); // knowledge is permanent

        p.Place("gold-nugget", "sunward-basin");
        p.Place("cold-sunstone", "sunward-basin");
        var soak = s.Pools.View("sunward-basin").Soaks.Single();
        Assert.True(soak.ConditionsMet, "the Sunward Basin with the nugget and snails should bless the stone");
        Assert.Equal(300, soak.EffectiveTicks);
        p.Tend(320);
        Assert.Contains(s.World.Items.All, i => i.Def == "waking-sunstone");
        p.AssertSurfaced("sunstone.blessed", "sunstone.egg");
        Assert.Contains(s.Essence.Record("gold-nugget")!.Catalyzed, c => c.Contains("Blessed by the Sun"));
        p.TakeOutAll("waking-sunstone");

        var amberMiss = p.Designate("cold-sunstone", "amber", "the-gallery", "a-sleeping-life");
        Assert.Contains(amberMiss.Lines, l => l.Kind == LineKind.NearMiss);
        p.Designate("cold-sunstone", "lampstone", "the-crown", "stored-sunlight");
        Assert.Contains("lamps-relit", s.State.FiredBeats);
        Assert.Contains(s.World.Items.At(Locations.DruidsBag), i => i.Def == "sun-mote-aura");

        // ── Save and reload mid-chapter: nothing changes. ──
        var before = s.SaveJson();
        s = p.SaveAndReload();
        Assert.Equal(Strip(before), Strip(s.SaveJson()));

        // ── 4. The black pearl: in the hollow Ledger of Tides, returned when the Pools opened. ──
        Assert.Contains(s.Library.Pile(), t => t.Id == "the-ledger-of-tides");
        p.Read("the-ledger-of-tides");
        Assert.True(p.Has("black-pearl"));
        p.Compare("black-pearl", "the-lamp-divers-of-the-ribs", 1);
        p.Compare("black-pearl", "mourning-customs-of-the-hem", 1);
        p.Inspect("black-pearl");
        p.Work("black-pearl", "mortar-and-pestle"); // no pouch yet: it pushes back, nothing breaks
        Assert.True(p.Has("black-pearl"));
        p.Log(s.Sanctum.Offer("stillkeepers-balance", p.Carried("black-pearl").Id), "offer pearl to the balance");
        Assert.False(s.Sanctum.Solved("stillkeepers-balance"));

        // Catalysis: in the Shade Pool the pearl's darkness lets Grey Guppies hatch a Shadow Guppy.
        p.Log(s.Pools.Cleanse("shade-pool"), "cleanse shade");
        p.Place("black-pearl", "shade-pool");
        Assert.True(s.Pools.Energy("shade-pool").Light <= -4);
        p.Tend(700);
        Assert.Contains("shadow-guppy", s.State.Pools.SpeciesSeen);
        Assert.Contains(s.Essence.Record("black-pearl")!.Catalyzed, c => c.Contains("Shadow Guppy"));
        p.TakeOutAll("black-pearl");

        var jet = p.Designate("black-pearl", "jet-bead", "the-hem", "grief");
        Assert.Contains(jet.Lines, l => l.Kind == LineKind.NearMiss);
        p.Designate("black-pearl", "smoke-pearl", "the-ribs", "sealed-flame");
        Assert.Contains("black-pearl", s.State.Evidence.Resolved);

        // Containment: crush the pearl into a waxed pouch → Smoke-In-A-Bag (+ husk). It opens the Balance.
        p.Discover("waxed-reed-pouch");
        p.Work("black-pearl", "mortar-and-pestle", "waxed-reed-pouch");
        Assert.True(p.Has("smoke-in-a-bag"));
        Assert.True(p.Has("pearl-husk"));
        p.Log(s.Sanctum.Offer("stillkeepers-balance", p.Carried("smoke-in-a-bag").Id), "offer smoke to the balance");
        Assert.True(s.Sanctum.Solved("stillkeepers-balance"));
        Assert.True(p.Has("sluice-key"), "the reliquary releases the great key");

        // Restoring the pearl makes only the bag inert; the guppy and the open Balance persist.
        var husk = p.Carried("pearl-husk");
        var pearlPreview = s.Sanctum.PreviewReconstitution(husk.Id)!;
        Assert.Single(pearlPreview.WillGoInert);
        Assert.Contains("Smoke-In-A-Bag", pearlPreview.WillGoInert[0]);
        Assert.DoesNotContain(pearlPreview.WillGoInert, w => w.Contains("Guppy") || w.Contains("Balance"));
        p.Log(s.Sanctum.Reconstitute(husk.Id), "reconstitute pearl");
        Assert.True(p.Has("black-pearl"));
        Assert.True(s.World.Items.All.Single(i => i.Def == "smoke-in-a-bag").Inert);
        Assert.True(s.Sanctum.Solved("stillkeepers-balance"));
        Assert.Contains(s.Pools.State("shade-pool").Lifeforms, l => l.Species == "shadow-guppy");

        // ── 5. The great key. ──
        p.Inspect("sluice-key");
        p.Compare("sluice-key", "the-waterworks-of-stairfall", 1);
        p.Place("sluice-key", "shade-pool");
        p.TakeOutAll("sluice-key");
        p.Designate("sluice-key", "sluice-key", "the-crown", "the-old-course");
        Assert.Contains(s.State.Evidence.NearMissesGiven, c => c == "key.crown");
        p.Designate("sluice-key", "sluice-key", "gallery-reach", "the-old-course");

        Assert.True(s.ChapterComplete);
        Assert.Contains("chapter-one", s.State.FiredBeats);
        // Old Stoic and the falls look exactly the same after the whole chapter as before it.
        Assert.Equal(courtyardAtStart, (s.Hub.Caption, s.Hub.Description, string.Join("|", s.Hub.Art)));

        // ── Epilogue: relight the lantern (the nugget again, this time knowing for certain) and read the margin. ──
        p.TakeOutAll("gold-nugget");
        p.Work("gold-nugget", "mortar-and-pestle");
        p.Work("gold-flakes", "bunsen-burner", "pressed-oil", "patience");
        p.Bottle("lamp-glass");
        p.Log(s.Sanctum.Assemble("lamplighters-lantern"), "re-assemble lantern");
        Assert.True(s.World.Conditions.HasCapability("library.read-faded"));
        p.Read("the-waterworks-of-stairfall");
        p.Compare("sluice-key", "the-waterworks-of-stairfall", 3);
        Assert.Contains("pilgrims-hand", s.State.FiredBeats);

        s.Essence.AssertInvariant();
        _out.WriteLine($"Finished at tick {s.World.Clock.Tick}; {s.State.Log.Count} log entries; {s.State.Apothecary.Known.Count} elements known.");
    }

    private static string Strip(string json) =>
        System.Text.RegularExpressions.Regex.Replace(json, "\"savedAtUtc\": \"[^\"]*\"", "");
}
