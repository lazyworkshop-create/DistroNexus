# Codex Bootstrap Prompt

Copy the following prompt into Codex after opening the target repository:

```text
Read the target repository's AGENTS.md first, then read this CodexCodingAgentTeam asset pack's AGENTS.md and templates. The target repository's rules take precedence.

First fill the `Target Project Configuration` section in AGENTS.md: project identity, repository root/branch, source/test/config/release roots, architecture boundaries, exact build/test/acceptance commands, allowed environments, release/rollback entry points, and sensitive-data policy. Inspect current repository instructions, docs, code, tests, scripts, configuration, and data/schema evidence before filling every {{PLACEHOLDER}}.

Then create or update an evidence-backed repository delivery profile from templates/repository-delivery-profile.template.md in the target repository's normal documentation location. Its project-topology paths and commands must agree with AGENTS.md.

Replace a placeholder only when current evidence supports it. For an unknown or contradictory fact, retain it as TODO: <evidence needed>, record the smallest closure action, and do not guess.

For a medium or large request, first create separate requirements from templates/requirements.template.md. Include problem/outcome, scope and non-goals, actors/trust boundaries, numbered functional requirements, non-functional constraints, binary acceptance criteria, source evidence, and explicit open decisions. Do not write technical implementation choices as requirements unless they are already accepted constraints.

Then create a coding-ready technical design from templates/technical-design.template.md. Map every material requirement to design and verification. Define component ownership, data/request flow, contracts, validation, authorization, scope/routing, errors, compatibility, audit, state/transaction/idempotency/concurrency behavior where relevant, failure/recovery, security, operations, and the exact verification strategy.

When an accepted product or technical choice is needed, create a decision record from templates/decision-record.template.md. When an API, event, configuration, or integration contract is material, create or update templates/interface-contract.template.md. Treat legacy material as parity evidence only until its conclusion is promoted into active requirements, design, decision, plan, or status records.

Do not start implementation or slice planning until requirements and design are both evidence-backed, separately written, and free of material unresolved TODOs. If a product decision, authority, or external environment is missing, record it as a blocking closure item and give the smallest next action.

Only then create an implementation slice plan from templates/implementation-slice-plan.template.md. Make each slice vertically complete, provide allowed and excluded paths, binary acceptance criteria, fresh verification commands, a single commit boundary, and a final closure slice for UAT/infrastructure/release evidence when repository tests cannot prove it. Do not delegate or implement slices with unresolved material contract, permission, or evidence gaps.

For a release-readiness request, complete templates/release-evidence-record.template.md. Distinguish Repository ready, UAT ready, and Production ready; missing environment evidence is Blocked. Do not deploy or otherwise mutate external systems unless explicit authority is supplied.

Return the profile, requirements, technical design, and plan paths; the evidence matrix; confirmed facts; explicit TODOs; readiness result; proposed slice order; and the next safe action. Do not deploy, publish, change databases, or mutate external systems unless the user explicitly authorizes it.
```
