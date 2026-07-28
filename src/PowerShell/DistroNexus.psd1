@{
    # Script module or binary module file associated with this manifest.
    RootModule = 'DistroNexus.psm1'

    # Version number of this module.
    ModuleVersion = '2.3.0'

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
        'Get-DistroNexusPackageCacheLocation',
        'Get-DistroNexusPackageCacheUsage',
        'Get-DistroNexusTerminalStatus',
        'Start-DistroNexusTerminal',
        'Open-DistroNexusPackageCacheFolder',
        'Clear-DistroNexusPackageCache',
        'Update-DistroNexusCatalog',
        'Invoke-DistroNexusTemplateAutomation',
        'Test-DistroNexusTemplateEnvironment',
        'Test-DistroNexusTemplateMetadata',
        'New-DistroNexusReleaseEvidenceBundle',
        'Compress-DistroNexusInstance',
        'Get-DistroNexusDockerIntegration',
        'Get-DistroNexusDockerIntegrationPreview',
        'Set-DistroNexusDockerIntegration',
        'Get-DistroNexusContainerRuntimeStatus',
        'Get-DistroNexusCapability',
        'Get-DistroNexusSystemdService',
        'Get-DistroNexusSystemdServiceDetail',
        'Get-DistroNexusSystemdServiceJournal',
        'Get-DistroNexusSystemdServicePreview',
        'Invoke-DistroNexusSystemdService',
        'Start-DistroNexusSystemdService',
        'Stop-DistroNexusSystemdService',
        'Restart-DistroNexusSystemdService',
        'Enable-DistroNexusSystemdService',
        'Disable-DistroNexusSystemdService',
        'Reload-DistroNexusSystemdService',
        'Get-DistroNexusWslgApplication',
        'Get-DistroNexusWslgStatus',
        'Start-DistroNexusWslgApplication',
        'Show-DistroNexusWslgApplicationEntry',
        'Set-DistroNexusWslgApplicationPin',
        'Get-DistroNexusRecoveryPoint',
        'Test-DistroNexusRecoveryPoint',
        'Get-DistroNexusRecoveryPointCreatePreview',
        'New-DistroNexusRecoveryPoint',
        'Get-DistroNexusRecoveryPointRestorePreview',
        'Restore-DistroNexusRecoveryPoint',
        'Get-DistroNexusRecoveryPointRemovePreview',
        'Remove-DistroNexusRecoveryPoint',
        'Get-DistroNexusRecoveryPointHistory',
        'Get-DistroNexusRecoveryPointRetention',
        'Get-DistroNexusRecoveryPointRetentionPreview',
        'Set-DistroNexusRecoveryPointRetention',
        'Set-DistroNexusRecoveryPointMetadata',
        'Get-DistroNexusRecoveryPointClonePreview',
        'Copy-DistroNexusRecoveryPoint',
        'Open-DistroNexusRecoveryPointFolder',
        'Get-DistroNexusMonitoringSnapshot',
        'Get-DistroNexusMonitoringProcessActionPreview',
        'Invoke-DistroNexusMonitoringProcessAction',
        'Invoke-DistroNexusHealthScan',
        'Get-DistroNexusHealthHistory',
        'Get-DistroNexusDiagnosticLogOption',
        'Get-DistroNexusHealthRepairPreview',
        'Repair-DistroNexusHealthFinding',
        'Get-DistroNexusDiagnosticReportPreview',
        'Export-DistroNexusDiagnosticReport',
        'Get-DistroNexusPodmanUserUnitPreview',
        'Invoke-DistroNexusPodmanUserUnit',
        'Get-DistroNexusPodmanConnectionPreview',
        'Invoke-DistroNexusPodmanConnection',
        'Enable-DistroNexusDockerIntegration',
        'Disable-DistroNexusDockerIntegration',
        'Export-DistroNexusInstance',
        'Import-DistroNexusInstance',
        'Get-DistroNexusWslConfig',
        'Open-DistroNexusWslConfigFile',
        'Set-DistroNexusWslConfig',
        'Get-DistroNexusInstanceConfig',
        'Get-DistroNexusInstanceResources',
        'Get-DistroNexusInstanceSparsePreview',
        'Set-DistroNexusInstanceSparseMode',
        'New-DistroNexusBackupSchedule',
        'Remove-DistroNexusBackupSchedule',
        'Get-DistroNexusBackupSchedule',
        'Invoke-DistroNexusBackup',
        'Get-DistroNexusPortMapping',
        'Get-DistroNexusNetworkStatus',
        'Get-DistroNexusInstanceIpAddress',
        'Test-DistroNexusNetworkProbe',
        'Get-DistroNexusNetworkMode',
        'Get-DistroNexusNetworkModePreview',
        'Set-DistroNexusNetworkMode',
        'Get-DistroNexusNetworkSettings',
        'Get-DistroNexusNetworkSettingsPreview',
        'Set-DistroNexusNetworkSettings',
        'Open-DistroNexusNetworkLoopback',
        'Get-DistroNexusFirewallRule',
        'Get-DistroNexusFirewallRuleCreatePreview',
        'New-DistroNexusFirewallRule',
        'Get-DistroNexusFirewallRuleRemovePreview',
        'Remove-DistroNexusFirewallRule',
        'Get-DistroNexusInstanceTag',
        'Set-DistroNexusInstanceTag',
        'Add-DistroNexusInstanceTag',
        'Remove-DistroNexusInstanceTag',
        'Rename-DistroNexusInstanceTags',
        'Get-DistroNexusCache',
        'Get-DistroNexusUsbDevice',
        'Connect-DistroNexusUsbDevice',
        'Disconnect-DistroNexusUsbDevice'
        ,'Get-DistroNexusSettings'
        ,'Set-DistroNexusSettings'
        ,'Reset-DistroNexusSettings'
        ,'Get-DistroNexusCatalogSource'
        ,'Add-DistroNexusCatalogSource'
        ,'Set-DistroNexusCatalogSource'
        ,'Remove-DistroNexusCatalogSource'
        ,'Test-DistroNexusCatalogSource'
        ,'Set-DistroNexusCatalogSourceActive'
        ,'Set-DistroNexusCatalogSourceOrder'
        ,'Get-DistroNexusDefaultCatalogSource'
        ,'Reset-DistroNexusCatalogSource'
        ,'Get-DistroNexusWorkspace'
        ,'Export-DistroNexusWorkspace'
        ,'New-DistroNexusWorkspace'
        ,'Set-DistroNexusWorkspace'
        ,'Copy-DistroNexusWorkspace'
        ,'Get-DistroNexusWorkspaceImportPreview'
        ,'Remove-DistroNexusWorkspace'
        ,'Import-DistroNexusWorkspace'
        ,'Get-DistroNexusWorkspaceLaunchPreview'
        ,'Approve-DistroNexusWorkspaceTrust'
        ,'Invoke-DistroNexusWorkspace'
        ,'Get-DistroNexusWorkspaceActionRetryPreview'
        ,'Retry-DistroNexusWorkspaceAction'
        ,'Get-DistroNexusTemplateSource'
        ,'Add-DistroNexusTemplateSource'
        ,'Set-DistroNexusTemplateSourceEnabled'
        ,'Remove-DistroNexusTemplateSource'
        ,'Approve-DistroNexusTemplateMarketplaceCandidate'
        ,'Get-DistroNexusTemplateMarketplaceReviewGrant'
        ,'Save-DistroNexusTemplateMarketplaceArtifact'
        ,'Get-DistroNexusTemplateMarketplaceArtifactHistory'
        ,'Get-DistroNexusTemplateMarketplaceScriptDiff'
        ,'Restore-DistroNexusTemplateMarketplaceArtifact'
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
            LicenseUri = 'https://github.com/LazyWorkshopCreate/DistroNexus/blob/main/LICENSE'

            # A URL to the main website for this project.
            ProjectUri = 'https://github.com/LazyWorkshopCreate/DistroNexus'

            # ReleaseNotes of this module
            ReleaseNotes = 'Version 2.3.0 release candidate - health, recovery, monitoring, workspaces, WSLg, containers, and trusted templates. External Windows/WSL and package acceptance gates remain open.'
        }
    }
}
