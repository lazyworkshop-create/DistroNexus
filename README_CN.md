# DistroNexus (发行版枢纽)

**中文** | [English](README.md)

**DistroNexus** 是一个功能全面的 GUI 应用程序（由 PowerShell 驱动），旨在简化 Windows Subsystem for Linux (WSL) 发行版的管理、下载和自定义安装。它是你构建 WSL 环境的核心枢纽，让你能够像在工厂中一样定制专属的 Linux 环境。

## 功能特性

*   **现代图形界面 (GUI)**：基于 Fyne 构建的跨平台图形化仪表盘，可视化管理一切。
*   **集中下载**：自动下载主流发行版（如 Ubuntu, Debian, Kali Linux, Oracle Linux）的最新离线安装包 (Appx/AppxBundle)。
*   **自定义安装**：将 WSL 发行版安装到你指定的任意目录或驱动器，不再受限于系统盘默认路径。
*   **高级实例管理**：
    *   **启动 (Start)**：在后台启动实例。
    *   **打开终端 (Open Terminal)**：为正在运行的实例打开一个新的终端窗口（支持自定义启动目录下）。
    *   **停止 (Stop)**：立即终止正在运行的实例。
    *   **移动 (Move)**：将现有发行版无损迁移到新的驱动器或文件夹。
    *   **重命名 (Rename)**：更改 WSL 实例的注册名称。
    *   **凭据 (Credentials)**：重置或设置任何实例的默认用户名和密码。
*   **安全检查**：内置验证机制，防止覆盖现有实例或安装到非空目录。
*   **多版本共存**：轻松安装同一发行版的多个版本（例如同时安装 Ubuntu 20.04 和 22.04），或同一版本的多个实例。
*   **离线支持**：利用本地缓存的安装包，极大加快重装或多实例部署的速度。
*   **包管理**：查看和管理本地缓存的发行版安装包。
*   **卸载助手**：一键注销并移除自定义的 WSL 实例文件。

## 配置

全局设置存储在 `config/settings.json` 文件中：

```json
{
    "DefaultInstallPath": "D:\\WSL",
    "PackageCachePath": "D:\\WSL_Cache",
    "DefaultTerminalStartPath": "~",
    "DefaultDistro": "Ubuntu-24.04"
}
```

*   `DefaultInstallPath`: 如果未提供路径，发行版将被安装到的根目录。
*   `PackageCachePath`: 下载离线包的存储目录。
*   `DefaultTerminalStartPath`: 打开终端时的默认启动目录 (例如 `~` 代表用户主目录，或 `/mnt/c/`)。
*   `DefaultDistro`: 快速模式下默认使用的发行版标识符。

## 图形用户界面 (GUI)

DistroNexus 现已包含一个现代化的图形界面 (`DistroNexus.exe`)，将强大的 PowerShell 脚本封装在用户友好的体验中。

### 主要功能
- **安装 (Install)**: 选择发行版家族/版本，配置用户，并实时监控安装日志。支持“快速模式”一键设置。
- **我的实例 (My Installs)**: 
    - 查看所有已注册的 WSL 发行版。
    - **操作面板**: 直接在卡片上停止、移动、重命名、设置凭据和卸载实例。
    - **磁盘使用量**: 监控每个发行版虚拟磁盘的大小。
- **包管理器 (Package Manager)**: 查看本地缓存的发行版包，查看其大小，并删除未使用的文件。
- **设置 (Settings)**: 配置默认路径（安装、缓存、终端）并支持重置配置。

![App Icon](tools/icon.png)

## 从源码构建

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

### 3. 管理脚本

*   **`move_instance.ps1`**: 将 WSL 实例移动到新位置（安全导出 -> 注销 -> 导入）。
*   **`rename_instance.ps1`**: 重命名 WSL 实例的注册表项。
*   **`start_instance.ps1`**: 启动发行版，可选指定启动目录 (`-StartPath`)。
*   **`stop_instance.ps1`**: 终止正在运行的实例。
*   **`set_credentials.ps1`**: 配置发行版内的默认用户和密码。

### 4. `download_all_distros.ps1`

将所有（或指定的）WSL 发行版安装包下载到配置的缓存路径。

### 5. `scan_wsl_instances.ps1`

扫描 `wsl -l -v` 的输出并同步内部的 `config/instances.json` 注册表。

### 基础架构

*   **`pwsh_utils.ps1`**: 用于日志记录和通用功能的共享库。日志存储在 `logs/` 目录中，支持轮转。

## 项目结构

```
DistroNexus/
├── build/                        # 编译后的可执行文件输出
├── config/                       # JSON 配置
│   ├── distros.json              # 发行版定义
│   └── settings.json             # 用户设置
├── scripts/                      # PowerShell 后端脚本
│   ├── download_all_distros.ps1  # 下载器
│   ├── install_wsl_custom.ps1    # 安装器
│   ├── move_instance.ps1         # 移动逻辑
│   ├── pwsh_utils.ps1            # 日志与工具
│   ├── rename_instance.ps1       # 重命名逻辑
│   ├── scan_wsl_instances.ps1    # 注册表同步
│   ├── set_credentials.ps1       # 用户/密码逻辑
│   ├── start_instance.ps1        # 启动器
│   ├── stop_instance.ps1         # 终止器
│   └── uninstall_wsl_custom.ps1  # 卸载器
├── src/                          # Go 源代码
│   ├── cmd/                      # 入口点
│   ├── internal/                 # 应用逻辑 & UI
│   │   ├── config/               # 配置加载器
│   │   ├── logic/                # 后端逻辑
│   │   ├── model/                # 数据类型
│   │   └── ui/                   # Fyne UI 组件
│   ├── go.mod                    # Go 依赖定义
│   └── vendor/                   # 依赖包副本
├── tools/                        # 构建工具和资源
├── docs/                         # 文档与归档
├── README.md                     # 英文说明文档
└── README_CN.md                  # 中文说明文档
```
