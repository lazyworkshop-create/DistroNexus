# Store Publish Implementation Task Plan

Date: 2026-02-15
Owner: Copilot (GPT-5.3-Codex)
Source Spec: `docs/specs/store-publish-analysis.md`

## Goal
Implement Store publishing engineering scope end-to-end in repository code/docs, keep standalone flow unchanged, and provide test evidence for acceptance checklist mapping.

## Phases
1. Gap Analysis and Baseline
	 - Status: Completed
	 - Actions:
		 - Reviewed spec/checklists and existing implementation state.
		 - Confirmed no existing `wapproj` and no Store build switch in `tools/build.ps1`.
2. Store Packaging Implementation
	- Status: Completed
	 - Actions:
		 - Add `src/DistroNexus.Package/` with `DistroNexus.Package.wapproj`.
		 - Add `Package.appxmanifest` with confirmed Partner Center identity and `runFullTrust` capability.
		 - Add package assets placeholders required by manifest.
3. Build Pipeline Integration
	- Status: Completed
	 - Actions:
		 - Add `-StoreBuild` flow to `tools/build.ps1`.
		 - Produce `.msixbundle` and `.msixupload` artifacts.
		 - Enforce Store version format (`Major.Minor.Patch.0`).
4. Packaged Runtime Path Compatibility
	- Status: Completed
	 - Actions:
		 - Consolidate file/module path discovery for packaged and development layouts.
		 - Update related services and tests.
5. Validation and Acceptance Evidence
	- Status: Completed (with external-environment exceptions)
	 - Actions:
		 - Execute targeted and full tests where possible.
		 - Update implementation/test/acceptance checklists with evidence-backed status.

## Constraints
- Do not break existing standalone release workflow.
- Keep scope focused on Store packaging + required docs.
- Mark manual Partner Center-only steps explicitly if they cannot be executed locally.