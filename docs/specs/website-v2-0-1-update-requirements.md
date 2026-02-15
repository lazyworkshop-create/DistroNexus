# Website Update Requirements - v2.0.1 Baseline

Document Version: 1.3  
Date: 2026-02-14  
Status: Draft (Ready for Implementation)

## 1. Background
The current Docusaurus website still reflects v1-era messaging (Fyne UI, script-centric operations, old release assets), while the product baseline is now v2.0.1 (.NET 10 + WPF rewrite, PowerShell module platform, template system, and updated release packaging).

This document defines the required updates to align website content with the v2.0.1 baseline.

## 2. Objective
Deliver a website content refresh that is factually consistent with v2.0.1 across homepage, docs, release blog, bilingual pages, and repository links.

## 3. Scope

### In Scope
- `website/src/pages/index.js`
- `website/i18n/zh-Hans/code.json`
- `website/docs/*.md`
- `website/blog/*.md`
- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/*.md`
- `website/i18n/zh-Hans/docusaurus-plugin-content-blog/*.md`
- `website/docusaurus.config.js`

### Out of Scope
- Visual redesign or new UI components/themes.
- New docs IA (information architecture) beyond minimal navigation additions required for v2.0.1.
- Changes to application code or PowerShell module implementation.
- New product features not already part of v2.0.1.
- Rewriting historical v1 release posts (`v1.0.1`, `v1.0.2`) except for adding a new v2.0.1 post.

## 4. Source of Truth
1. `docs/release_notes/v2.0.1.md`
2. `README.md`
3. `README_CN.md`
4. `src/PowerShell/DistroNexus.psd1` (authoritative exported cmdlet list)

If conflicts appear, release notes are authoritative for release statements, module manifest is authoritative for exported command surface, and README is secondary for usage examples.

## 5. Functional Requirements

### FR-1 Homepage Messaging Alignment
Update homepage content to remove v1/Fyne positioning and reflect v2.0.1:
- Replace Fyne/cross-platform wording with native Windows WPF/.NET 10 messaging.
- Highlight v2 capabilities: module cmdlets, template system, diagnostics/progress visibility.
- Update corresponding zh-Hans translation entries in `website/i18n/zh-Hans/code.json` for homepage feature text.
- Keep existing page structure unless a wording change requires a minor field update.

### FR-2 Installation Page Alignment
Update installation instructions to v2.0.1 release artifacts:
- Link to correct releases repository owner.
- Reflect current asset naming:
  - `DistroNexus-2.0.1-Setup.exe`
  - `DistroNexus-v2.0.1-Release.zip`
  - `DistroNexus-v2.0.1-Release-selfcontained.zip`
- Clarify runtime prerequisites (.NET 10 Desktop Runtime, WSL2).
- Remove obsolete references to v1 executable/package assumptions.

### FR-3 Intro and Usage Alignment
Refresh `intro.md` and `usage.md` to represent v2 operating model:
- Describe WPF desktop architecture at a user-facing level.
- Preserve core user workflows (install/manage/move/rename/remove instances).
- Add concise mention of template-assisted bootstrap workflows.
- Remove stale wording that implies old app stack or obsolete tabs/workflows.

### FR-4 Configuration Contract Alignment
Update `configuration.md`:
- Replace stale configuration paths/keys with v2 contract.
- Document settings location as `%APPDATA%\DistroNexus\settings.json`.
- Ensure examples align with keys used in v2 baseline docs.
- Avoid referencing deprecated `config/distros.json` as primary user configuration when not applicable.

### FR-5 Scripts Reference Migration to Cmdlet Reference
Update `scripts-reference.md` from script-first to module-first model:
- Replace legacy script catalog with 15 exported cmdlets.
- Group cmdlets logically (instance, package, catalog, template automation).
- Provide concise usage examples for representative commands.
- Keep wording user-oriented; avoid implementation-only internals.

### FR-6 Release Blog Coverage
Add v2.0.1 release post to website blog:
- New English post under `website/blog/` with release metadata and summary.
- New zh-Hans translated post under `website/i18n/zh-Hans/docusaurus-plugin-content-blog/`.
- Content should summarize major rewrite, module platform, templates, packaging options, and migration notes.

Use consistent blog metadata and naming:
- Filename/date should align with release date (`2026-01-31`) for both locales.
- Front matter must include aligned `slug`, `title`, `tags`, and `date` semantics across EN and zh-Hans.

### FR-7 Bilingual Consistency
Ensure all updated English docs have corresponding zh-Hans updates for parity:
- intro
- installation
- usage
- configuration
- scripts reference
- v2.0.1 release post

Allow minor language-level phrasing differences, but feature coverage must match.

### FR-8 Repository and Edit Links Consistency
Update `website/docusaurus.config.js` links to canonical repository owner (`lazyworkshop-create/DistroNexus`) for:
- Navbar GitHub link
- Docs/blog `editUrl`
- Any additional repository references in site config

Also align GitHub Pages ownership metadata to the same owner namespace where applicable:
- `organizationName` must be `lazyworkshop-create`
- `projectName` (validate existing value remains correct)

### FR-9 Navigation Integrity
Verify docs navigation remains valid after content updates:
- Existing sidebar entries continue to resolve.
- Newly added v2.0.1 blog post appears in blog index.
- No broken markdown links in updated pages.

## 6. Non-Functional Requirements

### NFR-1 Accuracy
All version numbers and architecture claims must match v2.0.1 release notes; exported command surface must match `src/PowerShell/DistroNexus.psd1`.

### NFR-2 Minimal Change Strategy
Use targeted content updates without introducing unnecessary new pages or structural complexity.

### NFR-3 Style and Tone
Maintain current docs style and readability:
- English content in English.
- Chinese localization in natural simplified Chinese.
- Keep command names and technical identifiers unchanged.

## 7. Deliverables
1. Updated website files in scope.
2. New v2.0.1 blog post in English and zh-Hans.
3. Link consistency fixes in Docusaurus config.
4. Successful site validation (`npm run build` in `website/`).

## 8. Acceptance Criteria
- AC-1: No references to Fyne as current architecture remain in homepage/docs.
- AC-2: Installation page references v2.0.1 assets and correct repository owner links.
- AC-3: Cmdlet-based documentation covers all 15 exported commands.
- AC-4: English and zh-Hans docs are updated for the same six artifacts listed in FR-7.
- AC-5: Blog includes v2.0.1 post in both languages.
- AC-6: Docusaurus build passes with no broken links.
- AC-7: Homepage localized feature text (zh-Hans) no longer contains v1/Fyne wording.
- AC-8: `docusaurus.config.js` owner metadata and repository URLs are consistent with canonical repository namespace.
- AC-9: New EN/zh-Hans v2.0.1 blog posts use aligned front matter and release date (`2026-01-31`).

## 9. Implementation Sequence (Recommended)
1. Update `docusaurus.config.js` links.
2. Update homepage messaging (`index.js`) and zh-Hans translation resources (`i18n/zh-Hans/code.json`).
3. Update core docs (`intro`, `installation`, `usage`, `configuration`, `scripts-reference`) in English.
4. Mirror updates to zh-Hans docs.
5. Add v2.0.1 blog posts (EN + zh-Hans).
6. Run website build verification and fix link/content regressions.

## 10. Risks and Mitigations
- Risk: English/Chinese pages diverge over time.  
  Mitigation: Treat FR-7 parity as release-gate criterion.

- Risk: Legacy terms reintroduced by partial edits.  
  Mitigation: Perform keyword regression checks for `Fyne` and legacy script filenames in homepage/docs/current release pages; exclude historical v1 blog posts from failure criteria.

- Risk: Build break due to link changes.  
  Mitigation: Run `npm run build` and resolve all broken links before publish.

## 11. Open Questions
1. Should website explicitly mark v2.0.1 as “current stable” in docs front matter or homepage badge?
2. Should migration guidance from v1 be a standalone doc page or remain in release blog only?
3. Should cmdlet reference include full parameter tables now, or start with concise command overview and expand later?
