# Store Submission Compliance Acceptance Checklist (v2.1.1)

Date: 2026-02-24  
Source Spec: `docs/specs/store-submission-compliance-requirements-v2.1.1.md`

## AC-1 Scope and Version Gate
- [x] Submission scope only contains compliance remediation changes.
- [x] Submission line version remains `2.1.1` as planned.
- [ ] No unrelated release scope creep is present.

## AC-2 FR-01 Store Mode Gate
- [x] Store channel detection is implemented and validated.
- [x] Store mode behavior is deterministic and testable.

## AC-3 FR-02 Update Path Compliance Gate
- [x] Startup app update check is disabled in Store mode.
- [x] Scheduled/background app update checks are disabled in Store mode.
- [x] Manual "Check for Updates" path is disabled or no-op in Store mode.
- [x] Store mode does not trigger external updater process or update URL.

## AC-4 FR-03 UI and Messaging Gate
- [x] Update-check UI entry points are hidden/disabled for Store users.
- [ ] Store-facing text does not direct users to non-Store app updates.
- [x] Non-Store channel UX remains valid.

## AC-5 FR-04 Packaging Gate
- [ ] Store outputs include valid `.msixbundle` and `.msixupload` artifacts.
- [ ] Store package content has no updater binaries/scripts/hooks.
- [ ] Standalone distribution artifacts remain unaffected.

## AC-6 FR-05 Logging Gate
- [x] Compliance log event exists for skipped update checks in Store mode.
- [ ] Logging output is certification-safe and attached to evidence bundle.

## AC-7 Submission Readiness Gate
- [ ] Certification notes explicitly state updates are Store-managed only.
- [ ] Listing/release text reflects compliance-driven update behavior.
- [ ] Required Partner Center metadata and declarations are completed.
- [ ] Evidence links are complete and auditable.

## Final Acceptance Decision
- [ ] All implementation checklist items are complete.
- [ ] All test checklist items are complete.
- [ ] No open blocker remains for Store submission.

## Sign-off
- Implementation Owner: Pending
- QA Owner: Pending
- Release Owner: Pending
- Final Result: [ ] Accepted  [ ] Accepted with Conditions  [ ] Rejected
