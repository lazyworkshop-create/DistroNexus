# v2.1.1 P1 - Acceptance Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) Phase 2 (P1).

## 1. Acceptance Criteria Mapping

### AC-P1-01 DevOps Category Completion (FR-2.1)
- [x] Given template catalog is loaded,
- [x] when querying `DevOps` category,
- [x] then `infra-cli-toolbox` is discoverable with valid metadata and script assets.

### AC-P1-02 Category Parity Closure (FR-2.1)
- [x] Given architecture/spec/checklist docs are reviewed,
- [x] when category coverage is checked,
- [x] then `DevOps` parity gap is explicitly closed or formally deferred with owner/milestone.

### AC-P1-03 Diagnostic Cmdlet Availability (FR-2.2)
- [x] Given module import is successful,
- [x] when diagnostic cmdlet is invoked,
- [x] then WSL/systemd/GPU/container prerequisites are reported in machine-readable output.

### AC-P1-04 Diagnostic Reusability (FR-2.2)
- [x] Given manual and automation workflows,
- [x] when diagnostics are consumed,
- [x] then the same capability semantics are used without contradictory classification.

### AC-P1-05 Capability Profile Presets (FR-2.3)
- [x] Given automation runner is executed with `CpuOnly` / `GpuCapable` / `SystemdCapable`,
- [x] when template checks run,
- [x] then gating behavior is deterministic and backward compatible with existing parameters.

### AC-P1-06 Artifact Traceability (FR-2.3)
- [x] Given a profile-based automation run,
- [x] when artifacts are generated,
- [x] then selected profile and blocked/fail reasons are traceable in summary/manifest outputs.

## 2. Evidence Requirements
- [x] Evidence link for template catalog update (`infra-cli-toolbox`).
- [x] Evidence link for script asset path and execution validation.
- [x] Evidence link for diagnostic cmdlet invocation samples and output contract.
- [x] Evidence link for automation runs per capability profile.
- [x] Evidence link for updated docs showing DevOps parity closure.

## 3. Exception Handling
- [x] Any unmet item is marked with explicit reason.
- [x] Deferred items include Owner and Milestone.
- [x] Deferred items include follow-up issue/task reference.

## 4. Final Sign-off
- Engineering sign-off: [x] Complete
- QA sign-off: [x] Complete
- Release sign-off: [ ] Complete
- Final result: [ ] Pass  [x] Pass with Exceptions  [ ] Fail

## 5. Exception Detail
- Owner: `Release Manager`
- Milestone: `v2.1.1-runtime-validation`
- Follow-up: Execute real WSL apply/idempotency validation for `infra-cli-toolbox` on a WSL-enabled host and attach evidence links.
