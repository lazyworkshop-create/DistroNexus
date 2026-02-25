using DistroNexus.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// Detects whether Store compliance mode should be enabled for the current process.
/// </summary>
public class StoreComplianceModeService : IStoreComplianceModeService
{
    private const string ComplianceOverrideVariableName = "DISTRONEXUS_STORE_COMPLIANCE_MODE";
    private readonly ILogger<StoreComplianceModeService> _logger;

    public StoreComplianceModeService(ILogger<StoreComplianceModeService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public bool IsStoreComplianceModeEnabled()
    {
        return IsStoreComplianceModeEnabled(
            Environment.GetEnvironmentVariable(ComplianceOverrideVariableName),
            Environment.GetEnvironmentVariable("PACKAGE_FAMILY_NAME"),
            AppContext.BaseDirectory);
    }

    public bool IsStoreComplianceModeEnabled(string? overrideValue, string? packageFamilyName, string? baseDirectory)
    {
        if (bool.TryParse(overrideValue, out var forcedMode))
        {
            _logger.LogInformation("Store compliance mode forced by environment variable {Variable}: {Mode}", ComplianceOverrideVariableName, forcedMode);
            return forcedMode;
        }

        if (!string.IsNullOrWhiteSpace(packageFamilyName))
        {
            _logger.LogDebug("Store compliance mode enabled because PACKAGE_FAMILY_NAME is present");
            return true;
        }

        if (!string.IsNullOrWhiteSpace(baseDirectory) &&
            baseDirectory.Contains("\\WindowsApps\\", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Store compliance mode enabled because base directory is under WindowsApps");
            return true;
        }

        return false;
    }
}
