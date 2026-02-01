# 快速验证 NLog 配置的 PowerShell 脚本
# 运行此脚本来检查日志系统是否正常工作

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host " DistroNexus 日志系统快速验证" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan
Write-Host ""

# 步骤 1: 检查路径
$localAppData = [Environment]::GetFolderPath('LocalApplicationData')
$expectedLogDir = Join-Path $localAppData "DistroNexus\Logs"
$expectedSettingsPath = Join-Path $localAppData "DistroNexus\settings.json"

Write-Host "步骤 1: 验证路径" -ForegroundColor Yellow
Write-Host "  LocalApplicationData: $localAppData" -ForegroundColor Gray
Write-Host "  预期日志目录: $expectedLogDir" -ForegroundColor Gray
Write-Host "  预期设置文件: $expectedSettingsPath" -ForegroundColor Gray
Write-Host ""

# 步骤 2: 创建必要的目录
Write-Host "步骤 2: 创建必要的目录" -ForegroundColor Yellow

if (-not (Test-Path $expectedLogDir)) {
    Write-Host "  创建日志目录..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $expectedLogDir -Force | Out-Null
    Write-Host "  ✓ 日志目录已创建" -ForegroundColor Green
} else {
    Write-Host "  ✓ 日志目录已存在" -ForegroundColor Green
}
Write-Host ""

# 步骤 3: 检查日志文件
Write-Host "步骤 3: 检查现有日志文件" -ForegroundColor Yellow

$logFiles = Get-ChildItem $expectedLogDir -Filter "*.log" -ErrorAction SilentlyContinue

if ($logFiles) {
    Write-Host "  找到 $($logFiles.Count) 个日志文件:" -ForegroundColor Green
    $logFiles | Sort-Object LastWriteTime -Descending | ForEach-Object {
        $sizeKB = [Math]::Round($_.Length / 1KB, 2)
        $age = (Get-Date) - $_.LastWriteTime
        Write-Host "    - $($_.Name) ($sizeKB KB, $([Math]::Round($age.TotalMinutes, 1)) 分钟前)" -ForegroundColor Gray
    }
    
    # 显示最新日志的最后几行
    $latestLog = $logFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    Write-Host ""
    Write-Host "  最新日志文件的最后 10 行:" -ForegroundColor Cyan
    Write-Host "  ----------------------------------------" -ForegroundColor DarkGray
    
    Get-Content $latestLog.FullName -Tail 10 -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $entry = $_ | ConvertFrom-Json
            $color = switch ($entry.level) {
                "ERROR" { "Red" }
                "WARN" { "Yellow" }
                "INFO" { "White" }
                "DEBUG" { "DarkGray" }
                default { "Gray" }
            }
            $time = ([DateTime]$entry.time).ToString("HH:mm:ss")
            Write-Host "  [$time] [$($entry.level)] $($entry.message)" -ForegroundColor $color
            
            if ($entry.exception) {
                Write-Host "    Exception: $($entry.exception.Substring(0, [Math]::Min(100, $entry.exception.Length)))..." -ForegroundColor DarkRed
            }
        } catch {
            Write-Host "  $_" -ForegroundColor DarkGray
        }
    }
    Write-Host "  ----------------------------------------" -ForegroundColor DarkGray
} else {
    Write-Host "  未找到日志文件" -ForegroundColor Red
    Write-Host "  可能原因:" -ForegroundColor Yellow
    Write-Host "    1. 应用程序尚未运行" -ForegroundColor Gray
    Write-Host "    2. NLog 配置有问题" -ForegroundColor Gray
    Write-Host "    3. 日志文件权限问题" -ForegroundColor Gray
}
Write-Host ""

# 步骤 4: 检查设置文件
Write-Host "步骤 4: 检查设置文件" -ForegroundColor Yellow

if (Test-Path $expectedSettingsPath) {
    Write-Host "  ✓ 设置文件存在" -ForegroundColor Green
    
    try {
        $settings = Get-Content $expectedSettingsPath | ConvertFrom-Json
        Write-Host "  关键配置:" -ForegroundColor Cyan
        Write-Host "    LogPath: $($settings.LogPath ?? '<使用默认>')" -ForegroundColor Gray
        Write-Host "    EnableLogging: $($settings.EnableLogging)" -ForegroundColor Gray
        Write-Host "    PowerShellModulePath: $($settings.PowerShellModulePath ?? '<未配置>')" -ForegroundColor Gray
        Write-Host "    DefaultInstallPath: $($settings.DefaultInstallPath)" -ForegroundColor Gray
        
        # 检查 PowerShell 模块路径
        if ($settings.PowerShellModulePath) {
            $modulePath = $settings.PowerShellModulePath
            $manifestPath = Join-Path $modulePath "DistroNexus.psd1"
            
            if (Test-Path $manifestPath) {
                Write-Host "  ✓ PowerShell 模块清单已找到" -ForegroundColor Green
            } else {
                Write-Host "  ✗ PowerShell 模块清单未找到: $manifestPath" -ForegroundColor Red
                Write-Host "    这会导致安装失败！" -ForegroundColor Red
            }
        } else {
            Write-Host "  ⚠ PowerShell 模块路径未配置" -ForegroundColor Yellow
            Write-Host "    需要配置才能安装 WSL 实例" -ForegroundColor Red
        }
    } catch {
        Write-Host "  ✗ 无法解析设置文件: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "  ✗ 设置文件不存在" -ForegroundColor Red
    Write-Host "    首次运行应用程序时会自动创建" -ForegroundColor Yellow
}
Write-Host ""

# 步骤 5: 提供快捷操作
Write-Host "快捷操作:" -ForegroundColor Yellow
Write-Host "  [1] 打开日志目录" -ForegroundColor Cyan
Write-Host "  [2] 打开设置文件" -ForegroundColor Cyan
Write-Host "  [3] 实时监控日志" -ForegroundColor Cyan
Write-Host "  [4] 配置 PowerShell 模块路径" -ForegroundColor Cyan
Write-Host "  [Q] 退出" -ForegroundColor Cyan
Write-Host ""

$choice = Read-Host "请选择 (1-4/Q)"

switch ($choice) {
    "1" {
        if (Test-Path $expectedLogDir) {
            explorer $expectedLogDir
            Write-Host "已打开日志目录" -ForegroundColor Green
        } else {
            Write-Host "日志目录不存在" -ForegroundColor Red
        }
    }
    "2" {
        if (Test-Path $expectedSettingsPath) {
            notepad $expectedSettingsPath
            Write-Host "已打开设置文件" -ForegroundColor Green
        } else {
            Write-Host "设置文件不存在，创建默认设置..." -ForegroundColor Yellow
            $defaultSettings = @{
                DefaultInstallPath = "C:\WSL"
                PackageCachePath = ""
                TerminalStartPath = "~"
                DefaultWslVersion = 2
                DefaultUsername = "root"
                DefaultDistributionId = ""
                EnableLogging = $true
                LogPath = ""
                CheckUpdatesOnStartup = $true
                Theme = "Auto"
                Language = "en-US"
                ShowConfirmationDialogs = $true
                MaxConcurrentDownloads = 3
                AutoRetryDownloads = $true
                MaxRetryAttempts = 3
                AutoSaveEnabled = $true
                AutoSaveInterval = 30
                PowerShellModulePath = ""
            } | ConvertTo-Json
            
            $settingsDir = Split-Path $expectedSettingsPath
            if (-not (Test-Path $settingsDir)) {
                New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
            }
            
            $defaultSettings | Set-Content $expectedSettingsPath
            notepad $expectedSettingsPath
            Write-Host "✓ 默认设置已创建并打开" -ForegroundColor Green
        }
    }
    "3" {
        $todayLog = Join-Path $expectedLogDir "DistroNexus_$(Get-Date -Format 'yyyy-MM-dd').log"
        if (Test-Path $todayLog) {
            Write-Host "实时监控日志文件: $todayLog" -ForegroundColor Green
            Write-Host "按 Ctrl+C 停止" -ForegroundColor Yellow
            Write-Host ""
            
            Get-Content $todayLog -Wait -Tail 10 | ForEach-Object {
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
        } else {
            Write-Host "今天的日志文件不存在: $todayLog" -ForegroundColor Red
        }
    }
    "4" {
        Write-Host ""
        Write-Host "配置 PowerShell 模块路径" -ForegroundColor Yellow
        Write-Host "当前工作目录: $PSScriptRoot" -ForegroundColor Gray
        Write-Host ""
        
        $defaultModulePath = "D:\wsl\DistroNexus\src\PowerShell"
        $modulePath = Read-Host "请输入 PowerShell 模块路径 (默认: $defaultModulePath)"
        
        if ([string]::IsNullOrWhiteSpace($modulePath)) {
            $modulePath = $defaultModulePath
        }
        
        # 验证路径
        $manifestPath = Join-Path $modulePath "DistroNexus.psd1"
        if (-not (Test-Path $manifestPath)) {
            Write-Host "⚠ 警告: 在指定路径未找到模块清单: $manifestPath" -ForegroundColor Red
            $continue = Read-Host "是否仍然保存此路径? (Y/N)"
            if ($continue -ne "Y" -and $continue -ne "y") {
                Write-Host "已取消" -ForegroundColor Yellow
                return
            }
        } else {
            Write-Host "✓ 模块清单已找到: $manifestPath" -ForegroundColor Green
        }
        
        # 更新设置
        if (Test-Path $expectedSettingsPath) {
            $settings = Get-Content $expectedSettingsPath | ConvertFrom-Json
        } else {
            $settings = [PSCustomObject]@{}
        }
        
        $settings | Add-Member -NotePropertyName "PowerShellModulePath" -NotePropertyValue $modulePath -Force
        
        # 保存
        $settingsDir = Split-Path $expectedSettingsPath
        if (-not (Test-Path $settingsDir)) {
            New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
        }
        
        $settings | ConvertTo-Json | Set-Content $expectedSettingsPath
        
        Write-Host "✓ PowerShell 模块路径已配置: $modulePath" -ForegroundColor Green
        Write-Host "  设置文件: $expectedSettingsPath" -ForegroundColor Gray
    }
    default {
        Write-Host "退出" -ForegroundColor Gray
    }
}
