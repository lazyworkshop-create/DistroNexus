using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

/// <summary>
/// Service for reading and writing the global WSL configuration file (~/.wslconfig).
/// </summary>
public interface IWslConfigService
{
    /// <summary>
    /// Reads the current .wslconfig settings.
    /// Returns an empty <see cref="WslConfig"/> if the file does not exist.
    /// </summary>
    Task<WslConfig> GetWslConfigAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates .wslconfig with the supplied values. Only non-null properties are written;
    /// existing keys not mentioned are preserved. Comments are preserved.
    /// </summary>
    Task SetWslConfigAsync(WslConfig config, CancellationToken ct = default);

    /// <summary>
    /// Returns the host machine's total RAM in MB and physical CPU count.
    /// </summary>
    Task<(long TotalRamMb, int CpuCount)> GetHostSpecsAsync(CancellationToken ct = default);
}
