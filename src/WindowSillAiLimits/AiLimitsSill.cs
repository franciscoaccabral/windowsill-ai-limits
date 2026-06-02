using System.ComponentModel.Composition;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using WindowSill.API;

using WindowSillAiLimits.Models;
using WindowSillAiLimits.Settings;
using WindowSillAiLimits.Services;
using WindowSillAiLimits.Services.ApiCosts;
using WindowSillAiLimits.Services.Claude;
using WindowSillAiLimits.Services.Codex;
using WindowSillAiLimits.ViewModels;
using WindowSillAiLimits.Views;

namespace WindowSillAiLimits;

[Export(typeof(ISill))]
[Name("AI Limits")]
[Priority(Priority.Lowest)]
[SupportMultipleMonitors]
public sealed class AiLimitsSill : ISillActivatedByDefault, ISillSingleView, IDisposable
{
    private readonly IUsageRefreshService _refreshService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly UsageOverExpectedAlertTracker _alertTracker;
    private readonly AiLimitsViewModel _viewModel;
    private readonly SillView _view;
    private readonly AiLimitsBarView _barView;
    private SillPopup? _popup;
    private bool _isPopupShowing;

    [ImportingConstructor]
    public AiLimitsSill(ISettingsProvider settingsProvider, IPluginInfo pluginInfo)
        : this(
            new UsageRefreshService(
                CreateProbes(settingsProvider, () => DateTimeOffset.Now),
                AiLimitsSettings.GetRefreshInterval(settingsProvider),
                snapshotCache: CreateSnapshotCache(pluginInfo),
                apiCostEstimator: new ApiCostEstimator(clock: () => DateTimeOffset.Now),
                costRefreshInterval: AiLimitsSettings.GetCostRefreshInterval(settingsProvider)),
            settingsProvider,
            pluginInfo.GetPluginContentDirectory())
    {
    }

    internal AiLimitsSill(
        IUsageRefreshService refreshService,
        ISettingsProvider? settingsProvider = null,
        string? pluginContentDirectory = null,
        IUsageAlertNotifier? alertNotifier = null)
    {
        _refreshService = refreshService;
        _settingsProvider = settingsProvider ?? new InMemorySettingsProvider();
        _alertTracker = new UsageOverExpectedAlertTracker(alertNotifier ?? new NativeUsageAlertNotifier());
        _viewModel = new AiLimitsViewModel(_refreshService);

        _view = new SillView();
        _barView = new AiLimitsBarView(
            _view,
            _viewModel,
            _settingsProvider.GetSetting(AiLimitsSettings.ShowProviderNamesInBar),
            _settingsProvider.GetSetting(AiLimitsSettings.ShowExpectedInBar),
            pluginContentDirectory);
        _view.Content = _barView;
        _view.PreviewFlyoutContent = _settingsProvider.GetSetting(AiLimitsSettings.ShowPreviewFlyout)
            ? new AiLimitsPreviewContent(_viewModel, pluginContentDirectory)
            : null;
        _view.PreviewFlyoutPlacementTarget = _barView;
        _barView.Clicked += OnBarClicked;
        _refreshService.UsageUpdated += OnUsageUpdatedForAlerts;
        _settingsProvider.SettingChanged += OnSettingChanged;
    }

    public string DisplayName => LocalizedText.Get("DisplayName");

    public SillView View => _view;

    public SillSettingsView[]? SettingsViews =>
    [
        new(LocalizedText.Get("DisplayName"), new Lazy<Microsoft.UI.Xaml.FrameworkElement>(() => new AiLimitsSettingsView(_settingsProvider))),
    ];

    public IconElement CreateIcon()
        => new FontIcon
        {
            Glyph = "\uE950",
        };

    public ValueTask OnActivatedAsync()
    {
        AiLimitsDiagnostics.Info("sill activated");
        _refreshService.StartMonitoring();
        return ValueTask.CompletedTask;
    }

    public ValueTask OnDeactivatedAsync()
    {
        AiLimitsDiagnostics.Info("sill deactivated");
        _refreshService.StopMonitoring();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _barView.Clicked -= OnBarClicked;
        _refreshService.UsageUpdated -= OnUsageUpdatedForAlerts;
        _settingsProvider.SettingChanged -= OnSettingChanged;
        _viewModel.Dispose();
        _refreshService.Dispose();
    }

    private async void OnBarClicked(object sender, RoutedEventArgs e)
    {
        AiLimitsDiagnostics.Info("compact bar click received");
        await ShowPopupAsync();
    }

    private async Task ShowPopupAsync()
    {
        if (_isPopupShowing)
        {
            AiLimitsDiagnostics.Info("popup show ignored because another popup is already open");
            return;
        }

        _isPopupShowing = true;
        try
        {
            AiLimitsDiagnostics.Info("popup show requested");
            _popup ??= new SillPopup
            {
                Content = new AiLimitsPopupContent(_viewModel, _settingsProvider),
            };

            AiLimitsDiagnostics.Info("popup content ready; calling SillPopup.ShowAsync");
            await _popup.ShowAsync(_view);
            AiLimitsDiagnostics.Info("popup closed");
        }
        catch (Exception ex)
        {
            AiLimitsDiagnostics.Error("popup show failed", ex);
            _popup = null;
        }
        finally
        {
            _isPopupShowing = false;
        }
    }

    private void OnSettingChanged(ISettingsProvider sender, SettingChangedEventArgs args)
    {
        if (string.Equals(args.SettingName, AiLimitsSettings.RefreshIntervalSeconds.Name, StringComparison.Ordinal))
        {
            _refreshService.UpdateRefreshInterval(AiLimitsSettings.GetRefreshInterval(_settingsProvider));
        }
        else if (string.Equals(args.SettingName, AiLimitsSettings.CostRefreshIntervalSeconds.Name, StringComparison.Ordinal))
        {
            _refreshService.UpdateCostRefreshInterval(AiLimitsSettings.GetCostRefreshInterval(_settingsProvider));
        }
        else if (string.Equals(args.SettingName, AiLimitsSettings.ShowExpectedInBar.Name, StringComparison.Ordinal))
        {
            _barView.SetShowExpectedInBar(_settingsProvider.GetSetting(AiLimitsSettings.ShowExpectedInBar));
        }
    }

    private void OnUsageUpdatedForAlerts(object? sender, UsageSnapshot snapshot)
        => _alertTracker.Process(
            GetOverExpectedAlerts(snapshot),
            _settingsProvider.GetSetting(AiLimitsSettings.ShowOverExpectedAlerts));

    public static IReadOnlyList<UsageAboveExpectedAlert> GetOverExpectedAlerts(UsageSnapshot snapshot, Func<DateTimeOffset>? clock = null)
    {
        var now = clock?.Invoke() ?? DateTimeOffset.Now;
        var alerts = new List<UsageAboveExpectedAlert>();
        foreach (var provider in snapshot.Providers)
        {
            if (provider.Status == ProviderStatus.NotInstalled)
            {
                continue;
            }

            foreach (var window in provider.Windows.Where(window => window.Id is "5h" or "7d"))
            {
                if (window.UsedPercent is null || !TryGetExpectedPercent(window, now, out var expectedPercent))
                {
                    continue;
                }

                if (window.UsedPercent.Value > expectedPercent)
                {
                    alerts.Add(new UsageAboveExpectedAlert(provider.DisplayName, window.Label, window.UsedPercent.Value, expectedPercent));
                }
            }
        }

        return alerts;
    }

    private static bool TryGetExpectedPercent(UsageWindow window, DateTimeOffset now, out double expectedPercent)
    {
        var duration = window.Duration ?? GetDefaultDuration(window.Id);
        if (duration is null || (window.StartedAt is null && window.ResetsAt is null))
        {
            expectedPercent = 0;
            return false;
        }

        expectedPercent = UsagePacingCalculator.Calculate(window with { Duration = duration }, now).ExpectedPercent;
        return true;
    }

    private static TimeSpan? GetDefaultDuration(string windowId)
        => windowId.ToLowerInvariant() switch
        {
            "5h" => TimeSpan.FromHours(5),
            "7d" => TimeSpan.FromDays(7),
            _ => null,
        };

    private static IUsageProbe[] CreateProbes(ISettingsProvider settingsProvider, Func<DateTimeOffset> clock)
    {
        if (settingsProvider.GetSetting(AiLimitsSettings.UseMockData))
        {
            return MockUsageProbe.CreateDefault(clock);
        }

        return
        [
            new CodexUsageProbe(
                new CodexAppServerClient(settingsProvider.GetSetting(AiLimitsSettings.CodexCommandPath), TimeSpan.FromSeconds(10)),
                clock),
            new ClaudeUsageProbe(new ClaudeOAuthUsageClient(TimeSpan.FromSeconds(10)), clock),
        ];
    }

    private static IUsageSnapshotCache CreateSnapshotCache(IPluginInfo pluginInfo)
        => new FileUsageSnapshotCache(System.IO.Path.Combine(pluginInfo.GetPluginDataFolder(), "last-usage-snapshot.json"));
}
