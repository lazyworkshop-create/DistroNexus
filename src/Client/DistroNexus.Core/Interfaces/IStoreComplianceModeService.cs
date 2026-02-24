namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Provides Store compliance mode detection for policy-sensitive runtime behavior.
/// </summary>
public interface IStoreComplianceModeService
{
    /// <summary>
    /// Gets whether Store compliance mode is enabled for the current process.
    /// </summary>
    /// <returns><c>true</c> when Store compliance mode is enabled; otherwise, <c>false</c>.</returns>
    bool IsStoreComplianceModeEnabled();
}
