using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows.Input;

using Microsoft.UI.Dispatching;

using WindowSillAiLimits.Models;
using WindowSillAiLimits.Services;

namespace WindowSillAiLimits.ViewModels;

public sealed class AiLimitsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IUsageRefreshService _refreshService;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Action<Action> _dispatchToUi;
    private bool _disposed;
    private bool _isApiCostsExpanded;

    public AiLimitsViewModel(
        IUsageRefreshService refreshService,
        Func<DateTimeOffset>? clock = null,
        Action<Action>? dispatchToUi = null)
    {
        _refreshService = refreshService;
        _clock = clock ?? (() => DateTimeOffset.Now);
        _dispatchToUi = dispatchToUi ?? CreateUiDispatcher();
        Snapshot = refreshService.CurrentSnapshot;
        RefreshCommand = new AsyncCommand(() => _refreshService.RefreshAsync());
        CostRefreshCommand = new AsyncCommand(() => _refreshService.RefreshCostsAsync());
        ToggleApiCostsCommand = new AsyncCommand(() =>
        {
            IsApiCostsExpanded = !IsApiCostsExpanded;
            return Task.CompletedTask;
        });
        _refreshService.UsageUpdated += OnUsageUpdated;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UsageSnapshot Snapshot { get; private set; }

    public ICommand RefreshCommand { get; }

    public ICommand CostRefreshCommand { get; }

    public ICommand ToggleApiCostsCommand { get; }

    public bool IsApiCostsExpanded
    {
        get => _isApiCostsExpanded;
        private set
        {
            if (_isApiCostsExpanded == value)
            {
                return;
            }

            _isApiCostsExpanded = value;
            OnPropertyChanged();
        }
    }

    public string OpenAiFiveHourText => GetWindowText(UsageProvider.Codex, "5h");

    public string OpenAiSevenDayText => GetWindowText(UsageProvider.Codex, "7d");

    public string ClaudeFiveHourText => GetWindowText(UsageProvider.Claude, "5h");

    public string ClaudeSevenDayText => GetWindowText(UsageProvider.Claude, "7d");

    public ProviderStatus OpenAiStatus => GetStatus(UsageProvider.Codex);

    public ProviderStatus ClaudeStatus => GetStatus(UsageProvider.Claude);

    public LimitSeverity OpenAiFiveHourSeverity => GetWindowSeverity(UsageProvider.Codex, "5h");

    public LimitSeverity OpenAiSevenDaySeverity => GetWindowSeverity(UsageProvider.Codex, "7d");

    public LimitSeverity ClaudeFiveHourSeverity => GetWindowSeverity(UsageProvider.Claude, "5h");

    public LimitSeverity ClaudeSevenDaySeverity => GetWindowSeverity(UsageProvider.Claude, "7d");

    public string CollapsedSummaryText
        => GetCollapsedSummary(CollapsedSummaryLayout.Wide);

    public string CollapsedSummaryWithExpectedText
        => GetCollapsedSummary(CollapsedSummaryLayout.Wide, includeExpected: true);

    public string NarrowSummaryText
        => GetCollapsedSummary(CollapsedSummaryLayout.Narrow);

    public string LastUpdatedText
        => Snapshot.LastUpdated == default
            ? LocalizedText.Get("ViewModel.NoUpdate")
            : LocalizedText.Format("ViewModel.UpdatedFormat", Snapshot.LastUpdated.ToString("HH:mm", CultureInfo.CurrentCulture));

    public IReadOnlyList<ProviderUsage> Providers => Snapshot.Providers;

    /// <summary>Provedores que devem aparecer na UI (oculta os que nao estao instalados).</summary>
    public IReadOnlyList<ProviderUsage> VisibleProviders
        => Snapshot.Providers.Where(provider => provider.Status != ProviderStatus.NotInstalled).ToArray();

    public IReadOnlyList<ProviderUsage> ApiCostProviders
        => VisibleProviders.Where(provider => provider.ApiCostEstimate is not null).ToArray();

    public string ApiCostTotalText
    {
        get
        {
            var estimates = ApiCostProviders.Select(provider => provider.ApiCostEstimate).OfType<ApiCostEstimate>().ToArray();
            return estimates.Length == 0
                ? "--"
                : estimates.Sum(estimate => estimate.TotalCostUsd).ToString("$0.00", CultureInfo.InvariantCulture);
        }
    }

    public string ApiCostTotalTokensText
    {
        get
        {
            var total = ApiCostProviders
                .Select(provider => provider.ApiCostEstimate)
                .OfType<ApiCostEstimate>()
                .Sum(estimate => estimate.TotalTokens.TotalTokens);

            return total == 0 ? "--" : $"{FormatCompactTokens(total)} {LocalizedText.Get("ViewModel.TokensSuffix")}";
        }
    }

    public string ApiCostLastUpdatedText
    {
        get
        {
            var lastUpdated = ApiCostProviders
                .Select(provider => provider.ApiCostEstimate?.CalculatedAt)
                .OfType<DateTimeOffset>()
                .OrderDescending()
                .FirstOrDefault();

            return lastUpdated == default
                ? LocalizedText.Get("ViewModel.CostsNoUpdate")
                : LocalizedText.Format("ViewModel.CostsUpdatedFormat", lastUpdated.ToString("HH:mm", CultureInfo.CurrentCulture));
        }
    }

    public Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
        => _refreshService.RefreshAsync(cancellationToken);

    public string GetCollapsedSummary(CollapsedSummaryLayout layout, bool includeExpected = false)
    {
        var segments = new List<string>(2);
        foreach (var provider in new[] { UsageProvider.Codex, UsageProvider.Claude })
        {
            if (GetStatus(provider) == ProviderStatus.NotInstalled)
            {
                continue;
            }

            segments.Add(BuildSummarySegment(provider, layout, includeExpected));
        }

        return segments.Count == 0
            ? AiLimitsDisplayText.NoProvidersDetected
            : string.Join(" | ", segments);
    }

    public string GetWindowDisplayText(UsageProvider provider, string windowId, bool includeExpected = false)
        => GetWindowText(provider, windowId, includeExpected);

    public LimitSeverity GetWindowDisplaySeverity(UsageProvider provider, string windowId, bool includeExpected = false)
    {
        var window = GetWindow(provider, windowId);
        if (includeExpected && TryGetExpectedPercent(window, out var expectedPercent) &&
            window!.UsedPercent!.Value > expectedPercent)
        {
            return LimitSeverity.Danger;
        }

        return GetWindowSeverity(provider, windowId);
    }

    public bool TryGetExpectedPercent(UsageProvider provider, string windowId, out double expectedPercent)
        => TryGetExpectedPercent(GetWindow(provider, windowId), out expectedPercent);

    private string BuildSummarySegment(UsageProvider provider, CollapsedSummaryLayout layout, bool includeExpected)
    {
        var isCodex = provider == UsageProvider.Codex;
        var fiveHour = GetWindowText(provider, "5h", includeExpected);
        var sevenDay = GetWindowText(provider, "7d", includeExpected);

        return layout switch
        {
            CollapsedSummaryLayout.Narrow => $"{(isCodex ? "◎" : "◇")} 5h {fiveHour} 7d {sevenDay}",
            CollapsedSummaryLayout.CriticalOnly => $"{(isCodex ? "◎" : "◇")} {GetWorstWindowText(provider, includeExpected, includeHiddenIndicator: true)}",
            _ => $"{(isCodex ? "OpenAI" : "Anthropic")} 5h {fiveHour} 7d {sevenDay}",
        };
    }

    public UsagePacing? GetWeeklyPacing(UsageProvider provider)
    {
        var window = GetWindow(provider, "7d");
        return window?.UsedPercent is null ? null : UsagePacingCalculator.Calculate(window, _clock());
    }

    public void Dispose()
    {
        _disposed = true;
        _refreshService.UsageUpdated -= OnUsageUpdated;
    }

    private void OnUsageUpdated(object? sender, UsageSnapshot snapshot)
        => _dispatchToUi(() => ApplySnapshot(snapshot));

    private void ApplySnapshot(UsageSnapshot snapshot)
    {
        if (_disposed)
        {
            return;
        }

        Snapshot = snapshot;
        RaiseAll();
    }

    private static Action<Action> CreateUiDispatcher()
    {
        DispatcherQueue? dispatcherQueue;
        try
        {
            dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }
        catch (COMException ex) when ((uint)ex.HResult == 0x80040154)
        {
            return action => action();
        }

        if (dispatcherQueue is null)
        {
            return action => action();
        }

        return action =>
        {
            if (dispatcherQueue.HasThreadAccess)
            {
                action();
                return;
            }

            _ = dispatcherQueue.TryEnqueue(() => action());
        };
    }

    private string GetWindowText(UsageProvider provider, string windowId)
        => GetWindowText(provider, windowId, includeExpected: false);

    private string GetWindowText(UsageProvider provider, string windowId, bool includeExpected)
    {
        var window = GetWindow(provider, windowId);
        if (window?.UsedPercent is null)
        {
            return "--";
        }

        var usedText = $"{Math.Round(window.UsedPercent.Value):0}%";
        if (!includeExpected || !TryGetExpectedPercent(window, out var expectedPercent))
        {
            return usedText;
        }

        return $"{usedText}/{Math.Round(expectedPercent):0}%";
    }

    private ProviderStatus GetStatus(UsageProvider provider)
        => Snapshot.GetProvider(provider)?.Status ?? ProviderStatus.Unavailable;

    private LimitSeverity GetWindowSeverity(UsageProvider provider, string windowId)
    {
        var value = GetWindow(provider, windowId)?.UsedPercent;

        if (value is null)
        {
            return LimitSeverity.Unavailable;
        }

        return value >= 90
            ? LimitSeverity.Danger
            : value >= 75
                ? LimitSeverity.Warning
                : LimitSeverity.Normal;
    }

    private UsageWindow? GetWindow(UsageProvider provider, string windowId)
        => Snapshot.GetProvider(provider)?.Windows.FirstOrDefault(window => string.Equals(window.Id, windowId, StringComparison.OrdinalIgnoreCase));

    private string GetWorstWindowText(UsageProvider provider, bool includeExpected, bool includeHiddenIndicator = false)
    {
        var windows = Snapshot.GetProvider(provider)?.Windows
            .Where(candidate => candidate.UsedPercent is not null)
            .ToArray();

        var window = windows?.MaxBy(candidate => candidate.UsedPercent);
        if (window is null)
        {
            return "--";
        }

        var hiddenIndicator = includeHiddenIndicator && windows is { Length: > 1 } ? " +" : string.Empty;
        return $"{window.Label} {GetWindowText(provider, window.Id, includeExpected)}{hiddenIndicator}";
    }

    private static bool TryCreateExpectedWindow(UsageWindow window, out UsageWindow expectedWindow)
    {
        var duration = window.Duration ?? GetDefaultDuration(window.Id);
        if (duration is null || (window.StartedAt is null && window.ResetsAt is null))
        {
            expectedWindow = window;
            return false;
        }

        expectedWindow = window with { Duration = duration };
        return true;
    }

    private static TimeSpan? GetDefaultDuration(string windowId)
        => windowId.ToLowerInvariant() switch
        {
            "5h" => TimeSpan.FromHours(5),
            "7d" => TimeSpan.FromDays(7),
            _ => null,
        };

    private bool TryGetExpectedPercent(UsageWindow? window, out double expectedPercent)
    {
        if (window?.UsedPercent is null || !TryCreateExpectedWindow(window, out var expectedWindow))
        {
            expectedPercent = 0;
            return false;
        }

        expectedPercent = UsagePacingCalculator.Calculate(expectedWindow, _clock()).ExpectedPercent;
        return true;
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Snapshot));
        OnPropertyChanged(nameof(Providers));
        OnPropertyChanged(nameof(VisibleProviders));
        OnPropertyChanged(nameof(ApiCostProviders));
        OnPropertyChanged(nameof(ApiCostTotalText));
        OnPropertyChanged(nameof(ApiCostTotalTokensText));
        OnPropertyChanged(nameof(ApiCostLastUpdatedText));
        OnPropertyChanged(nameof(OpenAiFiveHourText));
        OnPropertyChanged(nameof(OpenAiSevenDayText));
        OnPropertyChanged(nameof(ClaudeFiveHourText));
        OnPropertyChanged(nameof(ClaudeSevenDayText));
        OnPropertyChanged(nameof(OpenAiStatus));
        OnPropertyChanged(nameof(ClaudeStatus));
        OnPropertyChanged(nameof(OpenAiFiveHourSeverity));
        OnPropertyChanged(nameof(OpenAiSevenDaySeverity));
        OnPropertyChanged(nameof(ClaudeFiveHourSeverity));
        OnPropertyChanged(nameof(ClaudeSevenDaySeverity));
        OnPropertyChanged(nameof(CollapsedSummaryText));
        OnPropertyChanged(nameof(CollapsedSummaryWithExpectedText));
        OnPropertyChanged(nameof(NarrowSummaryText));
        OnPropertyChanged(nameof(LastUpdatedText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string FormatCompactTokens(long tokens)
    {
        if (tokens >= 1_000_000)
        {
            return (tokens / 1_000_000d).ToString("0.#M", CultureInfo.CurrentCulture);
        }

        if (tokens >= 1_000)
        {
            return (tokens / 1_000d).ToString("0.#K", CultureInfo.CurrentCulture);
        }

        return tokens.ToString("0", CultureInfo.CurrentCulture);
    }
}
