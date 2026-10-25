namespace Atrium.Core.Runtime;

// The complete, serialisable game state. Services hold no state of their own beyond indexes, so a save is
// exactly one GameState (save-anywhere, AGENTS §5.8).

public sealed class GameState
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;

    public ClockState Clock { get; set; } = new();
    public ulong RngState { get; set; }

    public List<ItemInstance> Items { get; set; } = new();
    public int NextItemId { get; set; } = 1;

    public HashSet<string> UnlockedLocales { get; set; } = new();
    public HashSet<string> VisitedLocales { get; set; } = new();
    public List<string> FiredBeats { get; set; } = new();
    public HashSet<string> Flags { get; set; } = new();
    public Dictionary<string, int> Counters { get; set; } = new();
    public HashSet<string> ArrivedObjects { get; set; } = new();

    public EvidenceState Evidence { get; set; } = new();
    public Dictionary<string, EssenceRecord> Essences { get; set; } = new();
    public LibraryState Library { get; set; } = new();
    public ApothecaryState Apothecary { get; set; } = new();
    public PoolsState Pools { get; set; } = new();
    public SanctumState Sanctum { get; set; } = new();
    public List<LogEntry> Log { get; set; } = new();
}

public sealed class ClockState
{
    public long Tick { get; set; }
    public bool Paused { get; set; }
    /// <summary>Fractional ticks carried between real-time steps.</summary>
    public double Carry { get; set; }
}

public sealed class ItemInstance
{
    public string Id { get; set; } = "";
    public string Def { get; set; } = "";
    public string Location { get; set; } = "";
    public int Quantity { get; set; } = 1;
    /// <summary>The essence this instance materially carries (invariant 2), if any.</summary>
    public string? Essence { get; set; }
    /// <summary>For remnants: the essence this was split from.</summary>
    public string? RemnantOf { get; set; }
    /// <summary>For remnants: the form the Circle restores, and the history step it was made at.</summary>
    public string? Restores { get; set; }
    public int RemnantStep { get; set; }
    /// <summary>Nullified by reconstitution: a husk with no traits, energy or capabilities.</summary>
    public bool Inert { get; set; }
    public string? FormerEssence { get; set; }
}

public sealed class EssenceRecord
{
    public string Essence { get; set; } = "";
    public string CarrierId { get; set; } = "";
    /// <summary>Forms the essence has passed through since its last restoration, oldest first.</summary>
    public List<string> History { get; set; } = new();
    public List<string> RemnantIds { get; set; } = new();
    /// <summary>Outcomes the essence enabled without inhabiting (catalysis). Never nullified.</summary>
    public List<string> Catalyzed { get; set; } = new();
}

public sealed class EvidenceState
{
    public Dictionary<string, List<string>> Surfaced { get; set; } = new();
    public HashSet<string> EarnedTerms { get; set; } = new();
    public Dictionary<string, string> TermSources { get; set; } = new();
    public HashSet<string> NearMissesGiven { get; set; } = new();
    public HashSet<string> Resolved { get; set; } = new();
    public List<AttemptRecord> Attempts { get; set; } = new();
}

public sealed class AttemptRecord
{
    public string Object { get; set; } = "";
    public Dictionary<string, string> Designation { get; set; } = new();
    public string Outcome { get; set; } = "";
    public string? Claim { get; set; }
    public long Tick { get; set; }
}

public sealed class LibraryState
{
    public List<ShelfState> Shelves { get; set; } = new();
    /// <summary>Tomes waiting to be shelved, in pile order (top first).</summary>
    public List<string> Pile { get; set; } = new();
    public HashSet<string> ArrivedTomes { get; set; } = new();
    public HashSet<string> ReadPages { get; set; } = new();
    public HashSet<string> DeliveredPages { get; set; } = new();
    public Dictionary<string, string> Notes { get; set; } = new();
    public List<string> CheckedOut { get; set; } = new();
    public HashSet<string> SettledShelves { get; set; } = new();
    public HashSet<string> EverShelved { get; set; } = new();
    public HashSet<string> RevealedPages { get; set; } = new();
}

public sealed class ShelfState
{
    public string Id { get; set; } = "";
    public string? Genre { get; set; }
    public List<string> Tomes { get; set; } = new();
}

public sealed class ApothecaryState
{
    public HashSet<string> Known { get; set; } = new();
    public List<string> DiscoveryOrder { get; set; } = new();
    public HashSet<string> Tried { get; set; } = new();
    public int Insight { get; set; }
    /// <summary>Revealed hints on the Discovery Map: <c>recipeId:inputIndex</c>.</summary>
    public HashSet<string> Hints { get; set; } = new();
}

public sealed class PoolsState
{
    public List<PoolState> Pools { get; set; } = new();
    public List<LifeformInstance> Holding { get; set; } = new();
    public HashSet<string> SpeciesSeen { get; set; } = new();
    public HashSet<string> RulesSeen { get; set; } = new();
    public int NextLifeformId { get; set; } = 1;
    public bool Stocked { get; set; }
}

public sealed class PoolState
{
    public string Id { get; set; } = "";
    public double Clarity { get; set; }
    public List<LifeformInstance> Lifeforms { get; set; } = new();
    /// <summary>Soak progress per <c>transformId:itemInstanceId</c>, 0..1.</summary>
    public Dictionary<string, double> Progress { get; set; } = new();
    /// <summary>Ticks since the last spawn, per spawn rule.</summary>
    public Dictionary<string, int> SpawnTimers { get; set; } = new();
    public int Cleanses { get; set; }
    public HashSet<string> FindsGiven { get; set; } = new();
}

public enum Sex { None, Female, Male }

public enum Mood { Thriving, Content, Sulking }

public sealed class LifeformInstance
{
    public int Id { get; set; }
    public string Species { get; set; } = "";
    public string Name { get; set; } = "";
    public Sex Sex { get; set; }
    public double Hunger { get; set; }
    public long BornTick { get; set; }
    public Mood Mood { get; set; } = Mood.Content;
    public string MoodReason { get; set; } = "";
    public int Gestation { get; set; }
    public string? Expecting { get; set; }
    public string? ExpectingRule { get; set; }
    public int Cooldown { get; set; }
}

public sealed class SanctumState
{
    public HashSet<string> SolvedSeals { get; set; } = new();
}

public enum LogKind { Story, Reading, NearMiss, Attempt, Resolved, Discovery, Pools, Library, Essence, Tool, Arrival, Info }

public sealed class LogEntry
{
    public long Tick { get; set; }
    public LogKind Kind { get; set; }
    public string Text { get; set; } = "";
    public string? Object { get; set; }
    public string? Locale { get; set; }
}
