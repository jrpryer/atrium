using Atrium.Core.Content;
using Atrium.Core.Locales;
using Atrium.Core.Runtime;
using Atrium.Terminal.Ui;

namespace Atrium.Terminal.Views;

public sealed class PoolsView : LocaleView
{
    public override string Title => "The Spirit Pools";
    public override string Locale => "pools";
    public override bool Live => true;

    public override void Render(Frame f)
    {
        var i = 1;
        foreach (var v in S.Pools.Pools())
        {
            var lfs = v.State.Lifeforms;
            var thriving = lfs.Count(l => l.Mood == Mood.Thriving);
            var sulking = lfs.Count(l => l.Mood == Mood.Sulking);
            f.Add($"  {Ansi.Fg(Palette.Dim, (i++).ToString())}  " + Ansi.BoldFg(Palette.Water, Cap(v.Pool.Name))
                  + Ansi.Fg(Palette.Dim, $"   light {Wrap.Signed(v.Energy.Light)} · heat {Wrap.Signed(v.Energy.Heat)} · clarity ")
                  + Ansi.Fg(v.State.Clarity >= 50 ? Palette.Water : Palette.Stone, Wrap.Bar(v.State.Clarity / 100, 10)));
            var mood = lfs.Count == 0 ? "empty" : $"{lfs.Count}/{v.Pool.Capacity} living · {thriving} thriving" + (sulking > 0 ? $" · {sulking} sulking" : "");
            f.Add("      " + Ansi.Fg(Palette.Text, mood) + (lfs.Any(l => l.Expecting is not null) ? Ansi.Fg(Palette.Moss, " · eggs!") : ""));
            if (v.Items.Count > 0) f.Add("      " + Ansi.Fg(Palette.Lamp, "in the water: " + string.Join(", ", v.Items.Select(S.World.Items.Name))));
            foreach (var soak in v.Soaks)
                f.Add("      " + Ansi.Fg(Palette.Moss, $"{soak.Transform}: ") + Ansi.Fg(soak.ConditionsMet ? Palette.Moss : Palette.Faint, Wrap.Bar(soak.Progress, 16))
                      + Ansi.Fg(Palette.Dim, soak.ConditionsMet ? "" : "  (paused; it wants " + string.Join(", ", soak.Wants) + ")"));
        }
        var bag = S.Pools.DruidsBag.ToList();
        f.Add();
        f.Add("  " + Ansi.Fg(Palette.Gold, "The druids' bag: ") + Ansi.Fg(Palette.Text, bag.Count == 0 ? "empty" : string.Join(", ", bag.Select(S.World.Items.Name))));
        if (S.Pools.Holding.Count > 0) f.Add("  " + Ansi.Fg(Palette.Gold, "Resting in the holding crock: ") + Ansi.Fg(Palette.Text, string.Join(", ", S.Pools.Holding.Select(l => $"{l.Name} the {C.LifeformName(l.Species)}"))));
    }

    internal static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    public override IEnumerable<MenuItem> Menu()
    {
        var i = 1;
        foreach (var p in C.Pools.Values)
        {
            var id = p.Id;
            yield return new MenuItem((i++).ToString(), "Go to " + p.Name.Replace("the ", ""), () => A.Run(new PoolScreen(id)));
        }
        yield return new MenuItem("l", "Breeding ledger", () => A.Run(new LedgerView()));
        yield return new MenuItem("w", "Sit and watch the water", () => A.Say(S.SitAWhile()));
        foreach (var m in Common()) yield return m;
    }
}

public sealed class PoolScreen : View
{
    private readonly string _pool;
    public PoolScreen(string pool) => _pool = pool;

    public override string Title => PoolsView.Cap(C.Pools[_pool].Name);
    public override string Locale => "pools";
    public override bool Live => true;

    public override void Render(Frame f)
    {
        var v = S.Pools.View(_pool);
        f.Text(v.Pool.Description, Palette.Dim);
        f.Add();
        var plane = Screen.Height >= 40 || !Screen.Interactive ? Plane(v) : new List<string>();
        var info = new List<string>
        {
            Ansi.Fg(Palette.Gold, "Light ") + Ansi.Fg(Palette.Text, Wrap.Signed(v.Energy.Light)) + Ansi.Fg(Palette.Gold, "   Heat ") + Ansi.Fg(Palette.Text, Wrap.Signed(v.Energy.Heat)),
            Ansi.Fg(Palette.Gold, "Clarity ") + Ansi.Fg(Palette.Water, Wrap.Bar(v.State.Clarity / 100, 12)) + Ansi.Fg(Palette.Dim, $" {v.State.Clarity:0}"),
            Ansi.Fg(Palette.Gold, "Room ") + Ansi.Fg(Palette.Text, $"{v.State.Lifeforms.Count} of {v.Pool.Capacity}"),
            "",
            Ansi.Fg(Palette.Water, "◎") + Ansi.Fg(Palette.Dim, " this pool now"),
            Ansi.Fg(Palette.Moss, "░") + Ansi.Fg(Palette.Dim, " where everyone here would thrive"),
        };
        if (v.Pool.SpawningPond) info.Add(Ansi.Fg(Palette.Dim, "A spawning pond: lay minerals and auras here."));
        for (int i = 0; i < Math.Max(plane.Count, info.Count); i++)
            f.Add("  " + Ansi.PadRight(i < plane.Count ? plane[i] : "", 30) + "   " + (i < info.Count ? info[i] : ""));
        f.Add();
        foreach (var lf in v.State.Lifeforms)
        {
            var sp = C.Lifeforms[lf.Species];
            var sex = lf.Sex switch { Sex.Female => "♀", Sex.Male => "♂", _ => "·" };
            var moodColor = lf.Mood switch { Mood.Thriving => Palette.Moss, Mood.Content => Palette.Text, _ => Palette.Stone };
            var mood = lf.Mood switch { Mood.Thriving => "thriving", Mood.Content => "content", _ => "sulking" } + (lf.MoodReason.Length > 0 ? $" ({lf.MoodReason})" : "");
            var hunger = sp.HungerPerTick > 0 ? Ansi.Fg(Palette.Faint, "  hunger ") + Ansi.Fg(lf.Hunger > 55 ? Palette.Copper : Palette.Dim, Wrap.Bar(lf.Hunger / 100, 6)) : "";
            var eggs = lf.Expecting is not null ? Ansi.Fg(Palette.Moss, lf.Gestation > 0 ? $"  carrying eggs, {S.World.Clock.Describe(lf.Gestation)} to go" : "  eggs ready, waiting for room") : "";
            f.Add($"  {Ansi.Fg(Palette.Water, sex)} {Ansi.Fg(Palette.Text, lf.Name)} {Ansi.Fg(Palette.Dim, "the " + sp.Name)}  " + Ansi.Fg(moodColor, mood) + hunger + eggs);
        }
        if (v.Items.Count > 0)
        {
            f.Add();
            foreach (var it in v.Items)
            {
                var e = S.World.Items.Energy(it);
                f.Add("  " + Ansi.Fg(Palette.Lamp, "◆ " + S.World.Items.Name(it)) + Ansi.Fg(Palette.Dim, e.IsZero ? "" : $"  (light {Wrap.Signed(e.Light)}, heat {Wrap.Signed(e.Heat)})"));
            }
        }
        foreach (var soak in v.Soaks)
        {
            f.Add("  " + Ansi.Fg(Palette.Moss, $"{soak.Transform} — {soak.Item}: ") + Ansi.Fg(soak.ConditionsMet ? Palette.Moss : Palette.Faint, Wrap.Bar(soak.Progress, 20)));
            var remaining = (int)Math.Ceiling((1 - soak.Progress) * soak.EffectiveTicks);
            f.Add("     " + Ansi.Fg(Palette.Dim, soak.ConditionsMet ? $"{soak.Text} About {S.World.Clock.Describe(remaining)} to go."
                : "Paused, but nothing is lost. It seems to want " + string.Join(", ", soak.Wants) + "."));
        }
    }

    /// <summary>The Light/Dark × Hot/Cold plane, Light up, Hot right.</summary>
    private List<string> Plane(PoolView v)
    {
        var light = C.Axis("light"); var heat = C.Axis("heat");
        var species = v.State.Lifeforms.Select(l => C.Lifeforms[l.Species]).Distinct().ToList();
        var rows = new List<string> { Ansi.Fg(Palette.Faint, "        Light") };
        for (int y = light.Max; y >= light.Min; y--)
        {
            var row = Ansi.Fg(Palette.Faint, $"{Wrap.Signed(y),3} ");
            for (int x = heat.Min; x <= heat.Max; x++)
            {
                var e = new Energy(y, x);
                if (v.Energy.Light == y && v.Energy.Heat == x) row += Ansi.BoldFg(Palette.Water, "◎ ");
                else if (species.Count > 0 && species.All(s => s.Thrive.Contains(e))) row += Ansi.Fg(Palette.Moss, "░ ");
                else row += Ansi.Fg(Palette.Faint, (x == 0 || y == 0) ? "+ " : "· ");
            }
            rows.Add(row);
        }
        rows.Add(Ansi.Fg(Palette.Faint, "    Cold        Hot"));
        return rows;
    }

    public override IEnumerable<MenuItem> Menu()
    {
        var v = S.Pools.View(_pool);
        var carryable = S.World.Items.Satchel.Concat(S.Pools.DruidsBag).ToList();
        yield return new MenuItem("c", "Cleanse the water", () => A.Say(S.Pools.Cleanse(_pool)));
        yield return new MenuItem("f", "Feed (pond flakes)", () => A.Say(S.Pools.Feed(_pool)));
        yield return new MenuItem("g", "Feed something from your satchel", FeedFrom, S.World.Items.Satchel.Any(i => S.World.Items.Def(i).FoodValue > 0));
        yield return new MenuItem("p", "Lay something in the water", Place, carryable.Count > 0);
        yield return new MenuItem("t", "Take something out", TakeOut, v.Items.Count > 0);
        yield return new MenuItem("m", "Move a creature", Move, v.State.Lifeforms.Count > 0 || S.Pools.Holding.Count > 0);
        yield return new MenuItem("i", "Look closely at a creature", Inspect, v.State.Lifeforms.Count > 0);
        yield return new MenuItem("w", "Sit and watch", () => A.Say(S.SitAWhile()));
        yield return new MenuItem("x", "Back", Close);
    }

    private void FeedFrom()
    {
        var food = A.Pick("Feed what?", S.World.Items.Satchel.Where(i => S.World.Items.Def(i).FoodValue > 0).ToList(), i => S.World.Items.Name(i));
        if (food is not null) A.Say(S.Pools.Feed(_pool, food.Id));
    }

    private void Place()
    {
        var items = S.World.Items.Satchel.Concat(S.Pools.DruidsBag).ToList();
        var item = A.Pick("Lay what in the water?", items, i => S.World.Items.Name(i) + (i.Location == Locations.DruidsBag ? "  (druids' bag)" : ""),
            i => S.World.Items.Energy(i) is { IsZero: false } e ? $"light {Wrap.Signed(e.Light)}, heat {Wrap.Signed(e.Heat)}" : null);
        if (item is not null) A.Say(S.Pools.Place(item.Id, _pool), "In the water");
    }

    private void TakeOut()
    {
        var item = A.Pick("Take what out?", S.Pools.View(_pool).Items, i => S.World.Items.Name(i));
        if (item is not null) A.Say(S.Pools.TakeOut(item.Id));
    }

    private sealed record Creature(LifeformInstance L, string Where);
    private sealed record Dest(string? Pool, string Label);

    private void Move()
    {
        var here = S.Pools.View(_pool).State.Lifeforms.Select(l => new Creature(l, "here"))
            .Concat(S.Pools.Holding.Select(l => new Creature(l, "resting in the crock"))).ToList();
        var c = A.Pick("Move which creature?", here, x => $"{x.L.Name} the {C.LifeformName(x.L.Species)} ({x.Where})");
        if (c is null) return;
        var dests = C.Pools.Values.Select(p => new Dest(p.Id, PoolsView.Cap(p.Name))).Append(new Dest(null, PoolsView.Cap(C.Config.Pools.HoldingName))).ToList();
        var d = A.Pick("Move it where?", dests, x => x.Label);
        if (d is not null) A.Say(S.Pools.MoveLifeform(c.L.Id, d.Pool));
    }

    private void Inspect()
    {
        var c = A.Pick("Which one?", S.Pools.View(_pool).State.Lifeforms, l => $"{l.Name} the {C.LifeformName(l.Species)}");
        if (c is null) return;
        var sp = C.Lifeforms[c.Species];
        A.Card($"{c.Name} the {sp.Name}", new[]
        {
            new Line(LineKind.Narration, sp.Description),
            new Line(LineKind.System, $"Bears light {sp.Survive.Light}, heat {sp.Survive.Heat}. Thrives at light {sp.Thrive.Light}, heat {sp.Thrive.Heat}."),
            new Line(LineKind.System, (sp.HungerPerTick > 0 ? "Needs feeding. " : "Never needs feeding. ")
                                      + (sp.WastePerTick < 0 ? "Keeps the water clean." : sp.WastePerTick > 0 ? "Muddies the water a little." : "")
                                      + (sp.Emits.IsZero ? "" : $" Changes the water around it: light {Wrap.Signed(sp.Emits.Light)}, heat {Wrap.Signed(sp.Emits.Heat)}.")),
        });
    }
}

public sealed class LedgerView : View
{
    public override string Title => "The Stillkeepers' breeding ledger";
    public override string Locale => "pools";

    public override void Render(Frame f)
    {
        f.Text("Every kind you've kept, and every pairing you've seen work. Brother Alder adds the odd comment in green ink.", Palette.Dim);
        f.Add();
        f.Add("  " + Ansi.Fg(Palette.Gold, "Kinds kept:"));
        foreach (var id in S.Pools.SpeciesSeen())
        {
            var sp = C.Lifeforms[id];
            f.Add("     " + Ansi.Fg(Palette.Water, sp.Name) + Ansi.Fg(Palette.Dim, $"  thrives at light {sp.Thrive.Light}, heat {sp.Thrive.Heat}"));
        }
        f.Add();
        f.Add("  " + Ansi.Fg(Palette.Gold, "Pairings seen:"));
        var rules = S.Pools.KnownRules();
        if (rules.Count == 0) f.Add("     " + Ansi.Fg(Palette.Faint, "Only like with like, so far."));
        foreach (var r in rules)
        {
            var pair = r.Ordered ? $"a {C.LifeformName(r.ParentA)} mother and a {C.LifeformName(r.ParentB)} father"
                : r.ParentA == r.ParentB ? $"two {C.LifeformName(r.ParentA)}s" : $"{C.LifeformName(r.ParentA)} with {C.LifeformName(r.ParentB)}";
            var parts = new List<string>();
            if (r.Requires is { } q)
            {
                if (q.Light.Min != int.MinValue || q.Light.Max != int.MaxValue) parts.Add($"light {q.Light}");
                if (q.Heat.Min != int.MinValue || q.Heat.Max != int.MaxValue) parts.Add($"heat {q.Heat}");
            }
            var cond = parts.Count == 0 ? "" : $" in water at {string.Join(", ", parts)}";
            f.Add("     " + Ansi.Fg(Palette.Text, $"{Cap(pair)}{cond}: ") + Ansi.Fg(Palette.Moss, string.Join(" or ", r.Offspring.Select(o => C.LifeformName(o.Lifeform)))));
        }
    }

    private static string Cap(string s) => PoolsView.Cap(s);

    public override IEnumerable<MenuItem> Menu()
    {
        yield return new MenuItem("x", "Back", Close);
    }
}
