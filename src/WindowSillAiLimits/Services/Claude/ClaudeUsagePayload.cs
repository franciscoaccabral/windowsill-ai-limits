namespace WindowSillAiLimits.Services.Claude;

public sealed record ClaudeUsagePayload(
    string UsageJson,
    string? PlanLabel,
    IReadOnlyDictionary<string, string>? RateLimitHeaders = null);
