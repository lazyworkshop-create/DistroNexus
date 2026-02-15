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
- Store deep link: `ms-windows-store://pdp/?ProductId=9MTK4BR3V436` (available after product goes live)
- Web Store URL: `https://apps.microsoft.com/detail/9MTK4BR3V436` (available after product goes live)

## Pricing and Availability
- **Base price**: Free (required field in Partner Center).
- **Markets**: All available markets (default). Adjust if regional restrictions apply.
- **Discoverability**: Make this product available and discoverable in the Store (default).
- **Organizational licensing**: Default (allow organizations to acquire).

## Packaging Conclusions
- Add `src/DistroNexus.Package/DistroNexus.Package.wapproj` as a Store wrapper project.
- Add `Package.appxmanifest` with:
    - Identity values exactly matching Partner Center.
    - `<rescap:Capability Name="runFullTrust" />` for WSL-related process execution.
    - `xmlns:rescap` namespace and `IgnorableNamespaces` including `rescap`.
    - `TargetDeviceFamily` constrained to `Windows.Desktop` with `MinVersion` and `MaxVersionTested` (e.g., `10.0.19041.0` / `10.0.22621.0`).
- Keep `src/Client/DistroNexus.Desktop/DistroNexus.Desktop.csproj` as the main runtime project.
- Update `tools/build.ps1` with a Store-specific path (e.g., `-StoreBuild`) without changing existing standalone outputs.
- **Packaged app runtime considerations**:
    - The app installs into a read-only location under `C:\Program Files\WindowsApps\`. Verify all resource paths (PowerShell module, config files, templates) resolve correctly relative to the package install path.
    - Full trust Desktop Bridge apps (`runFullTrust`) are **not** virtualized — `AppData` and registry writes go to real locations, same as standalone.
    - Working directory may differ from standalone; avoid hardcoded or relative-path assumptions.
    - If users switch from standalone to Store version, document any manual settings migration steps or implement automatic detection.

## Submission-Blocking Items
- Generate required Store visual assets:
    - Desktop screenshots: ≥ 1366×768 px, `.png`, ≤ 50 MB; at least 1, recommend 4+.
    - 1:1 App tile icon: 300×300 px (strongly recommended for apps; Store prioritizes over package icon).
    - Optional: unplated variants, 16:9 Super hero art (1920×1080).
- Publish a valid privacy policy URL before submission.
- Select **Category** (required) and optional subcategory in Properties (e.g., *Utilities & tools > Developer tools*).
- Prepare **Store listing Description** (required, ≤ 10,000 chars) and optional **Short Description** (≤ 270 chars for best display).
- Declare **System Requirements** in Properties if applicable (e.g., WSL feature required).
- Review **Product Declarations** checkboxes in Properties (e.g., network usage, no in-app purchases).
- In Partner Center **Submission options**, provide detailed justification for restricted capability `runFullTrust` (this can extend certification time). Also include testing steps, feature access instructions, and WSL prerequisite notes for certification testers.
- Provide **Contact Details** (required for business/company accounts).
- Run pre-submission validation:
    - Windows App Certification Kit (WACK)
    - Sideloading MSIX install verification before Store submission
    - Install / upgrade / uninstall verification
    - Offline startup and core WSL operations verification

## Operational Rules
- Use four-part package versioning (`Major.Minor.Patch.0`); **the 4th part is reserved for Store use and must be left as `0`** when building. Only increment the first three parts (`Major.Minor.Patch`) monotonically on every resubmission.
- Store will sign the package automatically; the developer build does **not** need an external trusted certificate.
- Keep release artifacts per submission (`.msixbundle`, `.msixupload`, manifest snapshot, symbols) for rollback and audit.
- If a release fails certification or post-release quality checks, withdraw and submit a hotfix with a higher package version.
- Consider providing multi-language Store listings (e.g., English + Chinese) if the app ships with localization resources.

## Current Status
- Partner Center registration: **Completed**
- Product identity collection: **Completed**
- Distribution strategy selection (MSIX for Store + existing packaging retained): **Completed**
- Engineering implementation (`.wapproj`, manifest, build script updates): **Pending**
- Store assets + privacy policy publication: **Pending**
- Submission options capability justification (`runFullTrust`): **Pending**
- Store listing content (Category, Description, screenshots, metadata): **Pending**
- System Requirements and Product Declarations review: **Pending**

## Execution Checklists
- Implementation checklist: `docs/development/store-publish-implementation-checklist.md`
- Test checklist: `docs/development/store-publish-test-checklist.md`
- Acceptance checklist: `docs/development/store-publish-acceptance-checklist.md`
