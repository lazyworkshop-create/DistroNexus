# v2.1.1 P2 - Implementation Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) Phase 3 (P2).

## 1. Scope
- FR-3.1 Historical Regression Diff
- FR-3.2 Template Metadata Lint
- FR-3.3 Release Evidence Collector

## 2. FR-3.1 Historical Regression Diff

### 2.1 Data Model and Inputs
- [ ] Define baseline selection policy (latest successful run vs explicit run ID).
- [ ] Define diff schema for template-level status changes.
- [ ] Ensure comparison supports `Pass`, `Fail`, `Blocked` transitions.

### 2.2 Runner Integration
- [ ] Add diff mode/parameters to `Invoke-DistroNexusTemplateAutomation`.
- [ ] Implement run-to-run comparison in summary generation flow.
- [ ] Ensure missing baseline run is handled with non-crash fallback messaging.

### 2.3 Artifact Output
- [ ] Generate deterministic diff artifact (for example `regression-diff.json`).
- [ ] Add diff section to `summary.md` with changed template items.
- [ ] Link diff artifact from results index entry.

## 3. FR-3.2 Template Metadata Lint

### 3.1 Lint Command Surface
- [ ] Add lint command/script entry point for `config/templates.json` validation.
- [ ] Define optional strict mode and non-zero exit on violation.
- [ ] Support local run and CI run with same output contract.

### 3.2 Validation Rules
- [ ] Validate required fields and schema shape for each template item.
- [ ] Validate script path safety (relative path, allowed root, no traversal).
- [ ] Validate category policy and duplicate ID detection.

### 3.3 Reporting
- [ ] Emit machine-readable lint report (JSON) and readable summary output.
- [ ] Classify rule violations by severity (error/warning).
- [ ] Provide actionable remediation hints per failed rule.

## 4. FR-3.3 Release Evidence Collector

### 4.1 Collector Inputs
- [ ] Define supported evidence sources (workflow runs, test artifacts, release links).
- [ ] Define mapping between evidence items and release checklist sections.
- [ ] Support manual override entries when external links are pending.

### 4.2 Collector Output
- [ ] Generate deterministic evidence bundle file under docs path.
- [ ] Include timestamp, source metadata, and unresolved item list.
- [ ] Ensure output format is stable for checklist consumption.

### 4.3 Documentation and Workflow
- [ ] Document collector usage in development/release docs.
- [ ] Add optional workflow/task entry to execute collector.
- [ ] Ensure no secrets are persisted in evidence output.

## 5. Cross-Cutting Quality Tasks
- [ ] Add/adjust unit tests for diff logic and lint rules.
- [ ] Add/adjust integration tests for collector pipeline.
- [ ] Verify backward compatibility for existing automation and checklist flows.
- [ ] Update related docs and checklist references.

## 6. Exit Gates (Implementation)
- [ ] FR-3.1 implemented with run diff artifacts and summary linkage.
- [ ] FR-3.2 implemented with lint rules and CI/local compatibility.
- [ ] FR-3.3 implemented with deterministic evidence collector output.
- [ ] Related docs updated and linked.

## 7. Ownership and Status
- Implementation Owner: [ ] Assigned
- Reviewer: [ ] Assigned
- Status: [ ] Not Started  [ ] In Progress  [ ] Complete
