# Task List for v1.0.2

## 1. Core Logic & Backend

### 1.1. Settings & Configuration
- [ ] **Config Update**: Update `Settings` struct to include `PackageCachePath` and `DefaultInstallPath`.
- [ ] **Persistence**: Ensure these new fields are correctly saved to and loaded from `settings.json`.
- [ ] **Custom Sources**: Implement a data structure and storage mechanism for "Custom Package Sources" (Name, Version, URL/Path).

### 1.2. Instance Management (Logic)
- [ ] **Disk Usage**: Implement a function to calculate the size of the instance's directory (specifically `ext4.vhdx`).
- [ ] **Stop Instance**: Implement `wsl --terminate <distro_name>` wrapper.
- [ ] **Rename Instance**: Implement logic to rename a registered WSL instance (Note: WSL doesn't natively support renaming easily; imply export/import or registry edit? *Refinement needed: If just display name, update local config. If actual WSL name, requires complex logic. Assumption: Display Name for this iteration or check feasibility*) -> *Refined: Implement logic to change local display name reference first, investigate actual WSL rename feasibility (export/import).*
- [ ] **Move Instance**: Implement logic to:
    1.  Export instance to tar/vhdx? Or move files if stopped? (Safer: `wsl --export`, unregister, `wsl --import` to new location).
    2.  Update configuration with new path.
- [ ] **Credentials**: Research and implement `wsl <distro> config --default-user <user>` or `passwd` command injection logic to reset credentials.

### 1.3. Package Management (Logic)
- [ ] **Cache Enumeration**: Create function to list files in the configured `PackageCachePath`.
- [ ] **Download Manager**: Refactor existing download logic to support independent downloads (not tied to immediate install).
- [ ] **Delete Package**: Implement function to delete files from cache.
- [ ] **Fetch Online List**: logical function to fetch `distros.json` independent of UI.

## 2. UI / Fyne Implementation

### 2.1. Main Window & Navigation
- [ ] **Icon Update**: Change "Installed" tab icon to `theme.HomeIcon()`.
- [ ] **New Tab**: Create "Package Management" tab with `theme.StorageIcon()` (or similar).
- [ ] **Tab Reordering**: Ensure "Installed" is the primary default view.

### 2.2. Installation UI Refactor
- [ ] **Modal Conversion**: Convert "Install" view from a main tab to a `dialog.NewCustom(...)` or a new Fyne Window.
- [ ] **Trigger**: Add a prominent "Plus" or "Add" button in the main toolbar/header to open the Install modal.
- [ ] **Location Picker**: Add `dialog.NewFolderOpen` trigger to select install destination in the modal.
- [ ] **Defaults**: Pre-fill destination with `DefaultInstallPath`.

### 2.3. Installed Instance Cards
- [ ] **Layout Update**: specific area to show "Disk Usage" (e.g., bottom right of card).
- [ ] **Action Buttons**: Add Toolbar or ButtonGroup to card:
    - [ ] **Stop**: (Enable only if running).
    - [ ] **Move**: Triggers folder picker -> logic.
    - [ ] **Rename**: Triggers input dialog.
    - [ ] **Credentials**: Triggers username/password dialog.

### 2.4. Package Management Tab
- [ ] **Split View**:
    - **Left/Top**: Online Sources (List of official + custom).
    - **Right/Bottom**: Local Cache (List of downloaded files).
- [ ] **Online Actions**: "Download" button for each item. "Update List" global button.
- [ ] **Cache Actions**: "Delete" button, "Install" button (optional, links to install flow).
- [ ] **Add Custom**: Dialog to input Name, URL/Path for custom source.

### 2.5. Global Settings UI
- [ ] **Paths**: Add File/Folder pickers for:
    - Default Install Path.
    - Package Cache Path.

## 3. Review & Testing
- [ ] **Safety Check**: Verify "Stop" confirmation dialog works.
- [ ] **Data Safety**: Test "Move" function carefully to ensure no data loss (backup first recommended during dev).
- [ ] **Path Handling**: Test with paths containing spaces and different drives (C: vs D:).
