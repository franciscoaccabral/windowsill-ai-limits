using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.ApiCosts;

public sealed class ApiCostEstimator : IApiCostEstimator
{
    private readonly ApiCostCatalog _catalog;
    private readonly ITokenUsageReader _codexReader;
    private readonly ITokenUsageReader _claudeReader;
    private readonly Func<DateTimeOffset> _clock;

    public ApiCostEstimator(
        ApiCostCatalog? catalog = null,
        ITokenUsageReader? codexReader = null,
        ITokenUsageReader? claudeReader = null,
        Func<DateTimeOffset>? clock = null)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _catalog = catalog ?? new ApiCostCatalog();
        _codexReader = codexReader ?? new CodexTokenUsageReader(System.IO.Path.Combine(userProfile, ".codex", "sessions"));
        _claudeReader = claudeReader ?? new ClaudeTokenUsageReader(System.IO.Path.Combine(userProfile, ".claude", "projects"));
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public ApiCostEstimate? Estimate(ProviderUsage provider)
    {
        var weekly = provider.Windows.FirstOrDefault(window => string.Equals(window.Id, "7d", StringComparison.OrdinalIgnoreCase));
        if (weekly?.StartedAt is null || weekly.ResetsAt is null)
        {
            return null;
        }

        var reader = provider.Provider == UsageProvider.Codex ? _codexReader : _claudeReader;
        var observed = reader.ReadUsage(weekly.StartedAt.Value, weekly.ResetsAt.Value);
        if (observed.Count == 0)
        {
            return null;
        }

        var lines = observed
            .OrderByDescending(pair => pair.Value.TotalTokens)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => BuildLine(provider.Provider, pair.Key, pair.Value))
            .ToArray();

        var pricedLines = lines.Where(line => line.CostUsd is not null).ToArray();
        return new ApiCostEstimate(
            weekly.Id,
            weekly.Label,
            weekly.StartedAt.Value,
            weekly.ResetsAt,
            lines,
            pricedLines.Aggregate(TokenUsageTotals.Empty, (total, line) => total.Add(line.Tokens)),
            pricedLines.Sum(line => line.CostUsd!.Value),
            _clock());
    }

    private ModelCostLine BuildLine(UsageProvider provider, string modelId, TokenUsageTotals tokens)
    {
        var price = _catalog.Find(provider, modelId);
        if (price is null)
        {
            return new ModelCostLine(modelId, modelId, tokens, null, "sem preço");
        }

        return new ModelCostLine(modelId, price.DisplayName, tokens, price.CalculateUsd(tokens), price.Summary);
    }
}
