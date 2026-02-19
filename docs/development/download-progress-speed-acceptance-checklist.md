# Download Progress & Speed - Acceptance Checklist

## 1. Functional Requirements
- [x] **Progress Bar**:
    - [x] Visible only when `IsDownloading` is true.
    - [x] Indicates percentage correctly (0% to 100%).
    - [ ] Shows indeterminate state if file size is unknown.
- [x] **Speed Indicator**:
    - [x] Displays current download speed.
    - [x] Format is readable (e.g., "1.2 MB/s", "500 KB/s").
    - [x] Updates frequently enough to feel "live" (e.g., ~1s interval).
- [ ] **Size Indicator**:
    - [x] Displays "Downloaded / Total" string.
    - [ ] Matches the file size shown in the package details.

## 2. User Experience
- [ ] **Smoothness**: UI does not freeze or stutter during high-speed downloads (updates are executed on UI thread but logical work is background).
- [x] **Readability**: Text contrast is good; fonts are consistent with the rest of the app.
- [x] **Responsiveness**: "Cancel" button remains clickable during download.

## 3. Performance & Stability
- [ ] **Memory**: No memory leaks observed after multiple downloads (DownloadTasks are cleaned up).
- [ ] **CPU**: Speed calculation does not consume excessive CPU.

## 4. Edge Cases
- [x] **Small Files**: Progress bar might jump to 100% quickly; this is acceptable.
- [ ] **Unknown Size**: UI handles `TotalBytes = -1` gracefully (e.g., hides progress bar or shows indeterminate).

## 5. Notes

- Items checked above are based on current code implementation inspection.
- UI automation evidence: `Category=UIAutomation` suite passed (`4 passed, 0 failed`) with deterministic download simulation enabled.
- Unchecked items require dedicated runtime/manual verification before acceptance sign-off.
