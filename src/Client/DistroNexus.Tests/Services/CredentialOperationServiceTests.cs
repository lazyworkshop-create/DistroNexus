using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace DistroNexus.Tests.Services;

public sealed class CredentialOperationServiceTests
{
    [Fact]
    public async Task Preview_RejectsMalformedEnvelopeBeforeAnyInstanceOrProcessOperation()
    {
        var processes = new Mock<IProcessRunner>(MockBehavior.Strict);
        var checkedInstance = false;
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus.CredentialTests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new CredentialOperationService(processes.Object, (_, _) => { checkedInstance = true; return Task.FromResult(true); }, root);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewAsync("Ubuntu", "developer", "not-base64"));
            Assert.Equal("Lifecycle.CredentialInvalid", error.Message);
            Assert.False(checkedInstance);
            processes.VerifyNoOtherCalls();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ExecuteAsync_ReplayFailsAfterFixedStdinInvocation()
    {
        var process = new Mock<IProcessRunner>(MockBehavior.Strict);
        process.Setup(x => x.RunAsync(It.Is<ProcessRequest>(r => r.FileName == "wsl.exe" && r.Arguments.Contains("chpasswd") && r.Arguments.All(a => !a.Contains("p@ss;$(x)", StringComparison.Ordinal)) && r.StandardInput == "dev:p@ss;$(x)\n"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessResult(0, "", "", TimeSpan.Zero, false, false, false, null));
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var service = new CredentialOperationService(process.Object, (_, _) => Task.FromResult(true), root);
        var envelope = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes("p@ss;$(x)"), null, DataProtectionScope.CurrentUser));

        var preview = await service.PreviewAsync("Ubuntu", "dev", envelope);
        var result = await service.ExecuteAsync(preview.PreviewToken);
        var replay = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(preview.PreviewToken));

        Assert.True(result.Succeeded);
        Assert.Equal("Lifecycle.CredentialGrantInvalid", replay.Message);
        process.VerifyAll();
    }

    [Fact]
    public async Task ExecuteAsync_FingerprintDriftFailsBeforeProcess()
    {
        var process = new Mock<IProcessRunner>(MockBehavior.Strict);
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var phase = 0;
        var service = new CredentialOperationService(process.Object, (_, _) => Task.FromResult(true), root, fingerprint: (_, _) => Task.FromResult(phase++ == 0 ? "before" : "after"));
        var envelope = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes("secret"), null, DataProtectionScope.CurrentUser));
        var preview = await service.PreviewAsync("Ubuntu", "dev", envelope);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(preview.PreviewToken));

        Assert.Equal("Lifecycle.CredentialStateChanged", error.Message);
        process.VerifyNoOtherCalls();
    }
}
