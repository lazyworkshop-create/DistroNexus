BeforeAll {
    $modulePath = Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\DistroNexus.psd1'
    Import-Module $modulePath -Force
}

Describe 'Get-DistroNexusTemplate' -Tag 'Unit', 'Public' {
    It 'exports the catalog command from the module manifest' {
        Get-Command Get-DistroNexusTemplate -ErrorAction Stop | Should -Not -BeNullOrEmpty
    }

    It 'uses only the fixed catalog list and get Bridge routes' {
        $path = Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\Get-DistroNexusTemplate.ps1'
        $content = Get-Content $path -Raw
        $content | Should -Match "template\.catalog\.get\.v1"
        $content | Should -Match "template\.catalog\.list\.v1"
        $content | Should -Not -Match 'Get-Content|ConvertFrom-Json|templates\.json'
    }
}
