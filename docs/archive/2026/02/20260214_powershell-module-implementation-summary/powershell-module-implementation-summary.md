# PowerShell 模块缺失功能实现方案 - 总览

## 📋 文档索引

本实现方案由以下文档组成：

1. **[主文档](PowerShell-Module-Missing-Features-Implementation.md)** - 实例管理功能（16项）
2. **[第二部分](PowerShell-Module-Missing-Features-Part2.md)** - 包管理、用户管理、其他功能（9项）
3. **[示例与测试](PowerShell-Module-Implementation-Examples.md)** - 完整示例、测试用例、性能基准

---

## 🎯 功能清单快速参考

### 实例管理功能（16项）

| ID | 功能 | 优先级 | 状态 | 文档位置 |
|----|------|--------|------|----------|
| 1.1 | 缓存机制 | 🔴 高 | ✅ 已设计 | 主文档 §1.1 |
| 1.2 | 强制更新参数 | 🔴 高 | ✅ 已设计 | 主文档 §1.2 |
| 1.3 | Release/User 信息查询 | 🟡 低 | ✅ 已设计 | 主文档 §1.3 |
| 1.4 | 交互式安装模式 | 🟡 低 | ✅ 已设计 | 主文档 §1.4 |
| 1.5 | 快速安装模式 | 🟡 低 | ✅ 已设计 | 主文档 §1.5 |
| 1.6 | 列表模式集成 | 🟢 中 | ✅ 已设计 | 主文档 §1.6 |
| 1.7 | 自动下载功能 | 🟢 中 | ✅ 已设计 | 主文档 §1.7 |
| 1.8 | 包类型自动处理 | 🟢 中 | ✅ 已设计 | 主文档 §1.8 |
| 1.9 | 实例注册维护 | 🟢 中 | ✅ 已设计 | 主文档 §1.9 |
| 1.10 | 完整用户配置 | 🟢 中 | ✅ 已设计 | 主文档 §1.10 |
| 1.11 | 交互式卸载 | 🟡 低 | ✅ 已设计 | 主文档 §1.11 |
| 1.12 | 非空目录检查 | 🔴 高 | ✅ 已设计 | 第二部分 §1.12 |
| 1.13 | 用户恢复功能 | 🟢 中 | ✅ 已设计 | 第二部分 §1.13 |
| 1.14 | 路径处理增强 | 🟡 低 | ✅ 已设计 | 第二部分 §1.14 |
| 1.15 | 启动模式增强 | 🟢 中 | ✅ 已设计 | 第二部分 §1.15 |
| 1.16 | 状态更新维护 | 🟢 中 | ✅ 已设计 | 第二部分 §1.16 |

### 包管理功能（6项）

| ID | 功能 | 优先级 | 状态 | 文档位置 |
|----|------|--------|------|----------|
| 2.1 | 批量下载功能 | 🔴 高 | ✅ 已设计 | 第二部分 §2.1 |
| 2.2 | 进度显示改进 | 🔴 高 | ✅ 已设计 | 第二部分 §2.2 |
| 2.3 | 筛选参数增强 | 🟢 中 | ✅ 已设计 | 第二部分 §2.3 |
| 2.4 | 配置备份机制 | 🔴 高 | ✅ 已设计 | 第二部分 §2.4 |
| 2.5 | LocalPath 保留逻辑 | 🔴 高 | ✅ 已设计 | 第二部分 §2.5 |
| 2.6 | .NET HttpClient 支持 | 🟢 中 | ✅ 已设计 | 第二部分 §2.6 |

### 用户管理功能（2项）

| ID | 功能 | 优先级 | 状态 | 文档位置 |
|----|------|--------|------|----------|
| 3.1 | wsl.conf 处理增强 | 🟢 中 | ✅ 已设计 | 第二部分 §3.1 |
| 3.2 | wheel 组支持 | 🔴 高 | ✅ 已设计 | 第二部分 §3.2 |

### 其他功能（1项）

| ID | 功能 | 优先级 | 状态 | 文档位置 |
|----|------|--------|------|----------|
| 4.1 | 独立扫描命令 | 🟡 低 | ✅ 已设计 | 第二部分 §4.1 |

**总计**: 25项功能 | 高优先级: 7 | 中优先级: 11 | 低优先级: 7

---

## 📁 新增/修改文件清单

### 新增 Private 函数

| 文件 | 函数 | 用途 |
|------|------|------|
| `Private/Cache.ps1` | `Get-InstanceCache` | 读取实例缓存 |
| | `Set-InstanceCache` | 写入实例缓存 |
| | `Update-InstanceCache` | 更新单个实例缓存 |
| | `Remove-InstanceFromCache` | 从缓存移除实例 |
| `Private/Interactive.ps1` | `Show-DistroSelectionMenu` | 交互式发行版选择 |
| | `Get-DistroCachePath` | 获取缓存路径 |
| `Private/PackageHandler.ps1` | `Expand-DistroPackage` | 解压各种包格式 |
| | `Test-PackageFormat` | 检查包格式支持 |
| `Private/UserConfig.ps1` | `Set-DistroDefaultUser` | 完整用户配置 |
| | `Restore-DistroDefaultUser` | 恢复默认用户 |
| | `Add-UserToSudoGroup` | 添加到sudo/wheel组 |

### 修改 Public 函数

| 文件 | 新增参数 | 功能增强 |
|------|----------|----------|
| `Get-DistroNexusInstance.ps1` | `-UseCache`, `-ForceUpdate`, `-IncludeRelease`, `-IncludeUser` | 缓存支持、扩展信息查询 |
| `Install-DistroNexusInstance.ps1` | `-Interactive`, `-Quick`, `-List`, `-AutoDownload`, `-InstanceName` | 交互式、快速模式、自动下载 |
| `Remove-DistroNexusInstance.ps1` | `-Interactive` | 交互式卸载 |
| `Move-DistroNexusInstance.ps1` | `-Force` | 非空目录检查、用户恢复 |
| `Rename-DistroNexusInstance.ps1` | `-NewPath` | 路径处理增强、用户恢复 |
| `Start-DistroNexusInstance.ps1` | `-OpenTerminal`, `-StartPath` | 终端启动、工作目录 |
| `Stop-DistroNexusInstance.ps1` | *(内部改进)* | 状态缓存更新 |
| `Save-DistroNexusPackage.ps1` | `-Family`, `-All`, `-Parallel` | 批量下载、并行支持 |
| `Set-DistroNexusCredential.ps1` | `-UpdateWslConf` | wsl.conf处理、wheel组 |
| `Update-DistroNexusCatalog.ps1` | `-NoBackup`, `-PreserveLocalPath` | 备份机制、LocalPath保留 |

### 新增 Public 函数

| 函数 | 别名 | 用途 |
|------|------|------|
| `Sync-DistroNexusCache` | `Scan-DistroNexusInstances` | 显式缓存同步 |

---

## 🚀 实施路线图

### 阶段 1: 核心基础（第 1-2 周）

**目标**: 实现关键的高优先级功能，建立稳定基础

**任务**:
- [ ] 实现缓存系统（Cache.ps1）
- [ ] 修改 Get-DistroNexusInstance 添加缓存支持
- [ ] 实现 Move-DistroNexusInstance 非空目录检查
- [ ] 实现 Update-DistroNexusCatalog 备份机制
- [ ] 实现 Save-DistroNexusPackage 批量下载
- [ ] 添加 wheel 组支持到用户配置
- [ ] 编写单元测试（Pester）

**交付物**:
- Cache.ps1 (完整)
- 修改后的 Get/Move/Update/Save 函数
- 单元测试套件（覆盖率 > 70%）

---

### 阶段 2: 用户体验增强（第 3-4 周）

**目标**: 改进用户体验和自动化能力

**任务**:
- [ ] 实现 PackageHandler.ps1（包格式处理）
- [ ] 实现 UserConfig.ps1（完整用户配置）
- [ ] 添加 Start-DistroNexusInstance 终端启动
- [ ] 实现用户恢复功能（Move/Rename）
- [ ] 添加 Install-DistroNexusInstance 自动下载
- [ ] 实现进度显示改进（HttpClient）
- [ ] 集成测试

**交付物**:
- PackageHandler.ps1 (完整)
- UserConfig.ps1 (完整)
- 修改后的 Start/Install 函数
- 集成测试套件

---

### 阶段 3: 高级特性（第 5-6 周）

**目标**: 实现交互式模式和高级功能

**任务**:
- [ ] 实现 Interactive.ps1（交互式UI）
- [ ] 添加 Install-DistroNexusInstance 交互式模式
- [ ] 添加快速安装模式
- [ ] 添加 Remove-DistroNexusInstance 交互式模式
- [ ] 实现 Release/User 信息查询
- [ ] 添加 Rename-DistroNexusInstance 路径处理
- [ ] 实现独立扫描命令
- [ ] 性能优化

**交付物**:
- Interactive.ps1 (完整)
- 所有交互式功能
- 性能基准报告

---

### 阶段 4: 文档和发布（第 7 周）

**目标**: 完善文档，准备发布

**任务**:
- [ ] 更新 PowerShell-Module.md
- [ ] 创建迁移指南
- [ ] 编写故障排除文档
- [ ] 录制视频教程
- [ ] 性能测试报告
- [ ] 发布说明

**交付物**:
- 完整文档集
- 迁移指南
- 发布包（v2.1.0）

---

## ⚠️ 风险评估与缓解策略

### 高风险项

#### 1. 缓存数据一致性

**风险**: 缓存与系统状态不同步导致错误操作

**缓解策略**:
- 实现自动缓存失效机制
- 关键操作前强制刷新
- 提供明确的缓存刷新命令
- 添加缓存版本标识

**代码示例**:
```powershell
# 在缓存中添加时间戳和版本
$cache = @{
    Version = "2.1.0"
    Timestamp = Get-Date
    Instances = $instanceList
}

# 读取时验证缓存有效性
if ($cache.Version -ne $ModuleVersion -or 
    ((Get-Date) - $cache.Timestamp).TotalMinutes -gt 30) {
    # 缓存过期，强制刷新
    $instances = Get-DistroNexusInstance -ForceUpdate
}
```

#### 2. 包格式处理兼容性

**风险**: 不同发行版使用不同的包格式，可能出现解压失败

**缓解策略**:
- 详细的格式检测逻辑
- 优雅的错误处理和回退机制
- 充分的格式兼容性测试
- 提供手动解压选项

**测试计划**:
- 测试 .tar, .tar.gz, .zip, .appx 格式
- 测试嵌套归档（appx 中的 install.tar.gz）
- 测试损坏的包文件

#### 3. 用户配置跨发行版兼容性

**风险**: 不同发行版的用户管理命令差异

**缓解策略**:
- 检测发行版类型
- 使用条件逻辑处理差异
- 提供发行版特定的配置选项
- 详细的日志记录

**发行版差异表**:

| 发行版 | sudo 组 | wheel 组 | useradd 选项 | 备注 |
|--------|---------|----------|--------------|------|
| Ubuntu/Debian | ✅ sudo | ❌ | `-m -s /bin/bash` | 标准 |
| CentOS/RHEL | ❌ | ✅ wheel | `-m -s /bin/bash` | 使用 wheel |
| Alpine | ✅ wheel | ❌ | `-m -s /bin/ash` | 使用 ash |
| Arch | ✅ wheel | ❌ | `-m -s /bin/bash` | 使用 wheel |

### 中风险项

#### 4. 交互式模式与自动化冲突

**风险**: 交互式功能可能影响自动化脚本

**缓解策略**:
- 使用独立的参数集
- 默认保持非交互式行为
- 在交互模式中提供 `-Force` 跳过确认

#### 5. 性能回退

**风险**: 新功能可能导致性能下降

**缓解策略**:
- 性能基准测试
- 可选的性能优化开关
- 异步操作（如并行下载）
- 定期性能审查

---

## 📊 配置文件格式

### instances.json（缓存文件）

```json
{
  "Version": "2.1.0",
  "Timestamp": "2026-01-29T14:30:00",
  "Instances": [
    {
      "Name": "Ubuntu-22.04",
      "State": "Running",
      "Version": "2",
      "BasePath": "D:\\WSL\\Ubuntu-22.04",
      "DiskSize": 5368709120,
      "InstallTime": "2026-01-15T10:20:00",
      "Release": "Ubuntu 22.04.3 LTS",
      "DefaultUser": "developer",
      "Guid": "{12345678-1234-1234-1234-123456789012}"
    }
  ]
}
```

### settings.json（配置文件）

```json
{
  "DefaultDistro": "Ubuntu-22.04",
  "DefaultInstallPath": "D:\\WSL",
  "DefaultUsername": "developer",
  "AutoDownload": true,
  "DistroCachePath": "D:\\WSL\\cache",
  "InstancesCacheFile": "config/instances.json",
  "DistroCatalogFile": "config/distros.json",
  "CacheExpirationMinutes": 30,
  "EnableAutoBackup": true,
  "MaxBackupCount": 5,
  "ParallelDownloadEnabled": false,
  "DetailedProgressEnabled": true
}
```

---

## 🔧 API 参考

### 新增参数完整列表

#### Get-DistroNexusInstance

```powershell
Get-DistroNexusInstance 
    [-Name <String>]
    [-UseCache]
    [-ForceUpdate]
    [-IncludeRelease]
    [-IncludeUser]
    [<CommonParameters>]
```

#### Install-DistroNexusInstance

```powershell
Install-DistroNexusInstance 
    -DistroName <String>
    -InstallPath <String>
    [-Interactive]
    [-Quick]
    [-List]
    [-AutoDownload]
    [-InstanceName <String>]
    [-Username <String>]
    [-Password <SecureString>]
    [-WhatIf]
    [-Confirm]
    [<CommonParameters>]
```

#### Save-DistroNexusPackage

```powershell
Save-DistroNexusPackage 
    -Name <String>
    [-Family <String>]
    [-All]
    [-Destination <String>]
    [-Parallel]
    [-WhatIf]
    [-Confirm]
    [<CommonParameters>]
```

---

## 📈 预期改进指标

### 性能改进

- **查询速度**: 使用缓存后提升 **60-80%**
- **批量下载**: 并行下载比串行快 **2-3x**
- **操作延迟**: 减少不必要的 WSL 启动，整体操作延迟降低 **40%**

### 用户体验

- **学习曲线**: 交互式模式降低新用户学习成本 **50%**
- **操作步骤**: 自动下载功能减少手动步骤 **30%**
- **错误率**: 增强的验证和检查减少操作错误 **70%**

### 代码质量

- **测试覆盖率**: 目标 **>80%**
- **文档完整性**: 所有公共函数 **100%** 文档化
- **代码重用**: 通过私有函数减少重复代码 **40%**

---

## 🎓 迁移指南

### 从旧版脚本迁移

**步骤 1: 评估依赖**

检查当前使用的脚本和功能：

```powershell
# 列出所有脚本调用
Get-ChildItem -Path "scripts\" -Filter "*.ps1" | Select-Object Name

# 检查自动化脚本中的引用
Select-String -Path "*.ps1","*.bat" -Pattern "scripts\\.*\.ps1"
```

**步骤 2: 更新调用**

使用映射表更新命令：

| 旧版脚本 | 新版 Cmdlet | 说明 |
|---------|------------|------|
| `./list_distros.ps1` | `Get-DistroNexusInstance` | 添加 `-UseCache` 提升性能 |
| `./install_wsl_custom.ps1` | `Install-DistroNexusInstance -Interactive` | 使用交互式模式 |
| `./install_wsl_custom.ps1 -name X` | `Install-DistroNexusInstance -Quick -InstanceName X` | 快速模式 |
| `./download_all_distros.ps1` | `Save-DistroNexusPackage -All` | 添加 `-Parallel` 加速 |
| `./start_instance.ps1 -OpenTerminal` | `Start-DistroNexusInstance -OpenTerminal` | 功能相同 |
| `./scan_wsl_instances.ps1` | `Sync-DistroNexusCache` | 新命令 |

**步骤 3: 测试迁移**

```powershell
# 1. 并行运行测试
$oldResult = & "scripts\list_distros.ps1"
$newResult = Get-DistroNexusInstance

# 2. 比较结果
Compare-Object $oldResult $newResult -Property Name, State

# 3. 验证关键功能
Install-DistroNexusInstance -List
Save-DistroNexusPackage -Family "Ubuntu" -Verbose
```

**步骤 4: 更新文档和培训**

- 更新内部文档
- 培训团队成员
- 创建示例脚本库

---

## 📚 相关资源

### 官方文档

- [PowerShell Module Documentation](./PowerShell-Module.md)
- [对比分析](./scripts_vs_module_comparison.md)
- [功能状态报告](./功能实现状态检查报告.md)

### 外部参考

- [PowerShell Best Practices](https://docs.microsoft.com/powershell/scripting/dev-cross-plat/writing-portable-modules)
- [Pester Testing Framework](https://pester.dev/)
- [WSL Documentation](https://docs.microsoft.com/windows/wsl/)

---

## 🤝 贡献指南

### 实现新功能

1. **选择功能**: 从清单中选择未实现的功能
2. **创建分支**: `git checkout -b feature/implement-xxx`
3. **编写代码**: 遵循现有代码风格
4. **添加测试**: 单元测试和集成测试
5. **更新文档**: 函数帮助和用户文档
6. **提交PR**: 附带完整的测试报告

### 代码审查清单

- [ ] 符合 PowerShell 最佳实践
- [ ] 包含完整的注释块（.SYNOPSIS等）
- [ ] 参数验证完整
- [ ] 错误处理健壮
- [ ] 单元测试覆盖 > 80%
- [ ] 文档更新
- [ ] 向后兼容

---

## 📞 联系与支持

### 问题报告

- GitHub Issues: [项目地址]
- 邮件: lazyworkshop.deron@gmail.com

### 社区

- Discord: [社区频道]
- 论坛: [讨论区]

---

**文档版本**: 1.0  
**创建日期**: 2026-01-29  
**维护者**: DistroNexus Team  
**许可证**: MIT

---

## ✅ 文档完成状态

- ✅ 主文档：实例管理功能（16项）
- ✅ 第二部分：包管理、用户管理、其他功能（9项）
- ✅ 示例与测试：使用场景、测试用例、性能基准
- ✅ 总览文档：索引、路线图、风险评估、配置格式

**总页数**: 约 150 页（合并后）  
**代码示例**: 80+ 个  
**测试用例**: 30+ 个  
**功能覆盖**: 100%（25/25项）

🎉 **文档完成！可以开始实施了！**
