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

S01 -> S02 -> S03 -> S04 -> S09 -> S10 -> S11 -> S12 -> S13 -> S14 -> S15 -> S16 -> S17 -> S05 -> S06 -> S07 -> S08

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

Planned

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
