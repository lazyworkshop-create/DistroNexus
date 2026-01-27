# GitHub Copilot Instructions for DistroNexus

## Project Context
DistroNexus is a Windows Subsystem for Linux (WSL) distribution management tool. Version 2.0.0 migrates from Go/Fyne to .NET/WPF with PowerShell modules.

## Technology Stack
- **Client**: .NET 6/7/8 with WPF (Windows Presentation Foundation)
- **UI Framework**: WPF UI / HandyControl / MaterialDesignInXamlToolkit
- **Architecture**: MVVM pattern with Dependency Injection
- **Backend**: PowerShell Module (`DistroNexus.psm1`)
- **Configuration**: JSON-based settings
- **Target Platform**: Windows 10/11

## Code Standards

### Language Requirements
**ALL code, comments, documentation, and suggestions MUST be in English.**

```csharp
// ✅ CORRECT
/// <summary>
/// Downloads the specified WSL distribution package.
/// </summary>
public async Task DownloadPackageAsync(string url, string destination)

// ❌ NEVER generate Chinese comments
/// <summary>
/// 下载指定的 WSL 发行版包
/// </summary>
```

### Naming Conventions
- **C# Classes/Interfaces**: PascalCase (`WslInstanceManager`, `IDownloadService`)
- **Methods**: PascalCase with verb prefix (`GetInstances()`, `InstallDistribution()`)
- **Properties**: PascalCase (`InstallPath`, `IsRunning`)
- **Private fields**: _camelCase (`_logger`, `_httpClient`)
- **Constants**: UPPER_SNAKE_CASE or PascalCase (`MAX_RETRY_COUNT` or `MaxRetryCount`)
- **PowerShell Cmdlets**: Verb-Noun format (`Install-DistroNexusInstance`, `Get-WslInstance`)

### C# Code Style
```csharp
// Use async/await for I/O operations
public async Task<List<WslInstance>> GetInstancesAsync()
{
    return await _powerShellService.ExecuteAsync<List<WslInstance>>("Get-WslInstance");
}

// Dependency Injection via constructor
public class WslManagerService : IWslManagerService
{
    private readonly IPowerShellService _powerShellService;
    private readonly ILogger<WslManagerService> _logger;

    public WslManagerService(IPowerShellService powerShellService, ILogger<WslManagerService> logger)
    {
        _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}

// MVVM ViewModels with CommunityToolkit.Mvvm
[ObservableObject]
public partial class MainViewModel
{
    [ObservableProperty]
    private ObservableCollection<WslInstanceViewModel> _instances;

    [RelayCommand]
    private async Task RefreshInstancesAsync()
    {
        // Implementation
    }
}
```

### PowerShell Module Style
```powershell
function Install-DistroNexusInstance {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$DistroName,
        
        [Parameter(Mandatory = $true)]
        [ValidateScript({ Test-Path $_ -IsValid })]
        [string]$InstallPath,
        
        [Parameter(Mandatory = $false)]
        [string]$Username = "root"
    )
    
    begin {
        Write-Verbose "Starting installation of $DistroName to $InstallPath"
    }
    
    process {
        # Implementation with proper error handling
        try {
            # Logic here
        }
        catch {
            Write-Error "Failed to install distribution: $_"
            throw
        }
    }
}
```

## Commit Message Format
Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `perf`, `style`

**Examples**:
- `feat(installer): add retry logic for failed downloads`
- `fix(ui): resolve memory leak in instance list view`
- `docs: update PowerShell module API reference`
- `refactor(core): migrate to PowerShell module architecture`

## Architecture Patterns

### MVVM Structure
```
View (XAML) ↔ ViewModel (C#) ↔ Service Layer ↔ PowerShell Module
```

### Service Layer Interfaces
```csharp
public interface IPowerShellService
{
    Task<T> ExecuteAsync<T>(string cmdlet, Dictionary<string, object> parameters = null);
    Task<string> ExecuteScriptAsync(string script);
}

public interface IWslManagerService
{
    Task<List<WslInstance>> GetInstancesAsync();
    Task InstallInstanceAsync(InstallOptions options);
    Task<bool> StartInstanceAsync(string instanceName);
    Task<bool> StopInstanceAsync(string instanceName);
}

public interface IDownloadService
{
    Task<bool> DownloadFileAsync(string url, string destination, IProgress<double> progress = null);
}

public interface ISettingsService
{
    Task<GlobalSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(GlobalSettings settings);
}
```

## Error Handling

### C# Exception Handling
```csharp
try
{
    await _wslManager.StopInstanceAsync(instanceName);
}
catch (WslInstanceNotFoundException ex)
{
    _logger.LogWarning(ex, "Instance {InstanceName} not found", instanceName);
    MessageBox.Show($"Instance '{instanceName}' does not exist.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to stop instance {InstanceName}", instanceName);
    MessageBox.Show($"Failed to stop instance: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
}
```

### PowerShell Error Handling
```powershell
try {
    $result = wsl --terminate $DistroName 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "WSL command failed with exit code $LASTEXITCODE: $result"
    }
}
catch {
    Write-Error "Failed to terminate instance '$DistroName': $_"
    throw
}
```

## Documentation

### XML Documentation (C#)
```csharp
/// <summary>
/// Installs a WSL distribution to the specified path.
/// </summary>
/// <param name="distroName">The name of the distribution to install.</param>
/// <param name="installPath">The directory where the distribution will be installed.</param>
/// <param name="cancellationToken">Token to cancel the operation.</param>
/// <returns>A task that represents the asynchronous installation operation.</returns>
/// <exception cref="ArgumentNullException">Thrown when distroName or installPath is null.</exception>
/// <exception cref="DirectoryNotFoundException">Thrown when installPath does not exist.</exception>
public async Task InstallDistributionAsync(string distroName, string installPath, CancellationToken cancellationToken = default)
```

### Comment-Based Help (PowerShell)
```powershell
<#
.SYNOPSIS
    Installs a WSL distribution to a custom location.

.DESCRIPTION
    Downloads and installs a specified WSL distribution to a user-defined path,
    bypassing the default system drive installation.

.PARAMETER DistroName
    The name of the distribution to install (e.g., "Ubuntu-22.04").

.PARAMETER InstallPath
    The target directory for installation.

.PARAMETER Username
    The default username to create in the distribution. Defaults to "root".

.EXAMPLE
    Install-DistroNexusInstance -DistroName "Ubuntu-22.04" -InstallPath "D:\WSL\Ubuntu"

.EXAMPLE
    Install-DistroNexusInstance -DistroName "Debian" -InstallPath "E:\Linux\Debian" -Username "admin"

.NOTES
    Requires Windows 10 version 2004 or later with WSL2 enabled.
#>
```

## Testing

### Unit Tests (xUnit)
```csharp
public class WslManagerServiceTests
{
    private readonly Mock<IPowerShellService> _mockPowerShellService;
    private readonly Mock<ILogger<WslManagerService>> _mockLogger;
    private readonly WslManagerService _service;

    public WslManagerServiceTests()
    {
        _mockPowerShellService = new Mock<IPowerShellService>();
        _mockLogger = new Mock<ILogger<WslManagerService>>();
        _service = new WslManagerService(_mockPowerShellService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetInstancesAsync_ShouldReturnInstances_WhenCallSucceeds()
    {
        // Arrange
        var expectedInstances = new List<WslInstance> { new WslInstance { Name = "Ubuntu" } };
        _mockPowerShellService
            .Setup(x => x.ExecuteAsync<List<WslInstance>>("Get-WslInstance", null))
            .ReturnsAsync(expectedInstances);

        // Act
        var result = await _service.GetInstancesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("Ubuntu", result[0].Name);
    }
}
```

### PowerShell Tests (Pester)
```powershell
Describe "Install-DistroNexusInstance" {
    Context "When provided valid parameters" {
        It "Should install the distribution successfully" {
            # Arrange
            $testPath = "TestDrive:\WSL\Test"
            New-Item -Path $testPath -ItemType Directory -Force

            # Act
            { Install-DistroNexusInstance -DistroName "Ubuntu-22.04" -InstallPath $testPath } | Should -Not -Throw

            # Assert
            # Add assertions here
        }
    }

    Context "When provided invalid path" {
        It "Should throw an error" {
            # Act & Assert
            { Install-DistroNexusInstance -DistroName "Ubuntu" -InstallPath "Z:\Invalid\Path" } | Should -Throw
        }
    }
}
```

## File Organization

### Project Structure
```
src/
├── Client/
│   ├── DistroNexus.Desktop/          # WPF Application
│   │   ├── Views/                    # XAML Views
│   │   ├── ViewModels/               # ViewModels
│   │   ├── Converters/               # Value Converters
│   │   ├── Resources/                # Images, Icons, Styles
│   │   └── App.xaml                  # Application entry
│   ├── DistroNexus.Core/             # Core Business Logic
│   │   ├── Services/                 # Service implementations
│   │   ├── Models/                   # Data models
│   │   └── Interfaces/               # Service interfaces
│   └── DistroNexus.Tests/            # Unit tests
├── PowerShell/
│   ├── Public/                       # Public cmdlets
│   ├── Private/                      # Internal helper functions
│   ├── DistroNexus.psd1              # Module manifest
│   └── DistroNexus.psm1              # Module script
└── tools/                            # Build and packaging scripts
```

## Security Considerations
- Never hardcode credentials or API keys
- Validate all user input paths (prevent path traversal)
- Use `SecureString` for password handling
- Sanitize PowerShell command parameters to prevent injection
- Log sensitive operations without exposing credentials

## Performance Guidelines
- Use async/await for all I/O operations
- Implement cancellation token support for long-running tasks
- Use `ObservableCollection<T>` for data-bound collections
- Dispose resources properly (implement `IDisposable` where needed)
- Cache frequently accessed data (e.g., distro catalog)

## When Generating Code
1. **Always use English** for all identifiers, comments, and strings (except user-facing UI text)
2. **Follow MVVM pattern** for WPF code
3. **Use dependency injection** for services
4. **Implement proper error handling** with specific exception types
5. **Add XML documentation** for public APIs
6. **Write testable code** with interface abstractions
7. **Use async patterns** for I/O and long-running operations
8. **Apply the Single Responsibility Principle**
9. **Prefer composition over inheritance**
10. **Keep methods focused and concise** (ideally < 20 lines)

## Additional Context
- This is a Windows-only application (no cross-platform requirements for v2.0)
- PowerShell scripts are being migrated to a formal PowerShell module
- Legacy v1.x was built with Go/Fyne (being completely replaced)
- Focus on native Windows UX with Fluent Design System aesthetics
- Support both portable and installed deployment modes
