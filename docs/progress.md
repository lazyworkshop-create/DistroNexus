# Store Publish Progress

## 2026-02-15

### Completed
- Completed implementation baseline audit against:
	- `docs/specs/store-publish-analysis.md`
	- `docs/development/store-publish-implementation-checklist.md`
	- `docs/development/store-publish-test-checklist.md`
	- `docs/development/store-publish-acceptance-checklist.md`
- Confirmed key engineering gaps: missing Store package project, missing Store build flow, and missing packaged-path evidence tests.

### In Progress
- Implementing Store packaging project and manifest.

### Completed
- Added Store packaging project:
	- `src/DistroNexus.Package/DistroNexus.Package.wapproj`
	- `src/DistroNexus.Package/Package.appxmanifest`
	- `src/DistroNexus.Package/Assets/*`
	- `src/DistroNexus.Package/Assets/StoreListing/Square44x44Logo.altform-unplated.png`
	- `src/DistroNexus.Package/Assets/StoreListing/Square150x150Logo.altform-unplated.png`
- Added Store build flow to `tools/build.ps1`:
	- New switch: `-StoreBuild`
	- Store version format enforcement: `Major.Minor.Patch.0`
	- Desktop Bridge prerequisite detection and fail-fast guidance
- Added packaged/development path resolver and refactored services:
	- `DistroNexus.Core/Services/AppResourcePathResolver.cs`
	- `TemplateService`, `CatalogService`, `PowerShellService`
- Added tests:
	- `DistroNexus.Tests/Services/AppResourcePathResolverTests.cs`
- Added Store submission support docs:
	- `docs/development/store-listing-metadata-template.md`
	- `docs/specs/privacy-policy.md`
	- `docs/development/store-settings-migration.md`
- Updated implementation/test/acceptance checklists with current status.

### Validation Results
- `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj` passed (`201/201`).
- `tools/build.ps1 -Publish -CreateZip -Configuration Release` passed.
- `tools/build.ps1 -StoreBuild -Configuration Release -Version 2.0.1` passed.
- Store artifacts generated:
	- `D:\repo\Local\DistroNexus\release\store\DistroNexus.Package_2.0.1.0_Test\DistroNexus.Package_2.0.1.0_x64_ARM64.msixbundle`
	- `D:\repo\Local\DistroNexus\release\store\DistroNexus.Package_2.0.1.0_x64_ARM64_bundle.msixupload`