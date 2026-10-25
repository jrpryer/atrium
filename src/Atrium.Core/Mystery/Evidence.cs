using Atrium.Core.Content;
using Atrium.Core.Runtime;

namespace Atrium.Core.Mystery;

/// <summary>
/// What a locale reports when its mechanics act on an item. Locales emit these without knowing anything about
/// mysteries; the evidence layer decides whether a reading surfaces (readings come through mechanics, never
/// through bespoke inspection menus).
/// </summary>
public sealed class LensEvent
{
    public required string Locale { get; init; }
    public required string Verb { get; init; }
    public required ItemInstance Item { get; init; }
    public string? Tool { get; init; }
    public IReadOnlyCollection<string> With { get; init; } = Array.Empty<string>();
    public string? Shelf { get; init; }
    public string? Tome { get; init; }
    public int? Page { get; init; }
    public string? Pool { get; init; }
    public Energy? Energy { get; init; }
    public string? Seal { get; init; }
    public string? Transform { get; init; }
    public string? Derivation { get; init; }
}

/// <summary>The sanctioned channel from a locale into the evidence layer.</summary>
public interface ILensService
{
    /// <summary>Surfaces any readings this event reveals. Returns new readings and recollections (empty when nothing matched).</summary>
    IReadOnlyList<Line> Observe(LensEvent e);
}

/// <summary>Tracks which readings the player has surfaced per object, and the taxonomy terms they earned.</summary>
public sealed class EvidenceLedger
{
    private readonly World _w;
    public EvidenceLedger(World w) => _w = w;

    private EvidenceState S => _w.State.Evidence;

    public bool Has(string obj, string reading) => S.Surfaced.TryGetValue(obj, out var l) && l.Contains(reading);

    public IReadOnlyList<ReadingResource> SurfacedFor(string obj)
    {
        if (!S.Surfaced.TryGetValue(obj, out var ids)) return Array.Empty<ReadingResource>();
        return ids.Where(_w.Content.Readings.ContainsKey).Select(id => _w.Content.Readings[id].Reading).ToList();
    }

    /// <summary>Distinct locales whose lenses have yielded evidence for this object.</summary>
    public IReadOnlyCollection<string> LocalesFor(string obj) => SurfacedFor(obj).Select(r => r.Locale).Distinct().ToList();

    public bool IsResolved(string obj) => S.Resolved.Contains(obj);

    /// <summary>Records a reading. Returns the lines to show (the reading and any terms earned), or empty if already known.</summary>
    public IReadOnlyList<Line> Surface(string obj, ReadingResource reading)
    {
        if (!S.Surfaced.TryGetValue(obj, out var list)) S.Surfaced[obj] = list = new List<string>();
        if (list.Contains(reading.Id)) return Array.Empty<Line>();
        list.Add(reading.Id);
        var lines = new List<Line> { new(LineKind.NewReading, reading.Text) };
        _w.Log.Add(LogKind.Reading, reading.Text, obj, reading.Locale);
        foreach (var term in reading.Grants)
        {
            if (!S.EarnedTerms.Add(term)) continue;
            S.TermSources[term] = obj;
            var t = _w.Content.Terms[term];
            var facet = _w.Content.Facets.TryGetValue(t.Facet, out var f) ? f.Label : t.Facet;
            lines.Add(new Line(LineKind.Term, $"New term for your journal — {facet}: “{t.Label}”."));
            _w.Bus.Publish(new TermEarned(term));
        }
        _w.Bus.Publish(new ReadingSurfaced(obj, reading.Id, reading.Locale));
        return lines;
    }
}

/// <summary>Matches lens events against designer-authored reading triggers.</summary>
public sealed class LensService : ILensService
{
    private readonly World _w;
    private readonly EvidenceLedger _ledger;

    public LensService(World w, EvidenceLedger ledger) { _w = w; _ledger = ledger; }

    public IReadOnlyList<Line> Observe(LensEvent e)
    {
        // Carriers match readings of their essence; remnants match only readings that name their form explicitly.
        var essence = e.Item.Inert ? null : e.Item.Essence;
        var objId = essence ?? e.Item.RemnantOf;
        if (objId is null || !_w.Content.Objects.TryGetValue(objId, out var obj)) return Array.Empty<Line>();

        var lines = new List<Line>();
        foreach (var r in obj.Readings)
        {
            if (r.Locale != e.Locale) continue;
            if (r.Form is null ? essence is null : r.Form != e.Item.Def) continue;
            if (!Matches(r.Trigger, e)) continue;
            if (!_w.Conditions.Holds(r.Requires)) continue;

            if (_ledger.Has(obj.Id, r.Id)) lines.Add(new Line(LineKind.Recall, r.Text));
            else lines.AddRange(_ledger.Surface(obj.Id, r));
        }
        return lines;
    }

    public static bool Matches(LensTrigger t, LensEvent e)
    {
        if (!string.Equals(t.Verb, e.Verb, StringComparison.Ordinal)) return false;
        if (t.Tool != "*" && !string.Equals(t.Tool, e.Tool, StringComparison.Ordinal)) return false;
        if (!SameMultiset(t.With, e.With)) return false;
        if (t.Shelf is not null && t.Shelf != e.Shelf) return false;
        if (t.Tome is not null && t.Tome != e.Tome) return false;
        if (t.Page is not null && t.Page != e.Page) return false;
        if (t.Pool is not null && t.Pool != e.Pool) return false;
        if (t.Seal is not null && t.Seal != e.Seal) return false;
        if (t.Transform is not null && t.Transform != e.Transform) return false;
        if (t.Derivation is not null && t.Derivation != e.Derivation) return false;
        if (t.Light is IntRange lr && (e.Energy is null || !lr.Contains(e.Energy.Value.Light))) return false;
        if (t.Heat is IntRange hr && (e.Energy is null || !hr.Contains(e.Energy.Value.Heat))) return false;
        return true;
    }

    public static bool SameMultiset(IEnumerable<string> a, IEnumerable<string> b) =>
        a.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(b.OrderBy(x => x, StringComparer.Ordinal));
}

/// <summary>
/// Invariant 4: answers only "may the player attempt this claim yet?" — never whether it is correct.
/// Plausible-wrong claims additionally need evidence from a second locale (invariant 6).
/// </summary>
public sealed class ComprehensionGate
{
    private readonly World _w;
    private readonly EvidenceLedger _ledger;
    public ComprehensionGate(World w, EvidenceLedger ledger) { _w = w; _ledger = ledger; }

    public bool MayAttempt(MysteryObjectResource obj, ClaimResource claim)
    {
        if (!_w.Conditions.Holds(claim.Requires)) return false;
        if (claim.Kind == ClaimKind.PlausibleWrong && _ledger.LocalesFor(obj.Id).Count < _w.Config.NearMissMinLocales) return false;
        return true;
    }

    /// <summary>True when the player's notes would let them commit at least one designation for this object.</summary>
    public bool AnyAttemptable(MysteryObjectResource obj) => obj.Claims.Any(c => MayAttempt(obj, c));
}
