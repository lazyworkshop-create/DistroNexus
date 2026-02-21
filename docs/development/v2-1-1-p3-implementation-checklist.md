# v2.1.1 P3 - Implementation Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) and deferred follow-up from P2 acceptance.

## 1. Scope
- FR-4.1 Regression Diff Hardening
- FR-4.2 Evidence Pipeline Standardization
- FR-4.3 Sign-off Workflow Closure

## 2. FR-4.1 Regression Diff Hardening

### 2.1 Diff Scenario Completion
- [ ] Implement explicit baseline run ID success-path handling with deterministic selection output.
- [ ] Implement identical-run zero-change optimization and clear summary wording.
- [ ] Implement added/removed template detection consistency across profile combinations.

### 2.2 Runner and Artifact Reliability
- [ ] Ensure run index and summary remain consistent for both real and sample fallback runs.
- [ ] Ensure diff artifacts use stable ordering for changed items.
- [ ] Ensure missing/invalid baseline metadata is captured with actionable diagnostics.

## 3. FR-4.2 Evidence Pipeline Standardization

### 3.1 Evidence Generation Workflow
- [ ] Add reusable task/script entry for one-command P2/P3 evidence generation.
- [ ] Ensure all generated evidence paths are repository-relative.
- [ ] Ensure generated evidence package includes acceptance evidence index by default.

### 3.2 Data Contract and Hygiene
- [ ] Define stable JSON contract versioning policy for lint and evidence outputs.
- [ ] Ensure sensitive tokens/query parameters are redacted in generated references.
- [ ] Ensure deterministic file naming strategy for repeatable verification runs.

## 4. FR-4.3 Sign-off Workflow Closure

### 4.1 Governance Mapping
- [ ] Define owner mapping for Engineering, QA, and Release sign-off fields.
- [ ] Define milestone and review trigger criteria for final sign-off eligibility.
- [ ] Define escalation path for unresolved deferred items.

### 4.2 Documentation Integration
- [ ] Integrate sign-off closure workflow into release process documentation.
- [ ] Add sign-off handoff template for issue/PR-based approvals.
- [ ] Ensure follow-up references are tracked in a single checklist index entry.

## 5. Cross-Cutting Quality Tasks
- [ ] Add/adjust unit tests for FR-4.1 behavior changes.
- [ ] Add/adjust integration checks for evidence generation workflow.
- [ ] Validate backward compatibility with existing P1/P2 artifacts and checklists.
- [ ] Update relevant docs/spec links and acceptance references.

## 6. Exit Gates (Implementation)
- [ ] FR-4.1 implemented with deterministic diff hardening coverage.
- [ ] FR-4.2 implemented with standardized evidence package outputs.
- [ ] FR-4.3 implemented with sign-off workflow closure documentation.
- [ ] Related docs and checklist references updated.

## 7. Ownership and Status
- Implementation Owner: [ ] Assigned
- Reviewer: [ ] Assigned
- Status: [ ] Not Started  [ ] In Progress  [ ] Complete
