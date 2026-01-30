using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
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
    private readonly string? _moduleBasePath;
    private bool _disposed;

    public PowerShellService(ILogger<PowerShellService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Find the PowerShell executable (prefer pwsh.exe for PowerShell Core, fallback to powershell.exe)
        _powerShellPath = FindPowerShellPath();
        
        // Detect DistroNexus module path (src/PowerShell relative to workspace root)
        _moduleBasePath = FindDistroNexusModulePath();
        
        _logger.LogInformation("PowerShell service initialized using: {PowerShellPath}", _powerShellPath);
        if (_moduleBasePath != null)
        {
            _logger.LogInformation("DistroNexus module detected at: {ModulePath}", _moduleBasePath);
        }
        else
        {
            _logger.LogWarning("DistroNexus PowerShell module not found, will use inline scripts");
        }
    }

    private static string? FindDistroNexusModulePath()
    {
        // Try to locate the DistroNexus module
        var possiblePaths = new[]
        {
            // Development paths (relative to bin directory)
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\PowerShell\DistroNexus.psd1"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\PowerShell\DistroNexus.psd1"),
            
            // Installed paths
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"DistroNexus\PowerShell\DistroNexus.psd1"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"DistroNexus\PowerShell\DistroNexus.psd1"),
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return Path.GetDirectoryName(fullPath);
            }
        }

        return null;
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

        // Quick check if pwsh is in PATH with timeout
        try
        {
            var task = Task.Run(() =>
            {
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
                        process.WaitForExit(2000); // 2 second timeout
                        if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                        {
                            var firstPath = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                            if (!string.IsNullOrEmpty(firstPath) && File.Exists(firstPath))
                                return firstPath;
                        }
                    }
                }
                catch
                {
                    // Ignore errors in the task
                }
                return null;
            });

            var completed = task.Wait(TimeSpan.FromSeconds(3));
            if (completed)
            {
                var result = task.Result;
                if (!string.IsNullOrEmpty(result))
                    return result;
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

            // Add timeout to prevent hanging
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await process.WaitForExitAsync(combinedCts.Token);

            var output = outputBuilder.ToString().TrimEnd();
            var error = errorBuilder.ToString().TrimEnd();

            if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
            {
                _logger.LogError("PowerShell script failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                throw new InvalidOperationException($"PowerShell script failed: {error}");
            }

            return output;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("PowerShell script execution timed out or was canceled");
            throw;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error executing PowerShell script");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<PowerShellScriptResult> ExecuteScriptWithResultAsync(string script, CancellationToken cancellationToken = default)
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

            var result = new PowerShellScriptResult
            {
                ExitCode = process.ExitCode,
                Output = output,
                Error = error
            };

            if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("PowerShell script failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
            }

            return result;
        }
        catch (Exception ex)
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

    /// <summary>
    /// Executes a DistroNexus PowerShell module cmdlet with typed result.
    /// </summary>
    /// <typeparam name="T">The expected return type.</typeparam>
    /// <param name="cmdletName">Name of the cmdlet (e.g., "Get-DistroNexusInstance").</param>
    /// <param name="parameters">Cmdlet parameters.</param>
    /// <param name="options">Module call options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed result of type T.</returns>
    public async Task<T?> ExecuteModuleCmdletAsync<T>(
        string cmdletName,
        Dictionary<string, object>? parameters = null,
        ModuleCallOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteModuleCmdletAsync(cmdletName, parameters, options, cancellationToken);
        
        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(result.Output);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize module output to {Type}", typeof(T).Name);
            return default;
        }
    }

    /// <summary>
    /// Executes a DistroNexus PowerShell module cmdlet and returns the raw result.
    /// </summary>
    /// <param name="cmdletName">Name of the cmdlet (e.g., "Get-DistroNexusInstance").</param>
    /// <param name="parameters">Cmdlet parameters.</param>
    /// <param name="options">Module call options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>PowerShell execution result.</returns>
    public async Task<PowerShellScriptResult> ExecuteModuleCmdletAsync(
        string cmdletName,
        Dictionary<string, object>? parameters = null,
        ModuleCallOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cmdletName);
        
        options ??= new ModuleCallOptions();

        // Check if module is available
        if (_moduleBasePath == null)
        {
            _logger.LogWarning("DistroNexus module not available, cannot execute cmdlet: {Cmdlet}", cmdletName);
            
            if (options.UseModuleFallback)
            {
                _logger.LogInformation("Module fallback is disabled or not implemented for {Cmdlet}", cmdletName);
            }
            
            return new PowerShellScriptResult
            {
                ExitCode = 1,
                Error = "DistroNexus PowerShell module not found",
                UsedModule = false
            };
        }

        try
        {
            // Build the cmdlet invocation script
            var scriptBuilder = new StringBuilder();
            
            // Import module
            scriptBuilder.AppendLine($"Import-Module '{_moduleBasePath}' -ErrorAction Stop");
            
            // Execute cmdlet
            scriptBuilder.Append(cmdletName);
            
            // Add parameters
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    scriptBuilder.Append($" -{param.Key} ");
                    scriptBuilder.Append(FormatParameterValue(param.Value));
                }
            }
            
            // Add common parameters
            if (options.ForceRefresh && cmdletName == "Get-DistroNexusInstance")
            {
                scriptBuilder.Append(" -ForceUpdate");
            }
            
            if (options.LogVerbose)
            {
                scriptBuilder.Append(" -Verbose");
            }
            
            // Convert output to JSON if requested
            if (options.ParseAsJson)
            {
                scriptBuilder.AppendLine(" | ConvertTo-Json -Depth 10 -Compress");
            }
            
            _logger.LogDebug("Executing module cmdlet: {Cmdlet}", cmdletName);
            
            // Execute with timeout
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            var result = await ExecuteScriptWithResultAsync(scriptBuilder.ToString(), combinedCts.Token);
            result.UsedModule = true;
            
            // Parse JSON output if available
            if (options.ParseAsJson && result.Success && !string.IsNullOrWhiteSpace(result.Output))
            {
                try
                {
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(result.Output);
                    
                    // Handle both single object and array results
                    if (jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        result.ParsedObjects = jsonElement.EnumerateArray().ToList();
                    }
                    else
                    {
                        result.ParsedObjects = [jsonElement];
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse cmdlet output as JSON");
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing module cmdlet: {Cmdlet}", cmdletName);
            
            return new PowerShellScriptResult
            {
                ExitCode = 1,
                Error = ex.Message,
                UsedModule = false
            };
        }
    }

    /// <summary>
    /// Formats a parameter value for PowerShell command line.
    /// </summary>
    private static string FormatParameterValue(object? value)
    {
        return value switch
        {
            null => "$null",
            string s => $"'{s.Replace("'", "''")}'",
            bool b => b ? "$true" : "$false",
            int or long or double or float or decimal => value.ToString()!,
            _ => $"'{value}'"
        };
    }
}
