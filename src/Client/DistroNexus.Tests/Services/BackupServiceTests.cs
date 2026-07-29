using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for BackupService (E-04).
/// </summary>
public class BackupServiceTests
{
    private readonly Mock<IPowerShellService> _mockPowerShellService;
    private readonly Mock<ILogger<BackupService>> _mockLogger;
    private readonly BackupService _service;
    private readonly string _tempDir;

    public BackupServiceTests()
    {
        _mockPowerShellService = new Mock<IPowerShellService>();
        _mockLogger            = new Mock<ILogger<BackupService>>();
        _tempDir               = Path.Combine(Path.GetTempPath(), "DistroNexusTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _service = new BackupService(
            _mockPowerShellService.Object,
            _mockLogger.Object,
            _tempDir);
    }

    // -----------------------------------------------------------------------
    // GetSchedulesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSchedulesAsync_WhenNoFile_ReturnsEmptyList()
    {
        // No schedule file written — service should return empty
        var result = await _service.GetSchedulesAsync();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetSchedulesAsync_ReturnsPersistedSchedules()
    {
        // Arrange — seed a schedule file
        var schedule = new BackupSchedule
        {
            Name           = "Ubuntu-22.04",
            Destination    = @"C:\Backups",
            Frequency      = "Daily",
            RetentionCount = 7,
            Time           = new TimeSpan(2, 0, 0)
        };
        await _service.SaveScheduleAsync(schedule);

        // Act
        var result = await _service.GetSchedulesAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Ubuntu-22.04", result[0].Name);
    }

    [Fact]
    public async Task LegacySchedules_AreReadAndMigratedOnNextWrite()
    {
        var path = Path.Combine(_tempDir, "backup-schedules.json");
        await File.WriteAllTextAsync(path, """[{"Name":"Legacy","Destination":"C:\\Backup","Frequency":"Daily","RetentionCount":2,"Time":"02:00:00"}]""");
        var schedules = await _service.GetSchedulesAsync();
        Assert.Single(schedules);
        await _service.SaveScheduleAsync(schedules[0]);
        var root = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("Legacy", root["value"]![0]!["Name"]!.GetValue<string>());
    }

    [Fact]
    public async Task NewerSchema_IsExplicitlyRejectedWithoutOverwritingIt()
    {
        var path = Path.Combine(_tempDir, "backup-schedules.json");
        var json = """{"schemaVersion":99,"revision":1,"updatedAt":"2026-01-01T00:00:00Z","value":[]}""";
        await File.WriteAllTextAsync(path, json);
        var exception = await Assert.ThrowsAsync<WslOperationFailedException>(() => _service.GetSchedulesAsync());
        Assert.Equal(DistroNexusErrorCode.StoreSchemaUnsupported, exception.Code);
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }

    // -----------------------------------------------------------------------
    // SaveScheduleAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SaveScheduleAsync_WithNullSchedule_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SaveScheduleAsync(null!));
    }

    [Fact]
    public async Task SaveScheduleAsync_WritesScheduleToFile()
    {
        var schedule = new BackupSchedule
        {
            Name           = "Debian",
            Destination    = @"D:\Backups",
            Frequency      = "Weekly:Monday",
            RetentionCount = 4,
            Time           = new TimeSpan(3, 0, 0)
        };

        await _service.SaveScheduleAsync(schedule);

        var schedules = await _service.GetSchedulesAsync();
        Assert.Contains(schedules, s => s.Name == "Debian" && s.Frequency == "Weekly:Monday");
    }

    [Fact]
    public async Task SaveScheduleAsync_OverwritesExistingScheduleForSameName()
    {
        var scheduleV1 = new BackupSchedule
        {
            Name           = "Ubuntu-22.04",
            Destination    = @"C:\Backups",
            Frequency      = "Daily",
            RetentionCount = 5,
            Time           = new TimeSpan(2, 0, 0)
        };
        var scheduleV2 = new BackupSchedule
        {
            Name           = "Ubuntu-22.04",
            Destination    = @"D:\Backups",
            Frequency      = "Weekly:Friday",
            RetentionCount = 10,
            Time           = new TimeSpan(4, 0, 0)
        };

        await _service.SaveScheduleAsync(scheduleV1);
        await _service.SaveScheduleAsync(scheduleV2);

        var schedules = await _service.GetSchedulesAsync();
        var match = schedules.Where(s => s.Name == "Ubuntu-22.04").ToList();
        Assert.Single(match);
        Assert.Equal("Weekly:Friday", match[0].Frequency);
    }

    // -----------------------------------------------------------------------
    // RemoveScheduleAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RemoveScheduleAsync_WithNullName_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.RemoveScheduleAsync(null!));
    }

    [Fact]
    public async Task RemoveScheduleAsync_WithEmptyName_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<WslOperationFailedException>(
            () => _service.RemoveScheduleAsync(string.Empty));
        Assert.Equal(DistroNexusErrorCode.InstanceNotFound, ex.Code);
    }

    [Fact]
    public async Task RemoveScheduleAsync_RemovesScheduleFromFile()
    {
        var schedule = new BackupSchedule
        {
            Name           = "Ubuntu-22.04",
            Destination    = @"C:\Backups",
            Frequency      = "Daily",
            RetentionCount = 7,
            Time           = new TimeSpan(2, 0, 0)
        };
        await _service.SaveScheduleAsync(schedule);

        await _service.RemoveScheduleAsync("Ubuntu-22.04");

        var schedules = await _service.GetSchedulesAsync();
        Assert.DoesNotContain(schedules, s => s.Name == "Ubuntu-22.04");
    }

    [Fact]
    public async Task RemoveScheduleAsync_ThrowsWhenScheduleNotFound()
    {
        var ex = await Assert.ThrowsAsync<WslOperationFailedException>(
            () => _service.RemoveScheduleAsync("NonExistentInstance"));
        Assert.Equal(DistroNexusErrorCode.ScheduleNotFound, ex.Code);
    }

    // -----------------------------------------------------------------------
    // InvokeBackupAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task InvokeBackupAsync_WithNullName_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.InvokeBackupAsync(null!, @"C:\Backups", 5));
    }

    [Fact]
    public async Task InvokeBackupAsync_WithEmptyDestination_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<WslOperationFailedException>(
            () => _service.InvokeBackupAsync("Ubuntu-22.04", string.Empty, 5));
        Assert.Equal(DistroNexusErrorCode.BackupDestinationFull, ex.Code);
    }

    [Fact]
    public async Task InvokeBackupAsync_Should_Invoke_Backup_Cmdlet()
    {
        // Arrange
        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Invoke-DistroNexusBackup",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult { ExitCode = 0, Error = string.Empty, UsedModule = true });

        // Act
        await _service.InvokeBackupAsync("Ubuntu-22.04", @"C:\Backups", 7);

        // Assert — correct cmdlet was called with correct parameters
        _mockPowerShellService.Verify(
            x => x.ExecuteModuleCmdletAsync(
                "Invoke-DistroNexusBackup",
                It.Is<Dictionary<string, object>>(d =>
                    d.ContainsKey("Name") && (string)d["Name"] == "Ubuntu-22.04" &&
                    d.ContainsKey("RetentionCount") && (int)d["RetentionCount"] == 7),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeBackupAsync_WhenCmdletFails_ThrowsInvalidOperationException()
    {
        _mockPowerShellService
            .Setup(x => x.ExecuteModuleCmdletAsync(
                "Invoke-DistroNexusBackup",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<ModuleCallOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PowerShellScriptResult
            {
                ExitCode   = 1,
                Error      = "Backup failed: export error",
                UsedModule = true
            });

        var ex = await Assert.ThrowsAsync<WslOperationFailedException>(
            () => _service.InvokeBackupAsync("Ubuntu-22.04", @"C:\Backups", 5));
        Assert.Equal(DistroNexusErrorCode.BackupFailed, ex.Code);
    }
}
