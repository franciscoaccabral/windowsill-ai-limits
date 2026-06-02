using System.ComponentModel;

using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using WindowSill.API;

using WindowSillAiLimits.Models;
using WindowSillAiLimits.ViewModels;

namespace WindowSillAiLimits.Views;

public sealed class AiLimitsBarView : Grid
{
    public const double MinimumCompactWidth = 0;
    public const double AssumedCompactWidth = 220;
    public const double CriticalOnlyMaximumWidth = 120;
    public const double ShowProviderNamesMinimumWidth = 420;

    private readonly SillView _sillView;
    private readonly AiLimitsViewModel _viewModel;
    private readonly bool _showProviderNamesSetting;
    private bool _showExpectedInBar;
    private readonly string? _pluginContentDirectory;
    private readonly Button _mainButton = new();
    private readonly Grid _contentRoot = new();
    private readonly StackPanel _summaryRoot;
    private readonly TextBlock _criticalSummary = ValueBlock();
    private readonly TextBlock _openAiFiveHourValue = ValueBlock();
    private readonly TextBlock _openAiSevenDayValue = ValueBlock();
    private readonly TextBlock _claudeFiveHourValue = ValueBlock();
    private readonly TextBlock _claudeSevenDayValue = ValueBlock();
    private readonly TextBlock _openAiName = NameBlock("OpenAI");
    private readonly TextBlock _claudeName = NameBlock("Anthropic");
    private readonly TextBlock _separator = new()
    {
        Text = "|",
        Foreground = AiLimitsPalette.MutedText,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly List<FrameworkElement> _providerIcons = [];
    private readonly List<TextBlock> _labelBlocks = [];
    private readonly List<StackPanel> _providerPanels = [];
    private readonly StackPanel _codexPanel;
    private readonly StackPanel _claudePanel;
    private DateTimeOffset _lastClickRaised;

    public event RoutedEventHandler? Clicked;

    public AiLimitsBarView(
        SillView sillView,
        AiLimitsViewModel viewModel,
        bool showProviderNames = true,
        bool showExpectedInBar = false,
        string? pluginContentDirectory = null)
    {
        _sillView = sillView;
        _viewModel = viewModel;
        _showProviderNamesSetting = showProviderNames;
        _showExpectedInBar = showExpectedInBar;
        _pluginContentDirectory = pluginContentDirectory;
        Background = new SolidColorBrush(Colors.Transparent);
        MinWidth = MinimumCompactWidth;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;

        _summaryRoot = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };

        SizeChanged += OnSizeChanged;
        Tapped += OnFallbackTapped;
        PointerReleased += OnFallbackPointerReleased;
        _sillView.IsSillOrientationOrSizeChanged += OnSillOrientationOrSizeChanged;

        _codexPanel = BuildProvider(UsageProvider.Codex, _openAiName, _openAiFiveHourValue, _openAiSevenDayValue);
        _claudePanel = BuildProvider(UsageProvider.Claude, _claudeName, _claudeFiveHourValue, _claudeSevenDayValue);
        _summaryRoot.Children.Add(_codexPanel);
        _summaryRoot.Children.Add(_separator);
        _summaryRoot.Children.Add(_claudePanel);

        _criticalSummary.Visibility = Visibility.Collapsed;
        _criticalSummary.HorizontalAlignment = HorizontalAlignment.Left;
        _criticalSummary.TextWrapping = TextWrapping.NoWrap;

        _contentRoot.HorizontalAlignment = HorizontalAlignment.Left;
        _contentRoot.VerticalAlignment = VerticalAlignment.Center;
        _contentRoot.Children.Add(_summaryRoot);
        _contentRoot.Children.Add(_criticalSummary);

        _mainButton.Padding = new Thickness(0);
        _mainButton.Background = new SolidColorBrush(Colors.Transparent);
        _mainButton.BorderThickness = new Thickness(0);
        _mainButton.MinWidth = 0;
        _mainButton.HorizontalAlignment = HorizontalAlignment.Left;
        _mainButton.HorizontalContentAlignment = HorizontalAlignment.Left;
        _mainButton.VerticalAlignment = VerticalAlignment.Center;
        _mainButton.VerticalContentAlignment = VerticalAlignment.Center;
        _mainButton.Content = _contentRoot;
        _mainButton.Click += OnMainButtonClicked;
        _mainButton.Tapped += OnFallbackTapped;
        _mainButton.PointerReleased += OnFallbackPointerReleased;
        TryApplySillButtonStyle(_mainButton);
        Children.Add(_mainButton);

        ToolTipService.SetToolTip(this, "AI Limits");
        _viewModel.PropertyChanged += OnViewModelChanged;
        RefreshText();
        ApplyLayout(ActualWidth);
    }

    private void OnMainButtonClicked(object sender, RoutedEventArgs e)
        => RaiseClicked(e);

    private void OnFallbackTapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        RaiseClicked(e);
    }

    private void OnFallbackPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
        RaiseClicked(e);
    }

    private void RaiseClicked(RoutedEventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastClickRaised < TimeSpan.FromMilliseconds(250))
        {
            return;
        }

        _lastClickRaised = now;
        Clicked?.Invoke(this, e);
    }

    private StackPanel BuildProvider(UsageProvider provider, TextBlock name, TextBlock fiveHourValue, TextBlock sevenDayValue)
    {
        var providerIcon = BuildProviderIcon(provider);
        _providerIcons.Add(providerIcon);

        var fiveHourLabel = LabelBlock("5h");
        var sevenDayLabel = LabelBlock("7d");
        _labelBlocks.Add(fiveHourLabel);
        _labelBlocks.Add(sevenDayLabel);

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _providerPanels.Add(panel);

        panel.Children.Add(providerIcon);
        panel.Children.Add(name);
        panel.Children.Add(fiveHourLabel);
        panel.Children.Add(fiveHourValue);
        panel.Children.Add(sevenDayLabel);
        panel.Children.Add(sevenDayValue);

        return panel;
    }

    private void RefreshText()
    {
        SetValueText(_openAiFiveHourValue, _showExpectedInBar ? GetExpectedWindowText(UsageProvider.Codex, "5h") : _viewModel.OpenAiFiveHourText, GetDisplaySeverity(UsageProvider.Codex, "5h"), AiLimitsPalette.Codex);
        SetValueText(_openAiSevenDayValue, _showExpectedInBar ? GetExpectedWindowText(UsageProvider.Codex, "7d") : _viewModel.OpenAiSevenDayText, GetDisplaySeverity(UsageProvider.Codex, "7d"), AiLimitsPalette.Codex);
        SetValueText(_claudeFiveHourValue, _showExpectedInBar ? GetExpectedWindowText(UsageProvider.Claude, "5h") : _viewModel.ClaudeFiveHourText, GetDisplaySeverity(UsageProvider.Claude, "5h"), AiLimitsPalette.Claude);
        SetValueText(_claudeSevenDayValue, _showExpectedInBar ? GetExpectedWindowText(UsageProvider.Claude, "7d") : _viewModel.ClaudeSevenDayText, GetDisplaySeverity(UsageProvider.Claude, "7d"), AiLimitsPalette.Claude);
        _criticalSummary.Text = _viewModel.GetCollapsedSummary(CollapsedSummaryLayout.CriticalOnly, _showExpectedInBar);
    }

    public void SetShowExpectedInBar(bool showExpectedInBar)
    {
        if (_showExpectedInBar == showExpectedInBar)
        {
            return;
        }

        _showExpectedInBar = showExpectedInBar;
        RefreshText();
        ApplyLayout(ActualWidth);
    }

    private string GetExpectedWindowText(UsageProvider provider, string windowId)
        => _viewModel.GetWindowDisplayText(provider, windowId, includeExpected: true);

    private LimitSeverity GetDisplaySeverity(UsageProvider provider, string windowId)
        => _viewModel.GetWindowDisplaySeverity(provider, windowId, _showExpectedInBar);

    private static void SetValueText(TextBlock block, string text, LimitSeverity severity, SolidColorBrush accent)
    {
        var usedBrush = AiLimitsPalette.ForSeverity(severity, accent);
        block.Inlines.Clear();
        block.Foreground = usedBrush;

        var slashIndex = text.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex < 0)
        {
            block.Text = text;
            return;
        }

        block.Text = string.Empty;
        block.Inlines.Add(new Run
        {
            Text = text[..slashIndex],
            Foreground = usedBrush,
        });
        block.Inlines.Add(new Run
        {
            Text = text[slashIndex..],
            Foreground = AiLimitsPalette.MutedText,
        });
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshText();
        ApplyLayout(ActualWidth);
    }

    private void OnSillOrientationOrSizeChanged(object? sender, EventArgs e)
        => ApplyLayout(ActualWidth);

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        => ApplyLayout(e.NewSize.Width);

    private void ApplyLayout(double width)
    {
        var layout = GetLayoutForHost(_sillView.SillOrientationAndSize, width);

        var codexInstalled = _viewModel.OpenAiStatus != ProviderStatus.NotInstalled;
        var claudeInstalled = _viewModel.ClaudeStatus != ProviderStatus.NotInstalled;
        var anyInstalled = codexInstalled || claudeInstalled;

        // Sem nenhum provedor instalado, mostra a mensagem neutra (reusa o bloco critical-only).
        var isCriticalOnly = layout == CollapsedSummaryLayout.CriticalOnly || !anyInstalled;

        _summaryRoot.Visibility = isCriticalOnly ? Visibility.Collapsed : Visibility.Visible;
        _criticalSummary.Visibility = isCriticalOnly ? Visibility.Visible : Visibility.Collapsed;

        // Oculta o painel de um provedor nao instalado e o separador quando so um aparece.
        _codexPanel.Visibility = codexInstalled ? Visibility.Visible : Visibility.Collapsed;
        _claudePanel.Visibility = claudeInstalled ? Visibility.Visible : Visibility.Collapsed;
        _separator.Visibility = codexInstalled && claudeInstalled ? Visibility.Visible : Visibility.Collapsed;

        var showNames = ShouldShowProviderNames(width, _showProviderNamesSetting) && layout == CollapsedSummaryLayout.Wide && anyInstalled;
        _openAiName.Visibility = showNames && codexInstalled ? Visibility.Visible : Visibility.Collapsed;
        _claudeName.Visibility = showNames && claudeInstalled ? Visibility.Visible : Visibility.Collapsed;
        ApplyMetrics(layout, showNames);
    }

    public static CollapsedSummaryLayout GetLayoutForWidth(double width)
        => NormalizeWidth(width) < CriticalOnlyMaximumWidth
            ? CollapsedSummaryLayout.CriticalOnly
            : NormalizeWidth(width) < ShowProviderNamesMinimumWidth
                ? CollapsedSummaryLayout.Narrow
                : CollapsedSummaryLayout.Wide;

    public static CollapsedSummaryLayout GetLayoutForHost(SillOrientationAndSize orientationAndSize, double width)
        => orientationAndSize is SillOrientationAndSize.VerticalLarge
                or SillOrientationAndSize.VerticalMedium
                or SillOrientationAndSize.VerticalSmall
            ? CollapsedSummaryLayout.CriticalOnly
            : GetLayoutForWidth(width);

    public static bool ShouldShowProviderNames(double width, bool showProviderNamesSetting)
        => showProviderNamesSetting && GetLayoutForWidth(width) == CollapsedSummaryLayout.Wide;

    private void ApplyMetrics(CollapsedSummaryLayout layout, bool showNames)
    {
        var fontSize = layout switch
        {
            CollapsedSummaryLayout.Wide => 12,
            CollapsedSummaryLayout.Narrow => 12,
            _ => 12,
        };
        var glyphSize = layout == CollapsedSummaryLayout.Wide ? 13 : 12;

        Padding = showNames ? new Thickness(4, 0, 4, 0) : new Thickness(2, 0, 2, 0);
        _summaryRoot.Spacing = showNames ? 8 : 4;
        ApplyTextMetrics(_separator, fontSize);
        ApplyTextMetrics(_criticalSummary, fontSize);
        _criticalSummary.FontWeight = FontWeights.SemiBold;

        foreach (var panel in _providerPanels)
        {
            panel.Spacing = showNames ? 4 : 2;
        }

        foreach (var icon in _providerIcons)
        {
            icon.Width = glyphSize;
            icon.Height = glyphSize;
        }

        foreach (var block in _labelBlocks)
        {
            ApplyTextMetrics(block, fontSize);
        }

        foreach (var block in new[] { _openAiName, _claudeName, _openAiFiveHourValue, _openAiSevenDayValue, _claudeFiveHourValue, _claudeSevenDayValue })
        {
            ApplyTextMetrics(block, fontSize);
        }
    }

    private static double NormalizeWidth(double width)
        => width > 0 ? width : AssumedCompactWidth;

    private static void ApplyTextMetrics(TextBlock block, double fontSize)
    {
        block.FontSize = fontSize;
        block.LineHeight = fontSize + 2;
        block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
    }

    private FrameworkElement BuildProviderIcon(UsageProvider provider)
    {
        var (automationName, assetPath) = provider == UsageProvider.Codex
            ? ("OpenAI", GetAssetUri("openai-mark.svg"))
            : ("Anthropic", GetAssetUri("anthropic-mark.svg"));

        var image = new Image
        {
            Width = 14,
            Height = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Source = new SvgImageSource(assetPath),
            Stretch = Stretch.Uniform,
        };
        AutomationProperties.SetName(image, automationName);
        return image;
    }

    private Uri GetAssetUri(string fileName)
    {
        if (!string.IsNullOrWhiteSpace(_pluginContentDirectory))
        {
            foreach (var assetPath in new[]
            {
                System.IO.Path.Combine(_pluginContentDirectory, "Assets", fileName),
                System.IO.Path.Combine(_pluginContentDirectory, "WindowSillAiLimits", "Assets", fileName),
            })
            {
                if (File.Exists(assetPath))
                {
                    return new Uri(System.IO.Path.GetFullPath(assetPath), UriKind.Absolute);
                }
            }
        }

        return new Uri($"ms-appx:///WindowSillAiLimits/Assets/{fileName}");
    }

    private static void TryApplySillButtonStyle(Button button)
    {
        if (Application.Current?.Resources.TryGetValue("SillButtonStyle", out var style) == true &&
            style is Style sillButtonStyle)
        {
            button.Style = sillButtonStyle;
        }
    }

    private static TextBlock LabelBlock(string text)
        => new()
        {
            Text = text,
            Foreground = AiLimitsPalette.Text,
            VerticalAlignment = VerticalAlignment.Center,
        };

    private static TextBlock ValueBlock()
        => new()
        {
            Foreground = AiLimitsPalette.Text,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };

    private static TextBlock NameBlock(string text)
        => new()
        {
            Text = text,
            Foreground = AiLimitsPalette.Text,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
}
