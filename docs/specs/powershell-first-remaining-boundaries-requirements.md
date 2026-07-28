# Remaining PowerShell-First Boundary Requirements

## Purpose

Finish the remaining migration from Desktop-owned product execution to the PowerShell module. The WPF client remains responsible only for input collection, navigation, rendering, and user-initiated presentation actions.

## Project Context

- Project and repository: DistroNexus; `D:/repo/lazyworkshop-create/DistroNexus`.
- Parent authority: `docs/specs/powershell-first-requirements.md` FR-001 through FR-007 and `docs/architecture/powershell-first-decision.md`.
- Permitted work: repository code, tests, documentation, commits, and the existing PR. Live WSL, USB device, UAC, package installation, signing, publishing, and deployment remain excluded.

## Scope

- In scope: the remaining direct Desktop product-service, product-state, download-job, platform-update, USB, instance-configuration, installation-preflight, and diagnostic paths identified by the current boundary inventory.
- Out of scope: visual dialogs, navigation, clipboard, file/folder picker selection, shell/browser launch of an already-returned display-safe result, shortcut rendering, and product-independent logging bootstrap.
- Compatibility: existing public command names remain facades where possible; new commands expose closed typed records and never arbitrary command text, paths, process arguments, or bridge operation identifiers.

## Actors and Trust Boundaries

| Actor/component | Trust level | Permitted responsibility |
| --- | --- | --- |
| PowerShell module | Product execution boundary | Validate public input, consent, invoke fixed internal capability routes, and return typed sanitized results. |
| Core/WorkspaceBridge | Internal trusted implementation | Own product state, authorization, durable operations, validation, and fixed host adapters. |
| WPF Desktop | Presentation client | Collect input, show consent/UI, invoke typed module methods, and render results. |
| External host/USB/network/update endpoints | Environment-owned | Receive only Core-selected fixed requests; outcomes remain sanitized. |

## Functional Requirements

### FR-101 Bootstrap, global settings, update, and compliance boundary

Desktop must load product settings, compliance state, and update status through fixed module operations after a deterministic product-owned module bootstrap. It must not deserialize product settings, query Core update/compliance services, or construct an update request itself.

`GlobalSettings.PowerShellModulePath` is retired as an execution selector. A persisted legacy value is ignored during bootstrap and cleared by the next successful module settings save; a new nonempty value is rejected with the stable `Settings.ModulePathRetired` error. It remains null-only in compatible read results until the next schema revision removes it.

Acceptance: no Desktop product-settings read/write or direct `ISettingsService`, `IStoreComplianceModeService`, or `IUpdateService` execution remains; product-owned module lookup failure has the stable `DistroNexus.ModuleBootstrapUnavailable` error; opening a module-returned validated HTTPS release URI remains a UI-only action.

### FR-102 Package cache, catalog source, and download-job parity

The module must provide fixed typed read/mutation operations for package cache location, custom source mutation, and download-job start/list/progress/cancel/retry/clear actions. Starting a job accepts only an allow-listed package identifier; it never accepts a URL, destination path, command text, process handle, or delegate. Desktop must not own download task state, handlers, or product cache state.

Acceptance: package/download presentation uses only typed module methods; start/cancel/retry/clear use reviewed fixed identifiers or opaque reviewed tokens and no caller-provided host command or path; status polling has bounded cancellation and disposal.

### FR-103 USB module-only capability

USB status, list, refresh, preview, execute, and notification behavior must be available through fixed typed module routes. Desktop may render bounded snapshots or poll a typed status method, but cannot use `IUsbDeviceService` or an `IUsbDeviceChangeWatcher`.

Acceptance: the broker-free status/list subset uses only fixed typed reads with bounded, sanitized results and cancellable visible-lifetime polling; no USB Core service/watcher reference remains in Desktop. Bind/unbind retains the accepted signed-broker and same-user grant design; absent authorized signing/packaging evidence remains an explicit blocker rather than a trust relaxation. The read subset must not expose an action token, device path, native command, raw diagnostic, or elevation capability.

### FR-104 Instance configuration module parity

Per-instance configuration reads, recovery-offer discovery, preview, and save must use a versioned typed module contract. Desktop must not read/write `wsl.conf`, create recovery state, or call `IDistributionConfigurationService`.

Acceptance: read and recovery results use bounded modeled records and stable outcome codes; preview returns a same-user, short-lived single-use token bound to instance identity, schema revision, canonical modeled changes, configuration fingerprint, and recovery-offer fingerprint; execute accepts only the token and revalidates state; WPF has no direct configuration service dependency. Recovery-offer discovery is read-only; backup/recovery creation remains Core-owned only when save execution requires it.

### FR-105 Install-target preflight ownership

Install-root validation, capacity checks, and all product-path mutations belong to the module/Core preview contract. Desktop may select a candidate path but must not create/delete directories, validate for write, or treat a drive check as authoritative.

Acceptance: both install presentation paths submit the selected path only to the typed target-preview operation; the resulting short-lived token, rather than a raw path, is the only target input to typed verified-install preview/execute. No Desktop `Directory.CreateDirectory`, cleanup, or `DriveInfo` path-preflight determines install eligibility. Existing public `-InstallRoot` parameter sets remain compatibility facades that perform the authoritative module preflight internally; they do not create a raw-path Bridge execute route.

### FR-106 Diagnostic module replacement

Every Desktop-visible diagnostic must use a fixed typed module snapshot, preview, or report operation. It must not call `IPowerShellService` directly to obtain raw product diagnostic information.

Acceptance: the raw diagnostic route is replaced by a bounded, redacted, cancellable typed snapshot with stable outcomes; Desktop contains no direct `IPowerShellService` diagnostic execution. A snapshot contains modeled readiness/status/notices only, never a module path, product path, host command, raw exception, or environment dump.

### FR-107 Enforced Desktop boundary and composition cleanup

Desktop must not register, resolve, or retain Core business-service interfaces unless they are a documented composition-only transport dependency. Structural tests must reject direct Core product service, product-state host-I/O, raw process execution, and bridge-protocol access from Desktop.

Acceptance: each supported Desktop business operation maps to one typed `IPowerShellModuleClient` method and exported command family; stale constructor dependencies and DI registrations are removed; the inventory documents and tests every remaining exception. Permitted exceptions are WPF rendering/navigation/dialog/clipboard, picker selection without product parsing, browser/Explorer launch of a module-returned display-safe target, and composition-only construction of `IPowerShellService` for `PowerShellModuleClient`. Direct product settings file reads, module imports, raw diagnostic execution, Core business-service resolution, product-directory creation, and raw process execution are not exceptions.

## Non-Functional Requirements

- Security/authorization: closed versioned requests; no generic dispatch; mutation consent and existing grants remain mandatory; no secrets or sensitive paths in public results.
- Reliability/recovery: preview grants are same-user, bounded, single-use, atomically consumed, and state-bound. Polling and cancellation are bounded.
- Compatibility: retain existing public commands as narrow facades when their behavior can safely map to a new contract.
- Operations: USB physical-device/UAC, update publication, package network transfer, and live WSL configuration remain external UAT evidence.

## Acceptance Criteria

- The current boundary inventory has no unexplained Desktop direct product-service or product-state operation.
- Each remaining capability family has exported PowerShell commands, closed module-client methods, and negative/consent/routing tests.
- The global Desktop boundary test forbids Core product interfaces and host-I/O outside the documented UI-only exception list.
- Full repository verification and external UAT evidence are reported separately.

## Open Decisions and External Inputs

| Item | Impact | Owner | Smallest next action |
| --- | --- | --- | --- |
| Signed USB broker packaging/publisher pin | Blocks bind/unbind migration; no safe fallback exists. | Release/security owner | Authorize and provide the pinned broker signing/package contract. |
| Physical USB/UAC, live WSL/configuration, package/update UAT | Cannot be proven by repository tests. | Release/UAT owner | Execute disposable-host scenarios after repository slices pass. |

## Source Evidence

| Area | Source | What it confirms | Confidence |
| --- | --- | --- | --- |
| Current Desktop inventory | `src/Client/DistroNexus.Desktop`, `IPowerShellModuleClient`, manifest, and architecture tests | Remaining direct capability clusters and absent typed contracts. | Confirmed |
| Parent boundary authority | `docs/specs/powershell-first-requirements.md`, `docs/architecture/powershell-first-decision.md` | Module is the only product execution boundary. | Confirmed |
| USB trust constraint | `docs/development/powershell-first-slice-plan.md` S25 | Unsigned/generic process identity cannot safely invoke the elevated helper. | Confirmed |
