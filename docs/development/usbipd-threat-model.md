# USB/IP device-operation threat model

## Scope

The Devices page discovers usbipd-win devices and can attach an already-shared device to a named WSL distribution. It never installs usbipd-win, Linux packages, udev rules, drivers, or Windows services.

## Trust boundaries

- `usbipd.exe` output is untrusted process output. The adapter applies output limits and accepts only validated bus IDs and known JSON/table rows.
- A bus ID is data, never command text. It is constrained to hexadecimal `N-N` form before it reaches a process request.
- A distribution name is validated for length and control characters. It is sent as a separate process argument.
- Attached hardware is visible to the WSL 2 VM, not isolated to an individual Linux distribution. The UI presents this warning before confirmation.
- Storage, serial, Android, Arduino, and smart-card devices can expose data or credentials. The UI gives device-class guidance and never changes Linux permission or middleware configuration.

## Privileged operations

Binding and unbinding change Windows host ownership and require the product-owned elevated helper. The desktop process never invokes `usbipd bind` or `usbipd unbind` as an administrator. The production broker resolves only the fixed packaged helper name, requires a valid Authenticode signature whose signer subject is exactly `CN=DistroNexus`, whose thumbprint equals the release-pinned `UsbElevatedHelperPublisherThumbprint`, and whose executable product identity is `DistroNexus.UsbElevatedHelper`; a merely valid signature from another publisher is rejected. Builds without a pinned thumbprint fail closed. Release packaging must sign the helper, inject the pinned thumbprint into Core's assembly metadata, and verify the resulting UAC identity.

The helper contract is protocol version 3. It accepts only a short-lived request containing an opaque preview token, allow-listed `Bind` or `Unbind` verb, validated bus ID, issuer ID, issue/expiry timestamps, and the caller's Windows SID. After UAC acceptance, the helper connects to a random `DistroNexus.Usb.*` pipe whose ACL permits only the initiating SID. Before it sends a hello or accepts a challenge, it obtains the connected pipe server PID from Windows, requires it to equal the desktop PID in the launch envelope, and verifies that process image as the signed, publisher-pinned `DistroNexus.Desktop` product. This prevents a same-user process which learns the pipe name or envelope from impersonating the desktop issuer. The desktop-side issuer validates issuer/token/SID/TTL and consumes the grant exactly once. It must not accept executable paths, raw command lines, arbitrary arguments, scripts, or detached confirmation state.

The elevated helper resolves `usbipd.exe` only under Program Files. It rejects PATH resolution, reparse/outside paths, files without the expected `usbipd-win` product identity, files not owned by an operating-system principal, signatures that fail `WinVerifyTrust`, and signers whose certificate thumbprint is not the release-pinned `UsbIpdPublisherThumbprint`. Builds without this pin fail closed. Release/UAT must verify the published vendor signer thumbprint and product metadata for every supported usbipd-win package.

## Parser compatibility contract

Only usbipd-win major versions 4 and 5 may mutate state. Both Core and PowerShell first request JSON and otherwise use the same table contract. Version 4 JSON fields are `busId`, `hardwareId`, `description`, `state`, and optional `client`/`distribution`; version 5 additionally accepts `bus_id`, `vidPid`, `device`, and `status`. Table rows require a validated `BUSID`, a four-hex-digit `VID:PID`, text description, and one of `Not shared`, `Shared`, `Attached`, or `Unknown`. Unknown versions may show parsed diagnostics but must disable mutation.

## Replay, TOCTOU, and recovery

Every action uses a single-use preview. Execution refreshes usbipd status and the device list, rejects absent or disconnected devices, and revalidates installation, service state, approved mutation version, and legal state transitions: `Available -> Bind -> Shared -> Attach -> Attached -> Detach -> Shared -> Unbind -> Available`. Attach verification is advisory: absence of `lsusb` does not cause package installation or a false success claim. Device notifications only request a refresh; disposal stops the watcher and removes event handlers.

## External acceptance gate

Physical-device bind/unbind, UAC helper signature/caller verification, usbipd service behavior, WSL attachment, Linux driver/udev behavior, and hardware recovery require controlled UAT on supported Windows and WSL versions. Default tests do not mutate a device or start a WSL distribution.
