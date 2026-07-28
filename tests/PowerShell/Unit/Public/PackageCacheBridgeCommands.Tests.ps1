Describe 'Package-cache bridge command contract' -Tag 'Unit', 'Public' {
    BeforeAll {
        $root = Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent
        Import-Module (Join-Path $root 'src\PowerShell\DistroNexus.psd1') -Force
    }

    It 'exports the fixed cache command family' {
        'Get-DistroNexusPackageCacheLocation', 'Get-DistroNexusPackageCacheUsage', 'Remove-DistroNexusPackage', 'Clear-DistroNexusPackageCache' | ForEach-Object {
            Get-Command $_ -Module DistroNexus | Should -Not -BeNullOrEmpty
        }
    }

    It 'maps read and token deletion requests to fixed routes' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ CacheEntryId = 'token' } }
            Get-DistroNexusPackageCacheLocation | Out-Null
            Get-DistroNexusPackageCacheUsage | Out-Null
            Remove-DistroNexusPackage -CacheEntryId 'token' -Confirm:$false | Out-Null
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'package-cache.location.v1' }
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'package-cache.usage.v1' }
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'package-cache.delete.v1' -and $Payload.CacheEntryId -eq 'token' }
        }
    }

    It 'does not invoke cache mutation routes for WhatIf' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'bridge should not run' }
            Remove-DistroNexusPackage -CacheEntryId 'token' -WhatIf
            Clear-DistroNexusPackageCache -WhatIf
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
        }
    }

    It 'does not invoke delete or clear when confirmation is declined' {
        $root = Split-Path (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent) -Parent
        $modulePath = Join-Path $root 'src\PowerShell\DistroNexus.psd1'
        $script = "Import-Module '$modulePath' -Force; & (Get-Module DistroNexus) { function Invoke-DistroNexusWorkspaceBridge { exit 9 }; Remove-DistroNexusPackage -CacheEntryId token -Confirm; Clear-DistroNexusPackageCache -Confirm }"
        'N', 'N' | & pwsh -NoProfile -Command $script | Out-Null
        $LASTEXITCODE | Should -Be 0
    }

    It 'sends compatibility paths only as modeled selectors, never as token authority' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'PackageCache.EntryInvalid' }
            { Remove-DistroNexusPackage -LocalPath 'C:\outside\entry.wsl' -Confirm:$false } | Should -Throw '*PackageCache.EntryInvalid*'
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter {
                $Operation -eq 'package-cache.delete.v1' -and $Payload.LocalPath -eq 'C:\outside\entry.wsl' -and -not $Payload.CacheEntryId
            }
        }
    }

    It 'maps DefaultName as a modeled bridge selector' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ Deleted = $true } }
            Remove-DistroNexusPackage -DefaultName 'ubuntu' -Confirm:$false | Out-Null
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter {
                $Operation -eq 'package-cache.delete.v1' -and $Payload.DefaultName -eq 'ubuntu' -and -not $Payload.CacheEntryId
            }
        }
    }

    It 'propagates bridge failures' {
        InModuleScope DistroNexus {
            Mock Invoke-DistroNexusWorkspaceBridge { throw 'PackageCache.EntryInvalid' }
            { Remove-DistroNexusPackage -CacheEntryId 'token' -Confirm:$false } | Should -Throw '*PackageCache.EntryInvalid*'
        }
    }
}
