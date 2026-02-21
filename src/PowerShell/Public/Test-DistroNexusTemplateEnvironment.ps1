function Test-DistroNexusTemplateEnvironment {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [string]$Distro,

        [Parameter()]
        [ValidateSet('All', 'Wsl', 'Systemd', 'Gpu', 'Container')]
        [string[]]$Capability = @('All')
    )

    function New-CapabilityResult {
        param(
            [Parameter(Mandatory = $true)]
            [string]$Capability,
            [Parameter(Mandatory = $true)]
            [ValidateSet('Pass', 'Fail', 'Blocked')]
            [string]$Status,
            [Parameter(Mandatory = $true)]
            [string]$Reason,
            [Parameter()]
            [object]$Details
        )

        [PSCustomObject]@{
            Capability = $Capability
            Status = $Status
            Reason = $Reason
            Details = $Details
        }
    }

    function Invoke-WslCommand {
        param(
            [Parameter(Mandatory = $true)]
            [scriptblock]$Script,
            [Parameter(Mandatory = $true)]
            [string]$Name
        )

        $output = @(& $Script 2>&1)
        $exitCode = $LASTEXITCODE
        if ($null -eq $exitCode) {
            $exitCode = 0
        }

        [PSCustomObject]@{
            Name = $Name
            ExitCode = [int]$exitCode
            Output = ($output | ForEach-Object { $_.ToString() })
            Success = ([int]$exitCode -eq 0)
        }
    }

    function Resolve-CapabilitySet {
        param(
            [Parameter(Mandatory = $true)]
            [string[]]$Requested
        )

        if ($Requested -contains 'All') {
            return @('Wsl', 'Systemd', 'Gpu', 'Container')
        }

        $orderedUnique = New-Object System.Collections.Generic.List[string]
        foreach ($item in $Requested) {
            if (-not $orderedUnique.Contains($item)) {
                [void]$orderedUnique.Add($item)
            }
        }

        return @($orderedUnique)
    }

    $results = @()
    $requestedCapabilities = Resolve-CapabilitySet -Requested $Capability

    $wslCommand = Get-Command wsl.exe -ErrorAction SilentlyContinue
    if (-not $wslCommand) {
        $results += (New-CapabilityResult -Capability 'Wsl' -Status 'Fail' -Reason 'wsl.exe is not available on current host.' -Details @{})

        foreach ($cap in $requestedCapabilities) {
            if ($cap -ne 'Wsl') {
                $results += (New-CapabilityResult -Capability $cap -Status 'Blocked' -Reason 'WSL capability check failed; dependent capability skipped.' -Details @{})
            }
        }

        return @($results)
    }

    $wslStatus = Invoke-WslCommand -Name 'wsl-status' -Script { wsl.exe --status }
    $wslVersion = Invoke-WslCommand -Name 'wsl-version' -Script { wsl.exe --version }

    $wslResultStatus = if ($wslStatus.Success) { 'Pass' } else { 'Fail' }
    $wslReason = if ($wslStatus.Success) { 'WSL is available.' } else { 'WSL command is available but status query failed.' }
    $results += (New-CapabilityResult -Capability 'Wsl' -Status $wslResultStatus -Reason $wslReason -Details ([PSCustomObject]@{
        StatusCommand = $wslStatus
        VersionCommand = $wslVersion
    }))

    $resolvedDistro = $Distro
    if ([string]::IsNullOrWhiteSpace($resolvedDistro)) {
        $listResult = Invoke-WslCommand -Name 'wsl-list-quiet' -Script { wsl.exe --list --quiet }
        if ($listResult.Success) {
            $candidateDistros = @($listResult.Output | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            if ($candidateDistros.Count -gt 0) {
                $resolvedDistro = $candidateDistros[0]
            }
        }
    }

    $needsDistroChecks = @($requestedCapabilities | Where-Object { $_ -ne 'Wsl' }).Count -gt 0
    if ($needsDistroChecks -and [string]::IsNullOrWhiteSpace($resolvedDistro)) {
        foreach ($cap in $requestedCapabilities) {
            if ($cap -ne 'Wsl') {
                $results += (New-CapabilityResult -Capability $cap -Status 'Blocked' -Reason 'No WSL distro available for capability check.' -Details @{})
            }
        }

        return @($results)
    }

    foreach ($cap in $requestedCapabilities) {
        switch ($cap) {
            'Wsl' {
                continue
            }
            'Systemd' {
                $check = Invoke-WslCommand -Name 'systemd-check' -Script { wsl.exe -d $resolvedDistro -- bash -lc 'command -v systemctl >/dev/null 2>&1 && systemctl status >/dev/null 2>&1' }
                if ($check.Success) {
                    $results += (New-CapabilityResult -Capability 'Systemd' -Status 'Pass' -Reason 'systemd is available in target distro.' -Details ([PSCustomObject]@{ Distro = $resolvedDistro; Command = $check }))
                }
                else {
                    $results += (New-CapabilityResult -Capability 'Systemd' -Status 'Blocked' -Reason 'systemd is not available in target distro.' -Details ([PSCustomObject]@{ Distro = $resolvedDistro; Command = $check }))
                }
            }
            'Gpu' {
                $check = Invoke-WslCommand -Name 'gpu-check' -Script { wsl.exe -d $resolvedDistro -- bash -lc 'if [ -e /dev/dxg ] || command -v nvidia-smi >/dev/null 2>&1; then exit 0; else exit 1; fi' }
                if ($check.Success) {
                    $results += (New-CapabilityResult -Capability 'Gpu' -Status 'Pass' -Reason 'GPU capability detected in WSL environment.' -Details ([PSCustomObject]@{ Distro = $resolvedDistro; Command = $check }))
                }
                else {
                    $results += (New-CapabilityResult -Capability 'Gpu' -Status 'Blocked' -Reason 'GPU capability is not available in current WSL environment.' -Details ([PSCustomObject]@{ Distro = $resolvedDistro; Command = $check }))
                }
            }
            'Container' {
                $check = Invoke-WslCommand -Name 'container-check' -Script { wsl.exe -d $resolvedDistro -- bash -lc 'if command -v docker >/dev/null 2>&1 || command -v podman >/dev/null 2>&1; then exit 0; else exit 1; fi' }
                if ($check.Success) {
                    $results += (New-CapabilityResult -Capability 'Container' -Status 'Pass' -Reason 'Container runtime command detected in target distro.' -Details ([PSCustomObject]@{ Distro = $resolvedDistro; Command = $check }))
                }
                else {
                    $results += (New-CapabilityResult -Capability 'Container' -Status 'Blocked' -Reason 'Container runtime command is not available in target distro.' -Details ([PSCustomObject]@{ Distro = $resolvedDistro; Command = $check }))
                }
            }
        }
    }

    return @($results)
}