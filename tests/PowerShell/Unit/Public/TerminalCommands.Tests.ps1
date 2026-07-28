Describe 'Terminal command contract' {
    BeforeAll { $modulePath = Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\DistroNexus.psd1'; Import-Module $modulePath -Force }
    It 'exports only typed terminal commands and honors WhatIf without bridge execution' {
        (Get-Command Get-DistroNexusTerminalStatus -Module DistroNexus).CommandType | Should -Be 'Function'
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'bridge should not run' } -ModuleName DistroNexus
        $result = Start-DistroNexusTerminal -Name Ubuntu -WhatIf
        $result.OutcomeCode | Should -Be 'Terminal.NotStarted'
        (Open-DistroNexusPackageCacheFolder -WhatIf).OutcomeCode | Should -Be 'PackageCache.NotOpened'
        Should -Invoke Invoke-DistroNexusWorkspaceBridge -ModuleName DistroNexus -Times 0
    }
    It 'rejects unsafe public terminal parameters before bridge invocation' {
        Mock Invoke-DistroNexusWorkspaceBridge { throw 'bridge should not run' } -ModuleName DistroNexus
        { Start-DistroNexusTerminal -Name "bad`nname" } | Should -Throw
        { Start-DistroNexusTerminal -Name Ubuntu -StartPath 'C:\outside' } | Should -Throw
        Should -Invoke Invoke-DistroNexusWorkspaceBridge -ModuleName DistroNexus -Times 0
    }
    It 'does not invoke the bridge when confirmation is declined' {
        $modulePath = Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\DistroNexus.psd1'
        $command = "Import-Module '$modulePath' -Force; Start-DistroNexusTerminal -Name Ubuntu -Confirm | ConvertTo-Json -Compress"
        $result = 'N' | & pwsh -NoProfile -Command $command
        ($result -join "`n") | Should -Match '"OutcomeCode":"Terminal.NotStarted"'
    }
}
