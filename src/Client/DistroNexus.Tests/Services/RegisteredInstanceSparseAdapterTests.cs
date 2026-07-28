using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.WorkspaceBridge;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class RegisteredInstanceSparseAdapterTests
{
    [Fact]
    public async Task OnlyFixedManageSparseRequestCanBeIssued()
    {
        var runner = new Mock<IProcessRunner>(MockBehavior.Strict);
        var expected = new[] { "--manage", "Ubuntu", "--set-sparse", "true" };
        runner.Setup(x => x.RunAsync(It.Is<ProcessRequest>(request => request.FileName == "wsl.exe" && request.Arguments.SequenceEqual(expected)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, null));
        var adapter = new RegisteredInstanceSparseAdapter(runner.Object, name => name == "Ubuntu" ? new("Ubuntu", "registered-id", 2, false) : null);
        var state = await adapter.GetAsync("Ubuntu");
        Assert.Equal(2, state!.WslVersion);
        Assert.True(await adapter.SetSparseAsync("Ubuntu", true));
        runner.VerifyAll();
    }
}
