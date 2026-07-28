function Update-DistroNexusCatalog {
    <# .SYNOPSIS Refreshes the catalog through the fixed WorkspaceBridge contract. #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([ValidateLength(1, 2048)][string]$SourceUrl)
    process {
        if ($PSBoundParameters.ContainsKey('SourceUrl')) {
            $uri = $null
            if (-not [uri]::TryCreate($SourceUrl, [System.UriKind]::Absolute, [ref]$uri) -or
                $uri.Scheme -notin @('http', 'https') -or [string]::IsNullOrWhiteSpace($uri.Host) -or
                -not [string]::IsNullOrEmpty($uri.UserInfo) -or -not [string]::IsNullOrEmpty($uri.Fragment)) {
                throw [System.ArgumentException]::new('Catalog source URL is invalid.', 'SourceUrl')
            }
        }
        if (-not $PSCmdlet.ShouldProcess('DistroNexus catalog', 'Refresh')) { return $false }
        $payload = @{}
        if ($PSBoundParameters.ContainsKey('SourceUrl')) { $payload.SourceUrl = $SourceUrl }
        Invoke-DistroNexusWorkspaceBridge -Operation 'catalog.refresh.v1' -Payload $payload
    }
}
