using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.ApiCosts;

public interface ITokenUsageReader
{
    IReadOnlyDictionary<string, TokenUsageTotals> ReadUsage(DateTimeOffset windowStart, DateTimeOffset windowEnd);
}
