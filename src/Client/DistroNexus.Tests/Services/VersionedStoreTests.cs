using System.Text.Json.Nodes;
using System.Text.Json;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public sealed class VersionedStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "DistroNexusTests", Guid.NewGuid().ToString("N"));
    private string PathName => Path.Combine(_directory, "state.json");

    [Fact]
    public async Task WriteAsync_UsesRevisionsAndPreservesUnknownEnvelopeProperties()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(PathName, """{"schemaVersion":1,"revision":4,"updatedAt":"2026-01-01T00:00:00Z","value":{"Name":"old"},"future":"keep"}""");
        var store = new VersionedJsonStore<State>(PathName);

        var result = await store.WriteAsync(new State("new"), 4);

        Assert.True(result.Succeeded);
        Assert.Equal(5, result.Value!.Revision);
        Assert.Equal("keep", JsonNode.Parse(await File.ReadAllTextAsync(PathName))!["future"]!.GetValue<string>());
    }

    [Fact]
    public async Task WriteAsync_OnConflict_RetainsPriorDocument()
    {
        var store = new VersionedJsonStore<State>(PathName);
        await store.WriteAsync(new State("old"), 0);
        var before = await File.ReadAllTextAsync(PathName);

        var result = await store.WriteAsync(new State("new"), 0);

        Assert.Equal(StoreErrorKind.RevisionConflict, result.Error);
        Assert.Equal(before, await File.ReadAllTextAsync(PathName));
    }

    [Fact]
    public async Task ReadAsync_RejectsNewerSchemaWithoutChangingFile()
    {
        Directory.CreateDirectory(_directory);
        var json = """{"schemaVersion":99,"revision":1,"value":{"Name":"old"}}""";
        await File.WriteAllTextAsync(PathName, json);
        var result = await new VersionedJsonStore<State>(PathName).ReadAsync();
        Assert.Equal(StoreErrorKind.NewerSchema, result.Error);
        Assert.Equal(json, await File.ReadAllTextAsync(PathName));
    }

    [Fact]
    public async Task TwoStoreInstances_SerializeAndRejectStaleRevision()
    {
        var first = new VersionedJsonStore<State>(PathName);
        var second = new VersionedJsonStore<State>(PathName);
        var initial = await first.WriteAsync(new State("initial"), 0);
        var results = await Task.WhenAll(
            first.WriteAsync(new State("first"), initial.Value!.Revision),
            second.WriteAsync(new State("second"), initial.Value.Revision));
        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => result.Error == StoreErrorKind.RevisionConflict);
    }

    [Fact]
    public async Task WriteAsync_RecordsUpdatedAt()
    {
        var before = DateTimeOffset.UtcNow;
        var result = await new VersionedJsonStore<State>(PathName).WriteAsync(new State("x"), 0);
        Assert.InRange(result.Value!.UpdatedAt, before, DateTimeOffset.UtcNow);
        Assert.NotNull(JsonNode.Parse(await File.ReadAllTextAsync(PathName))!["updatedAt"]);
    }

    [Fact]
    public async Task WriteAsync_WhenLateReplaceFails_RetainsPriorDocumentAndReturnsStructuredError()
    {
        var store = new VersionedJsonStore<State>(PathName);
        var first = await store.WriteAsync(new State("old"), 0);
        var before = await File.ReadAllTextAsync(PathName);
        Directory.CreateDirectory(PathName + ".bak");
        var result = await store.WriteAsync(new State("new"), first.Value!.Revision);
        Assert.Equal(StoreErrorKind.IoFailure, result.Error);
        Assert.Equal(before, await File.ReadAllTextAsync(PathName));
    }

    [Fact]
    public async Task ReadAsync_LegacyDocumentReportsSchemaOne()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(PathName, """{"Name":"legacy"}""");
        var result = await new VersionedJsonStore<State>(PathName, legacyReader: node => node.Deserialize<State>()!).ReadAsync();
        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Value!.SchemaVersion);
        Assert.Equal(0, result.Value.Revision);
    }

    [Fact]
    public async Task ReadAsync_AppliesOlderSchemaMigrationsInOrder()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(PathName, """{"schemaVersion":1,"revision":2,"updatedAt":"2026-01-01T00:00:00Z","value":{"Name":"a"}}""");
        var store = new VersionedJsonStore<State>(PathName, 3, migrations: new Dictionary<int, Func<State, State>>
        {
            [1] = value => value with { Name = value.Name + "b" },
            [2] = value => value with { Name = value.Name + "c" }
        });
        var result = await store.ReadAsync();
        Assert.Equal("abc", result.Value!.Value.Name);
    }

    [Fact]
    public async Task ReadAsync_PreservesLegacyNumericEnumDocumentsByDefault()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(PathName, """{"schemaVersion":1,"revision":1,"updatedAt":"2026-01-01T00:00:00Z","value":{"Mode":1}}""");
        var result = await new VersionedJsonStore<EnumState>(PathName).ReadAsync();
        Assert.True(result.Succeeded);
        Assert.Equal(TestMode.Second, result.Value!.Value.Mode);
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
    private sealed record State(string Name);
    private sealed record EnumState(TestMode Mode);
    private enum TestMode { First, Second }
}
