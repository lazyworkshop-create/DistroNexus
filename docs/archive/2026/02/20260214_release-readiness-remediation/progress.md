# Progress

## 2026-02-14
- Created planning/traceability documents for remediation implementation.
- Completed P0 implementation:
	- Unified catalog contract to `catalog.json`.
	- Removed runtime hardcoded development path fallback.
	- Added repository `config/catalog.json` for publish consistency.
- Completed P1 implementation:
	- Synced README/README_CN/release notes cmdlet names and count with module exports.
	- Fixed placeholder URLs and broken contribution reference handling.
	- Aligned version defaults to `2.0.1` in module and build/installer scripts.
- Completed P2 implementation:
	- Deprecated legacy installer script under `tools/packaging` with explicit guard.
- Validation results:
	- `dotnet test src/Client/DistroNexus.slnx -c Release` passed.
	- `tests/PowerShell/TestRunner.ps1` passed.
	- `tools/build.ps1 -Configuration Release -Publish` succeeded.
