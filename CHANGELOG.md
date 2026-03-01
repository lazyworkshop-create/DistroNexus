# Changelog

All notable changes to DistroNexus will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Added `Compress-DistroNexusInstance` cmdlet to compact WSL VHDX disks and reclaim unused space (F-01).
  Supports `-WhatIf` for dry-run estimates; uses `Optimize-VHD` (Hyper-V) with `diskpart` fallback.
- Added `CompactInstanceAsync` to `IWslManagerService` / `WslManagerService` (F-01).
- Added Docker Desktop integration management cmdlets: `Get-DistroNexusDockerIntegration`,
  `Enable-DistroNexusDockerIntegration`, and `Disable-DistroNexusDockerIntegration` (F-02).
  Reads/writes `integratedWslDistros` in Docker's settings JSON; guards against WSL v1 and reserved distros.
- Added `IDockerIntegrationService` / `DockerIntegrationService` with `IsDockerDesktopInstalledAsync`,
  `GetIntegrationStatusAsync`, and `SetIntegrationAsync` (F-02).

## [2.1.1] - 2026-02-21

### Added

- Added template metadata linting command (`Test-DistroNexusTemplateMetadata`) to validate template schemas.
- Added release evidence bundle collector (`New-DistroNexusReleaseEvidenceBundle`) for standardized release artifacts.
- Added historical regression diff generation for template automation runs.

### Changed

- Standardized evidence pipeline contracts with `SchemaVersion: '1.0'` for lint and evidence outputs.
- Hardened regression diff baseline resolution and ordering in PowerShell automation.
- Improved deterministic evidence generation with relative paths and stable naming for repeatable CI/local runs.
- Reorganized project tracking files chronologically for better auditability.

## [2.1.0] - 2026-02-19

### Added

- Added template-first install-wizard startup flow with advanced template options step.
- Added WSL2 validation workflow lane with environment capability gating.
- Added UI automation coverage for package download flow and screenshot regression baseline support.

### Changed

- Improved package download UX with byte-level progress/speed metrics and same-file package grouping.
- Updated CI test split strategy to explicit quick/full metadata scopes and aligned workflow execution behavior.
- Synchronized remediation governance documentation, acceptance tracking, and localization verification evidence.

### Fixed

- Hardened template execution and package install flows across C# and PowerShell integration paths.
- Fixed package cache path and filename resolution behavior in PowerShell module flows.
- Corrected template apply UI log-level and toggle visual consistency issues.

## [2.0.3] - 2026-02-18

### Fixed

-   Fixed critical configuration error where catalog URLs pointed to non-existent `main` branch (repository uses `master`).

## [2.0.2] - 2026-02-18

### Fixed

-   Fixed "Property not found" error when loading distribution list.
-   Fixed catalog loading issue when `IsCached` or `LocalPath` properties were missing.

### Changed

-   Automated fetching of official WSL distribution list from Microsoft during build.
-   Improved fallback logic for distribution catalog loading.
-   Updated `catalog.json` to be tracked in repository.

## [2.0.1] - 2026-01-31

### 🎉 Major Release - Complete Rewrite

This is the first v2 release of DistroNexus, migrating directly from v1.0.0 to .NET 10/WPF.

### Added

- **Modern WPF UI**: Native Windows application using WPF-UI (Fluent Design System)
- **Dark Mode Support**: Automatic theme switching based on system preferences
- **PowerShell Module**: 15 cmdlets for full automation capability
  - `Get-DistroNexusInstance` - List all WSL instances
  - `Start-DistroNexusInstance` - Start instances
  - `Stop-DistroNexusInstance` - Stop instances
  - `Move-DistroNexusInstance` - Relocate instances
  - `Rename-DistroNexusInstance` - Rename instances
  - `Remove-DistroNexusInstance` - Uninstall instances
  - `Install-DistroNexusInstance` - Custom installation
  - `Set-DistroNexusCredential` - Update credentials
  - `Get-DistroNexusPackage` - Browse available distributions
  - `Save-DistroNexusPackage` - Download distribution packages
  - `Remove-DistroNexusPackage` - Remove cached packages
  - `Update-DistroNexusCatalog` - Refresh catalog
  - `Get-DistroNexusTemplate` - List built-in templates
  - `Apply-DistroNexusTemplate` - Apply template to an instance
  - `Invoke-DistroNexusTemplateAutomation` - Run template automation pipeline
- **Package Manager**: Browse and download WSL distributions from catalog
- **Installation Wizard**: Step-by-step installation process
- **Settings Page**: Comprehensive configuration options
- **Progress Tracking**: Real-time progress for downloads and operations
- **Template System**: Built-in templates for rapid environment bootstrapping

### Changed

- Complete UI rewrite from Go/Fyne to .NET 10/WPF
- Configuration now stored in `%APPDATA%\DistroNexus\settings.json`
- Catalog contract standardized to `catalog.json`
- PowerShell scripts consolidated into formal PowerShell module
- Improved error handling and logging

### Cleanup

- **Removed v1.0 Go/Fyne artifacts**:
  - Deleted all Go source code (`src/go.mod`, `src/cmd/`, `src/internal/`)
  - Removed Go build scripts (`tools/build.sh`, `tools/windows_release.ps1`, `tools/setup_go_env.sh`, `tools/gen_gear.go`)
  - Deleted legacy standalone PowerShell scripts from `scripts/` directory (11 management scripts, 2 test utilities, 3 helper scripts) - all replaced by PowerShell module cmdlets
  - Removed empty v1.0 release directory
- **Archived v1 comparison documents** to `docs/archive/v1-comparison/` for historical reference
- **Updated documentation**: Removed v1.0 references from README.md and README_CN.md, kept only v2.0 .NET/WPF + PowerShell module architecture

---

## [1.0.2] - 2025-xx-xx

### Fixed

- Bug fixes and stability improvements

## [1.0.1] - 2025-xx-xx

### Fixed

- Initial bug fixes

## [1.0.0] - 2025-xx-xx

### Added

- Initial release with Go/Fyne UI
- Basic WSL instance management
- Distribution catalog
- PowerShell scripts for automation
