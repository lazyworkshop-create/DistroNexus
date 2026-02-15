---
id: automation-test-suite-manual
title: 模板自动化测试套件手册
sidebar_position: 7
---

# 模板自动化测试套件手册

最后更新：2026-02-15  
适用对象：QA 工程师、维护者、贡献者

## 1. 目标

本文说明内置模板自动化测试套件的架构、执行模式、产物和操作方式。

## 2. 入口

主要运行器 cmdlet：

- `Invoke-DistroNexusTemplateAutomation`

实现文件：

- `src/PowerShell/Public/Invoke-DistroNexusTemplateAutomation.ps1`

## 3. 套件验证内容

该套件通过“元数据驱动执行规划 + 运行时探针分类”来验证模板行为。

关键能力：

- 支持全模板与指定模板模式；
- 支持能力受限分类（`Pass` / `Fail` / `Blocked`）；
- 生成运行清单与报告产物；
- 本地优先策略与 CI 防护。

## 4. 前置条件

- 主机为 Windows 且启用 WSL2。
- 至少有一个可用发行版。
- 可导入 DistroNexus PowerShell 模块。

## 5. 命令参数面

重点参数：

- `-Mode`（`AllTemplates`、`SelectedTemplates`）
- `-TemplateIds`
- `-Distro`
- `-OutputRoot`
- `-IncludeCapabilityGated`
- `-DryRun`
- `-AllowCiOverride`
- `-UseSharedDistro`
- `-TestResultFormat`（`NUnitXml`、`JUnitXml`）

## 6. 常见用法

## 6.1 Dry Run（推荐第一步）

单模板：

```powershell
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds dotnet-dev -Distro Ubuntu -DryRun
```

多模板：

```powershell
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds dotnet-dev,nodejs-dev -Distro Ubuntu -DryRun
```

全模板：

```powershell
Invoke-DistroNexusTemplateAutomation -Mode AllTemplates -Distro Ubuntu -DryRun
```

## 6.2 全量执行（受控环境）

```powershell
Invoke-DistroNexusTemplateAutomation -Mode AllTemplates -Distro Ubuntu
```

当环境具备相关能力时，启用能力受限场景：

```powershell
Invoke-DistroNexusTemplateAutomation -Mode AllTemplates -Distro Ubuntu -IncludeCapabilityGated
```

## 7. 结果分类

- `Pass`：计划检查通过。
- `Fail`：应用/探针/清理过程出现失败。
- `Blocked`：因能力或策略限制被阻断。

在不具备必要能力（如 GPU/systemd）的环境中，出现 `Blocked` 是预期行为。

## 8. 产物结构

默认输出根目录：

- `docs/development/testing/results/`

单次运行产物：

- `summary.md`
- `run-manifest.json`
- `test-results.xml`
- `logs/*.json`

索引文件：

- `docs/development/testing/results/index.md`

## 9. 本地优先与 CI 防护

- 运行器会检测 CI 指示变量，在 CI 场景下默认跳过。
- 只有明确需要时才使用 `-AllowCiOverride`。

## 10. 故障排查

## 10.1 未发现模板

- 检查 `config/templates.json` 是否存在且可解析。
- 检查模块导入路径与 `Get-DistroNexusTemplate` 输出。

## 10.2 未知模板 ID

- 确保 `-TemplateIds` 与目录 ID 完全一致。

## 10.3 Blocked 结果

- 在 `summary.md` 中查看阻断原因。
- 对能力受限场景，使用具备能力的主机/运行时，或按需要启用相关开关。

## 10.4 运行时失败

- 检查 `logs/*.json` 与 `run-manifest.json`。
- 先缩小到较小模板集合重跑，以定位问题。

## 11. 推荐执行策略

1. 先跑单模板 dry-run。
2. 再跑相关模板组 dry-run。
3. 再跑全模板 dry-run。
4. 仅在受控环境执行非 dry-run。
5. 归档产物并在评审文档中引用。

## 12. 报告规范

报告建议包含：

- 运行 ID 与模式；
- pass/fail/blocked 计数；
- 产物路径；
- 被阻断项与原因清单。

## 13. 相关文档

- `docs/specs/built-in-template-automation-test-suite-requirements.md`
- `docs/development/template-system-implementation-checklist.md`
- `docs/development/template-system-acceptance-checklist.md`
