# v2.1.1 P3 - Test Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) and deferred follow-up from P2 acceptance.

## 1. Test Scope
- FR-4.1 Regression Diff Hardening
- FR-4.2 Evidence Pipeline Standardization
- FR-4.3 Sign-off Workflow Closure

## 2. FR-4.1 Regression Diff Hardening Tests

### 2.1 Diff Correctness
- [ ] Validate identical runs produce zero-change diff with zero deltas.
- [ ] Validate added templates are represented correctly in changed-item output.
- [ ] Validate removed templates are represented correctly in changed-item output.

### 2.2 Baseline Behavior
- [ ] Validate explicit baseline run ID success path with deterministic baseline metadata.
- [ ] Validate invalid explicit baseline run ID fails gracefully with actionable diagnostics.
- [ ] Validate latest-successful baseline fallback still behaves as expected.

### 2.3 Artifact Consistency
- [ ] Validate summary and index include synchronized diff linkage metadata.
- [ ] Validate changed-item ordering remains stable across repeated runs.
- [ ] Validate sample fallback and real-run output contracts remain compatible.

## 3. FR-4.2 Evidence Pipeline Tests

### 3.1 Path and Contract Validation
- [ ] Validate all generated file references are repository-relative.
- [ ] Validate lint/evidence JSON schema version field presence and correctness.
- [ ] Validate token/query redaction behavior in generated references.

### 3.2 Determinism and Repeatability
- [ ] Validate repeated runs produce deterministic file structure and required artifacts.
- [ ] Validate acceptance evidence index includes complete required evidence mapping.
- [ ] Validate evidence generation remains safe when WSL is unavailable.

## 4. FR-4.3 Sign-off Workflow Tests

### 4.1 Documentation and Traceability
- [ ] Validate sign-off guidance is documented in release process flow.
- [ ] Validate owner/milestone/follow-up fields are present in sign-off records.
- [ ] Validate unresolved-item escalation references are discoverable and actionable.

### 4.2 Operational Readiness
- [ ] Validate sign-off handoff template can be applied to real release checklist usage.
- [ ] Validate no broken links in checklist cross-references.

## 5. Regression and Safety Tests
- [ ] Validate no regressions in core PowerShell unit suites.
- [ ] Validate no regressions in P1/P2 checklist evidence references.
- [ ] Validate generated artifacts do not overwrite unrelated historical evidence.

## 6. Recommended Test Evidence
- [ ] Attach FR-4.1 explicit baseline success-path test evidence.
- [ ] Attach deterministic repeat-run evidence snapshots for FR-4.2.
- [ ] Attach sign-off workflow dry-run evidence and traceability links.

## 7. Sign-off
- Test Owner: [ ] Assigned
- Reviewer: [ ] Assigned
- Result: [ ] Pass  [ ] Pass with Exceptions  [ ] Fail
