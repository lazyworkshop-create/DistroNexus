# Findings - Website v2.0.1 Update Analysis

Date: 2026-02-14

## Authoritative Product Baseline
From `docs/release_notes/v2.0.1.md`:
- v2.0.1 is a major rewrite to .NET 10 + WPF (MVVM + DI + async-first model).
- PowerShell module platform exposes 15 cmdlets.
- Built-in template system is a first-class feature.
- Release artifacts are installer + portable + self-contained packages.
- Repository canonical links use `lazyworkshop-create/DistroNexus`.

## Website Gaps (Current State)

### 1) Homepage still describes v1/Fyne architecture
- `website/src/pages/index.js` still states cross-platform Fyne interface.

### 2) Docs pages are v1-oriented and outdated
- `website/docs/intro.md`: still claims Fyne-based cross-platform app.
- `website/docs/installation.md`: references v1 zip naming and old executable assumptions.
- `website/docs/usage.md`: workflow still framed around old dashboard semantics.
- `website/docs/configuration.md`: references `config/settings.json` and `config/distros.json`, inconsistent with v2 settings contract in release notes.
- `website/docs/scripts-reference.md`: documents legacy script files rather than v2 module cmdlets.

### 3) Blog has no v2.0.1 release post
- Existing posts:
  - `website/blog/2026-01-23-v1.0.1.md`
  - `website/blog/2026-01-24-v1.0.2.md`
- Missing v2.0.1 release announcement in English and zh-Hans.

### 4) Internationalization is also stale
- zh-Hans docs mirror outdated v1/Fyne content under:
  - `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/*.md`
- zh-Hans blog also only contains v1.0.1/v1.0.2 posts.

### 5) Site configuration link mismatch
- `website/docusaurus.config.js` points to `https://github.com/DistroNexus/DistroNexus`.
- v2.0.1 release notes and root README use `https://github.com/lazyworkshop-create/DistroNexus`.

## Conclusion
Website content is not aligned with the v2.0.1 baseline and requires coordinated updates across homepage messaging, installation/runtime guidance, cmdlet-based docs, release blog, bilingual content, and repository links.
