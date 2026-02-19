# Download Progress & Speed - Implementation Checklist

Based on [Download Progress & Speed Specs](../../specs/download-progress-speed-feature.md).

## 1. Core Logic (`DistroNexus.Core`)

### 1.1. Interfaces & Models
- [x] **Refactor `IDownloadService`**:
    - Change `IProgress<double>` to `IProgress<(long BytesRead, long TotalBytes)>` or similar struct.
- [x] **Update `DownloadTask` Model**:
    - [x] Add `long BytesPerSecond { get; set; }`
    - [x] Add `string FormattedSpeed { get; set; }` (e.g., "1.5 MB/s")
    - [x] Add `string FormattedProgress { get; set; }` (e.g., "150 MB / 1.2 GB")
- [x] **Update `DistroPackage` Model**:
    - [x] Add `[ObservableProperty] double _downloadProgress;` (0-100)
    - [x] Add `[ObservableProperty] string _downloadSpeed;`
    - [x] Add `[ObservableProperty] string _downloadStatusText;` (Size/Total)

### 1.2. Services
- [x] **Update `DownloadService`**:
    - [x] Modify `DownloadFileAsync` to report bytes read instead of just percentage.
    - [x] Ensure `TotalBytes` is captured from `Content-Length`.
- [x] **Update `DownloadTaskManager`**:
    - [x] Implement `SpeedCalculator` logic (track `LastBytes` and `LastTime`).
    - [x] Update `DownloadTask.BytesPerSecond` in the progress callback.
    - [x] Update `DownloadTask.FormattedSpeed` and `FormattedProgress`.
    - [x] Ensure updates are throttled (e.g., every 500ms) to avoid UI spam.

## 2. Desktop UI (`DistroNexus.Desktop`)

### 2.1. ViewModels
- [x] **Update `PackageManagerViewModel`**:
    - [x] In `MonitorDownloadTaskAsync`, map `DownloadTask` properties to `DistroPackage`:
        - `DistroPackage.DownloadProgress` = `DownloadTask.Progress`
        - `DistroPackage.DownloadSpeed` = `DownloadTask.FormattedSpeed`
        - `DistroPackage.DownloadStatusText` = `DownloadTask.FormattedProgress`

### 2.2. Views (`PackageManagerPage.xaml`)
- [x] **Add Progress UI Elements**:
    - [x] Insert `ProgressBar` in the package card (visible when `IsDownloading` is true).
    - [x] Insert `TextBlock` for Speed and Size info.
    - [ ] Ensure layout handles long strings gracefully.

## 3. Converters & Utilities
- [x] **Add `BytesToStringConverter`** (if not already present) for formatting file sizes (KB, MB, GB). *(already covered by existing `FileSizeFormatter`, no new converter needed)*

