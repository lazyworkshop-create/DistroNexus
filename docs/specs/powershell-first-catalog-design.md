# PowerShell-First Catalog and Package Cache Technical Design

## Scope and Requirement Traceability

- Requirements: `docs/specs/powershell-first-catalog-requirements.md` FR-101 through FR-109.
- Parent constraints: `docs/specs/powershell-first-requirements.md` FR-001 through FR-007 and `docs/architecture/powershell-first-decision.md`.
- Exclusions: arbitrary bridge execution, real-host mutation, and download-task implementation before its contract slice.

| Requirement | Design section | Test or verification |
| --- | --- | --- |
| FR-101 | Architecture and Ownership | Catalog dependency/bridge-route tests. |
| FR-102 | Contracts and Behavior | Pester, bridge, client, and view-model route tests. |
| FR-103 | Data and Execution Semantics | Native catalog refresh/cache tests. |
| FR-104 | Contracts and Behavior | `WhatIf`/decline/no-operation tests. |
| FR-105 | Data and Execution Semantics | Path-containment and cancellation tests. |
| FR-106 | Architecture and Ownership | Desktop structural and handler-routing tests. |
| FR-107 | Open Items | Separate accepted task-contract design before code. |
| FR-108 | Architecture and Ownership; Contracts and Behavior | Consumer matrix, legacy command Pester, and structural routing tests. |
| FR-109 | Data and Execution Semantics | Cache-entry containment, stale/forged identifier, and bounded usage tests. |

## Architecture and Ownership

The required request path is `PowerShell caller or WPF -> fixed exported catalog command -> fixed versioned WorkspaceBridge route -> native CatalogService -> typed result`. `CatalogService` receives only explicit settings/source, HTTP, and filesystem dependencies; it does not receive `IPowerShellService` and does not invoke the module. This prevents the prohibited recursive path `module -> bridge -> CatalogService -> module`.

Bridge routes are capability-specific: `catalog.list.v1`, `catalog.search.v1`, `catalog.get.v1`, `catalog.refresh.v1`, `package-cache.location.v1`, `package-cache.usage.v1`, `package-cache.delete.v1`, and `package-cache.clear.v1`. Location and usage use a pure canonical root resolver that never creates directories, enumerates files, or writes settings. Usage streams all eligible files to calculate totals, retains only the first 1,000 display entries, and returns `HasMoreEntries`. Each entry receives an HMAC capability token bound to canonical root identity, normalized relative path, length, last-write time, and a 15-minute expiry. The signing key is generated once per user, stored in the product settings root protected to the current Windows user, and loaded by every bridge process; it is never returned or logged. Deletion verifies HMAC, expiry, current root, current identity, containment, and no reparse-point escape immediately before delete; forged, expired, and stale tokens fail with stable sanitized `PackageCache.EntryInvalid`. A normal module/bridge restart does not invalidate an otherwise-current token. Clear streams every eligible file without materializing an unbounded list and returns per-file partial outcome totals. Each route accepts and returns a typed JSON envelope. Unknown or malformed payloads return the stable bridge invalid-request error before service execution.

The consumer migration matrix is fixed: Package Manager list/search/refresh/get routes use catalog reads; its legacy URL-only custom-source submission validates the URI, derives the source name from its DNS host, uses an empty description and `IsActive=true`, calls `Add-DistroNexusCatalogSource`, then explicitly calls `Update-DistroNexusCatalog`. Duplicate URLs are rejected by the existing source contract; identical derived names are allowed. Cache path/usage/delete/clear use package-cache routes; Settings uses cache usage/delete/clear only; installation wizard, workflow wizard, and SelectDistribution step use catalog list/get only. PackageManager download destination uses the typed package-cache location result but retains no permission to create or mutate that directory. Main download task state remains outside this matrix until FR-107.

The source manager remains the owner of source metadata. Source configuration is resolved as follows:

1. If `CustomData[CatalogSources]` is absent, create one in-memory legacy source from `GlobalSettings.CatalogUrl`; do not persist it during a read.
2. If the key is present, select active persisted sources ordered by `Priority`, then stable source identifier.
3. Refresh tries each source sequentially. The first successfully fetched and schema-valid catalog is the complete authoritative result; sources are not merged.
4. A successful refresh atomically replaces the product catalog cache. A failed refresh leaves the existing cache intact and returns a typed failure. Reads can still project that last known-good cache.

This compatibility decision preserves legacy `CatalogUrl` behavior until a user explicitly persists source-manager state, while making ordered source configuration authoritative thereafter.

## Contracts and Behavior

Public commands retain existing names where possible:

- `Get-DistroNexusPackage` gains only modeled read parameters such as `Family`, `Query`, or `Id`; it maps to list/search/get routes rather than loading config directly.
- `Update-DistroNexusCatalog` maps to `catalog.refresh.v1`, supports `ShouldProcess`, and returns a typed refresh result (success, selected source identifier, cache state, and sanitized diagnostic code).
- `Remove-DistroNexusPackage` maps to token-authorized cache deletion, supports `ShouldProcess`, and never accepts an arbitrary path or calls `Read-Host`. Its `CacheEntryId` parameter is the normal deletion contract.
- New `Get-DistroNexusPackageCacheLocation`, `Get-DistroNexusPackageCacheUsage`, and `Clear-DistroNexusPackageCache` commands provide typed cache operations rather than overloading instance-cache diagnostics.

Compatibility is exact: `Get-DistroNexusPackage -Family` filters list results; optional new `-Query` and `-Id` select search and lookup and cannot be combined. `Update-DistroNexusCatalog -SourceUrl` is a validated one-call override and does not write source state. `Remove-DistroNexusPackage -DefaultName` and `-LocalPath` are compatibility selectors only: the fixed delete route resolves a contained current entry, creates and immediately verifies the same opaque cache-entry authority used by `CacheEntryId`, and rejects ambiguity, missing entries, or invalid containment before filesystem mutation. They are never treated as file authority. `-Force` is not retained because standard `ShouldProcess` confirmation is authoritative. A rejected legacy path returns a typed containment error before a bridge filesystem operation.

Desktop exposes only named typed `IPowerShellModuleClient` methods with request records for query, identifier, and explicit cache actions. It does not expose cmdlet names, scripts, raw bridge operations, or raw JSON.

Input validation occurs first in the module, then repeats in Core for security-critical identifiers, URLs, and paths. Public mutation consent happens before bridge invocation. Service errors distinguish invalid input, source unavailable, malformed catalog, no known-good cache, cache containment rejection, and cancellation without revealing secrets or arbitrary local paths.

## Data and Execution Semantics

Catalog cache writes serialize a complete validated package set to a temporary file in the target directory, flush/close it, and replace the prior cache atomically. Failed download, parse, validation, or cancellation removes only the temporary file. In-memory cache replacement occurs only after the durable replacement succeeds.

Cold read precedence is deterministic: first a completed in-memory snapshot, then a durable validated cache, then the bundled `config/catalog.json` fallback, then a typed `Catalog.NotAvailable` empty result. Read operations never fetch the network. Package results include only the modeled package fields already returned by `Get-DistroNexusPackage`; refresh returns `Succeeded`, nullable `SourceId`, `CacheState` (`Updated`, `Preserved`, or `Unavailable`), and a sanitized diagnostic code. Success has `Updated` and a non-null source; a failed refresh with prior cache has `Preserved`; a failed first refresh has `Unavailable` and null source. Queries are limited to 256 characters, source URLs to 2,048 characters, source fetches to 10 seconds and 10 MiB, and parsed package collections to 10,000 records.

Package cache root resolution uses the modeled settings path or the existing product default. Every candidate file is resolved and verified as a child of the cache root before deletion. A cache-entry identifier is not a path authority: Core rejects malformed, stale, forged, absolute, or traversal-bearing identifiers before filesystem mutation. Clear enumerates bounded files beneath that root, checks cancellation between files, reports successes/failures deterministically, and updates package cache-state projections only for successfully affected files.

Concurrent refreshes serialize cache replacement for one process. Read operations may use the last completed in-memory snapshot while a refresh is in progress. A cancelled or failed refresh does not invalidate a previously completed snapshot.

## Security and Operations

Catalog refresh validates every persisted source, legacy `CatalogUrl`, and one-call override before any HTTP: absolute `http`/`https` URI, nonempty host, no userinfo, length at most 2,048, and no fragment. Fetches use a 10-second timeout, 10 MiB response cap, and redirects disabled. Invalid sources are reported as sanitized validation failures and skipped without HTTP while later priority sources remain eligible. Parsed package identifiers and downloadable URLs are treated as untrusted data and are validated before cache status projection. No public error exposes credentials, authorization headers, or unredacted filesystem paths.

Repository verification cannot prove remote-source behavior, proxy/TLS policy, or real user cache recovery. These remain explicit release/UAT evidence, not a substitute for deterministic native tests.

## Verification Strategy

- Native unit tests cover source resolution, validation, priority fallback, atomic replacement, offline fallback, cache containment, partial delete, and cancellation.
- Bridge tests cover every fixed route, payload validation, typed response, cancellation, and unknown-route rejection.
- Pester covers exported command mapping, invalid input, `WhatIf`, declined confirmation, and command failure behavior.
- Typed module-client and WPF routing tests cover list/search/get/refresh/cache flows; a structural test rejects Desktop `ICatalogService` use after consumer migration.
- A separate download-task design and test plan must pass the design gate before `IDownloadTaskManager` consumer migration begins.

## Open Items

| Item | Blocking level | Owner | Resolution |
| --- | --- | --- | --- |
| Download task persistence, progress, retry, and restart semantics | Blocker for FR-CAT-007 only | Repository maintainers | Produce and approve a dedicated task-contract design before a download migration slice. |
| Remote source/UAT evidence | Follow-up | Release/UAT owner | Run disposable-host source fallback and offline recovery scenarios. |
