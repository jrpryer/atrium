using System.Text.Json;
using System.Text.Json.Serialization;

namespace Atrium.Core.Content;

/// <summary>Shared JSON settings for content (strict: unknown keys are errors, so typos surface at load).</summary>
public static class ContentJson
{
    public static JsonSerializerOptions Options { get; } = Create(strict: true);

    /// <summary>Lenient settings for save files and tooling.</summary>
    public static JsonSerializerOptions SaveOptions { get; } = Create(strict: false, indented: true);

    private static JsonSerializerOptions Create(bool strict, bool indented = false)
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = null,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            UnmappedMemberHandling = strict ? JsonUnmappedMemberHandling.Disallow : JsonUnmappedMemberHandling.Skip,
        };
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        o.Converters.Add(new ConditionConverter());
        o.Converters.Add(new IntRangeConverter());
        return o;
    }
}

/// <summary>Reads a <see cref="Condition"/> from an object or from the string shorthand.</summary>
public sealed class ConditionConverter : JsonConverter<Condition>
{
    public override Condition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            try { return Condition.Parse(reader.GetString() ?? ""); }
            catch (FormatException ex) { throw new JsonException(ex.Message); }
        }
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A condition must be an object or a shorthand string.");

        var c = new Condition();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) return c;
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Malformed condition.");
            var name = reader.GetString()!.ToLowerInvariant();
            reader.Read();
            switch (name)
            {
                case "always": c.Always = reader.GetBoolean(); break;
                case "all": c.All = ReadList(ref reader, options); break;
                case "any": c.Any = ReadList(ref reader, options); break;
                case "not": c.Not = Read(ref reader, typeof(Condition), options); break;
                case "beat": c.Beat = reader.GetString(); break;
                case "resolved": c.Resolved = reader.GetString(); break;
                case "arrived": c.Arrived = reader.GetString(); break;
                case "reading": c.Reading = reader.GetString(); break;
                case "locale": c.Locale = reader.GetString(); break;
                case "capability": c.Capability = reader.GetString(); break;
                case "tool": c.Tool = reader.GetString(); break;
                case "flag": c.Flag = reader.GetString(); break;
                case "counter": c.Counter = reader.GetString(); break;
                case "atleast": c.AtLeast = reader.GetInt32(); break;
                case "discovered": c.Discovered = reader.GetString(); break;
                case "seal": c.Seal = reader.GetString(); break;
                case "lifeform": c.Lifeform = reader.GetString(); break;
                default: throw new JsonException($"Unknown condition key '{name}'.");
            }
        }
        throw new JsonException("Unterminated condition.");
    }

    private List<Condition> ReadList(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("'all'/'any' must be arrays.");
        var list = new List<Condition>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            list.Add(Read(ref reader, typeof(Condition), options));
        return list;
    }

    public override void Write(Utf8JsonWriter writer, Condition value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>Reads an <see cref="IntRange"/> from <c>[min, max]</c> or <c>{"min":…, "max":…}</c>.</summary>
public sealed class IntRangeConverter : JsonConverter<IntRange>
{
    public override IntRange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read(); var min = reader.GetInt32();
            reader.Read(); var max = reader.GetInt32();
            reader.Read();
            if (reader.TokenType != JsonTokenType.EndArray) throw new JsonException("A range array must have exactly two numbers.");
            return new IntRange(min, max);
        }
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("A range must be [min, max] or {min, max}.");
        var r = IntRange.Everything;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString()!.ToLowerInvariant();
            reader.Read();
            if (name == "min") r.Min = reader.GetInt32();
            else if (name == "max") r.Max = reader.GetInt32();
            else throw new JsonException($"Unknown range key '{name}'.");
        }
        return r;
    }

    public override void Write(Utf8JsonWriter writer, IntRange value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Min);
        writer.WriteNumberValue(value.Max);
        writer.WriteEndArray();
    }
}
