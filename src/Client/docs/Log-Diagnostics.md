# DistroNexus 日志诊断工具

## 快速检查

### 1. 检查日志文件是否存在
打开 PowerShell 并运行：
```powershell
$logPath = "$env:LOCALAPPDATA\DistroNexus\Logs"
Write-Host "Log directory: $logPath"
Write-Host "Directory exists: $(Test-Path $logPath)"

if (Test-Path $logPath) {
    Write-Host "`nLog files:"
    Get-ChildItem $logPath -Filter "*.log" | Format-Table Name, Length, LastWriteTime -AutoSize
}
```

### 2. 查看最新日志
```powershell
$logPath = "$env:LOCALAPPDATA\DistroNexus\Logs"
$latestLog = Get-ChildItem $logPath -Filter "DistroNexus_*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($latestLog) {
    Write-Host "Latest log file: $($latestLog.FullName)"
    Write-Host "Size: $($latestLog.Length) bytes"
    Write-Host "Last modified: $($latestLog.LastWriteTime)"
    Write-Host "`nLast 20 lines:"
    Get-Content $latestLog.FullName -Tail 20
} else {
    Write-Host "No log files found!"
}
```

### 3. 查看特定错误
```powershell
$logPath = "$env:LOCALAPPDATA\DistroNexus\Logs"
$latestLog = Get-ChildItem $logPath -Filter "DistroNexus_*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($latestLog) {
    Write-Host "Searching for ERROR entries..."
    Get-Content $latestLog.FullName | ConvertFrom-Json | Where-Object { $_.level -eq "ERROR" } | ForEach-Object {
        Write-Host "[$($_.time)] $($_.logger)"
        Write-Host "  Message: $($_.message)"
        if ($_.exception) {
            Write-Host "  Exception: $($_.exception.Substring(0, [Math]::Min(200, $_.exception.Length)))..."
        }
        Write-Host ""
    }
}
```

### 4. 实时监控日志 (Tail)
```powershell
$logPath = "$env:LOCALAPPDATA\DistroNexus\Logs"
$latestLog = Get-ChildItem $logPath -Filter "DistroNexus_*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($latestLog) {
    Write-Host "Monitoring log file: $($latestLog.FullName)"
    Write-Host "Press Ctrl+C to stop...`n"
    
    Get-Content $latestLog.FullName -Wait | ForEach-Object {
        try {
            $entry = $_ | ConvertFrom-Json
            $color = switch ($entry.level) {
                "ERROR" { "Red" }
                "WARN" { "Yellow" }
                "INFO" { "Green" }
                default { "White" }
            }
            Write-Host "[$($entry.time)] [$($entry.level)] $($entry.message)" -ForegroundColor $color
        } catch {
            Write-Host $_
        }
    }
}
```

## 常见问题排查

### 问题 1: 没有日志文件生成

**检查项**:
1. 确认目录是否存在：
   ```powershell
   Test-Path "$env:LOCALAPPDATA\DistroNexus\Logs"
   ```

2. 检查目录权限：
   ```powershell
   Get-Acl "$env:LOCALAPPDATA\DistroNexus\Logs" | Format-List
   ```

3. 手动创建目录：
   ```powershell
   New-Item -ItemType Directory -Path "$env:LOCALAPPDATA\DistroNexus\Logs" -Force
   ```

### 问题 2: 日志文件为空

**可能原因**:
- 应用程序可能没有启用 NLog
- 日志级别设置过高，过滤掉了所有日志

**解决方法**:
1. 检查 `nlog.config` 文件是否存在于应用程序目录
2. 检查 `nlog.config` 中的日志级别设置

### 问题 3: PowerShell 模块错误不被记录

**确认模块路径配置**:
```powershell
$settingsPath = "$env:LOCALAPPDATA\DistroNexus\settings.json"
if (Test-Path $settingsPath) {
    $settings = Get-Content $settingsPath | ConvertFrom-Json
    Write-Host "PowerShell Module Path: $($settings.PowerShellModulePath)"
} else {
    Write-Host "Settings file not found at: $settingsPath"
}
```

**验证模块是否存在**:
```powershell
$settings = Get-Content "$env:LOCALAPPDATA\DistroNexus\settings.json" | ConvertFrom-Json
$modulePath = $settings.PowerShellModulePath

if ($modulePath) {
    $manifestPath = Join-Path $modulePath "DistroNexus.psd1"
    Write-Host "Module manifest path: $manifestPath"
    Write-Host "Manifest exists: $(Test-Path $manifestPath)"
    
    if (Test-Path $manifestPath) {
        Import-Module $manifestPath -Force
        Get-Module -Name DistroNexus | Format-List
    }
}
```

## 安装失败日志分析

### 查找安装失败的详细信息
```powershell
$logPath = "$env:LOCALAPPDATA\DistroNexus\Logs"
$latestLog = Get-ChildItem $logPath -Filter "DistroNexus_*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($latestLog) {
    Write-Host "Searching for installation failures...`n"
    
    Get-Content $latestLog.FullName | ConvertFrom-Json | Where-Object { 
        $_.message -like "*INSTALLATION*" -or 
        $_.message -like "*Install*" -or
        $_.message -like "*failed*"
    } | ForEach-Object {
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "Time: $($_.time)" -ForegroundColor Yellow
        Write-Host "Level: $($_.level)" -ForegroundColor $(if ($_.level -eq "ERROR") { "Red" } else { "White" })
        Write-Host "Logger: $($_.logger)" -ForegroundColor Gray
        Write-Host "Message: $($_.message)" -ForegroundColor White
        
        if ($_.exception) {
            Write-Host "`nException:" -ForegroundColor Red
            Write-Host $_.exception -ForegroundColor Red
        }
        
        if ($_.properties) {
            Write-Host "`nProperties:" -ForegroundColor Gray
            $_.properties | Format-List
        }
        
        Write-Host ""
    }
}
```

### 导出完整日志用于分析
```powershell
$logPath = "$env:LOCALAPPDATA\DistroNexus\Logs"
$latestLog = Get-ChildItem $logPath -Filter "DistroNexus_*.log" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if ($latestLog) {
    $outputPath = "$env:USERPROFILE\Desktop\DistroNexus_Debug_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"
    
    Write-Host "Exporting log to: $outputPath"
    
    @"
DistroNexus Debug Log Export
Generated: $(Get-Date)
Source Log: $($latestLog.FullName)
===============================================

"@ | Out-File $outputPath
    
    Get-Content $latestLog.FullName | ConvertFrom-Json | ForEach-Object {
        @"
[$($_.time)] [$($_.level)] $($_.logger)
Message: $($_.message)
$(if ($_.exception) { "Exception: $($_.exception)" })
---
"@ | Out-File $outputPath -Append
    }
    
    Write-Host "Log exported successfully!"
    Write-Host "Opening file..."
    notepad $outputPath
}
```

## PowerShell 模块配置指南

### 设置模块路径（如果您从源代码运行）

1. 找到您的 PowerShell 模块路径，例如：
   - 源代码: `D:\wsl\DistroNexus\src\PowerShell`
   - 编译后: `D:\wsl\DistroNexus\src\DistroNexus.Desktop\bin\Debug\net10.0-windows\PowerShell`

2. 更新设置文件：
```powershell
$settingsPath = "$env:LOCALAPPDATA\DistroNexus\settings.json"

# 读取现有设置
$settings = if (Test-Path $settingsPath) {
    Get-Content $settingsPath | ConvertFrom-Json
} else {
    @{}
}

# 设置模块路径（根据实际情况修改）
$settings | Add-Member -NotePropertyName "PowerShellModulePath" -NotePropertyValue "D:\wsl\DistroNexus\src\PowerShell" -Force

# 保存设置
$settings | ConvertTo-Json | Set-Content $settingsPath

Write-Host "PowerShell module path configured successfully!"
Write-Host "Path: $($settings.PowerShellModulePath)"
```

3. 验证设置：
```powershell
Get-Content "$env:LOCALAPPDATA\DistroNexus\settings.json" | ConvertFrom-Json | Select-Object PowerShellModulePath
```
