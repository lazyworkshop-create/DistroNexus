---
sidebar_position: 3
---

# 用户指南

DistroNexus 提供了一个统一的仪表板应用程序 (`DistroNexus.exe`)，将强大的 PowerShell 脚本封装为用户友好的体验。

## 仪表板概览

应用程序分为几个选项卡，便于导航。

### Install (安装) 选项卡

使用此选项卡下载并安装新的 Linux 发行版。

*   **Quick Install (快速安装)**：选择一个默认发行版（可在设置中配置）并一键安装。
*   **Custom Install (自定义安装)**：
    1.  **Select Family (选择系列)**：选择发行版系列（例如 Ubuntu, Debian）。
    2.  **Select Version (选择版本)**：选择特定版本（例如 20.04 或 22.04）。
    3.  **Install Location (安装位置)**：浏览并选择任意驱动器上的自定义目录。
    4.  **Credentials (凭据)**：在安装过程中设置默认用户名和 root 密码。

### My Installs (我的实例) 选项卡

查看和管理所有当前注册的 WSL 发行版。

*   **列表视图**：显示发行版名称、版本、状态（运行中/已停止）和 WSL 版本（1 或 2）。
*   **操作**：
    *   **Start (启动)**：在后台启动实例。
    *   **Terminal (终端)**：打开通用终端或该实例专用的 Windows Terminal。
    *   **Stop (停止)**：优雅地关闭实例。
    *   **Terminate (终止)**：强制终止实例。
    *   **Move (移动)**：将实例重新定位到另一个磁盘（例如由于空间原因从 C: 移动到 D:）。
    *   **Rename (重命名)**：更改实例的显示名称。

### Package Manager (包管理器) 选项卡

管理 DistroNexus 下载的离线 `.appx` 或 `.appxbundle` 文件。

*   查看已下载的文件及其大小。
*   删除旧的安装包以释放空间。
*   如果从其他地方下载了安装包，可以手动添加。

## 常见任务

### 将发行版移动到另一个驱动器

1.  转到 **My Installs**。
2.  选择要移动的发行版。
3.  点击 **Move**。
4.  选择新的目标文件夹。
5.  等待导出/导入过程完成。在此过程中 **请勿关闭应用程序**。

### 安装多个实例

您可以安装同一发行版的多个副本（例如 "Ubuntu-Work" 和 "Ubuntu-Personal"）。

1.  转到 **Install**。
2.  选择 "Ubuntu"。
3.  第一次安装时，将其命名为 "Ubuntu-Work"。
4.  第二次安装时，重复此过程，但将其命名为 "Ubuntu-Personal" 并选择不同的文件夹。
