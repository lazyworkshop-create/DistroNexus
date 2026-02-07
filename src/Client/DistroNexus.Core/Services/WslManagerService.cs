using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Service for managing WSL instances using DistroNexus PowerShell module.
/// Falls back to inline scripts if module is not available.
/// </summary>
public partial class WslManagerService : IWslManagerService
{
    private readonly IPowerShellService _powerShellService;
    private readonly ICatalogService _catalogService;
    private readonly ILogger<WslManagerService> _logger;
    private readonly bool _useModuleFallback = true;

    private const string LxssRegistryPath = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss";

    // Timeout constants for various operations
    /// <summary>Quick operations (list, get): 10 seconds</summary>
    private const int QuickOperationTimeoutSeconds = 10;

    /// <summary>Normal operations (start, stop, remove, rename): 30 seconds</summary>
    private const int NormalOperationTimeoutSeconds = 30;

    /// <summary>Long operations (move, install): 120 seconds</summary>
    private const int LongOperationTimeoutSeconds = 120;

    /// <summary>Very long operations (download): 300 seconds (5 minutes)</summary>
    private const int VeryLongOperationTimeoutSeconds = 300;

    public WslManagerService(
        IPowerShellService powerShellService,
        ICatalogService catalogService,
        ILogger<WslManagerService> logger)
    {
        _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
        _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<List<WslInstance>> GetInstancesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving WSL instances using PowerShell module");

        // Always use PowerShell module with SkipDiskSize to avoid auto-starting stopped instances
        // Use ForceUpdate to ensure we get the latest running state from wsl --list, 
        // while preserving cached disk sizes (via SkipDiskSize)
        var moduleResult = await _powerShellService.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstance",
            parameters: new Dictionary<string, object>
            {
                { "SkipDiskSize", true },
                { "ForceUpdate", true }
            },
            options: new ModuleCallOptions
            {
                TimeoutSeconds = QuickOperationTimeoutSeconds,
                ParseAsJson = true,
                UseModuleFallback = false  // Do not use fallback, always require module
            },
            cancellationToken: cancellationToken);

        // Check if module call succeeded
        if (!moduleResult.Success)
        {
            var errorMessage = moduleResult.Exception != null 
                ? $"PowerShell module error: {moduleResult.Exception.Message}" 
                : "Failed to retrieve WSL instances using PowerShell module.";

            _logger.LogError(moduleResult.Exception, "Module call failed for Get-DistroNexusInstance");
            throw new InvalidOperationException(errorMessage, moduleResult.Exception);
        }

        if (!moduleResult.UsedModule)
        {
            _logger.LogError("PowerShell module was not used for Get-DistroNexusInstance");
            throw new InvalidOperationException(
                "DistroNexus PowerShell module is not available. Please ensure the module is properly installed.");
        }

        if (moduleResult.ParsedObjects == null || moduleResult.ParsedObjects.Count == 0)
        {
            _logger.LogInformation("No WSL instances found");
            return [];
        }

        _logger.LogInformation("Successfully retrieved {Count} instances using module", moduleResult.ParsedObjects.Count);

        // DIAGNOSTIC: Log raw module output
        if (!string.IsNullOrWhiteSpace(moduleResult.Output))
        {
            _logger.LogDebug("Raw module output (first 500 chars): {Output}", 
                moduleResult.Output.Length > 500 ? moduleResult.Output[..500] : moduleResult.Output);
        }

        var instances = ParseInstancesFromModule(moduleResult.ParsedObjects);

        // DIAGNOSTIC: Log parsed instances with disk sizes
        foreach (var instance in instances)
        {
            _logger.LogDebug("Parsed instance: Name={Name}, State={State}, Size={Size} bytes, Path={Path}", 
                instance.Name, instance.State, instance.Size, instance.InstallPath);
        }

        _logger.LogInformation("Parsed {Count} instances from module, returning to caller", instances.Count);
        return instances;
    }

    private List<WslInstance> ParseInstancesFromModule(List<JsonElement> parsedObjects)
    {
        var instances = new List<WslInstance>();

        _logger.LogDebug("Parsing {Count} instance objects from module", parsedObjects.Count);

        foreach (var element in parsedObjects)
        {
            try
            {
                // DIAGNOSTIC: Log available properties in this element
                var propertyNames = new List<string>();
                foreach (var prop in element.EnumerateObject())
                {
                    propertyNames.Add(prop.Name);
                }
                _logger.LogDebug("Instance JSON properties: {Properties}", string.Join(", ", propertyNames));

                var instanceName = element.GetProperty("Name").GetString() ?? "";

                // DIAGNOSTIC: Check if DiskSize property exists and log its value
                long diskSize = 0;
                if (element.TryGetProperty("DiskSize", out var sizeElement))
                {
                    if (sizeElement.ValueKind == JsonValueKind.Number)
                    {
                        diskSize = sizeElement.GetInt64();
                        _logger.LogDebug("Instance '{Name}': Found DiskSize property = {Size} bytes", instanceName, diskSize);
                    }
                    else if (sizeElement.ValueKind == JsonValueKind.String)
                    {
                        var sizeStr = sizeElement.GetString();
                        if (long.TryParse(sizeStr, out var parsedSize))
                        {
                            diskSize = parsedSize;
                            _logger.LogDebug("Instance '{Name}': Parsed DiskSize from string = {Size} bytes", instanceName, diskSize);
                        }
                        else
                        {
                            _logger.LogWarning("Instance '{Name}': DiskSize is string but cannot parse: '{Value}'", instanceName, sizeStr);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Instance '{Name}': DiskSize has unexpected type: {Type}", instanceName, sizeElement.ValueKind);
                    }
                }
                else
                {
                    _logger.LogDebug("Instance '{Name}': DiskSize property not found in JSON (SkipDiskSize=true)", instanceName);
                }

                var instance = new WslInstance
                {
                    Name = instanceName,
                    State = element.GetProperty("State").GetString() ?? "Unknown",
                    Version = element.TryGetProperty("Version", out var ver) ? 
                        int.Parse(ver.GetString() ?? "2") : 2,
                    InstallPath = element.GetProperty("BasePath").GetString() ?? "",
                    Size = diskSize,
                    Distribution = element.TryGetProperty("Distribution", out var dist) ? dist.GetString() ?? instanceName : instanceName,
                    IsDefault = false, // Will be determined from wsl --list
                    LastAccessed = element.TryGetProperty("InstallTime", out var time) && 
                        DateTime.TryParse(time.GetString(), out var dt) ? dt : (DateTime?)null
                };

                _logger.LogDebug("Created WslInstance object: Name={Name}, Size={Size}, State={State}", 
                    instance.Name, instance.Size, instance.State);

                instances.Add(instance);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse instance from module output. Element: {Element}", 
                    element.ToString());
            }
        }

        _logger.LogInformation("Successfully parsed {Count} instances from module output", instances.Count);
        return instances;
    }

    private async Task<List<WslInstance>> GetInstancesInlineAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Retrieving WSL instances");

            // Get WSL status from wsl.exe --list --verbose with timeout
            const string script = """
                $instances = @()
                $defaultDistro = $null
                
                # Get state from wsl --list --verbose (includes Running/Stopped state)
                $wslOutput = wsl --list --verbose 2>&1
                $wslStatus = @{}

                # Load cached metadata (Distribution names)
                $cacheMap = @{}
                try {
                    $jsonPath = Join-Path $env:APPDATA "DistroNexus\instances.json"
                    if (Test-Path $jsonPath) {
                        $cacheData = Get-Content $jsonPath -Raw | ConvertFrom-Json
                        foreach ($c in $cacheData) { 
                            if ($c.PSObject.Properties.Match('Distribution')) {
                                $cacheMap[$c.Name] = $c.Distribution 
                            }
                        }
                    }
                } catch { }
                
                if ($wslOutput -and $LASTEXITCODE -eq 0) {
                    foreach ($line in $wslOutput) {
                        $cleanLine = ($line -replace "`0", "").Trim()
                        if ([string]::IsNullOrWhiteSpace($cleanLine) -or $cleanLine -match "^NAME\s+STATE") { continue }
                        
                        $isDefault = $cleanLine.StartsWith("*")
                        $cleanLine = $cleanLine.TrimStart("*").Trim()
                        $parts = $cleanLine -split "\s+" | Where-Object { $_ }
                        
                        if ($parts.Count -ge 2) {
                            $name = $parts[0]
                            $wslStatus[$name] = @{
                                State = $parts[1]
                                Version = if ($parts.Count -ge 3) { [int]$parts[2] } else { 2 }
                                IsDefault = $isDefault
                            }
                            if ($isDefault) { $defaultDistro = $name }
                        }
                    }
                }
                
                # Get installation paths from registry - optimized with error handling
                $lxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
                if (Test-Path $lxssPath) {
                    $keys = Get-ChildItem -Path $lxssPath -ErrorAction SilentlyContinue | Select-Object -First 20  # Limit to 20 instances
                    foreach ($key in $keys) {
                        try {
                            $props = Get-ItemProperty -Path $key.PSPath -ErrorAction SilentlyContinue
                            $name = $props.DistributionName
                            if (-not $name) { continue }
                            
                            $basePath = $props.BasePath
                            
                            # DO NOT access VHDX files during scan - this may auto-start stopped instances!
                            # Size and LastAccessed will be loaded on-demand when needed
                            $size = 0
                            $lastAccessed = $null
                            
                            $state = "Stopped"
                            $version = 2
                            $isDefault = $false
                            
                            if ($wslStatus.ContainsKey($name)) {
                                $state = $wslStatus[$name].State
                                $version = $wslStatus[$name].Version
                                $isDefault = $wslStatus[$name].IsDefault
                            }
                            
                            $distName = if ($cacheMap.ContainsKey($name)) { $cacheMap[$name] } else { $name }
                            
                            # Probe running instance for better name
                            if ($state -eq "Running") {
                                $osRel = wsl -d $name -- cat /etc/os-release 2>$null
                                if ($LASTEXITCODE -eq 0 -and $osRel) {
                                  if ($osRel -match 'PRETTY_NAME="([^"]+)"') { $distName = $matches[1] }
                                  elseif ($osRel -match 'NAME="([^"]+)"') { $distName = $matches[1] }
                                }
                            }

                            $instances += [PSCustomObject]@{
                                Name = $name
                                State = $state
                                Version = $version
                                InstallPath = $basePath
                                IsDefault = $isDefault
                                Size = $size
                                Distribution = $distName
                                LastAccessed = $lastAccessed
                            }
                        } catch {
                            # Skip problematic instances
                            continue
                        }
                    }
                }
                
                $instances | ConvertTo-Json -Depth 3
                """;

            // Add timeout to prevent hanging
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(QuickOperationTimeoutSeconds));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var result = await _powerShellService.ExecuteScriptAsync(script, combinedCts.Token);

            if (string.IsNullOrWhiteSpace(result) || result == "null")
            {
                _logger.LogInformation("No WSL instances found");
                return [];
            }

            var instances = System.Text.Json.JsonSerializer.Deserialize<List<WslInstance>>(result);
            return instances ?? [];
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("WSL instance retrieval timed out or was canceled");
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve WSL instances");
            // Return empty list instead of throwing to prevent app startup failure
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task InstallInstanceAsync(InstallOptions options, IProgress<(double Percentage, string Message)>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.InstanceName))
            throw new ArgumentException("Instance name is required", nameof(options));

        if (string.IsNullOrWhiteSpace(options.InstallPath))
            throw new ArgumentException("Install path is required", nameof(options));

        try
        {
            _logger.LogInformation("===== INSTALLATION STARTED =====");
            _logger.LogInformation("Installing WSL instance '{InstanceName}' to '{InstallPath}'", 
                options.InstanceName, options.InstallPath);
            _logger.LogInformation("Distribution: {DistroName}, Package URL: {Url}", 
                options.Package?.Name ?? "Unknown", options.Package?.DownloadUrl ?? "None");
            _logger.LogInformation("Use Local Cache: {UseCache}, Username: {Username}", 
                options.UseLocalCache, options.Username ?? "root");

            progress?.Report((5, "Checking if instance already exists..."));

            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            // Check if instance name already exists
            _logger.LogDebug("Checking if instance '{InstanceName}' already exists...", options.InstanceName);
            var instances = await GetInstancesAsync(cancellationToken);
            if (instances.Any(i => i.Name == options.InstanceName))
            {
                _logger.LogError("Installation failed: Instance name '{InstanceName}' already exists", options.InstanceName);
                throw new InvalidOperationException(
                    $"An instance with the name \"{options.InstanceName}\" already exists. Please choose a different name.");
            }
            _logger.LogDebug("Instance name '{InstanceName}' is available", options.InstanceName);

            progress?.Report((10, "Preparing installation..."));
            cancellationToken.ThrowIfCancellationRequested();

            // Create instance-specific installation path by appending instance name to base path
            // This prevents conflicts when multiple instances are installed to the same base directory
            var actualInstallPath = Path.Combine(options.InstallPath, options.InstanceName);
            var escapedPath = EscapePowerShellString(actualInstallPath);

            progress?.Report((15, "Creating installation directory..."));
            cancellationToken.ThrowIfCancellationRequested();

            // Create installation directory
            _logger.LogDebug("Creating installation directory: {Path}", actualInstallPath);
            var createDirScript = 
                "$ProgressPreference = 'SilentlyContinue'; " +
                $"if (-not (Test-Path {escapedPath})) {{ New-Item -ItemType Directory -Path {escapedPath} -Force | Out-Null }}; " +
                "'success'";
            await _powerShellService.ExecuteScriptAsync(createDirScript, cancellationToken);
            _logger.LogDebug("Installation directory created successfully");

            progress?.Report((20, "Preparing distribution package..."));
            cancellationToken.ThrowIfCancellationRequested();

            // Prepare module call parameters
            var moduleParams = new Dictionary<string, object>
            {
                ["DistroName"] = options.Package?.Name ?? "Ubuntu",
                ["InstallPath"] = actualInstallPath,  // Use instance-specific path
                ["InstanceName"] = options.InstanceName,
                ["AutoDownload"] = true  // Enable auto-download if package not found in cache
            };

            // Add optional parameters
            if (!string.IsNullOrWhiteSpace(options.Username) && options.Username != "root")
            {
                moduleParams["Username"] = options.Username;
                if (!string.IsNullOrWhiteSpace(options.Password))
                {
                    moduleParams["Password"] = "***"; // Don't log actual password
                }
            }

            if (options.InitCommands != null && options.InitCommands.Count > 0)
            {
                moduleParams["InitCommands"] = options.InitCommands;
            }

            _logger.LogInformation("Module parameters: {@Parameters}", moduleParams);

            progress?.Report((30, "Calling PowerShell module for installation..."));
            cancellationToken.ThrowIfCancellationRequested();

            // Create a progress tracker wrapper to map module progress to our progress scale
            var progressReporter = new Progress<double>(p =>
            {
                // Map module progress (0-100) to our scale (30-95)
                var scaledProgress = 30 + (p * 0.65);
                progress?.Report((scaledProgress, $"Installing ({scaledProgress:F0}%)..."));
                _logger.LogInformation("Install progress: {Percentage}%", (int)scaledProgress);
            });

            _logger.LogInformation("Calling Install-DistroNexusInstance cmdlet...");

            // Try using PowerShell module Cmdlet first
            var moduleResult = await _powerShellService.ExecuteModuleCmdletAsync(
                "Install-DistroNexusInstance",
                moduleParams,
                new ModuleCallOptions
                {
                    TimeoutSeconds = VeryLongOperationTimeoutSeconds, // 5 minutes for long download+import
                    ParseAsJson = false,
                    UseModuleFallback = false, // Don't use fallback for Install as it's complex
                    ProgressTracker = progressReporter
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation("Module cmdlet execution completed. Success: {Success}, UsedModule: {UsedModule}", 
                moduleResult.Success, moduleResult.UsedModule);

            if (!string.IsNullOrWhiteSpace(moduleResult.Output))
            {
                _logger.LogDebug("Module output: {Output}", moduleResult.Output);
            }

            if (!string.IsNullOrWhiteSpace(moduleResult.Error))
            {
                _logger.LogError("Module error output: {Error}", moduleResult.Error);
            }

            if (moduleResult.Exception != null)
            {
                _logger.LogError(moduleResult.Exception, "Module execution exception");
            }

            if (moduleResult.Success)
            {
                _logger.LogInformation("===== INSTALLATION SUCCESSFUL =====");
                _logger.LogInformation("WSL instance '{InstanceName}' installed successfully using module", options.InstanceName);
                progress?.Report((100, "Installation complete"));
                return;
            }

            // If module installation fails, throw error (don't fall back to inline script)
            _logger.LogError("===== INSTALLATION FAILED =====");
            _logger.LogError("PowerShell module failed to install instance");
            _logger.LogError("Exit Code: {ExitCode}", moduleResult.ExitCode);
            _logger.LogError("Error Message: {Error}", moduleResult.Error ?? "<empty>");
            _logger.LogError("Output: {Output}", moduleResult.Output ?? "<empty>");
            _logger.LogError("Used Module: {UsedModule}", moduleResult.UsedModule);

            var errorMsg = !string.IsNullOrWhiteSpace(moduleResult.Error) 
                ? moduleResult.Error 
                : "Failed to install WSL distribution using PowerShell module. Please check the error logs and try again.";

            throw new InvalidOperationException(errorMsg, moduleResult.Exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "===== INSTALLATION EXCEPTION =====");
            _logger.LogError(ex, "Failed to install WSL instance '{InstanceName}'. Exception Type: {ExceptionType}", 
                options.InstanceName, ex.GetType().Name);
            _logger.LogError("Exception Message: {Message}", ex.Message);
            _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);

            if (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException, "Inner Exception: {InnerMessage}", ex.InnerException.Message);
            }

            // Extract user-friendly error message
            var friendlyMessage = ExtractUserFriendlyError(ex.Message);
            throw new InvalidOperationException(friendlyMessage, ex);
        }
    }

    /// <summary>
    /// Extracts a user-friendly error message from technical error output.
    /// </summary>
    private static string ExtractUserFriendlyError(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            return "Installation failed with an unknown error.";

        // Remove CLIXML tags
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            errorMessage, 
            @"#< CLIXML.*?<Objs.*?</Objs>", 
            "", 
            System.Text.RegularExpressions.RegexOptions.Singleline);

        // Check for common error patterns
        if (errorMessage.Contains("already in use", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return "The installation location or instance name is already in use. Please choose a different name or location.";
        }

        if (errorMessage.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("permission", StringComparison.OrdinalIgnoreCase))
        {
            return "Access denied. Please ensure you have administrator privileges and the installation path is writable.";
        }

        if (errorMessage.Contains("network", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("download", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("404", StringComparison.OrdinalIgnoreCase))
        {
            return "Failed to download the distribution. Please check your internet connection and try again.";
        }

        if (errorMessage.Contains("disk", StringComparison.OrdinalIgnoreCase) ||
            errorMessage.Contains("space", StringComparison.OrdinalIgnoreCase))
        {
            return "Insufficient disk space. Please free up some space and try again.";
        }

        if (errorMessage.Contains("WSL", StringComparison.OrdinalIgnoreCase))
        {
            // Already contains WSL in the message, return cleaned version
            return cleaned.Trim();
        }

        // Default: return first line or first 200 characters
        var firstLine = cleaned.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstLine) && firstLine.Length < 200)
        {
            return firstLine.Trim();
        }

        return cleaned.Length > 200 
            ? cleaned[..200].Trim() + "..." 
            : cleaned.Trim();
    }

    /// <inheritdoc/>
    public async Task<bool> StartInstanceAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName);

        try
        {
            _logger.LogInformation("Starting WSL instance '{InstanceName}'", instanceName);

            // Use PowerShell module Cmdlet first
            var moduleResult = await _powerShellService.ExecuteModuleCmdletAsync(
                "Start-DistroNexusInstance",
                new Dictionary<string, object> { ["Name"] = instanceName },
                new ModuleCallOptions
                {
                    TimeoutSeconds = NormalOperationTimeoutSeconds,
                    ParseAsJson = false,
                    UseModuleFallback = _useModuleFallback
                },
                cancellationToken: cancellationToken);

            if (moduleResult.Success)
            {
                _logger.LogInformation("WSL instance '{InstanceName}' started successfully using module", instanceName);
                return true;
            }

            // Fallback to inline script if module failed
            _logger.LogWarning("Module execution failed for StartInstanceAsync, falling back to inline script");
            var escapedName = EscapePowerShellString(instanceName);
            var script = 
                $"$result = wsl --distribution {escapedName} -- echo 'started' 2>&1; " +
                $"$exitCode = $LASTEXITCODE; " +
                $"if ($exitCode -ne 0) {{ throw \"Failed to start WSL instance: $result\" }}; " +
                "'success'";

            await _powerShellService.ExecuteScriptAsync(script, cancellationToken);

            _logger.LogInformation("WSL instance '{InstanceName}' started successfully using fallback script", instanceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WSL instance '{InstanceName}'", instanceName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StartInstanceWithKeepAliveAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName);

        try
        {
            _logger.LogInformation("Starting WSL instance '{InstanceName}' with keep-alive process", instanceName);

            // Start the keep-alive process directly from C# (not through PowerShell)
            // This ensures the background process actually starts
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = $"-d {instanceName} --exec sleep infinity",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            var keepAliveProcess = Process.Start(processStartInfo);
            if (keepAliveProcess == null)
            {
                throw new InvalidOperationException($"Failed to start keep-alive process for instance '{instanceName}'");
            }

            _logger.LogDebug("Keep-alive process started with PID {ProcessId}", keepAliveProcess.Id);

            // Wait for instance to fully start
            await Task.Delay(1500, cancellationToken);

            // Verify instance is running
            var instances = await GetInstancesAsync(cancellationToken);
            var instance = instances.FirstOrDefault(i => i.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase));

            if (instance == null || instance.State != "Running")
            {
                _logger.LogWarning("Instance '{InstanceName}' is not in running state after start attempt", instanceName);
                return false;
            }

            _logger.LogInformation("WSL instance '{InstanceName}' started successfully with keep-alive process", instanceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WSL instance '{InstanceName}' with keep-alive", instanceName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StopInstanceAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName);

        try
        {
            _logger.LogInformation("Stopping WSL instance '{InstanceName}'", instanceName);

            // Use PowerShell module Cmdlet first
            var moduleResult = await _powerShellService.ExecuteModuleCmdletAsync(
                "Stop-DistroNexusInstance",
                new Dictionary<string, object> { ["Name"] = instanceName },
                new ModuleCallOptions
                {
                    TimeoutSeconds = NormalOperationTimeoutSeconds,
                    ParseAsJson = false,
                    UseModuleFallback = _useModuleFallback
                },
                cancellationToken: cancellationToken);

            if (moduleResult.Success)
            {
                _logger.LogInformation("WSL instance '{InstanceName}' stopped successfully using module", instanceName);
                return true;
            }

            // Fallback to inline script if module failed
            _logger.LogWarning("Module execution failed for StopInstanceAsync, falling back to inline script");
            var escapedName = EscapePowerShellString(instanceName);
            var script = 
                $"$result = wsl --terminate {escapedName} 2>&1; " +
                $"$exitCode = $LASTEXITCODE; " +
                $"if ($exitCode -ne 0) {{ throw \"Failed to stop WSL instance: $result\" }}; " +
                "'success'";

            await _powerShellService.ExecuteScriptAsync(script, cancellationToken);

            _logger.LogInformation("WSL instance '{InstanceName}' stopped successfully using fallback script", instanceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop WSL instance '{InstanceName}'", instanceName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveInstanceAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName);

        try
        {
            _logger.LogInformation("Removing WSL instance '{InstanceName}'", instanceName);

            // Use PowerShell module Cmdlet first
            var moduleResult = await _powerShellService.ExecuteModuleCmdletAsync(
                "Remove-DistroNexusInstance",
                new Dictionary<string, object> { ["Name"] = instanceName },
                new ModuleCallOptions
                {
                    TimeoutSeconds = NormalOperationTimeoutSeconds,
                    ParseAsJson = false,
                    UseModuleFallback = _useModuleFallback
                },
                cancellationToken: cancellationToken);

            if (moduleResult.Success)
            {
                _logger.LogInformation("WSL instance '{InstanceName}' removed successfully using module", instanceName);
                return true;
            }

            // Fallback to inline script if module failed
            _logger.LogWarning("Module execution failed for RemoveInstanceAsync, falling back to inline script");
            var escapedName = EscapePowerShellString(instanceName);
            var script = 
                $"$result = wsl --unregister {escapedName} 2>&1; " +
                $"$exitCode = $LASTEXITCODE; " +
                $"if ($exitCode -ne 0) {{ throw \"Failed to remove WSL instance: $result\" }}; " +
                "'success'";

            await _powerShellService.ExecuteScriptAsync(script, cancellationToken);

            _logger.LogInformation("WSL instance '{InstanceName}' removed successfully using fallback script", instanceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove WSL instance '{InstanceName}'", instanceName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task MoveInstanceAsync(string instanceName, string newPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName);
        ArgumentNullException.ThrowIfNull(newPath);

        try
        {
            _logger.LogInformation("Moving WSL instance '{InstanceName}' to '{NewPath}'", instanceName, newPath);

            // Try using PowerShell module Cmdlet first
            var moduleParams = new Dictionary<string, object>
            {
                ["Name"] = instanceName,
                ["DestinationPath"] = newPath
            };

            // Create a wrapper to track progress
            var progressReporter = new Progress<double>(p =>
            {
                progress?.Report(p);
                _logger.LogInformation("Move progress: {Percentage}%", (int)p);
            });

            var moduleResult = await _powerShellService.ExecuteModuleCmdletAsync(
                "Move-DistroNexusInstance",
                moduleParams,
                new ModuleCallOptions
                {
                    TimeoutSeconds = LongOperationTimeoutSeconds,
                    ParseAsJson = false,
                    UseModuleFallback = _useModuleFallback,
                    ProgressTracker = progressReporter
                },
                cancellationToken: cancellationToken);

            if (moduleResult.Success)
            {
                _logger.LogInformation("WSL instance '{InstanceName}' moved successfully using module", instanceName);
                progress?.Report(100);
                return;
            }

            // Fallback to inline script if module failed
            _logger.LogWarning("Module execution failed for MoveInstanceAsync, falling back to inline script");
            var escapedName = EscapePowerShellString(instanceName);
            var escapedPath = EscapePowerShellString(newPath);
            var tempExportPath = EscapePowerShellString(Path.Combine(Path.GetTempPath(), $"{instanceName}_export.tar"));
            
            progress?.Report(10);

            // Export, unregister, and import to new location
            // Note: WSL outputs UTF-16 text, so we clean null characters
            var script = 
                $"$result = wsl --export {escapedName} {tempExportPath} 2>&1; " +
                $"$exitCode = $LASTEXITCODE; " +
                $"if ($exitCode -ne 0) {{ throw \"Export failed: $result\" }}; " +
                $"if (-not (Test-Path {escapedPath})) {{ New-Item -ItemType Directory -Path {escapedPath} -Force | Out-Null }}; " +
                $"$result = wsl --unregister {escapedName} 2>&1; " +
                $"$exitCode = $LASTEXITCODE; " +
                $"if ($exitCode -ne 0) {{ Remove-Item {tempExportPath} -Force -ErrorAction SilentlyContinue; throw \"Unregister failed: $result\" }}; " +
                $"$result = wsl --import {escapedName} {escapedPath} {tempExportPath} 2>&1; " +
                $"$exitCode = $LASTEXITCODE; " +
                $"Remove-Item {tempExportPath} -Force -ErrorAction SilentlyContinue; " +
                $"if ($exitCode -ne 0) {{ throw \"Import failed: $result\" }}; " +
                "'success'";

            progress?.Report(50);
            await _powerShellService.ExecuteScriptAsync(script, cancellationToken);

            _logger.LogInformation("WSL instance '{InstanceName}' moved successfully using fallback script", instanceName);
            progress?.Report(100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to move WSL instance '{InstanceName}'", instanceName);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task RenameInstanceAsync(string oldName, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(oldName);
        ArgumentNullException.ThrowIfNull(newName);

        try
        {
            _logger.LogInformation("Renaming WSL instance '{OldName}' to '{NewName}'", oldName, newName);

            // Try using PowerShell module Cmdlet first
            var moduleParams = new Dictionary<string, object>
            {
                ["OldName"] = oldName,
                ["NewName"] = newName
            };

            var moduleResult = await _powerShellService.ExecuteModuleCmdletAsync(
                "Rename-DistroNexusInstance",
                moduleParams,
                new ModuleCallOptions
                {
                    TimeoutSeconds = LongOperationTimeoutSeconds,
                    ParseAsJson = false,
                    UseModuleFallback = _useModuleFallback
                },
                cancellationToken: cancellationToken);

            if (moduleResult.Success)
            {
                _logger.LogInformation("WSL instance {OldName} renamed successfully using module", oldName);
                return;
            }

            // Fallback to inline script if module failed
            _logger.LogWarning("Module execution failed for RenameInstanceAsync, falling back to inline script");
            var escapedOldName = EscapePowerShellString(oldName);
            var escapedNewName = EscapePowerShellString(newName);
            var tempExportPath = EscapePowerShellString(Path.Combine(Path.GetTempPath(), $"{oldName}_rename.tar"));
            
            // First get the install path from registry, then export/unregister/import
            // Note: WSL outputs UTF-16 text, so we clean null characters
            var script = 
                "$lxssPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Lxss'; " +
                "$installPath = $null; " +
                $"Get-ChildItem -Path $lxssPath | ForEach-Object {{ $props = Get-ItemProperty -Path $_.PSPath; if ($props.DistributionName -eq {escapedOldName}) {{ $installPath = $props.BasePath }} }}; " +
                $"if (-not $installPath) {{ throw 'Instance not found: {oldName}' }}; " +
                $"$result = wsl --export {escapedOldName} {tempExportPath} 2>&1; " +
                $"$exitCode = $LASTEXITCODE; " +
                $"if ($exitCode -ne 0) {{ throw \"Export failed: $result\" }}; " +
                $"$result = wsl --unregister {escapedOldName} 2>&1; " +
                $"$exitCode = $LASTEXITCODE; " +
                $"if ($exitCode -ne 0) {{ Remove-Item {tempExportPath} -Force -ErrorAction SilentlyContinue; throw \"Unregister failed: $result\" }}; " +
                $"$result = wsl --import {escapedNewName} $installPath {tempExportPath} 2>&1; " +
                $"$exitCode = $LASTEXITCODE; " +
                $"Remove-Item {tempExportPath} -Force -ErrorAction SilentlyContinue; " +
                $"if ($exitCode -ne 0) {{ throw \"Import failed: $result\" }}; " +
                "'success'";

            await _powerShellService.ExecuteScriptAsync(script, cancellationToken);

            _logger.LogInformation("WSL instance renamed successfully using fallback script");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename WSL instance");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task SetCredentialsAsync(string instanceName, string username, string password, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        try
        {
            _logger.LogInformation("Setting credentials for WSL instance '{InstanceName}'", instanceName);

            // Try using PowerShell module Cmdlet first
            var moduleParams = new Dictionary<string, object>
            {
                ["Name"] = instanceName,
                ["Username"] = username,
                ["Password"] = password
            };

            var moduleResult = await _powerShellService.ExecuteModuleCmdletAsync(
                "Set-DistroNexusCredential",
                moduleParams,
                new ModuleCallOptions
                {
                    TimeoutSeconds = NormalOperationTimeoutSeconds,
                    ParseAsJson = false,
                    UseModuleFallback = _useModuleFallback
                },
                cancellationToken: cancellationToken);

            if (moduleResult.Success)
            {
                _logger.LogInformation("Credentials set successfully for WSL instance '{InstanceName}' using module", instanceName);
                return;
            }

            // Fallback to inline script if module failed
            _logger.LogWarning("Module execution failed for SetCredentialsAsync, falling back to inline script");
            var escapedName = EscapePowerShellString(instanceName);
            var escapedUsername = EscapePowerShellString(username);
            var escapedPassword = EscapePowerShellString(password);
            
            var script = 
                $"wsl --distribution {escapedName} -- bash -c \"id -u {escapedUsername} 2>/dev/null || useradd -m -s /bin/bash {escapedUsername}\"; " +
                $"if ($LASTEXITCODE -ne 0) {{ throw \"Failed to create user\" }}; " +
                $"wsl --distribution {escapedName} -- bash -c \"echo {escapedPassword} | chpasswd\"; " +
                $"if ($LASTEXITCODE -ne 0) {{ throw \"Failed to set password\" }}; " +
                $"wsl --distribution {escapedName} -- bash -c \"echo -e '[user]\\ndefault={escapedUsername}' > /etc/wsl.conf\"; " +
                $"if ($LASTEXITCODE -ne 0) {{ throw \"Failed to configure default user\" }}; " +
                "'success'";

            await _powerShellService.ExecuteScriptAsync(script, cancellationToken);

            _logger.LogInformation("Credentials set successfully for WSL instance '{InstanceName}' using fallback script", instanceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set credentials for WSL instance '{InstanceName}'", instanceName);
            throw;
        }
    }

    /// <summary>
    /// Downloads a distribution package to a temporary file.
    /// </summary>
    private async Task<string> DownloadPackageAsync(
        InstallOptions options, 
        IProgress<(double Percentage, string Message)>? progress,
        CancellationToken cancellationToken)
    {
        var downloadUrl = options.Package?.DownloadUrl ?? string.Empty;
        if (string.IsNullOrEmpty(downloadUrl))
            throw new ArgumentException("Download URL is required when UseLocalCache is false or no cached package is available.");

        var tempFile = Path.Combine(Path.GetTempPath(), $"{options.InstanceName}_{Guid.NewGuid():N}.tar.gz");
        var escapedTempFile = EscapePowerShellString(tempFile);
        var escapedUrl = EscapePowerShellString(downloadUrl);
        
        progress?.Report((35, "Downloading distribution package..."));
        
        var downloadScript = 
            "$ProgressPreference = 'SilentlyContinue'; " +
            "$ErrorActionPreference = 'Stop'; " +
            $"try {{ " +
            $"  Invoke-WebRequest -Uri {escapedUrl} -OutFile {escapedTempFile} -UseBasicParsing; " +
            $"  if (-not (Test-Path {escapedTempFile})) {{ throw 'Download failed: File was not created' }}; " +
            $"  {escapedTempFile} " +
            $"}} catch {{ " +
            $"  throw \"Download failed: $($_.Exception.Message)\" " +
            $"}}";
        
        try
        {
            var downloadedFile = await _powerShellService.ExecuteScriptAsync(downloadScript, cancellationToken);
            progress?.Report((55, "Package downloaded successfully"));
            return tempFile;
        }
        catch (Exception ex)
        {
            // Check if it was cancelled
            cancellationToken.ThrowIfCancellationRequested();
            
            throw new InvalidOperationException(
                $"Failed to download distribution package from {options.Package?.Name}. " +
                $"Please check your internet connection and try again.", ex);
        }
    }

    /// <summary>
    /// Escapes a string for safe use in PowerShell commands.
    /// </summary>
    private static string EscapePowerShellString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "''";
        
        // Escape single quotes by doubling them and wrap in single quotes
        return "'" + input.Replace("'", "''") + "'";
    }

    /// <inheritdoc/>
    public async Task<long> GetInstanceDiskSizeAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName);

        try
        {
            _logger.LogInformation("Getting disk size for WSL instance '{InstanceName}'", instanceName);

            // Try using PowerShell module Cmdlet first
            var moduleParams = new Dictionary<string, object>
            {
                ["Name"] = instanceName
            };

            var moduleResult = await _powerShellService.ExecuteModuleCmdletAsync(
                "Get-DistroNexusDiskSize",
                moduleParams,
                new ModuleCallOptions
                {
                    TimeoutSeconds = QuickOperationTimeoutSeconds,
                    ParseAsJson = false,
                    UseModuleFallback = _useModuleFallback
                },
                cancellationToken: cancellationToken);

            if (moduleResult.Success)
            {
                // Try to parse the size from module output
                if (long.TryParse(moduleResult.Output?.Trim(), out var moduleSize))
                {
                    _logger.LogInformation("Disk size for '{InstanceName}': {Size} bytes (from module)", instanceName, moduleSize);
                    return moduleSize;
                }
            }

            // Fallback to inline script if module failed
            _logger.LogWarning("Module execution failed for GetInstanceDiskSizeAsync, falling back to inline script");

            // Get the instance's install path from registry
            var instances = await GetInstancesAsync(cancellationToken);
            var instance = instances.FirstOrDefault(i => i.Name.Equals(instanceName, StringComparison.OrdinalIgnoreCase));
            
            if (instance == null)
            {
                _logger.LogWarning("Instance '{InstanceName}' not found", instanceName);
                return 0;
            }

            var basePath = instance.InstallPath;
            if (string.IsNullOrEmpty(basePath))
            {
                _logger.LogWarning("No install path found for instance '{InstanceName}'", instanceName);
                return 0;
            }

            // WARNING: Accessing VHDX file may auto-start a stopped instance!
            // This method should only be called when the instance is already running,
            // or when the user explicitly requests disk size information.
            var escapedPath = EscapePowerShellString(basePath);
            var script = $@"
                $basePath = {escapedPath}
                $vhdxPath = Join-Path $basePath 'ext4.vhdx'
                
                if (Test-Path $vhdxPath) {{
                    $fileInfo = Get-Item $vhdxPath -ErrorAction Stop
                    return $fileInfo.Length
                }} else {{
                    return 0
                }}
            ";

            var result = await _powerShellService.ExecuteScriptAsync(script, cancellationToken);
            
            if (long.TryParse(result?.Trim(), out var size))
            {
                _logger.LogInformation("Disk size for '{InstanceName}': {Size} bytes (from fallback)", instanceName, size);
                return size;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get disk size for instance '{InstanceName}'", instanceName);
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<WslInstance?> ForceRefreshInstanceAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Force refreshing instance {InstanceName}", instanceName);

            // First, get current state to check if instance is running
            var currentInstancesResult = await _powerShellService.ExecuteModuleCmdletAsync(
                "Get-DistroNexusInstance",
                parameters: new Dictionary<string, object> 
                { 
                    { "Name", instanceName },
                    { "SkipDiskSize", true },
                    { "ForceUpdate", true }
                },
                options: new ModuleCallOptions
                {
                    TimeoutSeconds = QuickOperationTimeoutSeconds,
                    ParseAsJson = true,
                    UseModuleFallback = _useModuleFallback
                },
                cancellationToken: cancellationToken);

            if (!currentInstancesResult.Success || currentInstancesResult.ParsedObjects == null || currentInstancesResult.ParsedObjects.Count == 0)
            {
                _logger.LogWarning("Force refresh failed: instance {InstanceName} not found", instanceName);
                return null;
            }

            var currentInstances = ParseInstancesFromModule(currentInstancesResult.ParsedObjects);
            var currentInstance = currentInstances.FirstOrDefault();
            
            if (currentInstance == null)
            {
                _logger.LogWarning("Force refresh failed: could not parse instance {InstanceName}", instanceName);
                return null;
            }

            // Only calculate disk size if instance is currently running
            // This prevents auto-starting stopped instances when accessing VHDX files
            if (currentInstance.State == "Running")
            {
                _logger.LogInformation("Instance {InstanceName} is running, calculating disk size", instanceName);
                
                var moduleResult = await _powerShellService.ExecuteModuleCmdletAsync(
                    "Get-DistroNexusInstance",
                    parameters: new Dictionary<string, object> 
                    { 
                        { "Name", instanceName },
                        { "ForceUpdate", true }
                        // Do NOT use SkipDiskSize - we want to calculate it
                    },
                    options: new ModuleCallOptions
                    {
                        TimeoutSeconds = NormalOperationTimeoutSeconds,
                        ParseAsJson = true,
                        UseModuleFallback = _useModuleFallback
                    },
                    cancellationToken: cancellationToken);

                if (moduleResult.Success && moduleResult.ParsedObjects != null && moduleResult.ParsedObjects.Count > 0)
                {
                    var instances = ParseInstancesFromModule(moduleResult.ParsedObjects);
                    if (instances.Count > 0)
                    {
                        var refreshedInstance = instances.FirstOrDefault();
                        _logger.LogInformation("Force refresh completed for {InstanceName}, DiskSize: {Size} bytes", instanceName, refreshedInstance?.Size);
                        return refreshedInstance;
                    }
                }
            }
            else
            {
                _logger.LogInformation("Instance {InstanceName} is {State}, skipping disk size calculation to prevent auto-start", 
                    instanceName, currentInstance.State);
            }

            // Return current instance data (without updated disk size if stopped)
            return currentInstance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to force refresh instance {InstanceName}", instanceName);
            return null;
        }
    }
}
