# DistroNexus NLog 诊断脚本
# 用于验证日志配置和排查日志问题

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "  DistroNexus NLog 诊断工具" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 1. 检查预期的日志目录
$expectedLogDir = "$env:LOCALAPPDATA\DistroNexus\Logs"
Write-Host "1. 预期日志目录:" -ForegroundColor Yellow
Write-Host "   $expectedLogDir"
Write-Host "   存在: $(Test-Path $expectedLogDir)" -ForegroundColor $(if (Test-Path $expectedLogDir) { "Green" } else { "Red" })
Write-Host ""

# 2. 检查设置文件
$settingsPath = "$env:LOCALAPPDATA\DistroNexus\settings.json"
Write-Host "2. 设置文件:" -ForegroundColor Yellow
Write-Host "   路径: $settingsPath"
Write-Host "   存在: $(Test-Path $settingsPath)" -ForegroundColor $(if (Test-Path $settingsPath) { "Green" } else { "Red" })

if (Test-Path $settingsPath) {
    try {
        $settings = Get-Content $settingsPath | ConvertFrom-Json
        Write-Host "   LogPath 配置: $($settings.LogPath)" -ForegroundColor Gray
        Write-Host "   PowerShellModulePath: $($settings.PowerShellModulePath)" -ForegroundColor Gray
    } catch {
        Write-Host "   错误: 无法解析设置文件" -ForegroundColor Red
    }
}
Write-Host ""

# 3. 检查应用程序目录
$appDir = Split-Path -Parent $PSScriptRoot
Write-Host "3. 应用程序目录:" -ForegroundColor Yellow
Write-Host "   $appDir"

$nlogConfigPath = Join-Path $appDir "DistroNexus.Desktop\nlog.config"
if (-not (Test-Path $nlogConfigPath)) {
    $nlogConfigPath = Join-Path $appDir "nlog.config"
}
if (-not (Test-Path $nlogConfigPath)) {
    # Check bin directory
    $binDirs = Get-ChildItem -Path $appDir -Recurse -Filter "nlog.config" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($binDirs) {
        $nlogConfigPath = $binDirs.FullName
    }
}

Write-Host "   nlog.config: $nlogConfigPath"
Write-Host "   存在: $(Test-Path $nlogConfigPath)" -ForegroundColor $(if (Test-Path $nlogConfigPath) { "Green" } else { "Red" })
Write-Host ""

# 4. 列出所有日志文件
Write-Host "4. 现有日志文件:" -ForegroundColor Yellow

$allLogLocations = @(
    "$env:LOCALAPPDATA\DistroNexus\Logs",
    "$env:APPDATA\DistroNexus\Logs",
    "$env:TEMP\DistroNexus\Logs"
)

$foundLogs = $false
foreach ($location in $allLogLocations) {
    if (Test-Path $location) {
        $logFiles = Get-ChildItem $location -Filter "*.log" -ErrorAction SilentlyContinue
        if ($logFiles) {
            Write-Host "   在 $location 找到:" -ForegroundColor Green
            $logFiles | ForEach-Object {
                Write-Host "     - $($_.Name) ($([Math]::Round($_.Length/1KB, 2)) KB, 最后修改: $($_.LastWriteTime))" -ForegroundColor Gray
            }
            $foundLogs = $true
        }
    }
}

if (-not $foundLogs) {
    Write-Host "   未找到任何日志文件" -ForegroundColor Red
}
Write-Host ""

# 5. 检查最新日志内容
Write-Host "5. 最新日志内容 (最后 20 行):" -ForegroundColor Yellow

$latestLog = Get-ChildItem "$env:LOCALAPPDATA\DistroNexus\Logs" -Filter "*.log" -ErrorAction SilentlyContinue | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1

if ($latestLog) {
    Write-Host "   文件: $($latestLog.FullName)" -ForegroundColor Green
    Write-Host "   大小: $([Math]::Round($latestLog.Length/1KB, 2)) KB" -ForegroundColor Gray
    Write-Host "   最后修改: $($latestLog.LastWriteTime)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   内容:" -ForegroundColor Cyan
    
    try {
        Get-Content $latestLog.FullName -Tail 20 | ForEach-Object {
            try {
                $entry = $_ | ConvertFrom-Json
                $color = switch ($entry.level) {
                    "ERROR" { "Red" }
                    "WARN" { "Yellow" }
                    "INFO" { "Green" }
                    default { "White" }
                }
                Write-Host "   [$($entry.time)] [$($entry.level)] $($entry.message)" -ForegroundColor $color
            } catch {
                Write-Host "   $_" -ForegroundColor Gray
            }
        }
    } catch {
        Write-Host "   错误: 无法读取日志文件" -ForegroundColor Red
    }
} else {
    Write-Host "   未找到日志文件" -ForegroundColor Red
    Write-Host "   请运行应用程序以生成日志" -ForegroundColor Yellow
}
Write-Host ""

# 6. PowerShell 模块检查
Write-Host "6. PowerShell 模块状态:" -ForegroundColor Yellow

if (Test-Path $settingsPath) {
    try {
        $settings = Get-Content $settingsPath | ConvertFrom-Json
        if ($settings.PowerShellModulePath) {
            Write-Host "   模块路径: $($settings.PowerShellModulePath)"
            
            $manifestPath = Join-Path $settings.PowerShellModulePath "DistroNexus.psd1"
            Write-Host "   清单文件: $manifestPath"
            Write-Host "   存在: $(Test-Path $manifestPath)" -ForegroundColor $(if (Test-Path $manifestPath) { "Green" } else { "Red" })
            
            if (Test-Path $manifestPath) {
                try {
                    Import-Module $manifestPath -Force -ErrorAction Stop
                    $module = Get-Module -Name DistroNexus
                    if ($module) {
                        Write-Host "   ✓ 模块已成功加载" -ForegroundColor Green
                        Write-Host "   版本: $($module.Version)" -ForegroundColor Gray
                        Write-Host "   导出的命令数量: $($module.ExportedCommands.Count)" -ForegroundColor Gray
                    }
                } catch {
                    Write-Host "   ✗ 模块加载失败: $($_.Exception.Message)" -ForegroundColor Red
                }
            }
        } else {
            Write-Host "   未配置 PowerShell 模块路径" -ForegroundColor Yellow
            Write-Host "   这是安装失败的可能原因！" -ForegroundColor Red
        }
    } catch {
        Write-Host "   错误: $($_.Exception.Message)" -ForegroundColor Red
    }
}
Write-Host ""

# 7. 建议的修复步骤
Write-Host "7. 故障排除建议:" -ForegroundColor Yellow

if (-not (Test-Path $expectedLogDir)) {
    Write-Host "   ⚠ 日志目录不存在" -ForegroundColor Red
    Write-Host "   → 运行应用程序以自动创建目录" -ForegroundColor Cyan
}

if (-not (Test-Path $settingsPath)) {
    Write-Host "   ⚠ 设置文件不存在" -ForegroundColor Red
    Write-Host "   → 运行应用程序以创建默认设置" -ForegroundColor Cyan
}

if (Test-Path $settingsPath) {
    $settings = Get-Content $settingsPath | ConvertFrom-Json
    if (-not $settings.PowerShellModulePath) {
        Write-Host "   ⚠ PowerShell 模块路径未配置" -ForegroundColor Red
        Write-Host "   → 配置方法:" -ForegroundColor Cyan
        Write-Host "      `$settings = Get-Content '$settingsPath' | ConvertFrom-Json" -ForegroundColor Gray
        Write-Host "      `$settings | Add-Member -NotePropertyName 'PowerShellModulePath' -NotePropertyValue 'D:\wsl\DistroNexus\src\PowerShell' -Force" -ForegroundColor Gray
        Write-Host "      `$settings | ConvertTo-Json | Set-Content '$settingsPath'" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "诊断完成！" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 提供快捷命令
Write-Host "快捷命令:" -ForegroundColor Yellow
Write-Host "  打开日志目录: explorer `"$expectedLogDir`"" -ForegroundColor Gray
Write-Host "  打开设置文件: notepad `"$settingsPath`"" -ForegroundColor Gray
Write-Host "  实时监控日志: Get-Content `"$expectedLogDir\DistroNexus_`$(Get-Date -Format 'yyyy-MM-dd').log`" -Wait -Tail 10" -ForegroundColor Gray
Write-Host ""
