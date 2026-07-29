---
name: agentteam-release-readiness
description: Assess a release candidate with evidence, separate repository readiness from UAT or production gates, and create a go/no-go/blocked release record with rollback posture. Use when a user asks whether code is ready to release, requests UAT/production readiness, needs a release evidence record, or wants to close a final delivery slice. Do not deploy, publish, change live data, or mutate infrastructure unless the user explicitly authorizes that operation.
---

# AgentTeam Release Readiness

Treat release readiness as an evidence decision, not a successful build. The target repository’s `AGENTS.md`, release runbooks, and environment permissions are authoritative.

## 1. Define the release boundary

1. Read `AGENTS.md`, release runbooks, the approved scope, current status/closure items, and `git status --short`.
2. Record candidate identity: commit/tag/artifact/image digest, components, environment, owner, explicit exclusions, and rollback target.
3. Confirm whether the user authorized only assessment, a dry run, UAT release, or production release. Assessment never grants deployment authority.

## 2. Build the evidence ledger

Use [release-evidence-template.md](references/release-evidence-template.md). Record each gate as `Passed`, `Failed`, `Blocked`, or `Not Applicable` with an exact command, URL, artifact, log, or owner.

Evaluate in order:

1. Scope and source identity: approved diff, commit/tag, artifact provenance, clean staging boundary.
2. Repository gates: targeted tests, architecture/catalog/dependency checks, package or manifest validation, security/no-secret scans.
3. Deployment preconditions: credentials, required configuration/secrets, migrations/backups, capacity, rollback target, and approved release window.
4. Runtime gates: workload/process readiness, health checks, version/image identity, observability and audit visibility.
5. Business gates: representative success path, authorized path, and predictable failure/authorization/tenant boundary.
6. Recovery gates: rollback command/target, owner, and rollback verification expectation.

## 3. Decide and report

- `Repository ready`: repository-owned checks have current passing evidence, but external gates may remain.
- `UAT ready` or `Production ready`: every applicable precondition and environment gate has current evidence.
- `Blocked`: an applicable gate lacks authority, environment evidence, a required secret/configuration, or a product decision.
- `No-go`: a gate failed or the candidate differs from approved scope.

Never claim UAT/production readiness from local tests alone. Do not repeatedly rerun local checks once they have fresh passing evidence; report the remaining external gates instead. Record cross-service blockers in the target repository’s designated status location and service/capability blockers in their owning design or plan.

## 4. Operate only with explicit authority

If the user explicitly authorizes release execution, follow the target repository runbook, preserve the approved component boundary, capture the real deployment and acceptance evidence, and stop/roll back according to the documented trigger. Otherwise produce the readiness record and exact next safe action only.
