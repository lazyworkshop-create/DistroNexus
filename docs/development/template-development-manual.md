# Template Development Manual

Last Updated: 2026-02-15  
Audience: Template contributors and maintainers

## 1. Purpose

This manual defines how to create, evolve, validate, and maintain templates in DistroNexus.

## 2. Repository Locations

- Metadata catalog: `config/templates.json`
- Script assets: `config/templates/<template-id>/`
- Shared helpers: `config/templates/common/`
- Core model and service:
  - `src/Client/DistroNexus.Core/Models/Template.cs`
  - `src/Client/DistroNexus.Core/Services/TemplateService.cs`

## 3. Template Authoring Workflow

1. Define template metadata in `config/templates.json`.
2. Add scripts under `config/templates/<template-id>/`.
3. Validate metadata completeness and script-path correctness.
4. Run selective automation dry run.
5. Run runtime validation in controlled WSL environment if needed.
6. Update related docs and checklists.

## 4. Required Metadata

Each template should provide:

- `Id`
- `Name`
- `Category`
- `Description`
- `CompatibleDistros`
- `Scripts`

Recommended additional metadata:

- `InstallMode`
- `ScenarioTags`
- `VersionOptions`
- `DefaultSelections`
- `PreflightChecks`
- `OutputArtifacts`

## 5. Script Authoring Standards

## 5.1 Safety

- Use relative script paths only.
- Never assume unrestricted host file-system access.
- Avoid destructive commands unless clearly guarded.

## 5.2 Idempotency

- Scripts should be safe to rerun.
- Use existence checks before install/config steps.
- Keep state transitions explicit and deterministic.

## 5.3 Error Behavior

- Fail fast for mandatory dependencies.
- Emit actionable messages.
- Use `ContinueOnError` only when partial completion is acceptable.

## 5.4 Variable Usage

- Use `${VARIABLE_NAME}` placeholders for configurable values.
- Prefer metadata-driven defaults over hardcoded script constants.

## 6. Version-Aware Template Design

For version-managed templates:

- Define `VersionOptions` with clear labels and default values.
- Ensure `DefaultSelections` are provided for required options.
- Add output artifacts for project pinning where relevant (for example `.nvmrc`, `global.json`).

## 7. Preflight Check Design

Preflight checks should:

- validate host/runtime capability before installation,
- separate required checks from optional checks,
- include clear remediation messages.

Typical capability-gated scenarios:

- GPU profile requirements,
- systemd-dependent setup,
- container runtime mode constraints.

## 8. Validation Workflow

## 8.1 Static Validation

- Confirm metadata parse and required fields.
- Confirm script path existence under `config/templates/`.

## 8.2 Automation Validation

Use runner for fast checks:

```powershell
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds your-template-id -Distro Ubuntu -DryRun
```

Then validate related family set:

```powershell
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds your-template-id,related-template -Distro Ubuntu -DryRun
```

## 8.3 Runtime Verification

In controlled environments, run non-dry automation and verify runtime probes for required tools.

## 9. Common Contributor Mistakes

1. Missing required metadata fields.
2. Using absolute or unresolved script paths.
3. Missing defaults for required version options.
4. Omitting preflight checks for capability-sensitive templates.
5. Writing non-idempotent scripts.

## 10. Definition of Done for Template Changes

A template contribution is done when:

- metadata and scripts are complete,
- validation checks pass,
- automation artifacts are generated for review,
- and documentation is updated.

## 11. Review Checklist for PRs

- Metadata correctness and readability.
- Script safety and idempotency.
- Version option clarity.
- Preflight and failure behavior quality.
- Validation evidence included.

## 12. Related Documents

- `docs/development/template-system-comprehensive-guide.md`
- `docs/development/template-system-implementation-checklist.md`
- `docs/development/template-system-acceptance-checklist.md`
- `docs/development/template-automation-test-suite-manual.md`
