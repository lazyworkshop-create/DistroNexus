# DistroNexus

[中文文档](README_CN.md) | **English**

> **🎉 Version 2.0 Released!** - Complete rewrite with .NET 10 + WPF for native Windows experience.

**DistroNexus** is a modern Windows application for managing Windows Subsystem for Linux (WSL) distributions. Built with .NET 10 and WPF, it provides a native, intuitive interface for downloading, installing, and managing your WSL instances.

## 📘 Documentation

Full documentation and user guides are hosted on our official website:
👉 **[https://lazyworkshop-create.github.io/DistroNexus/](https://lazyworkshop-create.github.io/DistroNexus/)**

## ✨ Features

### v2.0 Highlights
*   **Native Windows UI**: Modern WPF interface with Fluent Design System
*   **Dark Mode Support**: Automatic theme switching based on system preferences
*   **PowerShell Module**: 11 cmdlets for full automation capability
*   **Package Manager**: Browse and download WSL distributions from catalog
*   **One-Click Operations**: Start, stop, remove instances with single click
*   **Progress Tracking**: Real-time progress for downloads and operations

### Core Features
*   **Instance Management**: 
    *   ✅ Start/Stop instances
    *   ✅ Move instances to different drives
    *   ✅ Rename instances
    *   ✅ Remove instances
    *   ✅ Set credentials
*   **Custom Installation**: Install WSL distributions to any directory
*   **Distribution Catalog**: Browse and download from curated catalog
*   **Settings Management**: Comprehensive configuration options
*   **Logging System**: Detailed logging for troubleshooting

## 🚀 Quick Start

### Requirements
- Windows 10 version 2004 or later, or Windows 11
- .NET 10 Desktop Runtime (included in installer)
- WSL2 enabled (for usage)

### Installation

#### Option 1: Installer (Recommended)
1. Download `DistroNexus-2.0.0-Setup.exe` from [Releases](https://github.com/lazyworkshop-create/DistroNexus/releases)
2. Run the installer
3. Launch from Start Menu

#### Option 2: Portable
1. Download `DistroNexus-v2.0.0-Release.zip`
2. Extract to any folder
3. Run `DistroNexus.Desktop.exe`

#### Option 3: Self-Contained (No .NET Required)
1. Download `DistroNexus-v2.0.0-Release-selfcontained.zip`
2. Extract to any folder
3. Run `DistroNexus.Desktop.exe`

## 🛠️ PowerShell Module

DistroNexus 2.0 includes a PowerShell module for automation:

```powershell
# Import the module
Import-Module "C:\Program Files\DistroNexus\PowerShell\DistroNexus.psm1"

# List all instances
Get-WslInstance

# Install a custom instance
Install-DistroNexusInstance -DistroName "MyUbuntu" -InstallPath "D:\WSL\MyUbuntu" -Username "admin"

# Start an instance
Start-WslInstance -DistroName "Ubuntu-22.04"
```

Available cmdlets:
- `Get-WslInstance` - List all WSL instances
- `Start-WslInstance` - Start instances
- `Stop-WslInstance` - Stop instances
- `Move-WslInstance` - Relocate instances
- `Rename-WslInstance` - Rename instances
- `Remove-WslInstance` - Uninstall instances
- `Install-DistroNexusInstance` - Custom installation
- `Set-WslCredentials` - Update credentials
- `Get-DistroNexusPackage` - Browse distributions
- `Save-DistroNexusPackage` - Download packages
- `Update-DistroNexusCatalog` - Refresh catalog

## ⚙️ Configuration

Settings are stored at `%APPDATA%\DistroNexus\settings.json`:

```json
{
    "DefaultInstallPath": "C:\\WSL",
    "DefaultWslVersion": 2,
    "DefaultUsername": "root",
    "CatalogUrl": "https://raw.githubusercontent.com/yourusername/DistroNexus/main/config/distros.json",
    "Theme": "Auto",
    "EnableLogging": true
}
```

Configure via Settings page in the application or edit JSON directly.

## 🏗️ Building from Source

### Prerequisites
- .NET 10 SDK
- PowerShell 7.0 or later
- Windows 10/11

### Build Steps

```powershell
# Clone the repository
git clone https://github.com/lazyworkshop-create/DistroNexus.git
cd DistroNexus

# Build with the provided script
.\tools\build.ps1 -Configuration Release

# Or use dotnet CLI directly
dotnet build src/Client/DistroNexus.slnx -c Release
```

### Publish for Distribution

```powershell
# Create portable ZIP package (framework-dependent)
.\tools\build.ps1 -Publish -CreateZip -Configuration Release

# Create self-contained package (no .NET runtime required)
.\tools\build.ps1 -Publish -SelfContained -CreateZip -Configuration Release

# Build Windows installer (requires Inno Setup)
.\tools\build-installer.ps1 -Version 2.0.0

# Output will be in release/
```

## 🔧 Legacy Scripts (v1.x)

The `scripts/` directory contains PowerShell scripts from v1.x that are now integrated into the v2.0 PowerShell module. These are kept for reference:

- `install_wsl_custom.ps1` - Custom WSL installation (replaced by `Install-DistroNexusInstance`)
- `list_distros.ps1` - List distributions (replaced by `Get-WslInstance`)
- `start_instance.ps1` - Start instance (replaced by `Start-WslInstance`)
- `stop_instance.ps1` - Stop instance (replaced by `Stop-WslInstance`)
- `move_instance.ps1` - Move instance (replaced by `Move-WslInstance`)
- `rename_instance.ps1` - Rename instance (replaced by `Rename-WslInstance`)
- `set_credentials.ps1` - Set credentials (replaced by `Set-WslCredentials`)
- `uninstall_wsl_custom.ps1` - Uninstall instance (replaced by `Remove-WslInstance`)
- `download_all_distros.ps1` - Download distributions (replaced by `Save-DistroNexusPackage`)
- `update_distros.ps1` - Update catalog (replaced by `Update-DistroNexusCatalog`)

## 📁 Project Structure

```
DistroNexus/
├── src/
│   ├── Client/
│   │   ├── DistroNexus.Desktop/          # WPF Application
│   │   │   ├── Views/                    # XAML Views
│   │   │   ├── ViewModels/               # ViewModels (MVVM)
│   │   │   ├── Converters/               # Value Converters
│   │   │   ├── Resources/                # Images, Icons
│   │   │   └── App.xaml                  # Application entry
│   │   ├── DistroNexus.Core/             # Core Library
│   │   │   ├── Services/                 # Service implementations
│   │   │   ├── Models/                   # Data models
│   │   │   └── Interfaces/               # Service interfaces
│   │   └── DistroNexus.Tests/            # Unit tests
│   └── PowerShell/
│       ├── Public/                       # Public cmdlets (11 cmdlets)
│       ├── Private/                      # Internal utilities
│       ├── DistroNexus.psd1              # Module manifest
│       └── DistroNexus.psm1              # Module script
├── config/
│   ├── distros.json                      # Distribution catalog
│   └── settings.json                     # Default settings
├── docs/                                 # Documentation
│   ├── release_notes/                    # Version releases
│   └── archive/                          # Historical docs
├── scripts/                              # Legacy v1.x scripts (reference)
├── tools/
│   ├── build_v2.ps1                      # Build automation
│   └── packaging/                        # Installer resources
├── website/                              # Docusaurus documentation site
├── README.md                             # English documentation
└── README_CN.md                          # Chinese documentation
```

## 🔍 Troubleshooting

### Application Won't Start
- Ensure .NET 10 Desktop Runtime is installed
- Check `%APPDATA%\DistroNexus\logs\` for error messages
- Try running as Administrator

### PowerShell Module Not Working
```powershell
# Verify module path
Import-Module "C:\Program Files\DistroNexus\PowerShell\DistroNexus.psm1" -Verbose

# Check for errors
Get-Module DistroNexus
```

### WSL Instance Issues
- Verify WSL2 is installed: `wsl --status`
- Check WSL version: `wsl --list --verbose`
- Update WSL: `wsl --update`

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'feat: add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📜 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [WPF-UI](https://github.com/lepoco/wpfui) - Modern Fluent Design controls
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM infrastructure
- Microsoft WSL Team - For making Linux on Windows possible

## 📞 Support

- 📖 [Documentation](https://lazyworkshop-create.github.io/DistroNexus/)
- 🐛 [Issue Tracker](https://github.com/lazyworkshop-create/DistroNexus/issues)
- 💬 [Discussions](https://github.com/lazyworkshop-create/DistroNexus/discussions)

---

**DistroNexus v2.0** - Forge your perfect Linux environment on Windows with native .NET performance and elegance.
