# Task List for v1.0.2

## 0. PowerShell Scripts & Infrastructure

### 0.1. Core Scripts (Refactoring)
- [x] **Download Script**: Update `download_all_distros.ps1` to accept `SelectFamily` and `SelectVersion` parameters for targeted downloads.
- [x] **Install Script**: Refactor `install_wsl_custom.ps1` to invoke `download_all_distros.ps1` instead of duplicate download logic.
- [x] **Install Script**: Ensure `install_wsl_custom.ps1` reloads config after download to pick up new `local_path`.

### 0.2. Instance Registry Management
- [x] **Registry Format**: Define JSON structure for `config/instances.json` (Name, BasePath, State, Release, User, InstallTime).
- [x] **Registration**: Update `install_wsl_custom.ps1` to append new instances to `instances.json` upon success.
- [x] **Deregistration**: Update `uninstall_wsl_custom.ps1` to remove instances from `instances.json` upon uninstallation.
- [x] **Sync/Scan**: Create `scripts/scan_wsl_instances.ps1` to sync `wsl -l -v` state to `instances.json`.

### 0.3. Management Scripts (New)
*Note: All complex WSL interactions should be encapsulated in PowerShell scripts to keep Go logic simple.*
- [x] **Stop Script**: Create `scripts/stop_instance.ps1` (wrapper for `wsl --terminate`).
- [x] **Start Script**: Create `scripts/start_instance.ps1` (wrapper for `wsl -d`, updates LastUsed state).
- [x] **Move Script**: Create `scripts/move_instance.ps1` (Export -> Unregister -> Import logic).
- [x] **Rename Script**: Create `scripts/rename_instance.ps1` (Export -> Unregister -> Import logic).
- [x] **Credentials Script**: Create `scripts/set_credentials.ps1` (Handle `useradd`/`passwd` inside distro).


## 1. Core Logic & Backend

### 1.1. Settings & Configuration
- [ ] **Config Update**: Update `Settings` struct to include `PackageCachePath` and `DefaultInstallPath`.
- [ ] **Persistence**: Ensure these new fields are correctly saved to and loaded from `settings.json`.
- [ ] **Custom Sources**: Implement a data structure and storage mechanism for "Custom Package Sources" (Name, Version, URL/Path).

### 1.2. Instance Management (Logic)
- [ ] **Disk Usage**: Implement a function to calculate the size of the instance's directory (specifically `ext4.vhdx`).
- [ ] **Stop Instance**: Implement Go wrapper to call `scripts/stop_instance.ps1`.
- [ ] **Rename Instance**: Implement Go wrapper to call `scripts/rename_instance.ps1`.
- [ ] **Move Instance**: Implement Go wrapper to call `scripts/move_instance.ps1`.
    - Note: Ensure script updates `instances.json` or returns new path for Go to update.
- [ ] **Credentials**: Implement Go wrapper to call `scripts/set_credentials.ps1`.

### 1.3. Package Management (Logic)
- [ ] **Cache Enumeration**: Create function to list files in the configured `PackageCachePath`.
- [ ] **Download Manager**: Refactor existing download logic to support independent downloads (not tied to immediate install).
- [ ] **Delete Package**: Implement function to delete files from cache.
- [ ] **Fetch Online List**: logical function to fetch `distros.json` independent of UI.

## 2. UI / Fyne Implementation

### 2.1. Main Window & Navigation
- [ ] **Layout**: Switch to `BorderLayout` with top Toolbar and switched Center content.
- [ ] **Toolbar**: Icons only: `Home`, `Package`, `Spacer`, `Add`, `Settings`.

### 2.2. Installation UI Refactor
- [ ] **Quick Mode**: Restore Checkbox. Logic:
    - Checked: Hide Path/User/Pass. Use `DefaultInstallPath/<Name>` and user `root`.
    - Unchecked: Show all fields (including Path Picker).

### 2.3. Installed Instance Cards
- [ ] **Layout Update**: specific area to show "Disk Usage" (e.g., bottom right of card).
- [ ] **Action Buttons**: Add Toolbar or ButtonGroup to card:
    - [ ] **Stop**: (Enable only if running).
    - [ ] **Move**: Triggers folder picker -> logic.
    - [ ] **Rename**: Triggers input dialog.
    - [ ] **Credentials**: Triggers username/password dialog.

### 2.4. Package Management Tab
- [ ] **Unified List**:
    - **Grouped by Distro**: Section headers for "Ubuntu", "Debian", etc.
    - **Rows**: [Name/Version] [Spacer] [Status Icon/Text] [Action Button].
- [ ] **Logic**:
    - Check if file exists in Cache.
    - Status: "Cached (Size)" or "Available".
    - Button: "Delete" vs "Download".

### 2.5. Global Settings UI
- [ ] **Paths**: Add File/Folder pickers for:
    - Default Install Path.
    - Package Cache Path.

## 3. Review & Testing
- [ ] **Safety Check**: Verify "Stop" confirmation dialog works.
- [ ] **Data Safety**: Test "Move" function carefully to ensure no data loss (backup first recommended during dev).
- [ ] **Path Handling**: Test with paths containing spaces and different drives (C: vs D:).
