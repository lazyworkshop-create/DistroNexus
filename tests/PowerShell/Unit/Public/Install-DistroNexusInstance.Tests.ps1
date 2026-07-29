BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $rootPath 'src\PowerShell\DistroNexus.psd1') -Force
}

Describe 'Verified install module contract' -Tag 'Unit', 'Public', 'Install' {
    It 'uses the fixed source and acquisition routes only' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { $Payload }

            Get-DistroNexusInstallSource -PackageId ubuntu | Out-Null
            Get-DistroNexusPackageAcquisitionPreview -PackageId ubuntu | Out-Null
            Invoke-DistroNexusPackageAcquisition -PreviewToken ('a' * 64) -Confirm:$false | Out-Null

            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'install.source.resolve.v1' -and $Payload.PackageId -eq 'ubuntu' }
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'package.acquire.preview.v1' -and $Payload.PackageId -eq 'ubuntu' }
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'package.acquire.execute.v1' -and $Payload.PreviewToken -eq ('a' * 64) }
        }
    }

    It 'does not create a grant or route call when WhatIf declines installation' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not be called' }

            $result = Install-DistroNexusInstance -PackageReference ('a' * 64) -Name Ubuntu -InstallRoot 'D:\WSL' -Username developer -Shell bash -WhatIf

            $result.OutcomeCode | Should -Be 'WhatIf'
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
        }
    }

    It 'creates a fixed preview and then executes only the returned token' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge {
                if ($Operation -eq 'install.target.preview.v1') { return [pscustomobject]@{ IsEligible = $true; PreviewToken = ('c' * 64) } }
                if ($Operation -eq 'instance.install.preview.v1') { return [pscustomobject]@{ PreviewToken = ('b' * 64) } }
                return [pscustomobject]@{ Succeeded = $true; Operation = 'Install'; InstanceName = 'Ubuntu'; OutcomeCode = 'Lifecycle.Succeeded' }
            }

            $result = Install-DistroNexusInstance -PackageReference ('a' * 64) -Name Ubuntu -InstallRoot 'D:\WSL' -Username developer -Shell bash -SetAsDefault -Confirm:$false

            $result.Succeeded | Should -BeTrue
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'install.target.preview.v1' -and $Payload.InstallRoot -eq 'D:\WSL' -and $Payload.Count -eq 1 }
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'instance.install.preview.v1' -and $Payload.PackageReference -eq ('a' * 64) -and $Payload.Name -eq 'Ubuntu' -and $Payload.TargetPreviewToken -eq ('c' * 64) -and $Payload.Username -eq 'developer' -and $Payload.Shell -eq 'bash' -and $Payload.SetAsDefault }
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'instance.install.execute.v1' -and $Payload.PreviewToken -eq ('b' * 64) -and $Payload.Count -eq 1 }
        }
    }
}
