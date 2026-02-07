# NLog 日志配置说明

## 📝 概述

DistroNexus 使用 NLog 作为日志框架，提供结构化日志记录功能。

## 📂 日志位置

### 默认位置
当未在设置中指定日志路径时，日志将保存到：
```
%LOCALAPPDATA%\DistroNexus\Logs\
```

例如：
```
C:\Users\YourUsername\AppData\Local\DistroNexus\Logs\
```

### 自定义位置
用户可以在应用程序设置中指定自定义日志路径。

## 📋 日志文件命名

- **日志文件**: `DistroNexus_yyyy-MM-dd.log`
  - 例如: `DistroNexus_2025-01-26.log`
  
- **归档文件**: `archives\DistroNexus_{#}.log`
  - 自动按天归档
  - 保留最近 7 天的日志

## 🎯 日志级别

从低到高：
1. **Trace** - 最详细的调试信息
2. **Debug** - 调试信息
3. **Info** - 一般信息
4. **Warn** - 警告信息
5. **Error** - 错误信息
6. **Fatal** - 严重错误

### 默认配置
- **文件日志**: Info 及以上级别
- **控制台日志**: Debug 及以上级别
- **调试窗口**: Trace 及以上级别

## 📊 日志格式

日志以 JSON 格式保存，便于分析和查询：

```json
{
  "time": "2025-01-26 14:30:15.1234",
  "level": "INFO",
  "logger": "DistroNexus.Core.Services.WslManagerService",
  "message": "Installing WSL instance 'dev_c' to 'D:\\wsl'",
  "properties": {
    "InstanceName": "dev_c",
    "InstallPath": "D:\\wsl"
  }
}
```

### 错误日志示例
```json
{
  "time": "2025-01-26 14:35:42.5678",
  "level": "ERROR",
  "logger": "DistroNexus.Core.Services.PowerShellService",
  "message": "PowerShell script failed with exit code 1",
  "exception": "System.InvalidOperationException: PowerShell script failed: ...\n   at ..."
}
```

## 🛠️ 配置文件

配置文件位于: `nlog.config`

### 主要配置项

#### 1. 日志目标 (Targets)

**文件目标** - 写入日志文件
```xml
<target xsi:type="File" 
        name="fileTarget"
        fileName="${logDirectory}\${appName}_${shortdate}.log"
        archiveEvery="Day"
        maxArchiveFiles="7">
```

**控制台目标** - 开发时调试
```xml
<target xsi:type="Console" name="consoleTarget" />
```

**调试目标** - Visual Studio 输出窗口
```xml
<target xsi:type="Debugger" name="debugTarget" />
```

#### 2. 日志规则 (Rules)

```xml
<rules>
  <!-- 所有 Info 及以上级别写入文件 -->
  <logger name="*" minlevel="Info" writeTo="fileTarget" />
  
  <!-- 所有 Debug 及以上级别输出到控制台 -->
  <logger name="*" minlevel="Debug" writeTo="consoleTarget" />
  
  <!-- 过滤 Microsoft 框架日志 -->
  <logger name="Microsoft.*" maxlevel="Info" final="true" />
</rules>
```

## 🔍 如何查看日志

### 方法 1: 通过应用程序
1. 安装失败时，点击 "Open log folder for details" 按钮
2. 将自动打开日志文件夹

### 方法 2: 手动打开
1. 按 `Win + R` 打开运行对话框
2. 输入: `%LOCALAPPDATA%\DistroNexus\Logs`
3. 按回车

### 方法 3: 文件浏览器
1. 打开文件资源管理器
2. 在地址栏输入: `%LOCALAPPDATA%\DistroNexus\Logs`

## 📖 代码中使用日志

### 注入 Logger
```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;
    
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }
}
```

### 记录不同级别的日志

```csharp
// 信息
_logger.LogInformation("Installing instance {InstanceName}", instanceName);

// 警告
_logger.LogWarning("Instance {InstanceName} not found", instanceName);

// 错误
_logger.LogError(ex, "Failed to install instance {InstanceName}", instanceName);

// 带结构化数据
_logger.LogInformation("Operation completed: Duration={Duration}ms, Result={Result}", 
    duration, success);
```

### 条件日志（避免性能开销）

```csharp
if (_logger.IsEnabled(LogLevel.Debug))
{
    var expensiveData = ComputeExpensiveDebugInfo();
    _logger.LogDebug("Debug info: {Data}", expensiveData);
}
```

## ⚙️ 自定义配置

### 修改日志级别
编辑 `nlog.config`:
```xml
<!-- 改为 Debug 级别 -->
<logger name="*" minlevel="Debug" writeTo="fileTarget" />
```

### 修改保留天数
```xml
<!-- 保留 30 天 -->
<target ... maxArchiveFiles="30" />
```

### 添加特定类的详细日志
```xml
<!-- DistroNexus.Core 命名空间的所有日志都记录 Trace 级别 -->
<logger name="DistroNexus.Core.*" minlevel="Trace" writeTo="fileTarget" />
```

## 🚨 故障排除

### 日志未生成
1. 检查日志目录是否有写入权限
2. 检查 `nlog.config` 是否正确复制到输出目录
3. 查看调试输出窗口是否有 NLog 内部错误

### 日志文件过大
1. 减少 `maxArchiveFiles` 值
2. 提高日志级别（如从 Info 改为 Warn）
3. 添加更多过滤规则

### 性能问题
```xml
<!-- 启用异步日志 -->
<targets async="true">
  <target xsi:type="File" ... />
</targets>
```

## 📚 参考资源

- [NLog 官方文档](https://nlog-project.org/)
- [NLog GitHub](https://github.com/NLog/NLog)
- [NLog 配置示例](https://github.com/NLog/NLog/wiki/Configuration-file)
