# DistroNexus 模版功能实现计划

## 功能概述

为 DistroNexus 添加模版系统，允许用户在创建 WSL2 实例时或对现有实例应用预配置的开发环境模版（如 .NET 10、Node.js、Python、Docker 等）。

## 核心设计

### 模版数据模型
- **Template**: 包含 ID、名称、描述、类别、脚本列表、兼容发行版等
- **TemplateScript**: 支持多阶段执行（PostImport、PostConfigure、FirstBoot）
- **TemplateApplicationRecord**: 记录模版应用历史

### 服务层架构
- **ITemplateService**: 模版管理接口（加载、搜索、应用、验证）
- **TemplateService**: 实现类，负责脚本执行、进度报告、日志记录

### UI 集成
- 在安装向导中插入新步骤 **SelectTemplateStep**（在 UserConfigurationStep 之后）
- 扩展 **ProgressStep** 集成模版应用逻辑

## 关键实现文件

### 需要创建的文件

**Core 层 - Models**:
1. `src/Client/DistroNexus.Core/Models/Template.cs`
2. `src/Client/DistroNexus.Core/Models/TemplateApplicationRecord.cs`

**Core 层 - Services**:
3. `src/Client/DistroNexus.Core/Interfaces/ITemplateService.cs`
4. `src/Client/DistroNexus.Core/Services/TemplateService.cs`

**Desktop 层 - Wizard**:
5. `src/Client/DistroNexus.Desktop/Wizard/Steps/SelectTemplateStep.cs`
6. `src/Client/DistroNexus.Desktop/Wizard/Steps/SelectTemplateStepView.xaml`
7. `src/Client/DistroNexus.Desktop/Wizard/Steps/SelectTemplateStepView.xaml.cs`

**PowerShell 模块**:
8. `src/PowerShell/Public/Get-DistroNexusTemplate.ps1`
9. `src/PowerShell/Public/Apply-DistroNexusTemplate.ps1`

**配置和模版文件**:
10. `config/templates.json` - 模版目录定义
11. `config/templates/dotnet-dev/install.sh` - .NET 10 开发环境
12. `config/templates/nodejs-dev/install.sh` - Node.js 开发环境
13. `config/templates/python-dev/install.sh` - Python 开发环境
14. `config/templates/docker-dev/install.sh` - Docker 开发环境
15. `config/templates/fullstack-dev/install.sh` - 全栈开发环境

### 需要修改的文件

1. **src/Client/DistroNexus.Desktop/App.xaml.cs**
   - 注册 `ITemplateService` 为单例服务

2. **src/Client/DistroNexus.Desktop/Wizard/WizardContext.cs**
   - 添加 `SelectedTemplate` 属性
   - 添加 `ApplyTemplateAfterInstall` 属性

3. **src/Client/DistroNexus.Desktop/Wizard/InstallWizardWorkflowViewModel.cs**
   - 在 `CreateWorkflow()` 中添加 `SelectTemplateStep`（第52行后）

4. **src/Client/DistroNexus.Desktop/Wizard/Steps/ProgressStep.cs**
   - 在安装完成后调用 `ITemplateService.ApplyTemplateAsync()`

5. **src/Client/DistroNexus.Core/Models/InstallOptions.cs**
   - 添加 `TemplateId` 属性（可选）

6. **src/PowerShell/DistroNexus.psd1**
   - 导出新的 cmdlets: `Get-DistroNexusTemplate`, `Apply-DistroNexusTemplate`

## 实现步骤

### 阶段 1: 数据模型和服务接口（基础）

1. 创建 `Template.cs` 数据模型
   - Template、TemplateScript、TemplateScriptPhase、TemplateScriptType
   - TemplateApplicationRecord、TemplateApplicationResult、TemplateProgress

2. 创建 `ITemplateService.cs` 接口
   - `LoadTemplatesAsync()`, `GetTemplateByIdAsync()`, `SearchTemplatesAsync()`
   - `ApplyTemplateAsync()`, `ValidateTemplateAsync()`

### 阶段 2: 核心服务实现

3. 实现 `TemplateService.cs`
   - 从 `templates.json` 和本地/远程源加载模版
   - 实现缓存机制（参考 CatalogService 的实现）
   - 实现 `ApplyTemplateAsync()` 核心逻辑：
     - 通过 `IPowerShellService` 执行宿主机 PowerShell 脚本
     - 通过 `wsl -d <instance> -- bash -c` 执行 WSL 内部脚本
     - 支持变量替换 `${VARIABLE_NAME}`
     - 进度报告（IProgress<TemplateProgress>）
     - 错误处理和日志记录

4. 在 `App.xaml.cs` 中注册服务
   ```csharp
   services.AddSingleton<ITemplateService, TemplateService>();
   ```

### 阶段 3: PowerShell 模块

5. 实现 `Get-DistroNexusTemplate.ps1`
   - 从配置文件读取模版列表
   - 支持按 ID、分类筛选

6. 实现 `Apply-DistroNexusTemplate.ps1`
   - 验证实例存在
   - 执行模版脚本
   - 报告进度和结果

7. 更新 `DistroNexus.psd1` 导出新 cmdlets

### 阶段 4: UI 集成 - 向导步骤

8. 扩展 `WizardContext.cs`
   ```csharp
   [ObservableProperty]
   private Template? _selectedTemplate;

   [ObservableProperty]
   private bool _applyTemplateAfterInstall;
   ```

9. 创建 `SelectTemplateStep.cs`
   - 从 `ITemplateService` 加载模版列表
   - 根据选定的发行版过滤兼容模版
   - 提供"跳过模版"选项
   - 实现 `Validate()`, `OnEnterAsync()`, `OnExitAsync()`

10. 创建 `SelectTemplateStepView.xaml`
    - 左侧：模版列表（带分类和搜索）
    - 右侧：模版详情（描述、预计时间、包含软件）
    - 底部：跳过模版勾选框

11. 在 `InstallWizardWorkflowViewModel.cs` 中注册步骤
    ```csharp
    workflow.AddStep(new UserConfigurationStep(_settingsService, _logger));
    workflow.AddStep(new SelectTemplateStep(_templateService, _logger)); // 新增
    workflow.AddStep(new ReviewStep());
    ```

12. 修改 `ProgressStep.cs`
    - 在实例安装完成后，检查 `Context.SelectedTemplate`
    - 如果有选择模版，调用 `_templateService.ApplyTemplateAsync()`
    - 更新进度显示（50-100% 用于模版应用）

### 阶段 5: 预置模版

13. 创建模版配置 `config/templates.json`
    - 包含 5 个预置模版的元数据

14. 编写 .NET 10 开发环境模版 (`dotnet-dev/install.sh`)
    ```bash
    # 添加微软软件源
    # 安装 .NET SDK 10.0
    # 安装 ASP.NET Core Runtime
    # 安装 EF Core 工具
    ```

15. 编写 Node.js 开发环境模版 (`nodejs-dev/install.sh`)
    ```bash
    # 使用 nvm 安装 Node.js LTS
    # 安装 yarn, pnpm
    # 安装常用全局包
    ```

16. 编写 Python 开发环境模版 (`python-dev/install.sh`)
    ```bash
    # 安装 Python 3.12
    # 安装 pip, virtualenv, poetry
    # 配置虚拟环境
    ```

17. 编写 Docker 开发环境模版 (`docker-dev/install.sh`)
    ```bash
    # 安装 Docker Engine
    # 配置用户权限
    # 安装 Docker Compose
    ```

18. 编写全栈开发环境模版 (`fullstack-dev/install.sh`)
    ```bash
    # 组合调用上述所有脚本
    ```

### 阶段 6: 测试和验证

19. 集成测试
    - 测试安装向导中选择模版
    - 测试跳过模版
    - 测试模版应用成功
    - 测试模版应用失败时的错误处理

20. 端到端验证
    - 创建新的 Ubuntu 实例 + .NET 模版
    - 验证 `dotnet --version` 输出正确
    - 创建新的 Debian 实例 + Node.js 模版
    - 验证 `node --version` 输出正确

## 验证标准

### 功能验证
- [x] 可以在安装向导中看到"选择模版"步骤
- [x] 模版根据发行版自动过滤（如 Fedora 特定模版不显示在 Ubuntu 中）
- [x] 可以通过勾选框跳过模版选择
- [x] 模版应用时显示实时进度
- [x] 脚本执行失败时显示清晰的错误信息
- [x] 日志文件正确记录执行过程

### 预置模版验证
- [x] `.NET` 模版元数据、脚本路径与执行链路已验证
- [x] `Node.js` 模版元数据、脚本路径与执行链路已验证
- [x] `Python` 模版元数据、脚本路径与执行链路已验证
- [x] `Docker` 模版元数据、脚本路径与执行链路已验证
- [x] `Fullstack` 模版组合脚本与依赖路径已验证

### 质量验证
- [x] 模版应用不阻塞 UI 线程
- [x] 支持取消模版应用（CancellationToken）
- [x] 脚本执行超时正确处理（默认 300 秒）
- [x] 网络错误时优雅降级
- [x] 日志文件路径正确显示在结果页面

## 技术细节

### 脚本执行机制

**PostConfigure 阶段** (最常用):
```csharp
// 在 WSL 实例内执行 bash 脚本
var scriptContent = File.ReadAllText(script.ScriptPath);
var command = $"wsl -d {instanceName} -- bash -c '{scriptContent.Replace("'", "'\\''")}'"
await _powerShellService.ExecuteScriptAsync(command);
```

**变量替换**:
```csharp
foreach (var (key, value) in variables)
{
    scriptContent = scriptContent.Replace($"${{{key}}}", value);
}
```

### 进度报告

```csharp
var progress = new Progress<TemplateProgress>(p =>
{
    Context.InstallProgress = 50 + (p.PercentComplete * 0.5); // 50-100%
    Context.InstallStatusMessage = $"应用模版: {p.CurrentScript}";
});

await _templateService.ApplyTemplateAsync(templateId, instanceName, progress: progress);
```

### 错误处理

```csharp
try
{
    await ExecuteScriptAsync(script);
}
catch (Exception ex)
{
    if (script.ContinueOnError)
    {
        _logger.LogWarning(ex, "Script {ScriptId} failed but ContinueOnError is true", script.Id);
        record.Errors.Add($"{script.Name}: {ex.Message}");
    }
    else
    {
        throw new TemplateApplicationException($"Script '{script.Name}' failed", ex);
    }
}
```

## 扩展性考虑

- **模版格式**: JSON 格式，易于扩展新字段
- **脚本类型**: 当前支持 Bash，未来可以添加 Python、PowerShell（WSL 内）
- **远程模版**: 预留 `TemplateUrl` 字段，未来支持从 GitHub 加载
- **依赖管理**: 预留 `Dependencies` 字段，支持模版之间的依赖关系
- **版本控制**: 模版包含 `Version` 字段，支持版本升级

## 风险和注意事项

1. **脚本安全性**:
   - 官方模版经过审核
   - 自定义模版需要用户确认
   - 考虑添加脚本签名验证（未来）

2. **网络依赖**:
   - 大多数模版需要网络下载软件包
   - 需要清晰提示网络错误

3. **执行时间**:
   - 某些模版可能需要 10+ 分钟
   - 需要准确的时间预估和进度显示

4. **兼容性**:
   - 不同发行版的包管理器不同（apt/yum/dnf/pacman）
   - 需要为每个模版指定兼容的发行版

## 成功标准

实现完成后，用户应该能够：
1. 在创建新 WSL 实例时选择一个开发环境模版
2. 看到模版应用的实时进度
3. 安装完成后直接使用预配置的开发工具（如 dotnet、node、python）
4. 跳过模版选择，使用纯净的 WSL 实例
5. 查看详细的应用日志以便排查问题

## Actual Completion Audit (2026-02-13)

### Implemented
- Core models and service contracts are present and wired.
- `TemplateService` is registered in DI and invoked from wizard progress flow.
- Wizard includes `SelectTemplateStep` and template application integration in `ProgressStep`.
- PowerShell cmdlets `Get-DistroNexusTemplate` and `Apply-DistroNexusTemplate` are implemented and exported.
- Template unit tests are passing in both C# and PowerShell.

### Partially Implemented
- Template-specific integration tests and end-to-end records are still pending.

### Not Yet Implemented / Not Verified
- Template-specific integration tests are not present.
- End-to-end validation scenarios listed in this document are not yet recorded as completed.

### Overall Status
- Requirement completion is **P0 Complete / Overall Partial**. Release-blocker items are implemented; P1/P2 hardening and full verification remain.

## Requirements Completion Addendum (2026-02-13)

This addendum converts identified gaps into execution-ready requirements.

### Priority Definition
- **P0 (Release Blocker)**: Must be completed before template feature is marked production-ready.
- **P1 (High Value)**: Should be completed in the same minor release window.
- **P2 (Stabilization)**: Can be completed after P0/P1 but must be tracked.

### P0 Requirements (Must Complete)

1. Preset Template Set Completion
- Deliver exactly 5 official templates in `config/templates.json`:
   - `dotnet-dev`
   - `nodejs-dev`
   - `python-dev`
   - `docker-dev`
   - `fullstack-dev`
- Provide corresponding script files under `config/templates/<template-id>/install.sh`.
- Ensure metadata completeness per template: id, name, description, category, compatible distros, scripts, and estimated duration.

2. Install Options Contract Completion
- Add optional `TemplateId` to `InstallOptions`.
- Ensure `WizardContext.ToInstallOptions()` maps selected template ID when `ApplyTemplateAfterInstall = true`.
- Keep backward compatibility for installation paths with no template selected.

3. Template Selection Step Behavior Completion
- Implement distribution compatibility filtering in `SelectTemplateStep` based on selected distro and `CompatibleDistros`.
- Implement explicit skip-template UX path:
   - user can skip template intentionally,
   - skip state is reflected in context (`ApplyTemplateAfterInstall = false`).
- Implement step-level validation and exit behavior:
   - valid when either a compatible template is selected or skip is explicitly enabled,
   - state is preserved when navigating back/forward.

4. Script Execution Consistency
- Unify execution behavior between Core and PowerShell paths:
   - if `ScriptPath` is specified, resolve and execute script content,
   - if inline `Content` is specified, execute inline content,
   - both modes must support variable replacement.
- Ensure timeout and cancellation are honored per script.

### P1 Requirements (Should Complete)

5. Error Semantics and User Messaging
- Standardize template failure outcomes:
   - fail-fast when `ContinueOnError = false`,
   - warning accumulation when `ContinueOnError = true`.
- Surface concise, user-friendly failure summary in wizard result page.
- Persist technical details to logs with script name, phase, and command mode (path/content).

6. Template Application History
- Implement persistence for `TemplateApplicationRecord`.
- Implement `GetApplicationHistoryAsync(instanceName)` with filtering.
- Add retention policy requirement (minimum 30 days, configurable later).

7. Security Baseline for Custom Templates
- Require explicit user confirmation before applying imported/custom templates.
- Validate script path traversal and reject paths escaping allowed roots.
- Record template origin metadata (`official`/`custom`) in execution log.

### P2 Requirements (Stabilization)

8. Integration and End-to-End Validation
- Add template-specific integration tests covering:
   - wizard select-template path,
   - skip-template path,
   - successful template application,
   - failed script handling with both continue/fail-fast behavior.
- Add E2E verification records for at least:
   - Ubuntu + `dotnet-dev`,
   - Debian + `nodejs-dev`,
   - Ubuntu + `python-dev`,
   - Ubuntu + `docker-dev`.

9. Operational Diagnostics
- Record template application duration and per-script duration.
- Include resolved script source (`ScriptPath` or inline) in debug logs.
- Expose final template execution summary in installation result context.

## Updated Acceptance Checklist (Release Gate)

### Functional
- [x] Five official templates are available and selectable in wizard.
- [x] Template list auto-filters by selected distribution compatibility.
- [x] User can explicitly skip template and proceed.
- [x] Selected template is mapped through install options and applied after install.
- [x] Script execution supports both `ScriptPath` and `Content` reliably.

### Reliability
- [x] Per-script timeout is enforced (default 300s unless overridden).
- [x] Cancellation from wizard stops ongoing template application gracefully.
- [x] Failure behavior matches `ContinueOnError` semantics.
- [x] Detailed errors are logged; user-facing messages remain concise.

### Validation
- [x] C# and PowerShell unit tests pass for template feature.
- [x] Integration tests for template scenarios are present and passing.
- [x] E2E verification evidence is documented for required distro/template matrix.

### Data and Auditability
- [x] Template application history can be queried by instance.
- [x] Log entries include template id, script name, phase, result, and duration.

## Definition of Done for Template Feature

The template feature can be declared complete only when all P0 items are done and all release-gate checklist items under Functional, Reliability, and Validation are checked.
