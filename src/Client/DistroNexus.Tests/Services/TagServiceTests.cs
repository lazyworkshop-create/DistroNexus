using DistroNexus.Core.Exceptions;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Services;

/// <summary>
/// Unit tests for TagService (E-06).
/// </summary>
public class TagServiceTests
{
    private readonly Mock<ILogger<TagService>> _mockLogger;
    private readonly TagService _service;
    private readonly string _tempDir;

    public TagServiceTests()
    {
        _mockLogger = new Mock<ILogger<TagService>>();
        _tempDir    = Path.Combine(Path.GetTempPath(), "DistroNexusTagTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _service    = new TagService(_mockLogger.Object, _tempDir);
    }

    // -----------------------------------------------------------------------
    // GetTagsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetTagsAsync_WhenNoFile_ReturnsEmpty()
    {
        var result = await _service.GetTagsAsync("Ubuntu-22.04");
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTagsAsync_ReturnsTags()
    {
        await _service.SetTagsAsync("Ubuntu-22.04", ["dev", "docker"]);
        var tags = await _service.GetTagsAsync("Ubuntu-22.04");
        Assert.Contains("dev", tags);
        Assert.Contains("docker", tags);
    }

    // -----------------------------------------------------------------------
    // SetTagsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SetTagsAsync_WithNullName_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.SetTagsAsync(null!, ["dev"]));
    }

    [Fact]
    public async Task SetTagsAsync_NormalisesToLowercase()
    {
        await _service.SetTagsAsync("Ubuntu-22.04", ["DEV", "Docker"]);
        var tags = await _service.GetTagsAsync("Ubuntu-22.04");
        Assert.Contains("dev", tags);
        Assert.Contains("docker", tags);
        Assert.DoesNotContain("DEV", tags);
    }

    [Fact]
    public async Task SetTagsAsync_ThrowsWhenMoreThan10Tags()
    {
        var tooMany = Enumerable.Range(1, 11).Select(i => $"tag{i}").ToList();
        var ex = await Assert.ThrowsAsync<WslOperationFailedException>(
            () => _service.SetTagsAsync("Ubuntu-22.04", tooMany));
        Assert.Equal(DistroNexusErrorCode.TooManyTags, ex.Code);
    }

    [Fact]
    public async Task SetTagsAsync_ReplacesExistingTags()
    {
        await _service.SetTagsAsync("Ubuntu-22.04", ["dev", "docker"]);
        await _service.SetTagsAsync("Ubuntu-22.04", ["prod"]);
        var tags = await _service.GetTagsAsync("Ubuntu-22.04");
        Assert.Single(tags);
        Assert.Contains("prod", tags);
    }

    [Fact]
    public async Task SetTagsAsync_WithEmptyList_ClearsTags()
    {
        await _service.SetTagsAsync("Ubuntu-22.04", ["dev"]);
        await _service.SetTagsAsync("Ubuntu-22.04", []);
        var tags = await _service.GetTagsAsync("Ubuntu-22.04");
        Assert.Empty(tags);
    }

    // -----------------------------------------------------------------------
    // AddTagAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AddTagAsync_AddsTagToInstance()
    {
        await _service.AddTagAsync("Ubuntu-22.04", "dev");
        var tags = await _service.GetTagsAsync("Ubuntu-22.04");
        Assert.Contains("dev", tags);
    }

    [Fact]
    public async Task AddTagAsync_NormalisesToLowercase()
    {
        await _service.AddTagAsync("Ubuntu-22.04", "PROD");
        var tags = await _service.GetTagsAsync("Ubuntu-22.04");
        Assert.Contains("prod", tags);
        Assert.DoesNotContain("PROD", tags);
    }

    [Fact]
    public async Task AddTagAsync_DoesNotAddDuplicate()
    {
        await _service.AddTagAsync("Ubuntu-22.04", "dev");
        await _service.AddTagAsync("Ubuntu-22.04", "dev");
        var tags = await _service.GetTagsAsync("Ubuntu-22.04");
        Assert.Single(tags);
    }

    [Fact]
    public async Task AddTagAsync_ThrowsWhenAtMaxTags()
    {
        var tenTags = Enumerable.Range(1, 10).Select(i => $"tag{i}").ToList();
        await _service.SetTagsAsync("Ubuntu-22.04", tenTags);
        var ex = await Assert.ThrowsAsync<WslOperationFailedException>(
            () => _service.AddTagAsync("Ubuntu-22.04", "overflow"));
        Assert.Equal(DistroNexusErrorCode.TooManyTags, ex.Code);
    }

    [Fact]
    public async Task AddTagAsync_WhenTagExceeds32Chars_Throws_ArgumentException()
    {
        var longTag = new string('a', 33);
        var ex = await Assert.ThrowsAsync<WslOperationFailedException>(
            () => _service.AddTagAsync("Ubuntu", longTag, CancellationToken.None));
        Assert.Equal(DistroNexusErrorCode.TooManyTags, ex.Code);
    }

    [Fact]
    public async Task AddTagAsync_WhenTagIs32Chars_Succeeds()
    {
        var maxTag = new string('b', 32);
        await _service.AddTagAsync("Ubuntu", maxTag, CancellationToken.None);
    }

    // -----------------------------------------------------------------------
    // RemoveTagAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RemoveTagAsync_RemovesTag()
    {
        await _service.SetTagsAsync("Ubuntu-22.04", ["dev", "docker"]);
        await _service.RemoveTagAsync("Ubuntu-22.04", "docker");
        var tags = await _service.GetTagsAsync("Ubuntu-22.04");
        Assert.DoesNotContain("docker", tags);
        Assert.Contains("dev", tags);
    }

    [Fact]
    public async Task RemoveTagAsync_IsCaseInsensitive()
    {
        await _service.SetTagsAsync("Ubuntu-22.04", ["dev"]);
        await _service.RemoveTagAsync("Ubuntu-22.04", "DEV");
        var tags = await _service.GetTagsAsync("Ubuntu-22.04");
        Assert.Empty(tags);
    }

    [Fact]
    public async Task RemoveTagAsync_DoesNotThrowWhenTagNotPresent()
    {
        // No exception expected
        await _service.RemoveTagAsync("Ubuntu-22.04", "nonexistent");
    }

    // -----------------------------------------------------------------------
    // RenameInstanceTagsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RenameInstanceTagsAsync_MigratesTagsToNewName()
    {
        await _service.SetTagsAsync("Ubuntu-22.04", ["dev", "docker"]);
        await _service.RenameInstanceTagsAsync("Ubuntu-22.04", "Ubuntu-Renamed");

        var oldTags = await _service.GetTagsAsync("Ubuntu-22.04");
        var newTags = await _service.GetTagsAsync("Ubuntu-Renamed");

        Assert.Empty(oldTags);
        Assert.Contains("dev", newTags);
        Assert.Contains("docker", newTags);
    }

    // -----------------------------------------------------------------------
    // DeleteInstanceTagsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeleteInstanceTagsAsync_RemovesAllTagsForInstance()
    {
        await _service.SetTagsAsync("Ubuntu-22.04", ["dev", "docker"]);
        await _service.DeleteInstanceTagsAsync("Ubuntu-22.04");
        var tags = await _service.GetTagsAsync("Ubuntu-22.04");
        Assert.Empty(tags);
    }

    // -----------------------------------------------------------------------
    // GetAllTagsAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetAllTagsAsync_ReturnsUnionOfAllTags()
    {
        await _service.SetTagsAsync("Ubuntu-22.04", ["dev", "docker"]);
        await _service.SetTagsAsync("Debian", ["prod", "dev"]);

        var all = await _service.GetAllTagsAsync();
        Assert.Contains("dev", all);
        Assert.Contains("docker", all);
        Assert.Contains("prod", all);
        Assert.Equal(all.Distinct().Count(), all.Count); // no duplicates
    }
}
