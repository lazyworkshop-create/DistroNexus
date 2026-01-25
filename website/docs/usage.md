---
sidebar_position: 3
---

# User Guide

DistroNexus provides a unified Dashboard application (`DistroNexus.exe`) that wraps powerful PowerShell scripts into a user-friendly experience.

## Dashboard Overview

The application is divided into several tabs for easy navigation.

### Install Tab

Use this tab to download and install new Linux distributions.

*   **Quick Install**: Select a default distro (configurable in Settings) and install it with one click.
*   **Custom Install**: 
    1.  **Select Family**: Choose the distribution family (e.g., Ubuntu, Debian).
    2.  **Select Version**: Pick specific versions (e.g., 20.04 vs 22.04).
    3.  **Install Location**: Browse and select a custom directory on any drive.
    4.  **Credentials**: Set the default username and root password during installation.

### My Installs Tab

View and manage all your currently registered WSL distributions.

*   **List View**: Shows Distro Name, Version, State (Running/Stopped), and WSL Version (1 or 2).
*   **Actions**:
    *   **Start**: Boot the instance in the background.
    *   **Terminal**: Open a generic terminal or Windows Terminal specifically for this instance.
    *   **Stop**: Gracefully shut down the instance.
    *   **Terminate**: Force kill the instance.
    *   **Move**: Relocate the instance to another disk (e.g., move from C: to D: due to space).
    *   **Rename**: Change the display name of the instance.

### Package Manager Tab

Manage the offline `.appx` or `.appxbundle` files downloaded by DistroNexus.

*   View downloaded files and their sizes.
*   Delete old packages to free up space.
*   Manually add packages if you downloaded them elsewhere.

## Common Tasks

### Moving a Distro to Another Drive

1.  Go to **My Installs**.
2.  Select the distro you want to move.
3.  Click **Move**.
4.  Select the new target folder.
5.  Wait for the export/import process to complete. **Do not close the application** during this process.

### Installing Multiple Instances

You can install multiple copies of the same distro (e.g., "Ubuntu-Work" and "Ubuntu-Personal").

1.  Go to **Install**.
2.  Select "Ubuntu".
3.  For the first install, name it "Ubuntu-Work".
4.  For the second install, repeat the process but name it "Ubuntu-Personal" and choose a different folder.
