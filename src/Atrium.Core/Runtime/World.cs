using Atrium.Core.Content;

namespace Atrium.Core.Runtime;

/// <summary>
/// The shared services every module may use: content, state, the signal bus, items, counters, conditions,
/// the world-clock and the event log. Modules receive a <see cref="World"/>; they never receive each other.
/// </summary>
public sealed class World
{
    public ContentDatabase Content { get; }
    public GameState State { get; }
    public EventBus Bus { get; } = new();
    public ItemStore Items { get; }
    public Conditions Conditions { get; }
    public WorldClock Clock { get; }
    public EventLog Log { get; }
    public Rng Rng { get; }
    public GameConfig Config => Content.Config;

    private readonly List<Notice> _notices = new();

    public World(ContentDatabase content, GameState state)
    {
        Content = content;
        State = state;
        Items = new ItemStore(this);
        Conditions = new Conditions(this);
        Clock = new WorldClock(this);
        Log = new EventLog(this);
        Rng = new Rng(state);
    }

    public int Counter(string key) => State.Counters.TryGetValue(key, out var v) ? v : 0;

    public void Count(string key, int by = 1)
    {
        State.Counters[key] = Counter(key) + by;
    }

    /// <summary>Raised by every player action when it finishes; the progression director re-evaluates on it.</summary>
    public event Action? AfterChange;
    public void Changed() => AfterChange?.Invoke();

    public void Notify(NoticeKind kind, string title, string text) => _notices.Add(new Notice(kind, title, text));

    public IReadOnlyList<Notice> DrainNotices()
    {
        var copy = _notices.ToList();
        _notices.Clear();
        return copy;
    }
}

/// <summary>Deterministic, save-persisted PRNG (SplitMix64).</summary>
public sealed class Rng
{
    private readonly GameState _state;
    public Rng(GameState state) => _state = state;

    public ulong NextULong()
    {
        unchecked
        {
            var z = _state.RngState += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    public int Next(int maxExclusive) => maxExclusive <= 1 ? 0 : (int)(NextULong() % (ulong)maxExclusive);
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));
}

/// <summary>Evaluates designer <see cref="Condition"/>s against the current state.</summary>
public sealed class Conditions
{
    private readonly World _w;
    private int _depth;
    public Conditions(World w) => _w = w;

    public bool Holds(Condition? c)
    {
        if (c is null) return true;
        if (_depth > 24) return false; // a condition that (indirectly) depends on itself never holds
        _depth++;
        try { return Eval(c); }
        finally { _depth--; }
    }

    private bool Eval(Condition c)
    {
        var s = _w.State;
        if (c.Always is bool a && !a) return false;
        if (c.All is not null && !c.All.All(Holds)) return false;
        if (c.Any is not null && !c.Any.Any(Holds)) return false;
        if (c.Not is not null && Holds(c.Not)) return false;
        if (c.Beat is not null && !s.FiredBeats.Contains(c.Beat)) return false;
        if (c.Resolved is not null && !s.Evidence.Resolved.Contains(c.Resolved)) return false;
        if (c.Arrived is not null && !s.ArrivedObjects.Contains(c.Arrived)) return false;
        if (c.Reading is not null && !ReadingSurfaced(c.Reading)) return false;
        if (c.Locale is not null && !s.UnlockedLocales.Contains(c.Locale)) return false;
        if (c.Capability is not null && !HasCapability(c.Capability)) return false;
        if (c.Tool is not null && !ToolAvailable(c.Tool)) return false;
        if (c.Flag is not null && !s.Flags.Contains(c.Flag)) return false;
        if (c.Counter is not null && _w.Counter(c.Counter) < c.AtLeast) return false;
        if (c.Discovered is not null && !s.Apothecary.Known.Contains(c.Discovered)) return false;
        if (c.Seal is not null && !s.Sanctum.SolvedSeals.Contains(c.Seal)) return false;
        if (c.Lifeform is not null && !s.Pools.SpeciesSeen.Contains(c.Lifeform)) return false;
        return true;
    }

    public bool ReadingSurfaced(string readingId) =>
        _w.Content.Readings.TryGetValue(readingId, out var r) &&
        _w.State.Evidence.Surfaced.TryGetValue(r.Obj.Id, out var list) && list.Contains(readingId);

    public bool ToolAvailable(string toolId)
    {
        if (!_w.Content.Tools.TryGetValue(toolId, out var tool)) return false;
        if (tool.Kind == ToolKind.Fixed) return Holds(tool.AvailableWhen);
        return _w.Items.All.Any(i => i.Def == toolId && !i.Inert && i.Location != Locations.Gone);
    }

    public bool HasCapability(string capability) =>
        _w.Content.Tools.Values.Any(t => t.Grants.Contains(capability) && ToolAvailable(t.Id));

    /// <summary>Fixed tools of a locale that are currently available, in content order.</summary>
    public IEnumerable<ToolResource> FixedToolsAt(string locale) =>
        _w.Content.Tools.Values.Where(t => t.Kind == ToolKind.Fixed && t.Locale == locale && Holds(t.AvailableWhen));
}

/// <summary>The single slow, pausable world-clock (invariant 9). Drives every time-based simulation.</summary>
public sealed class WorldClock
{
    private readonly World _w;
    private readonly List<Action<long>> _tickers = new();
    public WorldClock(World w) => _w = w;

    public long Tick => _w.State.Clock.Tick;
    public bool Paused => _w.State.Clock.Paused;

    public void OnTick(Action<long> ticker) => _tickers.Add(ticker);

    public void SetPaused(bool paused) => _w.State.Clock.Paused = paused;

    /// <summary>Advances by real elapsed seconds (if not paused). Returns ticks advanced.</summary>
    public int Step(double realSeconds)
    {
        if (Paused || realSeconds <= 0) return 0;
        var c = _w.State.Clock;
        c.Carry += realSeconds / _w.Config.SecondsPerTick;
        var whole = (int)Math.Floor(c.Carry);
        c.Carry -= whole;
        whole = Math.Min(whole, _w.Config.MaxTicksPerStep);
        Advance(whole);
        return whole;
    }

    /// <summary>Advances a whole number of ticks (sitting a while, offline catch-up, tests). Ignores pause.</summary>
    public void Advance(int ticks)
    {
        if (ticks <= 0) return;
        var from = Tick;
        for (int i = 0; i < ticks; i++)
        {
            _w.State.Clock.Tick++;
            foreach (var t in _tickers) t(_w.State.Clock.Tick);
        }
        _w.Bus.Publish(new Ticked(from, Tick));
    }

    /// <summary>"The tree's shadow has moved a hand's width" style description of a tick span.</summary>
    public string Describe(long ticks)
    {
        if (_w.Config.TickDisplay == "numeric") return $"{ticks} tick{(ticks == 1 ? "" : "s")}";
        return ticks switch
        {
            <= 0 => "no time at all",
            < 20 => "a few breaths",
            < 60 => "a little while",
            < 150 => "a hand's width of shadow",
            < 300 => "a pool's length of shadow",
            < 600 => "the shadow of a branch",
            _ => "a long, slow stretch of shade",
        };
    }

    /// <summary>The time of day, told by Old Stoic's shadow. Purely presentational.</summary>
    public string TimeOfDay()
    {
        var phase = (Tick / 300) % 8;
        return phase switch
        {
            0 => "early morning",
            1 => "mid-morning",
            2 => "late morning",
            3 => "noon",
            4 => "early afternoon",
            5 => "late afternoon",
            6 => "evening",
            _ => "night",
        };
    }

    public long Day => Tick / 2400 + 1;
}

/// <summary>The automatic, permanent Event Log (README "Shared systems").</summary>
public sealed class EventLog
{
    private readonly World _w;
    public EventLog(World w) => _w = w;

    public void Add(LogKind kind, string text, string? obj = null, string? locale = null) =>
        _w.State.Log.Add(new LogEntry { Tick = _w.Clock.Tick, Kind = kind, Text = text, Object = obj, Locale = locale });

    public IReadOnlyList<LogEntry> Entries => _w.State.Log;
}
