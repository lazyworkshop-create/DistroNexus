# PowerShell 模块缺失功能实现方案

## 文档信息

- **版本**: 1.0
- **创建日期**: 2026-01-29
- **基于文档**: scripts_vs_module_comparison.md
- **目标模块**: src/PowerShell/DistroNexus
- **PowerShell 版本**: 7.0+

---

## 概述

本文档详细列出了 PowerShell 新版模块相对于旧版脚本缺失的所有功能，并为每个功能提供具体的实现方案。根据对比分析，共识别出 **25 项缺失功能**，分为 4 大类别。

### 缺失功能统计

| 类别 | 数量 | 优先级分布 |
|------|------|-----------|
| 实例管理功能 | 16 项 | 高优先级: 4, 中优先级: 7, 低优先级: 5 |
| 包管理功能 | 6 项 | 高优先级: 2, 中优先级: 3, 低优先级: 1 |
| 用户管理功能 | 2 项 | 高优先级: 1, 中优先级: 1 |
| 其他功能 | 1 项 | 低优先级: 1 |
| **总计** | **25 项** | **高: 7, 中: 11, 低: 7** |

### 实现原则

1. **向后兼容**: 所有新增参数使用可选默认值，保持现有调用方式不变
2. **代码风格一致**: 遵循现有模块的代码规范和 PowerShell 最佳实践
3. **渐进增强**: 优先实现高影响、低风险的功能
4. **性能考量**: 避免不必要的 WSL 实例启动和资源消耗
5. **安全优先**: 关键操作前进行检查和备份

---

## 第一部分：实例管理功能缺失项

### 1.1 缓存机制 【高优先级】

#### 功能描述
旧版 `list_distros.ps1` 支持将实例信息缓存到 `instances.json`，避免每次都扫描注册表和查询 WSL 状态。

#### 缺失原因
新版模块为了保证数据实时性，每次都直接查询系统状态，未实现本地缓存机制。

#### 影响分析
- **性能影响**: 每次调用 `Get-DistroNexusInstance` 都需要扫描注册表，耗时较长
- **用户体验**: 频繁查询时响应较慢
- **数据一致性**: 无缓存可能导致与旧版工具数据不一致

#### 实现方案

**新增文件**: `src/PowerShell/Private/Cache.ps1`

```powershell
function Get-InstanceCache {
    <#
    .SYNOPSIS
        从本地缓存文件读取实例信息
    
    .DESCRIPTION
        读取 config/instances.json 缓存文件，返回实例对象数组
        如果文件不存在或格式错误，返回 $null
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param()
    
    $cacheFile = Get-DistroNexusConfig -Key "InstancesCacheFile"
    if (-not $cacheFile) {
        $cacheFile = Join-Path $script:ModuleRoot "..\config\instances.json"
    }
    
    if (-not (Test-Path $cacheFile)) {
        Write-DistroNexusLog "Cache file not found: $cacheFile" -FileOnly
        return $null
    }
    
    try {
        $content = Get-Content -Path $cacheFile -Raw -ErrorAction Stop
        $instances = $content | ConvertFrom-Json
        
        # 验证缓存数据结构
        if ($instances -isnot [Array]) {
            $instances = @($instances)
        }
        
        Write-DistroNexusLog "Loaded $($instances.Count) instance(s) from cache" -FileOnly
        return $instances
    }
    catch {
        Write-DistroNexusLog "Failed to read cache: $_" -Level WARN
        return $null
    }
}

function Set-InstanceCache {
    <#
    .SYNOPSIS
        将实例信息写入本地缓存文件
    
    .DESCRIPTION
        将实例对象数组保存到 config/instances.json
        自动创建目录和备份旧文件
    
    .PARAMETER Instances
        实例对象数组
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [PSCustomObject[]]$Instances
    )
    
    $cacheFile = Get-DistroNexusConfig -Key "InstancesCacheFile"
    if (-not $cacheFile) {
        $cacheFile = Join-Path $script:ModuleRoot "..\config\instances.json"
    }
    
    $cacheDir = Split-Path -Parent $cacheFile
    if (-not (Test-Path $cacheDir)) {
        New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null
    }
    
    try {
        # 备份现有文件
        if (Test-Path $cacheFile) {
            $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
            $backupFile = "$cacheFile.$timestamp.bak"
            Copy-Item -Path $cacheFile -Destination $backupFile -Force
            
            # 保留最近 3 个备份
            $backups = Get-ChildItem -Path $cacheDir -Filter "instances.json.*.bak" |
                Sort-Object CreationTime -Descending
            if ($backups.Count -gt 3) {
                $backups | Select-Object -Skip 3 | Remove-Item -Force
            }
        }
        
        # 写入缓存
        $json = $Instances | ConvertTo-Json -Depth 5 -Compress:$false
        Set-Content -Path $cacheFile -Value $json -Force -ErrorAction Stop
        
        Write-DistroNexusLog "Cache updated: $($Instances.Count) instance(s)" -FileOnly
    }
    catch {
        Write-DistroNexusLog "Failed to write cache: $_" -Level WARN
    }
}

function Update-InstanceCache {
    <#
    .SYNOPSIS
        更新缓存中的单个实例信息
    
    .DESCRIPTION
        读取缓存，更新或添加指定实例，然后保存
        如果实例不存在则添加，存在则更新
    
    .PARAMETER Instance
        要更新的实例对象
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Instance
    )
    
    $cache = Get-InstanceCache
    if (-not $cache) {
        $cache = @()
    }
    
    # 查找现有实例
    $existing = $cache | Where-Object { $_.Name -eq $Instance.Name }
    
    if ($existing) {
        # 更新现有实例
        $index = [Array]::IndexOf($cache, $existing)
        $cache[$index] = $Instance
        Write-DistroNexusLog "Updated cache for instance: $($Instance.Name)" -FileOnly
    }
    else {
        # 添加新实例
        $cache += $Instance
        Write-DistroNexusLog "Added instance to cache: $($Instance.Name)" -FileOnly
    }
    
    Set-InstanceCache -Instances $cache
}

function Remove-InstanceFromCache {
    <#
    .SYNOPSIS
        从缓存中移除指定实例
    
    .PARAMETER Name
        实例名称
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )
    
    $cache = Get-InstanceCache
    if (-not $cache) {
        return
    }
    
    $updated = $cache | Where-Object { $_.Name -ne $Name }
    Set-InstanceCache -Instances $updated
    
    Write-DistroNexusLog "Removed instance from cache: $Name" -FileOnly
}
```

**修改文件**: `src/PowerShell/Public/Get-DistroNexusInstance.ps1`

添加参数和缓存逻辑：

```powershell
function Get-DistroNexusInstance {
    <#
    .SYNOPSIS
        Gets information about installed WSL instances.

    .DESCRIPTION
        Retrieves detailed information about WSL distributions registered on the system,
        including status, version, base path, and disk usage.
        
        Supports caching to improve performance. Use -ForceUpdate to refresh cache.

    .PARAMETER Name
        Filter by instance name. Supports wildcards.
    
    .PARAMETER UseCache
        Use cached instance data if available. Improves performance but may show stale data.
    
    .PARAMETER ForceUpdate
        Force refresh cache by querying system directly. Ignores cached data.

    .EXAMPLE
        Get-DistroNexusInstance
        # Gets all WSL instances from system

    .EXAMPLE
        Get-DistroNexusInstance -UseCache
        # Gets instances from cache (faster)
    
    .EXAMPLE
        Get-DistroNexusInstance -Name "Ubuntu*" -ForceUpdate
        # Forces refresh and filters Ubuntu instances

    .OUTPUTS
        PSCustomObject representing each WSL instance
    #>
    [CmdletBinding(DefaultParameterSetName = 'Default')]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $false, Position = 0)]
        [string]$Name,
        
        [Parameter(ParameterSetName = 'Cache')]
        [switch]$UseCache,
        
        [Parameter(ParameterSetName = 'ForceUpdate')]
        [switch]$ForceUpdate
    )
    
    begin {
        Initialize-DistroNexusLogger
        Write-DistroNexusLog "Scanning WSL instances..." -FileOnly
    }
    
    process {
        $instances = $null
        
        # 尝试使用缓存
        if ($UseCache -and -not $ForceUpdate) {
            $instances = Get-InstanceCache
            if ($instances) {
                Write-DistroNexusLog "Using cached instance data" -FileOnly
            }
        }
        
        # 如果没有缓存或强制更新，查询系统
        if (-not $instances -or $ForceUpdate) {
            # ... 原有的查询逻辑 ...
            # (保持现有代码不变，在最后添加缓存更新)
            
            # 查询完成后更新缓存
            if ($instances -and -not $Name) {
                # 只在查询所有实例时更新缓存
                Set-InstanceCache -Instances $instances
            }
        }
        
        # 应用名称过滤
        if ($Name -and $instances) {
            $instances = $instances | Where-Object { $_.Name -like $Name }
        }
        
        Write-DistroNexusLog "Found $($instances.Count) WSL instance(s)" -FileOnly
        return $instances
    }
}
```

**配置说明**:

在 `config/settings.json` 中可配置缓存路径：
```json
{
  "InstancesCacheFile": "config/instances.json"
}
```

---

### 1.2 强制更新参数 【高优先级】

#### 功能描述
旧版 `-ForceUpdate` 参数可强制刷新运行中实例的信息，包括重新查询状态和磁盘使用。

#### 缺失原因
新版未实现缓存机制，因此没有强制更新的概念。

#### 影响分析
- **数据准确性**: 无法强制刷新可能导致数据过时
- **用户控制**: 用户无法控制何时刷新数据

#### 实现方案

已在 1.1 缓存机制中实现 `-ForceUpdate` 参数。使用示例：

```powershell
# 强制刷新缓存
Get-DistroNexusInstance -ForceUpdate

# 使用缓存（快速）
Get-DistroNexusInstance -UseCache

# 默认行为（直接查询系统）
Get-DistroNexusInstance
```

---

### 1.3 Release 和 User 信息查询 【低优先级】

#### 功能描述
旧版会启动实例查询 `/etc/os-release` 获取发行版信息，并查询默认用户。

#### 缺失原因
- 查询需要启动实例，性能开销大
- 可能触发用户不期望的实例启动

#### 影响分析
- **信息完整性**: 缺少 Release 和 User 字段
- **性能**: 避免不必要的实例启动提升了性能
- **用户体验**: 减少意外的实例启动

#### 实现方案

新增可选参数，由用户决定是否查询：

**修改文件**: `src/PowerShell/Public/Get-DistroNexusInstance.ps1`

```powershell
function Get-DistroNexusInstance {
    # ... 前面的参数 ...
    
    [Parameter(Mandatory = $false)]
    [switch]$IncludeRelease,
    
    [Parameter(Mandatory = $false)]
    [switch]$IncludeUser
    
    # ... 在 process 块的实例处理部分添加 ...
    
    # 查询 Release 信息（可选）
    $release = $null
    if ($IncludeRelease) {
        try {
            $releaseInfo = wsl -d $distroName -e cat /etc/os-release 2>&1
            if ($LASTEXITCODE -eq 0 -and $releaseInfo) {
                # 解析 PRETTY_NAME 或 NAME
                $nameLine = $releaseInfo | Where-Object { $_ -match '^PRETTY_NAME=' }
                if ($nameLine) {
                    $release = ($nameLine -split '=', 2)[1] -replace '"', ''
                }
            }
        }
        catch {
            Write-Verbose "Failed to query release info for $distroName"
        }
    }
    
    # 查询默认用户（可选）
    $defaultUser = $null
    if ($IncludeUser) {
        try {
            # 从注册表读取 DefaultUid
            $defaultUid = $props.DefaultUid
            if ($defaultUid -and $defaultUid -ne 0) {
                $userInfo = wsl -d $distroName -e getent passwd $defaultUid 2>&1
                if ($LASTEXITCODE -eq 0 -and $userInfo) {
                    $defaultUser = ($userInfo -split ':')[0]
                }
            }
        }
        catch {
            Write-Verbose "Failed to query default user for $distroName"
        }
    }
    
    # 创建实例对象时添加字段
    $instance = [PSCustomObject]@{
        PSTypeName = 'DistroNexus.WslInstance'
        Name = $distroName
        State = $state
        Version = $version
        BasePath = $basePath
        DiskSize = $diskSize
        InstallTime = $installTime
        Release = $release      # 新增
        DefaultUser = $defaultUser  # 新增
        Guid = $key.PSChildName
    }
}
```

**使用示例**:

```powershell
# 基础信息（快速）
Get-DistroNexusInstance

# 包含发行版信息（会启动实例）
Get-DistroNexusInstance -IncludeRelease

# 包含所有信息（较慢）
Get-DistroNexusInstance -IncludeRelease -IncludeUser
```

**性能警告**: 在函数帮助中添加性能提示：

```powershell
.NOTES
    Using -IncludeRelease or -IncludeUser will start stopped instances to query information.
    This may take longer and affect instance state. Use with caution in automation scripts.
```

---

### 1.4 交互式安装模式 【低优先级】

#### 功能描述
旧版 `install_wsl_custom.ps1` 支持无参数运行时显示交互式菜单，让用户选择发行版和版本。

#### 缺失原因
新版模块采用完全参数化设计，符合 PowerShell 模块最佳实践，但失去了交互式便利性。

#### 影响分析
- **用户体验**: 新用户或手动操作时体验下降
- **自动化**: 参数化设计更适合自动化脚本
- **学习曲线**: 需要记住参数名称

#### 实现方案

**新增文件**: `src/PowerShell/Private/Interactive.ps1`

```powershell
function Show-DistroSelectionMenu {
    <#
    .SYNOPSIS
        显示发行版选择交互式菜单
    
    .DESCRIPTION
        从 distros.json 读取可用发行版，显示分类菜单供用户选择
        返回选中的发行版信息对象
    
    .OUTPUTS
        PSCustomObject with properties: Family, Version, Name, Url, DefaultName
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param()
    
    # 读取发行版目录
    $catalogFile = Get-DistroNexusConfig -Key "DistroCatalogFile"
    if (-not $catalogFile) {
        $catalogFile = Join-Path $script:ModuleRoot "..\config\distros.json"
    }
    
    if (-not (Test-Path $catalogFile)) {
        throw "Distribution catalog not found: $catalogFile"
    }
    
    try {
        $catalog = Get-Content -Path $catalogFile -Raw | ConvertFrom-Json
    }
    catch {
        throw "Failed to parse distribution catalog: $_"
    }
    
    # 构建发行版族列表
    $families = @()
    $familyIndex = 1
    foreach ($famKey in ($catalog.PSObject.Properties.Name | Sort-Object)) {
        $family = $catalog.$famKey
        $families += [PSCustomObject]@{
            Index = $familyIndex++
            Key = $famKey
            Name = $family.Name
            Data = $family
        }
    }
    
    # 显示发行版族选择
    Write-Host "`n=== Select Distribution Family ===" -ForegroundColor Cyan
    foreach ($fam in $families) {
        Write-Host "  [$($fam.Index)] $($fam.Name)" -ForegroundColor Yellow
    }
    Write-Host "  [0] Cancel" -ForegroundColor Gray
    
    do {
        $familyChoice = Read-Host "`nEnter family number"
        if ($familyChoice -eq '0') {
            return $null
        }
        $selectedFamily = $families | Where-Object { $_.Index -eq [int]$familyChoice }
    } while (-not $selectedFamily)
    
    # 构建版本列表
    $versions = @()
    $versionIndex = 1
    foreach ($verKey in ($selectedFamily.Data.Versions.PSObject.Properties.Name | Sort-Object)) {
        $version = $selectedFamily.Data.Versions.$verKey
        $versions += [PSCustomObject]@{
            Index = $versionIndex++
            Key = $verKey
            Data = $version
        }
    }
    
    # 显示版本选择
    Write-Host "`n=== Select Version for $($selectedFamily.Name) ===" -ForegroundColor Cyan
    foreach ($ver in $versions) {
        $cached = if (Test-Path (Join-Path (Get-DistroCachePath) $ver.Data.DefaultName)) { " [Cached]" } else { "" }
        Write-Host "  [$($ver.Index)] $($ver.Data.Name)$cached" -ForegroundColor Yellow
    }
    Write-Host "  [0] Back" -ForegroundColor Gray
    
    do {
        $versionChoice = Read-Host "`nEnter version number"
        if ($versionChoice -eq '0') {
            # 递归调用返回上一级
            return Show-DistroSelectionMenu
        }
        $selectedVersion = $versions | Where-Object { $_.Index -eq [int]$versionChoice }
    } while (-not $selectedVersion)
    
    # 返回选择结果
    return [PSCustomObject]@{
        FamilyKey = $selectedFamily.Key
        FamilyName = $selectedFamily.Name
        VersionKey = $selectedVersion.Key
        VersionData = $selectedVersion.Data
    }
}

function Get-DistroCachePath {
    <#
    .SYNOPSIS
        获取发行版缓存路径
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()
    
    $cachePath = Get-DistroNexusConfig -Key "DistroCachePath"
    if (-not $cachePath) {
        $cachePath = Join-Path $script:ModuleRoot "..\..\distro"
    }
    
    if (-not [System.IO.Path]::IsPathRooted($cachePath)) {
        $cachePath = Join-Path $script:ModuleRoot $cachePath
    }
    
    return [System.IO.Path]::GetFullPath($cachePath)
}
```

**修改文件**: `src/PowerShell/Public/Install-DistroNexusInstance.ps1`

添加 `-Interactive` 参数：

```powershell
function Install-DistroNexusInstance {
    [CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'Standard')]
    param(
        [Parameter(Mandatory = $true, ParameterSetName = 'Standard', Position = 0)]
        [string]$DistroName,
        
        [Parameter(Mandatory = $true, ParameterSetName = 'Standard', Position = 1)]
        [string]$InstallPath,
        
        [Parameter(Mandatory = $false, ParameterSetName = 'Interactive')]
        [switch]$Interactive,
        
        [Parameter(Mandatory = $false)]
        [string]$Username,
        
        [Parameter(Mandatory = $false)]
        [SecureString]$Password
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        # 交互式模式
        if ($Interactive) {
            Write-Host "=== DistroNexus Interactive Installation ===" -ForegroundColor Green
            
            # 选择发行版
            $selection = Show-DistroSelectionMenu
            if (-not $selection) {
                Write-Host "Installation cancelled." -ForegroundColor Yellow
                return
            }
            
            $DistroName = $selection.VersionData.DefaultName
            
            # 询问实例名称
            $defaultName = $selection.VersionData.DefaultName
            $instanceName = Read-Host "`nEnter instance name (default: $defaultName)"
            if ([string]::IsNullOrWhiteSpace($instanceName)) {
                $instanceName = $defaultName
            }
            
            # 询问安装路径
            $defaultPath = Join-Path $env:LOCALAPPDATA "WSL\$instanceName"
            Write-Host "`nDefault install path: $defaultPath" -ForegroundColor Gray
            $InstallPath = Read-Host "Enter install path (press Enter for default)"
            if ([string]::IsNullOrWhiteSpace($InstallPath)) {
                $InstallPath = $defaultPath
            }
            
            # 询问是否配置用户
            $configureUser = Read-Host "`nConfigure default user? (Y/N, default: N)"
            if ($configureUser -eq 'Y' -or $configureUser -eq 'y') {
                $Username = Read-Host "Enter username"
                $Password = Read-Host "Enter password" -AsSecureString
            }
            
            Write-Host "`n--- Installation Summary ---" -ForegroundColor Cyan
            Write-Host "Distribution: $($selection.VersionData.Name)"
            Write-Host "Instance Name: $instanceName"
            Write-Host "Install Path: $InstallPath"
            Write-Host "Default User: $(if ($Username) { $Username } else { '(none)' })"
            
            $confirm = Read-Host "`nProceed with installation? (Y/N)"
            if ($confirm -ne 'Y' -and $confirm -ne 'y') {
                Write-Host "Installation cancelled." -ForegroundColor Yellow
                return
            }
        }
        
        # ... 继续原有的安装逻辑 ...
    }
}
```

**使用示例**:

```powershell
# 交互式安装
Install-DistroNexusInstance -Interactive

# 参数化安装（保持原有方式）
Install-DistroNexusInstance -DistroName "Ubuntu-22.04" -InstallPath "D:\WSL\Ubuntu"
```

---

### 1.5 快速安装模式 【低优先级】

#### 功能描述
旧版支持 `-name` 参数配合 `settings.json` 中的 `DefaultDistro` 快速安装默认发行版。

#### 缺失原因
新版未实现配置文件读取和快速模式逻辑。

#### 影响分析
- **便利性**: 频繁安装相同发行版时效率较低
- **一致性**: 团队协作时难以统一默认配置

#### 实现方案

**新增参数**: `-Quick` 开关

**修改文件**: `src/PowerShell/Public/Install-DistroNexusInstance.ps1`

```powershell
function Install-DistroNexusInstance {
    param(
        # ... 现有参数 ...
        
        [Parameter(Mandatory = $false)]
        [switch]$Quick,
        
        [Parameter(Mandatory = $false)]
        [string]$InstanceName
    )
    
    process {
        # 快速模式逻辑
        if ($Quick) {
            Write-Host "Quick installation mode activated" -ForegroundColor Green
            
            # 读取默认配置
            $settings = Get-DistroNexusConfig
            $defaultDistro = $settings.DefaultDistro
            
            if (-not $defaultDistro) {
                throw "Quick mode requires DefaultDistro in settings.json. Run 'Set-DistroNexusConfig -Key DefaultDistro -Value Ubuntu-22.04'"
            }
            
            # 设置默认值
            if (-not $DistroName) {
                $DistroName = $defaultDistro
            }
            
            if (-not $InstanceName) {
                $InstanceName = Read-Host "Enter instance name"
                if ([string]::IsNullOrWhiteSpace($InstanceName)) {
                    throw "Instance name is required"
                }
            }
            
            if (-not $InstallPath) {
                $basePath = $settings.DefaultInstallPath
                if (-not $basePath) {
                    $basePath = Join-Path $env:LOCALAPPDATA "WSL"
                }
                $InstallPath = Join-Path $basePath $InstanceName
            }
            
            Write-Host "Installing $DistroName as $InstanceName to $InstallPath" -ForegroundColor Cyan
        }
        
        # ... 继续安装逻辑 ...
    }
}
```

**配置示例** (`config/settings.json`):

```json
{
  "DefaultDistro": "Ubuntu-22.04",
  "DefaultInstallPath": "D:\\WSL",
  "DefaultUsername": "dev",
  "AutoDownload": true
}
```

**使用示例**:

```powershell
# 快速安装（使用默认配置）
Install-DistroNexusInstance -Quick -InstanceName "MyUbuntu"

# 快速安装并指定路径
Install-DistroNexusInstance -Quick -InstanceName "DevEnv" -InstallPath "E:\WSL\Dev"
```

---

### 1.6 列表模式集成 【中优先级】

#### 功能描述
旧版 `install_wsl_custom.ps1 -List` 可直接列出可用发行版，无需单独调用。

#### 缺失原因
新版模块将功能拆分，列表功能独立为 `Get-DistroNexusPackage`。

#### 影响分析
- **集成度**: 用户需要记住两个命令
- **便利性**: 安装前查看可用版本需要额外步骤

#### 实现方案

添加 `-List` 参数到 `Install-DistroNexusInstance`：

```powershell
function Install-DistroNexusInstance {
    [CmdletBinding(DefaultParameterSetName = 'Standard')]
    param(
        # ... 现有参数 ...
        
        [Parameter(Mandatory = $false, ParameterSetName = 'List')]
        [switch]$List
    )
    
    process {
        # 列表模式
        if ($List) {
            Write-Host "`n=== Available Distributions ===" -ForegroundColor Cyan
            
            $packages = Get-DistroNexusPackage
            
            # 按族分组显示
            $grouped = $packages | Group-Object -Property Family
            
            foreach ($group in $grouped) {
                Write-Host "`n  $($group.Name):" -ForegroundColor Yellow
                foreach ($pkg in $group.Group) {
                    $cached = if ($pkg.IsCached) { " [Cached]" } else { "" }
                    Write-Host "    - $($pkg.Name)$cached" -ForegroundColor White
                    Write-Host "      DefaultName: $($pkg.DefaultName)" -ForegroundColor Gray
                }
            }
            
            Write-Host "`nTo install: Install-DistroNexusInstance -DistroName <DefaultName> -InstallPath <Path>" -ForegroundColor Green
            return
        }
        
        # ... 继续安装逻辑 ...
    }
}
```

**使用示例**:

```powershell
# 查看可用发行版
Install-DistroNexusInstance -List

# 或使用独立命令
Get-DistroNexusPackage
```

---

### 1.7 自动下载功能 【中优先级】

#### 功能描述
旧版安装时如果包不存在会自动调用 `download_all_distros.ps1` 下载。

#### 缺失原因
新版模块功能分离，需要手动先运行 `Save-DistroNexusPackage`。

#### 影响分析
- **自动化程度**: 需要多步操作
- **用户体验**: 新用户可能不知道需要先下载

#### 实现方案

添加 `-AutoDownload` 参数：

```powershell
function Install-DistroNexusInstance {
    param(
        # ... 现有参数 ...
        
        [Parameter(Mandatory = $false)]
        [switch]$AutoDownload
    )
    
    process {
        # 检查包是否存在
        $package = Get-DistroNexusPackage | Where-Object { $_.DefaultName -eq $DistroName }
        
        if (-not $package) {
            throw "Distribution not found in catalog: $DistroName. Run 'Update-DistroNexusCatalog' first."
        }
        
        if (-not $package.IsCached) {
            if ($AutoDownload) {
                Write-Host "Package not found locally. Downloading..." -ForegroundColor Yellow
                
                try {
                    Save-DistroNexusPackage -Name $DistroName -ErrorAction Stop
                    Write-Host "Download completed." -ForegroundColor Green
                }
                catch {
                    throw "Failed to download package: $_"
                }
            }
            else {
                $download = Read-Host "Package not cached. Download now? (Y/N)"
                if ($download -eq 'Y' -or $download -eq 'y') {
                    Save-DistroNexusPackage -Name $DistroName
                }
                else {
                    throw "Package not available. Download first with: Save-DistroNexusPackage -Name $DistroName"
                }
            }
        }
        
        # ... 继续安装逻辑 ...
    }
}
```

**使用示例**:

```powershell
# 自动下载并安装
Install-DistroNexusInstance -DistroName "Ubuntu-22.04" -InstallPath "D:\WSL\Ubuntu" -AutoDownload

# 或在快速模式中启用
Install-DistroNexusInstance -Quick -InstanceName "Test" -AutoDownload
```

---

### 1.8 包类型自动处理 【中优先级】

#### 功能描述
旧版支持自动识别和解压 `.appx`, `.zip`, `.tar.gz` 等多种包格式。

#### 缺失原因
新版简化了处理逻辑，仅支持直接导入 tar 文件。

#### 影响分析
- **兼容性**: 无法处理某些发行版的特殊格式
- **灵活性**: 用户需要手动预处理包文件

#### 实现方案

**新增文件**: `src/PowerShell/Private/PackageHandler.ps1`

```powershell
function Expand-DistroPackage {
    <#
    .SYNOPSIS
        解压发行版包到指定目录
    
    .DESCRIPTION
        自动识别包格式（.appx, .zip, .tar.gz, .tar）并解压
        返回解压后的 tar 文件路径或 rootfs 目录路径
    
    .PARAMETER PackagePath
        包文件路径
    
    .PARAMETER DestinationPath
        解压目标目录
    
    .OUTPUTS
        Hashtable with keys: Type, Path
        Type: 'tar' or 'rootfs'
        Path: 提取的 tar 文件路径或 rootfs 目录路径
    #>
    [CmdletBinding()]
    [OutputType([Hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateScript({ Test-Path $_ })]
        [string]$PackagePath,
        
        [Parameter(Mandatory = $false)]
        [string]$DestinationPath
    )
    
    $packageFile = Get-Item $PackagePath
    $extension = $packageFile.Extension.ToLower()
    
    if (-not $DestinationPath) {
        $DestinationPath = Join-Path $env:TEMP "DistroNexus_Extract_$(Get-Date -Format 'yyyyMMddHHmmss')"
    }
    
    if (-not (Test-Path $DestinationPath)) {
        New-Item -ItemType Directory -Path $DestinationPath -Force | Out-Null
    }
    
    Write-DistroNexusLog "Extracting package: $($packageFile.Name)" -FileOnly
    
    try {
        switch ($extension) {
            '.tar' {
                # 直接返回 tar 文件路径
                return @{
                    Type = 'tar'
                    Path = $PackagePath
                    TempDir = $null
                }
            }
            
            '.gz' {
                # tar.gz 文件
                if ($packageFile.Name -match '\.tar\.gz$') {
                    # 解压 tar.gz
                    $tarFile = Join-Path $DestinationPath ($packageFile.BaseName)
                    
                    Write-Host "Extracting .tar.gz archive..." -ForegroundColor Yellow
                    
                    # 使用 .NET 解压 gzip
                    $gzStream = New-Object System.IO.FileStream($PackagePath, [System.IO.FileMode]::Open)
                    $tarStream = New-Object System.IO.FileStream($tarFile, [System.IO.FileMode]::Create)
                    $gzipStream = New-Object System.IO.Compression.GZipStream($gzStream, [System.IO.Compression.CompressionMode]::Decompress)
                    
                    $gzipStream.CopyTo($tarStream)
                    
                    $gzipStream.Close()
                    $tarStream.Close()
                    $gzStream.Close()
                    
                    return @{
                        Type = 'tar'
                        Path = $tarFile
                        TempDir = $DestinationPath
                    }
                }
                else {
                    throw "Unsupported file format: $($packageFile.Name)"
                }
            }
            
            { $_ -in '.zip', '.appx' } {
                # ZIP 或 APPX 文件
                Write-Host "Extracting archive..." -ForegroundColor Yellow
                
                Expand-Archive -Path $PackagePath -DestinationPath $DestinationPath -Force
                
                # 查找 install.tar.gz 或 rootfs
                $installTar = Get-ChildItem -Path $DestinationPath -Filter "install.tar.gz" -Recurse | Select-Object -First 1
                
                if ($installTar) {
                    # 解压 install.tar.gz
                    $result = Expand-DistroPackage -PackagePath $installTar.FullName -DestinationPath $DestinationPath
                    $result.TempDir = $DestinationPath
                    return $result
                }
                
                # 查找 rootfs 目录
                $rootfs = Get-ChildItem -Path $DestinationPath -Directory -Filter "rootfs" -Recurse | Select-Object -First 1
                
                if ($rootfs) {
                    return @{
                        Type = 'rootfs'
                        Path = $rootfs.FullName
                        TempDir = $DestinationPath
                    }
                }
                
                # 查找 .tar 文件
                $tarFile = Get-ChildItem -Path $DestinationPath -Filter "*.tar" -Recurse | Select-Object -First 1
                
                if ($tarFile) {
                    return @{
                        Type = 'tar'
                        Path = $tarFile.FullName
                        TempDir = $DestinationPath
                    }
                }
                
                throw "No valid rootfs or tar file found in archive"
            }
            
            default {
                throw "Unsupported package format: $extension"
            }
        }
    }
    catch {
        # 清理临时目录
        if ($DestinationPath -and (Test-Path $DestinationPath)) {
            Remove-Item -Path $DestinationPath -Recurse -Force -ErrorAction SilentlyContinue
        }
        throw
    }
}

function Test-PackageFormat {
    <#
    .SYNOPSIS
        检查包格式是否受支持
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackagePath
    )
    
    if (-not (Test-Path $PackagePath)) {
        return $false
    }
    
    $extension = [System.IO.Path]::GetExtension($PackagePath).ToLower()
    $supportedExtensions = @('.tar', '.gz', '.zip', '.appx')
    
    return $supportedExtensions -contains $extension
}
```

**修改 Install-DistroNexusInstance**:

```powershell
# 在安装逻辑中使用 Expand-DistroPackage
$extractResult = Expand-DistroPackage -PackagePath $packagePath

try {
    if ($extractResult.Type -eq 'tar') {
        # 使用 tar 文件导入
        wsl --import $InstanceName $InstallPath $extractResult.Path
    }
    elseif ($extractResult.Type -eq 'rootfs') {
        # 使用 rootfs 目录创建 tar 并导入
        $tempTar = Join-Path $env:TEMP "$InstanceName.tar"
        
        # 使用 tar 创建归档
        tar -czf $tempTar -C $extractResult.Path .
        
        wsl --import $InstanceName $InstallPath $tempTar
        
        Remove-Item $tempTar -Force
    }
}
finally {
    # 清理临时文件
    if ($extractResult.TempDir -and (Test-Path $extractResult.TempDir)) {
        Remove-Item -Path $extractResult.TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
```

---

### 1.9 实例注册维护 【中优先级】

#### 功能描述
旧版会自动更新 `instances.json` 维护本地实例注册表。

#### 缺失原因
新版不维护本地配置文件，依赖系统注册表。

#### 影响分析
- **数据持久化**: 无法保存自定义元数据
- **兼容性**: 与旧版工具数据不互通

#### 实现方案

利用 1.1 中实现的缓存机制，在关键操作后自动更新缓存：

```powershell
# Install 后更新
$instance = [PSCustomObject]@{
    Name = $InstanceName
    State = "Stopped"
    Version = "2"
    BasePath = $InstallPath
    DiskSize = 0
    InstallTime = Get-Date
    Release = $null
    DefaultUser = $Username
    Guid = $null
}
Update-InstanceCache -Instance $instance

# Remove 后移除
Remove-InstanceFromCache -Name $InstanceName

# Move/Rename 后更新
$instance = Get-DistroNexusInstance -Name $Name
Update-InstanceCache -Instance $instance
```

---

### 1.10 完整用户配置 【中优先级】

#### 功能描述
旧版安装时会完整配置用户：创建用户、设置密码、添加到 sudo/wheel 组、配置 wsl.conf。

#### 缺失原因
新版简化了用户配置逻辑，在注释中说明"简化了用户配置"。

#### 影响分析
- **功能完整性**: 用户配置不完整
- **用户体验**: 需要手动进入实例配置

#### 实现方案

**新增文件**: `src/PowerShell/Private/UserConfig.ps1`

```powershell
function Set-DistroDefaultUser {
    <#
    .SYNOPSIS
        在 WSL 实例中配置默认用户
    
    .DESCRIPTION
        创建用户、设置密码、添加到 sudo 组、配置 wsl.conf
    
    .PARAMETER DistroName
        实例名称
    
    .PARAMETER Username
        用户名
    
    .PARAMETER Password
        密码（SecureString）
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DistroName,
        
        [Parameter(Mandatory = $true)]
        [string]$Username,
        
        [Parameter(Mandatory = $true)]
        [SecureString]$Password
    )
    
    Write-Host "Configuring default user: $Username" -ForegroundColor Cyan
    
    # 转换 SecureString 为明文（仅用于传递给 WSL）
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
    $plainPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)
    
    try {
        # 1. 创建用户
        Write-Host "  Creating user..." -ForegroundColor Yellow
        wsl -d $DistroName -e bash -c "useradd -m -s /bin/bash '$Username'" 2>&1 | Out-Null
        
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to create user (may already exist)"
        }
        
        # 2. 设置密码
        Write-Host "  Setting password..." -ForegroundColor Yellow
        $passwordCommand = "echo '${Username}:${plainPassword}' | chpasswd"
        wsl -d $DistroName -e bash -c $passwordCommand 2>&1 | Out-Null
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to set password"
        }
        
        # 3. 添加到 sudo 组
        Write-Host "  Adding to sudo group..." -ForegroundColor Yellow
        wsl -d $DistroName -e bash -c "usermod -aG sudo '$Username'" 2>&1 | Out-Null
        
        # 尝试添加到 wheel 组（某些发行版使用 wheel 而不是 sudo）
        wsl -d $DistroName -e bash -c "groups wheel" 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            wsl -d $DistroName -e bash -c "usermod -aG wheel '$Username'" 2>&1 | Out-Null
        }
        
        # 4. 配置 wsl.conf
        Write-Host "  Configuring wsl.conf..." -ForegroundColor Yellow
        
        $wslConfContent = @"
[user]
default=$Username
"@
        
        # 检查 wsl.conf 是否存在
        $wslConfExists = wsl -d $DistroName -e bash -c "test -f /etc/wsl.conf && echo exists" 2>$null
        
        if ($wslConfExists -eq "exists") {
            # 检查是否已有 [user] 段
            $hasUserSection = wsl -d $DistroName -e bash -c "grep -q '^\[user\]' /etc/wsl.conf && echo yes" 2>$null
            
            if ($hasUserSection -eq "yes") {
                # 更新现有的 default 设置
                wsl -d $DistroName -e bash -c "sed -i '/^\[user\]/,/^\[/s/^default=.*/default=$Username/' /etc/wsl.conf"
            }
            else {
                # 追加 [user] 段
                wsl -d $DistroName -e bash -c "echo '' >> /etc/wsl.conf && echo '[user]' >> /etc/wsl.conf && echo 'default=$Username' >> /etc/wsl.conf"
            }
        }
        else {
            # 创建新的 wsl.conf
            $escapedContent = $wslConfContent -replace "'", "'\\''"
            wsl -d $DistroName -e bash -c "echo '$escapedContent' | sudo tee /etc/wsl.conf > /dev/null"
        }
        
        # 5. 更新注册表中的 DefaultUid
        Write-Host "  Updating registry..." -ForegroundColor Yellow
        
        $uid = wsl -d $DistroName -e id -u $Username
        if ($uid -and $LASTEXITCODE -eq 0) {
            # 查找实例的注册表键
            $lxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
            $keys = Get-ChildItem -Path $lxssPath
            
            foreach ($key in $keys) {
                $props = Get-ItemProperty -Path $key.PSPath
                if ($props.DistributionName -eq $DistroName) {
                    Set-ItemProperty -Path $key.PSPath -Name "DefaultUid" -Value ([int]$uid)
                    Write-DistroNexusLog "Set DefaultUid to $uid for $DistroName" -FileOnly
                    break
                }
            }
        }
        
        Write-Host "User configuration completed successfully." -ForegroundColor Green
        Write-DistroNexusLog "Configured default user $Username for $DistroName" -FileOnly
    }
    finally {
        # 清理明文密码
        $plainPassword = $null
    }
}
```

**集成到 Install-DistroNexusInstance**:

```powershell
# 在 wsl --import 成功后
if ($Username -and $Password) {
    try {
        Set-DistroDefaultUser -DistroName $InstanceName -Username $Username -Password $Password
    }
    catch {
        Write-Warning "Failed to configure default user: $_"
        Write-Host "You can configure the user later with: Set-DistroNexusCredential -Name $InstanceName -Username $Username" -ForegroundColor Yellow
    }
}
```

---

### 1.11 交互式卸载 【低优先级】

#### 功能描述
旧版 `uninstall_wsl_custom.ps1` 无参数运行时显示实例列表供选择。

#### 缺失原因
新版完全参数化。

#### 影响分析
- **用户体验**: 手动操作时需要先查询再卸载

#### 实现方案

添加 `-Interactive` 参数到 `Remove-DistroNexusInstance`：

```powershell
function Remove-DistroNexusInstance {
    [CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'Standard')]
    param(
        [Parameter(Mandatory = $true, ParameterSetName = 'Standard', Position = 0, ValueFromPipelineByPropertyName = $true)]
        [string]$Name,
        
        [Parameter(Mandatory = $false, ParameterSetName = 'Interactive')]
        [switch]$Interactive,
        
        [Parameter(Mandatory = $false)]
        [switch]$Force,
        
        [Parameter(Mandatory = $false)]
        [switch]$KeepFiles
    )
    
    process {
        if ($Interactive) {
            # 获取所有实例
            $instances = Get-DistroNexusInstance
            
            if ($instances.Count -eq 0) {
                Write-Host "No instances found." -ForegroundColor Yellow
                return
            }
            
            # 显示选择菜单
            Write-Host "`n=== Select Instance to Remove ===" -ForegroundColor Cyan
            
            $index = 1
            $menu = @()
            foreach ($inst in $instances) {
                $menu += [PSCustomObject]@{
                    Index = $index++
                    Instance = $inst
                }
                
                $size = if ($inst.DiskSize -gt 0) { 
                    "{0:N2} GB" -f ($inst.DiskSize / 1GB) 
                } else { 
                    "Unknown" 
                }
                
                Write-Host "  [$($menu[-1].Index)] $($inst.Name) - $($inst.State) - $size" -ForegroundColor Yellow
            }
            Write-Host "  [0] Cancel" -ForegroundColor Gray
            
            # 获取用户选择
            do {
                $choice = Read-Host "`nEnter instance number"
                if ($choice -eq '0') {
                    Write-Host "Cancelled." -ForegroundColor Yellow
                    return
                }
                $selected = $menu | Where-Object { $_.Index -eq [int]$choice }
            } while (-not $selected)
            
            $Name = $selected.Instance.Name
            
            # 确认删除
            Write-Host "`nYou are about to remove: $Name" -ForegroundColor Red
            Write-Host "  Base Path: $($selected.Instance.BasePath)" -ForegroundColor Gray
            Write-Host "  Disk Size: $("{0:N2} GB" -f ($selected.Instance.DiskSize / 1GB))" -ForegroundColor Gray
            
            if (-not $Force) {
                $confirm = Read-Host "`nAre you sure? (yes/no)"
                if ($confirm -ne 'yes') {
                    Write-Host "Cancelled." -ForegroundColor Yellow
                    return
                }
                
                $deleteFiles = Read-Host "Delete files after unregistration? (yes/no)"
                if ($deleteFiles -eq 'yes') {
                    $KeepFiles = $false
                }
                else {
                    $KeepFiles = $true
                }
            }
        }
        
        # ... 继续原有的卸载逻辑 ...
    }
}
```

**使用示例**:

```powershell
# 交互式卸载
Remove-DistroNexusInstance -Interactive

# 参数化卸载（保持原有方式）
Remove-DistroNexusInstance -Name "Ubuntu-Test"
```

---

