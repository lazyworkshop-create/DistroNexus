using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace DistroNexus.Tests.Services;

public sealed class GlobalConfigurationServiceTests
{
    [Fact]
    public async Task DurableGrant_IsConsumedByFreshService_AndReplayHasNoSecondWriter()
    {
        using var scope = new TempScope(); var fixture = CreateFixture(scope.Path); var first = fixture.Create();
        var preview = await first.PreviewAsync(new Dictionary<string, string?> { ["wsl2.memory"] = "4GB" });
        var second = fixture.Create();
        await second.ExecuteAsync(preview.PreviewToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.ExecuteAsync(preview.PreviewToken));
        fixture.Configuration.Verify(x => x.SaveAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DurableGrant_RejectsExpiryForeignSidAndParallelReplay()
    {
        using var scope = new TempScope(); var fixture = CreateFixture(scope.Path); var preview = await fixture.Create().PreviewAsync(new Dictionary<string, string?> { ["wsl2.memory"] = "4GB" });
        fixture.Clock.Advance(TimeSpan.FromMinutes(3));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Create().ExecuteAsync(preview.PreviewToken));

        fixture.Clock.Advance(-TimeSpan.FromMinutes(3)); var valid = await fixture.Create().PreviewAsync(new Dictionary<string, string?> { ["wsl2.memory"] = "8GB" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Create("other-user").ExecuteAsync(valid.PreviewToken));

        var concurrent = await fixture.Create().PreviewAsync(new Dictionary<string, string?> { ["wsl2.memory"] = "6GB" });
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ => { try { await fixture.Create().ExecuteAsync(concurrent.PreviewToken); return true; } catch (InvalidOperationException) { return false; } }));
        Assert.Equal(1, outcomes.Count(x => x));
    }

    [Fact]
    public async Task DurableGrant_RejectsFingerprintCapabilityAndSchemaTampering_AndEnforcesCapacity()
    {
        using var scope = new TempScope(); var fixture = CreateFixture(scope.Path, maxRecords: 1); var first = fixture.Create();
        var preview = await first.PreviewAsync(new Dictionary<string, string?> { ["wsl2.memory"] = "4GB" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => first.PreviewAsync(new Dictionary<string, string?> { ["wsl2.swap"] = "1GB" }));
        fixture.Fingerprint = "changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Create().ExecuteAsync(preview.PreviewToken));

        fixture.Fingerprint = "fp"; var drift = await fixture.Create().PreviewAsync(new Dictionary<string, string?> { ["wsl2.memory"] = "3GB" });
        fixture.Capabilities[CapabilityId.ConfigFirewall] = new(CapabilityId.ConfigFirewall, CapabilityStatus.Supported, "test", CapabilitySource.WslCli, fixture.Clock.GetUtcNow());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Create().ExecuteAsync(drift.PreviewToken));

        File.WriteAllBytes(Path.Combine(scope.Path, "forged.bin"), [1, 2, 3]); fixture.Clock.Advance(TimeSpan.FromMinutes(3));
        await fixture.Create().PreviewAsync(new Dictionary<string, string?> { ["wsl2.swap"] = "1GB" });
        Assert.DoesNotContain(Directory.EnumerateFiles(scope.Path), path => Path.GetFileName(path) == "forged.bin");
    }

    [Fact]
    public async Task DurableGrant_ActualPersistedSchemaAndCorruptionTampering_FailWithoutSave()
    {
        using var scope = new TempScope(); var fixture = CreateFixture(scope.Path); var service = fixture.Create();
        var schema = await service.PreviewAsync(new Dictionary<string, string?> { ["wsl2.memory"] = "4GB" });
        var schemaFile = GrantPath(scope.Path, schema.PreviewToken); var json = JsonNode.Parse(await File.ReadAllTextAsync(schemaFile))!.AsObject(); json["SchemaRevision"] = "forged"; await File.WriteAllTextAsync(schemaFile, json.ToJsonString());
        var schemaFailure = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Create().ExecuteAsync(schema.PreviewToken));
        Assert.Contains("DN-8004", schemaFailure.Message); fixture.Configuration.Verify(x => x.SaveAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<string>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()), Times.Never);

        var corrupt = await fixture.Create().PreviewAsync(new Dictionary<string, string?> { ["wsl2.swap"] = "1GB" });
        await File.WriteAllBytesAsync(GrantPath(scope.Path, corrupt.PreviewToken), [0, 1, 2, 3]);
        var corruptionFailure = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Create().ExecuteAsync(corrupt.PreviewToken));
        Assert.Contains("DN-8004", corruptionFailure.Message); fixture.Configuration.Verify(x => x.SaveAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<string>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Preview_ConcurrentCapacityAdmission_AllowsExactlyOneGrant()
    {
        using var scope = new TempScope(); var fixture = CreateFixture(scope.Path, maxRecords: 1);
        var attempts = await Task.WhenAll(new[] { "4GB", "6GB" }.Select(async value => { try { await fixture.Create().PreviewAsync(new Dictionary<string, string?> { ["wsl2.memory"] = value }); return true; } catch (InvalidOperationException error) { Assert.StartsWith("DN-8005:", error.Message); return false; } }));
        Assert.Equal(1, attempts.Count(x => x)); Assert.Single(Directory.EnumerateFiles(scope.Path, "*.bin"));
    }

    [Theory]
    [MemberData(nameof(ValidGlobalValues))]
    public async Task Preview_AcceptsEveryModeledGlobalField(string id, string value)
    {
        using var scope = new TempScope(); var fixture = CreateFixture(scope.Path); fixture.EnableAllCapabilities();
        var preview = await fixture.Create().PreviewAsync(new Dictionary<string, string?> { [id] = value });
        Assert.Equal(id, Assert.Single(preview.Changes).Key);
    }

    [Theory]
    [MemberData(nameof(InvalidGlobalValues))]
    public async Task Preview_RejectsInvalidOrUnsupportedValuesBeforeGrantOrSave(string id, string value, bool capability)
    {
        using var scope = new TempScope(); var fixture = CreateFixture(scope.Path); if (capability) fixture.EnableAllCapabilities();
        var error = await Assert.ThrowsAsync<ArgumentException>(() => fixture.Create().PreviewAsync(new Dictionary<string, string?> { [id] = value }));
        Assert.StartsWith("DN-8003:", error.Message); Assert.Empty(Directory.EnumerateFiles(scope.Path)); fixture.Configuration.Verify(x => x.SaveAsync(It.IsAny<IReadOnlyDictionary<string, string?>>(), It.IsAny<string>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public static IEnumerable<object[]> ValidGlobalValues()
    {
        yield return ["wsl2.memory", "4GB"]; yield return ["wsl2.processors", "2"]; yield return ["wsl2.swap", "1GB"]; yield return ["wsl2.swapFile", "C:\\swap.vhdx"]; yield return ["wsl2.pageReporting", "true"]; yield return ["wsl2.localhostForwarding", "false"]; yield return ["wsl2.networkingMode", "nat"]; yield return ["wsl2.dnsTunneling", "true"]; yield return ["wsl2.firewall", "false"]; yield return ["wsl2.autoProxy", "true"]; yield return ["wsl2.hostAddressLoopback", "false"]; yield return ["wsl2.ignoredPorts", "80,443"]; yield return ["wsl2.bestEffortDnsParsing", "true"]; yield return ["wsl2.initialAutoProxyTimeout", "0"]; yield return ["wsl2.kernel", "C:\\kernel"]; yield return ["wsl2.kernelCommandLine", "quiet"]; yield return ["wsl2.nestedVirtualization", "true"]; yield return ["experimental.autoMemoryReclaim", "gradual"]; yield return ["experimental.sparseVhd", "false"];
    }
    public static IEnumerable<object[]> InvalidGlobalValues()
    {
        yield return ["wsl2.pageReporting", "not-bool", false]; yield return ["wsl2.networkingMode", "invalid", false]; yield return ["wsl2.memory", "four gigabytes", false]; yield return ["wsl2.ignoredPorts", "0,70000", true]; yield return ["wsl2.swapFile", "bad\npath", false]; yield return ["wsl2.processors", "99", false]; yield return ["wsl2.firewall", "true", false];
    }

    private static string GrantPath(string root, string token) => Path.Combine(root, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))) + ".bin");

    private static Fixture CreateFixture(string root, int maxRecords = 64) => new(root, maxRecords);
    private sealed class Fixture
    {
        public Mock<IWslConfigurationService> Configuration { get; } = new(); public Dictionary<CapabilityId, CapabilityResult> Capabilities { get; } = []; public MutableClock Clock { get; } = new(DateTimeOffset.Parse("2026-01-01T00:00:00Z")); public string Fingerprint = "fp"; private readonly string _root; private readonly int _maxRecords;
        public Fixture(string root, int maxRecords) { _root = root; _maxRecords = maxRecords; Configuration.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(new ConfigurationDocument<WslConfigurationSettings>(new(new Dictionary<string, string>()), LosslessIniDocument.Empty(), [], 0, Fingerprint, RestartScope.Wsl, ""))); Configuration.Setup(x => x.PreviewAsync(It.IsAny<IReadOnlyDictionary<string,string?>>(), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationPreview("", "", ["wsl2.memory"], RestartScope.Wsl)); Configuration.Setup(x => x.SaveAsync(It.IsAny<IReadOnlyDictionary<string,string?>>(), "fp", It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ConfigurationSaveResult("new", null, RestartScope.Wsl)); }
        public GlobalConfigurationService Create(string sid = "user") { var host = new Mock<IWslConfigService>(); host.Setup(x => x.GetHostSpecsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((16384L, 8)); var caps = new Mock<IPlatformCapabilityService>(); caps.Setup(x => x.GetHostSnapshotAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(new PlatformCapabilitySnapshot(new("", new Version(1,0), "x64", false, null, null, null, null, null), Capabilities, new Dictionary<CapabilityId, CapabilityResult>(), Clock.GetUtcNow())); return new(Configuration.Object, host.Object, caps.Object, _root, Clock, () => sid, bytes => bytes, bytes => bytes, _maxRecords); }
        public void EnableAllCapabilities() { foreach (var id in new[] { CapabilityId.MirroredNetworking, CapabilityId.SparseVhd, CapabilityId.ConfigDnsTunneling, CapabilityId.ConfigFirewall, CapabilityId.ConfigAutoProxy, CapabilityId.ConfigHostAddressLoopback, CapabilityId.ConfigIgnoredPorts, CapabilityId.ConfigBestEffortDnsParsing, CapabilityId.ConfigProxyTimeout, CapabilityId.ConfigAutoMemoryReclaim }) Capabilities[id] = new(id, CapabilityStatus.Supported, "test", CapabilitySource.WslCli, Clock.GetUtcNow()); }
    }
    private sealed class MutableClock(DateTimeOffset now) : TimeProvider { private DateTimeOffset _now = now; public override DateTimeOffset GetUtcNow() => _now; public void Advance(TimeSpan value) => _now += value; }
    private sealed class TempScope : IDisposable { public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N")); public TempScope() => Directory.CreateDirectory(Path); public void Dispose() => Directory.Delete(Path, true); }
}
