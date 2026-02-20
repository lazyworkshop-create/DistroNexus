# v2.1.1 P2 - Acceptance Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) Phase 3 (P2).

## 1. Acceptance Criteria Mapping

### AC-P2-01 Historical Regression Diff (FR-3.1)
- [ ] Given at least two automation runs are available,
- [ ] when regression diff is executed,
- [ ] then summary reports include `Pass/Fail/Blocked` deltas and changed template items.

### AC-P2-02 Diff Artifact Traceability (FR-3.1)
- [ ] Given a completed automation run with diff enabled,
- [ ] when artifacts are generated,
- [ ] then diff artifact is linked from summary and results index.

### AC-P2-03 Metadata Lint Availability (FR-3.2)
- [ ] Given template metadata lint command is invoked,
- [ ] when validation runs,
- [ ] then required fields/schema/path/category rules are evaluated with deterministic output.

### AC-P2-04 Lint CI/Local Consistency (FR-3.2)
- [ ] Given local and CI execution contexts,
- [ ] when lint command runs,
- [ ] then output contract and pass/fail semantics remain consistent.

### AC-P2-05 Evidence Collector Output (FR-3.3)
- [ ] Given release evidence collector is executed,
- [ ] when output is generated,
- [ ] then deterministic evidence bundle is produced for checklist consumption.

### AC-P2-06 Evidence Governance Quality (FR-3.3)
- [ ] Given unresolved or missing evidence links exist,
- [ ] when collector/report is reviewed,
- [ ] then unresolved items are explicitly surfaced with actionable follow-up context.

## 2. Evidence Requirements
- [ ] Evidence link for regression diff artifact and summary delta section.
- [ ] Evidence link for updated results index with diff linkage.
- [ ] Evidence link for lint output samples (pass + fail cases).
- [ ] Evidence link for CI/local lint consistency verification.
- [ ] Evidence link for evidence collector bundle and checklist mapping.

## 3. Exception Handling
- [ ] Any unmet item is marked with explicit reason.
- [ ] Deferred items include Owner and Milestone.
- [ ] Deferred items include follow-up issue/task reference.

## 4. Final Sign-off
- Engineering sign-off: [ ] Complete
- QA sign-off: [ ] Complete
- Release sign-off: [ ] Complete
- Final result: [ ] Pass  [ ] Pass with Exceptions  [ ] Fail
