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

v2.3.0 is currently a repository release candidate, not a published package. Do not assume an
installer, portable archive, self-contained archive, signature, or Store listing exists yet.

When an approved v2.3.0 package is published, obtain its exact asset name and signature guidance
from the release record. Until then, build only from the checked-out source using the documented
local build commands.

## Running the Application

### Installer package
1.  Run only the approved installer named by the published release record.
2.  Complete setup and launch DistroNexus from the Start Menu.

### Portable / Self-contained package
1.  Extract the selected ZIP package to a directory of your choice.
2.  Run `DistroNexus.Desktop.exe`.

## Troubleshooting

If the application fails to launch:
*   Verify WSL2 is installed and available.
*   If using portable build, ensure extracted files remain in the same directory structure.
*   Check antivirus policy if the executable is blocked.
