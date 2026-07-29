BeforeAll {
    $script:rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $script:rootPath 'src\PowerShell\DistroNexus.psd1') -Force
}

Describe 'Global settings bridge routing and consent' -Tag 'Unit', 'Public', 'Settings' {
    It 'maps get to its fixed versioned bridge operation' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Theme = 'Auto' } }

            Get-DistroNexusSettings | Should -Not -BeNullOrEmpty

            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'settings.get.v1' }
        }
    }

    It 'maps modeled updates to the fixed save operation without accepting arbitrary JSON' {
        InModuleScope DistroNexus {
            Mock Get-DistroNexusSettings { [pscustomobject]@{ DefaultInstallPath='C:\\WSL'; PackageCachePath=''; TerminalStartPath='~'; DefaultWslVersion=2; DefaultUsername='root'; DefaultDistributionId=''; EnableLogging=$true; LogPath=''; CheckUpdatesOnStartup=$true; CatalogUrl='https://example.test/catalog.json'; Theme='Auto'; Language='en-US'; ShowConfirmationDialogs=$true; MaxConcurrentDownloads=3; AutoRetryDownloads=$true; MaxRetryAttempts=3; AutoSaveEnabled=$true; AutoSaveInterval=30; CustomData=@{}; PowerShellModulePath=$null; LocalhostForwardingHealthEndpoint='' } }
            Mock Invoke-DistroNexusWorkspaceBridge { $Payload.Settings }

            $saved = Set-DistroNexusSettings -DefaultInstallPath 'D:\\WSL' -Theme Dark -Confirm:$false

            $saved.DefaultInstallPath | Should -Be 'D:\\WSL'
            $saved.Theme | Should -Be 'Dark'
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'settings.save.v1' -and $Payload.Settings.DefaultInstallPath -eq 'D:\\WSL' -and $Payload.Settings.Theme -eq 'Dark' }
        }
    }

    It 'does not invoke get or save under WhatIf' {
        InModuleScope DistroNexus {
            Mock Get-DistroNexusSettings { throw 'Settings must not be read.' }
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'Bridge must not be called.' }

            Set-DistroNexusSettings -Theme Dark -WhatIf | Should -BeFalse

            Should -Invoke Get-DistroNexusSettings -Times 0 -Exactly
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
        }
    }

    It 'maps reset to its fixed route and does not invoke it under WhatIf' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Theme = 'Auto' } }

            Reset-DistroNexusSettings -Confirm:$false | Should -Not -BeNullOrEmpty
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'settings.reset.v1' }

            Reset-DistroNexusSettings -WhatIf | Should -BeFalse
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'settings.reset.v1' }
        }
    }

    It 'does not invoke save or reset when confirmation is declined' {
        $modulePath = Join-Path $script:rootPath 'src\PowerShell\DistroNexus.psd1'
        $setOutput = 'N' | & pwsh -NoProfile -Command "& { Import-Module '$modulePath' -Force -DisableNameChecking; `$env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH = 'invalid'; Set-DistroNexusSettings -Theme Dark -Confirm }" 2>&1
        $resetOutput = 'N' | & pwsh -NoProfile -Command "& { Import-Module '$modulePath' -Force -DisableNameChecking; `$env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH = 'invalid'; Reset-DistroNexusSettings -Confirm }" 2>&1

        ($setOutput | Out-String) | Should -Not -Match 'WorkspaceBridgeUnavailable'
        ($resetOutput | Out-String) | Should -Not -Match 'WorkspaceBridgeUnavailable'
    }
}
