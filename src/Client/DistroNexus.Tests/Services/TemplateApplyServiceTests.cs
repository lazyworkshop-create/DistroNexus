using System.Diagnostics;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class TemplateApplyServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DistroNexus.TemplateApplyTests", Guid.NewGuid().ToString("N"));
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Fact]
    public async Task PreviewExecute_ReplayIsRejected_AndWorkerCompletesApprovedScript()
    {
        var service = Create(new Template { Id="dev", Name="Dev", Version="1", TrustState=TemplateTrustState.BuiltIn, Scripts=[new TemplateScript { Name="setup", Type=TemplateScriptType.PowerShell, Content="Write-Output ok", TimeoutSeconds=10 }] });
        var preview = await service.PreviewAsync("Ubuntu", "dev", new Dictionary<string,string>{{"x","y"}}, true);
        Assert.NotNull(preview.PreviewToken);
        var started = await service.ExecuteAsync(preview.PreviewToken!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(preview.PreviewToken!));
        await service.RunOperationAsync(started.OperationId);
        var status = await service.StatusAsync(started.OperationId);
        Assert.Equal(TemplateOperationState.Succeeded, status.State);
        Assert.Equal(new[] { "setup" }, status.ExecutedScripts);
    }

    [Fact]
    public async Task Preview_RequiresExplicitRecoveryDecline()
    {
        var template = new Template { Id="dev", Name="Dev", Version="1", TrustState=TemplateTrustState.BuiltIn, Scripts=[new TemplateScript { Name="setup", Type=TemplateScriptType.PowerShell, Content="ok" }] };
        var service = Create(template, new RecoveryOffer(true,"Ubuntu",RecoveryOfferReason.TemplateApplication,"offer"));
        var preview = await service.PreviewAsync("Ubuntu", "dev", null, false);
        Assert.Null(preview.PreviewToken); Assert.True(preview.RequiresRecoveryDecline);
    }

    [Fact]
    public async Task CancelBeforeWorker_StartsNoRuntimeProcess()
    {
        var runtime = new RecordingRuntime();
        var service = Create(new Template { Id="dev", Name="Dev", Version="1", TrustState=TemplateTrustState.BuiltIn, Scripts=[new TemplateScript { Name="setup", Type=TemplateScriptType.PowerShell, Content="ok" }] }, runtime: runtime);
        var preview = await service.PreviewAsync("Ubuntu", "dev", null, true);
        var started = await service.ExecuteAsync(preview.PreviewToken!);
        await service.CancelAsync(started.OperationId); await service.RunOperationAsync(started.OperationId);
        Assert.Empty(runtime.Plans); Assert.Equal(TemplateOperationState.Cancelled,(await service.StatusAsync(started.OperationId)).State);
    }

    [Fact]
    public async Task Recover_ClaimedPendingScriptWithoutWorkerLock_IsInterruptedAndNeverRetried()
    {
        var root=Path.Combine(_root,"recovery"); var store=new TemplateApplyOperationStore(root); var id=new string('a',64); var now=DateTimeOffset.UtcNow;
        var pending=new TemplatePendingScriptRecord(0,TemplateScriptType.PowerShell,new string('b',64),TemplatePendingScriptState.Claimed,"attempt",now,now,null,null);
        await store.CreateAsync(new TemplateApplyOperationRecord(1,id,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,TemplateOperationState.Running,now,now.AddMinutes(1),now,0,1,"setup","Running",null,[],false,pending));
        var recovered=await store.RecoverAsync(id);
        Assert.Equal(TemplateOperationState.Interrupted,recovered.State); Assert.Equal("Template.WorkerInterrupted",recovered.ErrorCode);
    }

    [Fact]
    public async Task StartClaimedChild_CancelAfterClaimBeforeStart_DoesNotInvokeChild()
    {
        var root=Path.Combine(_root,"cancel-race"); var store=new TemplateApplyOperationStore(root); var id=new string('c',64); var now=DateTimeOffset.UtcNow; var hash=new string('d',64);
        var pending=new TemplatePendingScriptRecord(0,TemplateScriptType.PowerShell,hash,TemplatePendingScriptState.Claimed,"attempt",now,now,null,null);
        await store.CreateAsync(new TemplateApplyOperationRecord(1,id,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,TemplateOperationState.Running,now,now.AddMinutes(1),now,0,1,"setup","Running",null,[],true,pending));
        var invoked=false;
        var child=await store.StartClaimedChildAsync(new GrantedTemplateScriptPlan(id,"Ubuntu",0,TemplateScriptType.PowerShell,10,"unused",hash),()=> { invoked=true; throw new InvalidOperationException(); });
        Assert.Null(child); Assert.False(invoked); Assert.Equal(TemplateOperationState.Cancelled,(await store.ReadAsync(id)).State);
    }

    [Fact]
    public async Task Recover_ImmediateStatusAfterExecute_KeepsQueuedUntilLaunchDeadline()
    {
        var root=Path.Combine(_root,"queued"); var store=new TemplateApplyOperationStore(root); var id=new string('e',64); var now=DateTimeOffset.UtcNow;
        await store.CreateAsync(new TemplateApplyOperationRecord(1,id,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,TemplateOperationState.Queued,now,now.AddMinutes(1),now,0,1,null,"Queued",null,[],false));
        Assert.Equal(TemplateOperationState.Queued,(await store.RecoverAsync(id)).State);
    }

    [Theory]
    [InlineData(TemplateOperationState.Queued, "Template.WorkerStartFailed")]
    [InlineData(TemplateOperationState.Running, "Template.WorkerInterrupted")]
    public async Task Cancel_StaleUnlockedOperationReturnsRecoveredTerminalStateWithoutRequestingCancellation(TemplateOperationState state, string errorCode)
    {
        var service=Create(new Template { Id="dev", Name="Dev", Version="1", TrustState=TemplateTrustState.BuiltIn });
        var store=new TemplateApplyOperationStore(Path.Combine(_root,"ops")); var id=state==TemplateOperationState.Queued ? new string('4',64) : new string('5',64); var now=DateTimeOffset.UtcNow;
        await store.CreateAsync(new TemplateApplyOperationRecord(1,id,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,state,now,now.AddSeconds(-1),now,0,0,null,state.ToString(),null,[],false));

        var result=await service.CancelAsync(id);

        Assert.False(result.Accepted); Assert.Equal(state==TemplateOperationState.Queued ? TemplateOperationState.Failed : TemplateOperationState.Interrupted,result.State);
        var terminal=await store.ReadAsync(id);
        Assert.False(terminal.CancelRequested); Assert.Equal(errorCode,terminal.ErrorCode);
    }

    [Fact]
    public async Task StartWorker_PersistsPidAndStartTimeUnderOperationState()
    {
        var root=Path.Combine(_root,"worker-start"); var store=new TemplateApplyOperationStore(root); var id=new string('8',64); var now=DateTimeOffset.UtcNow;
        await store.CreateAsync(new TemplateApplyOperationRecord(1,id,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,TemplateOperationState.Queued,now,now.AddMinutes(1),now,0,0,null,"Queued",null,[],false));
        var worker=await store.StartWorkerAsync(id,()=>Process.Start(new ProcessStartInfo("cmd.exe","/c exit 0") { UseShellExecute=false, CreateNoWindow=true })!);
        Assert.NotNull(worker);
        await worker!.WaitForExitAsync();
        var persisted=await store.ReadAsync(id);
        Assert.Equal(worker.Id,persisted.WorkerPid);
        Assert.NotNull(persisted.WorkerStartedAt);
        Assert.Equal(TemplateOperationState.Queued,persisted.State);
    }

    [Fact]
    public async Task StartWorker_FailureAndExpiredUnlockedQueue_ReachStableTerminalStates()
    {
        var root=Path.Combine(_root,"worker-failure"); var store=new TemplateApplyOperationStore(root); var id=new string('9',64); var now=DateTimeOffset.UtcNow;
        await store.CreateAsync(new TemplateApplyOperationRecord(1,id,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,TemplateOperationState.Queued,now,now.AddSeconds(-1),now,0,0,null,"Queued",null,[],false));
        Assert.Null(await store.StartWorkerAsync(id,()=>throw new InvalidOperationException("start failed")));
        var failed=await store.ReadAsync(id);
        Assert.Equal(TemplateOperationState.Failed,failed.State); Assert.Equal("Template.WorkerStartFailed",failed.ErrorCode);

        var queued=new string('7',64);
        await store.CreateAsync(new TemplateApplyOperationRecord(1,queued,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,TemplateOperationState.Queued,now,now.AddSeconds(-1),now,0,0,null,"Queued",null,[],false));
        using (store.TryAcquireWorkerLock(queued)) Assert.Equal(TemplateOperationState.Queued,(await store.RecoverAsync(queued)).State);
        var recovered=await store.RecoverAsync(queued);
        Assert.Equal(TemplateOperationState.Failed,recovered.State); Assert.Equal("Template.WorkerStartFailed",recovered.ErrorCode);
    }

    [Fact]
    public async Task StartClaimedChild_OutsideOrChangedStagingPlan_IsRejectedBeforeStart()
    {
        var staging=Path.Combine(_root,"staging"); var store=new TemplateApplyOperationStore(Path.Combine(_root,"forged"),staging); var id=new string('f',64); var now=DateTimeOffset.UtcNow; var file=Path.Combine(_root,"outside.ps1"); await File.WriteAllTextAsync(file,"bad"); var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(file))).ToLowerInvariant();
        var pending=new TemplatePendingScriptRecord(0,TemplateScriptType.PowerShell,hash,TemplatePendingScriptState.Claimed,"attempt",now,now,null,null);
        await store.CreateAsync(new TemplateApplyOperationRecord(1,id,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,TemplateOperationState.Running,now,now.AddMinutes(1),now,0,1,"setup","Running",null,[],false,pending));
        var started=false;
        var error=await Assert.ThrowsAsync<InvalidOperationException>(() => store.StartClaimedChildAsync(new GrantedTemplateScriptPlan(id,"Ubuntu",0,TemplateScriptType.PowerShell,10,file,hash),()=> { started=true; throw new InvalidOperationException(); }));
        Assert.Equal("Template.ExecutionPlanInvalid",error.Message); Assert.False(started);
    }

    [Fact]
    public async Task StartClaimedChild_StagingFileChangedAfterClaim_IsRejectedBeforeStart()
    {
        var staging=Path.Combine(_root,"staging-tamper"); var store=new TemplateApplyOperationStore(Path.Combine(_root,"tamper"),staging); var id=new string('1',64); var now=DateTimeOffset.UtcNow;
        var directory=Path.Combine(staging,id); Directory.CreateDirectory(directory); var file=Path.Combine(directory,"0000.ps1"); await File.WriteAllTextAsync(file,"Write-Output approved"); var hash=Hash(file);
        var pending=new TemplatePendingScriptRecord(0,TemplateScriptType.PowerShell,hash,TemplatePendingScriptState.Claimed,"attempt",now,now,null,null);
        await store.CreateAsync(new TemplateApplyOperationRecord(1,id,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,TemplateOperationState.Running,now,now.AddMinutes(1),now,0,1,"setup","Running",null,[],false,pending));
        await File.WriteAllTextAsync(file,"Write-Output tampered");
        var started=false;
        var error=await Assert.ThrowsAsync<InvalidOperationException>(() => store.StartClaimedChildAsync(new GrantedTemplateScriptPlan(id,"Ubuntu",0,TemplateScriptType.PowerShell,10,file,hash),()=> { started=true; throw new InvalidOperationException(); }));
        Assert.Equal("Template.ExecutionPlanInvalid",error.Message); Assert.False(started);
    }

    [Fact]
    public async Task FixedRuntime_CancelRequestDuringScript_KillsChildAndReportsCancelled()
    {
        var staging=Path.Combine(_root,"staging-cancel"); var store=new TemplateApplyOperationStore(Path.Combine(_root,"runtime-cancel"),staging); var id=new string('2',64); var now=DateTimeOffset.UtcNow;
        var directory=Path.Combine(staging,id); Directory.CreateDirectory(directory); var file=Path.Combine(directory,"0000.ps1"); await File.WriteAllTextAsync(file,"Start-Sleep -Seconds 30"); var hash=Hash(file);
        var pending=new TemplatePendingScriptRecord(0,TemplateScriptType.PowerShell,hash,TemplatePendingScriptState.Claimed,"attempt",now,now,null,null);
        await store.CreateAsync(new TemplateApplyOperationRecord(1,id,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,TemplateOperationState.Running,now,now.AddMinutes(1),now,0,1,"setup","Running",null,[],false,pending));
        var task=new FixedTemplateGrantedExecutionRuntime(store).ExecuteAsync(new GrantedTemplateScriptPlan(id,"Ubuntu",0,TemplateScriptType.PowerShell,60,file,hash));
        await WaitForChildAsync(store,id,task);
        Assert.True(await store.RequestCancelAsync(id));
        var result=await task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(result.Cancelled);
    }

    [Fact]
    public async Task OperationStore_AtomicReplacementRetriesTransientAccessFailures()
    {
        var root=Path.Combine(_root,"atomic-retry"); var id=new string('3',64); var now=DateTimeOffset.UtcNow;
        var initial=new TemplateApplyOperationRecord(1,id,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,TemplateOperationState.Running,now,now.AddMinutes(1),now,0,1,"setup","Running",null,[],false);
        var baseline=new TemplateApplyOperationStore(root);
        await baseline.CreateAsync(initial);
        var attempts=0;
        var store=new TemplateApplyOperationStore(root, null, (source,destination,overwrite) =>
        {
            if (attempts++ == 0) throw new FileContentionIOException();
            File.Move(source,destination,overwrite);
        });

        await store.WriteAsync(initial with { Message="Updated" });

        Assert.Equal(2,attempts);
        Assert.Equal("Updated",(await store.ReadAsync(id)).Message);
        Assert.Empty(Directory.EnumerateFiles(root,"*.tmp-*"));
    }

    [Fact]
    public async Task OperationStore_StatusReadAndWriteSerializeWithoutTemporaryFiles()
    {
        if (!OperatingSystem.IsWindows()) return;

        var root=Path.Combine(_root,"atomic-shared-reader"); var id=new string('7',64); var now=DateTimeOffset.UtcNow;
        var initial=new TemplateApplyOperationRecord(1,id,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,TemplateOperationState.Running,now,now.AddMinutes(1),now,0,1,"setup","Running",null,[],false);
        var store=new TemplateApplyOperationStore(root);
        await store.CreateAsync(initial);
        var reader=store.ReadAsync(id);
        var write=store.WriteAsync(initial with { Message="Updated" });
        await Task.WhenAll(reader,write).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Updated",(await store.ReadAsync(id)).Message);
        Assert.Empty(Directory.EnumerateFiles(root,"*.tmp-*"));
    }

    [Fact]
    public void OperationStore_AccessDeniedProbeIsNotTransient()
    {
        if (!OperatingSystem.IsWindows()) return;

        var destination=Path.Combine(_root,"access-denied-probe");
        Directory.CreateDirectory(destination);
        var accessDenied=new AccessDeniedException();

        Assert.False(TemplateApplyOperationStore.IsTransientSharingFailure(accessDenied,destination));
    }

    [Fact]
    public async Task OperationStore_AtomicReplacementDoesNotRetryPermanentAccessOrIoFailures()
    {
        var root=Path.Combine(_root,"atomic-no-retry"); var id=new string('4',64); var now=DateTimeOffset.UtcNow;
        var initial=new TemplateApplyOperationRecord(1,id,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,TemplateOperationState.Running,now,now.AddMinutes(1),now,0,1,"setup","Running",null,[],false);
        var baseline=new TemplateApplyOperationStore(root);
        await baseline.CreateAsync(initial);
        var ioAttempts=0;
        var ioStore=new TemplateApplyOperationStore(root, null, (_,_,_) => { ioAttempts++; throw new PermanentIOException(); });

        await Assert.ThrowsAsync<PermanentIOException>(() => ioStore.WriteAsync(initial with { Message="Updated" }));
        Assert.Equal(1,ioAttempts);

        var accessAttempts=0;
        var accessStore=new TemplateApplyOperationStore(root, null, (_,_,_) => { accessAttempts++; throw new AccessDeniedException(); });
        await Assert.ThrowsAsync<AccessDeniedException>(() => accessStore.WriteAsync(initial with { Message="Updated" }));

        Assert.Equal(1,accessAttempts);
        Assert.Equal("Running",(await baseline.ReadAsync(id)).Message);
        Assert.Empty(Directory.EnumerateFiles(root,"*.tmp-*"));
    }

    [Fact]
    public async Task OperationStore_AtomicReplacementHonorsCancellationWithoutReplacingRecord()
    {
        var root=Path.Combine(_root,"atomic-retry-cancel"); var id=new string('6',64); var now=DateTimeOffset.UtcNow;
        var initial=new TemplateApplyOperationRecord(1,id,TemplateApplyGrantStore.CurrentSid(),"Ubuntu","dev","1","","","",DigestFiles(),"",true,TemplateOperationState.Running,now,now.AddMinutes(1),now,0,1,"setup","Running",null,[],false);
        var baseline=new TemplateApplyOperationStore(root);
        await baseline.CreateAsync(initial);
        using var cancellation=new CancellationTokenSource();
        var store=new TemplateApplyOperationStore(root, null, (_,_,_) =>
        {
            cancellation.Cancel();
            throw new FileContentionIOException();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteAsync(initial with { Message="Updated" }, cancellation.Token));

        Assert.Equal("Running",(await baseline.ReadAsync(id)).Message);
        Assert.Empty(Directory.EnumerateFiles(root,"*.tmp-*"));
    }

    [Fact]
    public async Task RemoteMarketplace_SuccessPromotesExactlyOnce()
    {
        var (template,marketplace)=CreateRemoteMarketplaceTemplate();
        TemplateApplyService? service=null; TemplateOperationState? stateAtPromotion=null; string? operationId=null;
        marketplace.Setup(x=>x.CompleteSuccessfulExecutionAsync(template.SourceUrl,It.IsAny<TemplateManifestV2>(),It.IsAny<CancellationToken>())).Callback<string,TemplateManifestV2,CancellationToken>((_,_,_)=>stateAtPromotion=service!.StatusAsync(operationId!).GetAwaiter().GetResult().State).Returns(Task.CompletedTask);
        service=Create(template, marketplace:marketplace.Object); var preview=await service.PreviewAsync("Ubuntu","remote",null,true); var started=await service.ExecuteAsync(preview.PreviewToken!); operationId=started.OperationId;
        await service.RunOperationAsync(started.OperationId);
        marketplace.Verify(x=>x.CompleteSuccessfulExecutionAsync(template.SourceUrl,It.IsAny<TemplateManifestV2>(),It.IsAny<CancellationToken>()),Times.Once);
        Assert.Equal(TemplateOperationState.Succeeded,stateAtPromotion);
        Assert.Equal(TemplateOperationState.Succeeded,(await service.StatusAsync(started.OperationId)).State);
    }

    [Fact]
    public async Task RemoteMarketplace_FailureOrCancellationNeverPromotes()
    {
        var (failedTemplate,failedMarketplace)=CreateRemoteMarketplaceTemplate();
        var failed=Create(failedTemplate, runtime:new RecordingRuntime(new ProcessResult(1,"","",TimeSpan.Zero,false,false,false,1)), marketplace:failedMarketplace.Object); var preview=await failed.PreviewAsync("Ubuntu","remote",null,true); var started=await failed.ExecuteAsync(preview.PreviewToken!); await failed.RunOperationAsync(started.OperationId);
        failedMarketplace.Verify(x=>x.CompleteSuccessfulExecutionAsync(It.IsAny<string>(),It.IsAny<TemplateManifestV2>(),It.IsAny<CancellationToken>()),Times.Never);
        var (cancelTemplate,cancelMarketplace)=CreateRemoteMarketplaceTemplate();
        var cancelled=Create(cancelTemplate, marketplace:cancelMarketplace.Object); preview=await cancelled.PreviewAsync("Ubuntu","remote",null,true); started=await cancelled.ExecuteAsync(preview.PreviewToken!); await cancelled.CancelAsync(started.OperationId); await cancelled.RunOperationAsync(started.OperationId);
        cancelMarketplace.Verify(x=>x.CompleteSuccessfulExecutionAsync(It.IsAny<string>(),It.IsAny<TemplateManifestV2>(),It.IsAny<CancellationToken>()),Times.Never);
    }

    [Fact]
    public async Task RemoteMarketplace_ExecutableDriftBeforeRun_IsRejectedBeforeStartingScript()
    {
        var (template,marketplace)=CreateRemoteMarketplaceTemplate(); var runtime=new RecordingRuntime(); var service=Create(template,runtime:runtime,marketplace:marketplace.Object);
        var preview=await service.PreviewAsync("Ubuntu","remote",null,true); var started=await service.ExecuteAsync(preview.PreviewToken!);
        await File.WriteAllTextAsync(Path.Combine(template.MarketplaceArtifactRoot,"setup.ps1"),"tampered");
        await service.RunOperationAsync(started.OperationId);
        Assert.Empty(runtime.Plans); Assert.Equal(TemplateOperationState.Failed,(await service.StatusAsync(started.OperationId)).State);
        marketplace.Verify(x=>x.CompleteSuccessfulExecutionAsync(It.IsAny<string>(),It.IsAny<TemplateManifestV2>(),It.IsAny<CancellationToken>()),Times.Never);
    }

    [Fact]
    public async Task RemoteMarketplace_ArtifactRootDriftBeforeRun_IsRejectedBeforeStartingScript()
    {
        var (template,marketplace)=CreateRemoteMarketplaceTemplate(); var runtime=new RecordingRuntime(); var service=Create(template,runtime:runtime,marketplace:marketplace.Object);
        var preview=await service.PreviewAsync("Ubuntu","remote",null,true); var started=await service.ExecuteAsync(preview.PreviewToken!);
        var driftedRoot=Path.Combine(_root,"artifact-drift",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(driftedRoot); File.Copy(Path.Combine(template.MarketplaceArtifactRoot,"setup.ps1"),Path.Combine(driftedRoot,"setup.ps1"));
        var manifest=new TemplateManifestV2 { Id=template.Id,Version=template.Version,ArtifactSha256=template.ArtifactSha256,ExecutableFiles=template.MarketplaceExecutableFiles };
        marketplace.Setup(x=>x.GetVerifiedArtifactForExecutionAsync(template.SourceUrl,manifest,It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateArtifact(template.ArtifactSha256,driftedRoot,DateTimeOffset.UtcNow,template.Id,template.Version));
        await service.RunOperationAsync(started.OperationId);
        Assert.Empty(runtime.Plans); Assert.Equal(TemplateOperationState.Failed,(await service.StatusAsync(started.OperationId)).State);
    }

    private TemplateApplyService Create(Template template, RecoveryOffer? offer = null, RecordingRuntime? runtime = null, ITemplateMarketplaceService? marketplace = null)
    {
        Directory.CreateDirectory(_root); var t = new FakeTemplateService(template, offer ?? new RecoveryOffer(false,"Ubuntu",RecoveryOfferReason.TemplateApplication,"none"));
        return new TemplateApplyService(t, new TemplateApplyGrantStore(Path.Combine(_root,"grants")), new TemplateApplyOperationStore(Path.Combine(_root,"ops")), runtime ?? new RecordingRuntime(), Path.Combine(_root,"stage"), marketplace);
    }
    private (Template Template, Mock<ITemplateMarketplaceService> Marketplace) CreateRemoteMarketplaceTemplate()
    {
        var root=Path.Combine(_root,"artifact",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); var executable=Path.Combine(root,"setup.ps1"); File.WriteAllText(executable,"Write-Output reviewed"); var executableHash=Hash(executable); var template=new Template { Id="remote",Name="Remote",Version="1",SourceUrl="https://example.test/templates",MarketplaceManifestDigest=new string('b',64),ArtifactSha256=new string('a',64),MarketplaceArtifactRoot=root,MarketplaceExecutableFiles=[new TemplateExecutableFile("setup.ps1",executableHash)],TrustState=TemplateTrustState.Trusted,Scripts=[new TemplateScript { Name="setup",Type=TemplateScriptType.PowerShell,Content="Write-Output reviewed",TimeoutSeconds=10 }] };
        var manifest=new TemplateManifestV2 { Id=template.Id,Version=template.Version,ArtifactSha256=template.ArtifactSha256,ExecutableFiles=template.MarketplaceExecutableFiles };
        var marketplace=new Mock<ITemplateMarketplaceService>(MockBehavior.Loose);
        marketplace.Setup(x=>x.GetAuthorizedManifestForExecutionAsync(template.SourceUrl,template.Id,template.MarketplaceManifestDigest,template.ArtifactSha256,It.IsAny<CancellationToken>())).ReturnsAsync(manifest);
        marketplace.Setup(x=>x.GetVerifiedArtifactForExecutionAsync(template.SourceUrl,manifest,It.IsAny<CancellationToken>())).ReturnsAsync(new TemplateArtifact(template.ArtifactSha256,root,DateTimeOffset.UtcNow,template.Id,template.Version));
        return (template,marketplace);
    }
    private sealed class RecordingRuntime : ITemplateGrantedExecutionRuntime
    { private readonly ProcessResult _result; public RecordingRuntime(ProcessResult? result=null) => _result=result ?? new ProcessResult(0,"","",TimeSpan.Zero,false,false,false,1); public List<GrantedTemplateScriptPlan> Plans { get; }=[]; public Task<ProcessResult> ExecuteAsync(GrantedTemplateScriptPlan p,CancellationToken ct=default) { Plans.Add(p); return Task.FromResult(_result); } }
    private sealed class FakeTemplateService(Template template, RecoveryOffer offer) : ITemplateService
    {
        public Task<RecoveryOffer> GetRecoveryOfferAsync(string name,CancellationToken ct=default)=>Task.FromResult(offer);
        public Task<List<Template>> LoadTemplatesAsync(bool f=false,CancellationToken ct=default)=>Task.FromResult(new List<Template>{template});
        public Task<Template?> GetTemplateByIdAsync(string id,CancellationToken ct=default)=>Task.FromResult<Template?>(id==template.Id?template:null);
        public Task<List<Template>> SearchTemplatesAsync(string q,CancellationToken ct=default)=>LoadTemplatesAsync(false,ct); public Task RefreshTemplatesAsync(CancellationToken ct=default)=>Task.CompletedTask;
        public Task<TemplateApplicationResult> ApplyTemplateAsync(string a,string b,Dictionary<string,string>? c=null,IProgress<TemplateProgress>? d=null,CancellationToken e=default)=>throw new NotSupportedException();
        public Task<TemplateValidationResult> ValidateTemplateAsync(Template t,string? d=null)=>Task.FromResult(new TemplateValidationResult{IsValid=true}); public Task<bool> IsTemplateCompatibleAsync(string a,string b)=>Task.FromResult(true);
        public Task<bool> AddCustomTemplateAsync(Template t,CancellationToken c=default)=>Task.FromResult(true); public Task<bool> RemoveCustomTemplateAsync(string x,CancellationToken c=default)=>Task.FromResult(true); public Task<bool> ExportTemplateAsync(string x,string y,CancellationToken c=default)=>Task.FromResult(true); public Task<Template?> ImportTemplateAsync(string x,CancellationToken c=default)=>Task.FromResult<Template?>(template); public Task<List<TemplateApplicationRecord>> GetApplicationHistoryAsync(string? x=null)=>Task.FromResult(new List<TemplateApplicationRecord>()); public string GetTemplatesCachePath()=>""; public string GetTemplateScriptsPath()=>"";
    }
    private static string DigestFiles() => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Array.Empty<byte>())).ToLowerInvariant();
    private static string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private sealed class FileContentionIOException : IOException
    {
        public FileContentionIOException() : base("simulated sharing violation") { HResult=unchecked((int)0x80070020); }
    }
    private sealed class FileContentionAccessException : UnauthorizedAccessException
    {
        public FileContentionAccessException() : base("simulated sharing violation") { HResult=unchecked((int)0x80070020); }
    }
    private sealed class PermanentIOException : IOException
    {
        public PermanentIOException() : base("simulated disk full") { HResult=unchecked((int)0x80070070); }
    }
    private sealed class AccessDeniedException : UnauthorizedAccessException
    {
        public AccessDeniedException() : base("simulated access denied") { HResult=unchecked((int)0x80070005); }
    }
    private static async Task WaitForChildAsync(TemplateApplyOperationStore store, string id, Task execution)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while(!timeout.IsCancellationRequested)
        {
            try
            {
                if ((await store.ReadAsync(id)).PendingScript?.ChildProcessId is not null) return;
            }
            catch (IOException)
            {
                // The durable record is atomically replaced with an exclusive file handle; a test
                // observer may briefly race the worker's persistence write.
            }
            var completed=await Task.WhenAny(execution,Task.Delay(50,timeout.Token));
            if(completed==execution)
            {
                // Surface a startup error immediately rather than turning it into an unrelated
                // readiness timeout on slower CI hosts.
                await execution;
                throw new InvalidOperationException("The fixed runtime completed before its child process became observable.");
            }
        }
        throw new TimeoutException("The fixed runtime did not persist child readiness within 30 seconds.");
    }
}
