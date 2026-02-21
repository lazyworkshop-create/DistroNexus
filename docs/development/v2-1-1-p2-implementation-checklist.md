# v2.1.1 P2 - Implementation Checklist

Based on [v2.1.1 Tooling Enhancement Requirements](../specs/v2-1-1-tooling-enhancement-requirements.md) Phase 3 (P2).

## 1. Scope
- FR-3.1 Historical Regression Diff
- FR-3.2 Template Metadata Lint
- FR-3.3 Release Evidence Collector

## 2. FR-3.1 Historical Regression Diff

### 2.1 Data Model and Inputs
- [x] Define baseline selection policy (latest successful run vs explicit run ID).
- [x] Define diff schema for template-level status changes.
- [x] Ensure comparison supports `Pass`, `Fail`, `Blocked` transitions.

### 2.2 Runner Integration
- [x] Add diff mode/parameters to `Invoke-DistroNexusTemplateAutomation`.
- [x] Implement run-to-run comparison in summary generation flow.
- [x] Ensure missing baseline run is handled with non-crash fallback messaging.

### 2.3 Artifact Output
- [x] Generate deterministic diff artifact (for example `regression-diff.json`).
- [x] Add diff section to `summary.md` with changed template items.
- [x] Link diff artifact from results index entry.

## 3. FR-3.2 Template Metadata Lint

### 3.1 Lint Command Surface
- [x] Add lint command/script entry point for `config/templates.json` validation.
- [x] Define optional strict mode and non-zero exit on violation.
- [x] Support local run and CI run with same output contract.

### 3.2 Validation Rules
- [x] Validate required fields and schema shape for each template item.
- [x] Validate script path safety (relative path, allowed root, no traversal).
- [x] Validate category policy and duplicate ID detection.

### 3.3 Reporting
- [x] Emit machine-readable lint report (JSON) and readable summary output.
- [x] Classify rule violations by severity (error/warning).
- [x] Provide actionable remediation hints per failed rule.

## 4. FR-3.3 Release Evidence Collector

### 4.1 Collector Inputs
- [x] Define supported evidence sources (workflow runs, test artifacts, release links).
- [x] Define mapping between evidence items and release checklist sections.
- [x] Support manual override entries when external links are pending.

### 4.2 Collector Output
- [x] Generate deterministic evidence bundle file under docs path.
- [x] Include timestamp, source metadata, and unresolved item list.
- [x] Ensure output format is stable for checklist consumption.

### 4.3 Documentation and Workflow
- [x] Document collector usage in development/release docs.
- [x] Add optional workflow/task entry to execute collector.
- [x] Ensure no secrets are persisted in evidence output.

## 5. Cross-Cutting Quality Tasks
- [x] Add/adjust unit tests for diff logic and lint rules.
- [ ] Add/adjust integration tests for collector pipeline.
- [x] Verify backward compatibility for existing automation and checklist flows.
- [x] Update related docs and checklist references.

## 6. Exit Gates (Implementation)
- [x] FR-3.1 implemented with run diff artifacts and summary linkage.
- [x] FR-3.2 implemented with lint rules and CI/local compatibility.
- [x] FR-3.3 implemented with deterministic evidence collector output.
- [x] Related docs updated and linked.

## 7. Ownership and Status
- Implementation Owner: [ ] Assigned
- Reviewer: [ ] Assigned
- Status: [ ] Not Started  [x] In Progress  [ ] Complete
