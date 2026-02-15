# Standalone to Store Settings Migration

Date: 2026-02-15

## Current Behavior
- Both standalone and Store versions use the same settings root under `AppData\\Roaming\\DistroNexus`.
- Existing user settings (`settings.json`), local catalog cache (`catalog.json`), and templates cache (`templates.json`) remain reusable after Store installation.

## User Guidance
1. Close standalone DistroNexus.
2. Install Store version.
3. Launch Store version and verify:
   - Existing settings are loaded.
   - Templates and catalog cache are visible.
4. If startup configuration appears missing, back up and restore files from `AppData\\Roaming\\DistroNexus`.

## Validation Evidence
- Path resolution logic in core services now probes packaged layout and parent fallback paths.
- Unit test coverage added for packaged/development path discovery.