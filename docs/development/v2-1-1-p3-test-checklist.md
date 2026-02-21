# v2.1.1 P3 - Test Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) and deferred follow-up from P2 acceptance.

## 1. Test Scope
- FR-4.1 Regression Diff Hardening
- FR-4.2 Evidence Pipeline Standardization
- FR-4.3 Sign-off Workflow Closure

## 2. FR-4.1 Regression Diff Hardening Tests

### 2.1 Diff Correctness
- [x] Validate identical runs produce zero-change diff with zero deltas.
- [x] Validate added templates are represented correctly in changed-item output.
- [x] Validate removed templates are represented correctly in changed-item output.

### 2.2 Baseline Behavior
- [x] Validate explicit baseline run ID success path with deterministic baseline metadata.
- [x] Validate invalid explicit baseline run ID fails gracefully with actionable diagnostics.
- [x] Validate latest-successful baseline fallback still behaves as expected.

### 2.3 Artifact Consistency
- [x] Validate summary and index include synchronized diff linkage metadata.
- [x] Validate changed-item ordering remains stable across repeated runs.
- [x] Validate sample fallback and real-run output contracts remain compatible.

## 3. FR-4.2 Evidence Pipeline Tests

### 3.1 Path and Contract Validation
- [x] Validate all generated file references are repository-relative.
- [x] Validate lint/evidence JSON schema version field presence and correctness.
- [x] Validate token/query redaction behavior in generated references.

### 3.2 Determinism and Repeatability
- [x] Validate repeated runs produce deterministic file structure and required artifacts.
- [x] Validate acceptance evidence index includes complete required evidence mapping.
- [x] Validate evidence generation remains safe when WSL is unavailable.

## 4. FR-4.3 Sign-off Workflow Tests

### 4.1 Documentation and Traceability
- [x] Validate sign-off guidance is documented in release process flow.
- [x] Validate owner/milestone/follow-up fields are present in sign-off records.
- [x] Validate unresolved-item escalation references are discoverable and actionable.

### 4.2 Operational Readiness
- [x] Validate sign-off handoff template can be applied to real release checklist usage.
- [x] Validate no broken links in checklist cross-references.

## 5. Regression and Safety Tests
- [x] Validate no regressions in core PowerShell unit suites.
- [x] Validate no regressions in P1/P2 checklist evidence references.
- [x] Validate generated artifacts do not overwrite unrelated historical evidence.

## 6. Recommended Test Evidence
- [x] Attach FR-4.1 explicit baseline success-path test evidence.
- [x] Attach deterministic repeat-run evidence snapshots for FR-4.2.
- [x] Attach sign-off workflow dry-run evidence and traceability links.

Evidence pack: `docs/development/testing/results/p3-evidence-deterministic/`
- FR-4.1 regression tests: `docs/development/testing/results/p3-evidence-deterministic/fr-4-1-regression-diff-tests.txt`
- FR-4.2 repeatability: `docs/development/testing/results/p3-evidence-deterministic/fr-4-2-repeatability-notes.md`
- FR-4.3 sign-off dry run: `docs/development/testing/results/p3-evidence-deterministic/fr-4-3-signoff-dry-run.md`
- Acceptance index: `docs/development/testing/results/p3-evidence-deterministic/acceptance-evidence-index.md`

## 7. Sign-off
- Test Owner: [x] Assigned (`QA Lead`)
- Reviewer: [x] Assigned (`Engineering Lead`)
- Result: [ ] Pass  [x] Pass with Exceptions  [ ] Fail
