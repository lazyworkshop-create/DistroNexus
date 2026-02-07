# 快速参考: WPF-PowerShell 集成测试

## 📋 项目结构

```
tests/PowerShell/Integration/              # PowerShell 集成测试
├── CacheIntegrationTests.ps1              (缓存 TTL、刷新、协调)
├── BatchDownloadIntegrationTests.ps1      (并发、重试、进度)
└── EndToEndWorkflowTests.ps1              (模块、工作流、WPF 集成)

src/Client/DistroNexus.Tests/Integration/  # C# 集成测试
├── ModuleLoadingIntegrationTests.cs       (模块路径、搜索)
├── ParameterMarshallingIntegrationTests.cs(C# → PS 参数)
├── JsonDeserializationIntegrationTests.cs (PS 输出 → C# 模型)
├── ErrorMappingIntegrationTests.cs        (PS 错误 → 异常)
├── TimeoutHandlingIntegrationTests.cs     (10s, 30s, 120s, 300s)
└── WslManagerServiceIntegrationTests.cs   (8 个服务方法)

.github/workflows/                          # CI/CD 工作流
├── test.yml                               (完整: PS + C# + 覆盖率)
└── quick-test.yml                         (快速: 单元测试)

scripts/                                    # 本地工具
├── run-tests.ps1                          (运行测试: Quick/Full/PowerShell/CSharp)
└── validate-coverage.ps1                  (验证覆盖率达标)
```

## 🚀 快速命令

### 本地测试

```powershell
# 快速测试（推荐日常开发）
.\scripts\run-tests.ps1 -TestType Quick

# 完整测试 + 覆盖率
.\scripts\run-tests.ps1 -TestType Full -Coverage

# PowerShell 测试
.\scripts\run-tests.ps1 -TestType PowerShell

# C# 测试
.\scripts\run-tests.ps1 -TestType CSharp

# 集成测试
.\scripts\run-tests.ps1 -TestType Integration

# 验证覆盖率
.\scripts\validate-coverage.ps1

# 详细输出
.\scripts\run-tests.ps1 -TestType Full -Verbose
```

### 手动运行

```powershell
# PowerShell 单元测试
Invoke-Pester tests/PowerShell/Unit/

# PowerShell 集成测试
Invoke-Pester tests/PowerShell/Integration/

# C# 测试
dotnet test src/Client/DistroNexus.Tests/
```

## 📊 测试覆盖范围

### PowerShell (29 个场景)
| 测试套件 | 场景数 | 关键覆盖 |
|---------|-------|---------|
| CacheIntegrationTests | 6 | TTL、刷新、协调 |
| BatchDownloadIntegrationTests | 10 | 并发、重试、进度 |
| EndToEndWorkflowTests | 13 | 模块依赖、WPF 集成 |

### C# (63 个测试方法)
| 测试类 | 方法数 | 关键覆盖 |
|-------|-------|---------|
| ModuleLoadingIntegrationTests | 6 | 模块路径、搜索优先级 |
| ParameterMarshallingIntegrationTests | 9 | 类型编组、转义、数组 |
| JsonDeserializationIntegrationTests | 13 | 对象解析、类型转换 |
| ErrorMappingIntegrationTests | 12 | 异常映射、上下文 |
| TimeoutHandlingIntegrationTests | 12 | 4 个超时等级、取消 |
| WslManagerServiceIntegrationTests | 11 | 8 个服务方法 |

## 🎯 覆盖率目标

| 组件 | 目标 | 优先级 |
|------|------|--------|
| PowerShell 私有函数 | 75%+ | 高 |
| PowerShell 公开 cmdlet | 80%+ | 高 |
| C# PowerShellService | 85%+ | 高 |
| C# 异常类 | 90%+ | 中 |
| C# 模型类 | 90%+ | 中 |
| C# 集成测试 | 70%+ | 低 |

## 🔗 集成点检查清单

### ✅ 模块加载
- 环境变量: `DISTRONEXUS_MODULE_PATH`
- 搜索顺序: Dev → Installed → AppData → Docs
- 错误处理和回退

### ✅ 参数编组
- 字符串、整数、布尔、数组
- 特殊字符转义（`'`, `"`, `\`, 空格）
- Null 和复杂对象

### ✅ JSON 反序列化
- PowerShell JSON 输出 → C# 模型
- 类型映射（int, long, bool, DateTime）
- 错误处理

### ✅ 异常映射
- 8 个异常类型
- 上下文保留（操作、实例、超时）
- 用户友好消息

### ✅ 超时处理
- 快速: 10 秒
- 常规: 30 秒
- 长期: 120 秒
- 非常长: 300 秒
- CancellationToken 支持

### ✅ 服务集成
- 8 个 WslManagerService 方法
- 模块 cmdlet 调用
- 参数验证和进度报告

## 📁 文件映射

| 文件 | 用途 | 运行命令 |
|------|------|---------|
| CacheIntegrationTests.ps1 | 缓存验证 | `Invoke-Pester` |
| BatchDownloadIntegrationTests.ps1 | 下载验证 | `Invoke-Pester` |
| EndToEndWorkflowTests.ps1 | 工作流验证 | `Invoke-Pester` |
| ModuleLoadingIntegrationTests.cs | 模块加载 | `dotnet test` |
| ParameterMarshallingIntegrationTests.cs | 参数转换 | `dotnet test` |
| JsonDeserializationIntegrationTests.cs | JSON 解析 | `dotnet test` |
| ErrorMappingIntegrationTests.cs | 异常映射 | `dotnet test` |
| TimeoutHandlingIntegrationTests.cs | 超时验证 | `dotnet test` |
| WslManagerServiceIntegrationTests.cs | 服务集成 | `dotnet test` |

## 🔧 异常类型

```csharp
namespace DistroNexus.Core.Exceptions {
    WslException                        // 基础
    WslOperationException               // 操作异常
    WslInstanceNotFoundException        // 实例未找到
    WslInstanceAlreadyExistsException   // 实例已存在
    WslOperationTimeoutException        // 超时
    WslExportFailedException            // 导出失败
    WslImportFailedException            // 导入失败
    WslOperationCanceledException       // 取消
}
```

## 🎓 文档

| 文档 | 内容 |
|------|------|
| INTEGRATION_TEST_IMPLEMENTATION_GUIDE.md | 完整实现指南（所有细节） |
| IMPLEMENTATION_COMPLETE_SUMMARY.md | 完成总结（快速概览） |
| 本文件 | 快速参考（常用命令） |

## ⏱️ 执行时间预期

- 快速测试: ~2-3 分钟（单元测试）
- 完整测试: ~10-15 分钟（所有测试 + 覆盖率）
- 仅 PowerShell: ~5-7 分钟
- 仅 C#: ~5-8 分钟

## 🐛 常见问题

**Q: 测试找不到模块？**
```powershell
$env:DISTRONEXUS_MODULE_PATH = 'src/PowerShell'
```

**Q: Pester 版本错误？**
```powershell
Install-Module Pester -Force -MinimumVersion 5.0
```

**Q: .NET 版本问题？**
```powershell
dotnet --version  # 应该是 8.0+
```

**Q: 快速测试失败怎么办？**
```powershell
# 运行完整测试获取更多信息
.\scripts\run-tests.ps1 -TestType Full -Verbose
```

## ✨ 关键命令速记

```powershell
# 开发循环
cd DistroNexus
.\scripts\run-tests.ps1 -TestType Quick           # 快速反馈
# ... 修改代码 ...
.\scripts\run-tests.ps1 -TestType Quick           # 再次检查

# 提交前
.\scripts\run-tests.ps1 -TestType Full -Coverage  # 完整检查
.\scripts\validate-coverage.ps1                   # 验证目标

# CI/CD 监控
# 在 GitHub Actions 中自动运行
# 查看工作流结果和覆盖率报告
```

---

**最后更新**: 2026年1月31日  
**下一步**: 运行 `.\scripts\run-tests.ps1 -TestType Quick` 开始！
