# Technical Design: {{CAPABILITY_NAME}}

> Codex: write this after requirements clarification. Map each material requirement to a design section and verification. Resolve material `TODO` items before implementation.

## Scope and Requirement Traceability

- Project source/test/config/release roots: {{PROJECT_ROOTS}}
- Current architecture constraints: {{ARCHITECTURE_CONSTRAINTS}}
- Existing compatibility and migration boundary: {{COMPATIBILITY_BOUNDARY}}
- Requirements: {{REQUIREMENTS_PATH_AND_IDS}}
- Decisions/constraints: {{DECISION_PATHS}}
- Exclusions: {{EXCLUSIONS}}

| Requirement | Design section | Test or verification |
| --- | --- | --- |
| {{FR_ID}} | {{SECTION}} | {{VERIFICATION}} |

## Architecture and Ownership

{{COMPONENTS_AND_FLOW}}

## Contracts and Behavior

- Contracts: {{CONTRACTS}}
- Validation: {{VALIDATION}}
- Authorization and scope: {{AUTHORIZATION}}
- Errors and compatibility: {{ERRORS_AND_COMPATIBILITY}}
- Audit and observability: {{AUDIT_AND_OBSERVABILITY}}

## Data and Execution Semantics

- Data ownership and retention: {{DATA_OWNERSHIP}}
- State, transactions, idempotency, concurrency: {{STATE_AND_CONSISTENCY}}
- Failure, retry, cancellation, recovery: {{FAILURE_POLICY}}

## Security and Operations

- Threat/secret controls: {{SECURITY_CONTROLS}}
- Runtime/deployment constraints: {{OPERATIONS_CONSTRAINTS}}
- External acceptance: {{EXTERNAL_ACCEPTANCE}}

## Verification Strategy

- Unit/component: {{UNIT_TESTS}}
- Integration/runtime: {{INTEGRATION_TESTS}}
- Structural/packaging: {{STRUCTURAL_CHECKS}}

## Open Items

| Item | Blocking level | Owner | Resolution |
| --- | --- | --- | --- |
| {{OPEN_ITEM}} | Blocker / Follow-up | {{OWNER}} | {{NEXT_ACTION}} |
