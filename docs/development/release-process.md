# Release Process

This document outlines the steps required to release a new version of DistroNexus.

## 1. Versioning

DistroNexus follows [Semantic Versioning 2.0.0](https://semver.org/).

## 2. Pre-Release Checklist

Before creating a release tag, ensure the following files are updated with the new version number (e.g., `2.1.0`):

1.  **Project Version**: `src/Client/Directory.Build.props`
    ```xml
    <Version>2.1.0</Version>
    ```

2.  **PowerShell Module Version**: `src/PowerShell/DistroNexus.psd1`
    ```powershell
    ModuleVersion = '2.1.0'
    ```

3.  **Installer Version**: `tools/installer.iss`
    ```iss
    #define MyAppVersion "2.1.0"
    ```

4.  **Website Version**: `website/package.json`
    ```json
    "version": "2.1.0"
    ```

## 3. Release Process

1.  Update `CHANGELOG.md` with the new version and release notes.
2.  Update `docs/release_notes/vX.Y.Z.md`.
3.  Commit changes: `chore: update versions to 2.1.0`.
4.  Tag the release: `git tag v2.1.0`.
5.  Push the tag: `git push origin v2.1.0`.
6.  The GitHub Action `release.yml` will automatically build and publish the release.

## 4. Verification

The release workflow includes a step to verify that the version in the tag matches the version in `src/Client/Directory.Build.props`. If they do not match, the release will fail.

## 5. Release Evidence Bundle (v2.1.1+)

Before final sign-off, generate a deterministic evidence bundle for checklist consumption:

```powershell
Import-Module .\src\PowerShell\DistroNexus.psd1 -Force

New-DistroNexusReleaseEvidenceBundle \
    -ReleaseVersion v2.1.1 \
    -WorkflowRuns @('https://github.com/<owner>/<repo>/actions/runs/<id>') \
    -TestArtifacts @('https://github.com/<owner>/<repo>/actions/runs/<id>/artifacts/<id>') \
    -ReleaseLinks @('https://github.com/<owner>/<repo>/releases/tag/v2.1.1')
```

Default output path:

- `docs/development/release-evidence/vX.Y.Z-evidence.json`

If unresolved evidence links remain, the bundle marks them under `UnresolvedItems` for actionable follow-up.
