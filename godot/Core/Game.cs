using Atrium.Core;
using Atrium.Core.Content;
using Godot;

namespace AtriumGodot;

/// <summary>
/// The "Game" autoload (project.godot, [autoload]). At startup it loads and validates <c>res://content</c> through
/// Atrium.Core, keeps the <see cref="ContentDatabase"/> and a fresh <see cref="GameSession"/>, and reports every
/// content error to Godot's error log. Scenes reach it through <see cref="Instance"/>. It does not run the world
/// clock yet: no gameplay scene exists, and the clock's driving belongs to those.
/// </summary>
public partial class Game : Node
{
    public const string ContentRoot = "res://content";

    public static Game? Instance { get; private set; }

    /// <summary>The validated content, or null when it failed to load.</summary>
    public ContentDatabase? Content { get; private set; }

    /// <summary>The loader's report (errors and warnings); null only if loading threw before validation.</summary>
    public ValidationReport? Report { get; private set; }

    /// <summary>A new game over <see cref="Content"/>; null when the content failed.</summary>
    public GameSession? Session { get; private set; }

    /// <summary>Set when reading the content threw (a missing folder, an unreadable file).</summary>
    public string? LoadError { get; private set; }

    public bool Ok => Content is not null && LoadError is null;

    public override void _Ready()
    {
        Instance = this;
        Load();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>One line for HUDs and logs.</summary>
    public string Summary =>
        Content is { } db
            ? $"content OK: {db.Tomes.Count} tomes, {db.Items.Count} items, {db.Recipes.Count} recipes, {db.Lifeforms.Count} lifeforms, " +
              $"{db.Pools.Count} pools, {db.Objects.Count} clue-objects ({Report?.Warnings.Count ?? 0} warnings)"
            : $"content FAILED: {LoadError ?? $"{Report?.Errors.Count ?? 0} errors"}";

    private void Load()
    {
        try
        {
            var (db, report) = ContentLoader.TryLoad(new ResContentSource(ContentRoot));
            Report = report;
            foreach (var e in report.Errors) GD.PushError("content: " + e);
            foreach (var w in report.Warnings) GD.PushWarning("content: " + w);
            Content = db;
            if (db is not null) Session = GameSession.NewGame(db);
        }
        catch (Exception e)
        {
            LoadError = e.Message;
            GD.PushError($"content: could not load {ContentRoot}: {e}");
        }
        GD.Print("Game: " + Summary);
    }
}
