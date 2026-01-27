using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Service for executing PowerShell commands and scripts.
/// </summary>
public class PowerShellService : IPowerShellService, IDisposable
{
    private readonly ILogger<PowerShellService> _logger;
    private readonly Runspace _runspace;
    private bool _disposed;

    public PowerShellService(ILogger<PowerShellService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Create a runspace with minimal configuration to avoid snap-in loading issues
        var initialSessionState = InitialSessionState.CreateDefault();
        initialSessionState.ThrowOnRunspaceOpenError = true;
        
        _runspace = RunspaceFactory.CreateRunspace(initialSessionState);
        _runspace.Open();
        
        _logger.LogInformation("PowerShell service initialized");
    }

    /// <inheritdoc/>
    public async Task<T?> ExecuteAsync<T>(string cmdlet, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(cmdlet))
            throw new ArgumentNullException(nameof(cmdlet));

        try
        {
            using var ps = PowerShell.Create();
            ps.Runspace = _runspace;
            ps.AddCommand(cmdlet);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    ps.AddParameter(param.Key, param.Value);
                }
            }

            var result = await Task.Run(() => ps.Invoke(), cancellationToken);

            if (ps.HadErrors)
            {
                var errors = ps.Streams.Error.ReadAll();
                var errorMessage = string.Join(Environment.NewLine, errors.Select(e => e.ToString()));
                _logger.LogError("PowerShell command '{Cmdlet}' failed: {Errors}", cmdlet, errorMessage);
                throw new InvalidOperationException($"PowerShell command failed: {errorMessage}");
            }

            // Convert result to requested type
            if (typeof(T) == typeof(string))
            {
                return (T)(object)string.Join(Environment.NewLine, result.Select(r => r?.ToString() ?? string.Empty));
            }

            // For complex types, serialize to JSON and deserialize
            if (result.Count == 0)
                return default;

            var json = JsonSerializer.Serialize(result.Select(r => PSObjectToObject(r)));
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing PowerShell cmdlet '{Cmdlet}'", cmdlet);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<string> ExecuteScriptAsync(string script, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(script))
            throw new ArgumentNullException(nameof(script));

        try
        {
            using var ps = PowerShell.Create();
            ps.Runspace = _runspace;
            ps.AddScript(script);

            var result = await Task.Run(() => ps.Invoke(), cancellationToken);

            if (ps.HadErrors)
            {
                var errors = ps.Streams.Error.ReadAll();
                var errorMessage = string.Join(Environment.NewLine, errors.Select(e => e.ToString()));
                _logger.LogError("PowerShell script failed: {Errors}", errorMessage);
                throw new InvalidOperationException($"PowerShell script failed: {errorMessage}");
            }

            return string.Join(Environment.NewLine, result.Select(r => r?.ToString() ?? string.Empty));
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
        if (string.IsNullOrWhiteSpace(modulePath))
            throw new ArgumentNullException(nameof(modulePath));

        if (!File.Exists(modulePath))
            throw new FileNotFoundException($"Module file not found: {modulePath}");

        try
        {
            _logger.LogInformation("Importing PowerShell module from {ModulePath}", modulePath);
            
            await ExecuteScriptAsync($"Import-Module -Name '{modulePath}' -Force", cancellationToken);
            
            _logger.LogInformation("PowerShell module imported successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import PowerShell module from {ModulePath}", modulePath);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsModuleLoadedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ExecuteScriptAsync("Get-Module -Name DistroNexus", cancellationToken);
            return !string.IsNullOrWhiteSpace(result);
        }
        catch
        {
            return false;
        }
    }

    private static object PSObjectToObject(PSObject psObject)
    {
        if (psObject == null)
            return null!;

        return psObject.BaseObject;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _runspace?.Dispose();
        _disposed = true;
        
        GC.SuppressFinalize(this);
    }
}
