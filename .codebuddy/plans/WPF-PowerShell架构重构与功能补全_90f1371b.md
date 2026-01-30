---
name: WPF-PowerShell架构重构与功能补全
overview: 重构WPF客户端使其调用PowerShell模块作为后台服务层，消除重复实现；在PowerShell模块中补充WPF所需但缺失的功能；确保前后端分层清晰，所有WSL操作通过PowerShell模块处理。
todos:
  - id: powershell-cache-mechanism
    content: 在PowerShell模块中实现实例缓存机制（Private/Cache.ps1），包括Get/Set/Update-InstanceCache函数，并修改Get-DistroNexusInstance.ps1增加-ForceUpdate参数
    status: completed
  - id: powershell-package-handler
    content: 在PowerShell模块中实现包格式处理（Private/PackageHandler.ps1），支持.appx/.zip/.tar.gz自动解压，包括Expand-DistroPackage和Test-PackageFormat函数
    status: completed
  - id: powershell-terminal-launcher
    content: 在PowerShell模块中实现终端启动辅助（Private/TerminalLauncher.ps1），自动检测Windows Terminal/CMD并启动，包括Invoke-Terminal和Find-TerminalPath函数
    status: completed
  - id: powershell-batch-download
    content: 增强Save-DistroNexusPackage.ps1支持批量下载（-Family/-All参数）、并发控制（-MaxConcurrent）、智能重试（-RetryCount）和改进的进度显示
    status: completed
    dependencies:
      - powershell-package-handler
  - id: powershell-install-enhancements
    content: 增强Install-DistroNexusInstance.ps1，增加-Interactive、-AutoDownload、-OpenTerminal参数，以及完整的用户配置选项（Shell、Locale等）
    status: completed
    dependencies:
      - powershell-terminal-launcher
      - powershell-package-handler
  - id: powershell-start-terminal
    content: 修改Start-DistroNexusInstance.ps1增加-OpenTerminal和-StartPath参数，集成TerminalLauncher实现启动后自动打开终端
    status: completed
    dependencies:
      - powershell-terminal-launcher
  - id: powershell-move-safety
    content: 增强Move-DistroNexusInstance.ps1的安全检查，包括非空目录检查、-Force参数，以及移动后自动恢复DefaultUid配置
    status: completed
  - id: powershell-credential-wslconf
    content: 增强Set-DistroNexusCredential.ps1，增加wsl.conf自动配置、-AddToWheel参数支持Fedora/RHEL系列的wheel组
    status: completed
  - id: powershell-catalog-backup
    content: 修改Update-DistroNexusCatalog.ps1增加配置备份机制，自动轮转备份文件（.bak/.bak.1/.bak.2），保留最近N个备份
    status: completed
  - id: powershell-cache-cmdlet
    content: 创建新Cmdlet Get-DistroNexusCache.ps1，查询并显示缓存统计信息（路径、包数量、总大小）
    status: completed
    dependencies:
      - powershell-cache-mechanism
  - id: wpf-powershell-service-module
    content: 增强WPF的PowerShellService.cs，新增ExecuteModuleCmdletAsync方法支持导入DistroNexus模块并执行Cmdlet，包括参数格式化和结果解析
    status: completed
  - id: wpf-models-powershell-result
    content: 创建PowerShell执行结果模型（PowerShellResult.cs和ModuleCallOptions.cs），统一模块调用的输入输出格式
    status: completed
  - id: wpf-wslmanager-get-instances
    content: 重构WslManagerService.GetInstancesAsync方法，改为调用Get-DistroNexusInstance Cmdlet，实现PowerShell对象到WslInstance模型的映射
    status: completed
    dependencies:
      - wpf-powershell-service-module
      - wpf-models-powershell-result
      - powershell-cache-mechanism
  - id: wpf-wslmanager-basic-operations
    content: 重构WslManagerService的基础操作方法（StartInstanceAsync、StopInstanceAsync、RemoveInstanceAsync、RenameInstanceAsync），改为调用对应的PowerShell模块Cmdlet
    status: completed
    dependencies:
      - wpf-wslmanager-get-instances
  - id: wpf-wslmanager-move
    content: 重构WslManagerService.MoveInstanceAsync，调用Move-DistroNexusInstance Cmdlet并实现Write-Progress到IProgress
    status: completed
    dependencies:
      - wpf-wslmanager-basic-operations
      - powershell-move-safety
---

## 用户需求

审查并完善DistroNexus项目中WPF客户端与PowerShell模块的实现方案，确保前后端分层清晰，实现功能完整性和架构一致性。

## 产品概述

DistroNexus是一个WSL2分发版管理工具，包含两个核心组件：

1. **PowerShell模块**：提供11个公开Cmdlet的CLI自动化工具，适用于DevOps和脚本化场景
2. **WPF客户端**：基于.NET 10.0的现代化图形界面应用，提供可视化管理和用户友好体验

当前架构存在的核心问题是WPF客户端完全独立实现，未使用PowerShell模块，导致功能重复实现且无法复用模块能力。

## 核心功能

### 1. 架构重构 - WPF调用PowerShell模块

- WPF客户端的`WslManagerService`重构为调用PowerShell模块Cmdlet
- 保留`PowerShellService`作为执行层，增加模块导入支持
- 所有WSL操作统一通过PowerShell模块执行，避免内联脚本重复实现

### 2. PowerShell模块功能补全

- **实例管理增强**（16项）：缓存机制、交互式模式、Release/User查询、终端启动集成、非空目录检查等
- **包管理增强**（6项）：批量下载（-Family/-All参数）、并发控制、智能重试、进度改进、包格式处理、配置备份
- **用户管理增强**（2项）：wsl.conf自动配置、wheel组支持
- 确保PowerShell模块功能完整，可独立满足自动化需求

### 3. WPF客户端增强

- 补充调用PowerShell模块中已实现但WPF未使用的功能
- 保留WPF独有的GUI增强特性（并发下载管理、实时进度、安装向导、主题切换等）
- 增加批量操作、WhatIf预览、导出/导入配置等CLI特有功能的GUI实现

### 4. 功能对齐策略

- 核心CRUD操作保持功能一致
- PowerShell侧重自动化、批处理、CI/CD集成
- WPF侧重用户友好性、可视化、实时反馈
- 通过PowerShell模块作为共享业务逻辑层，减少重复代码

## 技术栈

### 现有技术栈

- **PowerShell模块**：PowerShell 7.0+，模块化架构（Public/Private分离）
- **WPF客户端**：.NET 10.0, WPF + WPF-UI 4.2.0, CommunityToolkit.Mvvm 8.4.0
- **共享层**：DistroNexus.Core（服务层、接口、模型）
- **依赖注入**：Microsoft.Extensions.DependencyInjection
- **日志系统**：PowerShell自定义日志 + Microsoft.Extensions.Logging

### 技术决策

保持现有技术栈，重点优化架构集成方式。

## 实施方案

### 高层策略

采用**渐进式重构**策略，分阶段实施，确保每个阶段都可独立测试和交付：

1. **阶段一：PowerShell模块功能补全**（优先级最高）

- 补全PowerShell模块缺失的18项核心功能
- 确保模块功能完整性，可独立运行
- 不影响现有WPF客户端功能

2. **阶段二：WPF架构重构 - 调用PowerShell模块**（核心重构）

- 重构`WslManagerService`调用PowerShell模块Cmdlet
- 增强`PowerShellService`支持模块导入和参数化调用
- 逐步迁移每个操作（Get/Install/Start/Stop/Move/Rename/Remove/SetCredentials）

3. **阶段三：WPF独有功能保留与增强**（功能完善）

- 保留并优化WPF独有的GUI增强特性
- 补充WPF缺失的CLI特有功能（批量操作、WhatIf预览等）
- 实现配置导出/导入、详细日志查看器等

### 核心实现方案

#### 1. PowerShell模块补全方案

**新增Private函数**：

- `Cache.ps1`：实例缓存管理（Get/Set/Update-InstanceCache）
- `PackageHandler.ps1`：包格式处理（Expand-DistroPackage, Test-PackageFormat）
- `Interactive.ps1`：交互式UI辅助（Show-DistroSelectionMenu）
- `TerminalLauncher.ps1`：终端检测和启动（Invoke-Terminal）

**修改Public Cmdlet**：

- `Get-DistroNexusInstance.ps1`：增加 `-ForceUpdate`, `-IncludeRelease`, `-IncludeUser` 参数
- `Install-DistroNexusInstance.ps1`：增加 `-Interactive`, `-AutoDownload`, `-OpenTerminal` 参数，完整用户配置
- `Start-DistroNexusInstance.ps1`：增加 `-OpenTerminal`, `-StartPath` 参数
- `Save-DistroNexusPackage.ps1`：增加 `-Family`, `-All`, `-MaxConcurrent`, `-RetryCount` 参数
- `Move-DistroNexusInstance.ps1`：增加非空目录检查和用户恢复逻辑
- `Set-DistroNexusCredential.ps1`：增加wsl.conf自动配置和wheel组支持
- `Update-DistroNexusCatalog.ps1`：增加配置备份机制

**实现优先级**：

- **P0（高影响、低风险）**：缓存机制、进度改进、非空目录检查、配置备份
- **P1（中影响、中风险）**：批量下载、包格式处理、终端集成、完整用户配置
- **P2（低影响或高风险）**：交互式模式、Release/User查询、自动下载

#### 2. WPF架构重构方案

**PowerShellService增强**：

```
// 新增方法：导入模块并执行Cmdlet
Task<PowerShellResult> ExecuteModuleCmdletAsync(
    string cmdletName, 
    Dictionary<string, object> parameters,
    CancellationToken cancellationToken = default);

// 示例：执行 Get-DistroNexusInstance
var result = await _powerShell.ExecuteModuleCmdletAsync(
    "Get-DistroNexusInstance",
    new Dictionary<string, object> { ["Name"] = "Ubuntu*" },
    cancellationToken);
```

**WslManagerService重构策略**：

```
// 当前实现（内联脚本）
var script = "wsl --list --verbose 2>&1";
var result = await _powerShell.ExecuteScriptAsync(script);

// 重构后（调用模块）
var result = await _powerShell.ExecuteModuleCmdletAsync(
    "Get-DistroNexusInstance",
    new Dictionary<string, object>(),
    cancellationToken);
```

**迁移顺序**（按风险从低到高）：

1. `GetInstancesAsync` → `Get-DistroNexusInstance`
2. `StartInstanceAsync` → `Start-DistroNexusInstance`
3. `StopInstanceAsync` → `Stop-DistroNexusInstance`
4. `RemoveInstanceAsync` → `Remove-DistroNexusInstance`
5. `RenameInstanceAsync` → `Rename-DistroNexusInstance`
6. `MoveInstanceAsync` → `Move-DistroNexusInstance`（保留进度回调）
7. `InstallInstanceAsync` → `Install-DistroNexusInstance`（保留安装向导）
8. `SetCredentialsAsync` → `Set-DistroNexusCredential`

**进度映射策略**：
PowerShell的`Write-Progress`输出映射到WPF的`IProgress<T>`：

```
// 解析PowerShell进度输出
// ProgressRecord: Activity="Moving instance", PercentComplete=50
// 映射到: progress?.Report((50.0, "Moving instance"))
```

#### 3. 功能对齐方案

**PowerShell模块独有功能**（保持CLI优势）：

- 管道批量操作（天然支持）
- WhatIf/Confirm标准化（ShouldProcess）
- 脚本集成和远程执行（PSRemoting）
- Get-Help文档系统

**WPF客户端独有功能**（保持GUI优势）：

- 并发下载管理器（DownloadTaskManager）
- 交互式安装向导（InstallWizardViewModel）
- 实时状态仪表板（10秒自动刷新）
- 主题/语言切换
- 缓存可视化管理
- 源管理界面

**需补充的功能**：

- WPF增加：批量实例操作（多选）、WhatIf预览对话框、配置导出/导入
- PowerShell增加：并发下载控制、智能重试、终端集成、缓存统计Cmdlet

## 实施细节

### 性能优化

1. **缓存策略**：

- PowerShell模块：`config/instances.json`缓存，通过`-ForceUpdate`参数控制
- WPF客户端：ViewModel内存缓存 + 10秒自动刷新，避免频繁调用模块

2. **并发控制**：

- PowerShell模块：使用PowerShell Jobs实现并发下载（`-MaxConcurrent`参数）
- WPF客户端：保留`DownloadTaskManager`的Semaphore并发控制

3. **启动优化**：

- PowerShell模块：惰性加载，仅在首次调用时初始化日志和配置
- WPF客户端：异步初始化，15秒超时保护，避免阻塞UI

### 日志集成

**统一日志策略**：

- PowerShell模块：保留自定义日志系统（`Write-DistroNexusLog`），输出到`logs/`目录
- WPF客户端：`Microsoft.Extensions.Logging`输出到同一`logs/`目录
- 日志格式保持一致，便于统一查看和分析

### 错误处理

**PowerShell模块**：

- 统一异常类型（使用`throw`替代`return $false`）
- 提供用户友好错误消息
- 详细技术错误记录到日志

**WPF客户端**：

- 捕获PowerShell模块抛出的异常
- 提取错误消息显示在UI对话框
- 保留详细错误日志供故障排查

### 向后兼容

1. **PowerShell模块**：

- 所有新增参数设置合理默认值
- 保持现有参数和行为不变
- 新功能通过开关参数控制

2. **WPF客户端**：

- 重构期间保留原有内联脚本实现作为回退方案
- 通过配置开关控制使用模块还是内联脚本
- 确保每个迁移步骤可独立测试和回滚

### Blast Radius控制

1. **独立测试**：每个Cmdlet补全独立提交，可单独测试
2. **渐进迁移**：WPF重构按功能逐步迁移，每个操作独立验证
3. **功能开关**：通过配置控制启用/禁用新功能，便于快速回退
4. **日志跟踪**：详细日志记录模块调用和参数，便于问题定位

## 目录结构

### PowerShell模块扩展结构

```
src/PowerShell/
├── DistroNexus.psd1                              # 模块清单（更新版本和导出函数）
├── DistroNexus.psm1                              # 根模块（无需修改）
├── Private/                                      # 私有辅助函数
│   ├── Config.ps1                                # [EXISTING] 配置管理
│   ├── Logger.ps1                                # [EXISTING] 日志系统
│   ├── Cache.ps1                                 # [NEW] 实例缓存管理
│   │   # 功能：Get-InstanceCache, Set-InstanceCache, Update-InstanceCache
│   │   # 用途：实现实例信息缓存到config/instances.json，减少注册表扫描频率
│   ├── PackageHandler.ps1                        # [NEW] 包格式处理
│   │   # 功能：Expand-DistroPackage, Test-PackageFormat
│   │   # 用途：支持.appx, .zip, .tar.gz自动解压和格式验证
│   ├── Interactive.ps1                           # [NEW] 交互式UI辅助
│   │   # 功能：Show-DistroSelectionMenu, Show-ConfirmPrompt
│   │   # 用途：为Install/Remove等操作提供Out-GridView或自定义选择菜单
│   └── TerminalLauncher.ps1                      # [NEW] 终端启动辅助
│       # 功能：Invoke-Terminal, Find-TerminalPath
│       # 用途：自动检测Windows Terminal/CMD并启动指定WSL实例
│
├── Public/                                       # 公开Cmdlet（所有文件都需修改）
│   ├── Get-DistroNexusInstance.ps1               # [MODIFY] 增加缓存和查询增强
│   │   # 新增参数：-ForceUpdate（强制刷新缓存）
│   │   # 新增参数：-IncludeRelease（查询发行版信息，需启动实例）
│   │   # 新增参数：-IncludeUser（查询当前用户，需启动实例）
│   │   # 实现：集成Cache.ps1，默认使用缓存，通过-ForceUpdate强制刷新
│   │
│   ├── Install-DistroNexusInstance.ps1           # [MODIFY] 增强安装功能
│   │   # 新增参数：-Interactive（交互式选择发行版和配置）
│   │   # 新增参数：-AutoDownload（包未缓存时自动下载）
│   │   # 新增参数：-OpenTerminal（安装后自动打开终端）
│   │   # 新增参数：-Shell（指定默认Shell，如zsh/fish）
│   │   # 新增参数：-Locale（区域设置）
│   │   # 实现：完整用户配置（用户名、密码、sudo、默认用户、wsl.conf）
│   │
│   ├── Start-DistroNexusInstance.ps1             # [MODIFY] 增加终端集成
│   │   # 新增参数：-OpenTerminal（启动后打开终端）
│   │   # 新增参数：-StartPath（终端启动路径）
│   │   # 实现：调用TerminalLauncher.ps1自动检测并启动终端
│   │
│   ├── Stop-DistroNexusInstance.ps1              # [MODIFY] 无需修改（功能完整）
│   │
│   ├── Move-DistroNexusInstance.ps1              # [MODIFY] 增强安全检查
│   │   # 新增逻辑：检查目标目录是否为空（非空则警告）
│   │   # 新增参数：-Force（覆盖非空目录检查）
│   │   # 新增逻辑：移动后自动恢复原有默认用户配置
│   │   # 实现：备份注册表DefaultUid并在移动后恢复
│   │
│   ├── Rename-DistroNexusInstance.ps1            # [MODIFY] 增加路径更改支持
│   │   # 新增参数：-NewPath（重命名同时移动到新路径）
│   │   # 实现：结合Rename和Move逻辑，一次性完成重命名和移动
│   │
│   ├── Remove-DistroNexusInstance.ps1            # [MODIFY] 增加交互式确认
│   │   # 新增参数：-Interactive（使用Out-GridView选择要删除的实例）
│   │   # 保留参数：-KeepFiles（仅注销WSL注册，保留文件）
│   │   # 实现：集成Interactive.ps1，提供可视化选择
│   │
│   ├── Set-DistroNexusCredential.ps1             # [MODIFY] 完善用户配置
│   │   # 新增逻辑：自动配置/etc/wsl.conf（default用户、automount等）
│   │   # 新增参数：-AddToWheel（对于Fedora/RHEL系列添加到wheel组）
│   │   # 新增参数：-ConfigureWslConf（是否配置wsl.conf，默认true）
│   │   # 实现：检测发行版类型，自动选择sudo或wheel组
│   │
│   ├── Get-DistroNexusPackage.ps1                # [MODIFY] 无需修改（功能完整）
│   │
│   ├── Save-DistroNexusPackage.ps1               # [MODIFY] 批量下载和进度改进
│   │   # 新增参数：-Family（批量下载同系列，如"Ubuntu"下载所有Ubuntu版本）
│   │   # 新增参数：-All（下载所有未缓存的包）
│   │   # 新增参数：-MaxConcurrent（并发下载数，1-10，默认3）
│   │   # 新增参数：-RetryCount（失败重试次数，0-10，默认3）
│   │   # 新增参数：-ShowSpeed（显示下载速度，默认true）
│   │   # 实现：改进Write-Progress显示百分比、速度、ETA
│   │   # 实现：使用PowerShell Jobs实现并发下载
│   │   # 实现：指数退避重试策略
│   │
│   ├── Update-DistroNexusCatalog.ps1             # [MODIFY] 配置备份机制
│   │   # 新增逻辑：更新前自动备份现有distros.json为distros.json.bak
│   │   # 新增参数：-KeepBackups（保留最近N个备份，默认3）
│   │   # 新增逻辑：保留LocalPath字段（旧版本遗留字段）
│   │   # 实现：轮转备份文件（.bak, .bak.1, .bak.2）
│   │
│   └── Get-DistroNexusCache.ps1                  # [NEW] 缓存统计Cmdlet
│       # 功能：查询缓存路径、包数量、总大小
│       # 参数：无（或-Detailed显示每个缓存包详情）
│       # 输出：PSCustomObject包含CachePath, PackageCount, TotalSize
│
└── config/
    └── instances.json                            # [NEW] 实例缓存文件（自动生成）
        # 格式：与Get-DistroNexusInstance输出一致
        # 字段：Name, State, Version, BasePath, DiskSize, InstallTime, Guid, CachedAt
```

### WPF客户端重构结构

```
src/Client/DistroNexus.Core/
├── Interfaces/
│   ├── IPowerShellService.cs                     # [MODIFY] 增加模块调用方法
│   │   # 新增方法：Task<PowerShellResult> ExecuteModuleCmdletAsync(
│   │   #   string cmdletName, 
│   │   #   Dictionary<string, object> parameters,
│   │   #   CancellationToken cancellationToken = default)
│   │   # 新增方法：Task<T> ExecuteModuleCmdletAsync<T>(...)（泛型版本）
│   │   # 用途：支持导入DistroNexus模块并执行Cmdlet
│   │
│   └── IWslManagerService.cs                     # [NO CHANGE] 接口保持不变
│       # 保持现有接口签名，内部实现改为调用PowerShell模块
│
├── Services/
│   ├── PowerShellService.cs                      # [MODIFY] 增强模块支持
│   │   # 新增字段：private string _moduleBasePath（模块路径）
│   │   # 新增方法：ExecuteModuleCmdletAsync（构建Import-Module + Cmdlet调用）
│   │   # 新增方法：ParsePowerShellOutput<T>（解析PowerShell对象输出为C#类型）
│   │   # 新增逻辑：启动时检测DistroNexus模块路径（src/PowerShell）
│   │   # 新增逻辑：解析Write-Progress输出映射到IProgress<T>
│   │
│   ├── WslManagerService.cs                      # [REFACTOR] 重构为调用模块
│   │   # GetInstancesAsync → Get-DistroNexusInstance
│   │   # StartInstanceAsync → Start-DistroNexusInstance
│   │   # StopInstanceAsync → Stop-DistroNexusInstance
│   │   # RemoveInstanceAsync → Remove-DistroNexusInstance
│   │   # RenameInstanceAsync → Rename-DistroNexusInstance
│   │   # MoveInstanceAsync → Move-DistroNexusInstance（保留进度映射）
│   │   # InstallInstanceAsync → Install-DistroNexusInstance（保留安装向导逻辑）
│   │   # SetCredentialsAsync → Set-DistroNexusCredential
│   │   # 策略：渐进式重构，每个方法独立迁移，保留回退方案
│   │
│   └── DownloadTaskManager.cs                    # [NO CHANGE] 保留WPF独有功能
│       # WPF的并发下载管理器保持不变，作为GUI增强特性
│
└── Models/
    ├── PowerShellResult.cs                       # [NEW] PowerShell执行结果模型
    │   # 字段：bool Success, string Output, string Error, int ExitCode
    │   # 字段：List<object> ParsedObjects（解析后的PowerShell对象）
    │   # 用途：统一PowerShell命令执行结果格式
    │
    └── ModuleCallOptions.cs                      # [NEW] 模块调用选项
        # 字段：bool UseModuleFallback（失败时回退到内联脚本）
        # 字段：int TimeoutSeconds（模块调用超时）
        # 字段：bool LogVerbose（是否记录详细日志）
```

## 关键代码结构

### PowerShell模块 - 缓存机制示例

```
# Private/Cache.ps1
function Get-InstanceCache {
    <#
    .SYNOPSIS
        从缓存文件加载实例信息
    #>
    [CmdletBinding()]
    param()
    
    $cacheFile = Join-Path (Get-DistroNexusConfig).CachePath "instances.json"
    if (Test-Path $cacheFile) {
        try {
            $cache = Get-Content $cacheFile -Raw | ConvertFrom-Json
            if ($cache.CachedAt -gt (Get-Date).AddMinutes(-10)) {
                return $cache.Instances
            }
        } catch {
            Write-DistroNexusLog "Failed to load cache: $_" -Level WARN
        }
    }
    return $null
}

function Set-InstanceCache {
    <#
    .SYNOPSIS
        将实例信息保存到缓存
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject[]]$Instances
    )
    
    $cacheFile = Join-Path (Get-DistroNexusConfig).CachePath "instances.json"
    $cache = @{
        CachedAt = (Get-Date).ToString("o")
        Instances = $Instances
    }
    
    try {
        $cache | ConvertTo-Json -Depth 5 | Set-Content $cacheFile -Force
        Write-DistroNexusLog "Instance cache updated" -FileOnly
    } catch {
        Write-DistroNexusLog "Failed to save cache: $_" -Level WARN
    }
}
```

### PowerShell模块 - 批量下载示例

```
# Public/Save-DistroNexusPackage.ps1（部分修改）
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $false, ParameterSetName = 'Single')]
    [string]$Name,
    
    [Parameter(Mandatory = $false, ParameterSetName = 'Family')]
    [string]$Family,
    
    [Parameter(Mandatory = $false, ParameterSetName = 'All')]
    [switch]$All,
    
    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 10)]
    [int]$MaxConcurrent = 3,
    
    [Parameter(Mandatory = $false)]
    [ValidateRange(0, 10)]
    [int]$RetryCount = 3
)

process {
    # 批量下载逻辑
    if ($Family -or $All) {
        $packages = Get-DistroNexusPackage
        if ($Family) {
            $packages = $packages | Where-Object { $_.Family -eq $Family }
        }
        
        # 并发下载（使用Jobs）
        $jobs = @()
        $semaphore = New-Object System.Threading.SemaphoreSlim($MaxConcurrent)
        
        foreach ($pkg in $packages) {
            $semaphore.Wait()
            $job = Start-Job -ScriptBlock {
                param($PkgUrl, $Retry)
                # 下载逻辑 + 重试
            } -ArgumentList $pkg.Url, $RetryCount
            $jobs += $job
        }
        
        # 等待完成并显示进度
        Wait-Job $jobs | Out-Null
        $jobs | Remove-Job
    }
}
```

### WPF客户端 - PowerShellService增强

```
// Services/PowerShellService.cs（新增方法）
public async Task<PowerShellResult> ExecuteModuleCmdletAsync(
    string cmdletName,
    Dictionary<string, object>? parameters = null,
    CancellationToken cancellationToken = default)
{
    // 构建PowerShell命令
    var scriptBuilder = new StringBuilder();
    scriptBuilder.AppendLine($"Import-Module '{_moduleBasePath}' -ErrorAction Stop");
    scriptBuilder.Append(cmdletName);
    
    if (parameters != null)
    {
        foreach (var param in parameters)
        {
            scriptBuilder.Append($" -{param.Key} ");
            scriptBuilder.Append(FormatParameterValue(param.Value));
        }
    }
    
    scriptBuilder.AppendLine(" | ConvertTo-Json -Depth 5");
    
    // 执行并解析结果
    var result = await ExecuteScriptAsync(scriptBuilder.ToString(), cancellationToken);
    
    if (result.Success && !string.IsNullOrEmpty(result.Output))
    {
        try
        {
            result.ParsedObjects = JsonSerializer.Deserialize<List<object>>(result.Output);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse PowerShell output as JSON");
        }
    }
    
    return result;
}

private string FormatParameterValue(object value)
{
    return value switch
    {
        string s => $"'{s.Replace("'", "''")}'",
        bool b => b ? "$true" : "$false",
        int or long or double => value.ToString(),
        _ => $"'{value}'"
    };
}
```

### WPF客户端 - WslManagerService重构示例

```
// Services/WslManagerService.cs（重构GetInstancesAsync）
public async Task<List<WslInstance>> GetInstancesAsync(CancellationToken cancellationToken = default)
{
    try
    {
        _logger.LogInformation("Retrieving WSL instances using PowerShell module");
        
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstance",
            parameters: null,
            cancellationToken);
        
        if (!result.Success)
        {
            _logger.LogError("Failed to get instances: {Error}", result.Error);
            return new List<WslInstance>();
        }
        
        // 解析PowerShell对象输出为WslInstance模型
        var instances = new List<WslInstance>();
        if (result.ParsedObjects != null)
        {
            foreach (var obj in result.ParsedObjects)
            {
                var instance = MapToWslInstance(obj);
                if (instance != null)
                {
                    instances.Add(instance);
                }
            }
        }
        
        _logger.LogInformation("Retrieved {Count} WSL instances", instances.Count);
        return instances;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving WSL instances");
        return new List<WslInstance>();
    }
}

private WslInstance? MapToWslInstance(object psObject)
{
    // 将PowerShell PSCustomObject映射为C# WslInstance模型
    // 使用JsonElement或动态类型解析
    try
    {
        var json = JsonSerializer.Serialize(psObject);
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        
        return new WslInstance
        {
            Name = element.GetProperty("Name").GetString() ?? "",
            State = element.GetProperty("State").GetString() ?? "Unknown",
            Version = element.GetProperty("Version").GetString() ?? "2",
            BasePath = element.GetProperty("BasePath").GetString() ?? "",
            DiskSize = element.GetProperty("DiskSize").GetInt64(),
            InstallTime = element.TryGetProperty("InstallTime", out var time) 
                ? DateTime.Parse(time.GetString()!) 
                : DateTime.MinValue,
            IsDefault = element.TryGetProperty("IsDefault", out var isDefault) 
                && isDefault.GetBoolean()
        };
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to map PowerShell object to WslInstance");
        return null;
    }
}
```

## 推荐使用的扩展

### SubAgent

- **code-explorer**
- **用途**：在实施阶段探索WPF客户端的具体实现细节，定位需要修改的服务类、接口和ViewModel
- **预期结果**：准确识别所有需要修改的C#文件路径，了解现有的依赖注入配置和服务调用链，确保重构不会遗漏关键调用点