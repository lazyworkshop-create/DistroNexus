# Findings Log

Date: 2026-02-20

## Active Milestone
- GitHub Actions pipeline stabilization and release-lane verification

## Findings
- Anonymous GitHub REST polling is insufficient for sustained diagnostics in this environment due rate limits; authenticated GitHub CLI (`gh`) gives reliable run/job/failed-step visibility.
- The dominant failures were test-environment mismatches and timing sensitivity, not core product runtime regressions:
	- PowerShell unit assertions conflicted with explicit CI policy behavior (`SkippedByPolicy` when `$env:CI=true` without override).
	- Instance-list non-empty assertion depended on host/runtime state not guaranteed on CI runners.
	- Download speed exact-zero assertion was brittle under asynchronous progress update timing.
	- Integration mock path required explicit `LASTEXITCODE=0` to emulate successful script execution semantics.
- Workflow hardening remains necessary even when test code is corrected:
	- Explicitly excluding `UIAutomation` from headless lanes prevents false negatives.
	- Result publication steps should be resilient to missing/non-matching files and must not become primary failure sources.
- End-to-end confidence requires validating all three channels after remediation (`CI Build`, `Integration Tests`, `Release Build`), not only one workflow family.

Date: 2026-02-19

## Active Milestone
- Project quality remediation checklist synchronization and template-test hang mitigation

## Findings
- Current template-focused test subset does not reproduce a deterministic deadlock; repeated local runs are stable and complete within expected time.
- The highest practical reliability risk is CI-visible indefinite waiting during test host/runtime edge cases rather than a consistent logic deadlock in template tests.
- Adding `--blame-hang` with bounded timeout to workflow `dotnet test` invocations is the most direct root-level mitigation: prevents infinite wait, forces diagnosable failure, and preserves future triage artifacts.
- `TestScope` metadata split behaves as intended after remediation (`Quick != Full`), and non-UI full lane remains independently executable.
- Remaining acceptance closure gaps are governance/store verification artifacts, which require release-lane evidence and were explicitly deferred with owner/milestone to avoid false closure.

Date: 2026-02-19

## Active Milestone
- Template toggle semantics refinement and ScriptPath sibling-template staging fix

## Findings
- Multi-version templates (`dotnet-multi-sdk-dev`, `nodejs-multi-version-dev`, similar composite templates) rely on sibling script references such as `../dotnet-dev/install.sh`; staging only the current script folder is insufficient.
- Staged execution must preserve the same relative folder topology as under `config/templates` to keep all script-to-script includes valid.
- Switching UX from "Skip template" to "Use template" is cleaner when the toggle directly controls whether template selection controls are enabled/disabled.
- Entry-based defaults are best implemented via startup payload presence (`TemplateId`) rather than extra UI state flags.
- Removing Review toggles should be paired with backend enforcement at step entry to prevent stale context values from previous flow states.

## Active Milestone
- Template selection two-subflow refactor and one-screen review layout

## Findings
- Keeping template selection and advanced option editing in one step increases visual density and causes decision overload.
- The most maintainable split is: first step for template choice/basic details, second step dedicated to version/options input.
- Conditional step visibility should be handled by workflow-level skip logic, not ad-hoc redirections in step code.
- Dynamic indicator numbering is required when steps are conditionally skipped; static numbering causes misleading progress labels.
- Review-page overflow is best resolved by compacting into a single card and colocating install switches with summary fields.

Date: 2026-02-19

## Active Milestone
- PowerShell template ScriptPath CRLF normalization fix and full install-flow revalidation

## Findings
- The C# `TemplateService` hardening alone is insufficient for full backend flow because `Apply-DistroNexusTemplate` has an independent script execution path.
- Real install + template apply validation surfaced PowerShell module failure: `/config/templates/nodejs-dev/install.sh: line 2: se: invalid option name`, confirming CRLF contamination in ScriptPath execution.
- Directly executing source script files via `wsl ... bash <path>` bypasses line-ending normalization for both primary scripts and sourced helper scripts.
- A robust PowerShell-side fix mirrors C# strategy: normalize content (`CRLF/CR -> LF`, strip BOM), stage script directory and sibling `common` directory, execute staged file, then clean up staging root.
- End-to-end revalidation after the patch succeeded for fresh instance install plus `nodejs-dev` template apply and runtime probe.

Date: 2026-02-19

## Active Milestone
- Template ScriptPath execution context fix and end-to-end install validation

## Findings
- Executing ScriptPath-based bash template content via stdin breaks script-location semantics (`BASH_SOURCE[0]` unbound under `set -u`).
- Template scripts that source relative helpers (`../common/lib.sh`) require execution from a real file path with expected directory layout.
- Normalizing only the primary script is insufficient when sourced helper files still contain Windows CRLF.
- A robust strategy is staging script directory and helper directory into a temporary WSL workspace with LF normalization, then executing from that staged file path.
- End-to-end validation must confirm both install completion and post-template runtime availability in a fresh shell session.

Date: 2026-02-19

## Active Milestone
- Template apply bash CRLF line-ending compatibility fix

## Findings
- After quote-transport fix, latest runtime error shifted to `bash: line 2: set: pipefail : invalid option name`.
- Root cause is Windows line endings (`\r\n`) in template script payload when executed in WSL bash via stdin.
- `set -euo pipefail` fails when `pipefail` token carries trailing `\r`.
- Normalizing bash payload to LF (`\n`) and stripping UTF-8 BOM before execution resolves this class of failures.
- Regression should assert decoded payload has no `\r` characters.

Date: 2026-02-19

## Active Milestone
- Template apply bash command quoting/format-exception fix

## Findings
- PowerShell-side inline command form `wsl -d <name> -- bash -c '...script...'` is fragile for multi-line scripts containing mixed quote/brace patterns.
- Failure symptom in runtime logs matched PowerShell parse-time string formatting error (`Expected an ASCII digit`) during template script invocation.
- Passing script content through Base64 and decoding inside WSL (`echo '<base64>' | base64 --decode | bash`) removes cross-shell quoting ambiguity and preserves script payload exactly.
- Preflight bash checks must use the same transport path to avoid equivalent quoting failures.
- Regression tests should decode captured command payloads instead of asserting raw script text in command strings.

Date: 2026-02-19

## Active Milestone
- Download progress/speed implementation verification and checklist closure

## Findings
- `Progress<T>` in tests can cause asynchronous callback timing issues; deterministic assertions should use a synchronous `IProgress<T>` implementation.
- Speed-to-zero assertion requires at least one baseline update interval and a subsequent stalled interval report; otherwise previous non-zero speed remains.
- Full regression status: `211 passed, 0 failed` on `DistroNexus.Tests`.
- Remaining unchecked acceptance items are manual/runtime UX validations (unknown-size indeterminate UI and visual/behavioral checks requiring interactive run).
- FlaUI UI automation requires serialized execution; collection-level `DisableParallelization = true` avoids COM startup contention.
- Package list and toolbar shared "Download" labels can cause ambiguous targeting; `AutomationProperties.AutomationId` is required for stable UI element selection.
- Deterministic UI download-flow automation is achieved with env-gated fake download mode (`DISTRONEXUS_UI_AUTOMATION_FAKE_DOWNLOAD=1`) that does not affect normal runtime.

---

Date: 2026-02-19

## Active Milestone
- Install auto-download catalog default-name fix and error mapping regression validation

## Findings
- Latest failure was not a pure network issue; root cause was passing display name (`Ubuntu 24.04 LTS`) into `Save-DistroNexusPackage -DefaultName`, which requires catalog default-name key.
- Post-download package resolution should support `DefaultName/Name/Version` matching to avoid false negatives when catalog metadata uses different display strings.
- User-facing mapping order matters: catalog/package metadata failures must be matched before generic `network/download/404` rules to avoid misleading guidance.
- Added regression coverage to lock both behavior paths: catalog miss maps to "refresh sources" guidance, and missing local file after metadata download maps to metadata-specific guidance (not generic network).
- Confirmed final runtime root cause from latest logs: `Save-DistroNexusPackage` download path used `$Package.Url` while catalog records provided `DownloadUrl`, causing empty URL in download attempts (`Downloading ... from `).
- Confirmed secondary control-flow issue: `Install-DistroNexusInstance` only checked whether download result object existed, not whether `Success = $false`, allowing misleading downstream "file not found" failure path.
- End-to-end runtime validation after fixes succeeded: package downloaded from Ubuntu release URL and instance import completed (`Successfully installed instance: copilot-verify-2404`).

---

Date: 2026-02-19

## Active Milestone
- Template Manager to Install Wizard flow improvement requirements definition

## Findings
- `TemplatesViewModel.InstallNewInstance` currently opens generic wizard flow without passing selected template context, causing intent-loss for template-first entry.
- Wizard context already supports template fields (`SelectedTemplate`, `ApplyTemplateAfterInstall`), so the gap is startup data propagation rather than missing domain model.
- Review step currently lacks explicit template summary visibility, reducing confidence before execution.
- The safest enhancement path is adding optional wizard startup payload with backward-compatible defaults for existing entry points.

---

Date: 2026-02-19

## Active Milestone
- Package Manager download UX refinement (progress alignment and same-file version merge)

## Findings
- Same-file merge should prefer currently-downloading or cached entry as representative to avoid hiding active state.
- A reliable same-file key requires layered fallback: SHA256 first, then file name + size, then URL, then package ID.
- Performing same-file merge at grouped presentation layer preserves original package collections and minimizes impact on download/business logic.
- A dedicated spacer column in card action area is the most stable way to prevent progress visuals from touching the cancel button across different widths.

---

Date: 2026-02-19

## Active Milestone
- Template Manager to Install Wizard flow improvement checklist generation

## Findings
- Existing project checklist documents follow a stable structure: implementation checklist, test checklist, and acceptance checklist as separate files in `docs/development/`.
- The requirements document already includes explicit acceptance criteria (AC-01 to AC-05), enabling direct one-to-one mapping in acceptance gating.
- This milestone is documentation-only; all generated checklist items should start unchecked to reflect planning and verification intent.
- The most reliable coverage split is: implementation by architecture layer, tests by test level (unit/workflow/UI automation/manual), and acceptance by AC mapping plus evidence requirements.

---

Date: 2026-02-19

## Active Milestone
- Template Manager to Install Wizard flow improvement implementation and validation

## Findings
- `Resources.Designer.cs` in desktop project is not auto-regenerated during CLI build in this workspace, so new localization keys require explicit strongly-typed property entries.
- The least invasive startup integration is a transient wizard startup payload object plus `SetStartupRequest(...)` on `InstallWizardWorkflowViewModel`, preserving all existing call sites with null/default behavior.
- Non-blocking startup warnings are best surfaced at `WizardContext` level and rendered in `WizardHostControl`, avoiding new pages or modal dialogs.
- Early compatibility feedback can be introduced in `SelectDistributionStep.OnExitAsync` using `ITemplateService.IsTemplateCompatibleAsync`, while final blocking remains in template-step validation.
- A stable UI automation selector for template install action requires explicit `AutomationProperties.AutomationId` on template card install button.

---

Date: 2026-02-19

## Active Milestone
- Select Template UI overlap fix and advanced options activation

## Findings
- The details-panel overlap is caused by the empty-state text always being visible even when a template is selected; visibility must be conditional on `SelectedTemplate == null`.
- Existing advanced options UI only displayed option labels and did not persist user selections into wizard context.
- The minimal effective implementation path is `SelectTemplateStep.VersionSelections -> WizardContext.TemplateVariableSelections -> TemplateApplyStep.ApplyTemplateAsync(variables)`.
- Required option validation should run in `SelectTemplateStep.Validate()` to prevent advancing with missing mandatory option values.

---

Date: 2026-02-19

## Active Milestone
- Review-step toggle overlap fix and template apply error sanitization

## Findings
- `ReviewStepView` used a plain `StackPanel`; once template summary rows increased, bottom toggles could be pushed beyond visible area without scroll support.
- The most surgical UI fix is wrapping Review content in a `ScrollViewer` with vertical auto-scroll, preserving current structure and bindings.
- `PowerShellService.ExecuteScriptStreamingAsync` surfaced raw stderr when exit code was non-zero, causing CLIXML and unapproved-verb warning noise to appear in user-facing install errors.
- Error sanitization at the PowerShell service layer is the correct root-cause fix because template apply and other callers reuse this execution path.
- Regression guard should assert sanitized exception text excludes `CLIXML` and unapproved-verb warning phrases while still retaining actionable failure content.

---

Date: 2026-02-19

## Active Milestone
- Template apply transient distro-not-found retry fix

## Findings
- Runtime logs show base install flow proceeded, but template bash script failed at `wsl -d <instance>` with `There is no distribution with the supplied name`.
- This failure can be transient immediately after installation, where WSL instance visibility lags behind step transition.
- A narrow retry policy scoped to this specific error in template bash execution path is safer than broad retries for all script failures.
- Existing error sanitization is effective, but retry is still needed to reduce false-negative template failures.

---

Date: 2026-02-19

## Active Milestone
- Install module False-output safety fix and end-to-end regression

## Findings
- `Install-DistroNexusInstance` PowerShell cmdlet returns `$false` in its catch block rather than throwing, so process exit code can remain `0`.
- C# service previously treated `ExitCode == 0` as unconditional install success, allowing flow to continue into template stage with a missing instance.
- Correct safety behavior requires parsing cmdlet output boolean semantics (`True`/`False`) in addition to exit code.
- Treating output `False` as installation failure prevents unsafe continuation and surfaces the real failure earlier in the wizard.

---

Date: 2026-02-19

## Active Milestone
- UI automation screenshot baseline validation setup

## Findings
- Existing `FlaUI` launch/navigation helpers are sufficient for a first screenshot regression flow; no additional UI driver package is required.
- Deterministic screenshot comparison needs an explicit baseline update switch; otherwise baseline churn is hard to control in CI.
- Pixel-level diff with a small tolerance and persisted diff artifacts provides actionable failure diagnostics for visual regressions.
- Stable first coverage should target low-volatility pages (`main-window`, `templates-page`) before expanding to highly dynamic install-progress views.
- Environment-gated execution (`DISTRONEXUS_RUN_UI_AUTOMATION=1`) keeps normal test runs fast and avoids accidental UI session requirements.
