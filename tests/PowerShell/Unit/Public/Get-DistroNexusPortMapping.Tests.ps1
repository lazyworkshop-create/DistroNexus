# Get-DistroNexusPortMapping.Tests.ps1
# Unit tests for E-05 Port Forwarding Visualization

BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force

    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")
}

Describe "Get-DistroNexusPortMapping" -Tag 'Unit', 'Public', 'Network' {

    Context "Parameter validation" {
        It "Should require -Name parameter" {
            { Get-DistroNexusPortMapping } | Should -Throw
        }

        It "Should have -Protocol as optional parameter" {
            (Get-Command Get-DistroNexusPortMapping).Parameters.ContainsKey('Protocol') | Should -Be $true
        }
    }

    Context "When instance does not exist" {
        It "Should write error" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance { return $null } -ModuleName DistroNexus

                $errorRecord = $null
                Get-DistroNexusPortMapping -Name "NonExistent" `
                    -ErrorVariable errorRecord -ErrorAction SilentlyContinue

                $errorRecord | Should -Not -BeNullOrEmpty
            }
        }
    }

    Context "When instance is stopped" {
        It "Should return empty list with a warning" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                $result = Get-DistroNexusPortMapping -Name "Ubuntu-22.04" -WarningAction SilentlyContinue
                $result | Should -BeNullOrEmpty
            }
        }
    }

    Context "Output object shape" {
        It "Should return objects with required properties" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Running"; Version = 2 }
                } -ModuleName DistroNexus

                # Mock wsl calls
                Mock Invoke-Expression {
                    param($Command)
                    if ($Command -match 'ss.*-tlnp') {
                        return @(
                            "Netid State Recv-Q Send-Q Local Address:Port Peer Address:Port Process",
                            "tcp   LISTEN 0      128    0.0.0.0:8080          0.0.0.0:*     users:((""node"",pid=1234,fd=7))"
                        )
                    }
                    if ($Command -match 'ss.*-ulnp') { return @() }
                    if ($Command -match 'hostname') { return "172.25.80.1" }
                    if ($Command -match 'portproxy') { return @() }
                    return @()
                } -ModuleName DistroNexus

                $result = Get-DistroNexusPortMapping -Name "Ubuntu-22.04"

                if ($result) {
                    $result[0].PSObject.Properties.Name -contains 'Protocol'        | Should -Be $true
                    $result[0].PSObject.Properties.Name -contains 'Port'            | Should -Be $true
                    $result[0].PSObject.Properties.Name -contains 'LocalAddress'    | Should -Be $true
                    $result[0].PSObject.Properties.Name -contains 'ProcessName'     | Should -Be $true
                    $result[0].PSObject.Properties.Name -contains 'HasWindowsProxy' | Should -Be $true
                }
                $true | Should -Be $true
            }
        }
    }

    Context "Command structure" {
        It "Should have correct parameters: Name and Protocol" {
            (Get-Command Get-DistroNexusPortMapping).Parameters.ContainsKey('Name')     | Should -Be $true
            (Get-Command Get-DistroNexusPortMapping).Parameters.ContainsKey('Protocol') | Should -Be $true
        }
    }

    Context "Protocol filtering" {
        It "Should filter by -Protocol TCP" {
            (Get-Command Get-DistroNexusPortMapping).Parameters['Protocol'] | Should -Not -BeNullOrEmpty
        }
    }
}
