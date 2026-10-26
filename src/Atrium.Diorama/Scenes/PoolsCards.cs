using System.Numerics;
using Atrium.Core.Content;
using Atrium.Core.Locales;
using Atrium.Core.Runtime;
using Atrium.Diorama.App;
using Atrium.Diorama.Overlays;
using Atrium.Diorama.Render;
using Atrium.Diorama.UI;
using Raylib_cs;
using LifeMood = Atrium.Core.Runtime.Mood;

namespace Atrium.Diorama.Scenes;

/// <summary>
/// The Spirit Pools' working cards: a tank's card (its place on the Light/Dark × Hot/Cold plane, its residents and
/// what's soaking in it), the holding crock, the druids' bag and the breeding ledger. Every action calls PoolsLocale.
/// </summary>
public sealed partial class PoolsScene
{
    private enum CardKind { None, Pool, Holding, Bag, Ledger }

    private CardKind _card;
    private string _cardPool = "";
    private float _cardAge;
    private int? _inspect;   // the resident whose bands are drawn on the plane (hovered in the list)

    private sealed record Choice(string Label, Action Run);

    // ───────────────────────── opening and closing ─────────────────────────

    private void OpenCard(CardKind k, string pool = "")
    {
        if (_card == k && _cardPool == pool) return;
        _card = k;
        _cardPool = pool;
        _cardAge = 0;
        App.Audio.Play("page", 0.5f);
        Ui.ResetScroll("pool-list");
        Ui.ResetScroll("pool-ledger");
        Ui.ResetScroll("pool-bag");
        Ui.ResetScroll("pool-crock");
    }

    private void CloseCard()
    {
        if (_card == CardKind.None) return;
        _card = CardKind.None;
        App.Audio.Play("slide", 0.35f);
    }

    protected override void OnSpot(string id)
    {
        if (id.StartsWith("tank:", StringComparison.Ordinal))
        {
            var pool = id[5..];
            if (_card == CardKind.Pool && _cardPool == pool) CloseCard();
            else
            {
                App.Audio.Play("glass", 0.4f);
                OpenCard(CardKind.Pool, pool);
            }
            return;
        }
        switch (id)
        {
            case SpotHolding: Toggle(CardKind.Holding); break;
            case SpotBag: App.Audio.Play("paper", 0.5f); Toggle(CardKind.Bag); break;
            case SpotLedger: Toggle(CardKind.Ledger); break;
            case SpotStool: Sit(); break;
        }
    }

    private void Toggle(CardKind k)
    {
        if (_card == k) CloseCard(); else OpenCard(k);
    }

    // ───────────────────────── actions (all rules live in PoolsLocale) ─────────────────────────

    private void Sit()
    {
        App.Audio.Play("chord", 0.3f);
        App.Show(S.SitAWhile());
    }

    private void Cleanse(string pool)
    {
        var r = S.Pools.Cleanse(pool);
        if (_tankOf.TryGetValue(pool, out var t)) t.CleanseT = 0;
        App.Audio.Play("rake", 0.6f);
        App.Show(r, PoolName(pool));
    }

    private void Feed(string pool, string? food = null)
    {
        var r = S.Pools.Feed(pool, food);
        if (_tankOf.TryGetValue(pool, out var t))
        {
            t.FeedT = 0;
            t.FeedAt = new Vector2(Rand(-t.W * 0.3f, t.W * 0.3f), Rand(-t.D * 0.2f, t.D * 0.2f));
            var p = App.Particles;
            var at = t.ToWorld(new Vector3(t.FeedAt.X, SurfY + 0.6f, t.FeedAt.Y));
            for (int i = 0; i < (R.ReduceMotion ? 4 : 16); i++)
                p.Emit(at + new Vector3(p.R(-0.2f, 0.2f), 0, p.R(-0.2f, 0.2f)), new Vector3(p.R(-0.3f, 0.3f), p.R(-0.2f, 0.4f), p.R(-0.3f, 0.3f)),
                       0.9f, p.R(0.04f, 0.07f), Palette.Hex(0xE3C07A), false, 0, 0.8f, -3f);
        }
        App.Audio.Play("sprinkle", 0.6f);
        App.Show(r, PoolName(pool));
    }

    private void Place(ItemInstance item, string pool)
    {
        var r = S.Pools.Place(item.Id, pool);
        if (_tankOf.TryGetValue(pool, out var t)) Splash(t, new Vector2(Rand(-t.W * 0.25f, t.W * 0.25f), Rand(-t.D * 0.15f, t.D * 0.15f)), 1.1f);
        App.Audio.Play("splash", 0.6f);
        App.Show(r, "In the water");
    }

    private void TakeOut(ItemInstance item, string pool)
    {
        var r = S.Pools.TakeOut(item.Id);
        if (_tankOf.TryGetValue(pool, out var t)) Splash(t, Vector2.Zero, 0.6f);
        App.Audio.Play("splash", 0.4f, 1.3f);
        App.Show(r, "Out of the water");
    }

    private void MoveCreature(LifeformInstance lf)
    {
        var entries = S.Content.Pools.Values
            .Select(p => new Picker<Choice>.Entry(new Choice(Cap(p.Name), () => DoMove(lf, p.Id)), Cap(p.Name), PoolSummary(p.Id)))
            .Append(new Picker<Choice>.Entry(new Choice("holding", () => DoMove(lf, null)), Cap(S.Content.Config.Pools.HoldingName), "to rest between pools"))
            .ToList();
        App.Open(new Picker<Choice>($"Move {lf.Name} where?", entries, c => c.Run()));
    }

    private void DoMove(LifeformInstance lf, string? to)
    {
        var r = S.Pools.MoveLifeform(lf.Id, to);
        App.Audio.Play("splash", 0.45f, 1.15f);
        App.Show(r);
    }

    private void ChooseMove(string? pool)
    {
        var here = (pool is null ? Enumerable.Empty<LifeformInstance>() : S.Pools.View(pool).State.Lifeforms).Select(l => (l, "here"))
            .Concat(S.Pools.Holding.Select(l => (l, "resting in the crock"))).ToList();
        if (here.Count == 0) { App.Toasts.Add("", "There's no one here to move.", NoticeKind.Info, LineKind.Soft); return; }
        var entries = here.Select(x => new Picker<Choice>.Entry(new Choice(x.l.Name, () => MoveCreature(x.l)),
            $"{x.l.Name} the {S.Content.LifeformName(x.l.Species)}", x.Item2, SpeciesTint(x.l.Species))).ToList();
        App.Open(new Picker<Choice>("Move which creature?", entries, c => c.Run()));
    }

    private void ChooseToLay(string pool)
    {
        var items = S.World.Items.Satchel.Concat(S.Pools.DruidsBag).ToList();
        if (items.Count == 0) { App.Toasts.Add("", "Nothing in your satchel or the druids' bag to lay in the water.", NoticeKind.Info, LineKind.Soft); return; }
        var entries = items.Select(i => new Picker<Choice>.Entry(new Choice(S.World.Items.Name(i), () => Place(i, pool)),
            S.World.Items.Name(i) + (i.Location == Locations.DruidsBag ? "  (druids' bag)" : ""), EnergyNote(i), App.Art.ColorOf(S.World.Items.Def(i)))).ToList();
        App.Open(new Picker<Choice>($"Lay what in {PoolName(pool)}?", entries, c => c.Run()));
    }

    private void ChooseFood(string pool)
    {
        var foods = S.World.Items.Satchel.Where(i => S.World.Items.Def(i).FoodValue > 0).ToList();
        if (foods.Count == 0) { Feed(pool); return; }
        var entries = new List<Picker<Choice>.Entry> { new(new Choice("flakes", () => Feed(pool)), "Pond flakes", "the Stillkeepers' usual") };
        entries.AddRange(foods.Select(i => new Picker<Choice>.Entry(new Choice(S.World.Items.Name(i), () => Feed(pool, i.Id)),
            S.World.Items.Name(i), "from your satchel", App.Art.ColorOf(S.World.Items.Def(i)))));
        App.Open(new Picker<Choice>("Feed them what?", entries, c => c.Run()));
    }

    private void Inspect(LifeformInstance lf)
    {
        var sp = S.Content.Lifeforms[lf.Species];
        App.Audio.Play("glass", 0.4f, 1.2f);
        App.Open(new ReadingCard($"{lf.Name} the {sp.Name}", new[]
        {
            new Line(LineKind.Narration, sp.Description),
            new Line(LineKind.System, $"Bears light {sp.Survive.Light}, heat {sp.Survive.Heat}. Thrives at light {sp.Thrive.Light}, heat {sp.Thrive.Heat}."),
            new Line(LineKind.System, (sp.HungerPerTick > 0 ? "Needs feeding. " : "Never needs feeding. ")
                                      + (sp.WastePerTick < 0 ? "Keeps the water clean." : sp.WastePerTick > 0 ? "Muddies the water a little." : "")
                                      + (sp.Emits.IsZero ? "" : $" Changes the water around it: light {Signed(sp.Emits.Light)}, heat {Signed(sp.Emits.Heat)}.")),
        }));
    }

    /// <summary>Satchel items: lay them in a pool (the open one first), or feed them if they're food.</summary>
    public override bool UseItem(ItemInstance item)
    {
        var def = S.World.Items.Def(item);
        var entries = new List<Picker<Choice>.Entry>();
        var pools = S.Content.Pools.Values.OrderBy(p => _card == CardKind.Pool && p.Id == _cardPool ? 0 : 1).ToList();
        foreach (var p in pools)
        {
            var id = p.Id;
            if (def.FoodValue > 0) entries.Add(new(new Choice("feed", () => Feed(id, item.Id)), $"Feed it to {p.Name}", PoolSummary(id), Palette.Pools));
            entries.Add(new(new Choice("lay", () => Place(item, id)), $"Lay it in {p.Name}", PoolSummary(id)));
        }
        App.Open(new Picker<Choice>(S.World.Items.Name(item), entries, c => c.Run(), EnergyNote(item)));
        return true;
    }

    private string PoolName(string pool) => Cap(S.Content.Pools[pool].Name);

    private string PoolSummary(string pool)
    {
        var v = S.Pools.View(pool);
        return $"light {Signed(v.Energy.Light)}, heat {Signed(v.Energy.Heat)} · {v.State.Lifeforms.Count} of {v.Pool.Capacity} living";
    }

    private string? EnergyNote(ItemInstance i) =>
        S.World.Items.Energy(i) is { IsZero: false } e ? $"pulls the water toward light {Signed(e.Light)}, heat {Signed(e.Heat)}" : null;

    // ───────────────────────── drawing ─────────────────────────

    protected override void DrawRoomUI()
    {
        var name = App.Content.Locales.TryGetValue(LocaleId, out var l) ? l.Name : "The Spirit Pools";
        int living = S.Content.Pools.Keys.Sum(p => S.Pools.View(p).State.Lifeforms.Count);
        var sub = $"{S.Content.Pools.Count} pools  ·  {living} living thing{(living == 1 ? "" : "s")}  ·  {S.Pools.SpeciesSeen().Count} kinds kept";
        var w = MathF.Max(Text.Measure(Ui.F.Hand, name, 44).X, Text.Measure(Ui.F.Italic, sub, 21).X) + 60;
        Raylib.DrawRectangleGradientH(0, 108, (int)w, 86, Palette.Alpha(Palette.Ink, 0.5f), Palette.Alpha(Palette.Ink, 0));
        Title(name, sub);

        _inspect = null;
        if (_card == CardKind.None) { Tabs(); return; }
        float t = Ease.OutBack(MathF.Min(1, _cardAge / 0.35f), 1.3f);
        float a = MathF.Min(1, _cardAge / 0.2f);
        var r = new Rectangle(Desk.X - (1 - t) * 60, Desk.Y, 600, Desk.Height);
        switch (_card)
        {
            case CardKind.Pool when S.Content.Pools.ContainsKey(_cardPool): PoolCard(r, a); break;
            case CardKind.Holding: HoldingCard(r, a); break;
            case CardKind.Bag: BagCard(r, a); break;
            case CardKind.Ledger: LedgerCard(r, a); break;
            default: _card = CardKind.None; break;
        }
    }

    /// <summary>Small tabs under the title, one per pool plus the ledger, for when a tank is hard to reach.</summary>
    private void Tabs()
    {
        float x = 30, y = 200;
        foreach (var p in S.Content.Pools.Values)
        {
            var label = Cap(p.Name.Replace("the ", ""));
            float bw = Text.Measure(Ui.F.Body, label, 21).X + 34;
            if (Ui.Button("pool-tab-" + p.Id, new Rectangle(x, y, bw, 38), label, Accent, size: 21)) OpenCard(CardKind.Pool, p.Id);
            x += bw + 10;
        }
        float lw = Text.Measure(Ui.F.Body, "Ledger", 21).X + 34;
        if (Ui.Button("pool-tab-ledger", new Rectangle(x, y, lw, 38), "Ledger", Accent, size: 21)) OpenCard(CardKind.Ledger);
    }

    private bool CloseButton(Rectangle r, string id)
    {
        bool c = Ui.Button(id, new Rectangle(r.X + r.Width - 58, r.Y + 18, 40, 38), "×", Accent, size: 30, tooltip: "Put the card aside (Esc)");
        if (c) CloseCard();
        return c;
    }

    private void PoolCard(Rectangle r, float alpha)
    {
        var v = S.Pools.View(_cardPool);
        Ui.Panel(r, alpha, Accent);
        CloseButton(r, "pool-close");
        float x = r.X + 32, y = r.Y + 22;
        Ui.Heading(Cap(v.Pool.Name), new Vector2(x, y), 40);
        Ui.Rule(x, y + 56, 220, Accent);
        y += 70;
        var desc = Text.Wrap(Ui.F.Italic, v.Pool.Description, 20, r.Width - 64);
        for (int i = 0; i < Math.Min(2, desc.Count); i++) Text.Draw(Ui.F.Italic, desc[i], new Vector2(x, y + i * 24), 20, Palette.InkSoft);
        y += Math.Min(2, desc.Count) * 24 + 12;

        // the residents, read before drawing the plane so a hovered row can show its bands there
        var listTop = y + 236;
        var buttonsY = r.Y + r.Height - 62;
        var list = new Rectangle(x - 6, listTop, r.Width - 52, buttonsY - listTop - 10);
        DrawResidents(v, list);

        DrawPlane(v, new Vector2(x + 34, y + 26), 16);
        DrawStats(v, new Vector2(x + 262, y + 16), r.X + r.Width - 32);

        // the verbs
        float bx = x, bw = (r.Width - 64 - 30) / 4;
        if (Ui.Button("pool-cleanse", new Rectangle(bx, buttonsY, bw, 42), "Cleanse", Palette.Pools, size: 22, tooltip: "Rake out the silt and clear the water")) Cleanse(_cardPool);
        bx += bw + 10;
        if (Ui.Button("pool-feed", new Rectangle(bx, buttonsY, bw, 42), "Feed", size: 22, tooltip: "Scatter pond flakes, or something from your satchel")) ChooseFood(_cardPool);
        bx += bw + 10;
        if (Ui.Button("pool-lay", new Rectangle(bx, buttonsY, bw, 42), "Lay in…", size: 22, tooltip: "Lay something from your satchel or the druids' bag in the water")) ChooseToLay(_cardPool);
        bx += bw + 10;
        bool canMove = v.State.Lifeforms.Count > 0 || S.Pools.Holding.Count > 0;
        if (Ui.Button("pool-move", new Rectangle(bx, buttonsY, bw, 42), "Move…", size: 22, enabled: canMove, tooltip: "Move a creature to another pool or the crock")) ChooseMove(_cardPool);
    }

    /// <summary>The Light/Dark × Hot/Cold plane: Light up, Hot right. The pool is a ring; thriving water is green.</summary>
    private void DrawPlane(PoolView v, Vector2 o, float cell)
    {
        var light = LightAxis; var heat = HeatAxis;
        int cols = heat.Max - heat.Min + 1, rows = light.Max - light.Min + 1;
        var species = v.State.Lifeforms.Select(lf => S.Content.Lifeforms[lf.Species]).Distinct().ToList();
        LifeformResource? focus = null;
        if (_inspect is { } id && v.State.Lifeforms.FirstOrDefault(lf => lf.Id == id) is { } lfi) focus = S.Content.Lifeforms[lfi.Species];
        Vector2 Cell(int h, int lt) => o + new Vector2((h - heat.Min) * cell, (light.Max - lt) * cell);

        Raylib.DrawRectangleRounded(new Rectangle(o.X - 6, o.Y - 6, cols * cell + 12, rows * cell + 12), 0.08f, 4, Palette.Alpha(Palette.ParchmentDark, 0.55f));
        for (int lt = light.Min; lt <= light.Max; lt++)
            for (int h = heat.Min; h <= heat.Max; h++)
            {
                var e = new Energy(lt, h);
                var p = Cell(h, lt);
                var rc = new Rectangle(p.X + 1, p.Y + 1, cell - 2, cell - 2);
                // a soft wash: warm to the right, gold toward the top, indigo toward the bottom
                var wash = Palette.Lerp(Palette.Lerp(Palette.Hex(0xB9D3E0), Palette.Hex(0xF0C9A0), (h - heat.Min) / (float)(cols - 1)),
                                        lt >= 0 ? Palette.Hex(0xF3E3A6) : Palette.Hex(0x7C7AA8), MathF.Abs(lt) / (float)Math.Max(1, Math.Max(light.Max, -light.Min)) * 0.5f);
                Raylib.DrawRectangleRec(rc, Palette.Alpha(wash, 0.55f));
                if (focus is not null)
                {
                    if (focus.Thrive.Contains(e)) Raylib.DrawRectangleRec(rc, Palette.Alpha(Palette.Moss, 0.65f));
                    else if (focus.Survive.Contains(e)) Raylib.DrawRectangleRec(rc, Palette.Alpha(Palette.Moss, 0.25f));
                }
                else if (species.Count > 0 && species.All(s => s.Thrive.Contains(e))) Raylib.DrawRectangleRec(rc, Palette.Alpha(Palette.Moss, 0.55f));
                if (h == 0 || lt == 0) Raylib.DrawRectangleRec(new Rectangle(p.X + cell / 2 - 1, p.Y + cell / 2 - 1, 2, 2), Palette.Alpha(Palette.InkSoft, 0.5f));
            }
        // the pool itself
        var c = Cell(Math.Clamp(v.Energy.Heat, heat.Min, heat.Max), Math.Clamp(v.Energy.Light, light.Min, light.Max)) + new Vector2(cell / 2);
        float pulse = R.ReduceMotion ? 0 : MathF.Sin(Ui.Time * 3) * 1.2f;
        Raylib.DrawCircleV(c, cell * 0.55f + pulse, Palette.Alpha(Palette.Teal, 0.35f));
        Raylib.DrawRing(c, cell * 0.36f, cell * 0.5f, 0, 360, 20, Palette.Teal);
        // axis labels from the taxonomy
        float w = cols * cell, h2 = rows * cell;
        Text.DrawCentered(Ui.F.Italic, light.PositiveLabel, new Vector2(o.X + w / 2, o.Y - 16), 17, Palette.InkFaint);
        Text.DrawCentered(Ui.F.Italic, light.NegativeLabel, new Vector2(o.X + w / 2, o.Y + h2 + 16), 17, Palette.InkFaint);
        Text.Draw(Ui.F.Italic, heat.NegativeLabel, new Vector2(o.X - 6 - Text.Measure(Ui.F.Italic, heat.NegativeLabel, 17).X - 4, o.Y + h2 / 2 - 9), 17, Palette.InkFaint);
        Text.Draw(Ui.F.Italic, heat.PositiveLabel, new Vector2(o.X + w + 10, o.Y + h2 / 2 - 9), 17, Palette.InkFaint);
    }

    private void DrawStats(PoolView v, Vector2 o, float right)
    {
        float y = o.Y;
        void Stat(string k, string val)
        {
            Text.Draw(Ui.F.Hand, k, new Vector2(o.X, y), 24, Palette.Ink);
            Text.Draw(Ui.F.Body, val, new Vector2(o.X + 92, y + 3), 21, Palette.InkSoft);
            y += 30;
        }
        Stat(Cap(LightAxis.Id), Signed(v.Energy.Light));
        Stat(Cap(HeatAxis.Id), Signed(v.Energy.Heat));
        Text.Draw(Ui.F.Hand, "Clarity", new Vector2(o.X, y), 24, Palette.Ink);
        var bar = new Rectangle(o.X + 92, y + 10, MathF.Min(150, right - o.X - 130), 9);
        Raylib.DrawRectangleRounded(bar, 1, 4, Palette.ParchmentDark);
        Raylib.DrawRectangleRounded(new Rectangle(bar.X, bar.Y, bar.Width * (float)(v.State.Clarity / 100), bar.Height), 1, 4, v.State.Clarity >= 50 ? Palette.Teal : Palette.Stone);
        Text.Draw(Ui.F.Body, $"{v.State.Clarity:0}", new Vector2(bar.X + bar.Width + 8, y + 3), 19, Palette.InkFaint);
        y += 30;
        Stat("Room", $"{v.State.Lifeforms.Count} of {v.Pool.Capacity}");
        y += 6;
        var notes = new List<string>();
        if (v.Pool.SpawningPond) notes.Add("A spawning pond: lay minerals and auras here.");
        notes.Add(_inspect is null ? "Green: water everyone here would thrive in." : "Green: where this one thrives; pale, where it gets by.");
        foreach (var n in notes)
            foreach (var line in Text.Wrap(Ui.F.Italic, n, 18, right - o.X))
            {
                Text.Draw(Ui.F.Italic, line, new Vector2(o.X, y), 18, Palette.InkFaint);
                y += 22;
            }
    }

    private void DrawResidents(PoolView v, Rectangle list)
    {
        const float rowH = 48;
        int rows = v.State.Lifeforms.Count + v.Items.Count + v.Soaks.Count + (v.State.Lifeforms.Count == 0 ? 1 : 0);
        float off = Ui.BeginScroll("pool-list", list, rows * rowH + 10);
        float y = list.Y - off;
        if (v.State.Lifeforms.Count == 0)
        {
            Text.Draw(Ui.F.Italic, "No one lives here yet. Move a creature in, or keep the water clear and see what comes.", new Vector2(list.X + 8, y + 12), 19, Palette.InkFaint);
            y += rowH;
        }
        foreach (var lf in v.State.Lifeforms)
        {
            var sp = S.Content.Lifeforms[lf.Species];
            var row = new Rectangle(list.X, y, list.Width - 14, rowH - 4);
            string sex = !sp.Sexed ? "" : lf.Sex == Sex.Female ? "she" : lf.Sex == Sex.Male ? "he" : "";
            string mood = lf.Mood switch { LifeMood.Thriving => "thriving", LifeMood.Content => "content", _ => "sulking" } + (lf.MoodReason.Length > 0 ? $" ({lf.MoodReason})" : "");
            var extra = new List<string> { mood };
            if (sex.Length > 0) extra.Insert(0, sex);
            if (sp.HungerPerTick > 0 && lf.Hunger > 55) extra.Add("hungry");
            if (lf.Expecting is not null) extra.Add(lf.Gestation > 0 ? $"carrying eggs, {S.World.Clock.Describe(lf.Gestation)} to go" : "eggs ready, waiting for room");
            if (Ui.Row("pool-lf-" + lf.Id, row, $"{lf.Name} the {sp.Name}", string.Join(" · ", extra), SpeciesTint(lf.Species), size: 22)) Inspect(lf);
            if (Ui.Hover(row)) _inspect = lf.Id;
            if (sp.HungerPerTick > 0)
            {
                var hb = new Rectangle(row.X + row.Width - 74, row.Y + 16, 60, 6);
                Raylib.DrawRectangleRounded(hb, 1, 4, Palette.Alpha(Palette.ParchmentDark, 0.9f));
                Raylib.DrawRectangleRounded(new Rectangle(hb.X, hb.Y, hb.Width * (float)Math.Clamp(1 - lf.Hunger / 100, 0, 1), hb.Height), 1, 4, lf.Hunger > 55 ? Palette.Hex(0xC27A4A) : Palette.Moss);
            }
            y += rowH;
        }
        foreach (var it in v.Items)
        {
            var row = new Rectangle(list.X, y, list.Width - 14, rowH - 4);
            Raylib.DrawRectangleRounded(row, 0.2f, 6, Palette.Alpha(Palette.Lantern, 0.12f));
            App.Art.Draw(S.World.Items.Def(it), new Vector2(row.X + 22, row.Y + row.Height / 2), 34, it.Inert, Ui.Time);
            Text.Draw(Ui.F.Body, S.World.Items.Name(it), new Vector2(row.X + 46, row.Y + 4), 21, Palette.Ink);
            Text.Draw(Ui.F.Italic, EnergyNote(it) ?? "lying in the water", new Vector2(row.X + 46, row.Y + 24), 17, Palette.InkFaint);
            if (Ui.Button("pool-take-" + it.Id, new Rectangle(row.X + row.Width - 116, row.Y + 5, 108, 34), "Take out", size: 19)) TakeOut(it, _cardPool);
            y += rowH;
        }
        foreach (var soak in v.Soaks)
        {
            var row = new Rectangle(list.X, y, list.Width - 14, rowH - 4);
            Text.Draw(Ui.F.Body, $"{soak.Transform}: {soak.Item}", new Vector2(row.X + 8, row.Y + 2), 20, Palette.Ink);
            var bar = new Rectangle(row.X + 8, row.Y + 28, row.Width * 0.45f, 7);
            Raylib.DrawRectangleRounded(bar, 1, 4, Palette.ParchmentDark);
            Raylib.DrawRectangleRounded(new Rectangle(bar.X, bar.Y, bar.Width * (float)soak.Progress, bar.Height), 1, 4, soak.ConditionsMet ? Palette.Moss : Palette.Stone);
            int remaining = (int)Math.Ceiling((1 - soak.Progress) * soak.EffectiveTicks);
            var note = soak.ConditionsMet ? $"about {S.World.Clock.Describe(remaining)} to go" : "paused; it wants " + string.Join(", ", soak.Wants);
            Text.Draw(Ui.F.Italic, note, new Vector2(bar.X + bar.Width + 12, row.Y + 22), 17, Palette.InkFaint);
            if (Ui.Hover(row) && soak.Text.Length > 0) Ui.Tooltip(soak.Text);
            y += rowH;
        }
        Ui.EndScroll();
    }

    private void HoldingCard(Rectangle r, float alpha)
    {
        var held = S.Pools.Holding;
        r.Height = MathF.Min(Desk.Height, 190 + Math.Max(1, held.Count) * 54);
        Ui.Panel(r, alpha, Accent);
        CloseButton(r, "crock-close");
        float x = r.X + 32, y = r.Y + 22;
        Ui.Heading(Cap(S.Content.Config.Pools.HoldingName), new Vector2(x, y), 40);
        Ui.Rule(x, y + 56, 220, Accent);
        y += 74;
        Text.Draw(Ui.F.Italic, "A place for a creature to rest between pools. Nothing here goes hungry.", new Vector2(x, y), 19, Palette.InkSoft);
        y += 36;
        if (held.Count == 0) Text.Draw(Ui.F.Italic, "Empty for now.", new Vector2(x, y + 8), 21, Palette.InkFaint);
        foreach (var lf in held.ToList())
        {
            var row = new Rectangle(x - 6, y, r.Width - 52 - 130, 46);
            if (Ui.Row("crock-" + lf.Id, row, $"{lf.Name} the {S.Content.LifeformName(lf.Species)}", null, SpeciesTint(lf.Species), size: 22)) Inspect(lf);
            if (Ui.Button("crock-move-" + lf.Id, new Rectangle(row.X + row.Width + 10, y + 4, 120, 38), "Move…", size: 21)) MoveCreature(lf);
            y += 54;
        }
    }

    private void BagCard(Rectangle r, float alpha)
    {
        var bag = S.Pools.DruidsBag.ToList();
        r.Height = MathF.Min(Desk.Height, 200 + Math.Max(1, bag.Count) * 56);
        Ui.Panel(r, alpha, Accent);
        CloseButton(r, "bag-close");
        float x = r.X + 32, y = r.Y + 22;
        Ui.Heading("The druids' bag", new Vector2(x, y), 40);
        Ui.Rule(x, y + 56, 220, Accent);
        y += 74;
        Text.Draw(Ui.F.Italic, "What the Sanctum sends for the pools waits here until you lay it in the water.", new Vector2(x, y), 19, Palette.InkSoft);
        y += 38;
        if (bag.Count == 0) Text.Draw(Ui.F.Italic, "Empty for now.", new Vector2(x, y + 8), 21, Palette.InkFaint);
        foreach (var it in bag)
        {
            var row = new Rectangle(x - 6, y, r.Width - 52, 48);
            Raylib.DrawRectangleRounded(row, 0.2f, 6, Palette.Alpha(Palette.ParchmentDark, 0.35f));
            App.Art.Draw(S.World.Items.Def(it), new Vector2(row.X + 26, row.Y + 24), 38, it.Inert, Ui.Time);
            Text.Draw(Ui.F.Body, S.World.Items.Name(it), new Vector2(row.X + 54, row.Y + 4), 22, Palette.Ink);
            Text.Draw(Ui.F.Italic, EnergyNote(it) ?? S.World.Items.Description(it), new Vector2(row.X + 54, row.Y + 26), 17, Palette.InkFaint);
            if (Ui.Button("bag-lay-" + it.Id, new Rectangle(row.X + row.Width - 130, row.Y + 6, 120, 36), "Lay in…", size: 20))
            {
                var item = it;
                var entries = S.Content.Pools.Values.Select(p => new Picker<Choice>.Entry(new Choice(p.Id, () => Place(item, p.Id)), Cap(p.Name), PoolSummary(p.Id))).ToList();
                App.Open(new Picker<Choice>($"Lay {S.World.Items.Name(item)} where?", entries, c => c.Run()));
            }
            y += 56;
        }
    }

    private void LedgerCard(Rectangle r, float alpha)
    {
        Ui.Panel(r, alpha, Accent);
        CloseButton(r, "ledger-close");
        float x = r.X + 32, y = r.Y + 22;
        Ui.Heading("The breeding ledger", new Vector2(x, y), 40);
        Ui.Rule(x, y + 56, 220, Accent);
        y += 72;
        var view = new Rectangle(x - 4, y, r.Width - 56, r.Y + r.Height - y - 24);
        var kinds = S.Pools.SpeciesSeen();
        var rules = S.Pools.KnownRules();
        var lines = new List<(string Text, Font Font, float Size, Color Col, Color? Swatch)>
        {
            ("Every kind you've kept, and every pairing you've seen work. Brother Alder adds the odd comment in green ink.", Ui.F.Italic, 19, Palette.InkSoft, null),
            ("Kinds kept", Ui.F.Hand, 28, Palette.Ink, null),
        };
        foreach (var id in kinds)
        {
            var sp = S.Content.Lifeforms[id];
            lines.Add(($"{sp.Name}: thrives at light {sp.Thrive.Light}, heat {sp.Thrive.Heat}", Ui.F.Body, 20, Palette.InkSoft, SpeciesTint(id)));
        }
        lines.Add(("Pairings seen", Ui.F.Hand, 28, Palette.Ink, null));
        if (rules.Count == 0) lines.Add(("Only like with like, so far.", Ui.F.Italic, 20, Palette.InkFaint, null));
        foreach (var rule in rules)
        {
            var pair = rule.Ordered ? $"a {S.Content.LifeformName(rule.ParentA)} mother and a {S.Content.LifeformName(rule.ParentB)} father"
                : rule.ParentA == rule.ParentB ? $"two {S.Content.LifeformName(rule.ParentA)}s" : $"{S.Content.LifeformName(rule.ParentA)} with {S.Content.LifeformName(rule.ParentB)}";
            var parts = new List<string>();
            if (rule.Requires is { } q)
            {
                if (q.Light.Min != int.MinValue || q.Light.Max != int.MaxValue) parts.Add($"light {q.Light}");
                if (q.Heat.Min != int.MinValue || q.Heat.Max != int.MaxValue) parts.Add($"heat {q.Heat}");
            }
            var cond = parts.Count == 0 ? "" : $" in water at {string.Join(", ", parts)}";
            lines.Add(($"{Cap(pair)}{cond}: {string.Join(" or ", rule.Offspring.Select(o => S.Content.LifeformName(o.Lifeform)))}", Ui.F.Body, 20, Palette.Ink,
                       rule.Offspring.Count > 0 ? SpeciesTint(rule.Offspring[0].Lifeform) : null));
        }
        // measure, then draw inside a scroll
        float total = 0;
        var wrapped = lines.Select(l => (l, Text.Wrap(l.Font, l.Text, l.Size, view.Width - 40))).ToList();
        foreach (var (l, w) in wrapped) total += w.Count * (l.Size * 1.3f) + (l.Font.Texture.Id == Ui.F.Hand.Texture.Id ? 14 : 8);
        float off = Ui.BeginScroll("pool-ledger", view, total + 10);
        float yy = view.Y - off;
        foreach (var (l, w) in wrapped)
        {
            bool heading = l.Font.Texture.Id == Ui.F.Hand.Texture.Id;
            if (heading) yy += 6;
            if (l.Swatch is { } sw) Raylib.DrawCircleV(new Vector2(view.X + 10, yy + l.Size * 0.62f), 6, sw);
            foreach (var line in w)
            {
                Text.Draw(l.Font, line, new Vector2(view.X + (l.Swatch is null ? 0 : 26), yy), l.Size, l.Col);
                yy += l.Size * 1.3f;
            }
            yy += heading ? 8 : 8;
        }
        Ui.EndScroll();
    }
}
