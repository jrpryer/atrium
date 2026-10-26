using System.Numerics;
using Atrium.Core.Content;
using Atrium.Diorama.App;
using Atrium.Diorama.Overlays;
using Atrium.Diorama.Render;
using Atrium.Diorama.UI;
using Raylib_cs;

namespace Atrium.Diorama.Scenes;

/// <summary>The Apothecary's parchment working cards: the bench, the Discovery Map and the old formulary.</summary>
public sealed partial class ApothecaryScene
{
    private enum Card { None, Bench, Map, Formulary }
    private Card _card;
    private float _cardAge;

    private void OpenCard(Card c)
    {
        if (_card == c) return;
        _card = c;
        _cardAge = 0;
        App.Audio.Play("page", 0.5f);
        Ui.ResetScroll("apo-map");
        Ui.ResetScroll("apo-form");
    }

    private void CloseCard()
    {
        if (_card == Card.None) return;
        _card = Card.None;
        App.Audio.Play("slide", 0.35f);
    }

    protected override void DrawRoomUI()
    {
        var loc = App.Content.Locales.TryGetValue(LocaleId, out var l) ? l.Name : "The Apothecary";
        var sub = $"{_jars.Count} element{(_jars.Count == 1 ? "" : "s")} on the shelves  ·  {_discovered} of {_total} discoveries";
        // a soft shadow so the heading reads against the pale wall
        var w = MathF.Max(Text.Measure(Ui.F.Hand, loc, 44).X, Text.Measure(Ui.F.Italic, sub, 21).X) + 60;
        Raylib.DrawRectangleGradientH(0, 108, (int)w, 86, Palette.Alpha(Palette.Ink, 0.5f), Palette.Alpha(Palette.Ink, 0));
        Title(loc, sub);

        _cardAge += Ui.Dt;
        if (_card == Card.None) Tabs();
        else
        {
            float t = Ease.OutBack(MathF.Min(1, _cardAge / 0.35f), 1.3f);
            float a = MathF.Min(1, _cardAge / 0.2f);
            var r = new Rectangle(Desk.X - (1 - t) * 60, Desk.Y, 600, Desk.Height);
            switch (_card)
            {
                case Card.Bench: BenchCard(r, a); break;
                case Card.Map: MapCard(r, a); break;
                case Card.Formulary: FormularyCard(r, a); break;
            }
        }
        DrawPockets();
    }

    /// <summary>Three small parchment tabs under the title, for when the furniture is out of reach.</summary>
    private void Tabs()
    {
        float x = 30, y = 200;
        foreach (var (label, card) in new[] { ("The bench", Card.Bench), ("Discovery Map", Card.Map), ("Old formulary", Card.Formulary) })
        {
            float bw = Text.Measure(Ui.F.Body, label, 21).X + 34;
            if (Ui.Button("apo-tab-" + card, new Rectangle(x, y, bw, 38), label, Accent, size: 21)) OpenCard(card);
            x += bw + 10;
        }
    }

    private bool CloseButton(Rectangle r, string id)
    {
        bool c = Ui.Button(id, new Rectangle(r.X + r.Width - 58, r.Y + 18, 40, 38), "×", Accent, size: 30, tooltip: "Put the card aside (Esc)");
        if (c) CloseCard();
        return c;
    }

    // ───────────────────────── the bench ─────────────────────────

    private void BenchCard(Rectangle r, float alpha)
    {
        int max = MaxHere;
        var obj = _object is null ? null : S.World.Items.Get(_object);
        ToolResource? tool = _tool is null ? null : _tools.FirstOrDefault(t => t.Def.Id == _tool)?.Def;
        string toolNote = tool is null ? "No tool: just hands, and time." : tool.Description;
        var noteLines = Text.Wrap(Ui.F.Italic, toolNote, 20, r.Width - 64);
        string frontier = _frontier > 0 ? $"The Discovery Map shows {_frontier} discover{(_frontier == 1 ? "y" : "ies")} within reach of what you know." : "";
        // with an object on the tray there isn't room for the map's hint as well; the map itself is one click away
        var frontierLines = frontier.Length > 0 && obj is null ? Text.Wrap(Ui.F.Italic, frontier, 20, r.Width - 64) : new List<string>();
        float h = 92 + (obj is not null ? 78 : 0) + 172 + 82 + noteLines.Count * 25 + 72 + frontierLines.Count * 25 + 62;
        r.Height = MathF.Min(Desk.Height, h);
        Ui.Panel(r, alpha, Accent);
        CloseButton(r, "apo-bench-close");
        float x = r.X + 32, y = r.Y + 22;
        Ui.Heading("At the bench", new Vector2(x, y), 42);
        Ui.Rule(x, y + 58, 220, Accent);
        y += 76;

        // the object on the tray
        if (obj is not null)
        {
            var def = S.Content.Item(obj.Def);
            var c = new Vector2(x + 30, y + 32);
            Raylib.DrawCircleV(c, 32, Palette.Alpha(Palette.ParchmentDark, 0.9f));
            App.Art.Draw(def, c, 56, obj.Inert, Ui.Time);
            Text.Draw(Ui.F.Body, S.World.Items.Name(obj), new Vector2(x + 76, y + 6), 26, Palette.Ink);
            Text.Draw(Ui.F.Italic, "on the brass tray, to be worked", new Vector2(x + 78, y + 36), 20, Palette.InkSoft);
            if (Ui.Button("apo-obj-off", new Rectangle(r.X + r.Width - 172, y + 14, 140, 38), "Take it off", size: 21)) TakeObjectOff();
            y += 78;
        }

        // what's in the vessel
        Text.Draw(Ui.F.Hand, "In the vessel", new Vector2(x, y), 30, Palette.Ink);
        var count = $"{_bench.Count} of {max}";
        Text.Draw(Ui.F.Italic, count, new Vector2(r.X + r.Width - 32 - Text.Measure(Ui.F.Italic, count, 20).X, y + 8), 20, Palette.InkSoft);
        y += 42;
        float slot = 104, gap = 16;
        for (int i = 0; i < max; i++)
        {
            var sr = new Rectangle(x + i * (slot + gap), y, slot, 120);
            var c = new Vector2(sr.X + slot / 2, sr.Y + 44);
            if (i < _bench.Count)
            {
                var def = S.Content.Item(_bench[i]);
                bool hov = Ui.Hover(sr);
                float hh = Ui.Anim("apo-slot-" + i, hov);
                Raylib.DrawCircleV(c + new Vector2(0, -hh * 3), 40, Palette.Lerp(Palette.Alpha(Palette.ParchmentDark, 0.9f), Palette.Alpha(Palette.LanternHot, 0.8f), hh * 0.5f));
                Raylib.DrawCircleLinesV(c + new Vector2(0, -hh * 3), 40, Palette.Alpha(Accent, 0.6f));
                App.Art.Draw(def, c + new Vector2(0, -hh * 3), 62, false, Ui.Time);
                var lines = Text.Wrap(Ui.F.Italic, def.Name, 19, slot + 8);
                for (int k = 0; k < Math.Min(2, lines.Count); k++)
                    Text.DrawCentered(Ui.F.Italic, lines[k], new Vector2(c.X, sr.Y + 98 + k * 20), 19, Palette.Ink);
                Ui.Block(sr);
                if (hov)
                {
                    Ui.Tooltip($"{def.Name}: click to take it out again.");
                    if (Input.Pressed) { Input.ClickConsumed = true; RemoveElementAt(i); break; }
                }
            }
            else
            {
                for (int k = 0; k < 16; k += 2)
                    Raylib.DrawRing(c, 37, 39, k * 22.5f, k * 22.5f + 14, 4, Palette.Alpha(Palette.InkFaint, 0.8f));
                Text.DrawCentered(Ui.F.Italic, "empty", c, 19, Palette.InkFaint);
            }
        }
        y += 130;

        // the tool in hand
        Text.Draw(Ui.F.Hand, "Tool", new Vector2(x, y), 30, Palette.Ink);
        Text.Draw(Ui.F.Body, tool is null ? "none" : $"{tool.Name}  ({tool.Verb})", new Vector2(x + 76, y + 4), 25, tool is null ? Palette.InkSoft : Palette.Ink);
        if (Ui.Button("apo-tool-pick", new Rectangle(r.X + r.Width - 172, y, 140, 38), "Choose…", size: 21)) ChooseTool();
        y += 44;
        foreach (var line in noteLines) { Text.Draw(Ui.F.Italic, line, new Vector2(x + 2, y), 20, Palette.InkSoft); y += 25; }
        y += 18;

        // mix, or work the object
        bool canWork = _bench.Count > 0 || _object is not null;
        bool canClear = _bench.Count > 0 || _tool is not null || _object is not null;
        var label = _object is null ? "Mix" : "Work the object";
        if (Ui.Button("apo-mix", new Rectangle(x, y, 300, 54), label, Accent, canWork, 28, _object is null ? null : "Work it with the tool, and anything in the vessel alongside.")) Work();
        if (Ui.Button("apo-clear", new Rectangle(x + 316, y, 220, 54), "Clear the bench", null, canClear, 24)) ClearBench();
        y += 72;

        foreach (var line in frontierLines) { Text.Draw(Ui.F.Italic, line, new Vector2(x + 2, y), 20, Palette.LeafDeep); y += 25; }
        y += 6;
        float lx = x;
        foreach (var (lab, act) in new (string, Action)[] { ("Discovery Map", () => OpenCard(Card.Map)), ("Old formulary", () => OpenCard(Card.Formulary)), ("Bottle an element", ChooseBottle) })
        {
            float bw = Text.Measure(Ui.F.Body, lab, 20).X + 30;
            if (Ui.Button("apo-link-" + lab, new Rectangle(lx, y, bw, 36), lab, Accent, size: 20)) act();
            lx += bw + 10;
        }
    }

    private void ChooseTool()
    {
        var none = new ToolResource { Id = "", Name = "No tool" };
        var entries = new List<Picker<ToolResource>.Entry> { new(none, "No tool, just hands and time", null, Palette.InkFaint) };
        entries.AddRange(_tools.Select(t => new Picker<ToolResource>.Entry(t.Def, $"{t.Def.Name} ({t.Def.Verb})", t.Def.Description, Palette.Gold)));
        App.Open(new Picker<ToolResource>("Which tool?", entries, t =>
        {
            if (t.Id.Length == 0) { _tool = null; App.Audio.Play("tock", 0.5f, 0.9f); }
            else if (_tool != t.Id) ToggleTool(t.Id);
        }));
    }

    // ───────────────────────── the Discovery Map ─────────────────────────

    private void MapCard(Rectangle r, float alpha)
    {
        var apo = S.Apothecary;
        var map = apo.Map();
        var recipes = apo.KnownRecipes();
        var made = recipes.Select(x => x.Output).ToHashSet();
        var given = map.Known.Where(k => !made.Contains(k.Id)).ToList();

        Ui.Panel(r, alpha, Accent);
        CloseButton(r, "apo-map-close");
        float x = r.X + 32, y = r.Y + 22, inner = r.Width - 64;
        Ui.Heading("The Discovery Map", new Vector2(x, y), 42);
        Ui.Rule(x, y + 58, 220, Accent);
        y += 72;
        // one diamond per discovery, lit as they're found
        int total = Math.Max(1, map.Total);
        float step = MathF.Min(18, inner / total);
        for (int i = 0; i < map.Total; i++)
        {
            var c = new Vector2(x + 8 + i * step, y + 10);
            bool lit = i < map.Discovered;
            var col = lit ? (i < recipes.Count ? App.Art.ColorOf(S.Content.Item(recipes[i].Output)) : Palette.Lantern) : Palette.Alpha(Palette.InkFaint, 0.45f);
            Ui.Diamond(c, lit ? 6.5f : 4.5f, col);
        }
        y += 28;
        var intro = $"Discovered {map.Discovered} of {map.Total}. Each new thing you find blooms on the map; every failed mix teaches you a little, and enough of those reveal part of something still hidden.";
        y += Ui.Paragraph(intro, new Rectangle(x, y, inner, 120), 20, Palette.InkSoft, Ui.F.Italic) + 8;

        var view = new Rectangle(x - 4, y, inner + 14, r.Y + r.Height - y - 24);
        float content = MapContentHeight(map, recipes, given, inner);
        float off = Ui.BeginScroll("apo-map", view, content);
        float yy = view.Y - off;
        if (map.Frontier.Count > 0)
        {
            Text.Draw(Ui.F.Hand, "Within reach of what you know", new Vector2(x, yy), 28, Palette.LeafDeep);
            yy += 38;
            foreach (var n in map.Frontier)
            {
                var c = new Vector2(x + 14, yy + 14);
                for (int k = 0; k < 12; k += 2) Raylib.DrawRing(c, 11, 13, k * 30f, k * 30f + 18, 3, Palette.LeafDeep);
                Text.DrawCentered(Ui.F.Bold, "?", c, 18, Palette.LeafDeep);
                var parts = n.Inputs.Select(i => i ?? "?").ToList();
                var tool = n.Tool is not null ? $", with the {n.Tool}" : n.NeedsTool ? ", and a tool" : "";
                yy += Ui.Paragraph("??? = " + string.Join(" + ", parts) + tool, new Rectangle(x + 40, yy + 2, inner - 44, 200), 21, Palette.Ink) + 8;
            }
            yy += 10;
        }
        Text.Draw(Ui.F.Hand, "How you made what you know", new Vector2(x, yy), 28, Accent);
        yy += 38;
        if (recipes.Count == 0) { Text.Draw(Ui.F.Italic, "Nothing yet. Put something in the vessel and see.", new Vector2(x, yy), 20, Palette.InkFaint); yy += 30; }
        foreach (var rec in recipes)
        {
            var def = S.Content.Item(rec.Output);
            App.Art.Draw(def, new Vector2(x + 14, yy + 14), 30, false, Ui.Time);
            Text.Draw(Ui.F.Body, def.Name, new Vector2(x + 40, yy), 23, Palette.Ink);
            var how = "= " + string.Join(" + ", rec.Inputs.Select(S.Content.ItemName)) + (rec.Tool is null ? "" : $"  ({S.Content.ToolName(rec.Tool)})");
            yy += 28;
            yy += Ui.Paragraph(how, new Rectangle(x + 40, yy, inner - 44, 200), 19, Palette.InkSoft, Ui.F.Italic) + 8;
        }
        if (given.Count > 0)
        {
            yy += 6;
            Text.Draw(Ui.F.Hand, "On the shelves already, or brought in", new Vector2(x, yy), 28, Palette.InkSoft);
            yy += 38;
            yy += Ui.Paragraph(string.Join(" · ", given.Select(g => g.Name)), new Rectangle(x, yy, inner, 400), 21, Palette.Ink) + 8;
        }
        Ui.EndScroll();
    }

    private float MapContentHeight(Atrium.Core.Locales.DiscoveryMapView map, IReadOnlyList<RecipeResource> recipes, List<ItemResource> given, float inner)
    {
        float h = 0;
        if (map.Frontier.Count > 0)
        {
            h += 38;
            foreach (var n in map.Frontier)
            {
                var parts = n.Inputs.Select(i => i ?? "?").ToList();
                var tool = n.Tool is not null ? $", with the {n.Tool}" : n.NeedsTool ? ", and a tool" : "";
                h += Text.Height(Ui.F.Body, "??? = " + string.Join(" + ", parts) + tool, inner - 44, 21, 1.3f) + 8;
            }
            h += 10;
        }
        h += 38;
        if (recipes.Count == 0) h += 30;
        foreach (var rec in recipes)
        {
            var how = "= " + string.Join(" + ", rec.Inputs.Select(S.Content.ItemName)) + (rec.Tool is null ? "" : $"  ({S.Content.ToolName(rec.Tool)})");
            h += 28 + Text.Height(Ui.F.Italic, how, inner - 44, 19, 1.3f) + 8;
        }
        if (given.Count > 0) h += 44 + Text.Height(Ui.F.Body, string.Join(" · ", given.Select(g => g.Name)), inner, 21, 1.3f) + 8;
        return h + 12;
    }

    // ───────────────────────── the old formulary ─────────────────────────

    private void FormularyCard(Rectangle r, float alpha)
    {
        var entries = S.Apothecary.Formulary();
        Ui.Panel(r, alpha, Accent);
        CloseButton(r, "apo-form-close");
        float x = r.X + 32, y = r.Y + 22, inner = r.Width - 64;
        Ui.Heading("The Old Formulary", new Vector2(x, y), 42);
        Ui.Rule(x, y + 58, 220, Accent);
        y += 74;
        y += Ui.Paragraph("Half recipes, half riddles, cracked along the spine. The handwriting changes every few pages.", new Rectangle(x, y, inner, 80), 20, Palette.InkSoft, Ui.F.Italic) + 10;
        var view = new Rectangle(x - 4, y, inner + 14, r.Y + r.Height - y - 24);
        float content = entries.Sum(e => 40 + Text.Height(Ui.F.Body, e.Text, inner, 22, 1.3f) + 18) + 10;
        if (entries.Count == 0) content = 60;
        float off = Ui.BeginScroll("apo-form", view, content);
        float yy = view.Y - off;
        if (entries.Count == 0)
            Ui.Paragraph("The pages that are left won't make sense to you yet. Perhaps later.", new Rectangle(x, yy, inner, 60), 22, Palette.InkFaint, Ui.F.Italic);
        foreach (var e in entries)
        {
            Text.Draw(Ui.F.Hand, e.Title, new Vector2(x, yy), 30, Accent);
            yy += 40;
            yy += Ui.Paragraph(e.Text, new Rectangle(x, yy, inner, 600), 22, Palette.Ink) + 18;
        }
        Ui.EndScroll();
    }

    // ───────────────────────── things flying into the satchel ─────────────────────────

    private void DrawPockets()
    {
        var to = new Vector2(Ui.W / 2, Ui.H - 50);
        foreach (var p in _pockets)
        {
            if (p.Delay > 0) continue;
            float t = Ease.InOutSine(p.T);
            var mid = (p.From + to) / 2 + new Vector2(0, -160);
            var pos = (1 - t) * (1 - t) * p.From + 2 * (1 - t) * t * mid + t * t * to;
            float s = 56 * (1 - 0.35f * t) * (0.7f + 0.3f * Ease.OutBack(MathF.Min(1, p.T * 4)));
            Raylib.DrawCircleV(pos, s * 0.6f, Palette.Alpha(Palette.LanternHot, 0.35f * (1 - t)));
            App.Art.Draw(p.Def, pos, s, false, Ui.Time);
            if (p.T + Ui.Dt / 0.8f >= 1) App.Audio.Play("tock", 0.4f, 1.4f);
        }
    }
}
