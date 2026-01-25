---
sidebar_position: 2
---

# 安装指南

## 前置条件

*   **操作系统**: Windows 10 版本 2004 或更高版本 (Build 19041 及以上) 或 Windows 11。
*   **WSL 已启用**: 您必须启用了 Windows Subsystem for Linux 功能。
    *   以管理员身份打开 PowerShell 并运行: `wsl --install` (如果尚未安装)。

## 下载 DistroNexus

1.  前往 [GitHub Releases](https://github.com/DistroNexus/DistroNexus/releases) 页面。
2.  下载最新的发布版 ZIP 文件 (例如 `DistroNexus_v1.0.2.zip`)。
3.  将 ZIP 文件解压到您选择的位置 (例如 `C:\Tools\DistroNexus`)。

## 运行应用程序

1.  导航到解压后的文件夹。
2.  双击 `DistroNexus.exe` 启动仪表板。
3.  **注意**: 应用程序依赖于位于 `scripts/` 文件夹中的 PowerShell 脚本。请确保这些文件存在（它们包含在发布版中）。

## 故障排除

如果应用程序无法启动：
*   确保您对安装文件夹拥有写入权限（`config/` 文件需要此权限）。
*   检查您的杀毒软件是否拦截了可执行文件或 PowerShell 脚本。
