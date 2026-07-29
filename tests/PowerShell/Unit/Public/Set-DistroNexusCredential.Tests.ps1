BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $rootPath 'src\PowerShell\DistroNexus.psd1') -Force
}

Describe 'Set-DistroNexusCredential error contract' -Tag 'Unit', 'Public', 'Credential' {
    It 'preserves <Code> and does not expose the failing bridge detail' -ForEach @(
        @{ Code = 'Lifecycle.CredentialInvalid' }
        @{ Code = 'Lifecycle.CredentialGrantInvalid' }
        @{ Code = 'Lifecycle.CredentialGrantExpired' }
        @{ Code = 'Lifecycle.CredentialStateChanged' }
        @{ Code = 'Lifecycle.CredentialFailed' }
    ) {
        InModuleScope DistroNexus -Parameters @{ StableCode = $Code } {
            param($StableCode)
            $password = [Security.SecureString]::new()
            'p@ss-plain-text' | ForEach-Object { $_.ToCharArray() | ForEach-Object { $password.AppendChar($_) } }
            $password.MakeReadOnly()
            Mock Invoke-DistroNexusWorkspaceBridge { throw "${StableCode}: C:\\private\\credential-input" }

            $errorRecord = $null
            try { Set-DistroNexusCredential -Name Ubuntu -Username developer -Password $password -Confirm:$false } catch { $errorRecord = $_ }

            $errorRecord | Should -Not -BeNullOrEmpty
            ($errorRecord.FullyQualifiedErrorId -split ',')[0] | Should -Be $StableCode
            $errorRecord.Exception.Message | Should -Be $StableCode
            $errorRecord.ToString() | Should -Not -Match 'private|credential-input|p@ss-plain-text'
            Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter {
                $Operation -eq 'instance.credential.preview.v1' -and
                -not $Payload.Contains('p@ss-plain-text') -and
                -not $Payload.Contains('Password')
            }
        }
    }
}
