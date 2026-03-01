# Analysis & Requirements: Store Fresh Install — Empty Distribution List

**Document Type**: Spec / Bug Analysis
**Branch**: `hotfix/store-install-empty-distro-list`
**Status**: Fix in progress

---

## 1. Problem Statement

After a clean installation from the Windows Store, the available distribution list
(shown in the install wizard's package selection step) is empty. The UI reports the
fetch as successful (no error dialog) but returns zero results.

The symptom is reproducible on any machine where:
- The app was installed via the Windows Store (MSIX) for the first time, **and**
- No prior `%APPDATA%\DistroNexus\catalog.json` exists.

Other install methods (portable/standalone) are unaffected because those layouts
keep `config\catalog.json` at a location the path-resolution fallback already
handles.

---

## 2. Root Cause Analysis

### 2.1 Catalog loading path

`CatalogService.LoadCatalogAsync` delegates entirely to the PowerShell module:

```
CatalogService.LoadCatalogAsync
  → PowerShellService.ExecuteAsync<List<DistroPackage>>("Get-DistroNexusPackage")
      → Get-DistroNexusPackage (ps1)
          → Get-DistroNexusConfig
              → reads catalog.json
```

### 2.2 `Get-DistroNexusConfig` path resolution order

| Priority | Path tried | Fresh Store install result |
|---|---|---|
| 1 | `%APPDATA%\DistroNexus\catalog.json` | **Missing** — no prior launch |
| 2 | `%APPDATA%\DistroNexus\config\catalog.json` | **Missing** |
| 3 | `$PSScriptRoot\..\..\..\config\catalog.json` | **Wrong level** — resolves *outside* the MSIX install root; also not present |

When all candidates fail, `$config.Distros` remains `$null`. `Get-DistroNexusPackage`
returns early with no output. `CatalogService` receives an empty list and silently
returns `[]`.

### 2.3 Bug 1 — `catalog.json` is not packaged in the MSIX

`DistroNexus.Package.wapproj` includes only `Assets\` and `..\PowerShell\`:

```xml
<Content Include="..\PowerShell\**\*.*">
  <Link>PowerShell\%(RecursiveDir)%(Filename)%(Extension)</Link>
</Content>
```

`config\catalog.json` is absent from the package manifest, so the bundled baseline
catalog never reaches the install directory.

### 2.4 Bug 2 — Wrong relative path in `Config.ps1` fallback

The dev-environment fallback:

```powershell
$devPath = Join-Path $PSScriptRoot "..\..\..\config\catalog.json"
```

In the source tree, `Config.ps1` is at `src/PowerShell/Private/`, so `..\..\..`
reaches the project root — correct for dev.

In the MSIX package, `Config.ps1` is at `PowerShell\Private\`, so `..\..\..`
escapes the install directory entirely. No catalog is found.

The correct module-relative path inside the MSIX is:
```
PowerShell\Private\  →  ..\  →  PowerShell\  →  ..\  →  <install root>\  →  config\catalog.json
```
That is: `$PSScriptRoot\..\..\config\catalog.json`

---

## 3. Impact

| Scenario | Affected? |
|---|---|
| Store install, first launch | **Yes** — empty distro list |
| Store install, second+ launch (after catalog cached to AppData) | No |
| Portable install (has `config\catalog.json` adjacent to `PowerShell\`) | No |
| Dev/run from source | No |

The bug also affects any clean-reinstall scenario where the AppData profile has been
cleared.

---

## 4. Requirements for the Fix

### FR-01 — Include `catalog.json` in MSIX
The file `config\catalog.json` MUST be included in the MSIX bundle so it is
present at `<install root>\config\catalog.json` after Store installation.

### FR-02 — Module-relative catalog path fallback
`Get-DistroNexusConfig` MUST attempt `$PSScriptRoot\..\..\config\catalog.json`
as an explicit fallback **before** the current generic dev-path heuristic, so it
correctly resolves the bundled catalog in both MSIX and portable layouts.

### FR-03 — Silent success behaviour preserved
When AppData catalog exists (all non-first launches), behaviour must remain
unchanged: AppData copy takes priority, falling back to the bundled copy only when
the AppData path is absent.

### NFR-01 — No breaking change for portable install
The portable install layout already places `config\catalog.json` two levels above
`PowerShell\Private\`. The new fallback path resolves identically for both MSIX and
portable layouts; existing behaviour is preserved.

### NFR-02 — No network dependency for first launch
First launch must show the bundled catalog without requiring internet access. The
online catalog refresh (`Update-DistroNexusCatalog`) remains opt-in.

---

## 5. Files to Change

| File | Change |
|---|---|
| `src/DistroNexus.Package/DistroNexus.Package.wapproj` | Add `config\catalog.json` as packaged content (FR-01) |
| `src/PowerShell/Private/Config.ps1` | Add `$PSScriptRoot\..\..\config\catalog.json` fallback (FR-02) |
