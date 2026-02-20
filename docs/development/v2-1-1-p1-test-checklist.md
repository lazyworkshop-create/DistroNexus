# v2.1.1 P1 - Test Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) Phase 2 (P1).

## 1. Test Scope
- FR-2.1 DevOps Category Completion
- FR-2.2 Environment Diagnostic Cmdlet
- FR-2.3 Capability Profile Presets

## 2. FR-2.1 DevOps Template Tests

### 2.1 Metadata and Discovery
- [ ] Validate `config/templates.json` parses successfully with new `infra-cli-toolbox` entry.
- [ ] Validate `Get-DistroNexusTemplate -Category DevOps` returns expected template.
- [ ] Validate tag-based filtering (if enabled from P0/P1 command surface) can discover template.

### 2.2 Script and Safety
- [ ] Validate referenced script files exist and resolve under allowed path roots.
- [ ] Validate no absolute path or traversal behavior is introduced.
- [ ] Validate template script rerun behavior is idempotent in local WSL test environment.

### 2.3 Functional Apply
- [ ] Run selected-template apply test for `infra-cli-toolbox` on supported distro.
- [ ] Validate expected toolchain baseline checks succeed after apply.
- [ ] Validate failures provide actionable error messages.

## 3. FR-2.2 Diagnostic Cmdlet Tests

### 3.1 Command Contract
- [ ] Validate cmdlet can be imported and invoked from module manifest export.
- [ ] Validate output object contains stable required fields for each capability item.
- [ ] Validate command exits safely (non-crash) when capability is absent.

### 3.2 Capability Matrix
- [ ] Validate WSL prerequisite diagnostics in normal environment.
- [ ] Validate systemd capability diagnostics in both available/unavailable scenarios.
- [ ] Validate GPU capability diagnostics in both available/unavailable scenarios.
- [ ] Validate container prerequisite diagnostics.

### 3.3 Machine-Readable Behavior
- [ ] Validate output can be piped/filtered in PowerShell without custom parsing.
- [ ] Validate status values are deterministic (`Pass`, `Fail`, `Blocked`, or documented equivalent).

## 4. FR-2.3 Profile Preset Tests

### 4.1 Parameter Compatibility
- [ ] Validate automation runner accepts each profile value.
- [ ] Validate existing runner parameter combinations remain functional.
- [ ] Validate unknown profile values are rejected with clear diagnostics.

### 4.2 Gating Behavior
- [ ] Validate `CpuOnly` blocks GPU-gated scenarios with explicit reason.
- [ ] Validate `GpuCapable` enables GPU checks and classifies missing capability correctly.
- [ ] Validate `SystemdCapable` behavior for systemd-dependent template checks.

### 4.3 Artifact and Summary
- [ ] Validate run artifacts capture chosen profile and gating decisions.
- [ ] Validate blocked/fail reason text reflects profile-context accurately.

## 5. Regression and Integration Tests
- [ ] Validate existing `AllTemplates` and `SelectedTemplates` modes still run.
- [ ] Validate template automation artifact generation remains intact (XML/JSON/summary/logs).
- [ ] Validate no regression in legacy template categories and apply flow.

## 6. Recommended Test Evidence
- [ ] Attach command transcripts for diagnostic cmdlet examples.
- [ ] Attach at least one selected-run summary including profile behavior.
- [ ] Attach before/after category distribution evidence showing DevOps coverage.

## 7. Sign-off
- Test Owner: [ ] Assigned
- Reviewer: [ ] Assigned
- Result: [ ] Pass  [ ] Pass with Exceptions  [ ] Fail
