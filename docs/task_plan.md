# Task Log

Date: 2026-02-19

## Active Milestone
- Template toggle semantics refinement and ScriptPath sibling-template staging fix

## Plan
1. Analyze latest install/template logs and confirm remaining ScriptPath runtime failure root cause.
2. Fix `TemplateService` staged bash execution to preserve template-relative sibling references (for multi-version templates).
3. Update Select Template step from skip-template to use-template semantics and remove duplicate action control.
4. Apply requested defaults: user/password toggle OFF by default; template toggle ON only for template-entry startup and OFF for package/home startup.
5. Remove Review step toggles and keep backend post-install options disabled.
6. Update regression tests and run targeted .NET tests.

## Status
- Completed

## Completion Notes
- Root cause confirmed from logs: staged script location (`script/`) broke relative sibling template references like `../nodejs-dev/install.sh` / `../dotnet-dev/install.sh`.
- Updated `TemplateService` staging to preserve full template-root relative layout, including sibling template directories and `common` helpers, with normalized line endings.
- Replaced skip-template semantics with use-template semantics in `SelectTemplateStep` and view; moved toggle to search row, disabled search/filter/list when OFF, removed duplicate details-side action button.
- Updated defaults to requested behavior: `CreateUser` default OFF; template apply default OFF unless startup payload contains a template (`TemplateId`).
- Removed Review toggles from UI and forced backend `SetAsDefault=false`, `LaunchAfterInstall=false` on review entry/quick defaults.
- Validation passed: targeted tests (`TemplateServiceTests`, `SelectTemplateStepTests`, `InstallWizardWorkflowViewModelTests`, `ReviewStepTests`) -> `22 passed, 0 failed`.

## Active Milestone
- Template selection two-subflow refactor and one-screen review layout

## Plan
1. Split template selection into two steps: basic template selection and advanced options.
2. Add conditional skip behavior so advanced options step appears only when selected template has version options.
3. Reorganize review page layout to keep summary and switches visible in one screen.
4. Update wizard-related tests and run targeted/full .NET regression suites.

## Status
- Completed

## Completion Notes
- Added `TemplateOptionsStep` as a dedicated second subflow for advanced option selection.
- Updated workflow navigation/indicator to support context-driven step skipping and dynamic step numbering.
- Kept first template step focused on template selection and basic info display.
- Reworked review page into a compact single-card layout with switches integrated below summary.
- Validation passed: targeted wizard tests and full suite (`228 passed, 0 failed`).

Date: 2026-02-19

## Active Milestone
- PowerShell template ScriptPath CRLF normalization fix and full install-flow revalidation

## Plan
1. Complete remaining `TemplateServiceTests` refactor for staged-script execution assertions.
2. Run targeted and full .NET test suites.
3. Execute real backend install + template apply flow and capture latest runtime evidence.
4. If runtime flow still fails, patch the concrete failing execution path and re-run end-to-end validation.

## Status
- Completed

## Completion Notes
- Updated tests to validate staged bash file execution instead of legacy inline base64 command assumptions.
- Confirmed .NET suite status: `226 passed, 0 failed`.
- Full runtime flow exposed additional PowerShell module path issue in `Apply-DistroNexusTemplate` when running `ScriptPath` templates with CRLF files.
- Patched PowerShell-side bash execution to normalize line endings/BOM and stage script + adjacent `common` files using UTF-8 (no BOM) before WSL execution.
- Re-ran full install + template apply (`nodejs-dev`) on a fresh instance; installation, template apply, and post-check all succeeded.

Date: 2026-02-19

## Active Milestone
- Template ScriptPath execution context fix and end-to-end install validation

## Plan
1. Analyze latest runtime logs after CRLF fix to identify remaining template apply failure root cause.
2. Fix ScriptPath bash execution so relative script includes work under WSL and normalize sourced shell files.
3. Add regression tests for ScriptPath execution strategy.
4. Run targeted and full .NET test suites.
5. Execute a full backend install + template apply flow on a fresh WSL instance.

## Status
- Completed

## Completion Notes
- Latest root cause was ScriptPath execution via stdin: `BASH_SOURCE[0]` was unbound and `../common/lib.sh` resolved to an incorrect location.
- Implemented staged execution in temporary directory under WSL to preserve script-relative includes and normalize shell files via `sed 's/\r$//'`.
- Completed full backend flow validation on fresh instance with successful install and Node.js template application.

Date: 2026-02-19

## Active Milestone
- Template apply bash CRLF line-ending compatibility fix

## Plan
1. Analyze latest runtime logs for the new failure after quote-transport patch.
2. Fix bash script execution to normalize Windows CRLF/BOM before WSL execution.
3. Add regression test for CRLF script payload path.
4. Run targeted and full .NET regression tests.
5. Perform command-level runtime validation in real WSL instance.

## Status
- Completed

## Completion Notes
- Latest runtime failure changed to `bash: line 2: set: pipefail ... invalid option name`, indicating CRLF contamination in bash payload.
- Implemented LF normalization and BOM stripping before Base64 transport execution.
- Verified through tests and command-level WSL runtime validation.

Date: 2026-02-19

## Active Milestone
- Template apply bash command quoting/format-exception fix

## Plan
1. Analyze runtime logs and isolate template-stage failure root cause in command construction path.
2. Replace inline `bash -c '...'` transport with a quote-safe command strategy for template scripts and preflight checks.
3. Add/update automated tests to assert script content is preserved and executed through the new transport path.
4. Run targeted and full .NET automated tests.
5. Perform command-level runtime verification against a real WSL instance.

## Status
- Completed

## Completion Notes
- Root cause confirmed: inline bash script embedding in PowerShell command string could trigger parsing/formatting failures when script content contains complex quote/brace patterns.
- Implemented Base64 transport execution for template bash scripts and preflight checks.
- Verified by automated tests and runtime command validation against installed distro.

Date: 2026-02-19

## Active Milestone
- Download progress/speed implementation verification and checklist closure

## Plan
1. Add automated tests for `DownloadService` byte-level progress reporting.
2. Add automated tests for `DownloadTaskManager` speed/throttle behavior.
3. Add automated tests for file size formatter conversion outputs.
4. Add UI automation tests for Package Manager download start/during/completion flow.
5. Run targeted tests and full regression test suite.
6. Update implementation/test/acceptance checklists based on verified evidence.

## Status
- Completed

## Completion Notes
- Root cause finalized as download URL property mismatch (`Url` vs `DownloadUrl`) plus missing `Success` check on download summary.
- Verified by automated tests and real runtime installation command path.

---

Date: 2026-02-19

## Active Milestone
- Install auto-download catalog default-name fix and error mapping regression validation

## Plan
1. Analyze latest installation logs and confirm the exact root cause of package download failure.
2. Fix `Install-DistroNexusInstance` auto-download mapping to use catalog `DefaultName` and robust package lookup conditions.
3. Refine `WslManagerService` user-friendly error mapping to distinguish catalog/package metadata failures from generic network failures.
4. Add PowerShell and C# automated regression tests for the new failure modes.
5. Run targeted and full automated test suites to confirm no regressions.

## Status
- Completed

---

Date: 2026-02-19

## Active Milestone
- Template Manager to Install Wizard flow improvement requirements definition

## Plan
1. Analyze current Template Manager -> Install Wizard entry path and context propagation behavior.
2. Identify UX and workflow consistency gaps for template-first install entry.
3. Produce a dedicated requirements document with goals, functional requirements, and acceptance criteria.
4. Record outputs in project tracking logs.

## Status
- Completed

---

Date: 2026-02-19

## Active Milestone
- Package Manager download UX refinement (progress alignment and same-file version merge)

## Plan
1. Adjust package item progress layout to keep vertical center alignment and add right-side spacing from cancel action.
2. Add same-file merge metadata on package model for UI display.
3. Merge package variants that map to the same underlying file in grouped package presentation.
4. Run regression tests to verify no functional breakage.

## Status
- Completed

---

Date: 2026-02-19

## Active Milestone
- Template Manager to Install Wizard flow improvement checklist generation

## Plan
1. Read requirements and existing checklist templates to align structure and scope.
2. Create implementation checklist for wizard startup payload propagation and UX consistency updates.
3. Create test checklist covering unit, workflow, UI automation, and regression verification.
4. Create acceptance checklist mapped to AC-01 ~ AC-05 and sign-off evidence.
5. Record outputs and conclusions in tracking logs.

## Status
- Completed

---

Date: 2026-02-19

## Active Milestone
- Template Manager to Install Wizard flow improvement implementation and validation

## Plan
1. Implement wizard startup payload model and backward-compatible startup contract.
2. Wire Template Manager install action to pass selected template into wizard startup request.
3. Update wizard initialization to resolve payload (template + optional distribution) with safe fallback and warning messaging.
4. Update Select Distribution / Select Template / Review steps for compatibility guidance and template summary visibility.
5. Add or update tests for startup payload parsing, context assignment, and review state rendering behavior.
6. Run targeted and full test suites; fix relevant failures.
7. Update checklist statuses and project tracking logs after verification.

## Status
- Completed

---

Date: 2026-02-19

## Active Milestone
- Select Template UI overlap fix and advanced options activation

## Plan
1. Fix Select Template details panel overlap by correcting empty-state visibility logic.
2. Implement version option selection model in Select Template step and bind advanced options controls.
3. Persist selected template options into wizard context and pass variables to template apply execution.
4. Add/update unit tests for advanced option assignment behavior.
5. Run targeted and full test suite to ensure no regressions.

## Status
- Completed

---

Date: 2026-02-19

## Active Milestone
- Review-step toggle overlap fix and template apply error sanitization

## Plan
1. Investigate Review step layout to identify why option toggles become obscured after template summary expansion.
2. Make Review step content scrollable to keep bottom toggles reachable regardless of content height.
3. Investigate template apply failure path and identify CLIXML/unapproved-verb noise leakage in user-facing errors.
4. Sanitize script error output at PowerShell execution layer while preserving non-zero exit failure behavior.
5. Add regression test for sanitized script failure message.
6. Run targeted and full test suite validation.
7. Update tracking logs with outcomes and evidence.

## Status
- Completed

---

Date: 2026-02-19

## Active Milestone
- Template apply transient distro-not-found retry fix

## Plan
1. Read runtime logs and identify the exact failing command and error content.
2. Confirm whether failure is caused by template bash execution against a not-yet-visible WSL instance.
3. Add targeted retry logic for `There is no distribution with the supplied name` in template bash execution path.
4. Add regression test for retry-success scenario.
5. Run targeted and full test suite validation.

## Status
- Completed

---

Date: 2026-02-19

## Active Milestone
- Install module False-output safety fix and end-to-end regression

## Plan
1. Read latest runtime logs for repeated failure and verify whether install stage is misclassified as success.
2. Confirm PowerShell module install command returns boolean output and map failure semantics in C# service.
3. Fix installation success criteria to treat module output `False` as failure even with exit code 0.
4. Add integration regression test for false-output path.
5. Run targeted and full automated test suites.

## Status
- Completed

---

Date: 2026-02-19

## Active Milestone
- UI automation screenshot baseline validation setup

## Plan
1. Review existing UI automation infrastructure and identify reusable app-launch/navigation hooks.
2. Implement screenshot baseline verifier with update mode and pixel-diff assertion output.
3. Add a minimal screenshot regression test case for stable pages (main window and templates page).
4. Document runbook for baseline generation and validation commands.
5. Execute targeted screenshot test in baseline update mode to validate end-to-end flow.

## Status
- Completed

## Completion Notes
- Added `ScreenshotVerifier` utility with baseline update switch (`DISTRONEXUS_UI_AUTOMATION_UPDATE_BASELINES`) and diff artifact output.
- Added `Screenshot_Regression_Main_And_Templates_Page` UI automation test in `TemplateUiAutomationSmokeTests`.
- Added baseline folder and guide: `src/Client/DistroNexus.Tests/UIAutomation/Baselines/README.md`.
- Added operation manual: `docs/development/ui-automation-screenshot-validation.md`.
- Validated with real run: `DISTRONEXUS_RUN_UI_AUTOMATION=1` + baseline update mode; test passed and screenshots were generated.
