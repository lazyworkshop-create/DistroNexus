# v2.1.1 Tooling Enhancement Requirements

## Document Metadata
- **Document Type**: Requirements Specification
- **Version**: 1.0
- **Date**: 2026-02-20
- **Target Release**: v2.1.1
- **Owner**: DistroNexus Team
- **Scope**: Tooling capability enhancement, template workflow DX, and delivery-governance automation

## 1. Background
Current tooling already provides a stable baseline (15 PowerShell cmdlets, 15 built-in templates, local-first template automation). However, several practical gaps remain in discoverability, dry-run safety, category completeness, and release-evidence automation.

v2.1.1 focuses on targeted, low-risk improvements that increase operational efficiency and reduce workflow friction without changing core architecture.

## 2. Goals
- Improve template discoverability and pre-execution predictability.
- Complete built-in template category parity for practical workflows.
- Strengthen local validation, diagnostics, and historical comparison.
- Improve governance efficiency for release evidence and metadata quality.

## 3. Non-Goals
- No large UI framework redesign.
- No cloud-first orchestration changes.
- No breaking changes to existing cmdlet names.
- No replacement of existing local-first validation policy.

## 4. Execution Policy (Tracking Files)
This release MUST follow the tracking-file policy below for:
- `docs/task_plan.md`
- `docs/findings.md`
- `docs/progress.md`

### 4.1 Normal Iteration
- Do not overwrite full file content directly.
- Append new dated sections/entries to preserve continuity and auditability.

### 4.2 Refresh Cycle
When a clean refresh is required:
1. Archive previous tracking files first under:
   - `docs/archive/{year}/{month}/{yyyymmdd}_{topic}/`
2. Then create new tracking files with the same canonical names.
3. New files must include milestone context and reference to archived location.

## 5. Three-Phase Requirements

### Phase 1: Query and Dry-Run Usability (P0)

#### FR-1.1 Extended Template Query Surface
**Requirement**
- `Get-DistroNexusTemplate` shall support additional filters:
  - `-Tag`
  - `-InstallMode`
  - `-CompatibleDistro`
  - optional fuzzy search by template name.

**Acceptance Criteria**
- Filtering by each new parameter works independently and in combination.
- Existing `-Id`/`-Category` behavior remains backward compatible.

#### FR-1.2 Template Apply Plan-Only Mode
**Requirement**
- `Apply-DistroNexusTemplate` shall provide `-DryRun` (or `-PlanOnly`) mode to output:
  - resolved target template,
  - effective variables,
  - preflight summary,
  - planned script sequence.

**Acceptance Criteria**
- Dry-run does not execute script side effects.
- Output clearly indicates plan-only status and actionable preflight guidance.

#### FR-1.3 Strict Instance Validation
**Requirement**
- Template apply shall perform strict distro existence validation before execution.

**Acceptance Criteria**
- Unknown distro name fails fast with clear remediation message.
- Validation behavior is deterministic across repeated runs.

---

### Phase 2: Coverage and Diagnostics (P1)

#### FR-2.1 DevOps Category Completion
**Requirement**
- Add at least one built-in `DevOps` template (`infra-cli-toolbox` minimum baseline).

**Acceptance Criteria**
- Template appears in catalog, is discoverable by category/tag, and has executable script assets.
- Category parity gap is closed in architecture/spec/checklist docs.

#### FR-2.2 Environment Diagnostic Cmdlet
**Requirement**
- Introduce a reusable diagnostic command (for example `Test-DistroNexusTemplateEnvironment`) for capability checks used by template workflows.

**Acceptance Criteria**
- Command reports WSL/systemd/GPU/container prerequisites with machine-readable status.
- Validation output can be consumed by both manual and automated workflows.

#### FR-2.3 Capability Profile Presets
**Requirement**
- Automation runner shall support capability profile presets:
  - `CpuOnly`
  - `GpuCapable`
  - `SystemdCapable`

**Acceptance Criteria**
- Presets map to consistent gating behavior.
- Existing parameter set remains compatible.

---

### Phase 3: Regression Insight and Governance Automation (P2)

#### FR-3.1 Historical Regression Diff
**Requirement**
- Template automation output shall support comparison with previous runs.

**Acceptance Criteria**
- Summary reports include delta of `Pass/Fail/Blocked` and changed template items.
- Diff artifacts are linked from run summary/index.

#### FR-3.2 Template Metadata Lint
**Requirement**
- Provide metadata linting for `config/templates.json` and script asset references.

**Acceptance Criteria**
- Lint validates required fields, schema shape, script path safety, and category policy.
- Lint can run in local validation and CI without side effects.

#### FR-3.3 Release Evidence Collector
**Requirement**
- Add helper tooling to collect workflow/test/release evidence links into release-governance docs.

**Acceptance Criteria**
- Collector outputs a deterministic evidence bundle for checklist consumption.
- Manual evidence collection effort is measurably reduced.

## 6. Verification Strategy

### 6.1 Functional Verification
- Cmdlet behavior tests for query and dry-run modes.
- Template apply validation tests for strict distro checks.
- Diagnostic command output contract tests.

### 6.2 Integration Verification
- Selected template automation runs validate new gating and profile behavior.
- At least one run demonstrates regression diff generation.

### 6.3 Governance Verification
- Lint command is integrated into at least one workflow or documented local gate.
- Evidence collector output is referenced by release checklists.

## 7. Exit Criteria
- Phase 1 requirements accepted.
- Phase 2 requirements accepted or explicitly deferred with owner and milestone.
- Phase 3 requirements accepted or tracked as bounded follow-up with no P0/P1 regressions.

## 8. Risks and Mitigations
- **Risk**: Expanded query parameters create ambiguity.
  - **Mitigation**: Keep defaults unchanged and add explicit parameter precedence rules.
- **Risk**: New diagnostics may duplicate existing checks.
  - **Mitigation**: Centralize capability evaluation and reuse in automation and apply flow.
- **Risk**: Governance automation coupling to external systems.
  - **Mitigation**: Keep collector output format file-based and source-agnostic.

## 9. Related Checklists
- Implementation checklist (P1): `docs/development/v2-1-1-p1-implementation-checklist.md`
- Test checklist (P1): `docs/development/v2-1-1-p1-test-checklist.md`
- Acceptance checklist (P1): `docs/development/v2-1-1-p1-acceptance-checklist.md`
- Implementation checklist (P2): `docs/development/v2-1-1-p2-implementation-checklist.md`
- Test checklist (P2): `docs/development/v2-1-1-p2-test-checklist.md`
- Acceptance checklist (P2): `docs/development/v2-1-1-p2-acceptance-checklist.md`
- Implementation checklist (P3): `docs/development/v2-1-1-p3-implementation-checklist.md`
- Test checklist (P3): `docs/development/v2-1-1-p3-test-checklist.md`
- Acceptance checklist (P3): `docs/development/v2-1-1-p3-acceptance-checklist.md`
