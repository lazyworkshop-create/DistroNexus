function Invoke-DistroNexusTemplateAutomation {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [ValidateSet('AllTemplates', 'SelectedTemplates')]
        [string]$Mode = 'AllTemplates',

        [Parameter()]
        [string[]]$TemplateIds,

        [Parameter()]
        [string]$Distro,

        [Parameter()]
        [string]$OutputRoot = (Join-Path $script:ProjectRoot 'docs\development\testing\results'),

        [Parameter()]
        [switch]$IncludeCapabilityGated,

        [Parameter()]
        [switch]$DryRun,

        [Parameter()]
        [switch]$AllowCiOverride,

        [Parameter()]
        [ValidateSet('NUnitXml', 'JUnitXml')]
        [string]$TestResultFormat = 'NUnitXml'
    )

    function Invoke-RunnerCommand {
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

    function Test-TemplateProbe {
        param(
            [Parameter(Mandatory = $true)]
            [PSCustomObject]$Template,
            [Parameter(Mandatory = $true)]
            [string]$TargetDistro
        )

        $commands = @()
        switch -Wildcard ($Template.Id) {
            'dotnet*' { $commands = @('dotnet --list-sdks') }
            'nodejs*' { $commands = @('node -v') }
            'python*' { $commands = @('python --version') }
            'java-jvm*' { $commands = @('java -version') }
            'rust*' { $commands = @('rustc --version', 'cargo --version') }
            'go*' { $commands = @('go version') }
            'container-runtime*' { $commands = @('bash -lc "if command -v docker >/dev/null; then docker --version; elif command -v podman >/dev/null; then podman --version; else exit 127; fi"') }
            'kubernetes-local*' { $commands = @('kubectl version --client') }
            'database-local-stack' { $commands = @('bash -lc "if command -v psql >/dev/null || command -v mysql >/dev/null || command -v redis-cli >/dev/null || command -v mongosh >/dev/null || command -v sqlite3 >/dev/null; then exit 0; else exit 127; fi"') }
            'ai-ml-gpu-dev' { $commands = @('python --version') }
            default { $commands = @('bash -lc "echo ok"') }
        }

        $results = @()
        foreach ($command in $commands) {
            $cmdResult = Invoke-RunnerCommand -Name $command -Script { wsl.exe -d $TargetDistro -- bash -lc $command }
            $results += $cmdResult
            if (-not $cmdResult.Success) {
                return [PSCustomObject]@{ Success = $false; Results = $results }
            }
        }

        [PSCustomObject]@{ Success = $true; Results = $results }
    }

    function Test-CapabilityGate {
        param(
            [Parameter(Mandatory = $true)]
            [PSCustomObject]$Template,
            [Parameter(Mandatory = $true)]
            [string]$TargetDistro,
            [Parameter(Mandatory = $true)]
            [bool]$AllowCapabilityGated
        )

        $tags = @()
        if ($Template.PSObject.Properties.Name -contains 'ScenarioTags' -and $Template.ScenarioTags) {
            $tags = @($Template.ScenarioTags | ForEach-Object { $_.ToString().ToLowerInvariant() })
        }

        $isCapabilityGated = ($tags -contains 'gpu') -or ($tags -contains 'microk8s')
        if ($isCapabilityGated -and -not $AllowCapabilityGated) {
            return [PSCustomObject]@{ Blocked = $true; Reason = 'Capability-gated template excluded. Use -IncludeCapabilityGated to enable.' }
        }

        if ($tags -contains 'gpu') {
            $gpuCheck = Invoke-RunnerCommand -Name 'gpu-check' -Script { wsl.exe -d $TargetDistro -- bash -lc 'if [ -e /dev/dxg ] || command -v nvidia-smi >/dev/null; then exit 0; else exit 1; fi' }
            if (-not $gpuCheck.Success) {
                return [PSCustomObject]@{ Blocked = $true; Reason = 'GPU capability is not available on current host/WSL environment.' }
            }
        }

        if ($Template.Id -eq 'kubernetes-local-dev') {
            $systemdCheck = Invoke-RunnerCommand -Name 'systemd-check' -Script { wsl.exe -d $TargetDistro -- bash -lc 'command -v systemctl >/dev/null && systemctl status >/dev/null' }
            if (-not $systemdCheck.Success) {
                return [PSCustomObject]@{ Blocked = $true; Reason = 'systemd is required for kubernetes-local-dev checks and is not available.' }
            }
        }

        [PSCustomObject]@{ Blocked = $false; Reason = $null }
    }

    function New-TestResultXml {
        param(
            [Parameter(Mandatory = $true)]
            [PSCustomObject[]]$Items,
            [Parameter(Mandatory = $true)]
            [string]$Format,
            [Parameter(Mandatory = $true)]
            [string]$SuiteName
        )

        if ($Format -eq 'JUnitXml') {
            $doc = New-Object System.Xml.XmlDocument
            $testsuites = $doc.CreateElement('testsuites')
            $testsuite = $doc.CreateElement('testsuite')
            $testsuite.SetAttribute('name', $SuiteName)
            $testsuite.SetAttribute('tests', [string]$Items.Count)
            $testsuite.SetAttribute('failures', [string](($Items | Where-Object { $_.Status -eq 'Fail' }).Count))
            $testsuite.SetAttribute('skipped', [string](($Items | Where-Object { $_.Status -eq 'Blocked' }).Count))

            foreach ($item in $Items) {
                $testcase = $doc.CreateElement('testcase')
                $testcase.SetAttribute('name', $item.TemplateId)
                $testcase.SetAttribute('classname', 'DistroNexus.TemplateAutomation')

                if ($item.Status -eq 'Fail') {
                    $failure = $doc.CreateElement('failure')
                    $failure.SetAttribute('message', $item.Reason)
                    [void]$testcase.AppendChild($failure)
                }
                elseif ($item.Status -eq 'Blocked') {
                    $skipped = $doc.CreateElement('skipped')
                    $skipped.SetAttribute('message', $item.Reason)
                    [void]$testcase.AppendChild($skipped)
                }

                [void]$testsuite.AppendChild($testcase)
            }

            [void]$testsuites.AppendChild($testsuite)
            [void]$doc.AppendChild($testsuites)
            return $doc.OuterXml
        }

        $nunitDoc = New-Object System.Xml.XmlDocument
        $testResults = $nunitDoc.CreateElement('test-results')
        $testResults.SetAttribute('name', $SuiteName)
        $testResults.SetAttribute('total', [string]$Items.Count)
        $testResults.SetAttribute('failures', [string](($Items | Where-Object { $_.Status -eq 'Fail' }).Count))
        $testResults.SetAttribute('not-run', [string](($Items | Where-Object { $_.Status -eq 'Blocked' }).Count))

        $testSuite = $nunitDoc.CreateElement('test-suite')
        $testSuite.SetAttribute('name', $SuiteName)

        foreach ($item in $Items) {
            $testCase = $nunitDoc.CreateElement('test-case')
            $testCase.SetAttribute('name', $item.TemplateId)
            $testCase.SetAttribute('executed', [string]($item.Status -ne 'Blocked').ToString().ToLowerInvariant())
            $testCase.SetAttribute('success', [string]($item.Status -eq 'Pass').ToString().ToLowerInvariant())

            if ($item.Status -ne 'Pass') {
                $failure = $nunitDoc.CreateElement('failure')
                $message = $nunitDoc.CreateElement('message')
                $message.InnerText = $item.Reason
                [void]$failure.AppendChild($message)
                [void]$testCase.AppendChild($failure)
            }

            [void]$testSuite.AppendChild($testCase)
        }

        [void]$testResults.AppendChild($testSuite)
        [void]$nunitDoc.AppendChild($testResults)
        return $nunitDoc.OuterXml
    }

    if ($env:CI -and -not $AllowCiOverride) {
        Write-Warning 'CI environment detected. Skipping template automation by default. Use -AllowCiOverride to force execution.'
        return [PSCustomObject]@{
            Status = 'SkippedByPolicy'
            Reason = 'CI guard'
        }
    }

    if (-not (Get-Command wsl.exe -ErrorAction SilentlyContinue)) {
        throw 'wsl.exe is not available. This suite requires Windows + WSL2.'
    }

    $allTemplates = @(Get-DistroNexusTemplate)
    if (-not $allTemplates -or $allTemplates.Count -eq 0) {
        throw 'No templates were discovered from config/templates.json.'
    }

    if ($Mode -eq 'SelectedTemplates') {
        if (-not $TemplateIds -or $TemplateIds.Count -eq 0) {
            throw '-TemplateIds is required when -Mode SelectedTemplates is used.'
        }

        $normalizedIds = @()
        foreach ($idValue in $TemplateIds) {
            if ([string]::IsNullOrWhiteSpace($idValue)) { continue }
            $normalizedIds += ($idValue -split ',') | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        }

        $knownIds = @($allTemplates | ForEach-Object { $_.Id })
        $unknownIds = @($normalizedIds | Where-Object { $knownIds -notcontains $_ })
        if ($unknownIds.Count -gt 0) {
            throw "Unknown template IDs: $($unknownIds -join ', ')"
        }

        $allTemplates = @($normalizedIds | ForEach-Object { $id = $_; $allTemplates | Where-Object { $_.Id -eq $id } | Select-Object -First 1 })
    }

    if ([string]::IsNullOrWhiteSpace($Distro)) {
        $distroLines = @(& wsl.exe --list --quiet 2>$null)
        $candidateDistros = @($distroLines | ForEach-Object { $_.ToString().Trim() } | Where-Object { $_ })
        if ($candidateDistros.Count -gt 0) {
            $Distro = $candidateDistros[0]
        }
        if ([string]::IsNullOrWhiteSpace($Distro)) {
            throw 'No WSL distro found. Provide -Distro explicitly after installing/importing a WSL distro.'
        }
    }

    $now = Get-Date
    $dateFolder = $now.ToString('yyyyMMdd')
    $runId = '{0}-{1}' -f $now.ToString('HHmmss'), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
    $runDirectory = Join-Path (Join-Path $OutputRoot $dateFolder) $runId
    $logsDirectory = Join-Path $runDirectory 'logs'

    [void](New-Item -Path $logsDirectory -ItemType Directory -Force)

    $environmentSnapshot = [ordered]@{
        Timestamp = $now.ToString('o')
        Distro = $Distro
        WslStatus = (Invoke-RunnerCommand -Name 'wsl-status' -Script { wsl.exe --status })
        WslVersion = (Invoke-RunnerCommand -Name 'wsl-version' -Script { wsl.exe --version })
        WslList = (Invoke-RunnerCommand -Name 'wsl-list' -Script { wsl.exe --list --verbose })
    }

    $results = @()
    foreach ($template in $allTemplates) {
        $itemStart = Get-Date
        $item = [ordered]@{
            TemplateId = $template.Id
            TemplateName = $template.Name
            Status = 'Pass'
            Reason = ''
            DurationSeconds = 0
            ProbeResults = @()
        }

        $gate = Test-CapabilityGate -Template $template -TargetDistro $Distro -AllowCapabilityGated:$IncludeCapabilityGated.IsPresent
        if ($gate.Blocked) {
            $item.Status = 'Blocked'
            $item.Reason = $gate.Reason
        }
        elseif (-not $DryRun) {
            try {
                Apply-DistroNexusTemplate -InstanceName $Distro -TemplateId $template.Id -Force -ErrorAction Stop | Out-Null
                $probe = Test-TemplateProbe -Template $template -TargetDistro $Distro
                $item.ProbeResults = @($probe.Results)
                if (-not $probe.Success) {
                    $item.Status = 'Fail'
                    $item.Reason = 'Runtime probe failed.'
                }
            }
            catch {
                $item.Status = 'Fail'
                $item.Reason = $_.Exception.Message
            }
        }
        else {
            $item.Status = 'Pass'
            $item.Reason = 'Dry run'
        }

        $item.DurationSeconds = [int]((Get-Date) - $itemStart).TotalSeconds
        $itemPath = Join-Path $logsDirectory ("{0}.json" -f $template.Id)
        $item | ConvertTo-Json -Depth 10 | Set-Content -Path $itemPath -Encoding UTF8
        $results += [PSCustomObject]$item
    }

    $passCount = ($results | Where-Object { $_.Status -eq 'Pass' }).Count
    $failCount = ($results | Where-Object { $_.Status -eq 'Fail' }).Count
    $blockedCount = ($results | Where-Object { $_.Status -eq 'Blocked' }).Count

    $manifest = [ordered]@{
        RunId = $runId
        Timestamp = $now.ToString('o')
        Mode = $Mode
        Distro = $Distro
        DryRun = [bool]$DryRun
        IncludeCapabilityGated = [bool]$IncludeCapabilityGated
        Summary = [ordered]@{
            Total = $results.Count
            Pass = $passCount
            Fail = $failCount
            Blocked = $blockedCount
        }
        Environment = $environmentSnapshot
        Results = $results
    }

    $manifestPath = Join-Path $runDirectory 'run-manifest.json'
    $manifest | ConvertTo-Json -Depth 12 | Set-Content -Path $manifestPath -Encoding UTF8

    $xmlContent = New-TestResultXml -Items $results -Format $TestResultFormat -SuiteName 'DistroNexus.TemplateAutomation'
    $xmlPath = Join-Path $runDirectory 'test-results.xml'
    Set-Content -Path $xmlPath -Value $xmlContent -Encoding UTF8

    $failedOrBlocked = @($results | Where-Object { $_.Status -ne 'Pass' })
    $summaryLines = @(
        '# Built-in Template Automation Run Summary',
        '',
        "- RunId: $runId",
        "- Timestamp: $($now.ToString('o'))",
        "- Mode: $Mode",
        "- Distro: $Distro",
        "- Total: $($results.Count)",
        "- Pass: $passCount",
        "- Fail: $failCount",
        "- Blocked: $blockedCount",
        "- DryRun: $DryRun",
        '',
        '## Failed/Blocked Items',
        ''
    )

    if ($failedOrBlocked.Count -eq 0) {
        $summaryLines += '- None'
    }
    else {
        foreach ($entry in $failedOrBlocked) {
            $summaryLines += "- [$($entry.Status)] $($entry.TemplateId): $($entry.Reason)"
        }
    }

    $summaryLines += @(
        '',
        '## Artifacts',
        '',
        "- test-results.xml",
        "- run-manifest.json",
        "- logs/*.json"
    )

    $summaryPath = Join-Path $runDirectory 'summary.md'
    Set-Content -Path $summaryPath -Value ($summaryLines -join [Environment]::NewLine) -Encoding UTF8

    $indexPath = Join-Path $OutputRoot 'index.md'
    if (-not (Test-Path $indexPath)) {
        Set-Content -Path $indexPath -Value '# Built-in Template Automation Results Index' -Encoding UTF8
    }

    $relativeRunPath = "{0}/{1}" -f $dateFolder, $runId
    Add-Content -Path $indexPath -Value ("- {0} | {1} | {2} | pass={3}, fail={4}, blocked={5}" -f $now.ToString('yyyy-MM-dd HH:mm:ss'), $Mode, $relativeRunPath, $passCount, $failCount, $blockedCount)

    [PSCustomObject]@{
        Status = if ($failCount -gt 0) { 'Failed' } elseif ($blockedCount -gt 0) { 'CompletedWithBlocked' } else { 'Passed' }
        RunId = $runId
        RunDirectory = $runDirectory
        SummaryPath = $summaryPath
        ManifestPath = $manifestPath
        TestResultPath = $xmlPath
        Total = $results.Count
        Pass = $passCount
        Fail = $failCount
        Blocked = $blockedCount
        Results = $results
    }
}
