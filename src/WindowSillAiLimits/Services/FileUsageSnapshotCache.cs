using System.Text.Json;

using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services;

public sealed class FileUsageSnapshotCache(string path) : IUsageSnapshotCache
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private readonly string _path = path;

    public UsageSnapshot? Read()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var dto = JsonSerializer.Deserialize<CachedSnapshot>(File.ReadAllText(_path), Options);
            if (dto is null || dto.Providers.Count == 0)
            {
                return null;
            }

            var providers = dto.Providers
                .Where(provider => provider.Windows.Count > 0)
                .Select(provider => new ProviderUsage(
                    provider.Provider,
                    provider.DisplayName,
                    provider.PlanLabel,
                    ProviderStatus.Stale,
                    provider.Windows.Select(window => new UsageWindow(
                        window.Id,
                        window.Label,
                        window.UsedPercent,
                        window.ResetsAt,
                        window.DurationSeconds is null ? null : TimeSpan.FromSeconds(window.DurationSeconds.Value),
                        window.StartedAt)).ToArray(),
                    provider.LastUpdated,
                    "Stale data from local cache; waiting for fresh usage.",
                    ToApiCostEstimate(provider.ApiCostEstimate)))
                .ToArray();

            return providers.Length == 0 ? null : new UsageSnapshot(providers, dto.LastUpdated);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Write(UsageSnapshot snapshot)
    {
        try
        {
            var providers = snapshot.Providers
                .Where(provider => provider.Windows.Count > 0)
                .Where(provider => provider.Status is ProviderStatus.Ok or ProviderStatus.Warning or ProviderStatus.Stale)
                .Select(provider => new CachedProvider(
                    provider.Provider,
                    provider.DisplayName,
                    provider.PlanLabel,
                    provider.Windows.Select(window => new CachedWindow(
                        window.Id,
                        window.Label,
                        window.UsedPercent,
                        window.ResetsAt,
                        window.Duration?.TotalSeconds,
                        window.StartedAt)).ToArray(),
                    provider.LastUpdated,
                    ToCachedApiCostEstimate(provider.ApiCostEstimate)))
                .ToArray();

            if (providers.Length == 0)
            {
                return;
            }

            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(new CachedSnapshot(snapshot.LastUpdated, providers), Options));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed record CachedSnapshot(DateTimeOffset LastUpdated, IReadOnlyList<CachedProvider> Providers);

    private sealed record CachedProvider(
        UsageProvider Provider,
        string DisplayName,
        string? PlanLabel,
        IReadOnlyList<CachedWindow> Windows,
        DateTimeOffset? LastUpdated,
        CachedApiCostEstimate? ApiCostEstimate);

    private sealed record CachedWindow(
        string Id,
        string Label,
        double? UsedPercent,
        DateTimeOffset? ResetsAt,
        double? DurationSeconds,
        DateTimeOffset? StartedAt);

    private sealed record CachedApiCostEstimate(
        string WindowId,
        string WindowLabel,
        DateTimeOffset StartedAt,
        DateTimeOffset? ResetsAt,
        IReadOnlyList<CachedModelCostLine> Lines,
        CachedTokenUsageTotals TotalTokens,
        decimal TotalCostUsd,
        DateTimeOffset CalculatedAt);

    private sealed record CachedModelCostLine(
        string ModelId,
        string DisplayName,
        CachedTokenUsageTotals Tokens,
        decimal? CostUsd,
        string PriceSummary);

    private sealed record CachedTokenUsageTotals(
        long InputTokens,
        long CachedInputTokens,
        long CacheWriteFiveMinuteTokens,
        long CacheWriteOneHourTokens,
        long OutputTokens);

    private static CachedApiCostEstimate? ToCachedApiCostEstimate(ApiCostEstimate? estimate)
        => estimate is null
            ? null
            : new CachedApiCostEstimate(
                estimate.WindowId,
                estimate.WindowLabel,
                estimate.StartedAt,
                estimate.ResetsAt,
                estimate.Lines.Select(line => new CachedModelCostLine(
                    line.ModelId,
                    line.DisplayName,
                    ToCachedTokenUsageTotals(line.Tokens),
                    line.CostUsd,
                    line.PriceSummary)).ToArray(),
                ToCachedTokenUsageTotals(estimate.TotalTokens),
                estimate.TotalCostUsd,
                estimate.CalculatedAt);

    private static ApiCostEstimate? ToApiCostEstimate(CachedApiCostEstimate? estimate)
        => estimate is null
            ? null
            : new ApiCostEstimate(
                estimate.WindowId,
                estimate.WindowLabel,
                estimate.StartedAt,
                estimate.ResetsAt,
                estimate.Lines.Select(line => new ModelCostLine(
                    line.ModelId,
                    line.DisplayName,
                    ToTokenUsageTotals(line.Tokens),
                    line.CostUsd,
                    line.PriceSummary)).ToArray(),
                ToTokenUsageTotals(estimate.TotalTokens),
                estimate.TotalCostUsd,
                estimate.CalculatedAt);

    private static CachedTokenUsageTotals ToCachedTokenUsageTotals(TokenUsageTotals totals)
        => new(
            totals.InputTokens,
            totals.CachedInputTokens,
            totals.CacheWriteFiveMinuteTokens,
            totals.CacheWriteOneHourTokens,
            totals.OutputTokens);

    private static TokenUsageTotals ToTokenUsageTotals(CachedTokenUsageTotals totals)
        => new(
            totals.InputTokens,
            totals.CachedInputTokens,
            totals.CacheWriteFiveMinuteTokens,
            totals.CacheWriteOneHourTokens,
            totals.OutputTokens);
}
