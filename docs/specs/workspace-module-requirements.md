# Requirements: Workspace Module-Client Migration

## Purpose

Make the PowerShell module the sole product boundary for workspace management and execution. The WPF workspace screen must present typed module results and collect user consent only; it must not call workspace business services, serialize workspace definitions, or own workspace import/export files.

## Project Context

- Project and repository: DistroNexus; `D:\repo\lazyworkshop-create\DistroNexus`.
- Existing capability and affected runtime surfaces: `WorkspaceService` and `WorkspaceRuntime` own validation, storage, durable grants, trust and runtime actions; the WorkspaceBridge already exposes fixed workspace operations; public workspace cmdlets exist; `WorkspacesViewModel` currently bypasses them through `IWorkspaceService`.
- Documentation/decision authority: `AGENTS.md`, `docs/specs/powershell-first-requirements.md`, `docs/specs/powershell-first-design.md`, and this approved capability record.
- Environments and permitted mutation: repository edits and non-destructive local tests are permitted; live WSL/workspace actions and production publishing are excluded.

## Scope

- In scope: typed client contracts and WPF migration for list, create, update, duplicate, delete, import, export, trust approval, launch preview/launch, retry, close preview/close and typed progress; Core/Bridge/module changes needed to make file content Core-owned and tokenized.
- Out of scope: shortcut creation, Windows file picker UX, template/marketplace migration, real WSL runtime UAT, release publishing, and adding arbitrary command or path execution.
- Compatibility and rollout boundary: existing public workspace cmdlets remain available with their reviewed names; their semantics may be tightened so import/export content reaches Core through fixed payloads rather than PowerShell file I/O.

## Actors and Trust Boundaries

| Actor/component | Trust level | Permitted responsibility |
| --- | --- | --- |
| WPF workspace view model | Untrusted presentation | Render results, collect typed user input and display confirmation. |
| Typed module client | Transport adapter | Invoke only fixed exported cmdlets and deserialize modeled results. |
| PowerShell module | Public product boundary | Enforce consent and forward only approved typed workspace operations. |
| WorkspaceBridge and Core workspace services | Trusted business boundary | Validate definitions, own storage and grants, execute approved actions, redact failures. |
| Local workspace document selected by user | Untrusted input | Supply bounded import content only; it cannot select a Core storage path or executable route. |

## Functional Requirements

### FR-001 Module-only workspace operations

Every product workspace operation initiated by WPF shall invoke a typed `IPowerShellModuleClient` workspace operation. The view model shall have no `IWorkspaceService` field, no direct workspace runtime call and no product-state file I/O.

Acceptance: a structural test rejects direct workspace-service dependencies and operation tests verify typed client use for each supported command.

### FR-002 Fixed, versioned typed operation contracts

The module/client contract shall provide named, versioned, closed request/response contracts for list, create/save preview+execute, duplicate preview+execute, remove preview+execute, import preview+execute, export preview+content result, trust preview+execute, launch preview+execute, retry preview+execute, close preview+execute and launch cancellation. Every destructive or executable operation shall preserve Core revision and opaque-preview-token checks.

Acceptance: callers cannot substitute an ID, revision, action ID, definition or token at execute time; each execute accepts only its documented preview token and each Bridge route rejects unknown fields.

### FR-003 Core-owned validation and document handling

Core validates workspace definitions and untrusted imported content. The desktop client may construct typed in-memory transport records from its visual editor, display returned serialized export content and collect imported content, but it shall not call `WorkspaceValidation`, deserialize a definition JSON document, or write/read product workspace documents. PowerShell public commands shall not perform independent `Get-Content` or `Set-Content` operations for workspace import/export.

Acceptance: malformed, oversized or untrusted import input is rejected before state mutation; export does not expose Core storage paths.

### FR-004 Trust, consent, progress and failure behavior

Launch, retry, trust approval, close and destructive mutations honor PowerShell `ShouldProcess`, preserve Core trust/revision/token validation and return modeled progress/results. A WPF-confirmed execute invokes its cmdlet non-interactively with `-Confirm:$false`; a declined WPF confirmation makes no module invocation. Interactive public PowerShell keeps normal `ShouldProcess` behavior. Launch and retry return a Core-issued running operation ID immediately; cancellation uses a distinct fixed request bound to that ID, and status/progress polling is durable, same-user and observed by Core between actions and by cooperative action handlers. Failures and previews are redacted and do not expose Core product paths, raw commands outside the modeled preview, secrets or arbitrary process arguments.

Acceptance: a declined `-Confirm`, `-WhatIf`, expired/replayed token, state drift, cancellation and per-action failure cannot cause a direct WPF/Core fallback; a cancellation request never authorizes a different operation.

## Non-Functional Requirements

- Security/authorization: only Core issues and consumes execution grants; launch/retry run only in the authenticated packaged workspace worker selected by the Bridge, never in a caller-selected executable; no generic bridge tunnel, arbitrary host path or command API is introduced.
- Reliability/recovery: execute operations are revision-checked and retain current Core recovery/outcome behavior; progress is transport-only and cancellation is propagated.
- Audit/operations/retention: workspace state remains Core-owned; logs and errors use existing sensitive-data redaction.
- Performance/limits: preserve existing workspace validation limits and bound transport payloads to the same validated definition/import constraints.

## Acceptance Criteria

- `WorkspacesViewModel` invokes only typed workspace module-client operations for supported product behavior.
- Public workspace commands use fixed contracts and do not directly read or write workspace documents.
- Contract, WPF-routing and PowerShell consent tests cover every operation family and negative token/revision cases.
- Real workspace command execution remains an explicitly recorded external WSL/UAT gate.

## Open Decisions and External Inputs

| Item | Impact | Owner | Smallest next action |
| --- | --- | --- | --- |
| Real action execution across Terminal, VS Code, browser, systemd and compose handlers | External acceptance only | Release/UAT owner | Execute the documented workspace UAT matrix after repository verification. |

## Source Evidence

| Area | Source | What it confirms | Confidence |
| --- | --- | --- | --- |
| WPF bypass | `src/Client/DistroNexus.Desktop/ViewModels/WorkspacesViewModel.cs` | Direct `IWorkspaceService` calls cover list through launch/retry/close. | Confirmed |
| Existing public surface | `src/PowerShell/Public/WorkspaceCommands.ps1`, `Get-DistroNexusWorkspace.ps1`, `Export-DistroNexusWorkspace.ps1` | Fixed named workspace commands already exist, but import/export currently do file I/O in the module. | Confirmed |
| Trusted execution | `src/Client/DistroNexus.WorkspaceBridge/Program.cs`, `src/Client/DistroNexus.Core/Interfaces/IWorkspaceService.cs` | Bridge routes fixed operations to Core preview/grant/revision logic. | Confirmed |
| Existing validation | `src/Client/DistroNexus.Core/Models/WorkspaceModels.cs` | Definitions, actions, paths and preflight shapes are Core-validated. | Confirmed |
