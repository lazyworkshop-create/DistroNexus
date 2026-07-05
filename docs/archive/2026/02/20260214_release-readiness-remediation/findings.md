# Findings

## 2026-02-14
- Initialized findings document for v2.0 release readiness remediation.
- Adopted `catalog.json` as canonical catalog contract across runtime, scripts, and publish output.
- Removed hardcoded local developer fallback from runtime catalog path resolution.
- Aligned README and release notes cmdlet naming/count with `FunctionsToExport` (15 commands).
- Updated public placeholders and repository links to `LazyWorkshopCreate/DistroNexus`.
- Normalized default version surface to `2.0.1` across module manifest and build/installer scripts.
- Marked `tools/packaging/DistroNexus.iss` as deprecated with explicit compile-time guard.
- Verification completed: .NET tests pass, PowerShell tests pass, publish succeeds with `config/catalog.json` present.
