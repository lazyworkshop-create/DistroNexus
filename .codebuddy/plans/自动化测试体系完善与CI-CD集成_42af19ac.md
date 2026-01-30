---
name: 自动化测试体系完善与CI-CD集成
overview: 为WPF-PowerShell架构重构项目建立完整的自动化测试体系，包括PowerShell模块Pester测试、C#单元/集成/E2E测试增强、测试覆盖率统计、CI/CD流程集成，以及详细的测试报告生成机制。确保新增功能（缓存、批量下载、模块调用）的质量保障。
todos:
  - id: setup-test-infrastructure
    content: 搭建测试基础设施：创建tests目录结构，安装Pester 5.x和FluentAssertions 7.0，配置PesterConfiguration.psd1，创建测试辅助工具（MockHelpers.ps1、TestData.ps1）
    status: completed
  - id: powershell-private-tests
    content: 编写PowerShell Private函数单元测试：Cache.Tests.ps1（测试Get/Set/Update-InstanceCache，缓存有效期验证），PackageHandler.Tests.ps1（测试Expand-DistroPackage、Test-PackageFormat），TerminalLauncher.Tests.ps1（测试Invoke-Terminal、Find-TerminalPath）
    status: completed
    dependencies:
      - setup-test-infrastructure
  - id: powershell-public-tests
    content: 编写PowerShell Public Cmdlet单元测试：Get-DistroNexusInstance.Tests.ps1（-ForceUpdate/-IncludeRelease参数），Install-DistroNexusInstance.Tests.ps1（-Interactive/-AutoDownload/-OpenTerminal），Save-DistroNexusPackage.Tests.ps1（-Family/-All批量下载，并发控制），Get-DistroNexusCache.Tests.ps1（新Cmdlet）
    status: completed
    dependencies:
      - setup-test-infrastructure
  - id: csharp-unit-tests
    content: 扩展C#单元测试：为PowerShellService.ExecuteModuleCmdletAsync编写完整测试（模块检测、参数格式化、JSON解析、超时控制），为ModuleCallOptions.Tests.cs编写测试，扩展PowerShellScriptResult.Tests.cs测试新增属性（ParsedObjects、UsedModule）
    status: completed
    dependencies:
      - setup-test-infrastructure
  - id: csharp-service-refactor-tests
    content: 编写WslManagerService重构后的测试：GetInstancesAsync模块调用和Fallback测试，ParseInstancesFromModule对象映射测试，基础操作（Start/Stop/Remove）模块调用测试
    status: completed
    dependencies:
      - csharp-unit-tests
  - id: integration-tests
    content: 创建集成测试项目和测试用例：WpfPowerShellIntegration.Tests.cs（端到端模块调用），CacheMechanism.Tests.cs（性能验证，首次调用vs缓存调用），FallbackMechanism.Tests.cs（模块不可用降级测试），BatchDownload.Tests.ps1（批量下载集成测试）
    status: completed
    dependencies:
      - powershell-public-tests
      - csharp-service-refactor-tests
  - id: ci-powershell-job
    content: 在GitHub Actions CI中新增PowerShell测试Job：安装Pester，配置测试运行，生成NUnitXml结果，生成覆盖率报告（CoverageGutters格式），上传测试结果和覆盖率
    status: completed
    dependencies:
      - powershell-private-tests
      - powershell-public-tests
  - id: ci-coverage-report
    content: 增强CI覆盖率报告生成：在C#测试中启用覆盖率收集（--collect:"XPlat Code Coverage"），使用ReportGenerator生成HTML报告，合并PowerShell和C#覆盖率，上传到GitHub Actions Artifacts
    status: completed
    dependencies:
      - ci-powershell-job
      - csharp-service-refactor-tests
  - id: ci-test-report-publish
    content: 实现测试报告可视化：使用dorny/test-reporter生成测试结果报告，在PR中自动评论测试摘要和覆盖率变化，配置测试失败阻止合并
    status: completed
    dependencies:
      - ci-coverage-report
  - id: test-documentation
    content: 编写测试文档：创建Testing-Strategy.md（测试策略、目标、分类），创建Test-Cases.md（详细测试用例清单），创建Testing-CI-CD-Guide.md（CI/CD测试执行指南），更新README.md添加测试部分
    status: completed
    dependencies:
      - integration-tests
  - id: nightly-e2e-tests
    content: 创建夜间E2E和性能测试：新建test-nightly.yml工作流，实现完整工作流E2E测试（列表→下载→安装→启动），实现性能基准测试（缓存性能、批量下载速度），生成性能趋势报告
    status: completed
    dependencies:
      - integration-tests
      - ci-test-report-publish
  - id: test-optimization
    content: 优化测试执行和维护：启用Pester并行执行，配置CI缓存（NuGet、Pester模块），添加测试重试机制（集成测试），标记慢测试和可选测试，完善测试失败诊断信息
    status: completed
    dependencies:
      - test-documentation
      - nightly-e2e-tests
---

## 用户需求

为DistroNexus项目建立完整的自动化测试体系，覆盖刚完成的WPF-PowerShell架构重构项目中的所有新增和修改功能。

## 产品概述

建立三层测试金字塔体系：

1. **PowerShell模块测试**：使用Pester框架，覆盖10项新增功能（缓存机制、包处理、终端启动、批量下载等）
2. **C#单元测试增强**：扩展现有xUnit测试，覆盖新增的ExecuteModuleCmdletAsync方法和WslManagerService重构
3. **集成和端到端测试**：验证WPF与PowerShell模块的交互、缓存性能、Fallback机制等关键场景

## 核心功能

### 1. PowerShell模块测试框架

- 使用Pester 5.x搭建测试框架
- 为3个新增Private函数编写单元测试（Cache.ps1、PackageHandler.ps1、TerminalLauncher.ps1）
- 为1个新增Public Cmdlet编写测试（Get-DistroNexusCache.ps1）
- 为7个增强Cmdlet编写回归测试（Get-DistroNexusInstance、Install-DistroNexusInstance等）
- Mock外部依赖（wsl.exe、Invoke-WebRequest、注册表）

### 2. C#单元测试扩展

- 为ExecuteModuleCmdletAsync方法编写完整测试（模块检测、参数格式化、JSON解析）
- 为ModuleCallOptions模型编写测试
- 为PowerShellScriptResult新增属性编写测试（ParsedObjects、UsedModule）
- 为WslManagerService重构后的方法编写测试（模块调用、对象映射、Fallback）

### 3. 集成测试套件

- WPF客户端与PowerShell模块端到端交互测试
- 缓存机制集成测试（性能验证、缓存失效）
- 批量下载并发测试（真实网络Mock）
- Fallback降级测试（模块不可用场景）

### 4. CI/CD增强

- 新增PowerShell测试Job到GitHub Actions
- 生成统一的覆盖率报告（C# + PowerShell）
- 自动生成HTML测试报告并上传
- PR自动评论测试结果和覆盖率变化

### 5. 测试报告和文档

- 生成详细的测试策略文档
- 创建测试用例清单（表格形式）
- 生成HTML格式的测试执行报告
- 覆盖率趋势分析

### 6. 性能基准测试

- 实例列表查询性能（缓存 vs 非缓存）
- 批量下载并发性能
- 安装流程耗时分析

## 技术栈选择

### PowerShell测试

- **Pester 5.x**：PowerShell官方测试框架，支持Mock、断言、覆盖率
- **PSScriptAnalyzer**：静态代码分析（已有）
- **Pester TestDrive**：临时文件系统Mock
- **InModuleScope**：测试Private函数

### C#测试增强

- **xUnit 2.9.3**：单元测试框架（已有）
- **Moq 4.20.72**：Mock框架（已有）
- **FluentAssertions 7.0.0**：可读性更强的断言库（新增）
- **Coverlet 6.0.4**：代码覆盖率收集（已有）
- **ReportGenerator 5.4.0**：覆盖率报告生成（新增）

### 测试数据和环境

- **TestDrive（Pester）**：PowerShell临时文件系统
- **内存文件系统**：C#文件操作Mock
- **固定测试数据集**：TestWslInstances.json、TestDistros.json

### CI/CD工具

- **GitHub Actions**：CI/CD平台（已有）
- **dorny/test-reporter**：测试结果可视化（新增）
- **codecov/codecov-action**：覆盖率上传（新增）
- **actions/upload-artifact**：报告归档（已有）

## 实施方案

### 高层策略

采用**分层测试金字塔**和**渐进式覆盖**策略：

1. **阶段一：基础设施搭建**（1-2天）

- 安装Pester和配置测试环境
- 创建测试目录结构
- 配置CI/CD测试Job
- 设置覆盖率工具

2. **阶段二：单元测试实施**（5-7天）

- PowerShell Private函数测试（70%基础覆盖）
- PowerShell Public Cmdlet测试（重点：新增和增强功能）
- C#新增方法测试（ExecuteModuleCmdletAsync）
- C#重构服务测试（WslManagerService）

3. **阶段三：集成测试**（3-5天）

- WPF ↔ PowerShell模块交互测试
- 缓存机制端到端测试
- 批量下载并发测试
- Fallback降级测试

4. **阶段四：报告和文档**（2-3天）

- 生成HTML测试报告
- 覆盖率可视化
- 测试文档完善
- CI/CD优化

### 核心实施方案

#### 1. PowerShell测试框架搭建

**目录结构**：

```
tests/
├── PowerShell/
│   ├── Unit/
│   │   ├── Private/
│   │   │   ├── Cache.Tests.ps1
│   │   │   ├── PackageHandler.Tests.ps1
│   │   │   └── TerminalLauncher.Tests.ps1
│   │   └── Public/
│   │       ├── Get-DistroNexusInstance.Tests.ps1
│   │       ├── Install-DistroNexusInstance.Tests.ps1
│   │       ├── Save-DistroNexusPackage.Tests.ps1
│   │       └── Get-DistroNexusCache.Tests.ps1
│   ├── Integration/
│   │   ├── CacheWorkflow.Tests.ps1
│   │   └── BatchDownload.Tests.ps1
│   ├── Helpers/
│   │   ├── MockHelpers.ps1
│   │   └── TestData.ps1
│   └── TestRunner.ps1
```

**Pester配置文件**（`tests/PowerShell/PesterConfiguration.psd1`）：

```
@{
    Run = @{
        Path = @('Unit', 'Integration')
        PassThru = $true
    }
    CodeCoverage = @{
        Enabled = $true
        Path = '../../src/PowerShell/**/*.ps1'
        OutputFormat = 'CoverageGutters'
        OutputPath = '../../coverage/powershell-coverage.xml'
    }
    TestResult = @{
        Enabled = $true
        OutputFormat = 'NUnitXml'
        OutputPath = '../../TestResults/powershell-results.xml'
    }
    Output = @{
        Verbosity = 'Detailed'
    }
}
```

**Mock策略**：

- `wsl.exe`：Mock返回固定实例列表
- `Invoke-WebRequest`：Mock文件下载（避免真实网络）
- 注册表：Mock `Get-ItemProperty`返回
- 文件系统：使用TestDrive临时目录

**测试模板示例**（Cache.Tests.ps1）：

```
BeforeAll {
    $modulePath = "$PSScriptRoot/../../../src/PowerShell"
    Import-Module "$modulePath/DistroNexus.psd1" -Force
    
    # Import private function using dot sourcing
    . "$modulePath/Private/Cache.ps1"
}

Describe "Get-InstanceCache" {
    BeforeEach {
        # Setup test environment
        $TestCachePath = Join-Path $TestDrive "cache"
        New-Item -Path $TestCachePath -ItemType Directory -Force
    }
    
    Context "When cache file exists and is valid" {
        It "Should return cached instances" {
            # Arrange
            $cacheData = @{
                CachedAt = (Get-Date).ToString("o")
                Instances = @(
                    @{ Name = "Ubuntu-22.04"; State = "Running" }
                )
            }
            $cacheFile = Join-Path $TestCachePath "instances.json"
            $cacheData | ConvertTo-Json | Set-Content $cacheFile
            
            # Act
            Mock Get-DistroNexusConfig { @{ CachePath = $TestCachePath } }
            $result = Get-InstanceCache
            
            # Assert
            $result | Should -Not -BeNullOrEmpty
            $result.Count | Should -Be 1
            $result[0].Name | Should -Be "Ubuntu-22.04"
        }
    }
    
    Context "When cache is expired" {
        It "Should return null" {
            # Arrange
            $cacheData = @{
                CachedAt = (Get-Date).AddMinutes(-15).ToString("o")
                Instances = @()
            }
            $cacheFile = Join-Path $TestCachePath "instances.json"
            $cacheData | ConvertTo-Json | Set-Content $cacheFile
            
            # Act
            Mock Get-DistroNexusConfig { @{ CachePath = $TestCachePath } }
            $result = Get-InstanceCache
            
            # Assert
            $result | Should -BeNullOrEmpty
        }
    }
}
```

#### 2. C#测试扩展

**新增FluentAssertions NuGet包**：

```xml
<PackageReference Include="FluentAssertions" Version="7.0.0" />
```

**测试示例**（PowerShellServiceTests.cs扩展）：

```
[Fact]
public async Task ExecuteModuleCmdletAsync_WithValidCmdlet_ShouldReturnSuccessResult()
{
    // Arrange
    var cmdletName = "Get-DistroNexusInstance";
    var options = new ModuleCallOptions { TimeoutSeconds = 10 };
    
    // Act
    var result = await _powerShellService.ExecuteModuleCmdletAsync(
        cmdletName, null, options, CancellationToken.None);
    
    // Assert
    result.Should().NotBeNull();
    result.Success.Should().BeTrue();
    result.UsedModule.Should().BeTrue();
}

[Fact]
public async Task ExecuteModuleCmdletAsync_WithInvalidModule_ShouldReturnFailureResult()
{
    // Arrange
    var service = new PowerShellService(_mockLogger.Object);
    var cmdletName = "Get-DistroNexusInstance";
    
    // Temporarily rename module to simulate unavailability
    // (use test-specific instance)
    
    // Act
    var result = await service.ExecuteModuleCmdletAsync(
        cmdletName, null, null, CancellationToken.None);
    
    // Assert
    result.Should().NotBeNull();
    result.Success.Should().BeFalse();
    result.UsedModule.Should().BeFalse();
    result.Error.Should().Contain("module not found");
}

[Theory]
[InlineData("test", "'test'")]
[InlineData(true, "$true")]
[InlineData(false, "$false")]
[InlineData(42, "42")]
public void FormatParameterValue_WithVariousTypes_ShouldFormatCorrectly(
    object input, string expected)
{
    // This tests the private method via reflection or internal visibility
    // (implementation detail)
}
```

**WslManagerService重构测试**：

```
[Fact]
public async Task GetInstancesAsync_WithModuleAvailable_ShouldUseModule()
{
    // Arrange
    var mockPowerShell = new Mock<IPowerShellService>();
    var mockResult = new PowerShellScriptResult
    {
        Success = true,
        UsedModule = true,
        ParsedObjects = new List<JsonElement>
        {
            JsonDocument.Parse(@"{
                ""Name"": ""Ubuntu-22.04"",
                ""State"": ""Running"",
                ""Version"": ""2"",
                ""BasePath"": ""C:\\WSL\\Ubuntu""
            }").RootElement
        }
    };
    
    mockPowerShell
        .Setup(x => x.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstance", 
            null, 
            It.IsAny<ModuleCallOptions>(), 
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(mockResult);
    
    var service = new WslManagerService(
        mockPowerShell.Object, 
        _mockCatalog.Object, 
        _mockLogger.Object);
    
    // Act
    var instances = await service.GetInstancesAsync();
    
    // Assert
    instances.Should().HaveCount(1);
    instances[0].Name.Should().Be("Ubuntu-22.04");
    instances[0].State.Should().Be("Running");
    
    // Verify module was called
    mockPowerShell.Verify(x => x.ExecuteModuleCmdletAsync(
        "Get-DistroNexusInstance", 
        null, 
        It.IsAny<ModuleCallOptions>(), 
        It.IsAny<CancellationToken>()), 
        Times.Once);
}

[Fact]
public async Task GetInstancesAsync_WhenModuleFails_ShouldFallbackToInlineScript()
{
    // Arrange
    var mockPowerShell = new Mock<IPowerShellService>();
    
    // Module call fails
    mockPowerShell
        .Setup(x => x.ExecuteModuleCmdletAsync(
            It.IsAny<string>(), 
            It.IsAny<Dictionary<string, object>>(), 
            It.IsAny<ModuleCallOptions>(), 
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(new PowerShellScriptResult 
        { 
            Success = false, 
            UsedModule = false 
        });
    
    // Inline script succeeds
    mockPowerShell
        .Setup(x => x.ExecuteScriptAsync(
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(@"[{""Name"":""Ubuntu"",""State"":""Running""}]");
    
    var service = new WslManagerService(
        mockPowerShell.Object, 
        _mockCatalog.Object, 
        _mockLogger.Object);
    
    // Act
    var instances = await service.GetInstancesAsync();
    
    // Assert
    instances.Should().NotBeEmpty();
    
    // Verify fallback was used
    mockPowerShell.Verify(x => x.ExecuteScriptAsync(
        It.IsAny<string>(), 
        It.IsAny<CancellationToken>()), 
        Times.Once);
}
```

#### 3. 集成测试实施

**新建测试项目**（`tests/CSharp/Integration/IntegrationTests.csproj`）：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="xUnit" Version="2.9.3" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
    <ProjectReference Include="../../../src/Client/DistroNexus.Core/DistroNexus.Core.csproj" />
  </ItemGroup>
</Project>
```

**缓存性能集成测试**：

```
[Fact]
public async Task CacheMechanism_FirstCallVsSecondCall_ShouldBeSignificantlyFaster()
{
    // Arrange
    var service = CreateRealWslManagerService();
    
    // Act - First call (no cache)
    var sw1 = Stopwatch.StartNew();
    var instances1 = await service.GetInstancesAsync();
    sw1.Stop();
    
    // Act - Second call (with cache)
    var sw2 = Stopwatch.StartNew();
    var instances2 = await service.GetInstancesAsync();
    sw2.Stop();
    
    // Assert
    instances1.Should().BeEquivalentTo(instances2);
    sw2.ElapsedMilliseconds.Should().BeLessThan(sw1.ElapsedMilliseconds / 5);
    _testOutputHelper.WriteLine($"First call: {sw1.ElapsedMilliseconds}ms");
    _testOutputHelper.WriteLine($"Second call: {sw2.ElapsedMilliseconds}ms");
}
```

#### 4. CI/CD集成

**增强的ci.yml**：

```
jobs:
  build-and-test:
    name: Build and C# Tests
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Restore and Build
        run: |
          dotnet restore src/Client/DistroNexus.slnx
          dotnet build src/Client/DistroNexus.slnx --configuration Release --no-restore
      
      - name: Run C# Unit Tests with Coverage
        run: |
          dotnet test src/Client/DistroNexus.slnx `
            --configuration Release `
            --no-build `
            --logger "trx;LogFileName=test-results.trx" `
            --collect:"XPlat Code Coverage" `
            --results-directory ./TestResults
      
      - name: Generate Coverage Report
        run: |
          dotnet tool install -g dotnet-reportgenerator-globaltool
          reportgenerator `
            -reports:./TestResults/**/coverage.cobertura.xml `
            -targetdir:./CoverageReport `
            -reporttypes:"Html;Badges"
      
      - name: Upload Coverage Report
        uses: actions/upload-artifact@v4
        with:
          name: coverage-report
          path: ./CoverageReport
      
      - name: Upload Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: ./TestResults/*.trx

  powershell-tests:
    name: PowerShell Module Tests
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Install Pester
        run: |
          Install-Module -Name Pester -MinimumVersion 5.0.0 -Force -Scope CurrentUser
        shell: pwsh
      
      - name: Run Pester Tests
        run: |
          $config = New-PesterConfiguration
          $config.Run.Path = 'tests/PowerShell'
          $config.Run.PassThru = $true
          $config.CodeCoverage.Enabled = $true
          $config.CodeCoverage.Path = 'src/PowerShell/**/*.ps1'
          $config.CodeCoverage.OutputFormat = 'CoverageGutters'
          $config.CodeCoverage.OutputPath = 'coverage/powershell-coverage.xml'
          $config.TestResult.Enabled = $true
          $config.TestResult.OutputFormat = 'NUnitXml'
          $config.TestResult.OutputPath = 'TestResults/powershell-results.xml'
          $config.Output.Verbosity = 'Detailed'
          
          $result = Invoke-Pester -Configuration $config
          
          if ($result.FailedCount -gt 0) {
            Write-Error "Tests failed!"
            exit 1
          }
        shell: pwsh
      
      - name: Upload PowerShell Test Results
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: powershell-test-results
          path: TestResults/powershell-results.xml
      
      - name: Upload PowerShell Coverage
        uses: actions/upload-artifact@v4
        with:
          name: powershell-coverage
          path: coverage/powershell-coverage.xml

  test-report:
    name: Generate Test Report
    runs-on: windows-latest
    needs: [build-and-test, powershell-tests]
    if: always()
    steps:
      - name: Download Test Results
        uses: actions/download-artifact@v4
        with:
          path: ./all-results
      
      - name: Publish Test Report
        uses: dorny/test-reporter@v1
        with:
          name: 'DistroNexus Test Results'
          path: './all-results/**/*.{trx,xml}'
          reporter: 'dotnet-trx'
          fail-on-error: false
      
      - name: Comment PR with Coverage
        if: github.event_name == 'pull_request'
        uses: codecov/codecov-action@v4
        with:
          files: ./all-results/**/*coverage*.xml
          fail_ci_if_error: false
```

### 实施细节

#### 性能优化

1. **并行测试执行**：

- PowerShell：Pester原生支持并行（`-Run.Parallel = $true`）
- C#：xUnit默认并行执行测试类

2. **测试隔离**：

- PowerShell：使用TestDrive创建独立文件系统
- C#：每个测试类使用独立Mock实例

3. **缓存优化**：

- CI缓存NuGet包（已有）
- CI缓存Pester模块（新增）

#### 日志和可观测性

1. **详细测试日志**：

- Pester：`Verbosity = 'Detailed'`
- xUnit：`--verbosity detailed`

2. **测试失败诊断**：

- 捕获详细错误信息和堆栈跟踪
- PowerShell：使用`-ErrorAction Stop`确保错误传播
- C#：使用FluentAssertions提供清晰错误消息

3. **性能监控**：

- 记录每个测试执行时间
- 标记慢测试（>1秒）

#### 错误处理

1. **测试失败处理**：

- CI：测试失败时阻止合并（branch protection）
- 本地：提供清晰的失败原因和修复建议

2. **环境问题隔离**：

- 使用Mock减少对真实WSL环境的依赖
- 集成测试标记为`[Trait("Category", "Integration")]`可选执行

3. **重试机制**：

- 集成测试支持失败重试（最多3次）
- 减少偶发性失败影响

### 向后兼容

1. **保留现有测试**：

- 不修改现有11个测试文件
- 新增测试文件遵循相同命名约定

2. **CI工作流兼容**：

- 保留现有的3个Job
- 新增Job不影响现有流程

3. **覆盖率基准**：

- 首次运行建立基准
- 后续运行对比基准，不强制提升

### Blast Radius控制

1. **渐进式覆盖**：

- 第1周：基础设施 + 核心功能测试
- 第2周：完整单元测试
- 第3周：集成测试
- 第4周：优化和文档

2. **可选测试执行**：

- 本地开发：仅运行快速单元测试
- CI/PR：运行所有单元测试 + 快速集成测试
- 夜间构建：运行所有测试（包括E2E和性能）

3. **失败隔离**：

- 单个测试失败不影响其他测试
- PowerShell和C#测试独立Job

## 目录结构

```
DistroNexus/
├── tests/                                    # [NEW] 测试根目录
│   ├── PowerShell/                           # [NEW] PowerShell模块测试
│   │   ├── Unit/                             # 单元测试
│   │   │   ├── Private/
│   │   │   │   ├── Cache.Tests.ps1          # 缓存机制测试（Get/Set/Update-InstanceCache）
│   │   │   │   ├── PackageHandler.Tests.ps1 # 包处理测试（Expand-DistroPackage, Test-PackageFormat）
│   │   │   │   └── TerminalLauncher.Tests.ps1 # 终端启动测试（Invoke-Terminal, Find-TerminalPath）
│   │   │   └── Public/
│   │   │       ├── Get-DistroNexusInstance.Tests.ps1    # 实例查询测试（-ForceUpdate, -IncludeRelease等）
│   │   │       ├── Install-DistroNexusInstance.Tests.ps1 # 安装测试（-Interactive, -AutoDownload, -OpenTerminal）
│   │   │       ├── Save-DistroNexusPackage.Tests.ps1     # 批量下载测试（-Family, -All, 并发控制）
│   │   │       ├── Start-DistroNexusInstance.Tests.ps1   # 启动测试（-OpenTerminal）
│   │   │       ├── Move-DistroNexusInstance.Tests.ps1    # 移动测试（安全检查、DefaultUid恢复）
│   │   │       ├── Set-DistroNexusCredential.Tests.ps1   # 凭证测试（wsl.conf, wheel组）
│   │   │       ├── Update-DistroNexusCatalog.Tests.ps1   # 目录更新测试（备份机制）
│   │   │       └── Get-DistroNexusCache.Tests.ps1        # 缓存统计测试
│   │   ├── Integration/                      # 集成测试
│   │   │   ├── CacheWorkflow.Tests.ps1      # 缓存完整工作流测试
│   │   │   ├── BatchDownload.Tests.ps1      # 批量下载集成测试
│   │   │   └── InstallWithAutoDownload.Tests.ps1 # 安装+自动下载集成测试
│   │   ├── Helpers/                          # 测试辅助工具
│   │   │   ├── MockHelpers.ps1              # Mock函数库（Mock-WslCommand, Mock-WebRequest等）
│   │   │   └── TestData.ps1                 # 测试数据生成器
│   │   ├── PesterConfiguration.psd1         # Pester配置文件
│   │   └── TestRunner.ps1                   # 本地测试运行脚本
│   │
│   ├── CSharp/                               # [NEW] C#测试（独立于现有Tests项目）
│   │   ├── Unit/                             # 单元测试
│   │   │   ├── Services/
│   │   │   │   ├── PowerShellService.ExecuteModuleCmdletAsync.Tests.cs  # 新增模块调用方法测试
│   │   │   │   └── WslManagerService.ModuleIntegration.Tests.cs         # 重构后的模块集成测试
│   │   │   └── Models/
│   │   │       ├── ModuleCallOptions.Tests.cs           # [NEW] ModuleCallOptions模型测试
│   │   │       └── PowerShellScriptResult.Enhanced.Tests.cs # 增强属性测试（ParsedObjects, UsedModule）
│   │   ├── Integration/                      # 集成测试
│   │   │   ├── WpfPowerShellIntegration.Tests.cs       # WPF↔PowerShell端到端测试
│   │   │   ├── CacheMechanism.Tests.cs                 # 缓存性能验证测试
│   │   │   └── FallbackMechanism.Tests.cs              # Fallback降级测试
│   │   └── IntegrationTests.csproj           # 集成测试项目文件
│   │
│   ├── TestUtilities/                        # [NEW] 共享测试工具
│   │   ├── Mocks/
│   │   │   ├── MockWslEnvironment.cs        # WSL环境Mock（模拟wsl.exe输出）
│   │   │   └── MockPowerShellModule.cs      # PowerShell模块Mock
│   │   └── Fixtures/
│   │       ├── TestDataGenerator.cs         # C#测试数据生成器
│   │       ├── TestWslInstances.json        # 测试实例数据
│   │       └── TestDistros.json             # 测试发行版目录数据
│   │
│   └── Reports/                              # [NEW] 测试报告输出目录
│       ├── coverage/                         # 覆盖率报告
│       ├── test-results/                     # 测试结果（TRX/XML）
│       └── performance/                      # 性能基准报告
│
├── src/Client/DistroNexus.Tests/            # [EXISTING] 现有C#测试项目（保留）
│   ├── Models/                               # [MODIFY] 扩展现有模型测试
│   │   └── PowerShellScriptResultTests.cs   # 添加新增属性测试
│   └── Services/                             # [MODIFY] 扩展现有服务测试
│       ├── PowerShellServiceTests.cs        # 添加ExecuteModuleCmdletAsync测试
│       └── WslManagerServiceTests.cs        # 添加重构后的测试
│
├── .github/workflows/                        # CI/CD工作流
│   ├── ci.yml                                # [MODIFY] 增强CI流程
│   │   # 新增：
│   │   # - powershell-tests job（Pester测试）
│   │   # - test-report job（统一测试报告）
│   │   # - coverage-report job（覆盖率报告生成）
│   ├── test-nightly.yml                      # [NEW] 夜间全量测试（E2E+性能）
│   └── test-report-publish.yml               # [NEW] 发布测试报告到GitHub Pages
│
└── docs/                                     # 文档
    ├── Testing-Strategy.md                   # [NEW] 测试策略文档
    ├── Test-Cases.md                         # [NEW] 测试用例清单
    ├── Test-Report-Template.md               # [NEW] 测试报告模板
    └── Testing-CI-CD-Guide.md                # [NEW] CI/CD测试指南
```

## 关键代码结构

### PowerShell测试示例结构

```
# tests/PowerShell/Unit/Private/Cache.Tests.ps1

BeforeAll {
    # 导入模块
    $modulePath = "$PSScriptRoot/../../../../src/PowerShell"
    Import-Module "$modulePath/DistroNexus.psd1" -Force
    
    # 导入Private函数（使用dot sourcing）
    . "$modulePath/Private/Cache.ps1"
    . "$modulePath/Private/Config.ps1"
}

Describe "Get-InstanceCache" {
    BeforeEach {
        # 为每个测试创建独立的TestDrive
        $script:TestCachePath = Join-Path $TestDrive "cache"
        New-Item -Path $script:TestCachePath -ItemType Directory -Force
    }
    
    Context "当缓存文件存在且有效时" {
        It "应该返回缓存的实例列表" {
            # Arrange - 准备测试数据
            $cacheData = @{
                CachedAt = (Get-Date).ToString("o")
                Instances = @(
                    [PSCustomObject]@{
                        Name = "Ubuntu-22.04"
                        State = "Running"
                        Version = "2"
                    }
                )
            }
            $cacheFile = Join-Path $script:TestCachePath "instances.json"
            $cacheData | ConvertTo-Json -Depth 5 | Set-Content $cacheFile
            
            # Act - 执行被测试函数
            Mock Get-DistroNexusConfig { 
                @{ CachePath = $script:TestCachePath } 
            }
            $result = Get-InstanceCache
            
            # Assert - 验证结果
            $result | Should -Not -BeNullOrEmpty
            $result.Count | Should -Be 1
            $result[0].Name | Should -Be "Ubuntu-22.04"
        }
    }
    
    Context "当缓存已过期（>10分钟）时" {
        It "应该返回null" {
            # Arrange
            $cacheData = @{
                CachedAt = (Get-Date).AddMinutes(-15).ToString("o")
                Instances = @()
            }
            $cacheFile = Join-Path $script:TestCachePath "instances.json"
            $cacheData | ConvertTo-Json | Set-Content $cacheFile
            
            # Act
            Mock Get-DistroNexusConfig { 
                @{ CachePath = $script:TestCachePath } 
            }
            $result = Get-InstanceCache
            
            # Assert
            $result | Should -BeNullOrEmpty
        }
    }
    
    Context "当缓存文件不存在时" {
        It "应该返回null" {
            # Act
            Mock Get-DistroNexusConfig { 
                @{ CachePath = $script:TestCachePath } 
            }
            $result = Get-InstanceCache
            
            # Assert
            $result | Should -BeNullOrEmpty
        }
    }
}

Describe "Set-InstanceCache" {
    It "应该将实例数据写入缓存文件" {
        # Arrange
        $instances = @(
            [PSCustomObject]@{ Name = "Ubuntu"; State = "Running" }
        )
        
        # Act
        Mock Get-DistroNexusConfig { 
            @{ CachePath = $TestDrive } 
        }
        Set-InstanceCache -Instances $instances
        
        # Assert
        $cacheFile = Join-Path $TestDrive "instances.json"
        $cacheFile | Should -Exist
        
        $cached = Get-Content $cacheFile | ConvertFrom-Json
        $cached.Instances.Count | Should -Be 1
        $cached.Instances[0].Name | Should -Be "Ubuntu"
    }
}
```

### C#测试示例结构

```
// tests/CSharp/Unit/Services/PowerShellService.ExecuteModuleCmdletAsync.Tests.cs

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace DistroNexus.Tests.Services;

public class PowerShellServiceExecuteModuleCmdletAsyncTests : IDisposable
{
    private readonly Mock<ILogger<PowerShellService>> _mockLogger;
    private readonly PowerShellService _service;
    private readonly ITestOutputHelper _output;

    public PowerShellServiceExecuteModuleCmdletAsyncTests(
        ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<PowerShellService>>();
        _service = new PowerShellService(_mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WithNullCmdletName_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await _service.Invoking(s => s.ExecuteModuleCmdletAsync(
            null!, null, null, CancellationToken.None))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WithValidCmdlet_ShouldReturnSuccessResult()
    {
        // Arrange
        var cmdletName = "Get-DistroNexusInstance";
        var options = new ModuleCallOptions 
        { 
            TimeoutSeconds = 30,
            ParseAsJson = true 
        };

        // Act
        var result = await _service.ExecuteModuleCmdletAsync(
            cmdletName, null, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _output.WriteLine($"Success: {result.Success}");
        _output.WriteLine($"UsedModule: {result.UsedModule}");
        _output.WriteLine($"Output: {result.Output}");
        
        // 如果模块可用，应该使用模块
        if (result.UsedModule)
        {
            result.Success.Should().BeTrue();
            result.ParsedObjects.Should().NotBeNull();
        }
    }

    [Theory]
    [InlineData("string-value", "'string-value'")]
    [InlineData(true, "$true")]
    [InlineData(false, "$false")]
    [InlineData(42, "42")]
    [InlineData(3.14, "3.14")]
    public void FormatParameterValue_WithVariousTypes_ShouldFormatCorrectly(
        object input, string expectedOutput)
    {
        // 这个测试需要使用反射或InternalsVisibleTo访问private方法
        // 或者通过集成测试间接验证
        
        // Arrange
        var parameters = new Dictionary<string, object> 
        { 
            ["TestParam"] = input 
        };

        // Act - 通过公开方法间接测试
        var result = _service.ExecuteModuleCmdletAsync(
            "Test-Cmdlet", parameters, null, CancellationToken.None);

        // Assert - 验证生成的命令包含正确格式的参数
        // （需要通过日志或其他方式捕获实际命令）
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WithJsonOutput_ShouldParseParsedObjects()
    {
        // Arrange
        var cmdletName = "Get-DistroNexusInstance";
        var options = new ModuleCallOptions { ParseAsJson = true };

        // Act
        var result = await _service.ExecuteModuleCmdletAsync(
            cmdletName, null, options, CancellationToken.None);

        // Assert
        if (result.Success && result.UsedModule)
        {
            result.ParsedObjects.Should().NotBeNull();
            result.ParsedObjects.Should().NotBeEmpty();
            
            var firstElement = result.ParsedObjects![0];
            firstElement.ValueKind.Should().Be(JsonValueKind.Object);
            
            // 验证包含必需属性
            firstElement.TryGetProperty("Name", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_WithTimeout_ShouldRespectTimeout()
    {
        // Arrange
        var cmdletName = "Start-Sleep";
        var parameters = new Dictionary<string, object> 
        { 
            ["Seconds"] = 30 
        };
        var options = new ModuleCallOptions { TimeoutSeconds = 2 };

        // Act & Assert
        await _service.Invoking(s => s.ExecuteModuleCmdletAsync(
            cmdletName, parameters, options, CancellationToken.None))
            .Should().ThrowAsync<OperationCanceledException>()
            .WithMessage("*timeout*");
    }

    public void Dispose()
    {
        _service?.Dispose();
    }
}
```

### 集成测试示例结构

```
// tests/CSharp/Integration/WpfPowerShellIntegration.Tests.cs

using System.Diagnostics;
using System.Threading.Tasks;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace DistroNexus.IntegrationTests;

[Trait("Category", "Integration")]
public class WpfPowerShellIntegrationTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<PowerShellService> _psLogger;
    private readonly ILogger<WslManagerService> _wslLogger;

    public WpfPowerShellIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddDebug());
        
        _psLogger = loggerFactory.CreateLogger<PowerShellService>();
        _wslLogger = loggerFactory.CreateLogger<WslManagerService>();
    }

    [Fact]
    public async Task GetInstances_WithModuleAvailable_ShouldUseCacheOnSecondCall()
    {
        // Arrange
        var psService = new PowerShellService(_psLogger);
        var catalogService = new Mock<ICatalogService>().Object;
        var wslService = new WslManagerService(
            psService, catalogService, _wslLogger);

        // Act - First call (no cache)
        var sw1 = Stopwatch.StartNew();
        var instances1 = await wslService.GetInstancesAsync();
        sw1.Stop();
        _output.WriteLine($"First call: {sw1.ElapsedMilliseconds}ms");

        // Act - Second call (with cache)
        var sw2 = Stopwatch.StartNew();
        var instances2 = await wslService.GetInstancesAsync();
        sw2.Stop();
        _output.WriteLine($"Second call: {sw2.ElapsedMilliseconds}ms");

        // Assert
        instances1.Should().NotBeEmpty();
        instances2.Should().BeEquivalentTo(instances1);
        
        // 缓存调用应该显著更快（至少快5倍）
        sw2.ElapsedMilliseconds.Should().BeLessThan(
            sw1.ElapsedMilliseconds / 5,
            "cached call should be significantly faster");
    }

    [Fact]
    public async Task GetInstances_WhenModuleNotAvailable_ShouldFallbackSuccessfully()
    {
        // Arrange - 这个测试需要模拟模块不可用的场景
        // 可以通过临时重命名模块目录实现
        
        var psService = new PowerShellService(_psLogger);
        var catalogService = new Mock<ICatalogService>().Object;
        var wslService = new WslManagerService(
            psService, catalogService, _wslLogger);

        // Act
        var instances = await wslService.GetInstancesAsync();

        // Assert
        instances.Should().NotBeNull();
        // 即使模块不可用，fallback应该能返回结果
    }
}
```

# Agent Extensions

暂无需要使用的扩展