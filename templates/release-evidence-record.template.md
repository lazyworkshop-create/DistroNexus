# Release Evidence Record: {{RELEASE_CANDIDATE}}

> Codex: complete this from current evidence. Do not record secrets. A missing environment-owned check is `Blocked`, not passed.

## Release Boundary

- Project and repository: {{PROJECT_NAME}}; {{REPOSITORY_ROOT}}
- Release runbook/readiness command: {{RELEASE_RUNBOOK_AND_READINESS_COMMAND}}
- Deployment/package/configuration roots: {{DELIVERY_ROOTS}}
- Candidate commit/tag/artifact digest: {{IDENTITY}}
- Environment: {{ENVIRONMENT}}
- Approved components/versions: {{COMPONENTS}}
- Explicit exclusions: {{EXCLUSIONS}}
- Assessment/execution authority: {{AUTHORITY}}
- Release and rollback owners: {{OWNERS}}

## Evidence Ledger

| Gate | Status | Exact evidence | Owner | Notes / next action |
| --- | --- | --- | --- | --- |
| Scope and source identity | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Repository tests and structural checks | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Configuration/credentials/migration/backups | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Deployment dry run/manifests | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Runtime health/version identity | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Representative business success and failure paths | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Observability/audit and rollback posture | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |

## Decision

- Outcome: Repository ready | UAT ready | Production ready | Blocked | No-go
- Supported claim: {{SUPPORTED_CLAIM}}
- Remaining external gates: {{EXTERNAL_GATES}}
- Next safe action: {{NEXT_ACTION}}
