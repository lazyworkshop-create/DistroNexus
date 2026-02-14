# Template Expansion Acceptance Test Checklist

## Scope
Acceptance checklist for the built-in template expansion initiative.

## Pass Standard Baseline
- For items marked `manual E2E required`, use hybrid execution defined in `template-expansion-hybrid-test-cases.md`.
- A checklist item can be checked only when:
	- Required UI workflow completes without unhandled error, and
	- Required probe commands return expected outputs and exit code `0`, and
	- Run evidence (logs + probe outputs) is attached to test records.
- If environment capability is missing (for example GPU/systemd/Docker Desktop integration), mark as `Blocked` with host-capability reason instead of `Fail`.

## A. Catalog and Metadata Acceptance
- [x] `config/templates.json` loads successfully with all new templates.
- [x] New metadata fields are parsed without breaking existing templates.
- [x] Templates can be listed and filtered by category and scenario tags.
- [x] Missing optional fields do not break template rendering.
- [x] Validation errors are actionable when metadata is invalid.

## B. Multi-Version Language Template Acceptance
### B1 .NET
- [ ] Can install using `LTS` channel. _(manual E2E required)_
- [ ] Can install using `STS/Current` channel. _(manual E2E required)_
- [ ] Can install with `SpecificVersion`. _(manual E2E required)_
- [ ] Optional `global.json` is generated correctly when enabled. _(manual E2E required)_
- [ ] `dotnet --list-sdks` includes selected version/channel result. _(manual E2E required)_

### B2 Node.js
- [ ] `nvm` is installed and available in new shell session. _(manual E2E required)_
- [ ] `lts/*` and `current` channel selection works. _(manual E2E required)_
- [ ] Specific version selection works. _(manual E2E required)_
- [ ] Optional `.nvmrc` is generated correctly. _(manual E2E required)_
- [ ] Selected package manager tools are installed and callable. _(manual E2E required)_

### B3 Python
- [ ] `pyenv` is installed and available in new shell session. _(manual E2E required)_
- [ ] Selected Python version is installed and selectable. _(manual E2E required)_
- [ ] Optional `.python-version` is generated correctly. _(manual E2E required)_
- [ ] Optional Poetry/Pipenv selection installs successfully. _(manual E2E required)_

### B4 Java/JVM
- [ ] `sdkman` is installed and available in new shell session. _(manual E2E required)_
- [ ] Chosen Java version/channel is activated correctly. _(manual E2E required)_
- [ ] Optional `.sdkmanrc` is generated correctly. _(manual E2E required)_

### B5 Rust
- [ ] `rustup` installs successfully under WSL. _(manual E2E required)_
- [ ] `stable`, `beta`, `nightly` channels are selectable. _(manual E2E required)_
- [ ] `rustc` and `cargo` are available after install. _(manual E2E required)_

### B6 Go
- [ ] Go installs successfully with selected stable channel. _(manual E2E required)_
- [ ] `go version` matches expected version family. _(manual E2E required)_

## C. WSL2 Scenario Acceptance
### C1 Container Runtime
- [ ] Docker Desktop integration mode works when enabled on Windows side. _(manual E2E required)_
- [ ] In-WSL engine mode installs and starts successfully. _(manual E2E required)_
- [ ] Podman mode installs and basic container command succeeds. _(manual E2E required)_
- [ ] Conflicting Docker context cases are detected with remediation message. _(manual E2E required)_

### C2 Kubernetes Local
- [ ] `kind` path creates a usable cluster. _(manual E2E required)_
- [ ] `k3d` path creates a usable cluster. _(manual E2E required)_
- [ ] `microk8s` path checks/handles systemd requirement. _(manual E2E required)_
- [ ] `kubectl` can query nodes for selected cluster option. _(manual E2E required)_

### C3 Database Local Stack
- [ ] Selected database components install successfully. _(manual E2E required)_
- [ ] Services can start and report healthy status. _(manual E2E required)_
- [ ] Basic connectivity check passes for each selected database. _(manual E2E required)_

### C4 AI/ML GPU
- [ ] CPU profile always completes on supported distros. _(manual E2E required)_
- [ ] GPU profile runs prerequisite checks (Windows build, kernel, driver hints). _(manual E2E required)_
- [ ] When GPU prerequisites are not met, fallback to CPU path is offered. _(manual E2E required)_
- [ ] Minimal framework smoke command succeeds in selected profile. _(manual E2E required)_

## D. Preflight and Error Handling Acceptance
- [x] Systemd-dependent templates fail fast with clear instructions when systemd is disabled. _(automated logic + script validation)_
- [x] GPU-dependent templates fail fast with clear prerequisite guidance. _(automated logic + script validation)_
- [x] Command-not-found and network failures produce user-friendly errors. _(service-level error mapping)_
- [x] Partial failures record logs with enough details to retry safely. _(script-level continue/fail behavior + history records)_

## E. Non-Functional Acceptance
- [x] Template scripts are idempotent for re-run.
- [x] Cancellation and timeout behavior work as expected.
- [x] Progress reporting is continuous and phase-aware.
- [x] Logs include selected template, channel/version, and output artifacts.

## F. Compatibility Matrix Acceptance
- [ ] Ubuntu latest LTS happy path for all mandatory templates. _(manual E2E required)_
- [ ] Debian stable happy path for all mandatory language templates. _(manual E2E required)_
- [ ] Unsupported distro path shows clear incompatibility reason. _(manual E2E required)_

## G. Regression Acceptance
- [x] Existing 5 templates remain usable without metadata migration errors.
- [x] Existing wizard flow still supports skip-template behavior.
- [x] Existing progress step behavior remains stable under non-template installs.

## H. Exit Criteria
- [ ] All A-G critical items pass. _(manual E2E block remains)_
- [ ] No blocker severity issues remain open.
- [x] Known limitations are documented in release notes and docs.

## I. Test Case Mapping
- Execute detailed cases and pass gates in `template-expansion-hybrid-test-cases.md`.
- Minimum required set for remaining unchecked scope:
	- B1-B6: `TC-HYB-001` to `TC-HYB-005`
	- C1-C4: `TC-HYB-006` to `TC-HYB-009`
	- F: `TC-HYB-010`
	- H: `TC-HYB-011` + Global Pass Gate
