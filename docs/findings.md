# Findings Log

Date: 2026-02-20

## Active Milestone
- Create Development Environment Setup Script

## Findings
- The project requires .NET 10 SDK, Visual Studio 2022/2026, and Node.js for the website.
- The website has a legacy shell script (`setup_website_env.sh`) which is not ideal for Windows developers.
- A unified PowerShell script (`tools/setup-dev-env.ps1`) will be created to check and initialize both the application and website environments.
- The project baseline is .NET 10. The setup script now validates .NET 10 SDK instead of 6/7/8.
- The setup script supports optional auto-install via `-AutoInstall` using `winget` for missing .NET 10 SDK and Node.js LTS.
- Website `npm audit fix` reduced vulnerabilities from 29 to 28, and remaining issues are currently transitive with no non-breaking fix available in the current Docusaurus 3.9.2 dependency graph.
- Docusaurus core and preset-classic are already at latest stable version 3.9.2; remediation now depends on upstream package updates.

---

Date: 2026-02-21

## Active Milestone
- v2.1.1 P3 implementation and acceptance closure

## Findings
- Regression diff baseline resolution required exact run ID matching to avoid non-deterministic manifest selection.
- Added deterministic ordering for `ChangedItems` and explicit zero-change flag/summary wording improved diff readability.
- Evidence pipeline required reusable phase-aware automation (`P2`/`P3`) and deterministic path strategy for repeat-run verification.
- Lint output contract lacked schema metadata; `SchemaVersion` was added to support contract versioning.
- Evidence references are now consistently repository-relative and URL query/fragment data remains redacted in evidence bundle outputs.
- Final pass criteria remain blocked only by human sign-off gates; implementation, test, and acceptance evidence are otherwise complete.
