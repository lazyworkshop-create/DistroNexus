# v2.1.1 P2 - Test Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) Phase 3 (P2).

## 1. Test Scope
- FR-3.1 Historical Regression Diff
- FR-3.2 Template Metadata Lint
- FR-3.3 Release Evidence Collector

## 2. FR-3.1 Regression Diff Tests

### 2.1 Diff Correctness
- [ ] Validate identical runs produce zero-change diff.
- [ ] Validate status transition matrix (`Pass↔Fail↔Blocked`) is correctly captured.
- [ ] Validate added/removed templates are correctly represented in diff output.

### 2.2 Baseline Selection
- [ ] Validate default baseline selection behavior is deterministic.
- [ ] Validate explicit baseline run ID selection path.
- [ ] Validate missing baseline handling with actionable message and no crash.

### 2.3 Artifact and Summary
- [ ] Validate diff artifact is generated with stable schema.
- [ ] Validate summary includes delta counters and changed-item table/list.
- [ ] Validate index links to diff artifact and run summary.

## 3. FR-3.2 Metadata Lint Tests

### 3.1 Rule Coverage
- [ ] Validate required-field rule failures are detected.
- [ ] Validate duplicate ID and category policy rule failures are detected.
- [ ] Validate script path traversal and absolute path violations are detected.

### 3.2 Output Contract
- [ ] Validate lint JSON output contains rule ID, severity, path, and message.
- [ ] Validate human-readable summary aligns with JSON results.
- [ ] Validate strict mode exits with non-zero code on errors.

### 3.3 CI/Local Compatibility
- [ ] Validate lint command runs in local shell and CI-compatible environment.
- [ ] Validate no side effects on template files during lint execution.

## 4. FR-3.3 Evidence Collector Tests

### 4.1 Input Processing
- [ ] Validate collector ingests configured evidence sources.
- [ ] Validate missing/invalid links are classified as unresolved items.
- [ ] Validate manual override entries are preserved in output.

### 4.2 Output Validation
- [ ] Validate evidence bundle includes timestamp and source metadata.
- [ ] Validate checklist section mapping is complete and deterministic.
- [ ] Validate sensitive fields are not exposed in output.

### 4.3 Workflow Integration
- [ ] Validate collector output can be referenced from release checklists.
- [ ] Validate rerun behavior is stable and idempotent.

## 5. Regression and Safety Tests
- [ ] Validate existing template automation pipeline remains functional.
- [ ] Validate existing docs/checklist references are not broken.
- [ ] Validate no test regressions in core PowerShell unit suites.

## 6. Recommended Test Evidence
- [ ] Attach sample regression diff artifact and summary snapshot.
- [ ] Attach lint report with at least one pass and one failure case.
- [ ] Attach evidence collector bundle and checklist linkage proof.

## 7. Sign-off
- Test Owner: [ ] Assigned
- Reviewer: [ ] Assigned
- Result: [ ] Pass  [ ] Pass with Exceptions  [ ] Fail
