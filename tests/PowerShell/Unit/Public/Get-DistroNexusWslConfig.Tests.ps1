# Get-DistroNexusWslConfig.Tests.ps1 / Set-DistroNexusWslConfig.Tests.ps1
# Unit tests for E-02 .wslconfig Global Configuration Editor

BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force

    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")

    $script:originalProfile = $env:USERPROFILE
}

AfterAll {
    $env:USERPROFILE = $script:originalProfile
}

Describe "Get-DistroNexusWslConfig" -Tag 'Unit', 'Public', 'WslConfig' {

    BeforeEach {
        $env:USERPROFILE = $TestDrive
    }

    Context "Command structure" {
        It "Should be exported" {
            Get-Command Get-DistroNexusWslConfig | Should -Not -BeNullOrEmpty
        }
    }

    Context "When .wslconfig does not exist" {
        It "Should return object with null fields" {
            $result = Get-DistroNexusWslConfig
            $result | Should -Not -BeNullOrEmpty
            $result.Memory     | Should -BeNullOrEmpty
            $result.Processors | Should -BeNullOrEmpty
        }
    }

    Context "When .wslconfig exists with values" {
        It "Should parse Memory and Processors correctly" {
            $wslconfigPath = Join-Path $TestDrive ".wslconfig"
            @"
[wsl2]
memory=4GB
processors=2
swap=2GB
localhostForwarding=true
networkingMode=NAT
"@ | Set-Content -Path $wslconfigPath -Encoding UTF8

            $result = Get-DistroNexusWslConfig
            $result.Memory             | Should -Be "4GB"
            $result.Processors         | Should -Be "2"
            $result.Swap               | Should -Be "2GB"
            $result.LocalhostForwarding| Should -Be "true"
            $result.NetworkingMode     | Should -Be "NAT"
        }

        It "Should ignore lines outside [wsl2] section" {
            $wslconfigPath = Join-Path $TestDrive ".wslconfig"
            @"
[experimental]
autoMemoryReclaim=gradual

[wsl2]
memory=8GB
"@ | Set-Content -Path $wslconfigPath -Encoding UTF8

            $result = Get-DistroNexusWslConfig
            $result.Memory | Should -Be "8GB"
        }
    }
}

Describe "Set-DistroNexusWslConfig" -Tag 'Unit', 'Public', 'WslConfig' {

    BeforeEach {
        $env:USERPROFILE = $TestDrive
    }

    Context "Command structure" {
        It "Should be exported" {
            Get-Command Set-DistroNexusWslConfig | Should -Not -BeNullOrEmpty
        }

        It "Should accept Memory, Processors, Swap, LocalhostForwarding, NetworkingMode parameters" {
            $cmd = Get-Command Set-DistroNexusWslConfig
            $cmd.Parameters.ContainsKey('Memory') | Should -Be $true
            $cmd.Parameters.ContainsKey('Processors') | Should -Be $true
            $cmd.Parameters.ContainsKey('Swap') | Should -Be $true
        }
    }

    Context "When writing new config" {
        It "Should create .wslconfig with specified values" {
            Set-DistroNexusWslConfig -Memory "4GB" -Processors 2

            $wslconfigPath = Join-Path $TestDrive ".wslconfig"
            Test-Path $wslconfigPath | Should -Be $true

            $content = Get-Content -Raw -Path $wslconfigPath
            $content | Should -Match "memory=4GB"
            $content | Should -Match "processors=2"
        }

        It "Should preserve existing keys not mentioned" {
            $wslconfigPath = Join-Path $TestDrive ".wslconfig"
            @"
[wsl2]
memory=2GB
processors=4
swap=1GB
"@ | Set-Content -Path $wslconfigPath -Encoding UTF8

            Set-DistroNexusWslConfig -Memory "8GB"

            $content = Get-Content -Raw -Path $wslconfigPath
            $content | Should -Match "memory=8GB"
            $content | Should -Match "processors=4"
            $content | Should -Match "swap=1GB"
        }
    }

    Context "Validation" {
        It "Should reject non-numeric Processors value" {
            { Set-DistroNexusWslConfig -Processors -1 } | Should -Throw
        }
    }
}
