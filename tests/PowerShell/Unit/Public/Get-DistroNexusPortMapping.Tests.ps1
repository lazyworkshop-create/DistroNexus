BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $rootPath 'src\PowerShell\DistroNexus.psd1') -Force
}
Describe 'Get-DistroNexusPortMapping' -Tag 'Unit', 'Public', 'Network' {
    It 'requires the instance name' { { Get-DistroNexusPortMapping } | Should -Throw }
    It 'uses only the fixed versioned bridge operation' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { @([pscustomobject]@{ Port = 8080; Protocol = 'TCP' }) }
            $result = Get-DistroNexusPortMapping -Name Ubuntu -Protocol TCP
            $result.Port | Should -Be 8080
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'network.port-mappings.v1' -and $Payload.Name -eq 'Ubuntu' -and $Payload.Protocol -eq 'TCP' }
        }
    }
    It 'does not contain direct host execution' {
        $command = (Get-Command Get-DistroNexusPortMapping).ScriptBlock.ToString()
        $command | Should -Not -Match '(?i)\bwsl\b|\bnetsh\b|ConvertFrom-SsLine'
    }
}
