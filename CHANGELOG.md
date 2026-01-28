# Changelog

All notable changes to DistroNexus will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-01-27

### 🎉 Major Release - Complete Rewrite

This is a complete rewrite of DistroNexus, migrating from Go/Fyne to .NET 10/WPF.

### Added

- **Modern WPF UI**: Native Windows application using WPF-UI (Fluent Design System)
- **Dark Mode Support**: Automatic theme switching based on system preferences
- **PowerShell Module**: 11 cmdlets for full automation capability
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
  - `Update-DistroNexusCatalog` - Refresh catalog
- **Package Manager**: Browse and download WSL distributions from catalog
- **Installation Wizard**: Step-by-step installation process
- **Settings Page**: Comprehensive configuration options
- **Progress Tracking**: Real-time progress for downloads and operations
- **Self-contained Package**: Option to run without .NET Runtime installed

### Changed

- Complete UI rewrite from Go/Fyne to .NET 10/WPF
- Configuration now stored in `%APPDATA%\DistroNexus\settings.json`
- PowerShell scripts consolidated into formal PowerShell module
- Improved error handling and logging

### Removed

- Go/Fyne-based UI (replaced with .NET/WPF)
- Legacy PowerShell scripts (replaced with module cmdlets)

### Technical

- .NET 10 with WPF UI Framework
- MVVM architecture with CommunityToolkit.Mvvm
- Dependency Injection via Microsoft.Extensions.DependencyInjection
- Async/await patterns throughout
- xUnit tests with Moq for unit testing

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
