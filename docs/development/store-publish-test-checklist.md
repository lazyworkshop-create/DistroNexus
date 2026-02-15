# Store Publish Test Checklist

Date: 2026-02-15  
Source Spec: `docs/specs/store-publish-analysis.md`

## A. Build and Packaging Verification
- [ ] Store build runs successfully with Store switch.
- [ ] x64 package is generated successfully.
- [ ] ARM64 package is generated successfully.
- [ ] Multi-arch bundle (`.msixbundle`) is generated successfully.
- [ ] Store upload artifact (`.msixupload`) is generated and valid.
- [ ] Package size is within Store limit (≤ 25 GB per bundle).
- [ ] Package version 4th part is `0` (reserved for Store use).

## B. Manifest and Identity Validation
- [ ] Manifest `Identity Name` matches Partner Center value.
- [ ] Manifest `Identity Publisher` matches Partner Center value.
- [ ] Manifest `PublisherDisplayName` matches Partner Center value.
- [ ] Manifest `Identity Version` uses `Major.Minor.Patch.0` format.
- [ ] `runFullTrust` capability exists and schema validates.
- [ ] `rescap` namespace and `IgnorableNamespaces` are correctly configured.
- [ ] `TargetDeviceFamily` is `Windows.Desktop`.
- [ ] `MinVersion` and `MaxVersionTested` are set and valid.

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
- [ ] PowerShell module loads correctly from package install path.
- [ ] Config files and templates are resolved correctly relative to the packaged binary.
- [ ] Working directory is correct and does not cause path resolution failures.
- [ ] `AppData` and registry reads/writes work identically to standalone version.
- [ ] Settings/data from a prior standalone install are accessible or migration path is documented.

## E. Offline and Reliability Validation
- [ ] App startup works without internet.
- [ ] Core local WSL management flow works offline.
- [ ] App handles missing/disabled WSL prerequisites with clear message.

## F. Certification-Oriented Checks
- [ ] Windows App Certification Kit (WACK) passes.
- [ ] No manifest schema errors.
- [ ] No package integrity/signature errors.
- [ ] No blocked capability usage beyond declared `runFullTrust`.

## G. Store Listing/Metadata Validation
- [ ] Privacy policy URL is reachable and valid.
- [ ] Support URL/email is reachable.
- [ ] Desktop screenshots meet requirements (≥ 1366×768, `.png`, ≤ 50 MB).
- [ ] 1:1 App tile icon (300×300 px) is provided and valid.
- [ ] Store listing Description is complete and ≤ 10,000 chars.
- [ ] Category is selected in Properties.
- [ ] System Requirements are declared if applicable.
- [ ] Product Declarations are reviewed and set correctly.
- [ ] Submission notes include `runFullTrust` justification and testing instructions.

## H. Regression Safety
- [ ] Existing standalone packaging still works.
- [ ] Existing standalone installer output is unchanged.
- [ ] Existing portable zip output is unchanged.

## I. Pricing and Metadata Verification
- [ ] Base price is set to **Free** in Partner Center.
- [ ] Markets selection is confirmed.
- [ ] Store deep link (`ms-windows-store://pdp/?ProductId=9MTK4BR3V436`) resolves after publishing.

## Evidence
- [ ] Attach build logs.
- [ ] Attach WACK output.
- [ ] Attach install/upgrade/uninstall test records.
- [ ] Attach packaged app path/context test records.
- [ ] Attach final artifact list and paths.

## Sign-off
- Test Owner: Pending
- Reviewer: Pending
- Result: [ ] Pass  [ ] Pass with Exceptions  [ ] Fail
