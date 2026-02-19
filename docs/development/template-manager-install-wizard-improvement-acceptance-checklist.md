# Template Manager -> Install Wizard Improvement - Acceptance Checklist

## 1. Acceptance Criteria Mapping

### AC-01 Intent Preservation
- [x] Given user clicks Install on template A from Template Manager,
- [x] When wizard opens,
- [x] Then template A is already selected in wizard context.

### AC-02 Review Clarity
- [x] Given template apply is enabled,
- [x] When user reaches Review step,
- [x] Then selected template summary is visible (name + category + optional descriptor).

### AC-03 Generic Flow Integrity
- [x] Given wizard opens from dashboard/package manager,
- [x] Then behavior remains unchanged from current baseline generic flow.

### AC-04 Fallback Safety
- [x] Given startup template ID is invalid,
- [x] Then wizard opens in generic mode with warning and no crash.

### AC-05 Test Coverage
- [x] Unit tests cover startup payload parsing and context assignment.
- [x] Workflow/integration tests cover with-template and generic entry modes.
- [x] UI automation verifies Template Manager -> Install initial template state.

## 2. Functional Acceptance Gates

- [x] Template Manager install action passes selected template identity to wizard startup.
- [x] Wizard initializes `SelectedTemplate` and `ApplyTemplateAfterInstall` for valid payload.
- [x] Select Template step reflects preselected template and allows user edits in current phase.
- [x] Review step always presents explicit template state (`Selected template` or `No template`).

## 3. UX Acceptance Gates

- [x] Existing wizard structure/navigation remains unchanged.
- [x] No extra pages or unnecessary dialogs are introduced.
- [x] Incompatibility guidance is clear and actionable.
- [x] Critical labels and status text are localized.

## 4. Stability and Compatibility Gates

- [x] Existing non-template entry points work with no payload changes.
- [x] Existing preselected distribution path is unaffected.
- [x] Invalid template payload degrades gracefully without blocking wizard startup.
- [x] Error logs include warning-level diagnostics for invalid/missing template payload.

## 5. Evidence Required for Sign-off

- [x] Unit test results attached (command + pass/fail summary).
- [x] UI automation results attached for template-first initial state and review-state assertions.
- [x] Manual validation notes attached for incompatibility guidance and localization checks.
- [x] Product/QA sign-off recorded for AC-01 to AC-05.
