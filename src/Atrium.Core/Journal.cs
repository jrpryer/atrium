using Atrium.Core.Content;
using Atrium.Core.Runtime;

namespace Atrium.Core;

public sealed record JournalReading(string Locale, string Text);

public sealed record JournalObject(
    MysteryObjectResource Object,
    bool Resolved,
    string Where,
    IReadOnlyList<JournalReading> Readings,
    IReadOnlyList<string> NearMisses,
    IReadOnlyList<string> Considered,
    IReadOnlyList<string> Touched,
    string? Designation);

public sealed record JournalNote(string Tome, int Page, string Text);

/// <summary>Read model for the Player Journal (kept at the Sanctum, readable anywhere). Nothing written here is ever lost.</summary>
public sealed class Journal
{
    private readonly GameSession _s;
    public Journal(GameSession s) => _s = s;

    public IReadOnlyList<JournalObject> Objects()
    {
        var c = _s.Content;
        var ev = _s.State.Evidence;
        var list = new List<JournalObject>();
        foreach (var o in _s.Sanctum.Objects())
        {
            var carrier = _s.World.Items.Carrier(o.Id);
            var where = carrier is null ? "nowhere whole" : $"{_s.World.Items.Name(carrier)}, {_s.World.Items.DescribeLocation(carrier.Location)}";
            var readings = _s.Ledger.SurfacedFor(o.Id).Select(r => new JournalReading(c.LocaleName(r.Locale), r.Text)).ToList();
            var misses = o.Claims.Where(cl => ev.NearMissesGiven.Contains(cl.Id)).Select(cl => $"{Phrase(o, cl.Designation)} — {cl.NearMiss}").ToList();
            var considered = ev.Attempts.Where(a => a.Object == o.Id && a.Outcome is "Invalid" or "NotYetComprehensible")
                .Select(a => Phrase(o, a.Designation)).Distinct().ToList();
            var touched = _s.Essence.Record(o.Id)?.Catalyzed.ToList() ?? new List<string>();
            var trueClaim = o.Claims.FirstOrDefault(cl => cl.Kind == ClaimKind.True);
            var designation = ev.Resolved.Contains(o.Id) && trueClaim is not null ? Phrase(o, trueClaim.Designation) : null;
            list.Add(new JournalObject(o, ev.Resolved.Contains(o.Id), where, readings, misses, considered, touched, designation));
        }
        return list;
    }

    private string Phrase(MysteryObjectResource o, IReadOnlyDictionary<string, string> d) =>
        string.Join(" · ", _s.Content.FacetsFor(o).Select(f => d.TryGetValue(f.Id, out var t) && t.Length > 0 ? _s.Content.TermLabel(t) : "—"));

    public IReadOnlyList<(FacetResource Facet, IReadOnlyList<TermResource> Terms)> Lexicon() =>
        _s.Content.Taxonomy.Facets.Select(f => (f, (IReadOnlyList<TermResource>)_s.State.Evidence.EarnedTerms
            .Select(t => _s.Content.Terms[t]).Where(t => t.Facet == f.Id).OrderBy(t => t.Label).ToList())).ToList();

    public IReadOnlyList<StoryBeatResource> Story() =>
        _s.State.FiredBeats.Where(_s.Content.Beats.ContainsKey).Select(b => _s.Content.Beats[b]).ToList();

    public IReadOnlyList<JournalNote> Notes() =>
        _s.State.Library.Notes.Select(kv =>
        {
            var hash = kv.Key.IndexOf('#');
            var tome = kv.Key[..hash];
            return new JournalNote(_s.Content.Tomes.TryGetValue(tome, out var t) ? t.Title : tome, int.Parse(kv.Key[(hash + 1)..]), kv.Value);
        }).ToList();

    public IReadOnlyList<LogEntry> Log() => _s.State.Log;
}
