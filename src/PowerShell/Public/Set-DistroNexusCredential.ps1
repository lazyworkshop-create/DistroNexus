function Set-DistroNexusCredential {
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Name,
        [Parameter(Mandatory)][ValidatePattern('^[a-z_][a-z0-9_-]{0,31}$')][string]$Username,
        [Parameter(Mandatory)][SecureString]$Password
    )

    # This is an opaque CurrentUser-DPAPI envelope. It is never written to logs, output, command
    # text, or a durable model; the Bridge decrypts it only while executing its fixed operation.
    if (-not $PSCmdlet.ShouldProcess($Name, 'Set WSL credentials')) {
        return [pscustomobject]@{ Succeeded = $false; InstanceName = $Name; OutcomeCode = 'Lifecycle.CredentialDeclined' }
    }

    $envelope = $null
    try {
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
        try {
            $secretBytes = [Text.Encoding]::UTF8.GetBytes([Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer))
            try { $envelope = [Convert]::ToBase64String([Security.Cryptography.ProtectedData]::Protect($secretBytes, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)) }
            finally { [Array]::Clear($secretBytes, 0, $secretBytes.Length) }
        }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
        $preview = Invoke-DistroNexusWorkspaceBridge -Operation 'instance.credential.preview.v1' -Payload ([ordered]@{ Name = $Name; Username = $Username; SecretEnvelope = $envelope })
        $result = Invoke-DistroNexusWorkspaceBridge -Operation 'instance.credential.execute.v1' -Payload ([ordered]@{ PreviewToken = $preview.PreviewToken })
        return [pscustomobject]@{ Succeeded = [bool]$result.Succeeded; InstanceName = $Name; OutcomeCode = [string]$result.OutcomeCode }
    }
    catch {
        $message = [string]$_.Exception.Message
        $stableCode = [regex]::Match($message, '(?<![A-Za-z0-9.])(Lifecycle\.Credential(?:Invalid|GrantInvalid|GrantExpired|StateChanged|Failed))(?![A-Za-z0-9.])').Value
        if ([string]::IsNullOrWhiteSpace($stableCode)) { $stableCode = 'Lifecycle.CredentialFailed' }
        $exception = [System.InvalidOperationException]::new($stableCode)
        $PSCmdlet.ThrowTerminatingError([System.Management.Automation.ErrorRecord]::new($exception, $stableCode, [System.Management.Automation.ErrorCategory]::OperationStopped, $Name))
    }
    finally {
        $envelope = $null
        $Password.Dispose()
    }
}
