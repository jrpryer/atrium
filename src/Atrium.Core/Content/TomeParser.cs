using System.Text;

namespace Atrium.Core.Content;

/// <summary>
/// Parses the tome authoring format: a <c>---</c> front-matter block of <c>key: value</c> lines, then pages that
/// each begin with <c>::: page &lt;kind&gt; [faded] [loose] [| Title]</c>. See docs/CONTENT_GUIDE.md.
/// </summary>
public static class TomeParser
{
    public static TomeResource Parse(string text, string sourceFile)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var tome = new TomeResource { SourceFile = sourceFile };
        int i = 0;
        while (i < lines.Length && lines[i].Trim().Length == 0) i++;
        if (i >= lines.Length || lines[i].Trim() != "---")
            throw new FormatException($"{sourceFile}: a tome must start with a '---' front-matter block.");
        i++;
        for (; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Trim() == "---") { i++; break; }
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#')) continue;
            var colon = line.IndexOf(':');
            if (colon <= 0) throw new FormatException($"{sourceFile}:{i + 1}: expected 'key: value'.");
            var key = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();
            switch (key)
            {
                case "id": tome.Id = value; break;
                case "title": tome.Title = value; break;
                case "author": tome.Author = value; break;
                case "year":
                    if (!int.TryParse(value, out var y)) throw new FormatException($"{sourceFile}:{i + 1}: year must be a whole number.");
                    tome.Year = y; break;
                case "genre": tome.Genre = value.ToLowerInvariant(); break;
                case "cover": tome.Cover = value; break;
                case "blurb": tome.Blurb = value; break;
                case "hint": tome.Hint = value; break;
                case "available":
                    tome.Available = value.StartsWith('{')
                        ? System.Text.Json.JsonSerializer.Deserialize<Condition>(value, ContentJson.Options)!
                        : Condition.Parse(value);
                    break;
                default: throw new FormatException($"{sourceFile}:{i + 1}: unknown front-matter key '{key}'.");
            }
        }

        PageResource? page = null;
        var body = new StringBuilder();
        void Flush()
        {
            if (page is null) return;
            page.Text = TrimBlankLines(body.ToString());
            tome.Pages.Add(page);
            body.Clear();
        }

        for (; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith(":::", StringComparison.Ordinal))
            {
                Flush();
                page = ParseHeader(line, sourceFile, i + 1);
                page.Number = tome.Pages.Count + 1;
                continue;
            }
            if (page is null)
            {
                if (line.Trim().Length == 0) continue;
                throw new FormatException($"{sourceFile}:{i + 1}: text before the first '::: page' header.");
            }
            body.Append(line.TrimEnd()).Append('\n');
        }
        Flush();
        if (string.IsNullOrWhiteSpace(tome.Id))
            tome.Id = Path.GetFileNameWithoutExtension(sourceFile);
        return tome;
    }

    private static PageResource ParseHeader(string line, string file, int lineNo)
    {
        var rest = line[3..].Trim();
        string? title = null;
        var bar = rest.IndexOf('|');
        if (bar >= 0) { title = rest[(bar + 1)..].Trim(); rest = rest[..bar].Trim(); }
        var words = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || !words[0].Equals("page", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"{file}:{lineNo}: page headers look like '::: page prose | Optional title'.");
        var p = new PageResource { Kind = words[1].ToLowerInvariant(), Title = string.IsNullOrEmpty(title) ? null : title };
        foreach (var w in words.Skip(2))
        {
            switch (w.ToLowerInvariant())
            {
                case "faded": p.Faded = true; break;
                case "loose": p.Loose = true; break;
                default: throw new FormatException($"{file}:{lineNo}: unknown page flag '{w}'.");
            }
        }
        return p;
    }

    private static string TrimBlankLines(string s)
    {
        var lines = s.Split('\n').ToList();
        while (lines.Count > 0 && lines[0].Trim().Length == 0) lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
        return string.Join("\n", lines);
    }
}
