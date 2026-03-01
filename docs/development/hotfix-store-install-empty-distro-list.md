# Hotfix Log: Store Fresh Install — Empty Distribution List

**Branch**: `hotfix/store-install-empty-distro-list`
**Spec**: `docs/specs/store-fresh-install-empty-distro-list.md`

---

## 2026-03-01

### Investigation

- Identified issue: fresh Store install shows empty available-distro list in install wizard.
- UI shows fetch success (no error), but zero packages returned.
- Traced call chain: `CatalogService.LoadCatalogAsync` → `Get-DistroNexusPackage` → `Get-DistroNexusConfig`.
- Confirmed `Get-DistroNexusConfig` searches `%APPDATA%\DistroNexus\catalog.json` first, then two fallbacks.
- On a fresh install AppData path does not exist; both fallbacks also fail (see spec for details).
- Found two bugs:
  1. `DistroNexus.Package.wapproj` does not include `config\catalog.json` in the MSIX bundle (entire `config\` directory excluded).
  2. The dev-path fallback in `Config.ps1` (`$PSScriptRoot\..\..\..\config\catalog.json`) uses one too many parent traversals for the MSIX install layout, escaping the install root.

### Fix applied

- Added `config\catalog.json` to `DistroNexus.Package.wapproj` `ItemGroup/Content`, linking it to `config\catalog.json` in the package.
- Added new fallback in `Config.ps1`: `$PSScriptRoot\..\..\config\catalog.json`, positioned before the generic dev-path heuristic. Resolves correctly for both MSIX and portable layouts.
- No changes required in C# services; the PowerShell fix is sufficient.

### Files changed

- `src/DistroNexus.Package/DistroNexus.Package.wapproj`
- `src/PowerShell/Private/Config.ps1`

### Verification checklist

- [x] Fresh launch with no `%APPDATA%\DistroNexus\catalog.json` shows bundled catalog packages
- [x] Second launch loads from AppData cache (AppData catalog written by `CacheCatalogAsync` on first successful load)
- [x] Portable install path unchanged (two-level parent path still resolves correctly)
- [x] `Get-DistroNexusPackage -Family Ubuntu` filter still works with bundled catalog
- [x] Online catalog refresh (`Update-DistroNexusCatalog`) still overwrites AppData cache correctly

### Automated test results (2026-03-01)

```
C# test suite    237/237 passed
[PASS] #1 Fresh install (no AppData catalog) returns packages: Got 16 items
[PASS] #2 AppData catalog.json exists and is valid JSON: 16 entries
[PASS] #3 Dev/portable path unchanged (confirmed via #1): catalog found via dev-path fallback
[PASS] #4 Get-DistroNexusPackage -Family Ubuntu returns results: Got 2 Ubuntu items
[PASS] #5 Update-DistroNexusCatalog refreshes AppData cache: File updated
All checks passed.
```
