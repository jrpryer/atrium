namespace Atrium.Core.Content;

/// <summary>An inclusive integer range. In JSON: <c>[min, max]</c> or <c>{"min": 0, "max": 4}</c>.</summary>
public struct IntRange
{
    public int Min { get; set; }
    public int Max { get; set; }

    public IntRange(int min, int max) { Min = min; Max = max; }

    public static IntRange Everything => new(int.MinValue, int.MaxValue);

    public readonly bool Contains(int v) => v >= Min && v <= Max;
    public readonly bool ContainsRange(IntRange inner) => inner.Min >= Min && inner.Max <= Max;
    public override readonly string ToString() => Min == int.MinValue && Max == int.MaxValue ? "any" : $"{Min}..{Max}";
}

/// <summary>A position (or contribution) on the two energy axes shared by every locale.</summary>
public struct Energy
{
    /// <summary>Light (+) ↔ Dark (−).</summary>
    public int Light { get; set; }
    /// <summary>Hot (+) ↔ Cold (−).</summary>
    public int Heat { get; set; }

    public Energy(int light, int heat) { Light = light; Heat = heat; }

    public static Energy operator +(Energy a, Energy b) => new(a.Light + b.Light, a.Heat + b.Heat);
    public readonly bool IsZero => Light == 0 && Heat == 0;
    public override readonly string ToString() => $"light {Light:+0;-0;0}, heat {Heat:+0;-0;0}";
}

/// <summary>A survival or thriving region on the Light/Dark × Hot/Cold plane.</summary>
public sealed class Band
{
    public IntRange Light { get; set; } = IntRange.Everything;
    public IntRange Heat { get; set; } = IntRange.Everything;

    public bool Contains(Energy e) => Light.Contains(e.Light) && Heat.Contains(e.Heat);
    public bool ContainsBand(Band inner) => Light.ContainsRange(inner.Light) && Heat.ContainsRange(inner.Heat);
    public override string ToString() => $"light {Light}, heat {Heat}";
}

/// <summary>Content delivered back into the world, typically on resolution or a story beat (routed by <c>SanctumRouter</c>).</summary>
public sealed class Payload
{
    /// <summary>Tomes handed to the librarians, which appear on the Library's pile to be shelved.</summary>
    public List<string> Tomes { get; set; } = new();
    /// <summary>Loose pages (<c>tome-id#page</c>) restored to an existing tome; the tome returns to the pile for re-shelving.</summary>
    public List<string> Pages { get; set; } = new();
    /// <summary>Elements added to the Apothecary's known stock.</summary>
    public List<string> Ingredients { get; set; } = new();
    /// <summary>Minerals and auras for the Spirit Pools; their destination container is a config tunable.</summary>
    public List<string> Minerals { get; set; } = new();
    /// <summary>Arbitrary items placed at a location.</summary>
    public List<ItemGrant> Items { get; set; } = new();
    public List<string> Flags { get; set; } = new();

    public bool IsEmpty => Tomes.Count == 0 && Pages.Count == 0 && Ingredients.Count == 0 && Minerals.Count == 0 && Items.Count == 0 && Flags.Count == 0;
}

public sealed class ItemGrant
{
    public string Item { get; set; } = "";
    public int Quantity { get; set; } = 1;
    /// <summary>Location key; defaults to the satchel.</summary>
    public string Location { get; set; } = Locations.Satchel;
}

/// <summary>Well-known item location keys. Pools use <c>pool:&lt;id&gt;</c>.</summary>
public static class Locations
{
    public const string Satchel = "satchel";
    public const string LibraryPile = "library.pile";
    public const string SanctumCabinet = "sanctum.cabinet";
    public const string DruidsBag = "pools.druids-bag";
    public const string Gone = "gone";

    public static string Pool(string poolId) => "pool:" + poolId;
    public static bool IsPool(string location, out string poolId)
    {
        if (location.StartsWith("pool:", StringComparison.Ordinal)) { poolId = location[5..]; return true; }
        poolId = "";
        return false;
    }
}
