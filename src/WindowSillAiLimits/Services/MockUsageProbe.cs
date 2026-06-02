using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services;

public sealed class MockUsageProbe(UsageProvider provider, Func<DateTimeOffset> clock) : IUsageProbe
{
    public UsageProvider Provider => provider;

    public Task<ProviderUsage> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = clock();
        var usage = provider switch
        {
            UsageProvider.Codex => new ProviderUsage(
                UsageProvider.Codex,
                "OpenAI",
                "Codex Plus",
                ProviderStatus.Ok,
                [
                    new UsageWindow("5h", "5h", 100, now.AddHours(1).AddMinutes(12), TimeSpan.FromHours(5), now.AddHours(-3).AddMinutes(-48)),
                    new UsageWindow("7d", "7d", 59, now.AddDays(4).AddHours(6), TimeSpan.FromDays(7), now.AddDays(-2).AddHours(-18)),
                ],
                now,
                "Dados fictícios; probe local do Codex desativado nesta configuração."),
            UsageProvider.Claude => new ProviderUsage(
                UsageProvider.Claude,
                "Anthropic",
                "Claude Code Max",
                ProviderStatus.Warning,
                [
                    new UsageWindow("5h", "5h", 70, now.AddHours(2).AddMinutes(20), TimeSpan.FromHours(5), now.AddHours(-2).AddMinutes(-40)),
                    new UsageWindow("7d", "7d", 50, now.AddDays(3).AddHours(20), TimeSpan.FromDays(7), now.AddDays(-3).AddHours(-4)),
                    new UsageWindow("seven_day_sonnet", "Sonnet", 34, now.AddDays(3).AddHours(20), TimeSpan.FromDays(7), now.AddDays(-3).AddHours(-4)),
                ],
                now,
                "Dados fictícios; probe local do Claude desativado nesta configuração."),
            _ => throw new InvalidOperationException($"Unsupported provider: {provider}"),
        };

        return Task.FromResult(usage);
    }

    public static IUsageProbe[] CreateDefault(Func<DateTimeOffset> clock)
        => [new MockUsageProbe(UsageProvider.Codex, clock), new MockUsageProbe(UsageProvider.Claude, clock)];
}
