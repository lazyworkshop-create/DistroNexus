Describe 'DistroNexus module export contract' -Tag 'Unit', 'Public', 'Contract' {
    It 'publishes the documented v2.3 export count from its manifest' {
        $manifest = Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\DistroNexus.psd1'
        $metadata = Test-ModuleManifest -Path $manifest

        $metadata.ExportedFunctions.Count | Should -Be 93
        $metadata.ExportedFunctions.Keys | Should -Contain 'Remove-DistroNexusRecoveryPoint'
    }
}
