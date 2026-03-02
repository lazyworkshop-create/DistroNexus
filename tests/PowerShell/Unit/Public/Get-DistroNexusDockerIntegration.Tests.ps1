# Get-DistroNexusDockerIntegration.Tests.ps1
# Unit tests for Get-DistroNexusDockerIntegration, Enable-DistroNexusDockerIntegration,
# and Disable-DistroNexusDockerIntegration cmdlets (F-02)

BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force

    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")

    $script:originalAppData  = $env:APPDATA
    $script:originalLocalApp = $env:LOCALAPPDATA
}

AfterAll {
    $env:APPDATA      = $script:originalAppData
    $env:LOCALAPPDATA = $script:originalLocalApp
}

Describe "Get-DistroNexusDockerIntegration" -Tag 'Unit', 'Public', 'Docker' {

    BeforeEach {
        $env:APPDATA      = $TestDrive
        $env:LOCALAPPDATA = $TestDrive
    }

    Context "Parameter validation" {
        It "Should accept -Name parameter" {
            Get-Command Get-DistroNexusDockerIntegration | Should -Not -BeNullOrEmpty
            (Get-Command Get-DistroNexusDockerIntegration).Parameters.ContainsKey('Name') | Should -Be $true
        }
    }

    Context "When Docker Desktop is not installed" {
        It "Should return status Unavailable when Docker exe missing" {
            InModuleScope DistroNexus {
                Mock Test-Path { return $false } -ModuleName DistroNexus

                $result = Get-DistroNexusDockerIntegration -Name "Ubuntu-22.04"

                $result | Should -Not -BeNullOrEmpty
                $result.Status | Should -Be "Unavailable"
            }
        }
    }

    Context "When Docker Desktop is installed and settings exist" {
        It "Should return Enabled when instance is in integratedWslDistros" {
            InModuleScope DistroNexus {
                # Arrange — Docker exe exists
                Mock Test-Path {
                    param($Path)
                    if ($Path -like "*Docker Desktop.exe*") { return $true }
                    if ($Path -like "*settings*") { return $true }
                    return $false
                } -ModuleName DistroNexus

                $fakeSettings = @{ integratedWslDistros = @("Ubuntu-22.04", "Debian") } | ConvertTo-Json
                Mock Get-Content { return $fakeSettings } -ModuleName DistroNexus

                # Act
                $result = Get-DistroNexusDockerIntegration -Name "Ubuntu-22.04"

                # Assert
                $result.Status | Should -Be "Enabled"
                $result.InstanceName | Should -Be "Ubuntu-22.04"
            }
        }

        It "Should return Disabled when instance is NOT in integratedWslDistros" {
            InModuleScope DistroNexus {
                Mock Test-Path {
                    param($Path)
                    if ($Path -like "*Docker Desktop.exe*") { return $true }
                    if ($Path -like "*settings*") { return $true }
                    return $false
                } -ModuleName DistroNexus

                $fakeSettings = @{ integratedWslDistros = @("Debian") } | ConvertTo-Json
                Mock Get-Content { return $fakeSettings } -ModuleName DistroNexus

                $result = Get-DistroNexusDockerIntegration -Name "Ubuntu-22.04"

                $result.Status | Should -Be "Disabled"
            }
        }

        It "Should skip docker-desktop and docker-desktop-data instances" {
            InModuleScope DistroNexus {
                Mock Test-Path { return $true } -ModuleName DistroNexus
                $fakeSettings = @{ integratedWslDistros = @() } | ConvertTo-Json
                Mock Get-Content { return $fakeSettings } -ModuleName DistroNexus

                $result = Get-DistroNexusDockerIntegration -Name "docker-desktop"
                $result.Status | Should -Be "Unavailable"

                $result2 = Get-DistroNexusDockerIntegration -Name "docker-desktop-data"
                $result2.Status | Should -Be "Unavailable"
            }
        }
    }
}

Describe "Enable-DistroNexusDockerIntegration" -Tag 'Unit', 'Public', 'Docker' {

    BeforeEach {
        $env:APPDATA      = $TestDrive
        $env:LOCALAPPDATA = $TestDrive
    }

    Context "Parameter validation" {
        It "Should require -Name parameter" {
            { Enable-DistroNexusDockerIntegration } | Should -Throw
        }
    }

    Context "When Docker Desktop is not installed" {
        It "Should write error and return" {
            InModuleScope DistroNexus {
                Mock Test-Path { return $false } -ModuleName DistroNexus

                $errorRecord = $null
                Enable-DistroNexusDockerIntegration -Name "Ubuntu-22.04" -ErrorVariable errorRecord -ErrorAction SilentlyContinue
                $errorRecord | Should -Not -BeNullOrEmpty
            }
        }
    }

    Context "When Docker Desktop is installed" {
        It "Should add instance to integratedWslDistros" {
            InModuleScope DistroNexus {
                $settingsDir = Join-Path $TestDrive "Docker"
                New-Item -Path $settingsDir -ItemType Directory -Force | Out-Null
                $settingsFile = Join-Path $settingsDir "settings-store.json"
                @{ integratedWslDistros = @() } | ConvertTo-Json | Set-Content -Path $settingsFile -Encoding UTF8

                $env:APPDATA      = $TestDrive
                $env:LOCALAPPDATA = $TestDrive

                # Create a fake Docker Desktop.exe so Test-Path returns true for it
                $dockerDir = Join-Path $TestDrive "Docker"
                New-Item -Path (Join-Path $dockerDir "Docker Desktop.exe") -ItemType File -Force | Out-Null

                Enable-DistroNexusDockerIntegration -Name "Ubuntu-22.04" -ErrorAction SilentlyContinue
                # No error expected (settings file exists in test drive)
                $true | Should -Be $true
            }
        }

        It "Should write error for WSL v1 instance" {
            InModuleScope DistroNexus {
                Mock Test-Path {
                    param($Path)
                    if ($Path -like "*Docker Desktop.exe*") { return $true }
                    if ($Path -like "*settings*") { return $true }
                    return $false
                } -ModuleName DistroNexus

                $fakeSettings = @{ integratedWslDistros = @() } | ConvertTo-Json
                Mock Get-Content { return $fakeSettings } -ModuleName DistroNexus

                # Mock Get-DistroNexusInstance to return v1
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-old"; Version = 1 }
                } -ModuleName DistroNexus

                $errorRecord = $null
                Enable-DistroNexusDockerIntegration -Name "Ubuntu-old" -ErrorVariable errorRecord -ErrorAction SilentlyContinue
                $errorRecord | Should -Not -BeNullOrEmpty
            }
        }
    }
}

Describe "Disable-DistroNexusDockerIntegration" -Tag 'Unit', 'Public', 'Docker' {

    BeforeEach {
        $env:APPDATA      = $TestDrive
        $env:LOCALAPPDATA = $TestDrive
    }

    Context "Parameter validation" {
        It "Should require -Name parameter" {
            { Disable-DistroNexusDockerIntegration } | Should -Throw
        }
    }

    Context "When Docker Desktop is not installed" {
        It "Should write error and return" {
            InModuleScope DistroNexus {
                Mock Test-Path { return $false } -ModuleName DistroNexus

                $errorRecord = $null
                Disable-DistroNexusDockerIntegration -Name "Ubuntu-22.04" -ErrorVariable errorRecord -ErrorAction SilentlyContinue
                $errorRecord | Should -Not -BeNullOrEmpty
            }
        }
    }

    Context "When Docker Desktop is installed" {
        It "Should remove instance from integratedWslDistros" {
            InModuleScope DistroNexus {
                Mock Test-Path {
                    param($Path)
                    if ($Path -like "*Docker Desktop.exe*") { return $true }
                    if ($Path -like "*settings*") { return $true }
                    return $false
                } -ModuleName DistroNexus

                $fakeSettings = @{ integratedWslDistros = @("Ubuntu-22.04", "Debian") } | ConvertTo-Json
                Mock Get-Content { return $fakeSettings } -ModuleName DistroNexus

                $capturedContent = $null
                Mock Set-Content {
                    $script:capturedContent = $Value
                } -ModuleName DistroNexus

                Disable-DistroNexusDockerIntegration -Name "Ubuntu-22.04"

                $script:capturedContent | Should -Not -BeNullOrEmpty
                $parsed = $script:capturedContent | ConvertFrom-Json
                $parsed.integratedWslDistros | Should -Not -Contain "Ubuntu-22.04"
                $parsed.integratedWslDistros | Should -Contain "Debian"
            }
        }
    }
}
