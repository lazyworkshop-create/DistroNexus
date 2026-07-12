using System.Text;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DistroNexus.Tests.Services;

public class WslConfigurationServiceTests
{
    [Fact]
    public async Task Save_PreservesUnrelatedBytes_CreatesBackupAndRejectsStaleFingerprint()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, ".wslconfig");
            var original = Encoding.UTF8.GetBytes("# note\r\n[wsl2]\r\nmemory = 2GB ; keep\r\n[custom]\r\nx = y\r\n");
            await File.WriteAllBytesAsync(path, original);
            var service = new WslConfigService(NullLogger<WslConfigService>.Instance, dir);
            var read = await service.ReadAsync();
            var saved = await service.SaveAsync(new Dictionary<string, string?> { ["wsl2.memory"] = "4GB" }, read.Fingerprint);
            Assert.NotNull(saved.BackupPath); Assert.Equal(original, await File.ReadAllBytesAsync(saved.BackupPath!));
            Assert.Equal(Encoding.UTF8.GetString(original).Replace("2GB", "4GB"), await File.ReadAllTextAsync(path));
            await Assert.ThrowsAsync<ConfigurationConflictException>(() => service.SaveAsync(
                new Dictionary<string, string?> { ["wsl2.memory"] = "8GB" }, read.Fingerprint));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Save_RejectsInvalidAndCapabilityGatedValuesWithoutWriting()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, ".wslconfig"); await File.WriteAllTextAsync(path, "[wsl2]\nprocessors=2\n");
            var service = new WslConfigService(NullLogger<WslConfigService>.Instance, dir); var read = await service.ReadAsync();
            await Assert.ThrowsAsync<ConfigurationValidationException>(() => service.SaveAsync(
                new Dictionary<string, string?> { ["wsl2.processors"] = "0" }, read.Fingerprint));
            await Assert.ThrowsAsync<ConfigurationValidationException>(() => service.SaveAsync(
                new Dictionary<string, string?> { ["wsl2.firewall"] = "true" }, read.Fingerprint, new HashSet<string>()));
            Assert.Equal("[wsl2]\nprocessors=2\n", await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Save_AppendsGlobalSettingAfterUnterminatedFinalRecordWithoutConcatenatingOrChangingUnrelatedContent()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, ".wslconfig");
            await File.WriteAllTextAsync(path, "# unchanged\n[wsl2]\nmemory=2GB");
            var service = new WslConfigService(NullLogger<WslConfigService>.Instance, dir);
            var read = await service.ReadAsync();

            await service.SaveAsync(new Dictionary<string, string?> { ["wsl2.processors"] = "4" }, read.Fingerprint);

            Assert.Equal("# unchanged\n[wsl2]\nmemory=2GB\nprocessors=4", await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task ConcurrentSaves_WithSameFingerprint_AllowExactlyOneWriter()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, ".wslconfig"), "[wsl2]\nmemory=2GB\n");
            var service = new WslConfigService(NullLogger<WslConfigService>.Instance, dir); var read = await service.ReadAsync();
            var writes = new[] { "4GB", "8GB" }.Select(v => Task.Run(async () =>
            {
                try { await service.SaveAsync(new Dictionary<string, string?> { ["wsl2.memory"] = v }, read.Fingerprint); return true; }
                catch (ConfigurationConflictException) { return false; }
            }));
            Assert.Equal(1, (await Task.WhenAll(writes)).Count(x => x));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task EmptySave_DoesNotRewriteOrCreateBackup()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, ".wslconfig"); var bytes = Encoding.UTF8.GetBytes("[wsl2]\r\n# untouched\r\n"); await File.WriteAllBytesAsync(path, bytes);
            var service = new WslConfigService(NullLogger<WslConfigService>.Instance, dir); var read = await service.ReadAsync();
            var result = await service.SaveAsync(new Dictionary<string, string?>(), read.Fingerprint);
            Assert.Null(result.BackupPath); Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task LegacyFacade_ChangesBasicKeyWhilePreservingUnsupportedGatedKeys()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, ".wslconfig");
            var original = "[wsl2]\nmemory=2GB\nfirewall=true\ndnsTunneling=true\n"; await File.WriteAllTextAsync(path, original);
            var service = new WslConfigService(NullLogger<WslConfigService>.Instance, dir);
            await service.SetWslConfigAsync(new WslConfig { Memory = "4GB" });
            Assert.Equal(original.Replace("2GB", "4GB"), await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(dir, true); }
    }
}
