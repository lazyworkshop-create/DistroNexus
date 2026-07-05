---
sidebar_position: 2
---

# Installation

## Prerequisites

*   **OS**: Windows 10 Version 2004 or higher (Build 19041 and above) or Windows 11.
*   **WSL2 Enabled**: Enable WSL2 before using DistroNexus.
    *   Open PowerShell as Administrator and run: `wsl --install`.
*   **.NET Runtime**: .NET 10 Desktop Runtime is required.
    *   The installer package includes runtime prerequisite handling.

## Downloading DistroNexus

1.  Go to the [GitHub Releases](https://github.com/lazyworkshop-create/DistroNexus/releases) page.
2.  Choose one of the v2.2.1 assets:
    *   Installer: `DistroNexus-2.2.1-Setup.exe`
    *   Portable: `DistroNexus-v2.2.1-Release.zip`
    *   Self-contained: `DistroNexus-v2.2.1-Release-selfcontained.zip`

## Running the Application

### Installer package
1.  Run `DistroNexus-2.2.1-Setup.exe`.
2.  Complete setup and launch DistroNexus from the Start Menu.

### Portable / Self-contained package
1.  Extract the selected ZIP package to a directory of your choice.
2.  Run `DistroNexus.Desktop.exe`.

## Troubleshooting

If the application fails to launch:
*   Verify WSL2 is installed and available.
*   If using portable build, ensure extracted files remain in the same directory structure.
*   Check antivirus policy if the executable is blocked.
