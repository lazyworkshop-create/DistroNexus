# Website v2.0.1 Implementation Checklist

Date: 2026-02-14  
Source Requirements: `docs/specs/website-v2-0-1-update-requirements.md` (v1.3)

## 0) Preparation
- [ ] Confirm source-of-truth inputs are available and unchanged:
  - [ ] `docs/release_notes/v2.0.1.md`
  - [ ] `README.md`
  - [ ] `README_CN.md`
  - [ ] `src/PowerShell/DistroNexus.psd1`
- [ ] Confirm scope boundaries with team (no visual redesign, no app/module code changes).

## 1) Site Configuration Alignment (FR-8)
Files:
- `website/docusaurus.config.js`

Tasks:
- [ ] Replace all repository links from `https://github.com/DistroNexus/DistroNexus` to `https://github.com/lazyworkshop-create/DistroNexus`.
- [ ] Update docs `editUrl` to canonical repository.
- [ ] Update blog `editUrl` to canonical repository.
- [ ] Update navbar GitHub URL to canonical repository.
- [ ] Update footer GitHub URL to canonical repository.
- [ ] Set `organizationName` to `lazyworkshop-create`.
- [ ] Validate `projectName` remains `DistroNexus`.

Definition of Done:
- [ ] No stale repository URL remains in `website/docusaurus.config.js`.

## 2) Homepage Messaging Alignment (FR-1)
Files:
- `website/src/pages/index.js`
- `website/i18n/zh-Hans/code.json`

Tasks:
- [ ] Replace v1/Fyne wording with v2 native WPF/.NET 10 wording in homepage feature text.
- [ ] Keep existing layout/component structure unchanged.
- [ ] Update corresponding zh-Hans translation keys for homepage strings:
  - [ ] `homepage.tagline`
  - [ ] `feature.gui.title`
  - [ ] `feature.gui.description`
  - [ ] `feature.install.title`
  - [ ] `feature.install.description`
  - [ ] `feature.offline.title`
  - [ ] `feature.offline.description`

Definition of Done:
- [ ] EN + zh-Hans homepage copy reflects v2.0.1 architecture and capability messaging.

## 3) English Documentation Refresh (FR-2/3/4/5)
Files:
- `website/docs/intro.md`
- `website/docs/installation.md`
- `website/docs/usage.md`
- `website/docs/configuration.md`
- `website/docs/scripts-reference.md`

Tasks:
- [ ] `intro.md`: remove Fyne/cross-platform architecture positioning; describe native WPF/.NET 10 baseline.
- [ ] `installation.md`: update release links and asset names:
  - [ ] `DistroNexus-2.0.1-Setup.exe`
  - [ ] `DistroNexus-v2.0.1-Release.zip`
  - [ ] `DistroNexus-v2.0.1-Release-selfcontained.zip`
  - [ ] Add/confirm prerequisites: .NET 10 Desktop Runtime + WSL2.
- [ ] `usage.md`: keep lifecycle workflows, add concise template-assisted bootstrap mention, remove obsolete v1 workflow language.
- [ ] `configuration.md`: align to `%APPDATA%\DistroNexus\settings.json`; remove deprecated user-facing reliance on `config/distros.json`.
- [ ] `scripts-reference.md`: migrate to cmdlet-first reference covering 15 exported commands from `src/PowerShell/DistroNexus.psd1`.
- [ ] Add concise command examples for representative categories (instance/package/template/catalog).

Definition of Done:
- [ ] English docs are v2.0.1-consistent and no longer script-file-centric.

## 4) zh-Hans Documentation Parity (FR-7)
Files:
- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/intro.md`
- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/installation.md`
- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/usage.md`
- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/configuration.md`
- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/scripts-reference.md`

Tasks:
- [ ] Mirror English updates for all five docs.
- [ ] Keep technical identifiers (cmdlets, paths, filenames) unchanged.
- [ ] Ensure content parity at feature level (allow wording differences only).

Definition of Done:
- [ ] zh-Hans docs cover the same v2 scope as English docs.

## 5) v2.0.1 Release Blog Addition (FR-6)
Files to create:
- `website/blog/2026-01-31-v2.0.1.md`
- `website/i18n/zh-Hans/docusaurus-plugin-content-blog/2026-01-31-v2.0.1.md`

Tasks:
- [ ] Add new English v2.0.1 blog post with release summary:
  - [ ] major rewrite (.NET 10 + WPF)
  - [ ] module platform (15 cmdlets)
  - [ ] template system
  - [ ] packaging options
  - [ ] migration notes
- [ ] Add corresponding zh-Hans post with aligned semantics.
- [ ] Ensure front matter alignment for EN/zh-Hans:
  - [ ] `slug`
  - [ ] `title`
  - [ ] `tags`
  - [ ] `date: 2026-01-31`

Definition of Done:
- [ ] Blog index contains new v2.0.1 post in EN and zh-Hans.

## 6) Regression Checks and Validation (FR-9, AC)
Tasks:
- [ ] Run targeted keyword checks in active v2 content scope (homepage, docs, zh-Hans current docs, new v2.0.1 blog, site config; excluding historical v1 blog files as allowed by spec):
  - [ ] `Fyne`
  - [ ] legacy script filenames in current docs
  - [ ] stale repo URL `DistroNexus/DistroNexus`
- [ ] Verify navigation integrity explicitly (FR-9):
  - [ ] Existing sidebar entries resolve to valid pages.
  - [ ] New v2.0.1 blog post appears in blog index for EN and zh-Hans.
  - [ ] No broken markdown links in updated pages.
- [ ] Run site build validation in `website/`:
  - [ ] `npm install` (if needed)
  - [ ] `npm run build`
- [ ] Resolve broken links or markdown issues from build output.

Definition of Done:
- [ ] Build passes with no broken links.

## 7) Delivery and Traceability
Tasks:
- [ ] Summarize changed files grouped by FR mapping.
- [ ] Record any deviations from requirements (if none, explicitly state none).
- [ ] Attach final acceptance checklist status.

Definition of Done:
- [ ] Implementation handoff is auditable against FR-1..FR-9 and AC-1..AC-9.
