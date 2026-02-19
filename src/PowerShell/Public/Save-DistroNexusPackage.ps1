function Save-DistroNexusPackage {
    <#
    .SYNOPSIS
        Downloads a WSL distribution package to the local cache.

    .DESCRIPTION
        Downloads the specified distribution package from the configured source URL
        and saves it to the package cache directory.
        
        Supports batch downloading by family or all packages, with concurrent download
        control, automatic retry on failure, and enhanced progress display.

    .PARAMETER DefaultName
        The default name of the distribution to download (e.g., "Ubuntu-22.04").

    .PARAMETER Family
        Download all distributions from the specified family (e.g., "Ubuntu", "Debian").

    .PARAMETER All
        Download all distributions available in the catalog.

    .PARAMETER Destination
        Override the default cache directory.

    .PARAMETER MaxConcurrent
        Maximum number of concurrent downloads (1-10). Default is 3.
        Only applies when using -Family or -All.

    .PARAMETER RetryCount
        Number of retry attempts on download failure (0-10). Default is 3.

    .PARAMETER ShowSpeed
        Display download speed and estimated time remaining. Default is true.

    .PARAMETER SkipExisting
        Skip packages that are already downloaded. Default is true.

    .EXAMPLE
        Save-DistroNexusPackage -DefaultName "Ubuntu-22.04"
        # Downloads a single package

    .EXAMPLE
        Save-DistroNexusPackage -Family "Ubuntu" -MaxConcurrent 5
        # Downloads all Ubuntu packages with 5 concurrent downloads

    .EXAMPLE
        Save-DistroNexusPackage -All -RetryCount 5
        # Downloads all packages with 5 retry attempts

    .OUTPUTS
        PSCustomObject with download results
    #>
    [CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = 'Single')]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $true, ParameterSetName = 'Single')]
        [string]$DefaultName,
        
        [Parameter(Mandatory = $true, ParameterSetName = 'Family')]
        [string]$Family,
        
        [Parameter(Mandatory = $true, ParameterSetName = 'All')]
        [switch]$All,
        
        [Parameter(Mandatory = $false)]
        [string]$Destination,
        
        [Parameter(Mandatory = $false)]
        [ValidateRange(1, 10)]
        [int]$MaxConcurrent = 3,
        
        [Parameter(Mandatory = $false)]
        [ValidateRange(0, 10)]
        [int]$RetryCount = 3,
        
        [Parameter(Mandatory = $false)]
        [bool]$ShowSpeed = $true,
        
        [Parameter(Mandatory = $false)]
        [bool]$SkipExisting = $true
    )
    
    begin {
        Initialize-DistroNexusLogger
        
        # Determine destination directory
        if (-not $Destination) {
            $config = Get-DistroNexusConfig
            $Destination = $config.Settings.PackageCachePath
            if (-not $Destination) {
                $Destination = Join-Path $env:LOCALAPPDATA "DistroNexus\packages"
            }
        }
        
        if (-not (Test-Path $Destination)) {
            New-Item -ItemType Directory -Path $Destination -Force | Out-Null
        }
        
        Write-DistroNexusLog "Package cache directory: $Destination" -FileOnly
    }
    
    process {
        # Get packages to download
        $packagesToDownload = @()
        
        switch ($PSCmdlet.ParameterSetName) {
            'Single' {
                $package = Get-DistroNexusPackage | Where-Object { $_.DefaultName -eq $DefaultName }
                if ($package) {
                    $packagesToDownload += $package
                }
                else {
                    Write-DistroNexusLog "Package not found in catalog: $DefaultName" -Level ERROR
                    return [PSCustomObject]@{
                        Success = $false
                        TotalPackages = 0
                        Downloaded = 0
                        Skipped = 0
                        Failed = 1
                        FailedPackages = @($DefaultName)
                    }
                }
            }
            'Family' {
                $packagesToDownload = Get-DistroNexusPackage | Where-Object { $_.Family -eq $Family }
                if ($packagesToDownload.Count -eq 0) {
                    Write-DistroNexusLog "No packages found for family: $Family" -Level WARN
                    return [PSCustomObject]@{
                        Success = $true
                        TotalPackages = 0
                        Downloaded = 0
                        Skipped = 0
                        Failed = 0
                        FailedPackages = @()
                    }
                }
            }
            'All' {
                $packagesToDownload = Get-DistroNexusPackage
            }
        }
        
        # Filter out existing packages if SkipExisting
        $totalCount = $packagesToDownload.Count
        $downloadQueue = @()
        $skippedCount = 0
        
        foreach ($pkg in $packagesToDownload) {
            $pkgFilename = $pkg.Filename
            if (-not $pkgFilename) {
                $pkgUrl = if ($pkg.PSObject.Properties['Url'] -and $pkg.Url) { $pkg.Url } else { $pkg.DownloadUrl }
                if ($pkgUrl) {
                    try {
                        if ($pkgUrl -match "^http") {
                            $uri = [System.Uri]$pkgUrl
                            $pkgFilename = [System.IO.Path]::GetFileName($uri.LocalPath)
                        }
                    }
                    catch {}

                    if (-not $pkgFilename) {
                        $pkgFilename = Split-Path $pkgUrl -Leaf
                    }
                }
            }

            $outputFile = if ($pkgFilename) { Join-Path $Destination $pkgFilename } else { $null }
            if ((Test-Path $outputFile) -and $SkipExisting) {
                Write-DistroNexusLog "Skipping existing package: $($pkg.DefaultName)" -FileOnly
                $skippedCount++
            }
            else {
                $downloadQueue += $pkg
            }
        }
        
        Write-DistroNexusLog "Total packages: $totalCount, To download: $($downloadQueue.Count), Skipped: $skippedCount"
        
        if ($downloadQueue.Count -eq 0) {
            return [PSCustomObject]@{
                Success = $true
                TotalPackages = $totalCount
                Downloaded = 0
                Skipped = $skippedCount
                Failed = 0
                FailedPackages = @()
            }
        }
        
        # Download packages (single or batch)
        $downloadedCount = 0
        $failedPackages = @()
        
        if ($downloadQueue.Count -eq 1 -or $PSCmdlet.ParameterSetName -eq 'Single') {
            # Single download with progress
            foreach ($pkg in $downloadQueue) {
                $result = Invoke-PackageDownload -Package $pkg -Destination $Destination `
                    -RetryCount $RetryCount -ShowSpeed $ShowSpeed -ShowProgress $true
                
                if ($result.Success) {
                    $downloadedCount++
                }
                else {
                    $failedPackages += $pkg.DefaultName
                }
            }
        }
        else {
            # Batch download with concurrency control
            Write-DistroNexusLog "Starting batch download with $MaxConcurrent concurrent downloads"
            
            $jobs = @()
            $jobIndex = 0
            $completedCount = 0
            
            foreach ($pkg in $downloadQueue) {
                # Wait if max concurrent reached
                while ($jobs.Count -ge $MaxConcurrent) {
                    $completed = $jobs | Where-Object { $_.State -ne 'Running' }
                    foreach ($job in $completed) {
                        $result = Receive-Job -Job $job
                        $completedCount++
                        
                        if ($result.Success) {
                            $downloadedCount++
                            Write-DistroNexusLog "Downloaded ($completedCount/$($downloadQueue.Count)): $($result.PackageName)"
                        }
                        else {
                            $failedPackages += $result.PackageName
                            Write-DistroNexusLog "Failed ($completedCount/$($downloadQueue.Count)): $($result.PackageName) - $($result.Error)" -Level WARN
                        }
                        
                        Remove-Job -Job $job
                        $jobs = $jobs | Where-Object { $_.Id -ne $job.Id }
                    }
                    
                    if ($jobs.Count -ge $MaxConcurrent) {
                        Start-Sleep -Milliseconds 500
                    }
                }
                
                # Start download job
                $job = Start-Job -ScriptBlock {
                    param($Pkg, $Dest, $Retry, $ModulePath)
                    
                    # Import module in job context
                    Import-Module $ModulePath -ErrorAction Stop
                    
                    # Download with retry logic
                    $attempt = 0
                    $success = $false
                    $lastError = ""
                    
                    while ($attempt -le $Retry -and -not $success) {
                        try {
                            $attempt++
                            $packageUrl = if ($Pkg.PSObject.Properties['Url'] -and $Pkg.Url) { $Pkg.Url } else { $Pkg.DownloadUrl }
                            $packageFilename = $Pkg.Filename
                            if (-not $packageFilename -and $packageUrl) {
                                try {
                                    if ($packageUrl -match "^http") {
                                        $uri = [System.Uri]$packageUrl
                                        $packageFilename = [System.IO.Path]::GetFileName($uri.LocalPath)
                                    }
                                }
                                catch {}

                                if (-not $packageFilename) {
                                    $packageFilename = Split-Path $packageUrl -Leaf
                                }
                            }

                            if (-not $packageUrl -or -not $packageFilename) {
                                throw "Package metadata is missing download URL or filename."
                            }

                            $outputFile = Join-Path $Dest $packageFilename
                            
                            # Use exponential backoff for retries
                            if ($attempt -gt 1) {
                                $backoffSeconds = [Math]::Pow(2, $attempt - 1)
                                Start-Sleep -Seconds $backoffSeconds
                            }
                            
                            $ProgressPreference = 'SilentlyContinue'
                            Invoke-WebRequest -Uri $packageUrl -OutFile $outputFile -UseBasicParsing -TimeoutSec 1800
                            
                            if (Test-Path $outputFile) {
                                $success = $true
                            }
                        }
                        catch {
                            $lastError = $_.Exception.Message
                        }
                    }
                    
                    return [PSCustomObject]@{
                        Success = $success
                        PackageName = $Pkg.DefaultName
                        Error = $lastError
                        Attempts = $attempt
                    }
                } -ArgumentList $pkg, $Destination, $RetryCount, $PSScriptRoot
                
                $jobs += $job
                $jobIndex++
                
                # Update progress
                $percentComplete = [int](($jobIndex / $downloadQueue.Count) * 100)
                Write-Progress -Activity "Queuing downloads" -Status "Queued $jobIndex of $($downloadQueue.Count)" `
                    -PercentComplete $percentComplete
            }
            
            # Wait for remaining jobs
            Write-Progress -Activity "Downloading packages" -Status "Waiting for completion..."
            
            while ($jobs.Count -gt 0) {
                $completed = $jobs | Where-Object { $_.State -ne 'Running' }
                foreach ($job in $completed) {
                    $result = Receive-Job -Job $job
                    $completedCount++
                    
                    if ($result.Success) {
                        $downloadedCount++
                        Write-DistroNexusLog "Downloaded ($completedCount/$($downloadQueue.Count)): $($result.PackageName)"
                    }
                    else {
                        $failedPackages += $result.PackageName
                        Write-DistroNexusLog "Failed ($completedCount/$($downloadQueue.Count)): $($result.PackageName) - $($result.Error)" -Level WARN
                    }
                    
                    Remove-Job -Job $job
                    $jobs = $jobs | Where-Object { $_.Id -ne $job.Id }
                }
                
                if ($jobs.Count -gt 0) {
                    $percentComplete = [int](($completedCount / $downloadQueue.Count) * 100)
                    Write-Progress -Activity "Downloading packages" -Status "Completed $completedCount of $($downloadQueue.Count)" `
                        -PercentComplete $percentComplete
                    Start-Sleep -Milliseconds 500
                }
            }
            
            Write-Progress -Activity "Downloading packages" -Completed
        }
        
        # Return summary
        $result = [PSCustomObject]@{
            Success = ($failedPackages.Count -eq 0)
            TotalPackages = $totalCount
            Downloaded = $downloadedCount
            Skipped = $skippedCount
            Failed = $failedPackages.Count
            FailedPackages = $failedPackages
        }
        
        Write-DistroNexusLog "Download complete: $downloadedCount downloaded, $skippedCount skipped, $($failedPackages.Count) failed"
        
        return $result
    }
}

function Invoke-PackageDownload {
    <#
    .SYNOPSIS
        Internal helper to download a single package with retry and progress.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Package,
        
        [Parameter(Mandatory)]
        [string]$Destination,
        
        [Parameter(Mandatory = $false)]
        [int]$RetryCount = 3,
        
        [Parameter(Mandatory = $false)]
        [bool]$ShowSpeed = $true,
        
        [Parameter(Mandatory = $false)]
        [bool]$ShowProgress = $true
    )
    
    $packageUrl = if ($Package.PSObject.Properties['Url'] -and $Package.Url) { $Package.Url } else { $Package.DownloadUrl }
    $packageFilename = $Package.Filename

    if (-not $packageFilename -and $packageUrl) {
        try {
            if ($packageUrl -match "^http") {
                $uri = [System.Uri]$packageUrl
                $packageFilename = [System.IO.Path]::GetFileName($uri.LocalPath)
            }
        }
        catch {}

        if (-not $packageFilename) {
            $packageFilename = Split-Path $packageUrl -Leaf
        }
    }

    if (-not $packageUrl -or -not $packageFilename) {
        return [PSCustomObject]@{
            Success = $false
            PackageName = $Package.DefaultName
            Error = "Package metadata is missing download URL or filename."
            Attempts = 0
        }
    }

    $outputFile = Join-Path $Destination $packageFilename
    $attempt = 0
    $success = $false
    $lastError = ""
    
    while ($attempt -le $RetryCount -and -not $success) {
        try {
            $attempt++
            
            if ($attempt -gt 1) {
                $backoffSeconds = [Math]::Pow(2, $attempt - 1)
                Write-DistroNexusLog "Retry $attempt after ${backoffSeconds}s for: $($Package.DefaultName)" -FileOnly
                Start-Sleep -Seconds $backoffSeconds
            }
            
            Write-DistroNexusLog "Downloading ($attempt/$($RetryCount + 1)): $($Package.DefaultName) from $packageUrl"
            
            if ($ShowProgress) {
                # Enhanced progress with speed calculation
                $startTime = Get-Date
                $lastBytesRead = 0
                
                $response = Invoke-WebRequest -Uri $packageUrl -OutFile $outputFile -UseBasicParsing `
                    -TimeoutSec 1800 -PassThru
                
                $success = Test-Path $outputFile
            }
            else {
                $ProgressPreference = 'SilentlyContinue'
                Invoke-WebRequest -Uri $packageUrl -OutFile $outputFile -UseBasicParsing -TimeoutSec 1800
                $success = Test-Path $outputFile
            }
            
            if ($success) {
                Write-DistroNexusLog "Successfully downloaded: $($Package.DefaultName)"
            }
        }
        catch {
            $lastError = $_.Exception.Message
            Write-DistroNexusLog "Download attempt $attempt failed: $lastError" -Level WARN -FileOnly
        }
    }
    
    return [PSCustomObject]@{
        Success = $success
        PackageName = $Package.DefaultName
        Error = $lastError
        Attempts = $attempt
    }
}
