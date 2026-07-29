# DistroNexus PowerShell Module
# Root module script with auto-loading of public/private functions

$ErrorActionPreference = 'Stop'

# Get module root path
$script:ModuleRoot = $PSScriptRoot

# Set the project root path
# In release: PowerShell module is at <root>/PowerShell, config is at <root>/config
# In development: PowerShell module is at src/PowerShell, config is at config (two levels up)
$parentDir = Split-Path $script:ModuleRoot -Parent
if (Test-Path (Join-Path $parentDir "config")) {
    # Release environment: config folder is sibling to PowerShell folder
    $script:ProjectRoot = $parentDir
} else {
    # Development environment: go two levels up
    $script:ProjectRoot = Split-Path $parentDir -Parent
}

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

# Workspace cmdlets are intentionally Core-owned. The bridge is resolved when a bridge-backed
# cmdlet runs so module-only commands remain usable in installations without bridge artifacts.
$ExecutionContext.SessionState.Module.OnRemove = { Stop-DistroNexusWorkspaceBridge }

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

# Export exactly the manifest contract. Some public files contain related commands,
# so exporting file base names would omit commands defined in composite files.
$manifest = Import-PowerShellDataFile -Path (Join-Path $PSScriptRoot 'DistroNexus.psd1')
Export-ModuleMember -Function $manifest.FunctionsToExport
