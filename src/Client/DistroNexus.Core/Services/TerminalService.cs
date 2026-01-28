using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Service for launching terminals and file explorers through PowerShell.
/// </summary>
public class TerminalService : ITerminalService
{
    private readonly IPowerShellService _powerShell;
    private readonly ILogger<TerminalService> _logger;

    public TerminalService(IPowerShellService powerShell, ILogger<TerminalService> logger)
    {
        _powerShell = powerShell;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> OpenTerminalAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var escapedName = EscapePowerShellString(instanceName);
            
            var script = $@"
                $distroName = '{escapedName}'
                
                # Try Windows Terminal first
                if (Get-Command wt.exe -ErrorAction SilentlyContinue) {{
                    Start-Process wt.exe -ArgumentList '-w', '0', 'wsl', '-d', $distroName
                    return $true
                }}
                
                # Fallback to cmd.exe
                if (Get-Command cmd.exe -ErrorAction SilentlyContinue) {{
                    Start-Process cmd.exe -ArgumentList '/k', 'wsl', '-d', $distroName
                    return $true
                }}
                
                # No terminal available
                throw 'No terminal application available'
            ";

            var result = await _powerShell.ExecuteScriptAsync(script, cancellationToken);
            
            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Successfully opened terminal for instance: {InstanceName}", instanceName);
                return true;
            }
            
            _logger.LogWarning("Failed to open terminal for instance: {InstanceName}. Exit code: {ExitCode}", 
                instanceName, result.ExitCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening terminal for instance: {InstanceName}", instanceName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> OpenTerminalInDirectoryAsync(string instanceName, string workingDirectory, CancellationToken cancellationToken = default)
    {
        try
        {
            var escapedName = EscapePowerShellString(instanceName);
            var escapedDir = EscapePowerShellString(workingDirectory);
            
            var script = $@"
                $distroName = '{escapedName}'
                $workDir = '{escapedDir}'
                
                # Try Windows Terminal first
                if (Get-Command wt.exe -ErrorAction SilentlyContinue) {{
                    Start-Process wt.exe -ArgumentList '-w', '0', 'wsl', '-d', $distroName, '--cd', $workDir
                    return $true
                }}
                
                # Fallback to cmd.exe (cd to directory after launching)
                if (Get-Command cmd.exe -ErrorAction SilentlyContinue) {{
                    $args = ""/k wsl -d $distroName --cd $workDir""
                    Start-Process cmd.exe -ArgumentList $args
                    return $true
                }}
                
                throw 'No terminal application available'
            ";

            var result = await _powerShell.ExecuteScriptAsync(script, cancellationToken);
            
            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Successfully opened terminal for instance: {InstanceName} in directory: {WorkingDirectory}", 
                    instanceName, workingDirectory);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening terminal in directory for instance: {InstanceName}", instanceName);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> OpenFileExplorerAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var escapedPath = EscapePowerShellString(folderPath);
            
            var script = $@"
                $path = '{escapedPath}'
                
                if (Test-Path $path) {{
                    Start-Process explorer.exe -ArgumentList $path
                    return $true
                }} else {{
                    throw ""Path does not exist: $path""
                }}
            ";

            var result = await _powerShell.ExecuteScriptAsync(script, cancellationToken);
            
            if (result.ExitCode == 0)
            {
                _logger.LogInformation("Successfully opened file explorer for path: {FolderPath}", folderPath);
                return true;
            }
            
            _logger.LogWarning("Failed to open file explorer for path: {FolderPath}", folderPath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening file explorer for path: {FolderPath}", folderPath);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetAvailableTerminalsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var script = @"
                $terminals = @()
                
                if (Get-Command wt.exe -ErrorAction SilentlyContinue) {
                    $terminals += 'Windows Terminal'
                }
                
                if (Get-Command cmd.exe -ErrorAction SilentlyContinue) {
                    $terminals += 'Command Prompt'
                }
                
                if (Get-Command powershell.exe -ErrorAction SilentlyContinue) {
                    $terminals += 'PowerShell'
                }
                
                $terminals | ConvertTo-Json
            ";

            var result = await _powerShell.ExecuteScriptAsync(script, cancellationToken);
            
            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
            {
                var terminals = System.Text.Json.JsonSerializer.Deserialize<List<string>>(result.Output);
                return terminals ?? new List<string>();
            }
            
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available terminals");
            return new List<string>();
        }
    }

    /// <summary>
    /// Escapes special characters in PowerShell strings to prevent injection attacks.
    /// </summary>
    private string EscapePowerShellString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input
            .Replace("'", "''")           // Single quote escape
            .Replace("`", "``")           // Backtick escape
            .Replace("$", "`$")           // Dollar sign escape
            .Replace("\n", "`n")          // Newline escape
            .Replace("\r", "`r");         // Carriage return escape
    }
}
