---
name: PowerShell模块功能补全
overview: 根据scripts/与src/PowerShell/的功能对比分析，系统地补全PowerShell模块中缺失的18项核心功能，包括交互式菜单、缓存机制、批量操作、高级安装特性等，确保新模块功能完整性的同时保持代码风格一致性和PowerShell最佳实践。
todos:
  - id: create-implementation-doc
    content: 创建PowerShell模块缺失功能实现方案文档，包含文档结构、功能分类、实现优先级和整体说明
    status: completed
  - id: implement-instance-management
    content: 编写实例管理类缺失功能实现方案（16项），包括缓存机制、交互式模式、Release/User查询、完整用户配置等功能的详细代码框架
    status: completed
    dependencies:
      - create-implementation-doc
  - id: implement-package-management
    content: 编写包管理类缺失功能实现方案（6项），包括批量下载、进度显示、包格式处理、配置备份等功能的详细代码框架
    status: completed
    dependencies:
      - create-implementation-doc
  - id: implement-user-management
    content: 编写用户管理类缺失功能实现方案（2项），包括wsl.conf处理和wheel组支持的详细代码框架
    status: completed
    dependencies:
      - create-implementation-doc
  - id: implement-other-features
    content: 编写其他缺失功能实现方案（独立扫描命令），说明功能集成策略和实现建议
    status: completed
    dependencies:
      - create-implementation-doc
  - id: add-implementation-examples
    content: 为每个缺失功能添加完整的代码示例、使用场景和测试建议
    status: completed
    dependencies:
      - implement-instance-management
      - implement-package-management
      - implement-user-management
      - implement-other-features
  - id: finalize-document
    content: 完善文档，添加总结、迁移路线图、风险评估和附录（API参考、配置文件格式等）
    status: completed
    dependencies:
      - add-implementation-examples
---

## 用户需求

根据对比文档 `scripts_vs_module_comparison.md` 中的详细分析，生成一份PowerShell新版模块缺失功能的详细实现方案文档。

## 文档要求

1. **按功能类别分组**：将缺失功能按实例管理、包管理、用户管理等类别进行系统性整理
2. **每个缺失功能详细说明**：

- 功能描述和旧版实现方式
- 缺失原因分析
- 对用户使用的影响程度
- 具体的补全实现方案

3. **实现方案要求**：

- 提供完整的代码框架和关键实现逻辑
- 保持与现有模块风格一致（CmdletBinding、标准注释块、参数验证等）
- 说明需要修改的文件路径和新增的函数
- 考虑向后兼容性和性能影响

## 文档输出

生成一份Markdown格式的技术方案文档，涵盖对比文档中识别的所有25项缺失功能，为每项提供可直接实施的代码框架和实现指南。

## 技术栈选择

- **文档格式**：Markdown
- **编程语言**：PowerShell 7.0+
- **目标架构**：PowerShell模块化结构（Private/Public分离）
- **现有依赖**：
- Get-DistroNexusConfig（配置管理）
- Write-DistroNexusLog（日志系统）
- 标准PowerShell Cmdlet特性（CmdletBinding、ShouldProcess等）

## 实施方案

### 1. 文档结构设计

基于对比分析，将25项缺失功能分为4个主要类别：

1. **实例管理功能**（16项）：涉及Get/Install/Remove/Move/Rename/Start等核心Cmdlet的增强
2. **包管理功能**（6项）：涉及Save-DistroNexusPackage和Update-DistroNexusCatalog的改进
3. **用户管理功能**（2项）：涉及Set-DistroNexusCredential的完善
4. **其他功能**（1项）：独立扫描命令的处理

### 2. 实现方案设计原则

**代码风格遵循**：

- 标准PowerShell注释块（.SYNOPSIS, .DESCRIPTION, .PARAMETER, .EXAMPLE）
- [CmdletBinding()] 和适当的特性（SupportsShouldProcess等）
- begin/process/end块结构
- try-catch错误处理
- Write-DistroNexusLog统一日志记录
- PSCustomObject输出对象

**功能增强策略**：

- **向后兼容**：新增参数使用可选默认值，保持现有调用方式不变
- **渐进增强**：优先实现影响大、风险小的功能
- **性能考量**：避免不必要的WSL实例启动（如Release/User查询）
- **配置管理**：引入instances.json本地缓存机制（可选特性）

### 3. 关键技术决策

**缓存机制实现**：

- 新增Private函数：Get-InstanceCache、Set-InstanceCache、Update-InstanceCache
- 缓存文件：config/instances.json
- 缓存策略：惰性更新，仅在显式请求时刷新（-ForceUpdate参数）
- 权衡：缓存提高性能但可能过时，通过参数让用户控制

**交互式模式支持**：

- 新增参数：-Interactive开关
- 实现方式：使用Out-GridView或自定义选择菜单
- 适用范围：Install、Remove等高频操作
- 权衡：提升用户体验但增加复杂度，作为可选特性

**批量操作支持**：

- Save-DistroNexusPackage：支持-Family参数批量下载
- 实现方式：内部循环调用单个下载逻辑
- 进度显示：使用Write-Progress显示总体进度

**包格式处理**：

- 新增Private函数：Expand-DistroPackage
- 支持格式：.appx, .zip, .tar.gz
- 实现方式：根据扩展名调用相应解压逻辑
- 工具依赖：Expand-Archive（内置）、tar命令（WSL）

### 4. 架构设计

#### 新增/修改文件清单

**Private目录新增**：

- `Cache.ps1`：实例缓存管理（Get/Set/Update-InstanceCache）
- `PackageHandler.ps1`：包格式处理（Expand-DistroPackage, Test-PackageFormat）
- `Interactive.ps1`：交互式UI辅助函数（Show-DistroSelectionMenu）

**Public目录修改**：

- `Get-DistroNexusInstance.ps1`：新增-ForceUpdate、-IncludeRelease、-IncludeUser参数
- `Install-DistroNexusInstance.ps1`：新增-Interactive、-List、-AutoDownload、完整用户配置
- `Remove-DistroNexusInstance.ps1`：新增-Interactive参数
- `Move-DistroNexusInstance.ps1`：新增非空目录检查、用户恢复逻辑
- `Rename-DistroNexusInstance.ps1`：新增-NewPath参数支持
- `Start-DistroNexusInstance.ps1`：新增-OpenTerminal、-StartPath参数
- `Save-DistroNexusPackage.ps1`：新增-Family、-All参数支持批量下载，改进进度显示
- `Set-DistroNexusCredential.ps1`：新增wsl.conf处理、wheel组支持
- `Update-DistroNexusCatalog.ps1`：新增备份机制、LocalPath保留逻辑

**Config目录**：

- `instances.json`：实例缓存文件（自动生成）

### 5. 实现优先级

**第一优先级（高影响、低风险）**：

1. 缓存机制（Get-DistroNexusInstance）
2. 进度显示改进（Save-DistroNexusPackage）
3. 非空目录检查（Move-DistroNexusInstance）
4. 配置备份（Update-DistroNexusCatalog）
5. wheel组支持（Set-DistroNexusCredential）

**第二优先级（中影响、中风险）**：

1. 批量下载（Save-DistroNexusPackage）
2. 包格式处理（Install-DistroNexusInstance）
3. -OpenTerminal参数（Start-DistroNexusInstance）
4. 完整用户配置（Install-DistroNexusInstance）
5. wsl.conf处理（Set-DistroNexusCredential）

**第三优先级（低影响或高风险）**：

1. 交互式模式（Install/Remove-DistroNexusInstance）
2. Release/User信息查询（Get-DistroNexusInstance）
3. -NewPath参数（Rename-DistroNexusInstance）
4. 自动下载（Install-DistroNexusInstance）

### 6. 性能和可靠性考虑

**性能优化**：

- 缓存减少注册表扫描频率
- 批量操作使用并行下载（可选）
- 避免不必要的WSL实例启动

**可靠性保障**：

- 配置文件操作前自动备份
- 目录操作前检查空间和权限
- 关键操作使用事务性逻辑（导出-操作-导入）

**向后兼容**：

- 所有新参数设置合理默认值
- 保持现有参数不变
- 新功能通过开关参数控制