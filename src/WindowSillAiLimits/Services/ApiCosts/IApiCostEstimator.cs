using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.ApiCosts;

public interface IApiCostEstimator
{
    ApiCostEstimate? Estimate(ProviderUsage provider);
}
