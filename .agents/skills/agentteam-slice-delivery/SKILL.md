---
name: agentteam-slice-delivery
description: Coordinate medium or large approved requirements and coding-ready technical designs as ordered, independently accepted, commit-ready implementation slices. Use when Codex must turn an approved design into an execution plan, delegate one bounded slice at a time, require independent read-only acceptance, drive bounded rework, and continue until a stated verifiable stopping condition is met. Do not use for a small edit, a read-only review, a release-only request, or work whose requirements/design gate is incomplete.
---

# AgentTeam Slice Delivery

Use this skill after `$agentteam-requirements-design`. The root agent is the only orchestrator, stager, and committer. One implementer edits one slice. One reviewer independently accepts the complete slice diff in read-only mode.

## 0. Entry gate

Before creating a slice plan, confirm all of the following:

- Requirements have stable ids, scope/non-goals, acceptance criteria, source evidence, and no material unresolved decision.
- Technical design traces every material requirement and defines affected contracts, ownership/boundaries, validation, authorization, errors, data/state/transaction/concurrency behavior where relevant, audit/observability, and verification.
- The repository profile identifies current worktree changes, permissions, architecture, source authority, and current test/runtime harnesses.
- External/UAT/production evidence that cannot be produced locally is named, owned, and excluded from an implementation-complete claim.

If a gate is false, stop and return to `$agentteam-requirements-design`. Never slice an unresolved design decision into code.

## 1. Create the delivery baseline

1. Read `AGENTS.md`; inspect `git status --short` and `git rev-parse HEAD`.
2. Name the authoritative requirements, design, decisions, status items, target repository profile, explicit exclusions, base commit, and mutation permissions.
3. For medium or large delivery, create one Goal. Its stopping condition is: all required slices are independently `ACCEPTED`, freshly reverified by root, committed within their own boundaries, and every required external closure item is either evidenced or explicitly prevents the claimed release state.
4. Read [slice-contract.md](references/slice-contract.md), [acceptance-gates.md](references/acceptance-gates.md), and [delegation-payloads.md](references/delegation-payloads.md).

## 2. Slice the approved design

Create the plan in the target repository's planning location. Start from [slice-plan-template.md](references/slice-plan-template.md) only when the target has no stronger template. Run `scripts/validate-slice-plan.ps1 -Path <plan>` before any delegation.

Use these splitting rules:

1. A slice delivers one observable capability, including its owned design update, production behavior, meaningful tests, and targeted verification.
2. Use dependency order, not directory order. Typical order is: policy/data/contract foundations -> public or integration boundary -> deterministic core behavior -> adapters/workers/UI -> feature bundles -> end-to-end and release closure.
3. Give every slice positive allowed paths and hard excluded paths. Do not use broad boundaries such as `src/**` unless the approved design truly owns all of it.
4. Put schema/migration, contract, and consumer changes in one slice only when they form one compatible, testable outcome. Otherwise establish the producer/contract foundation first.
5. Split when risks need distinct verification: authorization, persistence, routing, packaging, UI, external infrastructure, and release evidence usually need separate acceptance boundaries.
6. Never put unrelated cleanup, a second capability family, deployment, or live data mutation into an implementation slice.
7. Reserve a final closure slice for cross-boundary integration, security scans, architecture/catalog checks, and named external evidence. Local passing tests do not close environment-owned gates.

## 3. Execute one ready slice

Only a slice whose dependencies are `Committed` is ready. Do not run write-heavy slices concurrently in one worktree. Parallelize only independent read-only work, or isolated worktrees with no shared files, runtime state, or ordered dependencies.

1. Mark the ready slice `In Progress` in the plan and capture `base_commit`.
2. Delegate with the implementation payload in [delegation-payloads.md](references/delegation-payloads.md). Use `agentteam-slice-implementer` in parent-managed mode: it may edit and run scoped verification, but must not stage, commit, deploy, publish, or mutate external systems.
3. Require the implementer report before review. It must list changed files, requirements/design sections covered, behavior, test intent, exact commands/outcomes, verification limits, and remaining items.
4. If the implementer finds conflicting authority, missing material evidence, or a requested change outside allowed paths, mark the slice `Blocked`. Record the exact closure item; do not widen scope implicitly.

## 4. Independently accept or rework

1. Pass the complete diff from `base_commit` to `agentteam-contract-reviewer` using the review payload in [delegation-payloads.md](references/delegation-payloads.md). The reviewer must not rely on the implementer summary.
2. The only verdicts are `ACCEPTED`, `REWORK_REQUIRED`, and `BLOCKED`.
3. For `REWORK_REQUIRED`, send only the precise material findings to the same implementer. Re-review the complete updated diff, not only the last patch. Allow at most three implementation-review attempts per slice unless the user authorizes more.
4. For `BLOCKED`, preserve the current evidence, revert no unrelated user changes, update the owning design/status closure item if permitted, and continue only dependency-independent work.
5. Never downgrade a required database/runtime/UI/packaging/deployment check to a unit test because the target environment is unavailable.

## 5. Root verification and commit

After `ACCEPTED`, root must:

1. Inspect `git status --short` and the complete diff from `base_commit`; confirm it contains only allowed slice files.
2. Re-run the narrow critical verification that proves the accepted risk boundary. Use the reviewer’s verification only as corroboration, not the root’s evidence.
3. Stage only the accepted slice files; inspect the staged diff and status.
4. Create one English commit for the slice. Update its plan status to `Committed` and record the commit id and fresh verification.
5. Start only the next slice whose dependencies are now `Committed`.

## 6. Checkpoint and completion report

At every checkpoint report: Goal/stopping condition, slice id and status, base/commit ids, changed surfaces, independent verdict, root verification, external-evidence limits, blockers, and next ready slice. Complete the Goal only when the stated stopping condition has current evidence; do not claim UAT/production enablement while a named external gate remains open.
