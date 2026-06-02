using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Views;

internal static class AiLimitsPalette
{
    public static SolidColorBrush Surface { get; } = Brush(32, 34, 38);
    public static SolidColorBrush SubtleSurface { get; } = Brush(42, 45, 50);
    public static SolidColorBrush Text { get; } = Brush(246, 247, 248);
    public static SolidColorBrush MutedText { get; } = Brush(166, 172, 179);
    public static SolidColorBrush Codex { get; } = Brush(54, 214, 170);
    public static SolidColorBrush Claude { get; } = Brush(238, 177, 87);
    public static SolidColorBrush Warning { get; } = Brush(255, 202, 97);
    public static SolidColorBrush Danger { get; } = Brush(255, 107, 107);
    public static SolidColorBrush Stale { get; } = Brush(151, 159, 171);
    public static SolidColorBrush Border { get; } = Brush(68, 72, 80);

    public static SolidColorBrush ForSeverity(LimitSeverity severity, SolidColorBrush accent)
        => severity switch
        {
            LimitSeverity.Warning => Warning,
            LimitSeverity.Danger => Danger,
            LimitSeverity.Unavailable => Stale,
            _ => accent,
        };

    public static SolidColorBrush ForStatus(ProviderStatus status, SolidColorBrush accent)
        => status switch
        {
            ProviderStatus.Warning => Warning,
            ProviderStatus.Error => Danger,
            ProviderStatus.Unavailable => Stale,
            ProviderStatus.Stale => Stale,
            ProviderStatus.NotInstalled => Stale,
            _ => accent,
        };

    private static SolidColorBrush Brush(byte r, byte g, byte b)
        => new(Colors.Transparent with { A = 255, R = r, G = g, B = b });
}
