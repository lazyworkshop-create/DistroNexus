# WPF客户端与PowerShell模块架构重构 - 实施检查清单

**文档版本**: 1.0  
**生成日期**: 2026-01-30  
**项目**: DistroNexus v2.0.0

---

## 执行摘要

本文档是WPF客户端与PowerShell模块架构重构项目的完整实施检查清单。项目目标是：

1. **消除重复实现**：WPF客户端目前未使用PowerShell模块，所有WSL操作通过内联脚本重复实现
2. **建立清晰分层**：PowerShell模块作为后台服务层，WPF作为UI层
3. **功能完整性**：补全PowerShell模块缺失的18项功能，使其可独立满足自动化需求
4. **保持向后兼容**：所有修改不破坏现有功能

### 核心架构改进

**重构前** ❌：
```
WPF → PowerShellService → powershell.exe → wsl.exe
      (内联脚本)                           (直接调用)
```

**重构后** ✅：
```
WPF → PowerShellService → powershell.exe → DistroNexus模块 → wsl.exe
      (Import-Module)                       (封装业务逻辑)
```

---

## 第一阶段：PowerShell模块功能补全 ✅ (已部分完成)

### 1.1 实例缓存机制 ✅ COMPLETED

**文件**: `src/PowerShell/Private/Cache.ps1`  
**修改文件**: `src/PowerShell/Public/Get-DistroNexusInstance.ps1`

**实现内容**:
- ✅ `Get-InstanceCache`: 从instances.json加载缓存（10分钟有效期）
- ✅ `Set-InstanceCache`: 保存实例信息到缓存
- ✅ `Update-InstanceCache`: 强制刷新缓存
- ✅ `Clear-InstanceCache`: 清空缓存文件
- ✅ `Get-DistroNexusInstance`增加`-ForceUpdate`参数绕过缓存
- ✅ `Get-DistroNexusInstance`增加`-IncludeRelease`参数查询发行版信息
- ✅ `Get-DistroNexusInstance`增加`-IncludeUser`参数查询默认用户

**用户价值**:
- 减少注册表扫描频率，提升性能
- 默认使用缓存，通过`-ForceUpdate`手动刷新
- 缓存文件: `config/instances.json`

**示例**:
```powershell
# 使用缓存（快速）
Get-DistroNexusInstance

# 强制刷新
Get-DistroNexusInstance -ForceUpdate

# 查询详细信息（慢，需启动实例）
Get-DistroNexusInstance -IncludeRelease -IncludeUser
```

---

### 1.2 包格式处理 ✅ COMPLETED

**文件**: `src/PowerShell/Private/PackageHandler.ps1`

**实现内容**:
- ✅ `Test-PackageFormat`: 验证包文件格式（.tar/.tar.gz/.appx/.zip）
- ✅ `Get-PackageFormat`: 识别包类型
- ✅ `Expand-DistroPackage`: 自动解压各种格式到.tar
  - 支持`.tar.gz`解压为`.tar`
  - 支持`.appx`提取install.tar.gz
  - 支持`.appxbundle`解包并提取
  - 支持`.zip`转换为tar格式
- ✅ `Test-TarCommand`: 检测系统tar命令可用性

**用户价值**:
- 统一包格式处理，无需手动转换
- 支持Microsoft Store下载的.appx格式
- 自动化安装流程

**示例**:
```powershell
# 自动解压appx包为tar
Expand-DistroPackage -PackagePath "ubuntu.appx" -DestinationPath "ubuntu.tar"

# 解压tar.gz
Expand-DistroPackage -PackagePath "debian.tar.gz" -Force
```

---

### 1.3 终端启动辅助 ✅ COMPLETED

**文件**: `src/PowerShell/Private/TerminalLauncher.ps1`

**实现内容**:
- ✅ `Find-TerminalPath`: 自动检测Windows Terminal或CMD
- ✅ `Invoke-Terminal`: 启动终端并打开WSL实例
  - 支持Windows Terminal（wt.exe）
  - 回退到CMD（cmd.exe）
  - 支持指定启动路径（`-StartPath`）
- ✅ `Test-TerminalAvailable`: 检测特定终端是否可用
- ✅ `Get-AvailableTerminals`: 列出所有可用终端

**用户价值**:
- 一键启动实例并打开终端
- 自动选择最佳终端（优先Windows Terminal）
- 支持在指定目录启动

**示例**:
```powershell
# 启动Ubuntu并打开终端
Invoke-Terminal -InstanceName "Ubuntu-22.04"

# 指定启动路径
Invoke-Terminal -InstanceName "Debian" -StartPath "/var/www"

# 强制使用CMD
Invoke-Terminal -InstanceName "Ubuntu" -PreferredTerminal "CMD"
```

---

### 1.4 批量下载功能 ⏳ TODO

**文件**: `src/PowerShell/Public/Save-DistroNexusPackage.ps1` (需修改)

**实现内容** (待完成):
- ❌ 增加`-Family`参数：批量下载同系列包（如"Ubuntu"）
- ❌ 增加`-All`参数：下载所有未缓存包
- ❌ 增加`-MaxConcurrent`参数：并发下载控制（1-10，默认3）
- ❌ 增加`-RetryCount`参数：失败重试次数（0-10，默认3）
- ❌ 改进进度显示：百分比、速度、ETA
- ❌ 使用PowerShell Jobs实现并发下载
- ❌ 指数退避重试策略

**实施指南**:
参考`docs/PowerShell-Module-Missing-Features-Part2.md`第2.1节的完整代码示例。

**预期用法**:
```powershell
# 下载整个Ubuntu系列
Save-DistroNexusPackage -Family "Ubuntu"

# 下载所有包（并发3个）
Save-DistroNexusPackage -All -MaxConcurrent 3

# 带重试机制
Save-DistroNexusPackage -Name "Debian-11" -RetryCount 5
```

---

### 1.5 安装功能增强 ⏳ TODO

**文件**: `src/PowerShell/Public/Install-DistroNexusInstance.ps1` (需修改)

**实现内容** (待完成):
- ❌ 增加`-Interactive`参数：使用Out-GridView选择发行版
- ❌ 增加`-AutoDownload`参数：包未缓存时自动下载
- ❌ 增加`-OpenTerminal`参数：安装后自动打开终端
- ❌ 增加完整用户配置：`-Shell`, `-Locale`, `-TimeZone`
- ❌ 集成`Expand-DistroPackage`自动处理包格式
- ❌ 集成`Invoke-Terminal`终端启动

**依赖**:
- ✅ PackageHandler.ps1（已完成）
- ✅ TerminalLauncher.ps1（已完成）

**预期用法**:
```powershell
# 交互式安装
Install-DistroNexusInstance -Interactive

# 自动下载并安装
Install-DistroNexusInstance -DistroName "Ubuntu-22.04" -AutoDownload

# 安装后打开终端
Install-DistroNexusInstance -DistroName "Debian" -OpenTerminal
```

---

### 1.6 启动功能增强 ⏳ TODO

**文件**: `src/PowerShell/Public/Start-DistroNexusInstance.ps1` (需修改)

**实现内容** (待完成):
- ❌ 增加`-OpenTerminal`参数：启动后自动打开终端
- ❌ 增加`-StartPath`参数：终端启动路径
- ❌ 集成`Invoke-Terminal`调用

**依赖**:
- ✅ TerminalLauncher.ps1（已完成）

**预期用法**:
```powershell
# 启动并打开终端
Start-DistroNexusInstance -Name "Ubuntu-22.04" -OpenTerminal

# 在指定路径启动
Start-DistroNexusInstance -Name "Debian" -OpenTerminal -StartPath "/var/www"
```

---

### 1.7 移动功能增强 ⏳ TODO

**文件**: `src/PowerShell/Public/Move-DistroNexusInstance.ps1` (需修改)

**实现内容** (待完成):
- ❌ 增加非空目录检查逻辑
- ❌ 增加`-Force`参数：覆盖非空目录警告
- ❌ 移动后自动恢复DefaultUid配置（从注册表备份）

**实施重点**:
```powershell
# 检查目标目录
if ((Test-Path $NewPath) -and (Get-ChildItem $NewPath)) {
    if (-not $Force) {
        Write-Warning "Target directory is not empty: $NewPath"
        return $false
    }
}

# 备份DefaultUid
$oldGuid = (Get-DistroNexusInstance -Name $Name).Guid
$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss\$oldGuid"
$defaultUid = (Get-ItemProperty $regPath).DefaultUid

# 移动后恢复
# ...执行移动...
$newGuid = (Get-DistroNexusInstance -Name $Name).Guid
Set-ItemProperty -Path "HKCU:\...\Lxss\$newGuid" -Name "DefaultUid" -Value $defaultUid
```

---

### 1.8 凭证功能增强 ⏳ TODO

**文件**: `src/PowerShell/Public/Set-DistroNexusCredential.ps1` (需修改)

**实现内容** (待完成):
- ❌ 自动配置`/etc/wsl.conf`（default用户、automount等）
- ❌ 增加`-AddToWheel`参数：支持Fedora/RHEL的wheel组
- ❌ 增加`-ConfigureWslConf`参数：控制是否配置wsl.conf（默认true）

**实施重点**:
```powershell
# 创建wsl.conf
$wslConfContent = @"
[user]
default=$Username

[automount]
enabled=true
root=/mnt/
"@

wsl --distribution $Name -- bash -c "echo '$wslConfContent' | sudo tee /etc/wsl.conf"

# 检测发行版类型决定sudo/wheel
$release = wsl --distribution $Name -- bash -c "cat /etc/os-release"
if ($release -match "fedora|rhel|centos") {
    # 使用wheel组
    wsl --distribution $Name -- bash -c "sudo usermod -aG wheel $Username"
}
```

---

### 1.9 目录备份功能 ⏳ TODO

**文件**: `src/PowerShell/Public/Update-DistroNexusCatalog.ps1` (需修改)

**实现内容** (待完成):
- ❌ 更新前自动备份`distros.json`
- ❌ 增加`-KeepBackups`参数：保留最近N个备份（默认3）
- ❌ 轮转备份：`.bak`, `.bak.1`, `.bak.2`

**实施重点**:
```powershell
# 备份逻辑
$configPath = Join-Path (Get-DistroNexusConfig).ConfigRoot "distros.json"
if (Test-Path $configPath) {
    # 轮转备份
    for ($i = $KeepBackups - 1; $i -ge 1; $i--) {
        $oldBackup = "$configPath.bak.$i"
        $newBackup = "$configPath.bak.$($i + 1)"
        if (Test-Path $oldBackup) {
            Move-Item $oldBackup $newBackup -Force
        }
    }
    
    # 创建新备份
    Copy-Item $configPath "$configPath.bak" -Force
}
```

---

### 1.10 缓存统计Cmdlet ⏳ TODO

**文件**: `src/PowerShell/Public/Get-DistroNexusCache.ps1` (新建)

**实现内容** (待完成):
- ❌ 创建新Cmdlet查询缓存统计
- ❌ 显示缓存路径、包数量、总大小
- ❌ `-Detailed`参数显示每个缓存包详情

**预期输出**:
```powershell
PS> Get-DistroNexusCache

CachePath    : D:\WSL\Cache
PackageCount : 5
TotalSize    : 3.2 GB

PS> Get-DistroNexusCache -Detailed

Name              Size       CachedAt
----              ----       --------
ubuntu-22.04.tar  850 MB     2026-01-30 10:30:00
debian-11.tar     620 MB     2026-01-30 11:00:00
...
```

---

## 第二阶段：WPF服务层改造 ⏳ TODO

### 2.1 PowerShellService增强 ⏳ TODO

**文件**: `src/Client/DistroNexus.Core/Services/PowerShellService.cs`

**实现内容** (待完成):
- ❌ 新增`ExecuteModuleCmdletAsync`方法
- ❌ 支持导入DistroNexus模块并执行Cmdlet
- ❌ 参数格式化（字符串转义、布尔值、数组等）
- ❌ 结果解析（JSON序列化PowerShell对象）
- ❌ Write-Progress输出映射到IProgress<T>

**代码框架**:
```csharp
public async Task<PowerShellResult> ExecuteModuleCmdletAsync(
    string cmdletName,
    Dictionary<string, object>? parameters = null,
    CancellationToken cancellationToken = default)
{
    var scriptBuilder = new StringBuilder();
    
    // 导入模块
    var modulePath = Path.Combine(GetProjectRoot(), "src", "PowerShell");
    scriptBuilder.AppendLine($"Import-Module '{modulePath}' -ErrorAction Stop");
    
    // 构建Cmdlet调用
    scriptBuilder.Append(cmdletName);
    if (parameters != null)
    {
        foreach (var param in parameters)
        {
            scriptBuilder.Append($" -{param.Key} {FormatParameterValue(param.Value)}");
        }
    }
    
    // 输出为JSON以便解析
    scriptBuilder.AppendLine(" | ConvertTo-Json -Depth 5");
    
    var result = await ExecuteScriptAsync(scriptBuilder.ToString(), cancellationToken);
    
    // 解析JSON输出
    if (result.Success && !string.IsNullOrEmpty(result.Output))
    {
        result.ParsedObjects = JsonSerializer.Deserialize<List<JsonElement>>(result.Output);
    }
    
    return result;
}

private string FormatParameterValue(object value)
{
    return value switch
    {
        string s => $"'{s.Replace("'", "''")}'",
        bool b => b ? "$true" : "$false",
        int or long or double => value.ToString()!,
        _ => $"'{value}'"
    };
}
```

---

### 2.2 PowerShell结果模型 ⏳ TODO

**文件**: 
- `src/Client/DistroNexus.Core/Models/PowerShellResult.cs` (新建)
- `src/Client/DistroNexus.Core/Models/ModuleCallOptions.cs` (新建)

**实现内容** (待完成):
- ❌ `PowerShellResult`类：统一PowerShell执行结果
- ❌ `ModuleCallOptions`类：模块调用配置选项

**代码框架**:
```csharp
// PowerShellResult.cs
public class PowerShellResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public List<JsonElement>? ParsedObjects { get; set; }
}

// ModuleCallOptions.cs
public class ModuleCallOptions
{
    public bool UseModuleFallback { get; set; } = true;  // 失败时回退到内联脚本
    public int TimeoutSeconds { get; set; } = 30;        // 超时时间
    public bool LogVerbose { get; set; } = false;        // 详细日志
}
```

---

## 第三阶段：WPF调用重构 ⏳ TODO

### 3.1 重构GetInstancesAsync ⏳ TODO

**文件**: `src/Client/DistroNexus.Core/Services/WslManagerService.cs`

**实现内容** (待完成):
- ❌ 改为调用`Get-DistroNexusInstance` Cmdlet
- ❌ 实现PowerShell对象到WslInstance的映射
- ❌ 保留超时机制和错误处理

**代码框架**:
```csharp
public async Task<List<WslInstance>> GetInstancesAsync(CancellationToken ct = default)
{
    try
    {
        _logger.LogInformation("Retrieving instances using PowerShell module");
        
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstance",
            parameters: null,
            ct);
        
        if (!result.Success)
        {
            _logger.LogError("Failed to get instances: {Error}", result.Error);
            return new List<WslInstance>();
        }
        
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
        
        _logger.LogInformation("Retrieved {Count} instances", instances.Count);
        return instances;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving instances");
        return new List<WslInstance>();
    }
}

private WslInstance? MapToWslInstance(JsonElement element)
{
    try
    {
        return new WslInstance
        {
            Name = element.GetProperty("Name").GetString() ?? "",
            State = element.GetProperty("State").GetString() ?? "Unknown",
            Version = element.GetProperty("Version").GetString() ?? "2",
            BasePath = element.GetProperty("BasePath").GetString() ?? "",
            DiskSize = element.GetProperty("DiskSize").GetInt64(),
            InstallTime = DateTime.Parse(element.GetProperty("InstallTime").GetString()!),
            IsDefault = element.TryGetProperty("IsDefault", out var isDefault) && isDefault.GetBoolean()
        };
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to map instance");
        return null;
    }
}
```

---

### 3.2 重构基础操作方法 ⏳ TODO

**文件**: `src/Client/DistroNexus.Core/Services/WslManagerService.cs`

**实现内容** (待完成):
- ❌ `StartInstanceAsync` → `Start-DistroNexusInstance`
- ❌ `StopInstanceAsync` → `Stop-DistroNexusInstance`
- ❌ `RemoveInstanceAsync` → `Remove-DistroNexusInstance`
- ❌ `RenameInstanceAsync` → `Rename-DistroNexusInstance`

**代码模式** (以StartInstanceAsync为例):
```csharp
public async Task<bool> StartInstanceAsync(string instanceName, CancellationToken ct = default)
{
    try
    {
        _logger.LogInformation("Starting instance: {Name}", instanceName);
        
        var parameters = new Dictionary<string, object>
        {
            ["Name"] = instanceName
        };
        
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Start-DistroNexusInstance",
            parameters,
            ct);
        
        if (result.Success)
        {
            _logger.LogInformation("Instance {Name} started successfully", instanceName);
            return true;
        }
        else
        {
            _logger.LogError("Failed to start instance: {Error}", result.Error);
            return false;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error starting instance {Name}", instanceName);
        return false;
    }
}
```

---

### 3.3 重构MoveInstanceAsync ⏳ TODO

**文件**: `src/Client/DistroNexus.Core/Services/WslManagerService.cs`

**实现内容** (待完成):
- ❌ 调用`Move-DistroNexusInstance` Cmdlet
- ❌ 实现Write-Progress到IProgress的映射
- ❌ 保留进度回调机制

**进度映射策略**:
```csharp
// 解析PowerShell的Write-Progress输出
// 输出格式: "PROGRESS|25|Exporting instance..."
// 映射到: progress?.Report((25.0, "Exporting instance..."))

if (line.StartsWith("PROGRESS|"))
{
    var parts = line.Split('|');
    if (parts.Length >= 3 && int.TryParse(parts[1], out var percent))
    {
        progress?.Report((percent, parts[2]));
    }
}
```

---

## 实施路线图

### Phase 1: PowerShell模块补全 (1-2周)

**已完成** ✅:
1. 实例缓存机制
2. 包格式处理
3. 终端启动辅助

**待完成** ⏳:
4. 批量下载功能 (2天)
5. 安装功能增强 (2天)
6. 启动功能增强 (1天)
7. 移动功能增强 (1天)
8. 凭证功能增强 (1天)
9. 目录备份功能 (0.5天)
10. 缓存统计Cmdlet (0.5天)

**预计剩余时间**: 8天

---

### Phase 2: WPF服务层改造 (3-4天)

**待完成** ⏳:
1. PowerShellService增强 (1.5天)
2. PowerShell结果模型 (0.5天)

**关键里程碑**: PowerShellService可成功调用PowerShell模块Cmdlet

---

### Phase 3: WPF调用重构 (5-7天)

**待完成** ⏳:
1. 重构GetInstancesAsync (1天)
2. 重构基础操作方法 (2天)
3. 重构MoveInstanceAsync (1天)
4. 重构InstallInstanceAsync (2天)
5. 重构SetCredentialsAsync (1天)

**关键里程碑**: WPF完全通过PowerShell模块操作WSL，移除所有内联脚本

---

## 验证清单

### PowerShell模块验证

```powershell
# 测试缓存机制
Get-DistroNexusInstance                    # 应使用缓存
Get-DistroNexusInstance -ForceUpdate      # 应跳过缓存

# 测试包处理
Test-PackageFormat -Path "ubuntu.tar.gz"  # 应返回$true
Expand-DistroPackage -PackagePath "test.appx"  # 应成功解压

# 测试终端启动
Invoke-Terminal -InstanceName "Ubuntu"    # 应打开终端
```

### WPF客户端验证

1. 启动WPF应用
2. 查看实例列表（应显示所有实例）
3. 启动/停止实例
4. 移动实例（观察进度条）
5. 安装新实例
6. 检查日志确认调用了PowerShell模块

---

## 风险评估

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| PowerShell模块路径检测失败 | 高 | 添加配置项允许手动指定模块路径 |
| JSON序列化不兼容 | 中 | 添加回退机制使用原内联脚本 |
| 性能下降 | 中 | 使用缓存机制，基准测试对比 |
| 现有功能破坏 | 高 | 渐进式迁移，每个方法独立测试 |

---

## 后续改进方向

1. **WPF批量操作**：多选实例批量启动/停止/删除
2. **WhatIf预览**：删除/移动前显示预览对话框
3. **配置导出/导入**：备份和恢复WPF设置
4. **详细日志查看器**：WPF内置日志浏览器
5. **PowerShell脚本编辑器**：WPF内嵌脚本执行面板

---

## 附录

### A. 相关文档

- `docs/PowerShell-vs-WPF-Comparison.md` - 详细功能对比
- `docs/PowerShell模块功能补全.md` - PowerShell补全方案
- `docs/PowerShell-Module-Missing-Features-Part2.md` - 包管理详细实现

### B. 代码位置索引

**PowerShell模块**:
- Private函数: `src/PowerShell/Private/`
- Public Cmdlet: `src/PowerShell/Public/`
- 配置文件: `config/`

**WPF客户端**:
- 核心服务: `src/Client/DistroNexus.Core/Services/`
- 接口定义: `src/Client/DistroNexus.Core/Interfaces/`
- 数据模型: `src/Client/DistroNexus.Core/Models/`
- ViewModels: `src/Client/DistroNexus.Desktop/ViewModels/`

---

**文档维护**: 本文档应随实施进度实时更新，每完成一个todo项目在对应章节标记✅。

**联系方式**: https://github.com/LazyWorkshop-Create/DistroNexus  
**许可证**: MIT License
