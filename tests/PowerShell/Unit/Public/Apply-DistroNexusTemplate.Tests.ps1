Describe 'Retired mutable template executor' -Tag 'Unit', 'Public' {
    BeforeAll {
        $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
        Import-Module (Join-Path $rootPath 'src\PowerShell\DistroNexus.psd1') -Force
    }

    It 'does not export the unsafe mutable-template executor' {
        Get-Command Apply-DistroNexusTemplate -ErrorAction SilentlyContinue | Should -BeNullOrEmpty
        $manifest = Import-PowerShellDataFile (Join-Path $rootPath 'src\PowerShell\DistroNexus.psd1')
        $manifest.FunctionsToExport | Should -Not -Contain 'Apply-DistroNexusTemplate'
    }
}
