namespace WindowSillAiLimits.Services.Codex;

public interface ICodexAppServerClient
{
    Task<string> SendRequestAsync(string method, object? parameters, CancellationToken cancellationToken);
}
