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

Before final sign-off, generate deterministic checklist evidence using the reusable script entry:

```powershell
.\tools\collect-p2-test-evidence.ps1 -Phase P3 -DeterministicPathMode -EvidenceId p3-evidence-deterministic -UpdateChecklist:$false
```

Expected outputs (relative paths):

- `docs/development/testing/results/p3-evidence-deterministic/acceptance-evidence-index.md`
- `docs/development/testing/results/p3-evidence-deterministic/p3-test-evidence-proof.md`
- `docs/development/testing/results/p3-evidence-deterministic/p3-evidence-bundle.json`

If unresolved evidence links remain, the bundle marks them under `UnresolvedItems` for actionable follow-up.

## 6. Sign-off Closure Workflow (P3)

### 6.1 Owner Mapping
- Engineering sign-off owner: Engineering Lead
- QA sign-off owner: QA Lead
- Release sign-off owner: Release Manager

### 6.2 Sign-off Eligibility Triggers
- P3 implementation and test checklists are updated and evidence-linked.
- Acceptance checklist exception-handling fields include owner/milestone/follow-up references.
- No unresolved blocker without explicit release-manager decision.

### 6.3 Handoff and Escalation
1. Start sign-off request with template: `docs/development/v2-1-1-p3-signoff-handoff-template.md`.
2. Attach checklist and evidence references from: `docs/development/v2-1-1-p3-signoff-reference-index.md`.
3. If unresolved deferred items remain, escalate through release manager with go/no-go decision logged.
