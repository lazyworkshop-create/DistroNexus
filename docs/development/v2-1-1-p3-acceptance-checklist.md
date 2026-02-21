# v2.1.1 P3 - Acceptance Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) and deferred follow-up from P2 acceptance.

## 1. Acceptance Criteria Mapping

### AC-P3-01 Regression Diff Hardening (FR-4.1)
- [ ] Given baseline and current runs are available,
- [ ] when regression diff executes with explicit and default baseline modes,
- [ ] then identical/added/removed scenarios are represented correctly and deterministically.

### AC-P3-02 Diff Artifact Reliability (FR-4.1)
- [ ] Given repeated regression runs,
- [ ] when summary/index/diff artifacts are generated,
- [ ] then linkage and ordering remain stable and traceable.

### AC-P3-03 Evidence Pipeline Standardization (FR-4.2)
- [ ] Given evidence generation is executed,
- [ ] when outputs are produced,
- [ ] then all references are repository-relative and schema contract metadata is present.

### AC-P3-04 Evidence Hygiene and Determinism (FR-4.2)
- [ ] Given evidence bundle generation runs multiple times,
- [ ] when output is reviewed,
- [ ] then token-sensitive data is redacted and artifact structure remains deterministic.

### AC-P3-05 Sign-off Workflow Integration (FR-4.3)
- [ ] Given release governance documentation is reviewed,
- [ ] when sign-off process is followed,
- [ ] then owner, milestone, and escalation references are complete and actionable.

### AC-P3-06 Final Closure Readiness (FR-4.3)
- [ ] Given P3 implementation/test items are completed,
- [ ] when engineering, QA, and release sign-off are requested,
- [ ] then final closure can move from exceptions to pass criteria.

## 2. Evidence Requirements
- [ ] Evidence link for FR-4.1 explicit baseline success-path and zero-change behavior.
- [ ] Evidence link for FR-4.1 added/removed template diff coverage.
- [ ] Evidence link for FR-4.2 relative-path and schema-version compliance.
- [ ] Evidence link for FR-4.2 deterministic repeat-run comparison snapshots.
- [ ] Evidence link for FR-4.3 sign-off workflow handoff and escalation references.

## 3. Exception Handling
- [ ] Any unmet item is marked with explicit reason.
- [ ] Deferred items include Owner and Milestone.
- [ ] Deferred items include follow-up issue/task reference.

## 4. Final Sign-off
- Engineering sign-off: [ ] Complete
- QA sign-off: [ ] Complete
- Release sign-off: [ ] Complete
- Final result: [ ] Pass  [ ] Pass with Exceptions  [ ] Fail
