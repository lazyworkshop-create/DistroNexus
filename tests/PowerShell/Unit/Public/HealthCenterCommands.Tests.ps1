Describe 'Health Center PowerShell bridge adapters' -Tag 'Unit', 'Public', 'Automation' {
    BeforeAll {
        function Invoke-DistroNexusWorkspaceBridge { param($Operation, $Payload, $Token) [pscustomobject]@{ Operation = $Operation; Payload = $Payload; Token = $Token } }
        . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\HealthCenterCommands.ps1')
    }

    It 'routes every canonical health repair ID through the Core preview contract' -ForEach @(
        @{ RepairId = 'open.wsl-update' }, @{ RepairId = 'open.windows-virtualization-settings' }, @{ RepairId = 'config.global.known-values' },
        @{ RepairId = 'config.instance.known-values' }, @{ RepairId = 'wsl.update' }, @{ RepairId = 'wsl.restart' }, @{ RepairId = 'wsl.trim' }, @{ RepairId = 'enable.windows-features' }
    ) {
        param($RepairId)
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Operation = $Operation; Payload = $Payload; Token = $Token } }
        $finding = [pscustomobject]@{ Id = "finding.$RepairId"; RepairId = $RepairId; Title = 'Test repair'; InstanceName = 'Ubuntu' }
        $preview = Get-DistroNexusHealthRepairPreview -Finding $finding
        $preview.Operation | Should -Be 'healthRepairPreview'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'healthRepairPreview' -and $Payload.Finding.RepairId -eq $RepairId }
    }

    It 'returns the Core preview and does not execute an automatable repair under WhatIf' {
        Mock Invoke-DistroNexusWorkspaceBridge {
            if ($Operation -eq 'healthRepairPreview') { return [pscustomobject]@{ PreviewToken = 'preview'; RepairId = 'wsl.update' } }
            throw 'execution must not run under WhatIf'
        }
        $finding = [pscustomobject]@{ Id = 'update'; RepairId = 'wsl.update'; Title = 'Update WSL' }
        $preview = Get-DistroNexusHealthRepairPreview -Finding $finding
        $result = Repair-DistroNexusHealthFinding -Finding $finding -Preview $preview -WhatIf
        $result.OutcomeCode | Should -Be 'WhatIf'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'healthRepairPreview' }
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0 -ParameterFilter { $Operation -eq 'healthRepairExecute' }
    }
}
