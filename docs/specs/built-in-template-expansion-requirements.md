# Built-in Template Expansion Requirements

## Document Control
- Version: 1.0
- Date: 2026-02-14
- Scope: Product requirements for expanding built-in WSL2 templates in DistroNexus
- Out of scope: implementation details, UI redesign, PowerShell/C# code changes

## 1. Background
The current built-in template catalog covers five baseline development templates (`.NET`, `Node.js`, `Python`, `Docker`, `Fullstack`) and primarily targets Ubuntu/Debian with single-channel package installation (`latest`/`lts`).

To support broader real-world usage, the template system must expand in two directions:
1. More scenario coverage for common WSL2 development and operations workflows.
2. Explicit multi-version SDK support (for example LTS vs latest stable, or major-version families).

## 2. Goals
- Provide a built-in template catalog that covers mainstream WSL2 workflows end-to-end.
- Enable developers to choose SDK version channels without editing scripts manually.
- Keep templates reproducible, distro-aware, and maintainable.
- Preserve a simple first-run experience while allowing advanced customization.

## 3. Non-Goals
- No automatic cloud resource provisioning.
- No template marketplace or online third-party template ingestion in this phase.
- No custom UI wizard redesign beyond metadata consumption needs.

## 4. External Evidence Summary
The following official guidance informs requirements:
- Microsoft WSL docs emphasize common scenarios: web/dev setup, Docker integration, database setup, systemd services, GPU/ML acceleration.
- Microsoft WSL systemd docs highlight service-oriented scenarios (for example MicroK8s, service management).
- Microsoft/.NET docs confirm SDK selection rules and use of `global.json` for pinning.
- Official ecosystem tools support multi-version workflows:
  - Node: `nvm` with `lts/*` and project `.nvmrc`.
  - Python: `pyenv` with per-shell/per-project version selection.
  - Java and related SDKs: `sdkman` with per-shell and per-project `.sdkmanrc`.
  - Rust: `rustup` channels (`stable`, `beta`, `nightly`) and WSL installation support.
  - Go: official guidance supports multiple-version management via dedicated install management docs.

## 5. Requirement Principles
- Official-first: prefer vendor-supported repositories and installation paths.
- Version-explicit: all language templates expose a version channel or exact version selection model.
- Reproducible-by-default: templates can generate project-level version files where applicable.
- Distro-safe: templates declare compatible distributions and fail fast with actionable messages.
- Layered complexity: quick defaults for beginners, optional advanced toggles for power users.

## 6. Template Coverage Expansion

### 6.1 New Template Categories
In addition to current `Development` templates, add categories:
- `CloudNative`
- `DataAndAI`
- `Database`
- `DevOps`
- `Platform`

### 6.2 Required Built-in Templates (Phase-1 Mandatory)
1. `dotnet-multi-sdk-dev`
   - Purpose: .NET development with selectable SDK channel.
   - SDK channels: `LTS`, `STS/Current`, `SpecificVersion`.
   - Outputs: optional `global.json` and CLI verification.

2. `nodejs-multi-version-dev`
   - Purpose: frontend/backend JavaScript and TypeScript workflows.
   - Version manager: `nvm`.
   - Channels: `lts/*`, `current`, `specific`.
   - Outputs: optional `.nvmrc`, package manager selection (`npm`, `pnpm`, `yarn`).

3. `python-multi-version-dev`
   - Purpose: Python app and service development.
   - Version manager: `pyenv` (+ optional virtual environment bootstrap).
   - Channels: latest patch of selected major/minor line, or exact version.
   - Outputs: optional `.python-version`, optional Poetry/Pipenv bootstrap.

4. `java-jvm-dev`
   - Purpose: Java/Kotlin/Scala development in WSL.
   - Version manager: `sdkman`.
   - Channels: LTS lines and latest stable vendor build.
   - Outputs: optional `.sdkmanrc`.

5. `rust-dev`
   - Purpose: systems programming and CLI development.
   - Version manager: `rustup`.
   - Channels: `stable`, `beta`, `nightly`.

6. `go-dev`
   - Purpose: Go service and tooling development.
   - Version policy: latest stable and previous stable supported.

7. `container-runtime-dev`
   - Purpose: containerized development inside WSL2.
   - Modes: `Docker Desktop Integration`, `Docker Engine in WSL`, `Podman`.
   - Includes: post-install checks and context sanity validation.

8. `kubernetes-local-dev`
   - Purpose: local Kubernetes workflows on WSL2.
   - Choices: `kind`, `k3d`, or `microk8s` (systemd-dependent path).
   - Includes: kubectl + one local cluster option.

9. `database-local-stack`
   - Purpose: quick local data services in WSL2.
   - Options: PostgreSQL / MySQL / Redis / MongoDB / SQLite tooling.
   - Includes: optional systemd service enablement checks.

10. `ai-ml-gpu-dev`
    - Purpose: data science and ML development with optional GPU acceleration.
    - Profiles: `CPU`, `NVIDIA-CUDA`, `DirectML-Python`.
    - Includes: environment checks for Windows build, WSL kernel, and GPU driver hints.

### 6.3 Secondary Templates (Phase-2 Recommended)
- `devcontainers-workstation` (VS Code Remote-WSL + Dev Containers helper setup)
- `infra-cli-toolbox` (Azure CLI, Terraform, kubectl, helm, jq, yq)
- `security-testing-toolkit` (network/security utilities for controlled environments)

## 7. Multi-SDK Version Requirements

### 7.1 Version Abstraction Model
Each template that installs language SDKs must support one or more of:
- `Channel`: semantic alias (`lts`, `stable`, `current`, `nightly`)
- `MajorMinor`: example `8.0`, `3.12`, `21`
- `ExactVersion`: fully pinned version

### 7.2 Project Pinning Artifacts
When selected by user options, templates must generate and validate:
- `.NET`: `global.json`
- Node.js: `.nvmrc`
- Python: `.python-version`
- Java ecosystem: `.sdkmanrc`
- Rust (optional): `rust-toolchain.toml` in future phase

### 7.3 Support Policy
- Default offer: active LTS and latest stable/current channels.
- Legacy SDK lines are optional and explicitly marked as `Compatibility` tier.
- Templates must warn when a selected version is unavailable in the active distro repository strategy.

## 8. Template Metadata Requirements

## 8.1 New Metadata Fields
Extend template definitions with:
- `VersionOptions`: available channels/versions per SDK component
- `DefaultSelections`: default channel and optional packages
- `PreflightChecks`: required commands, distro constraints, WSL/kernel prerequisites
- `OutputArtifacts`: version files and config files to be generated
- `InstallMode`: `package-manager`, `version-manager`, `scripted`
- `ScenarioTags`: searchable tags (`web`, `api`, `data`, `ml`, `k8s`, `devops`, etc.)

## 8.2 Script Execution Requirements
- Templates must run preflight checks before installation.
- Failed preflight checks must return explicit remediation guidance.
- Scripts must be idempotent when rerun.

## 9. WSL2-Specific Requirements
- Systemd-dependent templates must detect and report whether `systemd` is enabled.
- Container templates must detect WSL mode and avoid conflicting Docker contexts.
- GPU templates must validate minimal prerequisites and provide non-GPU fallback path.
- Database templates must clarify dev-only assumptions where upstream tooling is not production-supported in WSL scenarios.

## 10. UX and Discoverability Requirements
- Template browsing must support category and scenario tags.
- Version choices must be shown as concise selectable options (not free-text by default).
- Advanced options (project pinning artifact generation, optional tooling) should be collapsed by default.

## 11. Acceptance Criteria
A release is acceptable when:
- At least 10 mandatory Phase-1 templates are available and load correctly from `config/templates.json`.
- For each multi-version language template, at least 2 channels (for example LTS and latest/current) are installable.
- Preflight check coverage exists for systemd/container/GPU sensitive templates.
- At least one distro-compatible happy-path validation completed per template family.
- Template logs clearly indicate selected SDK version/channel and produced artifacts.

## 12. Phased Delivery
- Milestone A: metadata schema extension and compatibility handling.
- Milestone B: language multi-version templates (`.NET`, `Node.js`, `Python`, `Java`, `Rust`, `Go`).
- Milestone C: WSL2 scenario templates (`Container`, `Kubernetes`, `Database`, `AI/ML GPU`).
- Milestone D: usability hardening and E2E validation.

## 13. Risks and Mitigations
- Rapid upstream SDK changes: mitigate via channel-based defaults and explicit pinning support.
- Distro package availability variance: mitigate with preflight checks and fallback installers.
- Script fragility in mixed WSL setups: mitigate with idempotent scripts and clear context detection.
- Maintenance overhead from many templates: mitigate via shared script fragments and common validation library.

## 14. Reference URLs
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
