# DistroNexus (发行版枢纽)

**中文** | [English](README.md)

**DistroNexus** 是一个功能强大的 PowerShell 工具包，旨在简化 Windows Subsystem for Linux (WSL) 发行版的管理、下载和自定义安装。它是你构建 WSL 环境的核心枢纽，让你能够像在工厂中一样定制专属的 Linux 环境。

## 功能特性

*   **集中下载**：自动下载主流发行版（如 Ubuntu, Debian, Kali Linux, Oracle Linux）的最新离线安装包 (Appx/AppxBundle)。
*   **自定义安装**：将 WSL 发行版安装到你指定的任意目录或驱动器，不再受限于系统盘默认路径。
*   **多版本共存**：轻松安装同一发行版的多个版本（例如同时安装 Ubuntu 20.04 和 22.04），或同一版本的多个实例。
*   **离线支持**：利用本地缓存的安装包，极大加快重装或多实例部署的速度。
*   **自动化支持**：支持强大的命令行参数，完美适配无人值守安装或脚本化部署。

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
    *   `-name`: 快速模式：设置发行版名称（使用默认发行版类型）。
    *   `-user`: 默认创建的用户名。
    *   `-pass`: 默认用户的密码。

## 项目结构

```
DistroNexus/
├── scripts/
│   ├── download_all_distros.ps1  # 下载工具脚本
│   └── install_wsl_custom.ps1    # 安装工具脚本
├── README.md                     # 英文说明文档
└── README_CN.md                  # 中文说明文档
```
