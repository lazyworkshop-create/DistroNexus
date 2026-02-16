# Store Listing Metadata Template

Date: 2026-02-15
Source: `docs/specs/store-publish-analysis.md`

## Category
- Primary: Utilities & tools
- Subcategory: Developer tools

## Description (Long, ≤ 10,000 chars)
DistroNexus is a Windows desktop manager for Windows Subsystem for Linux (WSL).
It helps developers discover distributions, install instances, and apply reusable environment templates for common development scenarios.

Key capabilities:
- Discover and manage WSL distributions from one desktop UI.
- Apply built-in templates for language/runtime stacks and development workflows.
- Run local WSL provisioning operations with transparent progress and logs.
- Work offline for local management after initial setup.

Important prerequisites:
- Windows 10/11 with WSL feature enabled.
- Administrator permission may be required for selected operations.

## Short Description (Recommended, ≤ 270 chars)
Windows desktop WSL manager for installing distros and applying reusable development templates with a guided workflow.

## What's New (Per Submission)
- Added Microsoft Store packaging pipeline (`.msixbundle` / `.msixupload`).
- Added Store identity-aligned manifest and `runFullTrust` declaration.
- Improved packaged path compatibility for config/templates/PowerShell module resolution.

## Support Information
- Support URL: https://github.com/lazyworkshop-create/DistroNexus/issues
- Support Email: lazyworkshop.deron@gmail.com
- Privacy Policy URL: https://lazyworkshop-create.github.io/DistroNexus/docs/privacy-policy

## Store Listing Assets (Copy-Ready, Partner Center)

### 1) Quick Submit (Windows Desktop Minimum)
Use this block for the fastest valid submission path.

1. Desktop screenshot 1
  - File: `<desktop-screenshot-01.png>`
  - Caption: Main dashboard for browsing and managing WSL distributions with status visibility and quick actions.
2. Desktop screenshot 2
  - File: `<desktop-screenshot-02.png>`
  - Caption: Package and instance management workflow showing guided install operations and progress feedback.
3. Desktop screenshot 3
  - File: `<desktop-screenshot-03.png>`
  - Caption: Template library view for applying reusable development environment templates to WSL instances.
4. App tile icon (recommended override)
  - Slot: 1:1 App tile icon (300 x 300)
  - File: `<store-logo-300x300.png>`
5. Short title
  - DistroNexus
6. Short description
  - Windows desktop WSL manager for installing distros and applying reusable development templates with a guided workflow.
7. Keywords
  - WSL; Windows Subsystem for Linux; Linux distro manager; developer tools; template automation; local dev environment; Windows utilities
8. Privacy policy URL
  - https://lazyworkshop-create.github.io/DistroNexus/docs/privacy-policy
9. Support email
  - lazyworkshop.deron@gmail.com

### 2) Full Copy-Ready (Page Order, includes already-filled fields)
Use this block on **Store listing - English (United States)** from top to bottom.

1. Product name
  - DistroNexus
2. Description
  - DistroNexus is a Windows desktop manager for Windows Subsystem for Linux (WSL).
  - It helps developers discover distributions, install instances, and apply reusable environment templates for common development scenarios.
  -
  - Key capabilities:
  - Discover and manage WSL distributions from one desktop UI.
  - Apply built-in templates for language/runtime stacks and development workflows.
  - Run local WSL provisioning operations with transparent progress and logs.
  - Work offline for local management after initial setup.
  -
  - Important prerequisites:
  - Windows 10/11 with WSL feature enabled.
  - Administrator permission may be required for selected operations.
3. What's new in this version
  - Added Microsoft Store packaging pipeline (`.msixbundle` / `.msixupload`).
  - Added Store identity-aligned manifest and `runFullTrust` declaration.
  - Improved packaged path compatibility for config/templates/PowerShell module resolution.
4. Product feature 1
  - Windows desktop WSL manager for installing distros and applying reusable development templates with a guided workflow.
5. Product feature 2
  - Discover and manage WSL distributions from one desktop UI.
6. Product feature 3
  - Apply built-in templates for language/runtime stacks and development workflows.
7. Desktop screenshot 1
  - File: `<desktop-screenshot-01.png>`
  - Caption: Main dashboard for browsing and managing WSL distributions with status visibility and quick actions.
8. Desktop screenshot 2
  - File: `<desktop-screenshot-02.png>`
  - Caption: Package and instance management workflow showing guided install operations and progress feedback.
9. Desktop screenshot 3
  - File: `<desktop-screenshot-03.png>`
  - Caption: Template library view for applying reusable development environment templates to WSL instances.
10. App tile icon 300x300 (recommended override)
  - File: `<store-logo-300x300.png>`
11. Short title
  - DistroNexus
12. Voice title
  - DistroNexus
13. Short description
  - Windows desktop WSL manager for installing distros and applying reusable development templates with a guided workflow.
14. Keywords
  - WSL; Windows Subsystem for Linux; Linux distro manager; developer tools; template automation; local dev environment; Windows utilities
15. Copyright and trademark info
  - DistroNexus © LazyWorkshop Create. All product names, logos, and brands are property of their respective owners.
16. Additional license terms
  - This application is provided "as is" without warranties. Users are responsible for system-level changes they initiate (for example WSL instance creation, import, or removal).
17. Developed by
  - LazyWorkshop Create
18. Privacy policy URL
  - https://lazyworkshop-create.github.io/DistroNexus/docs/privacy-policy
19. Support URL
  - https://github.com/lazyworkshop-create/DistroNexus/issues
20. Support email
  - lazyworkshop.deron@gmail.com

### 3) Asset Specs Reference
Desktop screenshots:
- At least 1 required.
- Recommended: 1366 x 768 or higher.
- 4K supported: 3840 x 2160.
- PNG only, landscape or portrait.
- Up to 30 files, each <= 50 MB.

Store logos (Windows 10/11 listing override):
- Store can use package logos by default.
- Use override logos only when needed.
- PNG only, each <= 5 MB.
- Suggested placeholders:
  - 300 x 300: `<store-logo-300x300.png>`
  - 150 x 150: `<store-logo-150x150.png>`
  - 71 x 71: `<store-logo-71x71.png>`

Optional trailer and hero image:
- Trailer is optional.
- If trailer is shown at top, add 16:9 super hero art.
- Placeholder: `<super-hero-16x9.png>` (1920 x 1080 or 3840 x 2160, PNG, <= 50 MB).

Optional Xbox-only assets (skip for Windows desktop-only submission):
- `<xbox-poster-720x1080.png>`
- `<xbox-box-1080x1080.png>`
- `<xbox-key-art-584x800.png>`
- `<xbox-titled-hero-1920x1080.png>`
- `<xbox-featured-1080x1080.png>`

## Privacy Policy Text (Copy-Ready)
Effective Date: 2026-02-16

DistroNexus is a local-first Windows application for managing Windows Subsystem for Linux (WSL) distributions and applying development templates. Most operations are executed on the user device.

Data we process:
- Local app settings and preferences under `AppData\\Roaming\\DistroNexus`.
- Local cache files used for catalog/template loading and offline usability.
- User-triggered operational data required to run WSL management commands.
- Local command output and logs.

How we use data:
- Provide requested WSL management and template automation features.
- Save user preferences and improve local usability.
- Show execution progress and diagnostics.

Network and sharing:
- The app may access configured network endpoints for catalog/template downloads.
- The app does not require account sign-in for core local workflows.
- The app does not include in-app purchases.
- DistroNexus does not intentionally sell personal data.

Retention and deletion:
- Local settings/cache/log data remains on the device until removed by the user.
- Users can delete local data manually or by uninstalling the app.

Privacy contact: lazyworkshop.deron@gmail.com

Publication note: Host the full privacy policy at a public HTTPS URL and submit that URL in Partner Center.

## Certification Notes (Draft)
- This app requires `runFullTrust` to invoke local Windows/WSL tooling (`wsl.exe`) for user-requested operations.
- No in-app purchases are used.
- Core validation scenarios:
  1. Launch app and load catalog/templates.
  2. Trigger WSL instance operations through guided workflow.
  3. Verify offline startup and local template-driven operations.

### Restricted Capability Justification (runFullTrust, Copy-Ready)
DistroNexus requires the `runFullTrust` capability because it is a desktop WSL management tool that must invoke local Windows and WSL command-line components (including `wsl.exe`) to perform user-requested operations such as listing distributions, creating/importing/removing instances, and applying setup templates. These operations cannot be completed with restricted app-container permissions alone. The capability is used only for explicit, user-initiated management actions and local process execution on the user’s device. DistroNexus does not use this capability for background surveillance, privilege escalation, or hidden remote control. No in-app purchases are used. User data (settings/cache/logs) is stored locally, and network access is limited to configured catalog/template endpoints required for product functionality.

### Additional Testing Information (Copy-Ready)
Paste this into **Supplemental info -> Additional Testing Information -> Description**:

DistroNexus is a desktop WSL management tool.
This app uses runFullTrust to invoke local Windows/WSL tooling (including wsl.exe) for user-initiated operations only.

Prerequisites:
1) Windows 10/11 with WSL enabled.
2) No account sign-in required.
3) No special credentials required.

Validation steps:
1) Launch DistroNexus.
2) Open distribution/catalog view and verify data loads.
3) Open template view and verify templates are listed.
4) Start a user-initiated WSL management action (for example list/import/create/remove flow) and verify progress/log output.
5) Close and relaunch the app; verify settings/cache are still available.
6) Optional: disconnect network and verify core local operations still work.

Notes:
- No in-app purchases.
- User data is stored locally (settings/cache/logs).
- Network access is only used for configured catalog/template endpoints.

Credentials section:
- Leave empty if no sign-in is required.