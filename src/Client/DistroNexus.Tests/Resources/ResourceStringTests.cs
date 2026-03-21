using DistroNexus.Desktop.Properties;
using Xunit;

namespace DistroNexus.Tests.Resources;

public class ResourceStringTests
{
    [Fact]
    public void TooltipSparseModeEnabled_IsNotNullOrEmpty()
    {
        var tooltip = DistroNexus.Desktop.Properties.Resources.TooltipSparseModeEnabled;
        Assert.False(string.IsNullOrWhiteSpace(tooltip),
            "TooltipSparseModeEnabled resource string must be defined.");
    }
}
