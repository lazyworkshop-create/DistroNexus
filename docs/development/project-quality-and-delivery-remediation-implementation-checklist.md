# Project Quality and Delivery Remediation - Implementation Checklist

Based on [Project Quality and Delivery Remediation Requirements](../specs/20260219_201650_project-quality-and-delivery-remediation-requirements.md).

## 1. P0 - CI and Workflow Reliability

### QDR-P0-001 Branch Condition Consistency
- [x] Align workflow trigger branches and conditional branch checks in `.github/workflows/ci.yml`.
- [x] Ensure packaging jobs run on the active default branch.
- [x] Remove or normalize any branch-name mismatch (`master` vs `main`).

### QDR-P0-002 .NET SDK Version Alignment
- [x] Align `.NET SDK` versions across `ci.yml`, `test.yml`, and `quick-test.yml` with `net10.0` targets.
- [x] Define one canonical SDK policy for C# workflows.
- [x] Validate restore/build/test on updated SDK matrix.

### QDR-P0-003 Integration Test Invocation Path Fix
- [x] Replace invalid integration test directory invocation with valid `.csproj` or `.slnx` path in `test.yml`.
- [x] Ensure integration step fails only on test failures, not MSBuild path errors.

### QDR-P0-004 Quick-Test Selection Validity
- [x] Introduce explicit test metadata for quick/full split (trait/category strategy).
- [x] Update quick-test filter to match actual metadata.
- [x] Verify quick workflow executes reduced scope compared with full test workflow.

## 2. P1 - Release Confidence Improvements

### QDR-P1-001 Store Publish Checklist Closure
- [ ] Complete unchecked high-value Store validation items or mark deferred with owner and milestone.
- [ ] Add missing evidence links for checklist sections C/D/E/F/G/I.
- [ ] Update sign-off status after closure.
- Deferred: Owner `Release Manager`; Milestone `v2.0.2 store-readiness gate`.

### QDR-P1-002 Real WSL Validation Lane
- [x] Add scheduled or manually-triggered workflow for WSL2-dependent tests.
- [x] Gate execution by environment capabilities.
- [x] Publish artifacts and reference results from release validation docs.

### QDR-P1-003 Requirements Status Synchronization
- [x] Update requirement docs/checklists to reflect completed implementation status.
- [x] Add evidence references from progress logs and test outputs.
- [x] Remove stale unchecked items for already completed milestones.

## 3. P2 - Governance and Documentation Cleanup

### QDR-P2-001 Test README and CI Contract Alignment
- [x] Align `tests/README.md` CI behavior statements with actual workflows.
- [x] Add explicit roadmap note for any planned-but-missing workflow.

### QDR-P2-002 Localization Plan Closure
- [x] Verify and update all checklist items in `docs/specs/realtime-localization-plan.md`.
- [x] Add evidence references for each checklist result.
- [x] Create follow-up items for any unmet localization verification criteria.

## 4. Implementation Exit Gates

- [x] All P0 items implemented and reviewed.
- [x] P1 items either completed or explicitly deferred with traceability.
- [x] Updated documentation merged with no branch-policy or workflow drift.
