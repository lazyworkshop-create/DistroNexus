# DistroNexus PowerShell 脚本 vs 模块对比分析

## 概述

本文档详细对比了 `scripts/` 文件夹中的旧版独立 PowerShell 脚本与 `src/PowerShell/` 目录下的新版 PowerShell 模块之间的功能差异。

---

## 一、架构对比

| 对比项 | 旧版脚本 (scripts/) | 新版模块 (src/PowerShell/) |
|--------|---------------------|---------------------------|
| **架构类型** | 独立脚本文件 | PowerShell 模块 |
| **文件组织** | 12个独立的 .ps1 文件 | 模块化结构（Private/Public分离） |
| **代码复用** | 每个脚本独立，通过 pwsh_utils.ps1 共享基础功能 | 私有函数在 Private/ 目录，公共函数在 Public/ 目录 |
| **命名约定** | 脚本命名采用下划线分隔（如 `install_wsl_custom.ps1`） | Cmdlet 命名采用 PowerShell 标准动词-名词（如 `Install-DistroNexusInstance`） |
| **PowerShell 版本要求** | 支持 PowerShell 5.1+ | 要求 PowerShell 7.0+ |
| **模块清单** | 无 | 完整的 .psd1 清单文件 |
| **帮助文档** | 代码注释为主 | 内置 PowerShell 帮助系统（.SYNOPSIS, .DESCRIPTION 等） |

---

## 二、核心功能对比

### 2.1 实例管理功能

#### 2.1.1 列出实例

| 功能 | 旧版脚本 | 新版模块 | 差异说明 |
|------|----------|----------|----------|
| **文件** | `list_distros.ps1` | `Get-DistroNexusInstance` |  |
| **调用方式** | `./list_distros.ps1 [-ForceUpdate]` | `Get-DistroNexusInstance [-Name <string>]` |  |
| **输出格式** | JSON 输出到 stdout | 返回 PSCustomObject 对象 | 新版模块返回对象，便于管道操作 |
| **缓存机制** | ✅ 支持缓存到 instances.json | ❌ 无缓存 | 旧版有本地缓存机制 |
| **强制更新** | ✅ 支持 -ForceUpdate 参数 | ❌ 无强制更新 | 旧版可强制刷新运行中实例的信息 |
| **磁盘大小计算** | ✅ 支持 | ✅ 支持 | 功能相同 |
| **筛选功能** | ❌ 无 | ✅ 支持 -Name 参数支持通配符 | 新版增加了筛选能力 |
| **详细状态** | ✅ State, Version, Release, User, DiskSize | ✅ State, Version, DiskSize, InstallTime | 新版不再直接查询 Release 和 User（减少启动实例） |
| **错误处理** | 基础错误处理 | 结构化错误处理 | 新版使用 try-catch 和 Write-DistroNexusLog |

**代码示例对比：**

```powershell
# 旧版：返回 JSON
./list_distros.ps1

# 新版：返回对象，支持管道
Get-DistroNexusInstance
Get-DistroNexusInstance -Name "Ubuntu*"
```

---

#### 2.1.2 安装实例

| 功能 | 旧版脚本 | 新版模块 | 差异说明 |
|------|----------|----------|----------|
| **文件** | `install_wsl_custom.ps1` | `Install-DistroNexusInstance` |  |
| **交互模式** | ✅ 完全交互式菜单选择 | ❌ 纯参数化 | 旧版支持交互式选择发行版和版本 |
| **快速模式** | ✅ 支持 -name 参数快速安装 | ❌ 无快速模式 | 旧版可通过 settings.json 的 DefaultDistro 快速安装 |
| **参数设计** | `-DistroName`, `-InstallPath`, `-name`, `-user`, `-pass` | `-DistroName`, `-InstallPath`, `-InstanceName`, `-Username`, `-Password` | 新版参数更规范，密码使用 SecureString |
| **密码安全** | ❌ 明文密码 | ✅ SecureString | 新版安全性更高 |
| **列表模式** | ✅ 支持 -List 列出可用发行版 | ❌ 无列表模式（需配合 Get-DistroNexusPackage） | 旧版集成了列表功能 |
| **自动下载** | ✅ 自动调用 download_all_distros.ps1 | ❌ 需先使用 Save-DistroNexusPackage | 旧版自动下载包，新版需手动下载 |
| **用户配置** | ✅ 完整用户配置（创建用户、设置密码、添加 sudo） | ⚠️ 简化（注释中说明简化了用户配置） | 旧版用户配置更完整 |
| **包类型处理** | ✅ 支持 appx, zip, tar.gz 自动解压 | ❌ 仅支持直接导入 | 旧版可处理复杂包格式 |
| **实例注册** | ✅ 自动更新 instances.json | ❌ 不维护 instances.json | 旧版维护本地实例注册表 |
| **支持确认** | ❌ 无 | ✅ 支持 ShouldProcess | 新版支持 -WhatIf 和 -Confirm |

**代码示例对比：**

```powershell
# 旧版：交互式安装
./install_wsl_custom.ps1

# 旧版：快速模式
./install_wsl_custom.ps1 -name "MyUbuntu" -user "dev" -pass "secret"

# 旧版：指定发行版安装
./install_wsl_custom.ps1 -SelectFamily "Ubuntu" -SelectVersion "1" -DistroName "Ubuntu"

# 新版：完全参数化
Install-DistroNexusInstance -DistroName "Ubuntu-22.04" -InstallPath "D:\WSL\Ubuntu"

# 新版：带用户配置
$pass = Read-Host -AsSecureString -Prompt "Password"
Install-DistroNexusInstance -DistroName "Ubuntu-22.04" -InstallPath "D:\WSL\Ubuntu" -Username "dev" -Password $pass
```

---

#### 2.1.3 卸载实例

| 功能 | 旧版脚本 | 新版模块 | 差异说明 |
|------|----------|----------|----------|
| **文件** | `uninstall_wsl_custom.ps1` | `Remove-DistroNexusInstance` |  |
| **交互模式** | ✅ 完全交互式菜单 | ❌ 纯参数化 | 旧版支持选择列表 |
| **保留文件** | ❌ 每次询问是否删除文件 | ✅ 支持 -KeepFiles 参数 | 新版提供了更灵活的选项 |
| **强制删除** | ✅ 支持 -Force | ✅ 支持 -Force | 功能相同 |
| **管道支持** | ❌ 无 | ✅ 支持管道输入 | 新版可以配合 Get-DistroNexusInstance 使用 |
| **文件删除逻辑** | 二次确认删除 | 根据 KeepFiles 参数决定 | 新版逻辑更清晰 |
| **配置更新** | ✅ 自动更新 instances.json | ❌ 不维护 instances.json | 旧版维护本地注册表 |
| **错误处理** | 基础 | 结构化 | 新版更完善 |

**代码示例对比：**

```powershell
# 旧版：交互式卸载
./uninstall_wsl_custom.ps1

# 旧版：指定名称卸载
./uninstall_wsl_custom.ps1 -DistroName "Ubuntu-Test"

# 旧版：强制卸载
./uninstall_wsl_custom.ps1 -DistroName "Ubuntu-Test" -Force

# 新版：直接卸载
Remove-DistroNexusInstance -Name "Ubuntu-Test"

# 新版：管道操作
Get-DistroNexusInstance -Name "Test*" | Remove-DistroNexusInstance

# 新版：只注销不删除文件
Remove-DistroNexusInstance -Name "Ubuntu-Test" -KeepFiles
```

---

#### 2.1.4 移动实例

| 功能 | 旧版脚本 | 新版模块 | 差异说明 |
|------|----------|----------|----------|
| **文件** | `move_instance.ps1` | `Move-DistroNexusInstance` |  |
| **参数设计** | `-DistroName`, `-NewPath` | `-Name`, `-Destination` | 新版命名更符合 PowerShell 规范 |
| **非空目录检查** | ✅ 检查并提示 | ❌ 无检查 | 旧版更安全 |
| **进度显示** | ❌ 无 | ✅ 使用 Write-Progress | 新版用户体验更好 |
| **用户恢复** | ✅ 尝试恢复默认用户 | ❌ 不处理用户恢复 | 旧版功能更完整 |
| **注册表更新** | ✅ 调用 scan_wsl_instances.ps1 | ❌ 不维护配置 | 旧版维护本地注册表 |
| **管道支持** | ❌ 无 | ✅ 支持管道输入 | 新版灵活性更高 |
| **确认机制** | ❌ 无 | ✅ 支持 ShouldProcess | 新版支持 -WhatIf 和 -Confirm |

**代码示例对比：**

```powershell
# 旧版
./move_instance.ps1 -DistroName "Ubuntu" -NewPath "D:\WSL\Ubuntu"

# 新版
Move-DistroNexusInstance -Name "Ubuntu" -Destination "D:\WSL\Ubuntu"

# 新版：管道操作
Get-DistroNexusInstance -Name "Ubuntu*" | Move-DistroNexusInstance -Destination "D:\WSL"
```

---

#### 2.1.5 重命名实例

| 功能 | 旧版脚本 | 新版模块 | 差异说明 |
|------|----------|----------|----------|
| **文件** | `rename_instance.ps1` | `Rename-DistroNexusInstance` |  |
| **参数设计** | `-OldName`, `-NewName`, `-NewPath` | `-Name`, `-NewName` | 新版简化了参数 |
| **路径处理** | ✅ 支持同时更改路径（-NewPath） | ❌ 只重命名，路径自动推导 | 旧版功能更灵活 |
| **源路径查找** | ✅ 先查 instances.json，再查注册表 | ❌ 只通过 Get-DistroNexusInstance 获取 | 旧版容错性更强 |
| **用户恢复** | ✅ 恢复默认用户配置 | ❌ 不处理 | 旧版功能更完整 |
| **配置更新** | ✅ 直接更新 instances.json 保留 Release/User | ❌ 不维护配置 | 旧版保留元数据 |
| **管道支持** | ❌ 无 | ✅ 支持管道输入 | 新版更灵活 |

**代码示例对比：**

```powershell
# 旧版：仅重命名
./rename_instance.ps1 -OldName "Ubuntu" -NewName "Ubuntu-Dev"

# 旧版：重命名并移动
./rename_instance.ps1 -OldName "Ubuntu" -NewName "Ubuntu-Dev" -NewPath "D:\WSL\Ubuntu-Dev"

# 新版：重命名（路径自动在原位置同级）
Rename-DistroNexusInstance -Name "Ubuntu" -NewName "Ubuntu-Dev"
```

---

#### 2.1.6 启动实例

| 功能 | 旧版脚本 | 新版模块 | 差异说明 |
|------|----------|----------|----------|
| **文件** | `start_instance.ps1` | `Start-DistroNexusInstance` |  |
| **启动模式** | ✅ 支持打开终端或后台启动 | ❌ 仅后台启动 | 旧版功能更丰富 |
| **工作目录** | ✅ 支持 -StartPath 参数 | ❌ 无工作目录参数 | 旧版可指定启动目录 |
| **状态更新** | ✅ 乐观更新 instances.json | ❌ 不维护配置 | 旧版维护状态 |
| **管道支持** | ❌ 无 | ✅ 支持管道输入 | 新版灵活性更高 |

**代码示例对比：**

```powershell
# 旧版：后台启动
./start_instance.ps1 -DistroName "Ubuntu"

# 旧版：打开终端
./start_instance.ps1 -DistroName "Ubuntu" -OpenTerminal

# 旧版：指定工作目录
./start_instance.ps1 -DistroName "Ubuntu" -OpenTerminal -StartPath "/home/dev/project"

# 新版：后台启动
Start-DistroNexusInstance -Name "Ubuntu"

# 新版：管道操作
Get-DistroNexusInstance | Start-DistroNexusInstance
```

---

#### 2.1.7 停止实例

| 功能 | 旧版脚本 | 新版模块 | 差异说明 |
|------|----------|----------|----------|
| **文件** | `stop_instance.ps1` | `Stop-DistroNexusInstance` |  |
| **确认机制** | ❌ 无 | ✅ 支持 -Confirm 和 ShouldProcess | 新版更安全 |
| **状态检查** | ❌ 无 | ✅ 检查实例是否已停止 | 新版避免重复操作 |
| **状态更新** | ✅ 更新 instances.json | ❌ 不维护配置 | 旧版维护状态 |
| **管道支持** | ❌ 无 | ✅ 支持管道输入 | 新版灵活性更高 |

**代码示例对比：**

```powershell
# 旧版
./stop_instance.ps1 -DistroName "Ubuntu"

# 新版：停止
Stop-DistroNexusInstance -Name "Ubuntu"

# 新版：管道操作
Get-DistroNexusInstance -State "Running" | Stop-DistroNexusInstance

# 新版：强制停止
Stop-DistroNexusInstance -Name "Ubuntu" -Force
```

---

### 2.2 包管理功能

#### 2.2.1 列出可用包

| 功能 | 旧版脚本 | 新版模块 | 差异说明 |
|------|----------|----------|----------|
| **对应脚本** | `install_wsl_custom.ps1 -List` | `Get-DistroNexusPackage` |  |
| **输出格式** | 交互式表格输出 | 返回 PSCustomObject | 新版支持管道操作 |
| **缓存状态** | ❌ 不显示缓存状态 | ✅ 显示 IsCached 和 LocalPath | 新版信息更完整 |
| **筛选功能** | ❌ 无 | ✅ 支持 -Family 参数 | 新版可按发行版族筛选 |
| **示例代码** | ✅ 内置示例代码 | ✅ 内置示例代码 | 相同 |

**代码示例对比：**

```powershell
# 旧版：集成在安装脚本中
./install_wsl_custom.ps1 -List

# 新版：独立命令
Get-DistroNexusPackage
Get-DistroNexusPackage -Family "Ubuntu"
```

---

#### 2.2.2 下载包

| 功能 | 旧版脚本 | 新版模块 | 差异说明 |
|------|----------|----------|----------|
| **文件** | `download_all_distros.ps1` | `Save-DistroNexusPackage` |  |
| **下载策略** | 批量下载所有或筛选下载 | 按需下载单个包 | 新版更精细 |
| **进度显示** | ✅ 详细进度（百分比、MB） | ❌ 简单进度（使用 Invoke-WebRequest 默认） | 旧版进度显示更好 |
| **筛选参数** | ✅ 支持 -SelectFamily 和 -SelectVersion | ❌ 需指定完整 DefaultName | 旧版筛选更灵活 |
| **配置更新** | ✅ 自动更新 distros.json 的 LocalPath | ❌ 不更新配置 | 旧版维护包路径 |
| **缓存目录** | 支持自定义路径（settings.json） | ✅ 支持自定义路径 | 相同 |
| **跳过已存在** | ✅ 检查并跳过 | ✅ 检查并跳过 | 相同 |
| **.NET HttpClient** | ✅ 使用 HttpClient | ❌ 使用 Invoke-WebRequest | 旧版控制更精细 |
| **批量操作** | ✅ 支持批量 | ❌ 单次下载一个 | 旧版效率更高 |

**代码示例对比：**

```powershell
# 旧版：下载所有
./download_all_distros.ps1

# 旧版：筛选下载
./download_all_distros.ps1 -SelectFamily "Ubuntu" -SelectVersion "1"

# 新版：下载单个包
Save-DistroNexusPackage -DefaultName "Ubuntu-22.04"

# 新版：指定目录
Save-DistroNexusPackage -DefaultName "Ubuntu-22.04" -Destination "D:\Downloads"
```

---

#### 2.2.3 更新目录

| 功能 | 旧版脚本 | 新版模块 | 差异说明 |
|------|----------|----------|----------|
| **文件** | `update_distros.ps1` | `Update-DistroNexusCatalog` |  |
| **数据源** | Microsoft WSL DistributionInfo.json | DistroNexus 项目配置 | 新版使用项目自己的配置 |
| **LocalPath 保留** | ✅ 保留现有 LocalPath | ❌ 直接覆盖，可能丢失 LocalPath | 旧版更安全 |
| **备份机制** | ✅ 自动备份（.timestamp.bak） | ❌ 无备份 | 旧版安全性更高 |
| **离线模式** | ❌ 下载失败则退出 | ✅ 失败时回退到本地目录 | 新版容错性更好 |
| **配置保留** | ✅ 尝试保留 LocalPath | ❌ 直接替换 | 旧版用户体验更好 |

**代码示例对比：**

```powershell
# 旧版
./update_distros.ps1
./update_distros.ps1 -SourceUrl "https://..."

# 新版
Update-DistroNexusCatalog
Update-DistroNexusCatalog -SourceUrl "https://..."
```

---

### 2.3 用户管理功能

#### 2.3.1 设置凭证

| 功能 | 旧版脚本 | 新版模块 | 差异说明 |
|------|----------|----------|----------|
| **文件** | `set_credentials.ps1` | `Set-DistroNexusCredential` |  |
| **参数设计** | `-DistroName`, `-UserName`, `-Password` | `-Name`, `-Username`, `-Password` | 新版参数更规范 |
| **密码类型** | ❌ 明文字符串 | ✅ SecureString | 新版安全性更高 |
| **配置更新** | ✅ 更新 instances.json | ✅ 直接更新注册表 | 实现方式不同，效果相同 |
| **sudo 配置** | ✅ 尝试添加到 sudo 和 wheel | ✅ 只添加 sudo | 旧版兼容性更好 |
| **wsl.conf 处理** | ✅ 检测并追加或覆盖 | ❌ 不处理 wsl.conf | 旧版功能更完整 |
| **管道支持** | ❌ 无 | ✅ 支持管道输入 | 新版灵活性更高 |

**代码示例对比：**

```powershell
# 旧版：明文密码
./set_credentials.ps1 -DistroName "Ubuntu" -UserName "dev" -Password "secret"

# 新版：SecureString 密码
$pass = Read-Host -AsSecureString -Prompt "Password"
Set-DistroNexusCredential -Name "Ubuntu" -Username "dev" -Password $pass
```

---

### 2.4 其他功能

#### 2.4.1 扫描实例

| 功能 | 旧版脚本 | 新版模块 | 差异说明 |
|------|----------|----------|----------|
| **文件** | `scan_wsl_instances.ps1` | ❌ 无独立命令 |  |
| **功能** | 扫描并更新 instances.json | 集成到 Get-DistroNexusInstance | 新版功能分散 |
| **Release 推断** | ✅ 从配置或名称推断 | ❌ 不查询 | 旧版信息更完整 |
| **User 推断** | ✅ 从配置保留 | ❌ 不查询 | 旧版信息更完整 |
| **输出格式** | JSON 文件 | PSCustomObject | 新版更适合管道 |

---

## 三、新增功能（新版独有）

### 3.1 PowerShell 标准特性

| 功能 | 描述 | 示例 |
|------|------|------|
| **模块清单** | 完整的 .psd1 模块清单文件，包含元数据、版本、导出函数等 | `Import-Module DistroNexus` |
| **帮助系统** | 内置 PowerShell 帮助（Get-Help） | `Get-Help Install-DistroNexusInstance` |
| **管道支持** | 所有公共函数都支持管道输入和输出 | `Get-DistroNexusInstance \| Stop-DistroNexusInstance` |
| **ShouldProcess** | 支持 -WhatIf 和 -Confirm 参数 | `Remove-DistroNexusInstance -WhatIf` |
| **错误处理** | 结构化的 try-catch 和 Write-DistroNexusLog | 统一的日志记录 |
| **PSTypeName** | 输出对象带有类型信息，支持格式化 | `$instance.PSTypeNames` |

---

## 四、删除或简化的功能（新版缺失）

### 4.1 完全删除的功能

| 功能 | 旧版 | 新版 | 影响 |
|------|------|------|------|
| **交互式菜单** | ✅ install_wsl_custom.ps1 支持交互式选择 | ❌ 纯参数化 | 用户体验降低 |
| **快速安装模式** | ✅ -name 参数使用 DefaultDistro | ❌ 无 | 便利性降低 |
| **批量下载** | ✅ download_all_distros.ps1 支持批量 | ❌ 单次下载一个 | 效率降低 |
| **自动包类型解压** | ✅ 自动处理 appx/zip/tar.gz | ❌ 仅支持直接导入 | 兼容性降低 |
| **打开终端启动** | ✅ start_instance.ps1 -OpenTerminal | ❌ 仅后台 | 功能缺失 |

---

### 4.2 简化的功能

| 功能 | 旧版实现 | 新版实现 | 影响 |
|---------|---------|---------|------|
| **用户配置** | 完整：创建用户、设置密码、添加 sudo/wheel、配置 wsl.conf | 简化：注释说明简化了用户配置 | 旧版更完整 |
| **Release 信息** | ✅ 查询 /etc/os-release | ❌ 不查询 | 旧版信息更完整 |
| **User 信息** | ✅ 查询并缓存 | ❌ 不查询 | 旧版信息更完整 |
| **非空目录检查** | ✅ move_instance.ps1 检查 | ❌ 不检查 | 旧版更安全 |
| **配置备份** | ✅ update_distros.ps1 自动备份 | ❌ 无备份 | 旧版更安全 |

---

## 五、代码质量对比

### 5.1 代码结构

| 对比项 | 旧版脚本 | 新版模块 | 差异 |
|--------|----------|----------|------|
| **文件数量** | 12 个独立脚本 | 13 个文件（1 清单 + 1 根模块 + 2 私有 + 10 公共） | 新版模块化 |
| **代码行数** | ~1500 行 | ~1000 行 | 新版更精简 |
| **函数封装** | 部分函数在 pwsh_utils.ps1 | 私有函数在 Private/ | 新版结构更清晰 |
| **注释风格** | 简单注释 | 标准 PowerShell 注释（.SYNOPSIS 等） | 新版更专业 |

---

### 5.2 错误处理

| 对比项 | 旧版脚本 | 新版模块 | 差异 |
|--------|----------|----------|------|
| **日志系统** | ✅ 自定义日志（pwsh_utils.ps1） | ✅ 模块化日志（Write-DistroNexusLog） | 新版更集成 |
| **错误捕获** | try-catch 分散 | 统一的 try-catch 结构 | 新版更一致 |
| **日志轮转** | ✅ 5MB 自动轮转，保留 5 个备份 | ✅ 5MB 自动轮转，保留 5 个备份 | 功能相同 |
| **日志位置** | logs/ 或 %LocalAppData%\DistroNexus\logs | logs/ 或 %LocalAppData%\DistroNexus\logs | 相同 |

---

### 5.3 参数验证

| 对比项 | 旧版脚本 | 新版模块 | 差异 |
|--------|----------|----------|------|
| **参数验证** | 手动检查 | ✅ 使用 PowerShell 特性（ValidateSet） | 新版更规范 |
| **Mandatory 属性** | ❌ 手动检查 | ✅ [Parameter(Mandatory)] | 新版更清晰 |
| **参数别名** | 部分（如 -ls） | 无 | 旧版有别名支持 |
| **默认值** | 手动设置 | ✅ 使用 PowerShell 默认值 | 新版更简洁 |

---

## 六、迁移建议

### 6.1 命令映射表

| 旧版命令 | 新版命令 | 迁移说明 |
|---------|---------|---------|
| `./list_distros.ps1` | `Get-DistroNexusInstance` | 输出格式不同，新版返回对象 |
| `./install_wsl_custom.ps1 -List` | `Get-DistroNexusPackage` | 新版列表功能独立 |
| `./install_wsl_custom.ps1 -name "MyDistro"` | 无直接对应 | 新版需先 Save-DistroNexusPackage，再 Install |
| `./install_wsl_custom.ps1 -DistroName "Ubuntu" -InstallPath "D:\..."` | `Install-DistroNexusInstance -DistroName "Ubuntu-22.04" -InstallPath "D:\..."` | 新版需先下载包 |
| `./uninstall_wsl_custom.ps1` | `Remove-DistroNexusInstance` | 新版失去交互模式 |
| `./move_instance.ps1` | `Move-DistroNexusInstance` | 参数名略有不同 |
| `./rename_instance.ps1` | `Rename-DistroNexusInstance` | 新版不支持同时更改路径 |
| `./start_instance.ps1` | `Start-DistroNexusInstance` | 新版不支持打开终端 |
| `./stop_instance.ps1` | `Stop-DistroNexusInstance` | 功能基本相同 |
| `./set_credentials.ps1` | `Set-DistroNexusCredential` | 新版使用 SecureString |
| `./download_all_distros.ps1` | `Save-DistroNexusPackage` | 新版不支持批量 |
| `./update_distros.ps1` | `Update-DistroNexusCatalog` | 数据源不同 |

---

### 6.2 适配建议

#### 对于旧版用户：

1. **交互式功能依赖**：如果习惯使用交互式菜单，建议保留旧版脚本
2. **快速安装需求**：如果使用 `-name` 快速安装模式，需手动实现或使用旧版脚本
3. **批量下载需求**：如果需要批量下载包，使用旧版 `download_all_distros.ps1`
4. **打开终端需求**：如果需要在启动时打开终端，使用旧版 `start_instance.ps1 -OpenTerminal`

#### 对于新版用户：

1. **管道操作**：充分利用管道功能进行批量操作
2. **对象输出**：利用对象输出进行筛选、排序等操作
3. **帮助系统**：使用 `Get-Help` 查看详细文档
4. **WhatIf/Confirm**：使用 -WhatIf 测试危险操作

---

## 七、总结

### 7.1 优势对比

**新版模块优势：**
- ✅ 符合 PowerShell 标准和最佳实践
- ✅ 完整的帮助系统和文档
- ✅ 支持管道操作和对象输出
- ✅ 模块化架构，代码复用性好
- ✅ 安全性更高（SecureString）
- ✅ 错误处理更一致

**旧版脚本优势：**
- ✅ 交互式体验更好
- ✅ 批量操作支持更好
- ✅ 包格式兼容性更广
- ✅ 信息更完整（Release、User）
- ✅ 用户体验细节更周到（备份、检查等）

---

### 7.2 适用场景

**使用新版模块：**
- 自动化脚本和 CI/CD
- 需要管道操作和对象处理
- 对 PowerShell 模块化有要求
- 重视安全性（SecureString）

**使用旧版脚本：**
- 日常手动操作
- 需要交互式界面
- 需要批量操作
- 对兼容性要求高

---

### 7.3 建议改进方向

**对新版模块：**
1. 恢复交互式安装模式（可选）
2. 实现批量下载功能
3. 添加包类型自动解压支持
4. 实现 Release 和 User 信息的查询
5. 添加配置备份机制
6. 支持 -OpenTerminal 参数

**对旧版脚本：**
1. 添加 PowerShell 帮助注释
2. 使用 PowerShell 标准参数验证
3. 支持 SecureString 密码
4. 添加管道支持
5. 改进错误处理一致性

---

## 附录：完整功能对照表

| 功能类别 | 具体功能 | 旧版 | 新版 | 备注 |
|---------|---------|------|------|------|
| **实例管理** | 列出实例 | ✅ | ✅ | 输出格式不同 |
| | 安装实例 | ✅ | ✅ | 旧版交互，新版参数化 |
| | 卸载实例 | ✅ | ✅ | 旧版交互，新版参数化 |
| | 移动实例 | ✅ | ✅ | 功能基本相同 |
| | 重命名实例 | ✅ | ✅ | 旧版支持同时改路径 |
| | 启动实例 | ✅ | ✅ | 旧版支持打开终端 |
| | 停止实例 | ✅ | ✅ | 功能基本相同 |
| | 扫描实例 | ✅ | 集成到 Get-DistroNexusInstance | 无独立命令 |
| **包管理** | 列出可用包 | ✅（集成） | ✅（独立） | 新版更独立 |
| | 下载包 | ✅（批量） | ✅（单个） | 旧版支持批量 |
| | 更新目录 | ✅ | ✅ | 数据源不同 |
| **用户管理** | 设置凭证 | ✅ | ✅ | 新版使用 SecureString |
| **配置管理** | LocalPath 自动更新 | ✅ | ❌ | 旧版维护配置 |
| | instances.json 维护 | ✅ | ❌ | 旧版有本地注册表 |
| | 配置备份 | ✅ | ❌ | 旧版更安全 |
| **高级功能** | 交互式菜单 | ✅ | ❌ | 旧版体验更好 |
| | 快速安装模式 | ✅ | ❌ | 旧版更便利 |
| | 自动解压 appx/zip | ✅ | ❌ | 旧版兼容性更好 |
| | 批量操作 | ✅ | ❌ | 旧版效率更高 |
| | 打开终端启动 | ✅ | ❌ | 旧版功能更全 |
| **技术特性** | 模块清单 | ❌ | ✅ | 新版更规范 |
| | 帮助系统 | ❌ | ✅ | 新版更专业 |
| | 管道支持 | ❌ | ✅ | 新版更灵活 |
| | ShouldProcess | ❌ | ✅ | 新版更安全 |
| | SecureString | ❌ | ✅ | 新版更安全 |
| | 参数验证 | 手动 | 标准特性 | 新版更规范 |
| | 进度显示 | 手动 | Write-Progress | 新版更一致 |
| | 日志系统 | ✅ | ✅ | 功能相同 |

---

**文档版本：** 1.0
**生成日期：** 2026-01-29
**对比对象：** scripts/ vs src/PowerShell/
**DistroNexus 版本：** 2.0.0
