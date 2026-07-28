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

S01 -> S02 -> S03 -> S04 -> S09 -> S10 -> S11 -> S12 -> S13 -> S14 -> S15 -> S16 -> S17 -> S18 -> S19 -> S20 -> S21 -> S22 -> S23 -> S24 -> {S25 blocked, S26 -> S27 -> S28 -> S29 -> S30 -> S31 -> S32 -> S33 -> S34} -> S06 -> S07 -> S08

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

## Slice S03: Instance-tag mutation consent contract

### Status

Committed

### Objective

Instance-tag mutations honor PowerShell consent semantics without changing their public grammar.

### Sources

Requirements FR-001 through FR-007 as applicable; `docs/specs/powershell-first-design.md`; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S02

### Allowed Paths

`src/PowerShell/Public/Add-DistroNexusInstanceTag.ps1`, `src/PowerShell/Public/Set-DistroNexusInstanceTag.ps1`, `src/PowerShell/Public/Remove-DistroNexusInstanceTag.ps1`, `src/PowerShell/Public/Get-DistroNexusInstanceTag.ps1`, `tests/PowerShell/Unit/Public/InstanceTagConsent.Tests.ps1`, plan

### Excluded Paths

Desktop consumers, WPF views, manifest changes, Core/bridge changes, release and publishing surfaces

### Contract and Documentation

Document the consent contract for tag mutation commands and preserve their existing output and validation behavior.

### Implementation Scope

Add `SupportsShouldProcess` and a `ShouldProcess` gate before each tag-state write. Do not change Desktop consumers, module exports, Core, or bridge routes.

### Test Scope

Success, validation, `WhatIf`, and declined confirmation cases using an isolated tag settings file.

### Acceptance Criteria

- Add, set, remove, and rename tag mutations do not persist state under `WhatIf` or declined confirmation.
- Existing tag mutation behavior still succeeds after confirmation and public parameters/output remain compatible.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "TestScope!=Full"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
```

### Commit Boundary

Tag mutation PowerShell consent behavior and focused Pester tests only.

### Out of Scope

Lifecycle/catalog/cache/backup/settings parity and all Desktop replacement work.

## Slice S04: Tag presentation-client migration

### Status

Committed

### Objective

All WPF tag interactions invoke typed PowerShell module operations rather than `ITagService`.

### Sources

Requirements FR-001 through FR-007 as applicable; `docs/specs/powershell-first-design.md`; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S02

### Allowed Paths

`src/Client/DistroNexus.Core/Interfaces/IPowerShellModuleClient.cs`, `src/Client/DistroNexus.Core/Services/PowerShellModuleClient.cs`, `src/Client/DistroNexus.Desktop/App.xaml.cs`, `src/Client/DistroNexus.Desktop/ViewModels/ManageTagsViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/SettingsViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/MainViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/WslInstanceViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/InstanceDetailViewModel.cs`, focused xUnit tests, plan

### Excluded Paths

PowerShell public command implementation, Core tag service/schema, bridge, unrelated Desktop surfaces, release/publishing surfaces

### Contract and Documentation

Define fixed typed tag query/mutation methods that map only to the supported tag cmdlets and preserve command error/cancellation semantics.

### Implementation Scope

Replace direct `ITagService` use in the named view models with `IPowerShellModuleClient`; remove their tag-service constructor dependencies and registrations only when no named consumer remains.

### Test Scope

Typed module-client parameter/response tests plus view-model tests proving tag load/mutation behavior uses the typed client.

### Acceptance Criteria

- Named WPF tag consumers have no `ITagService` dependency or direct tag-state persistence path.
- The typed module client cannot accept arbitrary command text and maps each tag action to its fixed exported cmdlet.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~Tag"
```

### Commit Boundary

Typed tag module-client and named WPF tag-consumer migration.

### Out of Scope

Configuration, template, marketplace, workspace, and other WPF capability families.

## Slice S09: Instance list, start, and stop bridge-backed module contract

### Status

Committed

### Objective

PowerShell automation can list, start, and stop WSL instances through typed WorkspaceBridge operations rather than direct cmdlet-side host access.

### Sources

FR-001 through FR-007; `BridgeWslManagerService.cs`; `IWslManagerService.cs`; existing instance cmdlets; lifecycle evidence in `docs/specs/powershell-first-design.md`.

### Dependencies

S02.

### Allowed Paths

`src/Client/DistroNexus.WorkspaceBridge/BridgeWslManagerService.cs`, `src/Client/DistroNexus.WorkspaceBridge/Program.cs`, `src/PowerShell/Public/Get-DistroNexusInstance.ps1`, `src/PowerShell/Public/Start-DistroNexusInstance.ps1`, `src/PowerShell/Public/Stop-DistroNexusInstance.ps1`, focused WorkspaceBridge xUnit tests, focused PowerShell Pester tests, plan.

### Excluded Paths

Desktop consumers/views, lifecycle mutations other than start/stop, manifest changes, Core `WslManagerService`, import/export/install/move/rename/remove commands, release/publishing surfaces.

### Contract and Documentation

Define versioned capability-specific bridge operations for instance list/start/stop; map stable typed payloads and bridge errors without exposing generic command execution.

### Implementation Scope

Expand the bridge adapter only for list/start/stop. Migrate the three cmdlets to invoke those operations; start/stop remain `SupportsShouldProcess` and do not invoke bridge work under `WhatIf` or declined confirmation.

### Test Scope

Bridge route success, unsupported/invalid payload failure, cmdlet fixed operation mapping, list result conversion, and start/stop consent behavior.

### Acceptance Criteria

- Get/Start/Stop-DistroNexusInstance execute through fixed bridge operations, not direct `wsl.exe`/registry calls in their public cmdlet bodies.
- Start/stop do not initiate bridge operations under `WhatIf` or declined confirmation.
- No generic script, command, or operation tunnel is introduced.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~WorkspaceBridgeProtocol|FullyQualifiedName~WslManager"
pwsh -NoProfile -Command "Invoke-Pester -Path tests/PowerShell/Unit/Public"
```

### Commit Boundary

Bridge-backed list/start/stop instance module contract and focused tests.

### Out of Scope

Desktop lifecycle consumer migration and all other instance lifecycle commands.

## Slice S10: Instance list and stop presentation-client migration

### Status

Committed

### Objective

The WPF consumers of instance listing and stopping invoke fixed typed PowerShell module operations instead of `IWslManagerService`.

### Sources

FR-001, FR-004, FR-005, FR-006; S09; lifecycle UI inventory; `IPowerShellModuleClient` and named view model evidence.

### Dependencies

S09.

### Allowed Paths

`src/Client/DistroNexus.Core/Interfaces/IPowerShellModuleClient.cs`, `src/Client/DistroNexus.Core/Services/PowerShellModuleClient.cs`, `src/Client/DistroNexus.Desktop/ViewModels/MainViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/WslInstanceViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/ManageTagsViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/WslConfigSectionViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/SettingsViewModel.cs`, `src/Client/DistroNexus.Desktop/Wizard/Steps/InstallPathStep.cs`, `src/Client/DistroNexus.Desktop/Wizard/InstallWizardWorkflowViewModel.cs`, directly impacted xUnit tests, plan.

### Excluded Paths

PowerShell public commands, WorkspaceBridge, Core `WslManagerService`, Desktop DI registration, all lifecycle operations except list/start/stop, release/publishing surfaces.

### Contract and Documentation

Extend the closed module client with typed list/start/stop methods only. Define typed list result conversion and cancellation/error behavior; no arbitrary command text or operation identifier may be accepted.

### Implementation Scope

Replace direct list/stop calls in the named consumers. Keep `IWslManagerService` where those objects still need distinct out-of-scope lifecycle methods. Replace it entirely only where no out-of-scope use remains.

### Test Scope

Module-client fixed command/parameter/result/cancellation tests; direct view-model behavior/routing tests for each named list/stop use; compile affected constructor fixtures.

### Acceptance Criteria

- Named WPF list/stop call sites have no direct `GetInstancesAsync` or `StopInstanceAsync` invocation.
- The typed client maps only to exported Get/Start/Stop instance commands and accepts no arbitrary command text.
- All directly affected view-model tests and typed-client tests pass.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~Tag|FullyQualifiedName~InstallWizard"
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

Typed instance list/start/stop client contract and named WPF consumer migration.

### Out of Scope

Import/export/install/move/rename/remove/backup lifecycle behavior and their WPF consumers.

## Slice S11: Global settings bridge-backed module contract

### Status

Committed

### Objective

Automation can get, save, and reset typed global DistroNexus settings through exported PowerShell commands backed by fixed WorkspaceBridge operations.

### Sources

FR-001 through FR-007; `ISettingsService.cs`; `SettingsService.cs`; current bridge composition; settings/catalog evidence inventory.

### Dependencies

S02.

### Allowed Paths

`src/Client/DistroNexus.WorkspaceBridge/Program.cs`, `src/PowerShell/DistroNexus.psd1`, `src/PowerShell/Public/Get-DistroNexusSettings.ps1`, `src/PowerShell/Public/Set-DistroNexusSettings.ps1`, `src/PowerShell/Public/Reset-DistroNexusSettings.ps1`, focused WorkspaceBridge xUnit tests, focused PowerShell Pester tests, plan.

### Excluded Paths

Desktop consumers, `SettingsService`/settings schema, catalog/source/package services, private Config scripts, existing package commands, release/publishing surfaces.

### Contract and Documentation

Define fixed versioned `settings.get.v1`, `settings.save.v1`, and `settings.reset.v1` bridge operations around the typed `GlobalSettings` model. The public setter accepts only modeled fields, not arbitrary JSON; reset and save use `ShouldProcess`.

### Implementation Scope

Add exported Get/Set/Reset settings commands and bridge routes. Preserve settings-service validation/persistence; reject malformed payloads and unknown operations with stable errors.

### Test Scope

Bridge route success/invalid-payload tests; command parameter/mapping/error/WhatIf/decline tests; manifest export contract coverage.

### Acceptance Criteria

- Global settings get/save/reset have fixed exported module commands and fixed bridge operations.
- Save/reset do not invoke the bridge under `WhatIf` or declined confirmation.
- No arbitrary settings JSON, script text, or bridge-operation input is exposed.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~WorkspaceBridgeProtocol|FullyQualifiedName~Settings"
pwsh -NoProfile -Command "Invoke-Pester -Path tests/PowerShell/Unit/Public"
```

### Commit Boundary

Typed global settings module contract and focused tests.

### Out of Scope

WPF settings page migration, catalog/source/cache, and private Config-script consolidation.

## Slice S12: Settings presentation-client migration

### Status

Committed

### Objective

The main settings UI, main-window preferences, and instance confirmation preference use fixed typed PowerShell settings operations instead of `ISettingsService`.

### Sources

FR-001, FR-004, FR-005, FR-006; S11; settings UI inventory; `SettingsViewModel`, `MainViewModel`, and `WslInstanceViewModel` evidence.

### Dependencies

S11.

### Allowed Paths

`src/Client/DistroNexus.Core/Interfaces/IPowerShellModuleClient.cs`, `src/Client/DistroNexus.Core/Services/PowerShellModuleClient.cs`, `src/Client/DistroNexus.Desktop/ViewModels/SettingsViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/MainViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/WslInstanceViewModel.cs`, `src/Client/DistroNexus.Desktop/App.xaml.cs`, directly impacted xUnit tests, plan.

### Excluded Paths

App bootstrap/module-path resolution, wizard settings defaults, legacy install wizard, PowerShell public commands, WorkspaceBridge, `SettingsService`/settings schema, catalog/source/package services, release/publishing surfaces.

### Contract and Documentation

Extend the closed client with typed get/save/reset settings methods that map solely to S11 commands. Document the intentional application-bootstrap exception: module path resolution precedes module client construction and remains an internal composition concern until a separate bootstrap design is approved.

### Implementation Scope

Remove direct `ISettingsService` dependencies/calls in the named view models. Preserve UI preference behavior, auto-save, confirmation decisions, cancellation and errors. Keep the service in app composition for bootstrap only.

### Test Scope

Fixed settings client mapping/partial update/reset/cancellation tests; named view-model load/save/reset/preference/confirmation routing tests; impacted constructor fixtures compile.

### Acceptance Criteria

- SettingsViewModel, MainViewModel, and WslInstanceViewModel contain no direct `ISettingsService` calls.
- Typed settings client maps only to Get/Set/Reset-DistroNexusSettings and exposes no arbitrary command or JSON input.
- App retains `ISettingsService` only for documented bootstrap/module-path resolution, not normal UI settings behavior.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~Settings|FullyQualifiedName~ViewModel"
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

Typed settings module-client extension and the named settings UI migration.

### Out of Scope

Wizard defaults, legacy install wizard, and bootstrap lifecycle redesign.

## Slice S13: Catalog source bridge-backed module contract

### Status

Committed

### Objective

Automation can list current/default sources, add, update, remove, test, enable, reorder, and reset catalog sources through fixed PowerShell commands backed by `ICatalogSourceManager` bridge operations.

### Sources

FR-001 through FR-007; `ICatalogSourceManager.cs`; `CatalogSourceManager.cs`; `SourceManagerViewModel.cs`; settings/catalog evidence inventory.

### Dependencies

S11.

### Allowed Paths

`src/Client/DistroNexus.WorkspaceBridge/Program.cs`, `src/PowerShell/DistroNexus.psd1`, catalog-source public command scripts, focused WorkspaceBridge xUnit tests, focused PowerShell Pester tests, plan.

### Excluded Paths

Desktop consumers, `CatalogSourceManager`/settings schema, `CatalogService`/package/cache services, private Config scripts, existing package commands, release/publishing surfaces.

### Contract and Documentation

Define capability-specific versioned source operations and typed payloads. Source mutations use modeled identifiers/URLs/order and `ShouldProcess`; no raw settings JSON, arbitrary URL command, script, or generic bridge operation is accepted.

### Implementation Scope

Compose `ICatalogSourceManager` in bridge and expose fixed list/defaults/add/update/remove/test/active/reorder/reset routes. Add matching exported command family while retaining manager validation and source persistence semantics.

### Test Scope

Bridge success/invalid payload/error tests; command mapping, validation, `WhatIf`/decline tests; manifest/export contract verification.

### Acceptance Criteria

- Every `ICatalogSourceManager` operation has a fixed exported command and bridge operation.
- Source mutations do no bridge work under `WhatIf` or declined confirmation.
- No generic configuration/script bridge surface is introduced.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~WorkspaceBridgeProtocol|FullyQualifiedName~CatalogSource"
pwsh -NoProfile -Command "Invoke-Pester -Path tests/PowerShell/Unit/Public"
```

### Commit Boundary

Catalog source module contract and focused tests.

### Out of Scope

Desktop source-manager migration, catalog refresh/load/search/package/cache behavior, and source-to-catalog refresh integration.

## Slice S14: Catalog source desktop consumer migration

### Status

Committed

### Objective

The Source Manager presentation flow uses the closed typed PowerShell module client for every catalog-source operation and no longer calls `ICatalogSourceManager` directly.

### Sources

FR-001 through FR-007; S13 catalog-source contract; `SourceManagerViewModel.cs`; `IPowerShellModuleClient.cs`; `PowerShellModuleClient.cs`.

### Dependencies

S13.

### Allowed Paths

`src/Client/DistroNexus.Core/Interfaces/IPowerShellModuleClient.cs`, `src/Client/DistroNexus.Core/Services/PowerShellModuleClient.cs`, `src/Client/DistroNexus.Core/Services/PowerShellService.cs`, `src/Client/DistroNexus.Desktop/ViewModels/SourceManagerViewModel.cs`, `src/Client/DistroNexus.Desktop/App.xaml.cs`, focused module-client, module-parameter formatting, and Source Manager xUnit tests, plan.

### Excluded Paths

PowerShell public commands/manifest, WorkspaceBridge, `ICatalogSourceManager`/`CatalogSourceManager`, catalog/package/cache services, views/XAML/navigation, settings schema, release/publishing surfaces.

### Contract and Documentation

Add only named catalog-source methods and explicit create/update request models to the presentation client. Map each method to its existing fixed module cmdlet and modeled parameters; do not expose command text, scripts, bridge operations, raw JSON, or generic execution APIs.

### Implementation Scope

Map list/singleton results, add/update source results, and boolean mutation results with deterministic invalid-result failures. Preserve explicit Boolean `false` values at the PowerShell module boundary. Migrate Source Manager list/add/update/remove/test/active/reorder/reset calls and remove the now-unused desktop `ICatalogSourceManager` registration.

### Test Scope

Module-client fixed-command/parameter/result/error/cancellation tests; module parameter-formatting tests for explicit Boolean values; Source Manager routing tests covering every command handler; exact typed-client surface assertion.

### Acceptance Criteria

- `SourceManagerViewModel` has no `ICatalogSourceManager` dependency or direct runtime manager call.
- Every Source Manager operation maps through one named typed client method and its existing fixed module cmdlet.
- The desktop client remains a closed, typed contract with no generic execution escape hatch.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~PowerShellModuleClientTests|FullyQualifiedName~SourceManagerViewModelRoutingTests"
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

Typed catalog-source desktop client and Source Manager migration with focused tests.

### Out of Scope

Catalog loading/refresh/search/package/cache behavior, source defaults UI, and source-to-catalog refresh integration.

## Slice S15: Native catalog read contract

### Status

Committed

### Objective

Catalog list, search, and package lookup run natively in Core behind fixed module and Bridge read contracts, with no Core-to-module invocation.

### Sources

`docs/specs/powershell-first-catalog-requirements.md` FR-101, FR-102, FR-106, FR-108; `docs/specs/powershell-first-catalog-design.md`.

### Dependencies

S14 and the approved catalog design.

### Allowed Paths

`src/Client/DistroNexus.Core/Services/CatalogService.cs`, `src/Client/DistroNexus.Core/Interfaces/IPowerShellModuleClient.cs`, `src/Client/DistroNexus.Core/Services/PowerShellModuleClient.cs`, `src/Client/DistroNexus.WorkspaceBridge/Program.cs`, `src/PowerShell/Public/Get-DistroNexusPackage.ps1`, `src/Client/DistroNexus.Desktop/ViewModels/PackageManagerViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/SettingsViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/InstallWizardViewModel.cs`, `src/Client/DistroNexus.Desktop/Wizard/InstallWizardWorkflowViewModel.cs`, `src/Client/DistroNexus.Desktop/Wizard/Steps/SelectDistributionStep.cs`, focused listed/new C# tests, `tests/PowerShell/Unit/Public/Get-DistroNexusPackageBridge.Tests.ps1`, plan.

### Excluded Paths

`Update-DistroNexusCatalog.ps1`, `Remove-DistroNexusPackage.ps1`, `Save-DistroNexusPackage.ps1`, private Config scripts, `CatalogSourceManager`, cache mutation methods, `DownloadTaskManager`, App composition, arbitrary bridge execution, release/publishing surfaces.

### Contract and Documentation

Use only `catalog.list.v1`, `catalog.search.v1`, and `catalog.get.v1` with typed bounded payloads and matching fixed public commands. Preserve legacy `Get-DistroNexusPackage -Family` behavior; do not add refresh or cache mutation routes.

### Implementation Scope

Remove `IPowerShellService` use from CatalogService read paths while retaining the field temporarily for excluded refresh/delete methods; the later refresh/cache slice removes the remaining dependency. Implement deterministic snapshot/cache/bundled fallback reads, compose native service in bridge, and migrate named read-only Desktop catalog consumers to the typed client. A no-catalog read returns an empty typed list/null lookup; it never fetches the network.

### Test Scope

Native read-precedence/search/lookup/cancellation tests; bridge payload/unknown-route tests; command mapping tests; typed client and WPF routing tests; structural assertion against Core-to-module catalog calls.

### Acceptance Criteria

- Catalog read paths never call the PowerShell module from Core.
- Public and WPF reads use the same fixed module contract.
- No refresh, cache deletion, or download state behavior changes in this slice.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Catalog|FullyQualifiedName~WorkspaceBridgeProtocol|FullyQualifiedName~PowerShellModuleClient"
pwsh -NoProfile -Command "Invoke-Pester -Path tests/PowerShell/Unit/Public"
```

### Commit Boundary

Native catalog read contract and read-only consumer migration.

### Out of Scope

Catalog refresh, source persistence, cache mutations, and durable download-task migration.

## Slice S16: Native catalog refresh contract

### Status

Committed

### Objective

Catalog refresh runs natively behind a fixed PowerShell and Bridge contract, removing CatalogService's remaining Core-to-module call.

### Sources

`docs/specs/powershell-first-catalog-requirements.md` FR-101, FR-103, FR-104, FR-108; `docs/specs/powershell-first-catalog-design.md`.

### Dependencies

S15 and accepted S16 cache mutation contract amendment.

### Allowed Paths

Catalog Core implementation/models required for native refresh; WorkspaceBridge `catalog.refresh.v1`; `Update-DistroNexusCatalog`; typed module client; Package Manager refresh consumer; focused C#/Pester tests; plan.

### Excluded Paths

All package-cache routes/commands/UI, download task lifecycle, `Save-DistroNexusPackage`, source-management commands/manager, generic bridge execution, package installation, release/publishing surfaces.

### Contract and Documentation

Implement only `catalog.refresh.v1`. The mutation uses `ShouldProcess`; refresh validates every source before HTTP, disables redirects, and atomically preserves known-good state on failure.

### Implementation Scope

Remove `IPowerShellService` from CatalogService. Add typed source-priority refresh and migrate Package Manager refresh calls.

### Test Scope

Native refresh/source security tests; bridge route and malformed payload tests; Pester compatibility/WhatIf/decline/failure tests; typed-client and Package Manager routing tests; structural test proving CatalogService has no PowerShell dependency.

### Acceptance Criteria

- CatalogService has no PowerShell service dependency or module call.
- Refresh is callable only through a fixed module contract and honors PowerShell consent.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Catalog|FullyQualifiedName~WorkspaceBridgeProtocol|FullyQualifiedName~PowerShellModuleClient"
pwsh -NoProfile -Command "Invoke-Pester -Path tests/PowerShell/Unit/Public"
```

### Commit Boundary

Native catalog refresh module contract and Package Manager refresh migration.

### Out of Scope

Package-cache operations, package download task persistence/progress/retry, package installation, and source manager UI changes.

## Slice S17: Native package-cache mutation contract

### Status

Committed

### Objective

Package-cache location, usage, deletion, and clear run through fixed PowerShell/Bridge contracts with persistent authenticated cache-entry tokens, and remaining Settings/Package Manager cache calls leave Desktop.

### Sources

FR-105, FR-108, FR-109; approved catalog cache design.

### Dependencies

S16.

### Allowed Paths

Catalog cache Core/bridge/module/client/Desktop consumer/test paths required by the approved contract and plan.

### Excluded Paths

Catalog refresh, download task lifecycle, source manager, package installation, and generic bridge execution.

### Contract and Documentation

Implement only package-cache location, usage, delete, and clear routes with persistent protected token verification and `ShouldProcess` mutations.

### Implementation Scope

Add pure root resolution, streaming usage, authenticated cross-process tokens, contained deletion/clear, fixed commands, typed client, and cache UI migration.

### Test Scope

Native token/containment/streaming tests; bridge/Pester consent tests; typed client and WPF routing tests.

### Acceptance Criteria

- No cache mutation occurs outside fixed module commands.
- Returned cache tokens work in a later module process only for unchanged contained files.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Catalog|FullyQualifiedName~PowerShellModuleClient"
pwsh -NoProfile -Command "Invoke-Pester -Path tests/PowerShell/Unit/Public"
```

### Commit Boundary

Native package-cache mutation contract and Desktop cache migration.

### Out of Scope

Download tasks, package installation, and catalog refresh.

## Slice S05: Network, systemd, firewall, recovery, health, and diagnostics command parity

### Status

In Progress

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

### Supersession Note

This broad legacy planning entry is not delegated. Its execution scope is split into S18, S19, and S20 below.

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

## Slice S18: Versioned systemd, recovery, and health command contracts

### Status

Committed

### Objective

Move existing systemd, recovery, and health module contracts to fixed versioned Bridge identifiers and add their missing typed read, history, and metadata operations.

### Sources

FR-001 through FR-007; `docs/specs/powershell-first-design.md` operational capability contract amendment; `docs/architecture/operational-bridge-versioning-decision.md`.

### Dependencies

S17.

### Allowed Paths

Existing systemd/recovery/health public commands and manifest; WorkspaceBridge; matching Core interfaces/services/models required for declared operations; focused bridge/C#/Pester tests; plan.

### Excluded Paths

Network/firewall/diagnostics, Desktop consumers, generic bridge execution, real host mutation, release/publishing.

### Contract and Documentation

Module calls only versioned `systemd.*.v1`, `recovery.*.v1`, and `health.*.v1` routes. Unversioned routes are private compatibility aliases with identical typed payloads and results. Retention uses `recovery.retention.preview.v1` to issue a one-shot fingerprint-bound token; `recovery.retention.set.v1` accepts that token and rejects missing, stale, replayed, or mismatched state before deletion.

### Implementation Scope

Add fixed versioned route handlers, typed command mappings, and only the missing declared read/history/metadata operations. Preserve existing public names and mutation protections.

### Test Scope

Route alias/version parity, typed validation/results, stale preview/grant failures, WhatIf/decline, cancellation, and unsupported operation errors.

### Acceptance Criteria

- Every supported systemd/recovery/health operation has one exported fixed command path and a versioned Bridge route.
- No mutation crosses Bridge after WhatIf or declined confirmation.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Systemd|FullyQualifiedName~Recovery|FullyQualifiedName~Health|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -Command "Invoke-Pester -Path tests/PowerShell/Unit/Public"
```

### Commit Boundary

Versioned systemd, recovery, and health module/Bridge contracts with focused tests.

### Out of Scope

Desktop migration and real host UAT.

## Slice S19: Network and firewall command contracts

### Status

Committed

### Objective

Expose network inspection/configuration and firewall operations only through typed fixed module and Bridge contracts.

### Sources

FR-001 through FR-007; operational capability contract amendment and versioning decision.

### Dependencies

S18.

### Allowed Paths

Network/firewall public commands and manifest; WorkspaceBridge; matching Core interfaces/services/models; focused C#/Pester tests; plan.

### Excluded Paths

Systemd/recovery/health/diagnostics, Desktop consumers, direct host script execution, generic bridge execution, live firewall/network mutation, release/publishing.

### Contract and Documentation

Use only declared `network.*.v1` and `firewall.*.v1` operations; preserve preview, collision, containment, and elevation grant requirements.

### Implementation Scope

Replace direct port-mapping host execution with fixed Bridge routing and add typed network/firewall command mappings without changing Desktop consumers.

### Test Scope

Typed route/result tests, invalid input/no execution, preview/grant/stale failures, WhatIf/decline, cancellation, and unknown operations.

### Acceptance Criteria

- Port mapping is no longer a direct module host script; it is a fixed versioned bridge operation.
- Every supported firewall mutation retains preview/elevation/consent protections.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Network|FullyQualifiedName~Firewall|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -Command "Invoke-Pester -Path tests/PowerShell/Unit/Public"
```

### Commit Boundary

Versioned network/firewall module and Bridge contracts with focused tests.

### Out of Scope

Desktop migration and live host UAT.

## Slice S20: Diagnostic preview and export contract

### Status

Committed

### Objective

Expose diagnostic preview and redacted export through a typed PowerShell and Bridge contract.

### Sources

FR-001 through FR-007; operational capability contract amendment and versioning decision.

### Dependencies

S18.

### Allowed Paths

Diagnostic public commands and manifest; WorkspaceBridge; diagnostic Core interfaces/services/models; focused C#/Pester tests; plan.

### Excluded Paths

Network/firewall/systemd/recovery/health, Desktop consumers, arbitrary paths/log collection, generic bridge execution, release/publishing.

### Contract and Documentation

`diagnostics.preview.v1` returns a typed preview token and redacted selection metadata. `diagnostics.export.v1` accepts only that token and validated allowed destination, and uses `ShouldProcess`.

### Implementation Scope

Add typed diagnostic preview/export Core, Bridge, and command contracts; retain Core redaction and token validation as the only export authority.

### Test Scope

Redaction, unsafe destination, stale token, WhatIf/decline, cancellation, and Bridge error tests.

### Acceptance Criteria

- No diagnostic export occurs without an unexpired preview token and PowerShell consent.
- No raw diagnostic path or unredacted host output enters public errors.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Diagnostic|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -Command "Invoke-Pester -Path tests/PowerShell/Unit/Public"
```

### Commit Boundary

Diagnostic preview/export module and Bridge contract with focused tests.

### Out of Scope

Desktop migration and real diagnostic collection UAT.

## Slice S21: Container runtime and Podman presentation-client migration

### Status

Committed

### Objective

Make the exported container-runtime and Podman command contracts the only execution path used by the integrations presentation surface.

### Sources

Requirements FR-001, FR-003 through FR-007; `docs/specs/powershell-first-design.md` Platform-integrated capability contract amendment; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S20

### Allowed Paths

`src/PowerShell/Public/Get-DistroNexusCapability.ps1`, `src/PowerShell/Public/Get-DistroNexusContainerRuntimeStatus.ps1`, `src/PowerShell/Public/Get-DistroNexusPodmanUserUnitPreview.ps1`, `src/PowerShell/Public/Invoke-DistroNexusPodmanUserUnit.ps1`, `src/PowerShell/Public/Get-DistroNexusPodmanConnectionPreview.ps1`, `src/PowerShell/Public/Invoke-DistroNexusPodmanConnection.ps1`, `src/PowerShell/DistroNexus.psd1`, `tests/PowerShell/Unit/Public/PodmanUserUnit.Tests.ps1`, `tests/PowerShell/Unit/Public/PodmanConnection.Tests.ps1`, `src/Client/DistroNexus.Core/Interfaces/IPowerShellModuleClient.cs`, `src/Client/DistroNexus.Core/Services/PowerShellModuleClient.cs`, `src/Client/DistroNexus.Tests/Services/PowerShellModuleClientTests.cs`, `src/Client/DistroNexus.Desktop/ViewModels/WslInstanceViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/InstanceDetailViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/Tabs/IntegrationsTabViewModel.cs`, `src/Client/DistroNexus.Tests/ViewModels/IntegrationsContainerRuntimeTests.cs`, focused Pester/xUnit tests, plan.

### Excluded Paths

Core container runtime adapters and their safety policy, Docker integration, container/image/project CRUD, Podman installation, arbitrary command execution, USB, monitoring, WSLg, terminal, release/publishing surfaces.

### Contract and Documentation

Record the existing fixed container/PODMAN public command names as the sole client contract. Add only explicit typed module-client methods for inventory, the existing `Get-DistroNexusCapability -Name <name> -InstanceOnly` query used to preserve the `InstanceSystemd` gate, user-unit preview/execute, and connection preview/execute. Retain pipeline `-Preview` compatibility and add strict scalar execute parameter sets from returned preview data: user-unit `PreviewToken`/`InstanceName`/`Unit`/`Action`, connection `PreviewToken`/`InstanceName`/`ConnectionName`/`Endpoint`. Preserve Core-issued token, TTL, fingerprint, replay, and endpoint-validation semantics without exposing generic cmdlet, Bridge-operation, object serializer, or raw process arguments.

### Implementation Scope

Replace direct `IContainerRuntimeService` and `IPlatformCapabilityService` execution in `IntegrationsTabViewModel` with typed `IPowerShellModuleClient` calls for the capability data it renders and for container/Podman actions it initiates. Preserve the current `InstanceSystemd` gate exclusively from the typed instance-capability command result. WPF continues to render effects and confirmation UI only; mutation execution goes through the existing public cmdlet's `ShouldProcess` boundary.

### Test Scope

Add command-shape/typed-result tests for every new module-client method; verify invalid values cannot result in command invocation; verify view-model inventory, preview, execute, errors, cancellation, and no direct Core execution. Retain Pester invalid-input, WhatIf/decline, safe endpoint, stale/replayed preview coverage for the existing public command family.

### Acceptance Criteria

- `IntegrationsTabViewModel` has no `IContainerRuntimeService` or `IPlatformCapabilityService` execution dependency.
- Its inventory and Podman actions reach only the existing exported PowerShell commands through closed typed client methods.
- No generic module-client execution API, generic Bridge route, Docker integration behavior, or Core container safety change is introduced.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~IntegrationsTabViewModel|FullyQualifiedName~PowerShellModuleClient"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted container-runtime/Podman presentation-client migration slice.

### Out of Scope

All uncontracted platform families and final Desktop-wide structural closure.

## Slice S22: WSLg discovery-grant contract and presentation-client migration

### Status

Committed

### Objective

Make the exported WSLg commands, backed by a short-lived protected discovery grant, the sole execution path for the applications presentation surface.

### Sources

Requirements FR-001 and FR-003 through FR-007; `docs/specs/powershell-first-design.md` WSLg discovery-grant contract amendment; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S21

### Allowed Paths

`src/Client/DistroNexus.Core/Models/WslgApplicationModels.cs`, `src/Client/DistroNexus.Core/Interfaces/IWslgApplicationService.cs`, `src/Client/DistroNexus.Core/Services/WslgApplicationService.cs`, a WSLg-specific Core discovery-grant store and its narrowly scoped test support, `src/Client/DistroNexus.WorkspaceBridge/Program.cs`, `src/PowerShell/Public/WslgCommands.ps1`, `src/PowerShell/DistroNexus.psd1`, `src/Client/DistroNexus.Core/Interfaces/IPowerShellModuleClient.cs`, `src/Client/DistroNexus.Core/Services/PowerShellModuleClient.cs`, `src/Client/DistroNexus.Desktop/ViewModels/ApplicationsViewModel.cs`, `src/Client/DistroNexus.Desktop/Views/ApplicationsPage.xaml`, `src/Client/DistroNexus.Desktop/App.xaml.cs`, `src/Client/DistroNexus.Tests/Architecture/S13DesktopCompositionAndLocalizationTests.cs`, focused WSLg/Core/Bridge/module-client/view-model/Pester tests, design, plan.

### Excluded Paths

Docker, containers, monitoring, USB, terminal/explorer, application update, arbitrary script or process execution, generic grant infrastructure, external WSLg UAT, release/publishing surfaces.

### Contract and Documentation

Define only `wslg.status.v1`, `wslg.discover.v1`, `wslg.launch.v1`, `wslg.reveal.v1`, and `wslg.pin.v1`. Discovery returns a sanitized `WslgDiscoveryResult` projection and a Core-issued short-lived opaque token. Actions accept only `DiscoveryToken`, `ApplicationId`, and (for pin) `Pinned`; they retain `ShouldProcess` and resolve the protected grant before Core revalidation. The direct application-object start parameter and unversioned UI routes are not used by the typed client.

### Implementation Scope

Implement the WSLg-specific protected discovery-grant store with expiry, bounded payloads, cross-process-safe access, stable redacted errors, and best-effort cleanup. Preserve Core capability gates, parser/path/icon limits, and read-before/read-after desktop-entry checks. Replace `ApplicationsViewModel` direct `IWslgApplicationService` usage with typed module-client calls, keep only visual state locally, clear a stale grant after refresh/unavailability/action failure, and remove the raw launch-command copy action.

### Test Scope

Add negative Core and Bridge tests for invalid/expired/foreign token, forged application id, changed desktop entry, unknown fields, and absence of a process action. Add public command validation and `WhatIf` tests. Add typed client command-shape/serialization tests proving no authority-bearing application fields cross the module boundary. Update view-model tests to prove module-only execution and stale-token clearing.

### Acceptance Criteria

- `ApplicationsViewModel` no longer references `IWslgApplicationService` and all WSLg business actions use closed typed module-client methods.
- No public action accepts executable, arguments, desktop-entry paths, or a caller-supplied `WslgApplication` object.
- Discovery grants survive the intended cross-process module invocation, expire authoritatively, and are revalidated before every action.
- WSLg actions preserve `ShouldProcess`, existing capability/path/parser safeguards, and stable redacted failures.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Wslg|FullyQualifiedName~ApplicationsViewModel|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted WSLg discovery-grant and presentation-client migration slice.

### Out of Scope

WSLg runtime installation/repair, Start Menu integration, remote icons, external WSLg UAT, and every other uncontracted platform family.

## Slice S23: Docker Desktop integration contract and presentation-client migration

### Status

Committed

### Objective

Make the exported Docker Desktop integration commands, backed by Core-owned atomic preview/execute semantics, the sole product path for Docker integration status and changes.

### Sources

Requirements FR-001 and FR-003 through FR-007; `docs/specs/powershell-first-design.md` Docker Desktop integration contract amendment; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S22

### Allowed Paths

`src/Client/DistroNexus.Core/Models/DockerIntegrationStatus.cs`, `src/Client/DistroNexus.Core/Interfaces/IDockerIntegrationService.cs`, `src/Client/DistroNexus.Core/Services/DockerIntegrationService.cs`, narrowly scoped Docker settings/grant store support, `src/Client/DistroNexus.WorkspaceBridge/Program.cs`, Docker public command files and `src/PowerShell/DistroNexus.psd1`, `src/Client/DistroNexus.Core/Interfaces/IPowerShellModuleClient.cs`, `src/Client/DistroNexus.Core/Services/PowerShellModuleClient.cs`, `src/Client/DistroNexus.Desktop/ViewModels/Tabs/IntegrationsTabViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/MainViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/InstanceDetailViewModel.cs`, `src/Client/DistroNexus.Desktop/ViewModels/WslInstanceViewModel.cs`, `src/Client/DistroNexus.Tests/ViewModels/MonitoringViewModelTests.cs`, `src/Client/DistroNexus.Tests/ViewModels/WslInstanceTagRoutingTests.cs`, `src/Client/DistroNexus.Tests/ViewModels/IntegrationsContainerRuntimeTests.cs`, focused Docker/Core/Bridge/module-client/view-model/Pester/architecture tests, design, plan.

### Excluded Paths

Container runtime adapters and Podman, Docker installation/restart/lifecycle, Docker CLI/context/machine/storage operations, arbitrary settings paths or environment overrides, release/publishing surfaces.

### Contract and Documentation

Define only `docker.integration.get.v1`, `docker.integration.preview-set.v1`, and `docker.integration.set.v1`. `Get-DistroNexusDockerIntegration` returns a path-free snapshot. `Get-DistroNexusDockerIntegrationPreview -Name -Enabled` returns a short-lived Core-issued preview; `Set-DistroNexusDockerIntegration -Name -Enabled -Preview` is the only execute contract and uses `ShouldProcess`. Existing Enable/Disable commands become thin compatibility facades over those contracts and no longer access settings directly.

### Implementation Scope

Implement strict Docker eligibility, settings identity/fingerprint capture, a protected durable single-use preview grant, and atomic replace of the existing selected settings file only. Preserve unrelated settings and deterministically validate/deduplicate `integratedWslDistros`; reject unprovable WSL2 status, malformed settings, state drift, and file switching. Replace all listed WPF Docker service consumption with typed module-client snapshot/preview/execute calls; WPF only confirms/renders and refreshes a successful result.

### Test Scope

Add Core/Bridge/module tests for rejected blank/reserved/WSL1/missing/malformed input, unknown fields, WhatIf, forged/expired/reused/mismatched/stale previews, and no write on rejection. Prove the writer preserves unrelated JSON, is atomic/conflict-safe, and never creates a settings file. Prove compatibility commands do not read/write settings directly. Add view-model and architecture tests proving no direct Docker service dependency and preview/decline/success behavior.

### Acceptance Criteria

- WPF Docker status and toggle flows invoke only closed typed module-client methods; no migrated view model depends on `IDockerIntegrationService`.
- Module commands are the sole public Docker integration contract; legacy commands cannot bypass preview, `ShouldProcess`, or Core atomic writing.
- Every execution is bound to a single-use durable Core-issued preview and current existing settings identity/fingerprint; DistroNexus-detected conflicts fail closed, while concurrent third-party Docker Desktop write behavior is an explicit UAT closure gate.
- Public results, errors, and UI state never disclose settings paths, raw JSON, registry values, or raw host output.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~DockerIntegration|FullyQualifiedName~IntegrationsTabViewModel|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted Docker integration contract and presentation-client migration slice.

### Out of Scope

Docker Desktop installation/restart, runtime container/image/project operations, third-party Docker Desktop concurrent-write UAT, and every remaining uncontracted platform family.

## Slice S24: Monitoring pull contract and presentation-client migration

### Status

Committed

### Objective

Make fixed module snapshot and process-action commands the sole monitoring product path, replacing WPF-owned Core sessions.

### Sources

Requirements FR-001 and FR-003 through FR-007; `docs/specs/powershell-first-design.md` Monitoring pull and process-action contract amendment; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S23

### Allowed Paths

`src/Client/DistroNexus.Core/Models/MonitoringModels.cs`, `src/Client/DistroNexus.Core/Interfaces/IMonitoringService.cs`, `src/Client/DistroNexus.Core/Services/MonitoringService.cs`, narrow monitoring automation/grant support, `src/Client/DistroNexus.WorkspaceBridge/Program.cs`, monitoring public command files and `src/PowerShell/DistroNexus.psd1`, `IPowerShellModuleClient`, `PowerShellModuleClient`, `MonitorTabViewModel`, `InstanceDetailViewModel`, `WslInstanceViewModel`, `App.xaml.cs`, focused monitoring/Core/Bridge/module-client/view-model/Pester tests, design, plan.

### Excluded Paths

Persistent bridge daemon, telemetry, WSL start/restart, arbitrary sampling scripts/signals/commands, automatic KILL escalation, system-wide process controls, USB, terminal, release/publishing.

### Contract and Documentation

Define only `monitoring.snapshot.v1`, `monitoring.process.preview.v1`, and `monitoring.process.execute.v1`. Snapshot has a fixed interval and sanitized bounded data plus an opaque grant. Process preview accepts only snapshot token/PID/allow-listed action; execute accepts only a Core preview token and retains `ShouldProcess`.

### Implementation Scope

Create bounded one-request Core session automation with durable same-user snapshot/action/TERM-eligibility grants. Preserve existing fixed probes, stopped-instance guard, PID/start-time revalidation, TERM-before-KILL flow, and bounded output. Replace direct WPF Core session ownership with visible-only module-client polling/cancellation and bounded local display state.

### Test Scope

Cover malformed/unknown payloads, fixed intervals, cancellation/disposal, stopped instances, forged/expired/replayed grants, PID drift, TERM/KILL sequencing, WhatIf/decline, public redaction/bounds, stale UI result suppression, and no direct monitoring service dependency.

### Acceptance Criteria

- MonitorTab has no `IMonitoringService`/Core session dependency and all probes/actions use typed module-client methods.
- No public endpoint accepts raw commands, signals, process objects, or unbounded probe data.
- TERM/KILL protections and PID identity revalidation survive the cross-process module boundary through durable Core grants.
- Polling is visible-only, cancellable, bounded, and never starts a stopped distribution.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Monitoring|FullyQualifiedName~MonitorTabViewModel|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted monitoring contract and presentation-client migration slice.

### Out of Scope

External monitoring UAT, long-running monitoring persistence, and every other uncontracted platform family.

## Slice S25: USB module contract, trusted elevation broker, and presentation-client migration

### Status

Blocked

### Objective

Make fixed PowerShell USB discovery and action commands the only product path while preserving the elevated helper's strict signed-caller boundary.

### Sources

Requirements FR-001 and FR-003 through FR-007; `docs/specs/powershell-first-design.md` USB action-grant and trusted-elevation-broker contract amendment; `docs/architecture/powershell-first-decision.md` USB elevation boundary amendment.

### Dependencies

S24

### Allowed Paths

`src/Client/DistroNexus.Core/Models/UsbDeviceModels.cs`, `src/Client/DistroNexus.Core/Interfaces/IUsbDeviceService.cs`, `src/Client/DistroNexus.Core/Services/UsbDeviceService.cs`, narrow USB action-grant/protection support, `src/Client/DistroNexus.WorkspaceBridge/Program.cs`, USB public command files and `src/PowerShell/DistroNexus.psd1`, `IPowerShellModuleClient`, `PowerShellModuleClient`, `UsbDevicesViewModel`, `App.xaml.cs`, `src/Client/DistroNexus.UsbElevatedHelper/Program.cs`, new `src/Client/DistroNexus.UsbElevationBroker/` project and required solution/packaging references, focused USB/Core/helper/broker/Bridge/module-client/view-model/Pester/architecture tests, design, decision, plan.

### Excluded Paths

Generic elevation APIs, accepting PowerShell/dotnet/admin/filename-only caller identity, arbitrary helper or usbipd paths/arguments, driver or usbipd installation, service/Windows-feature mutation, automatic UAC, persistent watches, WSL start, physical-device execution, release/publishing surfaces.

### Contract and Documentation

Define only `usb.status.v1`, `usb.list.v1`, `usb.action.preview.v1`, and `usb.action.execute.v1`. Preview accepts exactly Action, BusId and optional DistributionName; execute accepts only PreviewToken. Status/list and preview outputs are sanitized. Public status/list/preview/invoke commands use those routes; existing Connect/Disconnect are deprecated facades only.

### Implementation Scope

Implement a durable same-user protected, expiry-bound, atomically consumed USB action grant. Preserve Core revalidation and use a new signed `DistroNexus.UsbElevationBroker` only for Bind/Unbind so the elevated helper can retain strict product-signed caller proof. Replace direct view-model service use with typed module-client calls and non-authoritative typed refresh.

### Test Scope

Cover exact payloads, unknown fields, invalid action/distribution, WhatIf/decline, forged/expired/replayed/wrong-user grants, parallel consumption, BusId/HardwareId/state drift, UAC decline, untrusted caller/PID/nonce/proof/signature, redaction/bounds, compatibility facade no-native bypass, and module-only view-model behavior.

### Acceptance Criteria

- USB WPF has no `IUsbDeviceService` or helper authority and reaches all discovery/actions through closed typed module methods.
- Public USB actions use only the versioned preview/execute contract; no accepted input can supply a native command, path, device identity or elevation authority.
- Bind/Unbind never broaden helper trust to PowerShell/dotnet and retain signed broker, same-SID grant, pipe proof and final helper revalidation.
- Unavailable/unsupported usbipd, stale device state, bad/expired/reused grants and declined consent fail before a mutation.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Usb|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted USB module/elevation-boundary/presentation-client migration slice.

### Out of Scope

USB driver/usbipd installation, host configuration, signing deployment, real UAC/device UAT, and every remaining platform family.

### Blocker

The existing helper deliberately authorizes only the signed `DistroNexus.Desktop.exe` pipe server; Core defaults the publisher pin to empty and `tools/build.ps1` has no signed broker publishing path. A new broker cannot safely serve module Bind/Unbind until a release/security owner supplies a pinned broker signing and packaging contract and explicitly authorizes the required packaging/signing edits. Trusting unsigned broker, PowerShell, dotnet, admin membership, or filename-only identity is prohibited.

## Slice S26: Platform-capability module and presentation-client migration

### Status

Committed

### Objective

Make explicit versioned PowerShell capability snapshot commands the sole host and instance capability-query path for migrated WPF consumers.

### Sources

Requirements FR-001 and FR-004 through FR-007; `docs/specs/powershell-first-design.md` Platform-capability query contract amendment; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S24

### Allowed Paths

`src/Client/DistroNexus.WorkspaceBridge/Program.cs`, `src/PowerShell/Public/Get-DistroNexusCapability.ps1`, `src/PowerShell/DistroNexus.psd1`, `IPowerShellModuleClient`, `PowerShellModuleClient`, direct `IPlatformCapabilityService` WPF capability consumers and their constructor-composition support, focused Bridge/module-client/view-model/Pester/architecture tests, design and plan.

### Excluded Paths

`PlatformCapabilityService` probe/cache algorithms, Core capability model semantics, WSL update/repair, installation, Windows-feature mutation, arbitrary probes/scripts, terminal/explorer/update/USB, release/publishing surfaces.

### Contract and Documentation

Define only `capability.host.v1` with no payload and `capability.instance.v1` with exact `{ InstanceName }`. `Get-DistroNexusCapability` has explicit Host and Name parameter sets and invokes only v1. The typed client provides fixed host/instance methods; no generic capability name, bridge operation or payload is exposed.

### Implementation Scope

Add strict v1 routing and migrate the bounded direct WPF host/instance snapshot consumers to typed module-client calls. Preserve existing result shapes and read-only Core behavior; leave the legacy private Bridge alias only for compatibility callers not migrated in this slice.

### Test Scope

Cover no/unknown/malformed payload, invalid instance name, module parameter sets, typed command shape, legacy route isolation, module-only view-model routing, and no distribution/process/update action for rejected or read requests.

### Acceptance Criteria

- Migrated WPF capability consumers do not depend on `IPlatformCapabilityService`.
- All new public and typed calls use only explicit v1 host/instance routes.
- Capability queries remain bounded read-only snapshots and cannot become a generic host probe or update execution path.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~PlatformCapability|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol|FullyQualifiedName~ConfigurationTabViewModel|FullyQualifiedName~WslConfigSectionViewModel"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted platform-capability query and presentation-client migration slice.

### Out of Scope

Capability probe algorithm changes, platform mutation, application update, terminal launch, USB elevation, and external runtime UAT.

## Slice S27: Fixed terminal and package-cache launch module migration

### Status

Committed

### Objective

Make fixed PowerShell terminal and package-cache commands the only supported external-launch path for their migrated WPF consumers.

### Sources

Requirements FR-001 and FR-003 through FR-007; `docs/specs/powershell-first-design.md` Terminal and package-cache launch contract amendment; `docs/architecture/powershell-first-decision.md`.

### Dependencies

S26

### Allowed Paths

`src/Client/DistroNexus.Core/Interfaces/ITerminalService.cs`, `src/Client/DistroNexus.Core/Services/TerminalService.cs`, narrow terminal launch models only if needed, `src/Client/DistroNexus.WorkspaceBridge/Program.cs`, new terminal/package-cache public command files and `src/PowerShell/DistroNexus.psd1`, `IPowerShellModuleClient`, `PowerShellModuleClient`, direct terminal-service WPF consumers and constructor composition support, focused terminal/Bridge/module-client/view-model/Pester/architecture tests, design and plan.

### Excluded Paths

Arbitrary program/path/URI/argument launch, general file browsing, terminal installation, elevation, WSL command execution, application update, USB, release/publishing surfaces.

### Contract and Documentation

Define only `terminal.status.v1`, `terminal.launch.v1`, and `explorer.package-cache.v1`. Launch accepts exact InstanceName, optional Linux StartPath and allow-listed TerminalKind; cache launch has no payload. All launch cmdlets use `ShouldProcess`.

### Implementation Scope

Replace script-string launch construction and direct WPF service usage with fixed typed module contracts. Resolve only known distributions, Linux paths and the configured existing cache root; invoke fixed executables with fixed argument arrays and return redacted result records.

### Test Scope

Cover exact payloads, malformed/unknown fields, invalid names/paths/kinds, no arbitrary process arguments, WhatIf/decline, cache containment/existence, typed client command shapes and module-only view-model routing.

### Acceptance Criteria

- Migrated WPF consumers have no `ITerminalService` execution path.
- No public or Bridge input can select an executable, host path, URI, command string or raw argument.
- Rejected, WhatIf and declined requests start no process; accepted launch uses only a fixed executable/argument array.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Terminal|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted fixed terminal/package-cache module and presentation-client migration slice.

### Out of Scope

Terminal installation, arbitrary browsing/launching, application update, USB elevation and runtime UAT.

## Slice S28: Fixed WSL configuration and recovery-folder reveal migration

### Status

Committed

### Objective

Route WSL configuration and recovery-folder reveal through fixed PowerShell commands without accepting caller paths or generic launch inputs.

### Sources

Requirements FR-001 and FR-003 through FR-007; `docs/specs/powershell-first-design.md` Fixed WSL configuration and recovery-folder reveal contract amendment.

### Dependencies

S27

### Allowed Paths

WorkspaceBridge Program, narrow recovery-safe resolver support only if required, new public reveal command files and manifest, typed module-client interface/implementation, `WslConfigSectionViewModel`, `BackupTabViewModel`, focused Bridge/Core/module-client/view-model/Pester tests, design and plan.

### Excluded Paths

Generic Explorer/file/URI launcher, caller paths/arguments, old WSL config scripts, Services/Network/Resources, application update, USB, release/publishing.

### Contract and Documentation

Define only no-payload `explorer.wslconfig.v1` and exact-ID `explorer.recovery-point.v1`. Both public cmdlets use `ShouldProcess`; results contain only success/outcome code.

### Implementation Scope

Resolve the fixed current-user `.wslconfig` and existing owned recovery point only at execution time, reject unsafe/reparse/nonexistent targets, and launch only the fixed OS shell/Explorer behavior. WPF passes no path.

### Test Scope

Cover no/unknown/malformed payload, foreign/missing/unsafe recovery point, reparse/missing WSL config, WhatIf/decline, fixed launch shape and module-only view-model routing.

### Acceptance Criteria

- Neither migrated WPF view model invokes `Process.Start` or passes a filesystem path for these actions.
- No route accepts a caller-selected path, executable, URI or arguments.
- Unsafe/missing targets and declined/WhatIf requests cause no launch.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Recovery|FullyQualifiedName~WslConfig|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted fixed configuration/recovery reveal migration slice.

### Out of Scope

General file browsing, recovery mutation, WSL configuration read/write migration, services/network/resources migration, runtime UAT, deployment and publishing.

## Slice S29: Systemd durable module contract and services presentation migration

### Status

Committed

### Objective

Make the fixed systemd module contract, including durable one-shot preview grants, the only ServicesTab execution path.

### Sources

Requirements FR-001 and FR-003 through FR-007; `docs/specs/powershell-first-design.md` Systemd durable preview-grant and presentation-client contract amendment.

### Dependencies

S28

### Allowed Paths

Narrow Systemd Core service/grant support, WorkspaceBridge Program, systemd public command files, typed module client interface/implementation, `ServicesTabViewModel`, focused Systemd/Core/Bridge/module-client/view-model/Pester tests, design and plan.

### Excluded Paths

Network/Resources/Podman/container, generic process/shell APIs, Linux credential storage, arbitrary unit types/commands, elevation helpers, release/publishing.

### Contract and Documentation

Preserve only `systemd.list.v1`, `systemd.details.v1`, `systemd.journal.v1`, `systemd.preview.v1`, `systemd.execute.v1`; execute accepts exactly PreviewToken. Public mutations retain `ShouldProcess`.

### Implementation Scope

Replace in-memory cross-process previews with protected same-user expiry-bound atomic grants, then migrate ServicesTab to closed typed module calls. Core repeats current capability/precondition/postcondition safeguards.

### Test Scope

Cover fresh preview/execute service instances, forged/expired/reused/foreign/bound-mismatch grants, strict payloads, WhatIf/decline, no execute on rejection, and module-only tab list/details/journal/confirm/refresh behavior.

### Acceptance Criteria

- ServicesTab has no `ISystemdService` dependency or direct systemd execution path.
- Execute receives only an opaque Core-issued token and survives independent module processes once.
- No generic WSL/systemctl command or credential input is exposed.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Systemd|FullyQualifiedName~ServicesTabViewModel|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted durable systemd module/presentation migration slice.

### Out of Scope

Network/resource changes, systemd installation/repair, real WSL UAT, deployment and publishing.

## Slice S30: Network module and presentation-client migration

### Status

Committed

### Objective

Route NetworkTab fixed reads, probes, configuration and loopback launch through PowerShell while retaining firewall mutation as an explicit helper-integration closure item.

### Sources

Requirements FR-001 and FR-003 through FR-007; Network durable configuration and loopback-launch contract amendment.

### Dependencies

S29

### Allowed Paths

Narrow network configuration durable grant support, WorkspaceBridge Program, network/firewall public commands and manifest, typed module client, NetworkTab and constructor composition support, narrow fixed loopback helper, focused Core/Bridge/client/view-model/Pester tests, design and plan.

### Excluded Paths

Generic URL/process/network command APIs, firewall elevation/helper signing implementation, arbitrary firewall mutation, Services/Resources/USB/application update, release/publishing.

### Contract and Documentation

Use fixed existing network routes plus no-payload `network.settings.get.v1`, token-only mode/settings set routes, and exact `browser.loopback.v1 {Host,Port}`. Firewall mutations report existing unavailable state until helper closure.

### Implementation Scope

Create durable mode/settings grants, typed fixed client methods and NetworkTab migration. Replace WPF browser launching with constrained loopback route; do not change firewall elevation trust.

### Test Scope

Cover strict payloads, read/probe bounds, grants across fresh instances, expiry/replay/SID/fingerprint, WhatIf/decline, loopback allow-list/rejection and no direct WPF Core/browser execution.

### Acceptance Criteria

- NetworkTab has no direct network/configuration/firewall/browse execution path.
- Network execute consumes only Core token and fails closed on state drift.
- Loopback launch cannot become an arbitrary browser/URI route; firewall mutation remains explicitly unavailable until signed helper integration.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Network|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted network module/presentation migration slice excluding firewall helper implementation.

### Out of Scope

Firewall helper signing/elevation integration, actual firewall mutation UAT, arbitrary browser launch, and every other remaining product family.

## Slice S31: Instance resources and sparse-mode module migration

### Status

Committed

### Objective

Make typed resource snapshot and durable sparse-mode preview/execute commands the only ResourcesTab product path.

### Sources

Requirements FR-001 and FR-003 through FR-007; Instance resources and sparse-mode contract amendment.

### Dependencies

S30

### Allowed Paths

Narrow resource/sparse Core contracts, registered-instance sparse adapter and grant support, WorkspaceBridge Program, resource public commands/manifest, typed module client, ResourcesTab and composition support, focused Core/Bridge/client/view-model/Pester tests, design and plan.

### Excluded Paths

Disk compaction/VHDX/diskpart/Hyper-V/elevation, raw registry/config-path exposure, generic WSL commands, DiskTab, release/publishing.

### Contract and Documentation

Define `instance.resources.get.v1`, `instance.sparse.preview.v1`, `instance.sparse.execute.v1`; execute accepts exactly PreviewToken. Public mutation uses `ShouldProcess`.

### Implementation Scope

Replace direct untyped config/state calls with sanitized snapshot and durable same-user sparse grants; ResourcesTab confirms and refreshes via typed module client.

### Test Scope

Cover strict payload/name validation, fresh-service grants, forged/expired/replay/SID/identity/state mismatch/parallel/cleanup, WhatIf/decline and module-only tab behavior.

### Acceptance Criteria

- ResourcesTab has no direct WSL manager/configuration dependency.
- Sparse execute accepts only a Core-issued token and rechecks WSL2/current state.
- No raw config/registry/VHDX/process authority crosses the public boundary.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Resource|FullyQualifiedName~Sparse|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted resources/sparse module migration slice.

### Out of Scope

Disk compaction and elevation design, VHDX runtime UAT, and every other remaining capability family.

## Slice S32: Health and diagnostics module migration

### Status

Committed

### Objective

Make typed health/diagnostic module contracts the only HealthCenter product path while preserving DesktopOnly repair outcomes.

### Sources

Requirements FR-001 and FR-003 through FR-007; Health repair and diagnostics durable contract amendment.

### Dependencies

S31

### Allowed Paths

Narrow health repair/diagnostic durable support, WorkspaceBridge Program, health/diagnostic public command files, typed module client, HealthCenterViewModel, focused Core/Bridge/client/view-model/Pester tests, design and plan.

### Excluded Paths

Generic destination/path/shell APIs, Windows feature/UAC/navigation broker changes, app update, unrelated repairs, release/publishing.

### Contract and Documentation

Health repair execute accepts only PreviewToken; diagnostics export accepts only SnapshotToken, basename and bounded deadline. Add read-only log-options route.

### Implementation Scope

Durably protect repair previews and redacted diagnostic snapshots across fresh module processes, migrate typed client and HealthCenter; retain DesktopOnly structured outcomes.

### Test Scope

Cover fresh-instance grants, expiry/replay/SID/canonical mismatch/parallel/cleanup, strict payload/redaction/path rejection, fixed-directory basename export, WhatIf/decline and module-only UI behavior.

### Acceptance Criteria

- HealthCenter has no direct health/repair/report/log provider execution path.
- No action accepts caller repair/finding/content/full path; desktop-only repairs do not bypass authorization.
- Diagnostic export is redacted, one-shot and fixed-directory only.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Health|FullyQualifiedName~Diagnostic|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted health/diagnostics module migration slice.

### Out of Scope

UAC/Windows feature/navigation broker, app update, arbitrary report destinations and runtime UAT.

## Slice S33: Global WSL configuration module migration

### Status

Committed

### Objective

Make the complete supported global `.wslconfig` read, reviewed preview, and reviewed save contract available through the module and the only product path used by the desktop configuration editor.

### Sources

Requirements FR-001, FR-003 through FR-007, and FR-004A; Global WSL configuration durable preview contract amendment; Global WSL configuration boundary amendment; `WslConfigSectionViewModel`, `WslConfigService`, and legacy WSL configuration command evidence.

### Dependencies

S32

### Allowed Paths

`docs/specs/powershell-first-requirements.md`, `docs/specs/powershell-first-design.md`, `docs/architecture/powershell-first-decision.md`, this plan; narrow global configuration models/services/grant support; WorkspaceBridge Program; global WSL configuration public commands and manifest; typed module client; `WslConfigSectionViewModel`, `SettingsViewModel` constructor composition; focused Core/Bridge/client/view-model/Pester tests.

### Excluded Paths

Distribution configuration and `ConfigurationTabViewModel`, NetworkTab/network settings, arbitrary INI/file/path APIs, Explorer reveal behavior, WSL shutdown/restart execution, disk/Hyper-V/elevation, catalog/install/wizard, release/publishing, and real-host mutation.

### Contract and Documentation

Define `configuration.global.get.v1`, `configuration.global.preview.v1`, and `configuration.global.execute.v1`; preview accepts only a strictly allow-listed change map and execute accepts only PreviewToken. Retain legacy five-field cmdlets as constrained facades and document the no-path/no-raw-document result boundary.

### Implementation Scope

Use a same-user protected, expiry-bound, atomically single-use global configuration preview grant. Preserve Core lossless save, backup and optimistic conflict semantics while exposing only modeled values, bounded display preview and sanitized results. Replace direct WPF global configuration/host-spec reads, preview and save calls with typed module-client methods.

### Test Scope

Cover strict route payloads, all modeled schema/constraint/capability validation, legacy facade compatibility, `WhatIf`/decline zero grant/write, fresh-service grants, expiry/corruption/SID/replay/fingerprint-capability drift/parallel consumption/cleanup, stable errors/redaction, typed-client shapes, view-model routing and no direct global configuration services.

### Acceptance Criteria

- The module exposes complete modeled global configuration get/preview/execute operations and the legacy five-field commands no longer do direct file I/O.
- Execute consumes only a Core-issued one-shot token and preserves conflict/capability/fidelity safeguards.
- `WslConfigSectionViewModel` and its Settings composition contain no direct `IWslConfigService` or `IWslConfigurationService` product operation path.
- No public/Desktop input can provide a raw document, fingerprint, host path, arbitrary section/key, backup path or restart command.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~WslConfig|FullyQualifiedName~GlobalConfiguration|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted global WSL configuration module and presentation-client migration slice.

### Out of Scope

Real WSL restart/UAT, per-distribution configuration, arbitrary configuration documents, and all other remaining product families.

## Slice S34: Backup and recovery module migration

### Status

Committed

### Objective

Make module contracts the only product path for backup schedules, reviewed backup/recovery actions, history and pending backup notifications.

### Sources

Requirements FR-001, FR-003 through FR-007 and FR-004B; Backup and recovery durable execution contract amendment; backup/recovery evidence inventory.

### Dependencies

S33

### Allowed Paths

Requirements/design/plan; narrow backup/recovery/pending-notification Core contracts and durable grants; WorkspaceBridge; backup/recovery public commands and manifest; typed module client; `MainViewModel`, `WslInstanceViewModel`, `InstanceDetailViewModel`, `BackupTabViewModel`; focused tests.

### Excluded Paths

Instance lifecycle/import/export, disk/USB/network/templates/catalog/workspaces, arbitrary file/path APIs, Explorer reveal behavior, release/publishing and runtime backup/restore/WSL mutation.

### Contract and Documentation

Define fixed read/preview/execute routes for schedules, manual backup, recovery lifecycle/retention and notification consume. Execute accepts only PreviewToken; public results are path-free.

### Implementation Scope

Replace in-memory recovery previews with durable protected grants, preserve Core path/reservation/journal safeguards, constrain archive retention to Core-owned instance archives, and remove direct WPF service/state-file access.

### Test Scope

Cover strict payloads, consent, fresh-process grants, tamper/SID/expiry/replay/drift/parallel/capacity, path ownership and cross-instance retention safety, typed-client and WPF routing, notification single consume and sanitized failures.

### Acceptance Criteria

- Backup/recovery Desktop consumers contain no direct `IBackupService`, `IRecoveryPointService` or business-state-file operations.
- Every mutation consumes only a persistent Core-issued token and preserves Core reservation/journal/path checks.
- Execute/results never accept or expose archive/recovery/state paths; preview may accept only a bounded destination path through the module, with Core as its sole validation and filesystem authority. No caller can cause cross-instance archive deletion.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Backup|FullyQualifiedName~Recovery|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted backup and recovery module/presentation-client migration slice.

### Out of Scope

Real backup/restore/clone UAT and all non-backup lifecycle capabilities.

## Slice S35: Instance read, refresh, start, and stop module migration

### Status

Committed

### Objective

Make supported instance list/refresh and start/stop paths module-owned, with their WPF consumers using only typed module-client methods.

### Sources

Requirements FR-001 through FR-007 and FR-004C; `docs/specs/powershell-first-design.md` Instance lifecycle and disk contract amendment; lifecycle evidence inventory.

### Dependencies

S34

### Allowed Paths

Requirements/design/plan; instance list/start/stop Core contracts and fixed Bridge routes; list/start/stop public module commands; typed module client; `MainViewModel`, `WslInstanceViewModel` and composition consumers; focused tests.

### Excluded Paths

Path-bearing lifecycle operations (install/remove/move/rename/import/export/credential), disk-size/compaction, templates, marketplace, workspaces, USB/elevation, download-task orchestration, release/publishing, arbitrary command/process launch, and real WSL/VHDX mutation.

### Contract and Documentation

Define non-mutating force-refresh and explicit keep-alive start semantics. Record external lifecycle UAT gates.

### Implementation Scope

Preserve Core instance validation behind fixed versioned list/start/stop routes. Remove direct Desktop execution for only the covered read/start/stop operations. No generic WSL command, arbitrary path, process argument, or elevation bypass is introduced.

### Test Scope

Cover exact route payloads; invalid/unknown field rejection; `WhatIf` and declined confirmation; stopped-instance refresh; keep-alive result handling; typed client and WPF routing guards.

### Acceptance Criteria

- Covered read/start/stop operations in Desktop invoke only typed PowerShell module methods.
- Force refresh is read-only and never starts a stopped instance or triggers an implicit disk probe.
- Start and stop mutations honor PowerShell consent and return only sanitized results.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Lifecycle|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted instance read/start/stop contract and presentation-client migration slice.

### Out of Scope

Path-bearing lifecycle and disk operations, real WSL lifecycle, keep-alive persistence and rollback UAT; all non-lifecycle capability families.

## Slice S36: Path-bearing lifecycle and disk contract design

### Status

Committed

### Objective

Define and deliver module-only contracts for install, remove, move, rename, import, export, credential, disk-size and compaction without allowing a WPF/Core bypass.

### Sources

Requirements FR-001 through FR-007, FR-004C and FR-004D; lifecycle evidence inventory.

### Dependencies

S35

### Allowed Paths

Requirements/design/plan; lifecycle/disk Core, Bridge, module, typed-client and Desktop consumers; focused tests.

### Excluded Paths

Templates, marketplace, workspaces, USB/elevation, release/publishing and real WSL/VHDX mutation.

### Contract and Documentation

Before delegation, define exact schemas, Core-only path policy, credential transport, preview/grant behavior, recovery/rollback outcomes, redaction, and the install/download boundary.

### Implementation Scope

No implementation starts until the design gate closes every path-bearing and destructive-operation contract.

### Test Scope

Contract-first negative, consent, token, path-safety, rollback and routing coverage.

### Acceptance Criteria

- The design identifies a module-only contract for every remaining FR-004C operation.
- No route accepts arbitrary commands, uncontrolled paths, credentials in public results, or elevation bypasses.

### Verification Commands

```text
.\.agents\skills\agentteam-requirements-design\scripts\validate-design-readiness.ps1 -RequirementsPath docs\specs\powershell-first-requirements.md -DesignPath docs\specs\powershell-first-design.md
.\.agents\skills\agentteam-slice-delivery\scripts\validate-slice-plan.ps1 -Path docs\development\powershell-first-slice-plan.md
```

### Commit Boundary

One accepted design/contract baseline followed by separately accepted implementation slices.

### Out of Scope

Production WSL/VHDX/elevation UAT and all other capability families.

## Slice S37: Path-bearing lifecycle route foundation

### Status

Committed

### Objective

Provide reviewed fixed module routes for remove, move, rename, export and import, with Core-owned local-path validation, durable grants and recovery reporting.

### Sources

Requirements FR-001 through FR-007, FR-004C and FR-004D; path-bearing lifecycle contract amendment.

### Dependencies

S36

### Allowed Paths

Lifecycle Core/Bridge/module/client and `MainViewModel`/`WslInstanceViewModel` consumers; requirements/design/plan; focused tests.

### Excluded Paths

Package acquisition/install, credentials, compaction, templates, workspaces, USB/elevation, release/publishing and real WSL mutation.

### Contract and Documentation

Implement exact preview/execute payloads, local-root path resolver, grant/fingerprint rules, outcome/recovery codes and compatibility facades.

### Implementation Scope

Remove raw-Core/Desktop fallback execution and duplicate Desktop metadata cleanup for these operations. No arbitrary paths at execute, shell text, commands or privilege elevation.

### Test Scope

Path grammar/canonicalization/reparse/collision/target tests; grant SID/expiry/replay/concurrency; transition and rollback outcomes; PowerShell consent; typed-client and WPF routing guards.

### Acceptance Criteria

- Each covered mutation executes only a Core-issued opaque token.
- A rejected/failing module or Bridge request cannot trigger raw WSL fallback.
- Desktop neither mutates product paths nor duplicates lifecycle cleanup.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Lifecycle|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted path-bearing lifecycle foundation and presentation-client migration slice.

### Out of Scope

Install acquisition, credentials, compaction and production WSL UAT.

## Slice S38: Verified install and credential migration

### Status

Completed

### Objective

Make package acquisition, installation and credential configuration module-only, with verified artifacts and secret-safe transport.

### Sources

Requirements FR-001 through FR-007, FR-004C and FR-004D; path-bearing lifecycle contract amendment.

### Dependencies

S36, S37

### Allowed Paths

Package/install/credential Core/Bridge/module/client; install workflow and legacy wizard consumers; requirements/design/plan; focused tests.

### Excluded Paths

Compaction, templates, workspaces, USB/elevation, release/publishing and live download/WSL mutation.

### Contract and Documentation

Implement verified acquisition reference, install preview/execute, direct secure parameter binding, secret redaction and rollback/cancellation outcomes.

### Implementation Scope

Split package acquisition from install; remove plaintext secrets from Desktop/Core durable state and generated scripts; migrate both wizard paths.

### Test Scope

Verified/missing/corrupt source, consent/cancellation, local-root checks, secret redaction/metacharacters, rollback and WPF no-secret/no-service structural checks.

### Acceptance Criteria

- Install never downloads implicitly and only executes a verified artifact reference.
- Secrets never enter generated command text, logs, results or persistent wizard/Core state.
- Both active and legacy install paths use typed module contracts.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Install|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted verified-install and credential-safety migration slice.

### Out of Scope

Compaction and production package/WSL UAT.

## Slice S39: Compaction and disk presentation migration

### Status

Completed

### Objective

Route disk-size and compaction presentation through a reviewed module preview/execute contract without misleading savings estimates.

### Sources

Requirements FR-001 through FR-007, FR-004C and FR-004D; path-bearing lifecycle contract amendment.

### Dependencies

S36, S37

### Allowed Paths

Compaction Core/Bridge/module/client, `DiskTabViewModel` and related composition/tests; requirements/design/plan.

### Excluded Paths

Package/install/credential, templates, workspaces, USB/elevation, release/publishing and real VHDX mutation.

### Contract and Documentation

Implement read-only preview, same-user single-use execute grant, truthful estimate kind, fixed method/privilege outcomes and restart recovery result.

### Implementation Scope

Remove Core compaction calls and UI-side what-if/confirmation authority. No caller path, method, elevation flag, script or arbitrary process input.

### Test Scope

Preview no-side-effect, token/fingerprint/replay/expiry, privilege/state drift, result parsing and DiskTab routing/estimate-display tests.

### Acceptance Criteria

- Preview never represents current size as reclaimable size.
- Execute accepts only a token and preserves prior running state or reports recovery outcome.
- DiskTab has no direct lifecycle/disk execution path.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Compact|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted compaction module/presentation-client migration slice.

### Out of Scope

Production VHDX/privilege UAT and all unrelated lifecycle capabilities.

## Slice S06: Platform-integrated command parity

### Status

Planned

### Objective

Platform-integrated command parity. S21 supersedes the already-coded container/PODMAN presentation-client portion; the remaining families require their own contract amendments before implementation.

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

## Slice S40: Workspace module-client migration

### Status

Committed

### Objective

Make the PowerShell module and typed module client the sole product route for workspace list, editing, import/export, trust, launch, retry and close operations.

### Sources

`docs/specs/workspace-module-requirements.md` FR-001 through FR-004; `docs/specs/workspace-module-design.md`; existing workspace service and bridge evidence.

### Dependencies

S39

### Allowed Paths

Workspace Core contracts/services/grant-operation stores and tests; packaged `DistroNexus.WorkspaceWorker` project/composition/tests; WorkspaceBridge protocol/handlers/tests and package copy configuration; workspace public module commands and Pester tests; `IPowerShellModuleClient` and implementation/tests; `WorkspacesViewModel` and focused view-model tests; workspace requirements/design/plan.

### Excluded Paths

Shortcut-writing implementation, file picker UX, templates/marketplace, USB/elevation, unrelated lifecycle/disk behavior, release/publishing workflows and real WSL mutation.

### Contract and Documentation

Use fixed typed list, preview and opaque-token execute contracts. Move workspace import/export document content across modeled requests/results; neither WPF nor public module commands own workspace product files. Launch/retry execute starts only the fixed packaged, same-user authenticated WorkspaceWorker and uses durable operation status/cancel records.

### Implementation Scope

Replace all `IWorkspaceService` product calls in `WorkspacesViewModel` with typed module-client calls. Preserve Core trust/revision/grant semantics and ShouldProcess. Do not introduce generic bridge dispatch, arbitrary host paths or arbitrary command/process execution.

### Test Scope

Cover closed bridge payloads, content validation, WhatIf/declined confirmation, revision/token expiry/replay/state drift, launch/retry/close progress/cancellation, typed-client parsing and WPF structural/routing behavior.

### Acceptance Criteria

- `WorkspacesViewModel` has no direct `IWorkspaceService` or workspace product-state file operation.
- Module import/export uses fixed typed content contracts rather than public-command file I/O.
- All mutations execute only with a fresh Core-issued token and expected revision.
- Targeted xUnit, Pester unit tests and Debug build pass; real workspace action execution is recorded as external UAT.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Workspace|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted workspace module/client and presentation migration slice.

### Out of Scope

Live WSL workspace execution, shortcut/file-picker implementation and all unrelated capability families.

## Slice S41a: Retire unsafe template executor

### Status

Committed

### Objective

Remove the public mutable-template executor before any module replacement exists, so no product path can directly run caller-provided template script material.

### Sources

`docs/specs/template-module-requirements.md` FR-001 through FR-004; `docs/specs/template-module-design.md`; `docs/architecture/template-apply-recovery-decision.md`; `docs/contracts/template-module-v1-contract.md`.

### Dependencies

S40

### Allowed Paths

`src/PowerShell/DistroNexus.psd1`, retired template executor/automation public scripts, and focused Pester unit/integration tests.

### Excluded Paths

New template content, generic scripting/process execution, USB/elevation, unrelated lifecycle/workspace behavior, release/publishing workflows and live WSL mutation.

### Contract and Documentation

`Apply-DistroNexusTemplate` is removed from the manifest rather than retained with a mutable compatibility shape; non-dry-run automation fails closed pending the reviewed v1 route.

### Implementation Scope

Delete the unsafe public implementation/export and replace obsolete tests with retirement/fail-closed assertions.

### Test Scope

Assert the command is absent from the manifest/module and that automation cannot fall back to direct execution.

### Acceptance Criteria

- No public PowerShell path invokes mutable template script content.
- Pester Unit/Integration and Debug build pass.

### Verification Commands

```text
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Integration
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One accepted safety-retirement slice.

### Out of Scope

Live template execution/recovery UAT, publishing and unrelated capability families.

## Slice S41b: Template catalog and marketplace typed module boundary

### Status

Committed

### Objective

Expose catalog, compatibility, marketplace source/discovery/review/history and local template content operations through fixed typed v1 module/Bridge/client contracts, including durable review grants.

### Dependencies

S41a

### Allowed Paths

Template marketplace Core services/models/stores/tests; WorkspaceBridge template catalog/marketplace routes/tests; PowerShell template catalog/marketplace commands/manifest/Pester tests; `IPowerShellModuleClient` and implementation/tests; contract/design/plan only if needed for accepted rework.

### Excluded Paths

Template apply grant/operation/worker/runtime, Desktop consumers, template content, USB/elevation, unrelated lifecycle/workspace and live WSL mutation.

### Acceptance Criteria

- Every listed catalog/marketplace public command uses only fixed v1 typed routes.
- Marketplace review-to-approve succeeds across a fresh Bridge process with DPAPI/SID/expiry/replay protection.
- Targeted xUnit/Pester and Debug build pass.

## Slice S41c: Reviewed template application worker and runtime

### Status

Committed

### Dependencies

S41b

### Objective

Implement tokenized template preview/execute/status/cancel, durable grants/operations, fixed packaged worker and grant-bound execution runtime.

### Allowed Paths

Template apply Core contracts/stores/runtime/tests; TemplateWorker/packaging; WorkspaceBridge apply routes/tests; apply public commands/manifest/Pester; typed client/tests; design/contract/plan only if needed for accepted rework.

### Excluded Paths

Desktop consumers, marketplace catalog/source behavior except worker composition, template content, unrelated runtime paths and live WSL mutation.

### Acceptance Criteria

- Only a Core-issued token starts a same-SID durable operation.
- Fixed worker/runtime obeys provenance, pending-script, cancellation and interruption contracts.
- Targeted xUnit/Pester and Debug build pass.

## Slice S41d: Template presentation migration and closure

### Status

Committed

### Dependencies

S41b, S41c

### Objective

Move template page and wizard consumers to typed module client operations and close template structural/conformance gaps.

### Allowed Paths

`src/Client/DistroNexus.Desktop` template page/wizard consumers and their tests; typed client surface/tests; structural tests; plan and release evidence.

### Excluded Paths

Core execution semantics, generic scripting, template content, USB/elevation, release/publishing and live WSL mutation.

### Acceptance Criteria

- Desktop template consumers have no direct `ITemplateService` or `ITemplateMarketplaceService` dependency.
- WPF only requests typed operations, displays results and gathers consent.
- Wizard option selection uses the bounded `template.catalog.options.v1` display schema; it does not deserialize template content or obtain execution authority.
- Targeted xUnit/Pester/Debug build pass; disposable WSL UAT remains recorded externally.

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

## Slice S42: Bootstrap, settings, and host-status presentation boundary

### Status

Committed

### Objective

Desktop startup, global settings, compliance state, and update status use only fixed typed PowerShell module operations.

### Sources

`docs/specs/powershell-first-remaining-boundaries-requirements.md` FR-101 and FR-107; `docs/specs/powershell-first-remaining-boundaries-design.md` Bootstrap/settings/update; boundary inventory evidence.

### Dependencies

S41d.

### Allowed Paths

Bootstrap/settings/update Core/Bridge/module/client contracts and tests; `App.xaml.cs`, settings/wizard consumers and focused Desktop tests; remaining-boundaries requirements/design and this plan.

### Excluded Paths

Package jobs, USB, instance configuration, install path preflight, diagnostics, signing/release/publishing, and live host mutation.

### Contract and Documentation

Add closed bootstrap/settings/compliance/update records and document the immutable module-location bootstrap rule.

### Implementation Scope

Replace direct Desktop settings/compliance/update execution. No generic module location override, raw settings parsing, arbitrary URL, or update download mutation.

### Test Scope

Bootstrap failure, typed mappings, settings/update status rendering, URL validation, and structural dependency rejection.

### Acceptance Criteria

- Desktop performs no direct settings/compliance/update product read or write.
- Module bootstrap uses immutable product composition only, then obtains settings through the module.
- Update browser launch consumes only a module-returned validated HTTPS URI.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Settings|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~Architecture"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One coherent bootstrap/settings/update contract and named Desktop consumer migration.

### Out of Scope

Download-job actions, USB, configuration, install, diagnostics, and live update UAT.

## Slice S43: Package download-job module boundary

### Status

Committed

### Objective

Package/download task state and mutations use reviewed typed module operations, not Desktop task handlers.

### Sources

Remaining-boundaries requirements FR-102 and design Package/download jobs.

### Dependencies

S42.

### Allowed Paths

`docs/specs/powershell-first-remaining-boundaries-requirements.md`, `docs/specs/powershell-first-remaining-boundaries-design.md`, this plan; package-job Core models/interfaces/services and their focused tests; `src/Client/DistroNexus.WorkspaceBridge/Program.cs`; `src/Client/DistroNexus.Core/Interfaces/ICatalogService.cs`; `src/Client/DistroNexus.Core/Interfaces/IPowerShellModuleClient.cs`; `src/Client/DistroNexus.Core/Services/PowerShellModuleClient.cs`; `src/PowerShell/DistroNexus.psd1`; package-job public module commands and Pester tests; `src/Client/DistroNexus.Desktop/App.xaml.cs`; `src/Client/DistroNexus.Desktop/Converters/DownloadConverters.cs`; `src/Client/DistroNexus.Desktop/ViewModels/PackageManagerViewModel.cs`; `src/Client/DistroNexus.Desktop/ViewModels/MainViewModel.cs`; `src/Client/DistroNexus.Desktop/MainWindow.xaml`; `src/Client/DistroNexus.Desktop/MainWindow.xaml.cs`; `src/Client/DistroNexus.Desktop/Views/PackageManagerPage.xaml`; focused xUnit converter/view-model/protocol/client tests.

### Excluded Paths

USB, instance configuration, install target preflight, diagnostics, release/publishing, and live download mutation.

### Contract and Documentation

Define bounded read results and token-only job mutation contracts; document cancellation and polling behavior.

### Implementation Scope

Remove Desktop download task ownership. No arbitrary URLs, paths, command text, task delegates, or generic process controls.

### Test Scope

Job list/progress bounds, token expiry/replay/state drift, cancellation, retry, clear, and view-model routing.

### Acceptance Criteria

- Desktop owns no product download task state or task handlers.
- Every job mutation consumes an opaque reviewed token and results are bounded.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Package|FullyQualifiedName~Download|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One complete package-download contract and PackageManager migration.

### Out of Scope

Actual network download UAT, USB, configuration, install, and diagnostics.

## Slice S44: Instance configuration and install-target presentation migration

### Status

Committed

### Objective

Per-instance configuration and install-root preflight are module/Core-owned, tokenized capability contracts.

### Sources

Remaining-boundaries requirements FR-104, FR-105, and FR-107; design Instance configuration and Install target/diagnostics.

### Dependencies

S42.

### Allowed Paths

`docs/specs/powershell-first-remaining-boundaries-requirements.md`, `docs/specs/powershell-first-remaining-boundaries-design.md`, this plan; `src/Client/DistroNexus.Core/Interfaces/IWslConfigurationService.cs`; configuration/install Core models, grant services, `DistributionConfigurationService.cs`, `VerifiedInstallModels.cs`, `VerifiedInstallService.cs`, `IPowerShellModuleClient.cs`, `PowerShellModuleClient.cs`, and focused tests; `src/Client/DistroNexus.WorkspaceBridge/Program.cs`; `src/PowerShell/DistroNexus.psd1`, instance-configuration/install-target public module commands, `Install-DistroNexusInstance.ps1`, and focused Pester tests; `src/Client/DistroNexus.Desktop/App.xaml.cs`, `WslInstanceViewModel.cs`, `InstanceDetailViewModel.cs`, `ViewModels/Tabs/ConfigurationTabViewModel.cs`, `InstallWizardViewModel.cs`, `Wizard/Steps/InstallPathStep.cs`, `Wizard/Steps/ProgressStep.cs`, related wizard context, and focused xUnit tests.

### Excluded Paths

Package jobs, USB, diagnostics, release/publishing, and live WSL mutation.

### Contract and Documentation

Define fixed configuration read/recovery/preview/execute and authoritative install target/preflight result schemas.

### Implementation Scope

Move configuration and target validation behind tokenized Core-backed routes. No raw `wsl.conf`, Desktop directory creation, or caller-provided execute path.

### Test Scope

Payload rejection, grant expiry/replay/SID/state drift, path/capacity negatives, `WhatIf`, and presentation routing.

### Acceptance Criteria

- Desktop has no `IDistributionConfigurationService` dependency or product-path write preflight.
- Configuration save and install eligibility use tokenized, Core-revalidated typed operations.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Configuration|FullyQualifiedName~Install|FullyQualifiedName~PowerShellModuleClient|FullyQualifiedName~WorkspaceBridgeProtocol"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

One configuration/install-target contract and all named presentation migration.

### Out of Scope

USB, package job state, diagnostics, and real WSL mutation.

## Slice S45: USB read-only migration and secured-action closure

### Status

Blocked

### Objective

Move USB discovery to typed module reads and close action migration only with the accepted broker signing boundary.

### Sources

Remaining-boundaries requirements FR-103 and FR-107; design USB; S25 and the PowerShell-first decision USB amendment.

### Dependencies

S42.

### Allowed Paths

USB read contract/module/client/Desktop consumers/tests and requirements/design/plan only.

### Excluded Paths

Unsigned broker or helper trust changes, generic elevation, signing/release/publishing, and physical-device actions.

### Contract and Documentation

Use only fixed USB status/list/preview/execute records and retain the trusted broker decision.

### Implementation Scope

Migrate only broker-free read/discovery when proven safe; action execution remains blocked by the signed-broker contract.

### Test Scope

Bounded status/list mapping, watcher removal, payload rejection, and structural dependency checks.

### Acceptance Criteria

- Read-only USB presentation has no direct Core service/watcher path.
- No action path weakens broker identity or elevation authorization.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Usb|FullyQualifiedName~Architecture|FullyQualifiedName~PowerShellModuleClient"
pwsh -NoProfile -File tests/PowerShell/TestRunner.ps1 -TestType Unit
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

Read-only USB migration only, if unblocked by independent contract acceptance.

### Out of Scope

Bind/unbind implementation, UAC, device mutation, signing, and packaging.

### Blocker

The authorized signed broker packaging/publisher pin required for bind/unbind is absent. Read-only discovery may be split into a ready sub-slice only after its independent contract review proves it does not weaken the action boundary.

## Slice S46: Diagnostics replacement and final Desktop boundary enforcement

### Status

Planned

### Objective

Replace raw diagnostic execution, remove stale Core registrations/dependencies, and enforce the whole-Desktop presentation-only rule.

### Sources

Remaining-boundaries requirements FR-106 and FR-107; design Diagnostics and Verification Strategy.

### Dependencies

S43, S44, S45.

### Allowed Paths

Typed diagnostic contract/client/tests; Desktop composition/view-model cleanup; architecture/inventory tests; requirements/design/plan and release evidence.

### Excluded Paths

USB action implementation without its broker authorization, release/publishing, and live host mutation.

### Contract and Documentation

Define or select a bounded diagnostic snapshot/report contract and publish the UI-only exception inventory.

### Implementation Scope

Remove raw diagnostic execution, stale DI registrations, and forbidden Desktop dependencies. No generic diagnostic scripting.

### Test Scope

Typed diagnostic mapping/redaction/cancellation, full structural forbidden-reference scan, and composition tests.

### Acceptance Criteria

- No Desktop direct Core business service, product-state host-I/O, raw process execution, or `IPowerShellService` diagnostic path remains outside documented UI-only exceptions.
- Architecture tests enumerate and enforce every exception.

### Verification Commands

```text
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Architecture|FullyQualifiedName~Diagnostic|FullyQualifiedName~ViewModel"
dotnet build src/Client/DistroNexus.slnx -c Debug
```

### Commit Boundary

Diagnostic replacement plus final composition and structural enforcement.

### Out of Scope

USB action migration without authorization, publishing, and live host UAT.
