namespace WindowSillAiLimits.Models;

public sealed record ApiCostEstimate(
    string WindowId,
    string WindowLabel,
    DateTimeOffset StartedAt,
    DateTimeOffset? ResetsAt,
    IReadOnlyList<ModelCostLine> Lines,
    TokenUsageTotals TotalTokens,
    decimal TotalCostUsd,
    DateTimeOffset CalculatedAt)
{
    public bool HasUsage => Lines.Count > 0;
}
