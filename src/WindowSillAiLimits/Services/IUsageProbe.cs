using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services;

public interface IUsageProbe
{
    UsageProvider Provider { get; }

    Task<ProviderUsage> ReadAsync(CancellationToken cancellationToken);
}
