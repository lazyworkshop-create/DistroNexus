---
sidebar_position: 5
---

# PowerShell Scripts Reference

This page documents the DistroNexus PowerShell module command surface.

The authoritative exported commands come from `src/PowerShell/DistroNexus.psd1` and include 36 cmdlets.

## Instance Management

### `Get-DistroNexusInstance`
List registered WSL instances and state metadata.

**Parameters**
- `-Name <string>`: Filter by instance name (supports wildcards).
- `-ForceUpdate`: Bypass cache and perform fresh scan.
- `-IncludeRelease`: Include Linux release info.
- `-IncludeUser`: Include default user info.
- `-SkipDiskSize`: Skip VHDX size checks for faster results.

**Examples**
```powershell
Get-DistroNexusInstance
Get-DistroNexusInstance -Name "Ubuntu*" -ForceUpdate
Get-DistroNexusInstance -IncludeRelease -IncludeUser
```

### `Start-DistroNexusInstance`
Start an instance.

**Parameters**
- `-Name <string>`: Instance name.

**Example**
```powershell
Start-DistroNexusInstance -Name "Ubuntu-24.04"
```

### `Stop-DistroNexusInstance`
Stop a running instance.

**Parameters**
- `-Name <string>`: Instance name.
- `-Force`: Skip confirmation.

**Examples**
```powershell
Stop-DistroNexusInstance -Name "Ubuntu-24.04"
Stop-DistroNexusInstance -Name "Ubuntu-24.04" -Force
```

### `Move-DistroNexusInstance`
Move an instance to another storage location.

**Parameters**
- `-Name <string>`: Instance name.
- `-Destination <string>`: Destination root path.

**Example**
```powershell
Move-DistroNexusInstance -Name "Ubuntu-24.04" -Destination "D:\\WSL"
```

### `Rename-DistroNexusInstance`
Rename an instance via export/import workflow.

**Parameters**
- `-Name <string>`: Current name.
- `-NewName <string>`: New name.

**Example**
```powershell
Rename-DistroNexusInstance -Name "Ubuntu-24.04" -NewName "Ubuntu-Dev"
```

### `Remove-DistroNexusInstance`
Unregister an instance and optionally keep files.

**Parameters**
- `-Name <string>`: Instance name.
- `-Force`: Skip confirmation.
- `-KeepFiles`: Unregister only; keep files on disk.

**Examples**
```powershell
Remove-DistroNexusInstance -Name "Ubuntu-Temp" -Force
Remove-DistroNexusInstance -Name "Ubuntu-Archive" -KeepFiles
```

### `Install-DistroNexusInstance`
Install a new instance from catalog and package cache/download source.

**Parameters**
- `-DistroName <string>`: Catalog distro identifier.
- `-InstallPath <string>`: Target installation path.
- `-InstanceName <string>`: Custom instance name.
- `-Username <string>`: Default username (default: `root`).
- `-Password <SecureString>`: Password for user.
- `-Interactive`: Interactive install mode.
- `-AutoDownload`: Download package if missing.
- `-OpenTerminal`: Open terminal after install.
- `-Shell <bash|zsh|fish|sh>`: Default shell.
- `-Locale <string>`: Locale (for example `en_US.UTF-8`).
- `-SetAsDefault`: Set as default WSL distro.

**Examples**
```powershell
Install-DistroNexusInstance -DistroName "Ubuntu-24.04" -InstallPath "D:\\WSL\\Ubuntu-24.04" -AutoDownload

$password = Read-Host -AsSecureString "Password"
Install-DistroNexusInstance -DistroName "Debian" -InstallPath "E:\\WSL\\Debian" -Username "admin" -Password $password -Shell "zsh"
```

### `Set-DistroNexusCredential`
Set default user and optional password for an existing instance.

**Parameters**
- `-Name <string>`: Instance name.
- `-Username <string>`: Username to configure.
- `-Password <SecureString>`: Optional password.

**Example**
```powershell
$password = Read-Host -AsSecureString "Password"
Set-DistroNexusCredential -Name "Ubuntu-24.04" -Username "admin" -Password $password
```

## Package and Catalog

### `Get-DistroNexusPackage`
List package catalog entries and cache state.

**Parameters**
- `-Family <string>`: Filter by distro family (for example `Ubuntu`).

**Example**
```powershell
Get-DistroNexusPackage -Family "Ubuntu"
```

### `Save-DistroNexusPackage`
Download packages to local cache (single/family/all modes).

**Parameters**
- `-DefaultName <string>`: Download one distro package.
- `-Family <string>`: Download packages by family.
- `-All`: Download all catalog packages.
- `-Destination <string>`: Override cache path.
- `-MaxConcurrent <int>`: Concurrent downloads (1-10).
- `-RetryCount <int>`: Retry attempts (0-10).
- `-ShowSpeed <bool>`: Show download speed.
- `-SkipExisting <bool>`: Skip existing files.

**Examples**
```powershell
Save-DistroNexusPackage -DefaultName "Ubuntu-24.04"
Save-DistroNexusPackage -Family "Ubuntu" -MaxConcurrent 5
Save-DistroNexusPackage -All -RetryCount 5
```

### `Remove-DistroNexusPackage`
Remove cached package file by default name or explicit path.

**Parameters**
- `-DefaultName <string>`: Remove by catalog default name.
- `-LocalPath <string>`: Remove by full file path.
- `-Force`: Skip confirmation.

**Examples**
```powershell
Remove-DistroNexusPackage -DefaultName "Ubuntu-24.04" -Force
Remove-DistroNexusPackage -LocalPath "D:\\WSL\\packages\\ubuntu-24.04.wsl" -Force
```

### `Update-DistroNexusCatalog`
Refresh local catalog from remote source.

**Parameters**
- `-SourceUrl <string>`: Override catalog URL.

**Example**
```powershell
Update-DistroNexusCatalog
```

## Template Commands

### `Get-DistroNexusTemplate`
Query built-in templates.

**Parameters**
- `-Id <string>`: Filter by template ID.
- `-Category <string>`: Filter by category.

**Examples**
```powershell
Get-DistroNexusTemplate
Get-DistroNexusTemplate -Category "Development"
```

### `Apply-DistroNexusTemplate`
Apply a template to a target instance.

**Parameters**
- `-InstanceName <string>`: Target WSL instance.
- `-TemplateId <string>`: Template ID (ById set).
- `-Template <PSCustomObject>`: Template object (ByObject set).
- `-Variables <hashtable>`: Runtime variable overrides.
- `-Force`: Skip custom-template warning prompt.

**Example**
```powershell
Apply-DistroNexusTemplate -InstanceName "Ubuntu-24.04" -TemplateId "python-dev" -Variables @{ PythonVersion = "3.12" }
```

### `Invoke-DistroNexusTemplateAutomation`
Run template automation validation workflows.

**Parameters**
- `-Mode <AllTemplates|SelectedTemplates>`: Execution mode.
- `-TemplateIds <string[]>`: Template IDs for selected mode.
- `-Distro <string>`: Base distro name.
- `-OutputRoot <string>`: Results output root.
- `-IncludeCapabilityGated`: Include gated templates (for example GPU).
- `-DryRun`: Simulate execution.
- `-AllowCiOverride`: Allow run in CI environment.
- `-UseSharedDistro`: Reuse one distro instead of per-template isolation.
- `-TestResultFormat <NUnitXml|JUnitXml>`: Test result output format.

**Examples**
```powershell
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds "python-dev","nodejs-dev" -Distro "Ubuntu-24.04" -DryRun
Invoke-DistroNexusTemplateAutomation -Mode AllTemplates -Distro "Ubuntu-24.04" -IncludeCapabilityGated
```

For complete parameter details, use `Get-Help <CmdletName> -Detailed` in PowerShell after importing the module.

## Disk Management

### `Compress-DistroNexusInstance`
Compact a WSL instance's VHDX disk to reclaim unused space.

**Parameters**
- `-Name <string>`: Instance name.
- `-WhatIf`: Dry-run; estimate space savings without modifying the disk.

**Examples**
```powershell
Compress-DistroNexusInstance -Name "Ubuntu-24.04"
Compress-DistroNexusInstance -Name "Ubuntu-24.04" -WhatIf
```

## Docker Desktop Integration

### `Get-DistroNexusDockerIntegration`
Report Docker Desktop WSL backend integration status for all instances.

**Example**
```powershell
Get-DistroNexusDockerIntegration
```

### `Enable-DistroNexusDockerIntegration`
Enable Docker Desktop WSL backend for an instance.

**Parameters**
- `-Name <string>`: Instance name.

**Example**
```powershell
Enable-DistroNexusDockerIntegration -Name "Ubuntu-24.04"
```

### `Disable-DistroNexusDockerIntegration`
Disable Docker Desktop WSL backend for an instance.

**Parameters**
- `-Name <string>`: Instance name.

**Example**
```powershell
Disable-DistroNexusDockerIntegration -Name "Ubuntu-24.04"
```

## Backup and Archive

### `Export-DistroNexusInstance`
Export a WSL instance to a `.tar` archive.

**Parameters**
- `-Name <string>`: Instance name.
- `-Destination <string>`: Destination path (file or directory).
- `-Force`: Auto-stop a running instance before export.

**Examples**
```powershell
Export-DistroNexusInstance -Name "Ubuntu-24.04" -Destination "D:\Backups"
Export-DistroNexusInstance -Name "Ubuntu-24.04" -Destination "D:\Backups\ubuntu.tar" -Force
```

### `Import-DistroNexusInstance`
Import a `.tar` archive as a new WSL instance.

**Parameters**
- `-Name <string>`: New instance name.
- `-Source <string>`: Source `.tar` file path.
- `-InstallPath <string>`: Target installation path.

**Example**
```powershell
Import-DistroNexusInstance -Name "Ubuntu-Restored" -Source "D:\Backups\ubuntu.tar" -InstallPath "D:\WSL\Ubuntu-Restored"
```

### `New-DistroNexusBackupSchedule`
Create an automated backup schedule via Windows Task Scheduler.

**Parameters**
- `-Name <string>`: Instance name.
- `-Frequency <string>`: `Daily`, `Weekly:<DayName>`, or `Monthly:<DayNumber>`.
- `-Destination <string>`: Backup output path.
- `-RetentionCount <int>`: Number of backups to retain.

**Examples**
```powershell
New-DistroNexusBackupSchedule -Name "Ubuntu-24.04" -Frequency "Daily" -Destination "D:\Backups" -RetentionCount 7
New-DistroNexusBackupSchedule -Name "Ubuntu-24.04" -Frequency "Weekly:Monday" -Destination "D:\Backups" -RetentionCount 4
```

### `Remove-DistroNexusBackupSchedule`
Remove a backup schedule.

**Parameters**
- `-Name <string>`: Instance name.

**Example**
```powershell
Remove-DistroNexusBackupSchedule -Name "Ubuntu-24.04"
```

### `Get-DistroNexusBackupSchedule`
List configured backup schedules.

**Parameters**
- `-Name <string>`: Filter by instance name (optional).

**Example**
```powershell
Get-DistroNexusBackupSchedule
```

### `Invoke-DistroNexusBackup`
Run a backup immediately for the specified instance.

**Parameters**
- `-Name <string>`: Instance name.

**Example**
```powershell
Invoke-DistroNexusBackup -Name "Ubuntu-24.04"
```

## Configuration

### `Get-DistroNexusWslConfig`
Read the global `~/.wslconfig` file.

**Example**
```powershell
Get-DistroNexusWslConfig
```

### `Set-DistroNexusWslConfig`
Update keys in the global `~/.wslconfig` file while preserving unknown keys and comments.

**Parameters**
- `-Memory <string>`: Maximum memory (for example `4GB`).
- `-Processors <int>`: Maximum CPU count.
- `-Swap <string>`: Swap size (for example `2GB`).

**Example**
```powershell
Set-DistroNexusWslConfig -Memory "8GB" -Processors 4
```

### `Get-DistroNexusInstanceConfig`
Read per-instance resource configuration (sparse VHDX mode, global memory/CPU).

**Parameters**
- `-Name <string>`: Instance name.

**Example**
```powershell
Get-DistroNexusInstanceConfig -Name "Ubuntu-24.04"
```

### `Set-DistroNexusInstanceSparseMode`
Enable or disable sparse VHDX mode for an instance.

**Parameters**
- `-Name <string>`: Instance name.
- `-Enabled <bool>`: `$true` to enable, `$false` to disable.

**Example**
```powershell
Set-DistroNexusInstanceSparseMode -Name "Ubuntu-24.04" -Enabled $true
```

## Network

### `Get-DistroNexusPortMapping`
List all ports listening inside a WSL instance, with Windows proxy mapping information.

**Parameters**
- `-Name <string>`: Instance name.
- `-Protocol <TCP|UDP|All>`: Filter by protocol (default: `All`).

**Examples**
```powershell
Get-DistroNexusPortMapping -Name "Ubuntu-24.04"
Get-DistroNexusPortMapping -Name "Ubuntu-24.04" -Protocol TCP
```

## Tags

### `Get-DistroNexusInstanceTag`
Get tags for an instance.

**Parameters**
- `-Name <string>`: Instance name.

**Example**
```powershell
Get-DistroNexusInstanceTag -Name "Ubuntu-24.04"
```

### `Set-DistroNexusInstanceTag`
Replace all tags for an instance (max 10).

**Parameters**
- `-Name <string>`: Instance name.
- `-Tags <string[]>`: New tag list.

**Example**
```powershell
Set-DistroNexusInstanceTag -Name "Ubuntu-24.04" -Tags "dev", "python"
```

### `Add-DistroNexusInstanceTag`
Add one or more tags to an instance.

**Parameters**
- `-Name <string>`: Instance name.
- `-Tags <string[]>`: Tags to add.

**Example**
```powershell
Add-DistroNexusInstanceTag -Name "Ubuntu-24.04" -Tags "docker"
```

### `Remove-DistroNexusInstanceTag`
Remove one or more tags from an instance.

**Parameters**
- `-Name <string>`: Instance name.
- `-Tags <string[]>`: Tags to remove.

**Example**
```powershell
Remove-DistroNexusInstanceTag -Name "Ubuntu-24.04" -Tags "docker"
```

## Diagnostics

### `Get-DistroNexusCache`
Show instance cache diagnostics: age, entry count, and staleness state.

**Example**
```powershell
Get-DistroNexusCache
```
