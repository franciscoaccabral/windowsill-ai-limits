using System.ComponentModel.Composition;
using System.Reflection;
using System.Runtime.InteropServices;

using WindowSill.API;

using WindowSillAiLimits;
using WindowSillAiLimits.Models;
using WindowSillAiLimits.Services.ApiCosts;
using WindowSillAiLimits.Services.Claude;
using WindowSillAiLimits.Services.Codex;
using WindowSillAiLimits.Settings;
using WindowSillAiLimits.Services;
using WindowSillAiLimits.ViewModels;
using WindowSillAiLimits.Views;

if (args.Contains("--live-codex", StringComparer.Ordinal))
{
    var commandPath = Environment.GetEnvironmentVariable("CODEX_COMMAND") ?? "codex";
    var probe = new CodexUsageProbe(new CodexAppServerClient(commandPath, TimeSpan.FromSeconds(20)));
    var usage = await probe.ReadAsync(CancellationToken.None);

    Console.WriteLine($"Codex status: {usage.Status}");
    Console.WriteLine($"Codex plan: {usage.PlanLabel ?? "--"}");
    Console.WriteLine($"Codex windows: {usage.Windows.Count}");

    foreach (var window in usage.Windows)
    {
        Console.WriteLine($"{window.Label}: {FormatPercent(window.UsedPercent)} reset={FormatReset(window.ResetsAt)}");
    }

    if (!string.IsNullOrWhiteSpace(usage.Message))
    {
        Console.WriteLine($"Codex message: {UsageMessageSanitizer.Sanitize(usage.Message)}");
    }

    return usage.Status is ProviderStatus.Ok or ProviderStatus.Warning ? 0 : 1;
}

if (args.Contains("--live-claude", StringComparer.Ordinal))
{
    var probe = new ClaudeUsageProbe(new ClaudeOAuthUsageClient(TimeSpan.FromSeconds(20)));
    var usage = await probe.ReadAsync(CancellationToken.None);

    Console.WriteLine($"Claude status: {usage.Status}");
    Console.WriteLine($"Claude plan: {usage.PlanLabel ?? "--"}");
    Console.WriteLine($"Claude windows: {usage.Windows.Count}");

    foreach (var window in usage.Windows)
    {
        Console.WriteLine($"{window.Label}: {FormatPercent(window.UsedPercent)} reset={FormatReset(window.ResetsAt)}");
    }

    if (!string.IsNullOrWhiteSpace(usage.Message))
    {
        Console.WriteLine($"Claude message: {UsageMessageSanitizer.Sanitize(usage.Message)}");
    }

    return usage.Status is ProviderStatus.Ok or ProviderStatus.Warning ? 0 : 1;
}

var tests = new (string Name, Func<Task> Run)[]
{
    ("formats collapsed values and unavailable placeholders", Tests.FormatsCollapsedValuesAndUnavailablePlaceholders),
    ("formats collapsed values with expected percentages", Tests.FormatsCollapsedValuesWithExpectedPercentages),
    ("infers expected percentages from reset timestamps", Tests.InfersExpectedPercentagesFromResetTimestamps),
    ("omits expected percentages without timing data", Tests.OmitsExpectedPercentagesWithoutTimingData),
    ("marks above expected collapsed values as danger", Tests.MarksAboveExpectedCollapsedValuesAsDanger),
    ("over expected alerts fire once per crossing", Tests.OverExpectedAlertsFireOncePerCrossing),
    ("native usage alerts build sanitized Windows notification payloads", Tests.NativeUsageAlertsBuildSanitizedWindowsNotificationPayloads),
    ("formats narrow summary without provider names", Tests.FormatsNarrowSummaryWithoutProviderNames),
    ("formats critical-only summary with hidden window indicator", Tests.FormatsCriticalOnlySummaryWithHiddenWindowIndicator),
    ("classifies usage severity thresholds", Tests.ClassifiesUsageSeverityThresholds),
    ("chooses collapsed bar layout from available width", Tests.ChoosesCollapsedBarLayoutFromAvailableWidth),
    ("keeps full quota windows at normal host width", Tests.KeepsFullQuotaWindowsAtNormalHostWidth),
    ("keeps Claude quota text in narrow trailing edge budget", Tests.KeepsClaudeQuotaTextInNarrowTrailingEdgeBudget),
    ("honors collapsed provider name setting", Tests.HonorsCollapsedProviderNameSetting),
    ("defines expanded panel visual contract labels", Tests.DefinesExpandedPanelVisualContractLabels),
    ("chooses expanded provider columns from available width", Tests.ChoosesExpandedProviderColumnsFromAvailableWidth),
    ("preview flyout shows provider icons and pacing", Tests.PreviewFlyoutShowsProviderIconsAndPacing),
    ("defines popup icon button accessibility name", Tests.DefinesPopupIconButtonAccessibilityName),
    ("popup content uses SillPopupContent XAML root", Tests.PopupContentUsesSillPopupContentXamlRoot),
    ("popup content keeps detailed panel contract", Tests.PopupContentKeepsDetailedPanelContract),
    ("popup content renders consolidated API cost panel", Tests.PopupContentRendersConsolidatedApiCostPanel),
    ("expanded API cost panel uses single column and taller popup", Tests.ExpandedApiCostPanelUsesSingleColumnAndTallerPopup),
    ("API cost table prioritizes readable price column", Tests.ApiCostTablePrioritizesReadablePriceColumn),
    ("popup content uses compact typography", Tests.PopupContentUsesCompactTypography),
    ("compact bar owns popup tap handling", Tests.CompactBarOwnsPopupTapHandling),
    ("compact bar uses provider icons instead of placeholder glyphs", Tests.CompactBarUsesProviderIconsInsteadOfPlaceholderGlyphs),
    ("compact bar uses packaged svg provider icons", Tests.CompactBarUsesPackagedSvgProviderIcons),
    ("compact bar aligns with neighboring sill items", Tests.CompactBarAlignsWithNeighboringSillItems),
    ("popup click path writes sanitized diagnostics", Tests.PopupClickPathWritesSanitizedDiagnostics),
    ("preserves optional Claude Sonnet window", Tests.PreservesOptionalClaudeSonnetWindow),
    ("defines small non-sensitive settings", Tests.DefinesSmallNonSensitiveSettings),
    ("clamps refresh interval settings", Tests.ClampsRefreshIntervalSettings),
    ("offers five to sixty minute refresh presets", Tests.OffersFiveToSixtyMinuteRefreshPresets),
    ("defines four hour cost refresh setting", Tests.DefinesFourHourCostRefreshSetting),
    ("migrates legacy default refresh interval to fifteen minutes", Tests.MigratesLegacyDefaultRefreshIntervalToFifteenMinutes),
    ("rate limit cooldown skips live calls and serves cache", Tests.RateLimitCooldownSkipsLiveCallsAndServesCache),
    ("applies refresh interval setting changes to active sill", Tests.AppliesRefreshIntervalSettingChangesToActiveSill),
    ("updates refresh service interval while monitoring", Tests.UpdatesRefreshServiceIntervalWhileMonitoring),
    ("initial monitoring backfills missing API costs once", Tests.InitialMonitoringBackfillsMissingApiCostsOnce),
    ("mock data matches approved compact snapshot", Tests.MockDataMatchesApprovedCompactSnapshot),
    ("AiLimitsSill selects real probes by default", Tests.AiLimitsSillSelectsRealProbesByDefault),
    ("configures package output for local validation", Tests.ConfiguresPackageOutputForLocalValidation),
    ("resolves Windows command shims without shell execution", Tests.ResolvesWindowsCommandShimsWithoutShellExecution),
    ("parses Codex primary and secondary rate limits", Tests.ParsesCodexPrimaryAndSecondaryRateLimits),
    ("parses Codex schema integer reset and duration fields", Tests.ParsesCodexSchemaIntegerResetAndDurationFields),
    ("parses Codex rate limits by limit id", Tests.ParsesCodexRateLimitsByLimitId),
    ("maps Codex JSON-RPC errors to unavailable provider", Tests.MapsCodexJsonRpcErrorsToUnavailableProvider),
    ("maps Codex account JSON-RPC errors to unavailable provider", Tests.MapsCodexAccountJsonRpcErrorsToUnavailableProvider),
    ("sanitizes Codex parser error messages", Tests.SanitizesCodexParserErrorMessages),
    ("Codex probe sends initialize account and rate limit requests", Tests.CodexProbeSendsInitializeAccountAndRateLimitRequests),
    ("Codex probe maps client failure to unavailable", Tests.CodexProbeMapsClientFailureToUnavailable),
    ("parses Claude OAuth usage response", Tests.ParsesClaudeOAuthUsageResponse),
    ("parses Claude unified rate-limit headers", Tests.ParsesClaudeUnifiedRateLimitHeaders),
    ("parses Claude optional Opus usage window", Tests.ParsesClaudeOptionalOpusUsageWindow),
    ("parses Claude statusline fallback rate limits", Tests.ParsesClaudeStatuslineFallbackRateLimits),
    ("maps Claude usage errors to unavailable provider", Tests.MapsClaudeUsageErrorsToUnavailableProvider),
    ("maps Claude non-subscriber errors to unavailable provider", Tests.MapsClaudeNonSubscriberErrorsToUnavailableProvider),
    ("sanitizes Claude parser error messages", Tests.SanitizesClaudeParserErrorMessages),
    ("Claude probe maps client failure to unavailable", Tests.ClaudeProbeMapsClientFailureToUnavailable),
    ("Codex probe maps missing command to not installed", Tests.CodexProbeMapsMissingCommandToNotInstalled),
    ("Claude probe maps missing credentials to not installed", Tests.ClaudeProbeMapsMissingCredentialsToNotInstalled),
    ("hides not installed provider from UI", Tests.HidesNotInstalledProviderFromUi),
    ("shows neutral message when no provider installed", Tests.ShowsNeutralMessageWhenNoProviderInstalled),
    ("Claude OAuth client reports missing credentials without network", Tests.ClaudeOAuthClientReportsMissingCredentialsWithoutNetwork),
    ("Claude OAuth client rejects expired credentials without refresh token", Tests.ClaudeOAuthClientRejectsExpiredCredentialsWithoutRefreshToken),
    ("Claude OAuth client refreshes expired token before usage", Tests.ClaudeOAuthClientRefreshesExpiredTokenBeforeUsage),
    ("Claude OAuth client refreshes token within expiry buffer", Tests.ClaudeOAuthClientRefreshesTokenWithinExpiryBuffer),
    ("Claude OAuth client skips refresh for fresh token", Tests.ClaudeOAuthClientSkipsRefreshForFreshToken),
    ("Claude OAuth client persists rotated refresh token", Tests.ClaudeOAuthClientPersistsRotatedRefreshToken),
    ("Claude OAuth client preserves refresh token when rotation omitted", Tests.ClaudeOAuthClientPreservesRefreshTokenWhenRotationOmitted),
    ("Claude OAuth client accepts floating expires in", Tests.ClaudeOAuthClientAcceptsFloatingExpiresIn),
    ("Claude credential write preserves metadata and unknown fields", Tests.ClaudeCredentialWritePreservesMetadataAndUnknownFields),
    ("Claude OAuth client skips refresh after lock race", Tests.ClaudeOAuthClientSkipsRefreshAfterLockRace),
    ("Claude OAuth client maps lock timeout to sanitized failure", Tests.ClaudeOAuthClientMapsLockTimeoutToSanitizedFailure),
    ("Claude OAuth client does not expose refresh tokens", Tests.ClaudeOAuthClientDoesNotExposeRefreshTokens),
    ("Claude OAuth client does not expose HTTP response bodies", Tests.ClaudeOAuthClientDoesNotExposeHttpResponseBodies),
    ("Claude OAuth client captures only unified rate-limit headers", Tests.ClaudeOAuthClientCapturesOnlyUnifiedRateLimitHeaders),
    ("File usage snapshot cache excludes provider messages", Tests.FileUsageSnapshotCacheExcludesProviderMessages),
    ("sanitizes live smoke messages", Tests.SanitizesLiveSmokeMessages),
    ("sanitizes api key prefix tokens", Tests.SanitizesApiKeyPrefixTokens),
    ("disposes probes and codex process on service dispose", Tests.DisposesProbesAndCodexProcessOnServiceDispose),
    ("keeps other provider when one probe fails", Tests.KeepsOtherProviderWhenOneProbeFails),
    ("sanitizes direct provider result messages", Tests.SanitizesDirectProviderResultMessages),
    ("reuses prior provider data as stale after transient failure", Tests.ReusesPriorProviderDataAsStaleAfterTransientFailure),
    ("reuses cached Claude data after restart when usage endpoint is rate limited", Tests.ReusesCachedClaudeDataAfterRestartWhenUsageEndpointIsRateLimited),
    ("sanitizes stale provider failure messages", Tests.SanitizesStaleProviderFailureMessages),
    ("does not reuse stale cache for hard unavailable failures", Tests.DoesNotReuseStaleCacheForHardUnavailableFailures),
    ("calculates OpenAI API-equivalent cost", Tests.CalculatesOpenAiApiEquivalentCost),
    ("calculates Anthropic API-equivalent cost", Tests.CalculatesAnthropicApiEquivalentCost),
    ("Codex token reader filters weekly window and ignores cumulative totals", Tests.CodexTokenReaderFiltersWeeklyWindowAndIgnoresCumulativeTotals),
    ("Claude token reader sums message usage by model", Tests.ClaudeTokenReaderSumsMessageUsageByModel),
    ("local token readers ignore null JSON objects", Tests.LocalTokenReadersIgnoreNullJsonObjects),
    ("API cost estimator failure does not break provider", Tests.ApiCostEstimatorFailureDoesNotBreakProvider),
    ("manual cost refresh does not call usage probes", Tests.ManualCostRefreshDoesNotCallUsageProbes),
    ("cost refresh recalculates Codex and Claude together", Tests.CostRefreshRecalculatesCodexAndClaudeTogether),
    ("cost refresh preserves provider estimate when recalculation is empty", Tests.CostRefreshPreservesProviderEstimateWhenRecalculationIsEmpty),
    ("quota refresh preserves provider estimate when weekly window drifts by milliseconds", Tests.QuotaRefreshPreservesProviderEstimateWhenWeeklyWindowDriftsByMilliseconds),
    ("view model summarizes consolidated API costs", Tests.ViewModelSummarizesConsolidatedApiCosts),
    ("unknown price models stay visible and unpriced", Tests.UnknownPriceModelsStayVisibleAndUnpriced),
    ("File usage snapshot cache stores only cost aggregates", Tests.FileUsageSnapshotCacheStoresOnlyCostAggregates),
    ("dispatches usage updates through UI dispatcher", Tests.DispatchesUsageUpdatesThroughUiDispatcher),
    ("calculates pacing for a weekly window", Tests.CalculatesPacingForWeeklyWindow),
    ("projects weekly exhaustion before reset", Tests.ProjectsWeeklyExhaustionBeforeReset),
    ("marks weekly exhaustion after reset", Tests.MarksWeeklyExhaustionAfterReset),
    ("handles unavailable and exhausted weekly projections", Tests.HandlesUnavailableAndExhaustedWeeklyProjections),
    ("prevents overlapping refreshes", Tests.PreventsOverlappingRefreshes),
    ("refresh command prevents overlapping executions", Tests.RefreshCommandPreventsOverlappingExecutions),
    ("exports an always-active single-view sill", Tests.ExportsAlwaysActiveSingleViewSill),
};

var failures = 0;

foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine(ex);
    }
}

return failures == 0 ? 0 : 1;

static string FormatPercent(double? value)
    => value is null ? "--" : $"{value:0.#}%";

static string FormatReset(DateTimeOffset? value)
    => value is null ? "--" : value.Value.ToString("yyyy-MM-dd HH:mm zzz");

internal static class Tests
{
    public static Task FormatsCollapsedValuesAndUnavailablePlaceholders()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Codex,
                    "OpenAI",
                    "Plus",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("5h", "5h", 100, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                        new UsageWindow("7d", "7d", 59, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3)),
                    ],
                    now,
                    null),
                new ProviderUsage(
                    UsageProvider.Claude,
                    "Anthropic",
                    "Max",
                    ProviderStatus.Unavailable,
                    [],
                    now,
                    "Claude Code usage unavailable"),
            ],
            now);

        using var service = new StubUsageRefreshService(snapshot);
        using var viewModel = new AiLimitsViewModel(service, () => now);

        AssertEqual("100%", viewModel.OpenAiFiveHourText);
        AssertEqual("59%", viewModel.OpenAiSevenDayText);
        AssertEqual("--", viewModel.ClaudeFiveHourText);
        AssertEqual("--", viewModel.ClaudeSevenDayText);
        AssertEqual("OpenAI 5h 100% 7d 59% | Anthropic 5h -- 7d --", viewModel.CollapsedSummaryText);
        AssertEqual(LimitSeverity.Danger, viewModel.OpenAiFiveHourSeverity);
        AssertEqual(ProviderStatus.Unavailable, viewModel.ClaudeStatus);

        return Task.CompletedTask;
    }

    public static Task FormatsCollapsedValuesWithExpectedPercentages()
    {
        var now = new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.FromHours(-3));
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Codex,
                    "OpenAI",
                    "Plus",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("5h", "5h", 22, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                        new UsageWindow("7d", "7d", 11, now.AddDays(6), TimeSpan.FromDays(7), now.AddDays(-1)),
                    ],
                    now,
                    null),
                new ProviderUsage(
                    UsageProvider.Claude,
                    "Anthropic",
                    "Max",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("5h", "5h", 0, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                        new UsageWindow("7d", "7d", 0, now.AddDays(6), TimeSpan.FromDays(7), now.AddDays(-1)),
                    ],
                    now,
                    null),
            ],
            now);

        using var service = new StubUsageRefreshService(snapshot);
        using var viewModel = new AiLimitsViewModel(service, () => now);

        AssertEqual("OpenAI 5h 22%/60% 7d 11%/14% | Anthropic 5h 0%/60% 7d 0%/14%", viewModel.GetCollapsedSummary(CollapsedSummaryLayout.Wide, includeExpected: true));
        AssertEqual("◎ 5h 22%/60% 7d 11%/14% | ◇ 5h 0%/60% 7d 0%/14%", viewModel.GetCollapsedSummary(CollapsedSummaryLayout.Narrow, includeExpected: true));
        AssertEqual("◎ 5h 22%/60% + | ◇ 5h 0%/60% +", viewModel.GetCollapsedSummary(CollapsedSummaryLayout.CriticalOnly, includeExpected: true));

        return Task.CompletedTask;
    }

    public static Task OmitsExpectedPercentagesWithoutTimingData()
    {
        var now = new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.FromHours(-3));
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Codex,
                    "OpenAI",
                    "Plus",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("5h", "5h", 22, null, null, null),
                        new UsageWindow("7d", "7d", null, now.AddDays(6), TimeSpan.FromDays(7), now.AddDays(-1)),
                    ],
                    now,
                    null),
            ],
            now);

        using var service = new StubUsageRefreshService(snapshot);
        using var viewModel = new AiLimitsViewModel(service, () => now);

        AssertEqual("OpenAI 5h 22% 7d -- | Anthropic 5h -- 7d --", viewModel.GetCollapsedSummary(CollapsedSummaryLayout.Wide, includeExpected: true));

        return Task.CompletedTask;
    }

    public static Task InfersExpectedPercentagesFromResetTimestamps()
    {
        var now = new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.FromHours(-3));
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Codex,
                    "OpenAI",
                    null,
                    ProviderStatus.NotInstalled,
                    [],
                    now,
                    "Codex command not found or could not start: codex"),
                new ProviderUsage(
                    UsageProvider.Claude,
                    "Anthropic",
                    "Max",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("5h", "5h", 0, now.AddHours(2), null, null),
                        new UsageWindow("7d", "7d", 0, now.AddDays(6), null, null),
                    ],
                    now,
                    null),
            ],
            now);

        using var service = new StubUsageRefreshService(snapshot);
        using var viewModel = new AiLimitsViewModel(service, () => now);

        AssertEqual("Anthropic 5h 0%/60% 7d 0%/14%", viewModel.GetCollapsedSummary(CollapsedSummaryLayout.Wide, includeExpected: true));

        return Task.CompletedTask;
    }

    public static Task MarksAboveExpectedCollapsedValuesAsDanger()
    {
        var now = new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.FromHours(-3));
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Codex,
                    "OpenAI",
                    "Plus",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("5h", "5h", 35, now.AddHours(4), TimeSpan.FromHours(5), now.AddHours(-1)),
                    ],
                    now,
                    null),
                new ProviderUsage(
                    UsageProvider.Claude,
                    "Anthropic",
                    "Max",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("7d", "7d", 20, now.AddDays(6), null, null),
                    ],
                    now,
                    null),
            ],
            now);

        using var service = new StubUsageRefreshService(snapshot);
        using var viewModel = new AiLimitsViewModel(service, () => now);

        AssertEqual(LimitSeverity.Danger, viewModel.GetWindowDisplaySeverity(UsageProvider.Codex, "5h", includeExpected: true));
        AssertEqual(LimitSeverity.Danger, viewModel.GetWindowDisplaySeverity(UsageProvider.Claude, "7d", includeExpected: true));
        AssertEqual(LimitSeverity.Normal, viewModel.GetWindowDisplaySeverity(UsageProvider.Codex, "5h", includeExpected: false));

        return Task.CompletedTask;
    }

    public static Task OverExpectedAlertsFireOncePerCrossing()
    {
        var now = new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.FromHours(-3));
        var over = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Claude,
                    "Anthropic",
                    "Max",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("7d", "7d", 20, now.AddDays(6), null, null),
                    ],
                    now,
                    null),
            ],
            now);
        var under = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Claude,
                    "Anthropic",
                    "Max",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("7d", "7d", 5, now.AddDays(6), null, null),
                    ],
                    now,
                    null),
            ],
            now);

        var notifier = new RecordingUsageAlertNotifier();
        var tracker = new UsageOverExpectedAlertTracker(notifier);

        tracker.Process(AiLimitsSill.GetOverExpectedAlerts(over, () => now), isEnabled: true);
        tracker.Process(AiLimitsSill.GetOverExpectedAlerts(over, () => now), isEnabled: true);
        tracker.Process(AiLimitsSill.GetOverExpectedAlerts(under, () => now), isEnabled: true);
        tracker.Process(AiLimitsSill.GetOverExpectedAlerts(over, () => now), isEnabled: true);

        AssertEqual(2, notifier.Alerts.Count);
        AssertEqual("Anthropic", notifier.Alerts[0].ProviderName);
        AssertEqual("7d", notifier.Alerts[0].WindowLabel);

        return Task.CompletedTask;
    }

    public static Task NativeUsageAlertsBuildSanitizedWindowsNotificationPayloads()
    {
        var now = new DateTimeOffset(2026, 6, 2, 13, 0, 0, TimeSpan.FromHours(-3));
        var alert = new UsageAboveExpectedAlert("OpenAI", "5h", 2, 1);
        var sender = new RecordingUsageAlertNotificationSender();
        var notifier = new NativeUsageAlertNotifier(sender, () => now);

        notifier.NotifyUsageAboveExpected(alert);

        AssertEqual(1, sender.Notifications.Count);
        var notification = sender.Notifications[0];
        AssertEqual("AI Limits", notification.Title);
        AssertEqual("OpenAI 5h: realizado 2% passou o previsto 1%.", notification.Body);
        AssertEqual("ai-limits", notification.Group);
        AssertEqual("openai-5h", notification.Tag);
        AssertEqual(now.AddHours(6), notification.ExpiresAt);
        AssertEqual(true, notification.ExpiresOnReboot);
        AssertFalse(notification.Body.Contains("Authorization", StringComparison.OrdinalIgnoreCase), "Notification body must not include headers.");

        var failingSender = new ThrowingUsageAlertNotificationSender();
        var failingNotifier = new NativeUsageAlertNotifier(failingSender, () => now);
        failingNotifier.NotifyUsageAboveExpected(alert);
        AssertEqual(1, failingSender.CallCount);

        return Task.CompletedTask;
    }

    public static Task FormatsNarrowSummaryWithoutProviderNames()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Codex,
                    "OpenAI",
                    "Plus",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("5h", "5h", 100, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                        new UsageWindow("7d", "7d", 59, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3)),
                    ],
                    now,
                    null),
                new ProviderUsage(
                    UsageProvider.Claude,
                    "Anthropic",
                    "Max",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("5h", "5h", 70, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                        new UsageWindow("7d", "7d", 50, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3)),
                    ],
                    now,
                    null),
            ],
            now);

        using var service = new StubUsageRefreshService(snapshot);
        using var viewModel = new AiLimitsViewModel(service, () => now);

        AssertEqual("◎ 5h 100% 7d 59% | ◇ 5h 70% 7d 50%", viewModel.GetCollapsedSummary(CollapsedSummaryLayout.Narrow));
        AssertFalse(viewModel.GetCollapsedSummary(CollapsedSummaryLayout.Narrow).Contains("OpenAI", StringComparison.Ordinal), "Narrow summary should remove provider names first.");
        AssertFalse(viewModel.GetCollapsedSummary(CollapsedSummaryLayout.Narrow).Contains("Anthropic", StringComparison.Ordinal), "Narrow summary should remove provider names first.");

        return Task.CompletedTask;
    }

    public static Task FormatsCriticalOnlySummaryWithHiddenWindowIndicator()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Codex,
                    "OpenAI",
                    "Plus",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("5h", "5h", 100, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                        new UsageWindow("7d", "7d", 59, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3)),
                    ],
                    now,
                    null),
                new ProviderUsage(
                    UsageProvider.Claude,
                    "Anthropic",
                    "Max",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("5h", "5h", 70, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                        new UsageWindow("7d", "7d", 50, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3)),
                    ],
                    now,
                    null),
            ],
            now);

        using var service = new StubUsageRefreshService(snapshot);
        using var viewModel = new AiLimitsViewModel(service, () => now);
        var summary = viewModel.GetCollapsedSummary(CollapsedSummaryLayout.CriticalOnly);

        AssertEqual("◎ 5h 100% + | ◇ 5h 70% +", summary);
        AssertFalse(summary.Contains("7h", StringComparison.OrdinalIgnoreCase), "Collapsed summaries must never label the weekly window as 7h.");

        return Task.CompletedTask;
    }

    public static Task ClassifiesUsageSeverityThresholds()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Codex,
                    "OpenAI",
                    "Plus",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("5h", "5h", 74, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                        new UsageWindow("7d", "7d", 75, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3)),
                    ],
                    now,
                    null),
                new ProviderUsage(
                    UsageProvider.Claude,
                    "Anthropic",
                    "Max",
                    ProviderStatus.Warning,
                    [
                        new UsageWindow("5h", "5h", 89, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                        new UsageWindow("7d", "7d", 90, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3)),
                    ],
                    now,
                    null),
            ],
            now);

        using var service = new StubUsageRefreshService(snapshot);
        using var viewModel = new AiLimitsViewModel(service, () => now);

        AssertEqual(LimitSeverity.Normal, viewModel.OpenAiFiveHourSeverity);
        AssertEqual(LimitSeverity.Warning, viewModel.OpenAiSevenDaySeverity);
        AssertEqual(LimitSeverity.Warning, viewModel.ClaudeFiveHourSeverity);
        AssertEqual(LimitSeverity.Danger, viewModel.ClaudeSevenDaySeverity);

        return Task.CompletedTask;
    }

    public static Task DefinesExpandedPanelVisualContractLabels()
    {
        AssertEqual("Refresh", AiLimitsDisplayText.Refresh);
        AssertEqual("Settings", AiLimitsDisplayText.Settings);
        AssertEqual("Dados de ferramentas locais", AiLimitsDisplayText.SourceNote);
        AssertEqual("Esperado até agora", AiLimitsDisplayText.ExpectedSoFar);
        AssertEqual("Diferença", AiLimitsDisplayText.Difference);
        AssertEqual("Ritmo médio atual", AiLimitsDisplayText.CurrentAveragePace);
        AssertEqual("Previsto terminar", AiLimitsDisplayText.ProjectedExhaustion);
        AssertEqual("Impacto", AiLimitsDisplayText.ForecastImpact);
        AssertEqual("Próximo reset 7d", AiLimitsDisplayText.NextWeeklyReset);
        AssertEqual("Consultado em", AiLimitsDisplayText.QueriedAt);

        return Task.CompletedTask;
    }

    public static Task ChoosesCollapsedBarLayoutFromAvailableWidth()
    {
        AssertEqual(CollapsedSummaryLayout.Wide, AiLimitsBarView.GetLayoutForWidth(540));
        AssertEqual(CollapsedSummaryLayout.Narrow, AiLimitsBarView.GetLayoutForWidth(320));
        AssertEqual(CollapsedSummaryLayout.Narrow, AiLimitsBarView.GetLayoutForWidth(220));
        AssertEqual(CollapsedSummaryLayout.Narrow, AiLimitsBarView.GetLayoutForWidth(180));
        AssertEqual(CollapsedSummaryLayout.CriticalOnly, AiLimitsBarView.GetLayoutForWidth(100));
        AssertEqual(CollapsedSummaryLayout.Narrow, AiLimitsBarView.GetLayoutForHost(SillOrientationAndSize.HorizontalSmall, 180));
        AssertEqual(CollapsedSummaryLayout.CriticalOnly, AiLimitsBarView.GetLayoutForHost(SillOrientationAndSize.VerticalSmall, 280));
        AssertEqual(0, AiLimitsBarView.MinimumCompactWidth);
        AssertEqual(220, AiLimitsBarView.AssumedCompactWidth);

        return Task.CompletedTask;
    }

    public static Task KeepsFullQuotaWindowsAtNormalHostWidth()
    {
        var now = new DateTimeOffset(2026, 5, 24, 22, 21, 0, TimeSpan.FromHours(-3));
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Codex,
                    "OpenAI",
                    "Plus",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("5h", "5h", 0, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                        new UsageWindow("7d", "7d", 21, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3)),
                    ],
                    now,
                    null),
                new ProviderUsage(
                    UsageProvider.Claude,
                    "Anthropic",
                    "Max",
                    ProviderStatus.Unavailable,
                    [],
                    now,
                    null),
            ],
            now);

        using var service = new StubUsageRefreshService(snapshot);
        using var viewModel = new AiLimitsViewModel(service, () => now);

        AssertEqual(CollapsedSummaryLayout.Narrow, AiLimitsBarView.GetLayoutForWidth(220));
        AssertEqual("◎ 5h 0% 7d 21% | ◇ 5h -- 7d --", viewModel.GetCollapsedSummary(AiLimitsBarView.GetLayoutForWidth(220)));
        AssertEqual("◎ 7d 21% + | ◇ --", viewModel.GetCollapsedSummary(CollapsedSummaryLayout.CriticalOnly));

        return Task.CompletedTask;
    }

    public static Task KeepsClaudeQuotaTextInNarrowTrailingEdgeBudget()
    {
        var now = new DateTimeOffset(2026, 5, 24, 22, 21, 0, TimeSpan.FromHours(-3));
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Codex,
                    "OpenAI",
                    "Plus",
                    ProviderStatus.Warning,
                    [
                        new UsageWindow("5h", "5h", 100, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                        new UsageWindow("7d", "7d", 100, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3)),
                    ],
                    now,
                    null),
                new ProviderUsage(
                    UsageProvider.Claude,
                    "Anthropic",
                    "Max",
                    ProviderStatus.Warning,
                    [
                        new UsageWindow("5h", "5h", 100, now.AddHours(1), TimeSpan.FromHours(5), now.AddHours(-4)),
                        new UsageWindow("7d", "7d", 100, now.AddDays(2), TimeSpan.FromDays(7), now.AddDays(-5)),
                    ],
                    now,
                    null),
            ],
            now);

        using var service = new StubUsageRefreshService(snapshot);
        using var viewModel = new AiLimitsViewModel(service, () => now);

        var summary = viewModel.GetCollapsedSummary(CollapsedSummaryLayout.Narrow);
        AssertEqual("◎ 5h 100% 7d 100% | ◇ 5h 100% 7d 100%", summary);
        AssertTrue(summary.EndsWith("◇ 5h 100% 7d 100%", StringComparison.Ordinal), "Claude values should remain at the trailing edge in the narrow layout.");
        AssertTrue(EstimateCompactTextWidth(summary) <= 320, "Worst-case narrow quota summary should fit the local 320px budget before falling back to critical-only.");

        return Task.CompletedTask;
    }

    public static Task HonorsCollapsedProviderNameSetting()
    {
        AssertTrue(AiLimitsBarView.ShouldShowProviderNames(540, showProviderNamesSetting: true), "Provider names should show at wide widths when enabled.");
        AssertFalse(AiLimitsBarView.ShouldShowProviderNames(320, showProviderNamesSetting: true), "Provider names should hide before values at narrow widths.");
        AssertFalse(AiLimitsBarView.ShouldShowProviderNames(540, showProviderNamesSetting: false), "Provider names should hide when the setting is disabled.");

        return Task.CompletedTask;
    }

    public static Task ChoosesExpandedProviderColumnsFromAvailableWidth()
    {
        AssertEqual(2, AiLimitsPopupLayout.GetProviderColumnCount(560, providerCount: 2));
        AssertEqual(1, AiLimitsPopupLayout.GetProviderColumnCount(420, providerCount: 2));
        AssertEqual(1, AiLimitsPopupLayout.GetProviderColumnCount(560, providerCount: 1));

        return Task.CompletedTask;
    }

    public static Task PreviewFlyoutShowsProviderIconsAndPacing()
    {
        var previewPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "Views", "AiLimitsPreviewContent.cs"));
        var sillPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "AiLimitsSill.cs"));
        var previewSource = File.ReadAllText(previewPath);
        var sillSource = File.ReadAllText(sillPath);

        AssertTrue(previewSource.Contains("BuildProviderRow", StringComparison.Ordinal), "Hover preview should render provider rows instead of a single collapsed text summary.");
        AssertTrue(previewSource.Contains("BuildPacingRow", StringComparison.Ordinal), "Hover preview should show used vs expected pacing information.");
        AssertTrue(previewSource.Contains("SvgImageSource", StringComparison.Ordinal), "Hover preview should use the same SVG provider icon assets as the compact bar.");
        AssertTrue(previewSource.Contains("openai-mark.svg", StringComparison.Ordinal), "Hover preview should include the OpenAI SVG icon.");
        AssertTrue(previewSource.Contains("anthropic-mark.svg", StringComparison.Ordinal), "Hover preview should include the Anthropic SVG icon.");
        AssertTrue(previewSource.Contains("GetWeeklyPacing", StringComparison.Ordinal), "Hover preview should calculate expected usage from pacing.");
        AssertTrue(previewSource.Contains("ExpectedSoFar", StringComparison.Ordinal), "Hover preview should mention expected consumption.");
        AssertTrue(previewSource.Contains("DifferencePercentagePoints", StringComparison.Ordinal), "Hover preview should show whether usage is above or below expected.");
        AssertTrue(sillSource.Contains("new AiLimitsPreviewContent(_viewModel, pluginContentDirectory)", StringComparison.Ordinal), "Preview icons should resolve assets from the installed plugin content directory.");

        return Task.CompletedTask;
    }

    public static Task DefinesPopupIconButtonAccessibilityName()
    {
        var sourcePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "Views", "AiLimitsPopupContent.cs"));
        var source = File.ReadAllText(sourcePath);

        AssertTrue(source.Contains("AutomationProperties.SetName(RefreshIconButton, \"Atualizar uso\")", StringComparison.Ordinal), "The icon-only refresh button should define an accessible name.");
        AssertTrue(source.Contains("ToolTipService.SetToolTip(RefreshIconButton, \"Atualizar uso\")", StringComparison.Ordinal), "The icon-only refresh button should keep a tooltip.");

        return Task.CompletedTask;
    }

    public static Task PopupContentUsesSillPopupContentXamlRoot()
    {
        var xamlPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "Views", "AiLimitsPopupContent.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        AssertTrue(xaml.Contains("<api:SillPopupContent", StringComparison.Ordinal), "Popup content must use SillPopupContent as the XAML root.");
        AssertTrue(xaml.Contains("x:Class=\"WindowSillAiLimits.Views.AiLimitsPopupContent\"", StringComparison.Ordinal), "Popup XAML should bind to the popup code-behind.");

        return Task.CompletedTask;
    }

    public static Task PopupContentKeepsDetailedPanelContract()
    {
        var sourcePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "Views", "AiLimitsPopupContent.cs"));
        var source = File.ReadAllText(sourcePath);

        AssertTrue(source.Contains("BuildProviderSection", StringComparison.Ordinal), "The detailed popup should render provider sections.");
        AssertTrue(source.Contains("BuildWindowRow", StringComparison.Ordinal), "The detailed popup should render usage window rows.");
        AssertTrue(source.Contains("BuildPacingBlock", StringComparison.Ordinal), "The detailed popup should render pacing details.");
        AssertTrue(source.Contains("BuildApiCostPanel", StringComparison.Ordinal), "The detailed popup should render API-equivalent cost details in a consolidated panel.");
        AssertTrue(source.Contains("DifferencePercentagePoints", StringComparison.Ordinal), "The detailed popup should show p.p. difference.");
        AssertTrue(source.Contains("ProjectedExhaustion", StringComparison.Ordinal), "The detailed popup should show projected weekly exhaustion.");
        AssertTrue(source.Contains("ForecastImpact", StringComparison.Ordinal), "The detailed popup should show whether reset or exhaustion comes first.");
        AssertTrue(source.Contains("ElapsedDays", StringComparison.Ordinal), "The detailed popup should show elapsed window information.");
        AssertTrue(source.Contains("QueriedAt", StringComparison.Ordinal), "The detailed popup should show query timestamp.");
        AssertTrue(source.Contains("OpenSettingsPageForSill", StringComparison.Ordinal), "The detailed popup should expose the settings action.");
        AssertTrue(source.Contains("RefreshCommand", StringComparison.Ordinal), "The detailed popup should expose refresh actions.");

        return Task.CompletedTask;
    }

    public static Task PopupContentRendersConsolidatedApiCostPanel()
    {
        var sourcePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "Views", "AiLimitsPopupContent.cs"));
        var source = File.ReadAllText(sourcePath);

        AssertFalse(source.Contains("stack.Children.Add(BuildApiCostBlock(provider.ApiCostEstimate", StringComparison.Ordinal), "Provider cards should no longer render API cost blocks inline.");
        AssertTrue(source.Contains("BuildApiCostPanel", StringComparison.Ordinal), "The popup should render one consolidated API cost panel below provider cards.");
        AssertTrue(source.Contains("Custos API", StringComparison.Ordinal), "The consolidated cost panel should use the approved label.");
        AssertTrue(source.Contains("ToggleApiCostsCommand", StringComparison.Ordinal), "The consolidated cost panel should be expandable.");
        AssertTrue(source.Contains("CostRefreshCommand", StringComparison.Ordinal), "The consolidated cost panel should expose a cost-only refresh action.");
        AssertTrue(source.LastIndexOf("BuildProviderSection", StringComparison.Ordinal) < source.LastIndexOf("BuildApiCostPanel", StringComparison.Ordinal), "The consolidated cost panel should be declared after provider-card rendering.");

        return Task.CompletedTask;
    }

    public static Task ExpandedApiCostPanelUsesSingleColumnAndTallerPopup()
    {
        var repoRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var sourcePath = System.IO.Path.Combine(repoRoot, "src", "WindowSillAiLimits", "Views", "AiLimitsPopupContent.cs");
        var xamlPath = System.IO.Path.Combine(repoRoot, "src", "WindowSillAiLimits", "Views", "AiLimitsPopupContent.xaml");
        var source = File.ReadAllText(sourcePath);
        var xaml = File.ReadAllText(xamlPath);
        var gridStart = source.IndexOf("private Grid BuildApiCostProviderGrid()", StringComparison.Ordinal);
        var gridEnd = source.IndexOf("private static Border BuildUnavailableApiCostProvider", StringComparison.Ordinal);

        AssertTrue(gridStart >= 0 && gridEnd > gridStart, "Popup source should keep BuildApiCostProviderGrid as a distinct method.");
        var costGridSource = source[gridStart..gridEnd];

        AssertTrue(source.Contains("CollapsedPopupMaxHeight = 640", StringComparison.Ordinal), "Collapsed popup should keep the approved compact maximum height.");
        AssertTrue(source.Contains("ExpandedPopupMaxHeight = 900", StringComparison.Ordinal), "Expanded costs should allow the popup to grow on normal desktop screens.");
        AssertTrue(source.Contains("ApplyPopupHeight", StringComparison.Ordinal), "Popup should update MaxHeight when cost expansion changes.");
        AssertFalse(xaml.Contains("MaxHeight=\"640\"", StringComparison.Ordinal), "Popup XAML should not pin a low MaxHeight that forces scrolling when costs are expanded.");
        AssertFalse(costGridSource.Contains("GetProviderColumnCount", StringComparison.Ordinal), "Cost provider grid should not reuse the responsive provider-card column count.");
        AssertTrue(costGridSource.Contains("OrderApiCostProviders", StringComparison.Ordinal), "Cost provider grid should render providers in a stable order.");
        AssertTrue(costGridSource.Contains("Grid.SetColumn(block, 0)", StringComparison.Ordinal), "Expanded costs should render as one vertical column.");

        return Task.CompletedTask;
    }

    public static Task ApiCostTablePrioritizesReadablePriceColumn()
    {
        var sourcePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "Views", "AiLimitsPopupContent.cs"));
        var source = File.ReadAllText(sourcePath);
        var rowStart = source.IndexOf("private static Grid ModelCostRow", StringComparison.Ordinal);
        var rowEnd = source.IndexOf("private static string FormatUsd", StringComparison.Ordinal);

        AssertTrue(rowStart >= 0 && rowEnd > rowStart, "Popup source should keep ModelCostRow and Cell together for table layout.");
        var tableSource = source[rowStart..rowEnd];

        AssertTrue(tableSource.Contains("CostTableModelColumnWidth", StringComparison.Ordinal), "Model column should use an explicit compact width instead of consuming the whole spare area.");
        AssertTrue(tableSource.Contains("new GridLength(1, GridUnitType.Star)", StringComparison.Ordinal), "Valor/token should be the flexible column that receives spare width.");
        AssertTrue(tableSource.Contains("isPrice: true", StringComparison.Ordinal), "Price cells should be handled explicitly.");
        AssertTrue(tableSource.Contains("TextAlignment.Left", StringComparison.Ordinal), "Valor/token should align left for readability.");
        AssertTrue(tableSource.Contains("TextWrapping = isPrice ? TextWrapping.Wrap", StringComparison.Ordinal), "Valor/token should wrap instead of being truncated.");
        AssertTrue(tableSource.Contains("MaxLines = isPrice ? 2", StringComparison.Ordinal), "Long provider price summaries should be limited to two readable lines.");
        AssertFalse(tableSource.Contains("MaxWidth = alignment == TextAlignment.Left ? 120 : 86", StringComparison.Ordinal), "Cost table cells should not use the old narrow MaxWidth limits.");
        AssertTrue(source.Contains("ModelCostRow(\"Modelo\", \"Tokens\", \"Valor/token\", \"Custo\"", StringComparison.Ordinal), "The approved cost table headers should remain visible.");

        return Task.CompletedTask;
    }

    public static Task PopupContentUsesCompactTypography()
    {
        var repoRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var sourcePath = System.IO.Path.Combine(repoRoot, "src", "WindowSillAiLimits", "Views", "AiLimitsPopupContent.cs");
        var xamlPath = System.IO.Path.Combine(repoRoot, "src", "WindowSillAiLimits", "Views", "AiLimitsPopupContent.xaml");
        var source = File.ReadAllText(sourcePath);
        var xaml = File.ReadAllText(xamlPath);

        AssertTrue(source.Contains("CompactFontSize = 12", StringComparison.Ordinal), "The expanded popup should share the compact bar's 12px text scale.");
        AssertTrue(source.Contains("ApplyCompactText", StringComparison.Ordinal), "Popup text should use a shared compact typography helper.");
        AssertTrue(source.Contains("TextTrimming = TextTrimming.CharacterEllipsis", StringComparison.Ordinal), "Long popup labels and values should trim instead of overlapping.");
        AssertTrue(source.Contains("TextWrapping = TextWrapping.NoWrap", StringComparison.Ordinal), "Dense popup detail rows should stay single-line.");
        AssertFalse(xaml.Contains("FontSize=\"18\"", StringComparison.Ordinal), "Popup XAML should not force large title text that fights the compact layout.");

        return Task.CompletedTask;
    }

    public static Task CompactBarOwnsPopupTapHandling()
    {
        var sillPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "AiLimitsSill.cs"));
        var sillSource = File.ReadAllText(sillPath);
        var barPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "Views", "AiLimitsBarView.cs"));
        var barSource = File.ReadAllText(barPath);

        AssertTrue(sillSource.Contains("_barView.Clicked += OnBarClicked", StringComparison.Ordinal), "The compact bar should own popup click handling.");
        AssertTrue(barSource.Contains("SillButtonStyle", StringComparison.Ordinal), "The compact bar should use the host sill button style when available.");
        AssertTrue(barSource.Contains("public event RoutedEventHandler? Clicked", StringComparison.Ordinal), "The compact bar should expose one click route for the sill.");
        AssertTrue(barSource.Contains("Tapped += OnFallbackTapped", StringComparison.Ordinal), "The compact bar should keep a tap fallback for host event routing.");
        AssertTrue(barSource.Contains("PointerReleased += OnFallbackPointerReleased", StringComparison.Ordinal), "The compact bar should keep a pointer-release fallback for host event routing.");
        AssertTrue(barSource.Contains("TimeSpan.FromMilliseconds(250)", StringComparison.Ordinal), "Fallback routes should debounce duplicate popup opens.");
        AssertTrue(sillSource.Contains("Content = new AiLimitsPopupContent", StringComparison.Ordinal), "Click should open the detailed popup content, not preview content.");
        AssertTrue(sillSource.Contains("_popup.ShowAsync(_view)", StringComparison.Ordinal), "Popup placement should use the API-supported SillView target.");
        AssertTrue(sillSource.Contains("_isPopupShowing", StringComparison.Ordinal), "Popup opening should guard against duplicate taps.");
        AssertTrue(barSource.Contains("Colors.Transparent", StringComparison.Ordinal), "The compact bar should be hit-testable across its full area.");

        return Task.CompletedTask;
    }

    public static Task CompactBarUsesProviderIconsInsteadOfPlaceholderGlyphs()
    {
        var barPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "Views", "AiLimitsBarView.cs"));
        var source = File.ReadAllText(barPath);

        AssertTrue(source.Contains("BuildProvider(UsageProvider.Codex", StringComparison.Ordinal), "OpenAI/Codex should be represented by a provider-specific icon.");
        AssertTrue(source.Contains("BuildProvider(UsageProvider.Claude", StringComparison.Ordinal), "Claude/Anthropic should be represented by a provider-specific icon.");
        AssertTrue(source.Contains("openai-mark.svg", StringComparison.Ordinal), "The compact bar should draw an OpenAI icon instead of a text placeholder.");
        AssertTrue(source.Contains("anthropic-mark.svg", StringComparison.Ordinal), "The compact bar should draw a Claude/Anthropic icon instead of a text placeholder.");
        AssertFalse(source.Contains("BuildProvider(\"◎\"", StringComparison.Ordinal), "The compact bar should not render the old OpenAI placeholder glyph.");
        AssertFalse(source.Contains("BuildProvider(\"◇\"", StringComparison.Ordinal), "The compact bar should not render the old Claude placeholder glyph.");

        return Task.CompletedTask;
    }

    public static Task CompactBarUsesPackagedSvgProviderIcons()
    {
        var repoRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var barPath = System.IO.Path.Combine(repoRoot, "src", "WindowSillAiLimits", "Views", "AiLimitsBarView.cs");
        var projectPath = System.IO.Path.Combine(repoRoot, "src", "WindowSillAiLimits", "WindowSillAiLimits.csproj");
        var sillPath = System.IO.Path.Combine(repoRoot, "src", "WindowSillAiLimits", "AiLimitsSill.cs");
        var openAiIconPath = System.IO.Path.Combine(repoRoot, "src", "WindowSillAiLimits", "Assets", "openai-mark.svg");
        var claudeIconPath = System.IO.Path.Combine(repoRoot, "src", "WindowSillAiLimits", "Assets", "anthropic-mark.svg");
        var barSource = File.ReadAllText(barPath);
        var projectSource = File.ReadAllText(projectPath);
        var sillSource = File.ReadAllText(sillPath);

        AssertTrue(barSource.Contains("SvgImageSource", StringComparison.Ordinal), "The compact bar should render provider marks from SVG assets.");
        AssertTrue(barSource.Contains("openai-mark.svg", StringComparison.Ordinal), "The OpenAI bar icon should use the packaged SVG asset.");
        AssertTrue(barSource.Contains("anthropic-mark.svg", StringComparison.Ordinal), "The Claude bar icon should use the packaged SVG asset.");
        AssertTrue(barSource.Contains("\"WindowSillAiLimits\"", StringComparison.Ordinal), "The SVG resolver should tolerate plugin content directories rooted above the assembly resources.");
        AssertTrue(sillSource.Contains("pluginInfo.GetPluginContentDirectory()", StringComparison.Ordinal), "The installed plugin should resolve SVG icons from its WindowSill content directory.");
        AssertTrue(File.Exists(openAiIconPath), "The OpenAI SVG asset should exist in the extension project.");
        AssertTrue(File.Exists(claudeIconPath), "The Anthropic SVG asset should exist in the extension project.");
        AssertTrue(projectSource.Contains("Assets\\*.svg", StringComparison.Ordinal), "The extension package should include provider SVG assets.");

        return Task.CompletedTask;
    }

    public static Task CompactBarAlignsWithNeighboringSillItems()
    {
        var barPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "Views", "AiLimitsBarView.cs"));
        var source = File.ReadAllText(barPath);

        AssertTrue(source.Contains("HorizontalAlignment = HorizontalAlignment.Left", StringComparison.Ordinal), "The compact bar should not reserve a centered empty block.");
        AssertTrue(source.Contains("HorizontalContentAlignment = HorizontalAlignment.Left", StringComparison.Ordinal), "The compact button content should align next to neighboring sill items.");
        AssertTrue(source.Contains("new Thickness(2, 0, 2, 0)", StringComparison.Ordinal), "The compact bar should avoid extra vertical padding.");
        AssertTrue(source.Contains("LineStackingStrategy.BlockLineHeight", StringComparison.Ordinal), "The compact text should use a tight line box for vertical centering.");

        return Task.CompletedTask;
    }

    public static Task PopupClickPathWritesSanitizedDiagnostics()
    {
        var sillPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "AiLimitsSill.cs"));
        var sillSource = File.ReadAllText(sillPath);
        var diagnosticsPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "Services", "AiLimitsDiagnostics.cs"));
        var diagnosticsSource = File.ReadAllText(diagnosticsPath);

        AssertTrue(sillSource.Contains("AiLimitsDiagnostics.Info(\"compact bar click received\")", StringComparison.Ordinal), "Click handling should leave a host-visible diagnostic breadcrumb.");
        AssertTrue(sillSource.Contains("AiLimitsDiagnostics.Error(\"popup show failed\", ex)", StringComparison.Ordinal), "Popup failures should be logged outside Debug output.");
        AssertTrue(diagnosticsSource.Contains("UsageMessageSanitizer.Sanitize", StringComparison.Ordinal), "Diagnostics must sanitize messages before writing them.");
        AssertTrue(diagnosticsSource.Contains("diagnostics.log", StringComparison.Ordinal), "Diagnostics should write to a predictable local log file.");

        return Task.CompletedTask;
    }

    public static Task PreservesOptionalClaudeSonnetWindow()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var claude = new ProviderUsage(
            UsageProvider.Claude,
            "Anthropic",
            "Max",
            ProviderStatus.Ok,
            [
                new UsageWindow("5h", "5h", 70, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                new UsageWindow("7d", "7d", 50, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3)),
                new UsageWindow("seven_day_sonnet", "Sonnet", 34, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3)),
            ],
            now,
            null);

        AssertTrue(claude.Windows.Any(window => window.Label == "Sonnet" && window.UsedPercent == 34), "Claude Sonnet window must remain available for the generic expanded renderer.");

        return Task.CompletedTask;
    }

    public static Task DefinesSmallNonSensitiveSettings()
    {
        AssertEqual(900, AiLimitsSettings.RefreshIntervalSeconds.DefaultValue);
        AssertEqual(900, AiLimitsSettings.DefaultRefreshIntervalSeconds);
        AssertEqual("codex", AiLimitsSettings.CodexCommandPath.DefaultValue);
        AssertEqual("claude", AiLimitsSettings.ClaudeCommandPath.DefaultValue);
        AssertEqual(true, AiLimitsSettings.ShowProviderNamesInBar.DefaultValue);
        AssertEqual(false, AiLimitsSettings.ShowExpectedInBar.DefaultValue);
        AssertEqual(true, AiLimitsSettings.ShowOverExpectedAlerts.DefaultValue);
        AssertEqual(true, AiLimitsSettings.ShowPreviewFlyout.DefaultValue);
        AssertEqual(false, AiLimitsSettings.UseMockData.DefaultValue);

        var settingNames = AiLimitsSettings.All.Select(setting => setting.Name).ToArray();
        AssertTrue(settingNames.Length == settingNames.Distinct(StringComparer.Ordinal).Count(), "Setting names must be unique.");
        AssertFalse(settingNames.Any(name => name.Contains("token", StringComparison.OrdinalIgnoreCase)), "Settings must not imply credential storage.");
        AssertFalse(settingNames.Any(name => name.Contains("auth", StringComparison.OrdinalIgnoreCase)), "Settings must not imply credential storage.");

        return Task.CompletedTask;
    }

    public static Task ClampsRefreshIntervalSettings()
    {
        var settings = new TestSettingsProvider();

        AssertEqual(300, AiLimitsSettings.ClampRefreshIntervalSeconds(1));
        AssertEqual(600, AiLimitsSettings.ClampRefreshIntervalSeconds(600));
        AssertEqual(3600, AiLimitsSettings.ClampRefreshIntervalSeconds(99999));
        AssertEqual(TimeSpan.FromSeconds(900), AiLimitsSettings.GetRefreshInterval(settings));

        settings.SetSetting(AiLimitsSettings.RefreshIntervalSeconds, 1);
        AssertEqual(TimeSpan.FromSeconds(300), AiLimitsSettings.GetRefreshInterval(settings));

        settings.SetSetting(AiLimitsSettings.RefreshIntervalSeconds, 1800);
        AssertEqual(TimeSpan.FromSeconds(1800), AiLimitsSettings.GetRefreshInterval(settings));

        settings.SetSetting(AiLimitsSettings.RefreshIntervalSeconds, 99999);
        AssertEqual(TimeSpan.FromSeconds(3600), AiLimitsSettings.GetRefreshInterval(settings));

        return Task.CompletedTask;
    }

    public static Task OffersFiveToSixtyMinuteRefreshPresets()
    {
        AssertEqual(5, AiLimitsSettings.RefreshIntervalPresetsSeconds.Count);
        AssertTrue(
            AiLimitsSettings.RefreshIntervalPresetsSeconds.SequenceEqual([300, 600, 900, 1800, 3600]),
            "Refresh presets should be 5, 10, 15, 30 and 60 minutes.");
        AssertTrue(
            AiLimitsSettings.RefreshIntervalPresetsSeconds.Contains(AiLimitsSettings.DefaultRefreshIntervalSeconds),
            "Default refresh interval should be one of the offered presets.");

        return Task.CompletedTask;
    }

    public static Task DefinesFourHourCostRefreshSetting()
    {
        var settings = new TestSettingsProvider();

        AssertEqual(14_400, AiLimitsSettings.CostRefreshIntervalSeconds.DefaultValue);
        AssertEqual(14_400, AiLimitsSettings.DefaultCostRefreshIntervalSeconds);
        AssertEqual(TimeSpan.FromHours(4), AiLimitsSettings.GetCostRefreshInterval(settings));
        AssertEqual(3_600, AiLimitsSettings.ClampCostRefreshIntervalSeconds(1));
        AssertEqual(28_800, AiLimitsSettings.ClampCostRefreshIntervalSeconds(28_800));
        AssertEqual(43_200, AiLimitsSettings.ClampCostRefreshIntervalSeconds(99_999));
        AssertTrue(
            AiLimitsSettings.CostRefreshIntervalPresetsSeconds.SequenceEqual([3600, 7200, 14400, 28800, 43200]),
            "Cost refresh presets should be 1h, 2h, 4h, 8h and 12h.");

        settings.SetSetting(AiLimitsSettings.CostRefreshIntervalSeconds, 7_200);

        AssertEqual(TimeSpan.FromHours(2), AiLimitsSettings.GetCostRefreshInterval(settings));

        return Task.CompletedTask;
    }

    public static Task MigratesLegacyDefaultRefreshIntervalToFifteenMinutes()
    {
        var settings = new TestSettingsProvider();
        settings.SetSetting(AiLimitsSettings.RefreshIntervalSeconds, AiLimitsSettings.LegacyDefaultRefreshIntervalSeconds);

        AssertEqual(TimeSpan.FromSeconds(900), AiLimitsSettings.GetRefreshInterval(settings));

        AiLimitsSettings.MigrateLegacyRefreshInterval(settings);

        AssertEqual(900, settings.GetSetting(AiLimitsSettings.RefreshIntervalSeconds));
        AssertEqual(TimeSpan.FromSeconds(900), AiLimitsSettings.GetRefreshInterval(settings));

        return Task.CompletedTask;
    }

    public static async Task RateLimitCooldownSkipsLiveCallsAndServesCache()
    {
        var clock = new DateTimeOffset(2026, 5, 25, 10, 0, 0, TimeSpan.FromHours(-3));
        var priorClaude = new ProviderUsage(
            UsageProvider.Claude,
            "Anthropic",
            "Claude Pro",
            ProviderStatus.Ok,
            [new UsageWindow("7d", "7d", 15, clock.AddDays(5), TimeSpan.FromDays(7), clock.AddDays(-2))],
            clock,
            null);
        var rateLimited = new ProviderUsage(
            UsageProvider.Claude,
            "Anthropic",
            null,
            ProviderStatus.Unavailable,
            [],
            clock,
            "Claude usage endpoint rate limited (HTTP 429).");
        var probe = new SequenceProbe(
            UsageProvider.Claude,
            [ProbeStep.Success(priorClaude), ProbeStep.Success(rateLimited)]);

        using var service = new UsageRefreshService([probe], TimeSpan.FromMinutes(5), () => clock);

        _ = await service.RefreshAsync();              // good data, no cooldown
        clock = clock.AddMinutes(5);
        _ = await service.RefreshAsync();              // 429 -> cooldown set, serves stale
        AssertEqual(2, probe.CallCount);

        clock = clock.AddMinutes(5);                   // still inside the 15-minute cooldown
        var staleSnapshot = await service.RefreshAsync();
        AssertEqual(2, probe.CallCount);               // live call skipped during cooldown

        var claude = staleSnapshot.GetProvider(UsageProvider.Claude);
        AssertEqual(ProviderStatus.Stale, claude?.Status);
        AssertTrue(claude?.Windows.Any(window => window.Id == "7d" && window.UsedPercent == 15) == true, "Cached Claude usage must remain visible during the rate-limit cooldown.");

        clock = clock.AddMinutes(15);                  // cooldown expired -> live call retried
        _ = await service.RefreshAsync();
        AssertEqual(3, probe.CallCount);
    }

    public static Task AppliesRefreshIntervalSettingChangesToActiveSill()
    {
        var sourcePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "AiLimitsSill.cs"));
        var source = File.ReadAllText(sourcePath);

        AssertTrue(source.Contains("_settingsProvider.SettingChanged += OnSettingChanged", StringComparison.Ordinal), "The sill should observe settings changes.");
        AssertTrue(source.Contains("_settingsProvider.SettingChanged -= OnSettingChanged", StringComparison.Ordinal), "The sill should detach settings changes on dispose.");
        AssertTrue(source.Contains("RefreshIntervalSeconds.Name", StringComparison.Ordinal), "Refresh interval changes should be recognized by setting name.");
        AssertTrue(source.Contains("_refreshService.UpdateRefreshInterval(AiLimitsSettings.GetRefreshInterval(_settingsProvider))", StringComparison.Ordinal), "Refresh interval changes should update the active refresh service.");

        return Task.CompletedTask;
    }

    public static Task UpdatesRefreshServiceIntervalWhileMonitoring()
    {
        var now = new DateTimeOffset(2026, 5, 24, 22, 21, 0, TimeSpan.FromHours(-3));
        using var service = new UsageRefreshService(
            [new DelayedProbe(now)],
            TimeSpan.FromSeconds(60),
            () => now);

        AssertEqual(TimeSpan.FromSeconds(60), service.RefreshInterval);

        service.StartMonitoring();
        service.UpdateRefreshInterval(TimeSpan.FromSeconds(90));

        AssertEqual(TimeSpan.FromSeconds(90), service.RefreshInterval);

        return Task.CompletedTask;
    }

    public static async Task InitialMonitoringBackfillsMissingApiCostsOnce()
    {
        var now = new DateTimeOffset(2026, 5, 26, 11, 30, 0, TimeSpan.FromHours(-3));
        var claude = ProviderWithoutCost(UsageProvider.Claude, "Anthropic", now);
        var probe = new SequenceProbe(UsageProvider.Claude, [ProbeStep.Success(claude)]);
        var estimator = new FixedApiCostEstimator(CostEstimateFor("claude-opus-4-7", 1_000_000, 5m, now));
        using var service = new UsageRefreshService(
            [probe],
            TimeSpan.FromHours(1),
            () => now,
            apiCostEstimator: estimator,
            costRefreshInterval: TimeSpan.FromHours(4));

        service.StartMonitoring();

        await WaitUntilAsync(
            () => service.CurrentSnapshot.GetProvider(UsageProvider.Claude)?.ApiCostEstimate is not null,
            TimeSpan.FromSeconds(3));

        AssertEqual(1, probe.CallCount);
        AssertEqual(1, estimator.CallCount);
        AssertEqual(5m, service.CurrentSnapshot.GetProvider(UsageProvider.Claude)?.ApiCostEstimate?.TotalCostUsd);
    }

    public static async Task MockDataMatchesApprovedCompactSnapshot()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var providers = new List<ProviderUsage>();

        foreach (var probe in MockUsageProbe.CreateDefault(() => now))
        {
            providers.Add(await probe.ReadAsync(CancellationToken.None));
        }

        using var service = new StubUsageRefreshService(new UsageSnapshot(providers, now));
        using var viewModel = new AiLimitsViewModel(service, () => now);

        AssertEqual("OpenAI 5h 100% 7d 59% | Anthropic 5h 70% 7d 50%", viewModel.CollapsedSummaryText);
        AssertEqual(ProviderStatus.Ok, viewModel.OpenAiStatus);
        AssertEqual(ProviderStatus.Warning, viewModel.ClaudeStatus);
        AssertTrue(providers.Single(provider => provider.Provider == UsageProvider.Claude).Windows.Any(window => window.Label == "Sonnet"), "Mock Claude data should include the approved Sonnet detail window.");
    }

    public static Task AiLimitsSillSelectsRealProbesByDefault()
    {
        var settings = new TestSettingsProvider();
        var defaultProbes = CreateSillProbes(settings);

        AssertEqual(2, defaultProbes.Length);
        AssertTrue(defaultProbes.Any(probe => probe is CodexUsageProbe), "AiLimitsSill should use the Codex app-server probe by default.");
        AssertTrue(defaultProbes.Any(probe => probe is ClaudeUsageProbe), "AiLimitsSill should use the Claude OAuth probe by default.");
        AssertFalse(defaultProbes.Any(probe => probe is MockUsageProbe), "Mock probes must be opt-in.");

        settings.SetSetting(AiLimitsSettings.UseMockData, true);
        var mockProbes = CreateSillProbes(settings);

        AssertEqual(2, mockProbes.Length);
        AssertTrue(mockProbes.All(probe => probe is MockUsageProbe), "Mock probes should only be selected when UseMockData is enabled.");
        AssertTrue(mockProbes.Any(probe => probe.Provider == UsageProvider.Codex), "Mock probe set should include Codex.");
        AssertTrue(mockProbes.Any(probe => probe.Provider == UsageProvider.Claude), "Mock probe set should include Claude.");

        return Task.CompletedTask;
    }

    public static Task ConfiguresPackageOutputForLocalValidation()
    {
        var projectPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowSillAiLimits", "WindowSillAiLimits.csproj"));
        var project = File.ReadAllText(projectPath);

        AssertTrue(project.Contains("<PackageOutputPath>..\\..\\artifacts\\</PackageOutputPath>", StringComparison.Ordinal), "Package output should point to the repo artifacts directory.");
        AssertTrue(project.Contains("<GeneratePackageOnBuild>true</GeneratePackageOnBuild>", StringComparison.Ordinal), "Release builds should create the local package artifact.");
        AssertTrue(project.Contains("IncludeWinUiGeneratedResourcesInPackage", StringComparison.Ordinal), "Package output should include generated WinUI resources needed by the host.");
        AssertTrue(project.Contains("$(AssemblyName).pri", StringComparison.Ordinal), "The WinUI PRI resource index should be packaged.");
        AssertTrue(project.Contains("AiLimitsPopupContent.xbf", StringComparison.Ordinal), "The compiled popup XAML resource should be packaged.");

        return Task.CompletedTask;
    }

    public static Task ResolvesWindowsCommandShimsWithoutShellExecution()
    {
        var startInfo = CommandStartInfoFactory.Create("codex", ["--version"]);

        AssertFalse(startInfo.UseShellExecute, "Command process must keep shell execution disabled so stdio can be redirected.");
        AssertTrue(startInfo.RedirectStandardInput, "Command process must redirect stdin for JSON-RPC.");
        AssertTrue(startInfo.RedirectStandardOutput, "Command process must redirect stdout for JSON-RPC.");

        return Task.CompletedTask;
    }

    public static Task ParsesCodexPrimaryAndSecondaryRateLimits()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var accountJson = """
            {"result":{"account":{"email":"user@example.test","plan":"Plus"}}}
            """;
        var rateLimitsJson = """
            {"result":{"rateLimits":{"primary":{"usedPercent":100,"resetsAt":"2026-05-24T21:10:00-03:00"},"secondary":{"usedPercent":59,"resetsAt":"2026-05-30T09:00:00-03:00"}}}}
            """;

        var usage = CodexRateLimitParser.Parse(accountJson, rateLimitsJson, now);

        AssertEqual(UsageProvider.Codex, usage.Provider);
        AssertEqual("OpenAI", usage.DisplayName);
        AssertEqual("Plus", usage.PlanLabel);
        AssertEqual(ProviderStatus.Warning, usage.Status);
        AssertEqual(2, usage.Windows.Count);
        AssertEqual("5h", usage.Windows[0].Id);
        AssertEqual(100, usage.Windows[0].UsedPercent);
        AssertEqual("7d", usage.Windows[1].Id);
        AssertEqual(59, usage.Windows[1].UsedPercent);

        return Task.CompletedTask;
    }

    public static Task ParsesCodexSchemaIntegerResetAndDurationFields()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_764_000_000).ToLocalTime();
        var reset = now.AddHours(1);
        var accountJson = """{"result":{"account":{"type":"chatgpt","planType":"plus"}}}""";
        var rateLimitsJson = "{\"result\":{\"rateLimits\":{\"primary\":{\"usedPercent\":64,\"resetsAt\":" +
            reset.ToUnixTimeSeconds() +
            ",\"windowDurationMins\":300},\"secondary\":{\"usedPercent\":32,\"resetsAt\":" +
            now.AddDays(2).ToUnixTimeSeconds() +
            ",\"windowDurationMins\":10080}}}}";

        var usage = CodexRateLimitParser.Parse(accountJson, rateLimitsJson, now);

        AssertEqual("plus", usage.PlanLabel);
        AssertEqual(TimeSpan.FromMinutes(300), usage.Windows[0].Duration);
        AssertEqual(reset.ToUnixTimeSeconds(), usage.Windows[0].ResetsAt?.ToUnixTimeSeconds());
        AssertEqual(TimeSpan.FromMinutes(10080), usage.Windows[1].Duration);

        return Task.CompletedTask;
    }

    public static Task ParsesCodexRateLimitsByLimitId()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var accountJson = """{"result":{"planType":"Team"}}""";
        var rateLimitsJson = """
            {"result":{"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":72,"resetsAt":"2026-05-24T22:00:00-03:00","windowDurationMins":300},"secondary":{"usedPercent":41,"resetsAt":"2026-05-30T09:00:00-03:00","windowDurationMins":10080}}}}}
            """;

        var usage = CodexRateLimitParser.Parse(accountJson, rateLimitsJson, now);

        AssertEqual("Team", usage.PlanLabel);
        AssertEqual(2, usage.Windows.Count);
        AssertTrue(usage.Windows.Any(window => window.Id == "5h" && window.UsedPercent == 72), "Expected 5h window from limit id data.");
        AssertTrue(usage.Windows.Any(window => window.Id == "7d" && window.UsedPercent == 41), "Expected 7d window from limit id data.");
        AssertEqual(ProviderStatus.Ok, usage.Status);

        return Task.CompletedTask;
    }

    public static Task MapsCodexJsonRpcErrorsToUnavailableProvider()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var usage = CodexRateLimitParser.Parse(
            """{"result":{}}""",
            """{"error":{"code":401,"message":"not authenticated"}}""",
            now);

        AssertEqual(UsageProvider.Codex, usage.Provider);
        AssertEqual(ProviderStatus.Unavailable, usage.Status);
        AssertEqual(0, usage.Windows.Count);
        AssertTrue(usage.Message?.Contains("not authenticated", StringComparison.OrdinalIgnoreCase) == true, "Unavailable provider should keep an actionable error message.");

        return Task.CompletedTask;
    }

    public static Task MapsCodexAccountJsonRpcErrorsToUnavailableProvider()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var usage = CodexRateLimitParser.Parse(
            """{"error":{"code":401,"message":"not authenticated"}}""",
            """{"result":{"rateLimits":{"primary":{"usedPercent":10}}}}""",
            now);

        AssertEqual(ProviderStatus.Unavailable, usage.Status);
        AssertTrue(usage.Message?.Contains("not authenticated", StringComparison.OrdinalIgnoreCase) == true, "Account error should be surfaced.");

        return Task.CompletedTask;
    }

    public static Task SanitizesCodexParserErrorMessages()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var usage = CodexRateLimitParser.Parse(
            """{"result":{"account":{"plan":"Plus"}}}""",
            """{"error":{"code":401,"message":"not authenticated Authorization: Bearer codex-secret accessToken=visible-secret"}}""",
            now);

        AssertEqual(ProviderStatus.Unavailable, usage.Status);
        AssertTrue(usage.Message?.Contains("not authenticated", StringComparison.OrdinalIgnoreCase) == true, "Sanitized message should keep the actionable reason.");
        AssertTrue(usage.Message?.Contains("[redacted]", StringComparison.OrdinalIgnoreCase) == true, "Sensitive values should be redacted.");
        AssertFalse(usage.Message?.Contains("codex-secret", StringComparison.Ordinal) == true, "Bearer tokens must not be surfaced.");
        AssertFalse(usage.Message?.Contains("visible-secret", StringComparison.Ordinal) == true, "Access tokens must not be surfaced.");

        return Task.CompletedTask;
    }

    public static async Task CodexProbeSendsInitializeAccountAndRateLimitRequests()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var client = new FakeCodexAppServerClient(
            """{"result":{"capabilities":{}}}""",
            """{"result":{"account":{"plan":"Plus"}}}""",
            """{"result":{"rateLimits":{"primary":{"usedPercent":10},"secondary":{"usedPercent":20}}}}""");
        var probe = new CodexUsageProbe(client, () => now);

        var usage = await probe.ReadAsync(CancellationToken.None);

        AssertEqual(UsageProvider.Codex, probe.Provider);
        AssertEqual(ProviderStatus.Ok, usage.Status);
        AssertEqual("Plus", usage.PlanLabel);
        AssertEqual("initialize,account/read,account/rateLimits/read", string.Join(",", client.Methods));
        AssertTrue(client.Parameters.All(parameters => parameters is not null), "Codex app-server requests should always include a params object.");
    }

    public static async Task CodexProbeMapsClientFailureToUnavailable()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var probe = new CodexUsageProbe(new FailingCodexAppServerClient("codex command not found"), () => now);

        var usage = await probe.ReadAsync(CancellationToken.None);

        AssertEqual(ProviderStatus.Unavailable, usage.Status);
        AssertTrue(usage.Message?.Contains("codex command not found", StringComparison.OrdinalIgnoreCase) == true, "Failure message should be preserved.");
    }

    public static Task ParsesClaudeOAuthUsageResponse()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var usageJson = """
            {
              "five_hour": {"utilization": 42.7, "resets_at": "2026-05-24T22:30:00Z"},
              "seven_day": {"utilization": 86.2, "resets_at": "2026-05-30T12:00:00Z"},
              "seven_day_sonnet": {"utilization": 4.2, "resets_at": "2026-05-30T12:00:00Z"},
              "extra_usage": {"is_enabled": true, "monthly_limit": 5000, "used_credits": 2500}
            }
            """;

        var usage = ClaudeRateLimitParser.Parse(usageJson, "Claude Max 5x", now);

        AssertEqual(UsageProvider.Claude, usage.Provider);
        AssertEqual("Anthropic", usage.DisplayName);
        AssertEqual("Claude Max 5x", usage.PlanLabel);
        AssertEqual(ProviderStatus.Warning, usage.Status);
        AssertEqual(4, usage.Windows.Count);
        AssertTrue(usage.Windows.Any(window => window.Id == "5h" && window.UsedPercent == 42.7), "Expected Claude 5h utilization.");
        AssertTrue(usage.Windows.Any(window => window.Id == "7d" && window.UsedPercent == 86.2), "Expected Claude 7d utilization.");
        AssertTrue(usage.Windows.Any(window => window.Id == "seven_day_sonnet" && window.Label == "Sonnet" && window.UsedPercent == 4.2), "Expected Claude Sonnet utilization.");
        AssertTrue(usage.Windows.Any(window => window.Id == "extra_usage" && window.Label == "Extra" && window.UsedPercent == 50), "Expected Claude extra usage percentage.");

        return Task.CompletedTask;
    }

    public static Task ParsesClaudeUnifiedRateLimitHeaders()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var usage = ClaudeRateLimitParser.Parse(
            "{}",
            "Claude Pro",
            now,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["anthropic-ratelimit-unified-5h-utilization"] = "0.42",
                ["anthropic-ratelimit-unified-5h-reset"] = "1779991200",
                ["anthropic-ratelimit-unified-7d-utilization"] = "0.35",
                ["anthropic-ratelimit-unified-7d-reset"] = "1780509600",
            });

        AssertEqual(ProviderStatus.Ok, usage.Status);
        AssertEqual(2, usage.Windows.Count);
        AssertTrue(usage.Windows.Any(window => window.Id == "5h" && Math.Abs(window.UsedPercent.GetValueOrDefault() - 42) < 0.001), "Expected Claude 5h utilization from unified headers.");
        AssertTrue(usage.Windows.Any(window => window.Id == "7d" && Math.Abs(window.UsedPercent.GetValueOrDefault() - 35) < 0.001), "Expected Claude 7d utilization from unified headers.");
        AssertTrue(usage.Windows.All(window => window.ResetsAt is not null), "Unified header windows should preserve reset timestamps.");

        return Task.CompletedTask;
    }

    public static Task ParsesClaudeOptionalOpusUsageWindow()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var usageJson = """
            {
              "five_hour": {"utilization": 12, "resets_at": "2026-05-24T22:30:00Z"},
              "seven_day": {"utilization": 34, "resets_at": "2026-05-30T12:00:00Z"},
              "seven_day_opus": {"utilization": 56, "resets_at": "2026-05-30T12:00:00Z"}
            }
            """;

        var usage = ClaudeRateLimitParser.Parse(usageJson, "Claude Max", now);

        AssertEqual(ProviderStatus.Ok, usage.Status);
        AssertTrue(usage.Windows.Any(window => window.Id == "7d" && window.Label == "7d" && window.UsedPercent == 34), "Expected all-model Claude 7d window to remain visible.");
        AssertTrue(usage.Windows.Any(window => window.Id == "seven_day_opus" && window.Label == "Opus" && window.UsedPercent == 56), "Expected optional Claude Opus utilization.");
        AssertFalse(usage.Windows.Any(window => window.Id == "seven_day_sonnet"), "Absent optional Claude windows should not be synthesized.");

        return Task.CompletedTask;
    }

    public static Task ParsesClaudeStatuslineFallbackRateLimits()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var usageJson = """
            {
              "rate_limits": {
                "five_hour": {"used_percentage": 12, "resets_at": "2026-05-24T22:30:00Z"},
                "seven_day": {"used_percentage": 34, "resets_at": "2026-05-30T12:00:00Z"}
              }
            }
            """;

        var usage = ClaudeRateLimitParser.Parse(usageJson, "Claude Pro", now);

        AssertEqual(ProviderStatus.Ok, usage.Status);
        AssertEqual(2, usage.Windows.Count);
        AssertTrue(usage.Windows.Any(window => window.Id == "5h" && window.UsedPercent == 12), "Expected 5h from statusline rate_limits.");
        AssertTrue(usage.Windows.Any(window => window.Id == "7d" && window.UsedPercent == 34), "Expected 7d from statusline rate_limits.");

        return Task.CompletedTask;
    }

    public static Task MapsClaudeUsageErrorsToUnavailableProvider()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var usage = ClaudeRateLimitParser.Parse(
            """{"error":{"type":"authentication_error","message":"expired token"}}""",
            "Claude Pro",
            now);

        AssertEqual(UsageProvider.Claude, usage.Provider);
        AssertEqual(ProviderStatus.Unavailable, usage.Status);
        AssertEqual(0, usage.Windows.Count);
        AssertTrue(usage.Message?.Contains("expired token", StringComparison.OrdinalIgnoreCase) == true, "Claude error should be surfaced.");

        return Task.CompletedTask;
    }

    public static Task MapsClaudeNonSubscriberErrorsToUnavailableProvider()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var usage = ClaudeRateLimitParser.Parse(
            """{"error":{"type":"subscription_required","message":"Claude subscription required"}}""",
            "Claude Free",
            now);

        AssertEqual(ProviderStatus.Unavailable, usage.Status);
        AssertEqual(0, usage.Windows.Count);
        AssertTrue(usage.Message?.Contains("subscription required", StringComparison.OrdinalIgnoreCase) == true, "Non-subscriber errors should be actionable.");

        return Task.CompletedTask;
    }

    public static Task SanitizesClaudeParserErrorMessages()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var usage = ClaudeRateLimitParser.Parse(
            """{"error":{"type":"authentication_error","message":"expired token Authorization: Bearer claude-secret refreshToken: visible-secret"}}""",
            "Claude Pro",
            now);

        AssertEqual(ProviderStatus.Unavailable, usage.Status);
        AssertTrue(usage.Message?.Contains("expired token", StringComparison.OrdinalIgnoreCase) == true, "Sanitized message should keep the actionable reason.");
        AssertTrue(usage.Message?.Contains("[redacted]", StringComparison.OrdinalIgnoreCase) == true, "Sensitive values should be redacted.");
        AssertFalse(usage.Message?.Contains("claude-secret", StringComparison.Ordinal) == true, "Bearer tokens must not be surfaced.");
        AssertFalse(usage.Message?.Contains("visible-secret", StringComparison.Ordinal) == true, "Refresh tokens must not be surfaced.");

        return Task.CompletedTask;
    }

    public static async Task ClaudeProbeMapsClientFailureToUnavailable()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var probe = new ClaudeUsageProbe(new FailingClaudeUsageClient("Claude credentials not found"), () => now);

        var usage = await probe.ReadAsync(CancellationToken.None);

        AssertEqual(ProviderStatus.Unavailable, usage.Status);
        AssertTrue(usage.Message?.Contains("Claude credentials not found", StringComparison.OrdinalIgnoreCase) == true, "Failure message should be preserved.");
    }

    public static async Task ClaudeOAuthClientReportsMissingCredentialsWithoutNetwork()
    {
        var credentialsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"missing-claude-{Guid.NewGuid():N}.json");
        var client = new ClaudeOAuthUsageClient(
            TimeSpan.FromSeconds(1),
            credentialsPath,
            new HttpClient(new ThrowingHttpMessageHandler()));

        try
        {
            await client.ReadUsageAsync(CancellationToken.None);
            throw new InvalidOperationException("Expected missing Claude credentials to fail before HTTP.");
        }
        catch (ClaudeUsageException ex)
        {
            AssertTrue(ex.Message.Contains("credentials not found", StringComparison.OrdinalIgnoreCase), "Missing credentials should be actionable.");
        }
    }

    public static async Task ClaudeOAuthClientRejectsExpiredCredentialsWithoutRefreshToken()
    {
        var credentialsPath = CreateClaudeCredentialsFile(
            DateTimeOffset.Now.AddMinutes(-5),
            refreshToken: null);

        try
        {
            var client = new ClaudeOAuthUsageClient(
                TimeSpan.FromSeconds(1),
                credentialsPath,
                new HttpClient(new ThrowingHttpMessageHandler()));

            try
            {
                await client.ReadUsageAsync(CancellationToken.None);
                throw new InvalidOperationException("Expected expired Claude credentials to fail before HTTP.");
            }
            catch (ClaudeUsageException ex)
            {
                AssertTrue(ex.Message.Contains("expired", StringComparison.OrdinalIgnoreCase), "Expired credentials should be actionable.");
            }
        }
        finally
        {
            File.Delete(credentialsPath);
        }
    }

    public static async Task ClaudeOAuthClientRefreshesExpiredTokenBeforeUsage()
    {
        var credentialsPath = CreateClaudeCredentialsFile(DateTimeOffset.Now.AddMinutes(-5));
        var handler = new ClaudeRefreshHttpMessageHandler(
            tokenResponse: """{"access_token":"new-access-token","refresh_token":"new-refresh-token","expires_in":3600}""",
            usageResponse: """{"five_hour":{"utilization":25},"seven_day":{"utilization":35}}""");
        var client = CreateTestClaudeClient(credentialsPath, handler);

        var payload = await client.ReadUsageAsync(CancellationToken.None);

        AssertEqual(2, handler.Requests.Count);
        AssertEqual("POST", handler.Requests[0].Method);
        AssertEqual("GET", handler.Requests[1].Method);
        AssertTrue(handler.Requests[0].Body?.Contains("\"grant_type\":\"refresh_token\"", StringComparison.Ordinal) == true, "Refresh request should use refresh_token grant.");
        AssertTrue(handler.Requests[0].Body?.Contains(ClaudeOAuthUsageClient.ClaudeCodeClientId, StringComparison.Ordinal) == true, "Refresh request should use Claude Code public client id.");
        AssertEqual("Bearer new-access-token", handler.Requests[1].Authorization);
        AssertTrue(payload.UsageJson.Contains("five_hour", StringComparison.Ordinal), "Usage request should still return the usage payload after refresh.");
    }

    public static async Task ClaudeOAuthClientRefreshesTokenWithinExpiryBuffer()
    {
        var credentialsPath = CreateClaudeCredentialsFile(DateTimeOffset.Now.AddMinutes(2));
        var handler = new ClaudeRefreshHttpMessageHandler(
            tokenResponse: """{"access_token":"buffer-access-token","expires_in":3600}""",
            usageResponse: """{"five_hour":{"utilization":11},"seven_day":{"utilization":22}}""");
        var client = CreateTestClaudeClient(credentialsPath, handler);

        _ = await client.ReadUsageAsync(CancellationToken.None);

        AssertEqual(2, handler.Requests.Count);
        AssertEqual("POST", handler.Requests[0].Method);
        AssertEqual("Bearer buffer-access-token", handler.Requests[1].Authorization);
    }

    public static async Task ClaudeOAuthClientSkipsRefreshForFreshToken()
    {
        var credentialsPath = CreateClaudeCredentialsFile(DateTimeOffset.Now.AddHours(1), accessToken: "fresh-access-token");
        var handler = new ClaudeRefreshHttpMessageHandler(
            tokenResponse: """{"access_token":"should-not-be-used","expires_in":3600}""",
            usageResponse: """{"five_hour":{"utilization":11},"seven_day":{"utilization":22}}""");
        var client = CreateTestClaudeClient(credentialsPath, handler);

        _ = await client.ReadUsageAsync(CancellationToken.None);

        AssertEqual(1, handler.Requests.Count);
        AssertEqual("GET", handler.Requests[0].Method);
        AssertEqual("Bearer fresh-access-token", handler.Requests[0].Authorization);
    }

    public static async Task ClaudeOAuthClientPersistsRotatedRefreshToken()
    {
        var credentialsPath = CreateClaudeCredentialsFile(DateTimeOffset.Now.AddMinutes(-5));
        var handler = new ClaudeRefreshHttpMessageHandler(
            tokenResponse: """{"access_token":"rotated-access-token","refresh_token":"rotated-refresh-token","expires_in":3600}""",
            usageResponse: """{"five_hour":{"utilization":11},"seven_day":{"utilization":22}}""");
        var client = CreateTestClaudeClient(credentialsPath, handler);

        _ = await client.ReadUsageAsync(CancellationToken.None);
        var saved = File.ReadAllText(credentialsPath);

        AssertTrue(saved.Contains("\"accessToken\": \"rotated-access-token\"", StringComparison.Ordinal), "Refreshed access token should be written to Claude Code credentials.");
        AssertTrue(saved.Contains("\"refreshToken\": \"rotated-refresh-token\"", StringComparison.Ordinal), "Rotated refresh token should be written to Claude Code credentials.");
        AssertFalse(saved.Contains("test-access-token", StringComparison.Ordinal), "Old access token should be replaced.");
    }

    public static async Task ClaudeOAuthClientPreservesRefreshTokenWhenRotationOmitted()
    {
        var credentialsPath = CreateClaudeCredentialsFile(DateTimeOffset.Now.AddMinutes(-5), refreshToken: "stable-refresh-token");
        var handler = new ClaudeRefreshHttpMessageHandler(
            tokenResponse: """{"access_token":"new-access-no-rotation","expires_in":3600}""",
            usageResponse: """{"five_hour":{"utilization":11},"seven_day":{"utilization":22}}""");
        var client = CreateTestClaudeClient(credentialsPath, handler);

        _ = await client.ReadUsageAsync(CancellationToken.None);
        var saved = File.ReadAllText(credentialsPath);

        AssertTrue(saved.Contains("\"accessToken\": \"new-access-no-rotation\"", StringComparison.Ordinal), "Access token should be updated.");
        AssertTrue(saved.Contains("\"refreshToken\": \"stable-refresh-token\"", StringComparison.Ordinal), "Existing refresh token should be preserved when the endpoint omits rotation.");
    }

    public static async Task ClaudeOAuthClientAcceptsFloatingExpiresIn()
    {
        var credentialsPath = CreateClaudeCredentialsFile(DateTimeOffset.Now.AddMinutes(-5));
        var before = DateTimeOffset.Now;
        var handler = new ClaudeRefreshHttpMessageHandler(
            tokenResponse: """{"access_token":"float-expiry-access","expires_in":3600.0}""",
            usageResponse: """{"five_hour":{"utilization":11},"seven_day":{"utilization":22}}""");
        var client = CreateTestClaudeClient(credentialsPath, handler);

        _ = await client.ReadUsageAsync(CancellationToken.None);
        var expiresAt = ReadClaudeCredentialExpiresAt(credentialsPath);

        AssertTrue(expiresAt >= before.AddMinutes(55), "Floating expires_in should be accepted and converted to a future expiresAt.");
    }

    public static async Task ClaudeCredentialWritePreservesMetadataAndUnknownFields()
    {
        var credentialsPath = CreateClaudeCredentialsFile(
            DateTimeOffset.Now.AddMinutes(-5),
            includeUnknownTopLevel: true,
            includeScopes: true);
        var handler = new ClaudeRefreshHttpMessageHandler(
            tokenResponse: """{"access_token":"metadata-access-token","expires_in":3600}""",
            usageResponse: """{"five_hour":{"utilization":11},"seven_day":{"utilization":22}}""");
        var client = CreateTestClaudeClient(credentialsPath, handler);

        _ = await client.ReadUsageAsync(CancellationToken.None);
        var saved = File.ReadAllText(credentialsPath);

        AssertTrue(saved.Contains("\"someOtherField\": \"keep me\"", StringComparison.Ordinal), "Unknown top-level fields should survive credential write-back.");
        AssertTrue(saved.Contains("\"subscriptionType\": \"pro\"", StringComparison.Ordinal), "subscriptionType should be preserved.");
        AssertTrue(saved.Contains("\"rateLimitTier\": \"standard\"", StringComparison.Ordinal), "rateLimitTier should be preserved.");
        AssertTrue(saved.Contains("\"scopes\"", StringComparison.Ordinal), "scopes should be preserved.");
        AssertTrue(saved.Contains("user:inference", StringComparison.Ordinal), "Existing scopes should survive credential write-back.");
    }

    public static async Task ClaudeOAuthClientSkipsRefreshAfterLockRace()
    {
        var credentialsPath = CreateClaudeCredentialsFile(DateTimeOffset.Now.AddMinutes(-5), accessToken: "old-race-token");
        var lockPath = credentialsPath + ".refresh.lock";
        await using var locked = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var handler = new ClaudeRefreshHttpMessageHandler(
            tokenResponse: """{"access_token":"should-not-refresh","expires_in":3600}""",
            usageResponse: """{"five_hour":{"utilization":11},"seven_day":{"utilization":22}}""");
        var client = new ClaudeOAuthUsageClient(
            TimeSpan.FromSeconds(1),
            credentialsPath,
            new HttpClient(handler),
            usageEndpoint: "https://example.test/api/oauth/usage",
            tokenEndpoint: "https://example.test/v1/oauth/token",
            lockTimeout: TimeSpan.FromSeconds(2));

        var readTask = Task.Run(() => client.ReadUsageAsync(CancellationToken.None));
        await Task.Delay(100);
        RewriteClaudeCredentialAccessToken(credentialsPath, "race-fresh-token", DateTimeOffset.Now.AddHours(1));
        await locked.DisposeAsync();
        _ = await readTask;

        AssertEqual(1, handler.Requests.Count);
        AssertEqual("GET", handler.Requests[0].Method);
        AssertEqual("Bearer race-fresh-token", handler.Requests[0].Authorization);
    }

    public static async Task ClaudeOAuthClientMapsLockTimeoutToSanitizedFailure()
    {
        var credentialsPath = CreateClaudeCredentialsFile(DateTimeOffset.Now.AddMinutes(-5));
        var lockPath = credentialsPath + ".refresh.lock";
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(lockPath)!);

        await using var locked = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var client = new ClaudeOAuthUsageClient(
            TimeSpan.FromSeconds(1),
            credentialsPath,
            new HttpClient(new ThrowingHttpMessageHandler()),
            usageEndpoint: "https://example.test/api/oauth/usage",
            tokenEndpoint: "https://example.test/v1/oauth/token",
            lockTimeout: TimeSpan.FromMilliseconds(50));

        try
        {
            await client.ReadUsageAsync(CancellationToken.None);
            throw new InvalidOperationException("Expected lock timeout to fail before HTTP.");
        }
        catch (ClaudeUsageException ex)
        {
            AssertTrue(ex.Message.Contains("lock", StringComparison.OrdinalIgnoreCase), "Lock timeout should be actionable.");
            AssertFalse(ex.Message.Contains("test-refresh-token", StringComparison.Ordinal), "Lock timeout message must not leak refresh tokens.");
        }
    }

    public static async Task ClaudeOAuthClientDoesNotExposeRefreshTokens()
    {
        var credentialsPath = CreateClaudeCredentialsFile(DateTimeOffset.Now.AddMinutes(-5), refreshToken: "refresh-token-should-stay-secret");
        var handler = new ClaudeRefreshHttpMessageHandler(
            tokenStatusCode: 400,
            tokenResponse: """{"error":"invalid_grant","error_description":"Refresh token expired accessToken=leaky-access refreshToken=leaky-refresh"}""",
            usageResponse: """{"five_hour":{"utilization":11},"seven_day":{"utilization":22}}""");
        var client = CreateTestClaudeClient(credentialsPath, handler);

        try
        {
            await client.ReadUsageAsync(CancellationToken.None);
            throw new InvalidOperationException("Expected invalid refresh token to fail.");
        }
        catch (ClaudeUsageException ex)
        {
            AssertTrue(ex.Message.Contains("Refresh token expired", StringComparison.OrdinalIgnoreCase), "Safe refresh failure reason should remain visible.");
            AssertTrue(ex.Message.Contains("[redacted]", StringComparison.OrdinalIgnoreCase), "Sensitive token fields should be redacted.");
            AssertFalse(ex.Message.Contains("refresh-token-should-stay-secret", StringComparison.Ordinal), "Stored refresh token must not be exposed.");
            AssertFalse(ex.Message.Contains("leaky-access", StringComparison.Ordinal), "Parsed refresh error must redact access tokens.");
            AssertFalse(ex.Message.Contains("leaky-refresh", StringComparison.Ordinal), "Parsed refresh error must redact refresh tokens.");
        }
    }

    public static async Task ClaudeOAuthClientDoesNotExposeHttpResponseBodies()
    {
        var credentialsPath = CreateClaudeCredentialsFile(DateTimeOffset.Now.AddHours(1));
        var body = """{"error":{"message":"secret-token-or-payload"}}""";

        try
        {
            var client = new ClaudeOAuthUsageClient(
                TimeSpan.FromSeconds(1),
                credentialsPath,
                new HttpClient(new StaticHttpMessageHandler(401, body)));

            try
            {
                await client.ReadUsageAsync(CancellationToken.None);
                throw new InvalidOperationException("Expected non-success Claude response to fail.");
            }
            catch (ClaudeUsageException ex)
            {
                AssertFalse(ex.Message.Contains("secret-token-or-payload", StringComparison.Ordinal), "HTTP response bodies must not be exposed in errors.");
                AssertTrue(ex.Message.Contains("401", StringComparison.Ordinal), "HTTP status should remain visible.");
            }
        }
        finally
        {
            File.Delete(credentialsPath);
        }
    }

    public static async Task ClaudeOAuthClientCapturesOnlyUnifiedRateLimitHeaders()
    {
        var credentialsPath = CreateClaudeCredentialsFile(DateTimeOffset.Now.AddHours(1));

        try
        {
            var client = new ClaudeOAuthUsageClient(
                TimeSpan.FromSeconds(1),
                credentialsPath,
                new HttpClient(new StaticHttpMessageHandler(
                    200,
                    "{}",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["anthropic-ratelimit-unified-5h-utilization"] = "0.42",
                        ["anthropic-ratelimit-unified-5h-reset"] = "1779991200",
                        ["authorization"] = "Bearer should-not-be-captured",
                    })));

            var payload = await client.ReadUsageAsync(CancellationToken.None);

            AssertTrue(payload.RateLimitHeaders?.ContainsKey("anthropic-ratelimit-unified-5h-utilization") == true, "Claude client should retain unified rate-limit utilization headers.");
            AssertFalse(payload.RateLimitHeaders?.ContainsKey("authorization") == true, "Claude client must not retain auth headers.");
        }
        finally
        {
            File.Delete(credentialsPath);
        }
    }

    public static Task FileUsageSnapshotCacheExcludesProviderMessages()
    {
        var now = new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.FromHours(-3));
        var cachePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ai-limits-cache-{Guid.NewGuid():N}.json");
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Claude,
                    "Anthropic",
                    "Claude Pro",
                    ProviderStatus.Ok,
                    [new UsageWindow("5h", "5h", 12, now.AddHours(1), TimeSpan.FromHours(5), now.AddHours(-4))],
                    now,
                    "accessToken=cache-leak-access refreshToken=cache-leak-refresh Authorization: Bearer cache-leak-bearer"),
            ],
            now);

        try
        {
            new FileUsageSnapshotCache(cachePath).Write(snapshot);
            var cache = File.ReadAllText(cachePath);

            AssertFalse(cache.Contains("cache-leak-access", StringComparison.Ordinal), "Snapshot cache must not persist provider messages containing access tokens.");
            AssertFalse(cache.Contains("cache-leak-refresh", StringComparison.Ordinal), "Snapshot cache must not persist provider messages containing refresh tokens.");
            AssertFalse(cache.Contains("cache-leak-bearer", StringComparison.Ordinal), "Snapshot cache must not persist bearer tokens.");
        }
        finally
        {
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }

        return Task.CompletedTask;
    }

    public static Task SanitizesLiveSmokeMessages()
    {
        var message = "request failed Authorization: Bearer live-secret accessToken=visible-secret refreshToken: other-secret api key=plain-secret";
        var sanitized = UsageMessageSanitizer.Sanitize(message);

        AssertTrue(sanitized.Contains("[redacted]", StringComparison.OrdinalIgnoreCase), "Sanitized live messages should retain an explicit redaction marker.");
        AssertFalse(sanitized.Contains("live-secret", StringComparison.Ordinal), "Live smoke messages must not surface bearer tokens.");
        AssertFalse(sanitized.Contains("visible-secret", StringComparison.Ordinal), "Live smoke messages must not surface access tokens.");
        AssertFalse(sanitized.Contains("other-secret", StringComparison.Ordinal), "Live smoke messages must not surface refresh tokens.");
        AssertFalse(sanitized.Contains("plain-secret", StringComparison.Ordinal), "Live smoke messages must not surface API keys.");

        return Task.CompletedTask;
    }

    public static Task SanitizesApiKeyPrefixTokens()
    {
        var anthropicKey = string.Concat("sk-ant-api03-", "abc123DEF456ghi");
        var openAiKey = string.Concat("sk-proj-", "xyz789ABCDEF");
        var sanitized = UsageMessageSanitizer.Sanitize($"boom {anthropicKey} and {openAiKey} leaked");

        AssertTrue(sanitized.Contains("[redacted]", StringComparison.OrdinalIgnoreCase), "Prefixed API keys should be redacted.");
        AssertFalse(sanitized.Contains(anthropicKey, StringComparison.Ordinal), "Anthropic sk-ant- keys must not be surfaced.");
        AssertFalse(sanitized.Contains(openAiKey, StringComparison.Ordinal), "OpenAI sk- keys must not be surfaced.");

        return Task.CompletedTask;
    }

    public static Task DisposesProbesAndCodexProcessOnServiceDispose()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var codexClient = new DisposableFakeCodexAppServerClient();
        var codexProbe = new CodexUsageProbe(codexClient, () => now);
        var service = new UsageRefreshService([codexProbe], TimeSpan.FromMinutes(15), () => now);

        service.Dispose();

        AssertTrue(codexClient.Disposed, "Disposing the refresh service must tear down the Codex app-server client (no orphan process).");

        // Disposing a client that never started a process must not throw.
        using var unstarted = new CodexAppServerClient("codex", TimeSpan.FromSeconds(1));

        return Task.CompletedTask;
    }

    public static async Task KeepsOtherProviderWhenOneProbeFails()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        using var service = new UsageRefreshService(
            [
                new ThrowingProbe(UsageProvider.Codex),
                new DelayedProbe(now),
            ],
            TimeSpan.FromMinutes(1),
            () => now);

        var snapshot = await service.RefreshAsync();

        AssertEqual(2, snapshot.Providers.Count);
        AssertEqual(ProviderStatus.Error, snapshot.GetProvider(UsageProvider.Codex)?.Status);
        AssertEqual(ProviderStatus.Ok, snapshot.GetProvider(UsageProvider.Claude)?.Status);
    }

    public static async Task SanitizesDirectProviderResultMessages()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var unavailable = new ProviderUsage(
            UsageProvider.Claude,
            "Anthropic",
            null,
            ProviderStatus.Unavailable,
            [],
            now,
            "auth failed Authorization: Bearer direct-secret accessToken=visible-secret api key=plain-secret");
        using var service = new UsageRefreshService(
            [new SequenceProbe(UsageProvider.Claude, [ProbeStep.Success(unavailable)])],
            TimeSpan.FromMinutes(1),
            () => now);

        var snapshot = await service.RefreshAsync();
        var claude = snapshot.GetProvider(UsageProvider.Claude);

        AssertEqual(ProviderStatus.Unavailable, claude?.Status);
        AssertTrue(claude?.Message?.Contains("[redacted]", StringComparison.OrdinalIgnoreCase) == true, "Sensitive values should be redacted.");
        AssertFalse(claude?.Message?.Contains("direct-secret", StringComparison.Ordinal) == true, "Bearer tokens must not be surfaced.");
        AssertFalse(claude?.Message?.Contains("visible-secret", StringComparison.Ordinal) == true, "Access tokens must not be surfaced.");
        AssertFalse(claude?.Message?.Contains("plain-secret", StringComparison.Ordinal) == true, "API keys must not be surfaced.");
    }

    public static async Task ReusesPriorProviderDataAsStaleAfterTransientFailure()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var priorCodex = new ProviderUsage(
            UsageProvider.Codex,
            "OpenAI",
            "Plus",
            ProviderStatus.Ok,
            [
                new UsageWindow("5h", "5h", 22, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                new UsageWindow("7d", "7d", 44, now.AddDays(3), TimeSpan.FromDays(7), now.AddDays(-4)),
            ],
            now,
            null);
        using var service = new UsageRefreshService(
            [
                new SequenceProbe(
                    UsageProvider.Codex,
                    [ProbeStep.Success(priorCodex), ProbeStep.Failure(new TimeoutException("network timeout"))]),
                new DelayedProbe(now),
            ],
            TimeSpan.FromMinutes(1),
            () => now);

        _ = await service.RefreshAsync();
        var staleSnapshot = await service.RefreshAsync();
        var codex = staleSnapshot.GetProvider(UsageProvider.Codex);

        AssertEqual(ProviderStatus.Stale, codex?.Status);
        AssertEqual(2, codex?.Windows.Count);
        AssertTrue(codex?.Windows.Any(window => window.Id == "5h" && window.UsedPercent == 22) == true, "Stale provider should preserve prior 5h value.");
        AssertTrue(codex?.Message?.Contains("stale", StringComparison.OrdinalIgnoreCase) == true, "Stale provider message should name stale data.");
        AssertTrue(codex?.Message?.Contains("network timeout", StringComparison.OrdinalIgnoreCase) == true, "Stale provider message should keep a short failure reason.");
        AssertEqual(ProviderStatus.Ok, staleSnapshot.GetProvider(UsageProvider.Claude)?.Status);
    }

    public static async Task ReusesCachedClaudeDataAfterRestartWhenUsageEndpointIsRateLimited()
    {
        var now = new DateTimeOffset(2026, 5, 25, 10, 40, 0, TimeSpan.FromHours(-3));
        var cachePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ai-limits-cache-{Guid.NewGuid():N}.json");
        try
        {
            var priorClaude = new ProviderUsage(
                UsageProvider.Claude,
                "Anthropic",
                "Claude Pro",
                ProviderStatus.Ok,
                [
                    new UsageWindow("5h", "5h", 0, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                    new UsageWindow("7d", "7d", 15, now.AddDays(5), TimeSpan.FromDays(7), now.AddDays(-2)),
                ],
                now,
                null);

            using (var firstService = new UsageRefreshService(
                       [new SequenceProbe(UsageProvider.Claude, [ProbeStep.Success(priorClaude)])],
                       TimeSpan.FromMinutes(5),
                       () => now,
                       new FileUsageSnapshotCache(cachePath)))
            {
                _ = await firstService.RefreshAsync();
            }

            var rateLimited = new ProviderUsage(
                UsageProvider.Claude,
                "Anthropic",
                null,
                ProviderStatus.Unavailable,
                [],
                now.AddMinutes(1),
                "Claude usage endpoint returned HTTP 429.");
            using var restartedService = new UsageRefreshService(
                [new SequenceProbe(UsageProvider.Claude, [ProbeStep.Success(rateLimited)])],
                TimeSpan.FromMinutes(5),
                () => now.AddMinutes(1),
                new FileUsageSnapshotCache(cachePath));

            var snapshot = await restartedService.RefreshAsync();
            var claude = snapshot.GetProvider(UsageProvider.Claude);

            AssertEqual(ProviderStatus.Stale, claude?.Status);
            AssertTrue(claude?.Windows.Any(window => window.Id == "5h" && window.UsedPercent == 0) == true, "Cached Claude 5h must remain visible after HTTP 429.");
            AssertTrue(claude?.Windows.Any(window => window.Id == "7d" && window.UsedPercent == 15) == true, "Cached Claude 7d must remain visible after HTTP 429.");
            AssertTrue(claude?.Message?.Contains("429", StringComparison.OrdinalIgnoreCase) == true, "Stale message should keep the rate-limit reason.");
        }
        finally
        {
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }
    }

    public static async Task SanitizesStaleProviderFailureMessages()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var priorCodex = new ProviderUsage(
            UsageProvider.Codex,
            "OpenAI",
            "Plus",
            ProviderStatus.Ok,
            [new UsageWindow("5h", "5h", 22, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3))],
            now,
            null);
        using var service = new UsageRefreshService(
            [
                new SequenceProbe(
                    UsageProvider.Codex,
                    [
                        ProbeStep.Success(priorCodex),
                        ProbeStep.Failure(new TimeoutException("network timeout Authorization: Bearer secret-token accessToken=visible-secret refreshToken: other-secret")),
                    ]),
            ],
            TimeSpan.FromMinutes(1),
            () => now);

        _ = await service.RefreshAsync();
        var snapshot = await service.RefreshAsync();
        var codex = snapshot.GetProvider(UsageProvider.Codex);

        AssertEqual(ProviderStatus.Stale, codex?.Status);
        AssertTrue(codex?.Message?.Contains("[redacted]", StringComparison.OrdinalIgnoreCase) == true, "Sensitive values should be redacted.");
        AssertFalse(codex?.Message?.Contains("secret-token", StringComparison.Ordinal) == true, "Bearer tokens must not be surfaced.");
        AssertFalse(codex?.Message?.Contains("visible-secret", StringComparison.Ordinal) == true, "Access tokens must not be surfaced.");
        AssertFalse(codex?.Message?.Contains("other-secret", StringComparison.Ordinal) == true, "Refresh tokens must not be surfaced.");
    }

    public static async Task DoesNotReuseStaleCacheForHardUnavailableFailures()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var priorClaude = new ProviderUsage(
            UsageProvider.Claude,
            "Anthropic",
            "Claude Pro",
            ProviderStatus.Ok,
            [new UsageWindow("5h", "5h", 22, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3))],
            now,
            null);
        var hardFailure = new ProviderUsage(
            UsageProvider.Claude,
            "Anthropic",
            null,
            ProviderStatus.Unavailable,
            [],
            now,
            "Claude auth expired; run `claude auth login`.");
        using var service = new UsageRefreshService(
            [new SequenceProbe(UsageProvider.Claude, [ProbeStep.Success(priorClaude), ProbeStep.Success(hardFailure)])],
            TimeSpan.FromMinutes(1),
            () => now);

        _ = await service.RefreshAsync();
        var snapshot = await service.RefreshAsync();
        var claude = snapshot.GetProvider(UsageProvider.Claude);

        AssertEqual(ProviderStatus.Unavailable, claude?.Status);
        AssertEqual(0, claude?.Windows.Count);
        AssertTrue(claude?.Message?.Contains("auth expired", StringComparison.OrdinalIgnoreCase) == true, "Hard auth failures should not be hidden by stale cache.");
    }

    public static Task CalculatesOpenAiApiEquivalentCost()
    {
        var price = new ApiCostCatalog().Find(UsageProvider.Codex, "gpt-5.5");
        AssertNotNull(price, "GPT-5.5 should be present in the pinned price catalog.");

        var cost = price!.CalculateUsd(new TokenUsageTotals(
            InputTokens: 1_000_000,
            CachedInputTokens: 2_000_000,
            OutputTokens: 100_000));

        AssertEqual(9.00m, decimal.Round(cost, 2));
        return Task.CompletedTask;
    }

    public static Task CalculatesAnthropicApiEquivalentCost()
    {
        var price = new ApiCostCatalog().Find(UsageProvider.Claude, "claude-opus-4-7-20260501");
        AssertNotNull(price, "Claude Opus 4.7 should be present in the pinned price catalog.");

        var cost = price!.CalculateUsd(new TokenUsageTotals(
            InputTokens: 1_000_000,
            CachedInputTokens: 2_000_000,
            CacheWriteFiveMinuteTokens: 1_000_000,
            CacheWriteOneHourTokens: 1_000_000,
            OutputTokens: 100_000));

        AssertEqual(24.75m, decimal.Round(cost, 2));
        return Task.CompletedTask;
    }

    public static Task CodexTokenReaderFiltersWeeklyWindowAndIgnoresCumulativeTotals()
    {
        var root = CreateTempDirectory();
        var start = new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.FromHours(-3));
        var end = start.AddDays(7);

        try
        {
            File.WriteAllLines(System.IO.Path.Combine(root, "session.jsonl"),
            [
                "{\"timestamp\":\"2026-05-20T00:05:00-03:00\",\"payload\":{\"model\":\"gpt-5.5\"}}",
                "{\"timestamp\":\"2026-05-21T10:00:00-03:00\",\"payload\":{\"info\":{\"last_token_usage\":{\"input_tokens\":1000000,\"cached_input_tokens\":2000000,\"output_tokens\":100000},\"total_token_usage\":{\"input_tokens\":999000000,\"cached_input_tokens\":999000000,\"output_tokens\":999000000}}}}",
                "{\"timestamp\":\"2026-05-30T10:00:00-03:00\",\"payload\":{\"info\":{\"last_token_usage\":{\"input_tokens\":7000000,\"cached_input_tokens\":7000000,\"output_tokens\":7000000}}}}",
            ]);

            var usage = new CodexTokenUsageReader(root).ReadUsage(start, end);
            AssertEqual(1, usage.Count);
            AssertTrue(usage.TryGetValue("gpt-5.5", out var totals), "Codex token usage should be attributed to the nearest model in the session file.");
            AssertEqual(1_000_000L, totals!.InputTokens);
            AssertEqual(2_000_000L, totals.CachedInputTokens);
            AssertEqual(100_000L, totals.OutputTokens);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        return Task.CompletedTask;
    }

    public static Task ClaudeTokenReaderSumsMessageUsageByModel()
    {
        var root = CreateTempDirectory();
        var start = new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.FromHours(-3));
        var end = start.AddDays(7);

        try
        {
            File.WriteAllLines(System.IO.Path.Combine(root, "conversation.jsonl"),
            [
                "{\"timestamp\":\"2026-05-21T10:00:00-03:00\",\"message\":{\"model\":\"claude-opus-4-7-20260501\",\"usage\":{\"input_tokens\":1000000,\"cache_read_input_tokens\":2000000,\"ephemeral_5m_input_tokens\":3000000,\"ephemeral_1h_input_tokens\":4000000,\"cache_creation_input_tokens\":9000000,\"output_tokens\":500000}}}",
                "{\"timestamp\":\"2026-05-21T10:05:00-03:00\",\"message\":{\"model\":\"claude-opus-4-7-20260501\",\"usage\":{\"input_tokens\":10,\"cache_creation_input_tokens\":20,\"output_tokens\":30}}}",
                "{\"timestamp\":\"2026-05-30T10:00:00-03:00\",\"message\":{\"model\":\"claude-opus-4-7-20260501\",\"usage\":{\"input_tokens\":7000000,\"output_tokens\":7000000}}}",
            ]);

            var usage = new ClaudeTokenUsageReader(root).ReadUsage(start, end);
            AssertEqual(1, usage.Count);
            AssertTrue(usage.TryGetValue("claude-opus-4-7-20260501", out var totals), "Claude token usage should be grouped by message model.");
            AssertEqual(1_000_010L, totals!.InputTokens);
            AssertEqual(2_000_000L, totals.CachedInputTokens);
            AssertEqual(3_000_020L, totals.CacheWriteFiveMinuteTokens);
            AssertEqual(4_000_000L, totals.CacheWriteOneHourTokens);
            AssertEqual(500_030L, totals.OutputTokens);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        return Task.CompletedTask;
    }

    public static Task LocalTokenReadersIgnoreNullJsonObjects()
    {
        var codexRoot = CreateTempDirectory();
        var claudeRoot = CreateTempDirectory();
        var start = new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.FromHours(-3));
        var end = start.AddDays(7);

        try
        {
            File.WriteAllLines(System.IO.Path.Combine(codexRoot, "session.jsonl"),
            [
                "null",
                "{\"timestamp\":\"2026-05-21T10:00:00-03:00\",\"payload\":null}",
                "{\"timestamp\":\"2026-05-21T10:01:00-03:00\",\"payload\":{\"info\":null}}",
            ]);
            File.WriteAllLines(System.IO.Path.Combine(claudeRoot, "conversation.jsonl"),
            [
                "null",
                "{\"timestamp\":\"2026-05-21T10:00:00-03:00\",\"message\":null}",
                "{\"timestamp\":\"2026-05-21T10:01:00-03:00\",\"message\":{\"usage\":null}}",
            ]);

            AssertEqual(0, new CodexTokenUsageReader(codexRoot).ReadUsage(start, end).Count);
            AssertEqual(0, new ClaudeTokenUsageReader(claudeRoot).ReadUsage(start, end).Count);
        }
        finally
        {
            Directory.Delete(codexRoot, recursive: true);
            Directory.Delete(claudeRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    public static async Task ApiCostEstimatorFailureDoesNotBreakProvider()
    {
        var now = new DateTimeOffset(2026, 5, 26, 10, 24, 0, TimeSpan.FromHours(-3));
        var provider = new ProviderUsage(
            UsageProvider.Codex,
            "OpenAI",
            "plus",
            ProviderStatus.Ok,
            [new UsageWindow("7d", "7d", 40, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3))],
            now,
            null);
        using var service = new UsageRefreshService(
            [new StaticProbe(UsageProvider.Codex, provider)],
            TimeSpan.FromMinutes(15),
            () => now,
            apiCostEstimator: new ThrowingApiCostEstimator());

        var snapshot = await service.RefreshAsync();
        var codex = snapshot.GetProvider(UsageProvider.Codex);

        AssertEqual(ProviderStatus.Ok, codex?.Status);
        AssertEqual("plus", codex?.PlanLabel);
        AssertEqual(1, codex?.Windows.Count);
        AssertEqual<ApiCostEstimate?>(null, codex?.ApiCostEstimate);
    }

    public static async Task ManualCostRefreshDoesNotCallUsageProbes()
    {
        var now = new DateTimeOffset(2026, 5, 26, 10, 57, 0, TimeSpan.FromHours(-3));
        var codex = new ProviderUsage(
            UsageProvider.Codex,
            "OpenAI",
            "plus",
            ProviderStatus.Ok,
            [new UsageWindow("7d", "7d", 42, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3))],
            now,
            null);
        var snapshot = new UsageSnapshot([codex], now);
        var probe = new SequenceProbe(UsageProvider.Codex, [ProbeStep.Success(codex)]);
        var cache = new MemoryUsageSnapshotCache(snapshot);
        using var service = new UsageRefreshService(
            [probe],
            TimeSpan.FromMinutes(15),
            () => now,
            snapshotCache: cache,
            apiCostEstimator: new FixedApiCostEstimator(new ApiCostEstimate(
                "7d",
                "7d",
                now.AddDays(-3),
                now.AddDays(4),
                [new ModelCostLine("gpt-5.5", "GPT-5.5", new TokenUsageTotals(InputTokens: 1_000_000), 5m, "in/cache/out")],
                new TokenUsageTotals(InputTokens: 1_000_000),
                5m,
                now)),
            costRefreshInterval: TimeSpan.FromHours(4));

        var refreshed = await service.RefreshCostsAsync();

        AssertEqual(0, probe.CallCount);
        AssertEqual(5m, refreshed.GetProvider(UsageProvider.Codex)?.ApiCostEstimate?.TotalCostUsd);
        AssertEqual(TimeSpan.FromHours(4), service.CostRefreshInterval);
    }

    public static async Task CostRefreshRecalculatesCodexAndClaudeTogether()
    {
        var now = new DateTimeOffset(2026, 5, 26, 11, 10, 0, TimeSpan.FromHours(-3));
        var codex = ProviderWithoutCost(UsageProvider.Codex, "OpenAI", now);
        var claude = ProviderWithoutCost(UsageProvider.Claude, "Anthropic", now);
        var snapshot = new UsageSnapshot([codex, claude], now);
        var codexProbe = new SequenceProbe(UsageProvider.Codex, [ProbeStep.Success(codex)]);
        var claudeProbe = new SequenceProbe(UsageProvider.Claude, [ProbeStep.Success(claude)]);
        using var service = new UsageRefreshService(
            [codexProbe, claudeProbe],
            TimeSpan.FromMinutes(15),
            () => now,
            snapshotCache: new MemoryUsageSnapshotCache(snapshot),
            apiCostEstimator: new ProviderSpecificApiCostEstimator(new Dictionary<UsageProvider, ApiCostEstimate?>
            {
                [UsageProvider.Codex] = CostEstimateFor("gpt-5.5", 1_000_000, 5m, now),
                [UsageProvider.Claude] = CostEstimateFor("claude-sonnet-4-6-20260201", 2_000_000, 6m, now),
            }));

        var refreshed = await service.RefreshCostsAsync();

        AssertEqual(0, codexProbe.CallCount);
        AssertEqual(0, claudeProbe.CallCount);
        AssertEqual(5m, refreshed.GetProvider(UsageProvider.Codex)?.ApiCostEstimate?.TotalCostUsd);
        AssertEqual(6m, refreshed.GetProvider(UsageProvider.Claude)?.ApiCostEstimate?.TotalCostUsd);
    }

    public static async Task CostRefreshPreservesProviderEstimateWhenRecalculationIsEmpty()
    {
        var now = new DateTimeOffset(2026, 5, 26, 11, 17, 0, TimeSpan.FromHours(-3));
        var claude = ProviderWithCost(UsageProvider.Claude, "Anthropic", 143_000_000, 128.41m, now);
        var snapshot = new UsageSnapshot([claude], now);
        using var service = new UsageRefreshService(
            [new StaticProbe(UsageProvider.Claude, claude)],
            TimeSpan.FromMinutes(15),
            () => now,
            snapshotCache: new MemoryUsageSnapshotCache(snapshot),
            apiCostEstimator: new FixedApiCostEstimator(null));

        var refreshed = await service.RefreshCostsAsync();

        AssertEqual(128.41m, refreshed.GetProvider(UsageProvider.Claude)?.ApiCostEstimate?.TotalCostUsd);
        AssertEqual(143_000_000, refreshed.GetProvider(UsageProvider.Claude)?.ApiCostEstimate?.TotalTokens.TotalTokens);
    }

    public static async Task QuotaRefreshPreservesProviderEstimateWhenWeeklyWindowDriftsByMilliseconds()
    {
        var now = new DateTimeOffset(2026, 5, 26, 14, 35, 0, TimeSpan.FromHours(-3));
        var previous = ProviderWithCost(UsageProvider.Claude, "Anthropic", 143_000_000, 128.41m, now);
        var previousWindow = previous.Windows.Single(window => window.Id == "7d");
        var shifted = previous with
        {
            Windows =
            [
                previousWindow with
                {
                    StartedAt = previousWindow.StartedAt!.Value.AddMilliseconds(239),
                    ResetsAt = previousWindow.ResetsAt!.Value.AddMilliseconds(239),
                },
            ],
            ApiCostEstimate = null,
        };

        using var service = new UsageRefreshService(
            [new StaticProbe(UsageProvider.Claude, shifted)],
            TimeSpan.FromMinutes(15),
            () => now,
            snapshotCache: new MemoryUsageSnapshotCache(new UsageSnapshot([previous], now)));

        var refreshed = await service.RefreshAsync();

        AssertEqual(128.41m, refreshed.GetProvider(UsageProvider.Claude)?.ApiCostEstimate?.TotalCostUsd);
    }

    public static Task ViewModelSummarizesConsolidatedApiCosts()
    {
        var now = new DateTimeOffset(2026, 5, 26, 10, 57, 0, TimeSpan.FromHours(-3));
        var snapshot = new UsageSnapshot(
            [
                ProviderWithCost(UsageProvider.Codex, "OpenAI", 1_035_000_000, 2_945.77m, now),
                ProviderWithCost(UsageProvider.Claude, "Anthropic", 143_000_000, 128.41m, now.AddMinutes(-5)),
            ],
            now);
        using var service = new StubUsageRefreshService(snapshot);
        using var viewModel = new AiLimitsViewModel(service, () => now);

        AssertFalse(viewModel.IsApiCostsExpanded, "The consolidated API cost panel should start collapsed for each popup/view model.");
        AssertEqual("$3074.18", viewModel.ApiCostTotalText);
        AssertEqual("1178M tokens", viewModel.ApiCostTotalTokensText);
        AssertEqual("custos atualizados 10:57", viewModel.ApiCostLastUpdatedText);
        AssertEqual(2, viewModel.ApiCostProviders.Count);

        viewModel.ToggleApiCostsCommand.Execute(null);

        AssertTrue(viewModel.IsApiCostsExpanded, "The consolidated API cost panel should expand on command.");

        return Task.CompletedTask;
    }

    private static ProviderUsage ProviderWithoutCost(UsageProvider provider, string displayName, DateTimeOffset calculatedAt)
        => new(
            provider,
            displayName,
            provider == UsageProvider.Codex ? "plus" : "Claude Pro",
            ProviderStatus.Ok,
            [new UsageWindow("7d", "7d", 42, calculatedAt.AddDays(4), TimeSpan.FromDays(7), calculatedAt.AddDays(-3))],
            calculatedAt,
            null);

    private static ProviderUsage ProviderWithCost(
        UsageProvider provider,
        string displayName,
        long totalTokens,
        decimal totalCostUsd,
        DateTimeOffset calculatedAt)
        => new(
            provider,
            displayName,
            provider == UsageProvider.Codex ? "plus" : "Claude Pro",
            ProviderStatus.Ok,
            [new UsageWindow("7d", "7d", 42, calculatedAt.AddDays(4), TimeSpan.FromDays(7), calculatedAt.AddDays(-3))],
            calculatedAt,
            null,
            new ApiCostEstimate(
                "7d",
                "7d",
                calculatedAt.AddDays(-3),
                calculatedAt.AddDays(4),
                [new ModelCostLine($"{displayName}-model", $"{displayName} model", new TokenUsageTotals(InputTokens: totalTokens), totalCostUsd, "in/cache/out")],
                new TokenUsageTotals(InputTokens: totalTokens),
                totalCostUsd,
                calculatedAt));

    private static ApiCostEstimate CostEstimateFor(string modelId, long totalTokens, decimal totalCostUsd, DateTimeOffset calculatedAt)
        => new(
            "7d",
            "7d",
            calculatedAt.AddDays(-3),
            calculatedAt.AddDays(4),
            [new ModelCostLine(modelId, modelId, new TokenUsageTotals(InputTokens: totalTokens), totalCostUsd, "in/cache/out")],
            new TokenUsageTotals(InputTokens: totalTokens),
            totalCostUsd,
            calculatedAt);

    public static Task UnknownPriceModelsStayVisibleAndUnpriced()
    {
        var now = new DateTimeOffset(2026, 5, 26, 9, 0, 0, TimeSpan.FromHours(-3));
        var provider = new ProviderUsage(
            UsageProvider.Codex,
            "OpenAI",
            "plus",
            ProviderStatus.Ok,
            [new UsageWindow("7d", "7d", 40, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3))],
            now,
            null);
        var estimator = new ApiCostEstimator(
            codexReader: new StaticTokenUsageReader(new Dictionary<string, TokenUsageTotals>
            {
                ["future-model"] = new(InputTokens: 1_000_000, OutputTokens: 1_000_000),
            }),
            claudeReader: new StaticTokenUsageReader(new Dictionary<string, TokenUsageTotals>()),
            clock: () => now);

        var estimate = estimator.Estimate(provider);

        AssertNotNull(estimate, "Unknown models should still produce a visible estimate line.");
        AssertEqual(1, estimate!.Lines.Count);
        AssertEqual<decimal?>(null, estimate.Lines[0].CostUsd);
        AssertEqual(0m, estimate.TotalCostUsd);

        return Task.CompletedTask;
    }

    public static Task FileUsageSnapshotCacheStoresOnlyCostAggregates()
    {
        var now = new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.FromHours(-3));
        var cachePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ai-limits-cache-{Guid.NewGuid():N}.json");
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Codex,
                    "OpenAI",
                    "plus",
                    ProviderStatus.Ok,
                    [new UsageWindow("7d", "7d", 40, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3))],
                    now,
                    "Authorization: Bearer cache-cost-secret",
                    new ApiCostEstimate(
                        "7d",
                        "7d",
                        now.AddDays(-3),
                        now.AddDays(4),
                        [new ModelCostLine("gpt-5.5", "GPT-5.5", new TokenUsageTotals(InputTokens: 1_000_000), 5.00m, "in $5 / cache $0.5 / out $30")],
                        new TokenUsageTotals(InputTokens: 1_000_000),
                        5.00m,
                        now)),
            ],
            now);

        try
        {
            new FileUsageSnapshotCache(cachePath).Write(snapshot);
            var cache = File.ReadAllText(cachePath);

            AssertTrue(cache.Contains("gpt-5.5", StringComparison.Ordinal), "Snapshot cache should keep aggregate model identifiers for cost display.");
            AssertTrue(cache.Contains("inputTokens", StringComparison.Ordinal), "Snapshot cache should keep aggregate token counts.");
            AssertFalse(cache.Contains("cache-cost-secret", StringComparison.Ordinal), "Snapshot cache must not persist auth headers from provider messages.");
            AssertFalse(cache.Contains("prompt", StringComparison.OrdinalIgnoreCase), "Snapshot cache must not persist prompt fields.");
            AssertFalse(cache.Contains("response", StringComparison.OrdinalIgnoreCase), "Snapshot cache must not persist response fields.");
        }
        finally
        {
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }

        return Task.CompletedTask;
    }

    public static Task DispatchesUsageUpdatesThroughUiDispatcher()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var initial = UsageSnapshot.Empty(now);
        var next = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Codex,
                    "OpenAI",
                    "Plus",
                    ProviderStatus.Ok,
                    [new UsageWindow("5h", "5h", 42, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3))],
                    now,
                    null),
            ],
            now);
        var pendingUiWork = new List<Action>();
        using var service = new ManualUsageRefreshService(initial);
        using var viewModel = new AiLimitsViewModel(service, () => now, action => pendingUiWork.Add(action));

        service.Publish(next);

        AssertEqual("--", viewModel.OpenAiFiveHourText);
        AssertEqual(1, pendingUiWork.Count);

        pendingUiWork[0]();

        AssertEqual("42%", viewModel.OpenAiFiveHourText);

        return Task.CompletedTask;
    }

    public static Task CalculatesPacingForWeeklyWindow()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var window = new UsageWindow("7d", "7d", 59, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3));

        var pacing = UsagePacingCalculator.Calculate(window, now);

        AssertEqual(59, pacing.UsedPercent);
        AssertNear(42.9, pacing.ExpectedPercent, 0.1);
        AssertNear(16.1, pacing.DifferencePercentagePoints, 0.1);
        AssertNear(19.7, pacing.AverageDailyPacePercent, 0.1);
        AssertNear(3.0, pacing.ElapsedDays, 0.1);

        return Task.CompletedTask;
    }

    public static Task ProjectsWeeklyExhaustionBeforeReset()
    {
        var now = new DateTimeOffset(2026, 5, 26, 9, 19, 0, TimeSpan.FromHours(-3));
        var reset = new DateTimeOffset(2026, 5, 30, 17, 59, 0, TimeSpan.FromHours(-3));
        var elapsedDays = 38d / 14.4d;
        var window = new UsageWindow("7d", "7d", 38, reset, TimeSpan.FromDays(7), now.AddDays(-elapsedDays));

        var pacing = UsagePacingCalculator.Calculate(window, now);

        AssertNear(14.4, pacing.AverageDailyPacePercent, 0.1);
        AssertEqual(ProjectedExhaustionStatus.BeforeReset, pacing.ProjectedExhaustionStatus);
        AssertEqual<DateTimeOffset?>(new DateTimeOffset(2026, 5, 30, 16, 39, 0, TimeSpan.FromHours(-3)), pacing.ProjectedExhaustionAt);
        AssertNear(80, (reset - pacing.ProjectedExhaustionAt!.Value).TotalMinutes, 0.1);

        return Task.CompletedTask;
    }

    public static Task MarksWeeklyExhaustionAfterReset()
    {
        var now = new DateTimeOffset(2026, 5, 26, 9, 19, 0, TimeSpan.FromHours(-3));
        var reset = new DateTimeOffset(2026, 5, 30, 21, 0, 0, TimeSpan.FromHours(-3));
        var elapsedDays = 23d / 9.2d;
        var window = new UsageWindow("7d", "7d", 23, reset, TimeSpan.FromDays(7), now.AddDays(-elapsedDays));

        var pacing = UsagePacingCalculator.Calculate(window, now);

        AssertNear(9.2, pacing.AverageDailyPacePercent, 0.1);
        AssertEqual(ProjectedExhaustionStatus.AfterReset, pacing.ProjectedExhaustionStatus);
        AssertTrue(pacing.ProjectedExhaustionAt > reset, "Projection should occur after the weekly reset.");

        return Task.CompletedTask;
    }

    public static Task HandlesUnavailableAndExhaustedWeeklyProjections()
    {
        var now = new DateTimeOffset(2026, 5, 26, 9, 19, 0, TimeSpan.FromHours(-3));
        var reset = now.AddDays(2);
        var idleWindow = new UsageWindow("7d", "7d", 0, reset, TimeSpan.FromDays(7), now.AddDays(-2));

        var idlePacing = UsagePacingCalculator.Calculate(idleWindow, now);

        AssertEqual(ProjectedExhaustionStatus.Unavailable, idlePacing.ProjectedExhaustionStatus);
        AssertEqual<DateTimeOffset?>(null, idlePacing.ProjectedExhaustionAt);

        var exhaustedWindow = new UsageWindow("7d", "7d", 100, reset, TimeSpan.FromDays(7), now.AddDays(-2));
        var exhaustedPacing = UsagePacingCalculator.Calculate(exhaustedWindow, now);

        AssertEqual(ProjectedExhaustionStatus.BeforeReset, exhaustedPacing.ProjectedExhaustionStatus);
        AssertEqual<DateTimeOffset?>(now, exhaustedPacing.ProjectedExhaustionAt);

        return Task.CompletedTask;
    }

    public static async Task PreventsOverlappingRefreshes()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var probe = new DelayedProbe(now);
        using var service = new UsageRefreshService([probe], TimeSpan.FromMinutes(1), () => now);

        await Task.WhenAll(service.RefreshAsync(), service.RefreshAsync());

        AssertEqual(1, probe.CallCount);
    }

    public static async Task RefreshCommandPreventsOverlappingExecutions()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        var canExecuteChangedCount = 0;
        var command = new AsyncCommand(async () =>
        {
            Interlocked.Increment(ref callCount);
            started.TrySetResult();
            await release.Task;
        });
        command.CanExecuteChanged += (_, _) => Interlocked.Increment(ref canExecuteChangedCount);

        AssertTrue(command.CanExecute(null), "Refresh command should start enabled.");

        command.Execute(null);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        AssertFalse(command.CanExecute(null), "Refresh command should be disabled while a refresh is running.");

        command.Execute(null);
        AssertEqual(1, Volatile.Read(ref callCount));

        release.SetResult();
        await WaitUntilAsync(() => command.CanExecute(null), TimeSpan.FromSeconds(2));

        AssertTrue(command.CanExecute(null), "Refresh command should re-enable after the refresh completes.");
        AssertTrue(Volatile.Read(ref canExecuteChangedCount) >= 2, "Refresh command should notify the UI when enabled state changes.");
    }

    public static Task ExportsAlwaysActiveSingleViewSill()
    {
        var export = typeof(AiLimitsSill).GetCustomAttributes(typeof(ExportAttribute), inherit: false)
            .Cast<ExportAttribute>()
            .SingleOrDefault(attribute => attribute.ContractType == typeof(ISill));

        AssertNotNull(export, "AiLimitsSill must export ISill.");
        AssertTrue(typeof(ISillActivatedByDefault).IsAssignableFrom(typeof(AiLimitsSill)), "AiLimitsSill must be activated by default.");
        AssertTrue(typeof(ISillSingleView).IsAssignableFrom(typeof(AiLimitsSill)), "AiLimitsSill must provide a single custom view.");
        AssertFalse(typeof(ISillActivatedByProcess).IsAssignableFrom(typeof(AiLimitsSill)), "AiLimitsSill must not use process activation.");

        try
        {
            using var sill = new AiLimitsSill(new TestSettingsProvider(), new TestPluginInfo());

            AssertEqual("AI Limits", sill.DisplayName);
            AssertNotNull(sill.View, "AiLimitsSill.View must be creatable with fake settings.");
            AssertNotNull(sill.View.Content, "AiLimitsSill.View.Content must contain the compact bar view.");
            AssertNotNull(sill.CreateIcon(), "CreateIcon must return an IconElement.");

            var settingsViews = sill.SettingsViews;
            AssertNotNull(settingsViews, "SettingsViews must be enumerable.");
            AssertEqual(1, settingsViews?.Length ?? 0);
        }
        catch (COMException ex) when ((uint)ex.HResult == 0x80040154)
        {
            Console.WriteLine("SKIP WinUI view creation requires the WindowSill/WinUI host on this machine (REGDB_E_CLASSNOTREG).");
        }

        return Task.CompletedTask;
    }

    public static async Task CodexProbeMapsMissingCommandToNotInstalled()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var probe = new CodexUsageProbe(new MissingCommandCodexAppServerClient(), () => now);

        var usage = await probe.ReadAsync(CancellationToken.None);

        AssertEqual(ProviderStatus.NotInstalled, usage.Status);
        AssertEqual(0, usage.Windows.Count);
    }

    public static async Task ClaudeProbeMapsMissingCredentialsToNotInstalled()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var probe = new ClaudeUsageProbe(new NotConfiguredClaudeUsageClient(), () => now);

        var usage = await probe.ReadAsync(CancellationToken.None);

        AssertEqual(ProviderStatus.NotInstalled, usage.Status);
        AssertEqual(0, usage.Windows.Count);
    }

    public static Task HidesNotInstalledProviderFromUi()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(
                    UsageProvider.Codex,
                    "OpenAI",
                    "Plus",
                    ProviderStatus.Ok,
                    [
                        new UsageWindow("5h", "5h", 24, now.AddHours(2), TimeSpan.FromHours(5), now.AddHours(-3)),
                        new UsageWindow("7d", "7d", 28, now.AddDays(4), TimeSpan.FromDays(7), now.AddDays(-3)),
                    ],
                    now,
                    null),
                new ProviderUsage(
                    UsageProvider.Claude,
                    "Anthropic",
                    null,
                    ProviderStatus.NotInstalled,
                    [],
                    now,
                    "Claude credentials not found; run `claude auth login`."),
            ],
            now);

        using var service = new StubUsageRefreshService(snapshot);
        using var viewModel = new AiLimitsViewModel(service, () => now);

        AssertEqual(1, viewModel.VisibleProviders.Count);
        AssertEqual(UsageProvider.Codex, viewModel.VisibleProviders[0].Provider);

        var wide = viewModel.GetCollapsedSummary(CollapsedSummaryLayout.Wide);
        AssertEqual("OpenAI 5h 24% 7d 28%", wide);
        AssertFalse(wide.Contains("Anthropic", StringComparison.Ordinal), "A not-installed provider must not appear in the collapsed summary.");

        var critical = viewModel.GetCollapsedSummary(CollapsedSummaryLayout.CriticalOnly);
        AssertFalse(critical.Contains("◇", StringComparison.Ordinal), "A not-installed provider must not appear in the critical-only summary.");

        return Task.CompletedTask;
    }

    public static Task ShowsNeutralMessageWhenNoProviderInstalled()
    {
        var now = new DateTimeOffset(2026, 5, 24, 18, 0, 0, TimeSpan.FromHours(-3));
        var snapshot = new UsageSnapshot(
            [
                new ProviderUsage(UsageProvider.Codex, "OpenAI", null, ProviderStatus.NotInstalled, [], now, "Codex command not found or could not start: codex"),
                new ProviderUsage(UsageProvider.Claude, "Anthropic", null, ProviderStatus.NotInstalled, [], now, "Claude credentials not found; run `claude auth login`."),
            ],
            now);

        using var service = new StubUsageRefreshService(snapshot);
        using var viewModel = new AiLimitsViewModel(service, () => now);

        AssertEqual(0, viewModel.VisibleProviders.Count);
        AssertEqual(AiLimitsDisplayText.NoProvidersDetected, viewModel.GetCollapsedSummary(CollapsedSummaryLayout.Wide));
        AssertEqual(AiLimitsDisplayText.NoProvidersDetected, viewModel.GetCollapsedSummary(CollapsedSummaryLayout.CriticalOnly));

        return Task.CompletedTask;
    }

    private static void AssertEqual<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }

    private static void AssertNear(double expected, double actual, double tolerance)
    {
        if (Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException($"Expected {expected} +/- {tolerance}, got {actual}.");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertFalse(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertNotNull(object? value, string message)
    {
        if (value is null)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static int EstimateCompactTextWidth(string text)
    {
        var width = 12;

        foreach (var character in text)
        {
            width += character switch
            {
                ' ' => 4,
                '|' => 4,
                '%' => 9,
                '◎' or '◇' => 12,
                >= '0' and <= '9' => 8,
                >= 'A' and <= 'Z' => 9,
                >= 'a' and <= 'z' => 7,
                _ => 7,
            };
        }

        return width;
    }

    private static string CreateTempDirectory()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ai-limits-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("Timed out waiting for condition.");
            }

            await Task.Delay(10);
        }
    }

    private static IUsageProbe[] CreateSillProbes(ISettingsProvider settingsProvider)
    {
        var method = typeof(AiLimitsSill).GetMethod("CreateProbes", BindingFlags.NonPublic | BindingFlags.Static);
        AssertNotNull(method, "AiLimitsSill should keep probe creation available for composition validation.");

        var result = method!.Invoke(null, [settingsProvider, () => DateTimeOffset.Now]);
        return result as IUsageProbe[] ?? throw new InvalidOperationException("AiLimitsSill.CreateProbes did not return IUsageProbe[].");
    }

    private static ClaudeOAuthUsageClient CreateTestClaudeClient(string credentialsPath, HttpMessageHandler handler)
        => new(
            TimeSpan.FromSeconds(1),
            credentialsPath,
            new HttpClient(handler),
            usageEndpoint: "https://example.test/api/oauth/usage",
            tokenEndpoint: "https://example.test/v1/oauth/token");

    private static DateTimeOffset ReadClaudeCredentialExpiresAt(string credentialsPath)
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(credentialsPath));
        var milliseconds = document.RootElement
            .GetProperty("claudeAiOauth")
            .GetProperty("expiresAt")
            .GetInt64();

        return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
    }

    private static void RewriteClaudeCredentialAccessToken(string credentialsPath, string accessToken, DateTimeOffset expiresAt)
    {
        var json = File.ReadAllText(credentialsPath)
            .Replace("\"accessToken\": \"old-race-token\"", $"\"accessToken\": \"{accessToken}\"", StringComparison.Ordinal)
            .Replace(
                System.Text.RegularExpressions.Regex.Match(File.ReadAllText(credentialsPath), "\"expiresAt\": [0-9]+").Value,
                $"\"expiresAt\": {expiresAt.ToUnixTimeMilliseconds()}",
                StringComparison.Ordinal);
        File.WriteAllText(credentialsPath, json);
    }

    private static string CreateClaudeCredentialsFile(
        DateTimeOffset expiresAt,
        string accessToken = "test-access-token",
        string? refreshToken = "test-refresh-token",
        bool includeUnknownTopLevel = false,
        bool includeScopes = false)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"claude-credentials-{Guid.NewGuid():N}.json");
        var fields = new List<string>
        {
            $"                \"accessToken\": \"{accessToken}\"",
        };
        if (refreshToken is not null)
        {
            fields.Add($"                \"refreshToken\": \"{refreshToken}\"");
        }

        if (includeScopes)
        {
            fields.Add("                \"scopes\": [\"user:profile\", \"user:inference\"]");
        }

        fields.Add($"                \"expiresAt\": {expiresAt.ToUnixTimeMilliseconds()}");
        fields.Add("                \"subscriptionType\": \"pro\"");
        fields.Add("                \"rateLimitTier\": \"standard\"");

        var topLevelSuffix = includeUnknownTopLevel
            ? $",{Environment.NewLine}              \"someOtherField\": \"keep me\""
            : string.Empty;
        var json = "{"
            + Environment.NewLine
            + "              \"claudeAiOauth\": {"
            + Environment.NewLine
            + string.Join($",{Environment.NewLine}", fields)
            + Environment.NewLine
            + "              }"
            + topLevelSuffix
            + Environment.NewLine
            + "            }";
        File.WriteAllText(path, json);
        return path;
    }
}

internal sealed class StubUsageRefreshService(UsageSnapshot snapshot) : IUsageRefreshService
{
    public event EventHandler<UsageSnapshot>? UsageUpdated;

    public UsageSnapshot CurrentSnapshot { get; private set; } = snapshot;

    public TimeSpan CostRefreshInterval => TimeSpan.FromHours(4);

    public Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        UsageUpdated?.Invoke(this, CurrentSnapshot);
        return Task.FromResult(CurrentSnapshot);
    }

    public Task<UsageSnapshot> RefreshCostsAsync(CancellationToken cancellationToken = default)
    {
        UsageUpdated?.Invoke(this, CurrentSnapshot);
        return Task.FromResult(CurrentSnapshot);
    }

    public void StartMonitoring()
    {
    }

    public void StopMonitoring()
    {
    }

    public void UpdateRefreshInterval(TimeSpan refreshInterval)
    {
    }

    public void UpdateCostRefreshInterval(TimeSpan refreshInterval)
    {
    }

    public void Dispose()
    {
    }
}

internal sealed class ManualUsageRefreshService(UsageSnapshot initialSnapshot) : IUsageRefreshService
{
    public event EventHandler<UsageSnapshot>? UsageUpdated;

    public UsageSnapshot CurrentSnapshot { get; private set; } = initialSnapshot;

    public TimeSpan CostRefreshInterval => TimeSpan.FromHours(4);

    public void Publish(UsageSnapshot snapshot)
    {
        CurrentSnapshot = snapshot;
        UsageUpdated?.Invoke(this, CurrentSnapshot);
    }

    public Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CurrentSnapshot);

    public Task<UsageSnapshot> RefreshCostsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CurrentSnapshot);

    public void StartMonitoring()
    {
    }

    public void StopMonitoring()
    {
    }

    public void UpdateRefreshInterval(TimeSpan refreshInterval)
    {
    }

    public void UpdateCostRefreshInterval(TimeSpan refreshInterval)
    {
    }

    public void Dispose()
    {
    }
}

internal sealed class RecordingUsageRefreshService(UsageSnapshot initialSnapshot) : IUsageRefreshService
{
    public event EventHandler<UsageSnapshot>? UsageUpdated;

    public UsageSnapshot CurrentSnapshot { get; private set; } = initialSnapshot;

    public TimeSpan? LastRefreshInterval { get; private set; }

    public TimeSpan CostRefreshInterval => LastCostRefreshInterval ?? TimeSpan.FromHours(4);

    public TimeSpan? LastCostRefreshInterval { get; private set; }

    public Task<UsageSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        UsageUpdated?.Invoke(this, CurrentSnapshot);
        return Task.FromResult(CurrentSnapshot);
    }

    public Task<UsageSnapshot> RefreshCostsAsync(CancellationToken cancellationToken = default)
    {
        UsageUpdated?.Invoke(this, CurrentSnapshot);
        return Task.FromResult(CurrentSnapshot);
    }

    public void StartMonitoring()
    {
    }

    public void StopMonitoring()
    {
    }

    public void UpdateRefreshInterval(TimeSpan refreshInterval)
        => LastRefreshInterval = refreshInterval;

    public void UpdateCostRefreshInterval(TimeSpan refreshInterval)
        => LastCostRefreshInterval = refreshInterval;

    public void Dispose()
    {
    }
}

internal sealed class RecordingUsageAlertNotifier : IUsageAlertNotifier
{
    public List<UsageAboveExpectedAlert> Alerts { get; } = [];

    public void NotifyUsageAboveExpected(UsageAboveExpectedAlert alert)
        => Alerts.Add(alert);
}

internal sealed class RecordingUsageAlertNotificationSender : IUsageAlertNotificationSender
{
    public List<UsageAlertNotification> Notifications { get; } = [];

    public void Show(UsageAlertNotification notification)
        => Notifications.Add(notification);
}

internal sealed class ThrowingUsageAlertNotificationSender : IUsageAlertNotificationSender
{
    public int CallCount { get; private set; }

    public void Show(UsageAlertNotification notification)
    {
        CallCount++;
        throw new InvalidOperationException("notification blocked");
    }
}

internal sealed class MemoryUsageSnapshotCache(UsageSnapshot? initialSnapshot = null) : IUsageSnapshotCache
{
    public UsageSnapshot? LastWritten { get; private set; }

    public UsageSnapshot? Read()
        => initialSnapshot;

    public void Write(UsageSnapshot snapshot)
        => LastWritten = snapshot;
}

internal sealed class FixedApiCostEstimator(ApiCostEstimate? estimate) : IApiCostEstimator
{
    public int CallCount { get; private set; }

    public ApiCostEstimate? Estimate(ProviderUsage provider)
    {
        CallCount++;
        return estimate;
    }
}

internal sealed class ProviderSpecificApiCostEstimator(IReadOnlyDictionary<UsageProvider, ApiCostEstimate?> estimates) : IApiCostEstimator
{
    public int CallCount { get; private set; }

    public ApiCostEstimate? Estimate(ProviderUsage provider)
    {
        CallCount++;
        return estimates.TryGetValue(provider.Provider, out var estimate) ? estimate : null;
    }
}

internal sealed class StaticTokenUsageReader(IReadOnlyDictionary<string, TokenUsageTotals> usage) : ITokenUsageReader
{
    public IReadOnlyDictionary<string, TokenUsageTotals> ReadUsage(DateTimeOffset windowStart, DateTimeOffset windowEnd)
        => usage;
}

internal sealed class ThrowingApiCostEstimator : IApiCostEstimator
{
    public ApiCostEstimate? Estimate(ProviderUsage provider)
        => throw new InvalidOperationException("The requested operation requires an element of type 'Object', but the target element has type 'Null'.");
}

internal sealed class StaticProbe(UsageProvider provider, ProviderUsage usage) : IUsageProbe
{
    public UsageProvider Provider => provider;

    public Task<ProviderUsage> ReadAsync(CancellationToken cancellationToken)
        => Task.FromResult(usage);
}

internal sealed class DelayedProbe(DateTimeOffset now) : IUsageProbe
{
    public UsageProvider Provider => UsageProvider.Claude;

    public int CallCount { get; private set; }

    public async Task<ProviderUsage> ReadAsync(CancellationToken cancellationToken)
    {
        CallCount++;
        await Task.Delay(100, cancellationToken);

        return new ProviderUsage(
            UsageProvider.Claude,
            "Anthropic",
            "Mock",
            ProviderStatus.Ok,
            [new UsageWindow("5h", "5h", 25, now.AddHours(1), TimeSpan.FromHours(5), now.AddHours(-4))],
            now,
            null);
    }
}

internal sealed class ThrowingProbe(UsageProvider provider) : IUsageProbe
{
    public UsageProvider Provider { get; } = provider;

    public Task<ProviderUsage> ReadAsync(CancellationToken cancellationToken)
        => throw new InvalidOperationException("simulated probe failure");
}

internal sealed class SequenceProbe(UsageProvider provider, IReadOnlyList<ProbeStep> steps) : IUsageProbe
{
    private int _index;

    public UsageProvider Provider { get; } = provider;

    public int CallCount { get; private set; }

    public Task<ProviderUsage> ReadAsync(CancellationToken cancellationToken)
    {
        CallCount++;
        var step = steps[Math.Min(_index, steps.Count - 1)];
        _index++;

        if (step.Exception is not null)
        {
            throw step.Exception;
        }

        return Task.FromResult(step.Usage ?? throw new InvalidOperationException("Probe step did not define usage."));
    }
}

internal sealed record ProbeStep(ProviderUsage? Usage, Exception? Exception)
{
    public static ProbeStep Success(ProviderUsage usage)
        => new(usage, null);

    public static ProbeStep Failure(Exception exception)
        => new(null, exception);
}

internal sealed class FakeCodexAppServerClient(params string[] responses) : ICodexAppServerClient
{
    private int _index;

    public List<string> Methods { get; } = [];
    public List<object?> Parameters { get; } = [];

    public Task<string> SendRequestAsync(string method, object? parameters, CancellationToken cancellationToken)
    {
        Methods.Add(method);
        Parameters.Add(parameters);
        return Task.FromResult(responses[_index++]);
    }
}

internal sealed class FailingCodexAppServerClient(string message) : ICodexAppServerClient
{
    public Task<string> SendRequestAsync(string method, object? parameters, CancellationToken cancellationToken)
        => throw new CodexAppServerException(message);
}

internal sealed class DisposableFakeCodexAppServerClient : ICodexAppServerClient, IDisposable
{
    public bool Disposed { get; private set; }

    public Task<string> SendRequestAsync(string method, object? parameters, CancellationToken cancellationToken)
        => Task.FromResult("""{"result":{"rateLimits":{"primary":{"usedPercent":10},"secondary":{"usedPercent":20}}}}""");

    public void Dispose()
        => Disposed = true;
}

internal sealed class FailingClaudeUsageClient(string message) : IClaudeUsageClient
{
    public Task<ClaudeUsagePayload> ReadUsageAsync(CancellationToken cancellationToken)
        => throw new ClaudeUsageException(message);
}

internal sealed class MissingCommandCodexAppServerClient : ICodexAppServerClient
{
    public Task<string> SendRequestAsync(string method, object? parameters, CancellationToken cancellationToken)
        => throw new CodexAppServerException("Codex command not found or could not start: codex", null, commandMissing: true);
}

internal sealed class NotConfiguredClaudeUsageClient : IClaudeUsageClient
{
    public Task<ClaudeUsagePayload> ReadUsageAsync(CancellationToken cancellationToken)
        => throw new ClaudeUsageException("Claude credentials not found; run `claude auth login`.", notConfigured: true);
}

internal sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("HTTP should not be called.");
}

internal sealed class ClaudeRefreshHttpMessageHandler(
    string tokenResponse,
    string usageResponse,
    int tokenStatusCode = 200,
    int usageStatusCode = 200,
    Action? beforeTokenRequest = null) : HttpMessageHandler
{
    private bool _beforeTokenRequestInvoked;

    public List<RecordedHttpRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedHttpRequest(
            request.Method.Method,
            request.RequestUri?.AbsoluteUri ?? string.Empty,
            request.Headers.Authorization?.ToString(),
            body);

        if (recorded.Url.Contains("/v1/oauth/token", StringComparison.OrdinalIgnoreCase))
        {
            if (beforeTokenRequest is not null && !_beforeTokenRequestInvoked)
            {
                _beforeTokenRequestInvoked = true;
                beforeTokenRequest.Invoke();
                return await SendAsync(request, cancellationToken);
            }

            Requests.Add(recorded);
            return new HttpResponseMessage((System.Net.HttpStatusCode)tokenStatusCode)
            {
                Content = new StringContent(tokenResponse),
            };
        }

        Requests.Add(recorded);
        return new HttpResponseMessage((System.Net.HttpStatusCode)usageStatusCode)
        {
            Content = new StringContent(usageResponse),
        };
    }
}

internal sealed record RecordedHttpRequest(string Method, string Url, string? Authorization, string? Body);

internal sealed class StaticHttpMessageHandler(int statusCode, string content, IReadOnlyDictionary<string, string>? headers = null) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage((System.Net.HttpStatusCode)statusCode)
        {
            Content = new StringContent(content),
        };

        if (headers is not null)
        {
            foreach (var header in headers)
            {
                response.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return Task.FromResult(response);
    }
}

internal sealed class TestSettingsProvider : ISettingsProvider
{
    private readonly Dictionary<string, object?> _values = [];

    public event Windows.Foundation.TypedEventHandler<ISettingsProvider, SettingChangedEventArgs>? SettingChanged;

    public bool IsActivelyControlledByAdmin<T>(SettingDefinition<T> settingDefinition)
        => false;

    public T GetSetting<T>(SettingDefinition<T> settingDefinition)
        => _values.TryGetValue(settingDefinition.Name, out var value) && value is T typedValue
            ? typedValue
            : settingDefinition.DefaultValue;

    public void SetSetting<T>(SettingDefinition<T> settingDefinition, T value)
    {
        _values[settingDefinition.Name] = value;
        SettingChanged?.Invoke(this, new SettingChangedEventArgs(settingDefinition.Name, value));
    }

    public void ResetSetting<T>(SettingDefinition<T> settingDefinition)
    {
        _values.Remove(settingDefinition.Name);
        SettingChanged?.Invoke(this, new SettingChangedEventArgs(settingDefinition.Name, settingDefinition.DefaultValue));
    }

    public void OpenSettingsPageForSill(string internalSillName, string? sillSettingViewTitle)
    {
    }
}

internal sealed class TestPluginInfo : IPluginInfo
{
    private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ai-limits-plugin-{Guid.NewGuid():N}");

    public string GetPluginContentDirectory()
        => Ensure("content");

    public string GetPluginDataFolder()
        => Ensure("data");

    public string GetPluginTempFolder()
        => Ensure("temp");

    private string Ensure(string name)
    {
        var path = System.IO.Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
