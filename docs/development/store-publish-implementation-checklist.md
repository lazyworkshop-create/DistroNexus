# Store Publish Implementation Checklist

Date: 2026-02-15  
Source Spec: `docs/specs/store-publish-analysis.md`

## A. Scope and Channel Strategy
- [x] Keep existing standalone distribution flow (`zip`/Inno Setup) unchanged.
- [x] Add Store-only packaging flow for MSIX.
- [x] Ensure implementation scope is limited to Store packaging and related docs.
- [x] Confirm pricing strategy: **Free** (Base price in Partner Center).
- [x] Confirm target markets: **All available markets** (default) or specify restrictions.

## B. Store Packaging Project
- [x] Create `src/DistroNexus.Package/`.
- [x] Add `DistroNexus.Package.wapproj`.
- [x] Reference `src/Client/DistroNexus.Desktop/DistroNexus.Desktop.csproj` as entry app.
- [x] Keep desktop app normal debug/build behavior unchanged.
- [ ] Set up test certificate or enable Developer Mode for local sideloading during development.

## C. Manifest Implementation (`Package.appxmanifest`)
- [x] Set `Identity Name` = `LazyWorkshopCreate.DistroNexus`.
- [x] Set `Identity Publisher` = `CN=C4B6BD9D-352C-4CE3-82BD-5A54506C898B`.
- [x] Set `PublisherDisplayName` = `LazyWorkshop Create`.
- [x] Set `Identity Version` using `Major.Minor.Patch.0` format (4th part must be `0`, reserved for Store).
- [x] Add `xmlns:rescap` namespace.
- [x] Add `rescap` to `IgnorableNamespaces`.
- [x] Declare `<rescap:Capability Name="runFullTrust" />`.
- [x] Set `TargetDeviceFamily` to `Windows.Desktop`.
- [x] Set `MinVersion` (e.g., `10.0.19041.0`) and `MaxVersionTested` (e.g., `10.0.22621.0`).
- [x] Configure app executable mapping correctly to desktop binary.

## C2. Packaged App Path Compatibility
- [x] Verify PowerShell module loads correctly from package install path (`C:\Program Files\WindowsApps\...`).
- [x] Verify config files and templates resolve correctly relative to the packaged binary.
- [x] Verify working directory assumptions do not break under MSIX context.
- [x] Ensure `AppData` / registry paths work identically to standalone (full trust apps are not virtualized).
- [x] Document or implement settings migration path for users switching from standalone to Store version.

## D. Build Script Integration (`tools/build.ps1`)
- [x] Add Store build switch (for example, `-StoreBuild`).
- [x] Build x64 package.
- [x] Build ARM64 package.
- [x] Produce one multi-arch bundle (`.msixbundle`).
- [x] Produce Store submission artifact as `.msixupload` (preferred).
- [x] Skip external certificate signing for Store builds (Store signs packages automatically).
- [x] Keep existing non-Store release outputs unchanged.

## E. Store Assets and Metadata Preparation
- [ ] Prepare Desktop screenshots: ≥ 1366×768 px, `.png`, ≤ 50 MB; at least 1, recommend 4+.
- [x] Prepare 1:1 App tile icon (300×300 px) — strongly recommended; Store prioritizes over package icon.
- [x] Add optional unplated assets for better taskbar/start appearance.
- [ ] Optional: 16:9 Super hero art (1920×1080 or 3840×2160 px) for promotional display.
- [x] Write Store listing **Description** (required, ≤ 10,000 chars).
- [x] Write **Short Description** (recommended, ≤ 270 chars for best display).
- [x] Prepare **What's new in this version** text for updates.
- [x] Prepare release notes template for Store submissions.
- [x] Prepare support URL and support contact email.
- [x] Consider multi-language Store listings (English + Chinese) if app ships localized resources.

## F. Privacy and Compliance Docs
- [ ] Publish valid privacy policy URL.
- [x] Ensure privacy policy includes local-processing disclosure for WSL operations.
- [x] Ensure privacy policy is consistent with actual telemetry/network behavior.

## G. Partner Center Submission Setup
- [ ] Configure submission package using Store artifact.
- [x] Select **Category** (required) and optional subcategory (e.g., *Utilities & tools > Developer tools*).
- [ ] Fill restricted capability justification for `runFullTrust` in Submission options.
- [x] Provide **Notes for certification**: testing steps, feature access instructions, WSL prerequisite notes.
- [ ] Complete age rating questionnaire.
- [ ] Review **Product Declarations** checkboxes (network usage, in-app purchases, etc.).
- [ ] Declare **System Requirements** if applicable (e.g., WSL feature must be enabled).
- [x] Provide **Contact Details** (required for business/company accounts).
- [ ] Complete listing text and assets.

## H. Versioning and Release Operations
- [x] Enforce four-part package version format (`Major.Minor.Patch.0`); 4th part must be `0` (reserved for Store).
- [ ] Ensure first three version parts always increase for resubmissions.
- [x] Archive submission artifacts (`.msixbundle`, `.msixupload`, symbols, manifest snapshot).

## Sign-off
- Owner: Copilot (GPT-5.3-Codex)
- Reviewer: Pending
- Result: [ ] Pass  [x] Pass with Exceptions  [ ] Fail
