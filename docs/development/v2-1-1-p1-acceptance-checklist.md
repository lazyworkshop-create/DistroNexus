# v2.1.1 P1 - Acceptance Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) Phase 2 (P1).

## 1. Acceptance Criteria Mapping

### AC-P1-01 DevOps Category Completion (FR-2.1)
- [ ] Given template catalog is loaded,
- [ ] when querying `DevOps` category,
- [ ] then `infra-cli-toolbox` is discoverable with valid metadata and script assets.

### AC-P1-02 Category Parity Closure (FR-2.1)
- [ ] Given architecture/spec/checklist docs are reviewed,
- [ ] when category coverage is checked,
- [ ] then `DevOps` parity gap is explicitly closed or formally deferred with owner/milestone.

### AC-P1-03 Diagnostic Cmdlet Availability (FR-2.2)
- [ ] Given module import is successful,
- [ ] when diagnostic cmdlet is invoked,
- [ ] then WSL/systemd/GPU/container prerequisites are reported in machine-readable output.

### AC-P1-04 Diagnostic Reusability (FR-2.2)
- [ ] Given manual and automation workflows,
- [ ] when diagnostics are consumed,
- [ ] then the same capability semantics are used without contradictory classification.

### AC-P1-05 Capability Profile Presets (FR-2.3)
- [ ] Given automation runner is executed with `CpuOnly` / `GpuCapable` / `SystemdCapable`,
- [ ] when template checks run,
- [ ] then gating behavior is deterministic and backward compatible with existing parameters.

### AC-P1-06 Artifact Traceability (FR-2.3)
- [ ] Given a profile-based automation run,
- [ ] when artifacts are generated,
- [ ] then selected profile and blocked/fail reasons are traceable in summary/manifest outputs.

## 2. Evidence Requirements
- [ ] Evidence link for template catalog update (`infra-cli-toolbox`).
- [ ] Evidence link for script asset path and execution validation.
- [ ] Evidence link for diagnostic cmdlet invocation samples and output contract.
- [ ] Evidence link for automation runs per capability profile.
- [ ] Evidence link for updated docs showing DevOps parity closure.

## 3. Exception Handling
- [ ] Any unmet item is marked with explicit reason.
- [ ] Deferred items include Owner and Milestone.
- [ ] Deferred items include follow-up issue/task reference.

## 4. Final Sign-off
- Engineering sign-off: [ ] Complete
- QA sign-off: [ ] Complete
- Release sign-off: [ ] Complete
- Final result: [ ] Pass  [ ] Pass with Exceptions  [ ] Fail
