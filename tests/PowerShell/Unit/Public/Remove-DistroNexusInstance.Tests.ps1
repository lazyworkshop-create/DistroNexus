# Remove-DistroNexusInstance.Tests.ps1
# Unit tests for GAP-E04-2: backup schedule cleanup on instance remove

BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force

    $script:originalAppData = $env:APPDATA
}

AfterAll {
    $env:APPDATA = $script:originalAppData
}

Describe "Remove-DistroNexusInstance backup schedule cleanup" -Tag 'Unit', 'Public', 'Remove', 'Backup' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null
    }

    It "calls Remove-DistroNexusBackupSchedule when a schedule exists" {
        InModuleScope DistroNexus {
            # Arrange
            Mock Initialize-DistroNexusLogger {}
            Mock Write-DistroNexusLog {}

            $fakeInstance = [PSCustomObject]@{ Name = 'Ubuntu'; BasePath = $null }
            Mock Get-DistroNexusInstance {
                param($Name, [switch]$ForceUpdate)
                if ($ForceUpdate) { return @() }
                return @($fakeInstance)
            } -ModuleName DistroNexus

            Mock wsl {
                $global:LASTEXITCODE = 0
            } -ModuleName DistroNexus

            Mock Set-DistroNexusInstanceTag {} -ModuleName DistroNexus

            Mock Get-DistroNexusBackupSchedule {
                [PSCustomObject]@{ Name = 'Ubuntu' }
            } -ModuleName DistroNexus

            Mock Remove-DistroNexusBackupSchedule {} -ModuleName DistroNexus

            # Act
            $result = Remove-DistroNexusInstance -Name "Ubuntu" -Confirm:$false

            # Assert
            Should -Invoke Remove-DistroNexusBackupSchedule -Times 1 -Exactly -ModuleName DistroNexus
        }
    }

    It "does not call Remove-DistroNexusBackupSchedule when no schedule exists" {
        InModuleScope DistroNexus {
            # Arrange
            Mock Initialize-DistroNexusLogger {}
            Mock Write-DistroNexusLog {}

            $fakeInstance = [PSCustomObject]@{ Name = 'Ubuntu'; BasePath = $null }
            Mock Get-DistroNexusInstance {
                param($Name, [switch]$ForceUpdate)
                if ($ForceUpdate) { return @() }
                return @($fakeInstance)
            } -ModuleName DistroNexus

            Mock wsl {
                $global:LASTEXITCODE = 0
            } -ModuleName DistroNexus

            Mock Set-DistroNexusInstanceTag {} -ModuleName DistroNexus

            Mock Get-DistroNexusBackupSchedule {
                return $null
            } -ModuleName DistroNexus

            Mock Remove-DistroNexusBackupSchedule {} -ModuleName DistroNexus

            # Act
            $result = Remove-DistroNexusInstance -Name "Ubuntu" -Confirm:$false

            # Assert
            Should -Invoke Remove-DistroNexusBackupSchedule -Times 0 -Exactly -ModuleName DistroNexus
        }
    }
}
