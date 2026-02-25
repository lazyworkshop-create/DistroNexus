# Store Submission Compliance Implementation Checklist (v2.1.1)

Date: 2026-02-24  
Source Spec: `docs/specs/store-submission-compliance-requirements-v2.1.1.md`

## A. Scope and Branch Control
- [x] Confirm working branch is `compliance/store-publish-remediation-20260224`.
- [x] Confirm package/app version remains `2.1.1` for this submission line.
- [x] Confirm scope is limited to Store compliance remediation (no unrelated feature changes).

## B. FR-01 Store Channel Detection
- [x] Introduce a single Store compliance mode signal (e.g., `StoreComplianceMode`) in application runtime.
- [x] Ensure Store mode decision is deterministic for packaged Store builds.
- [x] Ensure Store mode decision is overridable/testable for unit and integration tests.
- [x] Wire Store mode service through DI with constructor injection.

## C. FR-02 Disable App Update Checks in Store Mode
- [x] Locate all app update check entry points (startup, scheduled/background, manual command).
- [x] Add centralized guard so Store mode skips startup update checks.
- [x] Add centralized guard so Store mode skips scheduled/background update checks.
- [x] Disable or no-op manual "Check for Updates" command in Store mode.
- [x] Ensure skipped paths do not trigger external updater process, URL, or launcher.

## D. FR-03 Store Mode UI Behavior
- [x] Hide or disable update-check UI entry points when Store mode is active.
- [ ] Update status/help text to avoid non-Store update guidance in Store mode.
- [x] Preserve existing non-Store UI behavior.

## E. FR-04 Packaging Pipeline Compliance
- [ ] Verify `tools/build.ps1` Store path still outputs `.msixbundle` and `.msixupload`.
- [x] Verify Store package does not include self-update executables/scripts.
- [ ] Verify Store packaging path remains isolated from standalone installer outputs.
- [ ] Keep standalone channels (`zip` / installer) unchanged unless required by compliance scope.

## F. FR-05 Logging and Diagnostics
- [x] Add informational log when update checks are skipped due to Store mode.
- [x] Ensure log messages do not suggest non-Store app update actions.
- [ ] Ensure log category and message are useful for certification evidence.

## G. Documentation and Submission Artifacts
- [ ] Update certification notes to state updates are Store-managed only.
- [x] Update release notes / listing draft text to reflect compliance behavior change.
- [ ] Record package inspection evidence for excluded updater hooks.
- [ ] Link evidence artifacts in release records.

## H. Traceability Completion
- [x] FR-01 fully mapped to implemented code paths.
- [x] FR-02 fully mapped to guarded update entry points.
- [x] FR-03 fully mapped to UI state behavior.
- [ ] FR-04 fully mapped to build/package outputs.
- [ ] FR-05 fully mapped to log records.

## Sign-off
- Implementation Owner: Pending
- Reviewer: Pending
- Result: [ ] Pass  [ ] Pass with Exceptions  [ ] Fail

## Current Blocker (2026-02-24)
- Store package generation is blocked on this machine: Desktop Bridge targets are missing.
- Build command attempted: `pwsh -File ./tools/build.ps1 -Configuration Release -StoreBuild -Version 2.1.1`
- Error: `Desktop Bridge targets not found. Install Visual Studio Build Tools with Universal Windows Platform build tools/Desktop Bridge workload.`

## Interim Evidence (2026-02-24)
- Packaging project static audit confirms no updater payload includes in package project items.
- `src/DistroNexus.Package/DistroNexus.Package.wapproj` includes `Assets/**` and `../PowerShell/**` only; no updater executable/script include entries found.
