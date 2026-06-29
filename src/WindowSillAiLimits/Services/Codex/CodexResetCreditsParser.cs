using System.Text.Json;

using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.Codex;

public static class CodexResetCreditsParser
{
    public static IReadOnlyList<CodexResetCredit> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("credits", out var credits) || credits.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<CodexResetCredit>();
        foreach (var item in credits.EnumerateArray())
        {
            if (!item.TryGetProperty("status", out var status) ||
                !string.Equals(status.GetString(), "available", StringComparison.OrdinalIgnoreCase) ||
                !TryGetDate(item, "granted_at", out var grantedAt) ||
                !TryGetDate(item, "expires_at", out var expiresAt))
            {
                continue;
            }

            result.Add(new CodexResetCredit(grantedAt, expiresAt));
        }

        return result.OrderBy(credit => credit.ExpiresAt).ToArray();
    }

    private static bool TryGetDate(JsonElement item, string propertyName, out DateTimeOffset value)
    {
        value = default;
        return item.TryGetProperty(propertyName, out var property) &&
               DateTimeOffset.TryParse(property.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.RoundtripKind, out value);
    }
}
