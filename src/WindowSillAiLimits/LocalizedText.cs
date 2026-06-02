using System.Globalization;

using WindowSill.API;

namespace WindowSillAiLimits;

public static class LocalizedText
{
    public const string AssemblyName = "WindowSillAiLimits";
    public const string ResourceFileName = "Resources";

    private static readonly IReadOnlyDictionary<string, string> EnUs = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DisplayName"] = "AI Limits",
        ["Action.Refresh"] = "Refresh",
        ["Action.Settings"] = "Settings",
        ["Action.RefreshUsage"] = "Refresh usage",
        ["Action.RefreshCosts"] = "Refresh API costs",
        ["Action.ToggleCosts"] = "Expand or collapse API costs",
        ["Action.OpenSettings"] = "Open AI Limits settings",
        ["Popup.SourceNote"] = "Local tool data",
        ["Popup.NoProvidersDetected"] = "No AI provider detected",
        ["Popup.ApiCostsTitle"] = "API costs",
        ["Popup.ApiCostsSubtitleFormat"] = "active 7d window - {0} - local estimate, not a bill",
        ["Popup.NoCostProviders"] = "No provider detected for cost estimates.",
        ["Popup.ProviderCostsUnavailable"] = "Costs unavailable for this provider.",
        ["Popup.ProviderCostSubtitleFormat"] = "cost by model used in the {0} window",
        ["Popup.ModelHeader"] = "Model",
        ["Popup.TokensHeader"] = "Tokens",
        ["Popup.PriceHeader"] = "Value/token",
        ["Popup.CostHeader"] = "Cost",
        ["Popup.NoPrice"] = "no price",
        ["Popup.MoreModels"] = "more models",
        ["Popup.MoreModelsFormat"] = "{0} more model(s)",
        ["Popup.TotalPriced"] = "Total priced",
        ["Popup.NoPricedTokens"] = "no priced tokens",
        ["Popup.LocalEstimateNotBill"] = "local estimate, not a bill",
        ["Popup.CodexSourceNote"] = "Using the local Codex app-server. No token is stored.",
        ["Popup.ClaudeSourceNote"] = "5h and 7d are read from local Claude Code OAuth state.",
        ["Provider.CodexTitle"] = "Codex",
        ["Provider.ClaudeTitle"] = "Claude Code",
        ["Provider.CodexSubtitle"] = "ChatGPT account",
        ["Provider.ClaudeSubtitle"] = "Subscription account",
        ["Status.Ok"] = "OK",
        ["Status.Warning"] = "Warning",
        ["Status.Unavailable"] = "Unavailable",
        ["Status.Stale"] = "Stale",
        ["Status.Error"] = "Error",
        ["Status.NotInstalled"] = "Not installed",
        ["Pacing.Used"] = "Used",
        ["Pacing.ExpectedSoFar"] = "Expected so far",
        ["Pacing.Difference"] = "Difference",
        ["Pacing.CurrentAveragePace"] = "Current average pace",
        ["Pacing.ProjectedExhaustion"] = "Projected exhaustion",
        ["Pacing.ForecastImpact"] = "Impact",
        ["Pacing.WeeklyWindowElapsed"] = "7d window elapsed",
        ["Pacing.NextWeeklyReset"] = "Next 7d reset",
        ["Pacing.FiveHourWindow"] = "5h window",
        ["Pacing.QueriedAt"] = "Queried at",
        ["Pacing.PerDaySuffix"] = "per day",
        ["Pacing.DaysElapsedFormat"] = "{0} of 7 days",
        ["Pacing.ResetUnavailable"] = "reset unavailable",
        ["Pacing.ResetTimeFormat"] = "reset {0}",
        ["Pacing.UsedResetFormat"] = "{0} used; {1}",
        ["Pacing.DoesNotExhaustBeforeReset"] = "does not exhaust before reset",
        ["Pacing.NoForecast"] = "no forecast",
        ["Pacing.ResetComesFirst"] = "reset comes first",
        ["Pacing.InsufficientPace"] = "insufficient pace",
        ["Pacing.BeforeResetFormat"] = "{0} before reset",
        ["Pacing.ResetTodayFormat"] = "reset today {0}",
        ["Pacing.ResetDayFormat"] = "reset {0}",
        ["Pacing.HoursMinutesFormat"] = "{0}h {1}m",
        ["Pacing.MinutesFormat"] = "{0}m",
        ["Preview.ExpectedFormat"] = "{0} {1}",
        ["Preview.ExpectedRatioFormat"] = "{0}% of {1}% ({2} of expected)",
        ["Preview.Above"] = "above",
        ["Preview.Below"] = "below",
        ["Preview.DifferenceFormat"] = "{0} p.p. {1}",
        ["ViewModel.NoUpdate"] = "no update",
        ["ViewModel.UpdatedFormat"] = "updated {0}",
        ["ViewModel.CostsNoUpdate"] = "costs not updated",
        ["ViewModel.CostsUpdatedFormat"] = "costs updated {0}",
        ["ViewModel.TokensSuffix"] = "tokens",
        ["Settings.RefreshInterval.Label"] = "Refresh interval",
        ["Settings.CostRefreshInterval.Label"] = "Cost refresh interval",
        ["Settings.CodexPath.Label"] = "Codex command path",
        ["Settings.ClaudePath.Label"] = "Claude command path",
        ["Settings.ShowProviderNames"] = "Show provider names in the bar",
        ["Settings.ShowExpectedInBar"] = "Show expected usage in the bar",
        ["Settings.ShowOverExpectedAlerts"] = "Notify when actual usage passes expected usage",
        ["Settings.ShowPreviewFlyout"] = "Show preview on hover",
        ["Settings.UseMockData"] = "Use mock data",
        ["Settings.OneHour"] = "1 hour",
        ["Settings.HoursFormat"] = "{0} hours",
        ["Settings.MinutesFormat"] = "{0} minutes",
        ["Notification.UsageAboveExpected.BodyFormat"] = "{0} {1}: actual {2}% passed expected {3}%.",
        ["Sanitizer.UsageQueryFailed"] = "Usage query failed.",
    };

    private static readonly IReadOnlyDictionary<string, string> PtBr = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DisplayName"] = "AI Limits",
        ["Action.Refresh"] = "Atualizar",
        ["Action.Settings"] = "Configurações",
        ["Action.RefreshUsage"] = "Atualizar uso",
        ["Action.RefreshCosts"] = "Atualizar custos API",
        ["Action.ToggleCosts"] = "Expandir ou recolher custos API",
        ["Action.OpenSettings"] = "Abrir configurações do AI Limits",
        ["Popup.SourceNote"] = "Dados de ferramentas locais",
        ["Popup.NoProvidersDetected"] = "Nenhum provedor de IA detectado",
        ["Popup.ApiCostsTitle"] = "Custos API",
        ["Popup.ApiCostsSubtitleFormat"] = "janela 7d ativa - {0} - estimativa local, não fatura",
        ["Popup.NoCostProviders"] = "Nenhum provider detectado para estimar custos.",
        ["Popup.ProviderCostsUnavailable"] = "Custos indisponíveis para este provider.",
        ["Popup.ProviderCostSubtitleFormat"] = "custo por modelo usado na janela {0}",
        ["Popup.ModelHeader"] = "Modelo",
        ["Popup.TokensHeader"] = "Tokens",
        ["Popup.PriceHeader"] = "Valor/token",
        ["Popup.CostHeader"] = "Custo",
        ["Popup.NoPrice"] = "sem preço",
        ["Popup.MoreModels"] = "mais modelos",
        ["Popup.MoreModelsFormat"] = "mais {0} modelo(s)",
        ["Popup.TotalPriced"] = "Total precificado",
        ["Popup.NoPricedTokens"] = "sem tokens precificados",
        ["Popup.LocalEstimateNotBill"] = "estimativa local, não fatura",
        ["Popup.CodexSourceNote"] = "Usando o Codex app-server local. Nenhum token é armazenado.",
        ["Popup.ClaudeSourceNote"] = "5h e 7d são lidos do estado OAuth local do Claude Code.",
        ["Provider.CodexTitle"] = "Codex",
        ["Provider.ClaudeTitle"] = "Claude Code",
        ["Provider.CodexSubtitle"] = "Conta ChatGPT",
        ["Provider.ClaudeSubtitle"] = "Conta de assinatura",
        ["Status.Ok"] = "OK",
        ["Status.Warning"] = "Atenção",
        ["Status.Unavailable"] = "Indisponível",
        ["Status.Stale"] = "Desatualizado",
        ["Status.Error"] = "Erro",
        ["Status.NotInstalled"] = "Não instalado",
        ["Pacing.Used"] = "Usado",
        ["Pacing.ExpectedSoFar"] = "Esperado até agora",
        ["Pacing.Difference"] = "Diferença",
        ["Pacing.CurrentAveragePace"] = "Ritmo médio atual",
        ["Pacing.ProjectedExhaustion"] = "Previsto terminar",
        ["Pacing.ForecastImpact"] = "Impacto",
        ["Pacing.WeeklyWindowElapsed"] = "Janela 7d decorrida",
        ["Pacing.NextWeeklyReset"] = "Próximo reset 7d",
        ["Pacing.FiveHourWindow"] = "Janela 5h",
        ["Pacing.QueriedAt"] = "Consultado em",
        ["Pacing.PerDaySuffix"] = "por dia",
        ["Pacing.DaysElapsedFormat"] = "{0} de 7 dias",
        ["Pacing.ResetUnavailable"] = "reset indisponível",
        ["Pacing.ResetTimeFormat"] = "reset {0}",
        ["Pacing.UsedResetFormat"] = "{0} usado; {1}",
        ["Pacing.DoesNotExhaustBeforeReset"] = "não esgota antes do reset",
        ["Pacing.NoForecast"] = "sem previsão",
        ["Pacing.ResetComesFirst"] = "reset chega primeiro",
        ["Pacing.InsufficientPace"] = "ritmo insuficiente",
        ["Pacing.BeforeResetFormat"] = "{0} antes do reset",
        ["Pacing.ResetTodayFormat"] = "reset hoje {0}",
        ["Pacing.ResetDayFormat"] = "reset {0}",
        ["Pacing.HoursMinutesFormat"] = "{0}h {1}m",
        ["Pacing.MinutesFormat"] = "{0}m",
        ["Preview.ExpectedFormat"] = "{0} {1}",
        ["Preview.ExpectedRatioFormat"] = "{0}% de {1}% ({2} do previsto)",
        ["Preview.Above"] = "acima",
        ["Preview.Below"] = "abaixo",
        ["Preview.DifferenceFormat"] = "{0} p.p. {1}",
        ["ViewModel.NoUpdate"] = "sem atualização",
        ["ViewModel.UpdatedFormat"] = "atualizado {0}",
        ["ViewModel.CostsNoUpdate"] = "custos sem atualização",
        ["ViewModel.CostsUpdatedFormat"] = "custos atualizados {0}",
        ["ViewModel.TokensSuffix"] = "tokens",
        ["Settings.RefreshInterval.Label"] = "Intervalo de atualização",
        ["Settings.CostRefreshInterval.Label"] = "Intervalo de atualização dos custos",
        ["Settings.CodexPath.Label"] = "Caminho do comando Codex",
        ["Settings.ClaudePath.Label"] = "Caminho do comando Claude",
        ["Settings.ShowProviderNames"] = "Mostrar nomes dos provedores na barra",
        ["Settings.ShowExpectedInBar"] = "Mostrar previsto na barra",
        ["Settings.ShowOverExpectedAlerts"] = "Avisar quando realizado passar o previsto",
        ["Settings.ShowPreviewFlyout"] = "Mostrar prévia ao passar o mouse",
        ["Settings.UseMockData"] = "Usar dados fictícios",
        ["Settings.OneHour"] = "1 hora",
        ["Settings.HoursFormat"] = "{0} horas",
        ["Settings.MinutesFormat"] = "{0} minutos",
        ["Notification.UsageAboveExpected.BodyFormat"] = "{0} {1}: realizado {2}% passou o previsto {3}%.",
        ["Sanitizer.UsageQueryFailed"] = "Usage query failed.",
    };

    public static string Get(string key)
    {
        var uid = Uid(key);
        try
        {
            var localized = uid.GetLocalizedString();
            return string.IsNullOrWhiteSpace(localized) || string.Equals(localized, uid, StringComparison.Ordinal)
                ? Get(key, CultureInfo.CurrentUICulture.Name)
                : localized;
        }
        catch (Exception)
        {
            return Get(key, CultureInfo.CurrentUICulture.Name);
        }
    }

    public static string Get(string key, string cultureName)
    {
        var resources = cultureName.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? PtBr : EnUs;
        return resources.TryGetValue(key, out var value) || EnUs.TryGetValue(key, out value)
            ? value
            : key;
    }

    public static string Uid(string key)
        => $"/{AssemblyName}/{ResourceFileName}/{key}";

    public static string Format(string key, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
