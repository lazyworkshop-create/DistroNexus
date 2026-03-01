function Get-DistroNexusInstanceTag {
    <#
    .SYNOPSIS
        Returns the tags associated with WSL instances.

    .PARAMETER Name
        Optional instance name. When omitted, returns tags for all instances.

    .EXAMPLE
        Get-DistroNexusInstanceTag

    .EXAMPLE
        Get-DistroNexusInstanceTag -Name "Ubuntu-22.04"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false, Position = 0)]
        [string]$Name
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        $tagMap = Get-InstanceTagMap

        if ($PSBoundParameters.ContainsKey('Name') -and $Name) {
            $tags = if ($tagMap.PSObject.Properties[$Name]) { @($tagMap.$Name) } else { @() }
            return [PSCustomObject]@{
                PSTypeName = 'DistroNexus.InstanceTags'
                Name       = $Name
                Tags       = $tags
            }
        }

        # Return all
        $tagMap.PSObject.Properties | ForEach-Object {
            [PSCustomObject]@{
                PSTypeName = 'DistroNexus.InstanceTags'
                Name       = $_.Name
                Tags       = @($_.Value)
            }
        }
    }
}

function Set-DistroNexusInstanceTag {
    <#
    .SYNOPSIS
        Replaces all tags for a WSL instance with the provided set.

    .PARAMETER Name
        The WSL instance name.

    .PARAMETER Tags
        The complete set of tags to assign (replaces any existing tags).
        Maximum 10 tags; each normalised to lowercase.

    .EXAMPLE
        Set-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tags @("dev","docker")
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory = $true, Position = 1)]
        [AllowEmptyCollection()]
        [string[]]$Tags
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        if ($Tags.Count -gt 10) {
            Write-Error "Maximum 10 tags allowed per instance. Got $($Tags.Count)." -ErrorId "DistroNexus.TooManyTags"
            return
        }

        $normalised = @($Tags | ForEach-Object { $_.ToLowerInvariant().Trim() } | Select-Object -Unique)

        Set-InstanceTagEntry -Name $Name -Tags $normalised

        Write-DistroNexusLog "Set tags for '$Name': $($normalised -join ', ')"
    }
}

function Add-DistroNexusInstanceTag {
    <#
    .SYNOPSIS
        Adds a single tag to a WSL instance.

    .PARAMETER Name
        The WSL instance name.

    .PARAMETER Tag
        The tag to add (normalised to lowercase; ignored if already present).

    .EXAMPLE
        Add-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tag "docker"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory = $true, Position = 1)]
        [ValidateNotNullOrEmpty()]
        [string]$Tag
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        $normalised = $Tag.ToLowerInvariant().Trim()
        $tagMap     = Get-InstanceTagMap
        $existing   = if ($tagMap.PSObject.Properties[$Name]) { @($tagMap.$Name) } else { @() }

        if ($existing.Count -ge 10) {
            Write-Error "Instance '$Name' already has 10 tags (maximum). Remove a tag before adding a new one." `
                -ErrorId "DistroNexus.TooManyTags"
            return
        }

        if ($existing -notcontains $normalised) {
            $existing = @($existing) + $normalised
            Set-InstanceTagEntry -Name $Name -Tags $existing
            Write-DistroNexusLog "Added tag '$normalised' to '$Name'"
        }
    }
}

function Remove-DistroNexusInstanceTag {
    <#
    .SYNOPSIS
        Removes a single tag from a WSL instance.

    .PARAMETER Name
        The WSL instance name.

    .PARAMETER Tag
        The tag to remove (case-insensitive).

    .EXAMPLE
        Remove-DistroNexusInstanceTag -Name "Ubuntu-22.04" -Tag "docker"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$Name,

        [Parameter(Mandatory = $true, Position = 1)]
        [ValidateNotNullOrEmpty()]
        [string]$Tag
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        $normalised = $Tag.ToLowerInvariant().Trim()
        $tagMap     = Get-InstanceTagMap
        $existing   = if ($tagMap.PSObject.Properties[$Name]) { @($tagMap.$Name) } else { @() }

        $updated = @($existing | Where-Object { $_ -ne $normalised })
        Set-InstanceTagEntry -Name $Name -Tags $updated

        Write-DistroNexusLog "Removed tag '$normalised' from '$Name' (if it was present)"
    }
}

# ── Module-private helpers ─────────────────────────────────────────────────────

function Get-TagSettingsFilePath {
    return Join-Path $env:APPDATA "DistroNexus\settings.json"
}

function Get-InstanceTagMap {
    $filePath = Get-TagSettingsFilePath
    if (-not (Test-Path $filePath)) {
        return [PSCustomObject]@{}
    }
    try {
        $content = Get-Content $filePath -Raw -ErrorAction Stop
        if ([string]::IsNullOrWhiteSpace($content)) { return [PSCustomObject]@{} }
        $obj = $content | ConvertFrom-Json
        if ($null -eq $obj.instanceTags) { return [PSCustomObject]@{} }
        return $obj.instanceTags
    }
    catch {
        return [PSCustomObject]@{}
    }
}

function Set-InstanceTagEntry {
    param(
        [string]$Name,
        [string[]]$Tags
    )
    $filePath = Get-TagSettingsFilePath
    $dir = Split-Path $filePath -Parent
    if (-not (Test-Path $dir)) { New-Item -Path $dir -ItemType Directory -Force | Out-Null }

    # Load existing settings or create fresh
    $settings = [PSCustomObject]@{ instanceTags = [PSCustomObject]@{} }
    if (Test-Path $filePath) {
        try {
            $raw = Get-Content $filePath -Raw -ErrorAction SilentlyContinue
            if (-not [string]::IsNullOrWhiteSpace($raw)) {
                $parsed = $raw | ConvertFrom-Json
                $settings = $parsed
                if ($null -eq $settings.instanceTags) {
                    $settings | Add-Member -NotePropertyName 'instanceTags' -NotePropertyValue ([PSCustomObject]@{}) -Force
                }
            }
        }
        catch { }
    }

    # Set or remove the entry
    if ($Tags -and $Tags.Count -gt 0) {
        if ($settings.instanceTags.PSObject.Properties[$Name]) {
            $settings.instanceTags.$Name = $Tags
        }
        else {
            $settings.instanceTags | Add-Member -NotePropertyName $Name -NotePropertyValue $Tags -Force
        }
    }
    else {
        # Remove the entry (empty tag list = no tags)
        if ($settings.instanceTags.PSObject.Properties[$Name]) {
            $newTags = [PSCustomObject]@{}
            $settings.instanceTags.PSObject.Properties |
                Where-Object { $_.Name -ne $Name } |
                ForEach-Object { $newTags | Add-Member -NotePropertyName $_.Name -NotePropertyValue $_.Value }
            $settings.instanceTags = $newTags
        }
    }

    $settings | ConvertTo-Json -Depth 10 | Set-Content $filePath -Encoding UTF8
}
