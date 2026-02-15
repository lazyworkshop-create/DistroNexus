# DistroNexus

[中文文档](README_CN.md) | **English**

> **🎉 Version 2.0 Released!** - Complete rewrite with .NET 10 + WPF for native Windows experience.

**DistroNexus** is a modern Windows application for managing Windows Subsystem for Linux (WSL) distributions. Built with .NET 10 and WPF, it provides a native, intuitive interface for downloading, installing, and managing your WSL instances.

## 📘 Documentation

Full documentation and user guides are hosted on our official website:
👉 **[https://lazyworkshop-create.github.io/DistroNexus/](https://lazyworkshop-create.github.io/DistroNexus/)**

## ✨ Features

### 1.0 Foundations
*   **Instance Management**:
    *   ✅ Start/Stop instances
    *   ✅ Open terminal for an instance (with configurable start path)
    *   ✅ Move instances to different drives
    *   ✅ Rename instances
    *   ✅ Remove instances
    *   ✅ Set or reset default credentials
*   **Custom Installation**: Install WSL distributions to any directory
*   **Distribution Catalog**: Browse and download distributions from curated sources

### 2.0 Additions
*   **Native Windows UI**: Modern WPF interface with Fluent Design System
*   **Dark Mode Support**: Automatic theme switching based on system preferences
*   **Bilingual Experience**: English and Simplified Chinese support across WPF client UI and project documentation
*   **PowerShell Module**: 15 cmdlets for full automation and scripting workflows
    *   ✅ Works both inside the app and in standalone PowerShell sessions
    *   ✅ Supports repeatable operations for CI, provisioning, and batch management
*   **Template System**: Built-in templates for fast environment bootstrapping
    *   ✅ Covers common stacks like language runtimes, containers, and local dev setups
    *   ✅ Supports parameterized template execution for environment-specific customization
*   **Package Manager Experience**: Better browsing and package download workflow
*   **Progress & Logging**: Real-time operation progress and detailed diagnostics

## 🚀 Quick Start

### Requirements
- Windows 10 version 2004 or later, or Windows 11
- .NET 10 Desktop Runtime (included in installer)
- WSL2 enabled (for usage)

### Installation

#### Option 1: Installer (Recommended)
1. Download `DistroNexus-2.0.1-Setup.exe` from [Releases](https://github.com/lazyworkshop-create/DistroNexus/releases)
2. Run the installer
3. Launch from Start Menu

#### Option 2: Portable
1. Download `DistroNexus-v2.0.1-Release.zip`
2. Extract to any folder
3. Run `DistroNexus.Desktop.exe`

#### Option 3: Self-Contained (No .NET Required)
1. Download `DistroNexus-v2.0.1-Release-selfcontained.zip`
2. Extract to any folder
3. Run `DistroNexus.Desktop.exe`

## 🛠️ PowerShell Module

DistroNexus 2.0 includes a PowerShell module for automation:

```powershell
# Import the module
Import-Module "C:\Program Files\DistroNexus\PowerShell\DistroNexus.psm1"

# List all instances
Get-DistroNexusInstance

# Install a custom instance
Install-DistroNexusInstance -DistroName "MyUbuntu" -InstallPath "D:\WSL\MyUbuntu" -Username "admin"

# Start an instance
Start-DistroNexusInstance -DistroName "Ubuntu-22.04"
```

Available cmdlets:
- `Get-DistroNexusInstance` - List all WSL instances
- `Start-DistroNexusInstance` - Start instances
- `Stop-DistroNexusInstance` - Stop instances
- `Move-DistroNexusInstance` - Relocate instances
- `Rename-DistroNexusInstance` - Rename instances
- `Remove-DistroNexusInstance` - Uninstall instances
- `Install-DistroNexusInstance` - Custom installation
- `Set-DistroNexusCredential` - Update credentials
- `Get-DistroNexusPackage` - Browse distributions
- `Save-DistroNexusPackage` - Download packages
- `Remove-DistroNexusPackage` - Remove cached packages
- `Update-DistroNexusCatalog` - Refresh catalog
- `Get-DistroNexusTemplate` - List built-in templates
- `Apply-DistroNexusTemplate` - Apply template to an instance
- `Invoke-DistroNexusTemplateAutomation` - Run template automation pipeline

## 🧩 Template System

DistroNexus includes a built-in template system for quickly turning a WSL instance into a ready-to-use development environment.

Template documentation index:
- Comprehensive guide: `docs/development/template-system-comprehensive-guide.md`
- Requirements analysis: `docs/specs/template-system-requirements-analysis.md`
- System design: `docs/architecture/template-system-design.md`
- User manual: `docs/development/template-system-user-manual.md`
- Template development manual: `docs/development/template-development-manual.md`
- Test suite manual: `docs/development/template-automation-test-suite-manual.md`

- Template catalog file: `config/templates.json`
- Template script assets: `config/templates/*`
- Main commands: `Get-DistroNexusTemplate`, `Apply-DistroNexusTemplate`, `Invoke-DistroNexusTemplateAutomation`

### Quick Start

```powershell
# List all templates
Get-DistroNexusTemplate

# Filter by category
Get-DistroNexusTemplate -Category "Development"

# Apply one template to an existing WSL instance
Apply-DistroNexusTemplate -InstanceName "Ubuntu-22.04" -TemplateId "python-dev" -Verbose

# Apply with runtime variables
Apply-DistroNexusTemplate -InstanceName "Ubuntu-22.04" -TemplateId "nodejs-dev" -Variables @{ NodeVersion = "20" }
```

### Template Automation Validation

```powershell
# Dry run automation for selected templates
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds "python-dev","nodejs-dev" -Distro "Ubuntu-22.04" -DryRun

# Execute full automation for all templates (use in controlled test environment)
Invoke-DistroNexusTemplateAutomation -Mode AllTemplates -Distro "Ubuntu-22.04"
```

### Safety Notes

- Applying custom templates may execute shell scripts in the target distro.
- Review template scripts before execution, especially third-party/custom templates.
- Use `-WhatIf` / `-Confirm` where applicable for safer trial runs.

## ⚙️ Configuration

Settings are stored at `%APPDATA%\DistroNexus\settings.json`:

```json
{
    "DefaultInstallPath": "C:\\WSL",
    "DefaultWslVersion": 2,
    "DefaultUsername": "root",
    "CatalogUrl": "https://raw.githubusercontent.com/lazyworkshop-create/DistroNexus/main/config/catalog.json",
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
.\tools\build-installer.ps1 -Version 2.0.1

# Output will be in release/
```



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
│       ├── Public/                       # Public cmdlets (15 cmdlets)
│       ├── Private/                      # Internal utilities
│       ├── DistroNexus.psd1              # Module manifest
│       └── DistroNexus.psm1              # Module script
├── config/
│   ├── catalog.json                      # Distribution catalog
│   ├── templates.json                    # Template metadata
│   └── templates/                        # Template script assets
├── docs/                                 # Documentation
│   ├── release_notes/                    # Version releases
│   └── archive/                          # Historical docs and v1 comparison
├── tools/
│   ├── build_v2.ps1                      # Build automation
│   ├── build-installer.ps1               # Installer builder
│   ├── package-portable.ps1              # Portable package creator
│   └── packaging/                        # Installer resources
├── tests/                                # Test suites
│   ├── PowerShell/                       # Pester tests
│   ├── CSharp/                           # xUnit tests
│   └── TestUtilities/                    # Shared test utilities
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

We welcome contributions! Please open an issue or pull request directly on GitHub.

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
