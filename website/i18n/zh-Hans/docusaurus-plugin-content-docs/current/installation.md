---
sidebar_position: 2
---

# 安装指南

## 前置条件

*   **操作系统**: Windows 10 版本 2004 或更高版本 (Build 19041 及以上) 或 Windows 11。
*   **WSL2 已启用**: 使用 DistroNexus 前请先启用 WSL2。
    *   以管理员身份打开 PowerShell 并运行: `wsl --install`。
*   **.NET 运行时**: 需要 .NET 10 Desktop Runtime。
    *   安装版会处理相关运行时前置条件。

## 下载 DistroNexus

1.  前往 [GitHub Releases](https://github.com/lazyworkshop-create/DistroNexus/releases) 页面。
2.  选择以下任一 v2.2.0 发布资产：
    *   安装版：`DistroNexus-2.2.0-Setup.exe`
    *   便携版：`DistroNexus-v2.2.0-Release.zip`
    *   自包含版：`DistroNexus-v2.2.0-Release-selfcontained.zip`

## 运行应用程序

### 安装版
1.  运行 `DistroNexus-2.2.0-Setup.exe`。
2.  完成安装后，从开始菜单启动 DistroNexus。

### 便携版 / 自包含版
1.  将 ZIP 包解压到目标目录。
2.  运行 `DistroNexus.Desktop.exe`。

## 故障排除

如果应用程序无法启动：
*   确认 WSL2 已正确安装并可用。
*   使用便携版时，确保解压后的目录结构保持完整。
*   检查杀毒软件或安全策略是否阻止可执行文件运行。
