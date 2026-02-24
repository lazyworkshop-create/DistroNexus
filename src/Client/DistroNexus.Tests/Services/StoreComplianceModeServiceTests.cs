using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

public class StoreComplianceModeServiceTests
{
    private readonly Mock<ILogger<StoreComplianceModeService>> _logger = new();

    [Theory]
    [InlineData("true", false)]
    [InlineData("false", false)]
    public void IsStoreComplianceModeEnabled_WithOverrideValue_UsesOverride(string overrideValue, bool packagePresent)
    {
        var service = new StoreComplianceModeService(_logger.Object);

        var result = service.IsStoreComplianceModeEnabled(
            overrideValue,
            packagePresent ? "DistroNexus_TestPackage" : null,
            @"C:\Program Files\DistroNexus\");

        Assert.Equal(bool.Parse(overrideValue), result);
    }

    [Fact]
    public void IsStoreComplianceModeEnabled_WithPackageFamilyName_ReturnsTrue()
    {
        var service = new StoreComplianceModeService(_logger.Object);

        var result = service.IsStoreComplianceModeEnabled(
            null,
            "LazyWorkshopCreate.DistroNexus_v70wxt9jxp4nt",
            @"C:\Program Files\DistroNexus\");

        Assert.True(result);
    }

    [Fact]
    public void IsStoreComplianceModeEnabled_WithWindowsAppsBasePath_ReturnsTrue()
    {
        var service = new StoreComplianceModeService(_logger.Object);

        var result = service.IsStoreComplianceModeEnabled(
            null,
            null,
            @"C:\Program Files\WindowsApps\LazyWorkshopCreate.DistroNexus_2.1.1.0_x64__v70wxt9jxp4nt\");

        Assert.True(result);
    }

    [Fact]
    public void IsStoreComplianceModeEnabled_WithNoSignals_ReturnsFalse()
    {
        var service = new StoreComplianceModeService(_logger.Object);

        var result = service.IsStoreComplianceModeEnabled(
            null,
            null,
            @"C:\Tools\DistroNexus\");

        Assert.False(result);
    }
}
