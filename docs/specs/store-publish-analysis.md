# Microsoft Store Publishing Analysis

## 1. Executive Summary

**Decision:** **Publish using MSIX** (via Windows Application Packaging Project).

We have confirmed the decision to use the **MSIX** format for DistroNexus. This approach provides:
1.  **Automatic Updates**: The Store handles versioning, delta updates, and installation.
2.  **Clean Uninstall**: MSIX containers ensure no registry rot or leftover files.
3.  **Capability Support**: The `runFullTrust` capability in MSIX explicitly supports the required interop with `wsl.exe` and `powershell.exe`.

Identity details have been secured and recorded in Section 2.1.

## 2. Prerequisites

Before technical implementation, the following administrative steps are required:

| Item | Details | Cost |
| :--- | :--- | :--- |
| **Developer Account** | Register at [Partner Center](https://partner.microsoft.com/dashboard/registration). | ~$19 USD (One-time) |
| **Publisher Name** | You must choose a global `Publisher Display Name` (CN) that matches your certificate. | Included |
| **App Name** | Reserve "DistroNexus" in Partner Center. **Action**: Record `Package Identity Name`, `Publisher ID`, and `Package Family Name` below. | Free with account |

### 2.1. Store Identity Information (Confirmed)
The following values are confirmed from **Product Management > Product Identity** in Partner Center and will be used in `Package.appxmanifest` and submission metadata.

| Field | Value | Notes |
| :--- | :--- | :--- |
| **Package Identity Name** | `LazyWorkshopCreate.DistroNexus` | FROM PARTNER CENTER |
| **Publisher ID** | `CN=C4B6BD9D-352C-4CE3-82BD-5A54506C898B` | FROM PARTNER CENTER |
| **Publisher Display Name** | `LazyWorkshop Create` | FROM PARTNER CENTER |
| **Package Family Name (PFN)** | `LazyWorkshopCreate.DistroNexus_v70wxt9jxp4nt` | FROM PARTNER CENTER |
| **Package SID** | `S-1-15-2-95963626-3239856491-2024139146-3067358567-2809728608-175757622-226365827` | FROM PARTNER CENTER |
| **Store ID** | `9MTK4BR3V436` | FROM PARTNER CENTER |
| **Store Protocol Link** | `Available after the product is live` | Expected format: `ms-windows-store://pdp/?productid=...` |
| **Web Store URL** | `Available after the product is live` | Expected format: `https://apps.microsoft.com/detail/...` |

## 3. Technical Requirements: MSIX vs Win32

### Comparison

| Feature | MSIX (Packaging Project) | Win32 (Raw .exe/.msi) |
| :--- | :--- | :--- |
| **WSL Interop** | **Supported** (via `runFullTrust`) | **Supported** (Native) |
| **Updates** | Handled by Store (Automatic) | Handled by App (Manual check) |
| **Installation** | One-click, Silent, Per-user | Requires Installer Silent Flags |
| **File System** | Virtualized (VFS), but can write to AppData | Full Access |
| **Registry** | Virtualized | Full Access |

### MSIX `runFullTrust` Verification
For a WSL Manager, we need to execute external processes (`wsl.exe`, `bash.exe`).
*   **Verdict**: **Confirmed**. The `runFullTrust` capability creates a "Full Trust" component that escapes the UWP sandbox, allowing the application to run with the user's privileges and execute standard system binaries.

### Inno Setup (Current State)
If we were to pursue the Win32 route, the current `tools/installer.iss` has gaps:
*   **Silent Install**: Store requires a truly silent install switch (usually `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`).
*   **Admin Rights**: The script uses `PrivilegesRequired=admin`. The prompt for elevation during a silent store install can sometimes fail validation depending on how the store wrapper invokes it.

## 4. Technical Gap Analysis

Current Repository Status vs. Store Requirements:

| Component | Status | Missing Action |
| :--- | :--- | :--- |
| **Packaging Project** | 🔴 Missing | Create `src/DistroNexus.Package/DistroNexus.Package.wapproj`. |
| **App Manifest** | 🔴 Missing | Create `Package.appxmanifest` with `runFullTrust`. |
| **Store Assets** | 🔴 Missing | Generate PNG assets (Logos: 44, 50, 150, 310px). |
| **Privacy Policy** | 🔴 Missing | A hosted Markdown/HTML page is required for the Store listing. |
| **Versioning** | 🟡 Partial | Need to sync package version (4 parts, e.g., 2.0.1.0) with AssemblyVersion. |

## 5. Assets & Listing Requirements

### Graphical Assets
All images should be PNG.

| Asset Type | Size (px) | Purpose |
| :--- | :--- | :--- |
| **Store Logo** | 50 x 50 | Partner Center listing icon. |
| **Square 44x44 Logo** | 44 x 44 | App list, taskbar, start menu tile (small). |
| **Square 150x150 Logo** | 150 x 150 | Start menu tile (medium). |
| **Wide 310x150 Logo** | 310 x 150 | Start menu tile (wide). |
| **App Icon** | .ico | Embedded in executable (Already exists). |
| **Screenshots** | 1920 x 1080 | At least 1 requires showing the app in action. |

### Textual Requirements
*   **Description**: 10+ characters.
*   **Privacy Policy URL**: Must be a valid link (e.g., `https://distronexus.org/privacy`).
*   **Support Contact**: Email or Issue Tracker URL.
*   **Age Rating**: IARC Questionnaire (Result: usually 3+ for tools, but "Users Interact" may apply if cloud features exist).

## 6. Distribution Strategy: Hybrid (Standalone + Store)

**Objective**: Maintain the existing standalone ZIP/Installer for GitHub/Manual downloads while adding a Store-specific MSIX workflow.

### 6.1. Build Workflow Separation
We will implement a parallel build strategy in `tools/build.ps1`:

| Channel | Format | Build Command | Output |
| :--- | :--- | :--- | :--- |
| **GitHub / Manual** | `.zip` (Portable) / `.exe` (Inno) | `dotnet publish` | `release/DistroNexus-v2.0.1-Release.zip` |
| **Microsoft Store** | `.msixbundle` | `msbuild /t:Publish /p:GenerateAppxPackageOnBuild=true` on the **Package Project** | `release/DistroNexus_2.0.1.0_x64_arm64.msixbundle` |

### 6.2. Solution Structure Changes
1.  **Keep**: `DistroNexus.Desktop.csproj` (The main app).
2.  **Add**: `DistroNexus.Package.wapproj` (The Store wrapper).
    *   This project essentially "references" the Desktop app and wraps it in the MSIX container.
    *   It will **NOT** affect the standard build or debugging of the desktop app.

### 6.3. Implementation Details
*   **Version Sync**: The build script must update both `AssemblyVersion` (for .exe) and `Package.appxmanifest` version (for .msix) to ensure they match (e.g., `2.0.1.0`).
*   **Artifacts**: The CI/CD pipeline (Actions) will produce two distinct sets of artifacts.

### 6.4 Updated Action Plan (Phase 2)
1.  **Create Packaging Project**: `src/DistroNexus.Package/DistroNexus.Package.wapproj`.
2.  **Modify `tools/build.ps1`**:
    *   Add parameter `[switch]$StoreBuild`.
    *   If `$StoreBuild` is present, execute the `msbuild` command for the packaging project.
    *   If `$CreateZip` is present, execute the existing `dotnet publish` and zip logic.

## 7. Store Requirements Addendum (Based on Review)

### 7.1. Architecture & Packaging Strategy
*   **Recommendation**: **Bundle (x64 + ARM64)**.
    *   **Reasoning**: As a WSL manager, the app invokes `wsl.exe` and `bash.exe`. On Windows on ARM devices, running the app as x64 (emulated) can cause issues with file system redirection when calling native system binaries.
    *   **Action**: Create separate build configurations for `x64` and `arm64`, then package them into a single `.msixbundle`.
    *   **Note**: Ensure any native P/Invoke calls have checks or compatible DLLs for ARM64 if they aren't standard Windows APIs.

### 7.2. Visual Assets (Unplated)
*   **Requirement**: **Highly Recommended for Windows 10/11 Taskbar**.
*   **Details**: The standard square logos will appear inside a colored "plate" on the Taskbar. To look native (transparent background), you need **Target-size based assets** with the `unplated` qualifier.
*   **Action**: Generate `Square44x44Logo.targetsize-*-altform-unplated.png` versions (sizes: 16, 24, 32, 48, 256).

### 7.3. Localization
*   **Requirement**: **Store Listing vs. Package Language**.
    *   You **do not** need localized Store descriptions for every language your package supports. You can list only in English (US) even if the binary supports Chinese.
    *   **Warning**: If your package declares `zh-CN` support in `Package.appxmanifest` but you don't provide a Chinese store listing, users in China will see the English listing but get the localized app (acceptable).
    *   **Recommendation**: Add a Chinese (Simplified) Store listing description to match the app capabilities.

### 7.4. Privacy Policy Compliance
*   **Requirement**: **Specific Disclosures for System Tools**.
*   **Content**: Since the app has `runFullTrust` and accesses file systems:
    1.  **Data Collection**: Explicitly state if *any* data works its way back to a server (e.g., telemetry, crash reports).
    2.  **Local Processing**: Explicitly state that "All WSL management operations occur locally on the user's device."
    3.  **Third-Party**: If you use AppCenter or Google Analytics, this must be disclosed.
*   **Action**: Add a specific "Data Handling" section to the `docs/privacy-policy.md`.

### 7.5. Generative AI Disclosure
*   **Requirement**: **New Store Policy (Late 2023/2024)**.
*   **Status**: **Low Risk / Safe**.
    *   Since the app uses "templates" (`config/templates/ai-ml-gpu-dev`) to *setup* environments but does not *generate* content (text, images, code) reliably at runtime using a model, it likely does **not** need the specific "AI Generated Content" disclosure tag.
    *   **Caveat**: If you add a feature that uses an LLM to write WSL configs dynamically, you MUST check the "This product features generative AI" box during submission.
    *   **Action**: For now, treat it as a standard tool. Do not check the "AI" box unless the app generates new content dynamically.

## 8. Missing Items Found in Review (Must Add)

### 8.1 Pre-Submission Validation (Missing)
*   **Windows App Certification Kit (WACK)**: Add a mandatory validation step before upload to catch manifest/capability/package issues early.
*   **Install/Upgrade/Uninstall Matrix**: Validate fresh install, upgrade from previous Store version, and uninstall cleanup for both x64 and ARM64.
*   **Offline Startup Check**: Verify app startup and core WSL management operations without internet.

### 8.2 Capability & Policy Traceability (Missing)
*   Add a short "Capability Justification" block in this document for `runFullTrust` (why needed, what binaries are launched, user-impact scope).
*   Keep a clear statement that DistroNexus does not require kernel drivers, background services, or startup tasks for baseline operation.

### 8.3 Listing Completeness (Missing)
*   **Release Notes Template**: Prepare a reusable changelog format for every Store submission.
*   **Support URL and Contact Email**: Define and freeze values now (not only issue tracker link).
*   **Known Limitations in Description**: Explicitly mention that WSL features depend on Windows optional component status and user environment.

### 8.4 Operational Readiness (Missing)
*   **Rollback Plan**: Define what to do if certification fails or a bad package is published (withdraw + hotfix version increment).
*   **Versioning Rule**: Enforce four-part package version policy (e.g., `2.0.1.0`) and monotonic increment for every resubmission.
*   **Artifact Retention**: Keep submitted `.msixbundle`, symbols, and manifest snapshot per release for audit/repro.

### 8.5 Recommended Next Actions
1.  Fill PFN/Package SID/Store protocol link in Section 2.1 from Partner Center.
2.  Add a "Pre-Submission Checklist" subsection to track WACK + matrix test status.
3.  Finalize support/privacy URLs and use them consistently in Partner Center and website docs.
