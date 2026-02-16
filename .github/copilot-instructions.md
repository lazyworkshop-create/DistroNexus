# DistroNexus Copilot Instructions

## Project Overview
DistroNexus is a Windows Subsystem for Linux (WSL) manager (v2.0).
- **Stack**: .NET 6/7/8, WPF (WPF UI / HandyControl), PowerShell Backend.
- **Architecture**: MVVM with Dependency Injection.
- **Platform**: Windows 10/11 only.

## Key Rules
1. **Language**: **English ONLY** for all code, comments, and internal docs.
2. **Conventions**:
   - **C#**: PascalCase for classes/methods (`GetInstancesAsync`), `_camelCase` for private fields.
   - **PowerShell**: Verb-Noun format (`Install-DistroNexusInstance`).
   - **Commits**: Follow [Conventional Commits](https://www.conventionalcommits.org/) (`feat(ui): add refresh button`).
3. **Coding Patterns**:
   - **MVVM**: `View` (XAML) ↔ `ViewModel` (CommunityToolkit.Mvvm) ↔ `Service` ↔ `PowerShell Module`.
   - **Async**: Use `async/await` for all I/O. Methods ending in `Async`.
   - **DI**: Constructor injection for all services (`IPowerShellService`, `ILogger`).
4. **Error Handling**: 
   - Catch specific exceptions first.
   - Log all errors. 
   - Display user-friendly messages for UI errors.

## Planning
- For complex tasks (3+ steps), always create `docs/task_plan.md`, `docs/findings.md`,
  and `docs/progress.md` before writing any code
- Before starting work, read `docs/task_plan.md` to understand current progress
- After completing each phase, immediately update the status in `docs/task_plan.md`
- Write research conclusions to `docs/findings.md`, not just in the conversation

## Documentation Management
- **Location**:
   - Descriptive top-level documents (for example `README*`, `LICENSE`, `CHANGELOG`) may stay at repository root.
   - All other documentation files MUST be placed under root `docs/` and organized by type (for example `docs/architecture/`, `docs/development/`, `docs/specs/`, `docs/release_notes/`).
   - Do not place non-descriptive documentation files at repository root.
- **Naming**: English filenames, `kebab-case` preferred.
- **Archiving**: 
   - Move completed plans, scratchpads, and status reports to `docs/archive/{year}/{month}/{yyyymmdd}_{topic}/` immediately after the milestone is reached.
   - Use zero-padded numeric folders for year/month (for example `docs/archive/2026/02/20260214_template-system-audit/`).
   - Do not store archived documents directly under `docs/archive/`.
- **Living Docs**: Update `docs/architecture/` and `docs/development/` when code changes. do NOT create new "Update Report" files; update the original doc instead.

## Project Structure
- `src/Client`:
    - `DistroNexus.Desktop`: WPF App (Views, ViewModels).
    - `DistroNexus.Core`: Business Logic, Models, Interfaces.
    - `DistroNexus.Tests`: xUnit tests.
- `src/PowerShell`: Backend logic (Public/Private functions, `.psm1`).
- `tools`: Build and packaging scripts.

## Tech Specifics
- **WPF**: Use `[ObservableObject]`, `[RelayCommand]`, `ObservableCollection<T>`.
- **PowerShell**: Use `[CmdletBinding()]`, validate parameters, use `Write-Error/Verbose`.
- **Testing**: xUnit + Moq (C#), Pester (PowerShell).
- **Security**: No hardcoded credentials. Validate all paths. Sanitize inputs.

## Build Environment Memory (Persistent)
- Local machine has both VS2022 BuildTools and VS2026 installed.
- VS2026 details (preferred for Store packaging):
   - Display: `Visual Studio Community 2026`
   - Version: `18.4.11506.43` (Insiders)
   - Path: `C:\Program Files\Microsoft Visual Studio\18\Insiders`
   - MSBuild: `C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe`
- VS2022 BuildTools path:
   - `C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools`
- Store packaging rule: always prefer the highest Visual Studio installation version (including prerelease) for DesktopBridge/MSBuild discovery to avoid .NET 10 and MSBuild 17 mismatch.
