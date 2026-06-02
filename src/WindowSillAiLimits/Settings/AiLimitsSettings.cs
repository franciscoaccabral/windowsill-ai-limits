using System.Reflection;

using WindowSill.API;

namespace WindowSillAiLimits.Settings;

public static class AiLimitsSettings
{
    // O endpoint de uso do Claude (api.anthropic.com/api/oauth/usage) rate-limita de forma
    // agressiva e nao envia Retry-After (ver claude-code#31637/#31021), entao o intervalo
    // automatico e conservador: padrao 15 min, minimo 5 min, maximo 60 min. As janelas 5h/7d
    // mudam devagar e ha botao de refresh manual para imediatismo.
    public const int MinimumRefreshIntervalSeconds = 300;
    public const int MaximumRefreshIntervalSeconds = 3600;
    public const int DefaultRefreshIntervalSeconds = 900;
    public const int LegacyDefaultRefreshIntervalSeconds = 60;
    public const int MinimumCostRefreshIntervalSeconds = 3600;
    public const int MaximumCostRefreshIntervalSeconds = 43200;
    public const int DefaultCostRefreshIntervalSeconds = 14400;

    /// <summary>Presets oferecidos na UI (em segundos): 5, 10, 15, 30 e 60 minutos.</summary>
    public static readonly IReadOnlyList<int> RefreshIntervalPresetsSeconds = [300, 600, 900, 1800, 3600];

    /// <summary>Presets oferecidos na UI (em segundos): 1h, 2h, 4h, 8h e 12h.</summary>
    public static readonly IReadOnlyList<int> CostRefreshIntervalPresetsSeconds = [3600, 7200, 14400, 28800, 43200];

    private static readonly Assembly Assembly = typeof(AiLimitsSettings).Assembly;

    public static readonly SettingDefinition<int> RefreshIntervalSeconds = new(DefaultRefreshIntervalSeconds, Assembly, "RefreshIntervalSeconds");

    public static readonly SettingDefinition<int> CostRefreshIntervalSeconds = new(DefaultCostRefreshIntervalSeconds, Assembly, "CostRefreshIntervalSeconds");

    public static readonly SettingDefinition<string> CodexCommandPath = new("codex", Assembly, "CodexCommandPath");

    public static readonly SettingDefinition<string> ClaudeCommandPath = new("claude", Assembly, "ClaudeCommandPath");

    public static readonly SettingDefinition<bool> ShowProviderNamesInBar = new(true, Assembly, "ShowProviderNamesInBar");

    public static readonly SettingDefinition<bool> ShowExpectedInBar = new(false, Assembly, "ShowExpectedInBar");

    public static readonly SettingDefinition<bool> ShowOverExpectedAlerts = new(true, Assembly, "ShowOverExpectedAlerts");

    public static readonly SettingDefinition<bool> ShowPreviewFlyout = new(true, Assembly, "ShowPreviewFlyout");

    public static readonly SettingDefinition<bool> UseMockData = new(false, Assembly, "UseMockData");

    public static IReadOnlyList<SettingMetadata> All { get; } =
    [
        new(RefreshIntervalSeconds.Name),
        new(CostRefreshIntervalSeconds.Name),
        new(CodexCommandPath.Name),
        new(ClaudeCommandPath.Name),
        new(ShowProviderNamesInBar.Name),
        new(ShowExpectedInBar.Name),
        new(ShowOverExpectedAlerts.Name),
        new(ShowPreviewFlyout.Name),
        new(UseMockData.Name),
    ];

    public static TimeSpan GetRefreshInterval(ISettingsProvider settingsProvider)
    {
        var seconds = GetRefreshIntervalSeconds(settingsProvider);
        return TimeSpan.FromSeconds(seconds);
    }

    public static int GetRefreshIntervalSeconds(ISettingsProvider settingsProvider)
    {
        var seconds = settingsProvider.GetSetting(RefreshIntervalSeconds);
        if (seconds == LegacyDefaultRefreshIntervalSeconds)
        {
            return DefaultRefreshIntervalSeconds;
        }

        return ClampRefreshIntervalSeconds(seconds);
    }

    public static void MigrateLegacyRefreshInterval(ISettingsProvider settingsProvider)
    {
        if (settingsProvider.GetSetting(RefreshIntervalSeconds) == LegacyDefaultRefreshIntervalSeconds)
        {
            settingsProvider.SetSetting(RefreshIntervalSeconds, DefaultRefreshIntervalSeconds);
        }
    }

    public static int ClampRefreshIntervalSeconds(int seconds)
        => Math.Clamp(seconds, MinimumRefreshIntervalSeconds, MaximumRefreshIntervalSeconds);

    public static TimeSpan GetCostRefreshInterval(ISettingsProvider settingsProvider)
    {
        var seconds = settingsProvider.GetSetting(CostRefreshIntervalSeconds);
        return TimeSpan.FromSeconds(ClampCostRefreshIntervalSeconds(seconds));
    }

    public static int ClampCostRefreshIntervalSeconds(int seconds)
        => Math.Clamp(seconds, MinimumCostRefreshIntervalSeconds, MaximumCostRefreshIntervalSeconds);
}

public sealed record SettingMetadata(string Name);
