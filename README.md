# DistroNexus

[中文文档](README_CN.md) | **English**

> **Current source candidate: v2.3.0** — operational health, recovery, monitoring, trusted automation, WSLg, containers, and template trust on .NET 10/WPF. Package publication remains an external release gate.

**DistroNexus** is a modern Windows application for managing Windows Subsystem for Linux (WSL) distributions. Built with .NET 10 and WPF, it provides a native, intuitive interface for downloading, installing, and managing your WSL instances.

![DistroNexus Main Interface](docs/promotion/image/20260215181619-Main.png)

## 📘 Documentation

Full documentation and user guides are hosted on our official website:
👉 **[https://lazyworkshopcreate.github.io/DistroNexus/](https://lazyworkshopcreate.github.io/DistroNexus/)**

## ✨ Features

### Instance lifecycle and storage

- Start, stop, refresh, rename, move, import, export, and remove WSL instances.
- Open an instance in Windows Terminal with a configurable start path.
- Install distributions to a user-selected directory from curated catalog sources.
- Inspect VHDX usage, enable sparse mode, and compact one or multiple WSL 2 disks.
- Set or reset the default Linux credentials.

### Deep instance management

- Use the instance detail view for disk, resource, integration, network, and backup controls.
- Edit global WSL memory, processor, swap, localhost forwarding, and networking-mode settings.
- View listening ports and WSL IP information for running instances.
- Manage Docker Desktop integration per supported WSL 2 instance.
- Organize instances with tags, filtering, grouping, and bulk selection.
- Track external WSL state changes without waiting for a fixed cache timeout.

### Backup and recovery

- Export and import instances through the desktop application or PowerShell.
- Create daily, weekly, or monthly backup schedules through Windows Task Scheduler.
- Run on-demand backups, configure retention, and review recent success and failure history.

### Developer experience and automation

- Native Fluent-style WPF interface with automatic light/dark theme support.
- English and Simplified Chinese desktop UI and documentation.
- 93 exported PowerShell functions for lifecycle, health, recovery, monitoring, configuration, services, WSLg, containers, workspaces, trusted templates, and diagnostics.
- 16 built-in development templates covering .NET, Node.js, Python, Java, Go, Rust, containers, Kubernetes, databases, AI/ML, and infrastructure tooling.
- Parameterized template execution, environment checks, metadata linting, dry runs, progress, and structured error codes.
- Package download progress, transfer speed, caching, and detailed application logs.

![DistroNexus Package Manager](docs/promotion/image/20260215181646-Package.png)

*   **Progress & Logging**: Real-time operation progress and detailed diagnostics

## 🚀 Quick Start

### Requirements
- Windows 10 version 2004 or later, or Windows 11
- .NET 10 Desktop Runtime (included in installer)
- WSL2 enabled (for usage)

### Installation

#### Option 1: Installer (Recommended)
1. When the v2.3.0 package is published, download the approved installer from [Releases](https://github.com/LazyWorkshopCreate/DistroNexus/releases)
2. Run the installer
3. Launch from Start Menu

#### Option 2: Portable
1. When published, download the v2.3.0 portable ZIP from [Releases](https://github.com/LazyWorkshopCreate/DistroNexus/releases)
2. Extract to any folder
3. Run `DistroNexus.Desktop.exe`

#### Option 3: Self-Contained (No .NET Required)
1. When published, download the v2.3.0 self-contained ZIP from [Releases](https://github.com/LazyWorkshopCreate/DistroNexus/releases)
2. Extract to any folder
3. Run `DistroNexus.Desktop.exe`

## 🛠️ PowerShell Module

DistroNexus 2.3.0 includes a PowerShell module for automation:

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

The module exports 93 functions grouped by capability:

- **Instances**: list, install, start, stop, move, rename, remove, set credentials, import, and export.
- **Storage and configuration**: compact VHDX, inspect instance configuration, manage sparse mode, and read/write `.wslconfig`.
- **Backup, recovery, and diagnostics**: manage backup schedules, create, verify, restore, and safely remove recovery points, inspect port mappings, query the instance cache, and create release evidence bundles.
- **Integrations and organization**: manage Docker Desktop integration and instance tags.
- **Operations**: query host capabilities, scan and repair health findings, inspect monitoring snapshots, and preview or operate supported systemd services.
- **Linux application and container tooling**: discover or launch WSLg applications, inspect container runtimes, and manage Podman user units and connections.
- **Workspaces and trusted templates**: manage workspace definitions and launches; browse/download packages; validate, apply, and automate templates; and manage trusted marketplace sources, review grants, artifacts, history, and rollback.

See [`src/PowerShell/DistroNexus.psd1`](src/PowerShell/DistroNexus.psd1) for the authoritative export list.

## 🧩 Template System

![DistroNexus Template System](docs/promotion/image/20260215181721-Template.png)

DistroNexus includes 16 built-in templates for quickly turning a WSL instance into a ready-to-use development environment.

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

# Discover DevOps template
Get-DistroNexusTemplate -Category "DevOps"

# Apply one template to an existing WSL instance
Apply-DistroNexusTemplate -InstanceName "Ubuntu-22.04" -TemplateId "python-dev" -Verbose

# Apply with runtime variables
Apply-DistroNexusTemplate -InstanceName "Ubuntu-22.04" -TemplateId "nodejs-dev" -Variables @{ NodeVersion = "20" }

# Apply infrastructure CLI toolbox template
Apply-DistroNexusTemplate -InstanceName "Ubuntu-22.04" -TemplateId "infra-cli-toolbox"
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
    "CatalogUrl": "https://raw.githubusercontent.com/LazyWorkshopCreate/DistroNexus/master/config/catalog.json",
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
git clone https://github.com/LazyWorkshopCreate/DistroNexus.git
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
.\tools\build-installer.ps1 -Version 2.3.0

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
│       ├── Public/                       # Public PowerShell functions
│       ├── Private/                      # Internal utilities
│       ├── DistroNexus.psd1              # Module manifest
│       └── DistroNexus.psm1              # Module script
├── config/
│   ├── catalog.json                      # Distribution catalog
│   ├── templates.json                    # Template metadata
│   └── templates/                        # Template script assets
├── docs/                                 # Specifications, architecture, guides, and release notes
│   ├── release_notes/                    # Version releases
│   └── archive/                          # Historical docs and v1 comparison
├── tools/
│   ├── build.ps1                         # Build automation
│   ├── build-installer.ps1               # Installer builder
│   ├── package-portable.ps1              # Portable package creator
│   └── installer.iss                     # Inno Setup installer definition
├── tests/
│   └── PowerShell/                       # Pester unit and integration tests
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

- 📖 [Documentation](https://lazyworkshopcreate.github.io/DistroNexus/)
- 🐛 [Issue Tracker](https://github.com/LazyWorkshopCreate/DistroNexus/issues)
- 💬 [Discussions](https://github.com/LazyWorkshopCreate/DistroNexus/discussions)

---

**DistroNexus v2.3.0** — Manage, automate, protect, and customize WSL environments from Windows.
