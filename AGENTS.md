# DistroNexus Codex Guide

## Purpose

This file is the repository-level guidance for Codex. Keep it concise, accurate, and focused on durable project conventions. Task-specific details belong in the user prompt or in the relevant docs.

## Project Snapshot

DistroNexus is a Windows WSL manager built with .NET 10, WPF, and PowerShell.

- `src/Client/DistroNexus.Desktop`: WPF desktop app, views, view models, converters, and UI services.
- `src/Client/DistroNexus.Core`: business logic, models, interfaces, and service implementations.
- `src/Client/DistroNexus.Tests`: xUnit tests for the .NET client code.
- `src/PowerShell`: PowerShell module, public cmdlets, private helpers, and module manifest.
- `tests/PowerShell`: Pester unit and integration tests for the PowerShell module.
- `config`: distribution catalog, template metadata, and template script assets.
- `docs`: architecture, specs, development docs, release notes, archive, and promotion materials.
- `website`: Docusaurus documentation site.
- `tools`: build, packaging, catalog, and release helper scripts.

## Language And Documentation

- Write all committed project artifacts in English: docs, specs, release notes, code comments, commit messages, PR descriptions, and filenames for new docs.
- Conversation with the user may use the user's preferred language.
- Place new non-root documentation under the appropriate `docs/` subdirectory. Keep root-level files for broad project documents such as `README*`, `LICENSE`, `CHANGELOG.md`, and `AGENTS.md`.

## Working Style

- Read the relevant files before editing and follow existing patterns over introducing new abstractions.
- Keep changes narrowly scoped to the request.
- Do not revert user changes or unrelated work in the tree.
- Prefer `rg` or `rg --files` for searches.
- Use structured parsers or project APIs for structured data when practical.
- Add comments only when they explain non-obvious behavior.

## Implementation Conventions

- C#: use existing MVVM patterns, constructor injection, async methods for I/O, and `Async` suffixes for async APIs.
- WPF: keep views in `DistroNexus.Desktop`, behavior in view models/services, and resource changes consistent with existing XAML style.
- PowerShell: use approved Verb-Noun cmdlet names, `[CmdletBinding()]`, validation attributes where useful, and clear error handling.
- Templates: update `config/templates.json` and matching `config/templates/*` assets together.
- Security: do not hardcode credentials. Validate paths and sanitize external input.

## Build And Test Commands

Use the smallest relevant verification for the change.

- Build client: `dotnet build src/Client/DistroNexus.slnx -c Debug`
- Build release: `.\tools\build.ps1 -Configuration Release`
- C# unit tests: `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "TestScope!=Full"`
- C# full non-UI integration tests: `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "TestScope=Full&Category!=UIAutomation"`
- PowerShell unit tests: from `tests/PowerShell`, run `.\TestRunner.ps1 -TestType Unit`
- PowerShell all tests: from `tests/PowerShell`, run `.\TestRunner.ps1 -TestType All -CodeCoverage`
- Website checks: from `website`, use the existing npm scripts in `package.json`.

WSL2-dependent tests must be opt-in. Only enable them when the user asks or the task clearly requires local WSL validation.

## Done Criteria

Before finishing a code change:

- Run the relevant build or tests when feasible.
- Report any verification that could not be run and why.
- Review the diff for accidental scope creep, generated artifacts, credentials, and unrelated formatting churn.
- Update docs when behavior, commands, configuration, or user-facing workflows change.

## Git And Review

- Do not create commits, branches, tags, or pull requests unless the user asks.
- Preserve unrelated modified or untracked files.
- For review requests, prioritize bugs, regressions, missing tests, and risky behavior with file and line references.
