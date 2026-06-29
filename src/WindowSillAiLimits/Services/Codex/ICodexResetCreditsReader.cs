using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.Codex;

public interface ICodexResetCreditsReader
{
    Task<IReadOnlyList<CodexResetCredit>> ReadAsync(CancellationToken cancellationToken);
}

