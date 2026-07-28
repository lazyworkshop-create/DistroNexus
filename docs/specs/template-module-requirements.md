# Requirements: Template Module-Client Migration

## Purpose

Make template catalog, marketplace and template application a PowerShell-module product capability. WPF may select, confirm and render typed results only; it must not invoke `ITemplateService` or `ITemplateMarketplaceService` business operations.

## Project Context

- Project and repository: DistroNexus; `D:\repo\lazyworkshop-create\DistroNexus`.
- Existing capability and affected surfaces: `TemplateService` currently owns local/marketplace template provenance and apply history; `TemplateMarketplaceService` owns source, manifest, artifact and review state; template wizard and template page call both directly.
- Authority: `AGENTS.md`, `docs/specs/powershell-first-requirements.md`, `docs/specs/powershell-first-design.md`, this requirements record and its accepted decision/contract records.
- Permitted mutation: repository code/tests only. Live WSL template execution, recovery creation and publication are excluded.

## Scope

- In scope: fixed module/Bridge/client contracts for template catalog, marketplace lifecycle and template apply preview/execute/status/cancel; wizard/template-page migration; removing unsafe legacy public apply execution.
- Out of scope: new template content, arbitrary script execution, user-selected Core paths, live WSL recovery/UAT and publishing.
- Compatibility: exported command names may be retained as safe wrappers; no legacy command may retain direct `Invoke-Expression` or bypass reviewed artifact provenance.

## Actors and Trust Boundaries

| Actor/component | Trust level | Responsibility |
| --- | --- | --- |
| WPF template screens | Presentation | Select typed data, obtain consent, render status and request cancellation. |
| Module/client | Transport | Invoke only documented fixed commands. |
| Bridge/Core | Trusted | Validate provenance, variables, recovery choice, grants and runtime execution. |
| Marketplace/template material | Untrusted until Core authorization | Supply declared metadata/artifact only; cannot select commands, paths or executables. |

## Functional Requirements

### FR-001 Module-only template behavior

Every template catalog, marketplace and apply operation initiated by Desktop uses `IPowerShellModuleClient`; affected Desktop classes contain no direct template or marketplace business-service dependency.

Acceptance: structural tests reject direct service fields/calls and operation tests verify typed client calls.

### FR-002 Reviewed tokenized application

Apply requires `template.apply.preview.v1` followed by token-only execute. The durable same-user grant binds instance identity, selected template/version, normalized source, manifest digest, artifact SHA/root, executable-file hashes, normalized variable digest, declared capabilities, recovery decision and expiry.

Acceptance: execute accepts only `PreviewToken`; tampered/expired/replayed/drifted inputs cannot run a script or promote a marketplace candidate.

### FR-003 Recovery, trust, progress and cancellation

The preview returns a recovery offer and whether explicit custom/marketplace trust is required. The user may decline a recovery point to preserve current UX; that explicit choice is token-bound and recorded. Execute returns an operation ID; status and cancel are fixed same-user operations. Cancellation reports a truthful terminal outcome and never claims rollback that Core cannot prove.

Acceptance: declined consent invokes nothing; cancellation never authorizes another operation; failure/cancel does not promote an unapproved marketplace candidate.

### FR-004 Marketplace and catalog ownership

Source, discovery, artifact, review, approval, rollback and local template import/export/remove contracts are fixed typed module operations. Public import/export accepts/returns bounded content only, not product-state paths. Core remains filesystem, network, manifest and artifact authority.

Acceptance: no Desktop service call or arbitrary Bridge operation/path remains; exact source/template/manifest identity is validated on mutation.

## Non-Functional Requirements

- Security: no `Invoke-Expression`, generic command tunnel or caller-controlled script/path/executable is introduced.
- Reliability: grants and operations are DPAPI `CurrentUser`, SID-bound, atomic, bounded and recoverable across module processes.
- Observability: progress and terminal results are modeled/redacted; history is Core-owned.
- Performance: template payloads, variables, output and status records are bounded.

## Acceptance Criteria

- Desktop template and marketplace consumers use only typed module client operations.
- Application runs only approved immutable material through a Core-issued grant.
- Module/Bridge/client/WPF tests cover closed payloads, consent, grants, provenance drift, cancellation and candidate-promotion failure.
- Live template/recovery/cancel behavior is explicitly left as an external WSL UAT gate.

## Open Decisions and External Inputs

| Item | Impact | Owner | Smallest next action |
| --- | --- | --- | --- |
| Live recovery and cancellation behavior | External acceptance | Release/UAT owner | Run disposable-WSL template matrix after repository acceptance. |

## Source Evidence

| Area | Source | What it confirms | Confidence |
| --- | --- | --- | --- |
| Wizard bypass | `InstallWizardWorkflowViewModel.cs`, `SelectTemplateStep.cs`, `SelectDistributionStep.cs`, `TemplateApplyStep.cs` | Direct template service list/compatibility/recovery/apply calls. | Confirmed |
| Template page bypass | `TemplatesViewModel.cs` | Direct local-template and marketplace service calls. | Confirmed |
| Provenance safeguards | `TemplateService.cs`, `TemplateMarketplaceService.cs` | Core already validates exact authorized material and candidate promotion. | Confirmed |
| Unsafe legacy command | `src/PowerShell/Public/Apply-DistroNexusTemplate.ps1` | Direct script content and `Invoke-Expression` lack current provenance/token contract. | Confirmed |
