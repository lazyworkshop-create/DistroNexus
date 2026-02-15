# Template Automation Test Suite Manual

Last Updated: 2026-02-15  
Audience: QA engineers, maintainers, and contributors

## 1. Purpose

This document explains the built-in template automation suite, including architecture, execution modes, outputs, and operational usage.

## 2. Entry Point

Primary runner cmdlet:

- `Invoke-DistroNexusTemplateAutomation`

Implementation file:

- `src/PowerShell/Public/Invoke-DistroNexusTemplateAutomation.ps1`

## 3. What the Suite Validates

The suite validates template behavior through metadata-driven execution planning and runtime probe classification.

Key capabilities:

- all-template and selected-template execution modes,
- capability-gated classification (`Pass` / `Fail` / `Blocked`),
- run manifest and report artifact generation,
- local-first policy with CI guard.

## 4. Prerequisites

- Windows host with WSL2.
- At least one available distro.
- DistroNexus PowerShell module importable.

## 5. Command Surface

Important parameters:

- `-Mode` (`AllTemplates`, `SelectedTemplates`)
- `-TemplateIds`
- `-Distro`
- `-OutputRoot`
- `-IncludeCapabilityGated`
- `-DryRun`
- `-AllowCiOverride`
- `-UseSharedDistro`
- `-TestResultFormat` (`NUnitXml`, `JUnitXml`)

## 6. Typical Usage

## 6.1 Dry Run (Recommended First Step)

Single template:

```powershell
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds dotnet-dev -Distro Ubuntu -DryRun
```

Multiple templates:

```powershell
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds dotnet-dev,nodejs-dev -Distro Ubuntu -DryRun
```

All templates:

```powershell
Invoke-DistroNexusTemplateAutomation -Mode AllTemplates -Distro Ubuntu -DryRun
```

## 6.2 Full Execution (Controlled Environment)

```powershell
Invoke-DistroNexusTemplateAutomation -Mode AllTemplates -Distro Ubuntu
```

Enable capability-gated scenarios when environment supports them:

```powershell
Invoke-DistroNexusTemplateAutomation -Mode AllTemplates -Distro Ubuntu -IncludeCapabilityGated
```

## 7. Result Classification

- `Pass`: planned checks succeeded.
- `Fail`: apply/probe/cleanup failure occurred.
- `Blocked`: scenario gated by missing capability or policy.

Blocked is expected for some scenarios in environments without required capability (for example GPU/systemd constraints).

## 8. Artifact Structure

Default output root:

- `docs/development/testing/results/`

Per run artifacts:

- `summary.md`
- `run-manifest.json`
- `test-results.xml`
- `logs/*.json`

Index file:

- `docs/development/testing/results/index.md`

## 9. Local-Only and CI Guard Behavior

- Runner checks CI indicators and skips by default in CI contexts.
- Use `-AllowCiOverride` only when CI execution is intentionally required.

## 10. Troubleshooting

## 10.1 No Template Discovered

- Verify `config/templates.json` exists and is parseable.
- Validate module import path and `Get-DistroNexusTemplate` output.

## 10.2 Unknown Template IDs

- Ensure `-TemplateIds` values exactly match catalog IDs.

## 10.3 Blocked Results

- Review block reason in `summary.md`.
- For capability-gated scenarios, use a capable host/runtime or include gating switch where appropriate.

## 10.4 Runtime Failure

- Inspect `logs/*.json` and `run-manifest.json`.
- Re-run with a smaller selected template set to isolate issue.

## 11. Recommended Execution Strategy

1. Run single-template dry run.
2. Run related-template group dry run.
3. Run all-template dry run.
4. Run non-dry execution only in controlled validation environment.
5. Archive and link artifacts in review docs.

## 12. Reporting Guidelines

When reporting results:

- include run ID and mode,
- include pass/fail/blocked counts,
- include artifact paths,
- explicitly list blocked reasons.

## 13. Related Documents

- `docs/specs/built-in-template-automation-test-suite-requirements.md`
- `docs/development/template-system-implementation-checklist.md`
- `docs/development/template-system-acceptance-checklist.md`
