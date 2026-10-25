namespace Atrium.Core.Runtime;

public enum LineKind
{
    /// <summary>Plain narration of what happened.</summary>
    Narration,
    /// <summary>A reading surfaced for the first time.</summary>
    NewReading,
    /// <summary>A reading the player already has, recalled.</summary>
    Recall,
    Discovery,
    NearMiss,
    Resolved,
    /// <summary>Gentle "this didn't work" responses. Never a buzzer.</summary>
    Soft,
    Term,
    System,
}

public readonly record struct Line(LineKind Kind, string Text);

/// <summary>What a player action produced. Actions never throw for ordinary player mistakes.</summary>
public sealed class ActionResult
{
    public bool Ok { get; init; } = true;
    public List<Line> Lines { get; } = new();

    public ActionResult Add(LineKind kind, string text)
    {
        if (!string.IsNullOrWhiteSpace(text)) Lines.Add(new Line(kind, text));
        return this;
    }

    public ActionResult AddRange(IEnumerable<Line> lines) { Lines.AddRange(lines); return this; }

    public static ActionResult Say(string text) => new ActionResult().Add(LineKind.Narration, text);
    public static ActionResult Soft(string text) => new ActionResult { Ok = false }.Add(LineKind.Soft, text);
}
