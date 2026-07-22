using DistroNexus.Desktop.Services;
namespace DistroNexus.Tests.Services;
public sealed class WorkspaceStartupRouteTests
{
    [Fact] public void ParsesOnlyExactGuidInvocation() { var id=Guid.NewGuid(); Assert.True(WorkspaceStartupRoute.TryParse(["--workspace",id.ToString("D")],out var parsed));Assert.Equal(id,parsed);Assert.False(WorkspaceStartupRoute.TryParse(["--workspace",id.ToString(),"x"],out _));Assert.False(WorkspaceStartupRoute.TryParse(["--workspace","cmd.exe"],out _)); }
}
