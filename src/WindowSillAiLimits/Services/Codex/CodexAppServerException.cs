namespace WindowSillAiLimits.Services.Codex;

public sealed class CodexAppServerException(string message, Exception? innerException = null, bool commandMissing = false)
    : Exception(message, innerException)
{
    /// <summary>True quando o comando Codex nao pode ser encontrado/iniciado (CLI nao instalada).</summary>
    public bool CommandMissing { get; } = commandMissing;
}
