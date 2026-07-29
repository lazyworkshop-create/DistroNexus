# DistroNexus Codex AgentTeam Guide

Use this repository with the Codex AgentTeam workflow. Repository-specific rules in this file are authoritative over reusable skill defaults.

## Target Project Configuration

- Project name and purpose: DistroNexus, a Windows WSL distribution and development-environment manager.
- Repository root and primary branch: repository root; `master`. Feature branches normally use the `feature/` prefix.
- Primary languages, frameworks, and build runtime: .NET 10, C#, WPF, PowerShell 7, xUnit, Pester, JavaScript, and Docusaurus.
- Source roots and architecture boundaries: `src/Client/DistroNexus.Desktop` owns WPF views, view models, converters, and UI services; `src/Client/DistroNexus.Core` owns business logic, models, interfaces, and service implementations; `src/PowerShell` owns the PowerShell module.
- Test roots and test categories: `src/Client/DistroNexus.Tests` for xUnit tests; `tests/PowerShell` for Pester unit and integration tests. WSL2-dependent tests are opt-in.
- Configuration, schema, and infrastructure roots: `config` for catalogs and template metadata/assets; `.github/workflows` and `tools` for CI, build, packaging, catalog, and release automation.
- Active documentation roots and ownership rules: active non-root documentation belongs under the appropriate `docs` subdirectory; website content belongs under `website`; root documents are limited to repository-wide files such as `README*`, `LICENSE`, `CHANGELOG.md`, and `AGENTS.md`.
- Archived/reference material policy: `docs/archive` is historical evidence only. Do not treat archived conclusions as current authority unless confirmed by active code, tests, specifications, or documentation.
- Standard build command: `dotnet build src/Client/DistroNexus.slnx -c Debug`.
- Targeted test command pattern: `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "TestScope!=Full"`; narrow further by fully qualified name when appropriate.
- Integration, runtime, UI, package, and architecture checks: use the commands under Verification Commands below. WSL2-dependent validation requires explicit scope or clear task necessity.
- Git/commit convention and protected paths: use English commit messages and one accepted implementation slice per commit; preserve unrelated changes; do not create commits, branches, tags, or pull requests unless the user asks. Do not modify release workflows, signing, store configuration, or publishing credentials outside explicit scope.
- Environments and mutation permissions: local repository reads and task-scoped edits are allowed. WSL runtime mutation is opt-in. Deployment, publishing, Windows feature changes, package installation, external-system writes, and live-environment mutation require explicit user authorization.
- Release runbooks, readiness checks, and rollback procedure: entry points are `tools/build.ps1`, `.github/workflows/release.yml`, release specifications/notes under `docs`, and website checks. Rollback is release-specific and must be established from the applicable release record or runbook; if absent, record `TODO: release-specific rollback evidence needed` and do not claim production readiness.
- Sensitive paths, secret policy, and redaction rules: never hardcode credentials or expose signing material, tokens, private keys, personal paths, backup contents, or distribution data. Validate paths, sanitize external input, redact sensitive command/log output, and treat marketplace/template scripts as untrusted.

## Project Conventions

### Language and Documentation

- Write all committed project artifacts in English: documentation, specifications, release notes, code comments, commit messages, PR descriptions, and new filenames.
- Conversation with the user may use the user's preferred language.
- Keep task-specific detail in the relevant requirements, design, decision, plan, or release record rather than expanding this file.

### Implementation

- Read relevant files before editing and follow existing patterns over introducing new abstractions.
- Keep changes narrowly scoped and do not revert user changes or unrelated work.
- Prefer `rg` or `rg --files` for searches and structured parsers or project APIs for structured data.
- C#: follow existing MVVM patterns, constructor injection, async I/O, and `Async` suffixes.
- WPF: keep views in Desktop and behavior in view models/services; follow existing XAML resources and localization patterns.
- PowerShell: use approved Verb-Noun names, `[CmdletBinding()]`, useful validation attributes, and clear errors.
- Templates: update `config/templates.json` and matching `config/templates/*` assets together.
- Add comments only when they explain non-obvious behavior.

### Verification Commands

Use the smallest relevant verification for the change:

- Client build: `dotnet build src/Client/DistroNexus.slnx -c Debug`
- Release build: `.\tools\build.ps1 -Configuration Release`
- C# unit tests: `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "TestScope!=Full"`
- C# full non-UI integration tests: `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "TestScope=Full&Category!=UIAutomation"`
- PowerShell unit tests: from `tests/PowerShell`, run `.\TestRunner.ps1 -TestType Unit`
- PowerShell all tests: from `tests/PowerShell`, run `.\TestRunner.ps1 -TestType All -CodeCoverage`
- Website checks: from `website`, use the existing scripts in `package.json`.

Before finishing a code change, run feasible relevant checks, report omitted checks and reasons, inspect the full diff for scope creep/generated artifacts/secrets/formatting churn, and update documentation when behavior, commands, configuration, or user workflows change.

## AgentTeam Roles

- Root agent: owns scope, evidence reconciliation, planning, delegation, final verification, staging, and user-authorized commits.
- Evidence explorer: performs explicitly delegated read-only evidence mapping.
- Slice implementer: edits one bounded slice and runs scoped verification; never stages, commits, deploys, publishes, or mutates external systems.
- Contract reviewer: independently reviews the complete slice diff in read-only mode and returns `ACCEPTED`, `REWORK_REQUIRED`, or `BLOCKED`.

## Collaboration Rules

1. Read this file and inspect `git status --short` before editing.
2. Keep one writer per worktree. Parallelize only independent read-only work or isolated worktrees with disjoint files and state.
3. Use `.agents/skills/agentteam-requirements-design` for medium or large work whose requirements or technical design are incomplete.
4. Do not begin implementation or slice planning while material requirements, contracts, permissions, or evidence remain unresolved.
5. Use `.agents/skills/agentteam-slice-delivery` only with approved, coding-ready requirements and design. Give every slice positive allowed paths and hard excluded paths.
6. Use `.agents/skills/agentteam-capability-delivery` for a single coherent capability family and `.agents/skills/agentteam-release-readiness` for evidence-based release decisions.
7. Require independent reviewer acceptance before staging an implementation slice, then rerun the narrow critical verification as root.
8. Stage only accepted slice files and create one English commit per slice only when the user has authorized commits.
9. Never infer permission to deploy, publish, install software, enable Windows features, change a database, mutate WSL instances, or write to another live system.
10. Treat archived documents, mocks, generated output, and old status reports as supporting evidence rather than current authority.

## Required Delivery Sequence

For medium or large changes:

1. Establish repository evidence and clarify the requested outcome, scope, exclusions, trust boundaries, and permissions.
2. Create separate evidence-backed requirements using `templates/requirements.template.md` when no stronger project artifact exists.
3. Create a coding-ready technical design using `templates/technical-design.template.md`, with requirement traceability, ownership, contracts, validation, errors, security, state/concurrency behavior, recovery, and exact verification.
4. Record material choices in `templates/decision-record.template.md` and material API/configuration/integration contracts in `templates/interface-contract.template.md`.
5. Validate design readiness with `.agents/skills/agentteam-requirements-design/scripts/validate-design-readiness.ps1`.
6. Split only the approved design into vertically complete slices using `templates/implementation-slice-plan.template.md`; validate the plan before delegation.
7. Implement one ready slice, independently review the complete diff, perform bounded rework if needed, rerun root verification, and commit only if authorized.
8. Use a closure slice for UAT, infrastructure, packaging, or production evidence that repository tests cannot prove.
9. For release decisions, complete `templates/release-evidence-record.template.md` and distinguish repository, UAT, and production readiness.

## Included Workflow Assets

- `.codex/agents`: reusable evidence explorer, slice implementer, and contract reviewer definitions.
- `.agents/skills/agentteam-requirements-design`: evidence-driven requirements and technical-design gate.
- `.agents/skills/agentteam-slice-delivery`: slice orchestration, acceptance gates, and plan validation.
- `.agents/skills/agentteam-capability-delivery`: bounded capability delivery workflow.
- `.agents/skills/agentteam-release-readiness`: release evidence and go/no-go workflow.
- `templates`: repository profile, requirements, technical design, decisions, contracts, slice plans, release evidence, and bootstrap prompt.

Unknown project or release facts must remain explicit as `TODO: <evidence needed>` with the smallest closure action. Never fabricate a command, environment state, authority, or verification result.
