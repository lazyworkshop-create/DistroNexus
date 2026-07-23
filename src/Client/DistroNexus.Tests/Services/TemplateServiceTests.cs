using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace DistroNexus.Tests.Services;

[Collection("TemplateServiceSerial")]
public class TemplateServiceTests : IDisposable
{
    private readonly Mock<ILogger<TemplateService>> _mockLogger;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IPowerShellService> _mockPowerShellService;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly string _userTemplatesPath;
    private readonly string _userTemplatesBackupPath;
    private readonly bool _hadUserTemplatesFile;

    public TemplateServiceTests()
    {
        _mockLogger = new Mock<ILogger<TemplateService>>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockPowerShellService = new Mock<IPowerShellService>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDataDistroNexusPath = Path.Combine(appDataPath, "DistroNexus");
        Directory.CreateDirectory(appDataDistroNexusPath);
        _userTemplatesPath = Path.Combine(appDataDistroNexusPath, "templates.json");
        _userTemplatesBackupPath = Path.Combine(appDataDistroNexusPath, $"templates.json.test-backup-{Guid.NewGuid():N}");
        _hadUserTemplatesFile = File.Exists(_userTemplatesPath);

        if (_hadUserTemplatesFile)
        {
            File.Copy(_userTemplatesPath, _userTemplatesBackupPath, true);
        }

        var testTemplates = new List<Template>
        {
            new() { Id = "test-1", Name = "Test Template 1", Category = "Test" },
            new() { 
                Id = "test-2", 
                Name = "Test Template 2", 
                Category = "Dev",
                Scripts = new List<TemplateScript> 
                {
                    new() { Name = "Script1", Content = "echo 1", Type = TemplateScriptType.Bash }
                }
            }
        };
        File.WriteAllText(_userTemplatesPath, JsonSerializer.Serialize(testTemplates));
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

        _httpClient.Dispose();
    }

    [Fact]
    public async Task LoadTemplatesAsync_ReadsLocalFile()
    {
        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        var result = await service.LoadTemplatesAsync();

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Id == "test-1");
    }

    [Fact]
    public async Task ApplyTemplateAsync_ExecutesScripts()
    {
        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        string? capturedCommand = null;

        _mockPowerShellService
            .Setup(x => x.ExecuteScriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((command, _) => capturedCommand = command)
            .ReturnsAsync("Output");

        var result = await service.ApplyTemplateAsync("test-2", "TestInstance");

        Assert.True(result.Success);
        Assert.Single(result.ExecutedScripts);
        Assert.NotNull(capturedCommand);
        Assert.Contains("wsl -d 'TestInstance' -- bash '/mnt/", capturedCommand, StringComparison.Ordinal);

        var stagedScriptPath = GetStagedScriptWindowsPath(capturedCommand);
        Assert.True(File.Exists(stagedScriptPath));
        var stagedScript = File.ReadAllText(stagedScriptPath);
        Assert.Contains("echo 1", stagedScript, StringComparison.Ordinal);
    }
    
    [Fact]
    public async Task ApplyTemplateAsync_WithVariables_ReplacesContent()
    {
        // Arrange
        var templateWithVar = new List<Template>
        {
            new() { 
                Id = "var-test", 
                Name = "Var Test", 
                Scripts = new List<TemplateScript> 
                {
                    new() { Name = "ScriptVar", Content = "echo ${MY_VAR}", Type = TemplateScriptType.Bash }
                }
            }
        };
        File.WriteAllText(_userTemplatesPath, JsonSerializer.Serialize(templateWithVar));
        
        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        // Force reload to get new file content
        await service.LoadTemplatesAsync(true);

        string? capturedCommand = null;
        _mockPowerShellService
            .Setup(x => x.ExecuteScriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((command, _) => capturedCommand = command)
            .ReturnsAsync("Output");

        // Act
        var result = await service.ApplyTemplateAsync(
            "var-test", 
            "TestInstance", 
            new Dictionary<string, string> { { "MY_VAR", "Hello" } });

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(capturedCommand);
        var stagedScriptPath = GetStagedScriptWindowsPath(capturedCommand);
        Assert.True(File.Exists(stagedScriptPath));
        var stagedScript = File.ReadAllText(stagedScriptPath);
        Assert.Contains("echo Hello", stagedScript, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchTemplatesAsync_FiltersByName()
    {
        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        var result = await service.SearchTemplatesAsync("Template 1");

        Assert.Single(result);
        Assert.Equal("test-1", result[0].Id);
    }

    [Fact]
    public async Task ApplyTemplateAsync_WhenDistributionTemporarilyUnavailable_RetriesAndSucceeds()
    {
        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        _mockPowerShellService
            .SetupSequence(x => x.ExecuteScriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("PowerShell script failed: There is no distribution with the supplied name."))
            .ReturnsAsync("Output");

        var result = await service.ApplyTemplateAsync("test-2", "TestInstance");

        Assert.True(result.Success);
        _mockPowerShellService.Verify(
            x => x.ExecuteScriptAsync(It.Is<string>(s => s.Contains("wsl -d 'TestInstance'")), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ApplyTemplateAsync_WithCrLfScript_NormalizesLineEndingsBeforeExecution()
    {
        var crlfTemplate = new List<Template>
        {
            new()
            {
                Id = "crlf-test",
                Name = "CRLF Test",
                Scripts = new List<TemplateScript>
                {
                    new()
                    {
                        Name = "CrlfScript",
                        Content = "#!/bin/bash\r\nset -euo pipefail\r\necho ok\r\n",
                        Type = TemplateScriptType.Bash
                    }
                }
            }
        };

        File.WriteAllText(_userTemplatesPath, JsonSerializer.Serialize(crlfTemplate));

        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        await service.LoadTemplatesAsync(true);

        string? capturedCommand = null;
        _mockPowerShellService
            .Setup(x => x.ExecuteScriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((command, _) => capturedCommand = command)
            .ReturnsAsync("Output");

        var result = await service.ApplyTemplateAsync("crlf-test", "TestInstance");

        Assert.True(result.Success);
        Assert.NotNull(capturedCommand);

        var stagedScriptPath = GetStagedScriptWindowsPath(capturedCommand);
        Assert.True(File.Exists(stagedScriptPath));
        var stagedScript = File.ReadAllText(stagedScriptPath);
        Assert.DoesNotContain("\r", stagedScript, StringComparison.Ordinal);
        Assert.Contains("set -euo pipefail\n", stagedScript, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyTemplateAsync_WithScriptPath_ExecutesViaTemporaryScriptInSourceDirectory()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appDataDistroNexusPath = Path.Combine(appDataPath, "DistroNexus");
        var templatesRoot = Path.Combine(appDataDistroNexusPath, "templates");
        var nodejsDir = Path.Combine(templatesRoot, "nodejs-dev");
        var commonDir = Path.Combine(templatesRoot, "common");
        Directory.CreateDirectory(nodejsDir);
        Directory.CreateDirectory(commonDir);

        var scriptFile = Path.Combine(nodejsDir, "install.sh");
        File.WriteAllText(scriptFile, "#!/bin/bash\r\nset -euo pipefail\r\nsource \"$(dirname \"${BASH_SOURCE[0]}\")/../common/lib.sh\"\r\necho ok\r\n");
        File.WriteAllText(Path.Combine(commonDir, "lib.sh"), "#!/bin/bash\r\nlog_info(){ echo \"$1\"; }\r\n");

        var siblingNodeJsDir = Path.Combine(templatesRoot, "nodejs-multi-version-dev");
        Directory.CreateDirectory(siblingNodeJsDir);
        File.WriteAllText(
            Path.Combine(siblingNodeJsDir, "install.sh"),
            "#!/bin/bash\r\nset -euo pipefail\r\nSCRIPT_DIR=\"$(cd \"$(dirname \"${BASH_SOURCE[0]}\")\" && pwd)\"\r\nbash \"${SCRIPT_DIR}/../nodejs-dev/install.sh\"\r\n");

        var pathTemplate = new List<Template>
        {
            new()
            {
                Id = "path-run",
                Name = "Path Run",
                Scripts = new List<TemplateScript>
                {
                    new()
                    {
                        Name = "RunFromPath",
                        ScriptPath = "templates/nodejs-multi-version-dev/install.sh",
                        Type = TemplateScriptType.Bash
                    }
                }
            }
        };
        File.WriteAllText(_userTemplatesPath, JsonSerializer.Serialize(pathTemplate));

        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        await service.LoadTemplatesAsync(true);

        string? capturedCommand = null;
        _mockPowerShellService
            .Setup(x => x.ExecuteScriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((command, _) => capturedCommand = command)
            .ReturnsAsync("Output");

        var result = await service.ApplyTemplateAsync("path-run", "TestInstance");

        Assert.True(result.Success);
        Assert.NotNull(capturedCommand);
        Assert.Contains("wsl -d 'TestInstance' -- bash '/mnt/", capturedCommand, StringComparison.Ordinal);

        var stagingRootPath = GetStagingRootWindowsPath(capturedCommand);
        var stagedScriptPath = GetStagedScriptWindowsPath(capturedCommand);
        var stagedCommonPath = Path.Combine(stagingRootPath, "common", "lib.sh");
        var stagedSiblingPath = Path.Combine(stagingRootPath, "nodejs-dev", "install.sh");
        var stagedTemplatePath = Path.Combine(stagingRootPath, "nodejs-multi-version-dev", "install.sh");

        Assert.True(Directory.Exists(Path.Combine(stagingRootPath, "nodejs-multi-version-dev")));
        Assert.True(File.Exists(stagedScriptPath));
        Assert.True(File.Exists(stagedCommonPath));
        Assert.True(File.Exists(stagedSiblingPath));
        Assert.True(File.Exists(stagedTemplatePath));

        var stagedScript = File.ReadAllText(stagedScriptPath);
        Assert.DoesNotContain("\r", stagedScript, StringComparison.Ordinal);
        Assert.Contains("../nodejs-dev/install.sh", stagedScript, StringComparison.Ordinal);

        var stagedCommon = File.ReadAllText(stagedCommonPath);
        Assert.DoesNotContain("\r", stagedCommon, StringComparison.Ordinal);
        Assert.Contains("log_info", stagedCommon, StringComparison.Ordinal);
    }

    private static string GetStagedScriptWindowsPath(string command)
    {
        var stagedScriptWslPath = ExtractSingleQuotedValue(command, " -- bash '", "'; $exitCode = $LASTEXITCODE");
        return ConvertWslPathToWindowsPath(stagedScriptWslPath);
    }

    private static string GetStagingRootWindowsPath(string command)
    {
        return ExtractSingleQuotedValue(command, "Remove-Item -LiteralPath '", "' -Recurse -Force");
    }

    private static string ExtractSingleQuotedValue(string source, string prefix, string suffix)
    {
        var start = source.IndexOf(prefix, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Prefix '{prefix}' not found in command.");
        start += prefix.Length;

        var end = source.IndexOf(suffix, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Suffix '{suffix}' not found in command.");

        var escapedValue = source[start..end];
        return escapedValue.Replace("''", "'", StringComparison.Ordinal);
    }

    private static string ConvertWslPathToWindowsPath(string wslPath)
    {
        const string mntPrefix = "/mnt/";
        Assert.StartsWith(mntPrefix, wslPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(wslPath.Length > mntPrefix.Length + 2, "Invalid WSL path format.");

        var drive = char.ToUpperInvariant(wslPath[mntPrefix.Length]);
        var relative = wslPath[(mntPrefix.Length + 2)..].Replace('/', '\\');
        if (relative.StartsWith("\\", StringComparison.Ordinal))
        {
            relative = relative[1..];
        }

        return $"{drive}:\\{relative}";
    }

    [Fact]
    public async Task SearchTemplatesAsync_FiltersByScenarioTag()
    {
        var testTemplates = new List<Template>
        {
            new() { Id = "scenario-1", Name = "Cloud Template", ScenarioTags = new List<string> { "cloudnative" } },
            new() { Id = "scenario-2", Name = "Data Template", ScenarioTags = new List<string> { "data" } }
        };
        File.WriteAllText(_userTemplatesPath, JsonSerializer.Serialize(testTemplates));

        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        await service.LoadTemplatesAsync(true);
        var result = await service.SearchTemplatesAsync("cloudnative");

        Assert.Single(result);
        Assert.Equal("scenario-1", result[0].Id);
    }

    [Fact]
    public async Task ValidateTemplateAsync_ProvidesCategoryWarnings()
    {
        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        var template = new Template
        {
            Id = "cloud-template",
            Name = "Cloud Template",
            Category = "CloudNative",
            Scripts = new List<TemplateScript>
            {
                new() { Name = "ok", Content = "echo ok", Type = TemplateScriptType.Bash }
            }
        };

        var result = await service.ValidateTemplateAsync(template, "Ubuntu");

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("ScenarioTag", StringComparison.OrdinalIgnoreCase));
    }
    [Fact]
    public async Task MarketplaceApply_PromotesOnlySuccessfulExactCandidateAndKeepsOldKnownGoodOnFailure()
    {
        File.WriteAllText(_userTemplatesPath, "[]");
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus", "marketplace-template-service", Guid.NewGuid().ToString("N"));
        try
        {
            var oldArchive = CreateMarketplaceZip("marketplace-demo", "echo old");
            var oldManifest = MarketplaceManifest("marketplace-demo", "1", "https://catalog.example.test/old.zip", oldArchive, "echo old");
            var handler = new MarketplaceHandler(new TemplateMarketplaceCatalogV2 { Templates = [oldManifest] }, new Dictionary<string, byte[]> { ["/old.zip"] = oldArchive });
            using var marketplaceHttp = new HttpClient(handler);
            var marketplace = new TemplateMarketplaceService(root, marketplaceHttp);
            var source = await marketplace.AddSourceAsync("https://catalog.example.test/catalog.json", TemplateSourceKind.Remote, false);
            var oldArtifact = await marketplace.DownloadArtifactAsync(source.Id, oldManifest.Id, marketplace.GetManifestDigest(oldManifest));
            await marketplace.ApproveCandidateAsync((await marketplace.CreateReviewGrantAsync(source.Id, oldArtifact.Sha256)).Token);
            _mockPowerShellService.Setup(x => x.ExecuteScriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("ok");
            var successfulService = new TemplateService(_mockLogger.Object, _mockSettingsService.Object, _mockPowerShellService.Object, _httpClient, marketplaceService: marketplace);
            Assert.True((await successfulService.ApplyTemplateAsync("marketplace-demo", "Ubuntu")).Success);
            Assert.Equal(oldArtifact.Sha256, (await marketplace.GetKnownGoodArtifactAsync("marketplace-demo"))!.Sha256);

            var candidateArchive = CreateMarketplaceZip("marketplace-demo", "exit 1");
            var candidateManifest = MarketplaceManifest("marketplace-demo", "2", "https://catalog.example.test/new.zip", candidateArchive, "exit 1");
            handler.Catalog = new TemplateMarketplaceCatalogV2 { Templates = [candidateManifest] };
            handler.Artifacts["/new.zip"] = candidateArchive;
            var candidateArtifact = await marketplace.DownloadArtifactAsync(source.Id, candidateManifest.Id, marketplace.GetManifestDigest(candidateManifest));
            await marketplace.ApproveCandidateAsync((await marketplace.CreateReviewGrantAsync(source.Id, candidateArtifact.Sha256)).Token);
            _mockPowerShellService.Reset();
            _mockPowerShellService.Setup(x => x.ExecuteScriptAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("simulated failure"));
            var failedService = new TemplateService(_mockLogger.Object, _mockSettingsService.Object, _mockPowerShellService.Object, _httpClient, marketplaceService: marketplace);
            Assert.False((await failedService.ApplyTemplateAsync("marketplace-demo", "Ubuntu")).Success);
            Assert.Equal(oldArtifact.Sha256, (await marketplace.GetKnownGoodArtifactAsync("marketplace-demo"))!.Sha256);
            Assert.Equal(candidateArtifact.Sha256, (await marketplace.GetVerifiedArtifactForExecutionAsync(source.Url, candidateManifest)).Sha256);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task LoadTemplatesAsync_ProjectsBuiltInProvenanceForProductTemplates()
    {
        File.Delete(_userTemplatesPath);
        var service = new TemplateService(
            _mockLogger.Object,
            _mockSettingsService.Object,
            _mockPowerShellService.Object,
            _httpClient);

        var template = (await service.LoadTemplatesAsync()).First();

        Assert.True(template.IsOfficial);
        Assert.Equal("distronexus://built-in", template.SourceUrl);
        Assert.Equal("DistroNexus", template.PublisherFingerprint);
        Assert.Equal(TemplateTrustState.BuiltIn, template.TrustState);
    }

    private static byte[] CreateMarketplaceZip(string id, string script)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("template.json");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write($"{{\"id\":\"{id}\",\"name\":\"Marketplace\",\"scripts\":[{{\"name\":\"setup\",\"content\":\"{script}\",\"type\":\"Bash\",\"order\":1}}]}}");
        }
        return stream.ToArray();
    }
    private static TemplateManifestV2 MarketplaceManifest(string id, string version, string url, byte[] archive, string script) => new() { Id = id, Name = "Marketplace", Version = version, ArtifactUrl = url, ArtifactSha256 = Hash(archive), PublisherFingerprint = "fixture", ScriptHashes = [Hash(Encoding.UTF8.GetBytes(script))] };
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private sealed class MarketplaceHandler(TemplateMarketplaceCatalogV2 catalog, Dictionary<string, byte[]> artifacts) : HttpMessageHandler
    {
        public TemplateMarketplaceCatalogV2 Catalog { get; set; } = catalog;
        public Dictionary<string, byte[]> Artifacts { get; } = artifacts;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = Artifacts.TryGetValue(request.RequestUri!.AbsolutePath, out var artifact) ? new ByteArrayContent(artifact) : new StringContent(JsonSerializer.Serialize(Catalog)) });
    }
}
