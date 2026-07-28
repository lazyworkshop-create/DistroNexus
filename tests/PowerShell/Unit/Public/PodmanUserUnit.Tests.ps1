Describe 'Podman user unit preview and execution' -Tag 'Unit', 'Public', 'Container' {
    BeforeAll {
        function Invoke-DistroNexusWorkspaceBridge { param($Operation,$Payload,$Token) [pscustomobject]@{ Token='issued'; InstanceName='Ubuntu'; Unit='Socket'; Action='Start'; Effects=@('effect') } }
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\Get-DistroNexusPodmanUserUnitPreview.ps1')
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\Invoke-DistroNexusPodmanUserUnit.ps1')
    }
    It 'creates a typed preview only for service/socket start/stop' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Token='issued'; InstanceName='Ubuntu'; Unit='Socket'; Action='Start'; Effects=@('effect') }
        }
        $preview = Get-DistroNexusPodmanUserUnitPreview -Name Ubuntu -Unit Socket -Action Start
        $preview.Unit | Should -Be 'Socket'
        $preview.Token | Should -Not -BeNullOrEmpty
        { Get-DistroNexusPodmanUserUnitPreview -Name Ubuntu -Unit Socket -Action Restart } | Should -Throw
    }
    It 'honors WhatIf without invoking a systemd mutation' {
        $preview = [pscustomobject]@{ InstanceName='Ubuntu'; Unit='Socket'; Action='Start'; Token='token' }
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }
        $result = Invoke-DistroNexusPodmanUserUnit -Preview $preview -WhatIf
        $result.OutcomeCode | Should -Be 'WhatIf'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
    }

    It 'routes strict scalar execution parameters through the existing bridge operation' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Succeeded=$true; OutcomeCode='Succeeded' } }

        $result = Invoke-DistroNexusPodmanUserUnit -PreviewToken issued -InstanceName Ubuntu -Unit Socket -Action Start -Confirm:$false

        $result.Succeeded | Should -BeTrue
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'executePodmanUnit' -and $Token -eq 'issued' -and $Payload.InstanceName -eq 'Ubuntu' -and $Payload.Unit -eq 'Socket' -and $Payload.Action -eq 'Start' }
    }
}
