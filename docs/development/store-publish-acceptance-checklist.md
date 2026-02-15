# Store Publish Acceptance Checklist

Date: 2026-02-15  
Source Spec: `docs/specs/store-publish-analysis.md`

## AC-1 Distribution Strategy
- [ ] Hybrid strategy is implemented: standalone + Store-only MSIX flow.
- [ ] Store path does not break existing release workflow.
- [ ] Pricing confirmed as **Free**.
- [ ] Target markets confirmed.

## AC-2 Store Identity Consistency
- [ ] Package identity values in manifest exactly match Partner Center.
- [ ] Store ID is correctly linked in release records.

## AC-3 Packaging Output Compliance
- [ ] Multi-architecture Store package is available.
- [ ] Store submission uses `.msixupload` as preferred artifact.
- [ ] Package version follows `Major.Minor.Patch.0` format (4th part = `0`, reserved for Store).
- [ ] First three version parts increment monotonically across resubmissions.
- [ ] Package size is within Store limit (≤ 25 GB).

## AC-4 Capability and Policy Compliance
- [ ] `runFullTrust` is declared only where required.
- [ ] Submission options include clear `runFullTrust` justification with testing instructions.
- [ ] Privacy policy is published and compliant with Store policy expectations.
- [ ] Product Declarations are reviewed and correctly set.
- [ ] System Requirements are declared if applicable (e.g., WSL enabled).

## AC-5 Product Quality Baseline
- [ ] Sideloading MSIX install verified before Store submission.
- [ ] Fresh install, upgrade, and uninstall pass required matrix.
- [ ] Install tested on manifest `MinVersion` OS build.
- [ ] Core WSL management scenarios pass on target architectures.
- [ ] Offline startup and core operations meet baseline expectations.
- [ ] Packaged app path compatibility verified (PowerShell module, configs, templates).
- [ ] Settings migration from standalone to Store version is documented or handled.

## AC-6 Store Listing Readiness
- [ ] **Category** is selected (required).
- [ ] **Description** (required, ≤ 10,000 chars) is provided.
- [ ] Desktop screenshots meet requirements (≥ 1366×768, `.png`, ≤ 50 MB, ≥ 1).
- [ ] 1:1 App tile icon (300×300 px) is provided.
- [ ] Support contact and website are provided.
- [ ] Contact Details provided (required for business/company accounts).
- [ ] Age rating and submission metadata are complete.

## AC-7 Release Operations Readiness
- [ ] Rollback path for failed certification/post-release issues is defined.
- [ ] Submission artifacts are archived for audit and re-release.
- [ ] Hotfix process is defined with version bump policy.

## Final Acceptance Gate
- [ ] All implementation checklist items are complete.
- [ ] All test checklist items are complete.
- [ ] No known blocker remains for Partner Center submission.

## Sign-off
- Implementation Owner: Pending
- QA Owner: Pending
- Release Owner: Pending
- Final Result: [ ] Accepted  [ ] Accepted with Conditions  [ ] Rejected
