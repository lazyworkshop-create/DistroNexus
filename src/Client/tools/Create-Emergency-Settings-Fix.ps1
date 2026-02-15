# 临时修复 - 禁用配置文件加载
# 这个脚本会创建一个内存中的 SettingsService，完全跳过文件 I/O

# 创建一个临时的备用 SettingsService 实现
$backupImplementation = @'
using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using Microsoft.Extensions.Logging;

namespace DistroNexus.Core.Services;

/// <summary>
/// EMERGENCY FALLBACK: In-memory only settings service (no file I/O).
/// Use this only if file-based settings are completely broken.
/// </summary>
public class SettingsServiceInMemory : ISettingsService
{
    private readonly ILogger<SettingsServiceInMemory> _logger;
    private GlobalSettings _settings;

    public SettingsServiceInMemory(ILogger<SettingsServiceInMemory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = new GlobalSettings();
        
        _logger.LogWarning("=== EMERGENCY MODE: Using in-memory settings (no file I/O) ===");
    }

    public Task<GlobalSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Returning in-memory settings (file I/O disabled)");
        return Task.FromResult(_settings);
    }

    public Task SaveSettingsAsync(GlobalSettings settings, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Settings save requested but file I/O is disabled (in-memory only)");
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        return Task.CompletedTask;
    }

    public Task ResetSettingsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting to default settings (in-memory)");
        _settings = new GlobalSettings();
        return Task.CompletedTask;
    }

    public string GetSettingsPath()
    {
        return "[IN-MEMORY ONLY - NO FILE]";
    }
}
'@

$backupFilePath = "D:\wsl\DistroNexus\src\Core\DistroNexus.Core\Services\SettingsServiceInMemory.cs"

Write-Host "=== DistroNexus 临时修复工具 ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "这个工具会创建一个备用的 SettingsService 实现" -ForegroundColor Cyan
Write-Host "该实现完全跳过文件 I/O，使用内存中的设置" -ForegroundColor Cyan
Write-Host ""
Write-Host "⚠️  警告: 这是临时解决方案！设置不会被保存到磁盘！" -ForegroundColor Red
Write-Host ""

$confirm = Read-Host "是否继续? (y/n)"
if ($confirm -ne 'y') {
    Write-Host "已取消" -ForegroundColor Gray
    exit
}

# 创建备用实现文件
Write-Host ""
Write-Host "创建备用实现..." -ForegroundColor Yellow
$backupImplementation | Set-Content -Path $backupFilePath -Encoding UTF8
Write-Host "✅ 已创建: $backupFilePath" -ForegroundColor Green

# 创建说明文件
$instructions = @"
# 如何使用备用 SettingsService

## 步骤 1: 修改 App.xaml.cs

在 App.xaml.cs 中，找到这行代码：

```csharp
services.AddSingleton<ISettingsService, SettingsService>();
```

替换为：

```csharp
// TEMPORARY: Using in-memory settings to bypass file I/O issues
services.AddSingleton<ISettingsService, SettingsServiceInMemory>();
```

## 步骤 2: 重新编译并运行

```powershell
dotnet build
dotnet run --project src/Client/DistroNexus.Desktop
```

## 步骤 3: 验证

应用应该能正常启动，但设置不会保存到文件。
查看日志应该会看到：

```
=== EMERGENCY MODE: Using in-memory settings (no file I/O) ===
Returning in-memory settings (file I/O disabled)
```

## 恢复正常

找到问题根源后，将 App.xaml.cs 改回：

```csharp
services.AddSingleton<ISettingsService, SettingsService>();
```

然后删除临时文件：

```powershell
Remove-Item src/Core/DistroNexus.Core/Services/SettingsServiceInMemory.cs
```

## 注意事项

- ⚠️  所有设置更改都只在内存中，重启后丢失
- ⚠️  主题和语言设置不会被保存
- ⚠️  仅用于紧急情况或调试
"@

$instructionsPath = "D:\wsl\DistroNexus\EMERGENCY_SETTINGS_FIX.md"
$instructions | Set-Content -Path $instructionsPath -Encoding UTF8

Write-Host ""
Write-Host "✅ 备用实现已创建" -ForegroundColor Green
Write-Host "✅ 使用说明已创建: $instructionsPath" -ForegroundColor Green
Write-Host ""
Write-Host "下一步:" -ForegroundColor Cyan
Write-Host "1. 打开 App.xaml.cs" -ForegroundColor White
Write-Host "2. 将 SettingsService 改为 SettingsServiceInMemory" -ForegroundColor White
Write-Host "3. 重新编译并运行" -ForegroundColor White
Write-Host ""
Write-Host "详细说明请查看: $instructionsPath" -ForegroundColor Gray
Write-Host ""

Read-Host "按 Enter 键打开说明文件"
notepad $instructionsPath
