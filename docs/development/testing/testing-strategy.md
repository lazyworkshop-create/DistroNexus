# DistroNexus Testing Strategy

## Overview

This document outlines the comprehensive testing strategy for the DistroNexus project, covering the WPF-PowerShell architecture and all new features introduced in version 2.0.

## Testing Objectives

1. **Ensure Quality**: Validate that all features work as expected
2. **Prevent Regressions**: Catch breaking changes early in development
3. **Improve Confidence**: Enable fearless refactoring and feature additions
4. **Document Behavior**: Tests serve as living documentation
5. **Performance Validation**: Verify performance improvements (caching, batch operations)

## Test Pyramid

We follow a balanced test pyramid approach:

```
           /\
          /E2E\         Integration & E2E Tests (10%)
         /------\       - Full workflow scenarios
        /        \      - Performance benchmarks
       /----------\  
      / Integration\    Integration Tests (20%)
     /--------------\   - Module interactions
    /                \  - WPF ↔ PowerShell communication
   /------------------\
  /   Unit Tests       \ Unit Tests (70%)
 /______________________\ - Individual functions/methods
                          - Fast & isolated
```

### Test Distribution
- **Unit Tests**: 70% - Fast, isolated, focused on individual components
- **Integration Tests**: 20% - Test interactions between components
- **E2E Tests**: 10% - Full user scenarios and performance validation

## Testing Frameworks and Tools

### PowerShell Testing
- **Pester 5.7.1+**: Unit and integration testing framework
- **PSScriptAnalyzer**: Static code analysis
- **TestDrive**: Isolated file system for tests
- **InModuleScope**: Test private functions
- **Mock**: Isolate external dependencies

### C# Testing
- **xUnit 2.9.3**: Unit testing framework
- **Moq 4.20.72**: Mocking framework
- **FluentAssertions 7.0.0**: Readable assertions
- **Coverlet 6.0.4**: Code coverage collection
- **ReportGenerator 5.4.0**: Coverage report generation

### CI/CD
- **GitHub Actions**: Automated test execution
- **Test Artifacts**: Store test results and coverage reports

## Test Categories

### 1. PowerShell Module Tests

#### Unit Tests - Private Functions
- **Cache.ps1**
  - `Get-InstanceCache`: Cache retrieval, expiration validation
  - `Set-InstanceCache`: Cache creation, directory handling
  - `Update-InstanceCache`: Force refresh, file removal
  - `Clear-InstanceCache`: Clean cache removal

- **PackageHandler.ps1**
  - `Test-PackageFormat`: Format validation (.tar, .tar.gz, .appx, .zip)
  - `Get-PackageFormat`: Format detection
  - `Expand-DistroPackage`: Package extraction (mock-based)
  - `Test-TarCommand`: Tar availability check

- **TerminalLauncher.ps1**
  - `Find-TerminalPath`: Terminal detection (Windows Terminal, CMD)
  - `Invoke-Terminal`: Terminal launch with parameters
  - `Test-TerminalAvailable`: Availability checks
  - `Get-AvailableTerminals`: List all terminals

#### Unit Tests - Public Cmdlets
- **Get-DistroNexusInstance**
  - Cache usage vs ForceUpdate
  - Name filtering (wildcards)
  - IncludeRelease/IncludeUser switches
  
- **Save-DistroNexusPackage**
  - Family-based filtering
  - Batch download orchestration

#### Integration Tests
- **CacheWorkflow.Tests.ps1**: End-to-end cache behavior
- **BatchDownload.Tests.ps1**: Concurrent download management

### 2. C# Tests

#### Unit Tests - Services
- **PowerShellService**
  - `ExecuteModuleCmdletAsync`: Module detection, parameter formatting, JSON parsing, timeout handling
  - Parameter value formatting (strings, booleans, numbers)
  - Module availability detection
  - Cancellation token handling

- **WslManagerService** (planned)
  - Module-first execution
  - Fallback to inline scripts
  - Object mapping from JSON

#### Unit Tests - Models
- **ModuleCallOptions**
  - Default values
  - Property setters
  - Object initialization

- **PowerShellScriptResult (Enhanced)**
  - `ParsedObjects` property
  - `UsedModule` property
  - Complex JSON object handling

#### Integration Tests
- **WpfPowerShellIntegration.Tests.cs**: End-to-end module calls from WPF
- **CacheMechanism.Tests.cs**: Performance validation (cache vs non-cache)
- **FallbackMechanism.Tests.cs**: Module unavailable scenarios

### 3. E2E Tests (Planned)
- Complete user workflows
- Installation → Configuration → Launch
- Performance benchmarks

## Coverage Goals

| Component | Current | Target | Status |
|-----------|---------|--------|--------|
| PowerShell Private Functions | 0% | 75%+ | ✅ Tests Created |
| PowerShell Public Cmdlets | 0% | 80%+ | ✅ Tests Created |
| C# PowerShellService | 60% | 85%+ | ✅ Tests Created |
| C# WslManagerService | 60% | 85%+ | 🔄 In Progress |
| C# Models | 50% | 90%+ | ✅ Tests Created |
| Integration Tests | N/A | Key Paths 100% | 📋 Planned |

**Legend**: ✅ Complete | 🔄 In Progress | 📋 Planned

## Test Execution Strategy

### Local Development
```powershell
# Run PowerShell tests
cd tests/PowerShell
.\TestRunner.ps1 -TestType All -CodeCoverage

# Run C# tests
dotnet test src/Client/DistroNexus.slnx --collect:"XPlat Code Coverage"
```

### Continuous Integration
- **On Push**: All unit tests (PowerShell + C#)
- **On Pull Request**: Unit + fast integration tests
- **Nightly**: Full test suite + E2E + performance benchmarks

### Test Isolation
- **PowerShell**: TestDrive for file system isolation, Mocks for external commands
- **C#**: Moq for dependency injection, in-memory data structures
- **Integration**: Separate test projects, marked with `[Trait("Category", "Integration")]`

## Mocking Strategy

### PowerShell Mocks
- **WSL Commands**: Mock `wsl.exe` output using `Invoke-Expression` mock
- **Web Requests**: Mock `Invoke-WebRequest` for download tests
- **Registry Access**: Mock `Get-ItemProperty` for distro detection
- **File System**: Use Pester's TestDrive for temporary files

### C# Mocks
- **IPowerShellService**: Mock module execution results
- **ICatalogService**: Mock distro catalog data
- **File System**: In-memory file systems (planned)
- **Process Execution**: Mock Start-Process (planned)

## Performance Testing

### Key Metrics
1. **Cache Performance**
   - First call (cold): Baseline
   - Second call (cache): Should be 5x+ faster
   - Cache expiration: 10 minutes

2. **Batch Download**
   - Concurrent job management: Max 5 parallel downloads
   - Retry mechanism: 3 attempts with exponential backoff
   - Progress tracking: Real-time updates

3. **Module vs Inline Scripts**
   - Module execution: Preferred (faster, structured)
   - Fallback execution: Should complete within 2x module time

### Performance Benchmarks (Goals)
- Instance listing with cache: < 50ms
- Instance listing without cache: < 500ms
- Module cmdlet execution: < 300ms
- Fallback script execution: < 600ms

## Test Data Management

### Test Fixtures
- **TestDataGenerator.cs**: C# test data generation
- **TestData.ps1**: PowerShell test data generation
- **MockHelpers.ps1**: Common mock functions

### Data Isolation
- Each test creates its own temporary environment
- Cleanup after test completion
- No shared state between tests

## Error Handling in Tests

### Expected Behaviors
- Graceful degradation when module not available
- Clear error messages for user-facing issues
- Retry logic for transient failures
- Timeout protection for long-running operations

### Test Validation
- Exception types are correct
- Error messages are descriptive
- Logging captures failures
- Exit codes are appropriate

## Test Maintenance

### Best Practices
1. **Test Names**: Descriptive, follows pattern `Method_Scenario_ExpectedBehavior`
2. **Arrange-Act-Assert**: Clear test structure
3. **One Assertion Per Test**: Focus on single behavior
4. **Fast Tests**: Unit tests complete in < 1 second
5. **Deterministic**: No flaky tests, reliable results
6. **Independent**: Tests don't depend on execution order

### Review Checklist
- [ ] Tests are readable and maintainable
- [ ] Edge cases are covered
- [ ] Error paths are tested
- [ ] Mocks are appropriate and minimal
- [ ] No hard-coded paths or environmental dependencies
- [ ] Tests run on clean CI environment

## Continuous Improvement

### Monitoring
- Track test execution time
- Monitor coverage trends
- Identify flaky tests
- Review failed tests in CI

### Goals
- Maintain >80% coverage for core components
- Keep unit test execution under 30 seconds
- Zero tolerance for flaky tests
- 100% green CI builds on main branch

## Appendices

### A. Test File Structure
```
tests/
├── PowerShell/
│   ├── Unit/Private/
│   ├── Unit/Public/
│   ├── Integration/
│   ├── Helpers/
│   ├── PesterConfiguration.psd1
│   └── TestRunner.ps1
├── CSharp/Integration/
├── TestUtilities/
└── README.md
```

### B. Related Documents
- [Test Cases Catalog](Test-Cases.md)
- [CI/CD Testing Guide](Testing-CI-CD-Guide.md)
- [Test Results](../tests/README.md)

### C. Resources
- [Pester Documentation](https://pester.dev/docs/quick-start)
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions](https://fluentassertions.com/introduction)
- [Moq Quickstart](https://github.com/moq/moq4/wiki/Quickstart)

---

**Last Updated**: 2026-01-30  
**Version**: 1.0  
**Status**: Active
