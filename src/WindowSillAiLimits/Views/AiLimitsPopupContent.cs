using System.ComponentModel;
using System.Globalization;

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using WindowSill.API;

using WindowSillAiLimits.Models;
using WindowSillAiLimits.Settings;
using WindowSillAiLimits.ViewModels;

namespace WindowSillAiLimits.Views;

public sealed partial class AiLimitsPopupContent : SillPopupContent
{
    private const double CompactFontSize = 12;
    private const double CompactSmallFontSize = 11;
    private const double HeaderFontSize = 15;
    private const double PopupTitleFontSize = 16;
    private const double CollapsedPopupMaxHeight = 640;
    private const double ExpandedPopupMaxHeight = 900;
    private const double CostTableModelColumnWidth = 210;
    private const double CostTableTokensColumnWidth = 78;
    private const double CostTableCostColumnWidth = 82;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly AiLimitsViewModel _viewModel;
    private readonly ISettingsProvider? _settingsProvider;

    public AiLimitsPopupContent(AiLimitsViewModel viewModel, ISettingsProvider? settingsProvider = null)
    {
        _viewModel = viewModel;
        _settingsProvider = settingsProvider;
        InitializeComponent();

        RootBorder.Background = AiLimitsPalette.Surface;
        RootBorder.BorderBrush = AiLimitsPalette.Border;
        TitleText.Foreground = AiLimitsPalette.Text;
        UpdatedText.Foreground = AiLimitsPalette.MutedText;
        ApplyCompactText(TitleText, PopupTitleFontSize);
        ApplyCompactText(UpdatedText);
        RefreshIconButton.Command = _viewModel.RefreshCommand;
        ToolTipService.SetToolTip(RefreshIconButton, "Atualizar uso");
        AutomationProperties.SetName(RefreshIconButton, "Atualizar uso");
        BuildFooter(FooterGrid);

        _viewModel.PropertyChanged += OnViewModelChanged;
        SizeChanged += OnSizeChanged;
        RefreshContent();
    }

    private void BuildFooter(Grid footer)
    {
        footer.ColumnSpacing = 8;

        footer.ColumnDefinitions.Clear();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var source = new TextBlock
        {
            Text = AiLimitsDisplayText.SourceNote,
            Foreground = AiLimitsPalette.MutedText,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ApplyCompactText(source);
        footer.Children.Add(source);

        var refresh = IconButton("\uE72C", "Atualizar uso");
        refresh.Command = _viewModel.RefreshCommand;
        Grid.SetColumn(refresh, 1);
        footer.Children.Add(refresh);

        var settings = IconButton("\uE713", "Abrir configura\u00E7\u00F5es do AI Limits");
        settings.IsEnabled = _settingsProvider is not null;
        settings.Click += (_, _) => _settingsProvider?.OpenSettingsPageForSill("AI Limits", "AI Limits");
        Grid.SetColumn(settings, 2);
        footer.Children.Add(settings);
    }

    private static Button IconButton(string glyph, string automationName)
    {
        var button = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = 14 },
            Padding = new Thickness(8, 5, 8, 5),
        };

        ToolTipService.SetToolTip(button, automationName);
        AutomationProperties.SetName(button, automationName);
        return button;
    }

    private void RefreshContent()
    {
        ApplyPopupHeight();
        UpdatedText.Text = _viewModel.LastUpdatedText;
        ProviderGrid.Children.Clear();
        ProviderGrid.ColumnDefinitions.Clear();
        ProviderGrid.RowDefinitions.Clear();
        ProviderGrid.ColumnSpacing = 10;
        ProviderGrid.RowSpacing = 10;
        ApiCostGrid.Children.Clear();
        ApiCostGrid.ColumnDefinitions.Clear();
        ApiCostGrid.RowDefinitions.Clear();

        var providers = _viewModel.VisibleProviders;
        if (providers.Count == 0)
        {
            ProviderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ProviderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var empty = new TextBlock
            {
                Text = AiLimitsDisplayText.NoProvidersDetected,
                Foreground = AiLimitsPalette.MutedText,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            };
            ApplyCompactText(empty, wrap: true);
            ProviderGrid.Children.Add(empty);
            ApiCostGrid.Children.Add(BuildApiCostPanel());
            return;
        }

        var width = ActualWidth > 0 ? ActualWidth : Width;
        var columns = AiLimitsPopupLayout.GetProviderColumnCount(width, providers.Count);
        for (var index = 0; index < columns; index++)
        {
            ProviderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var index = 0; index < providers.Count; index++)
        {
            if (index % columns == 0)
            {
                ProviderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            var section = BuildProviderSection(providers[index]);
            Grid.SetColumn(section, index % columns);
            Grid.SetRow(section, index / columns);
            ProviderGrid.Children.Add(section);
        }

        ApiCostGrid.Children.Add(BuildApiCostPanel());
    }

    private Border BuildProviderSection(ProviderUsage provider)
    {
        var accent = provider.Provider == UsageProvider.Codex ? AiLimitsPalette.Codex : AiLimitsPalette.Claude;

        var section = new Border
        {
            Padding = new Thickness(10),
            Background = AiLimitsPalette.SubtleSurface,
            BorderBrush = AiLimitsPalette.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            // Mantem as duas colunas de provider com a mesma altura, mesmo quando uma tem
            // janelas extras (ex.: "Extra" do Claude), para um layout equilibrado.
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var stack = new StackPanel { Spacing = 6 };

        var title = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        var headerText = new StackPanel { Spacing = 1 };
        var providerTitle = new TextBlock
        {
            Text = ProviderDisplayTitle(provider),
            Foreground = AiLimitsPalette.Text,
            FontWeight = FontWeights.SemiBold,
        };
        ApplyCompactText(providerTitle, HeaderFontSize);
        headerText.Children.Add(providerTitle);

        var subtitle = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(provider.PlanLabel) ? ProviderSubtitle(provider) : provider.PlanLabel,
            Foreground = AiLimitsPalette.MutedText,
        };
        ApplyCompactText(subtitle);
        headerText.Children.Add(subtitle);
        title.Children.Add(headerText);

        // Selo de status: nao usar LineHeight/BlockLineHeight (eles sobrescrevem TextLineBounds e
        // empurram o texto pra cima). Tight + alinhamento central centraliza nos dois eixos.
        var statusText = new TextBlock
        {
            Text = StatusLabel(provider.Status),
            Foreground = AiLimitsPalette.ForStatus(provider.Status, accent),
            FontWeight = FontWeights.SemiBold,
            FontSize = CompactFontSize,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.None,
            TextLineBounds = TextLineBounds.Tight,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };
        var status = new Border
        {
            Padding = new Thickness(12, 4, 12, 4),
            BorderBrush = AiLimitsPalette.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Background = AiLimitsPalette.Surface,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = statusText,
        };
        Grid.SetColumn(status, 1);
        title.Children.Add(status);
        stack.Children.Add(title);

        foreach (var window in provider.Windows)
        {
            stack.Children.Add(BuildWindowRow(window, accent));
        }

        var pacing = _viewModel.GetWeeklyPacing(provider.Provider);
        if (pacing is not null)
        {
            var fiveHour = provider.Windows.FirstOrDefault(window => string.Equals(window.Id, "5h", StringComparison.OrdinalIgnoreCase));
            stack.Children.Add(BuildPacingBlock(provider.Provider, pacing, fiveHour, provider.LastUpdated));
        }

        stack.Children.Add(BuildSourceNote(provider));

        section.Child = stack;
        return section;
    }

    private static Grid BuildWindowRow(UsageWindow window, Microsoft.UI.Xaml.Media.SolidColorBrush accent)
    {
        var severity = window.UsedPercent is null
            ? LimitSeverity.Unavailable
            : window.UsedPercent >= 90
                ? LimitSeverity.Danger
                : window.UsedPercent >= 75
                    ? LimitSeverity.Warning
                    : LimitSeverity.Normal;

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(46) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(42) },
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            },
            ColumnSpacing = 6,
            MinHeight = 34,
        };

        var label = new TextBlock
        {
            Text = window.Label,
            Foreground = AiLimitsPalette.MutedText,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ApplyCompactText(label);
        row.Children.Add(label);

        var progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = window.UsedPercent ?? 0,
            Height = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = AiLimitsPalette.ForSeverity(severity, accent),
        };
        Grid.SetColumn(progress, 1);
        row.Children.Add(progress);

        var percent = new TextBlock
        {
            Text = window.UsedPercent is null ? "--" : $"{Math.Round(window.UsedPercent.Value):0}%",
            Foreground = AiLimitsPalette.ForSeverity(severity, accent),
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ApplyCompactText(percent);
        Grid.SetColumn(percent, 2);
        row.Children.Add(percent);

        var reset = new TextBlock
        {
            Text = window.ResetsAt is null ? "" : FormatReset(window.ResetsAt.Value),
            Foreground = AiLimitsPalette.MutedText,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ApplyCompactText(reset, CompactSmallFontSize);
        Grid.SetRow(reset, 1);
        Grid.SetColumn(reset, 1);
        Grid.SetColumnSpan(reset, 2);
        row.Children.Add(reset);

        return row;
    }

    private static Border BuildPacingBlock(
        UsageProvider provider,
        UsagePacing pacing,
        UsageWindow? fiveHour,
        DateTimeOffset? queriedAt)
    {
        var stack = new StackPanel
        {
            Spacing = 2,
        };

        var title = new TextBlock
        {
            // O corpo deste bloco e sempre o pacing da janela semanal (7d) para os dois provedores.
            Text = provider == UsageProvider.Codex ? "Codex 7d" : "Claude 7d",
            Foreground = AiLimitsPalette.Text,
            FontWeight = FontWeights.SemiBold,
        };
        ApplyCompactText(title, HeaderFontSize);
        stack.Children.Add(title);
        stack.Children.Add(Detail(AiLimitsDisplayText.Used, $"{pacing.UsedPercent:0.#}%"));
        stack.Children.Add(Detail(AiLimitsDisplayText.ExpectedSoFar, $"{pacing.ExpectedPercent:0.#}% ({100 / 7:0.##}%/dia)"));
        stack.Children.Add(Detail(AiLimitsDisplayText.Difference, $"{pacing.DifferencePercentagePoints:+0.#;-0.#;0} p.p.", pacing.DifferencePercentagePoints <= 0 ? AiLimitsPalette.Codex : AiLimitsPalette.Warning));
        stack.Children.Add(Detail(AiLimitsDisplayText.CurrentAveragePace, $"{pacing.AverageDailyPacePercent:0.#}%/dia"));
        stack.Children.Add(Detail(AiLimitsDisplayText.ProjectedExhaustion, FormatProjectedExhaustion(pacing)));
        stack.Children.Add(Detail(AiLimitsDisplayText.ForecastImpact, FormatForecastImpact(pacing)));
        stack.Children.Add(Detail(AiLimitsDisplayText.WeeklyWindowElapsed, $"{pacing.ElapsedDays:0.#} de 7 dias"));

        if (pacing.ResetsAt is not null)
        {
            // Converte para o fuso local, igual as linhas de janela acima (FormatReset).
            stack.Children.Add(Detail(AiLimitsDisplayText.NextWeeklyReset, $"{pacing.ResetsAt.Value.ToLocalTime():dd/MM/yyyy HH:mm}"));
        }

        if (fiveHour is not null)
        {
            var reset = fiveHour.ResetsAt is null ? "reset indisponível" : $"reset {fiveHour.ResetsAt.Value.ToLocalTime():HH:mm}";
            var used = fiveHour.UsedPercent is null ? "--" : $"{fiveHour.UsedPercent:0.#}%";
            stack.Children.Add(Detail(AiLimitsDisplayText.FiveHourWindow, $"{used} usado; {reset}"));
        }

        if (queriedAt is not null)
        {
            stack.Children.Add(Detail(AiLimitsDisplayText.QueriedAt, $"{queriedAt.Value.ToLocalTime():dd/MM/yyyy HH:mm}"));
        }

        return new Border
        {
            Padding = new Thickness(8, 7, 8, 7),
            Background = AiLimitsPalette.Surface,
            BorderBrush = AiLimitsPalette.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = stack,
        };
    }

    private static Grid Detail(string label, string value, Brush? valueBrush = null)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        var labelText = new TextBlock
        {
            Text = label,
            Foreground = AiLimitsPalette.MutedText,
        };
        ApplyCompactText(labelText);
        row.Children.Add(labelText);

        var valueText = new TextBlock
        {
            Text = value,
            Foreground = valueBrush ?? AiLimitsPalette.Text,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Right,
            MaxWidth = 190,
        };
        ApplyCompactText(valueText);
        Grid.SetColumn(valueText, 1);
        row.Children.Add(valueText);

        return row;
    }

    private Border BuildApiCostPanel()
    {
        var stack = new StackPanel
        {
            Spacing = 8,
        };

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing = 8,
        };

        var toggle = IconButton(_viewModel.IsApiCostsExpanded ? "\uE70D" : "\uE70E", "Expandir ou recolher custos API");
        toggle.Command = _viewModel.ToggleApiCostsCommand;
        header.Children.Add(toggle);

        var titleStack = new StackPanel { Spacing = 1 };
        var title = new TextBlock
        {
            Text = "Custos API",
            Foreground = AiLimitsPalette.Text,
            FontWeight = FontWeights.SemiBold,
        };
        ApplyCompactText(title, HeaderFontSize);
        titleStack.Children.Add(title);

        var subtitle = new TextBlock
        {
            Text = $"janela 7d ativa · {_viewModel.ApiCostLastUpdatedText} · estimativa local, não fatura",
            Foreground = AiLimitsPalette.MutedText,
        };
        ApplyCompactText(subtitle, CompactSmallFontSize);
        titleStack.Children.Add(subtitle);
        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);

        var total = new TextBlock
        {
            Text = $"{_viewModel.ApiCostTotalText}\n{_viewModel.ApiCostTotalTokensText}",
            Foreground = AiLimitsPalette.Text,
            FontWeight = FontWeights.SemiBold,
            TextAlignment = TextAlignment.Right,
        };
        ApplyCompactText(total, HeaderFontSize);
        Grid.SetColumn(total, 2);
        header.Children.Add(total);

        var refresh = IconButton("\uE72C", "Atualizar custos API");
        refresh.Command = _viewModel.CostRefreshCommand;
        Grid.SetColumn(refresh, 3);
        header.Children.Add(refresh);
        stack.Children.Add(header);

        if (_viewModel.IsApiCostsExpanded)
        {
            var providerGrid = BuildApiCostProviderGrid();
            stack.Children.Add(providerGrid);
        }

        return new Border
        {
            Padding = new Thickness(8, 7, 8, 7),
            Background = AiLimitsPalette.Surface,
            BorderBrush = AiLimitsPalette.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = stack,
        };
    }

    private Grid BuildApiCostProviderGrid()
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8,
        };

        var providers = OrderApiCostProviders(_viewModel.VisibleProviders);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        if (providers.Count == 0)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Children.Add(CompactWrappedText("Nenhum provider detectado para estimar custos."));
            return grid;
        }

        for (var index = 0; index < providers.Count; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var provider = providers[index];
            var accent = provider.Provider == UsageProvider.Codex ? AiLimitsPalette.Codex : AiLimitsPalette.Claude;
            var block = provider.ApiCostEstimate is null
                ? BuildUnavailableApiCostProvider(provider)
                : BuildApiCostProviderBlock(provider, provider.ApiCostEstimate, accent);
            Grid.SetColumn(block, 0);
            Grid.SetRow(block, index);
            grid.Children.Add(block);
        }

        return grid;
    }

    private void ApplyPopupHeight()
        => MaxHeight = _viewModel.IsApiCostsExpanded ? ExpandedPopupMaxHeight : CollapsedPopupMaxHeight;

    private static IReadOnlyList<ProviderUsage> OrderApiCostProviders(IReadOnlyList<ProviderUsage> providers)
        => providers
            .OrderBy(provider => provider.Provider switch
            {
                UsageProvider.Codex => 0,
                UsageProvider.Claude => 1,
                _ => 2,
            })
            .ToArray();

    private static Border BuildUnavailableApiCostProvider(ProviderUsage provider)
    {
        var stack = new StackPanel { Spacing = 4 };
        var title = new TextBlock
        {
            Text = ProviderDisplayTitle(provider),
            Foreground = AiLimitsPalette.Text,
            FontWeight = FontWeights.SemiBold,
        };
        ApplyCompactText(title, HeaderFontSize);
        stack.Children.Add(title);
        stack.Children.Add(CompactWrappedText("Custos indisponíveis para este provider."));

        return new Border
        {
            Padding = new Thickness(8, 7, 8, 7),
            Background = AiLimitsPalette.SubtleSurface,
            BorderBrush = AiLimitsPalette.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = stack,
        };
    }

    private static Border BuildApiCostProviderBlock(ProviderUsage provider, ApiCostEstimate estimate, Microsoft.UI.Xaml.Media.SolidColorBrush accent)
    {
        var stack = new StackPanel
        {
            Spacing = 6,
        };

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        var titleStack = new StackPanel { Spacing = 1 };
        var title = new TextBlock
        {
            Text = $"{ProviderDisplayTitle(provider)} · {FormatUsd(estimate.TotalCostUsd)} · {FormatCompactTokens(estimate.TotalTokens.TotalTokens)} tokens",
            Foreground = AiLimitsPalette.Text,
            FontWeight = FontWeights.SemiBold,
        };
        ApplyCompactText(title, HeaderFontSize);
        titleStack.Children.Add(title);

        var subtitle = new TextBlock
        {
            Text = $"custo por modelo usado na janela {estimate.WindowLabel}",
            Foreground = AiLimitsPalette.MutedText,
        };
        ApplyCompactText(subtitle, CompactSmallFontSize);
        titleStack.Children.Add(subtitle);
        header.Children.Add(titleStack);

        var total = new TextBlock
        {
            Text = FormatCompactTokens(estimate.TotalTokens.TotalTokens),
            Foreground = AiLimitsPalette.Text,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Right,
        };
        ApplyCompactText(total, HeaderFontSize);
        Grid.SetColumn(total, 1);
        header.Children.Add(total);
        stack.Children.Add(header);

        var table = new StackPanel { Spacing = 0 };
        table.Children.Add(ModelCostRow("Modelo", "Tokens", "Valor/token", "Custo", isHeader: true));
        foreach (var line in estimate.Lines.Take(4))
        {
            table.Children.Add(ModelCostRow(
                line.DisplayName,
                FormatCompactTokens(line.Tokens.TotalTokens),
                line.PriceSummary,
                line.CostUsd is null ? "sem preço" : FormatUsd(line.CostUsd.Value),
                isHeader: false,
                valueBrush: line.CostUsd is null ? AiLimitsPalette.MutedText : AiLimitsPalette.Text));
        }

        if (estimate.Lines.Count > 4)
        {
            table.Children.Add(ModelCostRow(
                $"mais {estimate.Lines.Count - 4} modelo(s)",
                FormatCompactTokens(estimate.Lines.Skip(4).Sum(line => line.Tokens.TotalTokens)),
                "",
                "",
                isHeader: false,
                valueBrush: AiLimitsPalette.MutedText));
        }

        table.Children.Add(ModelCostRow(
            "Total precificado",
            FormatCompactTokens(estimate.TotalTokens.TotalTokens),
            estimate.WindowLabel,
            FormatUsd(estimate.TotalCostUsd),
            isHeader: false,
            valueBrush: accent,
            isTotal: true));
        stack.Children.Add(table);

        var breakdown = new TextBlock
        {
            Text = FormatTokenBreakdown(estimate.TotalTokens),
            Foreground = AiLimitsPalette.MutedText,
        };
        ApplyCompactText(breakdown, CompactSmallFontSize, wrap: true);
        stack.Children.Add(breakdown);

        return new Border
        {
            Padding = new Thickness(8, 7, 8, 7),
            Background = AiLimitsPalette.Surface,
            BorderBrush = accent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = stack,
        };
    }

    private static Grid ModelCostRow(
        string model,
        string tokens,
        string price,
        string cost,
        bool isHeader,
        Brush? valueBrush = null,
        bool isTotal = false)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(CostTableModelColumnWidth) },
                new ColumnDefinition { Width = new GridLength(CostTableTokensColumnWidth) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(CostTableCostColumnWidth) },
            },
            ColumnSpacing = 8,
            MinHeight = 18,
        };

        var brush = isHeader ? AiLimitsPalette.MutedText : valueBrush ?? AiLimitsPalette.Text;
        row.Children.Add(Cell(model, brush, isHeader || isTotal, TextAlignment.Left));

        var tokenCell = Cell(tokens, brush, isHeader || isTotal, TextAlignment.Right);
        Grid.SetColumn(tokenCell, 1);
        row.Children.Add(tokenCell);

        var priceCell = Cell(price, AiLimitsPalette.MutedText, isHeader, TextAlignment.Left, isPrice: true);
        Grid.SetColumn(priceCell, 2);
        row.Children.Add(priceCell);

        var costCell = Cell(cost, brush, isHeader || isTotal, TextAlignment.Right);
        Grid.SetColumn(costCell, 3);
        row.Children.Add(costCell);

        return row;
    }

    private static TextBlock Cell(string text, Brush foreground, bool bold, TextAlignment alignment, bool isPrice = false)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
            TextAlignment = alignment,
            TextWrapping = isPrice ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = isPrice ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            MaxLines = isPrice ? 2 : 1,
        };
        ApplyCompactText(block, CompactSmallFontSize, wrap: isPrice);
        return block;
    }

    private static string FormatUsd(decimal value)
        => value <= 0 ? "$0.00" : value.ToString("$0.00", CultureInfo.InvariantCulture);

    private static string FormatCompactTokens(long tokens)
    {
        if (tokens >= 1_000_000)
        {
            return (tokens / 1_000_000d).ToString("0.#M", PtBr);
        }

        if (tokens >= 1_000)
        {
            return (tokens / 1_000d).ToString("0.#K", PtBr);
        }

        return tokens.ToString("0", PtBr);
    }

    private static string FormatTokenBreakdown(TokenUsageTotals totals)
    {
        var parts = new List<string>();
        if (totals.InputTokens > 0)
        {
            parts.Add($"{FormatCompactTokens(totals.InputTokens)} input");
        }

        if (totals.CachedInputTokens > 0)
        {
            parts.Add($"{FormatCompactTokens(totals.CachedInputTokens)} cache");
        }

        var cacheWrites = totals.CacheWriteFiveMinuteTokens + totals.CacheWriteOneHourTokens;
        if (cacheWrites > 0)
        {
            parts.Add($"{FormatCompactTokens(cacheWrites)} cache write");
        }

        if (totals.OutputTokens > 0)
        {
            parts.Add($"{FormatCompactTokens(totals.OutputTokens)} output");
        }

        return parts.Count == 0
            ? "sem tokens precificados"
            : string.Join(" · ", parts) + " · estimativa local, não fatura";
    }

    private static string FormatProjectedExhaustion(UsagePacing pacing)
        => pacing.ProjectedExhaustionStatus switch
        {
            ProjectedExhaustionStatus.BeforeReset => pacing.ProjectedExhaustionAt!.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", PtBr),
            ProjectedExhaustionStatus.AfterReset => "não esgota antes do reset",
            _ => "sem previsão",
        };

    private static string FormatForecastImpact(UsagePacing pacing)
    {
        if (pacing.ProjectedExhaustionStatus == ProjectedExhaustionStatus.AfterReset)
        {
            return "reset chega primeiro";
        }

        if (pacing.ProjectedExhaustionStatus != ProjectedExhaustionStatus.BeforeReset ||
            pacing.ProjectedExhaustionAt is null ||
            pacing.ResetsAt is null)
        {
            return "ritmo insuficiente";
        }

        var leadTime = pacing.ResetsAt.Value - pacing.ProjectedExhaustionAt.Value;
        return $"{FormatDuration(leadTime)} antes do reset";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays}d {duration.Hours}h";
        }

        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h{duration.Minutes:00}";
        }

        return $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))}min";
    }

    private static Border BuildSourceNote(ProviderUsage provider)
    {
        var text = !string.IsNullOrWhiteSpace(provider.Message)
            ? provider.Message
            : provider.Provider == UsageProvider.Codex
                ? "Usando o Codex app-server local. Nenhum token é armazenado."
                : "5h e 7d são lidos do estado OAuth local do Claude Code.";

        return new Border
        {
            Padding = new Thickness(8, 7, 8, 7),
            Background = AiLimitsPalette.Surface,
            BorderBrush = AiLimitsPalette.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = CompactWrappedText(text),
        };
    }

    private static TextBlock CompactWrappedText(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = AiLimitsPalette.MutedText,
        };
        ApplyCompactText(block, CompactFontSize, wrap: true);
        return block;
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

    private static string ProviderDisplayTitle(ProviderUsage provider)
        => provider.Provider == UsageProvider.Codex ? "Codex" : "Claude Code";

    private static string ProviderSubtitle(ProviderUsage provider)
        => provider.Provider == UsageProvider.Codex ? "Conta ChatGPT" : "Conta de assinatura";

    private static string StatusLabel(ProviderStatus status)
        => status switch
        {
            ProviderStatus.Ok => "OK",
            ProviderStatus.Warning => "Atenção",
            ProviderStatus.Unavailable => "Indisponível",
            ProviderStatus.Stale => "Desatualizado",
            ProviderStatus.Error => "Erro",
            ProviderStatus.NotInstalled => "Não instalado",
            _ => status.ToString(),
        };

    private static string FormatReset(DateTimeOffset reset)
    {
        var local = reset.ToLocalTime();
        var today = DateTimeOffset.Now.Date;
        return local.Date == today
            ? $"reset hoje {local:HH:mm}"
            : "reset " + local.ToString("ddd HH:mm", PtBr);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
        => RefreshContent();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        => RefreshContent();
}
