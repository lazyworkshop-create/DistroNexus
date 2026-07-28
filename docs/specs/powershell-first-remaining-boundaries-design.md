# Remaining PowerShell-First Boundary Technical Design

## Scope and Requirement Traceability

- Requirements: `docs/specs/powershell-first-remaining-boundaries-requirements.md` FR-101 through FR-107.
- Parent constraints: `docs/specs/powershell-first-requirements.md`, `docs/architecture/powershell-first-decision.md`, and `AGENTS.md`.
- Exclusions: live WSL/USB/UAC/package/update mutation, signing, publishing, deployment, and generic execution channels.

| Requirement | Design section | Test or verification |
| --- | --- | --- |
| FR-101 | Bootstrap/settings/update contract | Module-client, bootstrap, and Desktop routing tests. |
| FR-102 | Package/download operations | Closed-route, cancellation, and view-model tests. |
| FR-103 | USB contract | Grant/broker route tests and Desktop structural test; UAT closure. |
| FR-104 | Instance configuration | Read/preview/execute token and routing tests. |
| FR-105 | Install target preview | Path/preflight negative tests and install UI routing tests. |
| FR-106 | Diagnostics | Typed-result/redaction and UI routing tests. |
| FR-107 | Enforcement | Whole-Desktop structural inventory and build. |

## Architecture and Ownership

Every capability follows `WPF -> IPowerShellModuleClient -> exported cmdlet -> fixed versioned Bridge/Core operation -> typed result`. WPF can obtain an absolute user-selected candidate path or a visual confirmation, but Core is authoritative for product state, host validation, mutation, download jobs, and recovery. `IPowerShellModuleClient` contains one method per public capability operation; it never exposes arbitrary command text, module paths, process arguments, or bridge operation names.

The only Desktop exceptions are rendering/navigation/dialog/clipboard, picking a user input, and opening a module-returned display-safe target with the shell. Bootstrap knows only a product-owned module location from immutable application composition; it imports that module before obtaining global settings. It does not read product settings to choose the module.

## Contracts and Behavior

### Bootstrap/settings/update

Module resolution is not a user setting or command input. `PowerShellService` uses a `ProductModuleLocator` selected from immutable product composition in this order: the packaged module directory adjacent to the signed Desktop/Bridge composition, then the repository development module directory only in an explicit development build. It never reads `%AppData%` settings or an environment/module-path override. If neither trusted location contains a valid manifest/module pair, every typed invocation fails before import with `DistroNexus.ModuleBootstrapUnavailable`; the error contains only the product component name and no attempted path.

`GlobalSettings.PowerShellModulePath` is a legacy compatibility field: bootstrap ignores it; `Get-DistroNexusSettings` returns it as null; `Set-DistroNexusSettings -PowerShellModulePath <nonempty>` returns `Settings.ModulePathRetired` without saving; a successful settings save removes the persisted legacy field. Settings UI removes its editable control. This is an intentional security correction, not an arbitrary module-path compatibility promise.

The exported read commands and typed client signatures are fixed as follows:

| Exported cmdlet | Parameters | Typed client method | Result |
| --- | --- | --- | --- |
| `Get-DistroNexusBootstrapSettings` | none | `GetBootstrapSettingsAsync(CancellationToken)` | `BootstrapSettingsResult(Settings, ModuleState)`; successful invocation always returns `ModuleState = Ready` and contains no path. When the locator cannot resolve a trusted module, this method, like every other typed method, fails with `DistroNexus.ModuleBootstrapUnavailable`; it does not fabricate an `Unavailable` result. |
| `Get-DistroNexusStoreComplianceStatus` | none | `GetStoreComplianceStatusAsync(CancellationToken)` | `StoreComplianceStatusResult(bool IsStoreManaged, string OutcomeCode)`. |
| `Get-DistroNexusUpdateStatus` | `-IncludePrerelease` (optional, false by default) | `GetUpdateStatusAsync(bool includePrerelease, CancellationToken)` | `UpdateStatusResult(CurrentVersion, LatestVersion?, IsUpdateAvailable, ReleaseNotes?, ReleaseUri?, ReleasedAt?, IsPreRelease, OutcomeCode)`. |

All three are reads and therefore do not use `ShouldProcess`. Their Bridge payloads are respectively `{}`, `{}`, and `{ IncludePrerelease: bool }`; unknown fields are rejected. `CurrentVersion`/`LatestVersion` are bounded normalized version strings, release notes are capped and sanitized, and a release URI is returned only when it is absolute HTTPS, host `github.com`, no userinfo/fragment, and path begins `/LazyWorkshopCreate/DistroNexus/releases`. Network, malformed API, Store-managed, and no-update outcomes are typed `OutcomeCode` values rather than raw exceptions. `PowerShellModuleClient` maps only these cmdlets and rejects unknown output fields. Desktop may call its existing safe browser-launch adapter only after it receives a non-null release URI in a successful typed result; the module never opens a browser.

### Package/download jobs

Existing cache/source commands are used where their typed records already suffice. New fixed routes are `package.jobs.start.preview.v1 { PackageId }`, `package.jobs.start.execute.v1 { PreviewToken }`, `package.jobs.list.v1`, `package.jobs.cancel.preview.v1`, `package.jobs.cancel.execute.v1`, `package.jobs.retry.preview.v1`, `package.jobs.retry.execute.v1`, and `package.jobs.clear.preview.v1`/`.execute.v1`. Read results contain bounded opaque job ids, public state/progress, and safe package labels. A start preview resolves the current catalog package by its allow-listed identifier and binds package identity/version/download fingerprint to a same-user short-lived grant; execute accepts only that grant and creates or resumes a Core-owned durable job. Other execute routes accept only a same-user preview token. No route accepts URLs, file paths, command text, task delegates, or process handles.

The exported public commands are `Start-DistroNexusPackageDownload -PackageId <string> -Preview`, returning `PackageJobStartPreviewResult { PreviewToken, ExpiresAt, PackageId, PackageLabel, OutcomeCode }`, and `Start-DistroNexusPackageDownload -PreviewToken <string>`, returning `PackageJobStartResult { JobId, OutcomeCode }`. Only the execute parameter set uses `SupportsShouldProcess`; preview never mutates. The typed client exposes matching `PreviewPackageDownloadJobStartAsync(string packageId, CancellationToken)` and `StartPackageDownloadJobAsync(string previewToken, CancellationToken)` methods and accepts only closed, bounded response fields. `PackageId` is trimmed ASCII `[A-Za-z0-9][A-Za-z0-9._-]{0,127}` and must resolve to exactly one current catalog record. Stable failures include `Package.JobPackageNotFound`, `Package.JobPackageMetadataInvalid`, `Package.JobGrantInvalid`, `Package.JobGrantExpired`, `Package.JobGrantReplayed`, `Package.JobStateChanged`, and `Package.JobUnavailable`.

The start fingerprint canonically binds the validated package id/version, catalog revision and source provenance, absolute HTTPS download endpoint, expected SHA-256 and size, and derived cache filename. Missing, malformed, untrusted, or unverifiable source/hash/size metadata fails preview before a grant is issued. Core stores grants and durable jobs using same-user protected records. Execute atomically consumes the grant, revalidates the fingerprint, and performs keyed get-or-create under the canonical fingerprint: simultaneous valid executes return one active opaque job id, with `Created` for the winner and `ExistingActive` for every concurrent or later caller. “Resume” applies only to that active job; terminal cancelled/failed jobs are never restarted by start and require retry preview/execute.

### USB

USB uses the already-approved `usb.status.v1`, `usb.list.v1`, `usb.action.preview.v1`, and `usb.action.execute.v1` records. Desktop replaces its watcher with bounded polling of `GetUsbStatusAsync`/`ListUsbDevicesAsync` while visible. Bind/unbind can only complete when the release/security-owned signed broker contract is available; otherwise typed results return the stable unavailable outcome before elevation.

### Instance configuration

Routes are `instance.config.read.v1 { Name }`, `instance.config.recovery.v1 { Name }`, `instance.config.preview.v1 { Name, Changes }`, and `instance.config.execute.v1 { PreviewToken }`. Unknown fields are rejected. `Name` uses the existing validated distribution-name grammar and `Changes` is the existing allow-listed modeled configuration document delta: no raw `wsl.conf`, section, path, free-form key, or command crosses the boundary. A changed value must pass the same Core validation as the current save path; a no-op preview returns `Instance.ConfigNoChanges` and issues no token.

The exports and typed methods are `Get-DistroNexusInstanceConfiguration -Name`, `Get-DistroNexusInstanceConfigurationRecoveryOffer -Name`, `Save-DistroNexusInstanceConfiguration -Name -Changes <modeled record> -Preview`, and `Save-DistroNexusInstanceConfiguration -PreviewToken`. The read result is `InstanceConfigurationReadResult { Name, SchemaRevision, Document, Fingerprint, OutcomeCode }`; the recovery result is `InstanceConfigurationRecoveryResult { Name, OfferState, RecoveryFingerprint?, OutcomeCode }`; preview is `InstanceConfigurationPreviewResult { PreviewToken, ExpiresAt, Name, ChangeSummary[<=32], OutcomeCode }`; and execute is `InstanceConfigurationSaveResult { Name, BackupCreated, RecoveryAction, OutcomeCode }`. All text fields are bounded, results include no raw host path or raw configuration text beyond the modeled `Document`, and stable failures are `Instance.ConfigNotFound`, `Instance.ConfigInvalidChanges`, `Instance.ConfigGrantInvalid`, `Instance.ConfigGrantExpired`, `Instance.ConfigGrantReplayed`, `Instance.ConfigStateChanged`, and `Instance.ConfigUnavailable`.

Preview persists a DPAPI CurrentUser grant with a five-minute maximum expiry, bound to SID, instance identity, schema revision, canonical modeled changes, current configuration fingerprint, and recovery-offer fingerprint. The store is atomically written and consumption is single-use/replay-safe. Execute accepts only the token, re-reads and revalidates all bindings before any backup/write, and returns a sanitized recovery action. Recovery-offer discovery is read-only; creation of any required recovery/backup artifact remains inside the Core save operation.

### Install target and diagnostics

The install target contract is `install.target.preview.v1 { InstallRoot }`. `InstallRoot` is input-only at the WPF boundary: Core canonicalizes it and rejects empty, device/UNC/root-only, reparse-point, non-writable, inaccessible, or capacity-insufficient targets without returning an unsafe host path. The fixed export/client pair is `Get-DistroNexusInstallTargetPreview -InstallRoot` / `PreviewInstallTargetAsync(string installRoot, CancellationToken)`, returning `InstallTargetPreviewResult { PreviewToken, ExpiresAt, DisplayName, AvailableBytes, RequiredBytes, IsEligible, OutcomeCode }`. `DisplayName` is a bounded display-safe volume/root label; numeric values are nonnegative and capped. Stable outcomes are `Install.TargetEligible`, `Install.TargetInvalid`, `Install.TargetUnavailable`, `Install.TargetInsufficientCapacity`, `Install.TargetGrantInvalid`, `Install.TargetGrantExpired`, `Install.TargetGrantReplayed`, and `Install.TargetStateChanged`.

Core stores the target preview as a DPAPI CurrentUser, five-minute, atomically consumed grant bound to SID, canonical target identity, current drive/capacity fingerprint, and minimum required capacity. `verified.install.preview.v1` changes to accept `TargetPreviewToken` rather than an installation root. After package/name validation it atomically consumes that target token, copies its canonical target identity and capacity fingerprint into the newly issued verified-install grant, and maps an invalid, expired, replayed, or changed target grant to the corresponding `Install.TargetGrantInvalid`, `Install.TargetGrantExpired`, `Install.TargetGrantReplayed`, or `Install.TargetStateChanged` outcome before creating an install grant. `verified.install.execute.v1` continues to accept only its verified-install token and revalidates target/name/package state before mutation. Both WPF installation paths pass only the selected path to target preview and carry the opaque token thereafter. Legacy public `Install-DistroNexusInstance -InstallRoot` parameter sets remain module-only facades: they call target preview internally and never expose a raw-path Bridge execute. WPF removes drive/directory write probes and only displays the returned eligibility. It never creates or deletes a candidate directory. Diagnostics use an existing typed report operation when it can express the UI need; otherwise `diagnostic.snapshot.v1` returns a bounded redacted modeled snapshot. No raw `IPowerShellService` result becomes a Desktop contract.

## Data and Execution Semantics

All previews are read-only until an explicit execute, issue short-lived same-user opaque grants, and bind the current request and security-relevant fingerprint. Execute accepts only the grant, revalidates current state, atomically consumes it, and returns stable sanitized success/failure/recovery codes. Package-job start is idempotent for the same active package fingerprint; terminal failed/cancelled jobs are retried only through the retry preview/execute pair. Download operation state and transient transfer ownership remain Core-owned. On restart, Core marks an interrupted nonterminal transfer `Interrupted`, discards any unverified partial artifact, and requires retry preview/execute; it never reports the job active until transfer state and the expected artifact hash/size are revalidated. Desktop polling stops when the view is unloaded/cancelled and has no durable state.

## Security and Operations

Public commands use `SupportsShouldProcess` for mutation. Unknown payload fields, malformed identifiers, foreign/expired/replayed grants, stale state, unavailable bridge, and cancellation fail before mutation. Core validates security-sensitive fields again. Errors/results redact host paths, credentials, raw configurations, task delegates, command lines, and broker proof material.

USB signing and real-host activity are not inferred from tests. The implementation must keep S25 blocked until its documented signed-broker authorization is available; it may still migrate read-only USB discovery only if the broker-free contract is independently accepted.

## Verification Strategy

- Unit/component: fixed command/method mappings, payload rejection, consent, grants, cancellation, and result parsing per family.
- Structural: a whole-Desktop forbidden-reference and forbidden-host-I/O test with explicit UI-only exceptions; no business-service registrations in `App.xaml.cs`.
- Integration/runtime: targeted xUnit/Pester per slice and Debug/Release builds. Real USB/UAC/WSL/package/update flows are recorded as external UAT.

## Open Items

| Item | Blocking level | Owner | Resolution |
| --- | --- | --- | --- |
| USB signed broker contract | Blocker for bind/unbind | Release/security owner | Supply/authorize publisher pin, packaging, and signing evidence. |
| Real host behavior | Follow-up | Release/UAT owner | Run disposable-host UAT after repository acceptance. |
