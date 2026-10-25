using Atrium.Core.Content;

namespace Atrium.Core.Runtime;

/// <summary>
/// Synchronous typed signal bus. Locales never reference each other; they communicate through these events,
/// the evidence layer, and the <c>SanctumRouter</c>. A Godot adapter can re-emit any of these as signals.
/// </summary>
public sealed class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public void Subscribe<T>(Action<T> handler)
    {
        if (!_handlers.TryGetValue(typeof(T), out var list)) _handlers[typeof(T)] = list = new List<Delegate>();
        list.Add(handler);
    }

    public void Publish<T>(T evt)
    {
        if (!_handlers.TryGetValue(typeof(T), out var list)) return;
        foreach (var h in list.ToArray()) ((Action<T>)h)(evt);
    }
}

// ───── Events ─────

public sealed record LocaleUnlocked(string Locale);
public sealed record LocaleEntered(string Locale);
public sealed record BeatFired(StoryBeatResource Beat);
public sealed record ObjectArrived(string Object, string InstanceId);
public sealed record ReadingSurfaced(string Object, string Reading, string Locale);
public sealed record TermEarned(string Term);
public sealed record ObjectResolved(string Object);
public sealed record NearMissGiven(string Object, string Claim);
public sealed record TomesDelivered(IReadOnlyList<string> Tomes);
public sealed record PagesDelivered(IReadOnlyList<string> Pages);
public sealed record IngredientsDelivered(IReadOnlyList<string> Ingredients);
public sealed record EssenceMoved(string Essence, string FromForm, string ToForm, string Edge);
public sealed record EssenceNullified(string Essence, string InstanceId);
public sealed record Reconstituted(string Essence, string Form);
public sealed record ToolAssembled(string Tool, string InstanceId);
public sealed record SealSolved(string Seal);
public sealed record LifeformBorn(string Pool, string Species, bool FirstOfKind);
public sealed record DiscoveryMade(string Element);
public sealed record Ticked(long From, long To);
/// <summary>Raised after any player action or clock step; progression re-evaluates on it.</summary>
public sealed record WorldChanged;

public enum NoticeKind { Info, Story, Unlock, Arrival, Discovery, Birth, Essence }

/// <summary>Something the front end should surface to the player outside the current action's result.</summary>
public sealed record Notice(NoticeKind Kind, string Title, string Text);
