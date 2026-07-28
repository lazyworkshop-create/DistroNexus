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

function Rename-DistroNexusInstanceTags {
    <#
    .SYNOPSIS
        Migrates all tags from one instance name to another.

    .DESCRIPTION
        Copies the tag list stored under OldName to NewName and removes the
        OldName entry.  Called automatically by Rename-DistroNexusInstance so
        tags follow the instance after an export/import rename.

    .PARAMETER OldName
        The current (pre-rename) instance name.

    .PARAMETER NewName
        The new instance name.

    .EXAMPLE
        Rename-DistroNexusInstanceTags -OldName "Ubuntu" -NewName "Ubuntu-Dev"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$OldName,

        [Parameter(Mandatory = $true)]
        [ValidateNotNullOrEmpty()]
        [string]$NewName
    )

    begin {
        Initialize-DistroNexusLogger
    }

    process {
        $tagMap = Get-InstanceTagMap
        $tags   = if ($tagMap.PSObject.Properties[$OldName]) { @($tagMap.$OldName) } else { @() }

        # Write tags under the new name
        Set-InstanceTagEntry -Name $NewName -Tags $tags

        # Remove the old entry
        Set-InstanceTagEntry -Name $OldName -Tags @()

        Write-DistroNexusLog "Tags migrated from '$OldName' to '$NewName'"
    }
}

# ── Module-private helpers ──────────────────────────────────────────────────────

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
