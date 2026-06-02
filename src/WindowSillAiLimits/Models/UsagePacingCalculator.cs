namespace WindowSillAiLimits.Models;

public static class UsagePacingCalculator
{
    public static UsagePacing Calculate(UsageWindow window, DateTimeOffset now)
    {
        var usedPercent = Clamp(window.UsedPercent ?? 0);
        var elapsed = GetElapsed(window, now);
        var duration = window.Duration ?? TimeSpan.Zero;
        var expectedPercent = duration > TimeSpan.Zero
            ? Clamp(elapsed.TotalMilliseconds / duration.TotalMilliseconds * 100)
            : 0;
        var elapsedDays = Math.Max(elapsed.TotalDays, 0);
        var averageDailyPace = elapsedDays > 0 ? usedPercent / elapsedDays : 0;
        var projectedExhaustionAt = GetProjectedExhaustionAt(usedPercent, averageDailyPace, now);
        var projectedExhaustionStatus = GetProjectedExhaustionStatus(projectedExhaustionAt, window.ResetsAt);

        return new UsagePacing(
            usedPercent,
            expectedPercent,
            usedPercent - expectedPercent,
            averageDailyPace,
            elapsedDays,
            window.ResetsAt,
            projectedExhaustionAt,
            projectedExhaustionStatus);
    }

    private static TimeSpan GetElapsed(UsageWindow window, DateTimeOffset now)
    {
        if (window.StartedAt is not null)
        {
            return now - window.StartedAt.Value;
        }

        if (window.Duration is not null && window.ResetsAt is not null)
        {
            return window.Duration.Value - (window.ResetsAt.Value - now);
        }

        return TimeSpan.Zero;
    }

    private static DateTimeOffset? GetProjectedExhaustionAt(double usedPercent, double averageDailyPace, DateTimeOffset now)
    {
        if (usedPercent >= 100)
        {
            return now;
        }

        if (averageDailyPace <= 0)
        {
            return null;
        }

        var remainingPercent = 100 - usedPercent;
        return RoundToNearestMinute(now.AddDays(remainingPercent / averageDailyPace));
    }

    private static ProjectedExhaustionStatus GetProjectedExhaustionStatus(DateTimeOffset? projectedExhaustionAt, DateTimeOffset? resetsAt)
    {
        if (projectedExhaustionAt is null || resetsAt is null)
        {
            return ProjectedExhaustionStatus.Unavailable;
        }

        return projectedExhaustionAt.Value <= resetsAt.Value
            ? ProjectedExhaustionStatus.BeforeReset
            : ProjectedExhaustionStatus.AfterReset;
    }

    private static DateTimeOffset RoundToNearestMinute(DateTimeOffset value)
    {
        var remainder = value.Ticks % TimeSpan.TicksPerMinute;
        return remainder >= TimeSpan.TicksPerMinute / 2
            ? value.AddTicks(TimeSpan.TicksPerMinute - remainder)
            : value.AddTicks(-remainder);
    }

    private static double Clamp(double value)
        => Math.Max(0, Math.Min(100, value));
}
