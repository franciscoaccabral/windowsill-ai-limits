using System.Net.Http.Headers;
using System.Text.Json;

using WindowSillAiLimits.Models;

namespace WindowSillAiLimits.Services.Codex;

public sealed class CodexResetCreditsReader(TimeSpan timeout) : ICodexResetCreditsReader
{
    private static readonly Uri Endpoint = new("https://chatgpt.com/backend-api/wham/rate-limit-reset-credits");

    public async Task<IReadOnlyList<CodexResetCredit>> ReadAsync(CancellationToken cancellationToken)
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        var authPath = System.IO.Path.Combine(
            string.IsNullOrWhiteSpace(codexHome)
                ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex")
                : codexHome,
            "auth.json");
        using var auth = JsonDocument.Parse(await File.ReadAllTextAsync(authPath, cancellationToken));
        var tokenRoot = auth.RootElement.TryGetProperty("tokens", out var tokens) && tokens.ValueKind == JsonValueKind.Object
            ? tokens
            : auth.RootElement;
        var accessToken = GetString(tokenRoot, "access_token") ?? GetString(auth.RootElement, "access_token")
            ?? throw new InvalidDataException("Codex authentication token is unavailable.");
        var accountId = GetString(tokenRoot, "account_id") ?? GetString(auth.RootElement, "account_id")
            ?? GetString(auth.RootElement, "chatgpt_account_id")
            ?? throw new InvalidDataException("Codex account id is unavailable.");

        using var client = new HttpClient { Timeout = timeout };
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("ChatGPT-Account-ID", accountId);
        request.Headers.Add("OpenAI-Beta", "codex-1");
        request.Headers.Add("originator", "Codex Desktop");
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return CodexResetCreditsParser.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private static string? GetString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
