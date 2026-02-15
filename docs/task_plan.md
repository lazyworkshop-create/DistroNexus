# Website Update Plan for v2.0.1

Last Updated: 2026-02-14
Owner: Copilot (GPT-5.3-Codex)

## Objective
Align the public documentation website with the v2.0.1 product baseline (.NET 10 + WPF rewrite, PowerShell module platform, template system, and updated distribution artifacts).

## Scope
- Docusaurus website content under `website/`
- Website requirement specification under `docs/specs/`
- Planning and analysis artifacts under `docs/`

## Phases

### Phase 1 — Repository Scan and Gap Analysis
Status: Completed
- Gather authoritative v2.0.1 release facts.
- Audit current website pages, blog, and site configuration.
- Identify mismatches, stale v1 references, and missing v2 pages.

### Phase 2 — Requirements Definition
Status: Completed
- Define mandatory updates for homepage, docs, blog, and links.
- Define bilingual (English + zh-Hans) synchronization requirements.
- Define acceptance criteria and out-of-scope constraints.

### Phase 3 — Handoff
Status: Completed
- Deliver requirement document path and summary.
- Propose implementation sequencing for next execution task.

### Phase 4 — Website v2.0.1 Implementation
Status: Completed
- Updated in-scope website files for FR-1..FR-9.
- Added EN + zh-Hans v2.0.1 release blog posts.
- Updated repository link consistency to canonical owner.

### Phase 5 — Validation and Acceptance
Status: Completed
- Executed regression keyword checks (Fyne/legacy scripts/stale repo URL).
- Executed `npm run build` in `website/` successfully for `en` and `zh-Hans`.
- Completed acceptance checklist sign-off with evidence.

## Deliverables
1. `docs/findings.md`
2. `docs/progress.md`
3. `docs/specs/website-v2-0-1-update-requirements.md`
4. `docs/development/website-v2-0-1-implementation-checklist.md`
5. `docs/development/website-v2-0-1-acceptance-checklist.md`

## Risks
- Existing website content still reflects v1 architecture and packaging.
- Potential inconsistency between English and Chinese pages.
- Repository links in website config may target incorrect owner namespace.

---

# Template System Comprehensive Documentation Plan

Last Updated: 2026-02-15
Owner: Copilot (GPT-5.3-Codex)

## Objective
Produce a complete template-system document covering requirement analysis, implementation architecture, built-in templates, and developer guidance.

## Phases

### Phase 1 — Requirements and Source Review
Status: Completed
- Reviewed template-related specs and implementation task lists.
- Identified current authoritative implementation files and command surfaces.

### Phase 2 — Implementation Mapping
Status: Completed
- Mapped template metadata model, core service workflow, desktop wizard integration, and PowerShell automation entry points.

### Phase 3 — Documentation Authoring
Status: Completed
- Authored comprehensive guide at `docs/development/template-system-comprehensive-guide.md`.

### Phase 4 — Tracking and Handoff
Status: Completed
- Updated `docs/findings.md` and `docs/progress.md` with task outcomes.

## Deliverables
1. `docs/development/template-system-comprehensive-guide.md`
2. `docs/findings.md` (updated)
3. `docs/progress.md` (updated)

### Phase 5 — Checklist Execution and Acceptance Closure
Status: Completed
- Executed checklist-driven verification for template system documentation.
- Completed implementation checklist with evidence-backed checkmarks.
- Completed acceptance checklist sign-off with `Pass` result and attached artifact paths.

## Additional Deliverables (2026-02-15)
4. `docs/development/template-system-implementation-checklist.md`
5. `docs/development/template-system-acceptance-checklist.md`

---

# Detailed Template Documentation Expansion Plan

Last Updated: 2026-02-15
Owner: Copilot (GPT-5.3-Codex)

## Objective
Produce detailed, dedicated documents derived from the comprehensive template guide:

1. Requirements analysis
2. System design (current implementation)
3. User manual
4. Template development manual
5. Test suite overview and usage manual

## Phases

### Phase 1 — Baseline and Naming Validation
Status: Completed
- Verified target document names and avoided filename collisions.

### Phase 2 — Authoring
Status: Completed
- Authored all five detailed documents in `docs/specs`, `docs/architecture`, and `docs/development`.

### Phase 3 — Traceability Update
Status: Completed
- Updated findings and progress logs with deliverable summary.

## Deliverables
1. `docs/specs/template-system-requirements-analysis.md`
2. `docs/architecture/template-system-design.md`
3. `docs/development/template-system-user-manual.md`
4. `docs/development/template-development-manual.md`
5. `docs/development/template-automation-test-suite-manual.md`

---

# Website Template System Module and Blog Plan

Last Updated: 2026-02-15
Owner: Copilot (GPT-5.3-Codex)

## Objective
Add a dedicated website presence for the template system by:
1. Publishing a standalone template-system blog post.
2. Adding a standalone docs module that aggregates all complete template documents.

## Phases

### Phase 1 — Website Structure and Content Mapping
Status: Completed
- Reviewed current blog/docs/i18n/sidebar structure under `website/`.
- Confirmed target insertion points for blog post and module navigation.

### Phase 2 — Authoring and Navigation Integration
Status: Completed
- Added EN + zh-Hans template-system blog posts.
- Added EN + zh-Hans `template-system` docs module pages.
- Added `template-system` entry in website sidebar.

### Phase 3 — Traceability and Validation
Status: Completed
- Update findings/progress logs.
- Ran website build verification successfully (`npm run build` in `website/`).

## Deliverables
1. `website/blog/2026-02-15-template-system.md`
2. `website/i18n/zh-Hans/docusaurus-plugin-content-blog/2026-02-15-template-system.md`
3. `website/docs/template-system.md`
4. `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/template-system.md`
5. `website/sidebars.js` (navigation update)

---

# Website Template System Main Navigation and Dedicated Sidebar Plan

Last Updated: 2026-02-15
Owner: Copilot (GPT-5.3-Codex)

## Objective
Place Template System in the website main navbar and serve template documentation from a dedicated left sidebar.

## Phases

### Phase 1 — Navigation Architecture Update
Status: Completed
- Added a dedicated docs plugin instance for template system (`routeBasePath: template-system`).
- Added a main navbar entry for Template System.

### Phase 2 — Dedicated Sidebar and Content Migration
Status: Completed
- Added dedicated sidebar config (`sidebars-template-system.js`).
- Migrated template docs entry page to dedicated plugin path (`template-docs/intro.md`) and zh-Hans localization mirror.
- Removed old template page from default docs set.

### Phase 3 — Link Alignment and Validation
Status: Completed
- Updated template blog links to new route `/template-system`.
- Built website successfully for `en` and `zh-Hans`.

## Deliverables
1. `website/docusaurus.config.js`
2. `website/sidebars.js`
3. `website/sidebars-template-system.js`
4. `website/template-docs/intro.md`
5. `website/i18n/zh-Hans/docusaurus-plugin-content-docs-template-system/current/intro.md`

---

# Packaging Script Local Validation Plan

Last Updated: 2026-02-15
Owner: Copilot (GPT-5.3-Codex)

## Objective
Validate packaging scripts locally, fix any blocking defects, and confirm successful output generation for portable distributions.

## Phases

### Phase 1 — Script Audit
Status: Completed
- Reviewed `tools/build_v2.ps1`, `tools/build.ps1`, `tools/package-portable.ps1`, and `tools/build-installer.ps1`.
- Verified packaging dependency chain and parameter flow.

### Phase 2 — Local Execution and Defect Isolation
Status: Completed
- Executed `tools/build_v2.ps1 -Configuration Release -Clean -Publish -CreateZip -Version 2.0.1`.
- Isolated failure at ZIP completion log due to `Write-Host` format operator and `-ForegroundColor` parameter binding conflict.

### Phase 3 — Fix and Regression Validation
Status: Completed
- Applied the same ZIP message formatting fix to `tools/build_v2.ps1` and `tools/build.ps1`.
- Re-ran `build_v2.ps1` successfully end-to-end.
- Re-ran `package-portable.ps1` successfully for framework-dependent and self-contained outputs.
- Executed `build-installer.ps1`; confirmed script-level guard works and fails early only because `ISCC.exe` is not installed locally.

## Deliverables
1. `tools/build_v2.ps1` (fixed)
2. `tools/build.ps1` (fixed)
3. Local artifacts under `release/`:
	- `DistroNexus-v2.0.1-Release.zip`
	- `DistroNexus-v2.0.1-Release-selfcontained.zip`
