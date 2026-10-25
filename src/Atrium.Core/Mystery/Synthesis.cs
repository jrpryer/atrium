using Atrium.Core.Content;
using Atrium.Core.Runtime;

namespace Atrium.Core.Mystery;

public enum SynthesisKind { Resolved, NearMiss, AlreadyConsidered, NotYetComprehensible, Invalid, AlreadyResolved }

public sealed record SynthesisOutcome(SynthesisKind Kind, string Text, string? ClaimId = null);

/// <summary>
/// Evaluates a designation against an object's claims: <c>resolved</c>, <c>near_miss(discrepancy)</c> (once per
/// plausible-wrong claim), or <c>not_yet_comprehensible</c>. A wrong answer costs time, never matter.
/// </summary>
public sealed class SynthesisResolver
{
    private readonly World _w;
    private readonly EvidenceLedger _ledger;
    private readonly ComprehensionGate _gate;

    public SynthesisResolver(World w, EvidenceLedger ledger, ComprehensionGate gate) { _w = w; _ledger = ledger; _gate = gate; }

    public SynthesisOutcome Attempt(string objectId, IReadOnlyDictionary<string, string> designation)
    {
        var obj = _w.Content.Objects[objectId];
        var ev = _w.State.Evidence;
        var text = _w.Config.Synthesis;
        if (ev.Resolved.Contains(objectId)) return new SynthesisOutcome(SynthesisKind.AlreadyResolved, text.AlreadyResolved);

        var facets = _w.Content.FacetsFor(obj).Select(f => f.Id).ToList();
        var claim = obj.Claims.FirstOrDefault(c => facets.All(f => designation.TryGetValue(f, out var t) && c.Designation.TryGetValue(f, out var ct) && t == ct));

        SynthesisOutcome outcome;
        if (claim is null)
            outcome = new SynthesisOutcome(SynthesisKind.Invalid, Pick(text.Invalid, objectId));
        else if (!_gate.MayAttempt(obj, claim))
            outcome = new SynthesisOutcome(SynthesisKind.NotYetComprehensible, Pick(text.NotYet, objectId), claim.Id);
        else if (claim.Kind == ClaimKind.True)
            outcome = new SynthesisOutcome(SynthesisKind.Resolved, obj.Resolution.Text, claim.Id);
        else if (ev.NearMissesGiven.Contains(claim.Id))
            outcome = new SynthesisOutcome(SynthesisKind.AlreadyConsidered, text.AlreadyConsidered, claim.Id);
        else
            outcome = new SynthesisOutcome(SynthesisKind.NearMiss, claim.NearMiss, claim.Id);

        ev.Attempts.Add(new AttemptRecord
        {
            Object = objectId,
            Designation = facets.ToDictionary(f => f, f => designation.TryGetValue(f, out var t) ? t : ""),
            Outcome = outcome.Kind.ToString(),
            Claim = outcome.Kind is SynthesisKind.Resolved or SynthesisKind.NearMiss or SynthesisKind.AlreadyConsidered ? claim?.Id : null,
            Tick = _w.Clock.Tick,
        });

        var phrase = string.Join(" · ", facets.Select(f => designation.TryGetValue(f, out var t) ? _w.Content.TermLabel(t) : "—"));
        switch (outcome.Kind)
        {
            case SynthesisKind.Resolved:
                ev.Resolved.Add(objectId);
                _w.Count("mystery.resolved");
                _w.Log.Add(LogKind.Resolved, $"Designated {obj.Name}: {phrase}.", objectId, "sanctum");
                _w.Bus.Publish(new ObjectResolved(objectId));
                break;
            case SynthesisKind.NearMiss:
                ev.NearMissesGiven.Add(claim!.Id);
                _w.Log.Add(LogKind.NearMiss, $"Considered {obj.Name} as {phrase}. {claim.NearMiss}", objectId, "sanctum");
                _w.Bus.Publish(new NearMissGiven(objectId, claim.Id));
                break;
            default:
                _w.Log.Add(LogKind.Attempt, $"Considered {obj.Name} as {phrase}; it didn't hold.", objectId, "sanctum");
                break;
        }
        return outcome;
    }

    private string Pick(List<string> lines, string objectId)
    {
        var n = _w.State.Evidence.Attempts.Count(a => a.Object == objectId);
        return lines[n % lines.Count];
    }
}

/// <summary>Which earned terms the designation card offers (open question 4 — a tunable, not a decision).</summary>
public sealed class Lexicon
{
    private readonly World _w;
    private readonly EvidenceLedger _ledger;
    public Lexicon(World w, EvidenceLedger ledger) { _w = w; _ledger = ledger; }

    public IReadOnlyList<TermResource> TermsFor(MysteryObjectResource obj, string facet)
    {
        var earned = _w.State.Evidence.EarnedTerms;
        IEnumerable<string> pool = _w.Config.LexiconMode switch
        {
            "perObject" => _ledger.SurfacedFor(obj.Id).SelectMany(r => r.Grants),
            "globalFiltered" => earned.Where(t => Relevant(obj).Contains(t)),
            _ => earned,
        };
        return pool.Distinct()
            .Where(t => earned.Contains(t) && _w.Content.Terms.TryGetValue(t, out var tr) && tr.Facet == facet)
            .Select(t => _w.Content.Terms[t])
            .OrderBy(t => t.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private HashSet<string> Relevant(MysteryObjectResource obj) =>
        obj.Claims.SelectMany(c => c.Designation.Values)
            .Concat(obj.Readings.SelectMany(r => r.Grants))
            .Concat(obj.ExtraRelevantTerms)
            .ToHashSet();
}

/// <summary>
/// The set of open (arrived, unresolved) objects. On entering a locale it re-presents them (invariant 10), at a
/// funnel strength set in config (open question 2).
/// </summary>
public sealed class UnresolvedRegistry
{
    private readonly World _w;
    private readonly EvidenceLedger _ledger;
    public UnresolvedRegistry(World w, EvidenceLedger ledger) { _w = w; _ledger = ledger; }

    public IEnumerable<MysteryObjectResource> Open =>
        _w.Content.Objects.Values
            .Where(o => _w.State.ArrivedObjects.Contains(o.Id) && !_ledger.IsResolved(o.Id))
            .OrderBy(o => _w.State.Log.FindIndex(l => l.Kind == LogKind.Arrival && l.Object == o.Id));

    /// <summary>Unsurfaced readings an object could still yield in a locale right now (prerequisites met).</summary>
    public IEnumerable<ReadingResource> Untried(MysteryObjectResource obj, string locale) =>
        obj.Readings.Where(r => r.Locale == locale && !_ledger.Has(obj.Id, r.Id) && _w.Conditions.Holds(r.Requires));

    public IReadOnlyList<Line> Resurface(string locale)
    {
        var lines = new List<Line>();
        var mode = _w.Config.Funnel;
        if (mode == "off") return lines;
        foreach (var obj in Open)
        {
            var carrier = _w.Items.Carrier(obj.Id);
            var where = carrier is null ? "somewhere in pieces" : _w.Items.DescribeLocation(carrier.Location);
            var untried = Untried(obj, locale).ToList();
            if (untried.Count == 0) continue;
            var name = carrier is null ? obj.Name : _w.Items.Name(carrier);
            var line = $"Still unexplained: {name} ({where}). It might have more to show you here.";
            if (mode == "strong" && untried.FirstOrDefault(r => r.FunnelHint.Length > 0) is { } hint) line += " " + hint.FunnelHint;
            lines.Add(new Line(LineKind.System, line));
        }
        return lines;
    }
}
