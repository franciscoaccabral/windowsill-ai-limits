namespace WindowSillAiLimits.Views;

public static class AiLimitsPopupLayout
{
    public const double TwoColumnMinimumWidth = 520;

    public static int GetProviderColumnCount(double availableWidth, int providerCount)
        => providerCount >= 2 && availableWidth >= TwoColumnMinimumWidth ? 2 : 1;
}
