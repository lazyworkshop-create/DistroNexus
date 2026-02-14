# Template Expansion Implementation Task List

## Scope
Implementation task breakdown derived from `docs/specs/built-in-template-expansion-requirements.md`.

## Milestone A: Metadata Schema Extension
- [x] Add template metadata fields in `config/templates.json` schema usage:
  - `VersionOptions`
  - `DefaultSelections`
  - `PreflightChecks`
  - `OutputArtifacts`
  - `InstallMode`
  - `ScenarioTags`
- [x] Update Core template models to support new metadata fields.
- [x] Add backward-compatible parsing for existing templates without new fields.
- [x] Add validation rules for required metadata by template category.
- [x] Add log output structure for selected channel/version and produced artifacts.

## Milestone B: Multi-Version Language Templates
### B1 .NET
- [x] Create `dotnet-multi-sdk-dev` template definition.
- [x] Support channel options: `LTS`, `STS/Current`, `SpecificVersion`.
- [x] Implement optional `global.json` generation.
- [x] Add post-install verification (`dotnet --list-sdks`, selected SDK detection).

### B2 Node.js
- [x] Create `nodejs-multi-version-dev` template definition.
- [x] Integrate `nvm` bootstrap and shell initialization.
- [x] Support channel options: `lts/*`, `current`, `specific`.
- [x] Implement optional `.nvmrc` generation.
- [x] Add package manager options (`npm`, `pnpm`, `yarn`).

### B3 Python
- [x] Create `python-multi-version-dev` template definition.
- [x] Integrate `pyenv` bootstrap and shell initialization.
- [x] Support channel and exact version resolution.
- [x] Implement optional `.python-version` generation.
- [x] Add optional Poetry/Pipenv bootstrap.

### B4 Java/JVM
- [x] Create `java-jvm-dev` template definition.
- [x] Integrate `sdkman` bootstrap.
- [x] Provide common LTS version options.
- [x] Implement optional `.sdkmanrc` generation.

### B5 Rust
- [x] Create `rust-dev` template definition.
- [x] Integrate `rustup` bootstrap.
- [x] Support channels: `stable`, `beta`, `nightly`.
- [x] Add post-install verification (`rustc --version`, `cargo --version`).

### B6 Go
- [x] Create `go-dev` template definition.
- [x] Support stable channel and previous stable fallback.
- [x] Add post-install verification (`go version`).

## Milestone C: WSL2 Scenario Templates
### C1 Container Runtime
- [x] Create `container-runtime-dev` template definition.
- [x] Add mode options: Docker Desktop integration / Docker Engine / Podman.
- [x] Add context sanity checks and conflict guidance.

### C2 Local Kubernetes
- [x] Create `kubernetes-local-dev` template definition.
- [x] Provide cluster option selection: `kind`, `k3d`, `microk8s`.
- [x] Add systemd precheck for `microk8s` path.

### C3 Local Database Stack
- [x] Create `database-local-stack` template definition.
- [x] Add selectable components: PostgreSQL, MySQL, Redis, MongoDB, SQLite tooling.
- [x] Add service startup and health-check scripts.

### C4 AI/ML with GPU Option
- [x] Create `ai-ml-gpu-dev` template definition.
- [x] Add profiles: `CPU`, `NVIDIA-CUDA`, `DirectML-Python`.
- [x] Add WSL kernel / Windows build / GPU driver preflight checks.
- [x] Add fallback flow to CPU profile when GPU prerequisites fail.

## Milestone D: Integration and UX Hardening
- [x] Add category and scenario tag support in template discovery/filtering logic.
- [x] Add concise version option rendering in template selection step.
- [x] Keep advanced options collapsed by default.
- [x] Add user-facing remediation messages for failed preflight checks.
- [x] Ensure all template scripts are idempotent and safe for re-run.

## Cross-Cutting Engineering Tasks
- [x] Introduce shared script helpers for:
  - distro detection
  - retry/backoff
  - command existence checks
  - structured logging
- [x] Add consistent timeout and cancellation handling across templates.
- [x] Add template execution telemetry hooks (if existing diagnostic framework supports it).
- [x] Update docs in `docs/development/` and `docs/specs/` when behavior changes.

## Suggested Execution Order
1. Milestone A (schema + model + validation)
2. Milestone B language templates (.NET, Node.js, Python first)
3. Milestone C scenario templates (Container and Kubernetes first)
4. Milestone D integration hardening and final documentation sync
