using System.Text.Json;

using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.ApiCosts;

public sealed class CodexTokenUsageReader(string rootPath) : JsonTokenUsageReader(rootPath)
{
    protected override void ReadFile(
        string path,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        Dictionary<string, TokenUsageTotals> usageByModel)
    {
        string? currentModel = null;

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
                currentModel = ExtractModel(root) ?? currentModel;
                if (!IsInsideWindow(root, windowStart, windowEnd) ||
                    !TryGetLastTokenUsage(root, out var tokenUsage))
                {
                    continue;
                }

                AddUsage(usageByModel, currentModel ?? "unknown-codex", new TokenUsageTotals(
                    GetInt64(tokenUsage, "input_tokens"),
                    GetInt64(tokenUsage, "cached_input_tokens"),
                    OutputTokens: GetInt64(tokenUsage, "output_tokens")));
            }
        }
    }

    private static bool TryGetLastTokenUsage(JsonElement root, out JsonElement usage)
    {
        usage = default;
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty("payload", out var payload) &&
               payload.ValueKind == JsonValueKind.Object &&
               payload.TryGetProperty("info", out var info) &&
               info.ValueKind == JsonValueKind.Object &&
               info.TryGetProperty("last_token_usage", out usage) &&
               usage.ValueKind == JsonValueKind.Object;
    }

    private static string? ExtractModel(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var direct = GetString(payload, "model");
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        if (payload.TryGetProperty("collaboration_mode", out var collaborationMode) &&
            collaborationMode.ValueKind == JsonValueKind.Object &&
            collaborationMode.TryGetProperty("settings", out var settings))
        {
            return GetString(settings, "model");
        }

        return null;
    }
}
