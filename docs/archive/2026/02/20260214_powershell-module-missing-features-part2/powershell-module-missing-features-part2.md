# PowerShell 模块缺失功能实现方案（第二部分）

> 接续：PowerShell-Module-Missing-Features-Implementation.md

---

## 第二部分：包管理功能缺失项

### 2.1 批量下载功能 【高优先级】

#### 功能描述
旧版 `download_all_distros.ps1` 支持批量下载所有或按族筛选的发行版包。

#### 缺失原因
新版 `Save-DistroNexusPackage` 设计为单个包下载，需要循环调用实现批量。

#### 影响分析
- **效率**: 首次配置需多次手动下载
- **用户体验**: 批量操作不便

#### 实现方案

**修改文件**: `src/PowerShell/Public/Save-DistroNexusPackage.ps1`

添加批量下载参数：

```powershell
function Save-DistroNexusPackage {
    <#
    .SYNOPSIS
        Downloads distribution packages to local cache.

    .DESCRIPTION
        Downloads one or more distribution packages from online sources.
        Supports single package download or batch download by family.

    .PARAMETER Name
        Package name to download (DefaultName from catalog)
    
    .PARAMETER Family
        Download all packages from specified family (e.g., "Ubuntu")
    
    .PARAMETER All
        Download all available packages from catalog
    
    .PARAMETER Destination
        Custom download directory. Defaults to configured DistroCachePath.
    
    .PARAMETER Parallel
        Enable parallel downloads (experimental, may use more bandwidth)

    .EXAMPLE
        Save-DistroNexusPackage -Name "Ubuntu-22.04"
        # Downloads single package

    .EXAMPLE
        Save-DistroNexusPackage -Family "Ubuntu"
        # Downloads all Ubuntu versions
    
    .EXAMPLE
        Save-DistroNexusPackage -All
        # Downloads all available packages
    
    .EXAMPLE
        Save-DistroNexusPackage -Family "Debian" -Destination "D:\Downloads"
        # Downloads to custom location
    #>
    [CmdletBinding(DefaultParameterSetName = 'Single', SupportsShouldProcess = $true)]
    [OutputType([void])]
    param(
        [Parameter(Mandatory = $true, ParameterSetName = 'Single', Position = 0)]
        [string]$Name,
        
        [Parameter(Mandatory = $true, ParameterSetName = 'Family')]
        [string]$Family,
        
        [Parameter(Mandatory = $true, ParameterSetName = 'All')]
        [switch]$All,
        
        [Parameter(Mandatory = $false)]
        [string]$Destination,
        
        [Parameter(Mandatory = $false, ParameterSetName = 'Family')]
        [Parameter(Mandatory = $false, ParameterSetName = 'All')]
        [switch]$Parallel
    )
    
    begin {
        Initialize-DistroNexusLogger
        
        # 确定下载目录
        if (-not $Destination) {
            $Destination = Get-DistroCachePath
        }
        
        if (-not (Test-Path $Destination)) {
            New-Item -ItemType Directory -Path $Destination -Force | Out-Null
        }
        
        Write-DistroNexusLog "Download destination: $Destination" -FileOnly
    }
    
    process {
        # 获取要下载的包列表
        $packagesToDownload = @()
        
        switch ($PSCmdlet.ParameterSetName) {
            'Single' {
                $package = Get-DistroNexusPackage | Where-Object { $_.DefaultName -eq $Name }
                if (-not $package) {
                    throw "Package not found in catalog: $Name"
                }
                $packagesToDownload += $package
            }
            
            'Family' {
                $packagesToDownload = Get-DistroNexusPackage | Where-Object { $_.Family -like $Family }
                if ($packagesToDownload.Count -eq 0) {
                    throw "No packages found for family: $Family"
                }
                Write-Host "Found $($packagesToDownload.Count) package(s) in family '$Family'" -ForegroundColor Cyan
            }
            
            'All' {
                $packagesToDownload = Get-DistroNexusPackage
                Write-Host "Found $($packagesToDownload.Count) package(s) in catalog" -ForegroundColor Cyan
                
                if (-not $PSCmdlet.ShouldProcess("$($packagesToDownload.Count) packages", "Download")) {
                    return
                }
            }
        }
        
        # 过滤已缓存的包
        $toDownload = $packagesToDownload | Where-Object { -not $_.IsCached }
        $alreadyCached = $packagesToDownload.Count - $toDownload.Count
        
        if ($alreadyCached -gt 0) {
            Write-Host "$alreadyCached package(s) already cached, skipping." -ForegroundColor Yellow
        }
        
        if ($toDownload.Count -eq 0) {
            Write-Host "All packages already downloaded." -ForegroundColor Green
            return
        }
        
        Write-Host "Downloading $($toDownload.Count) package(s)..." -ForegroundColor Cyan
        
        # 下载包
        if ($Parallel -and $toDownload.Count -gt 1) {
            # 并行下载（实验性）
            $jobs = @()
            foreach ($pkg in $toDownload) {
                $job = Start-Job -ScriptBlock {
                    param($PkgName, $PkgUrl, $DestPath)
                    
                    $fileName = [System.IO.Path]::GetFileName($PkgUrl)
                    $targetFile = Join-Path $DestPath $fileName
                    
                    try {
                        Invoke-WebRequest -Uri $PkgUrl -OutFile $targetFile -UseBasicParsing
                        return @{ Success = $true; Package = $PkgName }
                    }
                    catch {
                        return @{ Success = $false; Package = $PkgName; Error = $_.Exception.Message }
                    }
                } -ArgumentList $pkg.DefaultName, $pkg.Url, $Destination
                
                $jobs += $job
            }
            
            # 等待所有任务完成
            $completed = 0
            while ($jobs | Where-Object { $_.State -eq 'Running' }) {
                $finishedJobs = $jobs | Where-Object { $_.State -ne 'Running' -and $_.HasMoreData }
                foreach ($job in $finishedJobs) {
                    $result = Receive-Job $job
                    $completed++
                    
                    $percent = [math]::Round(($completed / $toDownload.Count) * 100)
                    Write-Progress -Activity "Downloading packages" -Status "$completed of $($toDownload.Count)" -PercentComplete $percent
                    
                    if ($result.Success) {
                        Write-Host "  ✓ $($result.Package)" -ForegroundColor Green
                    }
                    else {
                        Write-Host "  ✗ $($result.Package): $($result.Error)" -ForegroundColor Red
                    }
                }
                Start-Sleep -Milliseconds 500
            }
            
            # 清理任务
            $jobs | Remove-Job -Force
            Write-Progress -Activity "Downloading packages" -Completed
        }
        else {
            # 串行下载（默认）
            $current = 0
            foreach ($pkg in $toDownload) {
                $current++
                $percent = [math]::Round(($current / $toDownload.Count) * 100)
                
                Write-Progress -Activity "Downloading packages" -Status "$current of $($toDownload.Count): $($pkg.Name)" -PercentComplete $percent
                
                try {
                    Save-SinglePackage -Package $pkg -Destination $Destination
                    Write-Host "  ✓ $($pkg.Name)" -ForegroundColor Green
                    Write-DistroNexusLog "Downloaded $($pkg.DefaultName)" -FileOnly
                }
                catch {
                    Write-Host "  ✗ $($pkg.Name): $_" -ForegroundColor Red
                    Write-DistroNexusLog "Failed to download $($pkg.DefaultName): $_" -Level ERROR
                }
            }
            Write-Progress -Activity "Downloading packages" -Completed
        }
        
        Write-Host "`nDownload completed. $($toDownload.Count) package(s) processed." -ForegroundColor Green
    }
}

function Save-SinglePackage {
    <#
    .SYNOPSIS
        下载单个包（内部辅助函数）
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Package,
        
        [Parameter(Mandatory = $true)]
        [string]$Destination
    )
    
    $fileName = [System.IO.Path]::GetFileName($Package.Url)
    $targetFile = Join-Path $Destination $fileName
    
    # 创建子目录（按族组织）
    $familyDir = Join-Path $Destination $Package.Family
    if (-not (Test-Path $familyDir)) {
        New-Item -ItemType Directory -Path $familyDir -Force | Out-Null
    }
    $targetFile = Join-Path $familyDir $fileName
    
    # 下载文件
    Invoke-WebRequest -Uri $Package.Url -OutFile $targetFile -UseBasicParsing
    
    # 更新配置中的 LocalPath（可选）
    # 这里可以调用 Update-PackageLocalPath 更新 distros.json
}
```

**使用示例**:

```powershell
# 下载单个包
Save-DistroNexusPackage -Name "Ubuntu-22.04"

# 下载整个族
Save-DistroNexusPackage -Family "Ubuntu"

# 下载所有包
Save-DistroNexusPackage -All

# 并行下载（快速）
Save-DistroNexusPackage -Family "Debian" -Parallel

# 自定义目录
Save-DistroNexusPackage -All -Destination "D:\WSLDistros"
```

---

### 2.2 进度显示改进 【高优先级】

#### 功能描述
旧版使用 .NET `HttpClient` 显示详细进度（百分比、MB、速度）。

#### 缺失原因
新版使用 `Invoke-WebRequest` 的默认进度显示，信息较少。

#### 影响分析
- **用户体验**: 无法看到详细下载进度
- **大文件下载**: 长时间无反馈

#### 实现方案

**增强 Save-SinglePackage 函数**:

```powershell
function Save-SinglePackage {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Package,
        
        [Parameter(Mandatory = $true)]
        [string]$Destination,
        
        [Parameter(Mandatory = $false)]
        [switch]$ShowDetailedProgress
    )
    
    $fileName = [System.IO.Path]::GetFileName($Package.Url)
    $familyDir = Join-Path $Destination $Package.Family
    
    if (-not (Test-Path $familyDir)) {
        New-Item -ItemType Directory -Path $familyDir -Force | Out-Null
    }
    
    $targetFile = Join-Path $familyDir $fileName
    
    if ($ShowDetailedProgress) {
        # 使用 .NET HttpClient 进行详细进度显示
        try {
            $httpClient = New-Object System.Net.Http.HttpClient
            $httpClient.Timeout = [System.TimeSpan]::FromMinutes(30)
            
            # 获取文件大小
            $response = $httpClient.GetAsync($Package.Url, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).Result
            $totalBytes = $response.Content.Headers.ContentLength
            
            if (-not $totalBytes) {
                # 回退到简单下载
                $httpClient.Dispose()
                Invoke-WebRequest -Uri $Package.Url -OutFile $targetFile -UseBasicParsing
                return
            }
            
            $stream = $response.Content.ReadAsStreamAsync().Result
            $fileStream = [System.IO.File]::Create($targetFile)
            
            $buffer = New-Object byte[] 8192
            $totalRead = 0
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            
            while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $fileStream.Write($buffer, 0, $read)
                $totalRead += $read
                
                # 更新进度（每 100KB 更新一次）
                if ($totalRead % 102400 -lt 8192) {
                    $percent = [math]::Round(($totalRead / $totalBytes) * 100, 1)
                    $downloadedMB = [math]::Round($totalRead / 1MB, 2)
                    $totalMB = [math]::Round($totalBytes / 1MB, 2)
                    $speedMBps = if ($sw.Elapsed.TotalSeconds -gt 0) {
                        [math]::Round($totalRead / 1MB / $sw.Elapsed.TotalSeconds, 2)
                    } else { 0 }
                    
                    $status = "$downloadedMB MB / $totalMB MB ($speedMBps MB/s)"
                    Write-Progress -Activity "Downloading $($Package.Name)" -Status $status -PercentComplete $percent
                }
            }
            
            $fileStream.Close()
            $stream.Close()
            $httpClient.Dispose()
            $sw.Stop()
            
            Write-Progress -Activity "Downloading $($Package.Name)" -Completed
            
            $totalMB = [math]::Round($totalBytes / 1MB, 2)
            $totalSeconds = $sw.Elapsed.TotalSeconds
            $avgSpeed = [math]::Round($totalBytes / 1MB / $totalSeconds, 2)
            Write-Verbose "Downloaded $totalMB MB in $([math]::Round($totalSeconds, 1))s (avg: $avgSpeed MB/s)"
        }
        catch {
            # 清理
            if ($fileStream) { $fileStream.Close() }
            if ($stream) { $stream.Close() }
            if ($httpClient) { $httpClient.Dispose() }
            
            # 删除不完整的文件
            if (Test-Path $targetFile) {
                Remove-Item $targetFile -Force
            }
            
            throw
        }
    }
    else {
        # 使用默认 Invoke-WebRequest
        Invoke-WebRequest -Uri $Package.Url -OutFile $targetFile -UseBasicParsing
    }
}
```

**使用示例**:

```powershell
# 详细进度显示
$package = Get-DistroNexusPackage -Name "Ubuntu-22.04"
Save-SinglePackage -Package $package -Destination "D:\WSL" -ShowDetailedProgress -Verbose
```

---

### 2.3 筛选参数增强 【中优先级】

#### 功能描述
旧版支持 `-SelectFamily` 和 `-SelectVersion` 参数灵活筛选。

#### 缺失原因
新版需要精确指定 `DefaultName`。

#### 影响分析
- **灵活性**: 筛选不便
- **用户体验**: 需要查阅目录获取准确名称

#### 实现方案

在 2.1 的批量下载中已通过 `-Family` 参数实现。进一步增强 `Get-DistroNexusPackage`:

```powershell
function Get-DistroNexusPackage {
    <#
    .SYNOPSIS
        Lists available distribution packages.
    
    .PARAMETER Name
        Filter by package name (supports wildcards)
    
    .PARAMETER Family
        Filter by distribution family
    
    .PARAMETER Cached
        Show only cached packages
    
    .PARAMETER Available
        Show only uncached (available for download) packages
    
    .EXAMPLE
        Get-DistroNexusPackage -Family "Ubuntu"
        # Lists all Ubuntu packages
    
    .EXAMPLE
        Get-DistroNexusPackage -Family "Debian" -Cached
        # Lists cached Debian packages
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param(
        [Parameter(Mandatory = $false, Position = 0)]
        [string]$Name,
        
        [Parameter(Mandatory = $false)]
        [string]$Family,
        
        [Parameter(Mandatory = $false)]
        [switch]$Cached,
        
        [Parameter(Mandatory = $false)]
        [switch]$Available
    )
    
    # ... 原有逻辑 ...
    
    # 应用筛选
    if ($Family) {
        $packages = $packages | Where-Object { $_.Family -like $Family }
    }
    
    if ($Cached) {
        $packages = $packages | Where-Object { $_.IsCached }
    }
    
    if ($Available) {
        $packages = $packages | Where-Object { -not $_.IsCached }
    }
    
    return $packages
}
```

---

### 2.4 配置备份机制 【高优先级】

#### 功能描述
旧版 `update_distros.ps1` 更新前自动备份 `distros.json` 为 `.timestamp.bak`。

#### 缺失原因
新版未实现备份逻辑。

#### 影响分析
- **安全性**: 更新失败可能导致配置丢失
- **恢复能力**: 无法回滚到旧版本

#### 实现方案

**修改文件**: `src/PowerShell/Public/Update-DistroNexusCatalog.ps1`

```powershell
function Update-DistroNexusCatalog {
    <#
    .SYNOPSIS
        Refreshes the distribution catalog from online source.

    .DESCRIPTION
        Downloads the latest distribution catalog and updates local configuration.
        Automatically backs up existing catalog before updating.

    .PARAMETER SourceUrl
        Custom catalog URL. Defaults to official DistroNexus catalog.
    
    .PARAMETER NoBackup
        Skip backup of existing catalog
    
    .PARAMETER PreserveLocalPath
        Preserve LocalPath values from existing catalog

    .EXAMPLE
        Update-DistroNexusCatalog
        # Updates catalog with backup

    .EXAMPLE
        Update-DistroNexusCatalog -PreserveLocalPath
        # Updates while keeping local paths
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    [OutputType([void])]
    param(
        [Parameter(Mandatory = $false)]
        [string]$SourceUrl,
        
        [Parameter(Mandatory = $false)]
        [switch]$NoBackup,
        
        [Parameter(Mandatory = $false)]
        [switch]$PreserveLocalPath
    )
    
    begin {
        Initialize-DistroNexusLogger
    }
    
    process {
        # 确定目录文件路径
        $catalogFile = Get-DistroNexusConfig -Key "DistroCatalogFile"
        if (-not $catalogFile) {
            $catalogFile = Join-Path $script:ModuleRoot "..\config\distros.json"
        }
        
        $catalogDir = Split-Path -Parent $catalogFile
        
        # 默认源 URL
        if (-not $SourceUrl) {
            $SourceUrl = "https://raw.githubusercontent.com/LazyWorkshop-create/DistroNexus/main/config/distros.json"
        }
        
        Write-Host "Updating distribution catalog from: $SourceUrl" -ForegroundColor Cyan
        Write-DistroNexusLog "Updating catalog from $SourceUrl"
        
        # 备份现有目录
        if ((Test-Path $catalogFile) -and -not $NoBackup) {
            try {
                $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
                $backupFile = "$catalogFile.$timestamp.bak"
                
                Copy-Item -Path $catalogFile -Destination $backupFile -Force
                Write-Host "  Backup created: $([System.IO.Path]::GetFileName($backupFile))" -ForegroundColor Green
                Write-DistroNexusLog "Backed up catalog to $backupFile" -FileOnly
                
                # 保留最近 5 个备份
                $backups = Get-ChildItem -Path $catalogDir -Filter "distros.json.*.bak" |
                    Sort-Object CreationTime -Descending
                
                if ($backups.Count -gt 5) {
                    $backups | Select-Object -Skip 5 | ForEach-Object {
                        Remove-Item $_.FullName -Force
                        Write-Verbose "Removed old backup: $($_.Name)"
                    }
                }
            }
            catch {
                Write-Warning "Failed to create backup: $_"
            }
        }
        
        # 读取现有目录（如果需要保留 LocalPath）
        $existingCatalog = $null
        if ($PreserveLocalPath -and (Test-Path $catalogFile)) {
            try {
                $existingCatalog = Get-Content -Path $catalogFile -Raw | ConvertFrom-Json
            }
            catch {
                Write-Warning "Failed to read existing catalog: $_"
            }
        }
        
        # 下载新目录
        try {
            Write-Host "  Downloading catalog..." -ForegroundColor Yellow
            $newCatalogJson = Invoke-RestMethod -Uri $SourceUrl -UseBasicParsing -ErrorAction Stop
            
            # 如果需要保留 LocalPath
            if ($PreserveLocalPath -and $existingCatalog) {
                Write-Host "  Preserving local paths..." -ForegroundColor Yellow
                
                foreach ($famKey in $newCatalogJson.PSObject.Properties.Name) {
                    $newFamily = $newCatalogJson.$famKey
                    $existingFamily = $existingCatalog.$famKey
                    
                    if ($existingFamily) {
                        foreach ($verKey in $newFamily.Versions.PSObject.Properties.Name) {
                            $newVersion = $newFamily.Versions.$verKey
                            $existingVersion = $existingFamily.Versions.$verKey
                            
                            if ($existingVersion -and $existingVersion.LocalPath) {
                                # 保留旧的 LocalPath
                                $newVersion | Add-Member -NotePropertyName 'LocalPath' -NotePropertyValue $existingVersion.LocalPath -Force
                            }
                        }
                    }
                }
            }
            
            # 保存新目录
            if (-not (Test-Path $catalogDir)) {
                New-Item -ItemType Directory -Path $catalogDir -Force | Out-Null
            }
            
            $json = $newCatalogJson | ConvertTo-Json -Depth 10 -Compress:$false
            Set-Content -Path $catalogFile -Value $json -Force -ErrorAction Stop
            
            Write-Host "Catalog updated successfully." -ForegroundColor Green
            Write-DistroNexusLog "Catalog updated successfully"
            
            # 显示统计
            $familyCount = $newCatalogJson.PSObject.Properties.Count
            $totalVersions = 0
            foreach ($fam in $newCatalogJson.PSObject.Properties.Value) {
                $totalVersions += $fam.Versions.PSObject.Properties.Count
            }
            
            Write-Host "  Families: $familyCount" -ForegroundColor Gray
            Write-Host "  Total versions: $totalVersions" -ForegroundColor Gray
        }
        catch {
            Write-Error "Failed to update catalog: $_"
            Write-DistroNexusLog "Failed to update catalog: $_" -Level ERROR
            
            # 尝试从备份恢复
            if ((Test-Path "$catalogFile.$timestamp.bak") -and -not $NoBackup) {
                Write-Host "Attempting to restore from backup..." -ForegroundColor Yellow
                try {
                    Copy-Item -Path "$catalogFile.$timestamp.bak" -Destination $catalogFile -Force
                    Write-Host "Restored from backup." -ForegroundColor Green
                }
                catch {
                    Write-Warning "Failed to restore from backup: $_"
                }
            }
            
            throw
        }
    }
}
```

**使用示例**:

```powershell
# 更新并自动备份
Update-DistroNexusCatalog

# 更新并保留本地路径
Update-DistroNexusCatalog -PreserveLocalPath

# 不备份（不推荐）
Update-DistroNexusCatalog -NoBackup

# 从自定义源更新
Update-DistroNexusCatalog -SourceUrl "https://example.com/custom-catalog.json"
```

---

### 2.5 LocalPath 保留逻辑 【高优先级】

#### 功能描述
旧版更新目录时会保留现有的 `LocalPath` 字段，避免丢失下载信息。

#### 缺失原因
新版直接覆盖整个目录文件。

#### 影响分析
- **数据丢失**: 已下载包的路径信息丢失
- **重复下载**: 可能重新下载已有的包

#### 实现方案

已在 2.4 中通过 `-PreserveLocalPath` 参数实现。

---

### 2.6 .NET HttpClient 支持 【中优先级】

#### 功能描述
旧版使用 .NET `HttpClient` 进行精细的下载控制。

#### 缺失原因
新版使用 PowerShell 内置 `Invoke-WebRequest`。

#### 影响分析
- **控制能力**: 难以实现断点续传、自定义超时等高级功能
- **性能**: 某些场景下 HttpClient 性能更好

#### 实现方案

已在 2.2 进度显示改进中实现了 HttpClient 支持。可进一步扩展：

```powershell
function Save-PackageWithHttpClient {
    <#
    .SYNOPSIS
        使用 .NET HttpClient 下载包（高级功能）
    
    .PARAMETER Url
        下载 URL
    
    .PARAMETER Destination
        目标文件路径
    
    .PARAMETER Timeout
        超时时间（秒）
    
    .PARAMETER BufferSize
        缓冲区大小（字节）
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,
        
        [Parameter(Mandatory = $true)]
        [string]$Destination,
        
        [Parameter(Mandatory = $false)]
        [int]$Timeout = 1800,  # 30 minutes
        
        [Parameter(Mandatory = $false)]
        [int]$BufferSize = 8192
    )
    
    $httpClient = $null
    $stream = $null
    $fileStream = $null
    
    try {
        # 创建 HttpClient
        $httpClient = New-Object System.Net.Http.HttpClient
        $httpClient.Timeout = [System.TimeSpan]::FromSeconds($Timeout)
        
        # 发送请求
        $response = $httpClient.GetAsync($Url, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).Result
        
        if (-not $response.IsSuccessStatusCode) {
            throw "HTTP error: $($response.StatusCode) - $($response.ReasonPhrase)"
        }
        
        $totalBytes = $response.Content.Headers.ContentLength
        $stream = $response.Content.ReadAsStreamAsync().Result
        $fileStream = [System.IO.File]::Create($Destination)
        
        $buffer = New-Object byte[] $BufferSize
        $totalRead = 0
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        
        while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $fileStream.Write($buffer, 0, $read)
            $totalRead += $read
            
            # 发布进度事件（可被外部捕获）
            $percent = if ($totalBytes) { ($totalRead / $totalBytes) * 100 } else { 0 }
            $progressArgs = @{
                BytesRead = $totalRead
                TotalBytes = $totalBytes
                PercentComplete = $percent
                ElapsedTime = $sw.Elapsed
            }
            
            # 触发事件（如果实现了事件系统）
            # Invoke-DownloadProgressEvent $progressArgs
        }
        
        $sw.Stop()
        
        return @{
            Success = $true
            BytesDownloaded = $totalRead
            ElapsedSeconds = $sw.Elapsed.TotalSeconds
            AverageSpeedMBps = ($totalRead / 1MB) / $sw.Elapsed.TotalSeconds
        }
    }
    catch {
        throw "Download failed: $_"
    }
    finally {
        if ($fileStream) { $fileStream.Close() }
        if ($stream) { $stream.Close() }
        if ($httpClient) { $httpClient.Dispose() }
    }
}
```

---

## 第三部分：用户管理功能缺失项

### 3.1 wsl.conf 处理增强 【中优先级】

#### 功能描述
旧版 `set_credentials.ps1` 会检测并正确处理 `wsl.conf` 文件，支持追加和覆盖。

#### 缺失原因
新版 `Set-DistroNexusCredential` 不处理 `wsl.conf`。

#### 影响分析
- **用户体验**: 需要手动配置 wsl.conf
- **持久性**: 用户配置可能在重启后失效

#### 实现方案

已在第一部分 1.10 的 `Set-DistroDefaultUser` 函数中实现了完整的 `wsl.conf` 处理逻辑。

进一步增强 `Set-DistroNexusCredential`:

```powershell
function Set-DistroNexusCredential {
    <#
    .SYNOPSIS
        Sets or updates credentials for a WSL instance.

    .DESCRIPTION
        Sets the default user and optionally password for a WSL instance.
        Automatically configures /etc/wsl.conf and registry settings.

    .PARAMETER Name
        Instance name

    .PARAMETER Username
        Username to set as default

    .PARAMETER Password
        New password for the user (SecureString)
    
    .PARAMETER UpdateWslConf
        Update /etc/wsl.conf with default user setting (default: true)

    .EXAMPLE
        $pwd = Read-Host -AsSecureString
        Set-DistroNexusCredential -Name "Ubuntu" -Username "dev" -Password $pwd
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipelineByPropertyName = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true, Position = 1)]
        [string]$Username,

        [Parameter(Mandatory = $false)]
        [SecureString]$Password,
        
        [Parameter(Mandatory = $false)]
        [bool]$UpdateWslConf = $true
    )
    
    process {
        if ($PSCmdlet.ShouldProcess($Name, "Set credentials for user '$Username'")) {
            try {
                # 调用完整的用户配置函数
                if ($Password) {
                    Set-DistroDefaultUser -DistroName $Name -Username $Username -Password $Password
                }
                else {
                    # 仅更新默认用户，不更改密码
                    $uid = wsl -d $Name -e id -u $Username 2>&1
                    
                    if ($LASTEXITCODE -ne 0) {
                        throw "User '$Username' not found in instance '$Name'"
                    }
                    
                    # 更新注册表
                    $lxssPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss"
                    $keys = Get-ChildItem -Path $lxssPath
                    
                    foreach ($key in $keys) {
                        $props = Get-ItemProperty -Path $key.PSPath
                        if ($props.DistributionName -eq $Name) {
                            Set-ItemProperty -Path $key.PSPath -Name "DefaultUid" -Value ([int]$uid)
                            Write-Host "Default user set to: $Username" -ForegroundColor Green
                            break
                        }
                    }
                    
                    # 更新 wsl.conf
                    if ($UpdateWslConf) {
                        $hasUserSection = wsl -d $Name -e bash -c "grep -q '^\[user\]' /etc/wsl.conf && echo yes" 2>$null
                        
                        if ($hasUserSection -eq "yes") {
                            wsl -d $Name -e bash -c "sed -i '/^\[user\]/,/^\[/s/^default=.*/default=$Username/' /etc/wsl.conf"
                        }
                        else {
                            wsl -d $Name -e bash -c "echo -e '\n[user]\ndefault=$Username' | sudo tee -a /etc/wsl.conf > /dev/null"
                        }
                        
                        Write-Host "wsl.conf updated." -ForegroundColor Green
                    }
                }
                
                Write-DistroNexusLog "Set credentials for $Name : user=$Username"
            }
            catch {
                Write-Error "Failed to set credentials: $_"
                Write-DistroNexusLog "Failed to set credentials for $Name : $_" -Level ERROR
                throw
            }
        }
    }
}
```

---

### 3.2 wheel 组支持 【高优先级】

#### 功能描述
旧版会检测并同时添加用户到 `sudo` 和 `wheel` 组（兼容不同发行版）。

#### 缺失原因
新版只添加到 `sudo` 组。

#### 影响分析
- **兼容性**: 某些发行版（如 CentOS、Fedora）使用 `wheel` 组
- **权限问题**: 用户可能无法执行 sudo 命令

#### 实现方案

已在第一部分 1.10 的 `Set-DistroDefaultUser` 函数中实现。关键代码：

```powershell
# 添加到 sudo 组
wsl -d $DistroName -e bash -c "usermod -aG sudo '$Username'" 2>&1 | Out-Null

# 尝试添加到 wheel 组（某些发行版使用 wheel 而不是 sudo）
wsl -d $DistroName -e bash -c "groups wheel" 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    wsl -d $DistroName -e bash -c "usermod -aG wheel '$Username'" 2>&1 | Out-Null
    Write-Verbose "Added user to wheel group"
}
```

**完整兼容性检测**:

```powershell
function Add-UserToSudoGroup {
    <#
    .SYNOPSIS
        添加用户到 sudo/wheel 组（兼容多发行版）
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DistroName,
        
        [Parameter(Mandatory = $true)]
        [string]$Username
    )
    
    # 检测系统使用的 sudo 组
    $sudoGroup = $null
    
    # 检查 sudo 组
    $hasSudo = wsl -d $DistroName -e bash -c "getent group sudo" 2>$null
    if ($hasSudo -and $LASTEXITCODE -eq 0) {
        $sudoGroup = "sudo"
    }
    
    # 检查 wheel 组
    $hasWheel = wsl -d $DistroName -e bash -c "getent group wheel" 2>$null
    if ($hasWheel -and $LASTEXITCODE -eq 0) {
        if ($sudoGroup) {
            # 两个组都存在，都添加
            wsl -d $DistroName -e bash -c "usermod -aG $sudoGroup,'wheel' '$Username'"
            Write-Verbose "Added user to both sudo and wheel groups"
        }
        else {
            $sudoGroup = "wheel"
        }
    }
    
    if (-not $sudoGroup) {
        Write-Warning "Neither sudo nor wheel group found. User may not have administrative privileges."
        return
    }
    
    # 添加用户到组
    wsl -d $DistroName -e bash -c "usermod -aG $sudoGroup '$Username'" 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Added to $sudoGroup group" -ForegroundColor Green
    }
    else {
        Write-Warning "Failed to add user to $sudoGroup group"
    }
    
    # 验证组成员
    $groups = wsl -d $DistroName -e bash -c "groups '$Username'" 2>$null
    if ($groups) {
        Write-Verbose "User groups: $groups"
    }
}
```

---

## 第四部分：其他缺失功能

### 4.1 独立扫描命令 【低优先级】

#### 功能描述
旧版有独立的 `scan_wsl_instances.ps1` 脚本用于扫描并更新 `instances.json`。

#### 缺失原因
新版功能集成到 `Get-DistroNexusInstance`。

#### 影响分析
- **功能分散**: 没有显式的"扫描"命令
- **用户习惯**: 旧用户可能习惯使用独立扫描命令

#### 实现方案

**方案 1: 创建别名**

```powershell
# 在模块清单 DistroNexus.psd1 中添加
FunctionsToExport = @(
    'Get-DistroNexusInstance',
    # ... 其他函数 ...
    'Sync-DistroNexusCache'  # 新增
)

AliasesToExport = @(
    'Scan-DistroNexusInstances'  # 指向 Sync-DistroNexusCache
)
```

```powershell
# 在 DistroNexus.psm1 中添加
function Sync-DistroNexusCache {
    <#
    .SYNOPSIS
        Synchronizes instance cache with system state.
    
    .DESCRIPTION
        Scans all WSL instances and updates the local cache.
        This is an explicit cache refresh operation.
    
    .PARAMETER IncludeExtendedInfo
        Include Release and User information (slower)
    
    .EXAMPLE
        Sync-DistroNexusCache
        # Refreshes cache
    
    .EXAMPLE
        Sync-DistroNexusCache -IncludeExtendedInfo
        # Refreshes with extended info
    #>
    [CmdletBinding()]
    [Alias('Scan-DistroNexusInstances')]
    param(
        [Parameter(Mandatory = $false)]
        [switch]$IncludeExtendedInfo
    )
    
    Write-Host "Scanning WSL instances..." -ForegroundColor Cyan
    
    $params = @{
        ForceUpdate = $true
    }
    
    if ($IncludeExtendedInfo) {
        $params.IncludeRelease = $true
        $params.IncludeUser = $true
    }
    
    $instances = Get-DistroNexusInstance @params
    
    Write-Host "Scan completed. Found $($instances.Count) instance(s)." -ForegroundColor Green
    
    # 显示摘要
    if ($instances) {
        $running = ($instances | Where-Object { $_.State -eq 'Running' }).Count
        $stopped = ($instances | Where-Object { $_.State -eq 'Stopped' }).Count
        
        Write-Host "  Running: $running" -ForegroundColor Gray
        Write-Host "  Stopped: $stopped" -ForegroundColor Gray
    }
    
    return $instances
}
```

**使用示例**:

```powershell
# 刷新缓存
Sync-DistroNexusCache

# 或使用别名
Scan-DistroNexusInstances

# 包含扩展信息
Sync-DistroNexusCache -IncludeExtendedInfo
```

---

## 总结与实施建议

### 实施优先级

**第一阶段（核心功能，1-2周）**:
1. ✅ 缓存机制（1.1）
2. ✅ 强制更新参数（1.2）
3. ✅ 非空目录检查（1.12）
4. ✅ 配置备份机制（2.4）
5. ✅ LocalPath 保留（2.5）
6. ✅ wheel 组支持（3.2）
7. ✅ 批量下载（2.1）
8. ✅ 进度显示改进（2.2）

**第二阶段（用户体验增强，2-3周）**:
1. ✅ 包类型自动处理（1.8）
2. ✅ 完整用户配置（1.10）
3. ✅ 启动模式增强（1.15）
4. ✅ 用户恢复功能（1.13）
5. ✅ 自动下载功能（1.7）
6. ✅ 列表模式集成（1.6）
7. ✅ wsl.conf 处理（3.1）

**第三阶段（高级特性，3-4周）**:
1. ⚠️ 交互式安装模式（1.4）
2. ⚠️ 快速安装模式（1.5）
3. ⚠️ 交互式卸载（1.11）
4. ⚠️ Release/User 信息查询（1.3）
5. ⚠️ 路径处理增强（1.14）
6. ⚠️ 独立扫描命令（4.1）

### 风险评估

**低风险**:
- 缓存机制（可选功能）
- 配置备份（只读操作）
- 进度显示（UI改进）

**中风险**:
- 包类型处理（复杂解压逻辑）
- 用户配置（修改系统文件）
- wsl.conf 处理（需仔细测试）

**高风险**:
- 交互式模式（改变命令行为）
- Release/User 查询（会启动实例）
- 自动下载（网络依赖）

### 测试建议

1. **单元测试**: 为关键函数编写 Pester 测试
2. **集成测试**: 在多个 WSL 发行版上测试
3. **性能测试**: 测试缓存机制的性能提升
4. **兼容性测试**: 确保与旧版数据格式兼容
5. **回归测试**: 确保不破坏现有功能

### 文档更新

1. 更新 `PowerShell-Module.md` 添加新参数说明
2. 创建迁移指南 `Migration-Guide.md`
3. 添加故障排除章节
4. 更新示例脚本

---

**文档版本**: 1.0
**最后更新**: 2026-01-29
**相关文档**: PowerShell-Module-Missing-Features-Implementation.md
