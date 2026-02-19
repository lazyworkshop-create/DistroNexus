# Template Manager -> Install Wizard Improvement - Implementation Checklist

Based on [Template Manager to Install Wizard Flow Improvement Requirements](../specs/template-manager-install-wizard-improvement-requirements.md).

## 1. Wizard Startup Payload (`DistroNexus.Core` / Desktop workflow boundary)

### 1.1 Startup Request Model
- [x] Add a lightweight wizard startup request model (or equivalent parameter object).
- [x] Include optional `TemplateId` in startup request.
- [x] Keep optional `SelectedDistributionId` behavior compatible with existing entry points.
- [x] Ensure safe defaults when payload is not provided.

### 1.2 Startup Contract Integration
- [x] Update wizard startup API to accept optional startup payload.
- [x] Ensure existing callers compile and run without payload changes.
- [x] Add null/empty guard handling for startup payload fields.

## 2. Template Manager Entry (`DistroNexus.Desktop`)

### 2.1 Install Command Propagation
- [x] Update Template Manager install action to pass selected template identity into wizard startup request.
- [x] Keep current navigation flow unchanged except context propagation.
- [x] Log warning when selected template identity cannot be resolved before launch.

## 3. Wizard Initialization and Context (`InstallWizardWorkflowViewModel`)

### 3.1 Context Initialization
- [x] Consume startup payload in `InitializeAsync` before first step navigation.
- [x] Set `WizardContext.SelectedTemplate` when payload template is valid.
- [x] Set `WizardContext.ApplyTemplateAfterInstall = true` for valid template payload.

### 3.2 Fallback Safety
- [x] If template ID is invalid/unavailable, continue in generic mode.
- [x] Show non-blocking warning for invalid template payload.
- [x] Record warning logs with enough diagnostic context.

## 4. Select Template Step Consistency

- [x] Ensure preselected template is rendered as current selection.
- [x] Keep selection editable (change/clear) in this phase.
- [x] Keep future lock-mode behavior out of current implementation scope.

## 5. Review Step Visibility

- [x] Add template summary section when `ApplyTemplateAfterInstall` is enabled.
- [x] Display template name.
- [x] Display template category.
- [x] Display optional short descriptor when data is available.
- [x] Display explicit `No template` state when template apply is not enabled.

## 6. Compatibility Feedback Timing

- [x] Trigger compatibility evaluation as early as possible after distro selection for template-manager entry.
- [x] Provide clear guidance for incompatible combinations.
- [x] Allow user to resolve by changing template or skipping template.
- [x] Block final validation until incompatibility is resolved.

## 7. Localization and Messaging

- [x] Replace new/affected hardcoded status or critical label text with localized resources.
- [x] Keep message tone consistent with existing wizard copy.

## 8. Backward Compatibility

- [x] Verify dashboard/package manager generic entry remains unchanged.
- [x] Verify existing preselected distribution flow remains intact.
- [x] Ensure no behavior change when startup payload is omitted.

## 9. Completion Snapshot

- [x] P0 scope complete (intent propagation, context preselection, review summary, core tests).
- [x] P1 scope complete (earlier compatibility guidance, localization cleanup, targeted UI automation).
