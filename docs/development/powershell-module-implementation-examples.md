# PowerShell 模块缺失功能 - 实现示例与测试指南

> 本文档提供完整的代码示例、使用场景和测试建议

---

## 目录

1. [完整使用场景示例](#完整使用场景示例)
2. [测试用例设计](#测试用例设计)
3. [性能基准测试](#性能基准测试)
4. [故障排除指南](#故障排除指南)
5. [最佳实践](#最佳实践)

---

## 完整使用场景示例

### 场景 1: 团队开发环境快速部署

**需求**: 团队需要快速部署统一的开发环境

```powershell
# 1. 更新发行版目录
Update-DistroNexusCatalog -PreserveLocalPath

# 2. 批量下载所需的发行版
Save-DistroNexusPackage -Family "Ubuntu" -Parallel

# 3. 使用快速模式安装（基于团队配置）
# config/settings.json 中配置:
# {
#   "DefaultDistro": "Ubuntu-22.04",
#   "DefaultInstallPath": "D:\\Dev\\WSL",
#   "DefaultUsername": "developer"
# }

$password = Read-Host -AsSecureString -Prompt "Enter default password"

# 为每个开发者安装实例
$developers = @("Alice", "Bob", "Carol")

foreach ($dev in $developers) {
    $instanceName = "Dev-$dev"
    Install-DistroNexusInstance -Quick -InstanceName $instanceName -Username "dev" -Password $password -AutoDownload
}

# 4. 验证安装
Get-DistroNexusInstance | Format-Table Name, State, Version, DiskSize

# 5. 缓存实例信息以提高后续查询速度
Sync-DistroNexusCache -IncludeExtendedInfo
```

### 场景 2: 多发行版测试环境

**需求**: QA 团队需要在多个发行版上测试应用

```powershell
# 1. 列出可用的发行版
Get-DistroNexusPackage | Group-Object Family | Format-Table Name, Count

# 2. 交互式安装多个测试环境
$distros = @(
    @{ Family = "Ubuntu"; Version = "22.04" },
    @{ Family = "Debian"; Version = "11" },
    @{ Family = "Alpine"; Version = "3.18" }
)

foreach ($distro in $distros) {
    $name = "$($distro.Family)-$($distro.Version)-Test"
    $path = "E:\Testing\WSL\$name"
    
    Write-Host "`nInstalling $name..." -ForegroundColor Cyan
    
    # 自动下载并安装
    Install-DistroNexusInstance `
        -DistroName "$($distro.Family)-$($distro.Version)" `
        -InstallPath $path `
        -AutoDownload `
        -Verbose
}

# 3. 启动所有测试环境
Get-DistroNexusInstance -Name "*-Test" | Start-DistroNexusInstance

# 4. 运行测试脚本（示例）
Get-DistroNexusInstance -Name "*-Test" | ForEach-Object {
    Write-Host "Testing on $($_.Name)..." -ForegroundColor Yellow
    wsl -d $_.Name -e bash -c "uname -a"
    wsl -d $_.Name -e bash -c "cat /etc/os-release"
}
```

### 场景 3: 实例维护和清理

**需求**: 定期清理和维护 WSL 实例

```powershell
# 清理脚本
function Optimize-DistroNexusInstances {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [int]$MaxDiskSizeGB = 50,
        
        [Parameter(Mandatory = $false)]
        [int]$InactiveDays = 30
    )
    
    Write-Host "=== WSL Instance Maintenance ===" -ForegroundColor Cyan
    
    # 1. 刷新实例缓存
    Write-Host "`n1. Refreshing instance cache..." -ForegroundColor Yellow
    $instances = Sync-DistroNexusCache
    
    # 2. 检查磁盘使用
    Write-Host "`n2. Checking disk usage..." -ForegroundColor Yellow
    $largInstances = $instances | Where-Object { $_.DiskSize -gt ($MaxDiskSizeGB * 1GB) }
    
    if ($largInstances) {
        Write-Host "`nLarge instances found:" -ForegroundColor Yellow
        $largeInstances | ForEach-Object {
            $sizGB = [math]::Round($_.DiskSize / 1GB, 2)
            Write-Host "  - $($_.Name): $sizeGB GB" -ForegroundColor Red
        }
        
        $compact = Read-Host "`nCompact large instances? (Y/N)"
        if ($compact -eq 'Y') {
            foreach ($inst in $largeInstances) {
                Write-Host "  Compacting $($inst.Name)..." -ForegroundColor Cyan
                
                # 停止实例
                Stop-DistroNexusInstance -Name $inst.Name -Force
                
                # 压缩 VHDX
                $vhdxPath = Join-Path $inst.BasePath "ext4.vhdx"
                if (Test-Path $vhdxPath) {
                    Optimize-VHD -Path $vhdxPath -Mode Full
                    Write-Host "    ✓ Compacted" -ForegroundColor Green
                }
            }
        }
    }
    
    # 3. 检查非活动实例
    Write-Host "`n3. Checking inactive instances..." -ForegroundColor Yellow
    $cutoffDate = (Get-Date).AddDays(-$InactiveDays)
    
    $inactiveInstances = $instances | Where-Object {
        $_.State -eq 'Stopped' -and $_.InstallTime -lt $cutoffDate
    }
    
    if ($inactiveInstances) {
        Write-Host "`nInactive instances (>$InactiveDays days):" -ForegroundColor Yellow
        $inactiveInstances | ForEach-Object {
            $daysSince = ((Get-Date) - $_.InstallTime).Days
            Write-Host "  - $($_.Name): $daysSince days old" -ForegroundColor Gray
        }
        
        $remove = Read-Host "`nRemove inactive instances? (Y/N)"
        if ($remove -eq 'Y') {
            $inactiveInstances | Remove-DistroNexusInstance -Interactive
        }
    }
    
    # 4. 验证实例健康状态
    Write-Host "`n4. Verifying instance health..." -ForegroundColor Yellow
    foreach ($inst in $instances) {
        $healthy = $true
        
        # 检查路径是否存在
        if (-not (Test-Path $inst.BasePath)) {
            Write-Host "  ✗ $($inst.Name): Base path missing" -ForegroundColor Red
            $healthy = $false
        }
        
        # 检查 VHDX 文件
        $vhdxPath = Join-Path $inst.BasePath "ext4.vhdx"
        if (-not (Test-Path $vhdxPath)) {
            Write-Host "  ✗ $($inst.Name): VHDX file missing" -ForegroundColor Red
            $healthy = $false
        }
        
        if ($healthy) {
            Write-Host "  ✓ $($inst.Name): Healthy" -ForegroundColor Green
        }
    }
    
    Write-Host "`n=== Maintenance completed ===" -ForegroundColor Green
}

# 运行维护
Optimize-DistroNexusInstances -MaxDiskSizeGB 30 -InactiveDays 60
```

### 场景 4: 实例备份和迁移

**需求**: 备份实例并迁移到新机器

```powershell
# 备份脚本
function Backup-DistroNexusInstances {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$BackupPath,
        
        [Parameter(Mandatory = $false)]
        [string[]]$InstanceNames
    )
    
    if (-not (Test-Path $BackupPath)) {
        New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null
    }
    
    # 获取要备份的实例
    $instances = if ($InstanceNames) {
        Get-DistroNexusInstance | Where-Object { $InstanceNames -contains $_.Name }
    }
    else {
        Get-DistroNexusInstance
    }
    
    Write-Host "Backing up $($instances.Count) instance(s) to: $BackupPath" -ForegroundColor Cyan
    
    foreach ($inst in $instances) {
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $backupFile = Join-Path $BackupPath "$($inst.Name)-$timestamp.tar"
        
        Write-Host "`nBacking up: $($inst.Name)..." -ForegroundColor Yellow
        
        # 停止实例（如果正在运行）
        if ($inst.State -eq 'Running') {
            Write-Host "  Stopping instance..." -ForegroundColor Gray
            Stop-DistroNexusInstance -Name $inst.Name -Force
            Start-Sleep -Seconds 2
        }
        
        # 导出实例
        Write-Host "  Exporting..." -ForegroundColor Gray
        wsl --export $inst.Name $backupFile
        
        if ($LASTEXITCODE -eq 0) {
            $sizeGB = [math]::Round((Get-Item $backupFile).Length / 1GB, 2)
            Write-Host "  ✓ Backup completed: $sizeGB GB" -ForegroundColor Green
            
            # 保存元数据
            $metadata = @{
                Name = $inst.Name
                BackupDate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
                OriginalPath = $inst.BasePath
                Version = $inst.Version
                DiskSize = $inst.DiskSize
            }
            
            $metadataFile = "$backupFile.json"
            $metadata | ConvertTo-Json | Set-Content $metadataFile
        }
        else {
            Write-Host "  ✗ Backup failed" -ForegroundColor Red
        }
    }
    
    Write-Host "`nBackup completed. Files saved to: $BackupPath" -ForegroundColor Green
}

# 恢复脚本
function Restore-DistroNexusInstance {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$BackupFile,
        
        [Parameter(Mandatory = $false)]
        [string]$NewName,
        
        [Parameter(Mandatory = $false)]
        [string]$InstallPath
    )
    
    if (-not (Test-Path $BackupFile)) {
        throw "Backup file not found: $BackupFile"
    }
    
    # 读取元数据
    $metadataFile = "$BackupFile.json"
    $metadata = if (Test-Path $metadataFile) {
        Get-Content $metadataFile -Raw | ConvertFrom-Json
    }
    else {
        $null
    }
    
    # 确定实例名称
    if (-not $NewName) {
        if ($metadata) {
            $NewName = $metadata.Name
        }
        else {
            $NewName = [System.IO.Path]::GetFileNameWithoutExtension($BackupFile) -replace '-\d{8}-\d{6}$', ''
        }
    }
    
    # 确定安装路径
    if (-not $InstallPath) {
        if ($metadata) {
            $InstallPath = $metadata.OriginalPath
        }
        else {
            $InstallPath = Join-Path $env:LOCALAPPDATA "WSL\$NewName"
        }
    }
    
    Write-Host "Restoring instance: $NewName" -ForegroundColor Cyan
    Write-Host "  From: $BackupFile" -ForegroundColor Gray
    Write-Host "  To: $InstallPath" -ForegroundColor Gray
    
    # 检查实例是否已存在
    $existing = Get-DistroNexusInstance -Name $NewName
    if ($existing) {
        $overwrite = Read-Host "`nInstance '$NewName' already exists. Overwrite? (yes/no)"
        if ($overwrite -ne 'yes') {
            Write-Host "Restore cancelled." -ForegroundColor Yellow
            return
        }
        
        Remove-DistroNexusInstance -Name $NewName -Force
    }
    
    # 导入实例
    Write-Host "`nImporting instance..." -ForegroundColor Yellow
    wsl --import $NewName $InstallPath $BackupFile
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Instance restored successfully" -ForegroundColor Green
        
        # 更新缓存
        $instance = Get-DistroNexusInstance -Name $NewName -ForceUpdate
        Update-InstanceCache -Instance $instance
        
        # 显示信息
        $instance | Format-List Name, State, Version, BasePath, DiskSize
    }
    else {
        Write-Host "✗ Restore failed" -ForegroundColor Red
    }
}

# 使用示例
# 备份所有实例
Backup-DistroNexusInstances -BackupPath "E:\Backups\WSL"

# 备份特定实例
Backup-DistroNexusInstances -BackupPath "E:\Backups\WSL" -InstanceNames @("Ubuntu-Dev", "Debian-Test")

# 恢复实例
Restore-DistroNexusInstance -BackupFile "E:\Backups\WSL\Ubuntu-Dev-20260129-143022.tar"

# 恢复到新名称和路径
Restore-DistroNexusInstance -BackupFile "E:\Backups\WSL\Ubuntu-Dev-20260129-143022.tar" -NewName "Ubuntu-Prod" -InstallPath "D:\Production\WSL\Ubuntu"
```

---

## 测试用例设计

### 单元测试 (Pester)

#### 测试缓存机制

```powershell
Describe "Instance Cache Tests" {
    BeforeAll {
        Import-Module "$PSScriptRoot\..\DistroNexus.psd1" -Force
        $script:TestCacheDir = Join-Path $TestDrive "cache"
        New-Item -ItemType Directory -Path $script:TestCacheDir -Force | Out-Null
    }
    
    Context "Get-InstanceCache" {
        It "Should return null when cache file does not exist" {
            Mock Get-DistroNexusConfig { Join-Path $script:TestCacheDir "nonexistent.json" }
            
            $result = Get-InstanceCache
            $result | Should -BeNullOrEmpty
        }
        
        It "Should load valid cache file" {
            $cacheFile = Join-Path $script:TestCacheDir "instances.json"
            $testData = @(
                @{ Name = "Test1"; State = "Running" },
                @{ Name = "Test2"; State = "Stopped" }
            )
            $testData | ConvertTo-Json | Set-Content $cacheFile
            
            Mock Get-DistroNexusConfig { $cacheFile }
            
            $result = Get-InstanceCache
            $result | Should -HaveCount 2
            $result[0].Name | Should -Be "Test1"
        }
        
        It "Should handle corrupted cache file gracefully" {
            $cacheFile = Join-Path $script:TestCacheDir "corrupted.json"
            "{ invalid json" | Set-Content $cacheFile
            
            Mock Get-DistroNexusConfig { $cacheFile }
            
            $result = Get-InstanceCache
            $result | Should -BeNullOrEmpty
        }
    }
    
    Context "Set-InstanceCache" {
        It "Should create backup before overwriting" {
            $cacheFile = Join-Path $script:TestCacheDir "instances.json"
            @{ Name = "Old" } | ConvertTo-Json | Set-Content $cacheFile
            
            Mock Get-DistroNexusConfig { $cacheFile }
            
            $newData = @(@{ Name = "New" })
            Set-InstanceCache -Instances $newData
            
            # 检查备份文件是否创建
            $backups = Get-ChildItem -Path $script:TestCacheDir -Filter "instances.json.*.bak"
            $backups | Should -HaveCount 1
        }
        
        It "Should limit number of backups to 3" {
            $cacheFile = Join-Path $script:TestCacheDir "instances.json"
            Mock Get-DistroNexusConfig { $cacheFile }
            
            # 创建多个备份
            for ($i = 1; $i -le 5; $i++) {
                $data = @(@{ Name = "Test$i" })
                Set-InstanceCache -Instances $data
                Start-Sleep -Milliseconds 100  # 确保时间戳不同
            }
            
            # 验证只保留最新的 3 个备份
            $backups = Get-ChildItem -Path $script:TestCacheDir -Filter "instances.json.*.bak"
            $backups.Count | Should -BeLessOrEqual 3
        }
    }
    
    Context "Update-InstanceCache" {
        It "Should add new instance to empty cache" {
            Mock Get-InstanceCache { @() }
            Mock Set-InstanceCache { }
            
            $instance = [PSCustomObject]@{ Name = "Test"; State = "Running" }
            Update-InstanceCache -Instance $instance
            
            Assert-MockCalled Set-InstanceCache -Times 1
        }
        
        It "Should update existing instance" {
            $existingCache = @(
                [PSCustomObject]@{ Name = "Test"; State = "Stopped" }
            )
            Mock Get-InstanceCache { $existingCache }
            Mock Set-InstanceCache { param($Instances) $Instances[0].State | Should -Be "Running" }
            
            $updatedInstance = [PSCustomObject]@{ Name = "Test"; State = "Running" }
            Update-InstanceCache -Instance $updatedInstance
            
            Assert-MockCalled Set-InstanceCache -Times 1
        }
    }
}
```

#### 测试包下载功能

```powershell
Describe "Package Download Tests" {
    BeforeAll {
        Import-Module "$PSScriptRoot\..\DistroNexus.psd1" -Force
        $script:TestDownloadDir = Join-Path $TestDrive "downloads"
        New-Item -ItemType Directory -Path $script:TestDownloadDir -Force | Out-Null
    }
    
    Context "Save-DistroNexusPackage Single Package" {
        It "Should download single package successfully" {
            Mock Get-DistroNexusPackage {
                @(
                    [PSCustomObject]@{
                        DefaultName = "Test-Package"
                        Family = "TestFamily"
                        Name = "Test Package"
                        Url = "https://example.com/test.tar"
                        IsCached = $false
                    }
                )
            }
            
            Mock Get-DistroCachePath { $script:TestDownloadDir }
            Mock Invoke-WebRequest { }
            
            { Save-DistroNexusPackage -Name "Test-Package" } | Should -Not -Throw
            
            Assert-MockCalled Invoke-WebRequest -Times 1
        }
        
        It "Should skip already cached packages" {
            Mock Get-DistroNexusPackage {
                @(
                    [PSCustomObject]@{
                        DefaultName = "Cached-Package"
                        IsCached = $true
                    }
                )
            }
            
            Mock Invoke-WebRequest { }
            
            Save-DistroNexusPackage -Name "Cached-Package"
            
            Assert-MockCalled Invoke-WebRequest -Times 0
        }
    }
    
    Context "Save-DistroNexusPackage Batch Download" {
        It "Should download all packages in family" {
            Mock Get-DistroNexusPackage {
                @(
                    [PSCustomObject]@{
                        DefaultName = "Ubuntu-20.04"
                        Family = "Ubuntu"
                        IsCached = $false
                        Url = "https://example.com/ubuntu20.tar"
                    },
                    [PSCustomObject]@{
                        DefaultName = "Ubuntu-22.04"
                        Family = "Ubuntu"
                        IsCached = $false
                        Url = "https://example.com/ubuntu22.tar"
                    }
                )
            }
            
            Mock Get-DistroCachePath { $script:TestDownloadDir }
            Mock Save-SinglePackage { }
            
            Save-DistroNexusPackage -Family "Ubuntu"
            
            Assert-MockCalled Save-SinglePackage -Times 2
        }
    }
}
```

#### 测试用户配置功能

```powershell
Describe "User Configuration Tests" {
    BeforeAll {
        Import-Module "$PSScriptRoot\..\DistroNexus.psd1" -Force
    }
    
    Context "Set-DistroDefaultUser" {
        BeforeEach {
            Mock wsl { $global:LASTEXITCODE = 0; return "output" }
            Mock Write-Host { }
        }
        
        It "Should create user and set password" {
            $securePass = ConvertTo-SecureString "TestPass123" -AsPlainText -Force
            
            { Set-DistroDefaultUser -DistroName "TestDistro" -Username "testuser" -Password $securePass } | Should -Not -Throw
            
            # 验证调用了 useradd
            Assert-MockCalled wsl -ParameterFilter { $args -contains "useradd" }
            
            # 验证调用了 chpasswd
            Assert-MockCalled wsl -ParameterFilter { $args -contains "chpasswd" }
        }
        
        It "Should add user to sudo group" {
            $securePass = ConvertTo-SecureString "TestPass123" -AsPlainText -Force
            
            Set-DistroDefaultUser -DistroName "TestDistro" -Username "testuser" -Password $securePass
            
            # 验证调用了 usermod 添加到 sudo 组
            Assert-MockCalled wsl -ParameterFilter { $args -match "usermod.*sudo" }
        }
        
        It "Should update wsl.conf" {
            $securePass = ConvertTo-SecureString "TestPass123" -AsPlainText -Force
            
            Mock wsl { 
                param($d, $e, $Command)
                if ($Command -match "wsl.conf") {
                    $global:LASTEXITCODE = 0
                    return "exists"
                }
                $global:LASTEXITCODE = 0
                return ""
            }
            
            Set-DistroDefaultUser -DistroName "TestDistro" -Username "testuser" -Password $securePass
            
            # 验证尝试更新 wsl.conf
            Assert-MockCalled wsl -ParameterFilter { $args -match "wsl.conf" }
        }
    }
}
```

---

### 集成测试

#### 完整安装流程测试

```powershell
Describe "End-to-End Installation Test" -Tag "Integration" {
    BeforeAll {
        # 需要真实的 WSL 环境
        if (-not (Get-Command wsl -ErrorAction SilentlyContinue)) {
            Set-ItResult -Skipped -Because "WSL not available"
        }
        
        Import-Module "$PSScriptRoot\..\DistroNexus.psd1" -Force
        
        $script:TestInstanceName = "DistroNexus-Test-$(Get-Date -Format 'yyyyMMddHHmmss')"
        $script:TestInstallPath = Join-Path $TestDrive "WSL\$script:TestInstanceName"
    }
    
    It "Should install instance successfully" {
        # 假设已有缓存的包
        { Install-DistroNexusInstance -DistroName "Alpine" -InstallPath $script:TestInstallPath } | Should -Not -Throw
    }
    
    It "Should appear in instance list" {
        $instance = Get-DistroNexusInstance -Name $script:TestInstanceName
        $instance | Should -Not -BeNullOrEmpty
        $instance.Name | Should -Be $script:TestInstanceName
    }
    
    It "Should start instance" {
        { Start-DistroNexusInstance -Name $script:TestInstanceName } | Should -Not -Throw
        
        Start-Sleep -Seconds 2
        $instance = Get-DistroNexusInstance -Name $script:TestInstanceName
        $instance.State | Should -Be "Running"
    }
    
    It "Should stop instance" {
        { Stop-DistroNexusInstance -Name $script:TestInstanceName -Force } | Should -Not -Throw
        
        Start-Sleep -Seconds 2
        $instance = Get-DistroNexusInstance -Name $script:TestInstanceName
        $instance.State | Should -Be "Stopped"
    }
    
    It "Should move instance" {
        $newPath = Join-Path $TestDrive "WSL\Moved\$script:TestInstanceName"
        
        { Move-DistroNexusInstance -Name $script:TestInstanceName -Destination $newPath } | Should -Not -Throw
        
        $instance = Get-DistroNexusInstance -Name $script:TestInstanceName
        $instance.BasePath | Should -Be $newPath
    }
    
    AfterAll {
        # 清理测试实例
        if (Get-DistroNexusInstance -Name $script:TestInstanceName) {
            Remove-DistroNexusInstance -Name $script:TestInstanceName -Force
        }
    }
}
```

---

## 性能基准测试

### 缓存性能测试

```powershell
function Measure-CachePerformance {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [int]$Iterations = 10
    )
    
    Write-Host "=== Cache Performance Benchmark ===" -ForegroundColor Cyan
    
    # 测试 1: 无缓存查询
    Write-Host "`nTest 1: Without Cache" -ForegroundColor Yellow
    $noCacheTimes = @()
    
    for ($i = 1; $i -le $Iterations; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        Get-DistroNexusInstance | Out-Null
        $sw.Stop()
        $noCacheTimes += $sw.ElapsedMilliseconds
        Write-Host "  Run $i: $($sw.ElapsedMilliseconds) ms" -ForegroundColor Gray
    }
    
    $avgNoCache = ($noCacheTimes | Measure-Object -Average).Average
    
    # 测试 2: 有缓存查询
    Write-Host "`nTest 2: With Cache" -ForegroundColor Yellow
    
    # 先创建缓存
    Sync-DistroNexusCache | Out-Null
    
    $cacheTimes = @()
    
    for ($i = 1; $i -le $Iterations; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        Get-DistroNexusInstance -UseCache | Out-Null
        $sw.Stop()
        $cacheTimes += $sw.ElapsedMilliseconds
        Write-Host "  Run $i: $($sw.ElapsedMilliseconds) ms" -ForegroundColor Gray
    }
    
    $avgCache = ($cacheTimes | Measure-Object -Average).Average
    
    # 结果
    Write-Host "`n=== Results ===" -ForegroundColor Green
    Write-Host "Average without cache: $([math]::Round($avgNoCache, 2)) ms"
    Write-Host "Average with cache: $([math]::Round($avgCache, 2)) ms"
    Write-Host "Performance improvement: $([math]::Round(($avgNoCache - $avgCache) / $avgNoCache * 100, 2))%"
    
    return @{
        NoCacheAvg = $avgNoCache
        CacheAvg = $avgCache
        Improvement = ($avgNoCache - $avgCache) / $avgNoCache * 100
    }
}

# 运行基准测试
$results = Measure-CachePerformance -Iterations 20
```

### 批量下载性能测试

```powershell
function Measure-DownloadPerformance {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Family
    )
    
    $packages = Get-DistroNexusPackage -Family $Family -Available
    
    if ($packages.Count -eq 0) {
        Write-Host "No packages to download for $Family" -ForegroundColor Yellow
        return
    }
    
    Write-Host "=== Download Performance Test ===" -ForegroundColor Cyan
    Write-Host "Testing with $($packages.Count) packages from $Family" -ForegroundColor Gray
    
    # 测试 1: 串行下载
    Write-Host "`nTest 1: Sequential Download" -ForegroundColor Yellow
    $sw1 = [System.Diagnostics.Stopwatch]::StartNew()
    Save-DistroNexusPackage -Family $Family
    $sw1.Stop()
    
    $sequentialTime = $sw1.Elapsed.TotalSeconds
    Write-Host "Time: $([math]::Round($sequentialTime, 2)) seconds" -ForegroundColor Green
    
    # 清理
    Write-Host "Cleaning up..." -ForegroundColor Gray
    # ... 删除下载的文件 ...
    
    # 测试 2: 并行下载
    Write-Host "`nTest 2: Parallel Download" -ForegroundColor Yellow
    $sw2 = [System.Diagnostics.Stopwatch]::StartNew()
    Save-DistroNexusPackage -Family $Family -Parallel
    $sw2.Stop()
    
    $parallelTime = $sw2.Elapsed.TotalSeconds
    Write-Host "Time: $([math]::Round($parallelTime, 2)) seconds" -ForegroundColor Green
    
    # 结果
    Write-Host "`n=== Results ===" -ForegroundColor Cyan
    Write-Host "Sequential: $([math]::Round($sequentialTime, 2))s"
    Write-Host "Parallel: $([math]::Round($parallelTime, 2))s"
    Write-Host "Speedup: $([math]::Round($sequentialTime / $parallelTime, 2))x"
}
```

---

## 故障排除指南

### 常见问题解决

#### 问题 1: 缓存数据过时

**症状**: `Get-DistroNexusInstance -UseCache` 返回过时的信息

**解决方案**:
```powershell
# 强制刷新缓存
Get-DistroNexusInstance -ForceUpdate

# 或使用显式扫描
Sync-DistroNexusCache
```

#### 问题 2: 下载失败或超时

**症状**: `Save-DistroNexusPackage` 报告网络错误

**解决方案**:
```powershell
# 1. 检查网络连接
Test-NetConnection -ComputerName raw.githubusercontent.com -Port 443

# 2. 使用详细模式查看错误
Save-DistroNexusPackage -Name "Ubuntu-22.04" -Verbose

# 3. 增加超时时间（需修改代码）
# 在 Save-SinglePackage 中设置 Timeout 参数

# 4. 使用代理（如果需要）
$env:HTTP_PROXY = "http://proxy.example.com:8080"
$env:HTTPS_PROXY = "http://proxy.example.com:8080"
```

#### 问题 3: 用户配置失败

**症状**: `Set-DistroDefaultUser` 报告用户创建失败

**解决方案**:
```powershell
# 1. 手动验证实例状态
wsl -d YourInstance -e bash -c "whoami"

# 2. 检查是否有权限问题
wsl -d YourInstance -e bash -c "sudo -v"

# 3. 查看日志
Get-Content "$env:LOCALAPPDATA\DistroNexus\logs\distronexus.log" -Tail 50

# 4. 手动配置用户
wsl -d YourInstance -e bash -c "useradd -m -s /bin/bash newuser"
wsl -d YourInstance -e bash -c "echo 'newuser:password' | chpasswd"
wsl -d YourInstance -e bash -c "usermod -aG sudo newuser"
```

---

## 最佳实践

### 1. 性能优化

```powershell
# 使用缓存提高查询速度
Get-DistroNexusInstance -UseCache

# 批量操作时使用管道
Get-DistroNexusInstance -State "Running" | Stop-DistroNexusInstance

# 并行下载多个包
Save-DistroNexusPackage -All -Parallel
```

### 2. 安全建议

```powershell
# 使用 SecureString 存储密码
$password = Read-Host -AsSecureString -Prompt "Password"
Install-DistroNexusInstance -DistroName "Ubuntu" -InstallPath "D:\WSL" -Username "admin" -Password $password

# 定期备份实例
Backup-DistroNexusInstances -BackupPath "E:\Backups\WSL"

# 启用配置备份
Update-DistroNexusCatalog  # 自动备份
```

### 3. 自动化脚本

```powershell
# 定期维护脚本
$MaintenanceJob = {
    Import-Module DistroNexus
    
    # 刷新缓存
    Sync-DistroNexusCache
    
    # 更新发行版目录
    Update-DistroNexusCatalog -PreserveLocalPath
    
    # 压缩大实例
    Get-DistroNexusInstance | Where-Object { $_.DiskSize -gt 30GB } | ForEach-Object {
        Stop-DistroNexusInstance -Name $_.Name -Force
        $vhdx = Join-Path $_.BasePath "ext4.vhdx"
        Optimize-VHD -Path $vhdx -Mode Full
    }
}

# 创建计划任务
$Trigger = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Sunday -At 2am
$Action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -ExecutionPolicy Bypass -Command `"$MaintenanceJob`""
Register-ScheduledTask -TaskName "WSL Maintenance" -Trigger $Trigger -Action $Action -Description "Weekly WSL instance maintenance"
```

### 4. 团队协作

```powershell
# 共享配置文件
# 在项目根目录创建 wsl-config.json
@{
    DefaultDistro = "Ubuntu-22.04"
    DefaultInstallPath = "D:\Dev\WSL"
    DefaultUsername = "developer"
    RequiredPackages = @("Ubuntu-22.04", "Debian-11", "Alpine-3.18")
} | ConvertTo-Json | Set-Content "wsl-config.json"

# 团队成员使用统一配置
$config = Get-Content "wsl-config.json" | ConvertFrom-Json

# 下载所需的包
foreach ($pkg in $config.RequiredPackages) {
    Save-DistroNexusPackage -Name $pkg
}

# 使用配置安装
Install-DistroNexusInstance -Quick -InstanceName "TeamEnv"
```

---

**文档版本**: 1.0
**最后更新**: 2026-01-29
