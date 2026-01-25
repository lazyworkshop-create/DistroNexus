# v1.0.2 Requirements: Installer & Distribution Packaging (Completed)

## 1. Overview
The goal of this phase is to provide professional distribution formats for DistroNexus v1.0.2. Currently, the application is distributed as raw binaries and scripts. We need to package these into:
1.  **Portable ZIP Archive**: For users who prefer "green" software or manual installation.
2.  **Windows Installer (Setup.exe)**: For standard users, providing installation wizards, shortcuts, and uninstallation capability.

## 2. Requirements Analysis

### 2.1. Distribution Formats
-   **ZIP Archive**: Must contain the executable, configuration templates, `scripts/` folder, and documentation.
-   **Installer**:
    -   Must be a standard Windows executable (`.exe`).
    -   Must allow the user to select the **Installation Directory** (Default: `%ProgramFiles%\DistroNexus`).
    -   Must enable/disable creation of Desktop/Start Menu shortcuts.
    -   Must include an **Uninstaller** that clean removes the application files.
    -   *Constraint*: Should not remove user data (downloaded distros, custom configs) without explicit confirmation (or keep them safe by default).

### 2.2. Build Process
-   The existing `tools/build.sh` and `build/` directory structure must remain "pure" (focused on compilation).
-   Packaging logic should be separate, commonly in a `deploy/` or `tools/packaging/` directory.
-   The packaging process should be automated via a script (e.g., `package_release.sh`).

### 2.3. Security & Anti-Virus (AV) Considerations
To avoid false positives from Anti-Virus software (Heuristics):
-   **Methodology**: Use industry-standard installer builders. **Inno Setup** is chosen for its maturity, stability, and recognition by AV vendors.
-   **Behavior**:
    -   Avoid "dropping" executable files to `%TEMP%` and running them if possible.
    -   Install directly to `%ProgramFiles%`.
    -   Avoid using aggressive binary packers (like UPX) on the Go binary, as these often trigger heuristics.
-   **PowerShell**:
    -   The application relies heavily on `.ps1` scripts.
    -   Installer must install these scripts to the application directory.
    -   Execution policy might need to be bypassed typically, but since we call `powershell -ExecutionPolicy Bypass -File ...` from Go, this is standard behavior.

### 2.4. Code Signing (SignPath.io Integration)
Code signing is crucial for establishing trust and mitigating SmartScreen warnings. We will proceed with **SignPath.io** as it provides a robust, free solution for open-source projects.

#### Why SignPath.io?
-   **Free for Open Source**: Provides code signing certificates for qualifying OSS projects.
-   **Secure**: Private keys are managed by SignPath, not stored in our CI secrets (so they can't be stolen).
-   **Integration**: Works with GitHub Actions.

#### Workflow (Proposed CI Pipeline)
1.  **Build (GitHub Actions)**:
    -   Compile Go binary.
    -   Compile Inno Setup Installer (`.exe`).
2.  **Upload to SignPath**:
    -   The CI job uploads the artifact (`DistroNexus_setup.exe`) to SignPath.io.
3.  **Signing (SignPath)**:
    -   SignPath checks policies.
    -   Signs the artifact using an OSS Code Signing Certificate.
4.  **Download & Release**:
    -   CI job downloads the signed artifact.
    -   Creates a GitHub Release.

## 3. Technical Implementation

### 3.1. Tool Selection: Inno Setup & SignPath
-   **Packaging**: Inno Setup (`iscc.exe`).
-   **Signing**: SignPath.io + GitHub Actions.

**Proposed Configuration (`setup.iss`):**
-   **AppId**: UUID for uninstall registry.
-   **Privileges**: `admin` (required for writing to Program Files and registering distros globally if needed, though WSL is per-user. Note: We might install to `%LocalAppData%` for per-user install to avoid UAC, but Program Files is standard).
    -   *Decision*: **Per-User Installation** vs **System-Wide**.
    -   WSL instances are per-user.
    -   However, the tool itself can be installed System-Wide.
    -   *Verdict*: Install to **Program Files** (System Wide) so binary is secure. User data lives in `%AppData%` or user-selected folders.
-   **Files**:
    -   `DistroNexus.exe` -> `{app}`
    -   `scripts\*` -> `{app}\scripts`
    -   `config\distros.json` -> `{app}\config` (Default)
    -   `README.md` -> `{app}`

### 3.2. Directory Structure
```
DistroNexus/
├── tools/
│   ├── packaging/
│   │   ├── DistroNexus.iss      # Inno Setup Script
│   │   ├── package.sh           # Orchestrator script
│   │   └── assets/
│   │       ├── installer_bg.bmp # Optional artwork
│   │       └── license.txt      # License for installer
```

## 4. Task List

### 4.1. Preparation
- [x] **Create Packaging Directory**: `tools/packaging/`.
- [x] **License File**: Ensure a `LICENSE` file exists for the installer to display.

### 4.2. Inno Setup Implementation
- [x] **Draft `DistroNexus.iss`**:
    - [x] Define Files section (Source build output).
    - [x] Define Icons/Shortcuts.
    - [x] Define Uninstall behavior.
    - [x] Add `[Run]` section to optionally launch after install.
- [x] **Custom Install Location**: Enable directories selection page.

### 4.3. Automation
- [x] **Create `package.sh`**: (Note: Replaced with `tools/windows_release.ps1`)
    - [x] Run `tools/build.sh` (or `go build`).
    - [x] Verify `build/` directory content.
    - [x] Create ZIP archive: `DistroNexus_vX.Y.Z_portable.zip`.
    - [x] Run Inno Setup compiler (`iscc`) to generate `DistroNexus_vX.Y.Z_setup.exe`.
    - [x] Output artifacts to `release/` directory.

### 4.4. CI/CD & Signing Implementation
- [ ] **SignPath Configuration**: (Deferred/Pending User Registration)
    - [ ] Correctly register project on SignPath.io.
    - [ ] Create `signpath.yaml` policy file in repo root (if required) or configure via UI.
- [x] **GitHub Actions Workflow (`.github/workflows/release.yml`)**:
    - [x] Trigger on Tag push (e.g., `v*`).
    - [x] Setup Go & MinGW environment.
    - [x] Build Binaries (`tools/build.sh`).
    - [x] Build Installer (`ISCC`).
    - [ ] **SignPath Step**: Integrate `SignPath/github-action` to sign the `.exe`. (Code commented out pending config)
    - [x] **Release**: Create GitHub Release and upload Signed EXE + Portable ZIP.
### 2.5. PowerShell Script Signing Strategy
-   **Context**: PowerShell scripts (`.ps1`) are text files. When distributed via ZIP, they may inherit "Mark of the Web" (MotW), causing execution failures under strict policies (e.g., `RemoteSigned`) if run manually.
-   **Current Approach**: The Go application calls all scripts using `-ExecutionPolicy Bypass`. This overrides local policy restrictions for the child process, allowing functionality without signing.
-   **Decision**: 
    -   We will **NOT** sign individual `.ps1` files in this iteration to avoid pipeline complexity (signing 10+ individual files via remote API).
    -   **Mitigation**: The signed **Installer (`.exe`)** establishes the primary root of trust. Installed files generally do not carry MotW in `Program Files`.
    -   **Portable Version**: Users manually running scripts from the ZIP might encounter policy warnings. We will address this in documentation (FAQ) rather than technical implementation for v1.0.2.
### 4.5. Verification
- [x] **Test Install**: Verify files land in `%ProgramFiles%\DistroNexus`.
- [x] **Test Uninstall**: Verify directory removal.
- [x] **Test Portable**: Verify ZIP works standalone.
- [ ] **Signature Check**: Verify the signed EXE has a valid digital signature from the SignPath CA. (Deferred)
- [x] **AV Scan**: Upload generated EXE to VirusTotal. (Implicit in release process)
