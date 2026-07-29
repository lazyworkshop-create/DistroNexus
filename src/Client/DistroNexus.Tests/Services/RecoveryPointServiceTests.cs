using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DistroNexus.Tests.Services;

public sealed class RecoveryPointServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DistroNexusRecoveryTests", Guid.NewGuid().ToString("N"));
    private readonly FakeRuntime _runtime = new();
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public async Task Create_WritesPayloadBeforeManifest_AndVerifiesChecksum()
    {
        var service = Service(); var target = Path.Combine(_root, "payloads");
        var preview = await service.PreviewCreateAsync(new("Ubuntu", "Before upgrade", target, RecoveryPointFormat.Tar, "note", ["safe"]));
        var point = await service.CreateAsync(new("Ubuntu", "Before upgrade", target, RecoveryPointFormat.Tar, "note", ["safe"]), preview.Token);
        Assert.Equal(RecoveryPointVerification.Verified, await service.VerifyAsync(point.Manifest.Id));
        Assert.True(File.Exists(Path.Combine(point.DirectoryPath, "manifest.json")));
        Assert.StartsWith(Path.GetFullPath(target), point.DirectoryPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Before upgrade", point.Manifest.Name);
    }

    [Fact]
    public async Task FailedExport_LeavesNoValidRecoveryPoint()
    {
        _runtime.ThrowOnExport = true; var service = Service(); var request = new RecoveryPointCreateRequest("Ubuntu", "bad", Path.Combine(_root, "payloads"));
        var preview = await service.PreviewCreateAsync(request);
        await Assert.ThrowsAsync<IOException>(() => service.CreateAsync(request, preview.Token));
        Assert.Empty(await service.ListAsync());
    }

    [Fact]
    public async Task PreviewGrant_SurvivesFreshService_AndIsSingleUse()
    {
        var request = new RecoveryPointCreateRequest("Ubuntu", "durable", Path.Combine(_root, "payloads"));
        var first = Service();
        var preview = await first.PreviewCreateAsync(request);

        var fresh = Service();
        var point = await fresh.CreateAsync(request, preview.Token);

        Assert.Equal("durable", point.Manifest.Name);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().CreateAsync(request, preview.Token));
    }

    [Fact]
    public async Task PreviewGrant_RejectsDifferentSameMachineUser()
    {
        var request = new RecoveryPointCreateRequest("Ubuntu", "identity", Path.Combine(_root, "payloads"));
        var protect = new Func<byte[], byte[]>(x => x);
        var first = new RecoveryPointService(_runtime, root: Path.Combine(_root, "recovery"), sid: () => "S-1-test-a", protect: protect, unprotect: protect);
        var preview = await first.PreviewCreateAsync(request);
        var other = new RecoveryPointService(_runtime, root: Path.Combine(_root, "recovery"), sid: () => "S-1-test-b", protect: protect, unprotect: protect);

        await Assert.ThrowsAsync<InvalidOperationException>(() => other.CreateAsync(request, preview.Token));
    }

    [Fact]
    public async Task NotesPreviewGrant_SurvivesFreshService_ExecutesCanonicalData_AndCannotReplay()
    {
        var first = Service();
        var point = await Create(first, "notes", Path.Combine(_root, "payloads"));
        var preview = await first.PreviewUpdateNotesAsync(point.Manifest.Id, "updated", ["safe", "local"], true);

        await Service().ExecutePreviewAsync(preview.Token);

        var updated = Assert.Single(await Service().ListAsync());
        Assert.Equal("updated", updated.Manifest.Description);
        Assert.Equal(["safe", "local"], updated.Manifest.Tags);
        Assert.True(updated.Manifest.Pinned);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().ExecutePreviewAsync(preview.Token));
    }

    [Fact]
    public async Task NotesPreviewGrant_RejectsTamperForeignSidAndParallelReplay()
    {
        var protect = new Func<byte[], byte[]>(x => x);
        var root = Path.Combine(_root, "notes-grants");
        var first = new RecoveryPointService(_runtime, root: root, sid: () => "S-1-test-a", protect: protect, unprotect: protect);
        var point = await Create(first, "notes", Path.Combine(_root, "payloads"));
        var tampered = await first.PreviewUpdateNotesAsync(point.Manifest.Id, "tampered", [], false);
        var grant = Directory.EnumerateFiles(Path.Combine(root, "grants"), "*.grant").Single();
        await File.WriteAllTextAsync(grant, "not-a-grant");
        await Assert.ThrowsAsync<InvalidOperationException>(() => first.ExecutePreviewAsync(tampered.Token));

        var preview = await first.PreviewUpdateNotesAsync(point.Manifest.Id, "updated", [], false);
        var foreign = new RecoveryPointService(_runtime, root: root, sid: () => "S-1-test-b", protect: protect, unprotect: protect);
        await Assert.ThrowsAsync<InvalidOperationException>(() => foreign.ExecutePreviewAsync(preview.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => first.ExecutePreviewAsync(preview.Token));

        var parallel = await first.PreviewUpdateNotesAsync(point.Manifest.Id, "parallel", [], false);
        var executions = await Task.WhenAll(
            ExecuteOutcomeAsync(first, parallel.Token),
            ExecuteOutcomeAsync(new RecoveryPointService(_runtime, root: root, sid: () => "S-1-test-a", protect: protect, unprotect: protect), parallel.Token));
        Assert.Equal(1, executions.Count(x => x));
    }

    [Fact]
    public async Task NotesPreviewGrant_RejectsExpiry_AndPreviewSweepRemovesStaleConsumedArtifacts()
    {
        var protect = new Func<byte[], byte[]>(x => x);
        var root = Path.Combine(_root, "notes-expiry");
        var service = new RecoveryPointService(_runtime, root: root, sid: () => "S-1-test", protect: protect, unprotect: protect);
        var point = await Create(service, "notes", Path.Combine(_root, "payloads"));
        var preview = await service.PreviewUpdateNotesAsync(point.Manifest.Id, "expired", [], false);
        var grantRoot = Path.Combine(root, "grants");
        var grant = Directory.EnumerateFiles(grantRoot, "*.grant").Single();
        var envelope = JsonNode.Parse(await File.ReadAllTextAsync(grant))!.AsObject();
        envelope["ExpiresAt"] = DateTimeOffset.UtcNow.AddMinutes(-1);
        await File.WriteAllTextAsync(grant, envelope.ToJsonString());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecutePreviewAsync(preview.Token));

        var stale = Path.Combine(grantRoot, "orphan.grant.consumed.stale");
        await File.WriteAllTextAsync(stale, "stale");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddMinutes(-11));
        await service.PreviewUpdateNotesAsync(point.Manifest.Id, "fresh", [], false);
        Assert.False(File.Exists(stale));
    }

    [Fact]
    public async Task Restore_BlocksCorruptPayload_AndNeverOverwrites()
    {
        var service = Service(); var request = new RecoveryPointCreateRequest("Ubuntu", "good", Path.Combine(_root, "payloads"));
        var p = await service.PreviewCreateAsync(request); var point = await service.CreateAsync(request, p.Token);
        await File.AppendAllTextAsync(Path.Combine(point.DirectoryPath, point.Manifest.PayloadFile), "corrupt");
        var restore = new RecoveryRestoreRequest(point.Manifest.Id, "Clone", Path.Combine(_root, "clone"));
        var preview = await service.PreviewRestoreAsync(restore);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreAsync(restore, preview.Token));
        Assert.Empty(_runtime.Imported);
        _runtime.Existing.Add("Taken");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewRestoreAsync(restore with { TargetInstance = "Taken" }));
    }

    [Fact]
    public async Task ImportFailure_DoesNotUnregisterAnExternalRegistrationThatRacesAfterPrecheck()
    {
        var service = Service(); var request = new RecoveryPointCreateRequest("Ubuntu", "good", Path.Combine(_root, "payloads"));
        var p = await service.PreviewCreateAsync(request); var point = await service.CreateAsync(request, p.Token);
        _runtime.ThrowOnImport = true; _runtime.RegisterExternalOnImportFailure = true;
        var restore = new RecoveryRestoreRequest(point.Manifest.Id, "Clone", Path.Combine(_root, "clone"));
        var preview = await service.PreviewRestoreAsync(restore);
        var failure = await Assert.ThrowsAsync<WslOperationFailedException>(() => service.RestoreAsync(restore, preview.Token));
        Assert.Equal(DistroNexusErrorCode.RecoveryManualRecoveryRequired, failure.Code);
        Assert.Contains("Clone", _runtime.Existing);
        var journal = await File.ReadAllTextAsync(Directory.EnumerateFiles(Path.Combine(_root, "recovery", "operations"), "*.json").Single());
        Assert.Contains("ManualRecoveryRequired", journal);
        Assert.Contains("manual unregister", journal, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BootVerificationFailure_RetainsManualRecoveryEvidence_AndNeverAutoCleansRegistration()
    {
        var service = Service(); var request = new RecoveryPointCreateRequest("Ubuntu", "good", Path.Combine(_root, "payloads"));
        var p = await service.PreviewCreateAsync(request); var point = await service.CreateAsync(request, p.Token);
        _runtime.Boots = false;
        var restore = new RecoveryRestoreRequest(point.Manifest.Id, "Clone", Path.Combine(_root, "clone"));
        var preview = await service.PreviewRestoreAsync(restore);
        var failure = await Assert.ThrowsAsync<WslOperationFailedException>(() => service.RestoreAsync(restore, preview.Token));
        Assert.Equal(DistroNexusErrorCode.RecoveryManualRecoveryRequired, failure.Code);
        Assert.Contains("Clone", _runtime.Existing);
        var journal = Directory.EnumerateFiles(Path.Combine(_root, "recovery", "operations"), "*.json").Single();
        Assert.Contains("ManualRecoveryRequired", await File.ReadAllTextAsync(journal));
        await service.ListAsync();
        Assert.Contains("Clone", _runtime.Existing);
    }

    [Fact]
    public async Task CancellationDuringImport_WithoutSuccessfulRegistrationProof_DoesNotCleanup()
    {
        var service = Service(); var request = new RecoveryPointCreateRequest("Ubuntu", "good", Path.Combine(_root, "payloads"));
        var p = await service.PreviewCreateAsync(request); var point = await service.CreateAsync(request, p.Token);
        _runtime.ThrowCancelledOnImport = true;
        var restore = new RecoveryRestoreRequest(point.Manifest.Id, "Clone", Path.Combine(_root, "clone"));
        var preview = await service.PreviewRestoreAsync(restore);
        // A canceled import never establishes that a registration was created by this operation.
        var updates = new List<RecoveryOperationProgress>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RestoreAsync(restore, preview.Token, progress: new InlineProgress(updates)));
        Assert.Contains("ManualRecoveryRequired", await File.ReadAllTextAsync(Directory.EnumerateFiles(Path.Combine(_root, "recovery", "operations"), "*.json").Single()));
        Assert.Contains(updates, x => x.Stage == "Importing");
    }

    [Fact]
    public async Task List_NeverReconcilesAnIncompleteOperationJournal()
    {
        var service = Service();
        var target = Path.Combine(_root, "orphan");
        var id = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.Combine(_root, "recovery", "operations"));
        var marker = Path.Combine(_root, $".distronexus-recovery-{id}.json");
        var contents = JsonSerializer.Serialize(new { OperationId = id, TargetInstance = "Orphan", TargetDirectory = target, State = "Importing" });
        var registration = new RecoveryRegistration("operation-registration", target);
        contents = JsonSerializer.Serialize(new { OperationId = id, TargetInstance = "Orphan", TargetDirectory = target, State = "Imported", Registration = registration });
        _runtime.Existing.Add("Orphan"); _runtime.Registrations["Orphan"] = registration;
        await File.WriteAllTextAsync(marker, contents);
        await File.WriteAllTextAsync(Path.Combine(_root, "recovery", "operations", id + ".json"), contents);

        await service.ListAsync();

        Assert.Contains("Orphan", _runtime.Existing);
        Assert.True(File.Exists(marker));
    }

    [Fact]
    public async Task List_DoesNotTrustJournalWhenDurableMarkerDoesNotMatch()
    {
        var service = Service();
        var target = Path.Combine(_root, "orphan"); var id = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(Path.Combine(_root, "recovery", "operations"));
        await File.WriteAllTextAsync(Path.Combine(_root, $".distronexus-recovery-{id}.json"), JsonSerializer.Serialize(new { OperationId = id, TargetInstance = "Other", TargetDirectory = target, State = "Importing" }));
        await File.WriteAllTextAsync(Path.Combine(_root, "recovery", "operations", id + ".json"), JsonSerializer.Serialize(new { OperationId = id, TargetInstance = "Orphan", TargetDirectory = target, State = "Imported", Registration = new RecoveryRegistration("operation-registration", target) }));

        await service.ListAsync();

        Assert.False(_runtime.Existing.Contains("Orphan"));
    }

    [Fact]
    public async Task Retention_PreservesPinnedPoint_AndDeletionNeverImplicitlyRemovesOnlyPoint()
    {
        var service = Service(); var destination = Path.Combine(_root, "payloads");
        var a = await Create(service, "first", destination); var b = await Create(service, "second", destination);
        await service.UpdateNotesAsync(a.Manifest.Id, "", [], true);
        await service.ApplyRetentionAsync("Ubuntu", 1);
        Assert.Contains((await service.ListAsync()), x => x.Manifest.Id == a.Manifest.Id);
        await service.DeleteAsync(b.Manifest.Id, (await service.PreviewDeleteAsync(b.Manifest.Id)).Token);
        // Explicit confirmation permits deleting the final point; only retention is implicitly guarded.
        await service.DeleteAsync(a.Manifest.Id, (await service.PreviewDeleteAsync(a.Manifest.Id)).Token);
        Assert.Empty(await service.ListAsync());
    }

    [Fact]
    public async Task Retention_IsPersistedPerSource()
    {
        var service = Service();
        await service.ApplyRetentionAsync("Ubuntu", 3);
        Assert.Equal(3, await Service().GetRetentionAsync("Ubuntu"));
    }

    [Fact]
    public async Task Vhdx_IsGatedBySupportedWsl2()
    {
        _runtime.Source = new(1, 10, false, false, false); var service = Service();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewCreateAsync(new("Ubuntu", "vhd", Path.Combine(_root, "x"), RecoveryPointFormat.Vhdx)));
    }

    [Fact]
    public async Task VhdxRestore_IsGatedForStandardImportAsWellAsImportInPlace()
    {
        var service = Service();
        var create = new RecoveryPointCreateRequest("Ubuntu", "vhd", Path.Combine(_root, "payloads"), RecoveryPointFormat.Vhdx);
        var preview = await service.PreviewCreateAsync(create);
        var point = await service.CreateAsync(create, preview.Token);
        _runtime.Source = new(1, 10, false, false, false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewRestoreAsync(
            new RecoveryRestoreRequest(point.Manifest.Id, "Clone", Path.Combine(_root, "clone"))));
        Assert.Empty(_runtime.Imported);
    }

    [Fact]
    public async Task Clone_PrevalidatesVhdxImportBeforeCreatingAnyRecoveryPoint()
    {
        _runtime.Source = new(2, 10, false, true, false);
        var service = Service();
        var request = new RecoveryCloneRequest(new("Ubuntu", "clone", Path.Combine(_root, "payloads"), RecoveryPointFormat.Vhdx), "Clone", Path.Combine(_root, "clone"), true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewCloneAsync(request));
        Assert.Empty(await service.ListAsync()); Assert.Empty(_runtime.Lifecycle);
    }

    [Fact]
    public async Task SuccessfulClone_RetainsItsPortableRecoveryPoint()
    {
        var service = Service();
        var request = new RecoveryCloneRequest(new("Ubuntu", "clone", Path.Combine(_root, "payloads")), "Clone", Path.Combine(_root, "clone"));
        var preview = await service.PreviewCloneAsync(request);
        await service.RestoreCloneAsync(request, preview.Token);
        var retained = await service.ListAsync();
        Assert.Single(retained); Assert.Contains("Clone", _runtime.Imported);
        Assert.True(File.Exists(Path.Combine(retained[0].DirectoryPath, retained[0].Manifest.PayloadFile)));
    }

    [Fact]
    public async Task ImportInPlace_ServicePassesEmptyManagedTargetAcrossRuntimeContract_AndRetainsVhdxPayload()
    {
        var service = Service();
        var create = new RecoveryPointCreateRequest("Ubuntu", "vhd", Path.Combine(_root, "payloads"), RecoveryPointFormat.Vhdx);
        var point = await service.CreateAsync(create, (await service.PreviewCreateAsync(create)).Token);
        var restore = new RecoveryRestoreRequest(point.Manifest.Id, "Clone", "", ImportInPlace: true);

        var preview = await service.PreviewRestoreAsync(restore);
        await service.RestoreAsync(restore, preview.Token);

        Assert.Contains("Clone", _runtime.Imported);
        Assert.NotNull(_runtime.LastImport);
        Assert.Equal("", _runtime.LastImport.Value.TargetDirectory);
        Assert.True(_runtime.LastImport.Value.ImportInPlace);
        Assert.True(File.Exists(Path.Combine(point.DirectoryPath, point.Manifest.PayloadFile)));
    }

    [Fact]
    public async Task ImportInPlace_RejectsAManagedTargetDirectory()
    {
        var service = Service();
        var point = await Create(service, "tar", Path.Combine(_root, "payloads"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewRestoreAsync(
            new RecoveryRestoreRequest(point.Manifest.Id, "Clone", Path.Combine(_root, "managed"), ImportInPlace: true)));
    }

    [Fact]
    public async Task ConfirmedRestartAfterExport_StopsThenRestartsRunningSource()
    {
        _runtime.Source = new(2, 10, true, true, true); var service = Service();
        var request = new RecoveryPointCreateRequest("Ubuntu", "consistent", Path.Combine(_root, "payloads"), RestartAfterExport: true);
        var preview = await service.PreviewCreateAsync(request); await service.CreateAsync(request, preview.Token);
        Assert.Equal(["stop", "export", "start"], _runtime.Lifecycle);
    }

    [Fact]
    public async Task RecoveryOffer_IsAvailableOnlyWhenTheSourceCanBeRead_AndNeverExports()
    {
        var offers = new RecoveryOfferService(_runtime);

        var available = await offers.GetOfferAsync("Ubuntu", RecoveryOfferReason.TemplateApplication);
        Assert.True(available.IsAvailable);
        Assert.Equal("RecoveryOffer.OptionalBeforeOperation", available.MessageKey);
        Assert.Empty(_runtime.Lifecycle);

        _runtime.ThrowOnSource = true;
        var unavailable = await offers.GetOfferAsync("Ubuntu", RecoveryOfferReason.MajorConfigurationChange);
        Assert.False(unavailable.IsAvailable);
        Assert.Equal("RecoveryOffer.RuntimeUnavailable", unavailable.MessageKey);
        Assert.Empty(_runtime.Lifecycle);
    }

    [Fact]
    public async Task Delete_RequiresCurrentPreviewAndRejectsStaleOrInvalidTokens()
    {
        var service = Service(); var point = await Create(service, "only", Path.Combine(_root, "payloads"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(point.Manifest.Id, "invalid"));
        Assert.Single(await service.ListAsync());

        var stale = await service.PreviewDeleteAsync(point.Manifest.Id);
        await service.UpdateNotesAsync(point.Manifest.Id, "changed", [], false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(point.Manifest.Id, stale.Token));
        Assert.Single(await service.ListAsync());

        var valid = await service.PreviewDeleteAsync(point.Manifest.Id);
        await service.DeleteAsync(point.Manifest.Id, valid.Token);
        Assert.Empty(await service.ListAsync());
    }

    [Fact]
    public async Task Restore_AtomicallyReservesTargetAcrossConcurrentCalls()
    {
        var service = Service(); var point = await Create(service, "good", Path.Combine(_root, "payloads"));
        var request = new RecoveryRestoreRequest(point.Manifest.Id, "Clone", Path.Combine(_root, "clone"));
        var first = await service.PreviewRestoreAsync(request); var second = await service.PreviewRestoreAsync(request);
        _runtime.ImportGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var importing = service.RestoreAsync(request, first.Token);
        await _runtime.ImportStarted.Task;
        var failure = await Assert.ThrowsAsync<WslOperationFailedException>(() => service.RestoreAsync(request, second.Token));
        Assert.Equal(DistroNexusErrorCode.RecoveryTargetReserved, failure.Code);
        _runtime.ImportGate.SetResult(); await importing;
    }

    [Fact]
    public async Task Restore_AtomicallyReservesInstanceNameAcrossDifferentTargetDirectories()
    {
        var firstService = Service(); var secondService = Service();
        var point = await Create(firstService, "good", Path.Combine(_root, "payloads"));
        var firstRequest = new RecoveryRestoreRequest(point.Manifest.Id, "Clone", Path.Combine(_root, "clone-one"));
        var secondRequest = new RecoveryRestoreRequest(point.Manifest.Id, "Clone", Path.Combine(_root, "clone-two"));
        var first = await firstService.PreviewRestoreAsync(firstRequest);
        var second = await secondService.PreviewRestoreAsync(secondRequest);
        _runtime.ImportGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var importing = firstService.RestoreAsync(firstRequest, first.Token);
        await _runtime.ImportStarted.Task;
        var failure = await Assert.ThrowsAsync<WslOperationFailedException>(() => secondService.RestoreAsync(secondRequest, second.Token));

        Assert.Equal(DistroNexusErrorCode.RecoveryTargetReserved, failure.Code);
        _runtime.ImportGate.SetResult(); await importing;
    }

    [Fact]
    public async Task Create_RejectsAnyPreviewedRequestFieldThatChanges()
    {
        var service = Service();
        var request = new RecoveryPointCreateRequest("Ubuntu", "original", Path.Combine(_root, "payloads"), Description: "one", Tags: ["first"], RestartAfterExport: false);
        var preview = await service.PreviewCreateAsync(request);
        var changed = request with { Name = "renamed", Description = "two", Tags = ["changed"], RestartAfterExport = true };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(changed, preview.Token));
        Assert.Empty(_runtime.Lifecycle);
    }

    [Fact]
    public async Task Restore_RejectsChangedChecksumOrImportModeAfterPreview()
    {
        var service = Service(); var point = await Create(service, "good", Path.Combine(_root, "payloads"));
        var request = new RecoveryRestoreRequest(point.Manifest.Id, "Clone", Path.Combine(_root, "clone"), true, false);
        var preview = await service.PreviewRestoreAsync(request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreAsync(request with { VerifyChecksum = false }, preview.Token));
        Assert.Empty(_runtime.Imported);
    }

    [Fact]
    public async Task ConcurrentRetentionUpdates_PreserveBothSourcesInVersionedState()
    {
        var first = Service(); var second = Service();
        await Task.WhenAll(first.ApplyRetentionAsync("Ubuntu", 2), second.ApplyRetentionAsync("Debian", 3));

        var fresh = Service();
        Assert.Equal(2, await fresh.GetRetentionAsync("Ubuntu"));
        Assert.Equal(3, await fresh.GetRetentionAsync("Debian"));
    }

    [Fact]
    public async Task UnifiedHistory_UsesPersistedBackupDestinationAfterScheduleRemoval()
    {
        var destination = Path.Combine(_root, "backup-artifacts");
        Directory.CreateDirectory(destination);
        var artifact = Path.Combine(destination, "ubuntu.tar");
        await File.WriteAllTextAsync(artifact, "backup");
        var backups = new Mock<IBackupService>();
        backups.Setup(x => x.GetHealthHistoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new BackupHealthRecord("Ubuntu", DateTimeOffset.UtcNow, true, Destination: destination),
            new BackupHealthRecord("Ubuntu", DateTimeOffset.UtcNow.AddMinutes(-1), false, "DN-4006", "failed", destination)]);
        var service = new RecoveryPointService(_runtime, backups.Object, Path.Combine(_root, "recovery"));

        var history = await service.GetHistoryAsync();

        Assert.Contains(history, x => x.Kind == "BackupArtifact" && x.Location == artifact);
        Assert.Contains(history, x => x.Kind == "Backup" && x.Status == "Failed" && x.Location == destination);
        backups.Verify(x => x.GetSchedulesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task<RecoveryPointSummary> Create(RecoveryPointService service, string name, string destination)
    { var request = new RecoveryPointCreateRequest("Ubuntu", name, destination); var preview = await service.PreviewCreateAsync(request); return await service.CreateAsync(request, preview.Token); }
    private static async Task<bool> ExecuteOutcomeAsync(RecoveryPointService service, string token)
    { try { await service.ExecutePreviewAsync(token); return true; } catch (InvalidOperationException) { return false; } }
    private RecoveryPointService Service() => new(_runtime, root: Path.Combine(_root, "recovery"));

    private sealed class InlineProgress(List<RecoveryOperationProgress> updates) : IProgress<RecoveryOperationProgress>
    { public void Report(RecoveryOperationProgress value) => updates.Add(value); }

    private sealed class FakeRuntime : IRecoveryPointRuntime
    {
        public RecoveryRuntimeSource Source { get; set; } = new(2, 10, false, true, true);
        public bool ThrowOnExport { get; set; } public bool ThrowOnImport { get; set; } public bool RegisterExternalOnImportFailure { get; set; } public bool ThrowOnSource { get; set; } public bool ThrowCancelledOnImport { get; set; } public bool Boots { get; set; } = true;
        public TaskCompletionSource? ImportGate { get; set; }
        public TaskCompletionSource ImportStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public HashSet<string> Existing { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, RecoveryRegistration> Registrations { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Imported { get; } = [];
        public (string TargetDirectory, bool ImportInPlace)? LastImport { get; private set; }
        public List<string> Lifecycle { get; } = [];
        public Task<RecoveryRuntimeSource> GetSourceAsync(string instanceName, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSource) throw new IOException("source unavailable");
            return Task.FromResult(Source);
        }
        public async Task ExportAsync(string instanceName, string partialPayloadPath, RecoveryPointFormat format, CancellationToken cancellationToken = default) { Lifecycle.Add("export"); if (ThrowOnExport) throw new IOException("export failed"); Directory.CreateDirectory(Path.GetDirectoryName(partialPayloadPath)!); await File.WriteAllTextAsync(partialPayloadPath, "payload", cancellationToken); }
        public async Task ImportAsync(string operationId, string instanceName, string payloadPath, string targetDirectory, RecoveryPointFormat format, bool importInPlace, CancellationToken cancellationToken = default) { LastImport = (targetDirectory, importInPlace); ImportStarted.TrySetResult(); if (ImportGate is not null) await ImportGate.Task.WaitAsync(cancellationToken); if (ThrowCancelledOnImport) throw new OperationCanceledException(); if (ThrowOnImport) { if (RegisterExternalOnImportFailure) { Existing.Add(instanceName); Registrations[instanceName] = new RecoveryRegistration("external-registration", Path.Combine(Path.GetTempPath(), "external")); } throw new IOException("import failed"); } Imported.Add(instanceName); Existing.Add(instanceName); Registrations[instanceName] = new RecoveryRegistration(operationId, targetDirectory); }
        public Task<bool> InstanceExistsAsync(string instanceName, CancellationToken cancellationToken = default) => Task.FromResult(Existing.Contains(instanceName));
        public Task<bool> IsRegisteredAsync(string instanceName, CancellationToken cancellationToken = default) => Task.FromResult(Existing.Contains(instanceName));
        public Task<RecoveryRegistration?> GetRegistrationAsync(string instanceName, CancellationToken cancellationToken = default) => Task.FromResult(Registrations.TryGetValue(instanceName, out var value) ? value : null);
        public Task<bool> IsRunningAsync(string instanceName, CancellationToken cancellationToken = default) => Task.FromResult(Source.IsRunning);
        public Task StopAsync(string instanceName, CancellationToken cancellationToken = default) { Lifecycle.Add("stop"); return Task.CompletedTask; }
        public Task StartAsync(string instanceName, CancellationToken cancellationToken = default) { Lifecycle.Add("start"); return Task.CompletedTask; }
        public Task<bool> VerifyBootAsync(string instanceName, CancellationToken cancellationToken = default) => Task.FromResult(Boots);
    }
}
