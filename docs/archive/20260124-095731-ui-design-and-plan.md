# DistroNexus Go UI Development Plan

This document outlines the requirements, analysis, implementation plan, and task list for building a Graphical User Interface (GUI) for DistroNexus using Golang.

## 1. Requirements Analysis

### 1.1 Core Goal
Create a cross-platform desktop application (primarily targeting Windows) that provides a visual interface for DistroNexus's PowerShell script functionalities, lowering the barrier to entry for users.

### 1.2 Functional Requirements
1.  **Configuration Management**:
    *   Read and parse `/config/distros.json` to retrieve the list of supported distributions.
    *   Read and parse `/config/settings.json` to retrieve global settings (Default Install Path, Cache Path, etc.).
    *   Provide an interface to modify and save `settings.json`.
2.  **Distribution Browser**:
    *   Display all available distributions (Ubuntu, Debian, Kali, etc.) in a list or tree view.
    *   Show detailed version information for each distribution.
3.  **Installation Wizard**:
    *   Allow users to select a distribution and specific version.
    *   Allow users to specify the installation directory (with folder browsing).
    *   Allow users to input the username and password for the new system.
    *   Trigger the installation process.
4.  **Download/Install Execution**:
    *   Invoke the underlying download or installation logic.
    *   Display operation logs or simple progress indicators (e.g., "Installing...").
5.  **Multi-language Support** (Optional):
    *   Since the project already has a Chinese README, the UI should consider supporting multiple languages.

### 1.3 Non-functional Requirements
*   **Clean Interface**: Easy to use.
*   **Responsiveness**: The interface should not freeze during long-running operations (like downloading/installing).
*   **Stability**: Capable of handling script execution errors and notifying the user.

---

## 2. System Analysis

### 2.1 Architecture Design
Adopt a **Go Backend + GUI Frontend** monolithic application architecture.

*   **GUI Layer**: Responsible for displaying data and receiving user input.
*   **Logic Layer**:
    *   **Config Manager**: Responsible for reading and writing JSON files.
    *   **Process Manager**: Responsible for invoking PowerShell scripts or `wsl.exe` commands.
    *   **Data Model**: Structs mapping `distros.json` and `settings.json`.

### 2.2 Data Interaction
*   **Reading**: Load JSON files from the `config/` directory into memory structs upon application startup.
*   **Execution**:
    *   Option A (Recommended): Use Go's `os/exec` package to invoke the existing `scripts/install_wsl_custom.ps1` script. This maximizes the reuse of existing logic (downloading, unzipping, registering WSL, creating users, etc.).
    *   Option B: Rewrite the installation logic entirely in Go. This involves significant work and is prone to bugs, so it is not recommended for now.

### 2.3 Key Processes
1.  **Application Launch**: Check `config/` path -> Load JSON -> Render main interface list.
2.  **Click Install**:
    *   Pop up a dialog to collect information (Path, Account).
    *   Construct PowerShell command arguments: `.\scripts\install_wsl_custom.ps1 -DistroName "Ubuntu-22.04" -InstallPath "D:\WSL\Ubuntu" -user "dev" -pass "123"`.
    *   Execute command asynchronously to avoid blocking the UI.
    *   Capture standard output/error output and display in the log window.

---

## 3. Implementation Plan

### 3.1 Tech Stack
*   **Programming Language**: Go (Golang)
*   **GUI Framework**: **Fyne** (v2)
    *   *Reason*: Pure Go, no CGO dependency (for main components), easy cross-compilation, built-in Material Design style, rich components, very suitable for tool-like applications.
    *   *Alternative*: Wails (Web tech stack), if you are more proficient in HTML/JS, but Fyne offers faster development for this use case.
*   **Script Interaction**: `os/exec` (PowerShell Core or Windows PowerShell).

### 3.2 Directory Structure
```
DistroNexus/
├── src/                  # Go Source Code & Module Root
│   ├── cmd/
│   │   └── gui/
│   │       └── main.go   # Entry point
│   ├── internal/
│   │   ├── config/       # Configuration loading logic
│   │   ├── model/        # Data Model struct definitions
│   │   ├── logic/        # Business logic for script invocation
│   │   └── ui/           # Interface components (Forms, Widgets)
│   ├── go.mod            # Go module definition
│   └── go.sum
├── config/               # Existing configuration (distros.json, settings.json)
├── scripts/              # Existing PowerShell scripts
├── tools/                # Helper tools/scripts
└── docs/                 # Documentation
```

### 3.3 Data Model Definition (Model)
Define Go Structs matching `distros.json`:
```go
type DistroConfig struct {
    Name     string             `json:"Name"`
    Versions map[string]Version `json:"Versions"`
}

type Version struct {
    Name        string `json:"Name"`
    Url         string `json:"Url"`
    DefaultName string `json:"DefaultName"`
    Filename    string `json:"Filename"`
}
```

---

## 4. Task List

### Phase 1: Foundation
- [x] **Environment Setup**: Created `tools/setup_go_env.sh` to automate environment checks and module initialization.
- [x] **Project Structure**: Created `src/` directory layout and moved `go.mod/sum` into it.
- [x] **Define Models**: Create configuration Structs in `internal/model`.
- [x] **Load Configuration**: Implement functions to read `distros.json` and `settings.json` in `internal/config`.

### Phase 2: Interface Development
- [x] **Main Window**: Create the main Application window using Fyne.
- [x] **Sidebar/List**: Create a List or Tree component to display all Distros.
- [x] **Detail View**: Display detailed information (Version, URL) on the right side upon clicking a list item.

### Phase 3: Logic Integration
- [x] **Install Form**: Create a popup or new page containing: Install Path Input (File Picker), Username, Password.
- [x] **Script Executor**: Write `internal/logic/installer.go`, encapsulating `exec.Command("powershell", ...)` calls.
- [x] **Connect Installation**: Pass form data to the executor to trigger `install_wsl_custom.ps1`.

### Phase 4: Optimization & Release
- [x] **Settings Interface**: Added interface for modifying global default paths.
- [x] **Log Display**: Add a log output area at the bottom of the interface to display script execution results in real-time.
- [x] **Build Scripts**: Created `tools/build.sh` for cross-platform compilation.
- [ ] **Windows Testing**: Compile and run tests in a real Windows environment.

---

**Current Status**: All development phases (1-4) are complete. The application includes a full GUI for browsing distros, easy installation, real-time logging, and a settings manager.

**Next Step Recommendation**: Run the `tools/build.sh` script to generate binaries, then transfer `build/DistroNexus.exe` to a Windows machine for final end-to-end testing with WSL.
