# DistroNexus 2.0.0 Task List

> **Reference Document**: [2.0.0_REQUIREMENTS.md](2.0.0_REQUIREMENTS.md)  
> **Created**: 2026-01-27  
> **Target**: Complete migration from Go/Fyne to .NET 10/WPF with PowerShell Module

---

## Phase 1: PowerShell Module Refactoring

### 1.1 Module Infrastructure Setup
- [x] Create `src/PowerShell/` directory structure
  - [x] `Public/` - Public cmdlets
  - [x] `Private/` - Internal helper functions
  - [x] `Tests/` - Pester test files
- [x] Create `DistroNexus.psd1` module manifest
- [x] Create `DistroNexus.psm1` module script with auto-loading

### 1.2 Core Utility Migration
- [x] Migrate `pwsh_utils.ps1` → `Private/Logger.ps1`
  - [x] `Write-DistroNexusLog` function with log rotation
  - [x] `Get-DistroNexusConfig` function for settings loading
  - [x] Unified error handling wrapper

### 1.3 Instance Management Cmdlets
- [x] `Get-DistroNexusInstance` (from `scan_wsl_instances.ps1`)
  - [x] Return structured objects (PSCustomObject)
  - [x] Include: Name, State, Version, BasePath, DiskSize
  - [x] Support `-Name` parameter for filtering
- [x] `Start-DistroNexusInstance` (from `start_instance.ps1`)
  - [x] Background start support
  - [x] Return success/failure status
- [x] `Stop-DistroNexusInstance` (from `stop_instance.ps1`)
  - [x] Force terminate option
  - [x] Graceful shutdown option
- [x] `Move-DistroNexusInstance` (from `move_instance.ps1`)
  - [x] Export → Import workflow
  - [x] Progress reporting via Write-Progress
  - [x] Rollback on failure
- [x] `Rename-DistroNexusInstance` (from `rename_instance.ps1`)
  - [x] Validate new name uniqueness
- [x] `Remove-DistroNexusInstance` (from `uninstall_wsl_custom.ps1`)
  - [x] Confirmation prompt (-Force to skip)
  - [x] Clean up VHDX files option

### 1.4 Installation Cmdlets
- [x] `Install-DistroNexusInstance` (from `install_wsl_custom.ps1`)
  - [x] Parameters: DistroName, InstallPath, Username, Password
  - [x] Quick mode (defaults) vs Advanced mode
  - [x] Progress reporting for download/extract
- [x] `Set-DistroNexusCredential` (from `set_credentials.ps1`)
  - [x] Secure password handling (SecureString)

### 1.5 Package Management Cmdlets
- [x] `Get-DistroNexusPackage` (from `list_distros.ps1`)
  - [x] List available distros from catalog
  - [x] Show cached/online status
- [x] `Save-DistroNexusPackage` (from `download_all_distros.ps1`)
  - [x] Download to cache directory
  - [x] Resume support for interrupted downloads
- [x] `Update-DistroNexusCatalog` (from `update_distros.ps1`)
  - [x] Fetch latest distros.json from GitHub
  - [x] Fallback to local copy on failure

### 1.6 Testing
- [ ] Create Pester test suite structure
- [ ] Unit tests for each public cmdlet
- [ ] Integration tests for WSL operations (mock where needed)
- [ ] Test coverage report generation

> **Note**: Testing infrastructure is deferred to later iteration. Module is functional for integration testing.

---

## Phase 2: .NET Client Development (WPF)

### 2.1 Solution Setup
- [x] Create `src/Client/` directory
- [x] Initialize .NET 10 solution `DistroNexus.sln`
- [x] Create projects:
  - [x] `DistroNexus.Desktop` (WPF Application)
  - [x] `DistroNexus.Core` (Class Library)
  - [x] `DistroNexus.Tests` (xUnit Test Project)
- [x] Configure Directory.Build.props for shared settings
- [x] Add .editorconfig for code style enforcement

### 2.2 NuGet Packages
- [x] CommunityToolkit.Mvvm (MVVM infrastructure)
- [x] Microsoft.Extensions.DependencyInjection
- [x] Microsoft.Extensions.Logging
- [x] WPF-UI or MaterialDesignInXamlToolkit (UI framework)
- [x] System.Management.Automation (PowerShell hosting)
- [x] System.Text.Json (configuration)

### 2.3 Core Library (DistroNexus.Core)

#### 2.3.1 Models
- [x] `WslInstance` - Represents an installed WSL instance
- [x] `DistroPackage` - Represents an available/cached package
- [x] `GlobalSettings` - Application settings
- [x] `InstallOptions` - Installation configuration

#### 2.3.2 Interfaces
- [x] `IPowerShellService` - Execute PS cmdlets
- [x] `IWslManagerService` - High-level WSL operations
- [x] `IDownloadService` - File download with progress
- [x] `ISettingsService` - Settings persistence
- [x] `ICatalogService` - Distro catalog management

#### 2.3.3 Services Implementation
- [x] `PowerShellService`
  - [x] Execute cmdlets via System.Management.Automation
  - [x] Parse PSObject results to C# models
  - [x] Error handling and logging
- [x] `WslManagerService`
  - [x] Wrap PowerShell module calls
  - [x] Async operations with CancellationToken
- [x] `DownloadService`
  - [x] HttpClient with progress reporting
  - [x] Resume interrupted downloads
  - [x] Checksum verification
- [x] `SettingsService`
  - [x] JSON file read/write
  - [x] Settings validation
- [x] `CatalogService`
  - [x] Load/update distros.json
  - [x] Cache management

### 2.4 WPF Application (DistroNexus.Desktop)


#### 2.4.1 Infrastructure
- [x] Configure App.xaml with DI container
- [x] Implement INavigationService for page navigation
- [x] Theme support (Light/Dark mode)
- [x] Global exception handling

#### 2.4.2 ViewModels
- [x] `MainViewModel` - Shell/navigation
- [x] `DashboardViewModel` - Instance list and quick actions (merged into MainViewModel)
- [x] `InstallWizardViewModel` - Installation workflow
- [x] `InstanceDetailViewModel` - Single instance management (merged into WslInstanceViewModel)
- [x] `PackageManagerViewModel` - Package browsing/download
- [x] `SettingsViewModel` - Global settings

#### 2.4.3 Views (XAML)
- [x] `MainWindow.xaml` - Application shell with navigation
- [x] `DashboardPage.xaml` - Home page with instance cards (implemented in MainWindow)
- [x] `InstallWizardDialog.xaml` - Modal installation wizard
- [x] `InstanceDetailPage.xaml` - Instance details and actions (merged into MainWindow)
- [x] `PackageManagerPage.xaml` - Package list with actions
- [x] `SettingsPage.xaml` - Settings form

#### 2.4.4 UI Components
- [x] `InstanceCard` - UserControl for instance display (implemented inline in MainWindow)
- [x] `PackageListItem` - UserControl for package row (implemented inline in PackageManagerPage)
- [x] `ProgressDialog` - Modal progress indicator
- [x] `ConfirmationDialog` - Reusable confirmation prompt

#### 2.4.5 Converters & Helpers
- [x] `BoolToVisibilityConverter`
- [x] `InstanceStateToColorConverter`
- [x] `FileSizeFormatter`
- [x] `RelayCommand` (using CommunityToolkit.Mvvm)

### 2.5 Feature Implementation

#### Dashboard Features
- [x] Display all installed WSL instances
- [x] Show instance status (Running/Stopped) with live refresh
- [x] Quick action buttons: Start, Stop, Terminal
- [x] Instance context menu: Move, Rename, Credentials, Uninstall
- [x] Disk usage display per instance

#### Installation Wizard
- [x] Step 1: Select distribution from catalog
- [x] Step 2: Choose installation path (folder picker)
- [x] Step 3: Configure username/password (optional)
- [x] Step 4: Review and confirm
- [x] Progress display during installation
- [x] Success/failure notification

#### Package Manager
- [x] Display distro catalog grouped by family
- [x] Show cached/online status per package
- [x] Download button for online packages
- [x] Delete button for cached packages
- [x] Refresh catalog button
- [x] Add custom package source

#### Settings
- [x] Default installation path picker
- [x] Package cache path picker
- [x] Default terminal start path
- [x] Default distro selection
- [x] Theme selection (Light/Dark/System)
- [x] Online catalog source URL

### 2.6 Testing
- [x] Unit tests for all services
- [x] Unit tests for ViewModels
- [x] Mock PowerShellService for isolated testing
- [ ] Integration tests with real WSL (optional, CI consideration)

---

## Phase 3: Integration and Packaging

### 3.1 Build System
- [x] Create `tools/build.ps1` - Main build script
- [x] Configure Release build with optimizations
- [x] Embed PowerShell module in output
- [x] Copy configuration files to output

### 3.2 Packaging
- [x] Portable ZIP package
  - [x] Self-contained .NET runtime
  - [x] All dependencies included
  - [x] config/ folder with defaults
- [x] Installer (Inno Setup or WiX)
  - [x] Start menu shortcut
  - [x] Optional desktop shortcut
  - [x] Uninstaller
- [ ] Update manifest for auto-update (future)

### 3.3 CI/CD Updates
- [x] Update GitHub Actions workflow for .NET build
- [x] Automated testing on PR
- [x] Release artifact generation
- [ ] GitHub Pages documentation update

### 3.4 Documentation
- [x] Update README.md for v2.0.0
- [x] Update website docs for new UI
- [x] PowerShell module documentation (Get-Help)
- [x] Migration guide from v1.x

---

## Phase 4: Quality Assurance

### 4.1 Manual Testing Checklist
- [ ] Fresh install on clean Windows 10
- [ ] Fresh install on clean Windows 11
- [ ] Upgrade from v1.x (if applicable)
- [ ] All core features functional
- [ ] High-DPI display testing
- [ ] Dark mode testing
- [ ] Error handling (network offline, invalid paths, etc.)

### 4.2 Performance Validation
- [ ] Application startup time < 2 seconds
- [ ] Instance list refresh < 1 second
- [ ] Memory usage reasonable (< 150MB idle)
- [ ] No memory leaks during extended use

### 4.3 Release Preparation
- [x] Create release notes (docs/release_notes/v2.0.0.md)
- [x] Update version numbers
- [x] Tag release in git
- [x] Build release artifacts
- [ ] Publish to GitHub Releases

---

## Script to Cmdlet Mapping Reference

| Legacy Script | New Cmdlet | Status |
|--------------|------------|--------|
| `pwsh_utils.ps1` | `Private/Logger.ps1`, `Private/Config.ps1` | ✅ |
| `scan_wsl_instances.ps1` | `Get-DistroNexusInstance` | ✅ |
| `start_instance.ps1` | `Start-DistroNexusInstance` | ✅ |
| `stop_instance.ps1` | `Stop-DistroNexusInstance` | ✅ |
| `move_instance.ps1` | `Move-DistroNexusInstance` | ✅ |
| `rename_instance.ps1` | `Rename-DistroNexusInstance` | ✅ |
| `install_wsl_custom.ps1` | `Install-DistroNexusInstance` | ✅ |
| `uninstall_wsl_custom.ps1` | `Remove-DistroNexusInstance` | ✅ |
| `set_credentials.ps1` | `Set-DistroNexusCredential` | ✅ |
| `list_distros.ps1` | `Get-DistroNexusPackage` | ✅ |
| `download_all_distros.ps1` | `Save-DistroNexusPackage` | ✅ |
| `update_distros.ps1` | `Update-DistroNexusCatalog` | ✅ |

**Legend**: ⬜ Not Started | 🔄 In Progress | ✅ Completed
