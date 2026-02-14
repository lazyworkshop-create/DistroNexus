# Website v2.0.1 Acceptance Checklist

Date: 2026-02-14  
Source Requirements: `docs/specs/website-v2-0-1-update-requirements.md` (v1.3)

## A. Scope Verification
- [ ] Updated files are limited to in-scope paths under `website/` plus approved docs tracking files.
- [ ] No app/runtime/module implementation files are changed.
- [ ] Historical v1 release posts are not rewritten.

## B. Functional Acceptance (AC-1 .. AC-9)

### AC-1: Fyne references removed from current architecture messaging
- [ ] No Fyne references remain in homepage (`website/src/pages/index.js`).
- [ ] No Fyne references remain in current docs pages (`website/docs/*.md` + zh-Hans current docs).

### AC-2: Installation docs aligned to v2.0.1 assets and owner links
- [ ] Installation docs reference `DistroNexus-2.0.1-Setup.exe`.
- [ ] Installation docs reference `DistroNexus-v2.0.1-Release.zip`.
- [ ] Installation docs reference `DistroNexus-v2.0.1-Release-selfcontained.zip`.
- [ ] Installation docs use canonical repository owner links.

### AC-3: Cmdlet docs cover exported command surface
- [ ] `website/docs/scripts-reference.md` uses cmdlet-first model.
- [ ] zh-Hans scripts reference is updated equivalently.
- [ ] All 15 exported cmdlets from `src/PowerShell/DistroNexus.psd1` are documented.
- [ ] Per-cmdlet verification completed for:
	- [ ] `Get-DistroNexusInstance`
	- [ ] `Start-DistroNexusInstance`
	- [ ] `Stop-DistroNexusInstance`
	- [ ] `Move-DistroNexusInstance`
	- [ ] `Rename-DistroNexusInstance`
	- [ ] `Remove-DistroNexusInstance`
	- [ ] `Install-DistroNexusInstance`
	- [ ] `Set-DistroNexusCredential`
	- [ ] `Get-DistroNexusPackage`
	- [ ] `Save-DistroNexusPackage`
	- [ ] `Remove-DistroNexusPackage`
	- [ ] `Update-DistroNexusCatalog`
	- [ ] `Get-DistroNexusTemplate`
	- [ ] `Apply-DistroNexusTemplate`
	- [ ] `Invoke-DistroNexusTemplateAutomation`

### AC-4: EN/zh-Hans parity for required artifacts
- [ ] `intro` parity verified.
- [ ] `installation` parity verified.
- [ ] `usage` parity verified.
- [ ] `configuration` parity verified.
- [ ] `scripts-reference` parity verified.
- [ ] `v2.0.1 release post` parity verified.

### AC-5: v2.0.1 blog exists in both locales
- [ ] EN post exists: `website/blog/2026-01-31-v2.0.1.md`.
- [ ] zh-Hans post exists: `website/i18n/zh-Hans/docusaurus-plugin-content-blog/2026-01-31-v2.0.1.md`.

### AC-6: Build integrity
- [ ] `npm run build` succeeds in `website/`.
- [ ] No broken links reported.

### FR-9: Navigation integrity checks
- [ ] Existing sidebar entries resolve and open correctly.
- [ ] New v2.0.1 post is visible in blog index (EN + zh-Hans).
- [ ] Updated pages contain no broken markdown links.

### AC-7: zh-Hans homepage localized copy updated
- [ ] `website/i18n/zh-Hans/code.json` homepage feature text contains v2 wording.
- [ ] No Fyne wording remains in homepage localized keys.

### AC-8: Site config consistency
- [ ] `organizationName` is `lazyworkshop-create`.
- [ ] `projectName` remains valid (`DistroNexus`).
- [ ] Navbar/footer GitHub links use canonical repository URL.
- [ ] docs/blog `editUrl` use canonical repository URL.

### AC-9: v2.0.1 blog front matter alignment
- [ ] EN/zh-Hans posts share aligned `slug` semantics.
- [ ] EN/zh-Hans posts share aligned `title` semantics.
- [ ] EN/zh-Hans posts share aligned `tags` semantics.
- [ ] EN/zh-Hans posts both use `date: 2026-01-31`.

## C. Non-Functional Acceptance
- [ ] Architecture and release claims match `docs/release_notes/v2.0.1.md`.
- [ ] Cmdlet surface matches `src/PowerShell/DistroNexus.psd1`.
- [ ] Changes are minimal and do not introduce unnecessary IA or visual redesign.
- [ ] English docs are English; zh-Hans docs are natural Simplified Chinese.

## D. Evidence Checklist
- [ ] Attach grep/search outputs for stale keyword regression checks.
- [ ] Attach build output (`npm run build`).
- [ ] Attach final changed file list.
- [ ] Confirm all checked items are true before release.

## E. Sign-off
- Implementation Owner: ____________________
- Reviewer: ____________________
- Date: ____________________
- Result: [ ] Pass  [ ] Pass with Exceptions  [ ] Fail
- Notes: __________________________________
