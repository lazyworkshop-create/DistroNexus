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
- For complex tasks (3+ steps), always create `task_plan.md`, `findings.md`,
  and `progress.md` before writing any code
- Before starting work, read `task_plan.md` to understand current progress
- After completing each phase, immediately update the status in `task_plan.md`
- Write research conclusions to `findings.md`, not just in the conversation

## Documentation Management
- **Location**: All development docs go into `docs/`. User docs go into `website/`.
- **Naming**: English filenames, `kebab-case` preferred.
- **Archiving**: 
  - Move completed plans, scratchpads, and status reports to `docs/archive/` immediately after the milestone is reached.
  - Prefix archived files with date `YYYYMMDD_` if order matters.
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
