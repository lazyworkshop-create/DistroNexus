---
id: user-manual
title: User Manual
sidebar_position: 5
---

# Template System User Manual

Last Updated: 2026-02-15  
Audience: End users and operators

## 1. What the Template System Does

The template system helps you turn a WSL instance into a ready-to-use development environment with one guided workflow.

Typical outcomes:

- install language/runtime toolchains,
- apply common development stack defaults,
- and keep setup steps consistent across machines.

## 2. Prerequisites

- Windows 10/11 with WSL2 enabled.
- A target WSL distro instance (for example Ubuntu).
- DistroNexus installed and PowerShell module available.

## 3. Quick Start

## 3.1 In Desktop Wizard

1. Start instance installation workflow.
2. Select your distro and base options.
3. Choose a template in the template selection step.
4. Continue to apply step and monitor progress.
5. Review completion status and output.

## 3.2 In PowerShell

Import module:

```powershell
Import-Module "C:\Program Files\DistroNexus\PowerShell\DistroNexus.psm1"
```

List templates:

```powershell
Get-DistroNexusTemplate
Get-DistroNexusTemplate -Category Development
```

Apply one template:

```powershell
Apply-DistroNexusTemplate -InstanceName Ubuntu-22.04 -TemplateId python-dev -Verbose
```

Apply with variables:

```powershell
Apply-DistroNexusTemplate -InstanceName Ubuntu-22.04 -TemplateId nodejs-multi-version-dev -Variables @{ SDK_NODE_CHANNEL = 'lts/*' }
```

## 4. Built-in Template Categories

Current categories:

- `Development`
- `Platform`
- `CloudNative`
- `Database`
- `DataAndAI`

Use category filtering to quickly find the right template family.

## 5. Common User Workflows

## 5.1 Language Environment Bootstrap

- Choose one language-focused template (for example `.NET`, `Node.js`, `Python`, `Rust`, `Go`).
- Apply to a clean or existing development instance.

## 5.2 Fullstack Bootstrap

- Choose `fullstack-dev` for combined stack setup.

## 5.3 Local Platform Stack Setup

- Use `container-runtime-dev` / `kubernetes-local-dev` / `database-local-stack` depending on scenario.

## 5.4 AI/ML Profile Setup

- Use `ai-ml-gpu-dev` and select profile according to host capability.

## 6. Understanding Progress and Results

During apply, progress shows:

- current script,
- completed/total scripts,
- status message,
- latest output line.

A successful run means scripts finished under configured policy. If `ContinueOnError` is enabled for a script, warnings may still appear in a successful overall result.

## 7. Safety Notes

- Templates execute scripts. Treat custom templates as executable code.
- Review script content before applying custom or externally shared templates.
- Use dry-run validation workflows in controlled scenarios when possible.

## 8. Troubleshooting

## 8.1 Template Not Found

- Verify ID with `Get-DistroNexusTemplate`.
- Confirm category or ID filter is correct.

## 8.2 Preflight Failure

- Read error message for required capability or dependency.
- Resolve prerequisites and retry.

## 8.3 Capability-Gated Scenario Blocked

- GPU/systemd-dependent scenarios may be blocked by host/runtime constraints.
- Use environment-appropriate template profile or enable capability and rerun.

## 8.4 Script Execution Failure

- Re-run with `-Verbose` and inspect output.
- Check if target distro and required package sources are reachable.

## 9. Useful Commands

```powershell
# List all templates
Get-DistroNexusTemplate

# List one category
Get-DistroNexusTemplate -Category CloudNative

# Apply template
Apply-DistroNexusTemplate -InstanceName Ubuntu -TemplateId dotnet-dev -Verbose

# Dry-run automation validation for selected templates
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds dotnet-dev,nodejs-dev -Distro Ubuntu -DryRun
```

## 10. FAQ

## Q1: Can I apply templates to existing instances?

Yes. Use `Apply-DistroNexusTemplate` with an existing `-InstanceName`.

## Q2: Can I run all templates at once for validation?

Yes. Use `Invoke-DistroNexusTemplateAutomation -Mode AllTemplates`.

## Q3: Why do I see `Blocked` instead of `Fail`?

`Blocked` indicates capability gating (for example GPU/systemd constraints), not a script failure.

## 11. Related Documents

- `docs/development/template-system-comprehensive-guide.md`
- `docs/development/template-system-acceptance-checklist.md`
- `docs/development/template-automation-test-suite-manual.md`
