namespace Atrium.Core.Content;

/// <summary>
/// A designer-authored predicate over game state. Every field that is set must hold (implicit AND);
/// an empty condition is always true. Used for locale unlocks, story beats, arrivals, tome availability,
/// tool blueprints, claim gates and reading prerequisites.
/// </summary>
/// <remarks>
/// In JSON a condition is either an object (<c>{"beat": "unkept-vow"}</c>) or a shorthand string:
/// <c>"start"</c> / <c>"always"</c>, or <c>"key:value"</c> for any single string field
/// (<c>"beat:unkept-vow"</c>, <c>"locale:pools"</c>, <c>"reading:leaf.script"</c>).
/// </remarks>
public sealed class Condition
{
    public bool? Always { get; set; }
    public List<Condition>? All { get; set; }
    public List<Condition>? Any { get; set; }
    public Condition? Not { get; set; }

    /// <summary>A story beat has fired.</summary>
    public string? Beat { get; set; }
    /// <summary>A mystery object's true claim has been resolved.</summary>
    public string? Resolved { get; set; }
    /// <summary>A mystery object has arrived in the world.</summary>
    public string? Arrived { get; set; }
    /// <summary>A reading (by id) has been surfaced.</summary>
    public string? Reading { get; set; }
    /// <summary>A locale is unlocked.</summary>
    public string? Locale { get; set; }
    /// <summary>A live (non-inert) tool grants this capability.</summary>
    public string? Capability { get; set; }
    /// <summary>A tool is available: a fixed tool whose availability holds, or an assembled tool that exists and is not inert.</summary>
    public string? Tool { get; set; }
    public string? Flag { get; set; }
    /// <summary>A named counter has reached <see cref="AtLeast"/>.</summary>
    public string? Counter { get; set; }
    public int AtLeast { get; set; } = 1;
    /// <summary>An Apothecary element is known.</summary>
    public string? Discovered { get; set; }
    /// <summary>A seal has been solved.</summary>
    public string? Seal { get; set; }
    /// <summary>A lifeform species has been held at least once.</summary>
    public string? Lifeform { get; set; }

    public static Condition True => new() { Always = true };

    public bool IsEmpty =>
        Always is null && All is null && Any is null && Not is null && Beat is null && Resolved is null &&
        Arrived is null && Reading is null && Locale is null && Capability is null && Tool is null &&
        Flag is null && Counter is null && Discovered is null && Seal is null && Lifeform is null;

    /// <summary>Parses the string shorthand. Throws <see cref="FormatException"/> on unknown keys.</summary>
    public static Condition Parse(string text)
    {
        var t = text.Trim();
        if (t.Length == 0 || t.Equals("start", StringComparison.OrdinalIgnoreCase) || t.Equals("always", StringComparison.OrdinalIgnoreCase))
            return True;
        if (t.Equals("never", StringComparison.OrdinalIgnoreCase))
            return new Condition { Always = false };

        var colon = t.IndexOf(':');
        if (colon <= 0) throw new FormatException($"Condition shorthand '{text}' must be 'start' or 'key:value'.");
        var key = t[..colon].Trim().ToLowerInvariant();
        var value = t[(colon + 1)..].Trim();
        var c = new Condition();
        switch (key)
        {
            case "beat": c.Beat = value; break;
            case "resolved": c.Resolved = value; break;
            case "arrived": c.Arrived = value; break;
            case "reading": c.Reading = value; break;
            case "locale": c.Locale = value; break;
            case "capability": c.Capability = value; break;
            case "tool": c.Tool = value; break;
            case "flag": c.Flag = value; break;
            case "discovered": c.Discovered = value; break;
            case "seal": c.Seal = value; break;
            case "lifeform": c.Lifeform = value; break;
            case "counter":
                // counter:name>=3
                var ge = value.IndexOf(">=", StringComparison.Ordinal);
                if (ge > 0) { c.Counter = value[..ge].Trim(); c.AtLeast = int.Parse(value[(ge + 2)..].Trim()); }
                else c.Counter = value;
                break;
            default: throw new FormatException($"Unknown condition key '{key}' in '{text}'.");
        }
        return c;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Always is not null) parts.Add(Always.Value ? "always" : "never");
        if (Beat is not null) parts.Add($"beat:{Beat}");
        if (Resolved is not null) parts.Add($"resolved:{Resolved}");
        if (Arrived is not null) parts.Add($"arrived:{Arrived}");
        if (Reading is not null) parts.Add($"reading:{Reading}");
        if (Locale is not null) parts.Add($"locale:{Locale}");
        if (Capability is not null) parts.Add($"capability:{Capability}");
        if (Tool is not null) parts.Add($"tool:{Tool}");
        if (Flag is not null) parts.Add($"flag:{Flag}");
        if (Counter is not null) parts.Add($"counter:{Counter}>={AtLeast}");
        if (Discovered is not null) parts.Add($"discovered:{Discovered}");
        if (Seal is not null) parts.Add($"seal:{Seal}");
        if (Lifeform is not null) parts.Add($"lifeform:{Lifeform}");
        if (All is not null) parts.Add($"all[{string.Join(", ", All)}]");
        if (Any is not null) parts.Add($"any[{string.Join(", ", Any)}]");
        if (Not is not null) parts.Add($"not({Not})");
        return parts.Count == 0 ? "always" : string.Join(" & ", parts);
    }

    /// <summary>Every condition node in this tree, including this one.</summary>
    public IEnumerable<Condition> Walk()
    {
        yield return this;
        if (All is not null) foreach (var c in All) foreach (var n in c.Walk()) yield return n;
        if (Any is not null) foreach (var c in Any) foreach (var n in c.Walk()) yield return n;
        if (Not is not null) foreach (var n in Not.Walk()) yield return n;
    }
}
