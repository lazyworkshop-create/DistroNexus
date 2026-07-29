# Requirements: {{CAPABILITY_NAME}}

> Codex: create this from repository evidence and explicit product decisions. Requirements state what and why; do not hide unapproved technical design inside them. Keep unknown facts as `TODO: <decision or evidence needed>`.

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

- Security/authorization: {{SECURITY_REQUIREMENTS}}
- Reliability/recovery: {{RELIABILITY_REQUIREMENTS}}
- Audit/operations/retention: {{OPERATIONS_REQUIREMENTS}}
- Performance/limits: {{PERFORMANCE_REQUIREMENTS}}

## Acceptance Criteria

- {{END_TO_END_OBSERVABLE_CRITERION}}
- {{NEGATIVE_OR_SECURITY_CRITERION}}
- {{EXTERNAL_EVIDENCE_BOUNDARY}}

## Open Decisions and External Inputs

| Item | Impact | Owner | Smallest next action |
| --- | --- | --- | --- |
| {{OPEN_ITEM}} | {{IMPACT}} | {{OWNER}} | {{NEXT_ACTION}} |

## Source Evidence

| Area | Source | What it confirms | Confidence |
| --- | --- | --- | --- |
| {{AREA}} | {{SOURCE_PATH}} | {{FACT}} | Confirmed / Inferred |
