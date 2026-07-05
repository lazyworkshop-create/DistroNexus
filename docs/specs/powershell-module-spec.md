# DistroNexus PowerShell Module

The DistroNexus PowerShell module provides cmdlets for managing Windows Subsystem for Linux (WSL) distributions from the command line or in automation scripts.

## Installation

### From DistroNexus Application
The module is automatically installed with DistroNexus at:
```
C:\Program Files\DistroNexus\PowerShell\
```

### Manual Import
```powershell
Import-Module "C:\Program Files\DistroNexus\PowerShell\DistroNexus.psm1"
```

### Add to PowerShell Profile
To load automatically on every PowerShell session:
```powershell
Add-Content $PROFILE 'Import-Module "C:\Program Files\DistroNexus\PowerShell\DistroNexus.psm1"'
```

## Quick Reference

| Cmdlet | Description |
|--------|-------------|
| `Get-DistroNexusInstance` | List installed WSL instances |
| `Start-DistroNexusInstance` | Start a WSL instance |
| `Stop-DistroNexusInstance` | Stop a running instance |
| `Install-DistroNexusInstance` | Install WSL to custom location |
| `Remove-DistroNexusInstance` | Uninstall a WSL instance |
| `Move-DistroNexusInstance` | Relocate instance to new path |
| `Rename-DistroNexusInstance` | Rename an instance |
| `Set-DistroNexusCredential` | Set username/password |
| `Get-DistroNexusPackage` | List available distributions |
| `Save-DistroNexusPackage` | Download a distribution package |
| `Update-DistroNexusCatalog` | Refresh distribution catalog |

---

## Cmdlet Reference

### Get-DistroNexusInstance

Lists all installed WSL instances with detailed information.

**Syntax**
```powershell
Get-DistroNexusInstance [[-Name] <String>] [<CommonParameters>]
```

**Parameters**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Name` | String | No | Filter by instance name (supports wildcards) |

**Examples**
```powershell
# List all instances
Get-DistroNexusInstance

# Get specific instance
Get-DistroNexusInstance -Name "Ubuntu-22.04"

# Filter with wildcards
Get-DistroNexusInstance -Name "Ubuntu*"
```

**Output**
Returns `PSCustomObject` with properties: `Name`, `State`, `Version`, `BasePath`, `DiskSize`

---

### Start-DistroNexusInstance

Starts a stopped WSL instance.

**Syntax**
```powershell
Start-DistroNexusInstance [-Name] <String> [-Background] [<CommonParameters>]
```

**Parameters**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Name` | String | Yes | Name of the instance to start |
| `-Background` | Switch | No | Start without opening terminal |

**Examples**
```powershell
# Start and open terminal
Start-DistroNexusInstance -Name "Ubuntu-22.04"

# Start in background
Start-DistroNexusInstance -Name "Ubuntu-22.04" -Background
```

---

### Stop-DistroNexusInstance

Stops a running WSL instance.

**Syntax**
```powershell
Stop-DistroNexusInstance [-Name] <String> [-Force] [<CommonParameters>]
```

**Parameters**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Name` | String | Yes | Name of the instance to stop |
| `-Force` | Switch | No | Force terminate immediately |

**Examples**
```powershell
# Graceful stop
Stop-DistroNexusInstance -Name "Ubuntu-22.04"

# Force terminate
Stop-DistroNexusInstance -Name "Ubuntu-22.04" -Force
```

---

### Install-DistroNexusInstance

Installs a WSL distribution to a custom location.

**Syntax**
```powershell
Install-DistroNexusInstance [-DistroName] <String> [-InstallPath] <String> 
    [[-Username] <String>] [[-Password] <SecureString>] 
    [-Quick] [<CommonParameters>]
```

**Parameters**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-DistroName` | String | Yes | Distribution package name |
| `-InstallPath` | String | Yes | Target installation directory |
| `-Username` | String | No | Default user to create |
| `-Password` | SecureString | No | Password for default user |
| `-Quick` | Switch | No | Skip interactive prompts, use defaults |

**Examples**
```powershell
# Basic installation
Install-DistroNexusInstance -DistroName "Ubuntu-22.04" -InstallPath "D:\WSL\Ubuntu"

# With user configuration
$password = ConvertTo-SecureString "MyPassword123" -AsPlainText -Force
Install-DistroNexusInstance -DistroName "Debian" -InstallPath "E:\Linux\Debian" `
    -Username "admin" -Password $password

# Quick mode (no prompts)
Install-DistroNexusInstance -DistroName "Alpine" -InstallPath "D:\WSL\Alpine" -Quick
```

---

### Remove-DistroNexusInstance

Uninstalls a WSL instance.

**Syntax**
```powershell
Remove-DistroNexusInstance [-Name] <String> [-Force] [-KeepFiles] [<CommonParameters>]
```

**Parameters**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Name` | String | Yes | Name of instance to remove |
| `-Force` | Switch | No | Skip confirmation prompt |
| `-KeepFiles` | Switch | No | Keep VHDX files after unregistration |

**Examples**
```powershell
# Remove with confirmation
Remove-DistroNexusInstance -Name "OldUbuntu"

# Force remove without prompts
Remove-DistroNexusInstance -Name "TestDistro" -Force

# Unregister but keep disk files
Remove-DistroNexusInstance -Name "Backup" -KeepFiles
```

---

### Move-DistroNexusInstance

Relocates a WSL instance to a new path.

**Syntax**
```powershell
Move-DistroNexusInstance [-Name] <String> [-DestinationPath] <String> [<CommonParameters>]
```

**Parameters**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Name` | String | Yes | Name of instance to move |
| `-DestinationPath` | String | Yes | New directory path |

**Examples**
```powershell
# Move to different drive
Move-DistroNexusInstance -Name "Ubuntu-22.04" -DestinationPath "E:\WSL\Ubuntu"
```

**Notes**
- Instance will be stopped during move
- Uses export/import workflow (may take time for large instances)
- Original files are removed after successful move

---

### Rename-DistroNexusInstance

Renames a WSL instance.

**Syntax**
```powershell
Rename-DistroNexusInstance [-Name] <String> [-NewName] <String> [<CommonParameters>]
```

**Parameters**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Name` | String | Yes | Current instance name |
| `-NewName` | String | Yes | New name for the instance |

**Examples**
```powershell
Rename-DistroNexusInstance -Name "Ubuntu" -NewName "Ubuntu-Dev"
```

---

### Set-DistroNexusCredential

Sets or updates credentials for a WSL instance.

**Syntax**
```powershell
Set-DistroNexusCredential [-Name] <String> [-Username] <String> 
    [[-Password] <SecureString>] [<CommonParameters>]
```

**Parameters**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Name` | String | Yes | Instance name |
| `-Username` | String | Yes | Username to set as default |
| `-Password` | SecureString | No | New password for the user |

**Examples**
```powershell
# Set default user
Set-DistroNexusCredential -Name "Ubuntu-22.04" -Username "developer"

# Set user with password
$pwd = Read-Host -AsSecureString "Enter password"
Set-DistroNexusCredential -Name "Ubuntu-22.04" -Username "developer" -Password $pwd
```

---

### Get-DistroNexusPackage

Lists available distribution packages from catalog.

**Syntax**
```powershell
Get-DistroNexusPackage [[-Name] <String>] [-Cached] [<CommonParameters>]
```

**Parameters**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Name` | String | No | Filter by package name |
| `-Cached` | Switch | No | Show only cached (downloaded) packages |

**Examples**
```powershell
# List all available packages
Get-DistroNexusPackage

# Show only Ubuntu packages
Get-DistroNexusPackage -Name "Ubuntu*"

# Show downloaded packages
Get-DistroNexusPackage -Cached
```

---

### Save-DistroNexusPackage

Downloads a distribution package to local cache.

**Syntax**
```powershell
Save-DistroNexusPackage [-Name] <String> [[-DestinationPath] <String>] [<CommonParameters>]
```

**Parameters**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-Name` | String | Yes | Package name to download |
| `-DestinationPath` | String | No | Custom download location |

**Examples**
```powershell
# Download to default cache
Save-DistroNexusPackage -Name "Ubuntu-22.04"

# Download to custom location
Save-DistroNexusPackage -Name "Debian" -DestinationPath "D:\Downloads"
```

---

### Update-DistroNexusCatalog

Refreshes the distribution catalog from online source.

**Syntax**
```powershell
Update-DistroNexusCatalog [[-CatalogUrl] <String>] [<CommonParameters>]
```

**Parameters**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `-CatalogUrl` | String | No | Custom catalog URL |

**Examples**
```powershell
# Update from default source
Update-DistroNexusCatalog

# Use custom catalog
Update-DistroNexusCatalog -CatalogUrl "https://myserver.com/distros.json"
```

---

## Automation Examples

### Batch Installation Script
```powershell
# Install multiple distributions
$distros = @(
    @{ Name = "Ubuntu-22.04"; Path = "D:\WSL\Ubuntu" },
    @{ Name = "Debian"; Path = "D:\WSL\Debian" },
    @{ Name = "Alpine"; Path = "D:\WSL\Alpine" }
)

foreach ($distro in $distros) {
    Write-Host "Installing $($distro.Name)..."
    Install-DistroNexusInstance -DistroName $distro.Name -InstallPath $distro.Path -Quick
}
```

### Backup Script
```powershell
# Export all instances to backup location
$backupPath = "E:\WSL-Backup"
$instances = Get-DistroNexusInstance

foreach ($instance in $instances) {
    $exportFile = Join-Path $backupPath "$($instance.Name).tar"
    Write-Host "Backing up $($instance.Name)..."
    wsl --export $instance.Name $exportFile
}
```

### Status Monitor
```powershell
# Watch instance status
while ($true) {
    Clear-Host
    Get-DistroNexusInstance | Format-Table Name, State, DiskSize -AutoSize
    Start-Sleep -Seconds 5
}
```

---

## Troubleshooting

### Module Not Loading
```powershell
# Check execution policy
Get-ExecutionPolicy
# If restricted, run as Admin:
Set-ExecutionPolicy RemoteSigned -Scope CurrentUser

# Verify module path
Test-Path "C:\Program Files\DistroNexus\PowerShell\DistroNexus.psm1"
```

### Command Not Found
```powershell
# Verify module is loaded
Get-Module DistroNexus

# List available commands
Get-Command -Module DistroNexus
```

### WSL Errors
```powershell
# Check WSL status
wsl --status

# Update WSL
wsl --update

# List installed distros
wsl --list --verbose
```

---

## Related Resources

- [DistroNexus Documentation](https://lazyworkshopcreate.github.io/DistroNexus/)
- [WSL Documentation](https://docs.microsoft.com/windows/wsl/)
- [PowerShell Documentation](https://docs.microsoft.com/powershell/)
