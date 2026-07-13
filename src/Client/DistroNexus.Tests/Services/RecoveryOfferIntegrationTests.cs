using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class RecoveryOfferIntegrationTests
{
    [Fact]
    public async Task TemplateOffer_DelegatesOnlyToOfferService_WithoutApplyingTemplate()
    {
        var offers = new Mock<IRecoveryOfferService>(MockBehavior.Strict);
        offers.Setup(x => x.GetOfferAsync("Ubuntu", RecoveryOfferReason.TemplateApplication, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryOffer(true, "Ubuntu", RecoveryOfferReason.TemplateApplication, "RecoveryOffer.OptionalBeforeOperation"));
        var powerShell = new Mock<IPowerShellService>(MockBehavior.Strict);
        var service = new TemplateService(NullLogger<TemplateService>.Instance, new Mock<ISettingsService>().Object, powerShell.Object, new HttpClient(), offers.Object);

        var offer = await service.GetRecoveryOfferAsync("Ubuntu");

        Assert.True(offer.IsAvailable);
        offers.VerifyAll();
        powerShell.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConfigurationOffers_AreExplicitAndDoNotWriteConfiguration()
    {
        var global = new WslConfigService(NullLogger<WslConfigService>.Instance, Path.GetTempPath());
        var globalOffer = await global.GetRecoveryOfferAsync();
        Assert.False(globalOffer.IsAvailable);
        Assert.Equal("RecoveryOffer.HostConfigurationRequiresInstance", globalOffer.MessageKey);

        var offers = new Mock<IRecoveryOfferService>(MockBehavior.Strict);
        offers.Setup(x => x.GetOfferAsync("Ubuntu", RecoveryOfferReason.MajorConfigurationChange, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryOffer(true, "Ubuntu", RecoveryOfferReason.MajorConfigurationChange, "RecoveryOffer.OptionalBeforeOperation"));
        var runner = new Mock<IProcessRunner>(MockBehavior.Strict);
        var distribution = new DistributionConfigurationService(runner.Object, offers.Object);
        var distributionOffer = await distribution.GetRecoveryOfferAsync("Ubuntu");

        Assert.True(distributionOffer.IsAvailable);
        runner.VerifyNoOtherCalls();
        offers.VerifyAll();
    }

    [Fact]
    public async Task HealthRepairOffers_RequireAnInstanceAndOnlyOfferForDestructivePreview()
    {
        var offers = new Mock<IRecoveryOfferService>(MockBehavior.Strict);
        offers.Setup(x => x.GetOfferAsync("Ubuntu", RecoveryOfferReason.DestructiveRepair, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecoveryOffer(true, "Ubuntu", RecoveryOfferReason.DestructiveRepair, "RecoveryOffer.OptionalBeforeOperation"));
        var safe = Action("safe", RepairSafety.Safe);
        var disruptive = Action("disruptive", RepairSafety.PrivilegedOrDisruptive);
        var service = new HealthRepairService([safe.Object, disruptive.Object], offers.Object);

        var noInstance = await service.GetRecoveryOfferAsync(new HealthFinding("no", HealthSeverity.Warning, HealthScope.Host, "t", "d", RepairId: "safe"));
        var safeOffer = await service.GetRecoveryOfferAsync(new HealthFinding("safe", HealthSeverity.Warning, HealthScope.Instance, "t", "d", "Ubuntu", "safe"));
        var disruptiveOffer = await service.GetRecoveryOfferAsync(new HealthFinding("danger", HealthSeverity.Warning, HealthScope.Instance, "t", "d", "Ubuntu", "disruptive"));

        Assert.Equal("RecoveryOffer.InstanceRequired", noInstance.MessageKey);
        Assert.Equal("RecoveryOffer.NotRequired", safeOffer.MessageKey);
        Assert.True(disruptiveOffer.IsAvailable);
        offers.VerifyAll();
    }

    [Fact]
    public async Task HealthRepairOffer_IsUnavailableWithoutRecoveryService()
    {
        var action = Action("danger", RepairSafety.RequiresConfirmation);
        var service = new HealthRepairService([action.Object]);

        var offer = await service.GetRecoveryOfferAsync(new HealthFinding("danger", HealthSeverity.Warning, HealthScope.Instance, "t", "d", "Ubuntu", "danger"));

        Assert.False(offer.IsAvailable);
        Assert.Equal("RecoveryOffer.Unavailable", offer.MessageKey);
    }

    private static Mock<IRepairAction> Action(string id, RepairSafety safety)
    {
        var action = new Mock<IRepairAction>(MockBehavior.Strict);
        action.SetupGet(x => x.Id).Returns(id);
        action.Setup(x => x.PreviewAsync(It.IsAny<HealthFinding>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepairPreview(id, id, safety, RepairIdempotency.Idempotent, [], []));
        return action;
    }
}
