---
id: system-design
title: 系统设计
sidebar_position: 4
---

# 模板系统设计（当前实现）

最后更新：2026-02-15  
状态：Active

## 1. 概述

本文描述 DistroNexus 模板系统的当前实现设计，涵盖元数据、核心服务、桌面端流程集成、PowerShell 命令面与自动化验证。

## 2. 架构分层

## 2.1 元数据层

主数据源：

- `config/templates.json`

关键职责：

- 模板标识与分类定义；
- 脚本顺序与执行元数据；
- 版本/通道选项与默认值；
- 预检项与输出产物声明。

## 2.2 核心领域模型层

主要模型文件：

- `src/Client/DistroNexus.Core/Models/Template.cs`

核心实体：

- `Template`
- `TemplateScript`
- `TemplateVersionOption` / `TemplateOptionValue`
- `TemplatePreflightCheck`
- `TemplateOutputArtifact`
- `TemplateApplicationResult`
- `TemplateProgress`
- `TemplateValidationResult`

## 2.3 服务层

服务契约：

- `src/Client/DistroNexus.Core/Interfaces/ITemplateService.cs`

服务实现：

- `src/Client/DistroNexus.Core/Services/TemplateService.cs`

核心能力：

- 目录加载与缓存；
- 模板检索；
- 应用流程编排；
- 校验与兼容性检查；
- 自定义模板导入导出；
- 应用历史持久化。

## 2.4 桌面端集成层

DI 注册：

- `src/Client/DistroNexus.Desktop/App.xaml.cs`

向导集成：

- `src/Client/DistroNexus.Desktop/Wizard/InstallWizardWorkflowViewModel.cs`
- `src/Client/DistroNexus.Desktop/Wizard/Steps/SelectTemplateStep.cs`
- `src/Client/DistroNexus.Desktop/Wizard/Steps/TemplateApplyStep.cs`

## 2.5 PowerShell 命令面层

模块导出：

- `src/PowerShell/DistroNexus.psd1`

主要 cmdlet：

- `Get-DistroNexusTemplate`
- `Apply-DistroNexusTemplate`
- `Invoke-DistroNexusTemplateAutomation`

## 3. 运行时数据与存储

## 3.1 内置元数据源

- `config/templates.json`

## 3.2 用户/运行时数据（AppData）

- `%APPDATA%\DistroNexus\templates.json`（用户模板缓存）
- `%APPDATA%\DistroNexus\template-application-history.json`（执行历史）

## 4. 核心执行序列

执行入口：

- `TemplateService.ApplyTemplateAsync(templateId, instanceName, variables, progress, cancellationToken)`

序列：

1. 按 ID 解析模板。
2. 构建生效变量集合。
3. 执行预检。
4. 按 `Order` 顺序执行脚本。
5. 通过 `TemplateProgress` 上报进度。
6. 持久化历史记录。

## 5. 变量解析策略

生效变量按以下顺序合并：

1. `Template.Variables`
2. `Template.DefaultSelections`
3. `Template.VersionOptions.DefaultValue`（若缺失）
4. 调用时传入的运行时覆盖变量

后来源覆盖前来源。

## 6. 预检设计

预检由 `TemplatePreflightCheck` 建模，并在脚本执行前运行。

行为规则：

- 必需检查失败 => 中止应用流程。
- 可选检查失败 => 记录告警并继续。
- 通过 `AppliesToVariable` / `AppliesToValue` 支持条件适用。

## 7. 脚本执行设计

## 7.1 脚本来源

脚本来源可为：

- 内联内容（`Content`），或
- 相对路径（`ScriptPath`，通过允许根目录解析）。

## 7.2 脚本类型

- Bash 脚本在目标 WSL 实例内执行。
- PowerShell 脚本在主机上下文执行。

## 7.3 顺序与失败处理

- 脚本按 `Order` 排序执行。
- 每个脚本可配置超时（`TimeoutSeconds`）。
- `ContinueOnError` 决定失败脚本是否中止整体流程。

## 8. 安全模型

当前路径与执行防护包括：

- 拒绝绝对脚本路径；
- 检测路径穿越；
- 强制脚本解析在允许根目录下。

这些控制用于防止模板执行期间的脚本路径注入。

## 9. 桌面端用户流程集成

向导中的模板流程：

1. 用户进入模板选择步骤。
2. 用户选择模板（或按流程支持跳过）。
3. 应用步骤执行模板并展示状态/进度输出。
4. 完成状态回传到最终流程状态。

## 10. PowerShell 集成设计

## 10.1 发现

`Get-DistroNexusTemplate` 按 ID/分类读取并筛选模板目录。

## 10.2 应用

`Apply-DistroNexusTemplate` 解析模板并在目标实例上执行脚本，支持变量覆盖。

## 10.3 自动化验证

`Invoke-DistroNexusTemplateAutomation` 支持：

- `AllTemplates` 或 `SelectedTemplates` 模式；
- `DryRun`；
- 能力受限分类（`Blocked`）；
- CI 防护与显式覆盖；
- 结果产物（`test-results.xml`、`run-manifest.json`、`summary.md`）。

## 11. 内置模板拓扑（当前）

当前分类分布：

- `Development`：10
- `Platform`：1
- `CloudNative`：2
- `Database`：1
- `DataAndAI`：1

差距说明：

- `DevOps` 分类当前未作为独立内置分类呈现。

## 12. 设计权衡与约束

1. **本地优先验证，而非 CI 优先运行**
	- 对 WSL 依赖工作负载更安全、可控。

2. **脚本灵活性 + 路径约束**
	- 在保证扩展性的同时维持安全边界。

3. **元数据驱动扩展**
	- 新增模板时尽量减少核心代码变更。

## 13. 已知限制

- 能力受限模板会因主机能力不同被标记为 `Blocked`。
- 如需严格分类对齐，仍需补齐 `DevOps` 内置分类。

## 14. 相关文档

- `docs/development/template-system-comprehensive-guide.md`
- `docs/specs/template-system-requirements-analysis.md`
- `docs/development/template-system-implementation-checklist.md`
- `docs/development/template-system-acceptance-checklist.md`
