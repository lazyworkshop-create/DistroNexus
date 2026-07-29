BeforeAll { Import-Module (Join-Path (Resolve-Path "$PSScriptRoot/../../../..") 'src/PowerShell/DistroNexus.psd1') -Force }
Describe 'Docker Desktop install URI contract' -Tag 'Unit','Public' {
    It 'uses only the fixed empty payload' { InModuleScope DistroNexus { Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Uri='https://www.docker.com/products/docker-desktop/'; OutcomeCode='ExternalUri.Ready' } }; Get-DistroNexusDockerDesktopInstallUri | Out-Null; Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'external.docker-desktop-install-uri.v1' -and $Payload.Count -eq 0 } } }
}
