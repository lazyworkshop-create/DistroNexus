using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using System.Security.Cryptography;
using System.Text;

namespace DistroNexus.Tests.Services;

public sealed class LifecyclePathOperationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DistroNexus-Lifecycle-" + Guid.NewGuid().ToString("N"));
    public LifecyclePathOperationServiceTests() => Directory.CreateDirectory(_root);
    [Fact]
    public async Task Remove_ExecutesOnlyAfterGrantAndWritesRecoveryJournal()
    {
        var runtime = new FakeRuntime("Ubuntu"); var service = Service(runtime);
        var preview = await service.PreviewRemoveAsync("Ubuntu", false);
        var result = await service.ExecuteAsync(preview.PreviewToken);
        Assert.True(result.Succeeded); Assert.Equal(1, runtime.Removes); Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(_root, "lifecycle-recovery"), "*.json"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(preview.PreviewToken));
    }
    [Fact]
    public async Task Remove_RejectsKeepFilesBeforeIssuingGrant()
    { var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(new FakeRuntime("Ubuntu")).PreviewRemoveAsync("Ubuntu", true)); Assert.Equal("Lifecycle.KeepFilesUnavailable", ex.Message); }
    [Fact]
    public async Task Execute_RejectsStateDriftWithoutCallingRuntime()
    {
        var runtime = new FakeRuntime("Ubuntu"); var service = Service(runtime); var preview = await service.PreviewRemoveAsync("Ubuntu", false); runtime.Instances.Clear();
        var result = await service.ExecuteAsync(preview.PreviewToken); Assert.False(result.Succeeded); Assert.Equal("Lifecycle.StateChanged", result.OutcomeCode); Assert.Equal(0, runtime.Removes);
    }
    [Fact]
    public async Task Import_CancellationReturnsRecoveryIdAndPreservesOutcome()
    {
        var source = Path.Combine(_root, "source.tar"); await File.WriteAllTextAsync(source, "tar"); var root = Path.Combine(_root, "target"); Directory.CreateDirectory(root);
        var runtime = new FakeRuntime(); runtime.CancelImport = true; var result = await Service(runtime).ExecuteAsync((await Service(runtime).PreviewImportAsync("Ubuntu", source, root)).PreviewToken);
        Assert.False(result.Succeeded); Assert.Equal("Lifecycle.Cancelled", result.OutcomeCode); Assert.NotNull(result.RecoveryId);
    }
    [Fact]
    public async Task Grant_CorruptionFailsClosed()
    {
        var service = Service(new FakeRuntime("Ubuntu")); var preview = await service.PreviewRemoveAsync("Ubuntu", false); var path = Path.Combine(_root, "lifecycle-grants", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(preview.PreviewToken))) + ".grant");
        await File.WriteAllTextAsync(path, "corrupt"); var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(preview.PreviewToken)); Assert.Equal("Lifecycle.GrantInvalid", ex.Message);
    }
    [Fact]
    public async Task CorruptImportGrant_ReleasesReservation()
    {
        var source = Path.Combine(_root, "source.tar"); await File.WriteAllTextAsync(source, "tar"); var target = Path.Combine(_root, "target"); Directory.CreateDirectory(target); var service = Service(new FakeRuntime()); var preview = await service.PreviewImportAsync("Ubuntu", source, target);
        var grant = Path.Combine(_root, "lifecycle-grants", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(preview.PreviewToken))) + ".grant"); await File.WriteAllTextAsync(grant, "corrupt"); await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(preview.PreviewToken));
        await service.PreviewImportAsync("Ubuntu", source, target);
    }
    [Fact]
    public async Task Import_ReservationRejectsConcurrentPreviewForSameTarget()
    {
        var source = Path.Combine(_root, "source.tar"); await File.WriteAllTextAsync(source, "tar"); var target = Path.Combine(_root, "target"); Directory.CreateDirectory(target); var service = Service(new FakeRuntime());
        await service.PreviewImportAsync("Ubuntu", source, target); await Assert.ThrowsAsync<IOException>(() => service.PreviewImportAsync("Ubuntu", source, target));
    }
    [Fact]
    public async Task Grant_ExpiryFailsBeforeRuntimeExecution()
    {
        var clock = new AdjustableClock(DateTimeOffset.UtcNow); var runtime = new FakeRuntime("Ubuntu"); var service = new LifecyclePathOperationService(runtime, _root, null, null, clock); var preview = await service.PreviewRemoveAsync("Ubuntu", false); clock.Advance(TimeSpan.FromMinutes(3));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(preview.PreviewToken)); Assert.Equal("Lifecycle.GrantExpired", ex.Message); Assert.Equal(0, runtime.Removes);
    }
    [Fact]
    public async Task ExpiredImportGrant_ReleasesReservation()
    {
        var source = Path.Combine(_root, "source.tar"); await File.WriteAllTextAsync(source, "tar"); var target = Path.Combine(_root, "target"); Directory.CreateDirectory(target); var clock = new AdjustableClock(DateTimeOffset.UtcNow); var service = new LifecyclePathOperationService(new FakeRuntime(), _root, null, null, clock); var preview = await service.PreviewImportAsync("Ubuntu", source, target); clock.Advance(TimeSpan.FromMinutes(3)); await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(preview.PreviewToken)); await service.PreviewImportAsync("Ubuntu", source, target);
    }
    [Fact]
    public async Task SidMismatchImportGrant_ReleasesReservation()
    {
        var source = Path.Combine(_root, "source.tar"); await File.WriteAllTextAsync(source, "tar"); var target = Path.Combine(_root, "target"); Directory.CreateDirectory(target); var sid = "S-1-test-a"; var service = new LifecyclePathOperationService(new FakeRuntime(), _root, null, null, null, () => sid); var preview = await service.PreviewImportAsync("Ubuntu", source, target); sid = "S-1-test-b"; await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(preview.PreviewToken)); sid = "S-1-test-a"; await service.PreviewImportAsync("Ubuntu", source, target);
    }
    [Fact]
    public void Resolver_RejectsDirectoryReparseTraversal()
    {
        var real = Path.Combine(_root, "real"); var link = Path.Combine(_root, "link"); Directory.CreateDirectory(real); try { Directory.CreateSymbolicLink(link, real); } catch (IOException) { return; }
        Assert.Throws<ArgumentException>(() => new LifecyclePathResolver().ResolveApprovedRoot(link));
    }
    [Fact]
    public void Resolver_RejectsFileReparseSource()
    {
        var real = Path.Combine(_root, "real.tar"); var link = Path.Combine(_root, "link.tar"); File.WriteAllText(real, "tar"); try { File.CreateSymbolicLink(link, real); } catch (IOException) { return; }
        Assert.Throws<ArgumentException>(() => new LifecyclePathResolver().ResolveImportSource(link));
    }
    [Fact]
    public async Task CancellationAtRemoveCheckpointHasDurableRecovery()
    {
        var runtime = new FakeRuntime("Ubuntu") { CancelRemove = true }; var result = await Service(runtime).ExecuteAsync((await Service(runtime).PreviewRemoveAsync("Ubuntu", false)).PreviewToken);
        Assert.Equal("Lifecycle.Cancelled", result.OutcomeCode); Assert.NotNull(result.RecoveryId);
    }
    [Fact]
    public void Bridge_RegistersOnlyFixedLifecyclePreviewAndTokenExecuteRoutes()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "DistroNexus.WorkspaceBridge", "Program.cs"));
        Assert.Contains("instance.remove.preview.v1", text); Assert.Contains("ValidatePayload(request, [\"PreviewToken\"], [\"PreviewToken\"])", text); Assert.DoesNotContain("instance.remove.execute.v1\" => await instances", text);
    }
    [Fact]
    public void Resolver_RejectsSystemRoot() => Assert.Throws<ArgumentException>(() => new LifecyclePathResolver().ResolveApprovedRoot(Path.GetPathRoot(_root)!));
    [Theory]
    [InlineData("\\\\server\\share")]
    [InlineData("\\\\?\\C:\\device")]
    public void Resolver_RejectsUntrustedRoots(string value) => Assert.Throws<ArgumentException>(() => new LifecyclePathResolver().ResolveApprovedRoot(value));
    private LifecyclePathOperationService Service(FakeRuntime runtime) => new(runtime, _root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }
    private sealed class FakeRuntime(params string[] names) : ILifecyclePathRuntime
    {
        public List<WslInstance> Instances { get; } = names.Select(x => new WslInstance { Name = x, State = "Stopped" }).ToList(); public int Removes { get; private set; } public bool CancelImport { get; set; } public bool CancelRemove { get; set; }
        public Task<List<WslInstance>> GetInstancesAsync(CancellationToken ct = default) => Task.FromResult(Instances.ToList());
        public Task RemoveAsync(string n, bool k, CancellationToken ct = default) { if (CancelRemove) throw new OperationCanceledException(ct); Removes++; Instances.RemoveAll(x => x.Name == n); return Task.CompletedTask; }
        public Task MoveAsync(string n, string t, CancellationToken ct = default) => Task.CompletedTask;
        public Task RenameAsync(string n, string nn, CancellationToken ct = default) => Task.CompletedTask;
        public Task ExportAsync(string n, string d, bool s, CancellationToken ct = default) => Task.CompletedTask;
        public Task ImportAsync(string n, string s, string t, CancellationToken ct = default) { if (CancelImport) throw new OperationCanceledException(ct); Instances.Add(new WslInstance { Name = n, State = "Stopped" }); return Task.CompletedTask; }
    }
    private sealed class AdjustableClock(DateTimeOffset now) : TimeProvider
    { private DateTimeOffset _now = now; public override DateTimeOffset GetUtcNow() => _now; public void Advance(TimeSpan value) => _now += value; }
}
