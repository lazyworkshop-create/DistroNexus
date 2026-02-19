# Project Quality and Delivery Remediation Requirements

## Document Metadata
- **Document Type**: Requirements Specification
- **Version**: 1.0
- **Date**: 2026-02-19
- **Owner**: DistroNexus Team
- **Scope**: Engineering quality, CI reliability, release readiness, and documentation alignment

## 1. Background
Recent project-wide analysis shows that core product functionality is stable, but several delivery-pipeline and governance gaps remain. These gaps mainly affect CI execution consistency, release confidence, and documentation/test traceability.

This document defines remediation requirements to close those gaps without introducing unrelated feature work.

## 2. Goals
- Ensure CI workflows reflect the actual branch strategy and runtime stack.
- Prevent false-green or non-executed test/build jobs.
- Increase release readiness confidence for Store and packaged distribution flows.
- Align requirements/spec documents with implementation status and test evidence.

## 3. Non-Goals
- Introducing new end-user features.
- Reworking template or wizard UX beyond already accepted scope.
- Rewriting archived historical documents.

## 4. Priority Tiers
- **P0 (Must Fix)**: Can cause CI mis-execution, missing validation, or blocked release quality gates.
- **P1 (Should Fix)**: Improves release confidence, operational quality, and requirement traceability.
- **P2 (Could Fix)**: Governance and documentation clarity improvements.

---

## 5. Requirements

### 5.1 P0 Requirements (Must Fix)

#### QDR-P0-001: Branch Condition Consistency in CI
**Requirement**
- Workflow trigger branches and conditional branch checks shall be consistent in `ci.yml`.
- Jobs intended for the active default branch shall not be gated by a different branch name.

**Rationale**
- Current mismatch (`master` trigger vs `refs/heads/main` condition) can silently skip packaging jobs.

**Acceptance Criteria**
- Push to active default branch executes both portable and Store package jobs.
- No job in `ci.yml` references a non-existent branch policy.

---

#### QDR-P0-002: .NET SDK Version Alignment Across Workflows
**Requirement**
- Test/build workflows shall use SDK versions compatible with project target frameworks (`net10.0` / `net10.0-windows`).

**Rationale**
- Workflow-specific `.NET 8` setup can produce restore/build failures or hidden incompatibilities.

**Acceptance Criteria**
- `ci.yml`, `test.yml`, and `quick-test.yml` use a version policy compatible with current target frameworks.
- A single documented SDK policy is referenced by all C# workflow jobs.

---

#### QDR-P0-003: Fix Invalid C# Integration Test Invocation Path
**Requirement**
- `test.yml` integration test step shall invoke a valid `.csproj` or `.slnx` path.

**Rationale**
- Directory invocation (`src/Client/DistroNexus.Tests/Integration/`) causes MSBuild `MSB1003` and does not test intended scope.

**Acceptance Criteria**
- Integration test job runs successfully using a valid test project path.
- Workflow fails only on test failures, not path/configuration errors.

---

#### QDR-P0-004: Make Quick-Test Filtering Verifiable
**Requirement**
- Quick validation workflow shall use test filters that map to actual test metadata.

**Rationale**
- Current `Category!=Integration` filter may be ineffective if categories are not consistently defined.

**Acceptance Criteria**
- Quick workflow runtime is measurably lower than full workflow for same commit.
- Test selection criteria are documented and backed by explicit test metadata.

---

### 5.2 P1 Requirements (Should Fix)

#### QDR-P1-001: Close Store Publish Verification Gaps
**Requirement**
- Unchecked high-value items in Store publish test checklist shall be completed or explicitly deferred with rationale and owner.

**Rationale**
- Current unchecked areas include install matrix, offline behavior, WACK/signature checks, and listing readiness.

**Acceptance Criteria**
- Checklist sections C/D/E/F/G/I are either marked complete with evidence or marked deferred with issue links and target milestone.
- Release sign-off status is updated from "Pass with Exceptions" when blockers are cleared.

---

#### QDR-P1-002: Add Controlled Real-WSL Validation Lane
**Requirement**
- Add a controlled execution lane for WSL2-dependent tests (scheduled or manually triggered), gated by environment capability.

**Rationale**
- Current skip-guarded tests reduce confidence in real runtime paths for `wsl.exe` interactions.

**Acceptance Criteria**
- At least one CI workflow runs WSL2-gated tests in an appropriate environment on a recurring basis or on-demand.
- Results are published as artifacts and linked in release validation records.

---

#### QDR-P1-003: Requirements-to-Implementation Status Sync
**Requirement**
- Active requirements documents shall reflect completion status consistent with tracking logs and checklist outcomes.

**Rationale**
- Completed milestones still appear unchecked in some requirement docs, creating audit confusion.

**Acceptance Criteria**
- Requirements checkboxes are updated, or replaced by explicit status sections and evidence links.
- No active requirement remains stale relative to `docs/progress.md` for closed milestones.

---

### 5.3 P2 Requirements (Could Fix)

#### QDR-P2-001: Test README and Workflow Contract Alignment
**Requirement**
- `tests/README.md` shall describe only currently existing workflow behaviors (e.g., branch model, nightly status).

**Rationale**
- Documentation currently implies workflow behavior not present in repository automation.

**Acceptance Criteria**
- README statements about CI triggers and cadence match existing workflow files.
- Any planned-but-missing workflow is tracked as explicit roadmap item.

---

#### QDR-P2-002: Localization Plan Closure
**Requirement**
- Realtime localization plan checklist shall be validated and closed with test evidence.

**Rationale**
- The plan exists with all verification items unchecked despite implementation activity.

**Acceptance Criteria**
- Checklist in `realtime-localization-plan.md` updated with pass/fail and evidence.
- Any failed item is tracked by follow-up requirement entry.

---

## 6. Implementation Phasing

### Phase A (Immediate Stabilization)
- QDR-P0-001
- QDR-P0-002
- QDR-P0-003
- QDR-P0-004

### Phase B (Release Confidence)
- QDR-P1-001
- QDR-P1-002
- QDR-P1-003

### Phase C (Governance Cleanup)
- QDR-P2-001
- QDR-P2-002

## 7. Verification Strategy

### Automated Verification
- CI dry run on PR branch confirms expected jobs execute.
- C# and PowerShell test workflows produce valid test artifacts.
- Quick workflow duration and selected test set are validated against baseline.

### Process Verification
- Store checklist includes evidence attachments for all non-deferred items.
- Requirements documents and tracking logs are cross-consistent for closed milestones.

## 8. Exit Criteria
- All P0 requirements accepted.
- No CI job is skipped due to branch-name mismatch or invalid path.
- Release-readiness checklist has no unowned critical gaps.

## 9. Risks if Deferred
- Packaging jobs silently not running on primary branch.
- Integration test signal remains unreliable.
- Release artifacts published without complete readiness evidence.
- Increased maintenance cost from status/document drift.
