# Technical Design: Template Module-Client Migration

## Scope and Requirement Traceability

- Roots: `src/Client`, `src/PowerShell`, `tests/PowerShell`, `docs`.
- Constraints: Core is sole owner of template provenance, network, artifact, state, filesystem and execution; Bridge routes are closed/versioned; WPF is presentation-only.
- Requirements: `docs/specs/template-module-requirements.md` FR-001 through FR-004.
- Binding contract/decision: `docs/contracts/template-module-v1-contract.md`; `docs/architecture/template-apply-recovery-decision.md`.
- Exclusions: live WSL mutation/recovery UAT, arbitrary scripts/paths and publication.

| Requirement | Design section | Test or verification |
| --- | --- | --- |
| FR-001 | Client and presentation migration | WPF structural/routing tests |
| FR-002 | Closed apply contract and worker | grant/provenance/replay tests |
| FR-003 | Recovery and operation state machine | worker/cancel/interruption tests |
| FR-004 | Marketplace/local catalog contract | exact identity/review/promotion tests |

## Contracts and Behavior

The exact cmdlet, fixed-route, request/response record, mutation, limit and Core-method mapping is normative in `template-module-v1-contract.md`; implementation may not add generic transport operations. Add matching typed methods to `IPowerShellModuleClient`. `template.catalog.options.v1` provides only the bounded safe option schema and effective defaults needed by the existing `TemplateOptionsStep`; it must not reconstruct a Core `Template` or expose scripts, paths, packages, preflight commands or provenance. `TemplatesViewModel`, `InstallWizardWorkflowViewModel`, `SelectTemplateStep`, `SelectDistributionStep`, `TemplateOptionsStep`, and `TemplateApplyStep` use only those methods and typed display records; their constructors no longer accept `ITemplateService` or `ITemplateMarketplaceService`.

Remove `Apply-DistroNexusTemplate` from `DistroNexus.psd1` and delete its direct script execution implementation. It has no safe compatible argument shape because it accepts mutable templates and script material. The replacement is the explicit preview/execute cmdlet pair in the contract. Existing `Get-DistroNexusTemplate` becomes the catalog-list/get wrapper; all marketplace commands become named v1 wrappers with `ShouldProcess` for mutation.

## Architecture and Ownership

Add `TemplateApplyGrantStore`, `TemplateApplyOperationStore`, and `TemplateMarketplaceReviewGrantStore`, using the exact protected record schemas, path roots, token properties, lock/recovery behavior and terminal model in the contract. Register stores and `ITemplateService`/`ITemplateMarketplaceService` in a shared `TemplateBridgeComposition` used by Bridge host and worker; Desktop only starts the packaged Bridge host and never composes or calls those services. The stores use CurrentUser DPAPI plus current SID, SHA-256 token filenames, atomic rename consumption, atomic write-replace state updates, and an operation-local cross-process lock. Refactor `TemplateMarketplaceService` so its current in-memory review-token dictionary is replaced by `TemplateMarketplaceReviewGrantStore`; `CreateReviewGrantAsync` persists exact reviewed material and `ApproveCandidateAsync` atomically consumes/revalidates it across fresh Bridge processes.

Refactor the existing in-process `TemplateService.ApplyTemplateAsync` loop into an internal Core entry point `ExecuteGrantedApplyAsync(TemplateApplyOperationRecord operation, TemplateApplyGrantRecord grant, CancellationToken cancellationToken)`. It receives no caller supplied template, path, executable or progress callback. It resolves material exclusively from grant-bound values, revalidates before start and before every script, writes bounded progress through `TemplateApplyOperationStore`, and calls `CompleteSuccessfulExecutionAsync` only after the operation reaches `Succeeded`. The legacy public service method is removed or becomes an internal test adapter that cannot be reached by Desktop/module callers.

Replace the template path's `IPowerShellService` dependency with internal `ITemplateGrantedExecutionRuntime`; Bridge must not inject `BridgeReadOnlyPowerShellService` into the worker's `TemplateService`. `TemplateService` creates a `GrantedTemplateScriptPlan` only after grant/provenance validation: it resolves the permitted template material, applies only normalized grant-bound variables, verifies marketplace executable hashes, atomically stages UTF-8 no-BOM content under the Core-owned operation staging directory, hashes it, and writes the contract-defined `PendingScript` record in `Prepared` state. Before it launches anything, `FixedTemplateGrantedExecutionRuntime` takes that operation's state lock and atomically verifies matching operation ID/SID/nonterminal `Running` state, bound instance, pending ordinal/type/hash, operation-root containment and actual file hash; it transitions `PendingScript` to `Claimed` with a new attempt ID. Immediately before process creation it retakes and holds the lock through process start and PID write: if cancellation was requested, it clears the claimed entry and atomically writes terminal `Cancelled` without a child; otherwise it starts the child and persists PID/start time before releasing the lock. It clears the pending entry only after normal completion and returns `Template.ExecutionPlanInvalid` without a process on any mismatch. A dead worker with a `Claimed` entry makes the operation `Interrupted` rather than retrying, including the process-start/PID-write race. Only then may it launch either the fixed `wsl.exe --distribution <bound-instance> --user root -- bash <derived-mnt-stage-file>` form or fixed `pwsh.exe -NoLogo -NoProfile -NonInteractive -File <stage-file>` form. It supplies fixed process options, bounded output and timeout/cancellation tree termination. No Bridge request, public cmdlet, client field, template metadata, or runtime variable can select a host executable, argument, command text, stage root, working directory or environment override.

Create `DistroNexus.TemplateWorker`, packaged next to the fixed `DistroNexus.WorkspaceBridge.exe`. The Bridge verifies worker assembly name `DistroNexus.TemplateWorker` and its version equals Bridge version before it launches the fixed sibling executable with only `--operation-id <opaque-id>`. The worker verifies the fixed Bridge identity, resolves `TemplateBridgeComposition`, exclusively holds its operation `.worker.lock`, atomically claims the queued operation, and invokes `ExecuteGrantedApplyAsync`. It catches cancellation/failure, writes the exact terminal state, and does not launch another process. Bridge records `WorkerPid`/`WorkerStartedAt` only after successful process start; PID is diagnostic only. `WorkerLaunchDeadlineAt` and authoritative exclusive-lock probing distinguish a queued launch failure from a running worker interruption exactly as defined in the contract; worker start, status and cancel run that recovery sweep.

## Data and Execution Semantics

`New-DistroNexusTemplateApplyPreview` first normalizes the bounded variable map, obtains the current recovery offer, and resolves the exact template and marketplace material. If the offer is available and `DeclineRecoveryOffer` is false, it returns the current Core-shaped offer with `RequiresRecoveryDecline=true` and no token. WPF either ends the flow (current pause behavior) or, after explicit decline confirmation, requests a fresh preview with `DeclineRecoveryOffer=true`; only then does Core issue the DPAPI grant. Decline emits a warning and is retained in grant and operation history. This slice neither creates, selects nor applies a recovery point.

Execute consumes the token exactly once, creates `Queued`, starts the fixed worker and returns its operation ID. Status is durable and redacted. Cancel is same-SID and only requests cooperative cancellation of its own nonterminal operation; it cannot cancel an unrelated operation or authorize new execution. Exact progress fields and state transitions are in the contract. On cancellation/failure, Core keeps candidate state unpromoted and reports partial mutation truthfully; no outcome claims rollback.

## Marketplace, Local Templates, and Compatibility

Bridge calls only the exact `ITemplateMarketplaceService` operations enumerated by the contract. Exact `(SourceId, TemplateId, ManifestDigest)` identity is required for status, review and artifact download; review is a `ShouldProcess` mutation that downloads/verifies the exact candidate then creates and returns its one-shot Core review grant, and approve consumes only that grant. Source lifecycle retains HTTPS/explicit non-HTTPS rules. Import/export/remove use bounded content and preview/execute grants; the UI's file picker handles user filesystem I/O, then supplies/receives content through the client rather than a Core path.

`Test-DistroNexusTemplateCompatibility` maps Core `IsTemplateCompatibleAsync` to `Compatible` or `Incompatible` plus bounded warnings. Wizard filtering may use it for display, but preview always repeats final Core validation, preventing a stale compatibility result from executing.

## Verification Strategy

- `TemplateApplyGrantStoreTests`: SID, expiry, replay, tamper, normalized variable/provenance/recovery fingerprint rejection.
- `TemplateMarketplaceReviewGrantStoreTests`: cross-process review-to-approve, SID, expiry, replay and tamper rejection; exact source/manifest/artifact/script-diff binding.
- `TemplateApplyOperationStoreTests`: atomic claim, launch failure, stale queued/running interruption, pending-script prepared/claimed transitions, claimed-worker-death interruption/no retry, status bounds, same-SID cancel and cancel/status interleaving including cancel-after-claim/before-child-start.
- `TemplateWorkerTests`: fixed identity/version, opaque-ID-only command line, interruption and cancellation before/during every script; candidate never promoted unless `Succeeded`.
- `TemplateGrantedExecutionRuntimeTests`: reject forged plans and operation-record SID/state/instance/pending-ordinal/type/hash mismatch, non-operation staging paths, changed hashes and unsupported types; assert only the two fixed process forms, grant-bound variable staging, marketplace hash verification, bounded output, timeout and child-tree cancellation.
- `WorkspaceBridgeProtocolTests` successors: every named template route, exact request/result parsing, unknown-member rejection, record/response limits and arbitrary-route rejection.
- `PowerShellModuleClientTests` and WPF tests: typed routing, preview-dialog cancellation invokes nothing, explicit recovery decline is bound, no Desktop template-service dependency, and compatibility disposition.
- Pester: listed cmdlets, manifest excludes unsafe legacy apply, `WhatIf`/`Confirm`, identity-required marketplace mutations and preview-token-only execute.
- Run targeted xUnit, PowerShell Unit tests and Debug build. Disposable-WSL recovery/cancellation/integrity remains an external UAT gate.

## Open Items

| Item | Blocking level | Owner | Resolution |
| --- | --- | --- |
| Live WSL recovery/cancel/integrity matrix | Follow-up UAT | Release/UAT owner | Execute after repository acceptance. |
