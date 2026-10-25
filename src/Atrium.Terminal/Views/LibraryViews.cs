using Atrium.Core.Content;
using Atrium.Core.Locales;
using Atrium.Core.Runtime;
using Atrium.Terminal.Ui;

namespace Atrium.Terminal.Views;

public sealed class LibraryView : LocaleView
{
    public override string Title => "The Library";
    public override string Locale => "library";

    public override void Render(Frame f)
    {
        var lib = S.Library;
        foreach (var sh in lib.Shelves())
        {
            var g = sh.Genre is not null && C.Genres.TryGetValue(sh.Genre, out var gg) ? gg : null;
            var color = g is null ? Palette.Faint : Palette.Genre(g.Color);
            var spines = string.Concat(sh.Tomes.Select(_ => "▌"));
            var status = sh.Tomes.Count == 0 ? "" : sh.Settled ? Ansi.Fg(Palette.Lamp, "  ♪ settled") : sh.InOrder ? Ansi.Fg(Palette.Dim, "  in order") : Ansi.Fg(Palette.Rose, "  out of order");
            f.Add($"  {Ansi.Fg(Palette.Dim, (sh.Index + 1).ToString())}  " + Ansi.PadRight(Ansi.Fg(color, g is null ? "(untagged shelf)" : $"{g.Label}"), 16)
                  + Ansi.Fg(color, spines) + Ansi.Fg(Palette.Faint, $" {sh.Tomes.Count}") + status);
        }
        f.Add();
        var pile = lib.Pile();
        var items = lib.PileItems();
        f.Add("  " + Ansi.Fg(Palette.Gold, "The pile: ") + Ansi.Fg(Palette.Text, pile.Count == 0 ? "empty. Every book is home." : $"{pile.Count} book{(pile.Count == 1 ? "" : "s")} waiting")
              + Ansi.Fg(Palette.Dim, $"   ({lib.TotalShelved} of {lib.TotalArrived} shelved)"));
        foreach (var t in pile.Take(3)) f.Add("     " + Ansi.Fg(Palette.Dim, "· " + t.Title));
        foreach (var i in items) f.Add("     " + Ansi.Fg(Palette.Lamp, "· and something that isn't a book: " + S.World.Items.Name(i)));
        var out_ = lib.CheckedOut();
        if (out_.Count > 0) f.Add("  " + Ansi.Fg(Palette.Gold, "Checked out: ") + Ansi.Fg(Palette.Text, string.Join(", ", out_.Select(t => t.Title))));
    }

    public override IEnumerable<MenuItem> Menu()
    {
        var lib = S.Library;
        yield return new MenuItem("t", "Tag a shelf", TagShelf, lib.Tagged);
        yield return new MenuItem("p", "Take a book from the pile", TakeBook, lib.Pile().Count > 0);
        yield return new MenuItem("b", "Browse a shelf", Browse);
        yield return new MenuItem("o", "Something odd in the pile", OddThing, lib.PileItems().Count > 0);
        yield return new MenuItem("y", "Try an object on a shelf", TryObject, S.World.Items.Satchel.Any(Objectish));
        yield return new MenuItem("r", "Return a checked-out book", ReturnBook, lib.CheckedOut().Count > 0);
        foreach (var m in Common()) yield return m;
    }

    internal static bool Objectish(ItemInstance i) => i.Essence is not null || i.RemnantOf is not null;

    private void TagShelf()
    {
        var shelf = A.Pick("Which shelf?", S.Library.Shelves(), s => $"Shelf {s.Index + 1}: {s.Label} ({s.Tomes.Count} books)");
        if (shelf is null) return;
        var tags = C.Config.Library.Tags.Select(t => C.Genres[t]).ToList();
        var tag = A.Pick("Hang which genre tag?", tags, g => $"{g.Label} — {g.Sigil}", g => S.Library.Shelves().FirstOrDefault(s => s.Genre == g.Id) is { } on ? $"(currently on shelf {on.Index + 1})" : null);
        if (tag is null) return;
        A.Say(S.Library.TagShelf(shelf.Index, tag.Id));
    }

    private void TakeBook()
    {
        var tome = A.Pick("The pile", S.Library.Pile(), t => $"{t.Title}", t => $"{t.Author}, SR {t.Year} · {t.Cover}");
        if (tome is null) return;
        BookCard(tome);
    }

    private void BookCard(TomeResource tome)
    {
        while (true)
        {
            var f = new Frame(A.Width);
            f.Add(); f.Add();
            f.Add("     " + Ansi.BoldFg(Palette.Lamp, tome.Title));
            f.Add("     " + Ansi.Fg(Palette.Dim, $"{tome.Author} · SR {tome.Year}"));
            f.Add();
            f.Text($"Cover: {tome.Cover}.", Palette.Stone, "     ");
            if (tome.Blurb.Length > 0) f.Text(tome.Blurb, Palette.Text, "     ");
            f.Add();
            f.Add("     " + Ansi.BoldFg(Palette.Moss, "[S]") + " Shelve it    " + Ansi.BoldFg(Palette.Moss, "[R]") + " Read it    " + Ansi.BoldFg(Palette.Moss, "[X]") + " Put it back on the pile");
            Screen.Draw(f);
            var k = A.In.Read();
            if (k.Is('s')) { if (ShelveFlow(tome)) return; }
            else if (k.Is('r')) A.Run(new ReaderView(tome.Id));
            else if (k.Is('x') || k.Kind == KeyKind.Escape) return;
        }
    }

    /// <summary>Choose a shelf, then a slot along it. Returns true once the book is shelved.</summary>
    private bool ShelveFlow(TomeResource tome)
    {
        var shelf = A.Pick($"Shelve “{tome.Title}” (SR {tome.Year}) on which shelf?", S.Library.Shelves(), s => $"Shelf {s.Index + 1}: {s.Label} ({s.Tomes.Count} books)");
        if (shelf is null) return false;
        int? position = null;
        if (shelf.Genre == tome.Genre && shelf.Tomes.Count > 0)
        {
            var slots = Enumerable.Range(0, shelf.Tomes.Count + 1).Select(i => new Slot(i,
                i == 0 ? $"nearest the lamp, before “{Short(shelf.Tomes[0])}” ({shelf.Tomes[0].Year})"
                : i == shelf.Tomes.Count ? $"at the far end, after “{Short(shelf.Tomes[i - 1])}” ({shelf.Tomes[i - 1].Year})"
                : $"between {shelf.Tomes[i - 1].Year} and {shelf.Tomes[i].Year} (“{Short(shelf.Tomes[i - 1])}” | “{Short(shelf.Tomes[i])}”)")).ToList();
            var slot = A.Pick($"Where along the shelf? (this one is SR {tome.Year})", slots, s => s.Label);
            if (slot is null) return false;
            position = slot.Index;
        }
        var r = S.Library.Shelve(tome.Id, shelf.Index, position);
        A.Say(r);
        return r.Ok;
    }

    private sealed record Slot(int Index, string Label);

    private static string Short(TomeResource t)
    {
        var colon = t.Title.IndexOf(':');
        var s = colon > 0 ? t.Title[..colon] : t.Title;
        return s.Length > 34 ? s[..33] + "…" : s;
    }

    private void Browse()
    {
        var shelf = A.Pick("Which shelf?", S.Library.Shelves(), s => $"Shelf {s.Index + 1}: {s.Label} ({s.Tomes.Count} books)");
        if (shelf is not null) A.Run(new ShelfScreen(shelf.Index));
    }

    private void OddThing()
    {
        var item = A.Pick("Something odd in the pile", S.Library.PileItems(), i => S.World.Items.Name(i), i => S.World.Items.Description(i));
        if (item is null) return;
        if (A.Confirm(S.World.Items.Name(item), S.World.Items.Description(item) + "\n\nTry it on a shelf first?", "Try a shelf", "Just take it"))
        {
            var shelf = A.Pick("Which shelf?", S.Library.Shelves(), s => $"Shelf {s.Index + 1}: {s.Label}");
            if (shelf is not null) A.Say(S.Library.ShelveObject(item.Id, shelf.Index));
        }
        A.Say(S.Library.TakeFromPile(item.Id));
    }

    private void TryObject()
    {
        var item = A.Pick("Try which object on a shelf?", S.World.Items.Satchel.Where(Objectish).ToList(), i => S.World.Items.Name(i));
        if (item is null) return;
        var shelf = A.Pick("Which shelf?", S.Library.Shelves(), s => $"Shelf {s.Index + 1}: {s.Label}");
        if (shelf is not null) A.Say(S.Library.ShelveObject(item.Id, shelf.Index));
    }

    private void ReturnBook()
    {
        var t = A.Pick("Return which book?", S.Library.CheckedOut(), t => t.Title);
        if (t is not null) A.Say(S.Library.Return(t.Id));
    }
}

public sealed class ShelfScreen : View
{
    private readonly int _shelf;
    public ShelfScreen(int shelf) => _shelf = shelf;
    private Atrium.Core.Locales.ShelfView Shelf => S.Library.Shelves()[_shelf];

    public override string Title => $"Shelf {_shelf + 1}: {Shelf.Label}";
    public override string Locale => "library";

    public override void Render(Frame f)
    {
        var sh = Shelf;
        if (sh.Tomes.Count == 0) { f.Text("Bare wood and a little dust.", Palette.Dim); return; }
        var lamp = Ansi.Fg(Palette.Lamp, "☼ lamp");
        f.Add("  " + lamp);
        var outOn = S.Library.CheckedOut().Select(t => t.Id).ToHashSet();
        for (int i = 0; i < sh.Tomes.Count; i++)
        {
            var t = sh.Tomes[i];
            var mark = outOn.Contains(t.Id) ? Ansi.Fg(Palette.Faint, " (on loan to you)") : "";
            f.Add($"  {Ansi.Fg(Palette.Dim, $"{i + 1,2}.")} {Ansi.Fg(Palette.Gold, t.Year.ToString())}  {Ansi.Fg(Palette.Text, t.Title)}{mark}");
        }
        f.Add();
        f.Add("  " + (sh.Settled ? Ansi.Fg(Palette.Lamp, "♪ The shelf hums, settled.") : sh.InOrder ? Ansi.Fg(Palette.Dim, "In order, oldest nearest the lamp.") : Ansi.Fg(Palette.Rose, "Something about the run of years looks off.")));
    }

    public override IEnumerable<MenuItem> Menu()
    {
        var any = Shelf.Tomes.Count > 0;
        yield return new MenuItem("r", "Read a book", Read, any);
        yield return new MenuItem("m", "Move a book along the shelf", Move, Shelf.Tomes.Count > 1);
        yield return new MenuItem("t", "Take a book down", TakeDown, any);
        yield return new MenuItem("c", "Check a book out", CheckOut, any);
        yield return new MenuItem("x", "Back", Close);
    }

    private TomeResource? PickBook(string title) =>
        A.Pick(title, Shelf.Tomes, t => $"{t.Year}  {t.Title}");

    private void Read() { if (PickBook("Read which?") is { } t) A.Run(new ReaderView(t.Id)); }

    private void Move()
    {
        var t = PickBook("Move which?");
        if (t is null) return;
        var from = Shelf.Tomes.ToList().FindIndex(x => x.Id == t.Id);
        var others = Shelf.Tomes.Where(x => x.Id != t.Id).ToList();
        var slots = Enumerable.Range(0, others.Count + 1).Select(i => (i, i == 0 ? $"nearest the lamp (before {others[0].Year})" : i == others.Count ? $"at the far end (after {others[^1].Year})" : $"between {others[i - 1].Year} and {others[i].Year}")).Select(x => new SlotChoice(x.i, x.Item2)).ToList();
        var to = A.Pick($"Move “{t.Title}” (SR {t.Year}) to where?", slots, s => s.Label);
        if (to is not null) A.Say(S.Library.Reorder(_shelf, from, to.Index));
    }

    private sealed record SlotChoice(int Index, string Label);

    private void TakeDown()
    {
        var t = PickBook("Take down which?");
        if (t is not null) A.Say(S.Library.TakeDown(_shelf, Shelf.Tomes.ToList().FindIndex(x => x.Id == t.Id)));
    }

    private void CheckOut()
    {
        var t = PickBook("Check out which?");
        if (t is not null) A.Say(S.Library.CheckOut(t.Id));
    }
}

/// <summary>The tome reader. Also where objects are held against pages (the Library's cross-reference lens).</summary>
public sealed class ReaderView : View
{
    private readonly string _tome;
    private int _page;
    private TomeView? _view;

    public ReaderView(string tomeId) => _tome = tomeId;

    public override string Title => _view?.Tome.Title ?? "A book";
    public override string Locale => "library";

    public override void OnEnter() => _view = S.Library.Open(_tome);

    public override void Render(Frame f)
    {
        _view = S.Library.Open(_tome, record: false);
        if (_view is null) { f.Text("You can't find it.", Palette.Dim); return; }
        var t = _view.Tome;
        f.Add("  " + Ansi.Fg(Palette.Dim, $"{t.Author} · SR {t.Year} · {_view.Where}"));
        if (_view.Pages.Count == 0) { f.Text("There's nothing in it.", Palette.Dim); return; }
        _page = Math.Clamp(_page, 0, _view.Pages.Count - 1);
        var p = _view.Pages[_page];
        f.Add();
        f.Add("  " + Ansi.Fg(Palette.Gold, $"Page {p.Number} of {_view.Pages.Count}") + Ansi.Fg(Palette.Faint, $"  ({p.Kind}{(p.Faded ? ", faded ink" : "")})")
              + (p.Title is null ? "" : "  " + Ansi.BoldFg(Palette.Lamp, p.Title)));
        f.Add();
        if (p.Text is null)
            f.Text("The ink on this page has faded almost to nothing. You can just make out that something was written here, in a small careful hand, but not what. Perhaps by a different light…", Palette.Faint, "    ");
        else if (p.Verbatim) f.Verbatim(p.Text, p.Faded ? Palette.Lamp : Palette.Cream);
        else f.Text(p.Text, p.Faded ? Palette.Lamp : Palette.Cream, "    ");
        if (p.Note is not null)
        {
            f.Add();
            f.Text("✎ Your note: " + p.Note, Palette.Gold, "    ");
        }
    }

    public override IEnumerable<MenuItem> Menu()
    {
        var count = _view?.Pages.Count ?? 0;
        yield return new MenuItem(",", "Previous page", () => _page--, _page > 0);
        yield return new MenuItem(".", "Next page", () => _page++, _page < count - 1);
        yield return new MenuItem("h", "Hold an object against this page", Hold, S.World.Items.Satchel.Any(LibraryView.Objectish));
        yield return new MenuItem("n", "Write a note in the margin", Note, count > 0);
        yield return new MenuItem("c", "Check this book out", () => A.Say(S.Library.CheckOut(_tome)), !S.Library.CheckedOut().Any(t => t.Id == _tome));
        yield return new MenuItem("x", "Close the book", Close);
    }

    private void Hold()
    {
        var p = _view!.Pages[_page];
        var item = A.Pick("Hold which object against the page?", S.World.Items.Satchel.Where(LibraryView.Objectish).ToList(), i => S.World.Items.Name(i));
        if (item is not null) A.Say(S.Library.Compare(item.Id, _tome, p.Number));
    }

    private void Note()
    {
        var p = _view!.Pages[_page];
        var text = A.Prompt("A note in the margin", "Write a short note for this page (it will also appear in your journal). Leave it empty to rub out an old note.");
        A.Say(S.Library.Annotate(_tome, p.Number, text));
    }
}
