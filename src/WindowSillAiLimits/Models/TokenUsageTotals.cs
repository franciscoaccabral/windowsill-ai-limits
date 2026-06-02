namespace WindowSillAiLimits.Models;

public sealed record TokenUsageTotals(
    long InputTokens = 0,
    long CachedInputTokens = 0,
    long CacheWriteFiveMinuteTokens = 0,
    long CacheWriteOneHourTokens = 0,
    long OutputTokens = 0)
{
    public long TotalTokens
        => InputTokens + CachedInputTokens + CacheWriteFiveMinuteTokens + CacheWriteOneHourTokens + OutputTokens;

    public static TokenUsageTotals Empty { get; } = new();

    public TokenUsageTotals Add(TokenUsageTotals other)
        => new(
            InputTokens + other.InputTokens,
            CachedInputTokens + other.CachedInputTokens,
            CacheWriteFiveMinuteTokens + other.CacheWriteFiveMinuteTokens,
            CacheWriteOneHourTokens + other.CacheWriteOneHourTokens,
            OutputTokens + other.OutputTokens);
}
