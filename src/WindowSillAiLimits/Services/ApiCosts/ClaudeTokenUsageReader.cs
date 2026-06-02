using System.Text.Json;

using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.ApiCosts;

public sealed class ClaudeTokenUsageReader(string rootPath) : JsonTokenUsageReader(rootPath)
{
    protected override void ReadFile(
        string path,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        Dictionary<string, TokenUsageTotals> usageByModel)
    {
        IEnumerable<string> lines;
        try
        {
            lines = File.ReadLines(path);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        foreach (var line in lines)
        {
            if (!TryParseJsonLine(line, out var document))
            {
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                if (!IsInsideWindow(root, windowStart, windowEnd) ||
                    root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("message", out var message) ||
                    message.ValueKind != JsonValueKind.Object ||
                    !message.TryGetProperty("usage", out var usage) ||
                    usage.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                AddUsage(usageByModel, GetString(message, "model") ?? "unknown-claude", new TokenUsageTotals(
                    GetInt64(usage, "input_tokens"),
                    GetInt64(usage, "cache_read_input_tokens"),
                    GetInt64(usage, "ephemeral_5m_input_tokens") + GetFallbackCacheCreationTokens(usage),
                    GetInt64(usage, "ephemeral_1h_input_tokens"),
                    GetInt64(usage, "output_tokens")));
            }
        }
    }

    private static long GetFallbackCacheCreationTokens(JsonElement usage)
    {
        var explicitFiveMinute = GetInt64(usage, "ephemeral_5m_input_tokens");
        var explicitOneHour = GetInt64(usage, "ephemeral_1h_input_tokens");
        return explicitFiveMinute > 0 || explicitOneHour > 0
            ? 0
            : GetInt64(usage, "cache_creation_input_tokens");
    }
}
