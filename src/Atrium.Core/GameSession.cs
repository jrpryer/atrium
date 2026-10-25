using System.Text.Json;
using Atrium.Core.Content;
using Atrium.Core.Essence;
using Atrium.Core.Locales;
using Atrium.Core.Mystery;
using Atrium.Core.Runtime;
using Atrium.Core.Story;

namespace Atrium.Core;

/// <summary>
/// Composition root and facade. A front end (the terminal game, a Godot scene tree, tests) creates one session,
/// calls locale actions, steps the clock with real time, and drains notices. All rules live below this class.
/// </summary>
public sealed class GameSession
{
    public ContentDatabase Content { get; }
    public World World { get; }
    public GameState State => World.State;

    public EvidenceLedger Ledger { get; }
    public LensService Lens { get; }
    public ComprehensionGate Gate { get; }
    public SynthesisResolver Resolver { get; }
    public Lexicon Lexicon { get; }
    public UnresolvedRegistry Unresolved { get; }
    public EssenceRegistry Essence { get; }
    public SanctumRouter Router { get; }
    public ProgressionDirector Director { get; }

    public LibraryLocale Library { get; }
    public ApothecaryLocale Apothecary { get; }
    public PoolsLocale Pools { get; }
    public SanctumLocale Sanctum { get; }

    private GameSession(ContentDatabase content, GameState state)
    {
        Content = content;
        World = new World(content, state);
        Ledger = new EvidenceLedger(World);
        Lens = new LensService(World, Ledger);
        Gate = new ComprehensionGate(World, Ledger);
        Resolver = new SynthesisResolver(World, Ledger, Gate);
        Lexicon = new Lexicon(World, Ledger);
        Unresolved = new UnresolvedRegistry(World, Ledger);
        Essence = new EssenceRegistry(World);
        Router = new SanctumRouter(World);
        Director = new ProgressionDirector(World, Router, Essence, Ledger, Unresolved);

        Library = new LibraryLocale(World, Lens);
        Apothecary = new ApothecaryLocale(World, Lens, Essence);
        Pools = new PoolsLocale(World, Lens, Essence);
        Sanctum = new SanctumLocale(World, Lens, Essence, Resolver, Gate, Lexicon, Router);

        World.AfterChange += Director.Evaluate;
    }

    public static GameSession NewGame(ContentDatabase content, ulong? seed = null)
    {
        var state = new GameState { RngState = seed ?? content.Config.Seed };
        var s = new GameSession(content, state);
        s.Library.InitializeNewGame();
        s.Apothecary.InitializeNewGame();
        s.Pools.InitializeNewGame();
        s.World.Log.Add(LogKind.Story, "You arrived in the Atrium to take up the Keeper's charter.");
        s.Director.Announce = false;
        s.Director.Evaluate();
        s.Director.Announce = true;
        s.World.DrainNotices(); // the opening is presented by the front end from config.prologue
        s.Essence.AssertInvariant();
        return s;
    }

    public static GameSession FromState(ContentDatabase content, GameState state)
    {
        var s = new GameSession(content, state);
        s.Essence.AssertInvariant();
        s.Director.Evaluate(); // content may have grown since the save; let it catch up
        return s;
    }

    // ───── time ─────

    /// <summary>Feeds real elapsed time to the world-clock. Returns ticks advanced.</summary>
    public int StepRealTime(double seconds)
    {
        var t = World.Clock.Step(seconds);
        if (t > 0) Director.Evaluate();
        return t;
    }

    /// <summary>Sit by the water and let time pass (a diegetic fast-forward; nothing is ever urgent).</summary>
    public ActionResult SitAWhile(int? ticks = null)
    {
        var n = ticks ?? World.Config.SitTicks;
        World.Clock.Advance(n);
        Director.Evaluate();
        return ActionResult.Say($"You sit on the warm stone by the water for {World.Clock.Describe(n)}. The falls go on falling.");
    }

    public void AdvanceTicks(int ticks)
    {
        World.Clock.Advance(ticks);
        Director.Evaluate();
    }

    public void SetPaused(bool paused) => World.Clock.SetPaused(paused);

    // ───── navigation ─────

    public bool IsUnlocked(string locale) => State.UnlockedLocales.Contains(locale);

    /// <summary>Entering a locale: its intro on the first visit, then a re-presentation of open objects (invariant 10).</summary>
    public ActionResult Enter(string locale)
    {
        if (!IsUnlocked(locale)) return ActionResult.Soft(Content.Locales.TryGetValue(locale, out var l) ? l.LockedDescription : "It's closed.");
        var r = new ActionResult();
        if (State.VisitedLocales.Add(locale) && Content.Locales[locale].Intro.Length > 0)
            r.Add(LineKind.Narration, Content.Locales[locale].Intro);
        r.AddRange(Unresolved.Resurface(locale));
        World.Count("visits:" + locale);
        World.Bus.Publish(new LocaleEntered(locale));
        Director.Evaluate();
        return r;
    }

    public IReadOnlyList<Notice> DrainNotices() => World.DrainNotices();

    /// <summary>The fixed courtyard scene shown at the hub.</summary>
    public HubResource Hub => Content.Hub;

    public bool ChapterComplete => Content.Objects.Keys.All(State.Evidence.Resolved.Contains);

    // ───── save / load ─────

    public string SaveJson() => SaveFile.Serialize(this);

    public static GameSession LoadJson(ContentDatabase content, string json, DateTimeOffset? now = null)
    {
        var file = SaveFile.Deserialize(json);
        var s = FromState(content, file.State);
        if (content.Config.OfflineProgress == "capped" && file.SavedAtUtc is { } saved)
        {
            var elapsed = ((now ?? DateTimeOffset.UtcNow) - saved).TotalSeconds;
            var ticks = (int)Math.Min(content.Config.OfflineCapTicks, Math.Max(0, elapsed / content.Config.SecondsPerTick));
            if (ticks > 0)
            {
                s.World.Clock.Advance(ticks);
                s.Director.Evaluate();
                s.World.Notify(NoticeKind.Info, "While you were away", $"Time passed in the Atrium: {s.World.Clock.Describe(ticks)}.");
            }
        }
        return s;
    }
}

/// <summary>The on-disk save format: versioned JSON around the complete <see cref="GameState"/>.</summary>
public sealed class SaveFile
{
    public const string FormatName = "atrium-save";
    public string Format { get; set; } = FormatName;
    public int Version { get; set; } = GameState.CurrentVersion;
    public DateTimeOffset? SavedAtUtc { get; set; }
    public string Summary { get; set; } = "";
    public GameState State { get; set; } = new();

    public static string Serialize(GameSession s)
    {
        var resolved = s.State.Evidence.Resolved.Count;
        var file = new SaveFile
        {
            SavedAtUtc = DateTimeOffset.UtcNow,
            Summary = $"Day {s.World.Clock.Day}, {s.World.Clock.TimeOfDay()} · {resolved} of {s.Content.Objects.Count} mysteries settled",
            State = s.State,
        };
        return JsonSerializer.Serialize(file, ContentJson.SaveOptions);
    }

    public static SaveFile Deserialize(string json)
    {
        var file = JsonSerializer.Deserialize<SaveFile>(json, ContentJson.SaveOptions)
                   ?? throw new InvalidDataException("Empty save file.");
        if (file.Format != FormatName) throw new InvalidDataException("This isn't an Atrium save.");
        if (file.Version > GameState.CurrentVersion) throw new InvalidDataException("This save comes from a newer version of the game.");
        return file;
    }
}

/// <summary>Simple file-based save slots. A Godot adapter can point this at user://.</summary>
public sealed class SaveStore
{
    public string Directory { get; }
    public SaveStore(string directory) { Directory = directory; System.IO.Directory.CreateDirectory(directory); }

    public string PathFor(string slot) => Path.Combine(Directory, slot + ".json");

    public void Save(string slot, GameSession session)
    {
        var path = PathFor(slot);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, session.SaveJson());
        File.Move(tmp, path, overwrite: true);
    }

    public bool Exists(string slot) => File.Exists(PathFor(slot));

    public GameSession Load(string slot, ContentDatabase content) => GameSession.LoadJson(content, File.ReadAllText(PathFor(slot)));

    public string? Summary(string slot)
    {
        if (!Exists(slot)) return null;
        try { return SaveFile.Deserialize(File.ReadAllText(PathFor(slot))).Summary; }
        catch (Exception) { return "(unreadable save)"; }
    }
}
