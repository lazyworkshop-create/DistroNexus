# Decision Record: Template Apply Recovery and Execution Boundary

## Metadata

- Project and repository: DistroNexus; `D:\repo\lazyworkshop-create\DistroNexus`
- Date: 2026-07-29
- Status: Accepted
- Owner: DistroNexus maintainers
- Related requirements/design/plan: template module requirements/design; S41.

## Context

Template application currently bypasses the module, accepts mutable identifiers/variables and exposes only UI-local cancellation. A recovery offer exists but current UX allows an explicit decline.

## Decision

Use a Core-issued preview/execute/status/cancel contract with immutable provenance binding and a durable worker. Preserve only the existing executable choice: explicit recovery-offer decline. A preview without that consent returns the Core `RecoveryOffer` and no execution token; after the user confirms decline, a fresh preview binds and records the offer availability, instance, reason, message key and decline decision. Warn the user and never claim automatic rollback.

## Rationale

- Preserves existing user choice while making the risky choice explicit and auditable.
- Prevents changed template/artifact/variables from executing after review.
- Provides truthful cancellation across short-lived module processes.

## Consequences

- Positive: application has one module-owned, tokenized authority boundary.
- Trade-off: applying a template without a recovery point remains possible by explicit user choice; the current UI has no implementation that selects, creates, or executes a recovery point.
- Operational impact: live recovery/cancellation behavior requires WSL UAT.

## Alternatives Considered

1. Require automatic recovery creation before every apply; rejected because no current reliable cross-environment creation contract exists and it changes existing UX.

## Follow-Up Actions

- Deliver S41 and run disposable-instance recovery/cancel UAT.

## Evidence References

- `src/Client/DistroNexus.Desktop/Wizard/Steps/TemplateApplyStep.cs`
- `src/Client/DistroNexus.Core/Services/TemplateService.cs`
