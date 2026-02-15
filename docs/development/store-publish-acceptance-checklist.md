# Store Publish Acceptance Checklist

Date: 2026-02-15  
Source Spec: `docs/specs/store-publish-analysis.md`

## AC-1 Distribution Strategy
- [x] Hybrid strategy is implemented: standalone + Store-only MSIX flow.
- [x] Store path does not break existing release workflow.
- [x] Pricing confirmed as **Free**.
- [x] Target markets confirmed.

## AC-2 Store Identity Consistency
- [x] Package identity values in manifest exactly match Partner Center.
- [ ] Store ID is correctly linked in release records.

## AC-3 Packaging Output Compliance
- [x] Multi-architecture Store package is available.
- [x] Store submission uses `.msixupload` as preferred artifact.
- [x] Package version follows `Major.Minor.Patch.0` format (4th part = `0`, reserved for Store).
- [ ] First three version parts increment monotonically across resubmissions.
- [x] Package size is within Store limit (≤ 25 GB).

## AC-4 Capability and Policy Compliance
- [x] `runFullTrust` is declared only where required.
- [x] Submission options include clear `runFullTrust` justification with testing instructions.
- [ ] Privacy policy is published and compliant with Store policy expectations.
- [ ] Product Declarations are reviewed and correctly set.
- [ ] System Requirements are declared if applicable (e.g., WSL enabled).

## AC-5 Product Quality Baseline
- [ ] Sideloading MSIX install verified before Store submission.
- [ ] Fresh install, upgrade, and uninstall pass required matrix.
- [ ] Install tested on manifest `MinVersion` OS build.
- [ ] Core WSL management scenarios pass on target architectures.
- [ ] Offline startup and core operations meet baseline expectations.
- [x] Packaged app path compatibility verified (PowerShell module, configs, templates).
- [x] Settings migration from standalone to Store version is documented or handled.

## AC-6 Store Listing Readiness
- [x] **Category** is selected (required).
- [x] **Description** (required, ≤ 10,000 chars) is provided.
- [ ] Desktop screenshots meet requirements (≥ 1366×768, `.png`, ≤ 50 MB, ≥ 1).
- [x] 1:1 App tile icon (300×300 px) is provided.
- [x] Support contact and website are provided.
- [x] Contact Details provided (required for business/company accounts).
- [ ] Age rating and submission metadata are complete.

## AC-7 Release Operations Readiness
- [x] Rollback path for failed certification/post-release issues is defined.
- [x] Submission artifacts are archived for audit and re-release.
- [x] Hotfix process is defined with version bump policy.

## Final Acceptance Gate
- [ ] All implementation checklist items are complete.
- [ ] All test checklist items are complete.
- [ ] No known blocker remains for Partner Center submission.

## Sign-off
- Implementation Owner: Pending
- QA Owner: Pending
- Release Owner: Pending
- Final Result: [ ] Accepted  [x] Accepted with Conditions  [ ] Rejected
