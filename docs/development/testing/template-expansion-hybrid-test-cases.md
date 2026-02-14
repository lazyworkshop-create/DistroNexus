# Template Expansion Hybrid Test Cases and Pass Standards

## Purpose
Define executable test cases and objective pass standards for the remaining template-expansion acceptance scope.

## Scope
- Covers remaining items in sections B, C, F, and H of `template-expansion-acceptance-test-checklist.md`.
- Uses hybrid validation: desktop UI automation for workflow + WSL command probes for runtime verification.

## Execution Preconditions
- `DISTRONEXUS_RUN_UI_AUTOMATION=1`
- Test host has at least one WSL distro available.
- Network access to package sources is available unless the case explicitly tests network failure handling.

## Test Cases

### TC-HYB-001 .NET multi-version install
- Coverage: B1
- Flow:
  - Use UI automation to select `.NET Multi-SDK Developer` template.
  - Run once with `LTS`, once with `STS/Current`, once with `SpecificVersion`.
  - If enabled, verify `global.json` creation in workspace.
  - Probe in WSL with `dotnet --list-sdks`.
- Pass standard:
  - Requested channel/version appears in `dotnet --list-sdks` output.
  - `global.json` exists and contains selected version when option is enabled.

### TC-HYB-002 Node.js channel and package manager
- Coverage: B2
- Flow:
  - Select `Node.js Multi-Version Developer` template via UI automation.
  - Execute `lts/*`, `current`, and one specific version run.
  - Probe `nvm --version`, `node -v`, and selected package manager command.
- Pass standard:
  - `nvm` available in a new shell session.
  - `node -v` matches selected channel/version family.
  - Selected package manager command returns exit code `0`.

### TC-HYB-003 Python version and tooling
- Coverage: B3
- Flow:
  - Select `Python Multi-Version Developer` via UI automation.
  - Execute one specific Python version run.
  - Probe `pyenv versions`, `python --version`, and optional Poetry/Pipenv command.
- Pass standard:
  - Selected Python version appears in `pyenv versions`.
  - `python --version` matches requested major/minor.
  - Optional selected tool command returns exit code `0`.

### TC-HYB-004 Java/SDKMAN activation
- Coverage: B4
- Flow:
  - Select `Java/JVM Developer` template.
  - Execute channel/version selection and optional `.sdkmanrc` path.
  - Probe `sdk version` and `java -version`.
- Pass standard:
  - `sdkman` command available in new shell.
  - `java -version` output matches selected channel/version family.
  - `.sdkmanrc` exists when enabled.

### TC-HYB-005 Rust and Go baseline
- Coverage: B5, B6
- Flow:
  - Execute Rust template with `stable`, `beta`, and `nightly`.
  - Execute Go template with selected stable version.
  - Probe `rustc --version`, `cargo --version`, `go version`.
- Pass standard:
  - Rust channel switch succeeds for all selected channels.
  - `rustc`, `cargo`, and `go` commands are callable with exit code `0`.

### TC-HYB-006 Container runtime modes
- Coverage: C1
- Flow:
  - Run `Container Runtime` template with Docker Desktop integration, in-WSL engine, and Podman modes.
  - Probe mode-specific commands (`docker info` or `podman info`).
- Pass standard:
  - Selected mode command succeeds with exit code `0`.
  - Conflicting context scenario returns remediation guidance in logs.

### TC-HYB-007 Local Kubernetes options
- Coverage: C2
- Flow:
  - Run `Kubernetes Local` template with `kind`, `k3d`, and `microk8s`.
  - Probe `kubectl get nodes`.
- Pass standard:
  - Cluster creation command succeeds for selected mode.
  - `kubectl get nodes` returns at least one node in `Ready` state.
  - `microk8s` path reports clear systemd guidance when systemd is unavailable.

### TC-HYB-008 Database local stack health
- Coverage: C3
- Flow:
  - Run database stack template for selected DB components.
  - Probe service health and one connectivity command per DB.
- Pass standard:
  - Each selected database reports running/healthy service state.
  - Connectivity probe returns success exit code.

### TC-HYB-009 AI/ML CPU and GPU profile
- Coverage: C4
- Flow:
  - Run CPU profile and GPU profile in separate runs.
  - Collect prerequisite-check output for GPU path.
  - Probe minimal framework command in final selected path.
- Pass standard:
  - CPU profile always completes with exit code `0` on supported distro.
  - GPU path reports prerequisite status and offers CPU fallback when unmet.
  - Minimal framework probe command succeeds.

### TC-HYB-010 Compatibility matrix by distro
- Coverage: F
- Flow:
  - Execute mandatory template subset on Ubuntu LTS and Debian stable.
  - Execute one unsupported distro case.
- Pass standard:
  - Ubuntu and Debian runs pass all mandatory probes.
  - Unsupported distro run fails fast with explicit incompatibility reason.

### TC-HYB-011 End-to-end evidence integrity
- Coverage: H
- Flow:
  - Aggregate outputs from all runs (UI logs + probe outputs + exit status).
  - Produce one report per template/profile run.
- Pass standard:
  - Every run has timestamped evidence package and deterministic pass/fail marker.
  - No blocker-severity unresolved issue remains for passed runs.

## Global Pass Gate
- A test case is `Pass` only when:
  - UI automation flow succeeds (no unhandled UI exception, expected page transitions observed), and
  - All required probe commands meet case-specific pass standards, and
  - Result evidence is archived.
- Release-level acceptance for remaining scope is `Pass` only when:
  - `TC-HYB-001` to `TC-HYB-011` are all `Pass`, and
  - No blocker-severity defects remain open.
