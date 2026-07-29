BeforeAll { Import-Module (Join-Path (Resolve-Path "$PSScriptRoot/../../../..") 'src/PowerShell/DistroNexus.psd1') -Force }
Describe 'Template import file preview contract' -Tag 'Unit','Public' {
    It 'uses only the fixed source-path route' { InModuleScope DistroNexus { Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ PreviewToken=('a'*64) } }; Get-DistroNexusTemplateImportFilePreview -SourcePath 'C:\picked.json' | Out-Null; Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'template.local.import-file-preview.v1' -and $Payload.SourcePath -eq 'C:\picked.json' } } }
    It 'rejects control characters before bridge invocation' { InModuleScope DistroNexus { Mock Invoke-DistroNexusWorkspaceBridge {}; { Get-DistroNexusTemplateImportFilePreview -SourcePath "C:\bad`n.json" } | Should -Throw; Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0 } }
}
