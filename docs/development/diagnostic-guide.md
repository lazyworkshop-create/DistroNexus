# PowerShell 模块调用失败诊断指南

## 问题描述
在获取 WSL 实例列表时，PowerShell 模块调用可能会失败，导致无法正常显示已安装的实例。

## 诊断步骤

### 1. 启用详细日志

确保应用程序的日志级别设置为 `Debug`，以查看所有诊断信息。

在 `appsettings.json` 或日志配置文件中：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "DistroNexus": "Debug",
      "DistroNexus.Core.Services": "Debug"
    }
  }
}
```

### 2. 使用内置诊断工具

运行应用程序后，可以通过以下方式调用诊断工具：

#### 方法 A: 通过代码调用
在开发者工具或调试控制台中调用：
```csharp
await mainViewModel.ShowDiagnosticsCommand.ExecuteAsync(null);
```

#### 方法 B: 添加菜单项
在主窗口菜单中添加诊断选项（临时用于调试）。

### 3. 查看诊断输出

诊断工具会显示以下信息：

```
=== PowerShell Service Diagnostics ===

PowerShell Path: C:\Program Files\PowerShell\7\pwsh.exe
PowerShell Exists: True
PowerShell Version: 7.4.0

Module Base Path: D:\wsl\DistroNexus\PowerShell
Module Manifest: D:\wsl\DistroNexus\PowerShell\DistroNexus.psd1
Manifest Exists: True
Manifest Size: 2048 bytes
Manifest Modified: 2024-01-15 10:30:00

Attempting to import module...
Module Import: SUCCESS
Module Info: {"Name":"DistroNexus","Version":"2.0.0","Path":"D:\\wsl\\DistroNexus\\PowerShell\\DistroNexus.psd1","ExportedCommands":"Get-DistroNexusInstance, Install-DistroNexusInstance, ..."}

=== End Diagnostics ===
```

### 4. 常见错误及解决方案

#### 错误 1: Module Base Path: <NULL - Module Not Found>

**原因**: 无法找到 PowerShell 模块。

**解决方案**:
1. 检查诊断输出中的 "Checked paths" 列表
2. 确保模块文件存在于以下任一位置：
   - 开发环境: `{项目根目录}/PowerShell/DistroNexus.psd1`
   - 安装位置: `%ProgramFiles%\DistroNexus\PowerShell\DistroNexus.psd1`
   - 用户模块: `%USERPROFILE%\Documents\PowerShell\Modules\DistroNexus\DistroNexus.psd1`

3. 或设置环境变量:
   ```cmd
   setx DISTRONEXUS_MODULE_PATH "D:\wsl\DistroNexus\PowerShell"
   ```

#### 错误 2: Module Import: FAILED

**原因**: 模块文件存在但无法导入。

**检查项**:
1. 检查 PowerShell 执行策略:
   ```powershell
   Get-ExecutionPolicy
   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
   ```

2. 手动导入模块测试:
   ```powershell
   Import-Module "D:\wsl\DistroNexus\PowerShell" -Verbose
   ```

3. 检查模块文件完整性:
   ```powershell
   Test-ModuleManifest "D:\wsl\DistroNexus\PowerShell\DistroNexus.psd1"
   ```

#### 错误 3: Operation timed out after X seconds

**原因**: PowerShell 操作超时。

**解决方案**:
1. 检查 WSL 是否正常运行:
   ```cmd
   wsl --list --verbose
   ```

2. 检查系统资源占用
3. 增加超时时间（仅用于调试）

### 5. 日志分析

查看应用程序日志，寻找以下关键日志：

#### 成功的模块调用
```
[Debug] ExecuteModuleCmdletAsync called: Cmdlet=Get-DistroNexusInstance, ParameterCount=2, ModulePath=D:\wsl\DistroNexus\PowerShell
[Debug] Executing module cmdlet: Get-DistroNexusInstance from module path: D:\wsl\DistroNexus\PowerShell
[Debug] Cmdlet execution completed: ExitCode=0, OutputLength=1234, ErrorLength=0
[Debug] Parsing JSON output (length=1234)...
[Debug] Parsed JSON array with 3 elements
[Info] Successfully retrieved 3 instances using module
```

#### 失败的模块调用
```
[Debug] ExecuteModuleCmdletAsync called: Cmdlet=Get-DistroNexusInstance, ParameterCount=2, ModulePath=<null>
[Error] DistroNexus module not available, cannot execute cmdlet: Get-DistroNexusInstance
[Error] Module detection failed. Please ensure the PowerShell module is installed or DISTRONEXUS_MODULE_PATH is set.
```

### 6. 手动验证 PowerShell 模块

在 PowerShell 中手动测试：

```powershell
# 1. 导入模块
Import-Module "D:\wsl\DistroNexus\PowerShell" -Force -Verbose

# 2. 验证导入
Get-Module -Name DistroNexus

# 3. 测试 cmdlet
Get-DistroNexusInstance -SkipDiskSize -ForceUpdate | ConvertTo-Json -Depth 10

# 4. 检查输出格式
Get-DistroNexusInstance -SkipDiskSize -ForceUpdate | Get-Member
```

预期输出应包含以下属性：
- Name (string)
- State (string)
- Version (int or string)
- BasePath (string)
- DiskSize (long/int64) - 可能为 0 或空
- InstallTime (datetime)

### 7. 配置文件中的磁盘大小

检查配置文件 `%LOCALAPPDATA%\DistroNexus\settings.json`:

```json
{
  "instances": [
    {
      "name": "Ubuntu-22.04",
      "diskSize": 5368709120,  // 这是缓存的磁盘大小
      "lastSizeUpdate": "2024-01-15T10:30:00Z"
    }
  ]
}
```

**注意**: 当使用 `SkipDiskSize=true` 时，模块应该从配置文件读取缓存的磁盘大小。

### 8. 调试建议

#### 在 PowerShell 模块中添加诊断输出
编辑 `PowerShell/Public/Get-DistroNexusInstance.ps1`，添加详细输出：

```powershell
Write-Verbose "Reading configuration from: $configPath"
Write-Verbose "Found $($instances.Count) instances in registry"
Write-Verbose "SkipDiskSize: $SkipDiskSize"
Write-Verbose "ForceUpdate: $ForceUpdate"

foreach ($instance in $instances) {
    Write-Verbose "Processing instance: $($instance.Name), DiskSize from config: $($instance.DiskSize)"
}
```

然后使用 `-Verbose` 参数运行：

```powershell
Get-DistroNexusInstance -SkipDiskSize -ForceUpdate -Verbose
```

### 9. 常见问题 FAQ

**Q: 为什么配置文件中有磁盘大小，但界面不显示？**

A: 可能的原因：
1. PowerShell 模块未能正确读取配置文件
2. JSON 序列化/反序列化时丢失了 DiskSize 属性
3. C# 解析 JSON 时字段名不匹配（检查大小写）
4. DiskSize 值为 0 或 null

**Q: 如何强制重新计算磁盘大小？**

A: 
1. 确保实例正在运行
2. 调用 `Get-DistroNexusInstance -Name "实例名" -ForceUpdate`（不带 `-SkipDiskSize`）
3. 或使用 `ForceRefreshInstanceAsync` 方法

**Q: 日志级别如何在运行时更改？**

A: 如果使用 Serilog，可以动态调整：
```csharp
Serilog.Log.Logger.MinimumLevel.Debug();
```

## 联系支持

如果问题仍未解决，请提供以下信息：

1. 完整的诊断输出（来自 `ShowDiagnosticsAsync`）
2. 应用程序日志（包含 Debug 级别）
3. PowerShell 版本: `$PSVersionTable`
4. WSL 版本: `wsl --version`
5. 配置文件内容（脱敏后）
6. 手动测试 cmdlet 的输出

## 相关文件

- `DistroNexus.Core/Services/PowerShellService.cs` - PowerShell 执行服务
- `DistroNexus.Core/Services/WslManagerService.cs` - WSL 管理服务
- `PowerShell/Public/Get-DistroNexusInstance.ps1` - PowerShell Cmdlet
- `PowerShell/Private/Config.ps1` - 配置文件读写
