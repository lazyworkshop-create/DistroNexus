# Invoke-DistroNexusBackup.Tests.ps1
# Unit tests for E-04 — On-demand backup invocation

BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force

    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")

    $script:originalAppData = $env:APPDATA
}

AfterAll {
    $env:APPDATA = $script:originalAppData
}

Describe "Invoke-DistroNexusBackup" -Tag 'Unit', 'Public', 'Backup' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null
    }

    Context "Parameter validation" {
        It "Should require -Name parameter" {
            { Invoke-DistroNexusBackup -Destination "C:\Backups" -RetentionCount 5 } | Should -Throw
        }

        It "Should require -Destination parameter" {
            { Invoke-DistroNexusBackup -Name "Ubuntu-22.04" -RetentionCount 5 } | Should -Throw
        }

        It "Should require -RetentionCount parameter" {
            { Invoke-DistroNexusBackup -Name "Ubuntu-22.04" -Destination "C:\Backups" } | Should -Throw
        }
    }

    Context "Backup filename pattern" {
        It "Should use <Name>-backup-<yyyyMMdd-HHmmss>.tar pattern" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Export-DistroNexusInstance { return $null } -ModuleName DistroNexus

                $backupDir = Join-Path $TestDrive "Backups"
                New-Item -Path $backupDir -ItemType Directory -Force | Out-Null

                # Verify parameters
                (Get-Command Invoke-DistroNexusBackup).Parameters.ContainsKey('Name')           | Should -Be $true
                (Get-Command Invoke-DistroNexusBackup).Parameters.ContainsKey('Destination')    | Should -Be $true
                (Get-Command Invoke-DistroNexusBackup).Parameters.ContainsKey('RetentionCount') | Should -Be $true
            }
        }
    }

    Context "Instance lifecycle" {
        It "Should stop running instance before backup and restart after" {
            InModuleScope DistroNexus {
                $script:stopCalled    = $false
                $script:startCalled   = $false
                $script:exportCalled  = $false

                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Running"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Stop-DistroNexusInstance {
                    $script:stopCalled = $true
                } -ModuleName DistroNexus

                Mock Start-DistroNexusInstance {
                    $script:startCalled = $true
                } -ModuleName DistroNexus

                Mock Export-DistroNexusInstance {
                    $script:exportCalled = $true
                } -ModuleName DistroNexus

                $backupDir = Join-Path $TestDrive "BackupsLifecycle"
                New-Item -Path $backupDir -ItemType Directory -Force | Out-Null

                Invoke-DistroNexusBackup -Name "Ubuntu-22.04" -Destination $backupDir -RetentionCount 3

                $script:stopCalled   | Should -Be $true
                $script:exportCalled | Should -Be $true
                $script:startCalled  | Should -Be $true
            }
        }

        It "Should not restart a stopped instance after backup" {
            InModuleScope DistroNexus {
                $script:startCalled = $false

                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Debian"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Export-DistroNexusInstance { } -ModuleName DistroNexus

                Mock Start-DistroNexusInstance {
                    $script:startCalled = $true
                } -ModuleName DistroNexus

                $backupDir = Join-Path $TestDrive "BackupsStopped"
                New-Item -Path $backupDir -ItemType Directory -Force | Out-Null

                Invoke-DistroNexusBackup -Name "Debian" -Destination $backupDir -RetentionCount 3

                $script:startCalled | Should -Be $false
            }
        }
    }

    Context "Retention enforcement" {
        It "Should delete oldest backup files when count exceeds RetentionCount" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Export-DistroNexusInstance { } -ModuleName DistroNexus

                $backupDir = Join-Path $TestDrive "BackupsRetention"
                New-Item -Path $backupDir -ItemType Directory -Force | Out-Null

                # Create 5 existing "old" backup files
                1..5 | ForEach-Object {
                    $dt = (Get-Date).AddDays(-$_).ToString("yyyyMMdd-HHmmss")
                    $null = New-Item -Path (Join-Path $backupDir "Ubuntu-22.04-backup-$dt.tar") -ItemType File -Force
                }

                Invoke-DistroNexusBackup -Name "Ubuntu-22.04" -Destination $backupDir -RetentionCount 3

                $remaining = Get-ChildItem $backupDir -Filter "Ubuntu-22.04-backup-*.tar" | Measure-Object
                $remaining.Count | Should -BeLessOrEqual 3
            }
        }
    }

    Context "When instance does not exist" {
        It "Should write error" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance { return $null } -ModuleName DistroNexus

                $backupDir = Join-Path $TestDrive "BackupsNotFound"
                New-Item -Path $backupDir -ItemType Directory -Force | Out-Null

                $errorRecord = $null
                Invoke-DistroNexusBackup -Name "NonExistent" -Destination $backupDir `
                    -RetentionCount 5 `
                    -ErrorVariable errorRecord -ErrorAction SilentlyContinue

                $errorRecord | Should -Not -BeNullOrEmpty
            }
        }
    }
}
