---
sidebar_position: 5
---

# PowerShell 脚本参考

本页提供了位于 `scripts/` 目录下的 PowerShell 脚本的详细参考。这些脚本构成了 DistroNexus 功能的支柱，可独立用于自动化或故障排除。

## 核心管理

### `install_wsl_custom.ps1`

**描述**: 用于创建新 WSL 实例的主脚本。它处理下载发行版包（如果未缓存）、提取包并将其注册为特定目录中的新 WSL 实例。

**参数**:
*   `-DistroName <String>`: 要使用的发行版配置的内部名称（例如 "Ubuntu-22.04"）。
*   `-InstallPath <String>`: 应创建实例的目录的完整路径。
*   `-name <String>`: (可选) WSL 实例的自定义显示名称。
*   `-user <String>`: (可选) 要设置的默认用户名。
*   `-pass <String>`: (可选) 默认用户的密码。

### `uninstall_wsl_custom.ps1`

**描述**: 注销并移除 WSL 实例。

**参数**:
*   `-DistroName <String>`: 要移除的 WSL 实例的名称。
*   `-RemoveFiles`: (开关) 如果存在，则在注销后删除安装目录。

### `move_instance.ps1`

**描述**: 将现有的 WSL 实例重新定位到新的驱动器或文件夹。

**参数**:
*   `-DistroName <String>`: 要移动的有效 WSL 实例的名称。
*   `-NewPath <String>`: 目标文件夹路径。

**过程**:
1.  终止正在运行的实例。
2.  将文件系统导出为 tarball。
3.  注销旧实例。
4.  将 tarball 导入到新位置。
5.  恢复用户设置。

### `rename_instance.ps1`

**描述**: 更改 WSL 实例的注册名称。

**参数**:
*   `-OldName <String>`: 实例的当前名称。
*   `-NewName <String>`: 期望的新名称。

## 实例操作

### `start_instance.ps1`

**描述**: 在后台（无头模式）启动 WSL 实例。

**参数**:
*   `-DistroName <String>`: 要启动的实例名称。

### `stop_instance.ps1`

**描述**: 终止正在运行的 WSL 实例。

**参数**:
*   `-DistroName <String>`: 要停止的实例名称。

### `set_credentials.ps1`

**描述**: 配置特定实例的默认用户和密码。用于安装期间或密码重置。

**参数**:
*   `-DistroName <String>`: 目标实例。
*   `-Username <String>`: 要设置为默认的用户名。
*   `-Password <String>`: 要设置的密码。

### `scan_wsl_instances.ps1`

**描述**: 扫描 Windows 注册表 (`HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss`) 和实际 WSL 状态，以构建 DistroNexus 特定的元数据。这将使 `config/instances.json` 文件与实际情况同步。

**用法**: 无需参数。

## 包管理

### `list_distros.ps1`

**描述**: 读取 `config/distros.json` 文件并输出可用发行版的列表。

### `download_all_distros.ps1`

**描述**: 批量下载器，可以下载所有已定义发行版的安装包以供离线使用。

### `update_distros.ps1`

**描述**: 从在线源（如果已配置）获取最新的发行版定义并更新 `config/distros.json`。

## 实用工具

### `pwsh_utils.ps1`

**描述**: 其他脚本使用的共享函数库。不打算直接运行。

**包含**:
*   日志函数 (`Setup-Logger`, `Log-Message`)。
*   JSON 处理助手。
*   错误处理例程。
