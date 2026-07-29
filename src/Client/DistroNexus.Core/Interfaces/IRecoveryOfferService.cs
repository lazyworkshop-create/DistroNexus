using DistroNexus.Core.Models;
namespace DistroNexus.Core.Interfaces;
public interface IRecoveryOfferService { Task<RecoveryOffer> GetOfferAsync(string instanceName, RecoveryOfferReason reason, CancellationToken cancellationToken = default); }
public enum RecoveryOfferReason { TemplateApplication, MajorConfigurationChange, WslVersionConversion, DestructiveRepair }
public sealed record RecoveryOffer(bool IsAvailable, string InstanceName, RecoveryOfferReason Reason, string MessageKey);
