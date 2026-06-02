namespace WindowSillAiLimits.ViewModels;

public static class AiLimitsDisplayText
{
    public static string Refresh => LocalizedText.Get("Action.Refresh");
    public static string Settings => LocalizedText.Get("Action.Settings");
    public static string SourceNote => LocalizedText.Get("Popup.SourceNote");
    public static string NoProvidersDetected => LocalizedText.Get("Popup.NoProvidersDetected");
    public static string Used => LocalizedText.Get("Pacing.Used");
    public static string ExpectedSoFar => LocalizedText.Get("Pacing.ExpectedSoFar");
    public static string Difference => LocalizedText.Get("Pacing.Difference");
    public static string CurrentAveragePace => LocalizedText.Get("Pacing.CurrentAveragePace");
    public static string ProjectedExhaustion => LocalizedText.Get("Pacing.ProjectedExhaustion");
    public static string ForecastImpact => LocalizedText.Get("Pacing.ForecastImpact");
    public static string WeeklyWindowElapsed => LocalizedText.Get("Pacing.WeeklyWindowElapsed");
    public static string NextWeeklyReset => LocalizedText.Get("Pacing.NextWeeklyReset");
    public static string FiveHourWindow => LocalizedText.Get("Pacing.FiveHourWindow");
    public static string QueriedAt => LocalizedText.Get("Pacing.QueriedAt");
}
