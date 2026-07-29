# Decision Record: Package Cache Token Compatibility

## Metadata

- Project and repository: DistroNexus; `D:/repo/lazyworkshop-create/DistroNexus`
- Date: 20260728
- Status: Accepted
- Owner: DistroNexus maintainers
- Related requirements/design/plan: `docs/specs/powershell-first-catalog-requirements.md` FR-108 and FR-109; `docs/specs/powershell-first-catalog-design.md`; `docs/development/powershell-first-slice-plan.md` S17

## Context

FR-109 requires an authenticated opaque `CacheEntryId` as the cache-file authority. The existing public `Remove-DistroNexusPackage` command exposes legacy `DefaultName` and `LocalPath` selectors, and the prior design described deletion by package identifier. Treating either legacy selector as direct filesystem authority would violate FR-109, while rejecting them outright would break the stated compatibility requirement in FR-108.

## Decision

`CacheEntryId` is the only normal caller-provided authority for package-cache deletion. `Remove-DistroNexusPackage` retains `DefaultName` and `LocalPath` only as compatibility selectors. The fixed native delete route resolves each selector to exactly one current, contained cache entry, creates and verifies the same opaque token authority, then deletes only through that verification path. A selector never becomes a path or filename authority. Ambiguous, missing, outside-root, reparse-point, stale, forged, or expired selections fail before filesystem mutation with a sanitized error.

The package-cache public command family is `Get-DistroNexusPackageCacheLocation`, `Get-DistroNexusPackageCacheUsage`, `Remove-DistroNexusPackage`, and `Clear-DistroNexusPackageCache`. The legacy instance diagnostic `Get-DistroNexusCache` remains unrelated and is not overloaded.

## Rationale

- Preserves the public compatibility required by FR-108 without weakening FR-109's file-authorization boundary.
- Keeps WPF and automation on one fixed typed module and bridge path.
- Avoids exposing raw cache paths or allowing generic filesystem deletion through the module.

## Consequences

- Positive: cache deletion remains valid across ordinary module and bridge process restarts while every actual delete is bound to current protected token verification.
- Trade-off: compatibility removal may reject stale or ambiguous legacy selectors instead of guessing a file.
- Operational impact: tests must cover both token deletion and compatibility selector resolution, including failure without filesystem mutation.

## Alternatives Considered

1. Remove legacy selectors immediately. Rejected because FR-108 requires public compatibility.
2. Treat a validated legacy path as deletion authority. Rejected because it contradicts FR-109 and weakens containment guarantees.

## Follow-Up Actions

- Implement S17 fixed cache routes and typed module methods with this boundary.
- Add bridge, module, Core, and Desktop routing tests for token and compatibility selector behavior.

## Evidence References

- `docs/specs/powershell-first-catalog-requirements.md`
- `docs/specs/powershell-first-catalog-design.md`
- `src/Client/DistroNexus.Core/Services/CatalogService.cs`
- `src/PowerShell/Public/Remove-DistroNexusPackage.ps1`
