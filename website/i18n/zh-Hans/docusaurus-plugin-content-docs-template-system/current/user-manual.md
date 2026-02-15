---
id: user-manual
title: 用户手册
sidebar_position: 5
---

# 模板系统用户手册

最后更新：2026-02-15  
适用对象：终端用户与运维人员

## 1. 模板系统做什么

模板系统可以通过一次引导流程，把 WSL 实例转换为可直接开发的工作环境。

典型收益：

- 安装语言/运行时工具链；
- 应用常见开发栈默认配置；
- 在不同机器之间保持一致的搭建步骤。

## 2. 前置条件

- Windows 10/11 且已启用 WSL2。
- 至少一个目标 WSL 发行版实例（如 Ubuntu）。
- 已安装 DistroNexus 且可使用其 PowerShell 模块。

## 3. 快速开始

## 3.1 在桌面向导中

1. 启动实例安装流程。
2. 选择发行版与基础选项。
3. 在模板选择步骤中选择模板。
4. 进入应用步骤并观察进度。
5. 查看完成状态与输出。

## 3.2 在 PowerShell 中

导入模块：

```powershell
Import-Module "C:\Program Files\DistroNexus\PowerShell\DistroNexus.psm1"
```

列出模板：

```powershell
Get-DistroNexusTemplate
Get-DistroNexusTemplate -Category Development
```

应用单个模板：

```powershell
Apply-DistroNexusTemplate -InstanceName Ubuntu-22.04 -TemplateId python-dev -Verbose
```

带变量应用：

```powershell
Apply-DistroNexusTemplate -InstanceName Ubuntu-22.04 -TemplateId nodejs-multi-version-dev -Variables @{ SDK_NODE_CHANNEL = 'lts/*' }
```

## 4. 内置模板分类

当前分类：

- `Development`
- `Platform`
- `CloudNative`
- `Database`
- `DataAndAI`

建议先按分类筛选，再选取最匹配的模板。

## 5. 常见使用流程

## 5.1 语言环境引导

- 选择一个语言模板（如 `.NET`、`Node.js`、`Python`、`Rust`、`Go`）。
- 应用到全新或现有开发实例。

## 5.2 全栈环境引导

- 选择 `fullstack-dev` 一次性完成组合栈安装。

## 5.3 本地平台栈配置

- 按场景选择 `container-runtime-dev` / `kubernetes-local-dev` / `database-local-stack`。

## 5.4 AI/ML 配置档

- 使用 `ai-ml-gpu-dev`，并按主机能力选择配置档。

## 6. 进度与结果解读

应用过程中会展示：

- 当前脚本；
- 已完成/总脚本数；
- 状态消息；
- 最新输出行。

整体成功表示脚本按策略完成。如果某脚本启用了 `ContinueOnError`，则可能在整体成功时仍出现警告。

## 7. 安全提示

- 模板会执行脚本，请将自定义模板视为可执行代码。
- 对外部来源模板，应先审核脚本再应用。
- 在可控环境中优先使用 dry-run 验证流程。

## 8. 故障排查

## 8.1 找不到模板

- 使用 `Get-DistroNexusTemplate` 核对 ID。
- 检查分类/ID 过滤条件是否正确。

## 8.2 预检失败

- 根据错误信息确认缺失能力或依赖。
- 修复前置条件后重试。

## 8.3 能力受限导致 Blocked

- GPU/systemd 相关场景可能被主机能力限制。
- 选择适配配置档，或补齐能力后重试。

## 8.4 脚本执行失败

- 使用 `-Verbose` 重跑并检查输出。
- 检查目标发行版与软件源是否可达。

## 9. 常用命令

```powershell
# 列出全部模板
Get-DistroNexusTemplate

# 列出某分类
Get-DistroNexusTemplate -Category CloudNative

# 应用模板
Apply-DistroNexusTemplate -InstanceName Ubuntu -TemplateId dotnet-dev -Verbose

# 对指定模板做 dry-run 自动化验证
Invoke-DistroNexusTemplateAutomation -Mode SelectedTemplates -TemplateIds dotnet-dev,nodejs-dev -Distro Ubuntu -DryRun
```

## 10. FAQ

## Q1：可以对已有实例应用模板吗？

可以。使用 `Apply-DistroNexusTemplate` 并指定已有 `-InstanceName`。

## Q2：可以一次验证所有模板吗？

可以。使用 `Invoke-DistroNexusTemplateAutomation -Mode AllTemplates`。

## Q3：为什么看到 `Blocked` 而不是 `Fail`？

`Blocked` 表示能力受限（如 GPU/systemd 约束），不是脚本执行失败。

## 11. 相关文档

- `docs/development/template-system-comprehensive-guide.md`
- `docs/development/template-system-acceptance-checklist.md`
- `docs/development/template-automation-test-suite-manual.md`
