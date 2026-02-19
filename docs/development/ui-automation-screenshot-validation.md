# UI Automation Screenshot Validation

## Purpose
- Provide deterministic screenshot regression checks for key desktop pages.
- Detect visual regressions early (colors, backgrounds, spacing, control state visuals).

## Current Coverage
- `main-window`
- `templates-page`

## Test Entry
- Test class: `src/Client/DistroNexus.Tests/UIAutomation/TemplateUiAutomationSmokeTests.cs`
- Case: `Screenshot_Regression_Main_And_Templates_Page`

## Environment Variables
- `DISTRONEXUS_RUN_UI_AUTOMATION=1`
  - Required to enable UI automation tests.
- `DISTRONEXUS_UI_AUTOMATION_UPDATE_BASELINES=1`
  - Optional. When set, updates baseline images from current screenshots.
- `DISTRONEXUS_DESKTOP_EXE=<absolute path>`
  - Optional. Overrides desktop executable path for automation session.

## Run Commands
- Build desktop app:
  - `dotnet build src/Client/DistroNexus.Desktop/DistroNexus.Desktop.csproj -c Debug`
- Create or refresh baseline images:
  - `set DISTRONEXUS_RUN_UI_AUTOMATION=1`
  - `set DISTRONEXUS_UI_AUTOMATION_UPDATE_BASELINES=1`
  - `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Screenshot_Regression_Main_And_Templates_Page"`
- Verify against existing baselines:
  - `set DISTRONEXUS_RUN_UI_AUTOMATION=1`
  - `set DISTRONEXUS_UI_AUTOMATION_UPDATE_BASELINES=`
  - `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Screenshot_Regression_Main_And_Templates_Page"`

## Output Files
- Baseline images (committed):
  - `src/Client/DistroNexus.Tests/UIAutomation/Baselines/*.png`
- Runtime artifacts:
  - `src/Client/DistroNexus.Tests/bin/<Configuration>/<TFM>/TestResults/UIAutomationScreenshots/actual/*.png`
  - `src/Client/DistroNexus.Tests/bin/<Configuration>/<TFM>/TestResults/UIAutomationScreenshots/diff/*.diff.png`

## Notes
- Use the same display scale/theme/window layout when updating baselines to avoid noisy diffs.
- Screenshot diff threshold is currently `0.1%` changed pixels with per-channel tolerance.