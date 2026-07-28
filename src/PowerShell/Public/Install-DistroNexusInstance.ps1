function Get-DistroNexusInstallSource {
    <# .SYNOPSIS Resolves the path-free verified source state for one catalog package. #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateLength(1, 128)][string]$PackageId)

    Invoke-DistroNexusWorkspaceBridge -Operation 'install.source.resolve.v1' -Payload @{ PackageId = $PackageId }
}

function Get-DistroNexusPackageAcquisitionPreview {
    <# .SYNOPSIS Creates a short-lived, reviewed acquisition preview for one catalog package. #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][ValidateLength(1, 128)][string]$PackageId)

    Invoke-DistroNexusWorkspaceBridge -Operation 'package.acquire.preview.v1' -Payload @{ PackageId = $PackageId }
}

function Invoke-DistroNexusPackageAcquisition {
    <# .SYNOPSIS Acquires exactly the package authorized by a preview token. #>
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PreviewToken)

    if (-not $PSCmdlet.ShouldProcess('reviewed package acquisition', 'Acquire')) {
        return [pscustomobject]@{ PackageReference = $null; OutcomeCode = 'WhatIf' }
    }
    Invoke-DistroNexusWorkspaceBridge -Operation 'package.acquire.execute.v1' -Payload @{ PreviewToken = $PreviewToken }
}

function Install-DistroNexusInstance {
    <#
    .SYNOPSIS Installs exactly one already-verified package reference.

    .DESCRIPTION
    This command never discovers local package paths and never downloads implicitly. Use
    Get-DistroNexusPackageAcquisitionPreview and Invoke-DistroNexusPackageAcquisition first.
    The optional password is converted only to a CurrentUser-DPAPI envelope for the fixed bridge
    route; no plaintext password or command text is created.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$PackageReference,
        [Parameter(Mandatory)][ValidatePattern('^[^\r\n\0]{1,256}$')][string]$Name,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$InstallRoot,
        [Parameter(Mandatory)][ValidatePattern('^[a-z_][a-z0-9_-]{0,31}$')][string]$Username,
        [Parameter(Mandatory)][ValidateSet('bash', 'zsh', 'fish', 'sh')][string]$Shell,
        [ValidateLength(1, 128)][string]$Locale,
        [switch]$SetAsDefault,
        [SecureString]$Password
    )

    if (-not $PSCmdlet.ShouldProcess($Name, 'Install verified WSL distribution')) {
        return [pscustomobject]@{ Succeeded = $false; Operation = 'Install'; InstanceName = $Name; OutcomeCode = 'WhatIf' }
    }

    $envelope = $null
    try {
        if ($Password) {
            $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
            try {
                $secretBytes = [Text.Encoding]::UTF8.GetBytes([Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer))
                try { $envelope = [Convert]::ToBase64String([Security.Cryptography.ProtectedData]::Protect($secretBytes, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)) }
                finally { [Array]::Clear($secretBytes, 0, $secretBytes.Length) }
            }
            finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
        }

        $payload = [ordered]@{ PackageReference = $PackageReference; Name = $Name; InstallRoot = $InstallRoot; Username = $Username; Shell = $Shell; Locale = $Locale; SetAsDefault = [bool]$SetAsDefault }
        if ($envelope) { $payload.SecretEnvelope = $envelope }
        $preview = Invoke-DistroNexusWorkspaceBridge -Operation 'instance.install.preview.v1' -Payload $payload
        Invoke-DistroNexusWorkspaceBridge -Operation 'instance.install.execute.v1' -Payload @{ PreviewToken = $preview.PreviewToken }
    }
    finally {
        $envelope = $null
        if ($Password) { $Password.Dispose() }
    }
}
