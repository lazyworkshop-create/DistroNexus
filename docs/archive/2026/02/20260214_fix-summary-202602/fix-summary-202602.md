# 修复摘要：PowerShell 模块路径检测问题

## 问题描述

PowerShell 模块导入失败，错误信息：
```
Import-Module: The specified module 'D:\wsl\DistroNexus\src\PowerShell' was not loaded 
because no valid module file was found in any module directory.
```

## 根本原因

PowerShellService 在开发环境中搜索模块时，没有包含 `src\PowerShell` 这个目录结构。代码只检查了以下路径：
- `..\..\..\..\..\PowerShell\DistroNexus.psd1`
- `..\..\..\..\PowerShell\DistroNexus.psd1`

但实际项目结构是：
```
DistroNexus/
├── src/
│   ├── PowerShell/          ← 实际位置
│   │   └── DistroNexus.psd1
│   ├── DistroNexus.Core/
│   └── DistroNexus.Desktop/
```

当应用从 `DistroNexus.Desktop\bin\Debug\net10.0\` 运行时，向上导航到项目根目录需要经过 `src` 文件夹。

## 修复方案

在 `PowerShellService.cs` 的 `FindDistroNexusModulePathWithDebug()` 方法中，添加了对 `src\PowerShell` 路径的支持：

```csharp
// 2. Development paths (relative to bin directory)
var devPaths = new[]
{
    // Pattern: bin/Debug/net10.0 -> src/PowerShell
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\src\PowerShell\DistroNexus.psd1"),
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\src\PowerShell\DistroNexus.psd1"),
    // Pattern: bin/Debug/net10.0 -> PowerShell (legacy)
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\PowerShell\DistroNexus.psd1"),
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\PowerShell\DistroNexus.psd1"),
};
```

## 修复后的搜索顺序

现在 PowerShellService 按以下顺序搜索模块：

1. **环境变量**: `DISTRONEXUS_MODULE_PATH`
2. **开发路径** (新增 src 支持):
   - `{BaseDirectory}\..\..\..\..\..\src\PowerShell\DistroNexus.psd1`
   - `{BaseDirectory}\..\..\..\..\src\PowerShell\DistroNexus.psd1`
   - `{BaseDirectory}\..\..\..\..\..\PowerShell\DistroNexus.psd1` (向后兼容)
   - `{BaseDirectory}\..\..\..\..\PowerShell\DistroNexus.psd1` (向后兼容)
3. **安装路径**:
   - `C:\Program Files\DistroNexus\PowerShell\DistroNexus.psd1`
   - `%LOCALAPPDATA%\DistroNexus\PowerShell\DistroNexus.psd1`
   - `%USERPROFILE%\Documents\PowerShell\Modules\DistroNexus\DistroNexus.psd1`
4. **PowerShell Gallery 路径**:
   - `%USERPROFILE%\Documents\PowerShell\Modules\DistroNexus\`

## 验证修复

### 方法 1: 运行诊断工具
在应用中调用：
```csharp
await mainViewModel.ShowDiagnosticsCommand.ExecuteAsync(null);
```

应该看到：
```
Module Base Path: D:\wsl\DistroNexus\src\PowerShell
Module Manifest: D:\wsl\DistroNexus\src\PowerShell\DistroNexus.psd1
Manifest Exists: True
Module Import: SUCCESS
```

### 方法 2: 检查日志
启动应用时，应该看到类似日志：
```
[Info] PowerShell service initialized using: C:\Program Files\PowerShell\7\pwsh.exe
[Info] DistroNexus module detected at: D:\wsl\DistroNexus\src\PowerShell
```

如果仍然失败，会看到：
```
[Warning] DistroNexus PowerShell module not found after checking X locations
[Debug] Checked module paths: ...
```

### 方法 3: 手动测试 PowerShell 模块
在 PowerShell 中：
```powershell
# 导入模块
Import-Module "D:\wsl\DistroNexus\src\PowerShell" -Force -Verbose

# 验证导入
Get-Module -Name DistroNexus

# 测试 cmdlet
Get-DistroNexusInstance -SkipDiskSize -ForceUpdate
```

## 相关改进

在此修复中，还添加了以下增强功能：

1. **详细诊断日志** - 在模块调用的每个关键点添加了 Debug 级别日志
2. **诊断工具** - 新增 `GetDiagnosticInfoAsync()` 方法和 `ShowDiagnosticsCommand`
3. **错误处理改进** - 更详细的错误消息，包含异常类型和上下文
4. **移除降级策略** - `GetInstancesAsync` 现在总是使用 PowerShell 模块，失败时抛出异常而不是返回空列表

## 后续建议

### 1. 设置环境变量（推荐用于开发）
```cmd
setx DISTRONEXUS_MODULE_PATH "D:\wsl\DistroNexus\src\PowerShell"
```

这样可以避免依赖相对路径解析。

### 2. 使用 launchSettings.json
在 `DistroNexus.Desktop\Properties\launchSettings.json` 中：
```json
{
  "profiles": {
    "DistroNexus.Desktop": {
      "commandName": "Project",
      "environmentVariables": {
        "DISTRONEXUS_MODULE_PATH": "D:\\wsl\\DistroNexus\\src\\PowerShell"
      }
    }
  }
}
```

### 3. 添加单元测试
为模块路径检测逻辑添加单元测试：
```csharp
[Fact]
public void FindModulePath_ShouldDetectSrcPowerShellPath()
{
    // Test that src/PowerShell path is correctly detected
}
```

## 测试场景

修复后应验证以下场景：

- [ ] 从 Visual Studio 调试运行
- [ ] 从 bin 目录直接运行可执行文件
- [ ] 从发布的独立部署运行
- [ ] 从不同磁盘驱动器运行
- [ ] 在设置了 DISTRONEXUS_MODULE_PATH 环境变量的情况下运行

## 修改的文件

- `DistroNexus.Core/Services/PowerShellService.cs` - 添加 `src\PowerShell` 路径支持
- `DistroNexus.Core/Services/WslManagerService.cs` - 添加诊断日志
- `DistroNexus.Core/Interfaces/IPowerShellService.cs` - 添加 `GetDiagnosticInfoAsync` 接口
- `DistroNexus.Desktop/ViewModels/MainViewModel.cs` - 添加 `ShowDiagnosticsCommand`

## 相关文档

- [DIAGNOSTIC_GUIDE.md](./DIAGNOSTIC_GUIDE.md) - 完整的诊断指南
- [PowerShell 模块结构](./src/PowerShell/README.md) - 模块文档
