# PowerShell-First Product Boundary Technical Design

## Scope and Requirement Traceability

- Project source/test/config/release roots: `src/PowerShell`, `src/Client/DistroNexus.Core`, `src/Client/DistroNexus.WorkspaceBridge`, `src/Client/DistroNexus.Desktop`, `src/Client/DistroNexus.Tests`, `tests/PowerShell`, `docs`, `tools`.
- Current architecture constraints: Core retains domain algorithms and protected host adapters; PowerShell is the only supported product execution boundary; Desktop is presentation-only.
- Existing compatibility and migration boundary: preserve existing cmdlet names and output behavior where compatible; move direct WPF calls behind typed module operations incrementally by capability family.
- Requirements: `docs/specs/powershell-first-requirements.md` FR-001 through FR-007.
- Decisions/constraints: `docs/architecture/powershell-first-decision.md`.
- Exclusions: deployment/publishing, signing, release workflow edits, and mutation of real WSL/Windows hosts.

| Requirement | Design section | Test or verification |
| --- | --- | --- |
| FR-001 | Architecture and Ownership; Contracts and Behavior | Desktop structural boundary test and capability inventory test. |
| FR-002 | Contracts and Behavior; Data and Execution Semantics | Manifest/public-function contract Pester tests and lazy bridge tests. |
| FR-003 | Contracts and Behavior | Pester success, validation, WhatIf/Confirm, and failure tests per mutation family. |
| FR-004 | Architecture and Ownership | Command-family inventory, bridge routing tests, and targeted family tests. |
| FR-005 | Architecture and Ownership | C# structural tests plus targeted view-model module-client tests. |
| FR-006 | Contracts and Behavior; Data and Execution Semantics | Bridge request/response and unknown-operation tests. |
| FR-007 | Data and Execution Semantics; Security and Operations | Existing and new path, grant, redaction, recovery, and cancellation tests. |

## Architecture and Ownership

The public flow is `PowerShell caller or WPF -> PowerShell module -> capability adapter -> WorkspaceBridge/Core or approved native command -> typed result`. The WPF client accesses this flow through a closed, typed `IPowerShellModuleClient` abstraction. It cannot submit arbitrary PowerShell text, arbitrary bridge operation names, or raw process arguments. The client operation registry is compiled from supported cmdlet families and serializes only validated typed parameters.

`src/PowerShell` owns public command names, parameter grammar, `ShouldProcess`, output/error envelopes, and the decision whether a command needs bridge support. `src/Client/DistroNexus.WorkspaceBridge` owns only internal versioned capability operations and invokes Core services. `src/Client/DistroNexus.Core` owns domain models, validation helpers, safe persistence and platform adapters. `src/Client/DistroNexus.Desktop` owns binding, dialogs, navigation, rendering, and an explicitly narrow external-presentation adapter. It must not reference Core business service interfaces after the relevant migration slice.

The migration uses these ordered capability families: (1) module-contract integrity and boundary guard; (2) instance lifecycle, catalog/source/cache, backup, tags, settings; (3) WSL and distribution configuration; (4) templates, marketplace, and workspace; (5) network, systemd, firewall; (6) recovery, health, diagnostics; (7) WSLg, containers, Docker; (8) monitoring, USB, platform/update/terminal; (9) WPF consumers; (10) closure evidence. A family is not complete until its exported commands, bridge/internal route, WPF consumers, tests, and inventory rows agree.

## Contracts and Behavior

- Contracts: Each command family declares command names, typed parameters, result schema, mutation classification, and bridge requirement in the capability inventory. Bridge requests use an operation identifier selected by the module, a versioned JSON request body, cancellation correlation, and a typed JSON response/error envelope. Desktop uses the corresponding typed client method rather than command text. The initial closed `IPowerShellModuleClient` registry exposes only `GetInstanceTagsAsync`; it maps to `Get-DistroNexusInstanceTag` and accepts only an optional instance name and cancellation token. It deliberately has no text/script, cmdlet-name, bridge-operation, or raw-argument input.
- Validation: Public PowerShell parameters reject missing, malformed, unsafe, or unsupported input before bridge invocation. Core repeats security-critical validation. Desktop validates only presentation affordances and relies on the returned validation error as authoritative.
- Authorization and scope: All mutations use `[CmdletBinding(SupportsShouldProcess)]` and `ShouldProcess`; `WhatIf` and declined confirmation prevent internal execution. Existing preview tokens, fingerprints, elevation grants, and trusted executable checks remain mandatory and are passed only through typed fields.
- Errors and compatibility: Preserve existing successful output fields where feasible. New commands emit stable objects rather than formatted strings. Bridge-unavailable errors use a stable error id, state the required component, and do not prevent non-bridge command import/execution. Unknown operation ids are rejected.
- Audit and observability: No new external telemetry. Request correlation and sanitized operation/error identifiers may be logged through existing diagnostic infrastructure without secrets or unredacted sensitive paths.

### Operational capability contract amendment

The operational families previously grouped in S05 are delivered as three independently accepted command families. Existing exported cmdlet names remain the public compatibility surface. Their internal WorkspaceBridge operation identifiers are versioned and capability-specific; the Bridge accepts legacy unversioned identifiers only as temporary private aliases during migration and the module itself invokes only the versioned identifier. A legacy alias has the same typed payload, result, consent, and error behavior as its versioned counterpart and is removed only after all module callers and tests use the versioned route.

| Family | Fixed Bridge operations | Public command contract | Mutation boundary |
| --- | --- | --- | --- |
| Systemd | `systemd.list.v1`, `systemd.preview.v1`, `systemd.execute.v1`, `systemd.details.v1`, `systemd.journal.v1` | Existing systemd list/action cmdlets gain typed details/journal query parameters or companion query cmdlets; action names and unit identifiers are validated before Bridge invocation. | Actions retain preview/fingerprint fields and `ShouldProcess`; reads never mutate. |
| Recovery and health | `recovery.list.v1`, `recovery.verify.v1`, `recovery.preview-create.v1`, `recovery.create.v1`, `recovery.preview-restore.v1`, `recovery.restore.v1`, `recovery.preview-remove.v1`, `recovery.remove.v1`, `recovery.clone.v1`, `recovery.notes.v1`, `recovery.retention.get.v1`, `recovery.retention.preview.v1`, `recovery.retention.set.v1`, `recovery.history.v1`, `health.scan.v1`, `health.repair-preview.v1`, `health.repair.v1`, `health.history.v1` | Existing recovery/health commands retain their names; missing clone, notes, retention, history commands use noun-specific names and typed records. `Get-DistroNexusRecoveryPointRetentionPreview` returns a one-shot, fingerprint-bound typed preview token consumed by retention update. | Create, restore, remove, clone, note update, retention update, and repair require existing preview/grant fields where applicable plus `ShouldProcess`. Retention set rejects missing, stale, replayed, or fingerprint-mismatched preview tokens before any recovery point is deleted. |
| Network, firewall, diagnostics | `network.status.v1`, `network.ip.v1`, `network.port-mappings.v1`, `network.probe.v1`, `network.mode.get.v1`, `network.mode.preview.v1`, `network.mode.set.v1`, `network.settings.preview.v1`, `network.settings.set.v1`, `firewall.list.v1`, `firewall.preview-create.v1`, `firewall.create.v1`, `firewall.preview-remove.v1`, `firewall.remove.v1`, `diagnostics.preview.v1`, `diagnostics.export.v1` | New commands use `Get`/`Test` for reads, `Set`/`New`/`Remove` for changes, and `Export-DistroNexusDiagnosticReport` for report output. The existing port-mapping cmdlet becomes a fixed Bridge client rather than direct host execution. | Mode/settings/firewall changes preserve preview tokens and `ShouldProcess`. Firewall execution remains inside the existing trusted `IFirewallOperationBroker` boundary; it has no transferable elevation-grant contract, so the module neither accepts nor fabricates one. Diagnostic export accepts only selected allowed/redacted inputs and a preview token, with `ShouldProcess`. |

Each route accepts one typed JSON payload and returns a typed JSON result. Unknown fields, unsupported operation identifiers, missing preview/grant fields, unsafe paths, and stale fingerprints are rejected before Core execution. Stable sanitized error codes identify validation, preview, grant, and Bridge-unavailable failures without exposing raw host output or sensitive paths. Desktop migration remains a separate consumer slice: no S05 command implementation gives WPF a generic route, direct Core reference, or new host-I/O authority.

### Platform-integrated capability contract amendment

The current capability audit confirms that the public module already owns fixed read and preview/execute contracts for container runtime inventory and Podman user-unit/connection operations, but `IntegrationsTabViewModel` still directly invokes `IContainerRuntimeService` and `IPlatformCapabilityService`. That is a prohibited second product path under FR-001, FR-004, and FR-005. Slice S21 therefore closes this one coherent, already-specified capability family before the broader remaining S06 work.

| Requirement | Current evidence | S21 contract and ownership | Verification |
| --- | --- | --- | --- |
| FR-001, FR-005 | `IntegrationsTabViewModel` injects Core container and platform services; `Get-DistroNexusContainerRuntimeStatus` already calls the fixed `containerRuntimeStatus` route and `Get-DistroNexusCapability -Name <name> -InstanceOnly` already returns the fixed instance-capability route. | Add only typed `IPowerShellModuleClient` methods for container snapshot, instance capability snapshot, Podman unit preview/execute, and Podman connection preview/execute. The module client invokes the exported cmdlets only; the view model receives no Core container/platform service. | Client command-shape tests and view-model tests reject direct service use while preserving the `InstanceSystemd` gate. |
| FR-003, FR-007 | Existing Podman public commands expose Core-issued, single-use previews and use `ShouldProcess`; Core owns endpoint validation, fingerprints, expiry, and replay checks. The current typed module-client invocation seam serializes only scalar parameters. | Preserve pipeline `-Preview` compatibility and add strict scalar execute parameter sets: unit execution requires `PreviewToken`, `InstanceName`, `Unit`, and `Action`; connection execution requires `PreviewToken`, `InstanceName`, `ConnectionName`, and `Endpoint`. The client may pass only these explicit returned preview fields; it may not construct a token, submit arbitrary command text, or weaken Core endpoint validation. | Pester `WhatIf`/invalid-input/replay/scalar-parameter coverage and xUnit client command-shape tests. |
| FR-004, FR-006 | `containerRuntimeStatus`, `podmanUserUnit*`, and `podmanConnection*` are fixed Bridge routes. | No new generic route, script runner, container/image CRUD, installation, or Docker settings mutation is introduced. Core remains the internal implementation selected by the module. | Bridge unknown-operation and command allow-list tests; structural review of complete slice diff. |

The S21 public result models are the existing sanitized Core runtime, instance-capability, and Podman result records. `Get-DistroNexusContainerRuntimeStatus -Name` is the sole inventory entry point; `Get-DistroNexusCapability -Name <name> -InstanceOnly` is the sole source of the existing `InstanceSystemd` gate. The four existing Podman preview/execute cmdlets remain the sole mutation entry points and retain their current `ShouldProcess`, single-use preview, TTL, fingerprint, and safe-endpoint semantics. Execute cmdlets retain `-Preview` for existing pipeline callers, but expose only the explicit scalar parameter sets above for the typed desktop client; both parameter sets call the same fixed Bridge operation and Core-issued token validation. This avoids a generic object/script serializer in `PowerShellService`. WPF confirmation text is presentation only; it must call the matching typed module-client execute method after the module consent boundary, never call Core execute directly.

The following remaining platform families are intentionally not combined with S21 because their contracts are materially incomplete: WSLg pin/reveal needs an opaque discovery/revision contract; Docker integration needs a Core-issued preview token and atomic settings model; USB mutation needs an authenticated PowerShell-to-elevated-helper grant boundary; monitoring needs bounded session ownership/cancellation semantics; terminal/explorer and application update need allow-listed external-launch contracts. They remain S06 follow-up design work, not implied by S21 completion.

## Data and Execution Semantics

- Data ownership and retention: Core owns settings, cache, catalog, backup, recovery, templates, and configuration persistence. Desktop never writes those stores. Existing retention and cleanup policies remain unchanged.
- State, transactions, idempotency, concurrency: Keep existing atomic replacement, journal/recovery, preview token, fingerprint, and grant semantics. Commands that start monitoring or streams define disposal and cancellation; no background state is owned by the WPF view model.
- Failure, retry, cancellation, recovery: Every bridge request receives cancellation. The module translates a typed bridge failure to a non-secret PowerShell error. No automatic retry is added for destructive commands. Partial-operation recovery remains in Core and is surfaced through command results/errors.

## Security and Operations

- Threat/secret controls: Desktop cannot tunnel arbitrary scripts. The module cannot expose arbitrary bridge operations. Preserve path containment, archive validation, signature/trust checks, elevation grants, secure string handling, and error redaction.
- Runtime/deployment constraints: Bridge resolution is lazy and deterministic. The packaged bridge remains an explicit dependency only for command families that use it; module-only commands continue to import when it is absent. A bridge-backed invocation without the runtime fails with the stable `DistroNexus.WorkspaceBridgeUnavailable` error id. Development resolution must not be silently preferred over the packaged runtime when both exist.
- External acceptance: Real WSL lifecycle, Windows feature repair, elevated USB attach/detach, systemd/network mutation, and GUI application launch require a disposable Windows/WSL UAT host. Those results are release evidence, not repository-test substitutes.

## Verification Strategy

- Unit/component: Pester manifest/function/duplicate-definition checks; public command success/negative/WhatIf tests; xUnit typed module-client and view-model tests; bridge routing/error tests; security/recovery regression tests.
- Integration/runtime: `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "TestScope!=Full"`; `tests/PowerShell/TestRunner.ps1 -TestType Unit`; add narrow filters for each family. WSL-dependent checks remain opt-in and are recorded as external evidence.
- Structural/packaging: `dotnet build src/Client/DistroNexus.slnx -c Debug`; module import contract tests; a static Desktop boundary test; final `tools/build.ps1 -Configuration Release` and website checks when a release candidate is requested.

## Open Items

| Item | Blocking level | Owner | Resolution |
| --- | --- | --- |
| Production/UAT host evidence | Follow-up | Release/UAT owner | Run named real-host scenarios in the closure slice; do not claim production readiness without them. |
