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
- Backfill findings for v2.1.1 P1/P2 (chronological)

## Findings
### P1 (Chronological)
- 2026-02-20: P1 checklist definition committed (`8670bc5`).
- 2026-02-21: P1 implementation closure committed (`ec53000`).
- P1 closed the DevOps category gap by adding `infra-cli-toolbox` metadata and script assets in `config/templates.json` and `config/templates/infra-cli-toolbox/install.sh`.
- P1 introduced reusable environment diagnostics (`Test-DistroNexusTemplateEnvironment`) and integrated capability profile gating into `Invoke-DistroNexusTemplateAutomation`.

### P2 (Chronological)
- 2026-02-21: P2 checklist definition committed (`b8aba7f`).
- 2026-02-21: FR-3.1 regression diff committed (`3967f10`).
- 2026-02-21: FR-3.2 metadata lint committed (`d398928`).
- 2026-02-21: FR-3.3 release evidence collector committed (`6d39969`).
- 2026-02-21: P2 evidence package and checklist closure committed (`894f389`).

### Cross-Phase Verification
- P1/P2 implementation commits include corresponding PowerShell unit test additions and updates, with regression verification executed during implementation cycles.

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

---

Date: 2026-02-21

## Active Milestone
- Enhance Template Options UI to support MultiSelect and Text inputs

## Findings
- The database-local-stack template uses a comma-separated list for DB_COMPONENTS (e.g., postgresql,redis,sqlite).
- The client UI previously only supported single-select dropdowns (ComboBoxes) for template options, causing the advanced options step to be skipped for templates that required multi-select or text inputs.
- Added TemplateOptionType enum (Select, MultiSelect, Text) to Template.cs.
- Updated TemplateOptionsStepView.xaml to render ComboBox, ItemsControl (CheckBoxes), or TextBox based on the Type property using DataTriggers.
- Updated config/templates.json to use MultiSelect for database-local-stack, infra-cli-toolbox, 
odejs-dev, and 
odejs-multi-version-dev.
- Updated config/templates.json to use Text for dotnet-dev, dotnet-multi-sdk-dev, go-dev, and java-jvm-dev.
- The UI now correctly displays CheckBoxes for multi-select options and TextBoxes for free-form text options, allowing users to customize these templates during installation.
