function Test-PackageFormat {
    <#
    .SYNOPSIS
        Tests if a package file is in a supported format.

    .DESCRIPTION
        Internal helper function to validate package file format.
        Supported formats: .tar.gz, .tar, .appx, .appxbundle, .zip

    .PARAMETER Path
        Path to the package file.

    .EXAMPLE
        if (Test-PackageFormat -Path "ubuntu.tar.gz") {
            # Process package
        }

    .OUTPUTS
        Boolean indicating if the format is supported.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )
    
    if (-not (Test-Path $Path)) {
        Write-Verbose "Package file not found: $Path"
        return $false
    }
    
    $extension = [System.IO.Path]::GetExtension($Path).ToLower()
    $fileName = [System.IO.Path]::GetFileName($Path).ToLower()
    
    # Check supported formats
    $supportedFormats = @('.tar', '.appx', '.zip')
    $supportedDoubleExtensions = @('.tar.gz', '.tar.bz2', '.tar.xz')
    
    # Check for double extensions (e.g., .tar.gz)
    foreach ($doubleExt in $supportedDoubleExtensions) {
        if ($fileName.EndsWith($doubleExt)) {
            Write-Verbose "Package format detected: $doubleExt"
            return $true
        }
    }
    
    # Check single extension
    if ($extension -in $supportedFormats) {
        Write-Verbose "Package format detected: $extension"
        return $true
    }
    
    # Check for .appxbundle
    if ($fileName.EndsWith('.appxbundle')) {
        Write-Verbose "Package format detected: .appxbundle"
        return $true
    }
    
    Write-Verbose "Unsupported package format: $fileName"
    return $false
}

function Get-PackageFormat {
    <#
    .SYNOPSIS
        Determines the format type of a package file.

    .DESCRIPTION
        Internal helper function to identify package format.
        Returns one of: TarGz, Tar, Appx, AppxBundle, Zip, Unknown

    .PARAMETER Path
        Path to the package file.

    .EXAMPLE
        $format = Get-PackageFormat -Path "ubuntu.tar.gz"
        # Returns "TarGz"

    .OUTPUTS
        String representing the package format.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )
    
    $fileName = [System.IO.Path]::GetFileName($Path).ToLower()
    $extension = [System.IO.Path]::GetExtension($Path).ToLower()
    
    if ($fileName.EndsWith('.tar.gz')) { return 'TarGz' }
    if ($fileName.EndsWith('.tar.bz2')) { return 'TarBz2' }
    if ($fileName.EndsWith('.tar.xz')) { return 'TarXz' }
    if ($fileName.EndsWith('.appxbundle')) { return 'AppxBundle' }
    if ($extension -eq '.tar') { return 'Tar' }
    if ($extension -eq '.appx') { return 'Appx' }
    if ($extension -eq '.zip') { return 'Zip' }
    
    return 'Unknown'
}

function Expand-DistroPackage {
    <#
    .SYNOPSIS
        Extracts and converts distribution packages to tar format suitable for WSL.

    .DESCRIPTION
        Internal helper function to handle various package formats (.appx, .zip, .tar.gz)
        and convert them to a .tar file that can be imported by WSL.
        
        For .appx and .appxbundle files, extracts the package and locates the install.tar.gz file.
        For .zip files, extracts and creates a tar archive.
        For .tar.gz files, extracts to .tar format.

    .PARAMETER PackagePath
        Path to the source package file.

    .PARAMETER DestinationPath
        Path where the extracted .tar file should be placed.
        If not specified, uses the same directory as PackagePath.

    .PARAMETER Force
        Overwrites existing files without prompting.

    .EXAMPLE
        Expand-DistroPackage -PackagePath "ubuntu.appx" -DestinationPath "C:\temp\ubuntu.tar"

    .EXAMPLE
        Expand-DistroPackage -PackagePath "debian.tar.gz" -Force

    .OUTPUTS
        String representing the path to the extracted .tar file.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateScript({Test-Path $_})]
        [string]$PackagePath,
        
        [Parameter(Mandatory = $false)]
        [string]$DestinationPath,
        
        [Parameter(Mandatory = $false)]
        [switch]$Force
    )
    
    $format = Get-PackageFormat -Path $PackagePath
    $packageDir = [System.IO.Path]::GetDirectoryName($PackagePath)
    $packageName = [System.IO.Path]::GetFileNameWithoutExtension($PackagePath)
    
    # Determine destination path
    if (-not $DestinationPath) {
        $DestinationPath = Join-Path $packageDir "$packageName.tar"
    }
    
    # Check if destination already exists
    if ((Test-Path $DestinationPath) -and -not $Force) {
        Write-Warning "Destination file already exists: $DestinationPath. Use -Force to overwrite."
        return $DestinationPath
    }
    
    Write-DistroNexusLog "Expanding package: $PackagePath (Format: $format)" -FileOnly
    
    try {
        switch ($format) {
            'Tar' {
                # Already in tar format, just copy
                if ($PSCmdlet.ShouldProcess($PackagePath, "Copy tar file")) {
                    Copy-Item -Path $PackagePath -Destination $DestinationPath -Force
                    Write-Verbose "Package is already in tar format, copied to $DestinationPath"
                }
                return $DestinationPath
            }
            
            'TarGz' {
                # Extract .tar.gz to .tar
                if ($PSCmdlet.ShouldProcess($PackagePath, "Extract tar.gz")) {
                    $tempDir = Join-Path $env:TEMP "DistroNexus_Extract_$(Get-Random)"
                    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
                    
                    try {
                        # Use tar command (available in Windows 10+)
                        Write-Verbose "Extracting tar.gz using tar command..."
                        $extractedTar = Join-Path $tempDir "$packageName.tar"
                        & tar -xzf "$PackagePath" -C "$tempDir" 2>&1 | Out-Null
                        
                        if ($LASTEXITCODE -eq 0) {
                            # Find the extracted tar file
                            $tarFile = Get-ChildItem -Path $tempDir -Filter "*.tar" | Select-Object -First 1
                            if ($tarFile) {
                                Move-Item -Path $tarFile.FullName -Destination $DestinationPath -Force
                                Write-Verbose "Extracted tar.gz to $DestinationPath"
                            }
                            else {
                                throw "No .tar file found after extraction"
                            }
                        }
                        else {
                            throw "tar extraction failed with exit code $LASTEXITCODE"
                        }
                    }
                    finally {
                        # Cleanup temp directory
                        if (Test-Path $tempDir) {
                            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
                        }
                    }
                }
                return $DestinationPath
            }
            
            'Appx' {
                # Extract .appx and find install.tar.gz
                if ($PSCmdlet.ShouldProcess($PackagePath, "Extract appx package")) {
                    $tempDir = Join-Path $env:TEMP "DistroNexus_Appx_$(Get-Random)"
                    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
                    
                    try {
                        Write-Verbose "Extracting appx package..."
                        # Appx is essentially a zip file
                        Expand-Archive -Path $PackagePath -DestinationPath $tempDir -Force
                        
                        # Find install.tar.gz or similar
                        $tarGzFiles = Get-ChildItem -Path $tempDir -Filter "*.tar.gz" -Recurse
                        if ($tarGzFiles) {
                            $installTar = $tarGzFiles | Where-Object { $_.Name -match "install" } | Select-Object -First 1
                            if (-not $installTar) {
                                $installTar = $tarGzFiles | Select-Object -First 1
                            }
                            
                            # Extract the tar.gz
                            Write-Verbose "Found tar.gz: $($installTar.Name)"
                            & tar -xzf "$($installTar.FullName)" -C "$tempDir" 2>&1 | Out-Null
                            
                            # Find the extracted tar
                            $tarFile = Get-ChildItem -Path $tempDir -Filter "*.tar" | Select-Object -First 1
                            if ($tarFile) {
                                Move-Item -Path $tarFile.FullName -Destination $DestinationPath -Force
                                Write-Verbose "Extracted appx to $DestinationPath"
                            }
                            else {
                                throw "No .tar file found after extraction"
                            }
                        }
                        else {
                            throw "No .tar.gz file found in appx package"
                        }
                    }
                    finally {
                        if (Test-Path $tempDir) {
                            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
                        }
                    }
                }
                return $DestinationPath
            }
            
            'AppxBundle' {
                # Extract .appxbundle, find .appx, then process like Appx
                if ($PSCmdlet.ShouldProcess($PackagePath, "Extract appxbundle")) {
                    $tempDir = Join-Path $env:TEMP "DistroNexus_AppxBundle_$(Get-Random)"
                    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
                    
                    try {
                        Write-Verbose "Extracting appxbundle..."
                        Expand-Archive -Path $PackagePath -DestinationPath $tempDir -Force
                        
                        # Find the appropriate .appx file (usually x64)
                        $appxFiles = Get-ChildItem -Path $tempDir -Filter "*.appx" -Recurse
                        $appxFile = $appxFiles | Where-Object { $_.Name -match "x64" } | Select-Object -First 1
                        if (-not $appxFile) {
                            $appxFile = $appxFiles | Select-Object -First 1
                        }
                        
                        if ($appxFile) {
                            # Recursively call to process the appx
                            return Expand-DistroPackage -PackagePath $appxFile.FullName -DestinationPath $DestinationPath -Force:$Force
                        }
                        else {
                            throw "No .appx file found in appxbundle"
                        }
                    }
                    finally {
                        if (Test-Path $tempDir) {
                            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
                        }
                    }
                }
                return $DestinationPath
            }
            
            'Zip' {
                # Extract zip and create tar
                if ($PSCmdlet.ShouldProcess($PackagePath, "Extract zip and create tar")) {
                    $tempDir = Join-Path $env:TEMP "DistroNexus_Zip_$(Get-Random)"
                    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
                    
                    try {
                        Write-Verbose "Extracting zip package..."
                        Expand-Archive -Path $PackagePath -DestinationPath $tempDir -Force
                        
                        # Check if there's already a tar or tar.gz inside
                        $tarFiles = Get-ChildItem -Path $tempDir -Filter "*.tar*" -Recurse
                        if ($tarFiles) {
                            $tarFile = $tarFiles | Select-Object -First 1
                            if ($tarFile.Extension -eq '.tar') {
                                Move-Item -Path $tarFile.FullName -Destination $DestinationPath -Force
                            }
                            else {
                                # It's a tar.gz, extract it
                                return Expand-DistroPackage -PackagePath $tarFile.FullName -DestinationPath $DestinationPath -Force:$Force
                            }
                        }
                        else {
                            # Create tar from extracted contents
                            Write-Verbose "Creating tar archive from zip contents..."
                            & tar -czf "$DestinationPath" -C "$tempDir" . 2>&1 | Out-Null
                            if ($LASTEXITCODE -ne 0) {
                                throw "Failed to create tar archive"
                            }
                        }
                        
                        Write-Verbose "Extracted zip to $DestinationPath"
                    }
                    finally {
                        if (Test-Path $tempDir) {
                            Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
                        }
                    }
                }
                return $DestinationPath
            }
            
            default {
                throw "Unsupported package format: $format"
            }
        }
    }
    catch {
        Write-DistroNexusLog "Failed to expand package: $_" -Level ERROR
        throw
    }
}

function Test-TarCommand {
    <#
    .SYNOPSIS
        Tests if the tar command is available on the system.

    .DESCRIPTION
        Internal helper function to check if tar.exe is available.
        Tar is built into Windows 10 (1903+) and Windows 11.

    .EXAMPLE
        if (Test-TarCommand) {
            # Use tar for extraction
        }

    .OUTPUTS
        Boolean indicating if tar command is available.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()
    
    try {
        $null = Get-Command tar -ErrorAction Stop
        return $true
    }
    catch {
        Write-Verbose "tar command not found on system"
        return $false
    }
}
