# Template System Implementation Checklist

Date: 2026-02-15  
Source Guide: `docs/development/template-system-comprehensive-guide.md`

## 0) Preparation
- [x] Confirm source documents are available and unchanged:
  - [x] `docs/development/template-system-comprehensive-guide.md`
  - [x] `docs/specs/template-system-requirements.md`
  - [x] `docs/specs/built-in-template-expansion-requirements.md`
  - [x] `docs/specs/built-in-template-automation-test-suite-requirements.md`
- [x] Confirm implementation scope (template system only, no unrelated UI redesign).
- [x] Confirm target environment assumptions (Windows + WSL2 local validation).

## 1) Metadata and Catalog Management
Files:
- `config/templates.json`
- `config/templates/*`

Tasks:
- [x] Verify each built-in template has required base metadata:
  - [x] `Id`
  - [x] `Name`
  - [x] `Category`
  - [x] `Description`
  - [x] `CompatibleDistros`
  - [x] `Scripts`
- [x] Verify extended metadata consistency where applicable:
  - [x] `InstallMode`
  - [x] `VersionOptions`
  - [x] `DefaultSelections`
  - [x] `PreflightChecks`
  - [x] `OutputArtifacts`
  - [x] `ScenarioTags`
- [x] Ensure script assets exist for each `ScriptPath` referenced in metadata.
- [x] Ensure script paths are relative and constrained under `config/templates/`.

Definition of Done:
- [x] Catalog metadata is complete, valid, and script paths resolve correctly.

## 2) Core Model and Service Contract
Files:
- `src/Client/DistroNexus.Core/Models/Template.cs`
- `src/Client/DistroNexus.Core/Interfaces/ITemplateService.cs`

Tasks:
- [x] Confirm model coverage for metadata schema:
  - [x] Version options model
  - [x] Preflight checks model
  - [x] Output artifact model
  - [x] Install mode enum
- [x] Confirm service interface includes:
  - [x] Catalog load/search/get
  - [x] Apply/validate/compatibility checks
  - [x] Import/export custom templates
  - [x] Application history retrieval

Definition of Done:
- [x] Service contract fully represents template lifecycle operations.

## 3) Core Execution Implementation
Files:
- `src/Client/DistroNexus.Core/Services/TemplateService.cs`

Tasks:
- [x] Confirm catalog loading strategy (AppData cache + local fallback).
- [x] Confirm effective variable merge order:
  - [x] `Variables`
  - [x] `DefaultSelections`
  - [x] `VersionOptions.DefaultValue`
  - [x] Runtime overrides
- [x] Confirm preflight check flow:
  - [x] Required checks fail-fast
  - [x] Optional checks warn and continue
- [x] Confirm ordered script execution and timeout handling.
- [x] Confirm `ContinueOnError` behavior works as intended.
- [x] Confirm progress reporting via `TemplateProgress`.
- [x] Confirm history persistence to `%APPDATA%\DistroNexus\template-application-history.json`.
- [x] Confirm path safety protections:
  - [x] Absolute path rejected
  - [x] Path traversal blocked
  - [x] Allowed root validation enforced

Definition of Done:
- [x] Template apply pipeline is safe, observable, and deterministic.

## 4) Desktop Workflow Integration
Files:
- `src/Client/DistroNexus.Desktop/App.xaml.cs`
- `src/Client/DistroNexus.Desktop/Wizard/InstallWizardWorkflowViewModel.cs`
- `src/Client/DistroNexus.Desktop/Wizard/Steps/SelectTemplateStep.cs`
- `src/Client/DistroNexus.Desktop/Wizard/Steps/TemplateApplyStep.cs`

Tasks:
- [x] Confirm `ITemplateService` DI registration exists.
- [x] Confirm wizard includes template selection and apply steps in workflow.
- [x] Confirm selected template and variables pass into apply phase correctly.
- [x] Confirm template output/progress is visible in apply step UX.
- [x] Confirm cancel/error paths behave gracefully.

Definition of Done:
- [x] End users can select and apply templates inside install wizard reliably.

## 5) PowerShell Command Surface
Files:
- `src/PowerShell/DistroNexus.psd1`
- `src/PowerShell/Public/Get-DistroNexusTemplate.ps1`
- `src/PowerShell/Public/Apply-DistroNexusTemplate.ps1`
- `src/PowerShell/Public/Invoke-DistroNexusTemplateAutomation.ps1`

Tasks:
- [x] Confirm cmdlet export in module manifest:
  - [x] `Get-DistroNexusTemplate`
  - [x] `Apply-DistroNexusTemplate`
  - [x] `Invoke-DistroNexusTemplateAutomation`
- [x] Confirm template discovery by ID/category works.
- [x] Confirm template apply supports variable overrides.
- [x] Confirm local automation runner supports:
  - [x] `AllTemplates` mode
  - [x] `SelectedTemplates` mode
  - [x] `DryRun`
  - [x] CI guard + override
  - [x] Capability-gated classification (`Blocked`)

Definition of Done:
- [x] PowerShell surface supports discovery, apply, and local automation validation.

## 6) Built-in Catalog Coverage Review
Files:
- `config/templates.json`

Tasks:
- [x] Verify current built-in template set and category mapping:
  - [x] `Development`
  - [x] `Platform`
  - [x] `CloudNative`
  - [x] `Database`
  - [x] `DataAndAI`
- [x] Confirm all listed built-ins have metadata + scripts + basic probe readiness.
- [x] Record gap status for `DevOps` category target (if still missing).

Definition of Done:
- [x] Built-in coverage status is explicit and traceable.

## 7) Validation and Evidence
Tasks:
- [x] Execute selective template automation run (single template).
- [x] Execute selective template automation run (multiple templates).
- [x] Execute full-catalog run when environment permits.
- [x] Collect and verify artifacts under:
  - [x] `docs/development/testing/results/<yyyymmdd>/<run-id>/summary.md`
  - [x] XML result file (`NUnitXml` or `JUnitXml`)
  - [x] Run manifest JSON
- [x] Verify pass/fail/blocked classifications are accurate.

Definition of Done:
- [x] Evidence package is complete and reproducible.

## 8) Documentation and Handoff
Tasks:
- [x] Update template guide if behavior/schema changed.
- [x] Update release notes / website docs if user-visible template capabilities changed.
- [x] Summarize changed files and unresolved risks.

Definition of Done:
- [x] Template-system implementation changes are fully documented and auditable.

## Implementation Result
- [x] Completed with no functional deviations.
- [ ] Completed with documented deviations.
- [ ] Blocked (requires follow-up).

## Evidence Notes
- Catalog integrity:
  - `TOTAL=15 UNIQUE=15`
  - `REQUIRED_FIELDS_OK`
  - `SCRIPT_PATHS_OK`
  - Category distribution: `CloudNative=2; DataAndAI=1; Database=1; Development=10; Platform=1`
- Automation validation runs (DryRun):
  - Single template: `docs/development/testing/results/20260215/101440-61b61434/summary.md`
  - Multiple templates: `docs/development/testing/results/20260215/101441-c59034b1/summary.md`
  - Full catalog: `docs/development/testing/results/20260215/101441-05c338c1/summary.md`
- Full catalog dry-run result summary:
  - `Total=15, Pass=13, Fail=0, Blocked=2`
  - Blocked by capability gate: `kubernetes-local-dev`, `ai-ml-gpu-dev`
