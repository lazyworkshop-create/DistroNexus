using DistroNexus.Core.Interfaces;
namespace DistroNexus.Core.Services;
/// <summary>Returns an optional pre-operation recovery offer without mutating the caller's workflow.</summary>
public sealed class RecoveryOfferService : IRecoveryOfferService
{
    private readonly IRecoveryPointRuntime _runtime;
    public RecoveryOfferService(IRecoveryPointRuntime runtime) => _runtime = runtime;
    public async Task<RecoveryOffer> GetOfferAsync(string instanceName, RecoveryOfferReason reason, CancellationToken cancellationToken = default)
    { if (string.IsNullOrWhiteSpace(instanceName)) return new(false, instanceName, reason, "RecoveryOffer.InstanceRequired"); try { await _runtime.GetSourceAsync(instanceName, cancellationToken); return new(true, instanceName, reason, "RecoveryOffer.OptionalBeforeOperation"); } catch { return new(false, instanceName, reason, "RecoveryOffer.RuntimeUnavailable"); } }
}
