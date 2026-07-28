using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Internal fixed execution boundary for reviewed template content.</summary>
public interface ITemplateGrantedExecutionRuntime { Task<ProcessResult> ExecuteAsync(GrantedTemplateScriptPlan plan, CancellationToken cancellationToken = default); }

public sealed class FixedTemplateGrantedExecutionRuntime : ITemplateGrantedExecutionRuntime
{
    private readonly TemplateApplyOperationStore _operations;
    public FixedTemplateGrantedExecutionRuntime(TemplateApplyOperationStore operations) => _operations = operations;
    public async Task<ProcessResult> ExecuteAsync(GrantedTemplateScriptPlan plan, CancellationToken cancellationToken = default)
    {
        if (plan.ScriptType is not (TemplateScriptType.Bash or TemplateScriptType.PowerShell) || !File.Exists(plan.CoreStagedFile) || !string.Equals(HashFile(plan.CoreStagedFile), plan.StagedFileSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Template.ExecutionPlanInvalid");
        var start = new ProcessStartInfo(plan.ScriptType == TemplateScriptType.Bash ? "wsl.exe" : "pwsh.exe") { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
        var arguments = plan.ScriptType == TemplateScriptType.Bash
            ? new[] { "--distribution", plan.InstanceName, "--user", "root", "--", "bash", ToWslPath(plan.CoreStagedFile) }
            : new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", plan.CoreStagedFile };
        foreach(var argument in arguments) start.ArgumentList.Add(argument);
        var stopwatch=Stopwatch.StartNew();
        var child=await _operations.StartClaimedChildAsync(plan, () => { var p=new Process { StartInfo=start }; if(!p.Start()) throw new InvalidOperationException("Template.Failed"); return p; }, cancellationToken).ConfigureAwait(false);
        if(child is null) return new ProcessResult(null,"","",stopwatch.Elapsed,false,true,false,null);
        using(child)
        {
            var output=child.StandardOutput.ReadToEndAsync(); var error=child.StandardError.ReadToEndAsync(); using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(plan.TimeoutSeconds)); using var linked=CancellationTokenSource.CreateLinkedTokenSource(deadline.Token,cancellationToken);
            var timedOut=false; var cancelled=false;
            try
            {
                var wait = child.WaitForExitAsync(linked.Token);
                while (!wait.IsCompleted)
                {
                    if ((await _operations.ReadAsync(plan.OperationId, CancellationToken.None).ConfigureAwait(false)).CancelRequested)
                    {
                        cancelled=true; try { child.Kill(true); } catch { } await child.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); break;
                    }
                    await Task.Delay(25, linked.Token).ConfigureAwait(false);
                }
                if (!cancelled) await wait.ConfigureAwait(false);
            }
            catch(OperationCanceledException) { timedOut=deadline.IsCancellationRequested&&!cancellationToken.IsCancellationRequested; cancelled=cancellationToken.IsCancellationRequested; try { child.Kill(true); } catch { } await child.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); }
            stopwatch.Stop(); return new ProcessResult(child.ExitCode,Bound(await output.ConfigureAwait(false)),Bound(await error.ConfigureAwait(false)),stopwatch.Elapsed,timedOut,cancelled,false,child.Id);
        }
    }
    private static string Bound(string value) => value.Length <= 16*1024 ? value : value[..(16*1024)];
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string ToWslPath(string path)
    { var full=Path.GetFullPath(path); if (full.Length < 3 || full[1] != ':') throw new InvalidOperationException("Template.ExecutionPlanInvalid"); return "/mnt/" + char.ToLowerInvariant(full[0]) + "/" + full[3..].Replace('\\','/'); }
}

/// <summary>Owns the reviewed preview, durable operation and worker-side script execution flow.</summary>
public sealed class TemplateApplyService
{
    private readonly ITemplateService _templates;
    private readonly TemplateApplyGrantStore _grants;
    private readonly TemplateApplyOperationStore _operations;
    private readonly ITemplateGrantedExecutionRuntime _runtime;
    private readonly ITemplateMarketplaceService? _marketplace;
    private readonly string _stagingRoot;
    public TemplateApplyService(ITemplateService templates, TemplateApplyGrantStore grants, TemplateApplyOperationStore operations, ITemplateGrantedExecutionRuntime runtime, string stagingRoot, ITemplateMarketplaceService? marketplace = null)
    { _templates=templates; _grants=grants; _operations=operations; _runtime=runtime; _stagingRoot=stagingRoot; _marketplace=marketplace; Directory.CreateDirectory(stagingRoot); }

    public async Task<TemplateApplyPreviewResult> PreviewAsync(string instanceName, string templateId, IReadOnlyDictionary<string,string>? variables, bool declineRecoveryOffer, CancellationToken ct = default)
    {
        ValidateName(instanceName); ValidateName(templateId); ValidateVariables(variables);
        var template = await _templates.GetTemplateByIdAsync(templateId, ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Template.InvalidRequest");
        if (template.IsRemoteV2 || template.TrustState == TemplateTrustState.Untrusted) return new(null, new(false, instanceName, RecoveryOfferReason.TemplateApplication, "RecoveryOffer.Unavailable"), false, true, [], ["Template.TrustRequired"], null);
        var validation = await _templates.ValidateTemplateAsync(template, instanceName).ConfigureAwait(false);
        if (!validation.IsValid || template.Scripts.Any(x => x.Type is not (TemplateScriptType.Bash or TemplateScriptType.PowerShell))) throw new InvalidOperationException("Template.InvalidRequest");
        var recovery = await _templates.GetRecoveryOfferAsync(instanceName, ct).ConfigureAwait(false);
        if (recovery.IsAvailable && !declineRecoveryOffer) return new(null, recovery, true, false, [], ["Template.RecoveryDeclineRequired"], null);
        var normalized = NormalizeVariables(variables);
        var record = new TemplateApplyGrantRecord(1, "", instanceName, template.Id, template.Version, template.SourceUrl, template.MarketplaceManifestDigest, template.ArtifactSha256, Digest(template.MarketplaceArtifactRoot), DigestExecutableFiles(template.MarketplaceExecutableFiles), normalized, Digest(normalized), Digest(string.Join(',', template.Capabilities.Order())), RecoveryFingerprint(recovery), recovery.IsAvailable, recovery.InstanceName, recovery.Reason.ToString(), recovery.MessageKey, declineRecoveryOffer, default);
        var token = await _grants.IssueAsync(record, ct).ConfigureAwait(false);
        return new(token, recovery, false, false, template.Scripts.OrderBy(x=>x.Order).Select(x=>x.Name).ToArray(), validation.Warnings.Take(32).ToArray(), DateTimeOffset.UtcNow.AddMinutes(5));
    }

    public async Task<TemplateApplyExecuteResult> ExecuteAsync(string previewToken, CancellationToken ct = default)
    {
        var grant = await _grants.ConsumeAsync(previewToken, ct).ConfigureAwait(false);
        var operationId = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(); var now=DateTimeOffset.UtcNow;
        var template=await _templates.GetTemplateByIdAsync(grant.TemplateId,ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Template.ProvenanceChanged");
        if (template.Version != grant.TemplateVersion || !SameProvenance(template, grant)) throw new InvalidOperationException("Template.ProvenanceChanged");
        await RevalidateMarketplaceAsync(template, grant, ct).ConfigureAwait(false);
        var record = new TemplateApplyOperationRecord(1, operationId, TemplateApplyGrantStore.CurrentSid(), grant.InstanceName, grant.TemplateId, grant.TemplateVersion, grant.SourceUrl, grant.ManifestDigest, grant.ArtifactSha256, grant.ExecutableFilesDigest, grant.VariablesDigest, grant.RecoveryDeclined, TemplateOperationState.Queued, now, now.AddMinutes(1), now, 0, template.Scripts.Count, null, "Queued", null, [], false, NormalizedVariables:grant.NormalizedVariables, ArtifactRootDigest:grant.ArtifactRootDigest, CapabilitiesDigest:grant.CapabilitiesDigest, RecoveryFingerprint:grant.RecoveryFingerprint);
        await _operations.CreateAsync(record, ct).ConfigureAwait(false); return new(operationId);
    }
    public async Task<TemplateApplyOperationStatus> StatusAsync(string operationId, CancellationToken ct = default)
    { var r=await _operations.RecoverAsync(operationId,ct).ConfigureAwait(false); return new(r.OperationId,r.State,r.CompletedScripts,r.TotalScripts,r.CurrentScript,r.Message,r.ErrorCode,r.ExecutedScripts); }
    public async Task<TemplateApplyCancelResult> CancelAsync(string operationId, CancellationToken ct = default)
    {
        var recovered = await _operations.RecoverAsync(operationId, ct).ConfigureAwait(false);
        if (TemplateApplyOperationStore.Terminal(recovered.State))
            return new(operationId, false, recovered.State);
        var accepted=await _operations.RequestCancelAsync(operationId,ct).ConfigureAwait(false);
        var r=await _operations.ReadAsync(operationId,ct).ConfigureAwait(false);
        return new(operationId,accepted,r.State);
    }

    /// <summary>Called only by the fixed template worker/bridge composition.</summary>
    public async Task RunOperationAsync(string operationId, CancellationToken ct = default)
    {
        using var worker = _operations.TryAcquireWorkerLock(operationId); if (worker is null) return;
        var record=await _operations.ReadAsync(operationId,ct).ConfigureAwait(false); if (TemplateApplyOperationStore.Terminal(record.State)) return;
        record=await _operations.UpdateAsync(operationId,x=>x with { State=TemplateOperationState.Running, Message="Running" },ct).ConfigureAwait(false);
        try
        {
            var template=await _templates.GetTemplateByIdAsync(record.TemplateId,ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Template.ProvenanceChanged");
            if (template.Version != record.TemplateVersion || !SameProvenance(template, record)) throw new InvalidOperationException("Template.ProvenanceChanged");
            await RevalidateMarketplaceAsync(template, record, ct).ConfigureAwait(false);
            var scripts=template.Scripts.OrderBy(x=>x.Order).ToArray();
            for(var index=0;index<scripts.Length;index++)
            {
                record=await _operations.ReadAsync(operationId,ct).ConfigureAwait(false);
                if(record.CancelRequested) { await FinishAsync(record,TemplateOperationState.Cancelled,"Template.Cancelled",ct).ConfigureAwait(false); return; }
                if (Digest(record.NormalizedVariables) != record.VariablesDigest || Digest(string.Join(',', template.Capabilities.Order())) != record.CapabilitiesDigest || RecoveryFingerprint(await _templates.GetRecoveryOfferAsync(record.InstanceName, ct).ConfigureAwait(false)) != record.RecoveryFingerprint) throw new InvalidOperationException("Template.ProvenanceChanged");
                await RevalidateMarketplaceAsync(template, record, ct).ConfigureAwait(false);
                var script=scripts[index]; var file=await StageAsync(operationId,index,script,ct).ConfigureAwait(false); var hash=HashFile(file);
                var pending=new TemplatePendingScriptRecord(index,script.Type,hash,TemplatePendingScriptState.Prepared,Guid.NewGuid().ToString("N"),DateTimeOffset.UtcNow,null,null,null);
                record=await _operations.UpdateAsync(operationId,x=>x with { PendingScript=pending, CurrentScript=script.Name },ct).ConfigureAwait(false);
                if(record.CancelRequested) { await FinishAsync(record,TemplateOperationState.Cancelled,"Template.Cancelled",ct).ConfigureAwait(false); return; }
                pending=pending with { State=TemplatePendingScriptState.Claimed, ClaimedAt=DateTimeOffset.UtcNow };
                record=await _operations.UpdateAsync(operationId,x=>x with { PendingScript=pending },ct).ConfigureAwait(false);
                var result=await _runtime.ExecuteAsync(new(operationId,record.InstanceName,index,script.Type,Math.Clamp(script.TimeoutSeconds,1,3600),file,hash),ct).ConfigureAwait(false);
                if(result.Cancelled) { await FinishAsync(record,TemplateOperationState.Cancelled,"Template.Cancelled",ct).ConfigureAwait(false); return; }
                if(result.TimedOut || result.Failure != ProcessFailureKind.None || result.ExitCode != 0) { if(!script.ContinueOnError) { await FinishAsync(record,TemplateOperationState.Failed,"Template.Failed",ct).ConfigureAwait(false); return; } }
                record=await _operations.UpdateAsync(operationId,x=>x with { CompletedScripts=index+1, CurrentScript=script.Name, PendingScript=null, Message="Running", ExecutedScripts=x.ExecutedScripts.Append(script.Name).Take(500).ToArray() },ct).ConfigureAwait(false);
            }
            record=await _operations.ReadAsync(operationId,ct).ConfigureAwait(false);
            if(record.CancelRequested || !await _operations.TryFinishSucceededAsync(operationId,ct).ConfigureAwait(false))
            {
                record=await _operations.ReadAsync(operationId,ct).ConfigureAwait(false);
                if (!TemplateApplyOperationStore.Terminal(record.State)) await FinishAsync(record,TemplateOperationState.Cancelled,"Template.Cancelled",ct).ConfigureAwait(false);
                return;
            }
            try { await CompleteSuccessfulMarketplaceExecutionAsync(record, ct).ConfigureAwait(false); }
            catch { await _operations.UpdateAsync(operationId, x => x with { MarketplacePromotionErrorCode="Template.MarketplacePromotionFailed" }, CancellationToken.None).ConfigureAwait(false); }
        }
        catch(OperationCanceledException) { await FinishAsync(await _operations.ReadAsync(operationId,CancellationToken.None).ConfigureAwait(false),TemplateOperationState.Cancelled,"Template.Cancelled",CancellationToken.None).ConfigureAwait(false); }
        catch(InvalidOperationException ex) { await FinishAsync(await _operations.ReadAsync(operationId,CancellationToken.None).ConfigureAwait(false),TemplateOperationState.Failed,ex.Message.StartsWith("Template.",StringComparison.Ordinal)?ex.Message:"Template.Failed",CancellationToken.None).ConfigureAwait(false); }
        catch { await FinishAsync(await _operations.ReadAsync(operationId,CancellationToken.None).ConfigureAwait(false),TemplateOperationState.Failed,"Template.Failed",CancellationToken.None).ConfigureAwait(false); }
    }
    private async Task FinishAsync(TemplateApplyOperationRecord r, TemplateOperationState state,string message,CancellationToken ct) => await _operations.WriteAsync(r with { State=state,Message=message,ErrorCode=state==TemplateOperationState.Succeeded?null:message,CurrentScript=null,PendingScript=null,UpdatedAt=DateTimeOffset.UtcNow },ct).ConfigureAwait(false);
    private async Task<string> StageAsync(string operation,int ordinal,TemplateScript script,CancellationToken ct)
    { var root=Path.Combine(_stagingRoot,operation); Directory.CreateDirectory(root); var extension=script.Type==TemplateScriptType.Bash?".sh":".ps1"; var file=Path.Combine(root,ordinal.ToString("D4")+extension); var text=script.Content; if(string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Template.ExecutionPlanInvalid"); var record=await _operations.ReadAsync(operation,ct).ConfigureAwait(false); foreach(var pair in ParseVariables(record.NormalizedVariables)) text=text.Replace("${"+pair.Key+"}",pair.Value,StringComparison.Ordinal); var tmp=file+".tmp"; await File.WriteAllTextAsync(tmp,text,new UTF8Encoding(false),ct).ConfigureAwait(false); File.Move(tmp,file,true); return file; }
    private static void ValidateName(string x) { if(string.IsNullOrWhiteSpace(x)||x.Length>256||x.IndexOfAny(['\0','\r','\n'])>=0) throw new InvalidOperationException("Template.InvalidRequest"); }
    private static void ValidateVariables(IReadOnlyDictionary<string,string>? v) { if(v is null)return; if(v.Count>64||v.Any(x=>x.Key.Length>128||x.Value.Length>4096||x.Key.IndexOfAny(['\0','\r','\n'])>=0||x.Value.IndexOfAny(['\0','\r','\n'])>=0)) throw new InvalidOperationException("Template.InvalidRequest"); }
    private static string NormalizeVariables(IReadOnlyDictionary<string,string>? values) => values is null ? "" : string.Join("\n",values.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key+"="+x.Value));
    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    private static string DigestExecutableFiles(IReadOnlyList<TemplateExecutableFile> values) => Digest(string.Join("\n", values.OrderBy(x=>x.Path,StringComparer.Ordinal).Select(x=>x.Path+":"+x.Sha256)));
    private static bool SameProvenance(Template t, TemplateApplyGrantRecord g) => t.SourceUrl==g.SourceUrl && t.MarketplaceManifestDigest==g.ManifestDigest && t.ArtifactSha256==g.ArtifactSha256 && Digest(t.MarketplaceArtifactRoot)==g.ArtifactRootDigest && DigestExecutableFiles(t.MarketplaceExecutableFiles)==g.ExecutableFilesDigest;
    private static bool SameProvenance(Template t, TemplateApplyOperationRecord r) => t.SourceUrl==r.SourceUrl && t.MarketplaceManifestDigest==r.ManifestDigest && t.ArtifactSha256==r.ArtifactSha256 && Digest(t.MarketplaceArtifactRoot)==r.ArtifactRootDigest && DigestExecutableFiles(t.MarketplaceExecutableFiles)==r.ExecutableFilesDigest;
    private async Task RevalidateMarketplaceAsync(Template template, TemplateApplyGrantRecord grant, CancellationToken ct)
    { if(string.IsNullOrWhiteSpace(grant.SourceUrl)) return; if(_marketplace is null) throw new InvalidOperationException("Template.ProvenanceChanged"); var verified=await GetVerifiedMarketplaceMaterialAsync(grant.SourceUrl,grant.TemplateId,grant.ManifestDigest,grant.ArtifactSha256,grant.ArtifactRootDigest,grant.ExecutableFilesDigest,ct).ConfigureAwait(false); if(!string.Equals(Path.GetFullPath(template.MarketplaceArtifactRoot),Path.GetFullPath(verified.Artifact.RootPath),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Template.ProvenanceChanged"); }
    private async Task RevalidateMarketplaceAsync(Template template, TemplateApplyOperationRecord record, CancellationToken ct)
    { if(string.IsNullOrWhiteSpace(record.SourceUrl)) return; if(_marketplace is null) throw new InvalidOperationException("Template.ProvenanceChanged"); var verified=await GetVerifiedMarketplaceMaterialAsync(record.SourceUrl,record.TemplateId,record.ManifestDigest,record.ArtifactSha256,record.ArtifactRootDigest,record.ExecutableFilesDigest,ct).ConfigureAwait(false); if(!string.Equals(Path.GetFullPath(template.MarketplaceArtifactRoot),Path.GetFullPath(verified.Artifact.RootPath),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Template.ProvenanceChanged"); }
    private async Task<(TemplateManifestV2 Manifest, TemplateArtifact Artifact)> GetVerifiedMarketplaceMaterialAsync(string sourceUrl, string templateId, string manifestDigest, string artifactSha256, string artifactRootDigest, string executableFilesDigest, CancellationToken ct)
    {
        var manifest=await _marketplace!.GetAuthorizedManifestForExecutionAsync(sourceUrl,templateId,manifestDigest,artifactSha256,ct).ConfigureAwait(false);
        if(manifest is null || DigestExecutableFiles(manifest.ExecutableFiles.ToArray())!=executableFilesDigest) throw new InvalidOperationException("Template.ProvenanceChanged");
        var artifact=await _marketplace.GetVerifiedArtifactForExecutionAsync(sourceUrl,manifest,ct).ConfigureAwait(false);
        if(!string.Equals(artifact.Sha256,artifactSha256,StringComparison.OrdinalIgnoreCase) || Digest(artifact.RootPath)!=artifactRootDigest) throw new InvalidOperationException("Template.ProvenanceChanged");
        VerifyExecutableFiles(artifact.RootPath,manifest.ExecutableFiles);
        return (manifest,artifact);
    }
    private async Task CompleteSuccessfulMarketplaceExecutionAsync(TemplateApplyOperationRecord record, CancellationToken ct)
    { if(string.IsNullOrWhiteSpace(record.SourceUrl)) return; if(_marketplace is null) throw new InvalidOperationException("Template.ProvenanceChanged"); var verified=await GetVerifiedMarketplaceMaterialAsync(record.SourceUrl,record.TemplateId,record.ManifestDigest,record.ArtifactSha256,record.ArtifactRootDigest,record.ExecutableFilesDigest,ct).ConfigureAwait(false); await _marketplace.CompleteSuccessfulExecutionAsync(record.SourceUrl,verified.Manifest,ct).ConfigureAwait(false); }
    private static void VerifyExecutableFiles(string artifactRoot, IReadOnlyList<TemplateExecutableFile> files)
    {
        var root=Path.GetFullPath(artifactRoot).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        foreach(var file in files) { var path=Path.GetFullPath(Path.Combine(artifactRoot,file.Path.Replace('/',Path.DirectorySeparatorChar))); if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||!File.Exists(path)||!string.Equals(HashFile(path),file.Sha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Template.ProvenanceChanged"); }
    }
    private static string RecoveryFingerprint(RecoveryOffer offer) => Digest(string.Join("|",offer.IsAvailable,offer.InstanceName,offer.Reason,offer.MessageKey));
    private static IReadOnlyDictionary<string,string> ParseVariables(string normalized) => string.IsNullOrEmpty(normalized) ? new Dictionary<string,string>() : normalized.Split('\n').Select(x=>x.Split('=',2)).Where(x=>x.Length==2).ToDictionary(x=>x[0],x=>x[1],StringComparer.Ordinal);
}
