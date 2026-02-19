# UI Screenshot Baselines

Store approved UI automation baseline screenshots in this folder.

## Naming
- `main-window.png`
- `templates-page.png`

## Update flow
1. Set `DISTRONEXUS_RUN_UI_AUTOMATION=1`
2. Set `DISTRONEXUS_UI_AUTOMATION_UPDATE_BASELINES=1`
3. Run screenshot test:
   - `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj -c Debug --filter "FullyQualifiedName~Screenshot_Regression_Main_And_Templates_Page"`
4. Review changed PNG files and commit if expected.
