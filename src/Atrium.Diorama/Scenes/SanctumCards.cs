using System.Numerics;
using Atrium.Core.Content;
using Atrium.Core.Locales;
using Atrium.Core.Mystery;
using Atrium.Core.Runtime;
using Atrium.Diorama.App;
using Atrium.Diorama.Overlays;
using Atrium.Diorama.Render;
using Atrium.Diorama.UI;
using Raylib_cs;

namespace Atrium.Diorama.Scenes;

/// <summary>
/// The Sanctum's working cards: the designation desk and its card, the glass, the assembly bench, the cabinet, the
/// Reconstitution Circle (always with its explicit warning) and the Balance. Every action calls SanctumLocale.
/// </summary>
public sealed partial class SanctumScene
{
    private enum Card { None, Desk, Designation, Glass, Bench, Cabinet, Circle, Seal }

    private Card _card;
    private float _cardAge;
    private string? _seal;
    private string? _designObj;
    private readonly Dictionary<string, string> _choice = new();

    private IEnumerable<ToolResource> LensTools() => S.Sanctum.Tools().Where(t => !t.Grants.Contains(SanctumLocale.ReconstituteCapability));
    private ToolResource? CircleTool() => S.Sanctum.Tools().FirstOrDefault(t => t.Grants.Contains(SanctumLocale.ReconstituteCapability));

    private static bool Objectish(ItemInstance i) => i.Essence is not null || i.RemnantOf is not null;

    // ───────────────────────── opening and closing ─────────────────────────

    private void OpenCard(Card c)
    {
        if (_card == c && c != Card.Designation) return;
        _card = c;
        _cardAge = 0;
        App.Audio.Play("page", 0.5f);
        foreach (var id in new[] { "san-desk", "san-design", "san-glass", "san-bench", "san-cab", "san-circle", "san-seal" }) Ui.ResetScroll(id);
    }

    private void CloseCard()
    {
        if (_card == Card.None) return;
        _card = Card.None;
        App.Audio.Play("slide", 0.35f);
    }

    protected override void OnSpot(string id)
    {
        if (id.StartsWith("seal:", StringComparison.Ordinal))
        {
            var sid = id[5..];
            if (_card == Card.Seal && _seal == sid) { CloseCard(); return; }
            _seal = sid;
            App.Audio.Play("creak", 0.35f, 1.1f);
            OpenCard(Card.Seal);
            return;
        }
        switch (id)
        {
            case "desk": Toggle(_card == Card.Designation ? Card.Designation : Card.Desk); break;
            case "glass": App.Audio.Play("glass", 0.4f); Toggle(Card.Glass); break;
            case "bench": Toggle(Card.Bench); break;
            case "cabinet": App.Audio.Play("creak", 0.4f); Toggle(Card.Cabinet); break;
            case "circle":
                if (S.Sanctum.CircleAvailable) Toggle(Card.Circle);
                else { App.Audio.Play("hm", 0.5f); App.Toasts.Add("", "Just chalk, as far as you can tell.", NoticeKind.Info, LineKind.Soft); }
                break;
        }
    }

    private void Toggle(Card c)
    {
        if (_card == c) CloseCard(); else OpenCard(c);
    }

    public override bool Back()
    {
        if (_card == Card.Designation) { OpenCard(Card.Desk); return true; }
        if (_card != Card.None) { CloseCard(); return true; }
        return base.Back();
    }

    // ───────────────────────── actions (all rules live in SanctumLocale) ─────────────────────────

    private void Designate(string objectId)
    {
        var r = S.Sanctum.Designate(objectId, new Dictionary<string, string>(_choice));
        var outcome = S.Sanctum.LastOutcome(objectId);
        switch (outcome)
        {
            case SynthesisKind.Resolved when r.Lines.Any(l => l.Kind == LineKind.Resolved):
                _sealT = 0;
                Hop("desk", 2.4f);
                Burst(JournalAt + new Vector3(0, 0.2f, 0), Palette.Lerp(Accent, Palette.LanternHot, 0.5f), 36, 2.4f);
                Motes(JournalAt, Palette.LanternHot, 14);
                _choice.Clear();
                App.Show(r, S.Content.Objects[objectId].Name);
                break;
            case SynthesisKind.NearMiss:
                _flutter = 0;
                Hop("desk", 1.2f);
                App.Show(r, S.Content.Objects[objectId].Name);
                break;
            default:
                // not yet: a breath across the candles, and the words stay yours to try again
                _gust = 1;
                App.Audio.Play("notyet", 0.6f);
                foreach (var l in r.Lines) App.Toasts.Add("", l.Text, NoticeKind.Info, l.Kind);
                break;
        }
    }

    private void Examine(ItemInstance item, ToolResource tool)
    {
        var r = S.Sanctum.Inspect(item.Id, tool.Id);
        _glassItem = item.Id;
        _lens.Kick(R.ReduceMotion ? 0 : -2.2f);
        Hop("glass", 1.2f);
        App.Audio.Play("glass", 0.6f, 1.15f);
        App.Show(r, "Under the glass");
    }

    private void StartAssembly(BlueprintView b) => PickSlot(b, 0, new Dictionary<int, string>());

    /// <summary>Asks for each component that has a choice, one at a time, then confirms if an essence is going in.</summary>
    private void PickSlot(BlueprintView b, int i, Dictionary<int, string> picks)
    {
        for (; i < b.Slots.Count; i++)
        {
            var (slot, cands) = b.Slots[i];
            if (cands.Count <= 1) continue;
            int at = i;
            var entries = cands.Select(c => new Picker<Choice>.Entry(new Choice(() =>
            {
                picks[at] = c.Id;
                PickSlot(b, at + 1, picks);
            }), S.World.Items.Name(c), S.World.Items.DescribeLocation(c.Location), App.Art.ColorOf(S.World.Items.Def(c)))).ToList();
            var label = slot.Label.Length > 0 ? slot.Label : S.Content.ItemName(slot.Item ?? slot.Trait!);
            Do(() => App.Open(new Picker<Choice>($"Use what for {label}?", entries, ch => ch.Do())));
            return;
        }
        var carrier = b.Slots.SelectMany(s => s.Candidates).FirstOrDefault(c => c.Essence is not null
            && (picks.ContainsValue(c.Id) || b.Slots.Any(s => s.Candidates.Count == 1 && s.Candidates[0] == c)));
        if (carrier is not null)
            Do(() => App.Open(new Confirm("This will change it",
                $"{S.World.Items.Name(carrier)} will go into the {b.Tool.Name} and stay there. (If its original is ever made whole again at the Circle, the tool will go quiet.)",
                () => Assemble(b, picks), "Assemble", "Not yet", Accent)));
        else Assemble(b, picks);
    }

    private void Assemble(BlueprintView b, Dictionary<int, string> picks)
    {
        var r = S.Sanctum.Assemble(b.Tool.Id, picks);
        if (r.Lines.Any(l => l.Kind == LineKind.Discovery) || S.World.Items.All.Any(i => i.Def == b.Tool.Id && !i.Inert))
        {
            _benchFx = 0;
            Hop("bench", 2.2f);
            Burst(BenchPos + new Vector3(0, 1.2f, 0.2f), Palette.LanternHot, 30, 2.2f);
            App.Audio.Play("bell", 0.6f);
        }
        App.Show(r, b.Tool.Name);
    }

    private void Restore(ItemInstance rem)
    {
        var p = S.Sanctum.PreviewReconstitution(rem.Id);
        if (p is null) return;
        // the explicit warning: where the essence lives now, and exactly what goes inert
        var text = $"Set {p.RemnantName} in the circle and {p.RestoresName} will be made whole again.\n\n";
        text += p.CurrentResidence is null ? "Its essence isn't in anything right now." : $"Its essence is in {p.CurrentResidence}.";
        if (p.WillGoInert.Count > 0) text += "\n\nWhat changes:\n" + string.Join("\n", p.WillGoInert.Select(w => "• " + w));
        if (p.Retrace.Count > 0) text += $"\n\nTo have that back afterwards you would need to make it again: {string.Join(" → ", p.Retrace)}.";
        if (p.RemnantsCrumble.Count > 0) text += $"\n\nThese remnants would crumble: {string.Join(", ", p.RemnantsCrumble)}.";
        text += "\n\nAnything it only touched will stay as it is. What you've learned stays in your journal.";
        var restores = rem.Restores;
        Do(() => App.Open(new Confirm(CircleTool()?.Name ?? "The Reconstitution Circle", text, () =>
        {
            var r = S.Sanctum.Reconstitute(rem.Id);
            _circleFx = 0;
            _restoredDef = restores;
            Hop("circle", 1.0f);
            App.Audio.Play("shimmer", 0.7f);
            Motes(CirclePos, Palette.Lerp(Accent, Palette.LanternHot, 0.5f), 18);
            After(R.ReduceMotion ? 0.6f : 1.6f, () => App.Show(r, CircleTool()?.Name ?? "The Circle"));
        }, "Restore it", "Not yet", Accent)));
    }

    private void Offer(SealResource seal, ItemInstance item)
    {
        bool before = S.Sanctum.Solved(seal.Id);
        var a = AnimFor(seal.Id);
        a.PanDef = item.Def;
        a.PanInert = item.Inert;
        a.PanT = 0;
        var r = S.Sanctum.Offer(seal.Id, item.Id);
        App.Audio.Play("clink", 0.6f);
        App.Audio.Play("creak", 0.4f, 0.9f);
        if (!before && S.Sanctum.Solved(seal.Id))
        {
            After(0.5f, () =>
            {
                Burst(SealPos(SealIndex(seal.Id)) + new Vector3(0, 1.2f, 0), Palette.LanternHot, 34, 2.4f);
                App.Audio.Play("bell", 0.7f);
            });
        }
        else if (!R.ReduceMotion) a.Beam.Kick(1.4f); // it dips, considers, and settles back
        App.Show(r, Cap(seal.Name));
    }

    private void Store(ItemInstance i) { App.Audio.Play("slide", 0.5f); App.Show(S.Sanctum.Store(i.Id)); Hop("cabinet", 1.0f); }
    private void Retrieve(ItemInstance i) { App.Audio.Play("paper", 0.5f); App.Show(S.Sanctum.Retrieve(i.Id)); Hop("cabinet", 1.0f); }
    private void Sweep(ItemInstance i) { App.Audio.Play("rake", 0.4f, 1.3f); App.Show(S.Sanctum.ClearHusk(i.Id)); }

    /// <summary>The satchel: offers each sensible Sanctum use for what you're holding.</summary>
    public override bool UseItem(ItemInstance item)
    {
        var entries = new List<Picker<Choice>.Entry>();
        if (Objectish(item))
            foreach (var t in LensTools())
                entries.Add(new(new Choice(() => Examine(item, t)), $"{Cap(t.Verb)} it under the {t.Name}", t.Description, Palette.Sanctum));
        if (item.RemnantOf is not null && S.Sanctum.CircleAvailable)
            entries.Add(new(new Choice(() => Restore(item)), $"Set it in the {CircleTool()?.Name ?? "circle"}", "You'll see exactly what changes before anything does."));
        foreach (var seal in S.Sanctum.Seals().Where(s => !S.Sanctum.Solved(s.Id)))
            entries.Add(new(new Choice(() => Offer(seal, item)), $"Lay it on {seal.Name}", seal.Inscription));
        entries.Add(new(new Choice(() => Store(item)), "Put it away in the cabinet", "Anything kept there is at hand whenever you're in the Sanctum."));
        if (item.Inert) entries.Add(new(new Choice(() => Sweep(item)), "Sweep it away", "It's only matter now."));
        App.Open(new Picker<Choice>(S.World.Items.Name(item), entries, c => c.Do(), S.World.Items.Description(item)));
        return true;
    }

    // ───────────────────────── drawing ─────────────────────────

    protected override void DrawRoomUI()
    {
        var name = App.Content.Locales.TryGetValue(LocaleId, out var l) ? l.Name : "The Sanctum";
        int open = S.Unresolved.Open.Count(), settled = S.Sanctum.Objects().Count(o => S.Ledger.IsResolved(o.Id));
        var sub = open == 0 && settled == 0 ? "candles, chalk and a great deal of quiet" : $"{open} unexplained  ·  {settled} settled";
        var w = MathF.Max(Text.Measure(Ui.F.Hand, name, 44).X, Text.Measure(Ui.F.Italic, sub, 21).X) + 60;
        Raylib.DrawRectangleGradientH(0, 108, (int)w, 86, Palette.Alpha(Palette.Ink, 0.5f), Palette.Alpha(Palette.Ink, 0));
        Title(name, sub);

        if (_card == Card.None) { Tabs(); return; }
        float t = Ease.OutBack(MathF.Min(1, _cardAge / 0.35f), 1.3f);
        float a = MathF.Min(1, _cardAge / 0.2f);
        var r = new Rectangle(Desk.X - (1 - t) * 60, Desk.Y, 600, Desk.Height);
        switch (_card)
        {
            case Card.Desk: DeskCard(r, a); break;
            case Card.Designation when _designObj is not null: DesignationCard(r, a, _designObj); break;
            case Card.Glass: GlassCard(r, a); break;
            case Card.Bench: BenchCard(r, a); break;
            case Card.Cabinet: CabinetCard(r, a); break;
            case Card.Circle: CircleCard(r, a); break;
            case Card.Seal when _seal is not null && S.Sanctum.Seals().FirstOrDefault(s => s.Id == _seal) is { } seal: SealCard(r, a, seal); break;
            default: _card = Card.None; break;
        }
    }

    private void Tabs()
    {
        float x = 30, y = 200;
        var tabs = new List<(string Label, Action Open)>
        {
            ("The desk", () => OpenCard(Card.Desk)),
            ("The glass", () => OpenCard(Card.Glass)),
            ("The bench", () => OpenCard(Card.Bench)),
            ("The cabinet", () => OpenCard(Card.Cabinet)),
        };
        if (S.Sanctum.CircleAvailable) tabs.Add(("The circle", () => OpenCard(Card.Circle)));
        foreach (var s in S.Sanctum.Seals()) { var id = s.Id; tabs.Add(("The Balance", () => { _seal = id; OpenCard(Card.Seal); })); }
        int i = 0;
        foreach (var (label, open) in tabs)
        {
            float bw = Text.Measure(Ui.F.Body, label, 21).X + 34;
            if (x + bw > 660) { x = 30; y += 46; }
            if (Ui.Button("san-tab-" + i++, new Rectangle(x, y, bw, 38), label, Accent, size: 21)) open();
            x += bw + 10;
        }
    }

    private bool CloseButton(Rectangle r, string id)
    {
        bool c = Ui.Button(id, new Rectangle(r.X + r.Width - 58, r.Y + 18, 40, 38), "×", Accent, size: 30, tooltip: "Put the card aside (Esc)");
        if (c) CloseCard();
        return c;
    }

    private float Header(Rectangle r, float alpha, string title, string closeId, string? intro = null)
    {
        Ui.Panel(r, alpha, Accent);
        CloseButton(r, closeId);
        float x = r.X + 32, y = r.Y + 22;
        Ui.Heading(title, new Vector2(x, y), 40);
        Ui.Rule(x, y + 56, 220, Accent);
        y += 72;
        if (intro is not null)
            foreach (var line in Text.Wrap(Ui.F.Italic, intro, 19, r.Width - 70))
            {
                Text.Draw(Ui.F.Italic, line, new Vector2(x, y), 19, Palette.InkSoft);
                y += 23;
            }
        return y + 8;
    }

    private void DeskCard(Rectangle r, float alpha)
    {
        var objs = S.Sanctum.Objects();
        float y = Header(r, alpha, "The designation desk", "san-desk-close",
            "Write down what you believe each thing truly is. A wrong guess costs nothing, and a wrong guess of the interesting kind tells you exactly why.");
        float x = r.X + 26;
        var view = new Rectangle(x, y, r.Width - 52, r.Y + r.Height - y - 74);
        const float rowH = 62;
        float off = Ui.BeginScroll("san-desk", view, Math.Max(1, objs.Count) * rowH);
        float yy = view.Y - off;
        if (objs.Count == 0) Text.Draw(Ui.F.Italic, "Nothing has turned up yet.", new Vector2(x + 8, yy + 10), 21, Palette.InkFaint);
        foreach (var o in objs)
        {
            bool resolved = S.Ledger.IsResolved(o.Id);
            bool ready = !resolved && S.Gate.AnyAttemptable(o);
            var sub = resolved ? "settled, in your best hand" : ready ? "your notes feel full enough to try" : "you'd want to know more first";
            var swatch = resolved ? Palette.Sanctum : ready ? Palette.Moss : Palette.InkFaint;
            if (Ui.Row("san-obj-" + o.Id, new Rectangle(x, yy, view.Width - 14, rowH - 6), Cap(o.Name), sub, swatch, size: 23))
            {
                _designObj = o.Id;
                _choice.Clear();
                OpenCard(Card.Designation);
            }
            yy += rowH;
        }
        Ui.EndScroll();
        if (Ui.Button("san-journal", new Rectangle(r.X + 32, r.Y + r.Height - 62, 220, 42), "Open your journal", size: 22)) Do(() => App.Open(new JournalOverlay()));
    }

    private void DesignationCard(Rectangle r, float alpha, string objectId)
    {
        var card = S.Sanctum.Card(objectId);
        var obj = card.Object;
        bool resolved = S.Ledger.IsResolved(objectId);
        var j = new Atrium.Core.Journal(S).Objects().FirstOrDefault(o => o.Object.Id == objectId);
        float y = Header(r, alpha, Cap(obj.Name), "san-design-close", obj.Summary);
        float x = r.X + 32;
        if (Ui.Button("san-design-back", new Rectangle(r.X + r.Width - 210, r.Y + 22, 140, 38), "All curiosities", size: 19)) { OpenCard(Card.Desk); return; }

        if (resolved)
        {
            Text.Draw(Ui.F.Hand, j?.Designation ?? "Settled.", new Vector2(x, y + 6), 30, Palette.Sanctum);
            Text.Draw(Ui.F.Italic, "Settled. It's written in your journal in your best hand.", new Vector2(x, y + 50), 20, Palette.InkSoft);
            return;
        }

        foreach (var (facet, terms) in card.Facets)
        {
            var chosen = _choice.TryGetValue(facet.Id, out var tid) ? S.Content.TermLabel(tid) : null;
            Text.Draw(Ui.F.Hand, facet.Label, new Vector2(x, y), 25, Palette.Ink);
            Text.Draw(chosen is null ? Ui.F.Italic : Ui.F.Body, chosen ?? (terms.Count == 0 ? "no words for this yet" : "not chosen yet"), new Vector2(x + 14, y + 30), 21,
                chosen is null ? Palette.InkFaint : Palette.Sanctum);
            var fid = facet.Id; var list = terms; var prompt = facet.Prompt;
            if (Ui.Button("san-facet-" + fid, new Rectangle(r.X + r.Width - 170, y + 8, 136, 38), "Choose…", enabled: list.Count > 0, size: 20,
                    tooltip: $"{list.Count} term{(list.Count == 1 ? "" : "s")} known"))
            {
                var entries = list.Select(t => new Picker<TermResource>.Entry(t, t.Label, t.Gloss)).ToList();
                Do(() => App.Open(new Picker<TermResource>(prompt, entries, t => { _choice[fid] = t.Id; App.Audio.Play("term", 0.5f); })));
            }
            y += 62;
        }
        y += 4;
        var readyText = card.Ready ? "Your notes feel full enough to try." : "You'd want to know more before committing to anything. Try it in each house's own way.";
        foreach (var line in Text.Wrap(Ui.F.Italic, readyText, 19, r.Width - 70))
        {
            Text.Draw(Ui.F.Italic, line, new Vector2(x, y), 19, card.Ready ? Palette.Moss : Palette.InkFaint);
            y += 23;
        }
        y += 8;
        if (j is not null && j.NearMisses.Count > 0)
        {
            Text.Draw(Ui.F.Hand, "Already ruled out", new Vector2(x, y), 24, Palette.Apothecary);
            y += 32;
            var view = new Rectangle(x - 4, y, r.Width - 60, r.Y + r.Height - y - 76);
            var wrapped = j.NearMisses.Select(m => Text.Wrap(Ui.F.Italic, m, 18, view.Width - 30)).ToList();
            float total = wrapped.Sum(wl => wl.Count * 22 + 8);
            float off = Ui.BeginScroll("san-design", view, total);
            float yy = view.Y - off;
            foreach (var wl in wrapped)
            {
                Ui.Diamond(new Vector2(view.X + 8, yy + 11), 4, Palette.Apothecary);
                foreach (var line in wl) { Text.Draw(Ui.F.Italic, line, new Vector2(view.X + 22, yy), 18, Palette.InkSoft); yy += 22; }
                yy += 8;
            }
            Ui.EndScroll();
        }
        bool all = card.Facets.All(f => _choice.ContainsKey(f.Facet.Id));
        if (Ui.Button("san-commit", new Rectangle(x, r.Y + r.Height - 62, 260, 44), "Commit this designation", Accent, enabled: all, size: 22,
                tooltip: all ? "Write it in your journal" : "Choose a word for every line first"))
            Designate(objectId);
        if (Ui.Button("san-notes", new Rectangle(x + 274, r.Y + r.Height - 62, 200, 44), "Your notes on it", size: 21)) Do(() => App.Open(new JournalOverlay()));
    }

    private void GlassCard(Rectangle r, float alpha)
    {
        var lens = LensTools().ToList();
        var items = S.Sanctum.AtHand.Where(Objectish).ToList();
        float y = Header(r, alpha, lens.FirstOrDefault()?.Name ?? "The glass", "san-glass-close",
            lens.FirstOrDefault()?.Description ?? "A glass on a brass stand.");
        float x = r.X + 26;
        var view = new Rectangle(x, y, r.Width - 52, r.Y + r.Height - y - 24);
        const float rowH = 60;
        float off = Ui.BeginScroll("san-glass", view, Math.Max(1, items.Count) * rowH);
        float yy = view.Y - off;
        if (items.Count == 0)
            Text.Draw(Ui.F.Italic, "Nothing curious at hand. Bring odd things in your satchel, or keep them in the cabinet.", new Vector2(x + 6, yy + 10), 19, Palette.InkFaint);
        foreach (var it in items)
        {
            var row = new Rectangle(x, yy, view.Width - 14, rowH - 6);
            Raylib.DrawRectangleRounded(row, 0.2f, 6, Palette.Alpha(Palette.ParchmentDark, 0.35f));
            App.Art.Draw(S.World.Items.Def(it), new Vector2(row.X + 28, row.Y + row.Height / 2), 40, it.Inert, Ui.Time);
            Text.Draw(Ui.F.Body, S.World.Items.Name(it), new Vector2(row.X + 58, row.Y + 6), 22, Palette.Ink);
            Text.Draw(Ui.F.Italic, S.World.Items.DescribeLocation(it.Location), new Vector2(row.X + 58, row.Y + 30), 17, Palette.InkFaint);
            float bx = row.X + row.Width - 8;
            foreach (var tool in lens)
            {
                var label = Cap(tool.Verb.Length > 0 ? tool.Verb : "examine");
                float bw = Text.Measure(Ui.F.Body, label, 20).X + 30;
                bx -= bw;
                if (Ui.Button($"san-exam-{it.Id}-{tool.Id}", new Rectangle(bx, row.Y + 9, bw, 36), label, Accent, size: 20)) Examine(it, tool);
                bx -= 8;
            }
            yy += rowH;
        }
        Ui.EndScroll();
    }

    private void BenchCard(Rectangle r, float alpha)
    {
        var bps = S.Sanctum.Blueprints();
        float y = Header(r, alpha, "The assembly bench", "san-bench-close",
            bps.Count == 0 ? "You don't know how to make anything here yet. Ideas for tools turn up in books, and in what people tell you."
                           : "Components come from your satchel or the cabinet. Bottle elements at the Apothecary to bring them here.");
        float x = r.X + 26;
        var view = new Rectangle(x, y, r.Width - 52, r.Y + r.Height - y - 24);
        var heights = bps.Select(b => 76f + Text.Wrap(Ui.F.Italic, b.Tool.Description, 18, view.Width - 40).Count * 22 + b.Slots.Count * 26).ToList();
        float off = Ui.BeginScroll("san-bench", view, heights.Sum() + 10);
        float yy = view.Y - off;
        for (int i = 0; i < bps.Count; i++)
        {
            var b = bps[i];
            var box = new Rectangle(x, yy, view.Width - 14, heights[i] - 10);
            Raylib.DrawRectangleRounded(box, 0.08f, 6, Palette.Alpha(Palette.Hex(0xCFDCE6), 0.35f));
            Raylib.DrawRectangleRoundedLinesEx(box, 0.08f, 6, 1.2f, Palette.Alpha(Palette.Library, 0.35f));
            bool owned = S.World.Items.All.Any(it => it.Def == b.Tool.Id && !it.Inert);
            Text.Draw(Ui.F.Hand, b.Tool.Name, new Vector2(box.X + 14, box.Y + 8), 27, Palette.Ink);
            if (owned) Text.Draw(Ui.F.Italic, "you have one", new Vector2(box.X + 22 + Text.Measure(Ui.F.Hand, b.Tool.Name, 27).X, box.Y + 15), 18, Palette.Moss);
            float ly = box.Y + 44;
            foreach (var line in Text.Wrap(Ui.F.Italic, b.Tool.Description, 18, view.Width - 40))
            {
                Text.Draw(Ui.F.Italic, line, new Vector2(box.X + 14, ly), 18, Palette.InkSoft);
                ly += 22;
            }
            foreach (var (slot, cands) in b.Slots)
            {
                var label = slot.Label.Length > 0 ? slot.Label : S.Content.ItemName(slot.Item ?? slot.Trait!);
                var has = cands.Count > 0;
                if (has)
                {
                    // a drawn tick (the book face has no check-mark glyph)
                    Raylib.DrawLineEx(new Vector2(box.X + 15, ly + 11), new Vector2(box.X + 20, ly + 16), 2.4f, Palette.Moss);
                    Raylib.DrawLineEx(new Vector2(box.X + 20, ly + 16), new Vector2(box.X + 28, ly + 5), 2.4f, Palette.Moss);
                }
                else Raylib.DrawCircleV(new Vector2(box.X + 21, ly + 11), 3, Palette.InkFaint);
                Text.Draw(Ui.F.Body, label, new Vector2(box.X + 36, ly), 19, has ? Palette.Ink : Palette.InkFaint);
                ly += 26;
            }
            if (Ui.Button("san-asm-" + b.Tool.Id, new Rectangle(box.X + box.Width - 150, box.Y + 10, 136, 36), "Assemble", Accent, enabled: b.CanAssemble, size: 20,
                    tooltip: b.CanAssemble ? "You have everything it needs" : "Something's still missing"))
                StartAssembly(b);
            yy += heights[i];
        }
        Ui.EndScroll();
    }

    private void CabinetCard(Rectangle r, float alpha)
    {
        var cab = S.Sanctum.Cabinet.ToList();
        var sat = S.World.Items.Satchel.ToList();
        float y = Header(r, alpha, "The cabinet", "san-cab-close", "A tall cedar cabinet with deep drawers. Anything kept here is at hand whenever you're in the Sanctum.");
        float x = r.X + 26;
        var view = new Rectangle(x, y, r.Width - 52, r.Y + r.Height - y - 24);
        const float rowH = 54;
        float total = (Math.Max(1, cab.Count) + Math.Max(1, sat.Count)) * rowH + 90;
        float off = Ui.BeginScroll("san-cab", view, total);
        float yy = view.Y - off;
        void Section(string title, List<ItemInstance> items, string empty, string verb, Action<ItemInstance> act)
        {
            Text.Draw(Ui.F.Hand, title, new Vector2(x, yy), 26, Palette.Ink);
            yy += 38;
            if (items.Count == 0) { Text.Draw(Ui.F.Italic, empty, new Vector2(x + 8, yy + 6), 19, Palette.InkFaint); yy += rowH; }
            foreach (var it in items)
            {
                var row = new Rectangle(x, yy, view.Width - 14, rowH - 6);
                Raylib.DrawRectangleRounded(row, 0.2f, 6, Palette.Alpha(Palette.ParchmentDark, 0.3f));
                App.Art.Draw(S.World.Items.Def(it), new Vector2(row.X + 24, row.Y + row.Height / 2), 34, it.Inert, Ui.Time);
                Text.Draw(Ui.F.Body, S.World.Items.Name(it) + (it.Inert ? "  (inert)" : ""), new Vector2(row.X + 50, row.Y + 12), 21, it.Inert ? Palette.InkFaint : Palette.Ink);
                float bx = row.X + row.Width - 128;
                if (Ui.Button($"san-cab-{verb}-{it.Id}", new Rectangle(bx, row.Y + 7, 120, 34), verb, size: 19)) act(it);
                if (it.Inert && Ui.Button($"san-sweep-{it.Id}", new Rectangle(bx - 128, row.Y + 7, 120, 34), "Sweep away", size: 19)) Sweep(it);
                yy += rowH;
            }
            yy += 10;
        }
        Section("Put away", cab, "Empty, and smelling of cedar.", "Take out", Retrieve);
        Section("In your satchel", sat, "Your satchel is empty.", "Put away", Store);
        Ui.EndScroll();
    }

    private void CircleCard(Rectangle r, float alpha)
    {
        var tool = CircleTool();
        var rems = S.Sanctum.Remnants();
        float y = Header(r, alpha, tool?.Name ?? "The circle", "san-circle-close",
            ((tool?.Description ?? "").Length > 0 ? tool!.Description + " " : "Set a remnant in it to make its original whole again. ") + "You'll see exactly what changes before anything does.");
        float x = r.X + 26;
        var view = new Rectangle(x, y, r.Width - 52, r.Y + r.Height - y - 24);
        const float rowH = 66;
        float off = Ui.BeginScroll("san-circle", view, Math.Max(1, rems.Count) * rowH);
        float yy = view.Y - off;
        if (rems.Count == 0)
            Text.Draw(Ui.F.Italic, "No remnants at hand. When something is taken apart, what's left behind can be brought here.", new Vector2(x + 6, yy + 10), 19, Palette.InkFaint);
        foreach (var rem in rems)
        {
            var row = new Rectangle(x, yy, view.Width - 14, rowH - 6);
            Raylib.DrawRectangleRounded(row, 0.2f, 6, Palette.Alpha(Palette.ParchmentDark, 0.35f));
            App.Art.Draw(S.World.Items.Def(rem), new Vector2(row.X + 28, row.Y + row.Height / 2), 40, rem.Inert, Ui.Time);
            Text.Draw(Ui.F.Body, S.World.Items.Name(rem), new Vector2(row.X + 58, row.Y + 6), 22, Palette.Ink);
            var restores = rem.Restores is { } d ? $"would restore {S.Content.ItemName(d)}" : S.World.Items.Description(rem);
            Text.Draw(Ui.F.Italic, restores, new Vector2(row.X + 58, row.Y + 32), 18, Palette.InkFaint);
            if (Ui.Button("san-restore-" + rem.Id, new Rectangle(row.X + row.Width - 128, row.Y + 12, 120, 36), "Restore…", Accent, size: 20)) Restore(rem);
            yy += rowH;
        }
        Ui.EndScroll();
    }

    private void SealCard(Rectangle r, float alpha, SealResource seal)
    {
        bool solved = S.Sanctum.Solved(seal.Id);
        float y = Header(r, alpha, Cap(seal.Name), "san-seal-close", solved ? "It stands open above an empty reliquary." : seal.Inscription);
        if (solved) return;
        float x = r.X + 26;
        var items = S.Sanctum.AtHand.ToList();
        Text.Draw(Ui.F.Hand, "Lay something on the pan", new Vector2(x + 6, y), 26, Palette.Ink);
        y += 38;
        var view = new Rectangle(x, y, r.Width - 52, r.Y + r.Height - y - 24);
        const float rowH = 54;
        float off = Ui.BeginScroll("san-seal", view, Math.Max(1, items.Count) * rowH);
        float yy = view.Y - off;
        if (items.Count == 0) Text.Draw(Ui.F.Italic, "Nothing at hand to weigh. Offering never uses anything up.", new Vector2(x + 6, yy + 10), 19, Palette.InkFaint);
        foreach (var it in items)
        {
            var row = new Rectangle(x, yy, view.Width - 14, rowH - 6);
            Raylib.DrawRectangleRounded(row, 0.2f, 6, Palette.Alpha(Palette.ParchmentDark, 0.3f));
            App.Art.Draw(S.World.Items.Def(it), new Vector2(row.X + 24, row.Y + row.Height / 2), 34, it.Inert, Ui.Time);
            Text.Draw(Ui.F.Body, S.World.Items.Name(it), new Vector2(row.X + 50, row.Y + 4), 21, Palette.Ink);
            Text.Draw(Ui.F.Italic, S.World.Items.DescribeLocation(it.Location), new Vector2(row.X + 50, row.Y + 26), 17, Palette.InkFaint);
            if (Ui.Button("san-offer-" + it.Id, new Rectangle(row.X + row.Width - 128, row.Y + 7, 120, 34), "Lay it on", Accent, size: 19)) Offer(seal, it);
            yy += rowH;
        }
        Ui.EndScroll();
    }
}
