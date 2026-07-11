# Release Evidence Record: {{RELEASE_CANDIDATE}}

> Replace placeholders from current evidence. Do not record secrets. A gate without evidence is `Blocked`, not passed.

## Release Boundary

- Project and repository: {{PROJECT_NAME}}; {{REPOSITORY_ROOT}}
- Release runbook/readiness command: {{RELEASE_RUNBOOK_AND_READINESS_COMMAND}}
- Deployment/package/configuration roots: {{DELIVERY_ROOTS}}
- Candidate commit/tag/artifact digest: {{IDENTITY}}
- Environment: {{ENVIRONMENT}}
- Approved components and versions: {{COMPONENTS}}
- Explicit exclusions: {{EXCLUSIONS}}
- Assessment or execution authority: {{AUTHORITY}}
- Release and rollback owners: {{OWNERS}}

## Evidence Ledger

| Gate | Status | Exact evidence | Owner | Notes / next action |
| --- | --- | --- | --- | --- |
| Approved scope and source identity | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Targeted repository verification | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Structural/security/package checks | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Required configuration, credentials, and migration/backups | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Deployment dry run or manifest validation | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Runtime health and version identity | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Representative authorized business path | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Negative authorization/tenant/failure path | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Audit/log/observability visibility | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |
| Rollback target and verification | {{STATUS}} | {{EVIDENCE}} | {{OWNER}} | {{NEXT_ACTION}} |

## Decision

- Outcome: Repository ready | UAT ready | Production ready | Blocked | No-go
- Current evidence supports: {{SUPPORTED_CLAIM}}
- Remaining external gates: {{EXTERNAL_GATES}}
- Rollback posture: {{ROLLBACK_POSTURE}}
- Next safe action: {{NEXT_ACTION}}
