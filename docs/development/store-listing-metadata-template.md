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