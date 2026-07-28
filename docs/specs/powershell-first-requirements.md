# PowerShell-First Product Boundary Requirements

## Purpose

DistroNexus must expose every supported product capability through its PowerShell module. The WPF desktop application is an optional interaction surface: it gathers input, invokes the same module contract, and presents the result. It must not be a second business-operation path.

## Project Context

- Project and repository: DistroNexus; `D:/repo/lazyworkshop-create/DistroNexus`.
- Existing capability and affected runtime surfaces: the PowerShell module, WorkspaceBridge/Core host, and WPF desktop client currently provide overlapping but inconsistent execution paths.
- Documentation/decision authority: this requirements record and `docs/architecture/powershell-first-decision.md` implement the explicit product-direction decision made on 2026-07-28. They supersede the thin-adapter direction in `docs/development/v2.3.0-architecture.md` where the two conflict.
- Environments and permitted mutation: repository-scoped source, test, and documentation changes are permitted. WSL, Windows configuration, package installation, publishing, deployment, and live data mutation are not part of this work.

## Scope

- In scope: every currently supported DistroNexus business capability in the module, Core services, WorkspaceBridge, and WPF client; public cmdlet contract integrity; WPF-to-module execution; structural conformance checks; documentation and test coverage.
- Out of scope: changing a user machine's WSL configuration during verification, deployment/publishing, signing, store configuration, release workflow changes, and replacing Core implementation algorithms merely because they run behind the module.
- Compatibility and rollout boundary: existing exported cmdlets remain compatible unless a documented correctness or security fix requires a new parameter or error. Core may remain an internal implementation host, but it is not a supported execution boundary for WPF or automation.

## Actors and Trust Boundaries

| Actor/component | Trust level | Permitted responsibility |
| --- | --- | --- |
| PowerShell user or automation | External caller | Invoke documented cmdlets and receive stable, typed output and errors. |
| PowerShell module | Product execution boundary | Validate input, own command grammar and mutation semantics, call internal adapters/bridge, and return the public result. |
| WorkspaceBridge and Core | Internal trusted implementation | Execute capability-specific operations only when requested by the module; preserve validation, path safety, recovery, and authorization invariants. |
| WPF desktop client | Presentation client | Collect user intent, show UI-only dialogs/navigation, invoke the module client, and render returned data. |
| External tools, WSL, Windows, network, templates | Untrusted or environment-owned | Receive only validated, scoped commands and data; failures are surfaced without leaking secrets. |

## Functional Requirements

### FR-001 Single supported execution boundary

Every supported product business operation must have a documented exported PowerShell command or an explicitly documented command sub-operation. WPF must invoke that module contract rather than directly invoking Core business interfaces, `wsl.exe`, registry, file, network, or process APIs for the operation.

Acceptance: a structural test rejects new Desktop references to forbidden business-service contracts or direct host-I/O APIs, and the capability inventory maps every supported operation to an exported command.

### FR-002 Complete and deterministic module contract

The module manifest exports every intended public DistroNexus function exactly once. Import must not fail for commands that do not require the WorkspaceBridge runtime; bridge-backed commands must provide an actionable, stable error if their runtime is unavailable.

Acceptance: automated tests compare manifest exports, unique public function definitions, and command discovery; tests cover lazy bridge resolution and its unavailable-runtime error.

### FR-003 Module-owned validation and mutation semantics

The module owns public input validation, stable result/error shaping, and mutation consent. Every mutating command supports `ShouldProcess`, honors `-WhatIf` and `-Confirm`, and does not start the internal operation when declined. Destructive or privileged operations retain current preview/grant/token protections.

Acceptance: each migrated mutation has success, invalid-input, `WhatIf`/decline, and underlying-operation-failure coverage.

### FR-004 Capability parity

The public module provides parity for supported lifecycle, catalog/source/cache, templates/marketplace, configuration, backup/recovery, tags/settings, workspace, network/systemd/firewall, health/diagnostics, monitoring, WSLg, containers/Docker, USB, platform/update, and terminal capabilities. Existing script-only implementations may be retained internally only if the module remains the sole public executor and their behavior meets FR-003.

Acceptance: the maintained capability inventory has no supported Core or WPF business operation without an exported command and tests cover each command family.

### FR-005 WPF presentation-only behavior

The desktop client may own visual state, rendering, navigation, input dialogs, and user-initiated presentation actions such as opening a returned local result in the shell. It must not create, delete, edit, validate-for-write, download, configure, or execute product business state independently.

Acceptance: migrated view models use typed module-client operations; file/process behavior remaining in Desktop is covered by the documented UI-only exception list and has no product-state mutation.

### FR-006 Internal bridge containment

WorkspaceBridge/Core operations are private implementation details. They must be capability-specific, use a versioned request/response envelope, and may not expose a generic arbitrary command or script execution facility to Desktop. Bridge lifetimes, cancellation, errors, and serialization must be bounded and testable.

Acceptance: Desktop does not reference bridge protocol/process internals; bridge operation routing is covered by contract tests and rejects unknown operations.

### FR-007 Security, reliability, and compatibility

Migration preserves path validation, redaction, authorization/grant checks, atomic persistence, cancellation, optimistic-concurrency/preview tokens, and recovery behavior. No secret or sensitive path is emitted in a public error beyond existing sanitized policy.

Acceptance: existing security and recovery tests continue to pass, and each changed high-risk family gains negative or boundary tests proving its safety contract.

## Non-Functional Requirements

- Security/authorization: no generic remote execution channel; preserve signature validation, elevation grants, path safety, and redaction.
- Reliability/recovery: preserve atomic writes, cancellation, idempotency, preview tokens, and cleanup guarantees; module errors are actionable.
- Audit/operations/retention: command names, output schemas, and bridge operation identifiers are versioned and documented; no new telemetry or retention is introduced.
- Performance/limits: do not load or start WorkspaceBridge for a command that does not require it; do not add polling or background sessions without bounded cancellation and disposal.

## Acceptance Criteria

- The exported command list is an exact, duplicate-free representation of intended module functions, including the recovery removal preview command.
- Every supported business capability is represented in the PowerShell capability inventory and has an exported command family with a test owner.
- The WPF project has no direct business-service execution path after all migration slices are committed; its remaining host actions are documented UI-only presentation actions.
- Mutations honor PowerShell consent semantics and preserve high-risk preview/grant protections.
- Full repository build and targeted C#/Pester checks pass; WSL-dependent/UAT evidence is reported separately rather than inferred.

## Open Decisions and External Inputs

| Item | Impact | Owner | Smallest next action |
| --- | --- | --- | --- |
| Real WSL, elevated USB, and Windows-feature validation | Cannot be proven in a non-mutating repository run. | Release/UAT owner | Execute the named closure scenarios on a disposable Windows/WSL host before a production claim. |

## Source Evidence

| Area | Source | What it confirms | Confidence |
| --- | --- | --- | --- |
| Existing design | `docs/development/v2.3.0-architecture.md` | Core/bridge ownership and the previous thin-module direction that this decision changes. | Confirmed |
| Version 2.3 requirements | `docs/specs/v2.3.0-requirements.md` | Existing PowerShell parity and shared-Core expectations. | Confirmed |
| Module surface | `src/PowerShell/DistroNexus.psd1`, `src/PowerShell/DistroNexus.psm1`, `src/PowerShell/Public` | 94 public definitions, 93 manifest exports, duplicate tag definitions, and unconditional bridge resolution. | Confirmed |
| Bridge surface | `src/Client/DistroNexus.WorkspaceBridge/Program.cs` | Capability-specific operation routing covers only a subset of Core services. | Confirmed |
| Desktop/Core execution | `src/Client/DistroNexus.Desktop/ViewModels`, `src/Client/DistroNexus.Core/Services` | WPF directly uses business services while Core owns host I/O and side effects. | Confirmed |
