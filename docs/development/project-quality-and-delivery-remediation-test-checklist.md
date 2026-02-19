# Project Quality and Delivery Remediation - Test Checklist

Based on [Project Quality and Delivery Remediation Requirements](../specs/20260219_201650_project-quality-and-delivery-remediation-requirements.md).

## 1. Workflow Validation Tests (P0)

### QDR-P0-001 Branch Condition Consistency
- [x] Validate CI package jobs run on active default branch push.
- [x] Validate package jobs do not run on unrelated branches (if intended).
- [x] Validate no branch-gating mismatch remains in workflow execution logs.

### QDR-P0-002 .NET SDK Alignment
- [x] Validate C# workflows restore/build/test successfully using aligned SDK.
- [x] Validate no SDK-compatibility warnings/errors for `net10.0` projects.
- [x] Validate toolchain consistency across CI, test, and quick-test workflows.

### QDR-P0-003 Integration Path Fix
- [x] Validate C# integration test step runs with valid project/solution path.
- [x] Validate workflow no longer produces `MSB1003` path errors.
- [x] Validate integration test outputs are generated and uploaded.

### QDR-P0-004 Quick-Test Filter Effectiveness
- [x] Validate quick-test executes a reduced test set versus full workflow.
- [x] Validate test metadata filter matches expected test classes/cases.
- [x] Validate quick-test duration is significantly lower than full test workflow.

## 2. Release Readiness Tests (P1)

### QDR-P1-001 Store Publish Validation
- [ ] Validate install/upgrade/uninstall matrix on required platforms.
- [ ] Validate offline startup and core local WSL operations.
- [ ] Validate WACK and package integrity/signature checks.
- [ ] Validate Store listing assets/links and metadata requirements.
- Deferred: Owner `Release Manager`; Milestone `v2.0.2 store-readiness gate`.

### QDR-P1-002 Real WSL Validation Lane
- [x] Validate WSL2-gated tests run in designated CI lane.
- [x] Validate skipped-to-executed transition for guarded tests where environment is available.
- [x] Validate artifact publication and reproducible execution commands.

### QDR-P1-003 Requirement Status Sync
- [x] Validate requirement checkbox/status alignment with implementation and progress logs.
- [x] Validate all completed milestones have supporting evidence links.

## 3. Governance Tests (P2)

### QDR-P2-001 Documentation/Workflow Alignment
- [x] Validate `tests/README.md` CI behavior matches real workflow files.
- [x] Validate references to non-existent workflows are removed or marked planned.

### QDR-P2-002 Localization Plan Closure
- [x] Validate real-time language switching checklist items against runtime behavior.
- [x] Validate restart persistence and converter/tooltip updates.
- [x] Validate no localization-related binding warnings in build output.

## 4. Regression Safety

- [x] Validate no regressions in existing targeted local commands:
  - `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~TemplateServiceTests|FullyQualifiedName~SelectTemplateStepTests|FullyQualifiedName~InstallWizardWorkflowViewModelTests|FullyQualifiedName~ReviewStepTests"`
- [x] Validate Pester suite still passes for PowerShell module paths.
- [x] Validate CI artifact outputs remain available for csharp and powershell test jobs.
