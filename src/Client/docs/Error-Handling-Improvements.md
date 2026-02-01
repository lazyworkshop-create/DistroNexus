# PowerShell 模块错误处理改进

## 概述

改进了 PowerShell 模块调用时的错误处理，使错误信息更加用户友好，同时保留详细的技术日志用于调试。

## 主要改进

### 1. 友好错误消息提取

新增 `ExtractFriendlyErrorMessage()` 方法，自动识别和转换常见错误：

#### 错误类型识别

| 原始错误类型 | 友好提示 |
|-------------|---------|
| **模块导入失败** | "Failed to load the PowerShell module. Please verify that the module files are not corrupted and try restarting the application." |
| **权限不足** | "Access denied. Please ensure you have administrator privileges or the required permissions to perform this operation." |
| **文件未找到** | "A required file or directory was not found. Please verify the installation path and try again." |
| **网络/下载错误** | "Failed to download the required files. Please check your internet connection and try again." |
| **磁盘空间不足** | "Insufficient disk space. Please free up some space and try again." |
| **WSL 未安装** | "WSL is not installed or not properly configured. Please install WSL2 from Windows Features." |
| **实例名称冲突** | "A WSL distribution with this name already exists. Please choose a different name." |
| **参数验证失败** | "Invalid input provided. Please check your settings and try again." |
| **超时** | "The operation timed out after X seconds. This may be due to a slow network connection or a large file download. Please try again." |

### 2. 技术细节过滤

自动移除以下技术信息（用户不需要看到）：

- ✅ CLIXML 标签和 XML 噪音
- ✅ ANSI 颜色代码和转义序列
- ✅ PowerShell 堆栈跟踪 (`At line:`, `+ CategoryInfo`, etc.)
- ✅ 内部错误 ID 和分类信息

### 3. 双层日志记录

```csharp
// 原始错误 - 记录到日志文件（用于调试）
_logger.LogError("Raw PowerShell error: {RawError}", result.Error);

// 友好错误 - 显示给用户
var friendlyError = ExtractFriendlyErrorMessage(result.Error, cmdletName);
result.Error = friendlyError;

_logger.LogError("User-friendly error: {FriendlyError}", friendlyError);
```

## 使用示例

### Before（改进前）

**用户看到的错误**:
```
#< CLIXML
<Objs Version="1.1.0.1" xmlns="http://schemas.microsoft.com/powershell/2004/04">
  <Obj S="progress" RefId="0">
    <TN RefId="0">
      <T>System.Management.Automation.PSCustomObject</T>
      <T>System.Object</T>
    </TN>
    <MS>
      <I64 N="SourceId">1</I64>
      <PR N="Record">
        <AV>Preparing modules for first use.</AV>
        <AI>0</AI>
        <Nil />
        <PI>-1</PI>
        <PC>-1</PC>
        <T>Completed</T>
        <SR>-1</SR>
        <SD> </SD>
      </PR>
    </MS>
  </Obj>
  <S S="Error">Get-DistroNexusInstance : Access to the path 'D:\wsl\Ubuntu' is denied._x000D__x000A_</S>
  <S S="Error">At line:1 char:1_x000D__x000A_</S>
  <S S="Error">+ Get-DistroNexusInstance_x000D__x000A_</S>
  <S S="Error">+ ~~~~~~~~~~~~~~~~~~~~~~~_x000D__x000A_</S>
  <S S="Error">    + CategoryInfo          : NotSpecified: (:) [Get-DistroNexusInstance], UnauthorizedAccessException_x000D__x000A_</S>
  <S S="Error">    + FullyQualifiedErrorId : System.UnauthorizedAccessException,Get-DistroNexusInstance_x000D__x000A_</S>
</Objs>
```

### After（改进后）

**用户看到的错误**:
```
Access denied. Please ensure you have administrator privileges or the required permissions to perform this operation.
```

**日志文件中的记录**:
```json
{
  "time": "2026-02-01 22:30:15",
  "level": "ERROR",
  "logger": "DistroNexus.Core.Services.PowerShellService",
  "message": "Raw PowerShell error: #< CLIXML<Objs>...Access to the path 'D:\\wsl\\Ubuntu' is denied..."
}
{
  "time": "2026-02-01 22:30:15",
  "level": "ERROR",
  "logger": "DistroNexus.Core.Services.PowerShellService",
  "message": "User-friendly error: Access denied. Please ensure you have administrator privileges or the required permissions to perform this operation."
}
```

## 错误处理流程

```
PowerShell 错误发生
    ↓
记录原始错误到日志（完整技术细节）
    ↓
ExtractFriendlyErrorMessage()
    ├─ 移除 CLIXML 和 XML 标签
    ├─ 移除颜色代码和转义序列
    ├─ 移除堆栈跟踪信息
    ├─ 识别常见错误模式
    └─ 返回友好消息
    ↓
显示友好错误给用户
    ↓
记录友好错误到日志（用户看到的内容）
```

## 特殊场景处理

### 1. 模块未配置
```csharp
// 特殊处理：提供配置指导
return new PowerShellScriptResult
{
    ExitCode = 1,
    Error = $"PowerShell module path not configured. Please set PowerShellModulePath in settings: {settingsPath}",
    UsedModule = false
};
```

### 2. 模块文件丢失
```csharp
// 友好提示：模块安装问题
var friendlyError = $"PowerShell module files are missing. Please ensure the module is installed at: {_moduleBasePath}";
```

### 3. 超时
```csharp
// 提供上下文和建议
var friendlyError = $"The operation timed out after {options.TimeoutSeconds} seconds. This may be due to a slow network connection or a large file download. Please try again.";
```

## 调试建议

当用户报告错误时：

1. **用户截图** - 显示友好的错误消息
2. **日志文件** - 包含完整的技术细节
   - 位置: `%APPDATA%\DistroNexus\Logs`
   - 查找 "Raw PowerShell error" 获取完整错误

## 测试建议

### 测试场景

| 场景 | 预期友好消息 |
|------|-------------|
| 无管理员权限安装 | "Access denied. Please ensure you have administrator privileges..." |
| 网络断开时下载 | "Failed to download the required files. Please check your internet connection..." |
| 磁盘空间不足 | "Insufficient disk space. Please free up some space..." |
| WSL 未安装 | "WSL is not installed or not properly configured..." |
| 重复实例名 | "A WSL distribution with this name already exists..." |

### 测试方法

```powershell
# 1. 测试权限错误
New-Item -Path "C:\test" -ItemType Directory
icacls "C:\test" /deny Everyone:F

# 2. 测试网络错误
# 断开网络连接后尝试安装

# 3. 测试超时
# 设置短超时值并下载大文件

# 4. 检查日志
Get-Content "$env:APPDATA\DistroNexus\Logs\DistroNexus_$(Get-Date -Format 'yyyy-MM-dd').log" -Tail 50 | 
    ConvertFrom-Json | 
    Where-Object { $_.level -eq "ERROR" }
```

## 未来改进

1. **多语言支持** - 本地化友好错误消息
2. **错误分类** - 添加错误类别标签用于统计分析
3. **自动修复建议** - 提供"一键修复"按钮（如配置问题）
4. **错误报告** - 允许用户一键提交错误报告

## 相关文件

- `DistroNexus.Core\Services\PowerShellService.cs` - 错误处理逻辑
- `DistroNexus.Core\Services\WslManagerService.cs` - 调用 PowerShell 服务
- `docs\NLog-Configuration.md` - 日志配置说明
- `docs\Log-Diagnostics.md` - 日志诊断工具
