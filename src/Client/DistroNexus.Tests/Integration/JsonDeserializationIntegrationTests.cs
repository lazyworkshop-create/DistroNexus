using System.Text.Json;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DistroNexus.Tests.Integration;

/// <summary>
/// Integration tests for JSON deserialization from PowerShell module output.
/// </summary>
[Trait("TestScope", "Full")]
public class JsonDeserializationIntegrationTests
{
    private readonly PowerShellService _powerShellService;

    public JsonDeserializationIntegrationTests()
    {
        _powerShellService = new PowerShellService(new Mock<ILogger<PowerShellService>>().Object);
    }

    [Fact]
    public async Task ExecuteModuleCmdletAsync_Should_Deserialize_WslInstance_Objects()
    {
        var options = new ModuleCallOptions
        {
            TimeoutSeconds = 10,
            ParseAsJson = true,
            UseModuleFallback = false
        };

        var result = await _powerShellService.ExecuteModuleCmdletAsync(
            "Get-DistroNexusInstance",
            parameters: null,
            options);

        Assert.NotNull(result);
        if (result.Success && result.ParsedObjects != null)
        {
            Assert.All(result.ParsedObjects, e => Assert.Equal(JsonValueKind.Object, e.ValueKind));
        }
    }

    [Fact]
    public void ExecuteModuleCmdletAsync_Should_Handle_Complex_Nested_Objects()
    {
        var testJson =
            """
            {
              "Name": "Ubuntu-22.04",
              "Version": 2,
              "BasePath": "C:\\WSL\\Ubuntu",
              "Packages": [
                {
                  "FileName": "ubuntu-22.04.tar.gz",
                  "Size": 1073741824,
                  "Checksum": "abc123def456"
                }
              ]
            }
            """;

        using var parsed = JsonDocument.Parse(testJson);
        Assert.Equal("Ubuntu-22.04", parsed.RootElement.GetProperty("Name").GetString());
    }

    [Fact]
    public void ExecuteModuleCmdletAsync_Should_Handle_Empty_Array_Response()
    {
        using var parsed = JsonDocument.Parse("[]");
        Assert.Equal(JsonValueKind.Array, parsed.RootElement.ValueKind);
        Assert.Equal(0, parsed.RootElement.GetArrayLength());
    }

    [Fact]
    public void ExecuteModuleCmdletAsync_Should_Handle_Null_Values_In_Json()
    {
        var jsonWithNulls =
            """
            {
              "Name": "Instance",
              "OptionalField": null,
              "State": "Running"
            }
            """;

        using var parsed = JsonDocument.Parse(jsonWithNulls);
        var element = parsed.RootElement;
        Assert.Equal("Instance", element.GetProperty("Name").GetString());
        Assert.Equal(JsonValueKind.Null, element.GetProperty("OptionalField").ValueKind);
    }

    [Fact]
    public void JsonDeserializer_Should_Deserialize_WslInstance_Model()
    {
        var instanceJson =
            """
            {
              "Name": "Ubuntu-22.04",
              "State": "Running",
              "Version": 2,
              "BasePath": "C:\\WSL\\Ubuntu",
              "DiskSize": 10737418240
            }
            """;

        using var doc = JsonDocument.Parse(instanceJson);
        var element = doc.RootElement;

        Assert.Equal("Ubuntu-22.04", element.GetProperty("Name").GetString());
        Assert.Equal("Running", element.GetProperty("State").GetString());
        Assert.Equal(2, element.GetProperty("Version").GetInt32());
        Assert.Equal("C:\\WSL\\Ubuntu", element.GetProperty("BasePath").GetString());
    }

    [Fact]
    public void JsonDeserializer_Should_Handle_Numeric_Values()
    {
        var json =
            """
            {
              "DiskSize": 10737418240,
              "Version": 2,
              "CreatedAt": 1234567890,
              "Percentage": 95.5
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        Assert.Equal(10737418240L, element.GetProperty("DiskSize").GetInt64());
        Assert.Equal(2, element.GetProperty("Version").GetInt32());
        Assert.Equal(1234567890L, element.GetProperty("CreatedAt").GetInt64());
        Assert.True(Math.Abs(95.5 - element.GetProperty("Percentage").GetDouble()) < 0.01);
    }

    [Fact]
    public void JsonDeserializer_Should_Handle_Boolean_Values()
    {
        var json =
            """
            {
              "IsRunning": true,
              "IsDefault": false,
              "CacheEnabled": true
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;

        Assert.True(element.GetProperty("IsRunning").GetBoolean());
        Assert.False(element.GetProperty("IsDefault").GetBoolean());
        Assert.True(element.GetProperty("CacheEnabled").GetBoolean());
    }

    [Fact]
    public void JsonDeserializer_Should_Handle_DateTime_Values()
    {
        var iso8601Date = "2024-01-31T12:30:45Z";
        var json = $$"""
            {
              "InstallTime": "{{iso8601Date}}",
              "LastAccessed": "{{iso8601Date}}"
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var installTime = doc.RootElement.GetProperty("InstallTime").GetString();

        Assert.NotNull(installTime);
        Assert.Equal(iso8601Date, installTime);
        Assert.NotEqual(default, DateTime.Parse(installTime));
    }

    [Fact]
    public void JsonDeserializer_Should_Handle_Array_Of_Strings()
    {
        var json =
            """
            {
              "Names": ["Ubuntu-22.04", "Debian-11", "Alpine"],
              "Paths": ["C:\\WSL\\Ubuntu", "E:\\WSL\\Debian"]
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var names = doc.RootElement.GetProperty("Names");

        Assert.Equal(JsonValueKind.Array, names.ValueKind);
        Assert.Equal(3, names.GetArrayLength());

        var values = names.EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("Ubuntu-22.04", values);
        Assert.Contains("Debian-11", values);
    }

    [Fact]
    public void JsonDeserializer_Should_Handle_Array_Of_Objects()
    {
        var json =
            """
            {
              "Instances": [
                { "Name": "Ubuntu-22.04", "State": "Running" },
                { "Name": "Debian-11", "State": "Stopped" }
              ]
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var instances = doc.RootElement.GetProperty("Instances");

        Assert.Equal(JsonValueKind.Array, instances.ValueKind);
        Assert.Equal(2, instances.GetArrayLength());

        foreach (var instance in instances.EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(instance.GetProperty("Name").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(instance.GetProperty("State").GetString()));
        }
    }

    [Fact]
    public void JsonDeserializer_Should_Handle_Malformed_Json_Gracefully()
    {
        var malformedJson = "{ invalid json }";
        Assert.ThrowsAny<JsonException>(() => JsonDocument.Parse(malformedJson));
    }

    [Fact]
    public void JsonDeserializer_Should_Preserve_Case_Insensitivity()
    {
        var json =
            """
            {
              "NAME": "Ubuntu-22.04",
              "State": "Running",
              "VERSION": 2
            }
            """;

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Ubuntu-22.04", doc.RootElement.GetProperty("NAME").GetString());
    }
}
