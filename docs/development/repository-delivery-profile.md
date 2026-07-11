# Repository Delivery Profile: DistroNexus

## Authority

- Project root and primary branch: repository root; `master`
- Project purpose and technology baseline: Windows-native WSL distribution and development-environment manager; .NET 10, C#, WPF, PowerShell 7, xUnit, Pester, JavaScript, and Docusaurus
- Repository instructions: `AGENTS.md`
- Requirements: active specifications under `docs/specs`
- Design and decisions: active architecture and design documents under `docs/architecture` and `docs/specs`; use `docs/development` for implementation plans and delivery records when no more specific active location exists
- Status and documentation ownership: release notes under `docs/release_notes`; active non-root documents belong under the appropriate `docs` subdirectory; website content belongs under `website`

## Project Topology

- Production source roots: `src/Client/DistroNexus.Desktop`, `src/Client/DistroNexus.Core`, and `src/PowerShell`
- Test and acceptance roots: `src/Client/DistroNexus.Tests` and `tests/PowerShell`
- Configuration/schema roots: `config`, including catalog and template metadata/assets
- Deployment/package/release roots: `tools`, `.github/workflows`, `website`, and release documentation under `docs`
- Historical/reference roots and policy: `docs/archive` is historical evidence only and must be confirmed against active sources before reuse

## Scope and Permissions

- Requested capability: repository-wide Codex AgentTeam delivery workflow
- Explicit exclusions: no product behavior change, build/release change, deployment, publishing, WSL runtime mutation, or external-system mutation
- Allowed mutations: repository-local workflow assets, instructions, templates, and delivery profile
- Deployment/release permission: not granted; requires a separate explicit user request
- Existing changes to preserve: inspect `git status --short` before every slice and preserve changes outside the authorized slice
- Sensitive paths and redaction policy: do not expose credentials, signing material, tokens, private keys, personal paths, backup contents, or distribution data; redact sensitive logs and command output

## Runtime and Evidence

- Main runtime surfaces: WPF desktop application, Core service layer, PowerShell module, Docusaurus website, Windows build/package tooling, and GitHub Actions
- Data/schema evidence: `config/catalog.json`, `config/templates.json`, `config/templates/*`, application models, and persisted-format implementations under Core
- Behavior evidence: active code, xUnit/Pester tests, current specifications, build scripts, and current workflows
- Archive/reference policy: archived documents, old reports, and promotion material may explain history but are not current implementation authority

## Verification and Delivery

- Targeted tests: `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "TestScope!=Full"`; narrow by fully qualified test name where appropriate
- Additional checks: PowerShell unit tests from `tests/PowerShell` with `.\TestRunner.ps1 -TestType Unit`; website scripts from `website/package.json`; opt-in WSL2 validation only when explicitly required
- Build, lint/static analysis, and package commands: `dotnet build src/Client/DistroNexus.slnx -c Debug`; release package entry point `.\tools\build.ps1 -Configuration Release`
- Database/runtime/UI acceptance commands: no database exists; full non-UI integration command is `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "TestScope=Full&Category!=UIAutomation"`; UI automation and WSL runtime validation are environment-dependent and opt-in
- External evidence needed: Windows Store, release publishing, signing, real USB hardware, Docker/Podman, WSLg, and production website evidence must come from explicitly authorized environments
- Commit convention: English message, one accepted implementation slice per commit, root agent stages and commits only with user authorization
- Release/readiness entry points and rollback procedure: `tools/build.ps1`, `.github/workflows/release.yml`, release documentation, and website checks; `TODO: release-specific rollback target and verification evidence needed`
- Open blockers: none for repository-local use of the AgentTeam workflow; environment-specific delivery remains blocked until the applicable authority and evidence are supplied
