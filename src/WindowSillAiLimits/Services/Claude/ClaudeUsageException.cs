namespace WindowSillAiLimits.Services.Claude;

public sealed class ClaudeUsageException(string message, Exception? innerException = null, bool notConfigured = false)
    : Exception(message, innerException)
{
    /// <summary>True quando o Claude nao esta configurado localmente (arquivo de credenciais ausente).</summary>
    public bool NotConfigured { get; } = notConfigured;
}
