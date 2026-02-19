# Project Quality and Delivery Remediation - Test Checklist

Based on [Project Quality and Delivery Remediation Requirements](../specs/20260219_201650_project-quality-and-delivery-remediation-requirements.md).

## 1. Workflow Validation Tests (P0)

### QDR-P0-001 Branch Condition Consistency
- [ ] Validate CI package jobs run on active default branch push.
- [ ] Validate package jobs do not run on unrelated branches (if intended).
- [ ] Validate no branch-gating mismatch remains in workflow execution logs.

### QDR-P0-002 .NET SDK Alignment
- [ ] Validate C# workflows restore/build/test successfully using aligned SDK.
- [ ] Validate no SDK-compatibility warnings/errors for `net10.0` projects.
- [ ] Validate toolchain consistency across CI, test, and quick-test workflows.

### QDR-P0-003 Integration Path Fix
- [ ] Validate C# integration test step runs with valid project/solution path.
- [ ] Validate workflow no longer produces `MSB1003` path errors.
- [ ] Validate integration test outputs are generated and uploaded.

### QDR-P0-004 Quick-Test Filter Effectiveness
- [ ] Validate quick-test executes a reduced test set versus full workflow.
- [ ] Validate test metadata filter matches expected test classes/cases.
- [ ] Validate quick-test duration is significantly lower than full test workflow.

## 2. Release Readiness Tests (P1)

### QDR-P1-001 Store Publish Validation
- [ ] Validate install/upgrade/uninstall matrix on required platforms.
- [ ] Validate offline startup and core local WSL operations.
- [ ] Validate WACK and package integrity/signature checks.
- [ ] Validate Store listing assets/links and metadata requirements.

### QDR-P1-002 Real WSL Validation Lane
- [ ] Validate WSL2-gated tests run in designated CI lane.
- [ ] Validate skipped-to-executed transition for guarded tests where environment is available.
- [ ] Validate artifact publication and reproducible execution commands.

### QDR-P1-003 Requirement Status Sync
- [ ] Validate requirement checkbox/status alignment with implementation and progress logs.
- [ ] Validate all completed milestones have supporting evidence links.

## 3. Governance Tests (P2)

### QDR-P2-001 Documentation/Workflow Alignment
- [ ] Validate `tests/README.md` CI behavior matches real workflow files.
- [ ] Validate references to non-existent workflows are removed or marked planned.

### QDR-P2-002 Localization Plan Closure
- [ ] Validate real-time language switching checklist items against runtime behavior.
- [ ] Validate restart persistence and converter/tooltip updates.
- [ ] Validate no localization-related binding warnings in build output.

## 4. Regression Safety

- [ ] Validate no regressions in existing targeted local commands:
  - `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~TemplateServiceTests|FullyQualifiedName~SelectTemplateStepTests|FullyQualifiedName~InstallWizardWorkflowViewModelTests|FullyQualifiedName~ReviewStepTests"`
- [ ] Validate Pester suite still passes for PowerShell module paths.
- [ ] Validate CI artifact outputs remain available for csharp and powershell test jobs.
