Describe 'DistroNexus module export contract' -Tag 'Unit', 'Public', 'Contract' {
    BeforeAll {
        $manifest = Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\DistroNexus.psd1'
        $metadata = Test-ModuleManifest -Path $manifest
        $module = Import-Module -Name $manifest -Force -PassThru
        $publicDirectory = Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public'
        $publicDefinitions = Get-ChildItem -Path $publicDirectory -Filter '*.ps1' -File |
            ForEach-Object {
                [regex]::Matches(
                    (Get-Content -Path $_.FullName -Raw),
                    '(?im)^function\s+([A-Za-z][A-Za-z0-9-]*DistroNexus[A-Za-z0-9-]*)\b'
                ) | ForEach-Object { $_.Groups[1].Value }
            }
    }

    It 'exports exactly the unique public DistroNexus function definitions' {
        @($metadata.ExportedFunctions.Keys | Sort-Object) |
            Should -Be @($publicDefinitions | Sort-Object -Unique)
    }

    It 'exports the recovery removal preview command' {
        $metadata.ExportedFunctions.Keys | Should -Contain 'Get-DistroNexusRecoveryPointRemovePreview'
    }

    It 'discovers exactly the manifest exports after module import' {
        @(Get-Command -Module $module.Name -CommandType Function | Select-Object -ExpandProperty Name | Sort-Object) |
            Should -Be @($metadata.ExportedFunctions.Keys | Sort-Object)
    }

    It 'defines each tag command exactly once in the public directory' {
        @(
            'Get-DistroNexusInstanceTag',
            'Add-DistroNexusInstanceTag',
            'Set-DistroNexusInstanceTag',
            'Remove-DistroNexusInstanceTag'
        ) | ForEach-Object {
            $tagCommand = $_
            @($publicDefinitions | Where-Object { $_ -eq $tagCommand }).Count | Should -Be 1
        }
    }

    AfterAll {
        Remove-Module -Name $module.Name -Force
    }
}
