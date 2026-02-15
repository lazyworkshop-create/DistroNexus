# Store Publish Implementation Checklist

Date: 2026-02-15  
Source Spec: `docs/specs/store-publish-analysis.md`

## A. Scope and Channel Strategy
- [ ] Keep existing standalone distribution flow (`zip`/Inno Setup) unchanged.
- [ ] Add Store-only packaging flow for MSIX.
- [ ] Ensure implementation scope is limited to Store packaging and related docs.
- [ ] Confirm pricing strategy: **Free** (Base price in Partner Center).
- [ ] Confirm target markets: **All available markets** (default) or specify restrictions.

## B. Store Packaging Project
- [ ] Create `src/DistroNexus.Package/`.
- [ ] Add `DistroNexus.Package.wapproj`.
- [ ] Reference `src/Client/DistroNexus.Desktop/DistroNexus.Desktop.csproj` as entry app.
- [ ] Keep desktop app normal debug/build behavior unchanged.
- [ ] Set up test certificate or enable Developer Mode for local sideloading during development.

## C. Manifest Implementation (`Package.appxmanifest`)
- [ ] Set `Identity Name` = `LazyWorkshopCreate.DistroNexus`.
- [ ] Set `Identity Publisher` = `CN=C4B6BD9D-352C-4CE3-82BD-5A54506C898B`.
- [ ] Set `PublisherDisplayName` = `LazyWorkshop Create`.
- [ ] Set `Identity Version` using `Major.Minor.Patch.0` format (4th part must be `0`, reserved for Store).
- [ ] Add `xmlns:rescap` namespace.
- [ ] Add `rescap` to `IgnorableNamespaces`.
- [ ] Declare `<rescap:Capability Name="runFullTrust" />`.
- [ ] Set `TargetDeviceFamily` to `Windows.Desktop`.
- [ ] Set `MinVersion` (e.g., `10.0.19041.0`) and `MaxVersionTested` (e.g., `10.0.22621.0`).
- [ ] Configure app executable mapping correctly to desktop binary.

## C2. Packaged App Path Compatibility
- [ ] Verify PowerShell module loads correctly from package install path (`C:\Program Files\WindowsApps\...`).
- [ ] Verify config files and templates resolve correctly relative to the packaged binary.
- [ ] Verify working directory assumptions do not break under MSIX context.
- [ ] Ensure `AppData` / registry paths work identically to standalone (full trust apps are not virtualized).
- [ ] Document or implement settings migration path for users switching from standalone to Store version.

## D. Build Script Integration (`tools/build.ps1`)
- [ ] Add Store build switch (for example, `-StoreBuild`).
- [ ] Build x64 package.
- [ ] Build ARM64 package.
- [ ] Produce one multi-arch bundle (`.msixbundle`).
- [ ] Produce Store submission artifact as `.msixupload` (preferred).
- [ ] Skip external certificate signing for Store builds (Store signs packages automatically).
- [ ] Keep existing non-Store release outputs unchanged.

## E. Store Assets and Metadata Preparation
- [ ] Prepare Desktop screenshots: ≥ 1366×768 px, `.png`, ≤ 50 MB; at least 1, recommend 4+.
- [ ] Prepare 1:1 App tile icon (300×300 px) — strongly recommended; Store prioritizes over package icon.
- [ ] Add optional unplated assets for better taskbar/start appearance.
- [ ] Optional: 16:9 Super hero art (1920×1080 or 3840×2160 px) for promotional display.
- [ ] Write Store listing **Description** (required, ≤ 10,000 chars).
- [ ] Write **Short Description** (recommended, ≤ 270 chars for best display).
- [ ] Prepare **What's new in this version** text for updates.
- [ ] Prepare release notes template for Store submissions.
- [ ] Prepare support URL and support contact email.
- [ ] Consider multi-language Store listings (English + Chinese) if app ships localized resources.

## F. Privacy and Compliance Docs
- [ ] Publish valid privacy policy URL.
- [ ] Ensure privacy policy includes local-processing disclosure for WSL operations.
- [ ] Ensure privacy policy is consistent with actual telemetry/network behavior.

## G. Partner Center Submission Setup
- [ ] Configure submission package using Store artifact.
- [ ] Select **Category** (required) and optional subcategory (e.g., *Utilities & tools > Developer tools*).
- [ ] Fill restricted capability justification for `runFullTrust` in Submission options.
- [ ] Provide **Notes for certification**: testing steps, feature access instructions, WSL prerequisite notes.
- [ ] Complete age rating questionnaire.
- [ ] Review **Product Declarations** checkboxes (network usage, in-app purchases, etc.).
- [ ] Declare **System Requirements** if applicable (e.g., WSL feature must be enabled).
- [ ] Provide **Contact Details** (required for business/company accounts).
- [ ] Complete listing text and assets.

## H. Versioning and Release Operations
- [ ] Enforce four-part package version format (`Major.Minor.Patch.0`); 4th part must be `0` (reserved for Store).
- [ ] Ensure first three version parts always increase for resubmissions.
- [ ] Archive submission artifacts (`.msixbundle`, `.msixupload`, symbols, manifest snapshot).

## Sign-off
- Owner: Copilot (GPT-5.3-Codex)
- Reviewer: Pending
- Result: [ ] Pass  [ ] Pass with Exceptions  [ ] Fail
