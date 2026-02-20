# Progress Log

Date: 2026-02-20

## Active Milestone
- GitHub Actions pipeline stabilization and release-lane verification

## Progress
- Enabled authenticated GitHub CLI diagnostics and switched failure analysis from anonymous API polling to `gh run view` step/log inspection.
- Stabilized workflow execution paths in `.github/workflows/ci.yml`, `.github/workflows/test.yml`, and `.github/workflows/release.yml`:
	- Excluded `UIAutomation` lane from headless CI test runs.
	- Added robust Pester unit filter fallback behavior and result-publish guards.
	- Reduced non-critical publish/package noise impact in CI summary.
- Fixed CI-sensitive PowerShell tests:
	- Updated `Invoke-DistroNexusTemplateAutomation.Tests.ps1` to clear and restore `$env:CI` for execution-path assertions that must not be policy-skipped.
	- Updated `Get-DistroNexusInstance.Tests.ps1` to remove environment-dependent non-empty-instance assertion.
- Fixed flaky timing assertion in `DownloadTaskManagerProgressTests` to avoid CI scheduler variance failures.
- Fixed PowerShell integration mock behavior in `Apply-DistroNexusTemplate.Integration.Tests.ps1` by setting deterministic `wsl.exe` mock output and `LASTEXITCODE`.
- Validation results:
	- Latest `CI Build` run: `success` (run `22213808133`).
	- Latest `Integration Tests` run: `success` (run `22213808121`).
	- Manual `Release Build` workflow_dispatch (`version=2.1.0`): `success` (run `22214193152`).

Date: 2026-02-19

## Active Milestone
- Project quality remediation checklist synchronization and template-test hang mitigation

## Progress
- Reproduced template-focused test slice with hang diagnostics enabled and confirmed no active deadlock in current code path.
- Added CI hang safeguards to C# test commands in `.github/workflows/ci.yml`, `.github/workflows/test.yml`, and `.github/workflows/quick-test.yml` using `--blame-hang` and bounded hang timeout.
- Verified metadata-split execution paths after remediation:
	- `dotnet test ... --filter "TestScope!=Full"` -> `152 passed, 0 failed`
	- `dotnet test ... --filter "TestScope=Full&Category!=UIAutomation"` -> `72 passed, 0 failed`
	- `dotnet test ... --filter "FullyQualifiedName~TemplateServiceTests|FullyQualifiedName~SelectTemplateStepTests|FullyQualifiedName~InstallWizardWorkflowViewModelTests|FullyQualifiedName~ReviewStepTests"` -> `22 passed, 0 failed` (stable across repeated runs)
- Verified PowerShell regression baseline remains green:
	- `Get-Module Pester -ListAvailable; Invoke-Pester -Path tests/PowerShell -Output Detailed` -> passed (`ExitCode 0`).
- Synchronized remediation implementation/test/acceptance checklists with completed P0/P1-002/P1-003 status and explicit deferred ownership for remaining P1-001/P2 governance items.
- Post-freeze lightweight verification completed successfully:
	- `dotnet test ... FullyQualifiedName~TemplateServiceTests|...|ReviewStepTests --no-restore` -> `22 passed, 0 failed`
	- `dotnet build src/Client/DistroNexus.Desktop/DistroNexus.Desktop.csproj -c Debug --no-restore` -> `Build succeeded`

Date: 2026-02-19

## Active Milestone
- Template toggle semantics refinement and ScriptPath sibling-template staging fix

## Progress
- Updated `TemplateService` staged bash execution path to copy template-root directory structure recursively with LF normalization, then execute staged script using preserved relative path.
- Added/updated `TemplateServiceTests` coverage to validate sibling-template script reference availability in staged workspace.
- Updated `SelectTemplateStep` from `SkipTemplate` to `UseTemplate` semantics and removed duplicate "Do not use template" details action.
- Updated `SelectTemplateStepView` to place template toggle left of search box and disable selection/search/filter controls when toggle is OFF.
- Set wizard defaults to requested behavior in `WizardContext`: create-user toggle OFF by default; apply-template OFF by default.
- Updated startup behavior in `InstallWizardWorkflowViewModel` so template apply stays OFF unless startup request includes a valid template payload.
- Removed Review toggles from `ReviewStepView` and enforced backend OFF state in `ReviewStep`.
- Test results:
	- Targeted tests (`TemplateServiceTests`, `SelectTemplateStepTests`, `InstallWizardWorkflowViewModelTests`, `ReviewStepTests`): `22 passed, 0 failed`

## Active Milestone
- Template selection two-subflow refactor and one-screen review layout

## Progress
- Added new wizard step `TemplateOptionsStep` for advanced template options and inserted it after `SelectTemplateStep`.
- Updated `WizardWorkflow` to support conditional step skipping and dynamic indicator step numbering.
- Reduced `SelectTemplateStep` to template selection/basic information responsibilities only.
- Updated template selection UI to remove inline advanced option editors from the first step.
- Reorganized `ReviewStepView` into a compact one-card layout with both toggles inside the same screen region.
- Added `TemplateOptionsStepTests` and updated `SelectTemplateStepTests` for new responsibilities.
- Test results:
	- Targeted wizard tests: `15 passed, 0 failed`
	- Full .NET suite (`DistroNexus.Tests`): `228 passed, 0 failed`

Date: 2026-02-19

## Active Milestone
- PowerShell template ScriptPath CRLF normalization fix and full install-flow revalidation

## Progress
- Refactored `TemplateServiceTests` and `TemplateServiceIntegrationTests` to validate staged-script command behavior (new parser helpers for staged WSL paths) rather than legacy inline payload decoding.
- Updated `Apply-DistroNexusTemplate.ps1` bash execution path to normalize script content and stage `script` + sibling `common` files under temp root with UTF-8 (no BOM) writes.
- Added per-script staging cleanup in `finally` to avoid temp residue.
- Test results:
	- Targeted .NET (`TemplateServiceTests`): `9 passed, 0 failed`
	- Full .NET suite (`DistroNexus.Tests`): `226 passed, 0 failed`
	- Targeted Pester (`Apply-DistroNexusTemplate.Tests.ps1`): `2 passed, 0 failed, 2 skipped`
- Runtime validation:
	- Initial full flow run failed in PowerShell module path with `line 2: se: invalid option name` for `nodejs-dev/install.sh`.
	- After patch, reran full backend flow (`Install-DistroNexusInstance` + `Apply-DistroNexusTemplate -TemplateId nodejs-dev`) on fresh instance `dnx-e2e-0219171758`.
	- Install/import succeeded, template apply completed, Node.js runtime check succeeded, and instance cleanup completed.

Date: 2026-02-19

## Active Milestone
- Template ScriptPath execution context fix and end-to-end install validation

## Progress
- Updated `TemplateService` bash ScriptPath execution to stage template files in a WSL temp workspace (`/tmp/distronexus-template-*`) before execution.
- Added LF normalization for staged script and helper files (`sed 's/\r$//'`) to avoid CRLF-related bash option parsing failures.
- Preserved relative include behavior (`../common/lib.sh`) by staging both script directory and adjacent `common` directory.
- Added regression test `ApplyTemplateAsync_WithScriptPath_ExecutesViaTemporaryScriptInSourceDirectory`.
- Test results:
	- Targeted (`TemplateServiceTests` + `TemplateServiceIntegrationTests`): `13 passed, 0 failed`
	- Full .NET suite (`DistroNexus.Tests`): `226 passed, 0 failed`
- Full backend flow validation:
	- Fresh instance install succeeded: `ubuntu-e2e2-165702`
	- Node.js template apply succeeded (nvm install + node install logs completed)
	- Post-apply verification in fresh shell: `node v24.13.1`, `npm v11.8.0`

Date: 2026-02-19

## Active Milestone
- Template apply bash CRLF line-ending compatibility fix

## Progress
- Updated `TemplateService` bash execution path to normalize line endings from `CRLF/CR` to `LF` before Base64 transport.
- Added BOM stripping for bash payloads to avoid hidden first-character execution issues.
- Updated bash preflight command builder to reuse the same normalization path.
- Added regression test `ApplyTemplateAsync_WithCrLfScript_NormalizesLineEndingsBeforeExecution`.
- Test results:
	- Targeted (`TemplateServiceTests` + `TemplateServiceIntegrationTests`): `12 passed, 0 failed`
	- Full .NET suite (`DistroNexus.Tests`): `225 passed, 0 failed`
- Runtime validation:
	- Executed normalized CRLF-origin bash payload against real distro `ubuntu-2404-lts1241`.
	- Result output: `ok` (no `pipefail`/invalid option error).

Date: 2026-02-19

## Active Milestone
- Template apply bash command quoting/format-exception fix

## Progress
- Updated `TemplateService` bash script execution to use Base64 command transport: `wsl -d '<instance>' -- bash -lc "echo '<base64>' | base64 --decode | bash"`.
- Updated template preflight bash command builder to reuse the same Base64 transport path.
- Added/updated `TemplateServiceTests` assertions to decode command payload and verify script content after variable substitution.
- Updated `TemplateServiceIntegrationTests` command predicates to inspect decoded payload instead of raw inline script text.
- Test results:
	- Targeted (`TemplateServiceTests` + `TemplateServiceIntegrationTests`): `11 passed, 0 failed`
	- Full .NET suite (`DistroNexus.Tests`): `224 passed, 0 failed`
- Runtime verification:
	- Executed real command using new transport against existing distro `ubuntu-2404-lts12231`.
	- Command completed successfully and printed expected output with brace/quote-heavy script content.

Date: 2026-02-19

## Active Milestone
- Download progress/speed implementation verification and checklist closure

## Progress
- Added `DownloadServiceTests` to validate byte-level progress reporting, unknown content length behavior, and empty file handling.
- Added `DownloadTaskManagerProgressTests` to validate speed calculation, throttle interval behavior, and stalled transfer speed reporting.
- Added `FileSizeFormatterTests` to validate formatting outputs (`1024`, `1048576`, `0`).
- Added `PackageManagerUiAutomationTests` for Package Manager page navigation and download start/during/completion UI flow.
- Added deterministic UI automation support via `DISTRONEXUS_UI_AUTOMATION_FAKE_DOWNLOAD` and control automation IDs for Package Manager download controls.
- Executed targeted tests and full regression suite.
- Updated checklist documents with evidence-based completion states.

---

Date: 2026-02-19

## Active Milestone
- Install auto-download catalog default-name fix and error mapping regression validation

## Progress
- Updated `Install-DistroNexusInstance.ps1` to resolve and use catalog `DefaultName` (`catalogDefaultName`) when auto-downloading package files.
- Expanded package cache/download result lookup in install flow to include `DefaultName`, `Name`, and `Version` combinations.
- Updated `WslManagerService.ExtractUserFriendlyError` with explicit catalog/package metadata branches before generic network/download mapping.
- Added PowerShell regression test: `tests/PowerShell/Unit/Public/Install-DistroNexusInstance.Tests.ps1`.
- Added C# integration regressions in `WslManagerServiceIntegrationTests` for both catalog-not-found and downloaded-file-missing error paths.
- Fixed `Save-DistroNexusPackage.ps1` URL compatibility by supporting `DownloadUrl` fallback (in both single-download and batch job download paths) and filename fallback resolution.
- Fixed `Install-DistroNexusInstance.ps1` to fail fast when `Save-DistroNexusPackage` returns summary with `Success = $false`.
- Added PowerShell regression test for `DownloadUrl` fallback in `Invoke-PackageDownload`.
- Test results:
	- Targeted Pester (`Install-DistroNexusInstance.Tests.ps1`): `1 passed, 0 failed`
	- Targeted .NET (`WslManagerServiceIntegrationTests`): `14 passed, 0 failed`
	- Targeted Pester (`Save-DistroNexusPackage.Tests.ps1` + `Install-DistroNexusInstance.Tests.ps1`): `6 passed, 0 failed`
	- PowerShell Unit suite (`TestRunner -TestType Unit`): `96 total, 93 passed, 0 failed, 3 skipped`
	- Full .NET suite (`DistroNexus.Tests`): `224 passed, 0 failed`
	- Runtime install verification (`Install-DistroNexusInstance -AutoDownload`): `True` (successful download + import)

---

Date: 2026-02-19

## Active Milestone
- Template Manager to Install Wizard flow improvement requirements definition

## Progress
- Completed focused analysis for Template Manager entry into install wizard, including startup intent propagation and template selection consistency.
- Identified UX and workflow gaps around duplicated template decision points and review-stage visibility.
- Created a new requirements specification for implementation planning: `docs/specs/template-manager-install-wizard-improvement-requirements.md`.
- Documented phased priorities (P0/P1), acceptance criteria, and compatibility constraints for safe incremental implementation.

---

Date: 2026-02-19

## Active Milestone
- Package Manager download UX refinement (progress alignment and same-file version merge)

## Progress
- Updated package item action layout so the downloading progress block remains vertically centered.
- Added a fixed spacer column to keep downloading progress visuals from touching the right-side cancel button.
- Added same-file merge display metadata to `DistroPackage` (`IsSameFileMerged`, `SameFileTagText`).
- Implemented same-file package variant merge in `PackageManagerViewModel` grouping flow using SHA256/file-name/size key fallback strategy.
- Added merged-item visual tag (`Same file ×N`) in Package Manager list cards.
- Verified changes with `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj --nologo` (`211 passed, 0 failed`).

---

Date: 2026-02-19

## Active Milestone
- Template Manager to Install Wizard flow improvement checklist generation

## Progress
- Created implementation checklist: `docs/development/template-manager-install-wizard-improvement-implementation-checklist.md`.
- Created test checklist: `docs/development/template-manager-install-wizard-improvement-test-checklist.md`.
- Created acceptance checklist: `docs/development/template-manager-install-wizard-improvement-acceptance-checklist.md`.
- Mapped acceptance checklist directly to AC-01 through AC-05 from requirements.
- Added sign-off evidence section to support QA/Product acceptance closure.

---

Date: 2026-02-19

## Active Milestone
- Template Manager to Install Wizard flow improvement implementation and validation

## Progress
- Added startup payload model `InstallWizardStartupRequest` and integrated optional payload handling in `InstallWizardWorkflowViewModel`.
- Updated Template Manager install action to pass selected template ID into wizard startup payload.
- Updated Package Manager cached-install flow to use startup payload with `SelectedDistributionId` for backward-compatible preselection.
- Added startup warning channel `WizardContext.StartupWarningMessage` and rendered non-blocking warning UI in `WizardHostControl`.
- Implemented early compatibility guidance in `SelectDistributionStep.OnExitAsync` and localized compatibility/error messages in `SelectTemplateStep`.
- Added template summary/no-template state fields in `ReviewStep` and rendered summary rows in `ReviewStepView`.
- Added localization keys in `Resources.resx` and `Resources.zh-CN.resx`, and updated `Resources.Designer.cs` strongly-typed entries.
- Added unit tests: `InstallWizardWorkflowViewModelTests` and `ReviewStepTests`; existing `SelectTemplateStepTests` retained and passing.
- Added UI automation coverage support: `TemplateInstallButton` automation ID, `UiAutomationSession.TryOpenInstallWizardFromTemplateCard`, and smoke test `Open_Install_Wizard_From_Template_Install_Button`.
- Executed full test suite: `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj --nologo` -> `217 passed, 0 failed`.
- Updated implementation/test/acceptance checklists for this milestone to completed.

---

Date: 2026-02-19

## Active Milestone
- Select Template UI overlap fix and advanced options activation

## Progress
- Fixed Select Template details overlap by making empty-state text visible only when no template is selected.
- Upgraded advanced options from static labels to interactive option selection controls bound to `SelectTemplateStep.VersionSelections`.
- Added required-option validation in template step and mapped selected values into `WizardContext.TemplateVariableSelections`.
- Wired template variable selections into execution by passing context variables to `TemplateApplyStep -> ITemplateService.ApplyTemplateAsync`.
- Added/updated localization entries for template-step labels and advanced-option validation messages.
- Added unit tests in `SelectTemplateStepTests` for option assignment and required-option validation.
- Test results:
	- Targeted wizard/template tests: `13 passed, 0 failed`
	- Full suite: `219 passed, 0 failed`

---

Date: 2026-02-19

## Active Milestone
- Review-step toggle overlap fix and template apply error sanitization

## Progress
- Updated `ReviewStepView` root layout to `ScrollViewer` + stacked content so option toggles remain accessible when summary content grows.
- Updated `PowerShellService.ExecuteScriptStreamingAsync` failure path to always honor non-zero exit codes and to sanitize raw stderr before raising user-facing exceptions.
- Added script-error sanitization routine to remove CLIXML payloads, ANSI color codes, unapproved-verb warnings, and PowerShell diagnostic boilerplate while preserving meaningful error text.
- Added regression test `ExecuteScriptAsync_WithClixmlNoise_ShouldThrowSanitizedError` to verify CLIXML/warning noise is not exposed in thrown errors.
- Test results:
	- Targeted tests (`PowerShellServiceTests` + `ReviewStepTests`): `14 passed, 0 failed`
	- Full suite: `220 passed, 0 failed`

---

Date: 2026-02-19

## Active Milestone
- Template apply transient distro-not-found retry fix

## Progress
- Investigated runtime logs in AppData and confirmed template stage failures occurred at `wsl -d <instance> -- bash -c ...` with `There is no distribution with the supplied name`.
- Added targeted retry logic in `TemplateService` for this specific transient condition during bash template script execution.
- Kept retry policy bounded (`4` attempts, `300ms` delay) and limited to distribution-not-found path only.
- Added regression test `ApplyTemplateAsync_WhenDistributionTemporarilyUnavailable_RetriesAndSucceeds`.
- Test results:
	- Targeted (`TemplateServiceTests`): `7 passed, 0 failed`
	- Full suite: `221 passed, 0 failed`

---

Date: 2026-02-19

## Active Milestone
- Install module False-output safety fix and end-to-end regression

## Progress
- Investigated repeated failure logs and confirmed install stage could be misclassified as success before template execution.
- Updated `WslManagerService.InstallInstanceAsync` to parse module output as boolean and treat `False` as a hard install failure even when exit code is `0`.
- Added helper parsing logic to normalize/parse final output line (`True`/`False`) safely.
- Added integration test `InstallInstanceAsync_WhenModuleReturnsFalseOutput_ShouldThrow`.
- Test results:
	- Targeted (`WslManagerServiceIntegrationTests` + `TemplateServiceTests`): `19 passed, 0 failed`
	- Full suite: `222 passed, 0 failed`

---

Date: 2026-02-19

## Active Milestone
- UI automation screenshot baseline validation setup

## Progress
- Added `ScreenshotVerifier` in `src/Client/DistroNexus.Tests/UIAutomation/ScreenshotVerifier.cs` to capture window screenshots, manage baselines, and enforce pixel-diff thresholds.
- Added screenshot regression test `Screenshot_Regression_Main_And_Templates_Page` in `TemplateUiAutomationSmokeTests`.
- Added baseline directory and usage note: `src/Client/DistroNexus.Tests/UIAutomation/Baselines/README.md`.
- Added developer runbook: `docs/development/ui-automation-screenshot-validation.md`.
- Test results:
	- Compile/integration check: `dotnet test ... --filter "FullyQualifiedName~TemplateUiAutomationSmokeTests"` -> passed (`4/4`).
	- End-to-end screenshot baseline run: `DISTRONEXUS_RUN_UI_AUTOMATION=1` + `DISTRONEXUS_UI_AUTOMATION_UPDATE_BASELINES=1` with screenshot case filter -> passed (`1/1`).
