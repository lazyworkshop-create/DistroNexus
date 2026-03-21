# Compress-DistroNexusInstance.Tests.ps1
# Unit tests for Compress-DistroNexusInstance

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

Describe "Compress-DistroNexusInstance" -Tag 'Unit', 'Public', 'Compact' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null
    }

    Context "Parameter validation" {
        It "Should throw when -Name is empty" {
            { Compress-DistroNexusInstance -Name "" } | Should -Throw
        }

        It "Should throw when -Name is not provided" {
            { Compress-DistroNexusInstance } | Should -Throw
        }
    }

    Context "When using -WhatIf" {
        It "Should return size estimate without modifying VHDX" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                $mockVhdxSize = 10GB
                Mock Get-Item {
                    return [PSCustomObject]@{ Length = $mockVhdxSize }
                } -ModuleName DistroNexus

                Mock Get-ItemProperty {
                    return [PSCustomObject]@{ BasePath = "C:\WSL\Ubuntu-22.04" }
                } -ModuleName DistroNexus

                Mock Optimize-VHD {} -ModuleName DistroNexus
                $optimizeCalled = $false
                Mock Optimize-VHD { $script:optimizeCalled = $true } -ModuleName DistroNexus

                # Act
                $result = Compress-DistroNexusInstance -Name "Ubuntu-22.04" -WhatIf

                # Assert — Optimize-VHD must NOT have been called
                $script:optimizeCalled | Should -BeFalse
                $result | Should -Not -BeNullOrEmpty
                $result.PSObject.Properties.Name | Should -Contain "SizeBefore"
            }
        }
    }

    Context "When Hyper-V module is available" {
        It "Should call Optimize-VHD with Mode Full" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Get-ItemProperty {
                    return [PSCustomObject]@{ BasePath = "C:\WSL\Ubuntu-22.04" }
                } -ModuleName DistroNexus

                $script:vhdxPath = Join-Path $TestDrive "ext4.vhdx"
                New-Item -Path $script:vhdxPath -ItemType File -Force | Out-Null
                [System.IO.File]::WriteAllBytes($script:vhdxPath, (New-Object byte[] 1048576))

                Mock Get-ItemProperty {
                    return [PSCustomObject]@{ BasePath = $TestDrive }
                } -ModuleName DistroNexus

                Mock Get-Module {
                    return [PSCustomObject]@{ Name = "Hyper-V" }
                } -ModuleName DistroNexus -ParameterFilter { $ListAvailable -and $Name -eq "Hyper-V" }

                $script:optimizeVhdCalled = $false
                Mock Optimize-VHD {
                    $script:optimizeVhdCalled = $true
                } -ModuleName DistroNexus

                Mock Invoke-Expression {} -ModuleName DistroNexus

                # Act
                Compress-DistroNexusInstance -Name "Ubuntu-22.04" -Force

                # Assert
                $script:optimizeVhdCalled | Should -BeTrue
            }
        }
    }

    Context "When Hyper-V module is NOT available" {
        It "Should fall back to diskpart" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Get-ItemProperty {
                    return [PSCustomObject]@{ BasePath = $TestDrive }
                } -ModuleName DistroNexus

                $script:vhdxPath = Join-Path $TestDrive "ext4.vhdx"
                New-Item -Path $script:vhdxPath -ItemType File -Force | Out-Null

                Mock Get-Module { return $null } -ModuleName DistroNexus -ParameterFilter { $ListAvailable -and $Name -eq "Hyper-V" }

                $script:diskpartCalled = $false
                Mock Invoke-Expression {
                    if ($Command -match "diskpart") { $script:diskpartCalled = $true }
                } -ModuleName DistroNexus

                Mock Test-AdminPrivilege { return $true } -ModuleName DistroNexus

                # Act
                Compress-DistroNexusInstance -Name "Ubuntu-22.04" -Force

                # Assert
                $script:diskpartCalled | Should -BeTrue
            }
        }
    }

    Context "Lifecycle management" {
        It "Should stop a running instance before compaction and restart it after" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Running"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Get-ItemProperty {
                    return [PSCustomObject]@{ BasePath = $TestDrive }
                } -ModuleName DistroNexus

                New-Item -Path (Join-Path $TestDrive "ext4.vhdx") -ItemType File -Force | Out-Null

                Mock Get-Module {
                    return [PSCustomObject]@{ Name = "Hyper-V" }
                } -ModuleName DistroNexus -ParameterFilter { $ListAvailable -and $Name -eq "Hyper-V" }
                Mock Optimize-VHD {} -ModuleName DistroNexus

                $script:stopCalled = $false
                $script:startCalled = $false
                Mock Stop-DistroNexusInstance  { $script:stopCalled = $true }  -ModuleName DistroNexus
                Mock Start-DistroNexusInstance { $script:startCalled = $true } -ModuleName DistroNexus

                # Act
                Compress-DistroNexusInstance -Name "Ubuntu-22.04" -Force

                # Assert
                $script:stopCalled  | Should -BeTrue
                $script:startCalled | Should -BeTrue
            }
        }

        It "Should NOT restart an instance that was already stopped" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Debian"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Get-ItemProperty {
                    return [PSCustomObject]@{ BasePath = $TestDrive }
                } -ModuleName DistroNexus

                New-Item -Path (Join-Path $TestDrive "ext4.vhdx") -ItemType File -Force | Out-Null
                Mock Get-Module { return [PSCustomObject]@{ Name = "Hyper-V" } } -ModuleName DistroNexus -ParameterFilter { $ListAvailable -and $Name -eq "Hyper-V" }
                Mock Optimize-VHD {} -ModuleName DistroNexus

                $script:startCalled = $false
                Mock Start-DistroNexusInstance { $script:startCalled = $true } -ModuleName DistroNexus

                # Act
                Compress-DistroNexusInstance -Name "Debian" -Force

                # Assert
                $script:startCalled | Should -BeFalse
            }
        }
    }

    Context "Instance not found" {
        It "Should write an error and return when instance does not exist" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance { return $null } -ModuleName DistroNexus

                $result = Compress-DistroNexusInstance -Name "NonExistent" -Force -ErrorAction SilentlyContinue

                $result | Should -BeNullOrEmpty
            }
        }
    }

    Context "Output object" {
        It "Should return a result object with Name, SizeBefore, SizeAfter, SpaceSaved" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Get-ItemProperty {
                    return [PSCustomObject]@{ BasePath = $TestDrive }
                } -ModuleName DistroNexus

                $vhdx = Join-Path $TestDrive "ext4.vhdx"
                [System.IO.File]::WriteAllBytes($vhdx, (New-Object byte[] (5 * 1024 * 1024)))

                Mock Get-Module { return [PSCustomObject]@{ Name = "Hyper-V" } } -ModuleName DistroNexus -ParameterFilter { $ListAvailable -and $Name -eq "Hyper-V" }
                Mock Optimize-VHD {} -ModuleName DistroNexus

                $result = Compress-DistroNexusInstance -Name "Ubuntu-22.04" -Force

                $result.Name      | Should -Be "Ubuntu-22.04"
                $result.SizeBefore | Should -BeGreaterThan 0
                $result.PSObject.Properties.Name | Should -Contain "SizeAfter"
                $result.PSObject.Properties.Name | Should -Contain "SpaceSaved"
            }
        }
    }

    Context "Pipeline and array support" {
        It "Should accept an array of names via -Name parameter" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = $Name; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Get-ItemProperty {
                    return [PSCustomObject]@{ BasePath = $TestDrive }
                } -ModuleName DistroNexus

                $vhdx1 = Join-Path $TestDrive "ubuntu.vhdx"
                $vhdx2 = Join-Path $TestDrive "debian.vhdx"
                New-Item -Path $vhdx1 -ItemType File -Force | Out-Null
                New-Item -Path $vhdx2 -ItemType File -Force | Out-Null

                { Compress-DistroNexusInstance -Name "Ubuntu", "Debian" -WhatIf } | Should -Not -Throw
            }
        }

        It "Should accept names from pipeline" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = $Name; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Get-ItemProperty {
                    return [PSCustomObject]@{ BasePath = $TestDrive }
                } -ModuleName DistroNexus

                $vhdx1 = Join-Path $TestDrive "ubuntu.vhdx"
                $vhdx2 = Join-Path $TestDrive "debian.vhdx"
                New-Item -Path $vhdx1 -ItemType File -Force | Out-Null
                New-Item -Path $vhdx2 -ItemType File -Force | Out-Null

                { "Ubuntu", "Debian" | Compress-DistroNexusInstance -WhatIf } | Should -Not -Throw
            }
        }
    }
}
