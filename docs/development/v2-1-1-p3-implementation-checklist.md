# v2.1.1 P3 - Implementation Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) and deferred follow-up from P2 acceptance.

## 1. Scope
- FR-4.1 Regression Diff Hardening
- FR-4.2 Evidence Pipeline Standardization
- FR-4.3 Sign-off Workflow Closure

## 2. FR-4.1 Regression Diff Hardening

### 2.1 Diff Scenario Completion
- [x] Implement explicit baseline run ID success-path handling with deterministic selection output.
- [x] Implement identical-run zero-change optimization and clear summary wording.
- [x] Implement added/removed template detection consistency across profile combinations.

### 2.2 Runner and Artifact Reliability
- [x] Ensure run index and summary remain consistent for both real and sample fallback runs.
- [x] Ensure diff artifacts use stable ordering for changed items.
- [x] Ensure missing/invalid baseline metadata is captured with actionable diagnostics.

## 3. FR-4.2 Evidence Pipeline Standardization

### 3.1 Evidence Generation Workflow
- [x] Add reusable task/script entry for one-command P2/P3 evidence generation.
- [x] Ensure all generated evidence paths are repository-relative.
- [x] Ensure generated evidence package includes acceptance evidence index by default.

### 3.2 Data Contract and Hygiene
- [x] Define stable JSON contract versioning policy for lint and evidence outputs.
- [x] Ensure sensitive tokens/query parameters are redacted in generated references.
- [x] Ensure deterministic file naming strategy for repeatable verification runs.

## 4. FR-4.3 Sign-off Workflow Closure

### 4.1 Governance Mapping
- [x] Define owner mapping for Engineering, QA, and Release sign-off fields.
- [x] Define milestone and review trigger criteria for final sign-off eligibility.
- [x] Define escalation path for unresolved deferred items.

### 4.2 Documentation Integration
- [x] Integrate sign-off closure workflow into release process documentation.
- [x] Add sign-off handoff template for issue/PR-based approvals.
- [x] Ensure follow-up references are tracked in a single checklist index entry.

## 5. Cross-Cutting Quality Tasks
- [x] Add/adjust unit tests for FR-4.1 behavior changes.
- [x] Add/adjust integration checks for evidence generation workflow.
- [x] Validate backward compatibility with existing P1/P2 artifacts and checklists.
- [x] Update relevant docs/spec links and acceptance references.

## 6. Exit Gates (Implementation)
- [x] FR-4.1 implemented with deterministic diff hardening coverage.
- [x] FR-4.2 implemented with standardized evidence package outputs.
- [x] FR-4.3 implemented with sign-off workflow closure documentation.
- [x] Related docs and checklist references updated.

## 7. Ownership and Status
- Implementation Owner: [x] Assigned (`Tooling Maintainer`)
- Reviewer: [x] Assigned (`Engineering Lead`)
- Status: [ ] Not Started  [ ] In Progress  [x] Complete
