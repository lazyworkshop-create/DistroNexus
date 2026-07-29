# Decision Record: Operational Bridge Versioning and Slice Boundaries

## Metadata

- Project and repository: DistroNexus; `D:/repo/lazyworkshop-create/DistroNexus`
- Date: 20260728
- Status: Accepted
- Owner: DistroNexus maintainers
- Related requirements/design/plan: `docs/specs/powershell-first-requirements.md` FR-001 through FR-007; `docs/specs/powershell-first-design.md`; `docs/development/powershell-first-slice-plan.md` S05

## Context

The existing systemd, recovery, and health command families call private, unversioned WorkspaceBridge operation identifiers. Network, firewall, extended recovery/health, and diagnostics operations have no equivalent fixed module and Bridge contract. A single implementation slice would combine incompatible trust boundaries: WSL systemd actions, Windows firewall elevation, recovery preview tokens, and diagnostic redaction.

## Decision

The public compatibility surface keeps existing cmdlet names. All new module calls use versioned, capability-specific Bridge identifiers. Existing unversioned private operation identifiers remain temporary aliases with identical typed behavior until no module caller or contract test needs them.

S05 is split into three vertical contract slices:

1. Systemd plus existing recovery/health routes, including versioned aliases and missing read/history/metadata operations.
2. Network and firewall inspection/configuration, retaining preview, collision, containment, and elevation-grant boundaries.
3. Diagnostic preview/export with redaction and preview-token protections.

Desktop consumer migration remains S07 and does not move ahead of the command contracts.

## Rationale

- Preserves public compatibility while meeting the versioned Bridge requirement.
- Keeps privileged and destructive boundaries independently reviewable.
- Prevents direct scripts or WPF calls from being mislabeled as module parity.

## Consequences

- Positive: every operational capability receives a fixed, typed execution path before WPF migration.
- Trade-off: legacy Bridge aliases require temporary duplicate contract coverage.
- Operational impact: real network/firewall/systemd mutations remain UAT evidence rather than repository-test actions.

## Alternatives Considered

1. Migrate every operational capability in one S05 commit. Rejected because the scope cannot receive credible focused acceptance.
2. Treat existing unversioned Bridge operations as compliant. Rejected because FR-006 requires versioned private contracts.

## Follow-Up Actions

- Replace S05 in the slice plan with the three accepted vertical slices and exact verification scopes.
- Implement and independently accept one slice at a time.

## Evidence References

- `src/Client/DistroNexus.WorkspaceBridge/Program.cs`
- `src/PowerShell/Public/RecoveryPointCommands.ps1`
- `src/PowerShell/Public/HealthCenterCommands.ps1`
- `src/Client/DistroNexus.Core/Interfaces/ISystemdNetworkServices.cs`
- `src/Client/DistroNexus.Core/Interfaces/IRecoveryPointService.cs`
- `src/Client/DistroNexus.Core/Interfaces/IHealthServices.cs`
