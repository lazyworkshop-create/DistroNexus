# Store Publish Findings

Date: 2026-02-15

## Baseline Findings
- Repository has no Store packaging project (`.wapproj`) and no `Package.appxmanifest`.
- `tools/build.ps1` currently supports classic build/publish/zip only; no Store artifact generation.
- `DistroNexus.Desktop.csproj` already includes root `config/**` into output, useful for packaged fallback.
- `PowerShell` module files are not currently part of desktop build output by project file; classic release script copies them explicitly.

## Runtime Path Findings
- `TemplateService` and `CatalogService` each implement independent local config path probing logic.
- `PowerShellService` probes several module paths; packaged layout should work if `PowerShell` folder exists in app install directory.
- No direct `Directory.GetCurrentDirectory()` dependence found in `DistroNexus.Core` for these services.

## Implementation Decisions
- Add dedicated Store wrapper project under `src/DistroNexus.Package`.
- Add Store build mode to `tools/build.ps1` instead of changing existing publish outputs.
- Keep Store identity values exactly aligned with spec.
- Add targeted tests for packaged/development path probing behavior.

## Validation Findings
- Full automated test suite passed: `201 passed, 0 failed` (`dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj`).
- Standalone release and portable zip regression passed through `tools/build.ps1 -Publish -CreateZip`.
- Store build pipeline now works with VS 2026/18 (Insiders) DesktopBridge targets and Visual Studio MSBuild.
- Store artifacts generated successfully:
	- `D:\repo\Local\DistroNexus\release\store\DistroNexus.Package_2.0.1.0_Test\DistroNexus.Package_2.0.1.0_x64_ARM64.msixbundle`
	- `D:\repo\Local\DistroNexus\release\store\DistroNexus.Package_2.0.1.0_x64_ARM64_bundle.msixupload`

