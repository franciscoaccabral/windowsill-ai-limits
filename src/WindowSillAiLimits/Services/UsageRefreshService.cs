using WindowSillAiLimits.Models;
using WindowSillAiLimits.Services.ApiCosts;

namespace WindowSillAiLimits.Services;

public sealed class UsageRefreshService : IUsageRefreshService
{
    // Quando um provider e rate-limitado (HTTP 429), pulamos a chamada de rede por um periodo
    // crescente para nao prolongar o bloqueio. O endpoint do Claude pode ficar preso em 429 por
    // horas, entao o cooldown escala 15 -> 30 -> 60 min e serve o ultimo snapshot em cache.
    private static readonly TimeSpan BaseRateLimitCooldown = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaxRateLimitCooldown = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan CostWindowMatchTolerance = TimeSpan.FromMinutes(5);

    private readonly IReadOnlyList<IUsageProbe> _probes;
    private readonly Func<DateTimeOffset> _clock;
    private readonly IUsageSnapshotCache? _snapshotCache;
    private readonly IApiCostEstimator? _apiCostEstimator;
    private readonly Lock _timerLock = new();
    private readonly Dictionary<UsageProvider, RateLimitState> _rateLimitCooldowns = [];

    private Timer? _timer;
    private Timer? _costTimer;
    private TimeSpan _refreshInterval;
    private TimeSpan _costRefreshInterval;
    private int _isRefreshing;
    private int _isRefreshingCosts;

    public UsageRefreshService(
        IReadOnlyList<IUsageProbe> probes,
        TimeSpan refreshInterval,
        Func<DateTimeOffset>? clock = null,
        IUsageSnapshotCache? snapshotCache = null,
        IApiCostEstimator? apiCostEstimator = null,
        TimeSpan? costRefreshInterval = null)
    {
        _probes = probes;
        _refreshInterval = refreshInterval;
        _costRefreshInterval = costRefreshInterval ?? TimeSpan.FromHours(4);
        _clock = clock ?? (() => DateTimeOffset.Now);
        _snapshotCache = snapshotCache;
        _apiCostEstimator = apiCostEstimator;
        CurrentSnapshot = _snapshotCache?.Read() ?? UsageSnapshot.Empty(_clock());
    }

    public event EventHandler<UsageSnapshot>? UsageUpdated;

    public UsageSnapshot CurrentSnapshot { get; private set; }

    public TimeSpan RefreshInterval => _refreshInterval;

    public TimeSpan CostRefreshInterval => _costRefreshInterval;

    public async Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _isRefreshing, 1, 0) != 0)
        {
            return CurrentSnapshot;
        }

        try
        {
            var providers = new List<ProviderUsage>(_probes.Count);

            var now = _clock();

            foreach (var probe in _probes)
            {
                if (IsInRateLimitCooldown(probe.Provider, now))
                {
                    providers.Add(CreateFailureProvider(probe.Provider, "Rate limit cooldown; serving cached usage.", isTransient: true));
                    continue;
                }

                try
                {
                    var usage = await probe.ReadAsync(cancellationToken);
                    UpdateRateLimitState(probe.Provider, usage, now);
                    providers.Add(PreserveApiCostEstimate(ApplyResultCachePolicy(usage)));
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    providers.Add(CreateFailureProvider(probe.Provider, "Usage query timed out.", isTransient: true));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    providers.Add(CreateFailureProvider(probe.Provider, ex.Message, IsTransientException(ex)));
                }
            }

            CurrentSnapshot = new UsageSnapshot(providers, now);
            _snapshotCache?.Write(CurrentSnapshot);
            UsageUpdated?.Invoke(this, CurrentSnapshot);
            return CurrentSnapshot;
        }
        finally
        {
            Volatile.Write(ref _isRefreshing, 0);
        }
    }

    public void StartMonitoring()
    {
        lock (_timerLock)
        {
            if (_timer is not null)
            {
                return;
            }

            _timer = new Timer(
                _ => _ = RefreshAsync(),
                null,
                _refreshInterval,
                _refreshInterval);
            _costTimer = new Timer(
                _ => _ = RefreshCostsAsync(),
                null,
                _costRefreshInterval,
                _costRefreshInterval);
            _ = RunInitialRefreshAsync();
        }
    }

    public void StopMonitoring()
    {
        lock (_timerLock)
        {
            _timer?.Dispose();
            _timer = null;
            _costTimer?.Dispose();
            _costTimer = null;
        }
    }

    public void UpdateRefreshInterval(TimeSpan refreshInterval)
    {
        lock (_timerLock)
        {
            _refreshInterval = refreshInterval;
            _timer?.Change(_refreshInterval, _refreshInterval);
        }
    }

    public void UpdateCostRefreshInterval(TimeSpan refreshInterval)
    {
        lock (_timerLock)
        {
            _costRefreshInterval = refreshInterval;
            _costTimer?.Change(_costRefreshInterval, _costRefreshInterval);
        }
    }

    public void Dispose()
    {
        StopMonitoring();

        // Encerra clientes/probes que mantem recursos externos (ex.: o processo codex app-server).
        foreach (var probe in _probes)
        {
            (probe as IDisposable)?.Dispose();
        }
    }

    private bool IsInRateLimitCooldown(UsageProvider provider, DateTimeOffset now)
        => _rateLimitCooldowns.TryGetValue(provider, out var state) && now < state.Until;

    private async Task RunInitialRefreshAsync()
    {
        try
        {
            var snapshot = await RefreshAsync();
            if (ShouldBackfillCosts(snapshot))
            {
                await RefreshCostsAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AiLimitsDiagnostics.Error("initial usage refresh failed", ex);
        }
    }

    public Task<UsageSnapshot> RefreshCostsAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _isRefreshingCosts, 1, 0) != 0)
        {
            return Task.FromResult(CurrentSnapshot);
        }

        try
        {
            if (_apiCostEstimator is null || CurrentSnapshot.Providers.Count == 0)
            {
                return Task.FromResult(CurrentSnapshot);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var providers = CurrentSnapshot.Providers
                .Select(provider => RefreshProviderCost(provider))
                .ToArray();

            CurrentSnapshot = new UsageSnapshot(providers, CurrentSnapshot.LastUpdated);
            _snapshotCache?.Write(CurrentSnapshot);
            UsageUpdated?.Invoke(this, CurrentSnapshot);
            return Task.FromResult(CurrentSnapshot);
        }
        finally
        {
            Volatile.Write(ref _isRefreshingCosts, 0);
        }
    }

    private void UpdateRateLimitState(UsageProvider provider, ProviderUsage usage, DateTimeOffset now)
    {
        if (IsRateLimited(usage))
        {
            var consecutive = _rateLimitCooldowns.TryGetValue(provider, out var previous) ? previous.Consecutive + 1 : 1;
            var ticks = Math.Min(
                BaseRateLimitCooldown.Ticks * (long)Math.Pow(2, consecutive - 1),
                MaxRateLimitCooldown.Ticks);
            _rateLimitCooldowns[provider] = new RateLimitState(now + TimeSpan.FromTicks(ticks), consecutive);
            return;
        }

        if (usage.Status is ProviderStatus.Ok or ProviderStatus.Warning)
        {
            _rateLimitCooldowns.Remove(provider);
        }
    }

    private static bool IsRateLimited(ProviderUsage usage)
        => usage.Status is ProviderStatus.Unavailable or ProviderStatus.Error &&
           !string.IsNullOrWhiteSpace(usage.Message) &&
           (usage.Message.Contains("429", StringComparison.Ordinal) ||
            usage.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
            usage.Message.Contains("rate_limit", StringComparison.OrdinalIgnoreCase));

    private sealed record RateLimitState(DateTimeOffset Until, int Consecutive);

    private bool ShouldBackfillCosts(UsageSnapshot snapshot)
        => _apiCostEstimator is not null &&
           snapshot.Providers.Any(provider =>
               provider.ApiCostEstimate is null &&
               provider.Status is ProviderStatus.Ok or ProviderStatus.Warning &&
               provider.Windows.Any(window =>
                   string.Equals(window.Id, "7d", StringComparison.OrdinalIgnoreCase) &&
                   window.StartedAt is not null &&
                   window.ResetsAt is not null));

    private ProviderUsage ApplyResultCachePolicy(ProviderUsage usage)
    {
        if (IsTransientResult(usage))
        {
            var previous = CurrentSnapshot.GetProvider(usage.Provider);
            if (CanReuseAsStale(previous))
            {
                return CreateStaleProvider(previous!, usage.Message ?? "Usage query failed.");
            }
        }

        return SanitizeUsageMessage(usage);
    }

    private ProviderUsage CreateFailureProvider(UsageProvider provider, string message, bool isTransient)
    {
        var previous = CurrentSnapshot.GetProvider(provider);
        if (isTransient && CanReuseAsStale(previous))
        {
            return CreateStaleProvider(previous!, message);
        }

        return CreateErrorProvider(provider, message);
    }

    private ProviderUsage CreateErrorProvider(UsageProvider provider, string message)
        => new(
            provider,
            provider == UsageProvider.Codex ? "OpenAI" : "Anthropic",
            null,
            ProviderStatus.Error,
            [],
            _clock(),
            UsageMessageSanitizer.Sanitize(message));

    private ProviderUsage CreateStaleProvider(ProviderUsage previous, string message)
        => previous with
        {
            Status = ProviderStatus.Stale,
            Message = $"Stale data: {UsageMessageSanitizer.Sanitize(message)}",
        };

    private ProviderUsage PreserveApiCostEstimate(ProviderUsage usage)
    {
        if (usage.ApiCostEstimate is not null)
        {
            return usage;
        }

        var previousEstimate = CurrentSnapshot.GetProvider(usage.Provider)?.ApiCostEstimate;
        if (previousEstimate is null || !MatchesWeeklyWindow(usage, previousEstimate))
        {
            return usage;
        }

        return usage with { ApiCostEstimate = previousEstimate };
    }

    private ProviderUsage RefreshProviderCost(ProviderUsage usage)
    {
        if (_apiCostEstimator is null ||
            usage.Status is not (ProviderStatus.Ok or ProviderStatus.Warning))
        {
            return usage;
        }

        try
        {
            var estimate = _apiCostEstimator.Estimate(usage);
            return estimate is null
                ? PreserveApiCostEstimate(usage)
                : usage with { ApiCostEstimate = estimate };
        }
        catch (IOException)
        {
            return PreserveApiCostEstimate(usage);
        }
        catch (UnauthorizedAccessException)
        {
            return PreserveApiCostEstimate(usage);
        }
        catch (System.Text.Json.JsonException)
        {
            return PreserveApiCostEstimate(usage);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return PreserveApiCostEstimate(usage);
        }
    }

    private static bool MatchesWeeklyWindow(ProviderUsage usage, ApiCostEstimate estimate)
    {
        var weekly = usage.Windows.FirstOrDefault(window => string.Equals(window.Id, "7d", StringComparison.OrdinalIgnoreCase));
        if (weekly?.StartedAt is null)
        {
            return false;
        }

        return IsCloseTo(weekly.StartedAt.Value, estimate.StartedAt, CostWindowMatchTolerance) &&
               weekly.ResetsAt is not null &&
               estimate.ResetsAt is not null &&
               IsCloseTo(weekly.ResetsAt.Value, estimate.ResetsAt.Value, CostWindowMatchTolerance);
    }

    private static bool IsCloseTo(DateTimeOffset left, DateTimeOffset right, TimeSpan tolerance)
        => (left - right).Duration() <= tolerance;

    private static ProviderUsage SanitizeUsageMessage(ProviderUsage usage)
        => string.IsNullOrWhiteSpace(usage.Message)
            ? usage
            : usage with { Message = UsageMessageSanitizer.Sanitize(usage.Message) };

    private static bool CanReuseAsStale(ProviderUsage? previous)
        => previous is not null &&
           previous.Windows.Count > 0 &&
           previous.Status is ProviderStatus.Ok or ProviderStatus.Warning or ProviderStatus.Stale;

    private static bool IsTransientException(Exception exception)
        => exception is TimeoutException or HttpRequestException or IOException ||
           IsTransientMessage(exception.Message);

    private static bool IsTransientResult(ProviderUsage usage)
        => usage.Status is ProviderStatus.Unavailable or ProviderStatus.Error &&
           IsTransientMessage(usage.Message);

    private static bool IsTransientMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("temporar", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("could not be reached", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("endpoint", StringComparison.OrdinalIgnoreCase);
    }

}
