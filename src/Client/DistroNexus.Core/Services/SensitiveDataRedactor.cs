using System.Text.RegularExpressions;

namespace DistroNexus.Core.Services;

public static partial class SensitiveDataRedactor
{
    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        var result = CredentialRegex().Replace(value, "$1=[REDACTED]");
        result = PrivateKeyRegex().Replace(result, "[REDACTED PRIVATE KEY]");
        return UserPathRegex().Replace(result, "$1\\[REDACTED]");
    }

    [GeneratedRegex(@"(?i)\b(password|passwd|token|api[_-]?key|secret)\s*[=:]\s*[^\s;,]+")]
    private static partial Regex CredentialRegex();
    [GeneratedRegex(@"(?s)-----BEGIN [^-]*PRIVATE KEY-----.*?-----END [^-]*PRIVATE KEY-----")]
    private static partial Regex PrivateKeyRegex();
    [GeneratedRegex(@"(?i)\b(C:\\Users)\\[^\\\s]+")]
    private static partial Regex UserPathRegex();
}
