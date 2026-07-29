Describe 'Update-DistroNexusCatalog bridge contract' -Tag 'Unit', 'Public' {
    BeforeAll {
        $root = Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent
        Import-Module (Join-Path $root 'src\PowerShell\DistroNexus.psd1') -Force
    }

    It 'uses the fixed refresh route' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Succeeded = $true } }
            Update-DistroNexusCatalog -SourceUrl 'https://example.test/catalog.json' -Confirm:$false | Out-Null
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter {
                $Operation -eq 'catalog.refresh.v1' -and $Payload.SourceUrl -eq 'https://example.test/catalog.json'
            }
        }
    }

    It 'does not invoke the bridge for WhatIf or a declined confirmation' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'bridge should not run' }
            Update-DistroNexusCatalog -WhatIf | Should -BeFalse
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
        }
    }

    It 'does not invoke the bridge when confirmation is declined' {
        $root = Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent
        $modulePath = Join-Path $root 'src\PowerShell\DistroNexus.psd1'
        $script = "Import-Module '$modulePath' -Force; & (Get-Module DistroNexus) { function Invoke-DistroNexusWorkspaceBridge { exit 9 }; Update-DistroNexusCatalog -Confirm }"
        'N' | & pwsh -NoProfile -Command $script | Out-Null
        $LASTEXITCODE | Should -Be 0
    }

    It 'rejects invalid source URLs before bridge invocation' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'bridge should not run' }
            { Update-DistroNexusCatalog -SourceUrl 'https://user@example.test/catalog.json' -Confirm:$false } | Should -Throw '*Catalog source URL is invalid*'
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
        }
    }

    It 'propagates a bridge failure' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'Catalog.RefreshFailed' }
            { Update-DistroNexusCatalog -Confirm:$false } | Should -Throw '*Catalog.RefreshFailed*'
        }
    }
}
