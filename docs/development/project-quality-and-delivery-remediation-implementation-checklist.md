# Project Quality and Delivery Remediation - Implementation Checklist

Based on [Project Quality and Delivery Remediation Requirements](../specs/20260219_201650_project-quality-and-delivery-remediation-requirements.md).

## 1. P0 - CI and Workflow Reliability

### QDR-P0-001 Branch Condition Consistency
- [ ] Align workflow trigger branches and conditional branch checks in `.github/workflows/ci.yml`.
- [ ] Ensure packaging jobs run on the active default branch.
- [ ] Remove or normalize any branch-name mismatch (`master` vs `main`).

### QDR-P0-002 .NET SDK Version Alignment
- [ ] Align `.NET SDK` versions across `ci.yml`, `test.yml`, and `quick-test.yml` with `net10.0` targets.
- [ ] Define one canonical SDK policy for C# workflows.
- [ ] Validate restore/build/test on updated SDK matrix.

### QDR-P0-003 Integration Test Invocation Path Fix
- [ ] Replace invalid integration test directory invocation with valid `.csproj` or `.slnx` path in `test.yml`.
- [ ] Ensure integration step fails only on test failures, not MSBuild path errors.

### QDR-P0-004 Quick-Test Selection Validity
- [ ] Introduce explicit test metadata for quick/full split (trait/category strategy).
- [ ] Update quick-test filter to match actual metadata.
- [ ] Verify quick workflow executes reduced scope compared with full test workflow.

## 2. P1 - Release Confidence Improvements

### QDR-P1-001 Store Publish Checklist Closure
- [ ] Complete unchecked high-value Store validation items or mark deferred with owner and milestone.
- [ ] Add missing evidence links for checklist sections C/D/E/F/G/I.
- [ ] Update sign-off status after closure.

### QDR-P1-002 Real WSL Validation Lane
- [ ] Add scheduled or manually-triggered workflow for WSL2-dependent tests.
- [ ] Gate execution by environment capabilities.
- [ ] Publish artifacts and reference results from release validation docs.

### QDR-P1-003 Requirements Status Synchronization
- [ ] Update requirement docs/checklists to reflect completed implementation status.
- [ ] Add evidence references from progress logs and test outputs.
- [ ] Remove stale unchecked items for already completed milestones.

## 3. P2 - Governance and Documentation Cleanup

### QDR-P2-001 Test README and CI Contract Alignment
- [ ] Align `tests/README.md` CI behavior statements with actual workflows.
- [ ] Add explicit roadmap note for any planned-but-missing workflow.

### QDR-P2-002 Localization Plan Closure
- [ ] Verify and update all checklist items in `docs/specs/realtime-localization-plan.md`.
- [ ] Add evidence references for each checklist result.
- [ ] Create follow-up items for any unmet localization verification criteria.

## 4. Implementation Exit Gates

- [ ] All P0 items implemented and reviewed.
- [ ] P1 items either completed or explicitly deferred with traceability.
- [ ] Updated documentation merged with no branch-policy or workflow drift.
