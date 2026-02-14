# Task Plan: Built-in Template Expansion

## Objective
Define requirements for adding more built-in templates that cover common development scenarios (including multiple SDK version options) and typical WSL2 application scenarios.

## Scope
- Analyze current template catalog and gap areas.
- Research official guidance and ecosystem practices for WSL2 and common dev stacks.
- Propose a requirement specification for new built-in templates.
- Keep implementation out of scope for this task.

## Phases
1. Baseline review of existing templates and constraints.
2. Web research on WSL2 scenarios and toolchain version management.
3. Requirements drafting (template taxonomy, version policy, compatibility, metadata).
4. Deliverable finalization and traceability updates.

## Status
- [x] Phase 1: Baseline review
- [x] Phase 2: Web research
- [x] Phase 3: Requirements drafting
- [x] Phase 4: Finalization

## Deliverables
- `docs/findings.md`
- `docs/progress.md`
- `docs/specs/built-in-template-expansion-requirements.md`

---

# Task Plan: Built-in Template Automation Test Suite Requirements

## Objective
Define requirements for a local-only WSL2 automation suite that validates all built-in templates with real execution, supports selective template runs, and persists results to project documentation.

## Scope
- Assess current test infrastructure and template acceptance backlog.
- Research official tooling/docs for local test orchestration and report generation.
- Produce a requirements specification focused on local developer execution (not CI/CD).
- Update planning traceability documents.

## Phases
1. Baseline review of existing template-testing assets and runner capabilities.
2. External research on Pester filtering/reporting and WSL orchestration/systemd constraints.
3. Requirements drafting for full-catalog and selective template verification.
4. Finalization and documentation traceability updates.

## Status
- [x] Phase 1: Baseline review
- [x] Phase 2: Web research
- [x] Phase 3: Requirements drafting
- [x] Phase 4: Finalization

## Deliverables
- `docs/findings.md`
- `docs/progress.md`
- `docs/specs/built-in-template-automation-test-suite-requirements.md`

---

# Task Plan: Automation Suite Delivery Docs

## Objective
Create implementation checklist, acceptance criteria, and executable test checklist based on the built-in template automation suite requirements.

## Scope
- Translate requirement clauses into implementation tasks.
- Define objective acceptance criteria for delivery gate.
- Define executable checklist for verification runs.

## Phases
1. Review requirements and existing doc style.
2. Draft implementation task list.
3. Draft acceptance criteria.
4. Draft test checklist and finalize traceability.

## Status
- [x] Phase 1: Requirements and style review
- [x] Phase 2: Implementation task list drafted
- [x] Phase 3: Acceptance criteria drafted
- [x] Phase 4: Test checklist drafted and traceability updated

## Deliverables
- `docs/development/built-in-template-automation-implementation-task-list.md`
- `docs/development/testing/built-in-template-automation-acceptance-criteria.md`
- `docs/development/testing/built-in-template-automation-test-checklist.md`
