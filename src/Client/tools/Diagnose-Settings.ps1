# DistroNexus Settings 诊断脚本
# 用于检查 settings.json 文件是否存在问题

Write-Host "=== DistroNexus Settings 诊断工具 ===" -ForegroundColor Cyan
Write-Host ""

# 1. 检查文件路径
$settingsPath = Join-Path $env:APPDATA "DistroNexus\settings.json"
Write-Host "📁 配置文件路径: $settingsPath" -ForegroundColor Yellow

# 2. 检查文件是否存在
if (Test-Path $settingsPath) {
    Write-Host "✅ 文件存在" -ForegroundColor Green
    
    # 3. 检查文件大小
    $fileInfo = Get-Item $settingsPath
    $fileSize = $fileInfo.Length
    Write-Host "📊 文件大小: $fileSize 字节" -ForegroundColor Yellow
    
    if ($fileSize -eq 0) {
        Write-Host "⚠️  警告：文件为空！" -ForegroundColor Red
    }
    elseif ($fileSize -gt 10MB) {
        Write-Host "⚠️  警告：文件异常大（超过 10MB）！" -ForegroundColor Red
    }
    else {
        Write-Host "✅ 文件大小正常" -ForegroundColor Green
    }
    
    # 4. 测试文件读取速度
    Write-Host ""
    Write-Host "⏱️  测试文件读取速度..." -ForegroundColor Yellow
    $readTime = Measure-Command {
        $null = Get-Content $settingsPath
    }
    
    $readMs = $readTime.TotalMilliseconds
    Write-Host "   读取耗时: $([math]::Round($readMs, 2)) ms" -ForegroundColor Cyan
    
    if ($readMs -gt 1000) {
        Write-Host "⚠️  警告：读取速度很慢（超过 1 秒）！可能是磁盘问题或文件被锁定" -ForegroundColor Red
    }
    elseif ($readMs -gt 100) {
        Write-Host "⚠️  注意：读取速度较慢" -ForegroundColor Yellow
    }
    else {
        Write-Host "✅ 读取速度正常" -ForegroundColor Green
    }
    
    # 5. 验证 JSON 格式
    Write-Host ""
    Write-Host "📋 验证 JSON 格式..." -ForegroundColor Yellow
    try {
        $content = Get-Content $settingsPath -Raw
        $json = $content | ConvertFrom-Json
        Write-Host "✅ JSON 格式有效" -ForegroundColor Green
        
        # 显示 JSON 内容预览
        Write-Host ""
        Write-Host "📄 JSON 内容预览:" -ForegroundColor Yellow
        Write-Host $content.Substring(0, [Math]::Min(500, $content.Length))
        
        if ($content.Length -gt 500) {
            Write-Host "   ... (已截断，总共 $($content.Length) 字符)" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "❌ JSON 格式无效！" -ForegroundColor Red
        Write-Host "   错误: $($_.Exception.Message)" -ForegroundColor Red
        
        # 显示损坏的内容
        Write-Host ""
        Write-Host "⚠️  损坏的内容预览:" -ForegroundColor Red
        $content = Get-Content $settingsPath -Raw
        Write-Host $content.Substring(0, [Math]::Min(200, $content.Length))
    }
    
    # 6. 检查文件锁定
    Write-Host ""
    Write-Host "🔒 检查文件是否被锁定..." -ForegroundColor Yellow
    try {
        $fileStream = [System.IO.File]::Open($settingsPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $fileStream.Close()
        Write-Host "✅ 文件未被锁定" -ForegroundColor Green
    }
    catch {
        Write-Host "⚠️  警告：文件可能被其他进程锁定" -ForegroundColor Red
        Write-Host "   错误: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # 7. 检查文件权限
    Write-Host ""
    Write-Host "🔐 检查文件权限..." -ForegroundColor Yellow
    try {
        $acl = Get-Acl $settingsPath
        $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
        Write-Host "   当前用户: $currentUser" -ForegroundColor Cyan
        Write-Host "✅ 可以访问文件权限" -ForegroundColor Green
    }
    catch {
        Write-Host "⚠️  警告：无法读取文件权限" -ForegroundColor Red
    }
    
    # 8. 建议操作
    Write-Host ""
    Write-Host "💡 建议操作:" -ForegroundColor Cyan
    
    if ($fileSize -eq 0 -or ($readMs -gt 1000) -or ($null -eq $json)) {
        Write-Host ""
        Write-Host "⚠️  检测到问题！建议执行以下操作之一：" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "选项 1: 备份并删除损坏的文件（让应用创建新的）" -ForegroundColor White
        Write-Host "   Rename-Item '$settingsPath' '$settingsPath.backup'" -ForegroundColor Gray
        Write-Host ""
        Write-Host "选项 2: 手动创建最小配置" -ForegroundColor White
        Write-Host "   @{Theme='Dark';Language='en-US'} | ConvertTo-Json | Set-Content '$settingsPath'" -ForegroundColor Gray
        Write-Host ""
    }
    else {
        Write-Host "✅ 配置文件看起来正常！" -ForegroundColor Green
    }
}
else {
    Write-Host "❌ 文件不存在" -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 这是首次运行吗？应用应该会自动创建默认配置文件。" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "如果需要，可以手动创建：" -ForegroundColor Cyan
    Write-Host "   New-Item -Path '$settingsPath' -ItemType File -Force" -ForegroundColor Gray
    Write-Host "   @{Theme='Dark';Language='en-US'} | ConvertTo-Json | Set-Content '$settingsPath'" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== 诊断完成 ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "按任意键退出..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
