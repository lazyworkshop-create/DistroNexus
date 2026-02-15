# DistroNexus 紧急诊断 - 找出卡死的确切位置
# 这个脚本会持续监控应用启动过程并记录详细信息

$logFile = Join-Path $env:TEMP "DistroNexus-Emergency-Diagnostic.log"
$settingsPath = Join-Path $env:APPDATA "DistroNexus\settings.json"

function Write-Log {
    param($Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $logLine = "[$timestamp] $Message"
    Write-Host $logLine -ForegroundColor Cyan
    Add-Content -Path $logFile -Value $logLine
}

Write-Host "=== DistroNexus 紧急诊断工具 ===" -ForegroundColor Yellow
Write-Host "日志文件: $logFile" -ForegroundColor Gray
Write-Host ""

# 清空旧日志
"" | Set-Content $logFile
Write-Log "=== 诊断开始 ==="

# 1. 检查 settings.json 文件状态
Write-Log "检查 settings.json 文件..."
if (Test-Path $settingsPath) {
    $fileInfo = Get-Item $settingsPath
    Write-Log "文件存在: $settingsPath"
    Write-Log "文件大小: $($fileInfo.Length) 字节"
    Write-Log "最后修改: $($fileInfo.LastWriteTime)"
    
    # 尝试读取内容
    try {
        Write-Log "尝试读取文件内容..."
        $startRead = Get-Date
        $content = Get-Content $settingsPath -Raw -ErrorAction Stop
        $readTime = (Get-Date) - $startRead
        Write-Log "✅ 文件读取成功，耗时: $($readTime.TotalMilliseconds) ms"
        Write-Log "内容长度: $($content.Length) 字符"
        
        # 尝试解析 JSON
        try {
            Write-Log "尝试解析 JSON..."
            $startParse = Get-Date
            $json = $content | ConvertFrom-Json -ErrorAction Stop
            $parseTime = (Get-Date) - $startParse
            Write-Log "✅ JSON 解析成功，耗时: $($parseTime.TotalMilliseconds) ms"
        }
        catch {
            Write-Log "❌ JSON 解析失败: $($_.Exception.Message)"
            Write-Log "前 200 字符: $($content.Substring(0, [Math]::Min(200, $content.Length)))"
        }
    }
    catch {
        Write-Log "❌ 文件读取失败: $($_.Exception.Message)"
    }
}
else {
    Write-Log "⚠️  文件不存在: $settingsPath"
}

# 2. 检查文件锁定
Write-Log ""
Write-Log "检查文件锁定状态..."
try {
    $fileStream = [System.IO.File]::Open($settingsPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    $fileStream.Close()
    Write-Log "✅ 文件未被锁定"
}
catch {
    Write-Log "⚠️  文件可能被锁定: $($_.Exception.Message)"
    
    # 尝试找到锁定文件的进程
    Write-Log "尝试查找锁定进程..."
    $handle = Get-Process | Where-Object { 
        $_.Modules.FileName -contains $settingsPath 
    }
    if ($handle) {
        Write-Log "❌ 文件被以下进程锁定:"
        $handle | ForEach-Object {
            Write-Log "   - $($_.ProcessName) (PID: $($_.Id))"
        }
    }
}

# 3. 监控应用启动
Write-Log ""
Write-Log "准备启动 DistroNexus 并监控..."
Write-Log "按 Ctrl+C 中止监控"
Write-Log ""

# 查找 DistroNexus 可执行文件
$possiblePaths = @(
    "D:\wsl\DistroNexus\src\Client\DistroNexus.Desktop\bin\Debug\net10.0-windows\DistroNexus.Desktop.exe",
    "D:\wsl\DistroNexus\src\Client\DistroNexus.Desktop\bin\Release\net10.0-windows\DistroNexus.Desktop.exe"
)

$exePath = $null
foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $exePath = $path
        Write-Log "找到可执行文件: $exePath"
        break
    }
}

if (-not $exePath) {
    Write-Log "❌ 未找到 DistroNexus.Desktop.exe"
    Write-Log "请先编译项目"
    Write-Log ""
    Write-Log "按任意键退出..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit
}

# 启动应用并监控
Write-Log "启动应用: $exePath"
$startTime = Get-Date

try {
    $process = Start-Process -FilePath $exePath -PassThru -WindowStyle Normal
    Write-Log "进程已启动 (PID: $($process.Id))"
    
    # 监控进程 30 秒
    $monitorStart = Get-Date
    $timeout = 30
    
    Write-Host ""
    Write-Host "监控进程 30 秒..." -ForegroundColor Yellow
    Write-Host "如果应用卡住，日志会记录详细信息" -ForegroundColor Gray
    Write-Host ""
    
    for ($i = 0; $i -lt $timeout; $i++) {
        Start-Sleep -Seconds 1
        
        # 检查进程是否还在运行
        if ($process.HasExited) {
            Write-Log "⚠️  进程已退出 (退出代码: $($process.ExitCode))"
            break
        }
        
        # 每 5 秒记录一次进程状态
        if ($i % 5 -eq 0) {
            $elapsed = (Get-Date) - $startTime
            $process.Refresh()
            Write-Log "[$($i)s] 进程仍在运行，CPU: $($process.CPU)%, 内存: $([Math]::Round($process.WorkingSet64 / 1MB, 2)) MB"
            
            # 检查主窗口是否创建
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
                Write-Log "✅ 主窗口已创建！窗口标题: $($process.MainWindowTitle)"
            }
            else {
                Write-Log "⏳ 等待主窗口创建..."
            }
        }
    }
    
    $totalTime = (Get-Date) - $startTime
    Write-Log ""
    Write-Log "=== 监控完成 ==="
    Write-Log "总耗时: $($totalTime.TotalSeconds) 秒"
    
    if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
        Write-Log "✅ 应用启动成功"
    }
    else {
        Write-Log "⚠️  应用可能卡住（主窗口未创建）"
    }
}
catch {
    Write-Log "❌ 启动失败: $($_.Exception.Message)"
}

Write-Log ""
Write-Log "=== 诊断完成 ==="
Write-Log "完整日志已保存到: $logFile"

Write-Host ""
Write-Host "=== 诊断完成 ===" -ForegroundColor Green
Write-Host ""
Write-Host "日志文件: $logFile" -ForegroundColor Cyan
Write-Host ""
Write-Host "按任意键打开日志文件..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# 打开日志文件
notepad $logFile
