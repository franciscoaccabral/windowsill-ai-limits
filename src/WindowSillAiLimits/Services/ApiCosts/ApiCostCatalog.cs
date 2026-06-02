using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.ApiCosts;

public sealed class ApiCostCatalog
{
    // Prices pinned from official provider pages checked on 2026-05-26:
    // OpenAI API pricing/model docs, OpenAI Codex rate card, and Anthropic pricing.
    // Keep this offline on purpose; new models or price changes should update this source catalog.
    private readonly IReadOnlyList<ApiModelPrice> _prices =
    [
        new(UsageProvider.Codex, "gpt-5.5", "GPT-5.5", 5.00m, 0.50m, null, null, 30.00m),
        new(UsageProvider.Codex, "gpt-5.5-pro", "GPT-5.5 Pro", 30.00m, null, null, null, 180.00m),
        new(UsageProvider.Codex, "gpt-5.4", "GPT-5.4", 2.50m, 0.25m, null, null, 15.00m),
        new(UsageProvider.Codex, "gpt-5.3-codex", "GPT-5.3-Codex", 1.75m, 0.175m, null, null, 14.00m),
        new(UsageProvider.Codex, "gpt-5.2-codex", "GPT-5.2-Codex", 1.75m, 0.175m, null, null, 14.00m),
        new(UsageProvider.Codex, "gpt-5.1-codex", "GPT-5.1-Codex", 1.25m, 0.125m, null, null, 10.00m),
        new(UsageProvider.Codex, "gpt-5-codex", "GPT-5-Codex", 1.25m, 0.125m, null, null, 10.00m),
        new(UsageProvider.Codex, "gpt-5.1-codex-mini", "GPT-5.1-Codex Mini", 0.25m, 0.025m, null, null, 2.00m),

        new(UsageProvider.Claude, "claude-opus-4.7", "Claude Opus 4.7", 5.00m, 0.50m, 6.25m, 10.00m, 25.00m),
        new(UsageProvider.Claude, "claude-opus-4.6", "Claude Opus 4.6", 5.00m, 0.50m, 6.25m, 10.00m, 25.00m),
        new(UsageProvider.Claude, "claude-opus-4.5", "Claude Opus 4.5", 5.00m, 0.50m, 6.25m, 10.00m, 25.00m),
        new(UsageProvider.Claude, "claude-sonnet-4.6", "Claude Sonnet 4.6", 3.00m, 0.30m, 3.75m, 6.00m, 15.00m),
        new(UsageProvider.Claude, "claude-sonnet-4.5", "Claude Sonnet 4.5", 3.00m, 0.30m, 3.75m, 6.00m, 15.00m),
        new(UsageProvider.Claude, "claude-haiku-4.5", "Claude Haiku 4.5", 1.00m, 0.10m, 1.25m, 2.00m, 5.00m),
    ];

    public ApiModelPrice? Find(UsageProvider provider, string modelId)
    {
        var normalized = Normalize(provider, modelId);
        return _prices.FirstOrDefault(price =>
            price.Provider == provider &&
            string.Equals(price.ModelId, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static string Normalize(UsageProvider provider, string modelId)
    {
        var value = modelId.Trim().ToLowerInvariant().Replace('_', '-');

        if (provider == UsageProvider.Claude)
        {
            return value switch
            {
                var candidate when candidate.Contains("opus-4-7", StringComparison.Ordinal) => "claude-opus-4.7",
                var candidate when candidate.Contains("opus-4.7", StringComparison.Ordinal) => "claude-opus-4.7",
                var candidate when candidate.Contains("opus-4-6", StringComparison.Ordinal) => "claude-opus-4.6",
                var candidate when candidate.Contains("opus-4.6", StringComparison.Ordinal) => "claude-opus-4.6",
                var candidate when candidate.Contains("opus-4-5", StringComparison.Ordinal) => "claude-opus-4.5",
                var candidate when candidate.Contains("opus-4.5", StringComparison.Ordinal) => "claude-opus-4.5",
                var candidate when candidate.Contains("sonnet-4-6", StringComparison.Ordinal) => "claude-sonnet-4.6",
                var candidate when candidate.Contains("sonnet-4.6", StringComparison.Ordinal) => "claude-sonnet-4.6",
                var candidate when candidate.Contains("sonnet-4-5", StringComparison.Ordinal) => "claude-sonnet-4.5",
                var candidate when candidate.Contains("sonnet-4.5", StringComparison.Ordinal) => "claude-sonnet-4.5",
                var candidate when candidate.Contains("haiku-4-5", StringComparison.Ordinal) => "claude-haiku-4.5",
                var candidate when candidate.Contains("haiku-4.5", StringComparison.Ordinal) => "claude-haiku-4.5",
                _ => value,
            };
        }

        return value switch
        {
            var candidate when candidate.StartsWith("gpt-5.5-pro", StringComparison.Ordinal) => "gpt-5.5-pro",
            var candidate when candidate.StartsWith("gpt-5.5", StringComparison.Ordinal) => "gpt-5.5",
            var candidate when candidate.StartsWith("gpt-5.4", StringComparison.Ordinal) => "gpt-5.4",
            var candidate when candidate.StartsWith("gpt-5.3-codex", StringComparison.Ordinal) => "gpt-5.3-codex",
            var candidate when candidate.StartsWith("gpt-5.2-codex", StringComparison.Ordinal) => "gpt-5.2-codex",
            var candidate when candidate.StartsWith("gpt-5.1-codex-mini", StringComparison.Ordinal) => "gpt-5.1-codex-mini",
            var candidate when candidate.StartsWith("gpt-5.1-codex", StringComparison.Ordinal) => "gpt-5.1-codex",
            var candidate when candidate.StartsWith("gpt-5-codex", StringComparison.Ordinal) => "gpt-5-codex",
            _ => value,
        };
    }
}
