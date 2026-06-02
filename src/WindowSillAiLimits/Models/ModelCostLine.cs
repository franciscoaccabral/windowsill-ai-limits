namespace WindowSillAiLimits.Models;

public sealed record ModelCostLine(
    string ModelId,
    string DisplayName,
    TokenUsageTotals Tokens,
    decimal? CostUsd,
    string PriceSummary)
{
    public bool HasPrice => CostUsd is not null;
}
