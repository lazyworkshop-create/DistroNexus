---
sidebar_position: 5
---

# PowerShell Scripts Reference

This page provides a detailed reference for the PowerShell scripts located in the `scripts/` directory. These scripts form the backbone of DistroNexus functionality and can be used independently for automation or troubleshooting.

## Core Management

### `install_wsl_custom.ps1`

**Description**: The primary script for creating new WSL instances. It handles downloading the distribution package (if not cached), extracting it, and registering it as a new WSL instance in a specific directory.

**Parameters**:
*   `-DistroName <String>`: The internal name of the distro configuration to use (e.g., "Ubuntu-22.04").
*   `-InstallPath <String>`: The full path to the directory where the instance should be created.
*   `-name <String>`: (Optional) A custom display name for the WSL instance.
*   `-user <String>`: (Optional) Default username to set up.
*   `-pass <String>`: (Optional) Password for the default user.

### `uninstall_wsl_custom.ps1`

**Description**: Unregisters and removes a WSL instance.

**Parameters**:
*   `-DistroName <String>`: The name of the WSL instance to remove.
*   `-RemoveFiles`: (Switch) If present, deletes the installation directory after unregistering.

### `move_instance.ps1`

**Description**: Relocates an existing WSL instance to a new drive or folder.

**Parameters**:
*   `-DistroName <String>`: The name of the valid WSL instance to move.
*   `-NewPath <String>`: The destination folder path.

**Process**:
1.  Terminates the running instance.
2.  Exports the filesystem to a tarball.
3.  Unregisters the old instance.
4.  Imports the tarball to the new location.
5.  Restores user settings.

### `rename_instance.ps1`

**Description**: Changes the registered name of a WSL instance.

**Parameters**:
*   `-OldName <String>`: Current name of the instance.
*   `-NewName <String>`: Desired new name.

## Instance Operations

### `start_instance.ps1`

**Description**: Starts a WSL instance in the background (headless).

**Parameters**:
*   `-DistroName <String>`: Name of the instance to start.

### `stop_instance.ps1`

**Description**: Terminates a running WSL instance.

**Parameters**:
*   `-DistroName <String>`: Name of the instance to stop.

### `set_credentials.ps1`

**Description**: Configures the default user and password for a specific instance. Used during installation or for password resets.

**Parameters**:
*   `-DistroName <String>`: Target instance.
*   `-Username <String>`: The username to set as default.
*   `-Password <String>`: The password to set.

### `scan_wsl_instances.ps1`

**Description**: Scans the Windows Registry (`HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss`) and actual WSL state to build specific metadata for DistroNexus. This synchronizes the `config/instances.json` file with reality.

**Usage**: No parameters required.

## Package Management

### `list_distros.ps1`

**Description**: Reads the `config/distros.json` file and outputs the list of available distributions.

### `download_all_distros.ps1`

**Description**: Batch downloader that can download packages for all defined distributions for offline use.

### `update_distros.ps1`

**Description**: Fetches the latest distribution definitions from the online source (if configured) and updates `config/distros.json`.

## Utilities

### `pwsh_utils.ps1`

**Description**: A library of shared functions used by other scripts. It is not intended to be run directly.

**Includes**:
*   Logging functions (`Setup-Logger`, `Log-Message`).
*   JSON handling helpers.
*   Error handling routines.
