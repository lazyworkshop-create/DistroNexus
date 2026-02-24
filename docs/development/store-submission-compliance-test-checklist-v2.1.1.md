# Store Submission Compliance Test Checklist (v2.1.1)

Date: 2026-02-24  
Source Spec: `docs/specs/store-submission-compliance-requirements-v2.1.1.md`

## A. Test Preconditions
- [ ] Build environment is prepared for Store packaging validation.
- [ ] Test package version is `2.1.1.0` for Store artifact validation.
- [ ] Baseline non-Store build is available for regression comparison.

## B. FR-01 Store Channel Detection Tests
- [x] Unit test: Store mode resolves `true` in packaged Store context.
- [x] Unit test: Store mode resolves `false` in standalone context.
- [x] Unit test: Store mode can be mocked/overridden in tests.
- [ ] Integration test: DI resolves Store mode service correctly.

## C. FR-02 Update Check Disablement Tests
- [x] Startup test: app update check path is skipped in Store mode.
- [x] Background test: scheduled/background update path is skipped in Store mode.
- [x] Manual command test: "Check for Updates" action is disabled or no-op in Store mode.
- [x] Negative test: no external updater process/URL is invoked in Store mode.
- [x] Regression test: non-Store mode still executes expected update-check behavior.

## D. FR-03 UI Behavior Tests
- [x] UI test: update-check entry controls are hidden/disabled in Store mode.
- [ ] UI test: Store mode copy does not instruct external updates.
- [ ] UI test: non-Store mode UI remains unchanged.

## E. FR-04 Packaging Compliance Tests
- [ ] Build test: Store build produces `.msixbundle`.
- [ ] Build test: Store build produces `.msixupload`.
- [ ] Package inspection: no updater executable/script/hook included in Store artifact.
- [ ] Regression test: standalone packaging outputs are unchanged.

## F. FR-05 Logging Tests
- [ ] Runtime log includes informational event when update check is skipped in Store mode.
- [ ] Log content does not suggest non-Store update route.
- [ ] Log output is attached as evidence for certification notes.

## G. Policy and Certification-Oriented Tests
- [x] Validate alignment with policy 10.2.5 interpretation in runtime behavior.
- [ ] Certification notes include explicit statement: updates are Store-managed only.
- [ ] WACK and submission pre-check artifacts are collected.

## H. Evidence Collection
- [x] Attach unit test results for Store mode detection and guards.
- [ ] Attach integration/UI verification records.
- [ ] Attach package inspection output (artifact content listing).
- [ ] Attach runtime logs demonstrating skipped update checks.
- [x] Attach final test summary with pass/fail and open risks.

### Automated Test Evidence (2026-02-24)
- Command: `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --no-restore`
- Result: Passed
- Summary: `total: 237, failed: 0, succeeded: 237, skipped: 0`

## Sign-off
- Test Owner: Pending
- Reviewer: Pending
- Result: [ ] Pass  [ ] Pass with Exceptions  [ ] Fail
