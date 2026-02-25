# Task Plan

> **Note:** Previous tracking files for the v2.1.1 release have been archived to `docs/archive/2026/02/20260221_v2.1.1-release/`.

## Current Milestone: v2.1.1 (Store Compliance Resubmission)

### Objectives
- [x] Define objectives for the next milestone.
- [x] Define Store submission compliance requirements for policy 10.2.5 remediation.
- [x] Produce current-state Store submission release information document.

### Tasks
- [x] Create `docs/specs/store-submission-compliance-requirements-v2.1.1.md`.
- [x] Create `docs/development/store-submission-release-info-v2.1.1.md`.
- [x] Create `docs/development/store-submission-compliance-implementation-checklist-v2.1.1.md`.
- [x] Create `docs/development/store-submission-compliance-test-checklist-v2.1.1.md`.
- [x] Create `docs/development/store-submission-compliance-acceptance-checklist-v2.1.1.md`.
- [x] Implement Store-mode update check disablement in runtime.
- [x] Verify Store package content excludes non-Store update hooks.
- [x] Run automated tests for Store compliance changes (`237/237` passed).
- [x] Run Store package build (`tools/build.ps1 -StoreBuild -Version 2.1.1`) — artifacts generated at `release/store/`.
- [ ] Finalize Partner Center listing/declarations and submit.
