---
id: system-design
title: System Design
sidebar_position: 4
---

# Template System Design (Current Implementation)

Last Updated: 2026-02-15  
Status: Active

## 1. Overview

This document describes the current implementation design of the DistroNexus template system across metadata, core services, desktop workflow integration, PowerShell command surface, and automation validation.

## 2. Architectural Layers

## 2.1 Metadata Layer

Primary source:

- `config/templates.json`

Key responsibilities:

- Template identity and category definition.
- Script sequencing and execution metadata.
- Version/channel options and defaults.
- Preflight checks and output artifact declarations.

## 2.2 Core Domain Model Layer

Primary model file:

- `src/Client/DistroNexus.Core/Models/Template.cs`

Core entities:

- `Template`
- `TemplateScript`
- `TemplateVersionOption` / `TemplateOptionValue`
- `TemplatePreflightCheck`
- `TemplateOutputArtifact`
- `TemplateApplicationResult`
- `TemplateProgress`
- `TemplateValidationResult`

## 2.3 Service Layer

Service contract:

- `src/Client/DistroNexus.Core/Interfaces/ITemplateService.cs`

Service implementation:

- `src/Client/DistroNexus.Core/Services/TemplateService.cs`

Core capabilities:

- Catalog loading and caching.
- Template search/get operations.
- Apply pipeline orchestration.
- Validation and compatibility checks.
- Custom template import/export.
- Application history persistence.

## 2.4 Desktop Integration Layer

DI registration:

- `src/Client/DistroNexus.Desktop/App.xaml.cs`

Wizard integration:

- `src/Client/DistroNexus.Desktop/Wizard/InstallWizardWorkflowViewModel.cs`
- `src/Client/DistroNexus.Desktop/Wizard/Steps/SelectTemplateStep.cs`
- `src/Client/DistroNexus.Desktop/Wizard/Steps/TemplateApplyStep.cs`

## 2.5 PowerShell Surface Layer

Manifest exports:

- `src/PowerShell/DistroNexus.psd1`

Primary cmdlets:

- `Get-DistroNexusTemplate`
- `Apply-DistroNexusTemplate`
- `Invoke-DistroNexusTemplateAutomation`

## 3. Runtime Data and Storage

## 3.1 Built-in Metadata Source

- `config/templates.json`

## 3.2 User/Runtime Data (AppData)

- `%APPDATA%\DistroNexus\templates.json` (user template cache)
- `%APPDATA%\DistroNexus\template-application-history.json` (execution history)

## 4. Core Execution Sequence

Execution entry:

- `TemplateService.ApplyTemplateAsync(templateId, instanceName, variables, progress, cancellationToken)`

Sequence:

1. Resolve template by ID.
2. Build effective variable set.
3. Run preflight checks.
4. Execute scripts ordered by `Order`.
5. Report progress via `TemplateProgress`.
6. Persist history record.

## 5. Variable Resolution Strategy

Effective variables are merged in this order:

1. `Template.Variables`
2. `Template.DefaultSelections`
3. `Template.VersionOptions.DefaultValue` (when missing)
4. Runtime override variables passed to apply call

Later sources override earlier ones.

## 6. Preflight Check Design

Preflight checks are modeled by `TemplatePreflightCheck` and executed before scripts.

Behavior:

- Required check failure => terminate apply operation.
- Optional check failure => warning + continue.
- Conditional applicability supports `AppliesToVariable` / `AppliesToValue`.

## 7. Script Execution Design

## 7.1 Script Sources

Scripts may come from:

- inline content (`Content`), or
- relative path (`ScriptPath`) resolved via allowed roots.

## 7.2 Script Types

- Bash scripts execute inside target WSL instance.
- PowerShell scripts execute on host context.

## 7.3 Ordering and Failure Handling

- Scripts are ordered by `Order`.
- Timeout behavior is evaluated per script (`TimeoutSeconds`).
- `ContinueOnError` determines whether a failed script aborts the pipeline.

## 8. Security Model

Path and execution guardrails in current implementation:

- absolute script path rejection,
- path traversal detection,
- allowed-root enforcement for script file resolution.

These controls prevent unbounded script-path injection during template execution.

## 9. Desktop User Flow Integration

Template flow in wizard:

1. User reaches template selection step.
2. User chooses template (or skips as supported by workflow).
3. Apply step executes template and renders status/progress output.
4. Completion state is propagated to final workflow state.

## 10. PowerShell Integration Design

## 10.1 Discovery

`Get-DistroNexusTemplate` reads and filters catalog by ID/category.

## 10.2 Apply

`Apply-DistroNexusTemplate` resolves template and executes scripts for target instance with optional variable overrides.

## 10.3 Automation Validation

`Invoke-DistroNexusTemplateAutomation` supports:

- `AllTemplates` or `SelectedTemplates` modes,
- `DryRun`,
- capability-gated classification (`Blocked`),
- CI guard with explicit override,
- output artifacts (`test-results.xml`, `run-manifest.json`, `summary.md`).

## 11. Built-in Template Topology (Current)

Current category distribution:

- `Development`: 10
- `Platform`: 1
- `CloudNative`: 2
- `Database`: 1
- `DataAndAI`: 1

Gap note:

- `DevOps` category is not currently represented as a dedicated built-in category.

## 12. Design Trade-offs and Constraints

1. **Local-first validation over CI-first runtime execution**
	- safer and predictable for WSL-dependent workloads.

2. **Script flexibility with path constraints**
	- preserves extensibility while enforcing safety boundaries.

3. **Metadata-driven expansion**
	- minimizes core-code churn when adding new templates.

## 13. Known Limitations

- Capability-gated templates may classify as `Blocked` depending on host features.
- `DevOps` built-in category is pending if strict category parity is required.

## 14. Related Documents

- `docs/development/template-system-comprehensive-guide.md`
- `docs/specs/template-system-requirements-analysis.md`
- `docs/development/template-system-implementation-checklist.md`
- `docs/development/template-system-acceptance-checklist.md`
