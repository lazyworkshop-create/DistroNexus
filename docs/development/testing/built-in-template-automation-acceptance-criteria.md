# Built-in Template Automation Acceptance Criteria

## Scope
Acceptance criteria for delivering the local WSL2 built-in template automation suite.

## A. Execution Scope and Guardrails
- [x] Suite runs only in local development context by default.
- [x] CI context is detected and auto-skipped unless explicit override is provided.
- [x] Runner supports both execution modes:
  - `AllTemplates`
  - `SelectedTemplates`

## B. Discovery and Selection
- [x] All built-in templates are discovered from `config/templates.json`.
- [x] Single template selection by ID works end-to-end.
- [x] Multiple template selection by ID works end-to-end.
- [x] Unknown template IDs fail fast with explicit unresolved ID list.
- [x] Dry-run mode outputs planned execution list without applying templates.

## C. Real Validation Behavior
- [x] Each executed template run performs real apply and WSL runtime probes.
- [x] Language templates validate selected version/channel using tool-specific probes.
- [x] Scenario templates validate expected runtime behavior (container/k8s/db/AI profile).
- [x] Capability-gated templates classify missing prerequisites as `Blocked`, not `Fail`.

## D. Status and Error Semantics
- [x] Final status model is consistent and complete:
  - `Pass`: all required checks succeed
  - `Fail`: required checks executed and at least one fails
  - `Blocked`: required host capability unavailable
- [x] Failure messages include actionable remediation hints.
- [x] Blocked messages include explicit host capability reason.

## E. Output and Traceability
- [x] XML test result file is generated for each run.
- [x] JSON run manifest is generated for each run.
- [x] Markdown summary is generated at run-specific docs path.
- [x] Summary includes:
  - total executed templates
  - pass/fail/blocked counts
  - failed/blocked reason list
  - links to logs/artifacts
  - environment snapshot (`wsl --status`, distro, WSL version, timestamp)
- [x] Historical index page is updated with the new run entry.

## F. Compatibility Baseline
- [x] Runner can target specified distro parameter without manual edits.
- [x] Ubuntu LTS local baseline can execute full-catalog mode.
- [x] Debian stable local baseline can execute mandatory language template subset.

## G. Non-Functional Requirements
- [x] Runner supports cancellation and timeout handling without process hang.
- [x] Suite logs are deterministic and sufficient for post-run diagnosis.
- [x] Suite does not leak credentials or sensitive host data in artifacts.

## H. Release Acceptance Gate
The suite is accepted when all criteria in sections A-G are passed and no blocker-severity defects remain open for local WSL2 execution scope.
