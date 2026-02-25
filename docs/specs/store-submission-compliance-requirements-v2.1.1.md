# Store Submission Compliance Requirements (v2.1.1)

Date: 2026-02-24  
Owner: DistroNexus Team  
Target Branch: `compliance/store-publish-remediation-20260224`

## 1. Background

The previous Store submission line (v2.0.1) was blocked due to policy non-compliance with **Microsoft Store Policy 10.2.5 Security - Installing and Updating Store Apps**.

The key compliance gap is that a Store-distributed app must not install or update the Store app outside Microsoft Store mechanisms.

## 2. Objective

For the next Store submission (targeting v2.1.1), deliver a Store-compliant package and runtime behavior by:

1. Removing Store-channel update checks that could lead users to non-Store update flows.
2. Adjusting Store packaging and submission flow to enforce policy-safe artifacts.
3. Preserving existing non-Store distribution behavior in standalone channels.

## 3. Scope

### In Scope
- Store-only runtime behavior changes related to update checking.
- Store build and packaging pipeline hardening.
- Submission documentation and certification notes updates.

### Out of Scope
- Refactoring unrelated runtime features.
- Replacing existing standalone update guidance for non-Store distribution.
- Introducing a new updater system.

## 4. Compliance Interpretation (Policy 10.2.5)

For this release, DistroNexus applies the following interpretation:

- Store-installed app instances must not perform external app-update prompts, downloaders, or redirect-based update flows outside Store control.
- "Check for updates" functionality for the app binary itself must be disabled in Store-distributed packages.
- Store package updates must rely on Microsoft Store servicing only.

## 5. Functional Requirements

### FR-01 Store Channel Detection
- The app must support an explicit Store channel mode (`StoreComplianceMode`) resolved from packaging/runtime context.
- Store mode must be deterministic and testable.

### FR-02 Disable App Update Check in Store Mode
- On startup, Store mode must skip app update check execution.
- Any scheduled/background app update check must be skipped in Store mode.
- Any manual "Check for Updates" command path must be disabled in Store mode.

### FR-03 Store Mode UI Behavior
- Update-check related UI entry points must be hidden or disabled in Store mode.
- UI messages must not instruct Store users to update outside Microsoft Store.
- Existing update-related UI behavior may remain unchanged for non-Store channels.

### FR-04 Packaging Pipeline Compliance
- Store packaging output remains `.msixupload` (preferred) and `.msixbundle` artifacts.
- Store package content must not include app self-update binaries/scripts or non-Store update launch hooks.
- Store build path must remain isolated from standalone installer path (`zip` / Inno Setup).

### FR-05 Logging and Diagnostics
- When update checks are skipped in Store mode, the app must log a compliance-safe informational event.
- Logs must avoid suggesting non-Store update actions for Store users.

## 6. Non-Functional Requirements

### NFR-01 Backward Compatibility
- Non-Store distribution behavior must remain unchanged unless explicitly required by this scope.

### NFR-02 Security and Policy Safety
- No runtime behavior in Store mode may trigger external app update execution.

### NFR-03 Testability
- The Store mode decision and skipped-update paths must be covered by automated or scripted validation.

## 7. Verification and Acceptance

Release readiness requires all checks below to pass:

1. **Store Mode Runtime Validation**  
   Confirm startup/manual/background update checks are disabled under Store mode.
2. **Package Inspection**  
   Confirm Store artifacts do not include disallowed updater executables/scripts.
3. **Certification Notes**  
   Submission notes explicitly state that app updates are Store-managed only.
4. **Regression Check**  
   Confirm standalone channel still supports existing release/update guidance.

## 8. Deliverables

- Store compliance implementation changes in client/runtime and build scripts.
- Updated Store submission metadata and certification notes.
- Evidence artifacts for checklist sign-off.

## 9. Risks and Mitigations

- **Risk:** Hidden code paths still trigger update checks.  
  **Mitigation:** Add centralized guard and validate all entry points.
- **Risk:** Store packaging accidentally includes legacy updater hooks.  
  **Mitigation:** Add package content verification step in release checklist.
- **Risk:** User confusion about update behavior differences between channels.  
  **Mitigation:** Add channel-specific wording in Store listing and support docs.

## 10. Traceability

- Analysis baseline: `docs/specs/store-publish-analysis.md`
- Execution checklist: `docs/development/store-submission-compliance-implementation-checklist-v2.1.1.md`
- Testing checklist: `docs/development/store-submission-compliance-test-checklist-v2.1.1.md`
- Acceptance checklist: `docs/development/store-submission-compliance-acceptance-checklist-v2.1.1.md`