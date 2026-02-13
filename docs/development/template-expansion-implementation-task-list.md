# Template Expansion Implementation Task List

## Scope
Implementation task breakdown derived from `docs/specs/built-in-template-expansion-requirements.md`.

## Milestone A: Metadata Schema Extension
- [ ] Add template metadata fields in `config/templates.json` schema usage:
  - `VersionOptions`
  - `DefaultSelections`
  - `PreflightChecks`
  - `OutputArtifacts`
  - `InstallMode`
  - `ScenarioTags`
- [ ] Update Core template models to support new metadata fields.
- [ ] Add backward-compatible parsing for existing templates without new fields.
- [ ] Add validation rules for required metadata by template category.
- [ ] Add log output structure for selected channel/version and produced artifacts.

## Milestone B: Multi-Version Language Templates
### B1 .NET
- [ ] Create `dotnet-multi-sdk-dev` template definition.
- [ ] Support channel options: `LTS`, `STS/Current`, `SpecificVersion`.
- [ ] Implement optional `global.json` generation.
- [ ] Add post-install verification (`dotnet --list-sdks`, selected SDK detection).

### B2 Node.js
- [ ] Create `nodejs-multi-version-dev` template definition.
- [ ] Integrate `nvm` bootstrap and shell initialization.
- [ ] Support channel options: `lts/*`, `current`, `specific`.
- [ ] Implement optional `.nvmrc` generation.
- [ ] Add package manager options (`npm`, `pnpm`, `yarn`).

### B3 Python
- [ ] Create `python-multi-version-dev` template definition.
- [ ] Integrate `pyenv` bootstrap and shell initialization.
- [ ] Support channel and exact version resolution.
- [ ] Implement optional `.python-version` generation.
- [ ] Add optional Poetry/Pipenv bootstrap.

### B4 Java/JVM
- [ ] Create `java-jvm-dev` template definition.
- [ ] Integrate `sdkman` bootstrap.
- [ ] Provide common LTS version options.
- [ ] Implement optional `.sdkmanrc` generation.

### B5 Rust
- [ ] Create `rust-dev` template definition.
- [ ] Integrate `rustup` bootstrap.
- [ ] Support channels: `stable`, `beta`, `nightly`.
- [ ] Add post-install verification (`rustc --version`, `cargo --version`).

### B6 Go
- [ ] Create `go-dev` template definition.
- [ ] Support stable channel and previous stable fallback.
- [ ] Add post-install verification (`go version`).

## Milestone C: WSL2 Scenario Templates
### C1 Container Runtime
- [ ] Create `container-runtime-dev` template definition.
- [ ] Add mode options: Docker Desktop integration / Docker Engine / Podman.
- [ ] Add context sanity checks and conflict guidance.

### C2 Local Kubernetes
- [ ] Create `kubernetes-local-dev` template definition.
- [ ] Provide cluster option selection: `kind`, `k3d`, `microk8s`.
- [ ] Add systemd precheck for `microk8s` path.

### C3 Local Database Stack
- [ ] Create `database-local-stack` template definition.
- [ ] Add selectable components: PostgreSQL, MySQL, Redis, MongoDB, SQLite tooling.
- [ ] Add service startup and health-check scripts.

### C4 AI/ML with GPU Option
- [ ] Create `ai-ml-gpu-dev` template definition.
- [ ] Add profiles: `CPU`, `NVIDIA-CUDA`, `DirectML-Python`.
- [ ] Add WSL kernel / Windows build / GPU driver preflight checks.
- [ ] Add fallback flow to CPU profile when GPU prerequisites fail.

## Milestone D: Integration and UX Hardening
- [ ] Add category and scenario tag support in template discovery/filtering logic.
- [ ] Add concise version option rendering in template selection step.
- [ ] Keep advanced options collapsed by default.
- [ ] Add user-facing remediation messages for failed preflight checks.
- [ ] Ensure all template scripts are idempotent and safe for re-run.

## Cross-Cutting Engineering Tasks
- [ ] Introduce shared script helpers for:
  - distro detection
  - retry/backoff
  - command existence checks
  - structured logging
- [ ] Add consistent timeout and cancellation handling across templates.
- [ ] Add template execution telemetry hooks (if existing diagnostic framework supports it).
- [ ] Update docs in `docs/development/` and `docs/specs/` when behavior changes.

## Suggested Execution Order
1. Milestone A (schema + model + validation)
2. Milestone B language templates (.NET, Node.js, Python first)
3. Milestone C scenario templates (Container and Kubernetes first)
4. Milestone D integration hardening and final documentation sync
