using DistroNexus.Core.Models;

namespace DistroNexus.Core.Interfaces;

public interface IWslConfigurationService
{
    Task<ConfigurationDocument<WslConfigurationSettings>> ReadAsync(CancellationToken cancellationToken = default);
    Task<ConfigurationPreview> PreviewAsync(IReadOnlyDictionary<string, string?> values, string expectedFingerprint,
        IReadOnlySet<string> availableCapabilities, CancellationToken cancellationToken = default);
    Task<ConfigurationSaveResult> SaveAsync(IReadOnlyDictionary<string, string?> values, string expectedFingerprint,
        IReadOnlySet<string>? availableCapabilities = null, CancellationToken cancellationToken = default);
}

public interface IDistributionConfigurationService
{
    Task<ConfigurationDocument<DistributionConfigurationSettings>> ReadAsync(string distribution,
        CancellationToken cancellationToken = default);
    Task<ConfigurationPreview> PreviewAsync(string distribution, IReadOnlyDictionary<string, string?> values,
        string expectedFingerprint, CancellationToken cancellationToken = default);
    Task<ConfigurationSaveResult> SaveAsync(string distribution, IReadOnlyDictionary<string, string?> values,
        string expectedFingerprint, CancellationToken cancellationToken = default);
}
