@{
    # Script module or binary module file associated with this manifest.
    RootModule = 'DistroNexus.psm1'

    # Version number of this module.
    ModuleVersion = '2.1.0'

    # ID used to uniquely identify this module
    GUID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'

    # Author of this module
    Author = 'DistroNexus Team'

    # Company or vendor of this module
    CompanyName = 'LazyWorkshop'

    # Copyright statement for this module
    Copyright = '(c) 2026 DistroNexus Team. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'PowerShell module for managing Windows Subsystem for Linux (WSL) distributions with DistroNexus.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'

    # Functions to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no functions to export.
    FunctionsToExport = @(
        'Get-DistroNexusInstance',
        'Start-DistroNexusInstance',
        'Stop-DistroNexusInstance',
        'Get-DistroNexusTemplate',
        'Apply-DistroNexusTemplate',
        'Move-DistroNexusInstance',
        'Rename-DistroNexusInstance',
        'Remove-DistroNexusInstance',
        'Install-DistroNexusInstance',
        'Set-DistroNexusCredential',
        'Get-DistroNexusPackage',
        'Save-DistroNexusPackage',
        'Remove-DistroNexusPackage',
        'Update-DistroNexusCatalog',
        'Invoke-DistroNexusTemplateAutomation'
    )

    # Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no cmdlets to export.
    CmdletsToExport = @()

    # Variables to export from this module
    VariablesToExport = @()

    # Aliases to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no aliases to export.
    AliasesToExport = @()

    # Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
    PrivateData = @{
        PSData = @{
            # Tags applied to this module. These help with module discovery in online galleries.
            Tags = @('WSL', 'Windows', 'Linux', 'Distro', 'Management')

            # A URL to the license for this module.
            LicenseUri = 'https://github.com/LazyWorkshop-Create/DistroNexus/blob/main/LICENSE'

            # A URL to the main website for this project.
            ProjectUri = 'https://github.com/LazyWorkshop-Create/DistroNexus'

            # ReleaseNotes of this module
            ReleaseNotes = 'Version 2.1.0 - Template workflow hardening, CI reliability improvements, and release-governance alignment'
        }
    }
}
