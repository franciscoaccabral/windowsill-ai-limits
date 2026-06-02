using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services;

public interface IUsageRefreshService : IDisposable
{
    event EventHandler<UsageSnapshot>? UsageUpdated;

    UsageSnapshot CurrentSnapshot { get; }

    TimeSpan CostRefreshInterval { get; }

    Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken = default);

    Task<UsageSnapshot> RefreshCostsAsync(CancellationToken cancellationToken = default);

    void StartMonitoring();

    void StopMonitoring();

    void UpdateRefreshInterval(TimeSpan refreshInterval);

    void UpdateCostRefreshInterval(TimeSpan refreshInterval);
}
