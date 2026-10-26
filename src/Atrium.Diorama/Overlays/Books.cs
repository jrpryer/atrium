using System.Numerics;
using Atrium.Core;
using Atrium.Core.Locales;
using Atrium.Core.Runtime;
using Atrium.Diorama.App;
using Atrium.Diorama.Render;
using Atrium.Diorama.UI;
using Raylib_cs;

namespace Atrium.Diorama.Overlays;

/// <summary>Draws an open book: two cream pages, a gutter shadow, a leather edge.</summary>
public static class BookArt
{
    public static (Rectangle Left, Rectangle Right) Spread(Ui ui, float age, Color cover)
    {
        float t = Ease.OutBack(MathF.Min(1, age / 0.4f), 1.1f);
        float w = 1260 * (0.92f + 0.08f * t), h = 760 * (0.92f + 0.08f * t);
        var r = new Rectangle(Ui.W / 2 - w / 2, Ui.H / 2 - h / 2 + 10 + (1 - t) * 30, w, h);
        ui.Block(r);
        Raylib.DrawRectangleRounded(new Rectangle(r.X + 6, r.Y + 12, r.Width, r.Height), 0.04f, 8, Palette.Alpha(Palette.Ink, 0.35f));
        Raylib.DrawRectangleRounded(new Rectangle(r.X - 14, r.Y - 10, r.Width + 28, r.Height + 22), 0.04f, 8, cover);
        var left = new Rectangle(r.X, r.Y, r.Width / 2, r.Height);
        var right = new Rectangle(r.X + r.Width / 2, r.Y, r.Width / 2, r.Height);
        var page = Palette.Hex(0xF6EBD2);
        Raylib.DrawRectangleRounded(left, 0.03f, 6, page);
        Raylib.DrawRectangleRounded(right, 0.03f, 6, page);
        Raylib.DrawRectangleGradientH((int)(r.X + r.Width / 2 - 40), (int)r.Y, 40, (int)r.Height, Palette.Alpha(Palette.Ink, 0), Palette.Alpha(Palette.Ink, 0.16f));
        Raylib.DrawRectangleGradientH((int)(r.X + r.Width / 2), (int)r.Y, 40, (int)r.Height, Palette.Alpha(Palette.Ink, 0.16f), Palette.Alpha(Palette.Ink, 0));
        for (int i = 1; i <= 3; i++)
        {
            Raylib.DrawLineEx(new Vector2(r.X + 4, r.Y + r.Height + i * 2), new Vector2(r.X + r.Width - 4, r.Y + r.Height + i * 2), 1, Palette.Alpha(Palette.ParchmentDark, 0.9f));
        }
        return (Inset(left), Inset(right));
    }

    private static Rectangle Inset(Rectangle r) => new(r.X + 60, r.Y + 50, r.Width - 110, r.Height - 100);
}

/// <summary>A tome, open: turn pages, hold an object against a page, write in the margin, check it out.</summary>
public sealed class ReaderOverlay : Overlay
{
    private readonly string _tome;
    private int _page;
    private float _flip = 1;
    private bool _opened;

    public ReaderOverlay(string tomeId) => _tome = tomeId;

    public override void Draw()
    {
        if (!_opened) { _opened = true; S.Library.Open(_tome); App.Audio.Play("page"); }
        var view = S.Library.Open(_tome, record: false);
        if (view is null) { Close(); return; }
        Ui.Dim(0.5f * MathF.Min(1, Age / 0.2f));
        var t = view.Tome;
        var genre = S.Content.Genres.TryGetValue(t.Genre, out var g) ? Palette.Genre(g.Color) : Palette.StoneDark;
        var (left, right) = BookArt.Spread(Ui, Age, Palette.Mul(genre, 0.75f));

        // left page: the title page
        var titleLines = Text.Wrap(Ui.F.Hand, t.Title, 44, left.Width);
        float y = left.Y + 10;
        foreach (var l in titleLines) { Text.Draw(Ui.F.Hand, l, new Vector2(left.X, y), 44, Palette.Ink); y += 48; }
        Ui.Rule(left.X, y + 10, 220, genre);
        Text.Draw(Ui.F.Italic, $"{t.Author} · SR {t.Year}", new Vector2(left.X, y + 26), 24, Palette.InkSoft);
        Text.Draw(Ui.F.Italic, view.Where, new Vector2(left.X, y + 56), 22, Palette.InkFaint);
        Ui.Paragraph($"Bound in {t.Cover}.", new Rectangle(left.X, y + 100, left.Width, 120), 23, Palette.InkSoft, Ui.F.Italic);
        if (t.Blurb.Length > 0) Ui.Paragraph(t.Blurb, new Rectangle(left.X, y + 170, left.Width, 160), 25, Palette.Ink);

        // buttons on the left page
        float by = left.Y + left.Height - 150;
        bool canHold = S.World.Items.Satchel.Any(i => i.Essence is not null || i.RemnantOf is not null);
        if (view.Pages.Count > 0 && Ui.Button("rd-hold", new Rectangle(left.X, by, 320, 44), "Hold something against this page", Palette.Library, canHold))
            HoldSomething(view);
        if (view.Pages.Count > 0 && Ui.Button("rd-note", new Rectangle(left.X, by + 54, 200, 44), "Write a note"))
            App.Open(new TextEntry("A note in the margin", view.Pages[Math.Clamp(_page, 0, view.Pages.Count - 1)].Note ?? "",
                text => App.Show(S.Library.Annotate(_tome, view.Pages[_page].Number, text))));
        bool out_ = S.Library.CheckedOut().Any(b => b.Id == _tome);
        if (Ui.Button("rd-checkout", new Rectangle(left.X + 210, by + 54, 230, 44), out_ ? "Return it" : "Check it out"))
            App.Show(out_ ? S.Library.Return(_tome) : S.Library.CheckOut(_tome));
        if (Ui.Button("rd-close", new Rectangle(left.X, by + 108, 200, 40), "Close the book")) Close();

        // right page: the current page, with a page-turn squash
        if (view.Pages.Count == 0) { Ui.Paragraph("There's nothing in it.", right, 26, Palette.InkSoft, Ui.F.Italic); return; }
        _page = Math.Clamp(_page, 0, view.Pages.Count - 1);
        _flip = MathF.Min(1, _flip + Ui.Dt * 4);
        var p = view.Pages[_page];
        float sx = Ease.OutCubic(_flip);
        var pr = new Rectangle(right.X + right.Width * (1 - sx) / 2, right.Y, right.Width * sx, right.Height);
        Text.Draw(Ui.F.Italic, $"page {p.Number} of {view.Pages.Count}" + (p.Faded ? " · faded ink" : ""), new Vector2(pr.X, pr.Y), 21, Palette.InkFaint);
        float ty = pr.Y + 34;
        if (p.Title is not null) { Text.Draw(Ui.F.Hand, p.Title, new Vector2(pr.X, ty), 34, Palette.Ink); ty += 46; }
        var body = new Rectangle(pr.X, ty, pr.Width, pr.Y + pr.Height - ty - 90);
        if (p.Text is null)
            Ui.Paragraph("The ink here has faded almost to nothing. Something was written, in a small careful hand, but not what. Perhaps by a different light…", body, 25, Palette.InkFaint, Ui.F.Italic);
        else
        {
            var ink = p.Faded ? Palette.Hex(0xA8781E) : Palette.Ink;
            float hgt = Text.Height(p.Verbatim ? Ui.F.Body : Ui.F.Body, p.Text, body.Width, p.Verbatim ? 23 : 26, 1.3f, p.Verbatim);
            float off = Ui.BeginScroll("rd-page-" + _page, body, hgt + 8);
            Ui.Paragraph(p.Text, new Rectangle(body.X, body.Y - off, body.Width, body.Height), p.Verbatim ? 23 : 26, ink, null, int.MaxValue, p.Verbatim);
            Ui.EndScroll();
        }
        if (p.Note is not null)
            Ui.Paragraph("Your note: " + p.Note, new Rectangle(pr.X, pr.Y + pr.Height - 80, pr.Width, 60), 22, Palette.Library, Ui.F.Italic);

        if (_page > 0 && (Ui.Button("rd-prev", new Rectangle(right.X - 30, right.Y + right.Height + 6, 56, 40), "‹") || Input.KeyPressed(KeyboardKey.Left)))
        { _page--; _flip = 0; App.Audio.Play("page", 0.6f); }
        if (_page < view.Pages.Count - 1 && (Ui.Button("rd-next", new Rectangle(right.X + right.Width - 26, right.Y + right.Height + 6, 56, 40), "›") || Input.KeyPressed(KeyboardKey.Right)))
        { _page++; _flip = 0; App.Audio.Play("page", 0.6f); }
    }

    private void HoldSomething(TomeView view)
    {
        var page = view.Pages[_page];
        var items = S.World.Items.Satchel.Where(i => i.Essence is not null || i.RemnantOf is not null)
            .Select(i => new Picker<ItemInstance>.Entry(i, S.World.Items.Name(i), null, App.Art.ColorOf(S.Content.Item(i.Def)))).ToList();
        App.Open(new Picker<ItemInstance>("Hold what against the page?", items, item =>
        {
            var r = S.Library.Compare(item.Id, _tome, page.Number);
            App.Show(r, S.World.Items.Name(item));
        }));
    }
}

/// <summary>A one-line text entry (margin notes).</summary>
public sealed class TextEntry : Overlay
{
    private readonly string _title;
    private string _text;
    private readonly Action<string> _done;

    public TextEntry(string title, string initial, Action<string> done) { _title = title; _text = initial; _done = done; }

    public override void Draw()
    {
        Ui.Dim(0.4f);
        var r = new Rectangle(Ui.W / 2 - 400, Ui.H / 2 - 150, 800, 300);
        Ui.Panel(r);
        Ui.HeadingCentered(_title, new Vector2(r.X + 400, r.Y + 50), 40);
        if (Ui.InputEnabled)
        {
            int c;
            while ((c = Raylib.GetCharPressed()) != 0) if (_text.Length < 200) _text += char.ConvertFromUtf32(c);
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && _text.Length > 0) _text = _text[..^1];
            if (Raylib.IsKeyPressed(KeyboardKey.Enter)) { Close(); _done(_text.Trim()); return; }
        }
        var box = new Rectangle(r.X + 50, r.Y + 100, 700, 100);
        Raylib.DrawRectangleRounded(box, 0.1f, 6, Palette.Hex(0xF6EBD2));
        Raylib.DrawRectangleRoundedLinesEx(box, 0.1f, 6, 1.5f, Palette.InkSoft);
        bool caret = (int)(Ui.Time * 2) % 2 == 0;
        Ui.Paragraph(_text + (caret ? "|" : ""), new Rectangle(box.X + 14, box.Y + 12, box.Width - 28, box.Height - 20), 25, Palette.Library, Ui.F.Italic);
        Text.Draw(Ui.F.Italic, "Type your note, then press Enter. Leave it empty to rub out an old one.", new Vector2(r.X + 50, r.Y + 210), 20, Palette.InkFaint);
        if (Ui.Button("te-ok", new Rectangle(r.X + 230, r.Y + 240, 160, 42), "Done")) { Close(); _done(_text.Trim()); }
        if (Ui.Button("te-cancel", new Rectangle(r.X + 410, r.Y + 240, 160, 42), "Never mind")) Close();
    }
}

/// <summary>The Player Journal: an open book with ribbon tabs. Nothing written here is ever lost.</summary>
public sealed class JournalOverlay : Overlay
{
    private int _tab;
    private string? _obj;
    private static readonly string[] Tabs = { "Mysteries", "Lexicon", "Story", "Notes", "Log" };

    public override void Draw()
    {
        if (Age == 0 || Age < Ui.Dt * 1.5f) App.Audio.Play("page");
        Ui.Dim(0.5f * MathF.Min(1, Age / 0.2f));
        var (left, right) = BookArt.Spread(Ui, Age, Palette.Hex(0x6B4A36));
        var j = new Journal(S);
        // ribbon tabs down the right edge
        for (int i = 0; i < Tabs.Length; i++)
        {
            var tr = new Rectangle(right.X + right.Width + 58, right.Y + i * 74, 150, 58);
            bool sel = _tab == i;
            bool hov = Ui.Hover(tr);
            float h = Ui.Anim("jt" + i, hov || sel);
            Ui.Block(tr);
            var col = new[] { Palette.Sanctum, Palette.Library, Palette.Apothecary, Palette.Pools, Palette.InkSoft }[i];
            Raylib.DrawRectangleRounded(new Rectangle(tr.X - 10 + h * 8, tr.Y, tr.Width, tr.Height), 0.3f, 6, Palette.Lerp(Palette.Mul(col, 0.8f), col, h));
            Text.DrawCentered(Ui.F.Hand, Tabs[i], new Vector2(tr.X - 10 + h * 8 + tr.Width / 2, tr.Y + tr.Height / 2), 25, Palette.Parchment);
            if (hov && Input.Pressed) { _tab = i; Input.ClickConsumed = true; App.Audio.Play("page", 0.5f); }
        }
        if (Ui.Button("jr-close", new Rectangle(left.X, left.Y + left.Height + 12, 200, 40), "Close the journal")) Close();

        switch (_tab)
        {
            case 0: Mysteries(j, left, right); break;
            case 1: Lexicon(j, left, right); break;
            case 2: Story(j, left, right); break;
            case 3: Notes(j, left, right); break;
            default: Log(j, left, right); break;
        }
    }

    private void Mysteries(Journal j, Rectangle left, Rectangle right)
    {
        var objs = j.Objects();
        Ui.Heading("Curiosities", new Vector2(left.X, left.Y), 40);
        Ui.Rule(left.X, left.Y + 52, 260, Palette.Sanctum);
        if (objs.Count == 0) { Ui.Paragraph("Nothing odd has turned up yet.", new Rectangle(left.X, left.Y + 80, left.Width, 80), 25, Palette.InkSoft, Ui.F.Italic); return; }
        _obj ??= objs[0].Object.Id;
        float y = left.Y + 76;
        foreach (var o in objs)
        {
            var row = new Rectangle(left.X - 8, y, left.Width, 64);
            if (Ui.Row("jo-" + o.Object.Id, row, Cap(o.Object.Name), o.Resolved ? o.Designation : $"{o.Readings.Count} notes · {o.Where}",
                    o.Resolved ? Palette.Sanctum : Palette.InkFaint, _obj == o.Object.Id))
                _obj = o.Object.Id;
            y += 70;
        }
        var sel = objs.FirstOrDefault(o => o.Object.Id == _obj) ?? objs[0];
        Ui.Heading(Cap(sel.Object.Name), new Vector2(right.X, right.Y), 40);
        Text.Draw(Ui.F.Italic, sel.Object.Summary, new Vector2(right.X, right.Y + 52), 21, Palette.InkSoft);
        float content = Measure(sel, right.Width);
        var view = new Rectangle(right.X, right.Y + 90, right.Width + 30, right.Height - 90);
        float off = Ui.BeginScroll("jr-entry-" + sel.Object.Id, view, content);
        float yy = view.Y - off;
        if (sel.Designation is not null) { Text.Draw(Ui.F.Hand, sel.Designation, new Vector2(right.X, yy), 28, Palette.Sanctum); yy += 44; }
        foreach (var g in sel.Readings.GroupBy(r => r.Locale))
        {
            var col = Palette.Locale(S.Content.Locales.Values.FirstOrDefault(l => l.Name == g.Key)?.Id ?? "");
            Text.Draw(Ui.F.Hand, "At " + g.Key.Replace("The ", "the "), new Vector2(right.X, yy), 28, col);
            yy += 36;
            foreach (var r in g) yy += Ui.Paragraph(r.Text, new Rectangle(right.X + 16, yy, right.Width - 16, 600), 22) + 10;
        }
        if (sel.NearMisses.Count > 0)
        {
            Text.Draw(Ui.F.Hand, "Ruled out", new Vector2(right.X, yy), 28, Palette.Apothecary); yy += 36;
            foreach (var m in sel.NearMisses) yy += Ui.Paragraph(m, new Rectangle(right.X + 16, yy, right.Width - 16, 600), 22, Palette.Apothecary) + 10;
        }
        if (sel.Considered.Count > 0)
        {
            Text.Draw(Ui.F.Hand, "Considered, didn't hold", new Vector2(right.X, yy), 28, Palette.InkSoft); yy += 36;
            foreach (var m in sel.Considered) yy += Ui.Paragraph(m, new Rectangle(right.X + 16, yy, right.Width - 16, 200), 22, Palette.InkSoft, Ui.F.Italic) + 6;
        }
        if (sel.Touched.Count > 0)
        {
            Text.Draw(Ui.F.Hand, "What it has touched", new Vector2(right.X, yy), 28, Palette.LeafDeep); yy += 36;
            foreach (var m in sel.Touched) yy += Ui.Paragraph(m, new Rectangle(right.X + 16, yy, right.Width - 16, 200), 22, Palette.LeafDeep) + 6;
        }
        Ui.EndScroll();
    }

    private float Measure(JournalObject o, float w)
    {
        float h = o.Designation is null ? 0 : 44;
        foreach (var g in o.Readings.GroupBy(r => r.Locale)) { h += 36; foreach (var r in g) h += Text.Height(Ui.F.Body, r.Text, w - 16, 22, 1.3f) + 10; }
        if (o.NearMisses.Count > 0) { h += 36; foreach (var m in o.NearMisses) h += Text.Height(Ui.F.Body, m, w - 16, 22, 1.3f) + 10; }
        if (o.Considered.Count > 0) { h += 36; foreach (var m in o.Considered) h += Text.Height(Ui.F.Italic, m, w - 16, 22, 1.3f) + 6; }
        if (o.Touched.Count > 0) { h += 36; foreach (var m in o.Touched) h += Text.Height(Ui.F.Body, m, w - 16, 22, 1.3f) + 6; }
        return h + 20;
    }

    private void Lexicon(Journal j, Rectangle left, Rectangle right)
    {
        var lex = j.Lexicon();
        var cols = new[] { left, right };
        Ui.Heading("Words you've earned", new Vector2(left.X, left.Y), 40);
        float y = left.Y + 70;
        int col = 0;
        foreach (var (facet, terms) in lex)
        {
            float need = 40 + Math.Max(1, terms.Count) * 30;
            if (col == 0 && y + need > left.Y + left.Height) { col = 1; y = right.Y; }
            var area = cols[col];
            Text.Draw(Ui.F.Hand, facet.Label, new Vector2(area.X, y), 30, Palette.Sanctum);
            y += 40;
            if (terms.Count == 0) { Text.Draw(Ui.F.Italic, "(no words yet)", new Vector2(area.X + 16, y), 22, Palette.InkFaint); y += 30; }
            foreach (var t in terms) { Text.Draw(Ui.F.Body, t.Label, new Vector2(area.X + 16, y), 23, Palette.Ink); Text.Draw(Ui.F.Italic, t.Gloss, new Vector2(area.X + 260, y + 2), 19, Palette.InkSoft); y += 30; }
            y += 12;
        }
    }

    private void Story(Journal j, Rectangle left, Rectangle right)
    {
        var beats = j.Story();
        Ui.Heading("The story so far", new Vector2(left.X, left.Y), 40);
        Ui.Rule(left.X, left.Y + 52, 260, Palette.Apothecary);
        if (beats.Count == 0) { Ui.Paragraph("The story hasn't really started. Or it has, and you haven't noticed yet.", new Rectangle(left.X, left.Y + 80, left.Width, 120), 25, Palette.InkSoft, Ui.F.Italic); return; }
        float total = beats.Sum(b => 46 + Text.Height(Ui.F.Body, b.Text, left.Width + right.Width, 22, 1.3f) + 20);
        var view = new Rectangle(left.X, left.Y + 70, left.Width, left.Height - 70);
        float off = Ui.BeginScroll("jr-story", view, beats.Sum(b => 46 + Text.Height(Ui.F.Body, b.Text, left.Width, 22, 1.3f) + 20));
        float y = view.Y - off;
        foreach (var b in beats)
        {
            Text.Draw(Ui.F.Hand, b.Title, new Vector2(left.X, y), 30, Palette.Ink); y += 42;
            y += Ui.Paragraph(b.Text, new Rectangle(left.X, y, left.Width, 600), 22, Palette.InkSoft) + 20;
        }
        Ui.EndScroll();
        Ui.Paragraph("Everything here stays written down, whatever happens to the things themselves.", new Rectangle(right.X, right.Y + 40, right.Width, 100), 24, Palette.InkFaint, Ui.F.Italic);
    }

    private void Notes(Journal j, Rectangle left, Rectangle right)
    {
        Ui.Heading("Margin notes", new Vector2(left.X, left.Y), 40);
        var notes = j.Notes();
        if (notes.Count == 0) { Ui.Paragraph("You haven't written in any margins yet. Open a book in the Library and write a note.", new Rectangle(left.X, left.Y + 70, left.Width, 120), 24, Palette.InkSoft, Ui.F.Italic); return; }
        float y = left.Y + 70;
        foreach (var n in notes)
        {
            var area = y > left.Y + left.Height - 80 ? right : left;
            if (area.X == right.X && y > left.Y + left.Height - 80) y = right.Y;
            Text.Draw(Ui.F.Italic, $"{n.Tome}, p.{n.Page}", new Vector2(area.X, y), 20, Palette.InkFaint); y += 26;
            y += Ui.Paragraph(n.Text, new Rectangle(area.X + 12, y, area.Width - 12, 200), 23, Palette.Library, Ui.F.Italic) + 14;
        }
    }

    private void Log(Journal j, Rectangle left, Rectangle right)
    {
        Ui.Heading("Everything you've seen", new Vector2(left.X, left.Y), 40);
        var log = j.Log().Reverse().ToList();
        var view = new Rectangle(left.X, left.Y + 70, left.Width + right.Width + 110, left.Height - 70);
        float content = log.Count * 30;
        float off = Ui.BeginScroll("jr-log", view, content);
        float y = view.Y - off;
        foreach (var e in log)
        {
            if (y > view.Y - 30 && y < view.Y + view.Height)
            {
                var text = e.Text.Replace('\n', ' ');
                if (text.Length > 120) text = text[..118] + "…";
                Text.Draw(Ui.F.Italic, $"day {e.Tick / 2400 + 1}", new Vector2(view.X, y), 19, Palette.InkFaint);
                Text.Draw(Ui.F.Body, text, new Vector2(view.X + 70, y), 21, Palette.Ink);
            }
            y += 30;
        }
        Ui.EndScroll();
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
