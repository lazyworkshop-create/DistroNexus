# Acceptance Gates

The reviewer evaluates the complete diff from the slice base commit. Each applicable gate needs current evidence.

| Gate | Reviewer checks | Acceptance evidence |
| --- | --- | --- |
| Scope | Only allowed paths changed; exclusions and unrelated worktree changes remain untouched. | `git diff <base>` and status. |
| Traceability | Every changed behavior maps to requirement/design; every required criterion maps to code, test, or owned closure item. | Criteria evidence matrix. |
| Contract | DTO/API/event/config/schema changes define validation, compatibility, error behavior, and consumers. | Current docs, code, and tests. |
| Boundaries | Repository architecture and ownership remain intact; no forbidden runtime/archive dependency. | Project/code inspection and architecture checks. |
| State and security | Authorization, scope, audit, transactions, idempotency, concurrency, retry, and recovery are deliberate where relevant. | Negative/boundary tests and design alignment. |
| Tests | Tests exercise meaningful behavior and appropriate failure cases rather than implementation detail only. | Test diff and fresh results. |
| Verification | Commands prove the actual changed boundary, not merely compilation. | Exact current command results. |
| Operational evidence | Database/runtime/UI/packaging/release proof exists when the risk cannot be established locally. | Current trace, acceptance record, or explicit blocker. |
| Documentation | Requirements, design, decisions, status, and plans changed only within their authority. | Document diff. |
| Commit readiness | Diff is cohesive, reviewable, and one slice can be committed without unrelated work. | Complete diff inspection. |

Return `ACCEPTED` only when no material finding remains. Return `REWORK_REQUIRED` when an in-scope repository change can resolve a material finding. Return `BLOCKED` when a decision, authority, or external evidence is missing. Every non-accepted result includes the affected gate, exact evidence, impact, and smallest next action.
