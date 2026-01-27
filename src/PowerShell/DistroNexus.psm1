# DistroNexus PowerShell Module
# Root module script with auto-loading of public/private functions

$ErrorActionPreference = 'Stop'

# Get module root path
$script:ModuleRoot = $PSScriptRoot

# Import private helper functions
$privateFunctions = @(Get-ChildItem -Path "$PSScriptRoot\Private\*.ps1" -ErrorAction SilentlyContinue)
foreach ($import in $privateFunctions) {
    try {
        . $import.FullName
        Write-Verbose "Imported private function: $($import.BaseName)"
    }
    catch {
        Write-Error "Failed to import function $($import.FullName): $_"
    }
}

# Import public cmdlet functions
$publicFunctions = @(Get-ChildItem -Path "$PSScriptRoot\Public\*.ps1" -ErrorAction SilentlyContinue)
foreach ($import in $publicFunctions) {
    try {
        . $import.FullName
        Write-Verbose "Imported public function: $($import.BaseName)"
    }
    catch {
        Write-Error "Failed to import function $($import.FullName): $_"
    }
}

# Export public functions
Export-ModuleMember -Function $publicFunctions.BaseName
