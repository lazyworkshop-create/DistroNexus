# Findings: Built-in Template Expansion

## Baseline Observations
- Current catalog includes 5 templates: `.NET`, `Node.js`, `Python`, `Docker`, and `Fullstack`.
- Current metadata is single-version oriented (`latest`/`lts`) and does not expose explicit selectable SDK channels (for example: .NET 6/8/10, Node 18/20/22).
- Current compatible distro set is primarily Ubuntu/Debian.

## External Research Notes
- Microsoft WSL guidance consistently positions these as mainstream workflows: web/app development, containers, databases, systemd services, and GPU-enabled ML.
- Systemd on WSL is relevant for service-oriented templates (for example MicroK8s and local daemons).
- Docker on WSL2 has two practical modes that should be represented in templates:
	- Docker Desktop WSL integration
	- In-WSL Linux container runtime (Docker Engine/Podman)
- GPU/ML setup in WSL is now a first-class scenario (CUDA path and DirectML-related workflows), but requires explicit environment preflight checks.
- Multi-version SDK handling is best implemented with ecosystem-native managers:
	- .NET: package/version selection and `global.json` pinning behavior.
	- Node.js: `nvm` with `lts/*`, `current`, and `.nvmrc`.
	- Python: `pyenv` with global/local/shell selection and `.python-version`.
	- Java/JVM: `sdkman` with per-shell and per-project `.sdkmanrc`.
	- Rust: `rustup` channels (`stable`, `beta`, `nightly`).
	- Go: official multi-install guidance via dedicated management docs.

## Source References
- https://learn.microsoft.com/windows/wsl/setup/environment
- https://learn.microsoft.com/windows/wsl/tutorials/wsl-containers
- https://learn.microsoft.com/windows/wsl/systemd
- https://learn.microsoft.com/windows/wsl/tutorials/gpu-compute
- https://learn.microsoft.com/windows/ai/directml/gpu-cuda-in-wsl
- https://learn.microsoft.com/en-us/dotnet/core/versions/selection
- https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu
- https://github.com/nvm-sh/nvm
- https://github.com/pyenv/pyenv
- https://sdkman.io/usage
- https://rustup.rs/
- https://go.dev/doc/manage-install

## Conclusions
- The template catalog should expand from a language-only starter set into a scenario-first matrix that includes CloudNative, Data/AI, Database, DevOps, and Platform workflows.
- Multi-version SDK support must be a core metadata concept (channel + major/minor + exact pinning), not script-specific ad hoc logic.
- Project-level version pinning artifacts should be generated optionally by templates to improve reproducibility across teams.
- WSL2-sensitive templates (systemd/container/GPU) require mandatory preflight checks and explicit fallback guidance.
- A phased rollout is the safest path: metadata extension first, then language templates, then WSL2 scenario templates.

---

# Findings: Built-in Template Automation Test Suite (Local WSL2)

## Baseline Observations
- Existing PowerShell runner already supports local WSL2-gated execution via `-EnableWsl2Scenarios` and `DISTRONEXUS_RUN_WSL2_TESTS=1`.
- Existing acceptance checklist still contains many `manual E2E required` items for real runtime verification.
- Existing hybrid documents already define the right validation pattern: UI flow + WSL command probes.

## External Research Notes
- Pester v5 advanced configuration supports all required controls for local automation suite design:
	- test selection by tag (`Filter.Tag`, `Filter.ExcludeTag`) and path,
	- machine-readable test result files (`TestResult.OutputFormat`, `TestResult.OutputPath`),
	- structured object output for post-processing (`Run.PassThru`).
- Pester test result formats include `NUnitXml` and `JUnitXml`, suitable for deterministic run artifacts.
- WSL command reference confirms host-side orchestration and diagnostics primitives are stable for runner logic:
	- distro discovery and targeting (`wsl --list --verbose`, `wsl --distribution`),
	- environment snapshot (`wsl --status`, `wsl --version`),
	- lifecycle control (`wsl --shutdown`).
- WSL systemd documentation confirms systemd capability differs by distro/configuration and must be explicitly checked for service-oriented templates.

## Source References
- https://pester.dev/docs/usage/configuration
- https://pester.dev/docs/commands/New-PesterConfiguration
- https://pester.dev/docs/commands/Invoke-Pester
- https://pester.dev/docs/usage/test-results
- https://learn.microsoft.com/windows/wsl/basic-commands
- https://learn.microsoft.com/windows/wsl/systemd

## Conclusions
- The most suitable approach is a **local-only hybrid automation suite** driven by Pester and validated through real WSL probes.
- Full-catalog testing and selective template testing can both be implemented cleanly with metadata-driven discovery + tag/template ID filtering.
- Result persistence should be standardized as XML + JSON + markdown summaries under `docs/development/testing/results/` for auditability.
- Capability-gated scenarios (GPU/systemd/Docker Desktop integration) should use `Blocked` classification instead of false-negative `Failed` when host prerequisites are missing.
- Delivery-document mapping is now complete:
	- implementation tasks define build sequence and ownership-ready work items,
	- acceptance criteria define release gate conditions,
	- test checklist defines executable verification path and pass standards.

## Execution Findings (2026-02-14 Local Run)
- Full-catalog execution completed and produced artifacts successfully:
	- RunId `150754-8e089550`, Mode `AllTemplates`, Distro `Ubuntu`.
	- Result summary: Total 15, Pass 0, Fail 15, Blocked 0.
	- Artifacts: `summary.md`, `run-manifest.json`, `test-results.xml`, and per-template logs under `docs/development/testing/results/20260214/150754-8e089550/`.
- Focused reproduction run confirms the same failure pattern in single-template mode:
	- RunId `150925-70dd1248`, Mode `SelectedTemplates`, Template `dotnet-dev`.
	- Result summary: Total 1, Pass 0, Fail 1, Blocked 0.
- Terminal runtime output shows the consistent bootstrap error across runs:
	- `bash: line 4: BASH_SOURCE[0]: unbound variable`
- Implication:
	- Failures are likely concentrated in shell bootstrap/shared script resolution, not in template-specific package/probe checks.
	- Next repair focus should be shell entry strict-mode compatibility and shared library path resolution before re-running full catalog.

## Execution Findings Update (2026-02-14 Isolated Per-Template Runs)
- Runner isolation strategy validated:
	- Each template can be executed from a clean imported distro instance cloned from the selected base distro snapshot.
	- Automatic instance teardown (`terminate + unregister`) is reliable and leaves no residual `dnx-auto-*` distros.
- Root-context execution is required for non-interactive local automation:
	- Running template scripts as root avoids `sudo` password prompts in clean instances.
- Version-manager based templates require probe logic aligned with non-login shell behavior:
	- `nvm`, `sdkman`, and `rustup` workflows can succeed while default non-login PATH probes fail; probes must validate installation artifacts/initialized environments rather than assuming global PATH mutation.
- Final objective met with full-catalog run:
	- RunId `174259-9f5bd124` achieved `Pass 15 / Fail 0 / Blocked 0` in `PerTemplateIsolatedImport` mode.
