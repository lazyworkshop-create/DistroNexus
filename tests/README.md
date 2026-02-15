# DistroNexus Test Suite

Complete automated testing infrastructure for the DistroNexus project, covering PowerShell modules, C# services, and integration scenarios.

## Test Structure

```
tests/
├── PowerShell/              # PowerShell module tests (Pester)
│   ├── Unit/                # Unit tests for individual functions
│   │   ├── Private/         # Tests for Private functions
│   │   └── Public/          # Tests for Public cmdlets
│   ├── Integration/         # Integration tests for workflows
│   ├── Helpers/             # Test helper modules
│   │   ├── MockHelpers.ps1  # Mock functions
│   │   └── TestData.ps1     # Test data generators
│   ├── PesterConfiguration.psd1  # Pester configuration
│   └── TestRunner.ps1       # Test execution script
│
├── CSharp/                  # C# tests
│   └── Integration/         # Integration tests
│       └── IntegrationTests.csproj
│
└── TestUtilities/           # Shared C# test utilities
    └── Fixtures/            # Test data and environment setup
        ├── TestDataGenerator.cs
        └── TestDataFiles.cs
```

## Running Tests

### PowerShell Tests

**Run all tests:**
```powershell
cd tests/PowerShell
.\TestRunner.ps1 -TestType All -CodeCoverage
```

**Run unit tests only:**
```powershell
.\TestRunner.ps1 -TestType Unit
```

**Run integration tests only:**
```powershell
.\TestRunner.ps1 -TestType Integration -CodeCoverage
```

**Run with local WSL2-dependent scenarios enabled:**
```powershell
.\TestRunner.ps1 -TestType All -EnableWsl2Scenarios
```

This enables tests guarded by `DISTRONEXUS_RUN_WSL2_TESTS=1` (for example, tests that execute scripts inside a real local WSL instance).

**CI mode (for automation):**
```powershell
.\TestRunner.ps1 -TestType All -CodeCoverage -CI
```

### C# Tests

**Run unit tests:**
```powershell
dotnet test src/Client/DistroNexus.Tests/DistroNexus.Tests.csproj
```

**Run integration tests:**
```powershell
dotnet test tests/CSharp/Integration/IntegrationTests.csproj
```

**Run all tests with coverage:**
```powershell
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

## Prerequisites

### PowerShell Testing
- PowerShell 7.0 or higher
- Pester 5.x or higher
  ```powershell
  Install-Module -Name Pester -MinimumVersion 5.0.0 -Force -Scope CurrentUser
  ```

### C# Testing
- .NET 10.0 SDK
- Required NuGet packages (automatically restored):
  - xUnit 2.9.3
  - Moq 4.20.72
  - FluentAssertions 7.0.0
  - Coverlet 6.0.4

## Test Coverage Goals

| Component | Current | Target |
|-----------|---------|--------|
| PowerShell Private Functions | 0% | 75%+ |
| PowerShell Public Cmdlets | 0% | 80%+ |
| C# Services (PowerShellService) | 60% | 85%+ |
| C# Services (WslManagerService) | 60% | 85%+ |
| C# Models | 50% | 90%+ |
| Integration Tests | N/A | Key paths 100% |

## Key Test Scenarios

### PowerShell Module Tests
1. **Cache Mechanism**
   - Get/Set/Update cache operations
   - Cache expiration validation
   - Cache invalidation

2. **Package Handling**
   - Expand various package formats (.appx, .tar.gz, .zip)
   - Package format detection
   - Error handling for corrupted packages

3. **Terminal Launching**
   - Detect and launch Windows Terminal
   - Fallback to cmd.exe
   - Custom terminal support

4. **Batch Downloads**
   - Concurrent download management
   - Retry mechanism
   - Progress tracking

### C# Service Tests
1. **ExecuteModuleCmdletAsync**
   - Module detection and loading
   - Parameter formatting
   - JSON output parsing
   - Timeout handling

2. **WslManagerService Refactoring**
   - Module-first execution strategy
   - Fallback to inline scripts
   - Object mapping from JSON
   - Error handling

3. **Integration Tests**
   - WPF ↔ PowerShell module communication
   - Cache performance validation
   - End-to-end workflows

## CI/CD Integration

Tests are automatically run in GitHub Actions:
- **On Pull Request**: Unit tests + fast integration tests
- **On Push to main**: Full test suite
- **Nightly**: E2E tests + performance benchmarks

See `.github/workflows/ci.yml` for configuration.

## Writing New Tests

### PowerShell Test Template
```powershell
BeforeAll {
    $modulePath = "$PSScriptRoot/../../../src/PowerShell"
    Import-Module "$modulePath/DistroNexus.psd1" -Force
}

Describe "YourFunction" {
    BeforeEach {
        # Setup test environment
        $testEnv = Initialize-TestEnvironment -TestDrivePath $TestDrive
    }
    
    Context "When condition is met" {
        It "Should return expected result" {
            # Arrange
            # ... setup
            
            # Act
            $result = Your-Function -Parameter $value
            
            # Assert
            $result | Should -Be $expected
        }
    }
}
```

### C# Test Template
```csharp
public class YourServiceTests : IDisposable
{
    private readonly YourService _service;
    
    public YourServiceTests()
    {
        _service = new YourService();
    }
    
    [Fact]
    public async Task MethodName_WithCondition_ShouldReturnExpected()
    {
        // Arrange
        var input = "test";
        
        // Act
        var result = await _service.MethodAsync(input);
        
        // Assert
        result.Should().NotBeNull();
        result.Value.Should().Be("expected");
    }
    
    public void Dispose()
    {
        _service?.Dispose();
    }
}
```

## Troubleshooting

**Pester not found:**
```powershell
Install-Module -Name Pester -MinimumVersion 5.0.0 -Force
```

**Module import errors in tests:**
Ensure the module path is correct and the module is built before running tests.

**Coverage report not generated:**
Check that coverlet.collector is installed and the output path exists.

## Additional Resources

- [Pester Documentation](https://pester.dev)
- [xUnit Documentation](https://xunit.net)
- [FluentAssertions Documentation](https://fluentassertions.com)
- [Project Testing Strategy](../../docs/Testing-Strategy.md)
