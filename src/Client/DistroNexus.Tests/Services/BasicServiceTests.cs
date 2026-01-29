using System;
using DistroNexus.Core.Models;
using Xunit;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Basic compilation and functionality tests for services.
/// </summary>
public class BasicServiceTests
{
    [Fact]
    public void PowerShellScriptResult_CanBeCreated()
    {
        // Arrange & Act
        var result = new PowerShellScriptResult();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Success);
    }

    [Fact]
    public void PowerShellScriptResult_CanSetProperties()
    {
        // Arrange
        var result = new PowerShellScriptResult();

        // Act
        result.ExitCode = 1;
        result.Output = "Test Output";
        result.Error = "Test Error";

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("Test Output", result.Output);
        Assert.Equal("Test Error", result.Error);
        Assert.False(result.Success);
    }

    [Fact]
    public void GlobalSettings_CanBeCreated()
    {
        // Arrange & Act
        var settings = new GlobalSettings();

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(@"C:\WSL", settings.DefaultInstallPath);
        Assert.Equal(2, settings.DefaultWslVersion);
        Assert.Equal("root", settings.DefaultUsername);
    }

    [Fact]
    public void GlobalSettings_CanSetProperties()
    {
        // Arrange
        var settings = new GlobalSettings();

        // Act
        settings.DefaultInstallPath = @"C:\Custom\WSL";
        settings.MaxConcurrentDownloads = 5;
        settings.AutoSaveInterval = 60;

        // Assert
        Assert.Equal(@"C:\Custom\WSL", settings.DefaultInstallPath);
        Assert.Equal(5, settings.MaxConcurrentDownloads);
        Assert.Equal(60, settings.AutoSaveInterval);
    }

    [Fact]
    public void DownloadTask_CanBeCreated()
    {
        // Arrange & Act
        var task = new DownloadTask();

        // Assert
        Assert.NotNull(task);
        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal(DownloadStatus.Pending, task.Status);
    }

    [Fact]
    public void DownloadTask_CanSetProperties()
    {
        // Arrange
        var task = new DownloadTask();

        // Act
        task.PackageId = "test-package";
        task.PackageName = "Test Package";
        task.DownloadUrl = "http://example.com/package.tar.gz";
        task.DestinationPath = @"C:\Downloads";
        task.Status = DownloadStatus.Downloading;
        task.Progress = 50;
        task.DownloadedBytes = 1024;
        task.TotalBytes = 2048;

        // Assert
        Assert.Equal("test-package", task.PackageId);
        Assert.Equal("Test Package", task.PackageName);
        Assert.Equal("http://example.com/package.tar.gz", task.DownloadUrl);
        Assert.Equal(@"C:\Downloads", task.DestinationPath);
        Assert.Equal(DownloadStatus.Downloading, task.Status);
        Assert.Equal(50, task.Progress);
        Assert.Equal(1024, task.DownloadedBytes);
        Assert.Equal(2048, task.TotalBytes);
    }
}