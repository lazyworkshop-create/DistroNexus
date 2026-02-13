# 模版系统实现计划

- [x] Save Requirements and Create Plan <!-- id: 0 -->
- [x] Phase 1: Models and Interfaces <!-- id: 1 -->
    - [x] Create `Template.cs` <!-- id: 2 -->
    - [x] Create `TemplateApplicationRecord.cs` <!-- id: 3 -->
    - [x] Create `ITemplateService.cs` <!-- id: 4 -->
- [x] Phase 2: Core Service Implementation <!-- id: 5 -->
    - [x] Implement `TemplateService.cs` loading logic <!-- id: 6 -->
    - [x] Implement `TemplateService.cs` Apply logic <!-- id: 7 -->
    - [x] Register service in `App.xaml.cs` <!-- id: 8 -->
- [x] Phase 3: PowerShell Module <!-- id: 9 -->
    - [x] Create `Get-DistroNexusTemplate.ps1` <!-- id: 10 -->
    - [x] Create `Apply-DistroNexusTemplate.ps1` <!-- id: 11 -->
    - [x] Update `DistroNexus.psd1` <!-- id: 12 -->
- [x] Phase 4: UI Integration <!-- id: 13 -->
    - [x] Update `WizardContext.cs` <!-- id: 14 -->
    - [x] Create `SelectTemplateStep.cs` <!-- id: 15 -->
    - [x] Create `SelectTemplateStepView.xaml` <!-- id: 16 -->
    - [x] Register in `InstallWizardWorkflowViewModel.cs` <!-- id: 17 -->
    - [x] Update `ProgressStep.cs` <!-- id: 18 -->
- [x] Phase 5: Template Configuration <!-- id: 19 -->
    - [x] Create `config/templates.json` <!-- id: 20 -->
    - [x] Create sample scripts (dotnet, nodejs, python, docker) <!-- id: 21 -->
- [x] Phase 6: Template Management UI <!-- id: 26 -->
    - [x] Implement CRUD in TemplateService <!-- id: 27 -->
    - [x] Create TemplatesViewModel <!-- id: 28 -->
    - [x] Create TemplatesPage View <!-- id: 29 -->
    - [x] Add Navigation in MainWindow <!-- id: 30 -->
- [x] Phase 7: Testing and Verification <!-- id: 22 -->
    - [x] Unit Tests (PowerShell & C#) <!-- id: 25 -->
    - [x] Integration Tests <!-- id: 23 -->
    - [x] End-to-End Verification <!-- id: 24 -->

## Audit Update (2026-02-13)

- [x] Requirement-to-implementation audit completed
- [x] Evidence collected from source code, config, and tests
- [ ] Requirement-complete state reached

### Key Gap Summary
- Partial preset templates: only `dotnet-dev` and `nodejs-dev` are present under `config/templates/`.
- `InstallOptions` does not include `TemplateId`.
- `SelectTemplateStep` currently loads templates but does not implement distro-based filtering, skip checkbox flow, or explicit `Validate`/`OnExitAsync` behavior from the spec.
- No template-specific integration tests or end-to-end verification evidence found.

## Requirement Completion Plan (2026-02-13)

- [x] Phase 8: Requirement Refinement (P0/P1/P2) <!-- id: 31 -->
    - [x] Convert audit gaps to actionable requirements <!-- id: 32 -->
    - [x] Define release-gate acceptance checklist <!-- id: 33 -->
    - [x] Define Definition of Done for template feature <!-- id: 34 -->

- [x] Phase 9: P0 Delivery (Release Blockers) <!-- id: 35 -->
    - [x] Complete 5 official preset templates <!-- id: 36 -->
    - [x] Add `InstallOptions.TemplateId` and mapping <!-- id: 37 -->
    - [x] Complete select-template filtering/skip/validation flow <!-- id: 38 -->
    - [x] Unify `ScriptPath`/`Content` execution behavior <!-- id: 39 -->

- [x] Phase 10: P1 Delivery (Hardening) <!-- id: 40 -->
    - [x] Standardize template error semantics and user messaging <!-- id: 41 -->
    - [x] Implement template application history persistence <!-- id: 42 -->
    - [x] Implement custom template security baseline <!-- id: 43 -->

- [x] Phase 11: P2 Delivery (Stabilization) <!-- id: 44 -->
    - [x] Add template-focused integration tests <!-- id: 45 -->
    - [x] Complete E2E verification matrix and records <!-- id: 46 -->
    - [x] Improve diagnostics and execution summary reporting <!-- id: 47 -->

- [x] Phase 12: Wizard Step Refactor (UX Separation) <!-- id: 48 -->
    - [x] Restore installation-only `ProgressStep` UI and behavior <!-- id: 49 -->
    - [x] Add dedicated fullscreen `TemplateApplyStep` for template execution logs <!-- id: 50 -->
    - [x] Update workflow order to `ProgressStep -> TemplateApplyStep -> ResultStep` <!-- id: 51 -->
    - [x] Validate template-focused test suite after refactor <!-- id: 52 -->
