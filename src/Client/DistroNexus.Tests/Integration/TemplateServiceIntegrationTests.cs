using System.Text.Json;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Integration;

[Collection("TemplateServiceSerial")]
[Trait("TestScope", "Full")]
public class TemplateServiceIntegrationTests : IDisposable
{
    private readonly Mock<ILogger<TemplateService>> _mockLogger;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IPowerShellService> _mockPowerShellService;
    private readonly HttpClient _httpClient;
    private readonly string _userTemplatesPath;
    private readonly string _userTemplatesBackupPath;
    private readonly bool _hadUserTemplatesFile;
    private readonly string _historyPath;
    private readonly string _historyBackupPath;
    private readonly bool _hadHistoryFile;

    public TemplateServiceIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<TemplateService>>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockPowerShellService = new Mock<IPowerShellService>();
        _httpClient = new HttpClient();

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDataDistroNexusPath = Path.Combine(appDataPath, "DistroNexus");
        Directory.CreateDirectory(appDataDistroNexusPath);

        _userTemplatesPath = Path.Combine(appDataDistroNexusPath, "templates.json");
        _userTemplatesBackupPath = Path.Combine(appDataDistroNexusPath, $"templates.json.integration-backup-{Guid.NewGuid():N}");
        _hadUserTemplatesFile = File.Exists(_userTemplatesPath);
        if (_hadUserTemplatesFile)
        {
            File.Copy(_userTemplatesPath, _userTemplatesBackupPath, true);
        }
        File.WriteAllText(_userTemplatesPath, "[]");

        _historyPath = Path.Combine(appDataDistroNexusPath, "template-application-history.json");
        _historyBackupPath = Path.Combine(appDataDistroNexusPath, $"template-history.integration-backup-{Guid.NewGuid():N}");
        _hadHistoryFile = File.Exists(_historyPath);
        if (_hadHistoryFile)
        {
            File.Copy(_historyPath, _historyBackupPath, true);
        }
        File.WriteAllText(_historyPath, "[]");

        var templates = new List<Template>
        {
            new()
            {
                Id = "stop-template",
                Name = "Stop Template",
                Scripts = new List<TemplateScript>
                {
                    new() { Name = "Fail Script", Content = "echo fail-script", Type = TemplateScriptType.Bash, Order = 1, ContinueOnError = false },
                    new() { Name = "After Stop", Content = "echo after-stop", Type = TemplateScriptType.Bash, Order = 2 }
                }
            },
            new()
            {
                Id = "continue-template",
                Name = "Continue Template",
                Scripts = new List<TemplateScript>
                {
                    new() { Name = "Fail But Continue", Content = "echo fail-continue", Type = TemplateScriptType.Bash, Order = 1, ContinueOnError = true },
                    new() { Name = "After Continue", Content = "echo after-continue", Type = TemplateScriptType.Bash, Order = 2 }
                }
            },
            new()
            {
                Id = "history-template",
                Name = "History Template",
                Scripts = new List<TemplateScript>
                {
                    new() { Name = "History Script", Content = "echo history", Type = TemplateScriptType.Bash, Order = 1 }
                }
            },
            new()
            {
                Id = "path-template",
                Name = "Path Template",
                Scripts = new List<TemplateScript>
                {
                    new() { Name = "Traversal", ScriptPath = "..\\outside.sh", Type = TemplateScriptType.Bash, Order = 1 }
                }
            }
        };

        File.WriteAllText(_userTemplatesPath, JsonSerializer.Serialize(templates));

        _mockPowerShellService
            .Setup(x => x.ExecuteScriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ok");

        _mockPowerShellService
            .Setup(x => x.ExecuteScriptAsync(It.Is<string>(s => CommandContainsDecodedText(s, "fail-script")), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail-script"));

        _mockPowerShellService
            .Setup(x => x.ExecuteScriptAsync(It.Is<string>(s => CommandContainsDecodedText(s, "fail-continue")), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("fail-continue"));
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

        if (_hadHistoryFile && File.Exists(_historyBackupPath))
        {
            File.Copy(_historyBackupPath, _historyPath, true);
            File.Delete(_historyBackupPath);
        }
        else if (!_hadHistoryFile && File.Exists(_historyPath))
        {
            File.Delete(_historyPath);
        }

        _httpClient.Dispose();
    }

    [Fact]
    public async Task ApplyTemplateAsync_FailFast_WhenContinueOnErrorFalse()
    {
        var service = CreateService();

        var result = await service.ApplyTemplateAsync("stop-template", "inst-stop");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
        _mockPowerShellService.Verify(x => x.ExecuteScriptAsync(It.Is<string>(s => CommandContainsDecodedText(s, "after-stop")), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ApplyTemplateAsync_Continue_WhenContinueOnErrorTrue()
    {
        var service = CreateService();

        var result = await service.ApplyTemplateAsync("continue-template", "inst-continue");

        Assert.True(result.Success);
        Assert.Single(result.Errors);
        Assert.Contains("After Continue", result.ExecutedScripts);
    }

    [Fact]
    public async Task GetApplicationHistoryAsync_FiltersByInstance()
    {
        var service = CreateService();

        await service.ApplyTemplateAsync("history-template", "instance-a");
        await service.ApplyTemplateAsync("history-template", "instance-b");

        var all = await service.GetApplicationHistoryAsync();
        var filtered = await service.GetApplicationHistoryAsync("instance-a");

        Assert.True(all.Count >= 2);
        Assert.Single(filtered);
        Assert.Equal("instance-a", filtered[0].InstanceName);
    }

    [Fact]
    public async Task ApplyTemplateAsync_RejectsPathTraversal()
    {
        var service = CreateService();

        var result = await service.ApplyTemplateAsync("path-template", "inst-path");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("traversal", StringComparison.OrdinalIgnoreCase));
    }

    private TemplateService CreateService()
    {
        return new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);
    }

    private static bool CommandContainsDecodedText(string command, string expected)
    {
        if (command.Contains(expected, StringComparison.Ordinal))
        {
            return true;
        }

        const string prefix = " -- bash '";
        const string suffix = "'; $exitCode = $LASTEXITCODE";
        var start = command.IndexOf(prefix, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        start += prefix.Length;
        var end = command.IndexOf(suffix, start, StringComparison.Ordinal);
        if (end <= start)
        {
            return false;
        }

        try
        {
            var escapedWslPath = command[start..end];
            var wslPath = escapedWslPath.Replace("''", "'", StringComparison.Ordinal);
            const string mntPrefix = "/mnt/";
            if (!wslPath.StartsWith(mntPrefix, StringComparison.OrdinalIgnoreCase) || wslPath.Length <= mntPrefix.Length + 2)
            {
                return false;
            }

            var drive = char.ToUpperInvariant(wslPath[mntPrefix.Length]);
            var relativePath = wslPath[(mntPrefix.Length + 2)..].Replace('/', '\\');
            if (relativePath.StartsWith("\\", StringComparison.Ordinal))
            {
                relativePath = relativePath[1..];
            }

            var windowsPath = $"{drive}:\\{relativePath}";
            if (!File.Exists(windowsPath))
            {
                return false;
            }

            var scriptContent = File.ReadAllText(windowsPath);
            return scriptContent.Contains(expected, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
