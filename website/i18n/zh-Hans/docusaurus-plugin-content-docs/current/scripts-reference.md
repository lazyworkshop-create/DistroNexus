---
sidebar_position: 5
---

# PowerShell 脚本参考

本页提供 DistroNexus PowerShell 模块的命令参考。

命令来源以 `src/PowerShell/DistroNexus.psd1` 的导出列表为准，共 15 条 cmdlet。

## 实例管理

### `Get-DistroNexusInstance`
列出已注册 WSL 实例及其状态与元数据。

**参数**
- `-Name <string>`：按实例名过滤（支持通配符）。
- `-ForceUpdate`：绕过缓存并强制全量扫描。
- `-IncludeRelease`：包含发行版版本信息。
- `-IncludeUser`：包含默认用户信息。
- `-SkipDiskSize`：跳过磁盘体积检查以加快速度。

**示例**
```powershell
Get-DistroNexusInstance
Get-DistroNexusInstance -Name "Ubuntu*" -ForceUpdate
Get-DistroNexusInstance -IncludeRelease -IncludeUser
```

### `Start-DistroNexusInstance`
启动实例。

**参数**
- `-Name <string>`：实例名称。

**示例**
```powershell
Start-DistroNexusInstance -Name "Ubuntu-24.04"
```

### `Stop-DistroNexusInstance`
停止实例。

**参数**
- `-Name <string>`：实例名称。
- `-Force`：跳过确认。

**示例**
```powershell
Stop-DistroNexusInstance -Name "Ubuntu-24.04"
Stop-DistroNexusInstance -Name "Ubuntu-24.04" -Force
```

### `Move-DistroNexusInstance`
将实例迁移到新的存储路径。

**参数**
- `-Name <string>`：实例名称。
- `-Destination <string>`：目标根路径。

**示例**
```powershell
Move-DistroNexusInstance -Name "Ubuntu-24.04" -Destination "D:\\WSL"
```

### `Rename-DistroNexusInstance`
重命名实例。

**参数**
- `-Name <string>`：当前名称。
- `-NewName <string>`：新名称。

**示例**
```powershell
Rename-DistroNexusInstance -Name "Ubuntu-24.04" -NewName "Ubuntu-Dev"
```

### `Remove-DistroNexusInstance`
移除实例。

**参数**
- `-Name <string>`：实例名称。
- `-Force`：跳过确认。
- `-KeepFiles`：仅注销实例，保留磁盘文件。

**示例**
```powershell
Remove-DistroNexusInstance -Name "Ubuntu-Temp" -Force
Remove-DistroNexusInstance -Name "Ubuntu-Archive" -KeepFiles
```

### `Install-DistroNexusInstance`
从已配置包源安装新实例。

**参数**
- `-DistroName <string>`：目录中的发行版标识。
- `-InstallPath <string>`：安装路径。
- `-InstanceName <string>`：自定义实例名。
- `-Username <string>`：默认用户名（默认 `root`）。
- `-Password <SecureString>`：用户密码。
- `-Interactive`：交互式安装。
- `-AutoDownload`：缺包时自动下载。
- `-OpenTerminal`：安装完成后打开终端。
- `-Shell <bash|zsh|fish|sh>`：默认 shell。
- `-Locale <string>`：区域设置（如 `en_US.UTF-8`）。
- `-SetAsDefault`：设为默认 WSL 发行版。

**示例**
```powershell
Install-DistroNexusInstance -DistroName "Ubuntu-24.04" -InstallPath "D:\\WSL\\Ubuntu-24.04" -AutoDownload

$password = Read-Host -AsSecureString "Password"
Install-DistroNexusInstance -DistroName "Debian" -InstallPath "E:\\WSL\\Debian" -Username "admin" -Password $password -Shell "zsh"
```

### `Set-DistroNexusCredential`
设置或更新实例凭据。

**参数**
- `-Name <string>`：实例名称。
- `-Username <string>`：要设置的用户名。
- `-Password <SecureString>`：可选密码。

**示例**
```powershell
$password = Read-Host -AsSecureString "Password"
Set-DistroNexusCredential -Name "Ubuntu-24.04" -Username "admin" -Password $password
```

## 包与目录

### `Get-DistroNexusPackage`
查询可用或已缓存的安装包信息。

**参数**
- `-Family <string>`：按发行版家族过滤（例如 `Ubuntu`）。

**示例**
```powershell
Get-DistroNexusPackage -Family "Ubuntu"
```

### `Save-DistroNexusPackage`
下载并缓存安装包用于离线部署。

**参数**
- `-DefaultName <string>`：下载单个发行版包。
- `-Family <string>`：按家族批量下载。
- `-All`：下载目录中全部包。
- `-Destination <string>`：覆盖默认缓存目录。
- `-MaxConcurrent <int>`：并发下载数（1-10）。
- `-RetryCount <int>`：失败重试次数（0-10）。
- `-ShowSpeed <bool>`：显示下载速度。
- `-SkipExisting <bool>`：跳过已存在文件。

**示例**
```powershell
Save-DistroNexusPackage -DefaultName "Ubuntu-24.04"
Save-DistroNexusPackage -Family "Ubuntu" -MaxConcurrent 5
Save-DistroNexusPackage -All -RetryCount 5
```

### `Remove-DistroNexusPackage`
删除本地缓存包。

**参数**
- `-DefaultName <string>`：按目录默认名删除。
- `-LocalPath <string>`：按文件完整路径删除。
- `-Force`：跳过确认。

**示例**
```powershell
Remove-DistroNexusPackage -DefaultName "Ubuntu-24.04" -Force
Remove-DistroNexusPackage -LocalPath "D:\\WSL\\packages\\ubuntu-24.04.wsl" -Force
```

### `Update-DistroNexusCatalog`
更新发行版目录元数据。

**参数**
- `-SourceUrl <string>`：覆盖默认目录 URL。

**示例**
```powershell
Update-DistroNexusCatalog
```

## 模板命令

### `Get-DistroNexusTemplate`
列出可用内置模板。

**参数**
- `-Id <string>`：按模板 ID 过滤。
- `-Category <string>`：按分类过滤。

**示例**
```powershell
Get-DistroNexusTemplate
Get-DistroNexusTemplate -Category "Development"
```

### `Apply-DistroNexusTemplate`
应用指定模板。

**参数**
- `-InstanceName <string>`：目标实例名。
- `-TemplateId <string>`：模板 ID（ById 参数集）。
- `-Template <PSCustomObject>`：模板对象（ByObject 参数集）。
- `-Variables <hashtable>`：运行时变量覆盖。
- `-Force`：跳过自定义模板风险确认。

**示例**
```powershell
Apply-DistroNexusTemplate -InstanceName "Ubuntu-24.04" -TemplateId "python-dev" -Variables @{ PythonVersion = "3.12" }
```

### `Invoke-DistroNexusTemplateAutomation`
执行模板自动化流程，用于可重复环境引导。

**参数**
- `-Mode <AllTemplates|SelectedTemplates>`：执行模式。
- `-TemplateIds <string[]>`：选中模式下的模板 ID 列表。
- `-Distro <string>`：基础发行版名称。
- `-OutputRoot <string>`：结果输出目录。
- `-IncludeCapabilityGated`：包含能力门控模板（如 GPU）。
- `-DryRun`：仅演练不执行。
- `-AllowCiOverride`：允许在 CI 环境执行。
- `-UseSharedDistro`：复用单一发行版而非逐模板隔离。
- `-TestResultFormat <NUnitXml|JUnitXml>`：测试结果格式。

**示例**
```powershell
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds "python-dev","nodejs-dev" -Distro "Ubuntu-24.04" -DryRun
Invoke-DistroNexusTemplateAutomation -Mode AllTemplates -Distro "Ubuntu-24.04" -IncludeCapabilityGated
```

完整参数说明可在导入模块后执行 `Get-Help <CmdletName> -Detailed` 查看。
