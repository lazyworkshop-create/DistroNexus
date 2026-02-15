# Microsoft Store Publishing Conclusions

## Final Decision
- Use a **hybrid distribution strategy**:
    - Keep the existing standalone packaging flow (`.zip` / Inno Setup) for GitHub/manual distribution.
    - Add a dedicated **Store-only MSIX packaging flow** for Microsoft Store submission.
- Store channel package: **`.msixbundle`** (multi-arch artifact).
- Store submission upload: **prefer `.msixupload`** (officially recommended for Windows 10/11 submissions; `.msixbundle` is accepted but not preferred).
- Architecture strategy: **x64 + ARM64** in one bundle.

## Confirmed Store Identity
- Package/Identity/Name: `LazyWorkshopCreate.DistroNexus`
- Package/Identity/Publisher: `CN=C4B6BD9D-352C-4CE3-82BD-5A54506C898B`
- Package/Properties/PublisherDisplayName: `LazyWorkshop Create`
- Package Family Name (PFN): `LazyWorkshopCreate.DistroNexus_v70wxt9jxp4nt`
- Package SID: `S-1-15-2-95963626-3239856491-2024139146-3067358567-2809728608-175757622-226365827`
- Store ID: `9MTK4BR3V436`
- Store deep link: Available after product goes live
- Web Store URL: Available after product goes live

## Packaging Conclusions
- Add `src/DistroNexus.Package/DistroNexus.Package.wapproj` as a Store wrapper project.
- Add `Package.appxmanifest` with:
    - Identity values exactly matching Partner Center.
    - `<rescap:Capability Name="runFullTrust" />` for WSL-related process execution.
    - `xmlns:rescap` namespace and `IgnorableNamespaces` including `rescap`.
    - `TargetDeviceFamily` constrained to `Windows.Desktop`.
- Keep `src/Client/DistroNexus.Desktop/DistroNexus.Desktop.csproj` as the main runtime project.
- Update `tools/build.ps1` with a Store-specific path (e.g., `-StoreBuild`) without changing existing standalone outputs.

## Submission-Blocking Items
- Generate required Store visual assets; add unplated variants as a quality recommendation.
- Publish a valid privacy policy URL before submission.
- In Partner Center **Submission options**, provide detailed justification for restricted capability `runFullTrust` (this can extend certification time).
- Run pre-submission validation:
    - Windows App Certification Kit (WACK)
    - Install / upgrade / uninstall verification
    - Offline startup and core WSL operations verification

## Operational Rules
- Use four-part package versioning (`Major.Minor.Patch.Revision`), increment monotonically on every resubmission.
- Keep release artifacts per submission (`.msixbundle`, manifest snapshot, symbols) for rollback and audit.
- If a release fails certification or post-release quality checks, withdraw and submit a hotfix with a higher package version.

## Current Status
- Partner Center registration: **Completed**
- Product identity collection: **Completed**
- Distribution strategy selection (MSIX for Store + existing packaging retained): **Completed**
- Engineering implementation (`.wapproj`, manifest, build script updates): **Pending**
- Store assets + privacy policy publication: **Pending**
- Submission options capability justification (`runFullTrust`): **Pending**
