using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WindowSillAiLimits.Services.Claude;

public sealed class ClaudeOAuthUsageClient : IClaudeUsageClient
{
    private const string UsageEndpoint = "https://api.anthropic.com/api/oauth/usage";
    private const string TokenEndpoint = "https://platform.claude.com/v1/oauth/token";
    private const string UsageBetaHeader = "oauth-2025-04-20";
    private const string UnifiedRateLimitHeaderPrefix = "anthropic-ratelimit-unified-";
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(5);
    private static readonly string[] DefaultClaudeAiScopes =
    [
        "user:profile",
        "user:inference",
        "user:sessions:claude_code",
        "user:mcp_servers",
        "user:file_upload",
    ];

    public const string ClaudeCodeClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

    private readonly TimeSpan _requestTimeout;
    private readonly string _credentialsPath;
    private readonly HttpClient _httpClient;
    private readonly string _usageEndpoint;
    private readonly string _tokenEndpoint;
    private readonly TimeSpan _lockTimeout;
    private readonly Func<DateTimeOffset> _clock;

    public ClaudeOAuthUsageClient(
        TimeSpan requestTimeout,
        string? credentialsPath = null,
        HttpClient? httpClient = null,
        string? usageEndpoint = null,
        string? tokenEndpoint = null,
        TimeSpan? lockTimeout = null,
        Func<DateTimeOffset>? clock = null)
    {
        _requestTimeout = requestTimeout;
        _credentialsPath = string.IsNullOrWhiteSpace(credentialsPath) ? DefaultCredentialsPath() : credentialsPath;
        _httpClient = httpClient ?? new HttpClient();
        _usageEndpoint = string.IsNullOrWhiteSpace(usageEndpoint) ? UsageEndpoint : usageEndpoint;
        _tokenEndpoint = string.IsNullOrWhiteSpace(tokenEndpoint) ? TokenEndpoint : tokenEndpoint;
        _lockTimeout = lockTimeout ?? TimeSpan.FromSeconds(10);
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public async Task<ClaudeUsagePayload> ReadUsageAsync(CancellationToken cancellationToken)
    {
        var credentials = await GetFreshCredentialsAsync(cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _usageEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
            request.Headers.UserAgent.ParseAdd("WindowSillAiLimits/0.1");
            request.Headers.TryAddWithoutValidation("anthropic-beta", UsageBetaHeader);

            using var response = await _httpClient.SendAsync(request, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 429)
                {
                    // O endpoint normalmente nao envia Retry-After, mas honramos quando presente.
                    throw new ClaudeUsageException($"Claude usage endpoint rate limited (HTTP 429).{FormatRetryAfter(response)}");
                }

                throw new ClaudeUsageException($"Claude usage endpoint returned HTTP {(int)response.StatusCode}.");
            }

            return new ClaudeUsagePayload(body, credentials.PlanLabel, CaptureRateLimitHeaders(response));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Claude usage request timed out.");
        }
        catch (HttpRequestException ex)
        {
            throw new ClaudeUsageException("Claude usage endpoint could not be reached.", ex);
        }
    }

    private async Task<ClaudeCredentials> GetFreshCredentialsAsync(CancellationToken cancellationToken)
    {
        var credentials = ReadCredentials();
        if (!NeedsRefresh(credentials))
        {
            return credentials;
        }

        if (string.IsNullOrWhiteSpace(credentials.RefreshToken))
        {
            throw new ClaudeUsageException("Claude auth expired; run `claude auth login` to refresh local credentials.");
        }

        var lockPath = _credentialsPath + ".refresh.lock";
        using var refreshLock = AcquireCredentialLock(lockPath, cancellationToken);

        var lockedCredentials = ReadCredentials();
        if (!NeedsRefresh(lockedCredentials))
        {
            return lockedCredentials;
        }

        if (string.IsNullOrWhiteSpace(lockedCredentials.RefreshToken))
        {
            throw new ClaudeUsageException("Claude auth expired; run `claude auth login` to refresh local credentials.");
        }

        var refresh = await RefreshTokenAsync(lockedCredentials, cancellationToken);
        var updated = lockedCredentials with
        {
            AccessToken = refresh.AccessToken,
            RefreshToken = string.IsNullOrWhiteSpace(refresh.RefreshToken) ? lockedCredentials.RefreshToken : refresh.RefreshToken,
            ExpiresAt = _clock().AddSeconds(refresh.ExpiresIn),
        };
        WriteCredentials(updated);
        return updated;
    }

    private bool NeedsRefresh(ClaudeCredentials credentials)
        => credentials.ExpiresAt <= _clock().Add(RefreshBuffer);

    private FileStream AcquireCredentialLock(string lockPath, CancellationToken cancellationToken)
    {
        var directory = System.IO.Path.GetDirectoryName(lockPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var deadline = _clock() + _lockTimeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (_clock() < deadline)
            {
                Thread.Sleep(25);
            }
            catch (UnauthorizedAccessException) when (_clock() < deadline)
            {
                Thread.Sleep(25);
            }
            catch (IOException ex)
            {
                throw new ClaudeUsageException("Claude auth refresh lock timed out; run `claude auth login` if this persists.", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new ClaudeUsageException("Claude auth refresh lock could not be acquired.", ex);
            }
        }
    }

    private async Task<ClaudeRefreshResult> RefreshTokenAsync(ClaudeCredentials credentials, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint);
            request.Headers.UserAgent.ParseAdd("claude-cli/1.0");
            request.Headers.TryAddWithoutValidation("anthropic-beta", UsageBetaHeader);
            request.Content = JsonContent.Create(new
            {
                grant_type = "refresh_token",
                refresh_token = credentials.RefreshToken,
                client_id = ClaudeCodeClientId,
                scope = string.Join(' ', credentials.Scopes is { Length: > 0 } ? credentials.Scopes : DefaultClaudeAiScopes),
            });

            using var response = await _httpClient.SendAsync(request, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new ClaudeUsageException(
                    $"Claude auth refresh failed (HTTP {(int)response.StatusCode}): {ParseSafeRefreshError(body)} Run `claude auth login`.");
            }

            return ParseRefreshResult(body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Claude auth refresh timed out.");
        }
        catch (HttpRequestException ex)
        {
            throw new ClaudeUsageException("Claude auth refresh endpoint could not be reached.", ex);
        }
    }

    private static ClaudeRefreshResult ParseRefreshResult(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var accessToken = GetRequiredString(root, "access_token");
            var refreshToken = TryGetString(root, "refresh_token", out var rotatedRefreshToken)
                ? rotatedRefreshToken
                : null;
            var expiresIn = GetExpiresIn(root);

            return new ClaudeRefreshResult(accessToken, refreshToken, expiresIn);
        }
        catch (ClaudeUsageException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new ClaudeUsageException("Claude auth refresh response could not be parsed.", ex);
        }
    }

    private static double GetExpiresIn(JsonElement root)
    {
        var property = GetPropertyOrDefault(root, "expires_in");
        if (property.ValueKind == JsonValueKind.Number)
        {
            if (property.TryGetDouble(out var expiresIn) && expiresIn > 0)
            {
                return expiresIn;
            }
        }

        throw new ClaudeUsageException("Claude auth refresh response missing expires_in.");
    }

    private static string ParseSafeRefreshError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (TryGetString(root, "error_description", out var description))
            {
                return UsageMessageSanitizer.Sanitize(description ?? "Refresh failed.");
            }

            var error = GetPropertyOrDefault(root, "error");
            if (error.ValueKind == JsonValueKind.Object && TryGetString(error, "message", out var message))
            {
                return UsageMessageSanitizer.Sanitize(message ?? "Refresh failed.");
            }

            if (error.ValueKind == JsonValueKind.String)
            {
                return UsageMessageSanitizer.Sanitize(error.GetString() ?? "Refresh failed.");
            }
        }
        catch (JsonException)
        {
        }

        return "Refresh failed.";
    }

    private static string FormatRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var seconds = retryAfter?.Delta?.TotalSeconds
            ?? (retryAfter?.Date is { } date ? (date - DateTimeOffset.Now).TotalSeconds : null);

        return seconds is > 0 ? $" Retry after {Math.Ceiling(seconds.Value):0}s." : string.Empty;
    }

    private static IReadOnlyDictionary<string, string> CaptureRateLimitHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
        {
            if (header.Key.StartsWith(UnifiedRateLimitHeaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                headers[header.Key] = string.Join(",", header.Value);
            }
        }

        return headers;
    }

    private ClaudeCredentials ReadCredentials()
    {
        if (!File.Exists(_credentialsPath))
        {
            throw new ClaudeUsageException("Claude credentials not found; run `claude auth login`.", notConfigured: true);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(_credentialsPath));
            var oauth = GetPropertyOrDefault(document.RootElement, "claudeAiOauth");
            if (oauth.ValueKind != JsonValueKind.Object)
            {
                throw new ClaudeUsageException("Claude credentials file does not contain claudeAiOauth.");
            }

            var accessToken = GetRequiredString(oauth, "accessToken");
            var refreshToken = TryGetString(oauth, "refreshToken", out var refresh) ? refresh : null;
            var expiresAt = GetExpiresAt(oauth);
            var planLabel = BuildPlanLabel(
                TryGetString(oauth, "subscriptionType", out var subscription) ? subscription : null,
                TryGetString(oauth, "rateLimitTier", out var tier) ? tier : null);
            var scopes = GetScopes(oauth);

            return new ClaudeCredentials(accessToken, refreshToken, expiresAt, planLabel, subscription, tier, scopes);
        }
        catch (ClaudeUsageException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new ClaudeUsageException("Claude credentials file could not be parsed; run `claude auth login`.", ex);
        }
        catch (IOException ex)
        {
            throw new ClaudeUsageException("Claude credentials file could not be read.", ex);
        }
    }

    private void WriteCredentials(ClaudeCredentials credentials)
    {
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(_credentialsPath)) as JsonObject ?? [];
            var oauth = root["claudeAiOauth"] as JsonObject ?? [];
            oauth["accessToken"] = credentials.AccessToken;
            oauth["refreshToken"] = credentials.RefreshToken;
            oauth["expiresAt"] = credentials.ExpiresAt.ToUnixTimeMilliseconds();
            if (!string.IsNullOrWhiteSpace(credentials.SubscriptionType))
            {
                oauth["subscriptionType"] = credentials.SubscriptionType;
            }

            if (!string.IsNullOrWhiteSpace(credentials.RateLimitTier))
            {
                oauth["rateLimitTier"] = credentials.RateLimitTier;
            }

            if (credentials.Scopes is { Length: > 0 })
            {
                var scopes = new JsonArray();
                foreach (var scope in credentials.Scopes)
                {
                    scopes.Add(scope);
                }

                oauth["scopes"] = scopes;
            }

            root["claudeAiOauth"] = oauth;
            var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            AtomicWriteText(_credentialsPath, json);
        }
        catch (JsonException ex)
        {
            throw new ClaudeUsageException("Claude credentials file could not be updated.", ex);
        }
        catch (IOException ex)
        {
            throw new ClaudeUsageException("Claude credentials file could not be updated.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ClaudeUsageException("Claude credentials file could not be updated.", ex);
        }
    }

    private static void AtomicWriteText(string path, string content)
    {
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = System.IO.Path.Combine(directory ?? string.Empty, $".{System.IO.Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tempPath, content);
        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    private static string DefaultCredentialsPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
        }

        return System.IO.Path.Combine(home, ".claude", ".credentials.json");
    }

    private static JsonElement GetPropertyOrDefault(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property)
            ? property
            : default;

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (TryGetString(element, propertyName, out var value))
        {
            return value!;
        }

        throw new ClaudeUsageException($"Claude credentials missing {propertyName}; run `claude auth login`.");
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        var property = GetPropertyOrDefault(element, propertyName);
        if (property.ValueKind == JsonValueKind.String)
        {
            var text = property.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                value = text;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static DateTimeOffset GetExpiresAt(JsonElement oauth)
    {
        var property = GetPropertyOrDefault(oauth, "expiresAt");
        if (property.ValueKind == JsonValueKind.Number)
        {
            if (property.TryGetInt64(out var milliseconds))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            }

            if (property.TryGetDouble(out var asDouble))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds((long)asDouble);
            }
        }

        throw new ClaudeUsageException("Claude credentials missing expiresAt; run `claude auth login`.");
    }

    private static string[] GetScopes(JsonElement oauth)
    {
        var property = GetPropertyOrDefault(oauth, "scopes");
        if (property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var scopes = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(item.GetString()))
            {
                scopes.Add(item.GetString()!);
            }
        }

        return scopes.ToArray();
    }

    private static string? BuildPlanLabel(string? subscriptionType, string? rateLimitTier)
    {
        if (string.IsNullOrWhiteSpace(subscriptionType))
        {
            return null;
        }

        var label = "Claude " + char.ToUpperInvariant(subscriptionType[0]) + subscriptionType[1..];
        if (rateLimitTier?.Contains("5x", StringComparison.OrdinalIgnoreCase) == true)
        {
            label += " 5x";
        }
        else if (rateLimitTier?.Contains("20x", StringComparison.OrdinalIgnoreCase) == true)
        {
            label += " 20x";
        }

        return label;
    }

    private sealed record ClaudeCredentials(
        string AccessToken,
        string? RefreshToken,
        DateTimeOffset ExpiresAt,
        string? PlanLabel,
        string? SubscriptionType,
        string? RateLimitTier,
        string[] Scopes);

    private sealed record ClaudeRefreshResult(string AccessToken, string? RefreshToken, double ExpiresIn);
}
