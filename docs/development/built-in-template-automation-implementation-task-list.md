# Built-in Template Automation Implementation Task List

## Scope
Implementation breakdown derived from `docs/specs/built-in-template-automation-test-suite-requirements.md`.

## Milestone A: Runner Contract and Discovery
- [x] Add a dedicated local runner entry point for template automation suite.
- [x] Add CLI parameters:
  - `-Mode` (`AllTemplates`, `SelectedTemplates`)
  - `-TemplateIds` (single/multiple IDs)
  - `-Distro`
  - `-OutputRoot`
  - `-IncludeCapabilityGated`
  - `-DryRun`
  - `-AllowCiOverride`
- [x] Implement metadata-driven discovery from `config/templates.json`.
- [x] Implement unknown template ID validation with fail-fast diagnostics.
- [x] Implement planned-execution preview for `-DryRun`.

## Milestone B: Local-Only Guard and Environment Preflight
- [x] Add local-only guard (default skip when CI indicators are detected).
- [x] Add explicit override path guarded by `-AllowCiOverride`.
- [x] Capture environment snapshot at run start:
  - `wsl --status`
  - `wsl --version`
  - `wsl --list --verbose`
- [x] Add distro existence and readiness checks for selected target distro.
- [x] Add per-template capability checks (GPU/systemd/Docker Desktop integration).

## Milestone C: Execution Orchestration
- [x] Implement `AllTemplates` execution loop over discovered built-in templates.
- [x] Implement `SelectedTemplates` execution loop preserving user-specified order.
- [x] Invoke existing template apply flow for each test item.
- [x] Add per-item timeout, cancellation, and retry policy hooks.
- [x] Persist per-item raw execution logs.

## Milestone D: Probe Library and Status Classification
- [x] Create shared probe helpers for WSL command execution and output capture.
- [x] Implement language-family probe sets:
  - .NET (`dotnet --list-sdks`)
  - Node.js (`nvm`, `node`, package managers)
  - Python (`pyenv`, `python`, optional tools)
  - Java (`sdk`, `java -version`)
  - Rust (`rustc`, `cargo`, channel)
  - Go (`go version`)
- [x] Implement scenario-family probe sets:
  - Container runtime (docker/podman)
  - Kubernetes local (`kubectl get nodes` + cluster tool)
  - Database local stack (service/health/connectivity)
  - AI/ML profiles (CPU/GPU prerequisite + smoke command)
- [x] Implement unified result classifier:
  - `Pass`
  - `Fail`
  - `Blocked` (capability missing)

## Milestone E: Result Persistence and Documentation
- [x] Generate machine-readable XML result file (`NUnitXml` or `JUnitXml`).
- [x] Generate run manifest JSON with per-item details.
- [x] Generate markdown summary at:
  - `docs/development/testing/results/<yyyymmdd>/<run-id>/summary.md`
- [x] Maintain historical index at:
  - `docs/development/testing/results/index.md`
- [x] Ensure summary contains pass/fail/blocked counters and environment snapshot.

## Milestone F: Developer Experience and Safety
- [x] Add clear startup banner indicating local-only intent and expected runtime cost.
- [x] Add command examples for full run and selective run in `tests/README.md`.
- [x] Add troubleshooting guide section for common `Blocked` reasons.
- [x] Ensure logs never include secrets or sensitive host data.

## Milestone G: Final Validation and Handover
- [x] Run one full-catalog dry run to verify discovery contract.
- [x] Run one selective validation (single template ID).
- [x] Run one selective validation (multiple template IDs).
- [x] Verify XML + JSON + markdown outputs are generated and linkable.
- [x] Document known limitations and open follow-up tasks.

## Suggested Delivery Order
1. Milestone A-B (runner contract, guard, preflight)
2. Milestone C-D (execution + probes + classification)
3. Milestone E (artifact and docs output)
4. Milestone F-G (DX hardening and acceptance readiness)
