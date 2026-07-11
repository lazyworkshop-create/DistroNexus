---
name: agentteam-capability-delivery
description: Deliver one coherent capability family from repository evidence through coding-ready design, implementation, meaningful tests, fresh verification, and a reviewable commit. Use when adding, extending, aligning, or fixing a bounded domain or service capability with several runtime surfaces. Do not use for generic project planning, report analysis, or release-only work.
---

# AgentTeam Capability Delivery

## Scope and evidence

1. Read `AGENTS.md`, inspect `git status --short`, and name the capability, outcome, allowed environments, and exclusions.
2. Discover current documents, code, tests, and runtime assets. If the repository has no delivery profile, create one from `agentteam-slice-delivery/references/repository-profile-template.md`; fill placeholders only from evidence and retain unknown values as `TODO`.
3. Trace new write workflows to at least one data source and one behavior source when relevant. Record missing or contradictory evidence as an explicit closure item.
4. Use `agentteam-evidence-explorer` only for explicitly delegated, independent read-only evidence mapping.

## Design and implementation

Ensure the owning design covers contracts, validation, authorization, routing/scope, errors, transactions/concurrency where relevant, audit/observability, and targeted verification. Preserve architecture boundaries and keep entry points thin. Keep companion surfaces aligned when they exist; document intentional asymmetry. Add meaningful success, failure, and boundary tests. Use the narrowest fresh check that proves the real risk, including real database/runtime/UI/packaging acceptance where unit tests are insufficient. Diagnose unexpected failures before expanding the diff.

## Finish

Inspect the full diff against the capability boundary. In parent-managed mode return changed files, evidence, verification, limits, and closure items without staging or committing. Otherwise stage only the capability family and create one reviewable commit. Keep release and environment mutation as separate, explicitly authorized work.
