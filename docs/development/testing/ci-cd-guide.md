# CI/CD Testing Guide

## Overview
Guide for running automated tests in the DistroNexus CI/CD pipeline.

## GitHub Actions Workflows

### Main Workflow: `.github/workflows/ci.yml`

#### Jobs

**1. build (C# Tests with Coverage)**
- Runs on: `windows-latest`
- Triggers: Push to main/feature branches, Pull requests
- Steps:
  1. Checkout code
  2. Setup .NET 10.0
  3. Cache NuGet packages
  4. Restore dependencies
  5. Build solution
  6. Run C# tests with coverage collection
  7. Generate HTML coverage report
  8. Upload test results and coverage

**2. test-powershell (PowerShell Module Tests)**
- Runs on: `windows-latest`
- Steps:
  1. Checkout code
  2. Install Pester 5.x
  3. Run PowerShell tests with Pester
  4. Generate coverage report (CoverageGutters format)
  5. Upload test results (NUnitXml format)
  6. Upload coverage report

**3. lint-powershell (Static Analysis)**
- Runs on: `windows-latest`
- Steps:
  1. Install PSScriptAnalyzer
  2. Analyze PowerShell scripts
  3. Fail on errors, warn on warnings

## Running Tests Locally

### PowerShell Tests

**Quick run (all tests):**
```powershell
cd tests/PowerShell
.\TestRunner.ps1 -TestType All
```

**With coverage:**
```powershell
.\TestRunner.ps1 -TestType All -CodeCoverage
```

**Unit tests only:**
```powershell
.\TestRunner.ps1 -TestType Unit -CodeCoverage
```

**Integration tests only:**
```powershell
.\TestRunner.ps1 -TestType Integration
```

**CI mode (for local CI simulation):**
```powershell
.\TestRunner.ps1 -TestType All -CodeCoverage -CI
```

### C# Tests

**Run all tests:**
```powershell
dotnet test src/Client/DistroNexus.slnx
```

**With coverage:**
```powershell
dotnet test src/Client/DistroNexus.slnx `
  --collect:"XPlat Code Coverage" `
  --results-directory ./TestResults
```

**Specific test project:**
```powershell
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj
```

**Integration tests:**
```powershell
dotnet test tests/CSharp/Integration/IntegrationTests.csproj
```

**Generate coverage report:**
```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator `
  -reports:TestResults/**/coverage.cobertura.xml `
  -targetdir:CoverageReport `
  -reporttypes:"Html;Badges"

# Open report
Start-Process CoverageReport/index.html
```

## Test Result Artifacts

### PowerShell Tests
- **Location**: `TestResults/powershell-results.xml`
- **Format**: NUnitXml
- **Coverage**: `coverage/powershell-coverage.xml` (CoverageGutters format)
- **Retention**: 7 days in GitHub Actions

### C# Tests
- **Location**: `TestResults/*.trx`
- **Format**: TRX (Visual Studio Test Results)
- **Coverage**: `TestResults/**/coverage.cobertura.xml`
- **HTML Report**: `CoverageReport/index.html`
- **Retention**: 7 days in GitHub Actions

## Coverage Reports

### Accessing Coverage in CI
1. Go to GitHub Actions run
2. Navigate to "Artifacts" section
3. Download:
   - `coverage-report` (HTML report)
   - `powershell-coverage` (XML)
   - `csharp-test-results` (TRX files)

### Coverage Thresholds
| Component | Minimum | Target | Current |
|-----------|---------|--------|---------|
| PowerShell Private | 70% | 75%+ | ~70% |
| PowerShell Public | 75% | 80%+ | ~40% |
| C# Services | 80% | 85%+ | ~75% |
| C# Models | 85% | 90%+ | ~90% |

**Enforcement**: Currently informational only. Future versions will enforce minimum thresholds.

## Debugging Test Failures

### Local Debugging

**PowerShell:**
```powershell
# Run specific test file
Invoke-Pester -Path tests/PowerShell/Unit/Private/Cache.Tests.ps1 -Output Detailed

# Run specific test
Invoke-Pester -Path tests/PowerShell/Unit/Private/Cache.Tests.ps1 -FullNameFilter "*Should return cached instances*"

# Debug mode
Invoke-Pester -Path tests/PowerShell/Unit/Private/Cache.Tests.ps1 -Output Detailed -Debug
```

**C#:**
```powershell
# Run specific test class
dotnet test --filter "FullyQualifiedName~PowerShellServiceExecuteModuleCmdletAsyncTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~PowerShellServiceExecuteModuleCmdletAsyncTests.ExecuteModuleCmdletAsync_WithNullCmdletName_ShouldThrowArgumentNullException"

# Verbose output
dotnet test --logger "console;verbosity=detailed"
```

### CI Debugging

**View logs:**
1. Go to failed workflow run
2. Expand failed job
3. Click on failed step
4. View full logs

**Download artifacts:**
1. Go to workflow run
2. Scroll to "Artifacts" section
3. Download test results
4. Analyze with local tools

**Common Issues:**
- **Module not found**: Check module path detection logic
- **Timeout**: Increase timeout in test configuration
- **Flaky test**: Add retry logic or improve test isolation
- **Missing dependencies**: Verify NuGet restore and Pester installation

## Continuous Improvement

### Monitoring Test Health
- Review test execution time trends
- Track flaky test occurrences
- Monitor coverage changes over time
- Identify slow tests (> 1 second)

### Adding New Tests
1. Create test file following naming conventions
2. Run locally first
3. Ensure tests pass consistently (run 10+ times)
4. Check coverage impact
5. Submit with PR

### Test Best Practices
- ✅ Tests should be fast (< 1 second per unit test)
- ✅ Tests should be isolated (no shared state)
- ✅ Tests should be deterministic (same input = same output)
- ✅ Use descriptive test names
- ✅ Follow AAA pattern (Arrange, Act, Assert)
- ❌ No hard-coded paths or dates
- ❌ No dependencies on external systems (use mocks)
- ❌ No test order dependencies

## Troubleshooting

### Pester Not Found
```powershell
Install-Module -Name Pester -MinimumVersion 5.0.0 -Force -Scope CurrentUser
```

### .NET SDK Not Found
Download from: https://dotnet.microsoft.com/download/dotnet/10.0

### Coverage Tool Not Found
```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool
```

### Tests Pass Locally But Fail in CI
- Check for environment-specific dependencies
- Verify file paths are relative, not absolute
- Ensure no assumptions about system state
- Check for race conditions in parallel tests

## Future Enhancements

### Planned Features
1. **Test Reporter**: dorny/test-reporter for PR comments
2. **Coverage Badges**: Generate badges for README
3. **Nightly E2E Tests**: Full workflow tests on schedule
4. **Performance Benchmarks**: Track performance over time
5. **Parallel Test Execution**: Speed up test runs
6. **Test Retry**: Auto-retry flaky tests (3 attempts)

### Optimization Goals
- Reduce total test time to < 5 minutes
- Achieve 85%+ coverage across all components
- Zero flaky tests tolerance
- 100% green builds on main branch

## Support

For questions or issues with testing:
1. Check [Testing Strategy](Testing-Strategy.md)
2. Review [Test Cases](Test-Cases.md)
3. Open an issue on GitHub
4. Contact the development team

---

**Last Updated**: 2026-01-30  
**Maintained By**: DistroNexus Development Team
