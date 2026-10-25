using Atrium.Core.Content;
using Atrium.Core.Mystery;
using Atrium.Core.Runtime;

namespace Atrium.Core.Locales;

public sealed record ShelfView(int Index, string Id, string? Genre, string Label, IReadOnlyList<TomeResource> Tomes, bool InOrder, bool Settled, int Capacity);

public sealed record PageView(int Number, string Kind, string? Title, string? Text, bool Faded, bool Verbatim, string? Note);

public sealed record TomeView(TomeResource Tome, IReadOnlyList<PageView> Pages, string Where);

/// <summary>
/// The Library minigame: tag shelves with genres, shelve the pile in order, read and annotate tomes, check one
/// out. As a lens it answers classification and cross-reference questions (shelving an object, holding an
/// object against a page). The shelving model, genre list, shelf count and page count are all data.
/// </summary>
public sealed class LibraryLocale : LocaleBase
{
    public override string Id => "library";
    private LibraryConfig Cfg => W.Config.Library;
    private LibraryState S => W.State.Library;

    public LibraryLocale(World world, ILensService lens) : base(world, lens)
    {
        W.Bus.Subscribe<WorldChanged>(_ => RefreshArrivals());
        W.Bus.Subscribe<TomesDelivered>(e => { foreach (var t in e.Tomes) Arrive(t, announce: true); });
        W.Bus.Subscribe<PagesDelivered>(e => DeliverPages(e.Pages));
    }

    public void InitializeNewGame()
    {
        S.Shelves.Clear();
        if (Cfg.ShelvingModel == "prelabeled")
            foreach (var sh in Cfg.Shelves) S.Shelves.Add(new ShelfState { Id = sh.Id, Genre = sh.Genre });
        else
            for (int i = 0; i < Cfg.ShelfCount; i++) S.Shelves.Add(new ShelfState { Id = $"shelf-{i + 1}" });

        // The opening pile, in a seeded jumble.
        var start = C.Tomes.Values.Where(t => W.Conditions.Holds(t.Available)).OrderBy(t => t.Id, StringComparer.Ordinal).ToList();
        for (int i = start.Count - 1; i > 0; i--) { var j = W.Rng.Next(i + 1); (start[i], start[j]) = (start[j], start[i]); }
        foreach (var t in start) { S.ArrivedTomes.Add(t.Id); S.Pile.Add(t.Id); }
    }

    // ───── queries ─────

    public IReadOnlyList<ShelfView> Shelves() => S.Shelves.Select((sh, i) => View(sh, i)).ToList();

    private ShelfView View(ShelfState sh, int i)
    {
        var tomes = sh.Tomes.Select(t => C.Tomes[t]).ToList();
        var label = sh.Genre is null ? "an empty, untagged shelf"
            : C.Genres.TryGetValue(sh.Genre, out var g) ? g.Label : sh.Genre;
        var cfgShelf = Cfg.Shelves.FirstOrDefault(s => s.Id == sh.Id);
        if (cfgShelf is not null && cfgShelf.Label.Length > 0) label = cfgShelf.Label;
        return new ShelfView(i, sh.Id, sh.Genre, label, tomes, InOrder(tomes), S.SettledShelves.Contains(sh.Id), Cfg.ShelfCapacity);
    }

    public IReadOnlyList<TomeResource> Pile() => S.Pile.Select(t => C.Tomes[t]).ToList();

    /// <summary>Non-tome things lying in the pile (a misfiled leaf…).</summary>
    public IReadOnlyList<ItemInstance> PileItems() => W.Items.At(Locations.LibraryPile).ToList();

    public IReadOnlyList<GenreResource> AvailableTags() =>
        Cfg.ShelvingModel == "tagged"
            ? Cfg.Tags.Where(t => S.Shelves.All(s => s.Genre != t)).Select(t => C.Genres[t]).ToList()
            : Array.Empty<GenreResource>();

    public bool Tagged => Cfg.ShelvingModel == "tagged";

    public IReadOnlyList<TomeResource> CheckedOut() => S.CheckedOut.Select(t => C.Tomes[t]).ToList();

    public int TotalArrived => S.ArrivedTomes.Count;
    public int TotalShelved => S.Shelves.Sum(s => s.Tomes.Count);

    public string? ShelfOf(string tomeId)
    {
        var i = S.Shelves.FindIndex(s => s.Tomes.Contains(tomeId));
        return i < 0 ? null : View(S.Shelves[i], i).Label;
    }

    private bool InOrder(IReadOnlyList<TomeResource> tomes)
    {
        for (int i = 1; i < tomes.Count; i++) if (Compare(tomes[i - 1], tomes[i]) > 0) return false;
        return true;
    }

    private int Compare(TomeResource a, TomeResource b) => Cfg.OrderBy switch
    {
        "year" => a.Year != b.Year ? a.Year.CompareTo(b.Year) : 0,
        "title" => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase),
        "author" => string.Compare(a.Author, b.Author, StringComparison.OrdinalIgnoreCase),
        _ => 0,
    };

    public bool PageVisible(TomeResource t, PageResource p) => !p.Loose || S.DeliveredPages.Contains($"{t.Id}#{p.Number}");

    public bool PageReadable(TomeResource t, PageResource p) =>
        PageVisible(t, p) && (!p.Faded || S.RevealedPages.Contains($"{t.Id}#{p.Number}") || W.Conditions.Holds(Cfg.FadedRevealWhen));

    // ───── shelving ─────

    public ActionResult TagShelf(int shelf, string genre)
    {
        if (!Tagged) return ActionResult.Soft("These shelves came labelled; there's nothing to tag.");
        if (shelf < 0 || shelf >= S.Shelves.Count) return ActionResult.Soft("There's no such shelf.");
        if (!Cfg.Tags.Contains(genre)) return ActionResult.Soft("You don't have that tag.");
        var r = new ActionResult();
        var other = S.Shelves.FirstOrDefault(s => s.Genre == genre);
        if (other is not null && other != S.Shelves[shelf])
        {
            ReturnToPile(other);
            other.Genre = null;
            r.Add(LineKind.Narration, "You unhook the tag from the other shelf; its books go back on the pile.");
        }
        var sh = S.Shelves[shelf];
        if (sh.Genre is not null && sh.Genre != genre)
        {
            ReturnToPile(sh);
            r.Add(LineKind.Narration, "The books already on this shelf go back to the pile.");
        }
        sh.Genre = genre;
        var g = C.Genres[genre];
        r.Add(LineKind.Narration, $"You hang the {g.Label} tag — {g.Sigil} — and the shelf lights up {g.Color}.");
        W.Count("library.tagged");
        return Done(r);
    }

    private void ReturnToPile(ShelfState sh)
    {
        foreach (var t in sh.Tomes) S.Pile.Insert(0, t);
        sh.Tomes.Clear();
        S.SettledShelves.Remove(sh.Id);
    }

    /// <summary>Puts a tome from the pile onto a shelf at a position (0 = nearest the lamp; null = the end).</summary>
    public ActionResult Shelve(string tomeId, int shelf, int? position = null)
    {
        if (!S.Pile.Contains(tomeId)) return ActionResult.Soft("That book isn't on the pile.");
        if (shelf < 0 || shelf >= S.Shelves.Count) return ActionResult.Soft("There's no such shelf.");
        var sh = S.Shelves[shelf];
        var tome = C.Tomes[tomeId];
        if (sh.Genre is null) return ActionResult.Soft("This shelf has no tag yet. Hang a genre tag on it first.");
        if (sh.Tomes.Count >= Cfg.ShelfCapacity) return ActionResult.Soft("The shelf is full to the brackets.");
        if (sh.Genre != tome.Genre)
        {
            W.Count("library.misshelved");
            var g = C.Genres.TryGetValue(sh.Genre, out var gg) ? gg.Label : sh.Genre;
            var r0 = ActionResult.Soft($"The {g} shelf won't take it: the book slides gently back into your hands.");
            if (!string.IsNullOrWhiteSpace(tome.Hint)) r0.Add(LineKind.Narration, tome.Hint!);
            return Done(r0);
        }

        S.Pile.Remove(tomeId);
        var pos = Math.Clamp(position ?? sh.Tomes.Count, 0, sh.Tomes.Count);
        sh.Tomes.Insert(pos, tomeId);
        var r = new ActionResult().Add(LineKind.Narration, $"“{tome.Title}” slides home, and the shelf pulses with light.");
        if (S.EverShelved.Add(tomeId)) W.State.Counters["library.shelved"] = S.EverShelved.Count;

        var view = View(sh, shelf);
        if (!view.InOrder)
            r.Add(LineKind.Soft, OrderNote());
        CheckSettled(sh, shelf, r);
        return Done(r);
    }

    private string OrderNote() => Cfg.OrderBy switch
    {
        "year" => "Something about the run of spines looks off. The Quires keep the oldest nearest the lamp.",
        "title" => "The run of titles isn't quite in order.",
        "author" => "The run of authors isn't quite in order.",
        _ => "",
    };

    private void CheckSettled(ShelfState sh, int index, ActionResult r)
    {
        var genreTomes = S.ArrivedTomes.Where(t => C.Tomes[t].Genre == sh.Genre).ToHashSet();
        var view = View(sh, index);
        if (genreTomes.SetEquals(sh.Tomes) && view.InOrder && !S.CheckedOut.Any(sh.Tomes.Contains))
        {
            if (S.SettledShelves.Add(sh.Id))
            {
                W.Count("library.settled");
                r.Add(LineKind.Discovery, $"The {view.Label} shelf gives a low, contented hum. Every book where it belongs.");
                W.Log.Add(LogKind.Library, $"Settled the {view.Label} shelf.", locale: Id);
            }
        }
        else S.SettledShelves.Remove(sh.Id);
    }

    /// <summary>Moves a shelved tome to a new position on its shelf.</summary>
    public ActionResult Reorder(int shelf, int from, int to)
    {
        if (shelf < 0 || shelf >= S.Shelves.Count) return ActionResult.Soft("There's no such shelf.");
        var sh = S.Shelves[shelf];
        if (from < 0 || from >= sh.Tomes.Count) return ActionResult.Soft("There's no book there.");
        var t = sh.Tomes[from];
        sh.Tomes.RemoveAt(from);
        sh.Tomes.Insert(Math.Clamp(to, 0, sh.Tomes.Count), t);
        var r = ActionResult.Say($"You ease “{C.Tomes[t].Title}” along the shelf.");
        if (!View(sh, shelf).InOrder) r.Add(LineKind.Soft, OrderNote());
        CheckSettled(sh, shelf, r);
        return Done(r);
    }

    public ActionResult TakeDown(int shelf, int position)
    {
        if (shelf < 0 || shelf >= S.Shelves.Count) return ActionResult.Soft("There's no such shelf.");
        var sh = S.Shelves[shelf];
        if (position < 0 || position >= sh.Tomes.Count) return ActionResult.Soft("There's no book there.");
        var t = sh.Tomes[position];
        sh.Tomes.RemoveAt(position);
        S.Pile.Insert(0, t);
        S.SettledShelves.Remove(sh.Id);
        return Done(ActionResult.Say($"You lift down “{C.Tomes[t].Title}” and set it on top of the pile."));
    }

    // ───── reading ─────

    /// <summary>Opens a tome. With <paramref name="record"/> false it only looks (for redraws), changing nothing.</summary>
    public TomeView? Open(string tomeId, bool record = true)
    {
        if (!S.ArrivedTomes.Contains(tomeId)) return null;
        var t = C.Tomes[tomeId];
        var pages = new List<PageView>();
        foreach (var p in t.Pages.Where(p => PageVisible(t, p)))
        {
            var key = $"{t.Id}#{p.Number}";
            var readable = PageReadable(t, p);
            if (readable && record)
            {
                if (S.ReadPages.Add(key) && p.Faded) W.Log.Add(LogKind.Library, $"Read faded ink in “{t.Title}”, page {p.Number}.", locale: Id);
                if (p.Faded) S.RevealedPages.Add(key);
            }
            pages.Add(new PageView(p.Number, p.Kind, p.Title, readable ? p.Text : null, p.Faded, p.Verbatim, S.Notes.GetValueOrDefault(key)));
        }
        if (record)
        {
            var key0 = "library.read:" + t.Id;
            if (W.Counter(key0) == 0) W.State.Counters["library.read"] = W.Counter("library.read") + 1;
            W.Count(key0);
            W.Changed();
        }
        return new TomeView(t, pages, Where(tomeId));
    }

    private string Where(string tomeId)
    {
        if (S.CheckedOut.Contains(tomeId)) return "checked out, in your satchel";
        if (S.Pile.Contains(tomeId)) return "on the pile";
        return ShelfOf(tomeId) is { } s ? $"on the {s} shelf" : "somewhere in the Library";
    }

    public ActionResult Annotate(string tomeId, int page, string note)
    {
        var t = C.Tomes.GetValueOrDefault(tomeId);
        if (t is null || !S.ArrivedTomes.Contains(tomeId)) return ActionResult.Soft("You can't find that book.");
        if (t.Pages.All(p => p.Number != page)) return ActionResult.Soft("There's no such page.");
        var key = $"{tomeId}#{page}";
        if (string.IsNullOrWhiteSpace(note)) { S.Notes.Remove(key); return Done(ActionResult.Say("You rub out your note.")); }
        S.Notes[key] = note.Trim();
        W.Log.Add(LogKind.Library, $"Note in “{t.Title}”, p.{page}: {note.Trim()}", locale: Id);
        return Done(ActionResult.Say("You pencil a note in the margin. It'll be in your journal too."));
    }

    public ActionResult CheckOut(string tomeId)
    {
        if (!S.ArrivedTomes.Contains(tomeId)) return ActionResult.Soft("You can't find that book.");
        if (S.CheckedOut.Contains(tomeId)) return ActionResult.Soft("You already have it.");
        if (S.Pile.Contains(tomeId)) return ActionResult.Soft("Ada won't lend a book that hasn't been shelved. “Order first, Keeper.”");
        if (S.CheckedOut.Count >= Cfg.CheckoutLimit)
            return ActionResult.Soft($"You can carry {Cfg.CheckoutLimit} book{(Cfg.CheckoutLimit == 1 ? "" : "s")} at a time. Return one first.");
        S.CheckedOut.Add(tomeId);
        foreach (var sh in S.Shelves.Where(s => s.Tomes.Contains(tomeId))) S.SettledShelves.Remove(sh.Id);
        return Done(ActionResult.Say($"Mab stamps the card with a flourish. “{C.Tomes[tomeId].Title}” goes in your satchel."));
    }

    public ActionResult Return(string tomeId)
    {
        if (!S.CheckedOut.Remove(tomeId)) return ActionResult.Soft("You don't have that book.");
        var r = ActionResult.Say("You slip the book back into its gap on the shelf.");
        for (int i = 0; i < S.Shelves.Count; i++) if (S.Shelves[i].Tomes.Contains(tomeId)) CheckSettled(S.Shelves[i], i, r);
        return Done(r);
    }

    // ───── lens: clue-objects through Library mechanics ─────

    /// <summary>Tries to shelve a carried object. It never takes (objects aren't books), but the shelf's response is evidence.</summary>
    public ActionResult ShelveObject(string instanceId, int shelf)
    {
        var item = Carried(instanceId) ?? W.Items.At(Locations.LibraryPile).FirstOrDefault(i => i.Id == instanceId);
        if (item is null) return ActionResult.Soft("You don't have that with you.");
        if (shelf < 0 || shelf >= S.Shelves.Count) return ActionResult.Soft("There's no such shelf.");
        var genre = S.Shelves[shelf].Genre;
        if (genre is null) return ActionResult.Soft("This shelf has no tag yet; it has nothing to say about anything.");
        var r = new ActionResult();
        Observe(r, new LensEvent { Locale = Id, Verb = "shelve", Item = item, Shelf = genre });
        return Done(r);
    }

    /// <summary>Holds a carried object against a page of a tome (cross-reference).</summary>
    public ActionResult Compare(string instanceId, string tomeId, int page)
    {
        var item = Carried(instanceId);
        if (item is null) return ActionResult.Soft("You don't have that with you.");
        if (!C.Tomes.TryGetValue(tomeId, out var t) || !S.ArrivedTomes.Contains(tomeId)) return ActionResult.Soft("You can't find that book.");
        var p = t.Pages.FirstOrDefault(x => x.Number == page);
        if (p is null || !PageVisible(t, p)) return ActionResult.Soft("There's no such page.");
        if (!PageReadable(t, p)) return ActionResult.Soft("The ink on this page is too faint to read by ordinary light.");
        var r = new ActionResult();
        Observe(r, new LensEvent { Locale = Id, Verb = "compare", Item = item, Tome = tomeId, Page = page });
        return Done(r);
    }

    public ActionResult TakeFromPile(string instanceId)
    {
        var item = W.Items.At(Locations.LibraryPile).FirstOrDefault(i => i.Id == instanceId);
        if (item is null) return ActionResult.Soft("It isn't on the pile.");
        W.Items.Move(item, Locations.Satchel);
        return Done(ActionResult.Say($"You tuck {W.Items.Name(item)} into your satchel."));
    }

    // ───── arrivals ─────

    private void RefreshArrivals()
    {
        foreach (var t in C.Tomes.Values.OrderBy(t => t.Id, StringComparer.Ordinal))
            if (!S.ArrivedTomes.Contains(t.Id) && W.Conditions.Holds(t.Available)) Arrive(t.Id, announce: true);
    }

    private void Arrive(string tomeId, bool announce)
    {
        if (!S.ArrivedTomes.Add(tomeId)) return;
        S.Pile.Insert(0, tomeId);
        var t = C.Tomes[tomeId];
        foreach (var sh in S.Shelves.Where(s => s.Genre == t.Genre)) S.SettledShelves.Remove(sh.Id);
        if (announce)
        {
            W.Notify(NoticeKind.Info, "New for the Library", $"“{t.Title}” has joined the pile, waiting to be shelved.");
            W.Log.Add(LogKind.Library, $"“{t.Title}” arrived at the Library.", locale: Id);
        }
    }

    private void DeliverPages(IReadOnlyList<string> pages)
    {
        foreach (var key in pages)
        {
            if (!S.DeliveredPages.Add(key)) continue;
            var hash = key.IndexOf('#');
            var tomeId = key[..hash];
            if (!C.Tomes.TryGetValue(tomeId, out var t)) continue;
            Arrive(tomeId, announce: false);
            foreach (var sh in S.Shelves.Where(s => s.Tomes.Contains(tomeId)))
            {
                sh.Tomes.Remove(tomeId);
                S.SettledShelves.Remove(sh.Id);
                S.Pile.Insert(0, tomeId);
            }
            W.Notify(NoticeKind.Info, "A recovered page", $"The librarians have tucked a recovered page into “{t.Title}”. It's back on the pile for re-shelving.");
        }
    }
}
