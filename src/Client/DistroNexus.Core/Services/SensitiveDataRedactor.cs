using System.Text.RegularExpressions;

namespace DistroNexus.Core.Services;

public static partial class SensitiveDataRedactor
{
    /// <summary>Redacts credentials and key material but intentionally retains paths for local, non-redacted previews.</summary>
    public static string RedactSecrets(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        var normalized = value.Replace("\0", "[NUL]");
        normalized = JsonCredentialRegex().Replace(normalized, "\"$1\":\"[REDACTED]\"");
        return PrivateKeyRegex().Replace(CredentialRegex().Replace(normalized, "$1=[REDACTED]"), "[REDACTED PRIVATE KEY]");
    }

    public static string Redact(string? value)
    {
        var result = RedactSecrets(value);
        // JSON-formatted log records escape each path separator (C:\\Users\\name). Redact this
        // representation before processing normal display strings, otherwise a username can leak.
        result = EscapedWindowsPathRegex().Replace(result, "[REDACTED WINDOWS PATH]");
        result = UserPathRegex().Replace(result, "$1\\[REDACTED]");
        result = LinuxHomeRegex().Replace(result, "$1/[REDACTED]");
        return WindowsPathRegex().Replace(result, "[REDACTED WINDOWS PATH]");
    }

    [GeneratedRegex(@"(?i)\b(password|passwd|token|api[_-]?key|secret)\s*[=:]\s*[^\s;,]+")]
    private static partial Regex CredentialRegex();
    [GeneratedRegex("(?i)\\\"(password|passwd|token|api[_-]?key|secret)\\\"\\s*:\\s*\\\"(?:\\\\.|[^\\\"])*\\\"")]
    private static partial Regex JsonCredentialRegex();
    [GeneratedRegex(@"(?s)-----BEGIN [^-]*PRIVATE KEY-----.*?-----END [^-]*PRIVATE KEY-----")]
    private static partial Regex PrivateKeyRegex();
    [GeneratedRegex(@"(?i)\b(C:\\Users)\\[^\\\s]+")]
    private static partial Regex UserPathRegex();
    [GeneratedRegex(@"(?i)(/home|/root)/[^\s/]+")]
    private static partial Regex LinuxHomeRegex();
    [GeneratedRegex(@"(?i)\b[A-Z]:\\(?:[^\s\\]+\\)*[^\s\\]*")]
    private static partial Regex WindowsPathRegex();
    [GeneratedRegex("(?i)\\b[A-Z]:(?:\\\\\\\\)(?:(?:[^\\\\\\s\\\",}]|\\\\\\\\)+)")]
    private static partial Regex EscapedWindowsPathRegex();
}
