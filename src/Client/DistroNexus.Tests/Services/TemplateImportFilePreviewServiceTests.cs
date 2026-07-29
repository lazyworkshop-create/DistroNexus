using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public sealed class TemplateImportFilePreviewServiceTests
{
    [Theory]
    [InlineData("\\\\server\\share\\template.json")]
    [InlineData("\\\\?\\C:\\device\\template.json")]
    [InlineData("C:\\bad\n.json")]
    public async Task PreviewAsync_RejectsUnsafeSourceBeforeTemplateAccess(string source)
    {
        var service = new TemplateImportFilePreviewService(null!, null!);
        await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewAsync(source));
    }

    [Fact]
    public async Task PreviewAsync_RejectsOversizedFileBeforeTemplateAccess()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            await File.WriteAllBytesAsync(path, new byte[1024 * 1024 + 1]);
            var service = new TemplateImportFilePreviewService(null!, null!);
            await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewAsync(path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
