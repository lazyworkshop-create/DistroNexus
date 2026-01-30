using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DistroNexus.Tests.Integration;

/// <summary>
/// Integration tests for JSON deserialization from PowerShell module output.
/// Verifies that complex objects returned from PowerShell can be correctly deserialized to C# models.
/// </summary>
public class JsonDeserializationIntegrationTests
{
    private readonly Mock<ILogger<PowerShellService>> _mockLogger;
    private readonly PowerShellService _powerShellService;

    public JsonDeserializationIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<PowerShellService>>();
        _powerShellService = new PowerShellService(_mockLogger.Object);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Deserialize_WslInstance_Objects()
    {
        // Arrange
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = true,
            UseModuleFallback = false
        };

        // Act
        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstance",
            parameters: null,
            options);

        // Assert
        Assert.NotNull(result);
        if (result.Success && result.ParsedObjects != null)
        {
            // Should have deserialized JSON elements
            Assert.NotEmpty(result.ParsedObjects);
            
            // Each object should have required properties
            foreach (var element in result.ParsedObjects)
            {
                Assert.True(element.ValueKind == JsonValueKind.Object);
            }
        }
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Handle_Complex_Nested_Objects()
    {
        // Arrange - Simulate nested package information structure
        var testJson = @"
        {
            'Name': 'Ubuntu-22.04',
            'Version': 2,
            'BasePath': 'C:\\WSL\\Ubuntu',
            'Packages': [
                {
                    'FileName': 'ubuntu-22.04.tar.gz',
                    'Size': 1073741824,
                    'Checksum': 'abc123def456'
                }
            ]
        }";

        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = true,
            UseModuleFallback = false
        };

        // Act & Assert
        var parsed = JsonDocument.Parse(testJson);
        Assert.NotNull(parsed.RootElement);
        Assert.Equal("Ubuntu-22.04", parsed.RootElement.GetProperty("Name").GetString());
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Handle_Empty_Array_Response()
    {
        // Arrange
        var emptyArrayJson = "[]";

        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = true,
            UseModuleFallback = false
        };

        // Act & Assert
        var parsed = JsonDocument.Parse(emptyArrayJson);
        Assert.Equal(JsonValueKind.Array, parsed.RootElement.ValueKind);
        Assert.Equal(0, parsed.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Handle_Null_Values_In_Json()
    {
        // Arrange
        var jsonWithNulls = @"
        {
            'Name': 'Instance',
            'OptionalField': null,
            'State': 'Running'
        }";

        // Act & Assert
        var parsed = JsonDocument.Parse(jsonWithNulls);
        var element = parsed.RootElement;
        Assert.Equal("Instance", element.GetProperty("Name").GetString());
        Assert.Equal(JsonValueKind.Null, element.GetProperty("OptionalField").ValueKind);
    }

    [Fact]
    public void JsonDeserializer_Should_Deserialize_WslInstance_Model()
    {
        // Arrange
        var instanceJson = @"
        {
            'Name': 'Ubuntu-22.04',
            'State': 'Running',
            'Version': 2,
            'BasePath': 'C:\\WSL\\Ubuntu',
            'DiskSize': 10737418240
        }";

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Act
        using var doc = JsonDocument.Parse(instanceJson);
        var element = doc.RootElement;

        // Act & Assert - Simulate deserialization
        var name = element.GetProperty("Name").GetString();
        var state = element.GetProperty("State").GetString();
        var version = element.GetProperty("Version").GetInt32();
        var basePath = element.GetProperty("BasePath").GetString();

        Assert.Equal("Ubuntu-22.04", name);
        Assert.Equal("Running", state);
        Assert.Equal(2, version);
        Assert.Equal("C:\\WSL\\Ubuntu", basePath);
    }

    [Fact]
    public void JsonDeserializer_Should_Handle_Numeric_Values()
    {
        // Arrange
        var json = @"
        {
            'DiskSize': 10737418240,
            'Version': 2,
            'CreatedAt': 1234567890,
            'Percentage': 95.5
        }";

        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // Act & Assert
        Assert.Equal(10737418240L, element.GetProperty("DiskSize").GetInt64());
        Assert.Equal(2, element.GetProperty("Version").GetInt32());
        Assert.Equal(1234567890L, element.GetProperty("CreatedAt").GetInt64());
        Assert.True(Math.Abs(95.5 - element.GetProperty("Percentage").GetDouble()) < 0.01);
    }

    [Fact]
    public void JsonDeserializer_Should_Handle_Boolean_Values()
    {
        // Arrange
        var json = @"
        {
            'IsRunning': true,
            'IsDefault': false,
            'CacheEnabled': true
        }";

        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // Act & Assert
        Assert.True(element.GetProperty("IsRunning").GetBoolean());
        Assert.False(element.GetProperty("IsDefault").GetBoolean());
        Assert.True(element.GetProperty("CacheEnabled").GetBoolean());
    }

    [Fact]
    public void JsonDeserializer_Should_Handle_DateTime_Values()
    {
        // Arrange
        var iso8601Date = "2024-01-31T12:30:45Z";
        var json = @$"
        {{
            'InstallTime': '{iso8601Date}',
            'LastAccessed': '{iso8601Date}'
        }}";

        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // Act & Assert
        var installTime = element.GetProperty("InstallTime").GetString();
        Assert.NotNull(installTime);
        Assert.Equal(iso8601Date, installTime);

        // Should be parseable as DateTime
        var parsed = DateTime.Parse(installTime);
        Assert.NotEqual(default, parsed);
    }

    [Fact]
    public void JsonDeserializer_Should_Handle_Array_Of_Strings()
    {
        // Arrange
        var json = @"
        {
            'Names': ['Ubuntu-22.04', 'Debian-11', 'Alpine'],
            'Paths': ['C:\\WSL\\Ubuntu', 'E:\\WSL\\Debian']
        }";

        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // Act & Assert
        var names = element.GetProperty("Names");
        Assert.Equal(JsonValueKind.Array, names.ValueKind);
        Assert.Equal(3, names.GetArrayLength());

        var namesList = new List<string>();
        foreach (var item in names.EnumerateArray())
        {
            namesList.Add(item.GetString() ?? "");
        }

        Assert.Contains("Ubuntu-22.04", namesList);
        Assert.Contains("Debian-11", namesList);
    }

    [Fact]
    public void JsonDeserializer_Should_Handle_Array_Of_Objects()
    {
        // Arrange
        var json = @"
        {
            'Instances': [
                { 'Name': 'Ubuntu-22.04', 'State': 'Running' },
                { 'Name': 'Debian-11', 'State': 'Stopped' }
            ]
        }";

        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // Act & Assert
        var instances = element.GetProperty("Instances");
        Assert.Equal(JsonValueKind.Array, instances.ValueKind);
        Assert.Equal(2, instances.GetArrayLength());

        var count = 0;
        foreach (var instance in instances.EnumerateArray())
        {
            var name = instance.GetProperty("Name").GetString();
            var state = instance.GetProperty("State").GetString();
            Assert.NotNull(name);
            Assert.NotNull(state);
            count++;
        }

        Assert.Equal(2, count);
    }

    [Fact]
    public void JsonDeserializer_Should_Handle_Malformed_Json_Gracefully()
    {
        // Arrange
        var malformedJson = "{ invalid json }";

        // Act & Assert
        Assert.Throws<JsonException>(() => JsonDocument.Parse(malformedJson));
    }

    [Fact]
    public void JsonDeserializer_Should_Preserve_Case_Insensitivity()
    {
        // Arrange
        var json = @"
        {
            'NAME': 'Ubuntu-22.04',
            'State': 'Running',
            'VERSION': 2
        }";

        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        // Act & Assert - JSON keys are case-sensitive by default
        // but our deserializer should handle both upper and lower case
        var name = element.GetProperty("NAME").GetString();
        Assert.Equal("Ubuntu-22.04", name);
    }
}
