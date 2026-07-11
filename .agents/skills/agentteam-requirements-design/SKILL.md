---
name: agentteam-requirements-design
description: Clarify a medium or large software change from repository evidence and produce separate, traceable requirements and coding-ready technical design artifacts. Use before implementation or slice planning when requirements are incomplete, design decisions are implicit, existing behavior must be investigated, or the user asks for requirements clarification, technical design, or a docs-first delivery baseline. Do not use for a small self-evident edit or to implement before the design gate is satisfied.
---

# AgentTeam Requirements and Design

Create an executable baseline in this order: clarify -> requirements -> technical design -> design-readiness gate -> slice handoff. The target repository's `AGENTS.md` and document ownership rules override this skill.

## 1. Establish authority and evidence

1. Read `AGENTS.md`, inspect `git status --short`, and identify the target documentation location and naming rules.
2. State the user outcome, explicit exclusions, operational permissions, and the decision that this work must enable.
3. Build an evidence map before writing conclusions. Inspect current implementation, tests, configuration, schemas or data sources, runtime behavior, accepted decisions, and external primary sources when needed.
4. Classify every conclusion as `confirmed`, `inferred`, `decision required`, or `external validation required`. Never elevate archived code, a mock, or an old document into current authority without confirming it.
5. Delegate only independent read-only evidence lanes to `agentteam-evidence-explorer`; the root reconciles contradictions.

Read [evidence-and-clarification.md](references/evidence-and-clarification.md) for the required evidence matrix and question policy, and [evidence-policy.md](references/evidence-policy.md) for source classification, stop conditions, and promotion rules.

## 2. Write requirements, not a design disguised as requirements

1. Start from [requirements-template.md](references/requirements-template.md) unless the repository supplies a stronger template.
2. Define problem, goals, non-goals, actors/trust boundaries, numbered functional requirements, non-functional requirements, acceptance criteria, source evidence, and open decisions.
3. Make requirements observable and testable. Assign stable ids such as `FR-001`; include authorization, tenancy/scope, data ownership, compatibility, retention, and operational constraints when relevant.
4. Keep implementation mechanisms out of requirements unless they are accepted constraints. Record unresolved choices instead of silently selecting them.

## 3. Produce a coding-ready technical design

1. Start from [technical-design-template.md](references/technical-design-template.md) unless the repository supplies a stronger template.
2. Map every material requirement to a design section or an explicit closure item. Describe component and request/data flow, module ownership, contracts, validation, authorization, errors, audit, state/transactions/concurrency, configuration, observability, dependencies, security controls, and verification.
3. Keep requirements and design separate: requirements state what and why; design states how the repository will satisfy approved requirements.
4. Record intentional compatibility behavior, runtime asymmetry, and release-time or external evidence boundaries explicitly.

## 4. Gate and hand off

1. Run `scripts/validate-design-readiness.ps1 -RequirementsPath <requirements> -DesignPath <design>`.
2. Resolve material `TODO`, contradiction, missing contract, missing authority, or untestable acceptance gap before implementation. If the gap needs a product choice or external environment, stop with a concrete closure item.
3. When the gate passes, invoke `$agentteam-slice-delivery` with the requirements, design, evidence map, decisions, and explicit exclusions. Slice only approved, coding-ready scope.

## Deliverable

Return document paths, evidence used, confirmed requirements, non-goals, accepted design decisions, unresolved closure items, readiness result, and the exact next safe action. Do not claim implementation readiness if the gate failed.
