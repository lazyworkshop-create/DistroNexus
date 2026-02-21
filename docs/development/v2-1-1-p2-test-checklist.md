# v2.1.1 P2 - Test Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) Phase 3 (P2).

## 1. Test Scope
- FR-3.1 Historical Regression Diff
- FR-3.2 Template Metadata Lint
- FR-3.3 Release Evidence Collector

## 2. FR-3.1 Regression Diff Tests

### 2.1 Diff Correctness
- [ ] Validate identical runs produce zero-change diff.
- [x] Validate status transition matrix (`Pass↔Fail↔Blocked`) is correctly captured.
- [ ] Validate added/removed templates are correctly represented in diff output.

### 2.2 Baseline Selection
- [x] Validate default baseline selection behavior is deterministic.
- [ ] Validate explicit baseline run ID selection path.
- [x] Validate missing baseline handling with actionable message and no crash.

### 2.3 Artifact and Summary
- [x] Validate diff artifact is generated with stable schema.
- [x] Validate summary includes delta counters and changed-item table/list.
- [x] Validate index links to diff artifact and run summary.

## 3. FR-3.2 Metadata Lint Tests

### 3.1 Rule Coverage
- [x] Validate required-field rule failures are detected.
- [x] Validate duplicate ID and category policy rule failures are detected.
- [x] Validate script path traversal and absolute path violations are detected.

### 3.2 Output Contract
- [x] Validate lint JSON output contains rule ID, severity, path, and message.
- [x] Validate human-readable summary aligns with JSON results.
- [x] Validate strict mode exits with non-zero code on errors.

### 3.3 CI/Local Compatibility
- [x] Validate lint command runs in local shell and CI-compatible environment.
- [x] Validate no side effects on template files during lint execution.

## 4. FR-3.3 Evidence Collector Tests

### 4.1 Input Processing
- [x] Validate collector ingests configured evidence sources.
- [x] Validate missing/invalid links are classified as unresolved items.
- [x] Validate manual override entries are preserved in output.

### 4.2 Output Validation
- [x] Validate evidence bundle includes timestamp and source metadata.
- [x] Validate checklist section mapping is complete and deterministic.
- [x] Validate sensitive fields are not exposed in output.

### 4.3 Workflow Integration
- [x] Validate collector output can be referenced from release checklists.
- [x] Validate rerun behavior is stable and idempotent.

## 5. Regression and Safety Tests
- [x] Validate existing template automation pipeline remains functional.
- [x] Validate existing docs/checklist references are not broken.
- [x] Validate no test regressions in core PowerShell unit suites.

## 6. Recommended Test Evidence
- [x] Attach sample regression diff artifact and summary snapshot.
- [x] Attach lint report with at least one pass and one failure case.
- [x] Attach evidence collector bundle and checklist linkage proof.

Evidence pack (generated): `docs/development/testing/results/p2-evidence-20260221-110630/`
- Regression diff: `docs/development/testing/results/p2-evidence-20260221-110630/automation-sample/regression-diff.json`
- Regression summary: `docs/development/testing/results/p2-evidence-20260221-110630/automation-sample/summary.md`
- Lint pass report: `docs/development/testing/results/p2-evidence-20260221-110630/lint/lint-pass.json`
- Lint fail report: `docs/development/testing/results/p2-evidence-20260221-110630/lint/lint-fail.json`
- Evidence bundle: `docs/development/testing/results/p2-evidence-20260221-110630/p2-evidence-bundle.json`
- Linkage proof: `docs/development/testing/results/p2-evidence-20260221-110630/p2-test-evidence-proof.md`

## 7. Sign-off
- Test Owner: [ ] Assigned
- Reviewer: [ ] Assigned
- Result: [ ] Pass  [x] Pass with Exceptions  [ ] Fail

