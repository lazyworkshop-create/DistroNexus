# DistroNexus

[中文文档](README_CN.md) | **English**

**DistroNexus** is a powerful PowerShell toolkit designed to simplify the management, downloading, and custom installation of Windows Subsystem for Linux (WSL) distributions. It acts as a central hub for your WSL needs, allowing you to forge your perfect Linux environment on Windows.

## Features

*   **Centralized Download**: Automatically download the latest offline packages (Appx/AppxBundle) for popular distributions like Ubuntu, Debian, Kali Linux, and Oracle Linux.
*   **Custom Installation**: Install any WSL distro into a custom directory of your choice, bypassing the default system drive location.
*   **Side-by-Side Versions**: Easily install multiple versions of the same distro (e.g., Ubuntu 20.04 and 22.04) or multiple instances of the same version.
*   **Offline Support**: Uses a local cache of downloaded packages to speed up re-installations.
*   **Automation Ready**: Supports command-line arguments for unattended or scripted installations.

## Configuration

Global settings are stored in `config/settings.json`:

```json
{
    "DefaultInstallPath": "D:\\WSL",
    "DefaultDistro": "Ubuntu-24.04",
    "DistroCachePath": "..\\..\\distro"
}
```

*   `DefaultInstallPath`: The root directory where distros will be installed if no path is provided.
*   `DefaultDistro`: The identifier (DefaultName) of the distro to use for Quick Mode.
*   `DistroCachePath`: Directory to store downloaded offline packages. Can be absolute or relative to `scripts/`.

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

**Interactive Mode:**
Simply run the script to be guided through a menu to select the distro family, version, name, and install path.
```powershell
.\scripts\install_wsl_custom.ps1
```

**Command-Line Mode (Unattended):**
Use parameters to skip the interactive menus.

*   **One-Click Install (Quick Mode) with User Setup:**
    ```powershell
    .\scripts\install_wsl_custom.ps1 -name "MyDevEnv" -user "devops" -pass "securepass"
    ```

*   **Install by selecting Family and Version:**
    ```powershell
    .\scripts\install_wsl_custom.ps1 -SelectFamily "Ubuntu" -SelectVersion "22.04"
    ```

*   **Full customization without direct URL:**
    ```powershell
    .\scripts\install_wsl_custom.ps1 -SelectFamily "Debian" -SelectVersion "GNU/Linux" -DistroName "Debian-Dev" -InstallPath "D:\WSL\Debian-Dev"
    ```

*   **Parameters:**
    *   `-DistroName`: Manually specify the registered WSL name.
    *   `-InstallPath`: Manually specify the installation directory.
    *   `-SelectFamily`: The name of the distro family (e.g., "Ubuntu", "Debian").
    *   `-SelectVersion`: The version string to match (e.g., "24.04").
    *   `-name`: Quick install mode: sets the distro name (uses default distro type).
    *   `-user`: Default username to create.
    *   `-pass`: Password for the default user.

## Project Structure

```
DistroNexus/
├── scripts/
│   ├── download_all_distros.ps1  # Downloader script
│   └── install_wsl_custom.ps1    # Installer script
├── README.md                     # Documentation (English)
└── README_CN.md                  # Documentation (Chinese)
```
