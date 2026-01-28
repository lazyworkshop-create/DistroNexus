using System.Diagnostics;
using System.Text;
using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Service for executing PowerShell commands and scripts using the system-installed PowerShell.
/// Uses process-based execution to ensure access to all PowerShell modules.
/// </summary>
public class PowerShellService : IPowerShellService, IDisposable
{
    private readonly ILogger<PowerShellService> _logger;
    private readonly string _powerShellPath;
    private bool _disposed;

    public PowerShellService(ILogger<PowerShellService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Find the PowerShell executable (prefer pwsh.exe for PowerShell Core, fallback to powershell.exe)
        _powerShellPath = FindPowerShellPath();
        
        _logger.LogInformation("PowerShell service initialized using: {PowerShellPath}", _powerShellPath);
    }

    private static string FindPowerShellPath()
    {
        // Try PowerShell Core (pwsh) first - it's faster and more modern
        var pwshPaths = new[]
        {
            @"C:\Program Files\PowerShell\7\pwsh.exe",
            @"C:\Program Files\PowerShell\7-preview\pwsh.exe",
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\PowerShell\7\pwsh.exe"),
        };

        foreach (var path in pwshPaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Check if pwsh is in PATH
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "pwsh",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    var firstPath = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (!string.IsNullOrEmpty(firstPath) && File.Exists(firstPath))
                        return firstPath;
                }
            }
        }
        catch { /* Ignore and fallback */ }

        // Fallback to Windows PowerShell (always available on Windows)
        return @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
    }

    /// <inheritdoc/>
    public async Task<T?> ExecuteAsync<T>(string cmdlet, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cmdlet);

        // Build the command with parameters
        var scriptBuilder = new StringBuilder(cmdlet);
        
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                var value = param.Value switch
                {
                    string s => $"'{s.Replace("'", "''")}'",
                    bool b => b ? "$true" : "$false",
                    null => "$null",
                    _ => param.Value.ToString()
                };
                scriptBuilder.Append($" -{param.Key} {value}");
            }
        }

        var result = await ExecuteScriptAsync(scriptBuilder.ToString(), cancellationToken);

        if (typeof(T) == typeof(string))
        {
            return (T)(object)result;
        }

        if (string.IsNullOrWhiteSpace(result))
            return default;

        return System.Text.Json.JsonSerializer.Deserialize<T>(result);
    }

    /// <inheritdoc/>
    public async Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        try
        {
            // Encode the script as Base64 to avoid escaping issues
            var bytes = Encoding.Unicode.GetBytes(script);
            var encodedCommand = Convert.ToBase64String(bytes);

            var startInfo = new ProcessStartInfo
            {
                FileName = _powerShellPath,
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            var output = outputBuilder.ToString().TrimEnd();
            var error = errorBuilder.ToString().TrimEnd();

            if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
            {
                _logger.LogError("PowerShell script failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                throw new InvalidOperationException($"PowerShell script failed: {error}");
            }

            return output;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error executing PowerShell script");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task ImportModuleAsync(string modulePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(modulePath);

        if (!File.Exists(modulePath))
            throw new FileNotFoundException($"Module file not found: {modulePath}");

        _logger.LogInformation("Importing PowerShell module from {ModulePath}", modulePath);
        
        await ExecuteScriptAsync($"Import-Module -Name '{modulePath}' -Force", cancellationToken);
        
        _logger.LogInformation("PowerShell module imported successfully");
    }

    /// <inheritdoc/>
    public async Task<bool> IsModuleLoadedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ExecuteScriptAsync("Get-Module -Name DistroNexus | Select-Object -ExpandProperty Name", cancellationToken);
            return !string.IsNullOrWhiteSpace(result);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
