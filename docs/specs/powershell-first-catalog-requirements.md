# PowerShell-First Catalog and Package Cache Requirements

## Purpose

The catalog, catalog refresh, and package-cache capabilities must have one product execution path: callers and the WPF client invoke fixed PowerShell commands, while Core performs the internal, capability-specific work without calling the module back. This removes the current recursive execution path and makes catalog behavior available to automation and the desktop client under the same validation, consent, and result contracts.

## Project Context

- Project and repository: DistroNexus; `D:/repo/lazyworkshop-create/DistroNexus`.
- Parent authority: `docs/specs/powershell-first-requirements.md` FR-001 through FR-007 and `docs/architecture/powershell-first-decision.md`.
- Current behavior: `CatalogService` calls `Get-DistroNexusPackage`, `Update-DistroNexusCatalog`, and `Remove-DistroNexusPackage`, while the module owns those operations directly. That arrangement cannot be used behind WorkspaceBridge without a module-to-bridge-to-module cycle.
- Permitted mutation: repository source, tests, and documentation only. Network, WSL, installation, publishing, and user catalog/cache mutation are excluded from verification.

## Scope

- In scope: catalog list/search/get, source-aware refresh, catalog cache persistence/fallback, package-cache location/usage/delete/clear, fixed PowerShell commands, typed bridge contracts, and all WPF catalog/cache consumers.
- Out of scope: a generic command or script transport, package download task migration before its own durable task contract is designed, and mutation of a real user catalog or package cache during tests.
- Compatibility: existing package-list, update, and removal command names remain available. A legacy `GlobalSettings.CatalogUrl` remains readable as defined below; no existing user configuration is silently discarded.

## Actors and Trust Boundaries

| Actor/component | Trust level | Permitted responsibility |
| --- | --- | --- |
| PowerShell caller and WPF client | External/presentation | Submit only modeled catalog or cache input and render typed results. |
| PowerShell module | Product boundary | Own command grammar, validation, `ShouldProcess`, and public result/error shaping. |
| WorkspaceBridge and CatalogService | Internal trusted | Execute fixed catalog/cache operations and preserve filesystem/network safety. |
| Catalog source URL and catalog document | Untrusted | Supply only validated JSON over a validated URI; never supply commands, paths, or executable content. |

## Functional Requirements

### FR-101 Native catalog ownership

`CatalogService` is the internal owner of catalog loading, search, lookup, refresh, package-cache status, and cache cleanup. It must not invoke `IPowerShellService`, module commands, or arbitrary PowerShell text for these operations.

Acceptance: a structural test proves that the catalog implementation has no PowerShell-service dependency, and every new bridge route invokes the native service directly.

### FR-102 Fixed read contract

Automation can list packages, search by a bounded text query, and get a package by identifier through fixed exported commands and versioned bridge operations. Results are typed package objects and never formatted display text or raw catalog JSON.

Acceptance: command, bridge, and typed desktop-client tests cover list, search, lookup, singleton/empty results, cancellation, invalid identifiers, and unknown route rejection.

### FR-103 Source-aware refresh and recovery

Catalog refresh considers active persisted sources in ascending priority. If source metadata has never been persisted, the legacy `GlobalSettings.CatalogUrl` is the sole compatibility source. The first source that returns a valid catalog becomes authoritative for that refresh; later sources are attempted only after fetch or validation failure. A successful refresh replaces the local cache atomically. A failed refresh preserves the last known-good cache and reports a typed failure without exposing secrets.

Acceptance: tests prove priority/fallback selection, legacy compatibility, schema rejection, cancellation, atomic replacement, offline fallback, and no cache replacement after failure.

### FR-104 Mutation consent

Refresh, cached-package deletion, and cache clear are PowerShell mutations. They use `SupportsShouldProcess`; `WhatIf` and declined confirmation perform no bridge, HTTP, or filesystem mutation. No public command reads confirmation text with `Read-Host`.

Acceptance: each mutation has success, invalid input, `WhatIf`, declined confirmation, and underlying failure tests.

### FR-105 Package-cache containment

Package-cache paths derive only from modeled settings or the product default. Deletion and clearing operate only on files proven to be inside that resolved cache root, preserve cancellation, and return modeled outcomes rather than silently deleting arbitrary caller-provided paths.

Acceptance: tests cover containment, missing files, partial failures, cancellation, and cache-status projection.

### FR-106 Presentation migration

`PackageManagerViewModel`, catalog-related settings actions, and catalog-selecting wizard flows invoke the closed typed module client rather than `ICatalogService` or direct download/cache services. Download task behavior remains in scope only after FR-107 is designed.

Acceptance: structural and view-model routing tests reject direct catalog/cache execution from Desktop and prove each migrated handler maps to a named typed client operation.

### FR-107 Durable download task contract

Before WPF download-task ownership is removed, the product defines fixed module/bridge operations for starting a package download and observing, cancelling, retrying, and clearing its durable task state. The contract specifies lifetime, ownership, progress bounds, cancellation, and recovery across client restarts.

Acceptance: no implementation slice removes `IDownloadTaskManager` from Desktop until an approved contract and state/recovery tests exist.

### FR-108 Complete consumer and compatibility closure

Every current `ICatalogService` consumer has an explicit fixed-command and typed-client outcome. `AddCustomSourceAsync` is retired as a catalog mutation: callers create a named source through the source command family and then explicitly refresh. Package-cache location is returned only as a modeled cache-location result and is never a Desktop-owned directory-creation or download-execution permission. Legacy public parameters remain compatible as follows: `Get-DistroNexusPackage -Family` filters the typed list; `Update-DistroNexusCatalog -SourceUrl` is a one-call validated override that does not persist or alter source priority; `Remove-DistroNexusPackage -DefaultName` resolves through the native catalog, while `-LocalPath` is rejected unless it resolves inside the package cache root; `-Force` is removed in favor of standard PowerShell `-Confirm:$false` behavior.

Acceptance: a maintained consumer matrix maps Package Manager, Settings, and all wizard catalog flows to named typed operations; Pester proves every accepted legacy parameter and explicit rejection/migration behavior.

### FR-109 Cache-entry identity and bounds

Package-cache usage returns an authenticated opaque `CacheEntryId` for each listed file. Its user-local protected signing key persists across module and bridge processes. Cache deletion accepts that identifier and Core verifies its integrity, current root binding, file identity, expiry, and containment before every operation; caller-provided package names, paths, or filenames are never trusted as cache-file authority. Usage returns at most 1,000 entries and reports whether more eligible entries exist, while totals cover all eligible files.

Acceptance: tests prove a returned identifier deletes only its contained entry in a later module/bridge process, forged/traversal/stale/expired identifiers fail before filesystem mutation with a stable sanitized error, and usage bounds do not hide total usage or permit unbounded memory work.

## Non-Functional Requirements

- Security: reject unsafe URLs, malformed documents, path traversal, and arbitrary file paths; retain error redaction.
- Reliability: use atomic cache replacement; do not overwrite a known-good cache after a failed fetch or parse; preserve cancellation.
- Compatibility: treat `CatalogUrl` as a legacy source only when source metadata is absent; source-manager persistence becomes authoritative once present.
- Read precedence: a successful in-memory snapshot is used first; otherwise the durable validated catalog cache is used; otherwise the bundled catalog is used; otherwise reads return a typed no-catalog result. Network fetch occurs only through explicit refresh.
- Observability: use fixed operation/error identifiers and existing sanitized logging only; no new telemetry.

## Acceptance Criteria

- No supported catalog/cache operation can traverse `Core -> IPowerShellService -> module`.
- Catalog/source/cache public mutations share PowerShell consent behavior with WPF.
- WPF has no remaining direct catalog/cache business-service execution after the relevant migration slices.
- Download-task migration is blocked until its explicit durable-state contract is accepted.

## Open Decisions and External Inputs

| Item | Why it matters | Owner | Smallest next action |
| --- | --- | --- | --- |
| Real remote source and offline-host behavior | Repository tests cannot prove real TLS, proxy, or source availability. | Release/UAT owner | Exercise ordered source fallback and offline cache recovery on a disposable host. |

## Source Evidence

| Area | Source | What it confirms | Confidence |
| --- | --- | --- | --- |
| Recursive catalog path | `src/Client/DistroNexus.Core/Services/CatalogService.cs` | Load, refresh, and delete currently call module commands. | Confirmed |
| Existing public commands | `src/PowerShell/Public/Get-DistroNexusPackage.ps1`, `Update-DistroNexusCatalog.ps1`, `Remove-DistroNexusPackage.ps1` | Commands are module-native and refresh/removal lack standard consent behavior. | Confirmed |
| Source state | `src/Client/DistroNexus.Core/Services/CatalogSourceManager.cs` | Persisted source state, priorities, and defaults are independent of catalog refresh today. | Confirmed |
| WPF consumers | `PackageManagerViewModel.cs`, `SettingsViewModel.cs`, wizard catalog views | Desktop directly invokes catalog/cache and download-task services. | Confirmed |
