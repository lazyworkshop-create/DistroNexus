function New-DistroNexusReleaseEvidenceBundle {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseVersion,

        [Parameter()]
        [string[]]$WorkflowRuns,

        [Parameter()]
        [string[]]$TestArtifacts,

        [Parameter()]
        [string[]]$ReleaseLinks,

        [Parameter()]
        [object[]]$ManualOverrides,

        [Parameter()]
        [string]$ManualOverridesPath,

        [Parameter()]
        [string]$OutputPath
    )

    function ConvertTo-SafeLink {
        param(
            [Parameter()]
            [string]$Link
        )

        if ([string]::IsNullOrWhiteSpace($Link)) {
            return $null
        }

        try {
            $uri = [System.Uri]$Link
            $builder = [System.UriBuilder]::new($uri)
            $builder.Query = [string]::Empty
            $builder.Fragment = [string]::Empty
            return $builder.Uri.AbsoluteUri.TrimEnd('/')
        }
        catch {
            return $Link.Trim()
        }
    }

    function New-EvidenceItem {
        param(
            [Parameter(Mandatory = $true)]
            [string]$SourceType,
            [Parameter(Mandatory = $true)]
            [string]$Section,
            [Parameter()]
            [string]$Title,
            [Parameter()]
            [string]$Link,
            [Parameter()]
            [bool]$Manual,
            [Parameter()]
            [string]$PendingReason
        )

        $safeLink = ConvertTo-SafeLink -Link $Link
        $hasValidHttp = $false
        if (-not [string]::IsNullOrWhiteSpace($safeLink)) {
            $uriCheck = $null
            $hasValidHttp = [System.Uri]::TryCreate($safeLink, [System.UriKind]::Absolute, [ref]$uriCheck) -and ($uriCheck.Scheme -in @('http', 'https'))
        }

        $status = if ($hasValidHttp) { 'Resolved' } else { 'Unresolved' }
        $reason = if ($status -eq 'Unresolved') {
            if (-not [string]::IsNullOrWhiteSpace($PendingReason)) {
                $PendingReason
            }
            else {
                'Missing or invalid evidence link.'
            }
        }
        else {
            $null
        }

        $stableTitle = if ([string]::IsNullOrWhiteSpace($Title)) { "$SourceType evidence" } else { $Title.Trim() }
        $identityValue = if ([string]::IsNullOrWhiteSpace($safeLink)) { $stableTitle } else { $safeLink }
        $stableIdSource = "{0}|{1}|{2}" -f $SourceType, $Section, $identityValue
        $idHash = [System.BitConverter]::ToString(([System.Security.Cryptography.SHA256]::Create().ComputeHash([System.Text.Encoding]::UTF8.GetBytes($stableIdSource)))).Replace('-', '').Substring(0, 12).ToLowerInvariant()

        [PSCustomObject]@{
            Id = "ev-$idHash"
            SourceType = $SourceType
            Section = $Section
            Title = $stableTitle
            Link = $safeLink
            Status = $status
            PendingReason = $reason
            IsManualOverride = [bool]$Manual
        }
    }

    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        $defaultRoot = Join-Path $script:ProjectRoot 'docs\development\release-evidence'
        $safeVersion = $ReleaseVersion.Trim().TrimStart('v', 'V')
        $OutputPath = Join-Path $defaultRoot ("v{0}-evidence.json" -f $safeVersion)
    }

    $collectedManualOverrides = @()
    if ($ManualOverrides) {
        $collectedManualOverrides += $ManualOverrides
    }

    if (-not [string]::IsNullOrWhiteSpace($ManualOverridesPath) -and (Test-Path $ManualOverridesPath)) {
        try {
            $fromFile = Get-Content -Path $ManualOverridesPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($fromFile) {
                $collectedManualOverrides += @($fromFile)
            }
        }
        catch {
            throw "Failed to parse manual overrides file '$ManualOverridesPath': $($_.Exception.Message)"
        }
    }

    $items = [System.Collections.Generic.List[object]]::new()

    foreach ($link in @($WorkflowRuns | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $null = $items.Add((New-EvidenceItem -SourceType 'WorkflowRun' -Section 'BuildAndPackaging' -Title 'Workflow run' -Link $link -Manual:$false))
    }

    foreach ($link in @($TestArtifacts | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $null = $items.Add((New-EvidenceItem -SourceType 'TestArtifact' -Section 'AutomatedTests' -Title 'Test artifact' -Link $link -Manual:$false))
    }

    foreach ($link in @($ReleaseLinks | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $null = $items.Add((New-EvidenceItem -SourceType 'ReleaseLink' -Section 'ReleaseNotesAndDistribution' -Title 'Release link' -Link $link -Manual:$false))
    }

    foreach ($entry in @($collectedManualOverrides)) {
        $entryObject = [PSCustomObject]$entry
        $section = if ([string]::IsNullOrWhiteSpace($entryObject.Section)) { 'ManualFollowUp' } else { $entryObject.Section }
        $title = if ([string]::IsNullOrWhiteSpace($entryObject.Title)) { 'Manual evidence entry' } else { $entryObject.Title }
        $link = $entryObject.Link
        $pendingReason = $entryObject.PendingReason

        $null = $items.Add((New-EvidenceItem -SourceType 'ManualOverride' -Section $section -Title $title -Link $link -Manual:$true -PendingReason $pendingReason))
    }

    $orderedItems = @($items | Sort-Object -Property Section, SourceType, Id)
    $unresolvedItems = @($orderedItems | Where-Object { $_.Status -eq 'Unresolved' })

    $checklistMapping = @(
        $orderedItems |
            Group-Object -Property Section |
            Sort-Object -Property Name |
            ForEach-Object {
                $sectionItems = @($_.Group | Sort-Object -Property SourceType, Id)
                [PSCustomObject]@{
                    Section = $_.Name
                    ResolvedCount = (@($sectionItems | Where-Object { $_.Status -eq 'Resolved' })).Count
                    UnresolvedCount = (@($sectionItems | Where-Object { $_.Status -eq 'Unresolved' })).Count
                    Items = $sectionItems
                }
            }
    )

    $bundle = [PSCustomObject]@{
        SchemaVersion = '1.0'
        ReleaseVersion = $ReleaseVersion
        GeneratedAt = (Get-Date).ToString('o')
        Summary = [ordered]@{
            TotalItems = $orderedItems.Count
            Resolved = (@($orderedItems | Where-Object { $_.Status -eq 'Resolved' })).Count
            Unresolved = $unresolvedItems.Count
            Sections = $checklistMapping.Count
        }
        Sources = [ordered]@{
            WorkflowRunCount = @($WorkflowRuns).Count
            TestArtifactCount = @($TestArtifacts).Count
            ReleaseLinkCount = @($ReleaseLinks).Count
            ManualOverrideCount = @($collectedManualOverrides).Count
        }
        ChecklistMapping = $checklistMapping
        UnresolvedItems = $unresolvedItems
        Items = $orderedItems
    }

    $outputDirectory = Split-Path -Path $OutputPath -Parent
    if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
        [void](New-Item -Path $outputDirectory -ItemType Directory -Force)
    }

    $bundle | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath -Encoding UTF8

    [PSCustomObject]@{
        Status = if ($unresolvedItems.Count -gt 0) { 'CompletedWithUnresolved' } else { 'Completed' }
        ReleaseVersion = $ReleaseVersion
        OutputPath = $OutputPath
        Summary = $bundle.Summary
        UnresolvedItems = $unresolvedItems
    }
}
