# Decision Record: PowerShell-First Product Execution Boundary

## Metadata

- Project and repository: DistroNexus; `D:/repo/lazyworkshop-create/DistroNexus`
- Date: 20260728
- Status: Accepted
- Owner: Product direction and repository maintainers
- Related requirements/design/plan: `docs/specs/powershell-first-requirements.md`; `docs/specs/powershell-first-design.md`; `docs/development/powershell-first-slice-plan.md`

## Context

The product has both a PowerShell module and a WPF application, but the WPF client can directly call Core business services while the module mixes direct scripts with a partial WorkspaceBridge facade. This creates two product behavior paths, incomplete automation parity, and a weakly enforceable boundary.

## Decision

The PowerShell module is the sole supported product execution boundary. WPF is one presentation client of that module and cannot directly execute DistroNexus business services or host-I/O operations. Core and WorkspaceBridge remain internal capability-specific implementations selected by the module; they are not public automation or desktop boundaries.

## Rationale

- The explicit product requirement is that every feature is implemented through the PowerShell module.
- A single public grammar gives automation and WPF the same validation, consent, result, and error behavior.
- Core already contains mature safety and recovery algorithms that can remain private rather than being duplicated in scripts.

## Consequences

- Positive: feature parity becomes testable, automation becomes first-class, and WPF cannot drift from command behavior.
- Trade-off: bridge operations, typed desktop client contracts, and tests must be expanded before direct WPF service references can be removed.
- Operational impact: packaged bridge availability becomes command-family-specific; real-host UAT is still required for privileged and WSL-dependent operations.

### Global WSL configuration boundary amendment

The lossless `.wslconfig` document, fingerprint, comments, unknown keys, backup location and atomic write algorithm remain Core-owned implementation state. The supported public contract is limited to modeled schema fields, constrained preview and opaque execution token. This preserves configuration fidelity without making WPF or a cmdlet a generic INI/file editor. Compatibility facades may translate the legacy five global fields only into the restricted preview/execute path.

### USB elevation boundary amendment

For USB Bind/Unbind, the module cannot be made a trusted elevated-helper caller merely by accepting `powershell.exe` or `dotnet.exe`. The product therefore uses a dedicated, signed, unelevated `DistroNexus.UsbElevationBroker` as the only additional trusted pipe server alongside the signed Desktop executable. The broker consumes a same-user, short-lived Core grant and preserves the existing helper SID, PID, nonce, proof, trusted-`usbipd` and post-state checks. This adds a narrowly auditable product identity without granting generic scripts a privileged channel. Packaging/signing and physical-device/UAC evidence remain release/UAT gates.

## Alternatives Considered

1. Keep the module as a thin optional adapter while WPF calls Core directly. Rejected because it fails the explicit PowerShell-first requirement and preserves duplicate behavior paths.
2. Rewrite all Core algorithms in PowerShell. Rejected because it would duplicate validated security/recovery logic and introduce unnecessary risk; the module boundary, not script-language duplication, is the required product contract.

## Follow-Up Actions

- Establish command/manifest integrity and a structural Desktop boundary guard.
- Migrate every capability family and WPF consumer through typed module operations.
- Record real-host UAT evidence before a production readiness claim.

## Evidence References

- `docs/development/v2.3.0-architecture.md`
- `docs/specs/v2.3.0-requirements.md`
- `src/PowerShell/DistroNexus.psd1`
- `src/PowerShell/DistroNexus.psm1`
- `src/Client/DistroNexus.WorkspaceBridge/Program.cs`
- `src/Client/DistroNexus.Desktop/ViewModels/WslInstanceViewModel.cs`
