namespace WindowSillAiLimits.Models;

public sealed record UsageWindow(
    string Id,
    string Label,
    double? UsedPercent,
    DateTimeOffset? ResetsAt,
    TimeSpan? Duration,
    DateTimeOffset? StartedAt);
