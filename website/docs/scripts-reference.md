---
sidebar_position: 5
---

# PowerShell Scripts Reference

This page documents the DistroNexus PowerShell module command surface.

The authoritative exported commands come from `src/PowerShell/DistroNexus.psd1` and include 15 cmdlets.

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
