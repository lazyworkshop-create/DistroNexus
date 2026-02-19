# Template Manager -> Install Wizard Improvement - Test Checklist

## 1. Unit Tests (`DistroNexus.Tests`)

### 1.1 Startup Payload Parsing and Contract
- [x] Verify wizard startup accepts null payload and keeps generic behavior.
- [x] Verify valid `TemplateId` is parsed and carried into initialization path.
- [x] Verify invalid/unknown `TemplateId` falls back to generic mode without exception.
- [x] Verify `SelectedDistributionId` compatibility path remains intact.

### 1.2 Wizard Context Assignment
- [x] Verify valid payload sets `WizardContext.SelectedTemplate`.
- [x] Verify valid payload sets `WizardContext.ApplyTemplateAfterInstall = true`.
- [x] Verify invalid payload does not set template fields and does not crash.

### 1.3 Review ViewModel Data
- [x] Verify review state contains template summary fields when template apply is enabled.
- [x] Verify review state shows explicit `No template` when template apply is disabled.
- [x] Verify optional descriptor rendering is null-safe.

### 1.4 Compatibility Validation Logic
- [x] Verify incompatibility is detected after distro selection for template-manager entry.
- [x] Verify final validation is blocked while incompatibility remains unresolved.
- [x] Verify switching template or skipping template clears blocking condition.

## 2. Workflow / Integration Tests

### 2.1 Template-First Entry
- [x] Simulate Template Manager -> Install for a valid template.
- [x] Verify wizard opens with expected template preselected.
- [x] Verify user can continue through config steps without re-selecting unless changed.

### 2.2 Generic Entry Integrity
- [x] Simulate dashboard/package manager entry without template payload.
- [x] Verify wizard remains generic and unchanged from baseline.

### 2.3 Invalid Payload Fallback
- [x] Simulate startup with invalid template ID.
- [x] Verify non-blocking warning appears and workflow remains usable.
- [x] Verify no unhandled exceptions are logged.

## 3. UI Automation Tests (Targeted)

### 3.1 Initial State Assertions
- [x] Verify template card Install opens wizard.
- [x] Verify selected template name appears in initial wizard state.

### 3.2 Review Step Assertions
- [x] Verify review step displays template name/category when template is selected.
- [x] Verify review step displays `No template` when template is cleared/skipped.

### 3.3 Compatibility UX Assertions
- [x] Verify incompatible template+distro combination shows guidance.
- [x] Verify completion action remains blocked until issue is resolved.

## 4. Manual Validation

### 4.1 Functional Walkthrough
- [x] Template Manager entry preserves intent end-to-end.
- [x] Template can be changed or cleared in template selection step.
- [x] Review page reflects current template state accurately.

### 4.2 Localization and Messaging
- [x] New/changed flow labels use resource-based localization.
- [x] Warning and guidance messages are user-friendly and non-blocking where required.

## 5. Regression Scope

- [x] Execute unit/integration suite for wizard and template-related modules.
- [x] Execute full client test suite before merge.
- [x] Record command outputs and pass/fail counts in progress log.
