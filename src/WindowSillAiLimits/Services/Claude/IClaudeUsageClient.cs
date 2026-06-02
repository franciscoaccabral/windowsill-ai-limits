namespace WindowSillAiLimits.Services.Claude;

public interface IClaudeUsageClient
{
    Task<ClaudeUsagePayload> ReadUsageAsync(CancellationToken cancellationToken);
}
