# Template Expansion Acceptance Test Checklist

## Scope
Acceptance checklist for the built-in template expansion initiative.

## A. Catalog and Metadata Acceptance
- [ ] `config/templates.json` loads successfully with all new templates.
- [ ] New metadata fields are parsed without breaking existing templates.
- [ ] Templates can be listed and filtered by category and scenario tags.
- [ ] Missing optional fields do not break template rendering.
- [ ] Validation errors are actionable when metadata is invalid.

## B. Multi-Version Language Template Acceptance
### B1 .NET
- [ ] Can install using `LTS` channel.
- [ ] Can install using `STS/Current` channel.
- [ ] Can install with `SpecificVersion`.
- [ ] Optional `global.json` is generated correctly when enabled.
- [ ] `dotnet --list-sdks` includes selected version/channel result.

### B2 Node.js
- [ ] `nvm` is installed and available in new shell session.
- [ ] `lts/*` and `current` channel selection works.
- [ ] Specific version selection works.
- [ ] Optional `.nvmrc` is generated correctly.
- [ ] Selected package manager tools are installed and callable.

### B3 Python
- [ ] `pyenv` is installed and available in new shell session.
- [ ] Selected Python version is installed and selectable.
- [ ] Optional `.python-version` is generated correctly.
- [ ] Optional Poetry/Pipenv selection installs successfully.

### B4 Java/JVM
- [ ] `sdkman` is installed and available in new shell session.
- [ ] Chosen Java version/channel is activated correctly.
- [ ] Optional `.sdkmanrc` is generated correctly.

### B5 Rust
- [ ] `rustup` installs successfully under WSL.
- [ ] `stable`, `beta`, `nightly` channels are selectable.
- [ ] `rustc` and `cargo` are available after install.

### B6 Go
- [ ] Go installs successfully with selected stable channel.
- [ ] `go version` matches expected version family.

## C. WSL2 Scenario Acceptance
### C1 Container Runtime
- [ ] Docker Desktop integration mode works when enabled on Windows side.
- [ ] In-WSL engine mode installs and starts successfully.
- [ ] Podman mode installs and basic container command succeeds.
- [ ] Conflicting Docker context cases are detected with remediation message.

### C2 Kubernetes Local
- [ ] `kind` path creates a usable cluster.
- [ ] `k3d` path creates a usable cluster.
- [ ] `microk8s` path checks/handles systemd requirement.
- [ ] `kubectl` can query nodes for selected cluster option.

### C3 Database Local Stack
- [ ] Selected database components install successfully.
- [ ] Services can start and report healthy status.
- [ ] Basic connectivity check passes for each selected database.

### C4 AI/ML GPU
- [ ] CPU profile always completes on supported distros.
- [ ] GPU profile runs prerequisite checks (Windows build, kernel, driver hints).
- [ ] When GPU prerequisites are not met, fallback to CPU path is offered.
- [ ] Minimal framework smoke command succeeds in selected profile.

## D. Preflight and Error Handling Acceptance
- [ ] Systemd-dependent templates fail fast with clear instructions when systemd is disabled.
- [ ] GPU-dependent templates fail fast with clear prerequisite guidance.
- [ ] Command-not-found and network failures produce user-friendly errors.
- [ ] Partial failures record logs with enough details to retry safely.

## E. Non-Functional Acceptance
- [ ] Template scripts are idempotent for re-run.
- [ ] Cancellation and timeout behavior work as expected.
- [ ] Progress reporting is continuous and phase-aware.
- [ ] Logs include selected template, channel/version, and output artifacts.

## F. Compatibility Matrix Acceptance
- [ ] Ubuntu latest LTS happy path for all mandatory templates.
- [ ] Debian stable happy path for all mandatory language templates.
- [ ] Unsupported distro path shows clear incompatibility reason.

## G. Regression Acceptance
- [ ] Existing 5 templates remain usable without metadata migration errors.
- [ ] Existing wizard flow still supports skip-template behavior.
- [ ] Existing progress step behavior remains stable under non-template installs.

## H. Exit Criteria
- [ ] All A-G critical items pass.
- [ ] No blocker severity issues remain open.
- [ ] Known limitations are documented in release notes and docs.
