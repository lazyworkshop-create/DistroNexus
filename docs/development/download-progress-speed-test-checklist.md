# Download Progress & Speed - Test Checklist

## 1. Unit Tests (`DistroNexus.Tests`)

### 1.1. DownloadService
- [x] **Progress Reporting**:
    - [x] Mock `HttpClient` response.
    - [x] Verify `IProgress` is called with increasing byte counts.
    - [x] Verify `TotalBytes` is correctly reported from headers.
- [x] **Edge Cases**:
    - [x] Verify behavior when `Content-Length` is missing (unknown size).
    - [x] Verify behavior with empty files (0 bytes).

### 1.2. DownloadTaskManager
- [x] **Speed Calculation**:
    - [x] Simulate a download sequence with known timestamps and byte counts.
    - [x] Assert `BytesPerSecond` is calculated correctly.
    - [x] Assert speed drops to 0 if no bytes are received for a duration.
- [x] **Throttle Logic**:
    - [x] Ensure progress updates don't fire too frequently (if throttling is implemented).

### 1.3. Converters
- [x] **BytesToString**:
    - [x] Test `1024` -> `1 KB`
    - [x] Test `1048576` -> `1 MB`
    - [x] Test `0` -> `0 B`

## 2. Manual/Integration Tests

### 2.1. UI Behavior
- [x] **Start Download**:
    - [x] Verify Progress Bar appears immediately.
    - [x] Verify Speed shows "Calculating..." or similar initially.
- [x] **During Download**:
    - [x] Verify Progress Bar moves smoothly.
    - [x] Verify Speed updates (e.g., numbers change).
    - [x] Verify Size text (e.g., "50 MB / 100 MB") matches expectations.
- [x] **Completion**:
    - [x] Verify Progress Bar reaches 100%.
    - [x] Verify UI switches to "Cached" or "Install" state.

### 2.2. Network Conditions (Simulated)
- [ ] **Slow Network**:
    - Limit bandwidth (e.g., NetLimiter or DevTools).
    - Verify Speed display reflects low values (e.g., "50 KB/s").
- [ ] **Network Interruption**:
    - Disconnect network.
    - Verify Speed drops to 0 or task fails gracefully.

## 3. Current Verification Snapshot

- [x] **Regression Test Suite**:
    - `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj` executed.
    - Result: 211 passed, 0 failed.

- [x] **UI Automation Suite**:
    - `dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj --filter "Category=UIAutomation"` executed.
    - Result: 4 passed, 0 failed.
