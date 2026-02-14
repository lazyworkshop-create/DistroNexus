# Template Expansion UI Automation Assessment

## Purpose
Evaluate whether remaining unchecked acceptance items can be completed via UI automation after introducing a desktop UI automation framework.

## Related Execution Docs
- Acceptance checklist: `template-expansion-acceptance-test-checklist.md`
- Detailed cases and pass criteria: `template-expansion-hybrid-test-cases.md`

## Framework Introduced
- Framework: `FlaUI` (`FlaUI.Core` + `FlaUI.UIA3`) with `xUnit`
- Project integration: `src/Client/DistroNexus.Tests`
- Initial assets:
  - `src/Client/DistroNexus.Tests/UIAutomation/UiAutomationSession.cs`
  - `src/Client/DistroNexus.Tests/UIAutomation/TemplateUiAutomationSmokeTests.cs`

## Execution Model
- UI automation is opt-in to avoid CI flakiness in non-interactive environments.
- Enable with environment variable:
  - `DISTRONEXUS_RUN_UI_AUTOMATION=1`
- Optional explicit desktop executable path:
  - `DISTRONEXUS_DESKTOP_EXE=<path to DistroNexus.Desktop.exe>`

## Remaining Acceptance Items Coverage Assessment

### Summary
- Remaining unchecked items are mostly runtime-installation validations (language runtimes, containers, Kubernetes, databases, GPU).
- Pure UI automation is **not sufficient** for most of these items.
- A **hybrid automation approach** is feasible: UI automation to drive selection/workflow + command-based probes to verify installed components inside target distro.

### Feasibility by Section

#### B. Multi-Version Language Templates
- UI-only feasibility: **Partial**
- Hybrid feasibility: **High**
- Notes:
  - UI can select template/channel and trigger apply flow.
  - Runtime verification (`dotnet --list-sdks`, `nvm`, `pyenv`, `sdkman`, `rustup`, `go version`) must be executed inside WSL via scripted probes.

#### C. WSL2 Scenario Templates
- UI-only feasibility: **Low to Partial**
- Hybrid feasibility: **Medium**
- Notes:
  - UI can drive mode/profile choices.
  - Container/K8s/database/GPU validation requires environment capabilities (Docker Desktop integration, systemd availability, GPU drivers, network/package feeds).
  - Automation can be built, but reliability depends on dedicated matrix hosts.

#### F. Compatibility Matrix
- UI-only feasibility: **Low**
- Hybrid feasibility: **High (with infra)**
- Notes:
  - Requires multiple distro instances (Ubuntu/Debian) and clean reset between runs.
  - Recommended in hosted Windows runners or dedicated lab machines with pre-provisioned WSL distros.

#### H. Exit Criteria (All A-G pass)
- Feasibility: **Conditionally achievable**
- Condition:
  - Requires hybrid automation pipeline and environment matrix coverage.
  - Not achievable by UI-only tests in current developer workstation context.

## Recommended Automation Plan

### Phase 1 (Implemented baseline)
- Introduce FlaUI and smoke tests for app launch/navigation.

### Phase 2 (Next)
- Add wizard-driven UI automation scenarios:
  - Select template
  - Select channel/mode/profile
  - Start apply flow
  - Validate progress/log surface states

### Phase 3 (Hybrid verification)
- After UI flow finishes, run WSL probes to assert installed artifacts and versions.
- Capture evidence per run:
  - selected template/options
  - command outputs
  - pass/fail markers in report file

### Phase 4 (Matrix orchestration)
- Execute across Ubuntu + Debian and optional GPU/systemd-enabled hosts.
- Publish matrix evidence to `docs/development/testing/template-e2e-verification.md`.

## Final Conclusion
- Remaining unchecked acceptance items **can be substantially automated**, but **not through UI automation alone**.
- The viable approach is **UI automation + environment probes + matrix orchestration**.
- With this hybrid model, all currently remaining manual items are automatable except cases blocked by host capability constraints (for example missing GPU/systemd/Docker Desktop integration on the execution machine).

## Exit Standard for Automation Completion
- Consider the remaining scope automation-ready only after `TC-HYB-001` to `TC-HYB-011` all pass under the Global Pass Gate defined in `template-expansion-hybrid-test-cases.md`.
