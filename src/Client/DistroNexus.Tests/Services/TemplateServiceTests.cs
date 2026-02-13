using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace DistroNexus.Tests.Services;

[Collection("TemplateServiceSerial")]
public class TemplateServiceTests : IDisposable
{
    private readonly Mock<ILogger<TemplateService>> _mockLogger;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IPowerShellService> _mockPowerShellService;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly string _userTemplatesPath;
    private readonly string _userTemplatesBackupPath;
    private readonly bool _hadUserTemplatesFile;

    public TemplateServiceTests()
    {
        _mockLogger = new Mock<ILogger<TemplateService>>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockPowerShellService = new Mock<IPowerShellService>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDataDistroNexusPath = Path.Combine(appDataPath, "DistroNexus");
        Directory.CreateDirectory(appDataDistroNexusPath);
        _userTemplatesPath = Path.Combine(appDataDistroNexusPath, "templates.json");
        _userTemplatesBackupPath = Path.Combine(appDataDistroNexusPath, $"templates.json.test-backup-{Guid.NewGuid():N}");
        _hadUserTemplatesFile = File.Exists(_userTemplatesPath);

        if (_hadUserTemplatesFile)
        {
            File.Copy(_userTemplatesPath, _userTemplatesBackupPath, true);
        }

        var testTemplates = new List<Template>
        {
            new() { Id = "test-1", Name = "Test Template 1", Category = "Test" },
            new() { 
                Id = "test-2", 
                Name = "Test Template 2", 
                Category = "Dev",
                Scripts = new List<TemplateScript> 
                {
                    new() { Name = "Script1", Content = "echo 1", Type = TemplateScriptType.Bash }
                }
            }
        };
        File.WriteAllText(_userTemplatesPath, JsonSerializer.Serialize(testTemplates));
    }

    public void Dispose()
    {
        if (_hadUserTemplatesFile && File.Exists(_userTemplatesBackupPath))
        {
            File.Copy(_userTemplatesBackupPath, _userTemplatesPath, true);
            File.Delete(_userTemplatesBackupPath);
        }
        else if (!_hadUserTemplatesFile && File.Exists(_userTemplatesPath))
        {
            File.Delete(_userTemplatesPath);
        }

        _httpClient.Dispose();
    }

    [Fact]
    public async Task LoadTemplatesAsync_ReadsLocalFile()
    {
        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        var result = await service.LoadTemplatesAsync();

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Id == "test-1");
    }

    [Fact]
    public async Task ApplyTemplateAsync_ExecutesScripts()
    {
        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        _mockPowerShellService
            .Setup(x => x.ExecuteScriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Output");

        var result = await service.ApplyTemplateAsync("test-2", "TestInstance");

        Assert.True(result.Success);
        Assert.Single(result.ExecutedScripts);
        
        _mockPowerShellService.Verify(
            x => x.ExecuteScriptAsync(It.Is<string>(s => s.Contains("echo 1")), It.IsAny<CancellationToken>()),
            Times.Once);
    }
    
    [Fact]
    public async Task ApplyTemplateAsync_WithVariables_ReplacesContent()
    {
        // Arrange
        var templateWithVar = new List<Template>
        {
            new() { 
                Id = "var-test", 
                Name = "Var Test", 
                Scripts = new List<TemplateScript> 
                {
                    new() { Name = "ScriptVar", Content = "echo ${MY_VAR}", Type = TemplateScriptType.Bash }
                }
            }
        };
        File.WriteAllText(_userTemplatesPath, JsonSerializer.Serialize(templateWithVar));
        
        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        // Force reload to get new file content
        await service.LoadTemplatesAsync(true);

        _mockPowerShellService
            .Setup(x => x.ExecuteScriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Output");

        // Act
        var result = await service.ApplyTemplateAsync(
            "var-test", 
            "TestInstance", 
            new Dictionary<string, string> { { "MY_VAR", "Hello" } });

        // Assert
        Assert.True(result.Success);
        _mockPowerShellService.Verify(
            x => x.ExecuteScriptAsync(It.Is<string>(s => s.Contains("echo Hello")), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchTemplatesAsync_FiltersByName()
    {
        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        var result = await service.SearchTemplatesAsync("Template 1");

        Assert.Single(result);
        Assert.Equal("test-1", result[0].Id);
    }
}
