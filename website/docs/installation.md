---
sidebar_position: 2
---

# Installation

## Prerequisites

*   **OS**: Windows 10 Version 2004 or higher (Build 19041 and above) or Windows 11.
*   **WSL Enabled**: You must have the Windows Subsystem for Linux feature enabled.
    *   Open PowerShell as Administrator and access: `wsl --install` (if not already installed).

## Downloading DistroNexus

1.  Go to the [GitHub Releases](https://github.com/DistroNexus/DistroNexus/releases) page.
2.  Download the latest release ZIP file (e.g., `DistroNexus_v1.0.2.zip`).
3.  Extract the ZIP file to a location of your choice (e.g., `C:\Tools\DistroNexus`).

## Running the Application

1.  Navigate to the extracted folder.
2.  Double-click `DistroNexus.exe` to launch the dashboard.
3.  **Note**: The application relies on PowerShell scripts located in the `scripts/` folder. Ensure these are present (they are included in the release).

## Troubleshooting

If the application fails to launch:
*   Ensure you have write permissions to the installation folder (needed for `config/` files).
*   Check if your Antivirus is blocking the executable or the PowerShell scripts.
