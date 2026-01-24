# DistroNexus

[中文文档](README_CN.md) | **English**

**DistroNexus** is a comprehensive GUI application (powered by PowerShell) designed to simplify the management, downloading, and custom installation of Windows Subsystem for Linux (WSL) distributions. It acts as a central hub for your WSL needs, allowing you to forge your perfect Linux environment on Windows.

## Features

*   **Modern GUI Dashboard**: A cross-platform graphical interface (built with Fyne) to manage everything visually.
*   **Centralized Download**: Automatically download the latest offline packages (Appx/AppxBundle) for popular distributions like Ubuntu, Debian, Kali Linux, and Oracle Linux.
*   **Custom Installation**: Install any WSL distro into a custom directory of your choice, bypassing the default system drive location.
*   **Advanced Instance Management**: 
    *   **Start**: Start the instance in the background.
    *   **Open Terminal**: Open a new terminal window for a running instance (supports custom starting directory).
    *   **Stop/Terminate**: Immediately stop running instances.
    *   **Move**: Relocate an existing distro to a new drive or folder without data loss.
    *   **Rename**: Change the registered name of your WSL instance.
    *   **Credentials**: Reset or set the default username and password for any instance.
*   **Safety Checks**: Built-in validation prevents overwriting existing instances or installing into valid directories.
*   **Side-by-Side Versions**: Easily install multiple versions of the same distro (e.g., Ubuntu 20.04 and 22.04) or multiple instances of the same version.
*   **Offline Support**: Uses a local cache of downloaded packages to speed up re-installations.
*   **Package Management**: View and manage the local cache of downloaded distro packages.
*   **Uninstall Helper**: Easily unregister and remove custom WSL instances with a single click.

## Configuration

Global settings are stored in `config/settings.json`:

```json
{
    "DefaultInstallPath": "D:\\WSL",
    "PackageCachePath": "D:\\WSL_Cache",
    "DefaultTerminalStartPath": "~",
    "DefaultDistro": "Ubuntu-24.04"
}
```

*   `DefaultInstallPath`: The root directory where distros will be installed if no path is provided.
*   `PackageCachePath`: Directory to store downloaded offline packages.
*   `DefaultTerminalStartPath`: Default starting directory when opening a terminal (e.g., `~` for home, or `/mnt/c/`).
*   `DefaultDistro`: The identifier (DefaultName) of the distro to use for Quick Mode.

## Graphical User Interface (GUI)

DistroNexus now comes with a unified Dashboard application (`DistroNexus.exe`) that wraps the powerful PowerShell scripts into a user-friendly experience.

### Main Capabilities
- **Install Tab**: Select family/version, configure users, and monitor installation logs. Supports "Quick Mode" for one-click setup.
- **My Installs Tab**: 
    - View all registered WSL distributions.
    - **Actions Dashboard**: Stop, Move, Rename, Set Credentials, and Uninstall instances directly from the card.
    - **Disk Usage**: Monitor the size of each distro's virtual disk.
- **Package Manager**: View locally cached distro packages, see their size, and delete unused files.
- **Settings**: Configure default paths (Install, Cache, Terminal) and reset configuration.

![App Icon](tools/icon.png)

## Building from Source

The project includes a comprehensive build system for Windows target.

### Prerequisites
- **Go**: Version 1.22 or higher.
- **Fyne CLI**: Automatically installed by setup script.
- **MinGW-w64**: Required for cross-compiling to Windows from Linux (package `gcc-mingw-w64`).
- **PowerShell**: Required on the target Windows machine to run the backend scripts.

### Setup & Build
1.  **Initialize Environment** (Linux/WSL):
    ```bash
    ./tools/setup_go_env.sh
    ```
2.  **Compile**:
    ```bash
    ./tools/build.sh
    ```
    This will generate `build/DistroNexus.exe` with the embedded Application Icon.

## Scripts

The repository contains the following scripts in the `scripts/` directory:

### 1. `download_all_distros.ps1`

Downloads all supported WSL distribution packages to a local `distro` directory. This is useful for preparing an offline repository or ensuring you have the latest versions available.

**Usage:**
```powershell
.\scripts\download_all_distros.ps1
```

### 2. `install_wsl_custom.ps1`

The main installer script. It can be used interactively or with command-line arguments.

**List Available Distros:**
View all supported distributions and their identifiers (for configuration or selection).
```powershell
.\scripts\install_wsl_custom.ps1 -ls
```

### 3. Management Scripts

*   **`move_instance.ps1`**: Moves a WSL instance to a new location (Safe Export -> Unregister -> Import).
*   **`rename_instance.ps1`**: Renames a registry entry for a WSL instance.
*   **`start_instance.ps1`**: Starts a distro, optionally with a specific starting directory (`-StartPath`).
*   **`stop_instance.ps1`**: Terminates a running instance.
*   **`set_credentials.ps1`**: Configures the default user and password inside the distro.

### 4. `download_all_distros.ps1`

Downloads all (or specific) WSL distribution packages to the configured cache path.

### 5. `scan_wsl_instances.ps1`

Scans the output of `wsl -l -v` and synchronizes the internal `config/instances.json` registry.

### Infrastructure

*   **`pwsh_utils.ps1`**: Shared library for logging and common utilities. Logs are stored in `logs/` directory with rotation support.

## Project Structure

```
DistroNexus/
├── build/                        # Compiled executable output
├── config/                       # JSON configuration
│   ├── distros.json              # Distro definitions
│   └── settings.json             # User settings
├── scripts/                      # PowerShell backend
│   ├── download_all_distros.ps1  # Downloader
│   ├── install_wsl_custom.ps1    # Installer
│   ├── move_instance.ps1         # Move logic
│   ├── pwsh_utils.ps1            # Logging & Utils
│   ├── rename_instance.ps1       # Rename logic
│   ├── scan_wsl_instances.ps1    # Registry Sync
│   ├── set_credentials.ps1       # User/Pass logic
│   ├── start_instance.ps1        # Launcher
│   ├── stop_instance.ps1         # Terminator
│   └── uninstall_wsl_custom.ps1  # Uninstaller
├── src/                          # Go Source Code
│   ├── cmd/                      # Entry points
│   ├── internal/                 # App logic & UI
│   │   ├── config/               # Config loader
│   │   ├── logic/                # Backend logic
│   │   ├── model/                # Data types
│   │   └── ui/                   # Fyne UI components
│   ├── go.mod                    # Go dependencies
│   └── vendor/                   # Vendored dependencies
├── tools/                        # Build tools & resources
├── docs/                         # Documentation & Archive
├── README.md                     # Documentation (English)
└── README_CN.md                  # Documentation (Chinese)
```
