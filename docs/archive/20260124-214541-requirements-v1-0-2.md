# DistroNexus v1.0.2 Requirements Document

## 1. Overview
Version 1.0.2 focuses on improving the user interface and enhancing the management capabilities for existing WSL instances. The primary goal is to make the application more intuitive by centering the experience around installed instances and providing critical management tools like moving, stopping, and reconfiguring instances.

## 2. UI/UX Adjustments

### 2.1. Main Navigation Changes
- **Primary View**: The "Installed Instances" view will promote to become the primary functional content area.
- **Icon Update**: The icon for the "Installed Instances" tab/button will be changed to a "Home" (House) icon to signify its central role.

### 2.2. "Install New Instance" Revamp
- **Interaction Model**: Changing from a separate tab/page to a **Modal/Popup Window**.
- **Quick Mode**:
  - If checked: Use defaults (Root user, Default Path), hide details.
  - If unchecked: Show full configuration.
- **Functionality**:
  - **New Feature: Custom Install Location** (Standard Mode).

### 2.3. Global Settings Updates
- **Package Cache Location**:
  - Allow users to configure the directory where downloaded distribution packages (rootfs tarballs) are stored.
  - Useful for managing disk space or sharing caches.
- **Default Installation Path**:
  - Allow users to set a default root directory for all new instance installations.
  - This path will be pre-filled in the "Install New Instance" dialog but can be overridden.

## 3. Installed Instance Management Enhancements

The card or list item for each installed instance will be upgraded with the following features:

### 3.1. Information Display
- **Disk Usage**: Display the current disk space occupied by the instance (e.g., "Size: 2.5 GB").

### 3.2. New Management Actions
- **Move Location**:
  - **Function**: Allow the user to move the physical installation files (VHDX) of an instance to a new directory.
  - **UI**: A "Move" button that triggers a folder selection dialog.
  
- **Stop Instance**:
  - **Function**: Terminate the running WSL instance.
  - **Constraint**: Only available/active if the instance is currently running.
  - **Safety**: Must require a **Confirmation Dialog** ("Are you sure you want to force stop this instance?") before executing.

- **Rename Instance**:
  - **Function**: Allow the user to change the display name (and underlying registration name if applicable) of the instance.
  - **UI**: An "Edit Name" or "Rename" button triggering an input field.

- **Modify Credentials**:
  - **Function**: Allow the user to reset or change the default user's username and password for the instance.
  - **UI**: A "User Settings" or "Credentials" button opening a dialog to input the new username and password.

## 4. Implementation Notes regarding Go/Fyne
- Ensure valid path handling across Windows filesystems for the "Move" and "Install Location" features.
- The "Stop" feature likely requires invoking `wsl --terminate <distro_name>`.
- Disk usage calculation generally involves checking the size of the `ext4.vhdx` file.

## 5. Package Management (Main View)
Accessible via the main toolbar (Package Icon).

### 5.1. Unified View
- **Grouping**: Packages are grouped by Distribution Family (e.g., Ubuntu, Debian).
- **Naming**: Show precise version names (not indices).

### 5.2. Item Actions & Status
- **Status Indicator**: Show if the package is "Cached" (Downloaded) or "Online".
- **Actions**:
  - **Inline Buttons**:
    - **Download**: If not cached.
    - **Delete**: If cached (to clear space).
    - **Redownload**: Always available (e.g., context menu or secondary action).
  - No separate split pane. A single, clean list.

### 5.4. Custom Sources
- **Add Custom Package**:
  - Ability to add a local tarball or a custom URL as an installable source.
  - User can specify the Name, Version, and File Path/URL.
  - These custom entries should persist in the local configuration.

### 5.5. Online Catalog Synchronization
- **Source**: Fetch the latest distribution catalog (`distros.json`) directly from the official GitHub repository (e.g., via Raw Git content) to ensure the list of available distributions is always up-to-date.
- **Fallback Mechanism**:
  - If the online fetch fails (timeout, offline), the application must silently fallback to the local `distros.json` shipped with the application.
  - An indicator (e.g., "Offline Mode") should be visible if online synchronization failed.
- **Refresh**:
  - Provide a "Refresh" button in the toolbar to manually trigger a catalog update.
