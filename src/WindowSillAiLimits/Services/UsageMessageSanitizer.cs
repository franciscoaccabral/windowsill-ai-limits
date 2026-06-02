using System.Text.RegularExpressions;

namespace WindowSillAiLimits.Services;

public static class UsageMessageSanitizer
{
    private static readonly Regex BearerTokenPattern = new(
        @"(?i)\b(Bearer\s+)[A-Za-z0-9._~+/=-]+",
        RegexOptions.Compiled);

    private static readonly Regex CredentialPropertyPattern = new(
        @"(?i)([""']?\b(?:accessToken|refreshToken|authorization|api(?:[_-]|\s)?key)\b[""']?\s*[:=]\s*[""']?)[^""'\s,;}]+",
        RegexOptions.Compiled);

    // Defesa extra: redige chaves de alta entropia com prefixo sk- (OpenAI) e sk-ant- (Anthropic)
    // mesmo quando nao precedidas por um nome de propriedade.
    private static readonly Regex ApiKeyPrefixPattern = new(
        @"\bsk-(?:ant-)?[A-Za-z0-9_-]{8,}",
        RegexOptions.Compiled);

    public static string Sanitize(string message)
    {
        var firstLine = message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return "Usage query failed.";
        }

        var sanitized = BearerTokenPattern.Replace(firstLine, "$1[redacted]");
        sanitized = CredentialPropertyPattern.Replace(sanitized, "$1[redacted]");
        sanitized = ApiKeyPrefixPattern.Replace(sanitized, "[redacted]");

        return sanitized.Length <= 160 ? sanitized : sanitized[..160];
    }
}
