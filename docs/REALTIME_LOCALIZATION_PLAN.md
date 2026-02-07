# Real-time Localization Implementation Plan (WPFLocalizeExtension)

## 1. Overview
This document outlines the implementation plan for enabling **real-time language switching** (without application restart) using the third-party library **WPFLocalizeExtension**. This corresponds to **Option 3** from the technical analysis.

## 2. Dependencies

| Component | Detail |
|-----------|--------|
| **Library** | `WPFLocalizeExtension` |
| **Source** | NuGet |
| **Project** | `DistroNexus.Desktop` |

## 3. Implementation Steps

### Phase 1: Infrastructure Setup
1.  **Install NuGet Package**
    - Add `WPFLocalizeExtension` to `DistroNexus.Desktop.csproj`.
    - `dotnet add package WPFLocalizeExtension`

2.  **Configure Localization Provider**
    - Modify `App.xaml` resources to define the `ResxLocalizationProvider`.
    - Configure it to point to `DistroNexus.Desktop.Properties.Resources`.

### Phase 2: XAML Migration
This phase requires modifying all XAML files that currently use `{x:Static properties:Resources.*}`.

1.  **Add Namespace Reference**
    - Add `xmlns:lex="http://wpflocalizeextension.codeplex.com"` to all Window/Page/UserControl headers.

2.  **Replace Bindings (Regex Replacement)**
    - **Search**: `{x:Static properties:Resources\.(\w+)}`
    - **Replace**: `{lex:Loc $1}`
    - **Scope**:
        - `Views/*.xaml`
        - `Wizard/**/*.xaml`
        - `Controls/*.xaml`
        - `MainWindow.xaml`

3.  **Remove Old Namespace**
    - Remove `xmlns:properties="clr-namespace:DistroNexus.Desktop.Properties"` where no longer needed.

### Phase 3: Backend Logic Updates

1.  **Update SettingsViewModel**
    - **Current**: Saves settings -> Prompts user to restart.
    - **New**: 
        - Saves settings.
        - Calls `LocalizeDictionary.Instance.Culture = new CultureInfo(...)`.
        - Removes the "Restart Required" MessageBox.
        - Shows "Language changed" toast/status instead.

2.  **Update App.xaml.cs**
    - Ensure application startup synchronization between `LocalizeDictionary.Instance.Culture` and saved settings.

3.  **Synchronization**
    - Ensure `Thread.CurrentThread.CurrentUICulture` and `CurrentCulture` are also updated (for non-WPF string formatting dates/numbers).

## 4. Work Breakdown

| Task ID | Description | Complexity | Estimation |
|---------|-------------|------------|------------|
| T1      | Install & Configure WPFLocalizeExtension | Low | 15 mins |
| T2      | XAML Bulk Migration (Regex Replace) | Medium | 30 mins |
| T3      | Manual Fixes for Complex Bindings (Parameters) | Medium | 30 mins |
| T4      | Update ViewModel Logic (Remove Restart Prompt) | Low | 15 mins |
| T5      | Testing & Verification | Low | 20 mins |

## 5. Verification Checklist
- [ ] Changing language in Settings immediately updates all visible text.
- [ ] Navigation back/forward (Wizard) retains new language.
- [ ] Tooltips and Converters update correctly.
- [ ] Application restart retains the selected language.
- [ ] No build warnings related to bindings.

## 6. Rollback Strategy
If significant issues arise with the generic library:
1. Revert `csproj` changes.
2. Revert XAML regex replacements (git checkout).
3. Fallback to current "Restart Required" mechanism.
