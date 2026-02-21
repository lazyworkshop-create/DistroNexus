# v2.1.1 Release Readiness Checklist

Purpose: Track remaining pre-release work for v2.1.1 and provide a single go/no-go checklist.

Last updated: 2026-02-21

## 1. Scope and Baseline
- Target release: `v2.1.1`
- Source branch: `feature/v2.1.1-tooling-enhancement`
- Primary requirements: `docs/specs/v2-1-1-tooling-enhancement-requirements.md`

## 2. Feature Completion Status
### 2.1 P1 / P2 / P3 delivery
- [x] P1 implementation checklist reviewed and acceptable.
- [x] P2 implementation checklist reviewed and acceptable.
- [x] P3 implementation checklist reviewed and acceptable.
- [x] P1/P2/P3 test and acceptance checklists are present and linked.

### 2.2 Deferred/exception handling
- [x] All deferred items have explicit owner and milestone confirmation from release stakeholders.
- [x] Any remaining `Pass with Exceptions` items have explicit go/no-go decision.

## 3. Verification and Test Gates
### 3.1 Automated tests
- [x] PowerShell unit suite passes in current branch.
- [x] Full .NET test scope for release target passes (as applicable for this release cut).
- [x] Any required workflow/CI checks for release branch are green.

### 3.2 Evidence package
- [x] Deterministic evidence package exists for P3 (`docs/development/testing/results/p3-evidence-deterministic/`).
- [x] Acceptance evidence index exists and is reviewable.
- [x] Evidence package reviewed and approved by QA/release owner.

## 4. Version and Artifact Preparation
### 4.1 Version consistency
- [x] `src/Client/Directory.Build.props` set to `2.1.1`.
- [x] `src/PowerShell/DistroNexus.psd1` module version set to `2.1.1`.
- [x] `tools/installer.iss` version set to `2.1.1`.
- [x] `website/package.json` version set to `2.1.1`.

### 4.2 Release notes and changelog
- [x] `CHANGELOG.md` updated for `v2.1.1`.
- [x] English release note created: `docs/release_notes/v2.1.1.md`.
- [x] Chinese release note created: `docs/release_notes/v2.1.1.zh-CN.md`.
- [x] English blog created: `website/blog/{YYYY-MM-DD}-release-v2.1.1.md`.
- [x] Chinese blog created: `website/i18n/zh-Hans/docusaurus-plugin-content-blog/{YYYY-MM-DD}-release-v2.1.1.md`.

## 5. Sign-off and Governance
### 5.1 Formal sign-off
- [x] Engineering sign-off completed.
- [x] QA sign-off completed.
- [x] Release sign-off completed.

### 5.2 Handoff and auditability
- [x] Sign-off handoff record completed using `docs/development/v2-1-1-p3-signoff-handoff-template.md`.
- [x] Sign-off reference index reviewed: `docs/development/v2-1-1-p3-signoff-reference-index.md`.
- [x] Tracking files (`docs/task_plan.md`, `docs/findings.md`, `docs/progress.md`) are up to date for release cut.

## 6. Release Execution
- [x] Final release commit created (version/doc updates only).
- [x] Tag created: `git tag v2.1.1`.
- [x] Tag pushed: `git push origin v2.1.1`.
- [ ] Release workflow completed successfully.
- [ ] Published artifacts validated (GitHub release / Store channel as applicable).

## 7. Final Decision
- [x] Go for release.
- [ ] No-Go (document blockers and remediation owner).

## 8. Notes / Blockers
- Blocker 1:
- Blocker 2:
- Decision notes:
