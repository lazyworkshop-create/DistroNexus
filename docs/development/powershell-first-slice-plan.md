# Implementation Slice Plan: PowerShell-First Product Boundary

## Sources

- Repository profile: `AGENTS.md`
- Base commit and branch: `68514b243cd670e5764d7f45115dec94b8635467`; `feature/2.3.0`
- Project source/test/config/release roots: `src/PowerShell`, `src/Client/DistroNexus.Core`, `src/Client/DistroNexus.WorkspaceBridge`, `src/Client/DistroNexus.Desktop`, `src/Client/DistroNexus.Tests`, `tests/PowerShell`, `docs`, `tools`
- Requirements: `docs/specs/powershell-first-requirements.md` FR-001 through FR-007
- Design: `docs/specs/powershell-first-design.md`
- Decisions/status: `docs/architecture/powershell-first-decision.md`
- Code/evidence: module manifest/public files, WorkspaceBridge routing, Core services, and Desktop view models named in the requirements record

## Dependency Order

S01 -> S02 -> S03 -> S04 -> S05 -> S06 -> S07 -> S08

## Slice S01: Verified module contract and migration baseline

### Status

Committed

### Objective

Verified module contract and migration baseline.

### Sources

Requirements FR-001 through FR-007 as applicable; `docs/specs/powershell-first-design.md`; `docs/architecture/powershell-first-decision.md`.

### Dependencies

None

### Allowed Paths

`docs/specs/powershell-first-requirements.md`, `docs/specs/powershell-first-design.md`, `docs/architecture/powershell-first-decision.md`, `docs/development/powershell-first-slice-plan.md`, `src/PowerShell/DistroNexus.psd1`, `src/PowerShell/DistroNexus.psm1`, `src/PowerShell/Public/Get-DistroNexusInstanceTag.ps1`, `src/PowerShell/Public/Add-DistroNexusInstanceTag.ps1`, `src/PowerShell/Public/Set-DistroNexusInstanceTag.ps1`, `src/PowerShell/Public/Remove-DistroNexusInstanceTag.ps1`, `tests/PowerShell/Unit/Public/ModuleExportContract.Tests.ps1`

### Excluded Paths

`src/Client/DistroNexus.Desktop/**`, `src/Client/DistroNexus.Core/**`, `src/Client/DistroNexus.WorkspaceBridge/**`, release and publishing surfaces

### Contract and Documentation

Publish the approved baseline; make manifest exports equal the unique public function set; remove duplicate tag definitions without changing tag grammar.

### Implementation Scope

Export every manifest command at runtime, including functions defined in composite public files, and add contract tests for definitions, exports, and discovery.

### Test Scope

Pester manifest/function equality, uniqueness, and command discovery.

### Acceptance Criteria

- Manifest and unique public definitions are identical.
- No tag command has more than one definition.
- Design and slice-plan validators pass.

### Verification Commands

```text
pwsh -NoProfile -File .agents/skills/agentteam-requirements-design/scripts/validate-design-readiness.ps1 -RequirementsPath docs/specs/powershell-first-requirements.md -DesignPath docs/specs/powershell-first-design.md
pwsh -NoProfile -File .agents/skills/agentteam-slice-delivery/scripts/validate-slice-plan.ps1 -Path docs/development/powershell-first-slice-plan.md
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
```

### Commit Boundary

Baseline documents, manifest/export correction, tag consolidation, and Pester contract tests.

### Out of Scope

New capability routes and Desktop migration.

## Slice S02: Lazy bridge runtime and typed desktop module client

### Status

Committed

### Objective

Lazy bridge runtime and typed desktop module client.

### Sources

Requirements FR-001 through FR-007 as applicable; `docs/specs/powershell-first-design.md`; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S01

### Allowed Paths

`src/PowerShell/DistroNexus.psm1`, `src/PowerShell/Private/WorkspaceBridge.ps1`, `src/Client/DistroNexus.Core/Interfaces/IPowerShellModuleClient.cs`, `src/Client/DistroNexus.Core/Services/PowerShellModuleClient.cs`, `src/Client/DistroNexus.Desktop/App.xaml.cs`, focused xUnit/Pester tests, `docs/specs/powershell-first-design.md`, plan

### Excluded Paths

Desktop view models/views, Core business services, release and publishing surfaces

### Contract and Documentation

Define the closed typed operation registry, lazy bridge resolution, and stable bridge-unavailable error.

### Implementation Scope

Permit non-bridge imports without the DLL and register a typed module client that cannot execute arbitrary text.

### Test Scope

Import-without-bridge, unknown-operation, serialization, cancellation, and unavailable-runtime tests.

### Acceptance Criteria

- A non-bridge command imports and runs without bridge artifacts.
- Desktop invokes only registered typed module operations.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~PowerShellModuleClient"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
```

### Commit Boundary

Runtime isolation and module-client foundation.

### Out of Scope

Feature-family migration.

## Slice S03: Lifecycle, catalog, cache, backup, tags, and settings command parity

### Status

Planned

### Objective

Lifecycle, catalog, cache, backup, tags, and settings command parity.

### Sources

Requirements FR-001 through FR-007 as applicable; `docs/specs/powershell-first-design.md`; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S02

### Allowed Paths

`src/PowerShell/Private`, lifecycle/catalog/package/backup/tag public command files, matching Core interfaces, `src/Client/DistroNexus.WorkspaceBridge`, focused xUnit/Pester tests, plan

### Excluded Paths

Desktop consumers, WPF views, release and publishing surfaces

### Contract and Documentation

Inventory and expose every lifecycle and supporting-state operation through typed module contracts.

### Implementation Scope

Add missing catalog-source/cache/state operations and preserve consent and safe persistence.

### Test Scope

Success, invalid input, WhatIf, declined confirmation, and bridge failure cases.

### Acceptance Criteria

- No supported lifecycle or supporting-state operation is Core/WPF-only.
- Every mutation retains PowerShell consent semantics.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "TestScope!=Full"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
```

### Commit Boundary

Lifecycle and support-state command family.

### Out of Scope

Desktop replacement.

## Slice S04: Configuration, templates, marketplace, and workspace command parity

### Status

Planned

### Objective

Configuration, templates, marketplace, and workspace command parity.

### Sources

Requirements FR-001 through FR-007 as applicable; `docs/specs/powershell-first-design.md`; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S02

### Allowed Paths

`src/PowerShell/Private`, configuration/template/marketplace/workspace public command files, matching Core interfaces, `src/Client/DistroNexus.WorkspaceBridge`, focused xUnit/Pester tests, plan

### Excluded Paths

Desktop consumers, WPF views, real configuration mutation, release/publishing surfaces

### Contract and Documentation

Define preview/fingerprint, trust, artifact, workspace-action, and result contracts.

### Implementation Scope

Expose missing configuration/template/workspace operations through module routes while preserving path safety, atomic writes, and action gates.

### Test Scope

Stale preview, unsafe path/archive, trust, WhatIf, cancellation, and workflow tests.

### Acceptance Criteria

- No supported configuration/template/workspace workflow is Core/WPF-only.
- Safety tokens and trust gates remain mandatory.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Configuration|FullyQualifiedName~Template|FullyQualifiedName~Workspace"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
```

### Commit Boundary

Configuration, template, marketplace, and workspace command families.

### Out of Scope

Desktop replacement and real-host UAT.

## Slice S05: Network, systemd, firewall, recovery, health, and diagnostics command parity

### Status

Planned

### Objective

Network, systemd, firewall, recovery, health, and diagnostics command parity.

### Sources

Requirements FR-001 through FR-007 as applicable; `docs/specs/powershell-first-design.md`; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S02

### Allowed Paths

`src/PowerShell/Private`, network/systemd/firewall/recovery/health/diagnostic public command files, matching Core interfaces, `src/Client/DistroNexus.WorkspaceBridge`, focused xUnit/Pester tests, plan

### Excluded Paths

Desktop consumers, real firewall/repair/recovery execution, release/publishing surfaces

### Contract and Documentation

Define network and recovery preview/token, diagnostic export, health history, and sanitized error contracts.

### Implementation Scope

Expose missing inspection and mutation operations with retained preview/grant/consent behavior.

### Test Scope

Collision, stale preview, unsafe destination, redaction, WhatIf, cancellation, and recovery failure tests.

### Acceptance Criteria

- All supported operations in these families have module command coverage.
- No mutation begins after WhatIf or declined confirmation.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Network|FullyQualifiedName~Systemd|FullyQualifiedName~Recovery|FullyQualifiedName~Health"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
```

### Commit Boundary

Network, recovery, health, and diagnostics command families.

### Out of Scope

Live host UAT.

## Slice S06: Platform-integrated command parity

### Status

Planned

### Objective

Platform-integrated command parity.

### Sources

Requirements FR-001 through FR-007 as applicable; `docs/specs/powershell-first-design.md`; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S02

### Allowed Paths

`src/PowerShell/Private`, WSLg/Podman/Docker/monitor/USB/capability/update/terminal public command files, matching Core interfaces, `src/Client/DistroNexus.WorkspaceBridge`, focused xUnit/Pester tests, plan

### Excluded Paths

Desktop consumers, real elevated USB actions, release/publishing surfaces

### Contract and Documentation

Define WSLg pin/reveal/icon, Docker, monitoring session, USB grant, platform/update, and terminal result contracts.

### Implementation Scope

Expose remaining platform operations through capability-specific routes; no arbitrary script or bridge tunnel.

### Test Scope

Unknown operation, stream cancellation, stale process token, unsigned helper, WhatIf, and stable error tests.

### Acceptance Criteria

- Every supported platform capability has an exported tested command.
- No generic execution channel is added.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "TestScope!=Full"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
```

### Commit Boundary

Remaining platform command families.

### Out of Scope

Desktop replacement and elevated UAT.

## Slice S07: WPF presentation-client migration and boundary enforcement

### Status

Planned

### Objective

WPF presentation-client migration and boundary enforcement.

### Sources

Requirements FR-001 through FR-007 as applicable; `docs/specs/powershell-first-design.md`; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S03, S04, S05, S06

### Allowed Paths

`src/Client/DistroNexus.Desktop/App.xaml.cs`, `src/Client/DistroNexus.Desktop/ViewModels`, `src/Client/DistroNexus.Desktop/Wizard`, external presentation adapters, `IPowerShellModuleClient`, architecture/view-model tests, design, plan

### Excluded Paths

PowerShell public implementation, Core domain services, release/publishing surfaces

### Contract and Documentation

Document UI-only presentation exceptions and the forbidden Desktop reference policy.

### Implementation Scope

Replace direct business-service and product-state host-I/O calls with typed module-client operations; retain only visual interaction and constrained presentation launchers.

### Test Scope

View-model behavior, dependency/architecture, and arbitrary-execution negative tests.

### Acceptance Criteria

- Desktop has no direct Core business-service execution path or product-state mutation.
- Structural tests reject forbidden references.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Architecture|FullyQualifiedName~ViewModel"
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

Desktop migration and structural enforcement.

### Out of Scope

New business capability work.

## Slice S08: Conformance and release-evidence closure

### Status

Planned

### Objective

Conformance and release-evidence closure.

### Sources

Requirements FR-001 through FR-007 as applicable; `docs/specs/powershell-first-design.md`; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S07

### Allowed Paths

`docs/development/powershell-first-capability-inventory.md`, release evidence documentation, architecture/export tests, plan

### Excluded Paths

Production behavior except narrowly required conformance test corrections; deployment, publishing, signing, and live system mutation

### Contract and Documentation

Publish final capability inventory, local verification record, UAT scenarios, and rollback posture.

### Implementation Scope

Audit every supported operation, reconcile module/bridge/Desktop routes, and separate repository readiness from UAT/production gates.

### Test Scope

Full structural audit, targeted tests, build, and release-readiness evidence.

### Acceptance Criteria

- Inventory has no unsupported operation or unexplained Desktop exception.
- Repository readiness and external gates are explicitly separated.

### Verification Commands

```text
dotnet build src/Client/DistroNexus.slnx -c Debug
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "TestScope!=Full"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
```

### Commit Boundary

Closure evidence and conformance tests.

### Out of Scope

Deployment or publishing.
