namespace WindowSillAiLimits.Models;

public sealed record ProviderUsage(
    UsageProvider Provider,
    string DisplayName,
    string? PlanLabel,
    ProviderStatus Status,
    IReadOnlyList<UsageWindow> Windows,
    DateTimeOffset? LastUpdated,
    string? Message,
    ApiCostEstimate? ApiCostEstimate = null,
    IReadOnlyList<CodexResetCredit>? ResetCredits = null);
