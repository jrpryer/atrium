using Atrium.Core.Content;
using Atrium.Core.Mystery;
using Atrium.Core.Runtime;
using Xunit;

namespace Atrium.Core.Tests;

/// <summary>The evidence → gate → synthesis spine, and the essence/remnant rules, on minimal content.</summary>
public class SpineTests
{
    private static (GameSession S, ItemInstance Stone) NewStone(string? configPatch = null)
    {
        var mini = new MiniContent(withBase: false).Add("base.json", configPatch is null ? MiniContent.Base : MiniContent.Base.Replace("\"seed\": 7", configPatch)).WithStone();
        var s = GameSession.NewGame(mini.Load());
        return (s, s.World.Items.Satchel.Single(i => i.Def == "stone"));
    }

    private static Dictionary<string, string> D(string what, string origin) => new() { ["what"] = what, ["origin"] = origin };

    [Fact]
    public void NearMissNeedsASecondLocalesEvidence()
    {
        var (s, stone) = NewStone();
        s.Library.Compare(stone.Id, "a-book", 1); // one lens only: the Library
        var single = s.Resolver.Attempt("stone", D("leafy", "high"));
        Assert.Equal(SynthesisKind.NotYetComprehensible, single.Kind);

        s.Sanctum.Inspect(stone.Id, "glass"); // a second lens
        var miss = s.Resolver.Attempt("stone", D("leafy", "high"));
        Assert.Equal(SynthesisKind.NearMiss, miss.Kind);
        Assert.Equal("Not leafy at all, as the burner showed.", miss.Text);

        var again = s.Resolver.Attempt("stone", D("leafy", "high"));
        Assert.Equal(SynthesisKind.AlreadyConsidered, again.Kind);
    }

    [Fact]
    public void TheGateNeverRevealsCorrectnessAndWrongAnswersCostNothing()
    {
        var (s, stone) = NewStone();
        s.Library.Compare(stone.Id, "a-book", 1);
        var itemsBefore = s.State.Items.Count;
        var surfacedBefore = s.State.Evidence.Surfaced.Values.Sum(v => v.Count);

        // The true designation before its evidence is in: "not yet", exactly like any premature claim.
        var early = s.Resolver.Attempt("stone", D("stony", "low"));
        Assert.Equal(SynthesisKind.NotYetComprehensible, early.Kind);
        var invalid = s.Resolver.Attempt("stone", D("stony", "high"));
        Assert.Equal(SynthesisKind.Invalid, invalid.Kind);

        Assert.Equal(itemsBefore, s.State.Items.Count);
        Assert.Equal(surfacedBefore, s.State.Evidence.Surfaced.Values.Sum(v => v.Count));
        Assert.DoesNotContain("stone", s.State.Evidence.Resolved);

        s.Apothecary.Experiment(stone.Id, Array.Empty<string>(), "burner");
        s.Sanctum.Inspect(stone.Id, "glass");
        Assert.Equal(SynthesisKind.Resolved, s.Resolver.Attempt("stone", D("stony", "low")).Kind);
    }

    [Fact]
    public void DerivationMovesTheEssenceAndLeavesARemnant()
    {
        var (s, stone) = NewStone();
        Assert.False(s.Sanctum.CircleAvailable);
        s.Apothecary.Experiment(stone.Id, Array.Empty<string>(), "mortar");
        var dust = s.World.Items.Satchel.Single(i => i.Def == "dust");
        var core = s.World.Items.Satchel.Single(i => i.Def == "core");
        Assert.Equal("stone", dust.Essence);
        Assert.Null(core.Essence);
        Assert.Equal("stone", core.RemnantOf);
        Assert.DoesNotContain(s.State.Items, i => i.Def == "stone");
        Assert.True(s.Sanctum.CircleAvailable, "the Circle wakes once a remnant exists");
        s.Essence.AssertInvariant();
    }

    [Fact]
    public void ReconstitutionNullifiesOnlyTheContainmentChain()
    {
        var (s, stone) = NewStone();
        // Catalysis first: the stone darkens a pool enough to matter, and the outcome is recorded.
        s.Essence.RecordCatalysis("stone", "Helped something hatch.");
        s.Apothecary.Experiment(stone.Id, Array.Empty<string>(), "mortar");
        var core = s.World.Items.Satchel.Single(i => i.Def == "core");

        var preview = s.Sanctum.PreviewReconstitution(core.Id)!;
        Assert.Single(preview.WillGoInert);
        Assert.Contains("Dust", preview.WillGoInert[0]);
        Assert.DoesNotContain(preview.WillGoInert, w => w.Contains("hatch"));

        s.Sanctum.Reconstitute(core.Id);
        Assert.True(s.State.Items.Single(i => i.Def == "dust").Inert);
        Assert.Equal("stone", s.World.Items.Carrier("stone")!.Def);
        Assert.Contains("Helped something hatch.", s.Essence.Record("stone")!.Catalyzed);
        Assert.Empty(s.World.Items.Traits(s.State.Items.Single(i => i.Def == "dust")));
        s.Essence.AssertInvariant();
    }

    [Fact]
    public void TheOneInstanceRuleIsAsserted()
    {
        var (s, _) = NewStone();
        var dup = s.World.Items.Create("stone", Locations.Satchel, 1, "stone");
        Assert.Throws<InvariantViolationException>(() => s.Essence.AssertInvariant());
        s.State.Items.Remove(dup);
        s.Essence.AssertInvariant();
    }

    [Fact]
    public void EssenceCarriersCannotBeConsumedDirectly()
    {
        var (s, stone) = NewStone();
        Assert.Throws<InvariantViolationException>(() => s.World.Items.Consume(stone));
    }

    [Fact]
    public void FunnelStrengthIsATunable()
    {
        var (gentle, _) = NewStone();
        Assert.Contains(gentle.Enter("apothecary").Lines, l => l.Text.Contains("Still unexplained"));

        var (off, _) = NewStone("\"seed\": 7, \"funnel\": \"off\"");
        Assert.DoesNotContain(off.Enter("apothecary").Lines, l => l.Text.Contains("Still unexplained"));
    }

    [Fact]
    public void LexiconModesFilterTheDesignationCard()
    {
        var (s, stone) = NewStone("\"seed\": 7, \"lexiconMode\": \"perObject\"");
        Assert.All(s.Sanctum.Card("stone").Facets, f => Assert.Empty(f.Terms));
        s.Apothecary.Experiment(stone.Id, Array.Empty<string>(), "burner");
        var origin = s.Sanctum.Card("stone").Facets.Single(f => f.Facet.Id == "origin");
        Assert.Equal(new[] { "low" }, origin.Terms.Select(t => t.Id));
    }
}
