# Findings

> **Note:** Previous tracking files for the v2.1.1 release have been archived to `docs/archive/2026/02/20260221_v2.1.1-release/`.

## Current Milestone: v2.1.1 (Store Compliance Resubmission)

### Research & Discoveries
- Previous Store submission context indicates policy risk around Microsoft Store Policy 10.2.5 (Installing and Updating Store Apps).
- The most direct remediation strategy is to disable app update checks and related entry points in Store channel builds while preserving standalone channel behavior.
- Existing Store packaging baseline is already established (`.wapproj`, `Package.appxmanifest`, `.msixupload` path), so current priority is runtime compliance behavior and submission evidence.
- Two supporting docs were added for this phase:
	- `docs/specs/store-submission-compliance-requirements-v2.1.1.md`
	- `docs/development/store-submission-release-info-v2.1.1.md`
- Requirement-aligned execution artifacts are now split into three dedicated checklists:
	- `docs/development/store-submission-compliance-implementation-checklist-v2.1.1.md`
	- `docs/development/store-submission-compliance-test-checklist-v2.1.1.md`
	- `docs/development/store-submission-compliance-acceptance-checklist-v2.1.1.md`
- Runtime implementation now uses a centralized Store compliance mode service and applies guards at startup update check and update service entry points.
- Settings UI now disables the "Check updates on startup" toggle in Store compliance mode and persists `CheckUpdatesOnStartup=false` for Store channel.
- Automated validation result for this phase: `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --no-restore` passed (`237/237`).
