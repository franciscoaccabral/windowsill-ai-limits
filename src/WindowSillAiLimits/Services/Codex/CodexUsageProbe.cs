using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.Codex;

public sealed class CodexUsageProbe(ICodexAppServerClient client, Func<DateTimeOffset>? clock = null) : IUsageProbe, IDisposable
{
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.Now);

    public UsageProvider Provider => UsageProvider.Codex;

    public void Dispose()
        => (client as IDisposable)?.Dispose();

    public async Task<ProviderUsage> ReadAsync(CancellationToken cancellationToken)
    {
        var now = _clock();

        try
        {
            await client.SendRequestAsync("initialize", CreateInitializeParams(), cancellationToken);
            var accountJson = await client.SendRequestAsync("account/read", EmptyParams(), cancellationToken);
            var rateLimitsJson = await client.SendRequestAsync("account/rateLimits/read", EmptyParams(), cancellationToken);

            return CodexRateLimitParser.Parse(accountJson, rateLimitsJson, now);
        }
        catch (CodexAppServerException ex) when (ex.CommandMissing)
        {
            return CodexRateLimitParser.NotInstalled(now, ex.Message);
        }
        catch (CodexAppServerException ex)
        {
            return CodexRateLimitParser.Unavailable(now, ex.Message);
        }
        catch (TimeoutException ex)
        {
            return CodexRateLimitParser.Unavailable(now, ex.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CodexRateLimitParser.Unavailable(now, "Codex app-server request timed out.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CodexRateLimitParser.Unavailable(now, ex.Message);
        }
    }

    private static object CreateInitializeParams()
        => new
        {
            clientInfo = new
            {
                name = "WindowSillAiLimits",
                version = "0.1.0",
            },
        };

    private static object EmptyParams()
        => new { };
}
