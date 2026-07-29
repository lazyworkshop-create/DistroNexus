using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class FixedBackupRuntimeTests
{
    [Fact]
    public async Task PreviewBackup_SweepsEveryStaleOrCorruptGrantBeforeApplyingCapacityLimit()
    {
        var root = Path.Combine(Path.GetTempPath(), "DistroNexusFixedBackupTests", Guid.NewGuid().ToString("N"));
        var grantRoot = Path.Combine(root, "backup-grants");
        Directory.CreateDirectory(grantRoot);
        try
        {
            for (var index = 0; index < 300; index++)
                await File.WriteAllBytesAsync(Path.Combine(grantRoot, $"{index:X64}.grant"), [0x00]);

            var instances = new Mock<IWslManagerService>(MockBehavior.Strict);
            instances.Setup(service => service.GetInstancesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([new WslInstance { Name = "Ubuntu", Version = 2 }]);
            var runtime = new FixedBackupRuntime(instances.Object, new Mock<IProcessRunner>(MockBehavior.Strict).Object, root);

            var preview = await runtime.PreviewBackupAsync("Ubuntu", 1);

            Assert.Equal("Backup", preview.Operation);
            Assert.Single(Directory.EnumerateFiles(grantRoot, "*.grant", SearchOption.TopDirectoryOnly));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
