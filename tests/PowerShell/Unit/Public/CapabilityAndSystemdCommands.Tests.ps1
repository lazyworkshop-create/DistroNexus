Describe 'Capability and systemd PowerShell bridge adapters' -Tag 'Unit', 'Public', 'Automation' {
    BeforeAll {
        function Invoke-DistroNexusWorkspaceBridge { param($Operation, $Payload) [pscustomobject]@{ Operation = $Operation; Payload = $Payload } }
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\Get-DistroNexusCapability.ps1')
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\Get-DistroNexusSystemdService.ps1')
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\SystemdServiceCommands.ps1')
    }

    It 'routes instance capability facts only through the Core bridge' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Instance = @{ Name = 'Ubuntu' }; Capabilities = @{} } }
        $result = Get-DistroNexusCapability -Name Ubuntu -InstanceOnly
        $result.Instance.Name | Should -Be 'Ubuntu'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'capability' -and $Payload.InstanceName -eq 'Ubuntu' -and $Payload.InstanceOnly }
    }

    It 'returns a Core generated service preview for WhatIf without mutation' {
        Mock Invoke-DistroNexusWorkspaceBridge {
            if ($Operation -eq 'systemdPreview') { return [pscustomobject]@{ PreviewToken='current'; InstanceName='Ubuntu'; Unit=[pscustomobject]@{ Value='podman.socket' }; Action='Start'; Scope='User' } }
            throw 'must not execute'
        }
        $result = Start-DistroNexusSystemdService -Name Ubuntu -Unit podman.socket -WhatIf
        $result.PreviewToken | Should -Be 'current'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'systemdPreview' }
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0 -ParameterFilter { $Operation -eq 'systemdExecute' }
    }

    It 'rejects unsafe systemd unit input before bridge invocation' {
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }
        { Get-DistroNexusSystemdServicePreview -Name Ubuntu -Unit "bad.service`nnext" -Action Start } | Should -Throw
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
    }

    It 'validates and previews every Core systemd action without mutation' -ForEach @(
        @{ Action = 'Start' }, @{ Action = 'Stop' }, @{ Action = 'Restart' },
        @{ Action = 'Enable' }, @{ Action = 'Disable' }, @{ Action = 'Reload' }
    ) {
        param($Action)
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ PreviewToken='current'; InstanceName='Ubuntu'; Unit=[pscustomobject]@{ Value='demo.service' }; Action=$Action; Scope='User' } }
        $preview = Get-DistroNexusSystemdServicePreview -Name Ubuntu -Unit demo.service -Action $Action
        $preview.Action | Should -Be $Action
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'systemdPreview' -and $Payload.Action -eq $Action }
    }

    It 'returns previews and avoids execution for every additional mutating action under WhatIf' -ForEach @(
        @{ Name = 'Restart-DistroNexusSystemdService'; Action = 'Restart' },
        @{ Name = 'Enable-DistroNexusSystemdService'; Action = 'Enable' },
        @{ Name = 'Disable-DistroNexusSystemdService'; Action = 'Disable' },
        @{ Name = 'Reload-DistroNexusSystemdService'; Action = 'Reload' }
    ) {
        param($Name, $Action)
        Mock Invoke-DistroNexusWorkspaceBridge { if ($Operation -eq 'systemdPreview') { return [pscustomobject]@{ PreviewToken='current'; InstanceName='Ubuntu'; Unit=[pscustomobject]@{ Value='demo.service' }; Action=$Action; Scope='User' } }; throw 'must not execute' }
        $result = & $Name -Name Ubuntu -Unit demo.service -WhatIf
        $result.Action | Should -Be $Action
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'systemdPreview' -and $Payload.Action -eq $Action }
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0 -ParameterFilter { $Operation -eq 'systemdExecute' }
    }
}
