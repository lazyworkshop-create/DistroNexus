function Get-DistroNexusTemplate {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param (
        [Parameter(ValueFromPipeline = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Id,

        [Parameter(ValueFromPipelineByPropertyName = $true)]
        [string]$Category
    )

    process {
        # Determine config path. Try multiple locations relative to module.
        $possiblePaths = @(
            (Join-Path $PSScriptRoot "..\..\..\config\templates.json"), # Dev source
            (Join-Path $PSScriptRoot "..\config\templates.json")        # Released structure
        )

        $configPath = $null
        foreach ($p in $possiblePaths) {
            $p = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($p)
            if (Test-Path $p) {
                $configPath = $p
                break
            }
        }

        if (-not $configPath) {
            Write-Verbose "Template configuration not found."
            return
        }

        try {
            $json = Get-Content -Path $configPath -Raw | ConvertFrom-Json
            
            foreach ($t in $json) {
                if ($PSBoundParameters.ContainsKey('Id') -and $t.Id -ne $Id) { continue }
                if ($PSBoundParameters.ContainsKey('Category') -and $t.Category -ne $Category) { continue }
                
                Write-Output $t
            }
        }
        catch {
            Write-Error "Failed to load DistroNexus templates: $_"
        }
    }
}
