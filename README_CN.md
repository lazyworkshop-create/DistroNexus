# DistroNexus (发行版枢纽)

**中文** | [English](README.md)

**DistroNexus** 是一个功能全面的 GUI 应用程序（由 PowerShell 驱动），旨在简化 Windows Subsystem for Linux (WSL) 发行版的管理、下载和自定义安装。它是你构建 WSL 环境的核心枢纽，让你能够像在工厂中一样定制专属的 Linux 环境。

## 功能特性

*   **现代图形界面 (GUI)**：基于 Fyne 构建的跨平台图形化仪表盘，可视化管理一切。
*   **集中下载**：自动下载主流发行版（如 Ubuntu, Debian, Kali Linux, Oracle Linux）的最新离线安装包 (Appx/AppxBundle)。
*   **自定义安装**：将 WSL 发行版安装到你指定的任意目录或驱动器，不再受限于系统盘默认路径。
*   **实例管理**：查看已安装的发行版、状态、版本和路径。
*   **安全检查**：内置验证机制，防止覆盖现有实例或安装到非空目录。
*   **多版本共存**：轻松安装同一发行版的多个版本（例如同时安装 Ubuntu 20.04 和 22.04），或同一版本的多个实例。
*   **离线支持**：利用本地缓存的安装包，极大加快重装或多实例部署的速度。
*   **卸载助手**：一键注销并移除自定义的 WSL 实例文件。

## 图形用户界面 (GUI)

DistroNexus 现已包含一个现代化的图形界面 (`DistroNexus.exe`)，将强大的 PowerShell 脚本封装在用户友好的体验中。

### 主要功能
- **安装 (Install)**: 选择发行版家族/版本，配置用户，并实时监控安装日志。
- **我的实例 (My Installs)**: 查看所有已注册的 WSL 发行版，检查其运行状态，以及一键卸载（注销 + 文件清理）。
- **设置 (Settings)**: 配置默认安装路径和缓存位置。

要启动 GUI：
1.  确保你已安装 Go 环境和 Fyne 依赖。
2.  运行构建后的可执行文件 `build/DistroNexus.exe`（如果已构建），或直接运行源码（见下文）。

## 从源码构建

本项目采用 Go 语言编写，使用了 [Fyne](https://fyne.io/) GUI 框架。

### 前置要求

*   **Go**: 版本 1.22 或更高。
*   **C 编译器**: Windows 上推荐使用 TDM-GCC 或 MinGW-w64 (用于 Fyne 的 CGO 绑定)。
*   **Fyne**: 将在首次运行时自动下载，或通过脚本配置。

### 构建步骤

1.  **设置环境**:
    运行帮助脚本自动安装 Fyne 工具链。
    ```powershell
    .\tools\setup_go_env.sh
    ```

2.  **构建应用**:
    使用提供的构建脚本生成带图标的 Windows 可执行文件。
    ```powershell
    .\tools\build.sh
    ```
    构建产物将位于 `build/DistroNexus.exe`。

3.  **开发模式运行**:
    如果你不想编译二进制文件，也可以直接运行源码：
    ```powershell
    go run .\src\main.go
    ```

## 配置

全局设置存储在 `config/settings.json` 文件中：

```json
{
    "DefaultInstallPath": "D:\\WSL",
    "DefaultDistro": "Ubuntu-24.04",
    "DistroCachePath": "..\\..\\distro"
}
```

*   `DefaultInstallPath`: 如果未提供路径，发行版将被安装到的根目录。
*   `DefaultDistro`: 快速模式下默认使用的发行版标识符。
*   `DistroCachePath`: 下载离线包的存储目录。可以是绝对路径或相对于 `scripts/` 的相对路径。

## 脚本说明

所有脚本均位于 `scripts/` 目录下：

### 1. `download_all_distros.ps1`

将所有支持的 WSL 发行版安装包下载到本地的 `distro` 目录中。这非常适合建立离线仓库或确保本地拥有最新版本。

**使用方法：**
```powershell
.\scripts\download_all_distros.ps1
```

### 2. `install_wsl_custom.ps1`

核心安装脚本。支持交互式菜单操作，也支持命令行参数调用。

**查看可用发行版列表：**
列出所有支持的发行版及其标识符（用于配置默认发行版或手动选择）。
```powershell
.\scripts\install_wsl_custom.ps1 -ls
```

**交互模式：**
直接运行脚本，按照提示通过菜单选择发行版家族、版本，并设置名称和安装路径。
```powershell
.\scripts\install_wsl_custom.ps1
```

**命令行模式 (静默/自动化)：**
使用参数跳过交互步骤。

*   **一键安装 (包含用户配置):**
    ```powershell
    .\scripts\install_wsl_custom.ps1 -name "MyDevEnv" -user "devops" -pass "securepass"
    ```

*   **通过选择发行版和版本安装：**
    ```powershell
    .\scripts\install_wsl_custom.ps1 -SelectFamily "Ubuntu" -SelectVersion "22.04"
    ```

*   **完全自定义安装（指定名称、路径和发行版）：**
    ```powershell
    .\scripts\install_wsl_custom.ps1 -SelectFamily "Debian" -SelectVersion "GNU/Linux" -DistroName "Debian-Dev" -InstallPath "D:\WSL\Debian-Dev"
    ```

*   **参数列表：**
    *   `-DistroName`: 手动指定 WSL 注册名称。
    *   `-InstallPath`: 手动指定安装目录。
    *   `-SelectFamily`: 发行版家族名称 (例如 "Ubuntu", "Debian")。
    *   `-SelectVersion`: 版本匹配字符串 (例如 "24.04")。
    *   `-name`: 快速模式：指定实例名称（使用默认发行版类型）。
    *   `-user`: 默认创建的用户名。
    *   `-pass`: 默认用户的密码。

## 项目结构

```
DistroNexus/
├── build/                        # 编译后的可执行文件输出
├── config/                       # JSON 配置
│   ├── distros.json              # 发行版定义
│   └── settings.json             # 用户设置
├── scripts/                      # PowerShell 后端脚本
│   ├── download_all_distros.ps1  # 下载工具脚本
│   ├── install_wsl_custom.ps1    # 安装工具脚本
│   ├── list_distros.ps1          # 列表辅助脚本
│   └── uninstall_wsl_custom.ps1  # 卸载工具脚本
├── src/                          # Go 源代码
│   ├── cmd/                      # 入口点
│   ├── internal/                 # 应用逻辑 & UI
│   ├── go.mod                    # Go 依赖定义
│   └── vendor/                   # 依赖包副本
├── tools/                        # 构建工具和资源
│   ├── build.sh
│   ├── gen_gear.go               # 图标生成器
│   ├── icon.png                  # 应用图标
│   └── setup_go_env.sh           # 环境设置脚本
├── README.md                     # 英文说明文档
└── README_CN.md                  # 中文说明文档
```
