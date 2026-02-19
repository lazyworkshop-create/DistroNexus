# Download Progress & Speed Display Requirements

## 1. Overview
This document outlines the requirements and technical design for displaying real-time download progress and speed in the DistroNexus Package Manager UI. Currently, the application only shows an indeterminate "Downloading" state without quantitative feedback.

## 2. Problem Statement
- **User Experience**: Users cannot estimate when a download will finish.
- **Feedback**: No visual indication if a download is stalled or progressing slowly.
- **Current Architecture**: 
  - `DownloadService` only reports percentage (double).
  - `DistroPackage` lacks progress properties.
  - UI has no progress bar or speed text.

## 3. Requirements

### 3.1. Functional Requirements
1.  **Progress Display**: Show a determinate progress bar (0-100%) for each downloading package.
2.  **Speed Display**: Show current download speed (e.g., "1.2 MB/s", "500 KB/s").
3.  **Downloaded Size**: Show downloaded bytes vs total bytes (e.g., "50 MB / 1.2 GB").
4.  **Real-time Updates**: Update frequency should be sufficient for smooth UX (e.g., every 500ms or 1s) but not overwhelm the UI thread.

### 3.2. Technical Requirements
- **Speed Calculation**: Must be calculated based on the delta of bytes downloaded over a time interval.
- **Thread Safety**: UI updates must happen on the UI thread.
- **Performance**: Minimizing overhead of progress reporting.

## 4. Technical Design

### 4.1. Core Logic (`DistroNexus.Core`)
1.  **Modify `DownloadTask`**:
    - Add `BytesPerSecond` (long) property.
    - Add `FormattedSpeed` (string) helper property.
    - Add `DownloadedBytes` (existing) and `TotalBytes` (existing).
    
2.  **Update `DownloadTaskManager`**:
    - Implement a `SpeedCalculator` helper or logic inside `ProcessTaskAsync`.
    - Track `lastBytes` and `lastTimestamp`.
    - Update `DownloadTask.BytesPerSecond` periodically (e.g., every 1 second or on every progress report if throttled).
    
3.  **Update `DistroPackage`**:
    - Add `DownloadProgress` (double) property (`[ObservableProperty]`).
    - Add `DownloadSpeed` (string) property.
    - Add `DownloadedSize` (string) property (e.g., "100 MB / 500 MB").

4.  **Update `DownloadService`**:
    - Currently uses `IProgress<double>`.
    - **Option A**: Change to `IProgress<(long downloaded, long total)>` to allow accurate byte tracking.
    - **Option B**: Keep signature but ensure `DownloadTask` has access to byte counts via a shared context or different reporting mechanism.
    - *Recommendation*: Use Option A or overload `DownloadFileAsync` to support detailed progress.

### 4.2. View Logic (`DistroNexus.Desktop`)
1.  **`PackageManagerViewModel`**:
    - In `MonitorDownloadTaskAsync`, subscribe to `DownloadTask` property changes or poll more frequently.
    - Map `DownloadTask` properties (Progress, Speed) to the corresponding `DistroPackage` instance.
    
2.  **`PackageManagerPage.xaml`**:
    - Replace the "Cancel" button visibility trigger with a dedicated `Grid`/`StackPanel` for download status.
    - Add `ProgressBar` binding to `DownloadProgress`.
    - Add `TextBlock` binding to `DownloadSpeed` and `DownloadedSize`.
    
## 5. Implementation Steps
1.  Refactor `IDownloadService` to support byte-level progress reporting.
2.  Implement speed calculation logic in `DownloadTaskManager`.
3.  Extend `DistroPackage` model.
4.  Update ViewModel mapping logic.
5.  Update XAML layout.

## 6. Risks & Mitigations
- **Unknown File Size**: If Content-Length is missing, percentage is impossible.
  - *Mitigation*: Show indeterminate progress bar and only "X MB downloaded" (speed can still be calculated).
- **UI Performance**: Frequent updates might freeze UI.
  - *Mitigation*: Throttle property change notifications to 10-60fps max (e.g., restrict updates to every 100ms).

