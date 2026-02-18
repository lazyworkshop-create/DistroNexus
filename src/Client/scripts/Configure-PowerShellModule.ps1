# 自动配置 PowerShell 模块路径
# 此脚本会自动检测并配置正确的模块路径

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host " 自动配置 PowerShell 模块路径" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# 步骤 1: 查找模块路径
Write-Host "步骤 1: 查找 PowerShell 模块..." -ForegroundColor Yellow

$scriptRoot = Split-Path -Parent $PSScriptRoot
$possiblePaths = @(
    # 源代码路径
    (Join-Path $scriptRoot "src\PowerShell"),
    (Join-Path $scriptRoot "PowerShell"),
    
    # 编译输出路径
    (Join-Path $scriptRoot "src\DistroNexus.Desktop\bin\Debug\net10.0-windows\PowerShell"),
    (Join-Path $scriptRoot "src\DistroNexus.Desktop\bin\Release\net10.0-windows\PowerShell"),
    (Join-Path $scriptRoot "DistroNexus.Desktop\bin\Debug\net10.0-windows\PowerShell"),
    (Join-Path $scriptRoot "DistroNexus.Desktop\bin\Release\net10.0-windows\PowerShell")
)

$foundPath = $null
foreach ($path in $possiblePaths) {
    $manifestPath = Join-Path $path "DistroNexus.psd1"
    Write-Host "  检查: $manifestPath" -ForegroundColor Gray
    
    if (Test-Path $manifestPath) {
        $foundPath = $path
        Write-Host "  ✓ 找到模块！" -ForegroundColor Green
        break
    }
}

if (-not $foundPath) {
    Write-Host ""
    Write-Host "✗ 未找到 PowerShell 模块" -ForegroundColor Red
    Write-Host ""
    Write-Host "请手动指定模块路径:" -ForegroundColor Yellow
    $manualPath = Read-Host "PowerShell 模块路径 (包含 DistroNexus.psd1 的目录)"
    
    if ([string]::IsNullOrWhiteSpace($manualPath)) {
        Write-Host "已取消" -ForegroundColor Red
        exit 1
    }
    
    $manifestPath = Join-Path $manualPath "DistroNexus.psd1"
    if (-not (Test-Path $manifestPath)) {
        Write-Host "✗ 在指定路径未找到 DistroNexus.psd1: $manifestPath" -ForegroundColor Red
        exit 1
    }
    
    $foundPath = $manualPath
}

Write-Host ""
Write-Host "找到的模块路径: $foundPath" -ForegroundColor Green
Write-Host ""

# 步骤 2: 更新设置文件
Write-Host "步骤 2: 更新设置文件..." -ForegroundColor Yellow

$settingsPath = "$env:LOCALAPPDATA\DistroNexus\settings.json"
Write-Host "  设置文件路径: $settingsPath" -ForegroundColor Gray

# 确保目录存在
$settingsDir = Split-Path $settingsPath
if (-not (Test-Path $settingsDir)) {
    Write-Host "  创建设置目录: $settingsDir" -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
}

# 读取或创建设置
if (Test-Path $settingsPath) {
    Write-Host "  读取现有设置..." -ForegroundColor Cyan
    $settings = Get-Content $settingsPath | ConvertFrom-Json
} else {
    Write-Host "  创建默认设置..." -ForegroundColor Cyan
    $settings = [PSCustomObject]@{
        DefaultInstallPath = "C:\WSL"
        PackageCachePath = ""
        TerminalStartPath = "~"
        DefaultWslVersion = 2
        DefaultUsername = "root"
        DefaultDistributionId = ""
        EnableLogging = $true
        LogPath = ""
        CheckUpdatesOnStartup = $true
        CatalogUrl = "https://raw.githubusercontent.com/lazyworkshop-create/DistroNexus/master/config/catalog.json"
        Theme = "Auto"
        Language = "en-US"
        ShowConfirmationDialogs = $true
        MaxConcurrentDownloads = 3
        AutoRetryDownloads = $true
        MaxRetryAttempts = 3
        AutoSaveEnabled = $true
        AutoSaveInterval = 30
        CustomData = @{}
    }
}

# 更新 PowerShellModulePath
$settings | Add-Member -NotePropertyName "PowerShellModulePath" -NotePropertyValue $foundPath -Force

# 保存设置
$settings | ConvertTo-Json -Depth 5 | Set-Content $settingsPath

Write-Host "  ✓ 设置已保存" -ForegroundColor Green
Write-Host ""

# 步骤 3: 验证配置
Write-Host "步骤 3: 验证配置..." -ForegroundColor Yellow

$verifySettings = Get-Content $settingsPath | ConvertFrom-Json
Write-Host "  PowerShellModulePath: $($verifySettings.PowerShellModulePath)" -ForegroundColor Green

$verifyManifest = Join-Path $verifySettings.PowerShellModulePath "DistroNexus.psd1"
Write-Host "  模块清单: $verifyManifest" -ForegroundColor Gray
Write-Host "  清单存在: $(Test-Path $verifyManifest)" -ForegroundColor $(if (Test-Path $verifyManifest) { "Green" } else { "Red" })

# 尝试导入模块
try {
    Write-Host ""
    Write-Host "  尝试导入模块..." -ForegroundColor Cyan
    Import-Module $verifyManifest -Force -ErrorAction Stop
    
    $module = Get-Module -Name DistroNexus
    if ($module) {
        Write-Host "  ✓ 模块导入成功！" -ForegroundColor Green
        Write-Host "    版本: $($module.Version)" -ForegroundColor Gray
        Write-Host "    导出的命令:" -ForegroundColor Gray
        $module.ExportedCommands.Keys | ForEach-Object {
            Write-Host "      - $_" -ForegroundColor DarkGray
        }
    }
} catch {
    Write-Host "  ✗ 模块导入失败: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "配置完成！" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "下一步:" -ForegroundColor Yellow
Write-Host "  1. 重启 DistroNexus 应用程序" -ForegroundColor White
Write-Host "  2. 日志将保存到: $env:LOCALAPPDATA\DistroNexus\Logs" -ForegroundColor White
Write-Host "  3. 如有问题，运行: .\scripts\Diagnose-NLog.ps1" -ForegroundColor White
Write-Host ""
