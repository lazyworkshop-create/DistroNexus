# WPF-PowerShell 集成测试实现指南

## 概览

本文档提供了 DistroNexus 项目中 WPF 客户端与 PowerShell 模块集成的完整单元测试和集成测试框架的实现指南。

**完成日期**: 2026年1月31日  
**覆盖范围**: PowerShell 模块集成、C# 客户端集成、CI/CD 自动化

## 实现清单

### 1. ✅ 异常体系（步骤 2）

**位置**: `src/Client/DistroNexus.Core/Exceptions/`

已创建的异常类:
- `WslException` - 基础异常类
- `WslOperationException` - 操作异常
- `WslInstanceNotFoundException` - 实例不存在异常
- `WslInstanceAlreadyExistsException` - 实例已存在异常  
- `WslOperationTimeoutException` - 超时异常
- `WslExportFailedException` - 导出异常
- `WslImportFailedException` - 导入异常
- `WslOperationCanceledException` - 取消异常

**特点**:
- 包含操作名称、实例名称、超时时长等上下文信息
- 支持内部异常链接
- 可序列化用于日志记录

### 2. ✅ PowerShell 集成测试（步骤 3）

**位置**: `tests/PowerShell/Integration/`

#### 2.1 缓存工作流集成测试 (`CacheIntegrationTests.ps1`)

验证项目:
- ✅ 缓存保存和加载
- ✅ 10分钟 TTL 强制执行
- ✅ 缓存刷新和失效机制
- ✅ 跨模块缓存协调
- ✅ 缓存 API 一致性

运行: `Invoke-Pester -Path tests/PowerShell/Integration/CacheIntegrationTests.ps1`

#### 2.2 批量下载协调集成测试 (`BatchDownloadIntegrationTests.ps1`)

验证项目:
- ✅ 并发下载管理（默认3个，最多10个）
- ✅ 下载重试机制（指数退避）
- ✅ 缓存利用（跳过已缓存包）
- ✅ 部分下载恢复
- ✅ 进度报告（WPF 兼容格式）
- ✅ 文件完整性验证
- ✅ 错误处理和恢复

运行: `Invoke-Pester -Path tests/PowerShell/Integration/BatchDownloadIntegrationTests.ps1`

#### 2.3 端到端工作流集成测试 (`EndToEndWorkflowTests.ps1`)

验证项目:
- ✅ 模块加载顺序和依赖
- ✅ 所有公开 cmdlet 导出（11个）
- ✅ 跨模块交互
- ✅ 完整安装工作流
- ✅ 操作后状态一致性
- ✅ WSL 状态变化检测
- ✅ WPF 客户端集成点
- ✅ 性能特性验证

运行: `Invoke-Pester -Path tests/PowerShell/Integration/EndToEndWorkflowTests.ps1`

### 3. ✅ C# 集成测试（步骤 4-5）

**位置**: `src/Client/DistroNexus.Tests/Integration/`

#### 3.1 模块加载集成测试 (`ModuleLoadingIntegrationTests.cs`)

验证项目:
- ✅ 从开发路径查找模块
- ✅ 模块未找到时的回退
- ✅ 环境变量路径检测
- ✅ 模块搜索路径优先级（5个位置）
- ✅ 模块未找到错误报告
- ✅ 单次模块加载

测试方法: xUnit + Moq  
运行: `dotnet test src/Client/DistroNexus.Tests/Integration/ModuleLoadingIntegrationTests.cs`

#### 3.2 参数编组集成测试 (`ParameterMarshallingIntegrationTests.cs`)

验证项目:
- ✅ 字符串参数编组
- ✅ 整数参数编组
- ✅ 布尔参数编组
- ✅ 数组参数编组
- ✅ 特殊字符转义（引号、空格等）
- ✅ Null 参数处理
- ✅ 复杂对象编组
- ✅ 参数类型验证
- ✅ 大数组参数

运行: `dotnet test src/Client/DistroNexus.Tests/Integration/ParameterMarshallingIntegrationTests.cs`

#### 3.3 JSON 反序列化集成测试 (`JsonDeserializationIntegrationTests.cs`)

验证项目:
- ✅ WslInstance 对象反序列化
- ✅ 复杂嵌套对象处理
- ✅ 空数组响应
- ✅ JSON 中的 Null 值
- ✅ 数值类型（int, long, double）
- ✅ 布尔值解析
- ✅ DateTime 值处理
- ✅ 字符串数组
- ✅ 对象数组
- ✅ 格式不正确的 JSON 处理
- ✅ 属性名大小写敏感性

运行: `dotnet test src/Client/DistroNexus.Tests/Integration/JsonDeserializationIntegrationTests.cs`

#### 3.4 错误映射集成测试 (`ErrorMappingIntegrationTests.cs`)

验证项目:
- ✅ 实例未找到错误映射
- ✅ 访问被拒绝错误映射
- ✅ 异常上下文保留
- ✅ 自定义异常属性
- ✅ 内部异常支持
- ✅ 用户友好的错误消息
- ✅ 错误上下文保留
- ✅ 错误序列化安全性
- ✅ 多层异常包装

运行: `dotnet test src/Client/DistroNexus.Tests/Integration/ErrorMappingIntegrationTests.cs`

#### 3.5 超时处理集成测试 (`TimeoutHandlingIntegrationTests.cs`)

验证项目:
- ✅ 快速操作超时（10秒）
- ✅ 常规操作超时（30秒）
- ✅ 长操作超时（120秒）
- ✅ 非常长操作超时（300秒）
- ✅ CancellationToken 支持
- ✅ 长期运行操作取消
- ✅ 超时选项配置
- ✅ 独立操作超时
- ✅ 并发操作超时隔离
- ✅ 取消令牌传播
- ✅ 进度跟踪不延长超时
- ✅ 长下载可终止性

运行: `dotnet test src/Client/DistroNexus.Tests/Integration/TimeoutHandlingIntegrationTests.cs`

#### 3.6 WslManagerService 集成测试 (`WslManagerServiceIntegrationTests.cs`)

验证项目:
- ✅ GetInstancesAsync 使用 PowerShell 模块
- ✅ StartInstanceAsync 模块调用
- ✅ StopInstanceAsync 模块调用
- ✅ RemoveInstanceAsync 模块调用
- ✅ MoveInstanceAsync 模块调用
- ✅ RenameInstanceAsync 模块调用
- ✅ SetCredentialsAsync 模块调用
- ✅ InstallInstanceAsync 进度报告
- ✅ 模块故障回退处理
- ✅ 参数验证
- ✅ 取消令牌尊重

运行: `dotnet test src/Client/DistroNexus.Tests/Integration/WslManagerServiceIntegrationTests.cs`

### 4. ✅ CI/CD 自动化（步骤 6）

**位置**: `.github/workflows/`

#### 4.1 完整集成测试工作流 (`test.yml`)

**触发条件**: 
- Push 到 main 或 develop 分支
- PR 到 main 或 develop 分支

**作业**:

1. **test-powershell** - PowerShell 模块测试
   - 运行矩阵: PowerShell 7.2 和 latest
   - 单元测试 + 集成测试
   - 代码覆盖率收集（CoverageGutters 格式）
   - 工件上传
   - 结果发布

2. **test-csharp** - C# 客户端测试
   - .NET 8.0 SDK
   - 依赖恢复 + 构建 + 测试
   - 单元测试 + 集成测试
   - 代码覆盖率收集（Cobertura 格式）
   - 上传到 Codecov
   - 工件上传

3. **generate-coverage-report** - 覆盖率报告生成
   - 收集所有覆盖率文件
   - 生成 HTML 报告（ReportGenerator）
   - PR 评论包含覆盖率摘要
   - 工件上传

4. **summary** - 测试总结
   - 检查所有测试状态
   - 生成工作流总结

#### 4.2 快速测试工作流 (`quick-test.yml`)

**触发条件**: PR 更改 src/ 或 tests/ 下的文件

**作业**: 
- 仅运行单元测试（不是集成测试）
- PowerShell 快速测试
- C# 快速测试
- 快速反馈循环

### 5. ✅ 本地测试脚本（步骤 6）

**位置**: `scripts/`

#### 5.1 测试运行脚本 (`run-tests.ps1`)

**用途**: 本地开发者运行测试

**选项**:
```powershell
# 快速测试（默认）
.\scripts\run-tests.ps1 -TestType Quick

# 完整测试套件
.\scripts\run-tests.ps1 -TestType Full -Coverage

# PowerShell 测试
.\scripts\run-tests.ps1 -TestType PowerShell -Coverage

# C# 测试
.\scripts\run-tests.ps1 -TestType CSharp -Coverage

# 集成测试
.\scripts\run-tests.ps1 -TestType Integration

# 详细输出
.\scripts\run-tests.ps1 -TestType Full -Verbose
```

**特点**:
- 先决条件检查（.NET SDK、Pester）
- 彩色输出
- 测试结果总结
- 覆盖率报告生成
- 执行时间跟踪

#### 5.2 覆盖率验证脚本 (`validate-coverage.ps1`)

**用途**: 验证覆盖率是否达到目标

**覆盖率目标**:
- PowerShell 私有函数: 75%+
- PowerShell 公开 cmdlet: 80%+
- C# PowerShellService: 85%+
- C# 模型: 90%+
- C# 集成测试: 70%+

**使用**:
```powershell
.\scripts\validate-coverage.ps1
```

## 测试覆盖范围详情

### PowerShell 模块覆盖范围

| 组件 | 单元测试 | 集成测试 | 覆盖率目标 |
|------|---------|---------|-----------|
| 缓存模块 | ✅ | ✅ | 75% |
| 日志模块 | ✅ | ✅ | 75% |
| 配置模块 | ✅ | ✅ | 75% |
| 包处理模块 | ✅ | ✅ | 80% |
| 终端启动器 | ✅ | ✅ | 80% |
| Get-DistroNexusInstance | ✅ | ✅ | 80% |
| Install-DistroNexusInstance | ✅ | ✅ | 80% |
| Start-DistroNexusInstance | ✅ | ✅ | 80% |
| Stop-DistroNexusInstance | ✅ | ✅ | 80% |
| Remove-DistroNexusInstance | ✅ | ✅ | 80% |
| Move-DistroNexusInstance | ✅ | ✅ | 80% |
| Rename-DistroNexusInstance | ✅ | ✅ | 80% |
| Set-DistroNexusCredential | ✅ | ✅ | 80% |
| Get-DistroNexusPackage | ✅ | ✅ | 80% |
| Save-DistroNexusPackage | ✅ | ✅ | 80% |
| Update-DistroNexusCatalog | ✅ | ✅ | 80% |

### C# 覆盖范围

| 组件 | 单元测试 | 集成测试 | 覆盖率目标 |
|------|---------|---------|-----------|
| PowerShellService | ✅ | ✅ | 85% |
| WslManagerService | ✅ | ✅ | 80% |
| CatalogService | ✅ | ✅ | 80% |
| DownloadService | ✅ | ✅ | 80% |
| SettingsService | ✅ | ✅ | 80% |
| 模型类 | ✅ | ✅ | 90% |
| 异常类 | ✅ | ✅ | 90% |
| 转换器 | ✅ | ✅ | 85% |

## 运行测试的方法

### 方法 1: 使用本地脚本（开发者）

```powershell
# 快速反馈
.\scripts\run-tests.ps1 -TestType Quick

# 提交前完整测试
.\scripts\run-tests.ps1 -TestType Full -Coverage -Verbose

# 验证覆盖率
.\scripts\validate-coverage.ps1
```

### 方法 2: 使用 GitHub Actions（自动化）

- 任何推送到 `main` 或 `develop` 都触发完整测试
- 任何 PR 都触发快速测试
- 工作流运行报告生成 HTML 覆盖率报告
- 覆盖率上传到 Codecov 进行历史跟踪

### 方法 3: 手动运行特定测试

PowerShell 单元测试:
```powershell
Invoke-Pester -Path tests/PowerShell/Unit/ -Configuration @{
    Output = @{ Verbosity = 'Detailed' }
    CodeCoverage = @{ Enabled = $true }
}
```

PowerShell 集成测试:
```powershell
Invoke-Pester -Path tests/PowerShell/Integration/
```

C# 测试:
```powershell
dotnet test src/Client/DistroNexus.Tests/
dotnet test src/Client/DistroNexus.Tests/Integration/ --filter "Category=Integration"
```

## 关键集成点测试

### 1. 模块路径检测 (ModuleLoadingIntegrationTests)
- ✅ 优先级: 环境变量 → 开发路径 → 已安装路径 → AppData → 用户文档
- ✅ 故障回退机制
- ✅ 错误报告

### 2. 参数编组 (ParameterMarshallingIntegrationTests)
- ✅ C# 类型 → JSON → PowerShell 参数
- ✅ 特殊字符转义
- ✅ 数组和复杂对象

### 3. JSON 反序列化 (JsonDeserializationIntegrationTests)
- ✅ PowerShell 输出 → JSON → C# 模型
- ✅ 类型转换
- ✅ 错误处理

### 4. 异常映射 (ErrorMappingIntegrationTests)
- ✅ PowerShell 错误 → 自定义异常
- ✅ 上下文保留
- ✅ 用户友好消息

### 5. 超时处理 (TimeoutHandlingIntegrationTests)
- ✅ 按操作类型的超时（10s, 30s, 120s, 300s）
- ✅ 取消令牌支持
- ✅ 进度报告

### 6. 服务集成 (WslManagerServiceIntegrationTests)
- ✅ 所有 8 个公开方法的模块调用
- ✅ 参数验证
- ✅ 进度报告
- ✅ 故障回退

## 覆盖率目标进度

**目标**:
- PowerShell 私有函数: 75%+
- PowerShell 公开 cmdlet: 80%+
- C# PowerShellService: 85%+
- C# 模型: 90%+

**验证命令**:
```powershell
.\scripts\validate-coverage.ps1
```

## 故障排除

### 问题: 模块未找到

**解决方案**:
1. 确保 `src/PowerShell/DistroNexus.psm1` 存在
2. 设置环境变量: `$env:DISTRONEXUS_MODULE_PATH = 'src/PowerShell'`
3. 检查测试的工作目录

### 问题: Pester 版本不兼容

**解决方案**:
```powershell
Install-Module -Name Pester -Repository PSGallery -Force -MinimumVersion 5.0
```

### 问题: .NET SDK 版本不匹配

**解决方案**:
```powershell
dotnet --version  # 应该是 8.0+
```

## 下一步 - 增强建议

1. **性能基准**: 添加 Save-DistroNexusPackage 并发下载性能测试
2. **真实 WSL 验证**: 可选的真实 WSL2 环境集成测试
3. **模糊测试**: 对参数编组添加属性基础测试
4. **突变测试**: 使用 Stryker.NET 验证测试质量
5. **性能监控**: 在 CI 中跟踪测试执行时间趋势

## 参考资源

- [Pester 文档](https://pester.dev)
- [xUnit.net](https://xunit.net)
- [Moq 文档](https://github.com/moq/moq4)
- [GitHub Actions](https://docs.github.com/en/actions)
- [Codecov](https://codecov.io)
