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
        [switch]$UseSharedDistro,

        [Parameter()]
        [ValidateSet('CpuOnly', 'GpuCapable', 'SystemdCapable')]
        [string]$CapabilityProfile,

        [Parameter()]
        [ValidateSet('NUnitXml', 'JUnitXml')]
        [string]$TestResultFormat = 'NUnitXml'
    )

    function Resolve-CapabilityPolicy {
        param(
            [Parameter()]
            [string]$Profile,
            [Parameter(Mandatory = $true)]
            [bool]$IncludeCapabilityGatedSwitch
        )

        if ([string]::IsNullOrWhiteSpace($Profile)) {
            return [PSCustomObject]@{
                ProfileName = 'Default'
                AllowCapabilityGated = $IncludeCapabilityGatedSwitch
                RequireGpu = $false
                RequireSystemd = $false
            }
        }

        switch ($Profile) {
            'CpuOnly' {
                return [PSCustomObject]@{
                    ProfileName = 'CpuOnly'
                    AllowCapabilityGated = $false
                    RequireGpu = $false
                    RequireSystemd = $false
                }
            }
            'GpuCapable' {
                return [PSCustomObject]@{
                    ProfileName = 'GpuCapable'
                    AllowCapabilityGated = $true
                    RequireGpu = $true
                    RequireSystemd = $false
                }
            }
            'SystemdCapable' {
                return [PSCustomObject]@{
                    ProfileName = 'SystemdCapable'
                    AllowCapabilityGated = $true
                    RequireGpu = $false
                    RequireSystemd = $true
                }
            }
        }
    }

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

    function Initialize-IsolationContext {
        param(
            [Parameter(Mandatory = $true)]
            [string]$RunId
        )

        $root = Join-Path ([System.IO.Path]::GetTempPath()) (Join-Path 'DistroNexus' (Join-Path 'template-automation' $RunId))
        $instancesRoot = Join-Path $root 'instances'
        [void](New-Item -Path $instancesRoot -ItemType Directory -Force)

        [PSCustomObject]@{
            Root = $root
            InstancesRoot = $instancesRoot
            BaseExportPath = (Join-Path $root 'base-distro.tar')
            Prepared = $false
        }
    }

    function Ensure-IsolationBaseExport {
        param(
            [Parameter(Mandatory = $true)]
            [PSCustomObject]$Context,
            [Parameter(Mandatory = $true)]
            [string]$BaseDistro
        )

        if ($Context.Prepared -and (Test-Path $Context.BaseExportPath)) {
            return
        }

        $stopResult = Invoke-RunnerCommand -Name 'base-terminate' -Script { wsl.exe --terminate $BaseDistro }
        if (-not $stopResult.Success) {
            Write-Verbose "Base distro terminate returned non-zero before export: $($stopResult.ExitCode)"
        }

        $exportResult = Invoke-RunnerCommand -Name 'base-export' -Script { wsl.exe --export $BaseDistro $Context.BaseExportPath }
        if (-not $exportResult.Success) {
            throw "Failed to export base distro '$BaseDistro' for isolation: $($exportResult.Output -join ' | ')"
        }

        $Context.Prepared = $true
    }

    function New-IsolatedTemplateInstance {
        param(
            [Parameter(Mandatory = $true)]
            [PSCustomObject]$Context,
            [Parameter(Mandatory = $true)]
            [string]$BaseDistro,
            [Parameter(Mandatory = $true)]
            [string]$TemplateId
        )

        Ensure-IsolationBaseExport -Context $Context -BaseDistro $BaseDistro

        $sanitizedTemplateId = ($TemplateId.ToLowerInvariant() -replace '[^a-z0-9-]', '-')
        $uniqueSuffix = [Guid]::NewGuid().ToString('N').Substring(0, 6)
        $instanceName = "dnx-auto-$sanitizedTemplateId-$uniqueSuffix"
        $installPath = Join-Path $Context.InstancesRoot $instanceName
        [void](New-Item -Path $installPath -ItemType Directory -Force)

        $importResult = Invoke-RunnerCommand -Name 'isolation-import' -Script { wsl.exe --import $instanceName $installPath $Context.BaseExportPath --version 2 }
        if (-not $importResult.Success) {
            throw "Failed to import isolated distro '$instanceName': $($importResult.Output -join ' | ')"
        }

        [PSCustomObject]@{
            InstanceName = $instanceName
            InstallPath = $installPath
        }
    }

    function Remove-IsolatedTemplateInstance {
        param(
            [Parameter(Mandatory = $true)]
            [string]$InstanceName,
            [Parameter(Mandatory = $false)]
            [string]$InstallPath
        )

        $errors = @()

        $terminateResult = Invoke-RunnerCommand -Name 'isolation-terminate' -Script { wsl.exe --terminate $InstanceName }
        if (-not $terminateResult.Success) {
            Write-Verbose "Terminate returned non-zero for ${InstanceName}: $($terminateResult.ExitCode)"
        }

        $unregisterResult = Invoke-RunnerCommand -Name 'isolation-unregister' -Script { wsl.exe --unregister $InstanceName }
        if (-not $unregisterResult.Success) {
            $errors += "unregister failed: $($unregisterResult.Output -join ' | ')"
        }

        if ($InstallPath -and (Test-Path $InstallPath)) {
            try {
                Remove-Item -Path $InstallPath -Recurse -Force -ErrorAction Stop
            }
            catch {
                $errors += "install path cleanup failed: $($_.Exception.Message)"
            }
        }

        [PSCustomObject]@{
            Success = ($errors.Count -eq 0)
            Message = ($errors -join '; ')
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
            'nodejs*' { $commands = @('bash -lc ''[ -s /root/.nvm/nvm.sh ] || command -v node >/dev/null 2>&1 || command -v nodejs >/dev/null 2>&1''') }
            'python*' { $commands = @('bash -lc ''if command -v python >/dev/null; then python --version; elif command -v python3 >/dev/null; then python3 --version; else exit 127; fi''') }
            'java-jvm*' { $commands = @('bash -lc ''if command -v java >/dev/null 2>&1; then java -version; exit $?; fi; if [ -f "$HOME/.sdkman/bin/sdkman-init.sh" ]; then set +u; . "$HOME/.sdkman/bin/sdkman-init.sh"; fi; command -v java >/dev/null 2>&1 && java -version''') }
            'rust*' { $commands = @('bash -lc ''if [ -x "$HOME/.cargo/bin/rustc" ]; then "$HOME/.cargo/bin/rustc" --version; else if [ -f "$HOME/.cargo/env" ]; then . "$HOME/.cargo/env"; fi; export PATH="$HOME/.cargo/bin:$PATH"; rustc --version; fi''', 'bash -lc ''if [ -x "$HOME/.cargo/bin/cargo" ]; then "$HOME/.cargo/bin/cargo" --version; else if [ -f "$HOME/.cargo/env" ]; then . "$HOME/.cargo/env"; fi; export PATH="$HOME/.cargo/bin:$PATH"; cargo --version; fi''') }
            'go*' { $commands = @('bash -lc ''export PATH="$PATH:/usr/local/go/bin"; go version''') }
            'container-runtime*' { $commands = @('bash -lc "if command -v docker >/dev/null; then docker --version; elif command -v podman >/dev/null; then podman --version; else exit 127; fi"') }
            'kubernetes-local*' { $commands = @('kubectl version --client') }
            'database-local-stack' { $commands = @('bash -lc "if command -v psql >/dev/null || command -v mysql >/dev/null || command -v redis-cli >/dev/null || command -v mongosh >/dev/null || command -v sqlite3 >/dev/null; then exit 0; else exit 127; fi"') }
            'ai-ml-gpu-dev' { $commands = @('bash -lc ''if command -v python >/dev/null; then python --version; elif command -v python3 >/dev/null; then python3 --version; else exit 127; fi''') }
            'infra-cli-toolbox' { $commands = @('bash -lc ''if command -v jq >/dev/null && command -v yq >/dev/null; then exit 0; else exit 127; fi''') }
            default { $commands = @('bash -lc "echo ok"') }
        }

        $results = @()
        foreach ($command in $commands) {
            $cmdResult = Invoke-RunnerCommand -Name $command -Script { wsl.exe -d $TargetDistro -u root -- bash -lc $command }
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
            [PSCustomObject]$CapabilityPolicy
        )

        $tags = @()
        if ($Template.PSObject.Properties.Name -contains 'ScenarioTags' -and $Template.ScenarioTags) {
            $tags = @($Template.ScenarioTags | ForEach-Object { $_.ToString().ToLowerInvariant() })
        }

        $requiresGpu = ($tags -contains 'gpu')
        $requiresSystemd = ($tags -contains 'microk8s') -or ($Template.Id -eq 'kubernetes-local-dev')
        $isCapabilityGated = $requiresGpu -or $requiresSystemd

        if ($isCapabilityGated -and -not $CapabilityPolicy.AllowCapabilityGated) {
            $blockReason = if ($CapabilityPolicy.ProfileName -ne 'Default') {
                "Capability-gated template excluded by capability profile '$($CapabilityPolicy.ProfileName)'."
            }
            else {
                'Capability-gated template excluded. Use -IncludeCapabilityGated to enable.'
            }

            return [PSCustomObject]@{
                Blocked = $true
                Reason = $blockReason
                Diagnostics = @()
            }
        }

        $requestedCapabilities = @()
        if ($requiresGpu -or $CapabilityPolicy.RequireGpu) {
            $requestedCapabilities += 'Gpu'
        }

        if ($requiresSystemd -or $CapabilityPolicy.RequireSystemd) {
            $requestedCapabilities += 'Systemd'
        }

        if ($requestedCapabilities.Count -gt 0) {
            $diagnostics = @(Test-DistroNexusTemplateEnvironment -Distro $TargetDistro -Capability $requestedCapabilities)

            if ($requiresGpu) {
                $gpuResult = @($diagnostics | Where-Object { $_.Capability -eq 'Gpu' } | Select-Object -First 1)
                if ($gpuResult.Count -eq 0 -or $gpuResult[0].Status -ne 'Pass') {
                    return [PSCustomObject]@{
                        Blocked = $true
                        Reason = 'GPU capability is not available on current host/WSL environment.'
                        Diagnostics = $diagnostics
                    }
                }
            }

            if ($requiresSystemd) {
                $systemdResult = @($diagnostics | Where-Object { $_.Capability -eq 'Systemd' } | Select-Object -First 1)
                if ($systemdResult.Count -eq 0 -or $systemdResult[0].Status -ne 'Pass') {
                    return [PSCustomObject]@{
                        Blocked = $true
                        Reason = 'systemd is required for kubernetes-local-dev checks and is not available.'
                        Diagnostics = $diagnostics
                    }
                }
            }

            return [PSCustomObject]@{
                Blocked = $false
                Reason = $null
                Diagnostics = $diagnostics
            }
        }

        [PSCustomObject]@{ Blocked = $false; Reason = $null; Diagnostics = @() }
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

    $capabilityPolicy = Resolve-CapabilityPolicy -Profile $CapabilityProfile -IncludeCapabilityGatedSwitch:$IncludeCapabilityGated.IsPresent

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
    $isolationContext = $null
    if (-not $UseSharedDistro) {
        $isolationContext = Initialize-IsolationContext -RunId $runId
    }

    [void](New-Item -Path $logsDirectory -ItemType Directory -Force)

    $environmentSnapshot = [ordered]@{
        Timestamp = $now.ToString('o')
        Distro = $Distro
        WslStatus = (Invoke-RunnerCommand -Name 'wsl-status' -Script { wsl.exe --status })
        WslVersion = (Invoke-RunnerCommand -Name 'wsl-version' -Script { wsl.exe --version })
        WslList = (Invoke-RunnerCommand -Name 'wsl-list' -Script { wsl.exe --list --verbose })
        InstanceIsolation = if ($UseSharedDistro) { 'SharedDistro' } else { 'PerTemplateIsolatedImport' }
    }

    $results = @()
    foreach ($template in $allTemplates) {
        $itemStart = Get-Date
        $executionDistro = $Distro
        $isolatedInstance = $null
        $item = [ordered]@{
            TemplateId = $template.Id
            TemplateName = $template.Name
            Status = 'Pass'
            Reason = ''
            DurationSeconds = 0
            ProbeResults = @()
            CapabilityDiagnostics = @()
            CapabilityProfile = $capabilityPolicy.ProfileName
        }

        try {
            if (-not $DryRun -and -not $UseSharedDistro) {
                $isolatedInstance = New-IsolatedTemplateInstance -Context $isolationContext -BaseDistro $Distro -TemplateId $template.Id
                $executionDistro = $isolatedInstance.InstanceName
                $item.IsolatedInstance = $executionDistro
            }

            $gate = Test-CapabilityGate -Template $template -TargetDistro $executionDistro -CapabilityPolicy $capabilityPolicy
            $item.CapabilityDiagnostics = @($gate.Diagnostics)
            if ($gate.Blocked) {
                $item.Status = 'Blocked'
                $item.Reason = $gate.Reason
            }
            elseif (-not $DryRun) {
                Apply-DistroNexusTemplate -InstanceName $executionDistro -TemplateId $template.Id -Force -ErrorAction Stop | Out-Null
                $probe = Test-TemplateProbe -Template $template -TargetDistro $executionDistro
                $item.ProbeResults = @($probe.Results)
                if (-not $probe.Success) {
                    $item.Status = 'Fail'
                    $item.Reason = 'Runtime probe failed.'
                }
            }
            else {
                $item.Status = 'Pass'
                $item.Reason = 'Dry run'
            }
        }
        catch {
            $item.Status = 'Fail'
            $item.Reason = $_.Exception.Message
        }
        finally {
            if ($isolatedInstance) {
                $cleanup = Remove-IsolatedTemplateInstance -InstanceName $isolatedInstance.InstanceName -InstallPath $isolatedInstance.InstallPath
                if (-not $cleanup.Success) {
                    if ($item.Status -eq 'Pass') {
                        $item.Status = 'Fail'
                        $item.Reason = "Cleanup failed: $($cleanup.Message)"
                    }
                    else {
                        $item.Reason = "$($item.Reason); cleanup failed: $($cleanup.Message)"
                    }
                }
            }
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
        IsolationMode = if ($UseSharedDistro) { 'SharedDistro' } else { 'PerTemplateIsolatedImport' }
        DryRun = [bool]$DryRun
        IncludeCapabilityGated = [bool]$IncludeCapabilityGated
        CapabilityProfile = if ([string]::IsNullOrWhiteSpace($CapabilityProfile)) { $null } else { $CapabilityProfile }
        CapabilityPolicy = $capabilityPolicy
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
        "- Isolation: $(if ($UseSharedDistro) { 'SharedDistro' } else { 'PerTemplateIsolatedImport' })",
        "- Total: $($results.Count)",
        "- Pass: $passCount",
        "- Fail: $failCount",
        "- Blocked: $blockedCount",
        "- DryRun: $DryRun",
        "- CapabilityProfile: $($capabilityPolicy.ProfileName)",
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

    if ($isolationContext -and (Test-Path $isolationContext.Root)) {
        try {
            Remove-Item -Path $isolationContext.Root -Recurse -Force -ErrorAction Stop
        }
        catch {
            Write-Warning "Failed to remove isolation workspace '$($isolationContext.Root)': $($_.Exception.Message)"
        }
    }

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
