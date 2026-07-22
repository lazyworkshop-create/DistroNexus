Describe 'Podman connection preview and execution' -Tag 'Unit', 'Public', 'Container' {
    BeforeAll {
        function Invoke-DistroNexusWorkspaceBridge { param($Operation,$Payload,$Token) [pscustomobject]@{ Token='issued'; InstanceName='Ubuntu'; Name='local'; Endpoint='unix:///run/user/1000/podman/podman.sock'; Operation='Create'; Effects=@('effect') } }
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\Get-DistroNexusPodmanConnectionPreview.ps1')
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\Invoke-DistroNexusPodmanConnection.ps1')
    }

    It 'routes a credential-free local preview through the bridge' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Token='issued'; InstanceName='Ubuntu'; Name='local'; Endpoint='unix:///run/user/1000/podman/podman.sock'; Operation='Create'; Effects=@('effect') } }

        $preview = Get-DistroNexusPodmanConnectionPreview -Name Ubuntu -ConnectionName local -Endpoint 'unix:///run/user/1000/podman/podman.sock'

        $preview.Token | Should -Be 'issued'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'previewPodmanConnection' -and $Payload.InstanceName -eq 'Ubuntu' -and $Payload.Name -eq 'local' -and $Payload.Endpoint -eq 'unix:///run/user/1000/podman/podman.sock' }
    }

    It 'rejects unsafe endpoints before the bridge is called' {
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }

        { Get-DistroNexusPodmanConnectionPreview -Name Ubuntu -ConnectionName local -Endpoint 'http://user:secret@127.0.0.1:8080/?token=secret' } | Should -Throw

        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
    }

    It 'honors WhatIf without executing a connection mutation' {
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }
        $preview = [pscustomobject]@{ Token='issued'; InstanceName='Ubuntu'; Name='local'; Endpoint='unix:///run/user/1000/podman/podman.sock' }

        $result = Invoke-DistroNexusPodmanConnection -Preview $preview -WhatIf

        $result.OutcomeCode | Should -Be 'WhatIf'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
    }

    It 'rejects missing or forged preview data before execution' {
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }
        $missing = [pscustomobject]@{ Token=''; InstanceName='Ubuntu'; Name='local'; Endpoint='unix:///run/user/1000/podman/podman.sock' }
        $forged = [pscustomobject]@{ Token='forged'; InstanceName=''; Name='local'; Endpoint='unix:///run/user/1000/podman/podman.sock' }

        { Invoke-DistroNexusPodmanConnection -Preview $missing -Confirm:$false } | Should -Throw
        { Invoke-DistroNexusPodmanConnection -Preview $forged -Confirm:$false } | Should -Throw

        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
    }

    It 'routes a confirmed preview token and bound payload through the bridge' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Succeeded=$true; OutcomeCode='Succeeded' } }
        $preview = [pscustomobject]@{ Token='issued'; InstanceName='Ubuntu'; Name='local'; Endpoint='unix:///run/user/1000/podman/podman.sock' }

        $result = Invoke-DistroNexusPodmanConnection -Preview $preview -Confirm:$false

        $result.Succeeded | Should -BeTrue
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'executePodmanConnection' -and $Token -eq 'issued' -and $Payload.InstanceName -eq 'Ubuntu' -and $Payload.Name -eq 'local' -and $Payload.Endpoint -eq 'unix:///run/user/1000/podman/podman.sock' }
    }
}
