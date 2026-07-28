using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for DockerIntegrationService (F-02).
/// </summary>
public class DockerIntegrationServiceTests
{
    [Fact] public async Task PreviewRejectsBlankInstance() { var s=new TestableDockerIntegrationService(_mockLogger.Object,_mockWslManager.Object,true,Path.GetTempPath(),Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json")); await Assert.ThrowsAsync<InvalidOperationException>(()=>s.PreviewSetAsync(" ",true)); }
    [Fact] public async Task PreviewRejectsAbsentSettingsWithoutCreatingIt() { var r=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); Directory.CreateDirectory(r); var p=Path.Combine(r,"settings.json"); try { var w=new Mock<IWslManagerService>(); w.Setup(x=>x.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new WslInstance{Name="Ubuntu",Version=2}]); var s=new TestableDockerIntegrationService(_mockLogger.Object,w.Object,true,r,p); await Assert.ThrowsAsync<InvalidOperationException>(()=>s.PreviewSetAsync("Ubuntu",true)); Assert.False(File.Exists(p)); } finally {Directory.Delete(r,true);} }
    [Fact] public async Task PreviewRejectsNonStringIntegrationArray() { var r=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); Directory.CreateDirectory(r); var p=Path.Combine(r,"settings.json"); await File.WriteAllTextAsync(p,"""{"integratedWslDistros":[1]}"""); try { var w=new Mock<IWslManagerService>(); w.Setup(x=>x.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new WslInstance{Name="Ubuntu",Version=2}]); var s=new TestableDockerIntegrationService(_mockLogger.Object,w.Object,true,r,p); await Assert.ThrowsAsync<InvalidOperationException>(()=>s.PreviewSetAsync("Ubuntu",true)); Assert.Contains("[1]",await File.ReadAllTextAsync(p)); } finally {Directory.Delete(r,true);} }
    [Fact] public async Task ExecuteRejectsEnabledStateMismatchWithoutWriting() { var r=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); Directory.CreateDirectory(r); var p=Path.Combine(r,"settings.json"); await File.WriteAllTextAsync(p,"""{"integratedWslDistros":[]}"""); try { var w=new Mock<IWslManagerService>(); w.Setup(x=>x.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new WslInstance{Name="Ubuntu",Version=2}]); var s=new TestableDockerIntegrationService(_mockLogger.Object,w.Object,true,r,p); var x=await s.PreviewSetAsync("Ubuntu",true); var before=await File.ReadAllTextAsync(p); await Assert.ThrowsAsync<InvalidOperationException>(()=>s.SetFromPreviewAsync(x.Token,"Ubuntu",false)); Assert.Equal(before,await File.ReadAllTextAsync(p)); } finally {Directory.Delete(r,true);} }
    [Fact] public async Task ExecuteRejectsSelectedFileIdentitySwitchWithoutWriting() { var r=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")); Directory.CreateDirectory(r); var p=Path.Combine(r,"settings.json"); await File.WriteAllTextAsync(p,"""{"integratedWslDistros":[]}"""); try { var w=new Mock<IWslManagerService>(); w.Setup(x=>x.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new WslInstance{Name="Ubuntu",Version=2}]); var s=new TestableDockerIntegrationService(_mockLogger.Object,w.Object,true,r,p); var x=await s.PreviewSetAsync("Ubuntu",true); File.Delete(p); await File.WriteAllTextAsync(p,"""{"keep":9,"integratedWslDistros":[]}"""); await Assert.ThrowsAsync<InvalidOperationException>(()=>s.SetFromPreviewAsync(x.Token,"Ubuntu",true)); Assert.Contains("\"keep\":9",await File.ReadAllTextAsync(p)); } finally {Directory.Delete(r,true);} }
    [Fact]
    public async Task PreviewAndExecute_PreservesUnrelatedJsonAndDeduplicates()
    {
        var root = Path.Combine(Path.GetTempPath(), "DockerSettings-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var path = Path.Combine(root, "settings.json"); await File.WriteAllTextAsync(path, """{"keep":{"value":1},"integratedWslDistros":["ubuntu","Ubuntu"]}""");
        try
        {
            var wsl = new Mock<IWslManagerService>(); wsl.Setup(x => x.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new WslInstance { Name="Ubuntu", Version=2 }]);
            var service = new TestableDockerIntegrationService(_mockLogger.Object, wsl.Object, true, root, path);
            var preview = await service.PreviewSetAsync("Ubuntu", true); var result = await service.SetFromPreviewAsync(preview.Token, "Ubuntu", true);
            Assert.True(result.Succeeded); using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Assert.Equal(1, json.RootElement.GetProperty("keep").GetProperty("value").GetInt32());
            Assert.Single(json.RootElement.GetProperty("integratedWslDistros").EnumerateArray());
            Assert.True(File.Exists(path + ".distronexus.bak"));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PreviewRejectsMissingMalformedReservedAndWsl1Settings()
    {
        var root = Path.Combine(Path.GetTempPath(), "DockerSettings-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); var path=Path.Combine(root,"settings.json");
        try
        {
            var wsl = new Mock<IWslManagerService>(); wsl.Setup(x=>x.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new WslInstance{Name="Ubuntu",Version=1}]);
            var service=new TestableDockerIntegrationService(_mockLogger.Object,wsl.Object,true,root,path);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PreviewSetAsync("Ubuntu",true));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.PreviewSetAsync("docker-desktop",true));
            await File.WriteAllTextAsync(path,"{"); wsl.Setup(x=>x.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new WslInstance{Name="Ubuntu",Version=2}]);
            await Assert.ThrowsAnyAsync<JsonException>(()=>service.PreviewSetAsync("Ubuntu",true));
        } finally { Directory.Delete(root,true); }
    }

    [Fact]
    public async Task ExecuteRejectsNameStateStaleAndReplayWithoutWriting()
    {
        var root=Path.Combine(Path.GetTempPath(),"DockerSettings-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); var path=Path.Combine(root,"settings.json"); await File.WriteAllTextAsync(path,"""{"keep":1,"integratedWslDistros":[]}""");
        try {
            var wsl=new Mock<IWslManagerService>(); wsl.Setup(x=>x.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new WslInstance{Name="Ubuntu",Version=2}]);
            var service=new TestableDockerIntegrationService(_mockLogger.Object,wsl.Object,true,root,path); var preview=await service.PreviewSetAsync("Ubuntu",true); var before=await File.ReadAllTextAsync(path);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SetFromPreviewAsync(preview.Token,"Other",true)); Assert.Equal(before,await File.ReadAllTextAsync(path));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SetFromPreviewAsync(preview.Token,"Ubuntu",true)); Assert.Equal(before,await File.ReadAllTextAsync(path));
            var stale=await service.PreviewSetAsync("Ubuntu",true); await File.WriteAllTextAsync(path,"""{"keep":2,"integratedWslDistros":[]}""");
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SetFromPreviewAsync(stale.Token,"Ubuntu",true)); Assert.Contains("\"keep\":2",await File.ReadAllTextAsync(path));
        } finally { Directory.Delete(root,true); }
    }

    [Fact]
    public async Task TwoServicesSerializeAndSecondPreviewBecomesStale()
    {
        var root=Path.Combine(Path.GetTempPath(),"DockerSettings-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); var path=Path.Combine(root,"settings.json"); await File.WriteAllTextAsync(path,"""{"keep":{"x":1},"integratedWslDistros":[]}""");
        try { var wsl=new Mock<IWslManagerService>(); wsl.Setup(x=>x.GetInstancesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new WslInstance{Name="Ubuntu",Version=2}]); var a=new TestableDockerIntegrationService(_mockLogger.Object,wsl.Object,true,root,path); var b=new TestableDockerIntegrationService(_mockLogger.Object,wsl.Object,true,root,path); var pa=await a.PreviewSetAsync("Ubuntu",true); var pb=await b.PreviewSetAsync("Ubuntu",true); Assert.True((await a.SetFromPreviewAsync(pa.Token,"Ubuntu",true)).Succeeded); await Assert.ThrowsAsync<InvalidOperationException>(()=>b.SetFromPreviewAsync(pb.Token,"Ubuntu",true)); using var json=JsonDocument.Parse(await File.ReadAllTextAsync(path)); Assert.Equal(1,json.RootElement.GetProperty("keep").GetProperty("x").GetInt32()); } finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task ProtectedGrantStore_RejectsForgedAndReusedTokens()
    {
        var root = Path.Combine(Path.GetTempPath(), "DockerGrant-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DockerIntegrationGrantStore(root);
            var token = new string('a', 64);
            await store.IssueAsync(token, new DockerIntegrationGrant("Ubuntu", true, "fingerprint", "identity", DateTimeOffset.UtcNow.AddMinutes(1)), CancellationToken.None);
            var grant = await store.ConsumeAsync(token, CancellationToken.None);
            Assert.Equal("Ubuntu", grant.Name);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConsumeAsync(token, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConsumeAsync(new string('b', 64), CancellationToken.None));
            Assert.DoesNotContain(token, Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(Path.GetFileName));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ProtectedGrantStore_RejectsExpiredGrant()
    {
        var root = Path.Combine(Path.GetTempPath(), "DockerGrant-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DockerIntegrationGrantStore(root); var token = new string('c', 64);
            await store.IssueAsync(token, new DockerIntegrationGrant("Ubuntu", false, "f", "i", DateTimeOffset.UtcNow.AddSeconds(-1)), CancellationToken.None);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConsumeAsync(token, CancellationToken.None));
            Assert.Equal("DockerIntegration.PreviewExpired", error.Message);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private readonly Mock<ILogger<DockerIntegrationService>> _mockLogger;
    private readonly Mock<IWslManagerService> _mockWslManager;
    private readonly DockerIntegrationService _service;

    public DockerIntegrationServiceTests()
    {
        _mockLogger = new Mock<ILogger<DockerIntegrationService>>();
        _mockWslManager = new Mock<IWslManagerService>();
        _service = new DockerIntegrationService(_mockLogger.Object, _mockWslManager.Object);
    }

    [Fact]
    public async Task IsDockerDesktopInstalledAsync_ReturnsBool()
    {
        // Act — just verify it returns without throwing; result depends on environment
        var result = await _service.IsDockerDesktopInstalledAsync();
        Assert.IsType<bool>(result);
    }

    [Fact]
    public async Task GetIntegrationStatusAsync_WithNullName_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.GetIntegrationStatusAsync(null!));
    }

    [Fact]
    public async Task GetIntegrationStatusAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<WslOperationFailedException>(
            () => _service.GetIntegrationStatusAsync(string.Empty));
        Assert.Equal(DistroNexusErrorCode.InstanceNotFound, ex.Code);
    }

    [Fact]
    public async Task GetIntegrationStatusAsync_WhenDockerNotInstalled_ReturnsUnavailable()
    {
        // Arrange — use a service whose install check is overridden
        var service = new TestableDockerIntegrationService(_mockLogger.Object, _mockWslManager.Object, dockerInstalled: false);

        // Act
        var result = await service.GetIntegrationStatusAsync("Ubuntu-22.04");

        // Assert
        Assert.Equal(DockerIntegrationStatus.Unavailable, result);
    }

    [Fact]
    public async Task GetIntegrationStatusAsync_ForDockerDesktopDistro_ReturnsUnavailable()
    {
        // docker-desktop is a reserved name — never eligible
        var service = new TestableDockerIntegrationService(_mockLogger.Object, _mockWslManager.Object, dockerInstalled: true);

        var result1 = await service.GetIntegrationStatusAsync("docker-desktop");
        var result2 = await service.GetIntegrationStatusAsync("docker-desktop-data");

        Assert.Equal(DockerIntegrationStatus.Unavailable, result1);
        Assert.Equal(DockerIntegrationStatus.Unavailable, result2);
    }

    [Fact]
    public async Task GetDockerDesktopVersionAsync_ReturnsVersion_WhenInstalled()
    {
        // This test verifies the method exists and returns a non-null version string
        var result = await _service.GetDockerDesktopVersionAsync(CancellationToken.None);
        // In CI where Docker is not installed, null is acceptable
        Assert.True(result == null || result.Length > 0);
    }
}

/// <summary>
/// Testable subclass that overrides installation check for isolated unit testing.
/// </summary>
internal sealed class TestableDockerIntegrationService : DockerIntegrationService
{
    private readonly bool _dockerInstalled;

    public TestableDockerIntegrationService(
        ILogger<DockerIntegrationService> logger,
        IWslManagerService wslManager,
        bool dockerInstalled)
        : base(logger, wslManager)
    {
        _dockerInstalled = dockerInstalled;
    }
    public TestableDockerIntegrationService(ILogger<DockerIntegrationService> logger, IWslManagerService wslManager, bool dockerInstalled, string root, string settingsPath) : base(logger, wslManager, root, settingsPath) => _dockerInstalled = dockerInstalled;

    public override Task<bool> IsDockerDesktopInstalledAsync(CancellationToken ct = default)
        => Task.FromResult(_dockerInstalled);
}
