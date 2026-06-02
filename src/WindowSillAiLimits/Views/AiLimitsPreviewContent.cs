using System.ComponentModel;
using System.Globalization;

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

using WindowSillAiLimits.Models;
using WindowSillAiLimits.ViewModels;

namespace WindowSillAiLimits.Views;

public sealed class AiLimitsPreviewContent : Grid
{
    private const double CompactFontSize = 12;

    private readonly AiLimitsViewModel _viewModel;
    private readonly string? _pluginContentDirectory;
    private readonly StackPanel _providerStack = new();
    private readonly TextBlock _updated = new();

    public AiLimitsPreviewContent(AiLimitsViewModel viewModel, string? pluginContentDirectory = null)
    {
        _viewModel = viewModel;
        _pluginContentDirectory = pluginContentDirectory;
        var stack = new StackPanel
        {
            Spacing = 6,
            MinWidth = 360,
        };

        var title = new TextBlock
        {
            Text = "AI Limits",
            Foreground = AiLimitsPalette.Text,
            FontWeight = FontWeights.SemiBold,
        };
        ApplyCompactText(title, 13);
        stack.Children.Add(title);
        stack.Children.Add(_providerStack);
        stack.Children.Add(_updated);

        Children.Add(new Border
        {
            Padding = new Thickness(12, 10, 12, 10),
            Background = AiLimitsPalette.Surface,
            BorderBrush = AiLimitsPalette.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = stack,
        });

        _viewModel.PropertyChanged += OnViewModelChanged;
        RefreshText();
    }

    private void RefreshText()
    {
        _providerStack.Children.Clear();
        _providerStack.Spacing = 5;

        var providers = _viewModel.VisibleProviders;
        if (providers.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = AiLimitsDisplayText.NoProvidersDetected,
                Foreground = AiLimitsPalette.MutedText,
            };
            ApplyCompactText(empty, wrap: true);
            _providerStack.Children.Add(empty);
        }
        else
        {
            foreach (var provider in providers)
            {
                _providerStack.Children.Add(BuildProviderRow(provider));

                var pacing = _viewModel.GetWeeklyPacing(provider.Provider);
                if (pacing is not null)
                {
                    _providerStack.Children.Add(BuildPacingRow(provider.Provider, pacing));
                }
            }
        }

        _updated.Text = _viewModel.LastUpdatedText;
        _updated.Foreground = AiLimitsPalette.MutedText;
        ApplyCompactText(_updated);
    }

    private Grid BuildProviderRow(ProviderUsage provider)
    {
        var accent = provider.Provider == UsageProvider.Codex ? AiLimitsPalette.Codex : AiLimitsPalette.Claude;
        var row = new Grid
        {
            ColumnSpacing = 5,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        var icon = BuildProviderIcon(provider.Provider);
        Grid.SetColumn(icon, 0);
        row.Children.Add(icon);

        var name = new TextBlock
        {
            Text = provider.Provider == UsageProvider.Codex ? "OpenAI" : "Anthropic",
            Foreground = AiLimitsPalette.Text,
            FontWeight = FontWeights.SemiBold,
        };
        ApplyCompactText(name);
        Grid.SetColumn(name, 1);
        row.Children.Add(name);

        var fiveHour = new TextBlock
        {
            Text = $"5h {FormatWindow(provider, "5h")}",
            Foreground = accent,
            FontWeight = FontWeights.SemiBold,
        };
        ApplyCompactText(fiveHour);
        Grid.SetColumn(fiveHour, 2);
        row.Children.Add(fiveHour);

        var sevenDay = new TextBlock
        {
            Text = $"7d {FormatWindow(provider, "7d")}",
            Foreground = accent,
            FontWeight = FontWeights.SemiBold,
        };
        ApplyCompactText(sevenDay);
        Grid.SetColumn(sevenDay, 3);
        row.Children.Add(sevenDay);

        return row;
    }

    private StackPanel BuildPacingRow(UsageProvider provider, UsagePacing pacing)
    {
        var differenceIsAbove = pacing.DifferencePercentagePoints > 0;
        var differenceBrush = differenceIsAbove ? AiLimitsPalette.Warning : AiLimitsPalette.Codex;
        var expectedRatio = pacing.ExpectedPercent <= 0
            ? "--"
            : $"{pacing.UsedPercent / pacing.ExpectedPercent * 100:0}%";
        var direction = differenceIsAbove ? "acima" : "abaixo";
        var providerLabel = provider == UsageProvider.Codex ? "Codex 7d" : "Claude 7d";

        // Empilha em linhas que quebram para nao truncar o pacing no flyout estreito.
        var stack = new StackPanel { Spacing = 1 };

        var label = new TextBlock
        {
            Text = $"{providerLabel} {AiLimitsDisplayText.ExpectedSoFar}",
            Foreground = AiLimitsPalette.MutedText,
        };
        ApplyCompactText(label, wrap: true);
        stack.Children.Add(label);

        var expected = new TextBlock
        {
            Text = $"{pacing.UsedPercent:0.#}% de {pacing.ExpectedPercent:0.#}% ({expectedRatio} do previsto)",
            Foreground = AiLimitsPalette.Text,
            FontWeight = FontWeights.SemiBold,
        };
        ApplyCompactText(expected, wrap: true);
        stack.Children.Add(expected);

        var difference = new TextBlock
        {
            Text = $"{Math.Abs(pacing.DifferencePercentagePoints):0.#} p.p. {direction}",
            Foreground = differenceBrush,
            FontWeight = FontWeights.SemiBold,
        };
        ApplyCompactText(difference, wrap: true);
        stack.Children.Add(difference);

        return stack;
    }

    private FrameworkElement BuildProviderIcon(UsageProvider provider)
    {
        var (automationName, assetPath) = provider == UsageProvider.Codex
            ? ("OpenAI", GetAssetUri("openai-mark.svg"))
            : ("Anthropic", GetAssetUri("anthropic-mark.svg"));

        var image = new Image
        {
            Width = 13,
            Height = 13,
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

    private static string FormatWindow(ProviderUsage provider, string windowId)
    {
        var window = provider.Windows.FirstOrDefault(candidate => string.Equals(candidate.Id, windowId, StringComparison.OrdinalIgnoreCase));
        return window?.UsedPercent is null
            ? "--"
            : $"{Math.Round(window.UsedPercent.Value).ToString("0", CultureInfo.InvariantCulture)}%";
    }

    private static void ApplyCompactText(TextBlock block, double fontSize = CompactFontSize, bool wrap = false)
    {
        block.FontSize = fontSize;
        block.LineHeight = fontSize + 2;
        block.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        if (wrap)
        {
            block.TextWrapping = TextWrapping.Wrap;
            block.TextTrimming = TextTrimming.None;
            return;
        }

        block.TextWrapping = TextWrapping.NoWrap;
        block.TextTrimming = TextTrimming.CharacterEllipsis;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
        => RefreshText();
}
