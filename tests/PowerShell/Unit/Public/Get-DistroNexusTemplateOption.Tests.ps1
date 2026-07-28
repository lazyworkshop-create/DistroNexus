BeforeAll {
    $modulePath = Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\DistroNexus.psd1'
    Import-Module $modulePath -Force
}

Describe 'Get-DistroNexusTemplateOption' {
    It 'uses the fixed catalog options route with only the template identifier' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { @{ Options = @() } }

            Get-DistroNexusTemplateOption -TemplateId 'dev-template' | Out-Null

            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter {
                $Operation -eq 'template.catalog.options.v1' -and $Payload.TemplateId -eq 'dev-template' -and $Payload.Count -eq 1
            }
        }
    }
}
