using System.Globalization;
using System.Text.Json;

using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.Claude;

public static class ClaudeRateLimitParser
{
    public static ProviderUsage Parse(
        string usageJson,
        string? planLabel,
        DateTimeOffset now,
        IReadOnlyDictionary<string, string>? rateLimitHeaders = null)
    {
        using var document = JsonDocument.Parse(usageJson);
        var root = document.RootElement;

        if (TryGetError(root, out var errorMessage))
        {
            return Unavailable(now, errorMessage);
        }

        var source = GetPropertyOrDefault(root, "rate_limits");
        var isStatusLine = source.ValueKind == JsonValueKind.Object;
        if (!isStatusLine)
        {
            source = root;
        }

        var windows = new List<UsageWindow>();
        AddWindow(windows, source, isStatusLine ? "five_hour" : "five_hour", "5h", "5h", TimeSpan.FromHours(5), now);
        AddWindow(windows, source, "seven_day", "7d", "7d", TimeSpan.FromDays(7), now);
        AddWindow(windows, source, "seven_day_sonnet", "seven_day_sonnet", "Sonnet", TimeSpan.FromDays(7), now);
        AddWindow(windows, source, "seven_day_opus", "seven_day_opus", "Opus", TimeSpan.FromDays(7), now);
        AddExtraUsage(windows, source, now);
        AddUnifiedHeaderWindows(windows, rateLimitHeaders, now);

        if (windows.Count == 0)
        {
            return new ProviderUsage(
                UsageProvider.Claude,
                "Anthropic",
                planLabel,
                ProviderStatus.Unavailable,
                [],
                now,
                "Claude usage data unavailable.");
        }

        return new ProviderUsage(
            UsageProvider.Claude,
            "Anthropic",
            planLabel,
            windows.Any(window => window.UsedPercent >= 75) ? ProviderStatus.Warning : ProviderStatus.Ok,
            windows,
            now,
            null);
    }

    private static void AddUnifiedHeaderWindows(
        List<UsageWindow> windows,
        IReadOnlyDictionary<string, string>? headers,
        DateTimeOffset now)
    {
        if (headers is null || headers.Count == 0)
        {
            return;
        }

        AddUnifiedHeaderWindow(windows, headers, "5h", "5h", "5h", TimeSpan.FromHours(5), now);
        AddUnifiedHeaderWindow(windows, headers, "7d", "7d", "7d", TimeSpan.FromDays(7), now);
    }

    private static void AddUnifiedHeaderWindow(
        List<UsageWindow> windows,
        IReadOnlyDictionary<string, string> headers,
        string headerName,
        string id,
        string label,
        TimeSpan duration,
        DateTimeOffset now)
    {
        if (!TryGetHeaderDouble(headers, $"anthropic-ratelimit-unified-{headerName}-utilization", out var utilization) ||
            !TryGetHeaderDouble(headers, $"anthropic-ratelimit-unified-{headerName}-reset", out var resetSeconds))
        {
            return;
        }

        var usedPercent = utilization <= 1 ? utilization * 100 : utilization;
        var resetsAt = DateTimeOffset.FromUnixTimeSeconds((long)resetSeconds);
        windows.RemoveAll(window => string.Equals(window.Id, id, StringComparison.OrdinalIgnoreCase));
        windows.Add(new UsageWindow(
            id,
            label,
            Math.Clamp(usedPercent, 0, 100),
            resetsAt,
            duration,
            resetsAt - duration));
    }

    public static ProviderUsage Unavailable(DateTimeOffset now, string message)
        => new(
            UsageProvider.Claude,
            "Anthropic",
            null,
            ProviderStatus.Unavailable,
            [],
            now,
            UsageMessageSanitizer.Sanitize(message));

    public static ProviderUsage NotInstalled(DateTimeOffset now, string message)
        => new(
            UsageProvider.Claude,
            "Anthropic",
            null,
            ProviderStatus.NotInstalled,
            [],
            now,
            UsageMessageSanitizer.Sanitize(message));

    private static void AddWindow(
        List<UsageWindow> windows,
        JsonElement source,
        string sourceName,
        string id,
        string label,
        TimeSpan duration,
        DateTimeOffset now)
    {
        var window = GetPropertyOrDefault(source, sourceName);
        if (window.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!TryGetDouble(window, "utilization", out var usedPercent) &&
            !TryGetDouble(window, "used_percentage", out usedPercent) &&
            !TryGetDouble(window, "usedPercent", out usedPercent))
        {
            usedPercent = 0;
        }

        DateTimeOffset? resetsAt = TryGetDateTimeOffset(window, "resets_at", out var reset) ||
                                   TryGetDateTimeOffset(window, "resetsAt", out reset)
            ? reset
            : null;

        windows.Add(new UsageWindow(
            id,
            label,
            usedPercent,
            resetsAt,
            duration,
            resetsAt is null ? null : resetsAt.Value - duration));
    }

    private static void AddExtraUsage(List<UsageWindow> windows, JsonElement source, DateTimeOffset now)
    {
        var extra = GetPropertyOrDefault(source, "extra_usage");
        if (extra.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var enabled = TryGetBool(extra, "is_enabled", out var value) && value;
        if (!enabled ||
            !TryGetDouble(extra, "monthly_limit", out var limit) ||
            limit <= 0)
        {
            return;
        }

        _ = TryGetDouble(extra, "used_credits", out var spent);
        var percent = Math.Clamp(spent * 100 / limit, 0, 100);

        windows.Add(new UsageWindow(
            "extra_usage",
            "Extra",
            percent,
            null,
            null,
            now));
    }

    private static bool TryGetError(JsonElement root, out string message)
    {
        var error = GetPropertyOrDefault(root, "error");
        if (error.ValueKind == JsonValueKind.Object)
        {
            if (TryGetString(error, "message", out var errorMessage))
            {
                message = errorMessage ?? "Claude returned an unknown usage error.";
                return true;
            }

            if (TryGetString(error, "type", out var errorType))
            {
                message = errorType ?? "Claude returned an unknown usage error.";
                return true;
            }
        }

        message = string.Empty;
        return false;
    }

    private static JsonElement GetPropertyOrDefault(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property)
            ? property
            : default;

    private static bool TryGetBool(JsonElement element, string propertyName, out bool value)
    {
        var property = GetPropertyOrDefault(element, propertyName);
        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = property.GetBoolean();
            return true;
        }

        value = false;
        return false;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        var property = GetPropertyOrDefault(element, propertyName);
        if (property.ValueKind == JsonValueKind.String)
        {
            var text = property.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                value = text;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryGetDouble(JsonElement element, string propertyName, out double value)
    {
        var property = GetPropertyOrDefault(element, propertyName);
        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetDouble(out value);
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        value = 0;
        return false;
    }

    private static bool TryGetHeaderDouble(IReadOnlyDictionary<string, string> headers, string headerName, out double value)
    {
        if (headers.TryGetValue(headerName, out var text))
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        value = 0;
        return false;
    }

    private static bool TryGetDateTimeOffset(JsonElement element, string propertyName, out DateTimeOffset value)
    {
        var property = GetPropertyOrDefault(element, propertyName);
        if (property.ValueKind == JsonValueKind.String)
        {
            return DateTimeOffset.TryParse(property.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value);
        }

        value = default;
        return false;
    }
}
