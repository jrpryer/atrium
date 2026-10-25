using Atrium.Core.Content;
using Atrium.Core.Essence;
using Atrium.Core.Mystery;
using Atrium.Core.Runtime;

namespace Atrium.Core.Story;

/// <summary>
/// Re-evaluates designer conditions after every change: locale unlocks, story beats, clue-object arrivals.
/// Everything here is data-driven; nothing names a specific locale, beat or object.
/// </summary>
public sealed class ProgressionDirector
{
    private readonly World _w;
    private readonly SanctumRouter _router;
    private readonly EssenceRegistry _essence;
    private readonly EvidenceLedger _ledger;
    private readonly UnresolvedRegistry _unresolved;
    private bool _evaluating;

    /// <summary>False while setting up a new game, so the opening state isn't announced as news.</summary>
    public bool Announce { get; set; } = true;

    public ProgressionDirector(World w, SanctumRouter router, EssenceRegistry essence, EvidenceLedger ledger, UnresolvedRegistry unresolved)
    {
        _w = w; _router = router; _essence = essence; _ledger = ledger; _unresolved = unresolved;
    }

    /// <summary>Runs until stable (one beat can satisfy another).</summary>
    public void Evaluate()
    {
        if (_evaluating) return;
        _evaluating = true;
        try
        {
            for (int pass = 0; pass < 32; pass++)
            {
                bool changed = false;
                changed |= Locales();
                changed |= Arrivals();
                changed |= Beats();
                _w.Bus.Publish(new WorldChanged());
                if (!changed) break;
            }
        }
        finally { _evaluating = false; }
    }

    private bool Locales()
    {
        bool changed = false;
        foreach (var l in _w.Content.LocalesInOrder)
        {
            if (_w.State.UnlockedLocales.Contains(l.Id) || !_w.Conditions.Holds(l.UnlockWhen)) continue;
            _w.State.UnlockedLocales.Add(l.Id);
            changed = true;
            _w.Log.Add(LogKind.Story, $"{l.Name} opened.", locale: l.Id);
            _w.Bus.Publish(new LocaleUnlocked(l.Id));
            if (Announce)
            {
                _w.Notify(NoticeKind.Unlock, l.Name, l.UnlockText);
                var open = _unresolved.Open.ToList();
                if (open.Count > 0)
                    _w.Notify(NoticeKind.Info, "Your open questions",
                        $"A new place means new ways of looking. Still unexplained: {string.Join(", ", open.Select(o => o.Name))}.");
            }
        }
        return changed;
    }

    private bool Arrivals()
    {
        bool changed = false;
        foreach (var o in _w.Content.Objects.Values)
        {
            if (_w.State.ArrivedObjects.Contains(o.Id) || !_w.Conditions.Holds(o.Arrival.When)) continue;
            _w.State.ArrivedObjects.Add(o.Id);
            var inst = _w.Items.Create(o.BaseForm, o.Arrival.Location);
            _essence.Register(o.Id, inst);
            _w.Log.Add(LogKind.Arrival, o.Arrival.Text.Length > 0 ? o.Arrival.Text : $"{o.Name} turned up.", o.Id);
            if (o.Arrival.Text.Length > 0) _w.Notify(NoticeKind.Arrival, o.Name, o.Arrival.Text);
            if (o.Arrival.Reading is not null)
            {
                var reading = o.Readings.First(r => r.Id == o.Arrival.Reading);
                foreach (var line in _ledger.Surface(o.Id, reading).Where(l => l.Kind == LineKind.Term))
                    _w.Notify(NoticeKind.Info, "Journal", line.Text);
            }
            _w.Bus.Publish(new ObjectArrived(o.Id, inst.Id));
            changed = true;
        }
        return changed;
    }

    private bool Beats()
    {
        bool changed = false;
        foreach (var b in _w.Content.BeatsInOrder)
        {
            if (_w.State.FiredBeats.Contains(b.Id) || !_w.Conditions.Holds(b.When)) continue;
            _w.State.FiredBeats.Add(b.Id);
            changed = true;
            _w.Log.Add(LogKind.Story, $"{b.Title}. {b.Text}");
            _w.Notify(NoticeKind.Story, b.Title, b.Text);
            _w.Bus.Publish(new BeatFired(b));
            _router.Apply(b.Payload);
        }
        return changed;
    }
}
