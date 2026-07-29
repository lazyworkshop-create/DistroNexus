Describe 'Package download job commands' {
    BeforeAll { function Invoke-DistroNexusWorkspaceBridge { param($Operation, $Payload) }; . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\PackageDownloadJobCommands.ps1') }
    It 'starts only from an allow-listed package identifier preview' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ OutcomeCode='Package.JobPreviewReady' } }
        Start-DistroNexusPackageDownload -PackageId 'ubuntu-24.04' -Preview | Out-Null
        Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'package.jobs.start.preview.v1' -and $Payload.PackageId -eq 'ubuntu-24.04' }
    }
    It 'does not execute a job mutation under WhatIf' {
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not execute' }
        Start-DistroNexusPackageDownload -PreviewToken ('a' * 64) -WhatIf | Out-Null
        Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0
    }
    It 'routes a cancellation preview without accepting a path or URL' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ OutcomeCode='Package.JobPreviewReady' } }
        Invoke-DistroNexusPackageDownloadJobAction -JobId ('b' * 32) -Action cancel -Preview | Out-Null
        Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'package.jobs.cancel.preview.v1' -and $Payload.JobId -eq ('b' * 32) }
    }
    It 'routes retry and clear execution to matching bridge operations' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ OutcomeCode='Package.Retried' } }
        Invoke-DistroNexusPackageDownloadJobAction -Action retry -PreviewToken ('a' * 64) -Confirm:$false | Out-Null
        Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'package.jobs.retry.execute.v1' }
        Invoke-DistroNexusPackageDownloadJobAction -Action clear -PreviewToken ('b' * 64) -Confirm:$false | Out-Null
        Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'package.jobs.clear.execute.v1' }
    }
}
