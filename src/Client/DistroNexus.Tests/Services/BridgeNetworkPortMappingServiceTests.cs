using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class BridgeNetworkPortMappingServiceTests
{
    [Fact]
    public async Task PortMappings_MergeOnlyStrictFixedPortProxyFacts()
    {
        var runner = new Mock<IProcessRunner>(MockBehavior.Strict);
        runner.Setup(x => x.RunAsync(It.Is<ProcessRequest>(r => r.FileName == "wsl.exe" && r.Arguments.SequenceEqual(new[] { "--distribution", "Ubuntu", "--exec", "ss", "-tlnp" })), It.IsAny<CancellationToken>())).ReturnsAsync(Result("tcp LISTEN 0 128 0.0.0.0:8080 0.0.0.0:* users:((\"node\",pid=1234,fd=7))"));
        runner.Setup(x => x.RunAsync(It.Is<ProcessRequest>(r => r.FileName == "wsl.exe" && r.Arguments.SequenceEqual(new[] { "--distribution", "Ubuntu", "--exec", "hostname", "-I" })), It.IsAny<CancellationToken>())).ReturnsAsync(Result("172.20.1.5"));
        runner.Setup(x => x.RunAsync(It.Is<ProcessRequest>(r => r.FileName == "netsh.exe" && r.Arguments.SequenceEqual(new[] { "interface", "portproxy", "show", "v4tov4" }) && r.Timeout == TimeSpan.FromSeconds(10)), It.IsAny<CancellationToken>())).ReturnsAsync(Result("Listen on ipv4:             Connect to ipv4:\nAddress         Port        Address         Port\n--------------- ----------  --------------- ----------\n0.0.0.0         8080        172.20.1.5      8080\ninvalid 99999 ignored 80"));
        var status = new Mock<INetworkStatusAdapter>(); status.Setup(x => x.GetPortCollisionsAsync(It.IsAny<IReadOnlyList<PortMapping>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var mappings = await new BridgeNetworkPortMappingService(runner.Object, status.Object).GetPortMappingsAsync("Ubuntu", "TCP");

        var mapping = Assert.Single(mappings);
        Assert.Equal(8080, mapping.Port); Assert.True(mapping.HasWindowsProxy); Assert.Equal("172.20.1.5", mapping.InstanceIpAddress);
        runner.VerifyAll();
    }

    [Fact]
    public async Task PortMappings_PortProxyFailureDegradesToNoProxyWithoutExecutingAnythingElse()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(x => x.RunAsync(It.Is<ProcessRequest>(r => r.FileName == "wsl.exe" && r.Arguments.Contains("ss")), It.IsAny<CancellationToken>())).ReturnsAsync(Result("tcp LISTEN 0 128 0.0.0.0:8080 0.0.0.0:*"));
        runner.Setup(x => x.RunAsync(It.Is<ProcessRequest>(r => r.FileName == "wsl.exe" && r.Arguments.Contains("hostname")), It.IsAny<CancellationToken>())).ReturnsAsync(Result("172.20.1.5"));
        runner.Setup(x => x.RunAsync(It.Is<ProcessRequest>(r => r.FileName == "netsh.exe"), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("netsh unavailable"));
        var status = new Mock<INetworkStatusAdapter>(); status.Setup(x => x.GetPortCollisionsAsync(It.IsAny<IReadOnlyList<PortMapping>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var mapping = Assert.Single(await new BridgeNetworkPortMappingService(runner.Object, status.Object).GetPortMappingsAsync("Ubuntu", "TCP"));

        Assert.False(mapping.HasWindowsProxy);
        runner.Verify(x => x.RunAsync(It.Is<ProcessRequest>(r => r.FileName == "netsh.exe" && r.Arguments.SequenceEqual(new[] { "interface", "portproxy", "show", "v4tov4" })), It.IsAny<CancellationToken>()), Times.Once);
        runner.Verify(x => x.RunAsync(It.IsAny<ProcessRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    private static ProcessResult Result(string output) => new(0, output, string.Empty, TimeSpan.Zero, false, false, false, 1);
}
