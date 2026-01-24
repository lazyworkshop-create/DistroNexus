# DistroNexus v1.0.2 Requirements Document

## 1. Overview
Version 1.0.2 focuses on improving the user interface and enhancing the management capabilities for existing WSL instances. The primary goal is to make the application more intuitive by centering the experience around installed instances and providing critical management tools like moving, stopping, and reconfiguring instances.

## 2. UI/UX Adjustments

### 2.1. Main Navigation Changes
- **Primary View**: The "Installed Instances" view will promote to become the primary functional content area.
- **Icon Update**: The icon for the "Installed Instances" tab/button will be changed to a "Home" (House) icon to signify its central role.

### 2.2. "Install New Instance" Revamp
- **Interaction Model**: Changing from a separate tab/page to a **Modal/Popup Window**, consistent with the current "Settings" dialog behavior.
- **Icon Update**: The icon for triggering the installation process will be updated to be more distinct (e.g., a "Plus" or "Download" icon).
- **Functionality**:
  - Existing basic installation logic (select distro, configure basics) remains unchanged.
  - **New Feature: Custom Install Location**:
    - Users can specific the destination path for the new instance.
    - UI Element: A "Select Folder" button (with folder icon) allows the user to browse and pick the target directory.

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

## 5. Package Management (New Tab)
A new dedicated tab will be introduced for managing distribution packages (rootfs/images) independent of the installation process.

### 5.1. Capability
- **Tab Icon**: A box or package icon.
- **View**: A list or grid view of available and cached packages.

### 5.2. Cached Packages
- **Function**: Display a list of packages that have been downloaded to the local cache.
- **Actions**:
  - **Delete**: Remove the cached file to free up space.
  - **Details**: View file size, download date, and version.

### 5.3. Online Repository
- **Function**: View the list of available distributions from the official source (e.g., parsing `distros.json`).
- **Update List**: A button to fetch the latest `distros.json` from the remote source to ensure the list is up-to-date.
- **Actions**:
  - **Download**: Pre-download a specific package without installing it immediately.
  - **Redownload**: Force re-download of a package if a file is corrupted or to ensure freshness.

### 5.4. Custom Sources
- **Add Custom Package**:
  - Ability to add a local tarball or a custom URL as an installable source.
  - User can specify the Name, Version, and File Path/URL.
  - These custom entries should persist in the local configuration.
