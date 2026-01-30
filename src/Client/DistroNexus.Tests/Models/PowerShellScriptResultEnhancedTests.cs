using System.Collections.Generic;
using System.Text.Json;
using DistroNexus.Core.Models;
using FluentAssertions;
using Xunit;

namespace DistroNexus.Tests.Models;

public class PowerShellScriptResultEnhancedTests
{
    [Fact]
    public void ParsedObjects_ShouldBeNullByDefault()
    {
        // Arrange & Act
        var result = new PowerShellScriptResult();

        // Assert
        result.ParsedObjects.Should().BeNull();
    }

    [Fact]
    public void ParsedObjects_ShouldBeSettable()
    {
        // Arrange
        var result = new PowerShellScriptResult();
        var json = @"[{""name"":""Ubuntu"",""state"":""Running""}]";
        var jsonArray = JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

        // Act
        result.ParsedObjects = jsonArray;

        // Assert
        result.ParsedObjects.Should().NotBeNull();
        result.ParsedObjects.Should().HaveCount(1);
    }

    [Fact]
    public void ParsedObjects_WithMultipleObjects_ShouldStoreAll()
    {
        // Arrange
        var result = new PowerShellScriptResult();
        var json = @"[
            {""name"":""Ubuntu-22.04"",""state"":""Running""},
            {""name"":""Debian"",""state"":""Stopped""},
            {""name"":""Ubuntu-20.04"",""state"":""Running""}
        ]";
        var jsonArray = JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

        // Act
        result.ParsedObjects = jsonArray;

        // Assert
        result.ParsedObjects.Should().HaveCount(3);
    }

    [Fact]
    public void ParsedObjects_WithEmptyArray_ShouldStoreEmptyList()
    {
        // Arrange
        var result = new PowerShellScriptResult();
        var json = @"[]";
        var jsonArray = JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

        // Act
        result.ParsedObjects = jsonArray;

        // Assert
        result.ParsedObjects.Should().NotBeNull();
        result.ParsedObjects.Should().BeEmpty();
    }

    [Fact]
    public void ParsedObjects_WithComplexObjects_ShouldPreserveStructure()
    {
        // Arrange
        var result = new PowerShellScriptResult();
        var json = @"[{
            ""name"":""Ubuntu"",
            ""state"":""Running"",
            ""version"":""2"",
            ""properties"":{
                ""basePath"":""C:\\WSL\\Ubuntu"",
                ""diskSize"":1024
            }
        }]";
        var jsonArray = JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

        // Act
        result.ParsedObjects = jsonArray;

        // Assert
        result.ParsedObjects.Should().HaveCount(1);
        var firstObject = result.ParsedObjects![0];
        firstObject.TryGetProperty("name", out var nameProperty).Should().BeTrue();
        nameProperty.GetString().Should().Be("Ubuntu");
        
        firstObject.TryGetProperty("properties", out var propertiesProperty).Should().BeTrue();
        propertiesProperty.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public void UsedModule_ShouldBeFalseByDefault()
    {
        // Arrange & Act
        var result = new PowerShellScriptResult();

        // Assert
        result.UsedModule.Should().BeFalse();
    }

    [Fact]
    public void UsedModule_ShouldBeSettable()
    {
        // Arrange
        var result = new PowerShellScriptResult();

        // Act
        result.UsedModule = true;

        // Assert
        result.UsedModule.Should().BeTrue();
    }

    [Fact]
    public void Success_WithUsedModuleTrue_ShouldIndicateModuleExecution()
    {
        // Arrange & Act
        var result = new PowerShellScriptResult
        {
            ExitCode = 0,
            Output = "[]",
            UsedModule = true
        };

        // Assert
        result.Success.Should().BeTrue();
        result.UsedModule.Should().BeTrue();
    }

    [Fact]
    public void Success_WithUsedModuleFalse_ShouldIndicateFallbackExecution()
    {
        // Arrange & Act
        var result = new PowerShellScriptResult
        {
            ExitCode = 0,
            Output = "[]",
            UsedModule = false
        };

        // Assert
        result.Success.Should().BeTrue();
        result.UsedModule.Should().BeFalse();
    }

    [Fact]
    public void ObjectInitializer_WithAllEnhancedProperties_ShouldWorkCorrectly()
    {
        // Arrange
        var json = @"[{""name"":""Test""}]";
        var jsonArray = JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

        // Act
        var result = new PowerShellScriptResult
        {
            ExitCode = 0,
            Output = json,
            Error = string.Empty,
            ParsedObjects = jsonArray,
            UsedModule = true
        };

        // Assert
        result.ExitCode.Should().Be(0);
        result.Output.Should().Be(json);
        result.Error.Should().BeEmpty();
        result.ParsedObjects.Should().NotBeNull();
        result.ParsedObjects.Should().HaveCount(1);
        result.UsedModule.Should().BeTrue();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void ParsedObjects_WithSingleObject_ShouldBeAccessible()
    {
        // Arrange
        var result = new PowerShellScriptResult();
        var json = @"[{
            ""Name"":""Ubuntu-22.04"",
            ""State"":""Running"",
            ""Version"":""2"",
            ""IsDefault"":true
        }]";
        var jsonArray = JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();

        // Act
        result.ParsedObjects = jsonArray;

        // Assert
        result.ParsedObjects.Should().HaveCount(1);
        
        var instance = result.ParsedObjects![0];
        instance.TryGetProperty("Name", out var name).Should().BeTrue();
        name.GetString().Should().Be("Ubuntu-22.04");
        
        instance.TryGetProperty("State", out var state).Should().BeTrue();
        state.GetString().Should().Be("Running");
        
        instance.TryGetProperty("Version", out var version).Should().BeTrue();
        version.GetString().Should().Be("2");
        
        instance.TryGetProperty("IsDefault", out var isDefault).Should().BeTrue();
        isDefault.GetBoolean().Should().BeTrue();
    }
}
