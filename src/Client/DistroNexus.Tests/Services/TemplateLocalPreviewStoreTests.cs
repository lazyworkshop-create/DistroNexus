using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public sealed class TemplateLocalPreviewStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DistroNexus-template-local-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Preview_IsDurableSameSidAndSingleUse()
    {
        var issue = new TemplateLocalPreviewStore(_root, () => "S-1", Identity, Identity);
        var token = await issue.IssueAsync("import", "{\"Id\":\"demo\"}", default);

        var consumed = await new TemplateLocalPreviewStore(_root, () => "S-1", Identity, Identity).ConsumeAsync(token, "import", default);

        Assert.Equal("{\"Id\":\"demo\"}", consumed.Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => issue.ConsumeAsync(token, "import", default));
    }

    [Fact]
    public async Task Preview_RejectsForeignSidWrongOperationAndOversizedContent()
    {
        var issue = new TemplateLocalPreviewStore(_root, () => "S-1", Identity, Identity);
        var token = await issue.IssueAsync("remove", "demo", default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new TemplateLocalPreviewStore(_root, () => "S-2", Identity, Identity).ConsumeAsync(token, "remove", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => issue.IssueAsync("unknown", "demo", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => issue.IssueAsync("import", new string('x', 1024 * 1024 + 1), default));
    }

    private static byte[] Identity(byte[] value) => value;
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }
}
