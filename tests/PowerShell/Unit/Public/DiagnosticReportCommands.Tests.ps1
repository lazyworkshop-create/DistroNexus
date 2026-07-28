BeforeAll {
    $script:rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $script:rootPath 'src\PowerShell\DistroNexus.psd1') -Force
}

Describe 'Diagnostic report bridge contract' -Tag 'Unit', 'Public', 'Diagnostic' {
    It 'exports the fixed diagnostic command family' {
        'Get-DistroNexusDiagnosticReportPreview', 'Export-DistroNexusDiagnosticReport' | ForEach-Object {
            Get-Command $_ -Module DistroNexus | Should -Not -BeNullOrEmpty
        }
    }

    It 'maps preview and redacted export to fixed versioned routes' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ SnapshotToken = 'preview'; Format = 'Json'; Selection = [pscustomobject]@{ IsRedacted = $true } } }
            Get-DistroNexusDiagnosticReportPreview -Format Json -SelectedLogId 'app:current' -DeadlineMilliseconds 100 | Out-Null
            Export-DistroNexusDiagnosticReport -Preview ([pscustomobject]@{ SnapshotToken = 'preview'; Format = 'Json'; Selection = [pscustomobject]@{ IsRedacted = $true } }) -DestinationFileName report.json -DeadlineMilliseconds 100 -Confirm:$false | Out-Null
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'diagnostics.preview.v1' -and $Payload.Format -eq 'Json' -and $Payload.SelectedLogIds -contains 'app:current' -and $Payload.DeadlineMilliseconds -eq 100 }
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'diagnostics.export.v1' -and $Token -eq 'preview' -and $Payload.DestinationFileName -eq 'report.json' -and $Payload.DeadlineMilliseconds -eq 100 }
        }
    }

    It 'does not invoke export when WhatIf is requested' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'Bridge must not be called.' }
            Export-DistroNexusDiagnosticReport -Preview ([pscustomobject]@{ SnapshotToken = 'preview'; Format = 'Markdown'; Selection = [pscustomobject]@{ IsRedacted = $true } }) -DestinationFileName report.md -WhatIf | Should -BeFalse
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
        }
    }

    It 'does not invoke export when confirmation is declined' {
        $modulePath = Join-Path $script:rootPath 'src\PowerShell\DistroNexus.psd1'
        $script = "Import-Module '$modulePath' -Force; & (Get-Module DistroNexus) { function Invoke-DistroNexusWorkspaceBridge { exit 9 }; Export-DistroNexusDiagnosticReport -Preview ([pscustomobject]@{ SnapshotToken = 'preview'; Format = 'Json'; Selection = [pscustomobject]@{ IsRedacted = `$true } }) -DestinationFileName report.json -Confirm }"
        'N' | & pwsh -NoProfile -Command $script | Out-Null
        $LASTEXITCODE | Should -Be 0
    }

    It 'propagates only the bridge sanitized diagnostic failure' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'Diagnostic.ExportInvalid: <redacted>' }
            { Export-DistroNexusDiagnosticReport -Preview ([pscustomobject]@{ SnapshotToken = 'preview'; Format = 'Json'; Selection = [pscustomobject]@{ IsRedacted = $true } }) -DestinationFileName report.json -Confirm:$false } | Should -Throw '*Diagnostic.ExportInvalid: <redacted>*'
        }
    }

    It 'rejects an unsafe destination before bridge invocation' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'Bridge must not be called.' }
            { Export-DistroNexusDiagnosticReport -Preview ([pscustomobject]@{ SnapshotToken = 'preview'; Format = 'Json'; Selection = [pscustomobject]@{ IsRedacted = $true } }) -DestinationFileName '..\report.json' -Confirm:$false } | Should -Throw '*file name*'
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
        }
    }
}
