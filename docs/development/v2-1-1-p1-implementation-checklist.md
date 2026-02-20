# v2.1.1 P1 - Implementation Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) Phase 2 (P1).

## 1. Scope
- FR-2.1 DevOps Category Completion
- FR-2.2 Environment Diagnostic Cmdlet
- FR-2.3 Capability Profile Presets

## 2. FR-2.1 DevOps Category Completion

### 2.1 Catalog and Metadata
- [ ] Add built-in template entry `infra-cli-toolbox` in `config/templates.json`.
- [ ] Set `Category` to `DevOps` and define `ScenarioTags` for discoverability.
- [ ] Define `InstallMode`, `CompatibleDistros`, `EstimatedDurationMinutes`, and `DefaultSelections`.
- [ ] Ensure metadata fields satisfy current template schema and loading logic.

### 2.2 Script Assets
- [ ] Add script assets under `config/templates/infra-cli-toolbox/`.
- [ ] Ensure script path references are relative and remain under allowed roots.
- [ ] Ensure scripts are idempotent and include clear failure guidance.
- [ ] Ensure script execution timeout settings are defined in metadata.

### 2.3 Documentation Sync
- [ ] Update category distribution references in architecture/development docs where required.
- [ ] Mark `DevOps` category parity gap as resolved in active checklist/spec references.
- [ ] Add usage guidance for the new template in user-facing docs where applicable.

## 3. FR-2.2 Environment Diagnostic Cmdlet

### 3.1 Command Surface
- [ ] Create new cmdlet (target name: `Test-DistroNexusTemplateEnvironment`) in `src/PowerShell/Public/`.
- [ ] Export cmdlet in `src/PowerShell/DistroNexus.psd1`.
- [ ] Provide parameter set for target distro and optional capability scope.

### 3.2 Diagnostic Capabilities
- [ ] Implement WSL availability/version checks.
- [ ] Implement systemd capability checks.
- [ ] Implement GPU capability checks.
- [ ] Implement container runtime prerequisite checks.

### 3.3 Output Contract
- [ ] Return machine-readable status object with stable fields (`Capability`, `Status`, `Reason`, `Details`).
- [ ] Support non-terminating diagnostics for mixed-capability environments.
- [ ] Ensure output is consumable by both manual calls and automation runner.

## 4. FR-2.3 Capability Profile Presets

### 4.1 Runner Parameters
- [ ] Add preset parameter to `Invoke-DistroNexusTemplateAutomation` (e.g., `-CapabilityProfile`).
- [ ] Support profiles: `CpuOnly`, `GpuCapable`, `SystemdCapable`.
- [ ] Preserve backward compatibility with existing runner parameters.

### 4.2 Gating Mapping
- [ ] Define explicit mapping from each preset to gating behavior.
- [ ] Ensure mapping is deterministic and documented in runner help/comments.
- [ ] Ensure blocked reasons include profile-based explanation.

### 4.3 Integration with Diagnostics
- [ ] Reuse diagnostic cmdlet or shared checks instead of duplicating capability logic.
- [ ] Ensure profile decision and raw capability checks are traceable in run artifacts.

## 5. Cross-Cutting Quality Tasks
- [ ] Add/adjust Pester tests for new cmdlet and runner profile behavior.
- [ ] Add/adjust C# or integration-level tests if command contract affects desktop/service integration.
- [ ] Ensure no regressions in existing template apply and automation modes.
- [ ] Update docs for new command examples and profile usage.

## 6. Exit Gates (Implementation)
- [ ] FR-2.1 implemented with template metadata + script assets complete.
- [ ] FR-2.2 cmdlet implemented and exported with stable output contract.
- [ ] FR-2.3 profile presets implemented with deterministic gating behavior.
- [ ] Related docs updated and linked.

## 7. Ownership and Status
- Implementation Owner: [ ] Assigned
- Reviewer: [ ] Assigned
- Status: [ ] Not Started  [ ] In Progress  [ ] Complete
