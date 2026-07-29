# Technical Design: Workspace Module-Client Migration

## Scope and Requirement Traceability

- Project source/test/config/release roots: `src/Client`, `src/PowerShell`, `tests/PowerShell`, `docs`, `.github/workflows` and `tools`.
- Current architecture constraints: Core owns workspace validation, state, grants and execution; WorkspaceBridge exposes only named operations; WPF is presentation-only; public PowerShell commands are the supported product entry point.
- Existing compatibility and migration boundary: preserve exported workspace command names and current modeled workspace records; replace only internal parameter/payload plumbing necessary to remove module-side document I/O and WPF service calls.
- Requirements: `docs/specs/workspace-module-requirements.md` FR-001 through FR-004.
- Decisions/constraints: `AGENTS.md`, `docs/specs/powershell-first-design.md` and the module-first architectural principle.
- Exclusions: shortcut writing, picker UX, template/marketplace behavior, live WSL execution and release publishing.

| Requirement | Design section | Test or verification |
| --- | --- | --- |
| FR-001 | Architecture and ownership; typed client | WPF dependency and routing tests |
| FR-002 | Contracts and behavior; execution semantics | Bridge/client/module contract and grant tests |
| FR-003 | Data ownership | Import/export negative and structural tests |
| FR-004 | Security and operations | WhatIf/Confirm/token/revision/cancellation/progress tests |

## Architecture and Ownership

`WorkspacesViewModel` depends on `IPowerShellModuleClient` and retains only visual dialogs, in-memory editor state and UI cancellation. It uses `WorkspaceDefinition` and related records as passive transport data but has no `IWorkspaceService`, `WorkspaceValidation` or `JsonSerializer` dependency. The typed client invokes fixed exported cmdlets. The module serializes only typed parameters and enforces `ShouldProcess`; the Bridge validates a versioned, closed request shape; `WorkspaceService` remains the sole owner of definition parsing, validation, persistence, trust, revisions, import tokens, launch/retry/close grants and runtime handlers.

Launch and retry have a distinct packaged `DistroNexus.WorkspaceWorker` host. The Bridge resolves it from the same installed package root as WorkspaceBridge (development resolution is only the fixed repository build path), verifies the expected assembly identity/version and starts it detached with the single opaque `OperationId`; it never accepts a worker path, command or argument from a module or WPF caller. The worker opens the same-user DPAPI-protected operation record, obtains an atomic per-operation lock, constructs the approved Core workspace composition and runs the bound action(s). It survives the short-lived module/Bridge process. Status and cancellation use subsequent short-lived Bridge calls that access only the durable operation store, not the original Bridge instance. Worker startup failure makes the operation terminally failed; worker restart recovery marks an orphaned running operation failed with `Workspace.WorkerInterrupted` unless a verified live worker lock proves it is still active.

The migration introduces workspace client request/result records in Core. The records reuse `WorkspaceDefinition`, `WorkspaceDryRunResult`, `WorkspaceImportPreview`, `WorkspaceLaunchPreview`, `WorkspaceActionResult` and `WorkspaceLaunchResult`; they never contain storage paths, executable file names, process argument arrays outside already validated actions, or Core service handles. Progress is a bounded sequence of `WorkspaceActionResult` values returned by the module transport; it does not grant execution authority.

## Contracts and Behavior

- Contracts: add typed client methods for the commands in the closed-route table below. `WorkspaceDefinition` is permitted only in `workspace.save.preview.v1`; all execute payloads contain only `PreviewToken`. `WorkspaceEditorDraft` is a Desktop-only visual state type and is converted without validation or serialization into the preview transport record. The raw definition editor becomes a module-returned export-content viewer and a module preview-content importer; it no longer parses JSON locally.

| Bridge route | Cmdlet | Required payload | Response | Execute binding |
| --- | --- | --- | --- | --- |
| `workspace.list.v1` | `Get-DistroNexusWorkspace` | none | definitions | read-only |
| `workspace.save.preview.v1` | `Get-DistroNexusWorkspaceSavePreview` | `Definition`, `ExpectedRevision` | `WorkspaceOperationPreview` | n/a |
| `workspace.save.execute.v1` | `Save-DistroNexusWorkspace` | `PreviewToken` | definition | token binds definition digest, entity and document revision |
| `workspace.duplicate.preview.v1` / `execute.v1` | `Get-DistroNexusWorkspaceDuplicatePreview` / `Copy-DistroNexusWorkspace` | preview: `Id`, `Name`, `ExpectedRevision`; execute: `PreviewToken` | preview / definition | token binds source ID, normalized name and revision |
| `workspace.remove.preview.v1` / `execute.v1` | `Get-DistroNexusWorkspaceRemovePreview` / `Remove-DistroNexusWorkspace` | preview: `Id`, `ExpectedRevision`; execute: `PreviewToken` | preview / empty | token binds ID and revision |
| `workspace.import.preview.v1` / `execute.v1` | `Get-DistroNexusWorkspaceImportPreview` / `Import-DistroNexusWorkspace` | preview: `Content`; execute: `PreviewToken` | preview / definition | token binds content digest and document revision |
| `workspace.export.preview.v1` / `execute.v1` | `Get-DistroNexusWorkspaceExportPreview` / `Export-DistroNexusWorkspace` | preview: `Id`, `ExpectedRevision`; execute: `PreviewToken` | preview / `WorkspaceExportResult(Content)` | token binds ID, revision and export-definition digest |
| `workspace.trust.preview.v1` / `execute.v1` | `Get-DistroNexusWorkspaceTrustPreview` / `Approve-DistroNexusWorkspaceTrust` | preview: `Id`, `ExpectedRevision`; execute: `PreviewToken` | preview / definition | token binds ID and revision |
| `workspace.launch.preview.v1` / `execute.v1` | `Get-DistroNexusWorkspaceLaunchPreview` / `Invoke-DistroNexusWorkspace` | preview: `Id`; execute: `PreviewToken` | preview / `WorkspaceOperationStarted(OperationId)` | token binds ID, revision, action definition digest and trust state |
| `workspace.retry.preview.v1` / `execute.v1` | `Get-DistroNexusWorkspaceRetryPreview` / `Retry-DistroNexusWorkspaceAction` | preview: `Id`, `ActionId`; execute: `PreviewToken` | preview / `WorkspaceOperationStarted(OperationId)` | token binds ID, action ID, revision, definition digest and trust state |
| `workspace.close.preview.v1` / `execute.v1` | `Get-DistroNexusWorkspaceClosePreview` / `Close-DistroNexusWorkspace` | preview: `Id`; execute: `PreviewToken` | preview / action result | token binds ID, revision, close-policy digest |
| `workspace.operation.status.v1` | `Get-DistroNexusWorkspaceOperation` | `OperationId` | `WorkspaceOperationStatus(Progress, IsTerminal, Result)` | status is same-SID only and does not authorize execution |
| `workspace.cancel.v1` | `Stop-DistroNexusWorkspaceOperation` | `OperationId` | cancellation acknowledgement | operation ID is issued only with a running launch/retry result |

- Validation: every route uses a dedicated request record and `JsonUnmappedMemberHandling.Disallow`; no legacy generic `BridgeRequest` workspace operation is accepted after S40. Tokens and operation IDs are 32-byte random opaque values and records are bounded. The worker accepts only an operation ID whose record decrypts under the current user and has a matching packaged-worker identity; Core validates definition/action/path semantics and import/export content length/schema; WPF sends no raw Bridge JSON and performs no Core validation or JSON serialization/deserialization.
- Authorization and scope: all mutation/execute/cancel cmdlets use `SupportsShouldProcess`; execute/cancel is denied when WhatIf/confirmation declines. A WPF command shows its own dialog first; on approval the typed client supplies the PowerShell common parameter `Confirm=$false` to its non-interactive child invocation, and on decline it makes no cmdlet call. This is presentation consent only: the Core-issued token remains the execution authority. Direct public PowerShell preserves normal `-WhatIf` and interactive confirmation behavior. Preview tokens are single-use, same-user, time-limited and revision/fingerprint bound. No operation accepts a caller-selected Core root, host path, bridge operation name or arbitrary command.
- Errors and compatibility: preview/execute errors map to `Workspace.InvalidRequest`, `Workspace.PreviewInvalid`, `Workspace.PreviewExpired`, `Workspace.StateChanged`, `Workspace.TrustRequired`, `Workspace.OperationNotFound`, `Workspace.Cancelled` or `Workspace.Failed` with redacted message text. Existing command names remain exported where they denote an execute operation; `New-`/`Set-` become wrappers over save preview+execute only when called in one PowerShell process, while typed clients call the explicit preview command then the token-only execute command. New close/preview cmdlets are added to the manifest. Import/export parameters change from `Path` to `Content`/returned content; picker file I/O is a presentation adapter outside this slice.
- Audit and observability: Core continues structured outcome logging; module/client transport errors are mapped without raw payload, secret or path disclosure.

## Data and Execution Semantics

- Data ownership and retention: Core owns the workspace store and a new `WorkspaceGrantStore` plus `WorkspaceOperationStore` beneath the application root. Import content is transient request data; export content is a transient modeled response. The desktop may display it or use a user-selected external document interaction, but does not treat it as product state.
- State, transactions, idempotency, concurrency: `WorkspaceGrantStore` persists a DPAPI `CurrentUser`-protected record under a SHA-256 token filename and includes schema version, SID, operation kind, normalized request digest, workspace/document revision, definition/trust/close-policy fingerprint and expiry. It atomically consumes a token by rename-to-consumed before decrypting, validates all bindings after re-reading Core state, then deletes the consumed record. It sweeps invalid/expired/consumed records with bounded count/bytes. Every save/remove/duplicate/import/export/trust/launch/retry/close execute consumes only this durable token. List is read-only.
- Failure, retry, cancellation, recovery: preview causes no mutation. Launch/retry execute atomically consume their preview token, allocate a random Core-issued `OperationId`, persist a `Started` record and invoke the fixed packaged worker before returning `WorkspaceOperationStarted`. The client polls the fixed status cmdlet for bounded progress and the terminal result. `workspace.cancel.v1` writes a same-SID cancellation marker after `ShouldProcess`; the worker links a polling cancellation token to every action and checks the marker before/after actions; cooperative handlers receive that token. The worker writes a terminal redacted `Succeeded`/`Failed`/`Cancelled` result and deletes markers during bounded cleanup. At worker startup and status read, an operation that is marked running without a valid current worker lock is terminalized as `Workspace.WorkerInterrupted`; a duplicate worker cannot acquire the lock. When WPF cancels polling, it first invokes the fixed cancel cmdlet and then terminates/reaps the current status child; it never assumes process termination alone cancels Core work. Per-action progress/failure uses `WorkspaceActionResult`; retry requires a fresh retry preview. Close uses its distinct close token. The WPF layer never falls back to `IWorkspaceService` after transport failure.

## Security and Operations

- Threat/secret controls: treat imported content as untrusted; do not pass it through shell text or file paths. Preserve trust approval before command-capable actions. Redact transport exceptions and never return product store paths or executable process instructions outside modeled previews.
- Runtime/deployment constraints: no elevated helper or installation is added. Bridge protocol remains framed JSON with strict unmapped-member rejection.
- External acceptance: after repository checks, run a controlled real workspace matrix for each supported action type, trust transition, cancel, retry and close policy on a disposable WSL instance. This is a follow-up gate, not a condition for code-slice acceptance.

## Verification Strategy

- Unit/component: Core workspace service validation/grant/revision tests; durable operation start/status/cancel tests; typed client serialization/deserialization and `Confirm=$false` tests; module consent/content tests.
- Integration/runtime: Bridge protocol rejects unknown fields and accepts only fixed workspace routes; mocked runtime covers immediate operation IDs, progress polling, cancellation, action failure and token replay. Worker tests cover package-identity rejection, same-SID-only operation access, duplicate worker locking, startup failure, worker interruption/restart recovery and cancellation while the original module/Bridge process has exited.
- Structural/packaging: `WorkspacesViewModel` has no `IWorkspaceService`, no product document serializer/persistence route and invokes typed module methods; manifest exports only reviewed workspace commands; package contains exactly the reviewed WorkspaceWorker artifact. Run targeted xUnit workspace/client/bridge/worker/view-model tests, Pester unit tests and the Debug solution build.

## Open Items

| Item | Blocking level | Owner | Resolution |
| --- | --- | --- | --- |
| Repository contract implementation | Follow-up | Delivery slice S40 | Implement the fixed request/result routes and migration described above. |
| Real WSL workspace matrix | Follow-up | Release/UAT owner | Run after S40 repository acceptance. |
