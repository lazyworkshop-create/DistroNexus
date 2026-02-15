# v2.0 Release Readiness Remediation Requirements

## Document Metadata
- **Document Type**: Requirements Specification
- **Version**: 1.0
- **Date**: 2026-02-14
- **Target Release**: v2.0.x stabilization
- **Owner**: DistroNexus Team

## 1. Background
A full release-readiness inspection identified no blocking build/test failures, but found multiple consistency and maintainability gaps across runtime catalog handling, release metadata, and public documentation.

This document defines the remediation requirements needed to close those gaps before final v2.0 closure.

## 2. Goals
- Eliminate high-risk runtime/source configuration inconsistencies.
- Ensure release metadata and versioning are internally consistent.
- Align all user-facing documentation with the real exported PowerShell surface.
- Reduce accidental release risk caused by outdated legacy packaging assets.

## 3. Non-Goals
- Introducing new end-user features.
- Large refactors unrelated to release readiness.
- Rewriting archived historical documents.

## 4. Priority Tiers
- **P0 (Must Fix)**: Can cause runtime failure, wrong source behavior, or portability issues.
- **P1 (Should Fix)**: Causes user confusion, inaccurate release communication, or weak release governance.
- **P2 (Could Fix)**: Cleanup to reduce future accidental misuse.

---

## 5. Requirements

### P0 Requirements (Must Fix)

#### RRM-P0-001: Unify Catalog Source Contract
**Requirement**
- The system shall use a single canonical catalog filename contract (`catalog.json` or `distros.json`) across:
  - default remote URL
  - local fallback lookup
  - PowerShell update/load flow
  - packaging output

**Rationale**
- Current mixed usage creates source drift risk and broken fallback behavior.

**Acceptance Criteria**
- Exactly one canonical filename is defined in runtime and documentation.
- All hardcoded references in runtime code and scripts match that canonical filename.
- Publish output includes the expected catalog file at runtime path.

---

#### RRM-P0-002: Remove Development-Machine Path Hardcoding
**Requirement**
- Production/runtime code shall not contain absolute developer machine paths (e.g., `D:\wsl\...`).

**Rationale**
- Hardcoded local paths break portability and can hide packaging/path defects in development environments.

**Acceptance Criteria**
- No absolute local machine paths remain in non-test runtime code.
- Catalog fallback resolution is environment-agnostic and works from publish output.

---

### P1 Requirements (Should Fix)

#### RRM-P1-001: Align Cmdlet Naming and Count in Public Docs
**Requirement**
- Public docs shall reflect actual exported cmdlets from module manifest.
- Legacy aliases/names (`Get-WslInstance`, etc.) must be clearly marked as aliases only (if applicable) or removed from primary docs.

**Rationale**
- Current docs conflict with current module export names and count.

**Acceptance Criteria**
- README and release notes list matches `FunctionsToExport` contract or explicitly documents alias policy.
- Cmdlet count in text matches actual exported/publicly supported command set.

---

#### RRM-P1-002: Normalize Release Version Surface
**Requirement**
- Version declarations across changelog, module manifest, and build scripts shall be consistent for the intended released patch level.

**Rationale**
- Inconsistent version defaults create release artifact confusion and metadata mismatch.

**Acceptance Criteria**
- Changelog top version, module version, build script default version, and installer script defaults are aligned.
- A documented source of truth for release version exists (script parameter, centralized variable, or process rule).

---

#### RRM-P1-003: Remove Placeholder and Broken Public Links
**Requirement**
- User-facing docs shall not include placeholder repository owner (`yourusername`) or links to missing files.

**Rationale**
- Broken links lower trust and increase support burden.

**Acceptance Criteria**
- No `yourusername` placeholders remain in root README/release-note public docs.
- Any referenced contribution document exists, or the link is removed/repointed.

---

### P2 Requirements (Could Fix)

#### RRM-P2-001: Clarify or Retire Legacy Installer Script
**Requirement**
- Legacy installer definitions under `tools/packaging/` shall be either:
  1. Updated to current file layout and naming, or
  2. Explicitly marked deprecated/non-production.

**Rationale**
- Outdated packaging scripts are a frequent accidental release hazard.

**Acceptance Criteria**
- Legacy script cannot be mistaken as current release path without warning.
- Packaging docs clearly identify the authoritative installer workflow.

---

## 6. Implementation Checklist

### P0 Execution Checklist
- [x] Decide canonical catalog filename and URL contract.
- [x] Update runtime fallback lookup and PowerShell update/load scripts.
- [x] Ensure publish output includes canonical catalog file.
- [x] Remove absolute development path fallback from runtime code.

### P1 Execution Checklist
- [x] Reconcile README cmdlet names/count with module exports.
- [x] Reconcile release notes cmdlet names/count with module exports.
- [x] Align version defaults across manifest/build/installer/changelog.
- [x] Remove placeholder repository links and fix missing doc links.

### P2 Execution Checklist
- [x] Update or deprecate legacy `tools/packaging/DistroNexus.iss`.
- [x] Add explicit packaging workflow guidance in documentation.

---

## 7. Verification Plan

### Automated Verification
- [x] `dotnet test src/Client/DistroNexus.slnx -c Release` passes.
- [x] `tests/PowerShell/TestRunner.ps1` passes.
- [x] `tools/build.ps1 -Configuration Release -Publish` succeeds.

### Consistency Verification
- [x] Repo-wide search confirms no forbidden hardcoded developer path in runtime code.
- [x] Repo-wide search confirms no `yourusername` placeholder in public release docs.
- [x] README/release notes cmdlet lists match supported exported command surface.

### Release Artifact Verification
- [x] Published `release/*/config` contains canonical catalog contract files.
- [x] Installer pipeline uses the current authoritative script and valid input paths.

---

## 8. Exit Criteria
All P0 and P1 requirements are accepted, and verification checks pass. P2 may be deferred only with explicit note in release closure summary.

## 9. Risk if Deferred
- Catalog retrieval failures in specific environments.
- User confusion due to inaccurate cmdlet documentation.
- Version metadata drift across release artifacts.
- Higher probability of accidental use of outdated packaging pipeline.
