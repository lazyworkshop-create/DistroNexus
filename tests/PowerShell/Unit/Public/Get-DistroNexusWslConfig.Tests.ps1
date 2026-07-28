$rootPath = Resolve-Path "$PSScriptRoot/../../../.."
$global:DistroNexusGlobalConfigurationTestRoot = $rootPath
Import-Module (Join-Path $rootPath 'src\PowerShell\DistroNexus.psd1') -Force

function global:Invoke-DeclinedGlobalConfigurationCommand {
    param([Parameter(Mandatory)][string]$Command, [Parameter(Mandatory)][string]$Sentinel)
    $getPath = Join-Path $global:DistroNexusGlobalConfigurationTestRoot 'src\PowerShell\Public\Get-DistroNexusWslConfig.ps1'
    $setPath = Join-Path $global:DistroNexusGlobalConfigurationTestRoot 'src\PowerShell\Public\Set-DistroNexusWslConfig.ps1'
    $script = "`$sentinel = '$($Sentinel.Replace("'", "''"))'; function Invoke-DistroNexusWorkspaceBridge { New-Item -ItemType File -Force -Path `$sentinel | Out-Null; throw 'Bridge was called.' }; . '$getPath'; . '$setPath'; $Command -Confirm"
    'N' | & pwsh -NoProfile -Command "& { $script }" | Out-Null
    $LASTEXITCODE | Should -Be 0
}

Describe 'Global WSL configuration module contract' -Tag 'Unit', 'Public', 'WslConfig' {
    It 'exports the constrained read preview and execute commands' {
        'Get-DistroNexusWslConfig', 'Set-DistroNexusWslConfig', 'Get-DistroNexusGlobalConfiguration', 'Get-DistroNexusGlobalConfigurationPreview', 'Set-DistroNexusGlobalConfiguration' | ForEach-Object {
            Get-Command $_ -Module DistroNexus | Should -Not -BeNullOrEmpty
        }
    }

    InModuleScope DistroNexus {
        BeforeEach {
            Mock Invoke-DistroNexusWorkspaceBridge {
                [pscustomobject]@{ Values = @{ 'wsl2.memory' = '4GB'; 'wsl2.processors' = '2'; 'wsl2.swap' = '1GB'; 'wsl2.localhostForwarding' = 'true'; 'wsl2.networkingMode' = 'nat' } }
            }
        }

        It 'routes the legacy read facade to the fixed global read operation' {
            $value = Get-DistroNexusWslConfig
            $value.Memory | Should -Be '4GB'
            $value.ConfigPath | Should -BeNullOrEmpty
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'configuration.global.get.v1' -and $null -eq $Payload }
        }

        It 'rejects an empty preview map before opening a bridge operation' {
            { Get-DistroNexusGlobalConfigurationPreview -Changes @{} } | Should -Throw
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
        }

        It 'rejects unknown and malformed modeled changes before opening a bridge operation' {
            { Get-DistroNexusGlobalConfigurationPreview -Changes @{ 'custom.path' = 'C:\secret' } } | Should -Throw
            { Get-DistroNexusGlobalConfigurationPreview -Changes @{ 'wsl2.memory' = ("x" * 513) } } | Should -Throw
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
        }

        It 'uses WhatIf without creating a preview grant or write operation' {
            Set-DistroNexusWslConfig -Memory '4GB' -WhatIf | Out-Null
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 0
        }

        It 'declines direct public execute without any preview or execute bridge call' {
            $sentinel = Join-Path $TestDrive 'direct-bridge-called'
            Invoke-DeclinedGlobalConfigurationCommand -Command "Set-DistroNexusGlobalConfiguration -PreviewToken $('a' * 32)" -Sentinel $sentinel
            Test-Path -LiteralPath $sentinel | Should -BeFalse
        }

        It 'declines the legacy Set facade without any preview or execute bridge call' {
            $sentinel = Join-Path $TestDrive 'legacy-bridge-called'
            Invoke-DeclinedGlobalConfigurationCommand -Command "Set-DistroNexusWslConfig -Memory 4GB" -Sentinel $sentinel
            Test-Path -LiteralPath $sentinel | Should -BeFalse
        }

        It 'routes the legacy write facade through preview then token-only execute' {
            Mock Invoke-DistroNexusWorkspaceBridge {
                if ($Operation -eq 'configuration.global.preview.v1') { return [pscustomobject]@{ PreviewToken = ('a' * 32) } }
                return [pscustomobject]@{ PendingRestart = $true }
            }
            Set-DistroNexusWslConfig -Memory '4GB' -Confirm:$false | Out-Null
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'configuration.global.preview.v1' -and $Payload.Changes.'wsl2.memory' -eq '4GB' }
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'configuration.global.execute.v1' -and $Payload.PreviewToken -eq ('a' * 32) }
        }

        It 'forwards every modeled field only through the Changes map' {
            $changes = @{}; @('wsl2.memory','wsl2.processors','wsl2.swap','wsl2.swapFile','wsl2.pageReporting','wsl2.localhostForwarding','wsl2.networkingMode','wsl2.dnsTunneling','wsl2.firewall','wsl2.autoProxy','wsl2.hostAddressLoopback','wsl2.ignoredPorts','wsl2.bestEffortDnsParsing','wsl2.initialAutoProxyTimeout','wsl2.kernel','wsl2.kernelCommandLine','wsl2.nestedVirtualization','experimental.autoMemoryReclaim','experimental.sparseVhd') | ForEach-Object { $changes[$_] = 'x' }
            Get-DistroNexusGlobalConfigurationPreview -Changes $changes | Out-Null
            Assert-MockCalled Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'configuration.global.preview.v1' -and $Payload.Keys.Count -eq 1 -and $Payload.Changes.Keys.Count -eq 19 -and -not $Payload.ContainsKey('Fingerprint') -and -not $Payload.ContainsKey('Path') }
        }
    }
}
