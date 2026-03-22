using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Runs wsl.exe via <see cref="System.Diagnostics.Process"/> with redirected stdout.
/// Also provides static parse helpers for wsl.exe output formats.
/// </summary>
public sealed partial class WslCliRunner : IWslCliRunner
{
    private readonly ILogger<WslCliRunner> _logger;
    private bool _disposed;

    public WslCliRunner(ILogger<WslCliRunner> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ── IWslCliRunner ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<WslCliResult> RunAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "wsl.exe",
            Arguments              = arguments,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            // Unicode output from wsl.exe
            StandardOutputEncoding = Encoding.Unicode,
            StandardErrorEncoding  = Encoding.Unicode,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new WslCliResult
        {
            ExitCode = process.ExitCode,
            Output   = stdout.ToString(),
            Error    = stderr.ToString(),
        };
    }

    // ── Static parse helpers ────────────────────────────────────────────��────

    /// <summary>
    /// Parses the output of <c>wsl --list --verbose --all</c> into a list of instance descriptors.
    /// </summary>
    public static List<WslInstanceDescriptor> ParseWslListVerbose(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return [];

        var results = new List<WslInstanceDescriptor>();

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.TrimStart();

            // Header line
            if (trimmed.StartsWith("NAME", StringComparison.OrdinalIgnoreCase))
                continue;

            bool isDefault = trimmed.StartsWith('*');
            var cleaned    = trimmed.TrimStart('*').Trim();

            // Split by multiple spaces to parse columns
            var parts = cleaned.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;

            if (!int.TryParse(parts[2], out var version)) continue;

            results.Add(new WslInstanceDescriptor
            {
                Name      = parts[0],
                State     = parts[1],
                Version   = version,
                IsDefault = isDefault,
            });
        }

        return results;
    }

    /// <summary>
    /// Parses the output of <c>wsl --list --running</c> into a set of running instance names.
    /// </summary>
    public static HashSet<string> ParseWslListRunning(string? output)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(output)) return result;

        // Running output: lines like "Ubuntu-22.04 (Running)" or just "Ubuntu-22.04"
        var pattern = RunningLineRegex();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var m = pattern.Match(line.Trim());
            if (m.Success)
                result.Add(m.Groups["name"].Value);
        }

        return result;
    }

    [GeneratedRegex(@"^(?<name>[^\s(]+)\s*(?:\(Running\))?$", RegexOptions.IgnoreCase)]
    private static partial Regex RunningLineRegex();

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
    }
}

/// <summary>
/// Lightweight descriptor returned by <see cref="WslCliRunner.ParseWslListVerbose"/>.
/// </summary>
public sealed class WslInstanceDescriptor
{
    public string Name      { get; init; } = string.Empty;
    public string State     { get; init; } = string.Empty;
    public int    Version   { get; init; }
    public bool   IsDefault { get; init; }
}
