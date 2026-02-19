# Template Manager to Install Wizard Flow Improvement Requirements

Date: 2026-02-19
Status: Draft for implementation
Scope: Desktop client (WPF) workflow and UX consistency

## 1. Background

Current behavior allows users to click Install from Template Manager, but the selected template is not passed into the install wizard context. The wizard opens as a generic flow and asks for template selection again later.

This creates a mismatch between user intent at the entry point and the actual wizard state.

## 2. Problem Statement

When the user starts from Template Manager:

- The Install action does not pre-bind the selected template to wizard context.
- The wizard entry experience is not template-centric, despite user starting from a template card.
- Review step does not clearly show selected template summary before execution.
- Compatibility guidance is delayed to later wizard interaction.

Result: redundant decisions, weaker predictability, and avoidable cognitive load.

## 3. Goals

1. Preserve user intent from Template Manager entry.
2. Reduce duplicated template selection decisions.
3. Improve pre-execution visibility of template choice and impact.
4. Keep changes minimal and consistent with existing wizard architecture.

## 4. Non-Goals

- No redesign of full wizard UI framework.
- No change to template execution engine behavior.
- No change to template metadata schema for this phase.
- No new pages or modal flows outside existing wizard.

## 5. Target User Flow

### 5.1 Entry from Template Manager

1. User clicks Install on a specific template card.
2. Wizard opens with that template pre-selected in `WizardContext.SelectedTemplate`.
3. User continues distro/path/user configuration.
4. Template selection step either:
   - shows the preselected template as selected and editable, or
   - is skipped when preselection lock mode is enabled (future option).
5. Review step displays template summary.
6. Install + template apply executes as current workflow.

### 5.2 Entry from Other Entrances (unchanged)

- Opening wizard from dashboard/package manager remains generic and starts without preselected template unless explicitly provided.

## 6. Functional Requirements

### FR-01 Template Intent Propagation

- Triggering Install from Template Manager MUST pass the selected template identity into wizard startup context.
- The wizard MUST initialize with `SelectedTemplate` set and `ApplyTemplateAfterInstall = true`.

### FR-02 Wizard Startup Contract

- Wizard startup MUST support optional startup payload:
  - `TemplateId` (optional)
  - `SelectedDistributionId` (optional, existing behavior compatibility)
- If payload template is invalid or unavailable, wizard MUST continue in generic mode and show non-blocking warning.

### FR-03 Template Selection Step Consistency

- If a valid template is preselected, SelectTemplateStep MUST render it as current selection.
- User MAY change or clear the selection unless entry mode is explicitly locked (not required in this phase).

### FR-04 Review Visibility

- Review step MUST display template summary when template apply is enabled:
  - Template name
  - Category
  - Optional short descriptor (for example, package/script count if available)
- If template is skipped, review step MUST show explicit “No template” state.

### FR-05 Compatibility Feedback Timing

- For Template Manager entry, compatibility checks SHOULD happen as early as possible after distro selection.
- If incompatibility is detected, user MUST receive clear guidance and be able to switch template or skip template.

### FR-06 Status Messaging

- Status text and critical labels involved in this flow SHOULD use localized resources instead of hardcoded text where applicable.

## 7. UX Requirements

- Keep the existing wizard structure and navigation model.
- Do not introduce extra pages.
- Do not add additional confirmation dialogs unless failure or incompatibility occurs.
- Preserve current visual style and component library usage.

## 8. Technical Requirements

### 8.1 Desktop Layer

- Introduce a lightweight wizard startup request model (or equivalent parameter passing approach).
- Update Template Manager install command to call wizard with startup request.
- Ensure `InstallWizardWorkflowViewModel.InitializeAsync` consumes startup request before step navigation starts.

### 8.2 Wizard Context

- Reuse existing `WizardContext.SelectedTemplate` and `WizardContext.ApplyTemplateAfterInstall`.
- Avoid duplicate context fields for the same concept.

### 8.3 Backward Compatibility

- Existing entry points that open generic wizard MUST continue to work without template payload.
- Existing package-manager preselected distribution flow MUST remain intact.

## 9. Error Handling Requirements

- Missing template ID: log warning, continue generic flow.
- Template loading failure: show step-level error guidance without crashing wizard.
- Incompatible template: block final validation until user resolves by changing template or skipping.

## 10. Acceptance Criteria

### AC-01 Intent Preservation

- Given user clicks Install on template A from Template Manager,
- When wizard opens,
- Then template A is already selected in wizard context.

### AC-02 Review Clarity

- Given template apply is enabled,
- When user reaches Review step,
- Then selected template summary is visible.

### AC-03 Generic Flow Integrity

- Given wizard opened from dashboard/package manager,
- Then flow behavior remains unchanged from current baseline.

### AC-04 Fallback Safety

- Given template ID is invalid,
- Then wizard opens in generic mode with warning and no crash.

### AC-05 Test Coverage

- Unit tests cover startup payload parsing and context assignment.
- Workflow tests cover both with-template and generic entry modes.
- UI automation verifies Template Manager -> Install opens wizard with expected initial template state.

## 11. Suggested Implementation Phases

### Phase 1 (P0)

- Implement startup payload propagation from Template Manager.
- Preselect template in wizard context.
- Show template summary in Review step.
- Add/adjust core unit tests.

### Phase 2 (P1)

- Improve earlier compatibility guidance after distro selection.
- Complete localization cleanup of hardcoded flow strings.
- Add targeted UI automation assertions for preselection and review rendering.

## 12. Risks and Mitigations

- Risk: Introducing startup payload may break existing wizard opening calls.
  - Mitigation: optional payload with safe defaults.

- Risk: Template object resolution timing mismatch with step loading.
  - Mitigation: resolve and set context before first step enters.

- Risk: Review step data binding drift.
  - Mitigation: add explicit tests for review view model fields.

## 13. Validation Checklist

- [ ] Template Manager install passes selected template into wizard startup.
- [ ] Wizard initializes context with selected template and apply flag.
- [ ] Review step shows selected template summary.
- [ ] Generic wizard entry still works unchanged.
- [ ] Invalid template startup payload gracefully degrades.
- [ ] Unit and UI automation tests updated and passing.
