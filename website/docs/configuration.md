---
sidebar_position: 4
---

# Configuration

Most settings are managed from the desktop UI, but advanced users can edit the settings file directly.

Global settings are stored in `%APPDATA%\\DistroNexus\\settings.json`.

```json
{
    "DefaultInstallPath": "D:\\WSL",
    "PackageCachePath": "D:\\WSL\\packages",
    "TerminalStartPath": "~",
    "DefaultWslVersion": 2,
    "DefaultUsername": "root",
    "DefaultDistributionId": "Ubuntu-24.04",
    "EnableLogging": true,
    "CatalogUrl": "https://raw.githubusercontent.com/lazyworkshop-create/DistroNexus/master/config/catalog.json",
    "Theme": "Auto"
}
```

## Settings Reference

| Key | Description | Default |
| :--- | :--- | :--- |
| `DefaultInstallPath` | The root directory where distros will be installed if no custom path is provided during installation. | `D:\WSL` |
| `PackageCachePath` | Directory used to store downloaded offline packages (`.appx`, `.appxbundle`). | `D:\WSL\packages` |
| `TerminalStartPath` | Default starting directory when opening a terminal. Use `~` for Linux home or an absolute path. | `~` |
| `DefaultWslVersion` | Default WSL version for new installations. | `2` |
| `DefaultUsername` | Default username applied during new instance setup. | `root` |
| `DefaultDistributionId` | Default distribution identifier used for quick install defaults. | `Ubuntu-24.04` |
| `EnableLogging` | Enables logging and diagnostics output. | `true` |
| `CatalogUrl` | Distribution catalog source URL. | `https://raw.githubusercontent.com/lazyworkshop-create/DistroNexus/master/config/catalog.json` |
| `Theme` | UI theme preference (`Light`, `Dark`, `Auto`). | `Auto` |

## Notes

Catalog and template metadata are managed by DistroNexus runtime workflows. For user-level customization, edit `settings.json` first and keep keys valid JSON.
