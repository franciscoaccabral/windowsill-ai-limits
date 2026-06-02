namespace WindowSillAiLimits.Models;

public sealed record ApiModelPrice(
    UsageProvider Provider,
    string ModelId,
    string DisplayName,
    decimal InputUsdPerMillion,
    decimal? CachedInputUsdPerMillion,
    decimal? CacheWriteFiveMinuteUsdPerMillion,
    decimal? CacheWriteOneHourUsdPerMillion,
    decimal OutputUsdPerMillion)
{
    public decimal CalculateUsd(TokenUsageTotals tokens)
        => Cost(tokens.InputTokens, InputUsdPerMillion) +
           Cost(tokens.CachedInputTokens, CachedInputUsdPerMillion) +
           Cost(tokens.CacheWriteFiveMinuteTokens, CacheWriteFiveMinuteUsdPerMillion) +
           Cost(tokens.CacheWriteOneHourTokens, CacheWriteOneHourUsdPerMillion) +
           Cost(tokens.OutputTokens, OutputUsdPerMillion);

    public string Summary
    {
        get
        {
            if (CacheWriteFiveMinuteUsdPerMillion is not null || CacheWriteOneHourUsdPerMillion is not null)
            {
                return $"in ${InputUsdPerMillion:0.###} / cache ${CachedInputUsdPerMillion:0.###} / write ${CacheWriteFiveMinuteUsdPerMillion:0.###}-${CacheWriteOneHourUsdPerMillion:0.###} / out ${OutputUsdPerMillion:0.###}";
            }

            return CachedInputUsdPerMillion is null
                ? $"in ${InputUsdPerMillion:0.###} / out ${OutputUsdPerMillion:0.###}"
                : $"in ${InputUsdPerMillion:0.###} / cache ${CachedInputUsdPerMillion:0.###} / out ${OutputUsdPerMillion:0.###}";
        }
    }

    private static decimal Cost(long tokens, decimal? usdPerMillion)
        => usdPerMillion is null || tokens <= 0 ? 0 : tokens / 1_000_000m * usdPerMillion.Value;
}
