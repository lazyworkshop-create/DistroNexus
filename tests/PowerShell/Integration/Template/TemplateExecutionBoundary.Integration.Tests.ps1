Describe 'Template execution boundary integration' -Tag 'Integration', 'Template' {
    BeforeAll {
        $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
        Import-Module (Join-Path $rootPath 'src\PowerShell\DistroNexus.psd1') -Force
    }

    It 'does not expose the retired mutable-template executor' {
        Get-Command Apply-DistroNexusTemplate -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
    }
}
