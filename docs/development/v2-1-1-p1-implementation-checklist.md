# v2.1.1 P1 - Implementation Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) Phase 2 (P1).

## 1. Scope
- FR-2.1 DevOps Category Completion
- FR-2.2 Environment Diagnostic Cmdlet
- FR-2.3 Capability Profile Presets

## 2. FR-2.1 DevOps Category Completion

### 2.1 Catalog and Metadata
- [x] Add built-in template entry `infra-cli-toolbox` in `config/templates.json`.
- [x] Set `Category` to `DevOps` and define `ScenarioTags` for discoverability.
- [x] Define `InstallMode`, `CompatibleDistros`, `EstimatedDurationMinutes`, and `DefaultSelections`.
- [x] Ensure metadata fields satisfy current template schema and loading logic.

### 2.2 Script Assets
- [x] Add script assets under `config/templates/infra-cli-toolbox/`.
- [x] Ensure script path references are relative and remain under allowed roots.
- [x] Ensure scripts are idempotent and include clear failure guidance.
- [x] Ensure script execution timeout settings are defined in metadata.

### 2.3 Documentation Sync
- [x] Update category distribution references in architecture/development docs where required.
- [x] Mark `DevOps` category parity gap as resolved in active checklist/spec references.
- [x] Add usage guidance for the new template in user-facing docs where applicable.

## 3. FR-2.2 Environment Diagnostic Cmdlet

### 3.1 Command Surface
- [x] Create new cmdlet (target name: `Test-DistroNexusTemplateEnvironment`) in `src/PowerShell/Public/`.
- [x] Export cmdlet in `src/PowerShell/DistroNexus.psd1`.
- [x] Provide parameter set for target distro and optional capability scope.

### 3.2 Diagnostic Capabilities
- [x] Implement WSL availability/version checks.
- [x] Implement systemd capability checks.
- [x] Implement GPU capability checks.
- [x] Implement container runtime prerequisite checks.

### 3.3 Output Contract
- [x] Return machine-readable status object with stable fields (`Capability`, `Status`, `Reason`, `Details`).
- [x] Support non-terminating diagnostics for mixed-capability environments.
- [x] Ensure output is consumable by both manual calls and automation runner.

## 4. FR-2.3 Capability Profile Presets

### 4.1 Runner Parameters
- [x] Add preset parameter to `Invoke-DistroNexusTemplateAutomation` (e.g., `-CapabilityProfile`).
- [x] Support profiles: `CpuOnly`, `GpuCapable`, `SystemdCapable`.
- [x] Preserve backward compatibility with existing runner parameters.

### 4.2 Gating Mapping
- [x] Define explicit mapping from each preset to gating behavior.
- [x] Ensure mapping is deterministic and documented in runner help/comments.
- [x] Ensure blocked reasons include profile-based explanation.

### 4.3 Integration with Diagnostics
- [x] Reuse diagnostic cmdlet or shared checks instead of duplicating capability logic.
- [x] Ensure profile decision and raw capability checks are traceable in run artifacts.

## 5. Cross-Cutting Quality Tasks
- [x] Add/adjust Pester tests for new cmdlet and runner profile behavior.
- [x] Add/adjust C# or integration-level tests if command contract affects desktop/service integration.
- [x] Ensure no regressions in existing template apply and automation modes.
- [x] Update docs for new command examples and profile usage.

## 6. Exit Gates (Implementation)
- [x] FR-2.1 implemented with template metadata + script assets complete.
- [x] FR-2.2 cmdlet implemented and exported with stable output contract.
- [x] FR-2.3 profile presets implemented with deterministic gating behavior.
- [x] Related docs updated and linked.

## 7. Ownership and Status
- Implementation Owner: [x] Assigned (`Copilot implementation pass`)
- Reviewer: [ ] Assigned
- Status: [ ] Not Started  [ ] In Progress  [x] Complete

## 8. Evidence Notes
- Unit tests passed: `tests/PowerShell/TestRunner.ps1 -TestType Unit` (2026-02-20).
- Functional evidence (local):
	- `Get-DistroNexusTemplate -Category DevOps` => count `1`.
	- `Test-DistroNexusTemplateEnvironment -Capability Wsl` => machine-readable result generated.
	- `Invoke-DistroNexusTemplateAutomation -DryRun -CapabilityProfile CpuOnly` => blocked classification and summary artifact generated.
