using System.Globalization;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using WindowSill.API;

namespace WindowSillAiLimits.Settings;

public sealed class AiLimitsSettingsView : Grid
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly ComboBox _refreshInterval = new();
    private readonly ComboBox _costRefreshInterval = new();
    private readonly TextBox _codexPath = new();
    private readonly TextBox _claudePath = new();
    private readonly CheckBox _showProviderNames = new();
    private readonly CheckBox _showExpectedInBar = new();
    private readonly CheckBox _showOverExpectedAlerts = new();
    private readonly CheckBox _showPreviewFlyout = new();
    private readonly CheckBox _useMockData = new();

    public AiLimitsSettingsView(ISettingsProvider settingsProvider)
    {
        _settingsProvider = settingsProvider;
        Padding = new Thickness(16);

        var stack = new StackPanel
        {
            Spacing = 12,
            MaxWidth = 520,
        };

        stack.Children.Add(Header("AI Limits"));
        stack.Children.Add(Field("Intervalo de atualização", _refreshInterval));
        stack.Children.Add(Field("Intervalo de atualização dos custos", _costRefreshInterval));
        stack.Children.Add(Field("Caminho do comando Codex", _codexPath));
        stack.Children.Add(Field("Caminho do comando Claude", _claudePath));
        stack.Children.Add(_showProviderNames);
        stack.Children.Add(_showExpectedInBar);
        stack.Children.Add(_showOverExpectedAlerts);
        stack.Children.Add(_showPreviewFlyout);
        stack.Children.Add(_useMockData);

        Children.Add(stack);

        LoadSettings();
        WireEvents();
    }

    private void LoadSettings()
    {
        AiLimitsSettings.MigrateLegacyRefreshInterval(_settingsProvider);

        _refreshInterval.HorizontalAlignment = HorizontalAlignment.Stretch;
        _refreshInterval.Items.Clear();
        foreach (var seconds in AiLimitsSettings.RefreshIntervalPresetsSeconds)
        {
            _refreshInterval.Items.Add(new ComboBoxItem
            {
                Content = FormatIntervalLabel(seconds),
                Tag = seconds,
            });
        }

        SelectClosestInterval(AiLimitsSettings.GetRefreshIntervalSeconds(_settingsProvider));

        _costRefreshInterval.HorizontalAlignment = HorizontalAlignment.Stretch;
        _costRefreshInterval.Items.Clear();
        foreach (var seconds in AiLimitsSettings.CostRefreshIntervalPresetsSeconds)
        {
            _costRefreshInterval.Items.Add(new ComboBoxItem
            {
                Content = FormatIntervalLabel(seconds),
                Tag = seconds,
            });
        }

        SelectClosestCostInterval(_settingsProvider.GetSetting(AiLimitsSettings.CostRefreshIntervalSeconds));

        _codexPath.Text = _settingsProvider.GetSetting(AiLimitsSettings.CodexCommandPath);
        _claudePath.Text = _settingsProvider.GetSetting(AiLimitsSettings.ClaudeCommandPath);

        _showProviderNames.Content = "Mostrar nomes dos provedores na barra";
        _showProviderNames.IsChecked = _settingsProvider.GetSetting(AiLimitsSettings.ShowProviderNamesInBar);

        _showExpectedInBar.Content = "Mostrar previsto na barra";
        _showExpectedInBar.IsChecked = _settingsProvider.GetSetting(AiLimitsSettings.ShowExpectedInBar);

        _showOverExpectedAlerts.Content = "Avisar quando realizado passar o previsto";
        _showOverExpectedAlerts.IsChecked = _settingsProvider.GetSetting(AiLimitsSettings.ShowOverExpectedAlerts);

        _showPreviewFlyout.Content = "Mostrar prévia ao passar o mouse";
        _showPreviewFlyout.IsChecked = _settingsProvider.GetSetting(AiLimitsSettings.ShowPreviewFlyout);

        _useMockData.Content = "Usar dados fictícios";
        _useMockData.IsChecked = _settingsProvider.GetSetting(AiLimitsSettings.UseMockData);
    }

    private void WireEvents()
    {
        _refreshInterval.SelectionChanged += (_, _) =>
        {
            if (_refreshInterval.SelectedItem is ComboBoxItem { Tag: int seconds })
            {
                _settingsProvider.SetSetting(
                    AiLimitsSettings.RefreshIntervalSeconds,
                    AiLimitsSettings.ClampRefreshIntervalSeconds(seconds));
            }
        };
        _costRefreshInterval.SelectionChanged += (_, _) =>
        {
            if (_costRefreshInterval.SelectedItem is ComboBoxItem { Tag: int seconds })
            {
                _settingsProvider.SetSetting(
                    AiLimitsSettings.CostRefreshIntervalSeconds,
                    AiLimitsSettings.ClampCostRefreshIntervalSeconds(seconds));
            }
        };
        _codexPath.TextChanged += (_, _) => _settingsProvider.SetSetting(AiLimitsSettings.CodexCommandPath, _codexPath.Text);
        _claudePath.TextChanged += (_, _) => _settingsProvider.SetSetting(AiLimitsSettings.ClaudeCommandPath, _claudePath.Text);
        _showProviderNames.Checked += (_, _) => _settingsProvider.SetSetting(AiLimitsSettings.ShowProviderNamesInBar, true);
        _showProviderNames.Unchecked += (_, _) => _settingsProvider.SetSetting(AiLimitsSettings.ShowProviderNamesInBar, false);
        _showExpectedInBar.Checked += (_, _) => _settingsProvider.SetSetting(AiLimitsSettings.ShowExpectedInBar, true);
        _showExpectedInBar.Unchecked += (_, _) => _settingsProvider.SetSetting(AiLimitsSettings.ShowExpectedInBar, false);
        _showOverExpectedAlerts.Checked += (_, _) => _settingsProvider.SetSetting(AiLimitsSettings.ShowOverExpectedAlerts, true);
        _showOverExpectedAlerts.Unchecked += (_, _) => _settingsProvider.SetSetting(AiLimitsSettings.ShowOverExpectedAlerts, false);
        _showPreviewFlyout.Checked += (_, _) => _settingsProvider.SetSetting(AiLimitsSettings.ShowPreviewFlyout, true);
        _showPreviewFlyout.Unchecked += (_, _) => _settingsProvider.SetSetting(AiLimitsSettings.ShowPreviewFlyout, false);
        _useMockData.Checked += (_, _) => _settingsProvider.SetSetting(AiLimitsSettings.UseMockData, true);
        _useMockData.Unchecked += (_, _) => _settingsProvider.SetSetting(AiLimitsSettings.UseMockData, false);
    }

    private void SelectClosestInterval(int seconds)
    {
        var closest = AiLimitsSettings.RefreshIntervalPresetsSeconds
            .OrderBy(preset => Math.Abs(preset - seconds))
            .First();

        for (var index = 0; index < _refreshInterval.Items.Count; index++)
        {
            if (_refreshInterval.Items[index] is ComboBoxItem { Tag: int preset } && preset == closest)
            {
                _refreshInterval.SelectedIndex = index;
                return;
            }
        }
    }

    private void SelectClosestCostInterval(int seconds)
    {
        var closest = AiLimitsSettings.CostRefreshIntervalPresetsSeconds
            .OrderBy(preset => Math.Abs(preset - seconds))
            .First();

        for (var index = 0; index < _costRefreshInterval.Items.Count; index++)
        {
            if (_costRefreshInterval.Items[index] is ComboBoxItem { Tag: int preset } && preset == closest)
            {
                _costRefreshInterval.SelectedIndex = index;
                return;
            }
        }
    }

    private static string FormatIntervalLabel(int seconds)
    {
        if (seconds >= 3600 && seconds % 3600 == 0)
        {
            var hours = seconds / 3600;
            return hours == 1
                ? "1 hora"
                : string.Create(CultureInfo.InvariantCulture, $"{hours} horas");
        }

        var minutes = seconds / 60;
        return string.Create(CultureInfo.InvariantCulture, $"{minutes} minutos");
    }

    private static TextBlock Header(string text)
        => new()
        {
            Text = text,
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };

    private static StackPanel Field(string label, Control input)
    {
        var stack = new StackPanel
        {
            Spacing = 4,
        };

        stack.Children.Add(new TextBlock { Text = label });
        stack.Children.Add(input);
        return stack;
    }
}
