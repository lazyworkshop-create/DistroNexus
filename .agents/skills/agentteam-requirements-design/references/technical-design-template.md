# Technical Design: {{CAPABILITY_NAME}}

> Implement only accepted requirements. Map each material requirement to a section below or an explicit closure item.

## Scope and Requirement Traceability

- Project source/test/config/release roots: {{PROJECT_ROOTS}}
- Current architecture constraints: {{ARCHITECTURE_CONSTRAINTS}}
- Existing compatibility and migration boundary: {{COMPATIBILITY_BOUNDARY}}
- Requirements: {{REQUIREMENTS_PATH_AND_IDS}}
- Decisions and constraints: {{DECISION_PATHS}}
- Exclusions: {{EXCLUSIONS}}

| Requirement | Design section | Test or verification |
| --- | --- | --- |
| {{FR_ID}} | {{SECTION}} | {{VERIFICATION}} |

## Architecture and Ownership

- Components and module boundaries: {{COMPONENTS_AND_OWNERSHIP}}
- Request, event, and data flow: {{FLOW}}
- Dependencies and configuration: {{DEPENDENCIES}}

## Contracts and Behavior

- Public and internal request/response contracts: {{CONTRACTS}}
- Validation and normalization: {{VALIDATION}}
- Authorization and scope/tenant routing: {{AUTHORIZATION}}
- Error semantics and compatibility: {{ERRORS_AND_COMPATIBILITY}}
- Audit and observability: {{AUDIT_AND_OBSERVABILITY}}

## Data and Execution Semantics

- Persistence, ownership, and retention: {{DATA_OWNERSHIP}}
- State transitions, transactions, idempotency, and concurrency: {{STATE_AND_CONSISTENCY}}
- Failure, retry, cancellation, and recovery: {{FAILURE_POLICY}}

## Security and Operations

- Threat controls and secret handling: {{SECURITY_CONTROLS}}
- Runtime/deployment constraints: {{OPERATIONS_CONSTRAINTS}}
- External acceptance required: {{EXTERNAL_ACCEPTANCE}}

## Verification Strategy

- Unit/component: {{UNIT_TESTS}}
- Integration/runtime: {{INTEGRATION_TESTS}}
- Architecture/catalog/packaging: {{STRUCTURAL_CHECKS}}

## Open Items

| Item | Blocking level | Owner | Resolution |
| --- | --- | --- | --- |
| {{OPEN_ITEM}} | Blocker / Follow-up | {{OWNER}} | {{NEXT_ACTION}} |
