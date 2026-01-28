using System.Text.RegularExpressions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Service for managing WSL instances using inline PowerShell scripts.
/// </summary>
public partial class WslManagerService : IWslManagerService
{
    private readonly IPowerShellService _powerShellService;
    private readonly ICatalogService _catalogService;
    private readonly ILogger<WslManagerService> _logger;

    private const string LxssRegistryPath = @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss";

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
        try
        {
            _logger.LogInformation("Retrieving WSL instances");

            // Get WSL status from wsl.exe --list --verbose (no interpolation needed)
            const string script = """
                $instances = @()
                $defaultDistro = $null
                
                # Get running state and version from wsl --list --verbose
                $wslOutput = wsl --list --verbose 2>&1
                $wslStatus = @{}
                
                if ($wslOutput -and $LASTEXITCODE -eq 0) {
                    foreach ($line in $wslOutput) {
                        $cleanLine = ($line -replace "`0", "").Trim()
                        if ([string]::IsNullOrWhiteSpace($cleanLine) -or $cleanLine -match "^NAME\s+STATE") { continue }
                        
                        $isDefault = $cleanLine.StartsWith("*")
                        $cleanLine = $cleanLine.TrimStart("*").Trim()
                        $parts = $cleanLine -split "\s+" | Where-Object { $_ }
                        
                        if ($parts.Count -ge 3) {
                            $name = $parts[0]
                            $wslStatus[$name] = @{
                                State = $parts[1]
                                Version = [int]$parts[2]
                                IsDefault = $isDefault
                            }
                            if ($isDefault) { $defaultDistro = $name }
                        }
                    }
                }
                
                # Get installation paths from registry
                $lxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
                if (Test-Path $lxssPath) {
                    $keys = Get-ChildItem -Path $lxssPath -ErrorAction SilentlyContinue
                    foreach ($key in $keys) {
                        $props = Get-ItemProperty -Path $key.PSPath -ErrorAction SilentlyContinue
                        $name = $props.DistributionName
                        if (-not $name) { continue }
                        
                        $basePath = $props.BasePath
                        $size = 0
                        $lastAccessed = $null
                        
                        if ($basePath -and (Test-Path $basePath)) {
                            try {
                                $vhdxPath = Join-Path $basePath "ext4.vhdx"
                                if (Test-Path $vhdxPath) {
                                    $size = (Get-Item $vhdxPath).Length
                                    $lastAccessed = (Get-Item $vhdxPath).LastAccessTime.ToString("o")
                                }
                            } catch {}
                        }
                        
                        $state = "Stopped"
                        $version = 2
                        $isDefault = $false
                        
                        if ($wslStatus.ContainsKey($name)) {
                            $state = $wslStatus[$name].State
                            $version = $wslStatus[$name].Version
                            $isDefault = $wslStatus[$name].IsDefault
                        }
                        
                        $instances += [PSCustomObject]@{
                            Name = $name
                            State = $state
                            Version = $version
                            InstallPath = $basePath
                            IsDefault = $isDefault
                            Size = $size
                            Distribution = $name
                            LastAccessed = $lastAccessed
                        }
                    }
                }
                
                $instances | ConvertTo-Json -Depth 3
                """;

            var result = await _powerShellService.ExecuteScriptAsync(script, cancellationToken);

            if (string.IsNullOrWhiteSpace(result) || result == "null")
            {
                return [];
            }

            var instances = System.Text.Json.JsonSerializer.Deserialize<List<WslInstance>>(result);
            return instances ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve WSL instances");
            throw;
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
            _logger.LogInformation("Installing WSL instance '{InstanceName}' to '{InstallPath}'", 
                options.InstanceName, options.InstallPath);

            progress?.Report((5, "Checking if instance already exists..."));
            
            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            // Check if instance name already exists
            var checkScript = 
                "$ProgressPreference = 'SilentlyContinue'; " +
                $"$exists = (wsl --list --quiet 2>&1) -replace \"`0\", '' | Where-Object {{ $_.Trim() -eq '{options.InstanceName}' }}; " +
                $"if ($exists) {{ throw 'An instance with the name \"{options.InstanceName}\" already exists. Please choose a different name.' }}; " +
                "'ok'";
            
            await _powerShellService.ExecuteScriptAsync(checkScript, cancellationToken);

            progress?.Report((10, "Preparing installation..."));
            cancellationToken.ThrowIfCancellationRequested();

            var escapedPath = EscapePowerShellString(options.InstallPath);
            var downloadUrl = options.Package?.DownloadUrl ?? string.Empty;
            var escapedUrl = EscapePowerShellString(downloadUrl);
            
            progress?.Report((20, "Creating installation directory..."));
            cancellationToken.ThrowIfCancellationRequested();

            // Create installation directory
            var createDirScript = 
                "$ProgressPreference = 'SilentlyContinue'; " +
                $"if (-not (Test-Path {escapedPath})) {{ New-Item -ItemType Directory -Path {escapedPath} -Force | Out-Null }}; " +
                "'success'";
            await _powerShellService.ExecuteScriptAsync(createDirScript, cancellationToken);

            progress?.Report((30, "Preparing distribution package..."));
            cancellationToken.ThrowIfCancellationRequested();

            string packageFile;
            
            // Check for cached package first
            if (options.UseLocalCache && !string.IsNullOrEmpty(options.Package?.Id))
            {
                var cachePath = _catalogService.GetPackageCachePath();
                var cachedFile = Path.Combine(cachePath, $"{options.Package.Id}.tar.gz");
                
                if (File.Exists(cachedFile))
                {
                    _logger.LogInformation("Using cached package: {CachedFile}", cachedFile);
                    packageFile = cachedFile;
                    progress?.Report((40, "Using cached distribution package..."));
                }
                else
                {
                    _logger.LogInformation("Cached package not found, downloading from remote");
                    packageFile = await DownloadPackageAsync(options, progress, cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation("Downloading package from remote (cache disabled)");
                packageFile = await DownloadPackageAsync(options, progress, cancellationToken);
            }
                
                progress?.Report((60, "Importing distribution..."));
                cancellationToken.ThrowIfCancellationRequested();

                // Construct full installation path (WSL expects the full directory path, not the parent)
                var fullInstallPath = Path.Combine(options.InstallPath, options.InstanceName);
                var escapedFullPath = EscapePowerShellString(fullInstallPath);

                // Import the distribution
                var importScript = 
                    "$ProgressPreference = 'SilentlyContinue'; " +
                    "$ErrorActionPreference = 'Continue'; " +
                    $"$installPath = {escapedFullPath}; " +
                    $"$instanceName = '{options.InstanceName}'; " +
                    $"$tarFile = {EscapePowerShellString(packageFile)}; " +
                    // Capture WSL output and exit code
                    "$wslOutput = wsl --import $instanceName $installPath $tarFile 2>&1 | Out-String; " +
                    "$exitCode = $LASTEXITCODE; " +
                    // Clean up temp file
                    "Remove-Item $tarFile -Force -ErrorAction SilentlyContinue; " +
                    // Check result
                    "if ($exitCode -ne 0) { " +
                    "  $cleanOutput = $wslOutput -replace \"`0\", '' -replace \"`r\", '' -replace \"`n\", ' ' | Out-String; " +
                    "  $cleanOutput = $cleanOutput.Trim(); " +
                    "  if ([string]::IsNullOrWhiteSpace($cleanOutput)) { " +
                    "    throw 'WSL import failed with no error message. Please ensure WSL is properly installed and configured.'; " +
                    "  } else { " +
                    "    throw \"WSL import failed: $cleanOutput\"; " +
                    "  } " +
                    "} " +
                    "'success'";
                
                try
                {
                    await _powerShellService.ExecuteScriptAsync(importScript, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Check if it was cancelled
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Cleanup: try to unregister if import failed
                    try
                    {
                        await _powerShellService.ExecuteScriptAsync(
                            "$ProgressPreference = 'SilentlyContinue'; " +
                            $"wsl --unregister '{options.InstanceName}' 2>&1 | Out-Null", 
                            CancellationToken.None); // Don't use cancellation token for cleanup
                    }
                    catch { /* Ignore cleanup errors */ }
                    
                    // Re-throw with user-friendly message
                    var errorMessage = ExtractUserFriendlyError(ex.Message);
                    throw new InvalidOperationException(
                        $"Failed to import WSL distribution. {errorMessage}", ex);
                }
            }


            progress?.Report((80, "Configuring user..."));
            cancellationToken.ThrowIfCancellationRequested();

            // Set up default user if specified
            if (!string.IsNullOrWhiteSpace(options.Username) && options.Username != "root")
            {
                var userScript = 
                    "$ProgressPreference = 'SilentlyContinue'; " +
                    "$ErrorActionPreference = 'Continue'; " +
                    $"wsl --distribution '{options.InstanceName}' -- bash -c \"id -u {options.Username} 2>/dev/null || useradd -m -s /bin/bash {options.Username}\"; " +
                    $"wsl --distribution '{options.InstanceName}' -- bash -c \"echo -e '[user]\\ndefault={options.Username}' > /etc/wsl.conf\"";
                
                if (!string.IsNullOrWhiteSpace(options.Password))
                {
                    userScript += $"; wsl --distribution '{options.InstanceName}' -- bash -c \"echo '{options.Username}:{options.Password}' | chpasswd\"";
                }
                
                try
                {
                    await _powerShellService.ExecuteScriptAsync(userScript, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Check if it was cancelled
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    _logger.LogWarning(ex, "Failed to configure user, but instance was created successfully");
                    // Don't fail the entire installation if user configuration fails
                }
            }

            progress?.Report((90, "Finalizing installation..."));
            cancellationToken.ThrowIfCancellationRequested();

            // Execute initialization commands
            if (options.InitCommands != null && options.InitCommands.Count > 0)
            {
                progress?.Report((95, "Running initialization commands..."));
                
                foreach (var command in options.InitCommands)
                {
                    try
                    {
                        _logger.LogInformation("Running init command: {Command}", command);
                        
                        var initScript = 
                            "$ProgressPreference = 'SilentlyContinue'; " +
                            "$ErrorActionPreference = 'Continue'; " +
                            $"wsl --distribution '{options.InstanceName}' -- bash -c \"{EscapePowerShellString(command)}\"";
                        
                        var result = await _powerShellService.ExecuteScriptAsync(initScript, cancellationToken);
                        _logger.LogInformation("Init command completed: {Command}", command);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Init command failed: {Command}", command);
                        // Continue with other commands even if one fails
                    }
                }
            }

            _logger.LogInformation("WSL instance '{InstanceName}' installed successfully", options.InstanceName);
            
            progress?.Report((100, "Installation complete"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install WSL instance '{InstanceName}'", options.InstanceName);
            
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

            var escapedName = EscapePowerShellString(instanceName);
            var script = 
                $"$result = (wsl --distribution {escapedName} -- echo 'started' 2>&1) -replace \"`0\", ''; " +
                $"if ($LASTEXITCODE -ne 0) {{ throw \"Failed to start WSL instance: $result\" }}; " +
                "'success'";

            await _powerShellService.ExecuteScriptAsync(script, cancellationToken);

            _logger.LogInformation("WSL instance '{InstanceName}' started successfully", instanceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WSL instance '{InstanceName}'", instanceName);
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

            var escapedName = EscapePowerShellString(instanceName);
            var script = 
                $"$result = (wsl --terminate {escapedName} 2>&1) -replace \"`0\", ''; " +
                $"if ($LASTEXITCODE -ne 0) {{ throw \"Failed to stop WSL instance: $result\" }}; " +
                "'success'";

            await _powerShellService.ExecuteScriptAsync(script, cancellationToken);

            _logger.LogInformation("WSL instance '{InstanceName}' stopped successfully", instanceName);
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

            var escapedName = EscapePowerShellString(instanceName);
            var script = 
                $"$result = (wsl --unregister {escapedName} 2>&1) -replace \"`0\", ''; " +
                $"if ($LASTEXITCODE -ne 0) {{ throw \"Failed to remove WSL instance: $result\" }}; " +
                "'success'";

            await _powerShellService.ExecuteScriptAsync(script, cancellationToken);

            _logger.LogInformation("WSL instance '{InstanceName}' removed successfully", instanceName);
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

            var escapedName = EscapePowerShellString(instanceName);
            var escapedPath = EscapePowerShellString(newPath);
            var tempExportPath = EscapePowerShellString(Path.Combine(Path.GetTempPath(), $"{instanceName}_export.tar"));
            
            // Export, unregister, and import to new location
            // Note: WSL outputs UTF-16 text, so we clean null characters
            var script = 
                $"$result = (wsl --export {escapedName} {tempExportPath} 2>&1) -replace \"`0\", ''; " +
                $"if ($LASTEXITCODE -ne 0) {{ throw \"Export failed: $result\" }}; " +
                $"if (-not (Test-Path {escapedPath})) {{ New-Item -ItemType Directory -Path {escapedPath} -Force | Out-Null }}; " +
                $"$result = (wsl --unregister {escapedName} 2>&1) -replace \"`0\", ''; " +
                $"if ($LASTEXITCODE -ne 0) {{ Remove-Item {tempExportPath} -Force -ErrorAction SilentlyContinue; throw \"Unregister failed: $result\" }}; " +
                $"$result = (wsl --import {escapedName} {escapedPath} {tempExportPath} 2>&1) -replace \"`0\", ''; " +
                $"Remove-Item {tempExportPath} -Force -ErrorAction SilentlyContinue; " +
                $"if ($LASTEXITCODE -ne 0) {{ throw \"Import failed: $result\" }}; " +
                "'success'";

            await _powerShellService.ExecuteScriptAsync(script, cancellationToken);

            _logger.LogInformation("WSL instance '{InstanceName}' moved successfully", instanceName);
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
                $"$result = (wsl --export {escapedOldName} {tempExportPath} 2>&1) -replace \"`0\", ''; " +
                $"if ($LASTEXITCODE -ne 0) {{ throw \"Export failed: $result\" }}; " +
                $"$result = (wsl --unregister {escapedOldName} 2>&1) -replace \"`0\", ''; " +
                $"if ($LASTEXITCODE -ne 0) {{ Remove-Item {tempExportPath} -Force -ErrorAction SilentlyContinue; throw \"Unregister failed: $result\" }}; " +
                $"$result = (wsl --import {escapedNewName} $installPath {tempExportPath} 2>&1) -replace \"`0\", ''; " +
                $"Remove-Item {tempExportPath} -Force -ErrorAction SilentlyContinue; " +
                $"if ($LASTEXITCODE -ne 0) {{ throw \"Import failed: $result\" }}; " +
                "'success'";

            await _powerShellService.ExecuteScriptAsync(script, cancellationToken);

            _logger.LogInformation("WSL instance renamed successfully");
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

            var escapedName = EscapePowerShellString(instanceName);
            
            var script = 
                $"wsl --distribution {escapedName} -- bash -c \"id -u {username} 2>/dev/null || useradd -m -s /bin/bash {username}\"; " +
                $"wsl --distribution {escapedName} -- bash -c \"echo '{username}:{password}' | chpasswd\"; " +
                $"wsl --distribution {escapedName} -- bash -c \"echo -e '[user]\\ndefault={username}' > /etc/wsl.conf\"; " +
                "'success'";

            await _powerShellService.ExecuteScriptAsync(script, cancellationToken);

            _logger.LogInformation("Credentials set successfully for WSL instance '{InstanceName}'", instanceName);
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
}
