namespace WindowSillAiLimits.Models;

public sealed record UsagePacing(
    double UsedPercent,
    double ExpectedPercent,
    double DifferencePercentagePoints,
    double AverageDailyPacePercent,
    double ElapsedDays,
    DateTimeOffset? ResetsAt,
    DateTimeOffset? ProjectedExhaustionAt,
    ProjectedExhaustionStatus ProjectedExhaustionStatus);

public enum ProjectedExhaustionStatus
{
    Unavailable,
    BeforeReset,
    AfterReset,
}
