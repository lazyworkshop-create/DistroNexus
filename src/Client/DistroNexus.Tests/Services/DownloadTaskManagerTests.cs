using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for DownloadTaskManager.
/// </summary>
public class DownloadTaskManagerTests : IDisposable
{
    private readonly Mock<IDownloadService> _mockDownloadService;
    private readonly Mock<ICatalogService> _mockCatalogService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<ILogger<DownloadTaskManager>> _mockLogger;
    private readonly DownloadTaskManager _downloadTaskManager;

    public DownloadTaskManagerTests()
    {
        _mockDownloadService = new Mock<IDownloadService>();
        _mockCatalogService = new Mock<ICatalogService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockLogger = new Mock<ILogger<DownloadTaskManager>>();

        // Setup default settings
        var settings = new GlobalSettings
        {
            MaxConcurrentDownloads = 3,
            AutoRetryDownloads = true,
            MaxRetryAttempts = 3
        };

        _mockSettingsService.Setup(x => x.LoadSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        _downloadTaskManager = new DownloadTaskManager(
            _mockDownloadService.Object,
            _mockCatalogService.Object,
            _mockSettingsService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullDownloadService_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DownloadTaskManager(
            null!,
            _mockCatalogService.Object,
            _mockSettingsService.Object,
            _mockLogger.Object));
    }

    [Fact]
    public void AddTask_WithValidParameters_ShouldCreateTask()
    {
        // Arrange
        var package = new DistroPackage { Id = "test-package", Name = "Test Package", DownloadUrl = "http://example.com/package.tar.gz" };
        var destinationPath = @"C:\Downloads";

        // Act
        var task = _downloadTaskManager.AddTask(package, destinationPath);

        // Assert
        Assert.NotNull(task);
        Assert.Equal(package.Id, task.PackageId);
        Assert.Equal(package.Name, task.PackageName);
        Assert.Equal(destinationPath, task.DestinationPath);
        Assert.Equal(DownloadStatus.Pending, task.Status);
    }

    [Fact]
    public void AddTask_WithNullPackage_ShouldThrowArgumentNullException()
    {
        // Arrange
        var destinationPath = @"C:\Downloads";

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _downloadTaskManager.AddTask(null!, destinationPath));
    }

    [Fact]
    public void AddTask_WithNullDestinationPath_ShouldThrowArgumentNullException()
    {
        // Arrange
        var package = new DistroPackage { Id = "test-package" };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _downloadTaskManager.AddTask(package, null!));
    }

    [Fact]
    public void GetTask_WithValidId_ShouldReturnTask()
    {
        // Arrange
        var package = new DistroPackage { Id = "test-package" };
        var destinationPath = @"C:\Downloads";
        var task = _downloadTaskManager.AddTask(package, destinationPath);

        // Act
        var result = _downloadTaskManager.GetTask(task.Id.ToString());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(task.Id.ToString(), result.Id.ToString());
    }

    [Fact]
    public void GetTask_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var invalidId = Guid.NewGuid().ToString();

        // Act
        var result = _downloadTaskManager.GetTask(invalidId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void RemoveTask_WithValidId_ShouldRemoveTask()
    {
        // Arrange
        var package = new DistroPackage { Id = "test-package" };
        var destinationPath = @"C:\Downloads";
        var task = _downloadTaskManager.AddTask(package, destinationPath);

        // Act
        var removed = _downloadTaskManager.RemoveTask(task.Id.ToString());

        // Assert
        Assert.True(removed);
        Assert.Null(_downloadTaskManager.GetTask(task.Id.ToString()));
    }

    [Fact]
    public void RemoveTask_WithInvalidId_ShouldReturnFalse()
    {
        // Arrange
        var invalidId = Guid.NewGuid().ToString();

        // Act
        var removed = _downloadTaskManager.RemoveTask(invalidId);

        // Assert
        Assert.False(removed);
    }

    [Fact]
    public void CancelTask_WithValidId_ShouldCancelTask()
    {
        // Arrange
        var package = new DistroPackage { Id = "test-package" };
        var destinationPath = @"C:\Downloads";
        var task = _downloadTaskManager.AddTask(package, destinationPath);

        // Act
        var cancelled = _downloadTaskManager.CancelTask(task.Id.ToString());

        // Assert
        Assert.True(cancelled);
    }

    [Fact]
    public void CancelTask_WithInvalidId_ShouldReturnFalse()
    {
        // Arrange
        var invalidId = Guid.NewGuid().ToString();

        // Act
        var cancelled = _downloadTaskManager.CancelTask(invalidId);

        // Assert
        Assert.False(cancelled);
    }

    [Fact]
    public void ClearCompletedTasks_ShouldRemoveCompletedTasks()
    {
        // Arrange
        var package1 = new DistroPackage { Id = "test-package-1" };
        var package2 = new DistroPackage { Id = "test-package-2" };
        var destinationPath = @"C:\Downloads";
        
        var task1 = _downloadTaskManager.AddTask(package1, destinationPath);
        var task2 = _downloadTaskManager.AddTask(package2, destinationPath);

        // Manually set one task as completed
        task1.Status = DownloadStatus.Completed;

        // Act
        _downloadTaskManager.ClearCompletedTasks();

        // Assert
        Assert.Null(_downloadTaskManager.GetTask(task1.Id.ToString()));
        Assert.NotNull(_downloadTaskManager.GetTask(task2.Id.ToString()));
    }

    [Fact]
    public void Tasks_InitiallyEmpty_ShouldBeEmpty()
    {
        // Act
        var tasks = _downloadTaskManager.Tasks;

        // Assert
        Assert.NotNull(tasks);
        Assert.Empty(tasks);
    }

    [Fact]
    public void GetActiveTasksCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var package1 = new DistroPackage { Id = "test-package-1" };
        var package2 = new DistroPackage { Id = "test-package-2" };
        var destinationPath = @"C:\Downloads";
        
        var task1 = _downloadTaskManager.AddTask(package1, destinationPath);
        var task2 = _downloadTaskManager.AddTask(package2, destinationPath);

        // Set one task as completed
        task1.Status = DownloadStatus.Completed;
        task2.Status = DownloadStatus.Downloading;

        // Act
        var count = _downloadTaskManager.GetActiveTasksCount();

        // Assert
        Assert.Equal(1, count);
    }

    public void Dispose()
    {
        // DownloadTaskManager doesn't implement IDisposable, so no need to dispose
    }
}