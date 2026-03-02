# Release Process

This document outlines the steps required to release a new version of DistroNexus.

## 1. Versioning

DistroNexus follows [Semantic Versioning 2.0.0](https://semver.org/).

## 2. Pre-Release Checklist

Before creating a release tag, ensure the following files are updated with the new version number (e.g., `X.Y.Z`):

1.  **Project Version**: `src/Client/Directory.Build.props`
    ```xml
    <Version>X.Y.Z</Version>
    ```

2.  **PowerShell Module Version**: `src/PowerShell/DistroNexus.psd1`
    ```powershell
    ModuleVersion = 'X.Y.Z'
    ```

3.  **Installer Version**: `tools/installer.iss`
    ```iss
    #define MyAppVersion "X.Y.Z"
    ```

4.  **Website Version**: `website/package.json`
    ```json
    "version": "X.Y.Z"
    ```

## 3. Documentation Authoring

All written content must be authored in English first, then translated into Simplified Chinese. Complete the Chinese translation immediately after the English version is done.

### 3.1 Release Notes

1.  Create the English release notes at `docs/release_notes/vX.Y.Z.md`.
2.  Translate and save the Chinese version at `docs/release_notes/vX.Y.Z.zh-CN.md`.

### 3.2 Blog Post

1.  Create the English blog post at `website/blog/YYYY-MM-DD-release-vX.Y.Z.md` following the existing convention:

    ```markdown
    ---
    slug: release-vX.Y.Z
    title: DistroNexus vX.Y.Z Released
    authors: []
    tags: [release, distronexus, wsl, tooling]
    ---

    Brief summary of the release.

    <!--truncate-->

    ## What's New in vX.Y.Z?

    ...highlights from CHANGELOG and release notes...
    ```

2.  Create the Chinese translation at `website/i18n/zh-Hans/docusaurus-plugin-content-blog/YYYY-MM-DD-release-vX.Y.Z.md`.

### 3.3 Website Documentation Pages

Review and update the following pages if the release changes any of their content. For each English page updated, update the corresponding file under `website/i18n/zh-Hans/docusaurus-plugin-content-docs/current/`.

- **`website/docs/installation.md`**: Update asset filenames and version numbers in the download section (e.g., `DistroNexus-X.Y.Z-Setup.exe`).
- **`website/docs/scripts-reference.md`**: Add or remove cmdlet entries to reflect changes to the PowerShell module's exported command surface (`src/PowerShell/DistroNexus.psd1`).
- **`website/docs/intro.md`**: Update the feature list or key capabilities description if significant features were added or removed.
- **`website/docs/usage.md`** and **`website/docs/configuration.md`**: Update any workflow steps or configuration options that changed in this release.

### 3.4 Homepage

If the release introduces a major capability, update the feature highlights in `website/src/pages/index.js` and the corresponding entries in `website/i18n/zh-Hans/`.

### 3.5 WeChat Official Account Article

Prepare a WeChat article announcing the release. Write the article in Chinese, covering:

- Release headline and version number
- Key new features or improvements (3–5 highlights with brief explanations)
- Link to the GitHub Release page
- Link to the website and installation guide

The article is published after the GitHub Release is confirmed live (see [Section 5.4](#54-wechat-official-account)).

## 4. Release Process

1.  Complete all version file updates described in [Section 2](#2-pre-release-checklist).
2.  Complete all documentation authoring described in [Section 3](#3-documentation-authoring).
3.  Update `CHANGELOG.md` with the new version and release notes.
4.  Commit all changes: `chore: release vX.Y.Z`.
5.  Push the release branch and open a Pull Request targeting `master`:
    ```bash
    git push origin feat/vX.Y.Z
    gh pr create --base master --title "chore: release vX.Y.Z" --body "Release vX.Y.Z — see docs/release_notes/vX.Y.Z.md for details."
    ```
6.  After review and CI pass, merge the PR into `master`. This triggers `deploy-site.yml` to deploy the updated website.
7.  Tag the release on `master`:
    ```bash
    git tag vX.Y.Z
    git push origin vX.Y.Z
    ```
    > `release.yml` verifies that the tag version matches `src/Client/Directory.Build.props` before proceeding. If they do not match, the build will fail. On success, it automatically builds and publishes the GitHub Release.

## 5. Post-Release Verification

### 5.1 GitHub Release

After `release.yml` completes:

1.  Open the GitHub Releases page and confirm the release is published.
2.  Verify all expected artifacts are attached:
    - `DistroNexus-vX.Y.Z-Release.zip` (portable)
    - `DistroNexus-vX.Y.Z-Release-selfcontained.zip` (self-contained)
    - `DistroNexus-X.Y.Z-Setup.exe` (installer — no `v` prefix by convention)
    - `.msixbundle` / `.msixupload` (Store package)
3.  Confirm the release notes body renders correctly.

### 5.2 Website Deployment

After merging to `master`:

1.  Confirm the `deploy-site.yml` workflow completes successfully in GitHub Actions.
2.  Open [https://lazyworkshop-create.github.io/DistroNexus/](https://lazyworkshop-create.github.io/DistroNexus/) and verify:
    - The new blog post appears in the blog listing (both English and Chinese).
    - The installation page shows the updated version and correct asset filenames.
    - Any updated documentation pages reflect the release changes.

### 5.3 Microsoft Store (if applicable)

If a Store submission is required for this release:

1.  Download the `.msixupload` artifact from the GitHub Release.
2.  Submit it to [Microsoft Partner Center](https://partner.microsoft.com/) for Store certification.
3.  Monitor the Partner Center dashboard until the submission is certified and live.

### 5.4 WeChat Official Account

Once the GitHub Release is confirmed live:

1.  Insert the final GitHub Release URL into the prepared article (see [Section 3.5](#35-wechat-official-account-article)).
2.  Publish the article on the WeChat Official Account.

## 6. Release Evidence Bundle (v2.1.1+)

Before final sign-off, generate deterministic checklist evidence using the reusable script entry:

```powershell
.\tools\collect-p2-test-evidence.ps1 -Phase P3 -DeterministicPathMode -EvidenceId p3-evidence-deterministic -UpdateChecklist:$false
```

Expected outputs (relative paths):

- `docs/development/testing/results/p3-evidence-deterministic/acceptance-evidence-index.md`
- `docs/development/testing/results/p3-evidence-deterministic/p3-test-evidence-proof.md`
- `docs/development/testing/results/p3-evidence-deterministic/p3-evidence-bundle.json`

If unresolved evidence links remain, the bundle marks them under `UnresolvedItems` for actionable follow-up.

## 7. Sign-off Closure Workflow (P3)

### 7.1 Owner Mapping
- Engineering sign-off owner: Engineering Lead
- QA sign-off owner: QA Lead
- Release sign-off owner: Release Manager

### 7.2 Sign-off Eligibility Triggers
- P3 implementation and test checklists are updated and evidence-linked.
- Acceptance checklist exception-handling fields include owner/milestone/follow-up references.
- No unresolved blocker without explicit release-manager decision.

### 7.3 Handoff and Escalation
1. Start sign-off request with template: `docs/development/v2-1-1-p3-signoff-handoff-template.md`.
2. Attach checklist and evidence references from: `docs/development/v2-1-1-p3-signoff-reference-index.md`.
3. If unresolved deferred items remain, escalate through release manager with go/no-go decision logged.
