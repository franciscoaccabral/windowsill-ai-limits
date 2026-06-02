using System.Text.Json;

using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.ApiCosts;

public abstract class JsonTokenUsageReader(string rootPath) : ITokenUsageReader
{
    private readonly string _rootPath = rootPath;

    public IReadOnlyDictionary<string, TokenUsageTotals> ReadUsage(DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        var usage = new Dictionary<string, TokenUsageTotals>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(_rootPath))
        {
            return usage;
        }

        foreach (var path in EnumerateFiles(_rootPath))
        {
            ReadFile(path, windowStart, windowEnd, usage);
        }

        return usage;
    }

    protected abstract void ReadFile(
        string path,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        Dictionary<string, TokenUsageTotals> usageByModel);

    protected static void AddUsage(
        Dictionary<string, TokenUsageTotals> usageByModel,
        string? modelId,
        TokenUsageTotals usage)
    {
        if (usage.TotalTokens <= 0)
        {
            return;
        }

        var key = string.IsNullOrWhiteSpace(modelId) ? "unknown" : modelId.Trim();
        usageByModel[key] = usageByModel.TryGetValue(key, out var previous)
            ? previous.Add(usage)
            : usage;
    }

    protected static bool TryParseJsonLine(string line, out JsonDocument document)
    {
        try
        {
            document = JsonDocument.Parse(line);
            return true;
        }
        catch (JsonException)
        {
            document = null!;
            return false;
        }
    }

    protected static bool IsInsideWindow(JsonElement root, DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        if (!TryGetTimestamp(root, out var timestamp))
        {
            return false;
        }

        return timestamp >= windowStart && timestamp <= windowEnd;
    }

    protected static bool TryGetTimestamp(JsonElement root, out DateTimeOffset timestamp)
    {
        timestamp = default;
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty("timestamp", out var property) &&
               property.ValueKind == JsonValueKind.String &&
               DateTimeOffset.TryParse(property.GetString(), out timestamp);
    }

    protected static long GetInt64(JsonElement source, string propertyName)
    {
        if (source.ValueKind != JsonValueKind.Object ||
            !source.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt64(out var value) => Math.Max(0, value),
            JsonValueKind.String when long.TryParse(property.GetString(), out var value) => Math.Max(0, value),
            _ => 0,
        };
    }

    protected static string? GetString(JsonElement source, string propertyName)
        => source.ValueKind == JsonValueKind.Object &&
           source.TryGetProperty(propertyName, out var property) &&
           property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static IEnumerable<string> EnumerateFiles(string rootPath)
    {
        IEnumerable<string> jsonl;
        IEnumerable<string> json;

        try
        {
            jsonl = Directory.EnumerateFiles(rootPath, "*.jsonl", SearchOption.AllDirectories);
            json = Directory.EnumerateFiles(rootPath, "*.json", SearchOption.AllDirectories);
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        return jsonl.Concat(json);
    }
}
