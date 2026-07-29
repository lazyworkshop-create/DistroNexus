using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace DistroNexus.Tests.Services;

public sealed class VerifiedInstallServiceTests
{
    [Fact]
    public async Task PreviewAcquireAsync_InvalidPackageId_FailsBeforeCatalogAccess()
    {
        var catalog = new Mock<ICatalogService>(MockBehavior.Strict);
        var processes = new Mock<IProcessRunner>(MockBehavior.Strict);
        var service = new VerifiedInstallService(catalog.Object, processes.Object, (_, _) => Task.FromResult(false), Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewAcquireAsync(" "));

        Assert.Equal("Lifecycle.AcquisitionInvalid", error.Message);
        catalog.VerifyNoOtherCalls();
        processes.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveAsync_CorruptCachedArtifact_IsNotReportedVerified()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "ubuntu.wsl"), "corrupt");
        var catalog = new Mock<ICatalogService>(MockBehavior.Strict);
        catalog.Setup(x => x.GetDistributionByIdAsync("ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(new DistroPackage { Id="ubuntu", DownloadUrl="https://example.test/ubuntu.wsl", Sha256="00", FileSize=7 });
        catalog.Setup(x => x.GetPackageCachePath()).Returns(root);
        var service = new VerifiedInstallService(catalog.Object, Mock.Of<IProcessRunner>(), (_, _) => Task.FromResult(false), Path.Combine(root, "grants"));

        var result = await service.ResolveAsync("ubuntu");

        Assert.Equal("Missing", result.CacheState);
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task InstallAsync_ConsumedGrantCannotReplay()
    {
        var root = TempRoot(); var token = new string('a', 64); await WriteGrantAsync(root, token, DateTimeOffset.UtcNow.AddMinutes(1), CurrentSid(), Path.Combine(root, "missing", "target"));
        var service = Service(root);

        await service.InstallAsync(token);
        var replay = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InstallAsync(token));

        Assert.Equal("Lifecycle.GrantInvalid", replay.Message);
    }

    [Fact]
    public async Task InstallAsync_ExpiredAndForeignSidFailBeforeProcess()
    {
        var root = TempRoot(); var expired = new string('b', 64); var foreign = new string('c', 64);
        await WriteGrantAsync(root, expired, DateTimeOffset.UtcNow.AddMinutes(-1), CurrentSid(), Path.Combine(root, "x"));
        await WriteGrantAsync(root, foreign, DateTimeOffset.UtcNow.AddMinutes(1), "S-1-0-0", Path.Combine(root, "y"));
        var service = Service(root);

        var expiry = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InstallAsync(expired));
        var sid = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InstallAsync(foreign));

        Assert.Equal("Lifecycle.GrantExpired", expiry.Message);
        Assert.Equal("Lifecycle.GrantInvalid", sid.Message);
    }

    [Fact]
    public async Task InstallAsync_PreexecuteRootDriftReleasesReservation()
    {
        var root = TempRoot(); var target = Path.Combine(root, "deleted", "Ubuntu"); var token = new string('d', 64);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!); await File.WriteAllTextAsync(target + ".distronexus-install", "");
        await WriteGrantAsync(root, token, DateTimeOffset.UtcNow.AddMinutes(1), CurrentSid(), target); Directory.Delete(Path.GetDirectoryName(target)!, true);
        var result = await Service(root).InstallAsync(token);

        Assert.Equal("Lifecycle.StateChanged", result.OutcomeCode);
        Assert.False(File.Exists(target + ".distronexus-install"));
    }

    private static VerifiedInstallService Service(string root) => new(Mock.Of<ICatalogService>(MockBehavior.Strict), Mock.Of<IProcessRunner>(MockBehavior.Strict), (_, _) => Task.FromResult(false), root);
    private static string TempRoot(){var root=Path.Combine(Path.GetTempPath(),"DistroNexus.VerifiedInstallTests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);return root;}
    private static string CurrentSid() => WindowsIdentity.GetCurrent().User!.Value;
    private static async Task WriteGrantAsync(string root,string token,DateTimeOffset expires,string sid,string target)
    {
        var record = System.Text.Json.JsonSerializer.Serialize(new { Sid=sid, Operation="install", PackageId="ubuntu", Path=Path.Combine(root,"missing.tar"), Name="Ubuntu", ExpiresAt=expires, Hash=(string?)null, Size=0L, Target=target, Reference=(string?)null, Username="root", Shell="bash", Locale=(string?)null, SetDefault=false, Envelope=(string?)null });
        var folder=Path.Combine(root,"verified-install-grants");Directory.CreateDirectory(folder);var file=Path.Combine(folder,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))+".grant");
        await File.WriteAllBytesAsync(file,ProtectedData.Protect(Encoding.UTF8.GetBytes(record),null,DataProtectionScope.CurrentUser));
    }
}
