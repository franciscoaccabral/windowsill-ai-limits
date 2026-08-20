using System.Globalization;
using System.Text.Json;

using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.Codex;

public static class CodexRateLimitParser
{
    public static ProviderUsage Parse(string accountJson, string rateLimitsJson, DateTimeOffset now)
    {
        using var accountDocument = JsonDocument.Parse(accountJson);
        using var rateLimitsDocument = JsonDocument.Parse(rateLimitsJson);

        if (TryGetJsonRpcError(accountDocument.RootElement, out var accountErrorMessage))
        {
            return Unavailable(now, accountErrorMessage);
        }

        if (TryGetJsonRpcError(rateLimitsDocument.RootElement, out var errorMessage))
        {
            return Unavailable(now, errorMessage);
        }

        var planLabel = ExtractPlanLabel(accountDocument.RootElement);
        var windows = ExtractWindows(rateLimitsDocument.RootElement, now);
        var status = GetStatus(windows);

        return new ProviderUsage(
            UsageProvider.Codex,
            "OpenAI",
            planLabel,
            status,
            windows,
            now,
            windows.Count == 0 ? "Codex rate limit data unavailable." : null);
    }

    public static ProviderUsage Unavailable(DateTimeOffset now, string message)
        => new(
            UsageProvider.Codex,
            "OpenAI",
            null,
            ProviderStatus.Unavailable,
            [],
            now,
            UsageMessageSanitizer.Sanitize(message));

    public static ProviderUsage NotInstalled(DateTimeOffset now, string message)
        => new(
            UsageProvider.Codex,
            "OpenAI",
            null,
            ProviderStatus.NotInstalled,
            [],
            now,
            UsageMessageSanitizer.Sanitize(message));

    private static IReadOnlyList<UsageWindow> ExtractWindows(JsonElement root, DateTimeOffset now)
    {
        var result = GetPropertyOrDefault(root, "result");
        var rateLimits = GetPropertyOrDefault(result, "rateLimits");
        var windows = new List<UsageWindow>();

        AddNamedWindow(windows, rateLimits, "primary", "5h", now);
        AddNamedWindow(windows, rateLimits, "secondary", "7d", now);

        if (windows.Count > 0)
        {
            return windows;
        }

        var byLimitId = GetPropertyOrDefault(result, "rateLimitsByLimitId");
        if (byLimitId.ValueKind != JsonValueKind.Object)
        {
            byLimitId = GetPropertyOrDefault(rateLimits, "rateLimitsByLimitId");
        }

        if (byLimitId.ValueKind == JsonValueKind.Object)
        {
            foreach (var limit in byLimitId.EnumerateObject())
            {
                var primary = GetPropertyOrDefault(limit.Value, "primary");
                var secondary = GetPropertyOrDefault(limit.Value, "secondary");

                if (primary.ValueKind == JsonValueKind.Object || secondary.ValueKind == JsonValueKind.Object)
                {
                    AddNamedWindow(windows, limit.Value, "primary", "5h", now);
                    AddNamedWindow(windows, limit.Value, "secondary", "7d", now);
                }
                else
                {
                    var id = NormalizeWindowId(limit.Name, limit.Value);
                    var label = id == "7d" ? "7d" : "5h";
                    windows.Add(CreateWindow(limit.Value, id, label, now));
                }
            }
        }

        return windows
            .OrderBy(window => window.Id == "5h" ? 0 : 1)
            .ToArray();
    }

    private static void AddNamedWindow(List<UsageWindow> windows, JsonElement rateLimits, string sourceName, string fallbackId, DateTimeOffset now)
    {
        var source = GetPropertyOrDefault(rateLimits, sourceName);
        if (source.ValueKind == JsonValueKind.Object)
        {
            var normalizedId = NormalizeWindowId(fallbackId, source);
            windows.Add(CreateWindow(source, normalizedId, normalizedId, now));
        }
    }

    private static UsageWindow CreateWindow(JsonElement source, string id, string label, DateTimeOffset now)
    {
        var duration = TryGetDurationMinutes(source, out var minutes)
            ? TimeSpan.FromMinutes(minutes)
            : id == "7d"
                ? TimeSpan.FromDays(7)
                : TimeSpan.FromHours(5);
        DateTimeOffset? resetsAt = TryGetDateTimeOffset(source, "resetsAt", out var reset) ? reset : null;

        return new UsageWindow(
            id,
            label,
            TryGetDouble(source, "usedPercent", out var usedPercent) ? usedPercent : null,
            resetsAt,
            duration,
            resetsAt is null ? null : resetsAt.Value - duration);
    }

    private static string NormalizeWindowId(string limitId, JsonElement source)
    {
        var name = limitId.ToLowerInvariant();
        if (name.Contains("7d", StringComparison.Ordinal) ||
            name.Contains("week", StringComparison.Ordinal) ||
            name.Contains("weekly", StringComparison.Ordinal) ||
            (TryGetDurationMinutes(source, out var minutes) && minutes >= 10080))
        {
            return "7d";
        }

        return "5h";
    }

    private static ProviderStatus GetStatus(IReadOnlyList<UsageWindow> windows)
    {
        if (windows.Count == 0)
        {
            return ProviderStatus.Unavailable;
        }

        return windows.Any(window => window.UsedPercent >= 75)
            ? ProviderStatus.Warning
            : ProviderStatus.Ok;
    }

    private static string? ExtractPlanLabel(JsonElement root)
    {
        var result = GetPropertyOrDefault(root, "result");
        var account = GetPropertyOrDefault(result, "account");

        foreach (var propertyName in new[] { "plan", "planType", "plan_type" })
        {
            if (TryGetString(account, propertyName, out var value) || TryGetString(result, propertyName, out value))
            {
                return value;
            }
        }

        var subscription = GetPropertyOrDefault(account, "subscriptionPlan");
        return TryGetString(subscription, "name", out var subscriptionName) ? subscriptionName : null;
    }

    private static bool TryGetJsonRpcError(JsonElement root, out string message)
    {
        var error = GetPropertyOrDefault(root, "error");
        if (error.ValueKind == JsonValueKind.Object && TryGetString(error, "message", out var errorMessage))
        {
            message = errorMessage ?? "Codex returned an unknown JSON-RPC error.";
            return true;
        }

        message = string.Empty;
        return false;
    }

    private static JsonElement GetPropertyOrDefault(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property)
            ? property
            : default;

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

    private static bool TryGetDurationMinutes(JsonElement element, out double value)
        => TryGetDouble(element, "durationMinutes", out value) ||
           TryGetDouble(element, "windowDurationMins", out value);

    private static bool TryGetDateTimeOffset(JsonElement element, string propertyName, out DateTimeOffset value)
    {
        var property = GetPropertyOrDefault(element, propertyName);
        if (property.ValueKind == JsonValueKind.String)
        {
            return DateTimeOffset.TryParse(property.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value);
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var unixSeconds))
        {
            value = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            return true;
        }

        value = default;
        return false;
    }
}
