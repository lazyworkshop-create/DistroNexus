# Progress Log

Date: 2026-02-19

## Active Milestone
- Download progress/speed implementation verification and checklist closure

## Progress
- Added `DownloadServiceTests` to validate byte-level progress reporting, unknown content length behavior, and empty file handling.
- Added `DownloadTaskManagerProgressTests` to validate speed calculation, throttle interval behavior, and stalled transfer speed reporting.
- Added `FileSizeFormatterTests` to validate formatting outputs (`1024`, `1048576`, `0`).
- Added `PackageManagerUiAutomationTests` for Package Manager page navigation and download start/during/completion UI flow.
- Added deterministic UI automation support via `DISTRONEXUS_UI_AUTOMATION_FAKE_DOWNLOAD` and control automation IDs for Package Manager download controls.
- Executed targeted tests and full regression suite.
- Updated checklist documents with evidence-based completion states.

---

Date: 2026-02-19

## Active Milestone
- Package Manager download UX refinement (progress alignment and same-file version merge)

## Progress
- Updated package item action layout so the downloading progress block remains vertically centered.
- Added a fixed spacer column to keep downloading progress visuals from touching the right-side cancel button.
- Added same-file merge display metadata to `DistroPackage` (`IsSameFileMerged`, `SameFileTagText`).
- Implemented same-file package variant merge in `PackageManagerViewModel` grouping flow using SHA256/file-name/size key fallback strategy.
- Added merged-item visual tag (`Same file ×N`) in Package Manager list cards.
- Verified changes with `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj --nologo` (`211 passed, 0 failed`).

---

Date: 2026-02-19

## Active Milestone
- Template Manager to Install Wizard flow improvement requirements definition

## Progress
- Completed focused analysis for Template Manager entry into install wizard, including startup intent propagation and template selection consistency.
- Identified UX and workflow gaps around duplicated template decision points and review-stage visibility.
- Created a new requirements specification for implementation planning: `docs/specs/template-manager-install-wizard-improvement-requirements.md`.
- Documented phased priorities (P0/P1), acceptance criteria, and compatibility constraints for safe incremental implementation.
