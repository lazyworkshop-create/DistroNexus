# Project Quality and Delivery Remediation - Acceptance Checklist

Based on [Project Quality and Delivery Remediation Requirements](../specs/20260219_201650_project-quality-and-delivery-remediation-requirements.md).

## 1. Acceptance Criteria Mapping

### AC-01 CI Branch and Job Execution Integrity (QDR-P0-001)
- [ ] Given a push to active default branch,
- [ ] when CI runs,
- [ ] then portable and Store packaging jobs execute as intended.

### AC-02 Toolchain Compatibility Integrity (QDR-P0-002)
- [ ] Given current `net10.0` targets,
- [ ] when workflows build/test,
- [ ] then no SDK mismatch blocks occur across CI/test/quick-test pipelines.

### AC-03 Integration Test Invocation Integrity (QDR-P0-003)
- [ ] Given integration test workflow execution,
- [ ] when the C# integration step runs,
- [ ] then it uses a valid test project/solution path and produces test artifacts.

### AC-04 Quick Validation Scope Integrity (QDR-P0-004)
- [ ] Given quick-test workflow execution,
- [ ] when test filtering is applied,
- [ ] then only intended reduced test scope runs and completes faster than full test workflow.

### AC-05 Store Readiness Closure (QDR-P1-001)
- [ ] Given release readiness review,
- [ ] when Store checklist is assessed,
- [ ] then critical install/offline/certification/listing items are complete or formally deferred with owner/milestone.

### AC-06 Real WSL Path Validation (QDR-P1-002)
- [ ] Given WSL-capable validation lane,
- [ ] when guarded tests run,
- [ ] then WSL2-dependent test evidence is produced and linked.

### AC-07 Requirement Traceability Integrity (QDR-P1-003)
- [ ] Given completed milestones,
- [ ] when requirements/specs are reviewed,
- [ ] then status matches implementation and test evidence.

### AC-08 Governance Consistency (QDR-P2-001 / QDR-P2-002)
- [ ] Given docs/governance review,
- [ ] when README/spec checklists are audited,
- [ ] then CI behavior descriptions and localization verification status are accurate.

## 2. Sign-off Evidence Requirements

- [ ] Workflow run links for `ci.yml`, `test.yml`, and `quick-test.yml`.
- [ ] C# and PowerShell test artifact links.
- [ ] Integration workflow run proving path-fix validity (no `MSB1003`).
- [ ] Quick vs full workflow timing comparison snapshot.
- [ ] Updated Store publish checklist with evidence attachments.
- [ ] Requirement/status alignment evidence from active docs.

## 3. Final Sign-off

- [ ] Engineering sign-off complete.
- [ ] QA sign-off complete.
- [ ] Release manager sign-off complete.
- [ ] Final result: [ ] Pass  [ ] Pass with Exceptions  [ ] Fail
