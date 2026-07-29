Describe 'Get-DistroNexusContainerRuntimeStatus' -Tag 'Unit', 'Public', 'Container' {
    BeforeAll { function Invoke-DistroNexusWorkspaceBridge { param($Operation, $Payload) [pscustomobject]@{ InstanceName=$Payload.InstanceName; ReadOnly=$true; Runtimes=@() } }; . (Join-Path $PSScriptRoot '..\..\..\..\src\PowerShell\Public\Get-DistroNexusContainerRuntimeStatus.ps1') }
    It 'routes complete read-only runtime fields to the Core bridge model' {
        Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ InstanceName='Ubuntu'; ReadOnly=$true; Runtimes=@([pscustomobject]@{ Kind='PodmanWsl'; Health='healthy' }); Containers=@{ PodmanWsl=@([pscustomobject]@{ Name='web'; Image='nginx'; State='running' }) }; Images=@{ PodmanWsl=@([pscustomobject]@{ Repository='nginx'; Tag='latest' }) }; Projects=@{ PodmanWsl=@([pscustomobject]@{ Name='web'; Status='running'; ServiceCount=1 }) }; Failures=@{ PodmanDesktop='DN-8101' } } }
        $result = Get-DistroNexusContainerRuntimeStatus -Name Ubuntu
        $result.ReadOnly | Should -BeTrue
        $result.Runtimes[0].Kind | Should -Be 'PodmanWsl'
        $result.Containers.PodmanWsl[0].Image | Should -Be 'nginx'
        $result.Images.PodmanWsl[0].Tag | Should -Be 'latest'
        $result.Projects.PodmanWsl[0].ServiceCount | Should -Be 1
        $result.Failures.PodmanDesktop | Should -Be 'DN-8101'
        Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'containerRuntimeStatus' -and $Payload.InstanceName -eq 'Ubuntu' }
    }
}
