---
id: template-development-manual
title: 模板开发手册
sidebar_position: 6
---

# 模板开发手册

最后更新：2026-02-15  
适用对象：模板贡献者与维护者

## 1. 目标

本文定义在 DistroNexus 中创建、演进、验证、维护模板的标准方式。

## 2. 仓库位置

- 元数据目录：`config/templates.json`
- 脚本资源：`config/templates/<template-id>/`
- 共享辅助：`config/templates/common/`
- 核心模型与服务：
	- `src/Client/DistroNexus.Core/Models/Template.cs`
	- `src/Client/DistroNexus.Core/Services/TemplateService.cs`

## 3. 模板编写流程

1. 在 `config/templates.json` 中定义模板元数据。
2. 在 `config/templates/<template-id>/` 下添加脚本。
3. 校验元数据完整性与脚本路径正确性。
4. 先执行选择性自动化 dry-run。
5. 必要时在受控 WSL 环境进行运行时验证。
6. 更新相关文档与检查清单。

## 4. 必需元数据

每个模板应提供：

- `Id`
- `Name`
- `Category`
- `Description`
- `CompatibleDistros`
- `Scripts`

推荐附加字段：

- `InstallMode`
- `ScenarioTags`
- `VersionOptions`
- `DefaultSelections`
- `PreflightChecks`
- `OutputArtifacts`

## 5. 脚本编写标准

## 5.1 安全性

- 仅使用相对脚本路径。
- 不要假设主机可无限制访问文件系统。
- 破坏性操作必须有明确保护条件。

## 5.2 幂等性

- 脚本应可重复执行。
- 安装/配置前做存在性检查。
- 状态转换保持明确且可预测。

## 5.3 错误行为

- 对必需依赖快速失败。
- 输出可操作错误信息。
- 仅在可接受部分完成时使用 `ContinueOnError`。

## 5.4 变量使用

- 用 `${VARIABLE_NAME}` 占位支持配置项。
- 优先由元数据提供默认值，避免脚本硬编码。

## 6. 版本感知模板设计

对于版本管理型模板：

- 定义清晰标签和默认值的 `VersionOptions`；
- 对必需选项提供 `DefaultSelections`；
- 在需要时输出项目固定文件（如 `.nvmrc`、`global.json`）。

## 7. 预检设计

预检应当：

- 在安装前验证主机/运行时能力；
- 区分必需检查与可选检查；
- 提供清晰修复建议。

典型能力受限场景：

- GPU 配置档需求；
- systemd 依赖；
- 容器运行时模式约束。

## 8. 验证流程

## 8.1 静态验证

- 校验元数据可解析与必填字段。
- 校验脚本路径均存在于 `config/templates/` 下。

## 8.2 自动化验证

使用运行器快速验证：

```powershell
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds your-template-id -Distro Ubuntu -DryRun
```

再验证相关模板集合：

```powershell
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds your-template-id,related-template -Distro Ubuntu -DryRun
```

## 8.3 运行时验证

在受控环境执行非 dry-run，并通过运行时探针确认工具可用性。

## 9. 贡献者常见错误

1. 缺少必需元数据字段。
2. 使用绝对路径或不可解析脚本路径。
3. 必需版本选项缺失默认值。
4. 能力敏感模板遗漏预检项。
5. 编写了非幂等脚本。

## 10. 模板改动完成定义

模板贡献可视为完成的条件：

- 元数据与脚本完整；
- 验证检查通过；
- 自动化产物可供审阅；
- 文档已同步更新。

## 11. PR 评审清单

- 元数据正确性与可读性；
- 脚本安全性与幂等性；
- 版本选项清晰度；
- 预检与失败行为质量；
- 是否包含验证证据。

## 12. 相关文档

- `docs/development/template-system-comprehensive-guide.md`
- `docs/development/template-system-implementation-checklist.md`
- `docs/development/template-system-acceptance-checklist.md`
- `docs/development/template-automation-test-suite-manual.md`
