# backup-runner.Tests.ps1
# Integration tests for backup-runner.ps1 — E-04-1 pending-notification persistence

BeforeAll {
    $script:backupRunnerSrc = Resolve-Path "$PSScriptRoot/../../../src/PowerShell/backup-runner.ps1"
    $script:originalAppData = $env:APPDATA
}

AfterAll {
    $env:APPDATA = $script:originalAppData
}

Describe "backup-runner failure notification" -Tag 'Unit', 'BackupRunner' {

    BeforeEach {
        # Redirect APPDATA to TestDrive so real APPDATA is never touched
        $env:APPDATA = $TestDrive

        # Build a temporary script directory that contains:
        #   backup-runner.ps1  (copy of the real one)
        #   DistroNexus.psd1   (minimal fake manifest)
        #   DistroNexus.psm1   (fake module with a throwing Invoke-DistroNexusBackup)
        $script:tmpDir = Join-Path $TestDrive "runner-test"
        New-Item -Path $script:tmpDir -ItemType Directory -Force | Out-Null

        # Copy the real backup-runner.ps1 into the temp dir so $PSScriptRoot
        # resolves to tmpDir, which contains the fake module.
        Copy-Item $script:backupRunnerSrc (Join-Path $script:tmpDir "backup-runner.ps1") -Force

        # Fake module script (.psm1) — provides only what backup-runner.ps1 needs.
        # Invoke-DistroNexusBackup throws to simulate a backup failure.
        $fakeModuleContent = @'
function Invoke-DistroNexusBackup {
    param(
        [string]$Name,
        [string]$Destination,
        [int]$RetentionCount
    )
    throw "Simulated backup failure for testing"
}
'@
        $fakeModuleContent | Set-Content (Join-Path $script:tmpDir "DistroNexus.psm1") -Encoding UTF8

        # Minimal module manifest (.psd1)
        $fakeManifestContent = @"
@{
    ModuleVersion = '0.0.1'
    RootModule    = 'DistroNexus.psm1'
    FunctionsToExport = @('Invoke-DistroNexusBackup')
}
"@
        $fakeManifestContent | Set-Content (Join-Path $script:tmpDir "DistroNexus.psd1") -Encoding UTF8
    }

    AfterEach {
        $env:APPDATA = $script:originalAppData
    }

    It "creates pending-notifications.json when a backup fails" {
        $runnerScript = Join-Path $script:tmpDir "backup-runner.ps1"
        $expectedNotifPath = Join-Path $TestDrive "DistroNexus\pending-notifications.json"

        # Ensure the notification file does not pre-exist
        Remove-Item $expectedNotifPath -Force -ErrorAction SilentlyContinue

        # Invoke backup-runner.ps1 in a subprocess so $PSScriptRoot resolves to tmpDir.
        # We pass APPDATA via the environment block so the notification lands in TestDrive.
        $psArgs = @(
            '-NonInteractive'
            '-NoProfile'
            '-File'
            $runnerScript
            '-InstanceName', 'TestInstance'
            '-Destination', (Join-Path $TestDrive "Backups")
            '-RetentionCount', '5'
        )

        $process = Start-Process -FilePath 'pwsh' `
            -ArgumentList $psArgs `
            -Wait -PassThru -NoNewWindow `
            -Environment @{ APPDATA = $TestDrive }

        # Script should exit non-zero (re-throws the caught exception)
        $process.ExitCode | Should -Not -Be 0

        # The notification file should have been written
        $expectedNotifPath | Should -Exist

        $json = Get-Content $expectedNotifPath -Raw | ConvertFrom-Json
        $json.notifications | Should -Not -BeNullOrEmpty
        $json.notifications[0].type     | Should -Be 'BackupFailure'
        $json.notifications[0].instance | Should -Be 'TestInstance'
        $json.notifications[0].message  | Should -Not -BeNullOrEmpty
        $json.notifications[0].time     | Should -Not -BeNullOrEmpty
    }

    It "appends to existing pending-notifications.json rather than overwriting" {
        $runnerScript = Join-Path $script:tmpDir "backup-runner.ps1"
        $notifDir  = Join-Path $TestDrive "DistroNexus"
        $notifPath = Join-Path $notifDir "pending-notifications.json"

        # Pre-seed an existing notification
        New-Item -Path $notifDir -ItemType Directory -Force | Out-Null
        @{
            notifications = @(
                @{
                    type     = "BackupFailure"
                    instance = "PreviousInstance"
                    message  = "previous error"
                    time     = (Get-Date -Format "o")
                }
            )
        } | ConvertTo-Json -Depth 5 | Set-Content $notifPath -Encoding UTF8

        $psArgs = @(
            '-NonInteractive'
            '-NoProfile'
            '-File'
            $runnerScript
            '-InstanceName', 'AnotherInstance'
            '-Destination', (Join-Path $TestDrive "Backups2")
            '-RetentionCount', '3'
        )

        Start-Process -FilePath 'pwsh' `
            -ArgumentList $psArgs `
            -Wait -NoNewWindow `
            -Environment @{ APPDATA = $TestDrive } | Out-Null

        $json = Get-Content $notifPath -Raw | ConvertFrom-Json
        $json.notifications.Count | Should -BeGreaterOrEqual 2
        ($json.notifications | Where-Object { $_.instance -eq 'PreviousInstance' }) | Should -Not -BeNullOrEmpty
        ($json.notifications | Where-Object { $_.instance -eq 'AnotherInstance' }) | Should -Not -BeNullOrEmpty
    }
}
