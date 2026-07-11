# Requirements: {{CAPABILITY_NAME}}

> Fill every placeholder from evidence or an explicit product decision. Use `TODO: <decision or evidence needed>`; do not put technical implementation choices here unless they are constraints.

## Purpose

{{PROBLEM_AND_USER_OUTCOME}}

## Project Context

- Project and repository: {{PROJECT_NAME}}; {{REPOSITORY_ROOT}}
- Existing capability and affected runtime surfaces: {{CURRENT_CAPABILITY_AND_SURFACES}}
- Documentation/decision authority: {{DOCUMENTATION_AUTHORITY}}
- Environments and permitted mutation: {{ENVIRONMENTS_AND_PERMISSIONS}}

## Scope

- In scope: {{IN_SCOPE}}
- Out of scope: {{OUT_OF_SCOPE}}
- Compatibility and rollout boundary: {{COMPATIBILITY_BOUNDARY}}

## Actors and Trust Boundaries

| Actor/component | Trust level | Permitted responsibility |
| --- | --- | --- |
| {{ACTOR}} | {{TRUST_LEVEL}} | {{RESPONSIBILITY}} |

## Functional Requirements

### FR-001 {{REQUIREMENT_TITLE}}

{{OBSERVABLE_BEHAVIOR}}

Acceptance: {{BINARY_ACCEPTANCE}}

## Non-Functional Requirements

- Security and authorization: {{SECURITY_REQUIREMENTS}}
- Reliability, transaction, and recovery: {{RELIABILITY_REQUIREMENTS}}
- Observability, audit, and retention: {{OPERATIONS_REQUIREMENTS}}
- Performance and limits: {{PERFORMANCE_REQUIREMENTS}}

## Acceptance Criteria

- {{END_TO_END_OBSERVABLE_CRITERION}}
- {{NEGATIVE_OR_SECURITY_CRITERION}}
- {{EXTERNAL_EVIDENCE_BOUNDARY}}

## Open Decisions and External Inputs

| Item | Why it matters | Owner | Smallest next action |
| --- | --- | --- | --- |
| {{OPEN_ITEM}} | {{IMPACT}} | {{OWNER}} | {{NEXT_ACTION}} |

## Source Evidence

| Area | Source | What it confirms | Confidence |
| --- | --- | --- | --- |
| {{AREA}} | {{SOURCE_PATH}} | {{FACT}} | Confirmed / Inferred |
