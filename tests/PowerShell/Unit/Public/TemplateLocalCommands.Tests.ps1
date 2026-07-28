BeforeAll {
    $modulePath = Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\DistroNexus.psd1'
    Import-Module $modulePath -Force
}

Describe 'Template local PowerShell commands' -Tag 'Unit', 'Public' {
    $commands = @(
        'Test-DistroNexusTemplateCompatibility', 'Get-DistroNexusTemplateImportPreview', 'Import-DistroNexusTemplate',
        'Get-DistroNexusTemplateExportPreview', 'Export-DistroNexusTemplate',
        'Get-DistroNexusTemplateRemovePreview', 'Remove-DistroNexusTemplate'
    )

    It 'exports the fixed local-template command family' {
        foreach ($name in $commands) { Get-Command $name -ErrorAction Stop | Should -Not -BeNullOrEmpty }
    }

    It 'guards every local mutation with ShouldProcess and WhatIf' {
        foreach ($name in 'Get-DistroNexusTemplateImportPreview', 'Import-DistroNexusTemplate', 'Remove-DistroNexusTemplate') {
            (Get-Command $name).Parameters.ContainsKey('WhatIf') | Should -BeTrue
        }
        Get-DistroNexusTemplateImportPreview -Content '{"Id":"demo"}' -WhatIf | Should -BeNullOrEmpty
        Import-DistroNexusTemplate -PreviewToken ('a' * 64) -WhatIf | Should -BeNullOrEmpty
        Remove-DistroNexusTemplate -PreviewToken ('a' * 64) -WhatIf | Should -BeNullOrEmpty
    }

    It 'validates token and bounded content arguments before a bridge invocation' {
        { Get-DistroNexusTemplateImportPreview -Content '' -ErrorAction Stop } | Should -Throw
        { Import-DistroNexusTemplate -PreviewToken 'forged' -ErrorAction Stop } | Should -Throw
        { Export-DistroNexusTemplate -PreviewToken 'forged' -ErrorAction Stop } | Should -Throw
        { Remove-DistroNexusTemplate -PreviewToken 'forged' -ErrorAction Stop } | Should -Throw
    }

    It 'contains only fixed v1 local-template routes and no filesystem import path' {
        $path = Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\TemplateLocalCommands.ps1'
        $content = Get-Content $path -Raw
        foreach ($route in 'template.compatibility.v1', 'template.local.import-preview.v1', 'template.local.import-execute.v1', 'template.local.export-preview.v1', 'template.local.export-execute.v1', 'template.local.remove-preview.v1', 'template.local.remove-execute.v1') { $content | Should -Match ([regex]::Escape($route)) }
        $content | Should -Not -Match 'ImportTemplateAsync|ExportTemplateAsync|Get-Content|ReadAllText|Path'
    }
}
