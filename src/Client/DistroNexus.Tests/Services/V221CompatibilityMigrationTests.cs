using System.Text.Json.Nodes;
using System.Text.Json;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DistroNexus.Tests.Services;

/// <summary>Deterministic v2.2.1 persistence fixtures used by the v2.3 compatibility boundary.</summary>
public sealed class V221CompatibilityMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "DistroNexusV221", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SettingsAndTags_LegacyDocument_RemainReadableAndBecomeEnvelopeSafeOnWrite()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(path, """{"DefaultWslVersion":2,"instanceTags":{"Ubuntu":["dev","local"]},"futureSetting":"preserve"}""");

        var tags = new TagService(NullLogger<TagService>.Instance, _root);
        Assert.Equal(["dev", "local"], await tags.GetTagsAsync("Ubuntu"));

        // This is the shape emitted after v2.3 settings persistence. It must remain readable
        // and a tag update must not discard envelope metadata or unknown user settings.
        await File.WriteAllTextAsync(path, """{"schemaVersion":1,"revision":3,"updatedAt":"2026-01-01T00:00:00Z","value":{"DefaultWslVersion":2,"instanceTags":{"Ubuntu":["dev"]},"futureSetting":"preserve"},"futureEnvelope":"preserve"}""");
        Assert.Equal(["dev"], await tags.GetTagsAsync("Ubuntu"));
        await tags.AddTagAsync("Ubuntu", "local");

        var document = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject();
        Assert.Equal("preserve", document["futureEnvelope"]!.GetValue<string>());
        Assert.Equal("preserve", document["value"]!["futureSetting"]!.GetValue<string>());
        Assert.Equal(["dev", "local"], document["value"]!["instanceTags"]!["Ubuntu"]!.AsArray().Select(x => x!.GetValue<string>()));
    }

    [Fact]
    public async Task BackupAndTemplateLegacyFixtures_AreAcceptedByVersionedStoresWithoutMutation()
    {
        Directory.CreateDirectory(_root);
        var schedules = Path.Combine(_root, "backup-schedules.json");
        await File.WriteAllTextAsync(schedules, """[{"Name":"Ubuntu","Destination":"D:\\Backups","Frequency":"Daily","RetentionCount":3,"Time":"02:00:00"}]""");
        var service = new BackupService(new Moq.Mock<DistroNexus.Core.Interfaces.IPowerShellService>().Object, NullLogger<BackupService>.Instance, _root);
        var recovered = await service.GetSchedulesAsync();
        Assert.Single(recovered);
        Assert.Equal("Ubuntu", recovered[0].Name);

        var templates = Path.Combine(_root, "templates.json");
        await File.WriteAllTextAsync(templates, """[{"Id":"legacy","Name":"Legacy","Version":"2.2.1","Scripts":[]}]""");
        var store = new VersionedJsonStore<List<Template>>(templates, legacyReader: n => n.Deserialize<List<Template>>() ?? []);
        var result = await store.ReadAsync();
        Assert.True(result.Succeeded);
        Assert.Equal("legacy", result.Value!.Value.Single().Id);
        Assert.Contains("2.2.1", await File.ReadAllTextAsync(templates), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NewerV23Schema_IsRejectedWithoutChangingLegacyData()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "backup-schedules.json");
        const string json = """{"schemaVersion":99,"revision":1,"value":[]}""";
        await File.WriteAllTextAsync(path, json);
        var store = new VersionedJsonStore<List<BackupSchedule>>(path, legacyReader: n => n.Deserialize<List<BackupSchedule>>() ?? []);
        var result = await store.ReadAsync();
        Assert.Equal(StoreErrorKind.NewerSchema, result.Error);
        Assert.Equal(json, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task LegacyInstanceRegistrationFixture_MigratesOnceAndPreservesRegistrationData()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "instance-registrations.json");
        await File.WriteAllTextAsync(path, """[{"Name":"Ubuntu","State":"Stopped","Version":2,"InstallPath":"D:\\WSL\\Ubuntu"}]""");
        var store = new VersionedJsonStore<List<WslInstance>>(path, legacyReader: node => node.Deserialize<List<WslInstance>>() ?? []);
        var legacy = await store.ReadAsync();
        Assert.True(legacy.Succeeded);
        Assert.Equal("Ubuntu", legacy.Value!.Value.Single().Name);

        var write = await store.WriteAsync(legacy.Value.Value, legacy.Value.Revision);
        Assert.True(write.Succeeded);
        var firstMigration = await File.ReadAllTextAsync(path);
        Assert.Contains("\"schemaVersion\"", firstMigration, StringComparison.Ordinal);
        Assert.Equal(1, write.Value!.Revision);

        // A second read/write is deterministic and does not reinterpret the migrated payload.
        var reread = await store.ReadAsync();
        var second = await store.WriteAsync(reread.Value!.Value, reread.Value.Revision);
        Assert.True(second.Succeeded);
        Assert.Equal(2, second.Value!.Revision);
        Assert.Equal("Ubuntu", (await store.ReadAsync()).Value!.Value.Single().Name);
    }

    [Fact]
    public async Task TemplateService_LoadsAndPreservesV221CacheThroughAnIdempotentWrite()
    {
        Directory.CreateDirectory(_root);
        var cache = Path.Combine(_root, "templates.json");
        await File.WriteAllTextAsync(cache, """[{"Id":"legacy-dev","Name":"Legacy developer","Version":"2.2.1","Category":"Development","Scripts":[]}]""");
        var settings = new Moq.Mock<DistroNexus.Core.Interfaces.ISettingsService>();
        var powershell = new Moq.Mock<DistroNexus.Core.Interfaces.IPowerShellService>();
        using var http = new HttpClient();
        var service = new TemplateService(NullLogger<TemplateService>.Instance, settings.Object, powershell.Object, http,
            appDataDirectory: _root, localTemplatesPath: Path.Combine(_root, "missing-builtins.json"));

        var legacy = await service.LoadTemplatesAsync();
        Assert.Single(legacy);
        Assert.Equal("legacy-dev", legacy[0].Id);
        Assert.True(legacy[0].IsCustom);

        Assert.True(await service.AddCustomTemplateAsync(new Template { Id = "v23-tool", Name = "v2.3 tool", IsCustom = true }));
        var reloaded = new TemplateService(NullLogger<TemplateService>.Instance, settings.Object, powershell.Object, http,
            appDataDirectory: _root, localTemplatesPath: Path.Combine(_root, "missing-builtins.json"));
        var persisted = await reloaded.LoadTemplatesAsync();
        Assert.Equal(["legacy-dev", "v23-tool"], persisted.Select(x => x.Id).OrderBy(x => x));

        // A duplicate custom id is an idempotent upsert, not a duplicate cache entry.
        Assert.True(await reloaded.AddCustomTemplateAsync(new Template { Id = "legacy-dev", Name = "updated legacy", IsCustom = true }));
        var afterUpsert = await reloaded.LoadTemplatesAsync(true);
        Assert.Equal(2, afterUpsert.Count);
        Assert.Equal("updated legacy", afterUpsert.Single(x => x.Id == "legacy-dev").Name);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
