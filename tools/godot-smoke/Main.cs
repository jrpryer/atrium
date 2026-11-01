using System.Runtime.InteropServices;
using Atrium.Core;
using Atrium.Core.Content;
using Godot;

/// <summary>
/// Loads the repository's content through Atrium.Core inside Godot, validates it, plays a few hundred world-clock
/// ticks and round-trips a save, printing the .NET runtime it ran on. Exits 0 when everything worked.
/// </summary>
public partial class Main : Node
{
    public override void _Ready()
    {
        GD.Print($"Godot {Engine.GetVersionInfo()["string"].AsString()} on {RuntimeInformation.FrameworkDescription}");
        var content = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "..", "content"));
        var (db, report) = ContentLoader.TryLoad(new DirectoryContentSource(content));
        foreach (var e in report.Errors) GD.PrintErr("content error: " + e);
        if (db is null)
        {
            GD.PrintErr($"FAILED: content at {content} did not load");
            GetTree().Quit(1);
            return;
        }
        GD.Print($"Content: {db.Tomes.Count} tomes, {db.Items.Count} items, {db.Recipes.Count} recipes, " +
                 $"{db.Lifeforms.Count} lifeforms, {db.Pools.Count} pools, {db.Objects.Count} clue-objects " +
                 $"({report.Warnings.Count} warnings)");

        var session = GameSession.NewGame(db, 1310);
        session.AdvanceTicks(600);
        var json = session.SaveJson();
        var back = GameSession.LoadJson(db, json);
        GD.Print($"Played 600 ticks; saved {json.Length:N0} characters and loaded them back ({back.SaveJson().Length:N0}).");
        GD.Print(report.Ok ? "OK: Atrium.Core works in this Godot on this runtime." : "FAILED: content has errors.");
        GetTree().Quit(report.Ok ? 0 : 1);
    }
}
