Describe 'DistroNexus catalog source bridge commands' -Tag 'Unit', 'Public' {
    BeforeAll {
        $script:rootPath = Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent
        $script:modulePath = Join-Path $script:rootPath 'src\PowerShell\DistroNexus.psd1'
        Import-Module $script:modulePath -Force
    }

    It 'maps every manager operation to a fixed versioned bridge route' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { $Payload }

            Get-DistroNexusCatalogSource | Out-Null
            Get-DistroNexusDefaultCatalogSource | Out-Null
            Add-DistroNexusCatalogSource -Name 'Test' -Url 'https://example.test/catalog.json' -Confirm:$false | Out-Null
            Set-DistroNexusCatalogSource -SourceId 'source-1' -Name 'Updated' -Url 'https://example.test/updated.json' -Confirm:$false | Out-Null
            Remove-DistroNexusCatalogSource -SourceId 'source-1' -Confirm:$false | Out-Null
            Test-DistroNexusCatalogSource -Url 'https://example.test/catalog.json' | Out-Null
            Set-DistroNexusCatalogSourceActive -SourceId 'source-1' -IsActive:$false -Confirm:$false | Out-Null
            Set-DistroNexusCatalogSourceOrder -SourceId @('source-2', 'source-1') -Confirm:$false | Out-Null
            Reset-DistroNexusCatalogSource -Confirm:$false | Out-Null

            @('catalog-source.list.v1', 'catalog-source.defaults.get.v1', 'catalog-source.add.v1', 'catalog-source.update.v1', 'catalog-source.remove.v1', 'catalog-source.test.v1', 'catalog-source.active.set.v1', 'catalog-source.reorder.v1', 'catalog-source.defaults.reset.v1') |
                ForEach-Object {
                    $route = $_
                    Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq $route }
                }
        }
    }

    It 'sends only the modeled source fields to mutation routes' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { $Payload }

            Add-DistroNexusCatalogSource -Name 'Test' -Url 'https://example.test/catalog.json' -Description 'Description' -IsActive:$false -Confirm:$false | Out-Null
            Set-DistroNexusCatalogSourceOrder -SourceId @('source-2', 'source-1') -Confirm:$false | Out-Null

            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter {
                $Operation -eq 'catalog-source.add.v1' -and $Payload.Name -eq 'Test' -and $Payload.Url -eq 'https://example.test/catalog.json' -and $Payload.IsActive -eq $false -and $Payload.PSObject.Properties.Name -notcontains 'Script'
            }
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter {
                $Operation -eq 'catalog-source.reorder.v1' -and @($Payload.SourceIds).Count -eq 2 -and $Payload.PSObject.Properties.Name -notcontains 'Operation'
            }
        }
    }

    It 'does not invoke a mutation route under WhatIf or declined confirmation' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'Bridge must not be called.' }

            Add-DistroNexusCatalogSource -Name 'Test' -Url 'https://example.test/catalog.json' -WhatIf | Should -BeFalse
            Set-DistroNexusCatalogSource -SourceId 'source-1' -Name 'Test' -Url 'https://example.test/catalog.json' -WhatIf | Should -BeFalse
            Remove-DistroNexusCatalogSource -SourceId 'source-1' -WhatIf | Should -BeFalse
            Set-DistroNexusCatalogSourceActive -SourceId 'source-1' -IsActive:$false -WhatIf | Should -BeFalse
            Set-DistroNexusCatalogSourceOrder -SourceId 'source-1' -WhatIf | Should -BeFalse
            Reset-DistroNexusCatalogSource -WhatIf | Should -BeFalse

            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
        }

        $declinedCommands = @(
            "Add-DistroNexusCatalogSource -Name Test -Url https://example.test/catalog.json -Confirm",
            "Set-DistroNexusCatalogSource -SourceId source-1 -Name Test -Url https://example.test/catalog.json -Confirm",
            "Remove-DistroNexusCatalogSource -SourceId source-1 -Confirm",
            "Set-DistroNexusCatalogSourceActive -SourceId source-1 -IsActive:`$false -Confirm",
            "Set-DistroNexusCatalogSourceOrder -SourceId source-1 -Confirm",
            "Reset-DistroNexusCatalogSource -Confirm"
        )
        foreach ($command in $declinedCommands) {
            $declinedOutput = 'N' | & pwsh -NoProfile -Command "& { Import-Module '$script:modulePath' -Force -DisableNameChecking; `$env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH = 'invalid'; $command }" 2>&1
            ($declinedOutput | Out-String) | Should -Not -Match 'WorkspaceBridgeUnavailable'
        }
    }

    It 'rejects invalid public mutation input before bridge invocation' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'Bridge must not be called.' }

            { Add-DistroNexusCatalogSource -Name '' -Url 'https://example.test/catalog.json' -Confirm:$false } | Should -Throw
            { Set-DistroNexusCatalogSource -SourceId '' -Name 'Test' -Url 'https://example.test/catalog.json' -Confirm:$false } | Should -Throw
            { Remove-DistroNexusCatalogSource -SourceId '' -Confirm:$false } | Should -Throw
            { Set-DistroNexusCatalogSourceActive -SourceId '' -IsActive:$true -Confirm:$false } | Should -Throw
            { Set-DistroNexusCatalogSourceOrder -SourceId @() -Confirm:$false } | Should -Throw
            { Reset-DistroNexusCatalogSource -Unexpected } | Should -Throw

            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
        }
    }

    It 'propagates bridge failures unchanged for every mutation command' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'Workspace.Bridge.Invalid: catalog source failure' }

            { Add-DistroNexusCatalogSource -Name 'Test' -Url 'https://example.test/catalog.json' -Confirm:$false } | Should -Throw '*Workspace.Bridge.Invalid: catalog source failure*'
            { Set-DistroNexusCatalogSource -SourceId 'source-1' -Name 'Test' -Url 'https://example.test/catalog.json' -Confirm:$false } | Should -Throw '*Workspace.Bridge.Invalid: catalog source failure*'
            { Remove-DistroNexusCatalogSource -SourceId 'source-1' -Confirm:$false } | Should -Throw '*Workspace.Bridge.Invalid: catalog source failure*'
            { Set-DistroNexusCatalogSourceActive -SourceId 'source-1' -IsActive:$true -Confirm:$false } | Should -Throw '*Workspace.Bridge.Invalid: catalog source failure*'
            { Set-DistroNexusCatalogSourceOrder -SourceId 'source-1' -Confirm:$false } | Should -Throw '*Workspace.Bridge.Invalid: catalog source failure*'
            { Reset-DistroNexusCatalogSource -Confirm:$false } | Should -Throw '*Workspace.Bridge.Invalid: catalog source failure*'
        }
    }

    AfterAll {
        Remove-Module DistroNexus -Force
    }
}
