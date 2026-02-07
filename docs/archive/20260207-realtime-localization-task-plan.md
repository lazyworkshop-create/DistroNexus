# Task Plan: Real-time Localization Implementation

**Status**: Completed
**Goal**: Implement real-time language switching using `WPFLocalizeExtension`.

## Phase 1: Infrastructure Setup
- [ ] Install `WPFLocalizeExtension` NuGet package. <!-- id: 1 -->
- [ ] Configure `App.xaml` for `ResxLocalizationProvider`. <!-- id: 2 -->

## Phase 2: XAML Migration
- [ ] Identify all XAML files using static resource bindings. <!-- id: 3 -->
- [ ] Add `xmlns:lex` namespace to XAML files. <!-- id: 4 -->
- [ ] Replace `{x:Static properties:Resources.Key}` with `{lex:Loc Key}`. <!-- id: 5 -->
- [ ] Clean up unused namespaces. <!-- id: 6 -->

## Phase 3: Backend Logic Updates
- [ ] Update `SettingsViewModel` to switch `LocalizeDictionary.Instance.Culture`. <!-- id: 7 -->
- [ ] Remove "Restart Required" logic from `SettingsViewModel`. <!-- id: 8 -->
- [ ] Sync `App.xaml.cs` startup logic. <!-- id: 9 -->

## Phase 4: Verification
- [ ] Manual verification of language switching. <!-- id: 10 -->
