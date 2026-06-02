namespace WindowSillAiLimits.Models;

public enum ProviderStatus
{
    Ok,
    Warning,
    Unavailable,
    Stale,
    Error,

    /// <summary>O ferramental local do provedor nao esta presente (CLI/credenciais ausentes).</summary>
    NotInstalled,
}
