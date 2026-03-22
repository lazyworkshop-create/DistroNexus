# DistroNexus v2.2.0 正式发布：WSL 实例管理，从"基础可用"到"深度运营"

> DistroNexus 是一款专为 Windows WSL2 设计的实例管理工具，提供图形界面和 PowerShell 模块。

如果你已经在用 DistroNexus 管理 WSL 实例，你可能会遇到这些问题：

- VHDX 磁盘越跑越大，占用越来越多，却不知道怎么安全回收；
- Docker Desktop 要在哪个实例里用，每次都要手动去 Docker 设置里点；
- 做了一年的开发环境，没有备份，某天 WSL 出问题直接重装；
- 同时跑三四个实例，完全不知道哪个在干什么，哪个端口在监听。

v2.2.0 专门来解决这些问题。

---

## 这次发布解决了什么？

这次的核心主题是**深度实例管理**：把原本只有"启动/停止/迁移"的基础操作，升级成一套完整的运营能力体系。

PowerShell 模块的导出命令数量从 **15 个增长至 36 个**，新增两项主要功能和九项专项增强。

---

## 主要新功能

### VHDX 磁盘压缩

WSL 实例用久了，VHDX 文件会因为文件删除而产生大量"空洞"，磁盘占用虚高。现在一条命令就能回收空间：

```powershell
# 查看能回收多少，不实际修改
Compress-DistroNexusInstance -Name "Ubuntu-24.04" -WhatIf

# 确认后执行压缩
Compress-DistroNexusInstance -Name "Ubuntu-24.04"
```

压缩流程全自动：先执行 `fstrim` 清零已删除块，再调用 `Optimize-VHD` 或 `diskpart` 压缩 VHDX。运行中的实例会自动停止，完成后重启。

### Docker Desktop 集成管理

不再需要打开 Docker Desktop 设置界面手动操作，直接在 PowerShell 里控制：

```powershell
# 查看所有实例的集成状态
Get-DistroNexusDockerIntegration

# 为指定实例启用/禁用 Docker 后端
Enable-DistroNexusDockerIntegration -Name "Ubuntu-24.04"
Disable-DistroNexusDockerIntegration -Name "Ubuntu-24.04"
```

---

## 九项专项增强

### 实例导出与导入

把实例备份为 `.tar`，迁移或恢复时直接导入：

```powershell
Export-DistroNexusInstance -Name "Ubuntu-24.04" -Destination "D:\Backups" -Force
Import-DistroNexusInstance -Name "Ubuntu-Restored" -Source "D:\Backups\Ubuntu-24.04-20260302.tar" -InstallPath "D:\WSL\Ubuntu-Restored"
```

### 自动备份调度

通过 Windows 任务计划程序设置定时备份，再也不怕数据丢失：

```powershell
# 每天自动备份，保留最近 7 份
New-DistroNexusBackupSchedule -Name "Ubuntu-24.04" -Frequency "Daily" -Destination "D:\Backups" -RetentionCount 7

# 随时按需执行一次
Invoke-DistroNexusBackup -Name "Ubuntu-24.04"
```

### 端口转发可视化

直接看实例内部在监听哪些端口、是否有 Windows 代理：

```powershell
Get-DistroNexusPortMapping -Name "Ubuntu-24.04"
```

输出包含协议、本地地址、端口号、进程名、是否有 Windows portproxy 映射、实例 IP 地址。

### 实例标签管理

给实例打标签，方便分类和识别：

```powershell
Set-DistroNexusInstanceTag -Name "Ubuntu-24.04" -Tags "dev", "python", "docker"
Get-DistroNexusInstanceTag -Name "Ubuntu-24.04"
```

每个实例最多 10 个标签，自动去重，持久化存储。

### 全局 `.wslconfig` 编辑器

不再手动编辑 INI 文件，用命令读写全局 WSL 配置，保留注释和未知键：

```powershell
Get-DistroNexusWslConfig
Set-DistroNexusWslConfig -Memory "8GB" -Processors 4
```

内存设置超过主机 RAM 的 80% 时会自动发出警告。

### 实例级资源配置

```powershell
# 查看实例的稀疏 VHDX 模式和全局配置
Get-DistroNexusInstanceConfig -Name "Ubuntu-24.04"

# 启用稀疏 VHDX（磁盘实际使用量更贴近真实数据量）
Set-DistroNexusInstanceSparseMode -Name "Ubuntu-24.04" -Enabled $true
```

### 主动缓存失效

实例列表现在会在 WSL 状态变化后自动刷新，无需手动触发。新增诊断命令：

```powershell
Get-DistroNexusCache
```

### 性能优化：原生实例列表

`WslManagerService` 现在可以直接调用 `wsl --list --verbose` 原生解析，减少一次 PowerShell 进程开销，实例列表响应更快。

### 统一错误码系统

所有异常现在携带结构化错误码（如 `DNEX-1001`），便于日志追踪和问题定位。错误码按功能模块分类（1xxx 实例管理、2xxx 磁盘操作、3xxx Docker 集成、4xxx 备份导入导出、5xxx 配置管理）。

---

## 下载与安装

**系统要求**：Windows 10 版本 2004（Build 19041）或更高，WSL2 已启用，.NET 10 Desktop Runtime。

### Microsoft Store（推荐）

在 Windows 应用商店搜索 **DistroNexus**，或直接访问：

👉 **[Microsoft Store - DistroNexus](https://apps.microsoft.com/detail/9mtk4br3v436?hl=zh-CN&gl=CN)**

商店版自动更新，无需手动下载，适合日常使用。

### GitHub Releases

前往 [GitHub Releases](https://github.com/lazyworkshop-create/DistroNexus/releases/tag/v2.2.0) 下载，提供三种打包方式：

- **安装版**：`DistroNexus-2.2.0-Setup.exe`（自动处理 .NET 运行时）
- **便携版**：`DistroNexus-v2.2.0-Release.zip`
- **自包含版**：`DistroNexus-v2.2.0-Release-selfcontained.zip`（无需安装 .NET 运行时）

---

## 完整变更记录

详细技术说明请参阅 [v2.2.0 发行说明](https://github.com/lazyworkshop-create/DistroNexus/blob/master/docs/release_notes/v2.2.0.zh-CN.md) 和 [CHANGELOG](https://github.com/lazyworkshop-create/DistroNexus/blob/master/CHANGELOG.md)。

---

## 反馈与交流

如果你在使用中遇到问题或有功能建议，欢迎：

- 提交 Issue：[GitHub Issues](https://github.com/lazyworkshop-create/DistroNexus/issues)
- 加入讨论：[GitHub Discussions](https://github.com/lazyworkshop-create/DistroNexus/discussions)

感谢所有使用 DistroNexus 的朋友。

觉得有用？点个「在看」让更多人知道 👇
