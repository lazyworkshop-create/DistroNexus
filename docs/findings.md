# Findings Log

Date: 2026-02-19

## Active Milestone
- Download progress/speed implementation verification and checklist closure

## Findings
- `Progress<T>` in tests can cause asynchronous callback timing issues; deterministic assertions should use a synchronous `IProgress<T>` implementation.
- Speed-to-zero assertion requires at least one baseline update interval and a subsequent stalled interval report; otherwise previous non-zero speed remains.
- Full regression status: `211 passed, 0 failed` on `DistroNexus.Tests`.
- Remaining unchecked acceptance items are manual/runtime UX validations (unknown-size indeterminate UI and visual/behavioral checks requiring interactive run).
- FlaUI UI automation requires serialized execution; collection-level `DisableParallelization = true` avoids COM startup contention.
- Package list and toolbar shared "Download" labels can cause ambiguous targeting; `AutomationProperties.AutomationId` is required for stable UI element selection.
- Deterministic UI download-flow automation is achieved with env-gated fake download mode (`DISTRONEXUS_UI_AUTOMATION_FAKE_DOWNLOAD=1`) that does not affect normal runtime.

---

Date: 2026-02-19

## Active Milestone
- Package Manager download UX refinement (progress alignment and same-file version merge)

## Findings
- Same-file merge should prefer currently-downloading or cached entry as representative to avoid hiding active state.
- A reliable same-file key requires layered fallback: SHA256 first, then file name + size, then URL, then package ID.
- Performing same-file merge at grouped presentation layer preserves original package collections and minimizes impact on download/business logic.
- A dedicated spacer column in card action area is the most stable way to prevent progress visuals from touching the cancel button across different widths.

---

Date: 2026-02-19

## Active Milestone
- Template Manager to Install Wizard flow improvement requirements definition

## Findings
- `TemplatesViewModel.InstallNewInstance` currently opens generic wizard flow without passing selected template context, causing intent-loss for template-first entry.
- Wizard context already supports template fields (`SelectedTemplate`, `ApplyTemplateAfterInstall`), so the gap is startup data propagation rather than missing domain model.
- Review step currently lacks explicit template summary visibility, reducing confidence before execution.
- The safest enhancement path is adding optional wizard startup payload with backward-compatible defaults for existing entry points.
