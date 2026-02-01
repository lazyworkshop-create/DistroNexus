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

    public PowerShellService(ILogger<PowerShellService> logger, string? customModulePath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Find the PowerShell executable (prefer pwsh.exe for PowerShell Core, fallback to powershell.exe)
        _powerShellPath = FindPowerShellPath();

        // 1. Try configuration first
        if (!string.IsNullOrWhiteSpace(customModulePath))
        {
            var manifestPath = Path.Combine(customModulePath, "DistroNexus.psd1");
            if (File.Exists(manifestPath))
            {
                _moduleBasePath = customModulePath;
                _logger.LogInformation("Using configured PowerShell module path: {Path}", customModulePath);
            }
            else
            {
                _logger.LogWarning("Configured PowerShell module not found at: {Path}", manifestPath);
            }
        }

        // 2. Auto-detection if not found yet
        if (string.IsNullOrEmpty(_moduleBasePath))
        {
            _moduleBasePath = FindModulePath();
            if (!string.IsNullOrEmpty(_moduleBasePath))
            {
                _logger.LogInformation("Auto-detected PowerShell module at: {Path}", _moduleBasePath);
            }
            else
            {
                _logger.LogWarning("PowerShell module path could not be determined. Functionality may be limited.");
            }
        }

        _logger.LogInformation("PowerShell service initialized using: {PowerShellPath}", _powerShellPath);
    }

    private string? FindModulePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var searchPaths = new[]
        {
            Path.Combine(baseDir, "PowerShell"), // Release layout
            Path.Combine(baseDir, "..", "..", "..", "..", "PowerShell"), // Source layout (Dev)
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "PowerShell") // Alternative Source layout
        };

        foreach (var path in searchPaths)
        {
            var fullPath = Path.GetFullPath(path);
            var manifestPath = Path.Combine(fullPath, "DistroNexus.psd1");
            if (File.Exists(manifestPath))
            {
                return fullPath;
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
                if (param.Value is bool b)
                {
                    // Use colon syntax for booleans to support both [bool] and [switch] parameters
                    // e.g. -Force:$true works for both switch can bool types
                    scriptBuilder.Append($" -{param.Key}:{(b ? "$true" : "$false")}");
                }
                else
                {
                    var value = param.Value switch
                    {
                        string s => $"'{s.Replace("'", "''")}'",
                        null => "$null",
                        _ => param.Value.ToString()
                    };
                    scriptBuilder.Append($" -{param.Key} {value}");
                }
            }
        }

        // If generic type is not string, we expect JSON output
        if (typeof(T) != typeof(string))
        {
             // Use depth to ensure nested objects are serialized (though our structure is flat now)
             scriptBuilder.Append(" | ConvertTo-Json -Depth 10 -Compress");
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

    private string PrepareScript(string script)
    {
        if (string.IsNullOrEmpty(_moduleBasePath))
            return script;

        var sb = new StringBuilder();
        var manifestPath = Path.Combine(_moduleBasePath, "DistroNexus.psd1");
        // Import-Module -Force to reload if changed, -ErrorAction Stop to fail immediately if missing
        sb.AppendLine($"Import-Module '{manifestPath}' -Force -ErrorAction Stop;");
        sb.AppendLine(script);
        return sb.ToString();
    }

    /// <inheritdoc/>
    public async Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(script);

        // Prepend module import if available
        var fullScript = PrepareScript(script);

        try
        {
            // Encode the script as Base64 to avoid escaping issues
            var bytes = Encoding.Unicode.GetBytes(fullScript);
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

        // Prepend module import if available
        var fullScript = PrepareScript(script);

        try
        {
            // Encode the script as Base64 to avoid escaping issues
            var bytes = Encoding.Unicode.GetBytes(fullScript);
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

        // DIAGNOSTIC: Log cmdlet call details
        _logger.LogDebug("ExecuteModuleCmdletAsync called: Cmdlet={Cmdlet}, ParameterCount={ParamCount}, ModulePath={ModulePath}",
            cmdletName, parameters?.Count ?? 0, _moduleBasePath ?? "<null>");

        // Check if module is available
        if (_moduleBasePath == null)
        {
            _logger.LogError("DistroNexus PowerShell module path not configured");

            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DistroNexus",
                "settings.json");

            _logger.LogError("Please configure PowerShellModulePath in settings file located at: {SettingsPath}", settingsPath);

            var errorMessage = $"PowerShell module path not configured. Please set PowerShellModulePath in settings: {settingsPath}";

            return new PowerShellScriptResult
            {
                ExitCode = 1,
                Error = errorMessage,
                UsedModule = false,
                Exception = new InvalidOperationException(errorMessage)
            };
        }

        try
        {
            // DIAGNOSTIC: Verify module file exists
            var moduleManifestPath = Path.Combine(_moduleBasePath, "DistroNexus.psd1");
            if (!File.Exists(moduleManifestPath))
            {
                _logger.LogError("Module manifest not found at: {Path}", moduleManifestPath);

                var friendlyError = $"PowerShell module files are missing. Please ensure the module is installed at: {_moduleBasePath}";

                return new PowerShellScriptResult
                {
                    ExitCode = 1,
                    Error = friendlyError,
                    UsedModule = false,
                    Exception = new FileNotFoundException(friendlyError, moduleManifestPath)
                };
            }

            // Build the cmdlet invocation script
            var scriptBuilder = new StringBuilder();

            // Import module with verbose error handling - use full path to manifest file
            scriptBuilder.AppendLine("$ErrorActionPreference = 'Stop'");
            scriptBuilder.AppendLine($"Import-Module '{moduleManifestPath}' -Force -ErrorAction Stop");

            // DIAGNOSTIC: Verify module imported
            scriptBuilder.AppendLine("if (-not (Get-Module -Name DistroNexus)) { throw 'Module DistroNexus failed to import' }");

            // Execute cmdlet
            scriptBuilder.Append(cmdletName);

            // Add parameters
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    // Handle switch parameters (boolean true) - don't add value
                    if (param.Value is bool boolValue)
                    {
                        if (boolValue)
                        {
                            scriptBuilder.Append($" -{param.Key}");
                        }
                        // If false, don't add the parameter at all
                    }
                    else
                    {
                        scriptBuilder.Append($" -{param.Key} ");
                        scriptBuilder.Append(FormatParameterValue(param.Value));
                    }
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

            var script = scriptBuilder.ToString();
            _logger.LogDebug("Executing module cmdlet: {Cmdlet} from module path: {ModulePath}", cmdletName, _moduleBasePath);
            _logger.LogDebug("Generated PowerShell script (first 200 chars): {Script}",
                script.Length > 200 ? script[..200] : script);

            // Execute with timeout
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var result = await ExecuteScriptWithResultAsync(script, combinedCts.Token);
            result.UsedModule = true;

            // DIAGNOSTIC: Log execution result
            _logger.LogDebug("Cmdlet execution completed: ExitCode={ExitCode}, OutputLength={OutputLen}, ErrorLength={ErrorLen}",
                result.ExitCode, result.Output?.Length ?? 0, result.Error?.Length ?? 0);

            // If execution failed, process the error to make it user-friendly
            if (!result.Success && !string.IsNullOrWhiteSpace(result.Error))
            {
                // Log the raw error for debugging
                _logger.LogError("Raw PowerShell error: {RawError}", result.Error);

                // Extract friendly error message
                var friendlyError = ExtractFriendlyErrorMessage(result.Error, cmdletName);
                result.Error = friendlyError;

                _logger.LogError("User-friendly error: {FriendlyError}", friendlyError);
            }

            // Parse JSON output if available
            if (options.ParseAsJson && result.Success && !string.IsNullOrWhiteSpace(result.Output))
            {
                try
                {
                    _logger.LogDebug("Parsing JSON output (length={Length})...", result.Output.Length);
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(result.Output);

                    // Handle both single object and array results
                    if (jsonElement.ValueKind == JsonValueKind.Array)
                    {
                        result.ParsedObjects = jsonElement.EnumerateArray().ToList();
                        _logger.LogDebug("Parsed JSON array with {Count} elements", result.ParsedObjects.Count);
                    }
                    else
                    {
                        result.ParsedObjects = [jsonElement];
                        _logger.LogDebug("Parsed single JSON object");
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse cmdlet output as JSON. Output (first 500 chars): {Output}",
                        result.Output?.Length > 500 ? result.Output[..500] : result.Output);
                }
            }
            else if (options.ParseAsJson && result.Success)
            {
                _logger.LogWarning("Expected JSON output but received empty/null output from cmdlet {Cmdlet}", cmdletName);
            }
            else if (!result.Success)
            {
                _logger.LogError("Cmdlet {Cmdlet} failed with exit code {ExitCode}. Error: {Error}",
                    cmdletName, result.ExitCode, result.Error);
            }

            return result;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("Module cmdlet {Cmdlet} execution canceled or timed out after {Timeout}s",
                cmdletName, options.TimeoutSeconds);

            var friendlyError = $"The operation timed out after {options.TimeoutSeconds} seconds. This may be due to a slow network connection or a large file download. Please try again.";

            return new PowerShellScriptResult
            {
                ExitCode = 1,
                Error = friendlyError,
                UsedModule = false,
                Exception = ex
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing module cmdlet: {Cmdlet}. Exception type: {ExceptionType}, Message: {Message}",
                cmdletName, ex.GetType().Name, ex.Message);

            var friendlyError = ExtractFriendlyErrorMessage(ex.Message, cmdletName);

            return new PowerShellScriptResult
            {
                ExitCode = 1,
                Error = friendlyError,
                UsedModule = false,
                Exception = ex
            };
        }
    }

    /// <summary>
    /// Extracts a user-friendly error message from PowerShell error output.
    /// </summary>
    /// <param name="errorMessage">The raw error message from PowerShell.</param>
    /// <param name="cmdletName">The name of the cmdlet that failed.</param>
    /// <returns>A user-friendly error message.</returns>
    private static string ExtractFriendlyErrorMessage(string errorMessage, string cmdletName)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return "An unknown error occurred. Please check the logs for details.";

        // Remove CLIXML tags and XML noise
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            errorMessage,
            @"#< CLIXML.*?<Objs.*?</Objs>",
            "",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        // Remove color codes and escape sequences
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\x1b\[[0-9;]*m", "");

        // Remove "At line:" and stack trace information
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"At line:\d+.*?(\r?\n|$)",
            "",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        // Remove "+ CategoryInfo" and similar PowerShell diagnostic info
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned,
            @"\+\s+(CategoryInfo|FullyQualifiedErrorId).*?(\r?\n|$)",
            "",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        // Check for common error patterns and provide friendly messages

        // Module import errors
        if (errorMessage.Contains("failed to import", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("Module DistroNexus failed to import", StringComparison.OrdinalIgnoreCase))
        {
            return "Failed to load the PowerShell module. Please verify that the module files are not corrupted and try restarting the application.";
        }

        // Access denied / permission errors
        if (errorMessage.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("access to the path", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return "Access denied. Please ensure you have administrator privileges or the required permissions to perform this operation.";
        }

        // File not found errors
        if (errorMessage.Contains("cannot find path", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("file not found", StringComparison.OrdinalIgnoreCase))
        {
            return "A required file or directory was not found. Please verify the installation path and try again.";
        }

        // Network/download errors
        if (errorMessage.Contains("download", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("network", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("unable to connect", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("404", StringComparison.OrdinalIgnoreCase))
        {
            return "Failed to download the required files. Please check your internet connection and try again.";
        }

        // Disk space errors
        if (errorMessage.Contains("disk", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("space", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("not enough", StringComparison.OrdinalIgnoreCase))
        {
            return "Insufficient disk space. Please free up some space and try again.";
        }

        // WSL-specific errors
        if (errorMessage.Contains("wsl", StringComparison.OrdinalIgnoreCase))
        {
            if (errorMessage.Contains("not installed", StringComparison.OrdinalIgnoreCase))
            {
                return "WSL is not installed or not properly configured. Please install WSL2 from Windows Features.";
            }
            if (errorMessage.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                errorMessage.Contains("already in use", StringComparison.OrdinalIgnoreCase))
            {
                return "A WSL distribution with this name already exists. Please choose a different name.";
            }
            if (errorMessage.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            {
                return "Invalid WSL operation. Please ensure WSL2 is properly installed and configured.";
            }
        }

        // Parameter validation errors
        if (errorMessage.Contains("parameter", StringComparison.OrdinalIgnoreCase) &&
            (errorMessage.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
             errorMessage.Contains("cannot validate", StringComparison.OrdinalIgnoreCase)))
        {
            return "Invalid input provided. Please check your settings and try again.";
        }

        // Extract the main error message (first meaningful line)
        var lines = cleaned.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var meaningfulLines = lines
            .Where(line => 
                !string.IsNullOrWhiteSpace(line) &&
                !line.StartsWith("+", StringComparison.Ordinal) &&
                !line.StartsWith("At ", StringComparison.Ordinal) &&
                !line.Contains("CategoryInfo", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("FullyQualifiedErrorId", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (meaningfulLines.Count > 0)
        {
            var mainError = meaningfulLines[0].Trim();

            // If the message is too technical, provide a generic friendly message
            if (mainError.Length > 200 || 
                mainError.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                mainError.Contains("StackTrace", StringComparison.OrdinalIgnoreCase))
            {
                return $"Operation '{cmdletName}' failed. Please check the logs for detailed error information.";
            }

            return mainError;
        }

        // Last resort: provide a generic message
        return $"Operation '{cmdletName}' failed. Please check the logs for detailed error information.";
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

    /// <inheritdoc/>
    public async Task<string> GetDiagnosticInfoAsync(CancellationToken cancellationToken = default)
    {
        var diagnostics = new StringBuilder();
        diagnostics.AppendLine("=== PowerShell Service Diagnostics ===");
        diagnostics.AppendLine();

        // PowerShell executable
        diagnostics.AppendLine($"PowerShell Path: {_powerShellPath}");
        diagnostics.AppendLine($"PowerShell Exists: {File.Exists(_powerShellPath)}");

        try
        {
            var versionScript = "$PSVersionTable.PSVersion.ToString()";
            var version = await ExecuteScriptAsync(versionScript, cancellationToken);
            diagnostics.AppendLine($"PowerShell Version: {version}");
        }
        catch (Exception ex)
        {
            diagnostics.AppendLine($"PowerShell Version: ERROR - {ex.Message}");
        }

        diagnostics.AppendLine();

        // Module path detection
        diagnostics.AppendLine($"Module Base Path: {_moduleBasePath ?? "<NULL - Module Not Found>"}");

        if (_moduleBasePath != null)
        {
            var manifestPath = Path.Combine(_moduleBasePath, "DistroNexus.psd1");
            diagnostics.AppendLine($"Module Manifest: {manifestPath}");
            diagnostics.AppendLine($"Manifest Exists: {File.Exists(manifestPath)}");

            if (File.Exists(manifestPath))
            {
                try
                {
                    var manifestInfo = new FileInfo(manifestPath);
                    diagnostics.AppendLine($"Manifest Size: {manifestInfo.Length} bytes");
                    diagnostics.AppendLine($"Manifest Modified: {manifestInfo.LastWriteTime}");
                }
                catch (Exception ex)
                {
                    diagnostics.AppendLine($"Manifest Info Error: {ex.Message}");
                }
            }

            // Try to import the module
            try
            {
                diagnostics.AppendLine();
                diagnostics.AppendLine("Attempting to import module...");
                var importScript = $@"
                    $ErrorActionPreference = 'Stop'
                    Import-Module '{_moduleBasePath}' -Force
                    $module = Get-Module -Name DistroNexus
                    if ($module) {{
                        [PSCustomObject]@{{
                            Name = $module.Name
                            Version = $module.Version.ToString()
                            Path = $module.Path
                            ExportedCommands = ($module.ExportedCommands.Keys -join ', ')
                        }} | ConvertTo-Json
                    }} else {{
                        throw 'Module not loaded'
                    }}
                ";

                var moduleInfo = await ExecuteScriptAsync(importScript, cancellationToken);
                diagnostics.AppendLine("Module Import: SUCCESS");
                diagnostics.AppendLine($"Module Info: {moduleInfo}");
            }
            catch (Exception ex)
            {
                diagnostics.AppendLine($"Module Import: FAILED - {ex.Message}");
            }
        }
        else
        {
            diagnostics.AppendLine();
            diagnostics.AppendLine("Module path detection FAILED. Checked paths:");

            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var appModulePath = Path.Combine(appDirectory, "PowerShell");
            var appManifestPath = Path.Combine(appModulePath, "DistroNexus.psd1");

            diagnostics.AppendLine($"  1. Application Directory: {appManifestPath} (Exists: {File.Exists(appManifestPath)})");

            diagnostics.AppendLine();
            diagnostics.AppendLine("Directories:");
            diagnostics.AppendLine($"  AppDomain.BaseDirectory: {appDirectory}");
            diagnostics.AppendLine($"  Expected Module Path: {appModulePath}");
        }

        diagnostics.AppendLine();
        diagnostics.AppendLine("=== End Diagnostics ===");

        return diagnostics.ToString();
    }
}
