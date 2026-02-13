# Findings

## Research Notes
- Initial requirements saved to `docs/specs/template-system-requirements.md`

## Template System Audit (2026-02-13)

### Evidence Confirmed
- Core files exist: `Template.cs`, `TemplateApplicationRecord.cs`, `ITemplateService.cs`, `TemplateService.cs`.
- UI integration points exist: `SelectTemplateStep`, `SelectTemplateStepView`, workflow registration, and template invocation in `ProgressStep`.
- PowerShell cmdlets exist and are exported in `DistroNexus.psd1`.
- Unit tests pass:
	- C# template-filtered tests: 6 passed, 0 failed.
	- PowerShell template cmdlet tests: 6 passed, 0 failed.

### Gaps Against `docs/specs/template-system-requirements.md`
- Preset templates are incomplete: `python-dev`, `docker-dev`, and `fullstack-dev` scripts/config entries are missing.
- `InstallOptions` is missing optional `TemplateId` requested by the spec.
- `SelectTemplateStep` does not yet implement all required behavior (distro compatibility filtering, explicit skip-template interaction, and step validation/exit handling).
- No template-specific integration tests or E2E validation artifacts were found under `tests/CSharp/Integration` or `tests/PowerShell/Integration`.

### Current Assessment
- Overall completion for the requirement set is **partial**: core architecture is implemented, but requirement completeness and validation completeness are not yet achieved.

## Requirement Refinement Update (2026-02-13)

- Completed requirement hardening for template feature with explicit P0/P1/P2 priorities.
- Added release-gate acceptance checklist and Definition of Done into the spec.
- Converted high-level suggestions into concrete, testable requirements for:
	- preset template set completeness,
	- install contract completeness (`TemplateId`),
	- wizard behavior completeness (filtering/skip/validation),
	- execution consistency (`ScriptPath` vs `Content`),
	- integration/E2E verification scope.

## P0 Implementation Update (2026-02-13)

- Completed all P0 release-blocker requirements:
	- 5 official templates are now defined in `config/templates.json`.
	- Added missing preset scripts: `python-dev`, `docker-dev`, `fullstack-dev`.
	- Added `InstallOptions.TemplateId` and mapped from `WizardContext.ToInstallOptions()`.
	- Implemented select-template compatibility filtering, explicit skip behavior, validation, and exit-state persistence.
	- Unified script execution in PowerShell cmdlet for both `ScriptPath` and inline `Content`.
	- Added per-script timeout handling in `TemplateService`.

- Verification evidence:
	- C# template tests: 13 passed, 0 failed.
	- PowerShell template tests: 9 passed, 0 failed.

## Final Completion Update (2026-02-13)

- Completed P1 hardening:
	- failure semantics validated for `ContinueOnError` paths,
	- detailed script-level logging with duration and source mode,
	- template application history persisted and queryable by instance,
	- custom-template confirmation and script path traversal protection.
- Completed P2 stabilization:
	- added C# template integration tests,
	- added PowerShell template integration tests,
	- documented E2E verification matrix in `docs/development/testing/template-e2e-verification.md`.
- Requirement checklist in `docs/specs/template-system-requirements.md` is fully checked with test evidence.

## Wizard UX Refactor Update (2026-02-14)

- Completed structural split for installation and template application UX:
	- `ProgressStep` restored to installation-only behavior and original-style progress UI.
	- Added `TemplateApplyStep` with dedicated fullscreen log-focused template execution UI.
	- Workflow updated to `ProgressStep -> TemplateApplyStep -> ResultStep`.
	- Added a default `IsLogFullscreen` property in `WizardStepBase` and explicit override in `TemplateApplyStep`.

- Validation evidence:
	- Template-focused C# tests: 13 passed, 0 failed.
	- `DistroNexus.Core` and `DistroNexus.Desktop` compile successfully in the same run.

- Note:
	- Full solution test run still contains unrelated pre-existing failures outside template scope.
