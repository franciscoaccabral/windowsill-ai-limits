using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.Claude;

public sealed class ClaudeUsageProbe(IClaudeUsageClient client, Func<DateTimeOffset>? clock = null) : IUsageProbe
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.Now);

    public UsageProvider Provider => UsageProvider.Claude;

    public async Task<ProviderUsage> ReadAsync(CancellationToken cancellationToken)
    {
        var now = _clock();

        try
        {
            var payload = await client.ReadUsageAsync(cancellationToken);
            return ClaudeRateLimitParser.Parse(payload.UsageJson, payload.PlanLabel, now, payload.RateLimitHeaders);
        }
        catch (ClaudeUsageException ex) when (ex.NotConfigured)
        {
            return ClaudeRateLimitParser.NotInstalled(now, ex.Message);
        }
        catch (ClaudeUsageException ex)
        {
            return ClaudeRateLimitParser.Unavailable(now, ex.Message);
        }
        catch (TimeoutException ex)
        {
            return ClaudeRateLimitParser.Unavailable(now, ex.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ClaudeRateLimitParser.Unavailable(now, "Claude usage request timed out.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ClaudeRateLimitParser.Unavailable(now, ex.Message);
        }
    }
}
