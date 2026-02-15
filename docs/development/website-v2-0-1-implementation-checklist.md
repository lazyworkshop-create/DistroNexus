# Website v2.0.1 Implementation Checklist

Date: 2026-02-15  
Source Requirements: `docs/specs/website-v2-0-1-update-requirements.md` (v1.3)

## 0) Preparation
- [x] Confirm source-of-truth inputs are available and unchanged:
  - [x] `docs/release_notes/v2.0.1.md`
  - [x] `README.md`
  - [x] `README_CN.md`
  - [x] `src/PowerShell/DistroNexus.psd1`
- [x] Confirm scope boundaries with team (no visual redesign, no app/module code changes).

## 1) Site Configuration Alignment (FR-8)
Files:
- `website/docusaurus.config.js`

Tasks:
- [x] Replace all repository links from `https://github.com/DistroNexus/DistroNexus` to `https://github.com/lazyworkshop-create/DistroNexus`.
- [x] Update docs `editUrl` to canonical repository.
- [x] Update blog `editUrl` to canonical repository.
- [x] Update navbar GitHub URL to canonical repository.
- [x] Update footer GitHub URL to canonical repository.
- [x] Set `organizationName` to `lazyworkshop-create`.
- [x] Validate `projectName` remains `DistroNexus`.

Definition of Done:
- [x] No stale repository URL remains in `website/docusaurus.config.js`.

## 2) Homepage Messaging Alignment (FR-1)
Files:
- `website/src/pages/index.js`
- `website/i18n/zh-Hans/code.json`

Tasks:
- [x] Replace v1/Fyne wording with v2 native WPF/.NET 10 wording in homepage feature text.
- [x] Keep existing layout/component structure unchanged.
- [x] Update corresponding zh-Hans translation keys for homepage strings:
  - [x] `homepage.tagline`
  - [x] `feature.gui.title`
  - [x] `feature.gui.description`
  - [x] `feature.install.title`
  - [x] `feature.install.description`
  - [x] `feature.offline.title`
  - [x] `feature.offline.description`

Definition of Done:
- [x] EN + zh-Hans homepage copy reflects v2.0.1 architecture and capability messaging.

## 3) English Documentation Refresh (FR-2/3/4/5)
Files:
- `website/docs/intro.md`
- `website/docs/installation.md`
- `website/docs/usage.md`
- `website/docs/configuration.md`
- `website/docs/scripts-reference.md`

Tasks:
- [x] `intro.md`: remove Fyne/cross-platform architecture positioning; describe native WPF/.NET 10 baseline.
- [x] `installation.md`: update release links and asset names:
  - [x] `DistroNexus-2.0.1-Setup.exe`
  - [x] `DistroNexus-v2.0.1-Release.zip`
  - [x] `DistroNexus-v2.0.1-Release-selfcontained.zip`
  - [x] Add/confirm prerequisites: .NET 10 Desktop Runtime + WSL2.
- [x] `usage.md`: keep lifecycle workflows, add concise template-assisted bootstrap mention, remove obsolete v1 workflow language.
- [x] `configuration.md`: align to `%APPDATA%\DistroNexus\settings.json`; remove deprecated user-facing reliance on `config/distros.json`.
- [x] `scripts-reference.md`: migrate to cmdlet-first reference covering 15 exported commands from `src/PowerShell/DistroNexus.psd1`.
- [x] Add concise command examples for representative categories (instance/package/template/catalog).

Definition of Done:
- [x] English docs are v2.0.1-consistent and no longer script-file-centric.

## 4) zh-Hans Documentation Parity (FR-7)
Files:
- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/intro.md`
- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/installation.md`
- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/usage.md`
- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/configuration.md`
- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/scripts-reference.md`

Tasks:
- [x] Mirror English updates for all five docs.
- [x] Keep technical identifiers (cmdlets, paths, filenames) unchanged.
- [x] Ensure content parity at feature level (allow wording differences only).

Definition of Done:
- [x] zh-Hans docs cover the same v2 scope as English docs.

## 5) v2.0.1 Release Blog Addition (FR-6)
Files to create:
- `website/blog/2026-01-31-v2.0.1.md`
- `website/i18n/zh-Hans/docusaurus-plugin-content-blog/2026-01-31-v2.0.1.md`

Tasks:
- [x] Add new English v2.0.1 blog post with release summary:
  - [x] major rewrite (.NET 10 + WPF)
  - [x] module platform (15 cmdlets)
  - [x] template system
  - [x] packaging options
  - [x] migration notes
- [x] Add corresponding zh-Hans post with aligned semantics.
- [x] Ensure front matter alignment for EN/zh-Hans:
  - [x] `slug`
  - [x] `title`
  - [x] `tags`
  - [x] `date: 2026-01-31`

Definition of Done:
- [x] Blog index contains new v2.0.1 post in EN and zh-Hans.

## 6) Regression Checks and Validation (FR-9, AC)
Tasks:
- [x] Run targeted keyword checks in active v2 content scope (homepage, docs, zh-Hans current docs, new v2.0.1 blog, site config; excluding historical v1 blog files as allowed by spec):
  - [x] `Fyne`
  - [x] legacy script filenames in current docs
  - [x] stale repo URL `DistroNexus/DistroNexus`
- [x] Verify navigation integrity explicitly (FR-9):
  - [x] Existing sidebar entries resolve to valid pages.
  - [x] New v2.0.1 blog post appears in blog index for EN and zh-Hans.
  - [x] No broken markdown links in updated pages.
- [x] Run site build validation in `website/`:
  - [x] `npm install` (if needed)
  - [x] `npm run build`
- [x] Resolve broken links or markdown issues from build output.

Definition of Done:
- [x] Build passes with no broken links.

## 7) Delivery and Traceability
Tasks:
- [x] Summarize changed files grouped by FR mapping.
- [x] Record any deviations from requirements (if none, explicitly state none).
- [x] Attach final acceptance checklist status.

Definition of Done:
- [x] Implementation handoff is auditable against FR-1..FR-9 and AC-1..AC-9.

## Implementation Result
- [x] Completed with no functional deviations from requirements.
- [x] Acceptance checklist status: Pass.
