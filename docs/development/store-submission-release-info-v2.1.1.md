# Store Submission Release Information (Current State, v2.1.1)

Date: 2026-02-24  
Owner: DistroNexus Team  
Target Branch: `compliance/store-publish-remediation-20260224`

## 1. Submission Context

- Submission target: Microsoft Store update for DistroNexus.
- Compliance driver: Prior submission line was blocked against policy **10.2.5 Security - Installing and Updating Store Apps**.
- This submission cycle prioritizes compliance remediation for Store channel behavior and packaging.

## 2. Release Positioning

This release is a **compliance and packaging stabilization update**.  
Primary user impact: Store users receive a policy-aligned build where app update flow is managed by Microsoft Store only.

## 3. Planned User-Facing Notes (Draft)

### Short Description (Draft)
Store compliance and packaging reliability improvements for DistroNexus, with safer channel-specific update behavior.

### What is New in This Version (Draft)
- Improved Microsoft Store submission compliance for app update behavior.
- Store packaging and release process hardening for certification reliability.
- Internal governance updates to strengthen release evidence and auditability.

## 4. Current Engineering Status Snapshot

### Completed
- Store packaging architecture established (`.wapproj`, manifest identity, `runFullTrust`, multi-arch bundle path).
- Store build outputs integrated (`.msixbundle` + `.msixupload`).
- Store metadata templates and release-note templates prepared.
- Core store-publish analysis and checklists are in place.

### In Progress / Pending
- Implement and verify Store-mode behavior to disable app update checks.
- Complete remaining Partner Center submission fields and declarations.
- Finalize mandatory listing assets (latest screenshot set, privacy policy URL verification).
- Complete certification notes specifically referencing the 10.2.5 remediation strategy.

## 5. Packaging and Compliance Notes for Submission

- Store package must remain isolated from standalone installer pipeline.
- Store-distributed app must not provide non-Store app update flow.
- Submission notes should clearly state:
  - Updates are delivered through Microsoft Store.
  - No external app self-update mechanism is active in Store channel.
  - WSL prerequisites and test steps for certification are provided.

## 6. Release Readiness Gates

Before submission, all gates below must be green:

1. Store-mode update-check disablement verified.
2. Store package content inspection passed (no updater hooks/scripts).
3. Partner Center listing/content/declarations complete.
4. Evidence bundle archived for audit and rollback.

## 7. References

- `docs/specs/store-submission-compliance-requirements-v2.1.1.md`
- `docs/specs/store-publish-analysis.md`
- `docs/development/store-submission-compliance-implementation-checklist-v2.1.1.md`
- `docs/development/store-submission-compliance-test-checklist-v2.1.1.md`
- `docs/development/store-submission-compliance-acceptance-checklist-v2.1.1.md`