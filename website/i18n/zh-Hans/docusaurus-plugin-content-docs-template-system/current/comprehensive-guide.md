---
id: comprehensive-guide
title: 综合文档
sidebar_position: 2
---

# DistroNexus 模板系统综合文档

最后更新：2026-02-15
维护者：Copilot (GPT-5.3-Codex)

## 1. 文档目标

本文档提供 DistroNexus 模板系统的端到端视图，覆盖：

- 需求分析，
- 当前实现架构，
- 内置模板目录，
- 以及贡献者/开发者指南。

它作为模板相关产品、工程、测试、文档更新的统一工作参考。

## 2. 需求分析

## 2.1 问题陈述

WSL 环境搭建在不同开发者和机器之间常常重复且不一致。模板系统通过“声明式元数据 + 可执行脚本”提供可重复的环境引导能力。

## 2.2 需求来源

本仓库中的主要需求输入：

- `docs/specs/template-system-requirements.md`
- `docs/specs/built-in-template-expansion-requirements.md`
- `docs/specs/built-in-template-automation-test-suite-requirements.md`
- `docs/development/template-expansion-implementation-task-list.md`
- `docs/development/built-in-template-automation-implementation-task-list.md`

## 2.3 功能需求（汇总）

模板系统需要提供：

1. **基于目录的发现能力**
	- 从 `config/templates.json` 加载模板。
	- 支持按 ID / 分类 / 标签进行搜索与筛选。

2. **脚本化应用流程**
	- 将模板应用到目标 WSL 实例。
	- 支持脚本顺序、超时控制、失败继续（continue-on-error）。

3. **版本感知模板**
	- 支持 `VersionOptions` 与 `DefaultSelections`，用于 SDK/通道选择。

4. **预检与受限场景保护**
	- 在模板脚本执行前运行预检。
	- 对必需检查执行强制校验并给出可操作错误。

5. **历史与可观测性**
	- 在 AppData 下持久化模板应用历史。
	- 为长任务提供进度与输出暴露。

6. **自动化测试运行器支持**
	- 通过 `Invoke-DistroNexusTemplateAutomation` 验证全部或指定模板。
	- 支持结果分类（`Pass` / `Fail` / `Blocked`）与产物输出。

## 2.4 非功能需求（汇总）

- **优先幂等**：脚本应可重复运行。
- **安全性**：禁止绝对脚本路径；阻止路径穿越。
- **本地优先验证**：完整自动化主要在本地 Windows + WSL2 执行。
- **兼容性感知**：模板声明兼容发行版与场景标签。
- **可追溯性**：自动化运行输出机器可读与 Markdown 产物。

## 2.5 当前对齐说明

- 已实现分类：`Development`、`Platform`、`CloudNative`、`Database`、`DataAndAI`。
- 扩展规范提到 `DevOps` 分类目标；当前内置目录中尚无独立 `DevOps` 分类模板。
- 以当前 15 个内置模板实现了 Phase-1 覆盖广度。

## 3. 实现总览

## 3.1 架构分层

1. **元数据层**
	- `config/templates.json` 定义模板标识、选项、检查项、脚本与产物。

2. **核心领域层**
	- `src/Client/DistroNexus.Core/Models/Template.cs`
	- 包含模板模型、脚本模型、版本选项、预检项、输出产物、结果/进度模型。

3. **服务层**
	- `src/Client/DistroNexus.Core/Interfaces/ITemplateService.cs`
	- `src/Client/DistroNexus.Core/Services/TemplateService.cs`
	- 负责加载、检索、校验、应用执行、兼容性检查、导入导出与历史持久化。

4. **桌面端工作流集成层**
	- `src/Client/DistroNexus.Desktop/Wizard/InstallWizardWorkflowViewModel.cs`
	- `SelectTemplateStep` 负责模板选择。
	- `TemplateApplyStep` 负责执行与进度展示。

5. **PowerShell 命令面层**
	- `src/PowerShell/Public/Get-DistroNexusTemplate.ps1`
	- `src/PowerShell/Public/Apply-DistroNexusTemplate.ps1`
	- `src/PowerShell/Public/Invoke-DistroNexusTemplateAutomation.ps1`
	- 由 `src/PowerShell/DistroNexus.psd1` 导出。

## 3.2 TemplateService 执行流程

`TemplateService.ApplyTemplateAsync(...)` 的高层流程：

1. 按 ID 解析模板。
2. 合并生效变量（`Variables`、`DefaultSelections`、`VersionOptions.DefaultValue`、运行时覆盖）。
3. 执行预检（必需检查失败即中止）。
4. 按 `Order` 顺序执行脚本。
5. 对 Bash 脚本在目标 WSL 实例内执行。
6. 执行过程中持续上报 `TemplateProgress`。
7. 持久化 `TemplateApplicationRecord` 历史记录。

## 3.3 安全与防护控制

当前实现包含：

- 拒绝绝对脚本路径；
- 通过根路径校验阻止路径穿越；
- 在允许根目录下解析脚本；
- 支持取消和超时传递；
- 对 `ContinueOnError` 进行显式处理。

## 3.4 持久化与运行时路径

模板相关主要 AppData 文件：

- `%APPDATA%\DistroNexus\templates.json`（用户/自定义模板缓存）
- `%APPDATA%\DistroNexus\template-application-history.json`（应用历史）

内置目录来源：

- `config/templates.json`

## 4. 内置模板目录

当前内置模板（来源 `config/templates.json`）：

| 分类 | 模板 ID | 名称 | 描述 |
|---|---|---|---|
| CloudNative | `container-runtime-dev` | 容器运行时开发 | 为 WSL2 开发流程配置容器运行时模式。 |
| CloudNative | `kubernetes-local-dev` | 本地 Kubernetes 开发 | 安装本地 Kubernetes 工具并支持集群模式选择。 |
| DataAndAI | `ai-ml-gpu-dev` | AI/ML GPU 开发 | 提供 CPU/GPU 配置档位与降级指引。 |
| Database | `database-local-stack` | 本地数据库栈 | 在 WSL2 中安装可选本地数据库服务与工具。 |
| Development | `docker-dev` | Docker 开发 | 安装 Docker Engine 与 Docker Compose 插件。 |
| Development | `dotnet-dev` | .NET 开发 | 在 Ubuntu/Debian 上安装 .NET SDK 与常用开发工具。 |
| Development | `dotnet-multi-sdk-dev` | .NET 多 SDK 开发 | 按通道选择安装 .NET SDK，并可选生成 global.json。 |
| Development | `fullstack-dev` | 全栈开发 | 在一个模板中安装 .NET、Node.js、Python、Docker。 |
| Development | `go-dev` | Go 开发 | 安装 Go 稳定工具链，支持版本固定。 |
| Development | `nodejs-dev` | Node.js 开发 | 安装 Node.js LTS、npm、yarn、pnpm。 |
| Development | `nodejs-multi-version-dev` | Node.js 多版本开发 | 通过 nvm 安装 Node.js，支持通道选择及 .nvmrc 生成。 |
| Development | `python-dev` | Python 开发 | 安装 Python 3、pip、venv、Poetry。 |
| Development | `python-multi-version-dev` | Python 多版本开发 | 通过 pyenv 安装 Python，支持项目版本固定与环境工具引导。 |
| Development | `rust-dev` | Rust 开发 | 安装 Rust 并支持 rustup 通道选择。 |
| Platform | `java-jvm-dev` | Java/JVM 开发 | 通过 SDKMAN 安装 Java SDK，并可选生成 .sdkmanrc。 |

## 5. 开发者指南

## 5.1 新增一个内置模板

1. 在 `config/templates.json` 增加模板元数据：
	- `Id`、`Name`、`Category`、`Description`、`CompatibleDistros`、`ScenarioTags`
	- `InstallMode`、`VersionOptions`、`DefaultSelections`
	- `PreflightChecks`、`OutputArtifacts`、`Scripts`

2. 在 `config/templates/<template-id>/` 下添加脚本资源。

3. 确保元数据中的脚本路径均为 `config/templates/` 下的相对路径。

4. 通过以下方式验证模型兼容性：
	- `TemplateService.ValidateTemplateAsync(...)`
	- 以及本地模板应用冒烟测试。

## 5.2 脚本编写规范

- 保持幂等（可重复执行）。
- 快速失败并输出可操作信息。
- 按需使用变量占位（`${VARIABLE_NAME}`）。
- 避免硬编码机器相关路径。
- 每个脚本步骤职责单一，通过顺序进行组合。

## 5.3 PowerShell 用法（开发/运维）

发现模板：

```powershell
Get-DistroNexusTemplate
Get-DistroNexusTemplate -Category Development
Get-DistroNexusTemplate -Id dotnet-multi-sdk-dev
```

应用模板：

```powershell
Apply-DistroNexusTemplate -InstanceName Ubuntu-22.04 -TemplateId python-dev -Verbose
Apply-DistroNexusTemplate -InstanceName Ubuntu-22.04 -TemplateId nodejs-dev -Variables @{ SDK_NODE_CHANNEL = 'lts/*' }
```

执行自动化验证：

```powershell
Invoke-DistroNexusTemplateAutomation -Mode AllTemplates -Distro Ubuntu-22.04
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds 'dotnet-dev','nodejs-dev' -Distro Ubuntu-22.04 -DryRun
```

## 5.4 测试与验证路径

推荐顺序：

1. **元数据健全性**
	- 校验 JSON 结构与必填字段。

2. **单模板本地运行**
	- 在受控发行版实例上应用目标模板。

3. **选择性自动化**
	- `Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates ...`

4. **全目录回归（按需）**
	- `Invoke-DistroNexusTemplateAutomation -Mode AllTemplates ...`

5. **产物审查**
	- 检查 `docs/development/testing/results/` 下的输出。

## 5.5 常见问题

- 使用绝对 `ScriptPath`（会被服务路径防护阻止）。
- 必需 `VersionOptions` 缺少默认值。
- 对受能力约束模板遗漏预检项。
- 脚本假设单一发行版却未在 `CompatibleDistros` 中约束。

## 6. 维护检查清单

更新模板系统时：

- 数据模型或执行行为变化时同步更新本文。
- 保持模板目录与文档一致。
- 保持发布说明与网站文档对齐。
- 维持模板相关页面的双语一致性。

## 7. 建议改进项

1. 增加显式 `DevOps` 内置模板以补齐分类。
2. 在预合并检查中加入 `config/templates.json` 的 schema 校验。
3. 增补“模板 × 发行版”的轻量兼容矩阵文档。
4. 在 `config/templates/common/` 下提供可复用脚本辅助库并规范使用约定。
