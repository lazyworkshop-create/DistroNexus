using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public sealed class ProductLogRevealTargetServiceTests
{
    [Theory]
    [InlineData("\\\\server\\share\\Logs")]
    [InlineData("\\\\?\\C:\\device\\Logs")]
    [InlineData("C:\\")]
    public void GetRevealTarget_RejectsUnsafeOrRootConfiguredPath(string configuredPath)
    {
        var target = new ProductLogRevealTargetService(() => configuredPath).GetRevealTarget();
        Assert.Null(target.RevealUri);
        Assert.Equal("ProductLog.Unavailable", target.OutcomeCode);
    }
}
