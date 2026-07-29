Describe 'Capability and systemd PowerShell bridge adapters' -Tag 'Unit', 'Public', 'Automation' {
    BeforeAll {
        function Invoke-DistroNexusWorkspaceBridge { param($Operation, $Payload) [pscustomobject]@{ Operation = $Operation; Payload = $Payload } }
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\Get-DistroNexusCapability.ps1')
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\Get-DistroNexusSystemdService.ps1')
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\SystemdServiceCommands.ps1')
    }

    It 'routes explicit host and instance capability facts only through the v1 Core bridge contracts' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Instance = @{ Name = 'Ubuntu' }; Capabilities = @{} } }
        $result = Get-DistroNexusCapability -Name Ubuntu
        $result.Instance.Name | Should -Be 'Ubuntu'
        Get-DistroNexusCapability -Host | Out-Null
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'capability.instance.v1' -and $Payload.Count -eq 1 -and $Payload.InstanceName -eq 'Ubuntu' }
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'capability.host.v1' -and $null -eq $Payload }
    }

    It 'rejects invalid capability names and mixed parameter sets before bridge invocation' {
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }
        { Get-DistroNexusCapability -Name "bad`nname" } | Should -Throw
        { Get-DistroNexusCapability -Host -Name Ubuntu } | Should -Throw
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
    }

    It 'returns a Core generated service preview for WhatIf without mutation' {
        Mock Invoke-DistroNexusWorkspaceBridge {
            if ($Operation -eq 'systemd.preview.v1') { return [pscustomobject]@{ PreviewToken='current'; InstanceName='Ubuntu'; Unit=[pscustomobject]@{ Value='podman.socket' }; Action='Start'; Scope='User' } }
            throw 'must not execute'
        }
        $result = Start-DistroNexusSystemdService -Name Ubuntu -Unit podman.socket -WhatIf
        $result.PreviewToken | Should -Be 'current'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'systemd.preview.v1' }
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0 -ParameterFilter { $Operation -eq 'systemd.execute.v1' }
    }

    It 'rejects unsafe systemd unit input before bridge invocation' {
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not run' }
        { Get-DistroNexusSystemdServicePreview -Name Ubuntu -Unit "bad.service`nnext" -Action Start } | Should -Throw
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
    }
    It 'executes only a Core-issued token payload and never resubmits unit or action' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Succeeded=$true; OutcomeCode='Succeeded' } }
        Invoke-DistroNexusSystemdService -PreviewToken '0123456789abcdef0123456789abcdef' -Confirm:$false | Out-Null
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'systemd.execute.v1' -and $Payload.Count -eq 1 -and $Payload.PreviewToken -eq '0123456789abcdef0123456789abcdef' }
    }

    It 'does not execute malformed or WhatIf preview requests' {
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not execute' }
        { Invoke-DistroNexusSystemdService -PreviewToken 'bad' -Confirm:$false } | Should -Throw
        Invoke-DistroNexusSystemdService -PreviewToken '0123456789abcdef0123456789abcdef' -WhatIf | Out-Null
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
    }
    It 'uses typed versioned detail and journal routes' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Operation=$Operation; Payload=$Payload } }
        (Get-DistroNexusSystemdServiceDetail -Name Ubuntu -Unit demo.service).Operation | Should -Be 'systemd.details.v1'
        (Get-DistroNexusSystemdServiceJournal -Name Ubuntu -Unit demo.service -LineLimit 20).Operation | Should -Be 'systemd.journal.v1'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'systemd.journal.v1' -and $Payload.LineLimit -eq 20 }
    }

    It 'validates and previews every Core systemd action without mutation' -ForEach @(
        @{ Action = 'Start' }, @{ Action = 'Stop' }, @{ Action = 'Restart' },
        @{ Action = 'Enable' }, @{ Action = 'Disable' }, @{ Action = 'Reload' }
    ) {
        param($Action)
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ PreviewToken='current'; InstanceName='Ubuntu'; Unit=[pscustomobject]@{ Value='demo.service' }; Action=$Action; Scope='User' } }
        $preview = Get-DistroNexusSystemdServicePreview -Name Ubuntu -Unit demo.service -Action $Action
        $preview.Action | Should -Be $Action
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'systemd.preview.v1' -and $Payload.Action -eq $Action }
    }

    It 'returns previews and avoids execution for every additional mutating action under WhatIf' -ForEach @(
        @{ Name = 'Restart-DistroNexusSystemdService'; Action = 'Restart' },
        @{ Name = 'Enable-DistroNexusSystemdService'; Action = 'Enable' },
        @{ Name = 'Disable-DistroNexusSystemdService'; Action = 'Disable' },
        @{ Name = 'Reload-DistroNexusSystemdService'; Action = 'Reload' }
    ) {
        param($Name, $Action)
        Mock Invoke-DistroNexusWorkspaceBridge { if ($Operation -eq 'systemd.preview.v1') { return [pscustomobject]@{ PreviewToken='current'; InstanceName='Ubuntu'; Unit=[pscustomobject]@{ Value='demo.service' }; Action=$Action; Scope='User' } }; throw 'must not execute' }
        $result = & $Name -Name Ubuntu -Unit demo.service -WhatIf
        $result.Action | Should -Be $Action
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'systemd.preview.v1' -and $Payload.Action -eq $Action }
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0 -ParameterFilter { $Operation -eq 'systemd.execute.v1' }
    }
}
