namespace WindowSillAiLimits.Models;

public sealed record UsageSnapshot(IReadOnlyList<ProviderUsage> Providers, DateTimeOffset LastUpdated)
{
    public static UsageSnapshot Empty(DateTimeOffset now)
        => new([], now);

    public ProviderUsage? GetProvider(UsageProvider provider)
        => Providers.FirstOrDefault(candidate => candidate.Provider == provider);
}
