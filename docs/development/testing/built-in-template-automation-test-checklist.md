# Built-in Template Automation Test Checklist

## Scope
Executable checklist for validating implementation against
`docs/specs/built-in-template-automation-test-suite-requirements.md` and
`built-in-template-automation-acceptance-criteria.md`.

## Pass Standard
- A checklist item is `Pass` only when:
  - command exit code is `0` (unless case expects non-zero),
  - expected output markers are present,
  - required run artifact is persisted.
- For capability-gated scenarios, unmet prerequisites must be marked `Blocked` with reason.

## 1) Runner Contract Tests
- [x] `-Mode AllTemplates` executes without parameter ambiguity.
- [x] `-Mode SelectedTemplates -TemplateIds <single>` executes selected single template only.
- [x] `-Mode SelectedTemplates -TemplateIds <id1,id2>` executes selected templates only.
- [x] Unknown template ID returns fail-fast diagnostics and non-zero exit.
- [x] `-DryRun` prints plan and does not apply templates.

## 2) Local-Only Guard Tests
- [x] In local shell (no CI env), runner proceeds normally.
- [x] With CI indicator set and no override, runner skips/aborts by policy.
- [x] With CI indicator and explicit override, runner proceeds with warning banner.

## 3) Metadata Discovery Tests
- [x] Runner loads template list from `config/templates.json`.
- [x] All built-in template IDs are included in discovered execution set.
- [x] Template filtering for selected IDs is deterministic and order-preserving.

## 4) Environment Preflight Tests
- [x] Environment snapshot commands execute and are captured:
  - `wsl --status`
  - `wsl --version`
  - `wsl --list --verbose`
- [x] Target distro exists and can execute basic shell probe.
- [x] Missing target distro returns clear failure reason.

## 5) Language Template Probe Tests
### 5.1 .NET
- [x] Channel/version validation via `dotnet --list-sdks`.

### 5.2 Node.js
- [x] `nvm` availability in new shell session.
- [x] `node -v` matches selected channel/version family.
- [x] Selected package manager command is callable.

### 5.3 Python
- [x] `pyenv versions` includes selected version.
- [x] `python --version` matches expectation.
- [x] Optional Poetry/Pipenv probe succeeds when selected.

### 5.4 Java
- [x] `sdk version` callable.
- [x] `java -version` matches selected channel/version family.

### 5.5 Rust
- [x] `rustc --version` callable.
- [x] `cargo --version` callable.

### 5.6 Go
- [x] `go version` matches expected version family.

## 6) Scenario Template Probe Tests
### 6.1 Container Runtime
- [x] Docker Desktop integration mode probe behaves correctly.
- [x] In-WSL Docker Engine mode probe behaves correctly.
- [x] Podman mode probe behaves correctly.

### 6.2 Kubernetes Local
- [x] Selected cluster path can be created (`kind`/`k3d`/`microk8s`).
- [x] `kubectl get nodes` probe returns expected readiness markers.

### 6.3 Database Local Stack
- [x] Selected DB services report running/healthy state.
- [x] Connectivity smoke probe passes per selected DB.

### 6.4 AI/ML Profile
- [x] CPU profile completes and smoke probe passes.
- [x] GPU profile performs prerequisite checks.
- [x] Missing GPU prerequisites result in `Blocked` or CPU fallback per policy.

## 7) Status Classification Tests
- [x] Successful run is classified as `Pass`.
- [x] Probe failure with executed checks is classified as `Fail`.
- [x] Missing host capability is classified as `Blocked`.
- [x] Final summary counts match per-item status totals.

## 8) Artifact Persistence Tests
- [x] XML result file exists and is parseable.
- [x] JSON manifest exists and includes all executed templates.
- [x] Markdown summary exists under run-specific docs path.
- [x] Summary includes pass/fail/blocked counts and environment snapshot.
- [x] Historical index includes link entry for the run.

## 9) Reliability and Safety Tests
- [x] Cancellation path exits gracefully with partial results preserved.
- [x] Timeout path exits gracefully with clear timeout diagnostics.
- [x] Logs avoid secrets and sensitive host values.

## 10) Exit Checklist
- [x] Full-catalog run completed on at least one prepared local WSL2 distro.
- [x] Selective single-template run completed.
- [x] Selective multi-template run completed.
- [x] No blocker-severity unresolved issues remain for local scope.
