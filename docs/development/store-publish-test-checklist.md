# Store Publish Test Checklist

Date: 2026-02-15  
Source Spec: `docs/specs/store-publish-analysis.md`

## A. Build and Packaging Verification
- [x] Store build runs successfully with Store switch.
- [x] x64 package is generated successfully.
- [x] ARM64 package is generated successfully.
- [x] Multi-arch bundle (`.msixbundle`) is generated successfully.
- [x] Store upload artifact (`.msixupload`) is generated and valid.
- [x] Package size is within Store limit (≤ 25 GB per bundle).
- [x] Package version 4th part is `0` (reserved for Store use).

## B. Manifest and Identity Validation
- [x] Manifest `Identity Name` matches Partner Center value.
- [x] Manifest `Identity Publisher` matches Partner Center value.
- [x] Manifest `PublisherDisplayName` matches Partner Center value.
- [x] Manifest `Identity Version` uses `Major.Minor.Patch.0` format.
- [x] `runFullTrust` capability exists and schema validates.
- [x] `rescap` namespace and `IgnorableNamespaces` are correctly configured.
- [x] `TargetDeviceFamily` is `Windows.Desktop`.
- [x] `MinVersion` and `MaxVersionTested` are set and valid.

## C. Install / Upgrade / Uninstall Matrix
- [ ] Sideloading MSIX install succeeds before Store submission.
- [ ] Fresh install on Windows 10/11 x64.
- [ ] Fresh install on Windows 11 ARM64.
- [ ] Fresh install on manifest `MinVersion` OS build to verify compatibility.
- [ ] Upgrade from previous Store package on x64.
- [ ] Upgrade from previous Store package on ARM64.
- [ ] Uninstall removes package cleanly without blocking residue.

## D. Runtime Functional Validation
- [ ] App launches normally after Store package install.
- [ ] Core WSL operations execute successfully.
- [ ] External process invocation (`wsl.exe`) works under packaged app context.
- [ ] App remains responsive under typical operation flows.

## D2. Packaged App Path and Context Validation
- [x] PowerShell module loads correctly from package install path.
- [x] Config files and templates are resolved correctly relative to the packaged binary.
- [x] Working directory is correct and does not cause path resolution failures.
- [x] `AppData` and registry reads/writes work identically to standalone version.
- [x] Settings/data from a prior standalone install are accessible or migration path is documented.

## E. Offline and Reliability Validation
- [ ] App startup works without internet.
- [ ] Core local WSL management flow works offline.
- [ ] App handles missing/disabled WSL prerequisites with clear message.

## F. Certification-Oriented Checks
- [ ] Windows App Certification Kit (WACK) passes.
- [x] No manifest schema errors.
- [ ] No package integrity/signature errors.
- [ ] No blocked capability usage beyond declared `runFullTrust`.

## G. Store Listing/Metadata Validation
- [ ] Privacy policy URL is reachable and valid.
- [x] Support URL/email is reachable.
- [ ] Desktop screenshots meet requirements (≥ 1366×768, `.png`, ≤ 50 MB).
- [x] 1:1 App tile icon (300×300 px) is provided and valid.
- [x] Store listing Description is complete and ≤ 10,000 chars.
- [x] Category is selected in Properties.
- [ ] System Requirements are declared if applicable.
- [ ] Product Declarations are reviewed and set correctly.
- [x] Submission notes include `runFullTrust` justification and testing instructions.

## H. Regression Safety
- [x] Existing standalone packaging still works.
- [ ] Existing standalone installer output is unchanged.
- [x] Existing portable zip output is unchanged.

## I. Pricing and Metadata Verification
- [x] Base price is set to **Free** in Partner Center.
- [x] Markets selection is confirmed.
- [ ] Store deep link (`ms-windows-store://pdp/?ProductId=9MTK4BR3V436`) resolves after publishing.

## Evidence
- [x] Attach build logs.
- [ ] Attach WACK output.
- [ ] Attach install/upgrade/uninstall test records.
- [x] Attach packaged app path/context test records.
- [x] Attach final artifact list and paths.

### Artifact Evidence
- `D:\repo\Local\DistroNexus\release\store\DistroNexus.Package_2.0.1.0_Test\DistroNexus.Package_2.0.1.0_x64_ARM64.msixbundle` (~146.60 MB)
- `D:\repo\Local\DistroNexus\release\store\DistroNexus.Package_2.0.1.0_x64_ARM64_bundle.msixupload` (~146.62 MB)

## Sign-off
- Test Owner: Pending
- Reviewer: Pending
- Result: [ ] Pass  [x] Pass with Exceptions  [ ] Fail
