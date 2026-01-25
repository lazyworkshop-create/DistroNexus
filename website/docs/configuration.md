---
sidebar_position: 4
---

# Configuration

While most settings can be managed via the GUI, advanced users can modify the configuration files directly.

Global settings are stored in `config/settings.json`.

```json
{
    "DefaultInstallPath": "D:\\WSL",
    "PackageCachePath": "D:\\WSL_Cache",
    "DefaultTerminalStartPath": "~",
    "DefaultDistro": "Ubuntu-24.04"
}
```

## Settings Reference

| Key | Description | Default |
| :--- | :--- | :--- |
| `DefaultInstallPath` | The root directory where distros will be installed if no custom path is provided during installation. | `D:\WSL` |
| `PackageCachePath` | Directory to store downloaded offline packages (`.appx`, `.appxbundle`). | `D:\WSL_Cache` |
| `DefaultTerminalStartPath` | Default starting directory when opening a terminal. Use `~` for the Linux home directory or `/mnt/c/` for Windows C drive. | `~` |
| `DefaultDistro` | The identifier of the distro to use for "Quick Mode" installation. | `Ubuntu-24.04` |

## Distro Definitions

The list of available distributions is maintained in `config/distros.json`. This file is updated automatically but can be edited to add custom sources.
