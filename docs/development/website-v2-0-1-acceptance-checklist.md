# Website v2.0.1 Acceptance Checklist

Date: 2026-02-14  
Source Requirements: `docs/specs/website-v2-0-1-update-requirements.md` (v1.3)

## A. Scope Verification
- [x] Updated files are limited to in-scope paths under `website/` plus approved docs tracking files.
- [x] No app/runtime/module implementation files are changed.
- [x] Historical v1 release posts are not rewritten.

## B. Functional Acceptance (AC-1 .. AC-9)

### AC-1: Fyne references removed from current architecture messaging
- [x] No Fyne references remain in homepage (`website/src/pages/index.js`).
- [x] No Fyne references remain in current docs pages (`website/docs/*.md` + zh-Hans current docs).

### AC-2: Installation docs aligned to v2.0.1 assets and owner links
- [x] Installation docs reference `DistroNexus-2.0.1-Setup.exe`.
- [x] Installation docs reference `DistroNexus-v2.0.1-Release.zip`.
- [x] Installation docs reference `DistroNexus-v2.0.1-Release-selfcontained.zip`.
- [x] Installation docs use canonical repository owner links.

### AC-3: Cmdlet docs cover exported command surface
- [x] `website/docs/scripts-reference.md` uses cmdlet-first model.
- [x] zh-Hans scripts reference is updated equivalently.
- [x] All 15 exported cmdlets from `src/PowerShell/DistroNexus.psd1` are documented.
- [x] Per-cmdlet verification completed for:
	- [x] `Get-DistroNexusInstance`
	- [x] `Start-DistroNexusInstance`
	- [x] `Stop-DistroNexusInstance`
	- [x] `Move-DistroNexusInstance`
	- [x] `Rename-DistroNexusInstance`
	- [x] `Remove-DistroNexusInstance`
	- [x] `Install-DistroNexusInstance`
	- [x] `Set-DistroNexusCredential`
	- [x] `Get-DistroNexusPackage`
	- [x] `Save-DistroNexusPackage`
	- [x] `Remove-DistroNexusPackage`
	- [x] `Update-DistroNexusCatalog`
	- [x] `Get-DistroNexusTemplate`
	- [x] `Apply-DistroNexusTemplate`
	- [x] `Invoke-DistroNexusTemplateAutomation`

### AC-4: EN/zh-Hans parity for required artifacts
- [x] `intro` parity verified.
- [x] `installation` parity verified.
- [x] `usage` parity verified.
- [x] `configuration` parity verified.
- [x] `scripts-reference` parity verified.
- [x] `v2.0.1 release post` parity verified.

### AC-5: v2.0.1 blog exists in both locales
- [x] EN post exists: `website/blog/2026-01-31-v2.0.1.md`.
- [x] zh-Hans post exists: `website/i18n/zh-Hans/docusaurus-plugin-content-blog/2026-01-31-v2.0.1.md`.

### AC-6: Build integrity
- [x] `npm run build` succeeds in `website/`.
- [x] No broken links reported.

### FR-9: Navigation integrity checks
- [x] Existing sidebar entries resolve and open correctly.
- [x] New v2.0.1 post is visible in blog index (EN + zh-Hans).
- [x] Updated pages contain no broken markdown links.

### AC-7: zh-Hans homepage localized copy updated
- [x] `website/i18n/zh-Hans/code.json` homepage feature text contains v2 wording.
- [x] No Fyne wording remains in homepage localized keys.

### AC-8: Site config consistency
- [x] `organizationName` is `LazyWorkshopCreate`.
- [x] `projectName` remains valid (`DistroNexus`).
- [x] Navbar/footer GitHub links use canonical repository URL.
- [x] docs/blog `editUrl` use canonical repository URL.

### AC-9: v2.0.1 blog front matter alignment
- [x] EN/zh-Hans posts share aligned `slug` semantics.
- [x] EN/zh-Hans posts share aligned `title` semantics.
- [x] EN/zh-Hans posts share aligned `tags` semantics.
- [x] EN/zh-Hans posts both use `date: 2026-01-31`.

## C. Non-Functional Acceptance
- [x] Architecture and release claims match `docs/release_notes/v2.0.1.md`.
- [x] Cmdlet surface matches `src/PowerShell/DistroNexus.psd1`.
- [x] Changes are minimal and do not introduce unnecessary IA or visual redesign.
- [x] English docs are English; zh-Hans docs are natural Simplified Chinese.

## D. Evidence Checklist
- [x] Attach grep/search outputs for stale keyword regression checks.
- [x] Attach build output (`npm run build`).
- [x] Attach final changed file list.
- [x] Confirm all checked items are true before release.

### Evidence Notes
- Regression checks: `Fyne` => no matches in homepage/current docs/zh-Hans current docs/homepage localized keys; legacy script filenames => no matches in current docs scope; stale URL `https://github.com/DistroNexus/DistroNexus` => no matches in `website/docusaurus.config.js`.
- Cmdlet coverage checks: all 15 exported cmdlets from `src/PowerShell/DistroNexus.psd1` matched in both EN and zh-Hans `scripts-reference.md`.
- Build output: `npm run build` in `website/` passed for locales `en` and `zh-Hans`; static files generated with no broken-link failure.
- Changed files list (git status):
	- `website/src/pages/index.js`
	- `website/docusaurus.config.js`
	- `website/docs/intro.md`
	- `website/docs/installation.md`
	- `website/docs/usage.md`
	- `website/docs/configuration.md`
	- `website/docs/scripts-reference.md`
	- `website/blog/2026-01-31-v2.0.1.md`
	- `website/i18n/zh-Hans/code.json`
	- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/intro.md`
	- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/installation.md`
	- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/usage.md`
	- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/configuration.md`
	- `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/scripts-reference.md`
	- `website/i18n/zh-Hans/docusaurus-plugin-content-blog/2026-01-31-v2.0.1.md`
	- `website/package-lock.json`

## E. Sign-off
- Implementation Owner: Copilot (GPT-5.3-Codex)
- Reviewer: Pending
- Date: 2026-02-15
- Result: [x] Pass  [ ] Pass with Exceptions  [ ] Fail
- Notes: All AC/NFR checks completed and validated by build and regression scan outputs.
