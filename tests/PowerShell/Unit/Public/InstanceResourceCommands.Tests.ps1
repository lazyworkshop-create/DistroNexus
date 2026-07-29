Describe 'Instance resource sparse PowerShell bridge commands' -Tag 'Unit', 'Public', 'Automation' {
    BeforeAll {
        function Invoke-DistroNexusWorkspaceBridge { param($Operation, $Payload) [pscustomobject]@{ Operation = $Operation; Payload = $Payload } }
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\InstanceResourceCommands.ps1')
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\Set-DistroNexusInstanceSparseMode.ps1')
    }

    It 'uses strict resource and preview routes' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Operation = $Operation; Payload = $Payload } }
        (Get-DistroNexusInstanceResources -Name Ubuntu).Operation | Should -Be 'instance.resources.get.v1'
        (Get-DistroNexusInstanceSparsePreview -Name Ubuntu -Enabled $true).Operation | Should -Be 'instance.sparse.preview.v1'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'instance.resources.get.v1' -and $Payload.Count -eq 1 -and $Payload.Name -eq 'Ubuntu' }
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'instance.sparse.preview.v1' -and $Payload.Count -eq 2 -and $Payload.Enabled }
    }

    It 'executes only the Core-issued token and honors WhatIf and decline' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Succeeded = $true; OutcomeCode = 'Succeeded' } }
        Set-DistroNexusInstanceSparseMode -PreviewToken ('a' * 64) -Confirm:$false | Out-Null
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'instance.sparse.execute.v1' -and $Payload.Count -eq 1 -and $Payload.PreviewToken -eq ('a' * 64) }
        Set-DistroNexusInstanceSparseMode -PreviewToken ('a' * 64) -WhatIf | Out-Null
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly
    }

    It 'rejects malformed names and tokens before bridge invocation' {
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not execute' }
        { Get-DistroNexusInstanceResources -Name "bad`nname" } | Should -Throw
        { Set-DistroNexusInstanceSparseMode -PreviewToken bad -Confirm:$false } | Should -Throw
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
    }
}
