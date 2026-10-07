using System.Text.Json;

namespace Microsoft.AI.Local.Adapters;

/// <summary>Parses tables returned by language models: JSON (preferred), Markdown, or delimited lines.</summary>
internal static class TableParser
{
    public static IReadOnlyList<IReadOnlyList<string>>? Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = StripCodeFence(text.Trim());
        return TryParseJson(trimmed) ?? TryParseDelimited(trimmed);
    }

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstNewLine = text.IndexOf('\n', StringComparison.Ordinal);
        var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewLine >= 0 && lastFence > firstNewLine ? text[(firstNewLine + 1)..lastFence].Trim() : text;
    }

    private static List<IReadOnlyList<string>>? TryParseJson(string text)
    {
        var start = text.IndexOfAny(['[', '{']);
        if (start < 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text.AsMemory(start), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                // Accept {"rows": [...]} / {"table": [...]} and similar wrappers.
                root = root.EnumerateObject().Select(p => p.Value).FirstOrDefault(v => v.ValueKind == JsonValueKind.Array);
            }

            if (root.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var rows = new List<IReadOnlyList<string>>();
            List<string>? headers = null;
            foreach (var row in root.EnumerateArray())
            {
                switch (row.ValueKind)
                {
                    case JsonValueKind.Array:
                        rows.Add([.. row.EnumerateArray().Select(ToCell)]);
                        break;
                    case JsonValueKind.Object:
                        // Array of records: the property names become the header row.
                        if (headers is null)
                        {
                            headers = [.. row.EnumerateObject().Select(p => p.Name)];
                            rows.Add(headers);
                        }

                        rows.Add([.. headers.Select(h => row.TryGetProperty(h, out var value) ? ToCell(value) : string.Empty)]);
                        break;
                    default:
                        return null;
                }
            }

            return rows.Count == 0 ? null : rows;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<IReadOnlyList<string>>? TryParseDelimited(string text)
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            char separator = line.Contains('|', StringComparison.Ordinal) ? '|' : line.Contains('\t', StringComparison.Ordinal) ? '\t' : ',';
            var cells = line.Trim('|').Split(separator).Select(c => c.Trim()).ToList();

            // Skip Markdown header separators such as |---|:---:|.
            if (cells.All(c => c.Length > 0 && c.All(ch => ch is '-' or ':' or ' ')))
            {
                continue;
            }

            rows.Add(cells);
        }

        return rows.Count > 0 && rows.Any(r => r.Count > 1) ? rows : null;
    }

    private static string ToCell(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => value.GetRawText(),
    };
}
