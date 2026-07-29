using System.Text;
using DistroNexus.Core.Models;

namespace DistroNexus.Tests.Models;

public class LosslessIniDocumentTests
{
    [Fact]
    public void ParseAndSerialize_PreservesBomMixedNewlinesAndMalformedLines()
    {
        var body = "# head\r\n[wsl2]\nmemory = 4GB  # keep\r\ninvalid\n\r\n[Other]\r\nKey=Value";
        var bytes = Encoding.UTF8.Preamble.ToArray().Concat(Encoding.UTF8.GetBytes(body)).ToArray();
        var document = LosslessIniDocument.Parse(bytes);
        Assert.Equal(bytes, document.ToBytes());
        Assert.Contains(document.Tokens, t => t.Kind == ConfigurationTokenKind.Malformed && t.Line == 4);
    }

    [Fact]
    public void WithValue_ChangesOnlyLastDuplicateValueAndPreservesFormatting()
    {
        var text = "[wsl2]\r\nmemory=2GB\r\n; between\r\nMemory  =  4GB  # active\r\n[unknown]\r\nx=y\r\n";
        var edited = LosslessIniDocument.Parse(Encoding.UTF8.GetBytes(text)).WithValue("wsl2", "memory", "8GB");
        Assert.Equal(text.Replace("4GB", "8GB"), edited.ToString());
    }

    [Fact]
    public void WithValue_AppendedToFinalSectionWithoutNewline_SeparatesRecordsAndRetainsBomAndConvention()
    {
        var text = "[custom]\r\nkeep=value\r\n[wsl2]\nmemory=2GB";
        var bytes = Encoding.UTF8.Preamble.ToArray().Concat(Encoding.UTF8.GetBytes(text)).ToArray();

        var edited = LosslessIniDocument.Parse(bytes).WithValue("wsl2", "processors", "4");

        var expected = Encoding.UTF8.Preamble.ToArray().Concat(Encoding.UTF8.GetBytes("[custom]\r\nkeep=value\r\n[wsl2]\nmemory=2GB\r\nprocessors=4")).ToArray();
        Assert.Equal(expected, edited.ToBytes());
    }

    [Fact]
    public void Validation_ReportsSourceLineForMalformedAndInvalidValues()
    {
        var document = LosslessIniDocument.Parse(Encoding.UTF8.GetBytes("[wsl2]\nprocessors=zero\nbroken"));
        var diagnostics = DistroNexus.Core.Services.WslConfigurationSchema.Validate(document, DistroNexus.Core.Services.WslConfigurationSchema.Global,
            DistroNexus.Core.Services.WslConfigurationSchema.Global.Where(x => x.RequiredCapability is not null).Select(x => x.RequiredCapability!).ToHashSet());
        Assert.Contains(diagnostics, d => d.Line == 2 && d.Code == "config.invalidValue");
        Assert.Contains(diagnostics, d => d.Line == 3 && d.Code == "config.malformed");
    }

    [Theory]
    [InlineData("kernelCommandLine=quiet # literal ; value")]
    [InlineData("command=printf '# literal;still-value'")]
    public void StringCommands_DoNotTreatHashOrSemicolonAsInlineComments(string record)
    {
        var section = record.StartsWith("command", StringComparison.Ordinal) ? "boot" : "wsl2";
        var document = LosslessIniDocument.Parse(Encoding.UTF8.GetBytes($"[{section}]\n{record}\n"));
        Assert.Contains("# literal", document.Tokens.Single(t => t.Kind == ConfigurationTokenKind.KeyValue).Value);
        Assert.Equal($"[{section}]\n{record}\n", document.ToString());
    }

    [Theory]
    [InlineData("nat")]
    [InlineData("mirrored")]
    [InlineData("none")]
    [InlineData("virtioproxy")]
    public void NetworkingModes_HaveOneEffectiveSchemaAndValidate(string mode)
    {
        Assert.Single(DistroNexus.Core.Services.WslConfigurationSchema.Global,
            d => d.Section == "wsl2" && d.Key == "networkingMode");
        var document = LosslessIniDocument.Parse(Encoding.UTF8.GetBytes($"[wsl2]\nnetworkingMode={mode}\n"));
        Assert.Empty(DistroNexus.Core.Services.WslConfigurationSchema.Validate(document,
            DistroNexus.Core.Services.WslConfigurationSchema.Global));
    }

    [Fact]
    public void CapabilityMapping_DoesNotInferUnprobedFeatures()
    {
        var now = DateTimeOffset.UtcNow;
        var supportedWsl = new CapabilityResult(CapabilityId.Wsl, CapabilityStatus.Supported, "ok", CapabilitySource.WslCli, now);
        var mirrored = new CapabilityResult(CapabilityId.MirroredNetworking, CapabilityStatus.Supported, "ok", CapabilitySource.WslCli, now);
        var sparse = new CapabilityResult(CapabilityId.SparseVhd, CapabilityStatus.Unsupported, "no", CapabilitySource.WslCli, now);
        var snapshot = new PlatformCapabilitySnapshot(new("", new Version(10, 0), "x64", false, null, null, null, null, null),
            new Dictionary<CapabilityId, CapabilityResult> { [CapabilityId.Wsl] = supportedWsl, [CapabilityId.MirroredNetworking] = mirrored, [CapabilityId.SparseVhd] = sparse },
            new Dictionary<CapabilityId, CapabilityResult>(), now);
        var mapped = DistroNexus.Core.Services.WslConfigurationSchema.MapCapabilities(snapshot);
        Assert.Equal(["wsl.config.mirroredNetworking"], mapped);
        var required = DistroNexus.Core.Services.WslConfigurationSchema.Global.Where(d => d.RequiredCapability is not null).Select(d => d.RequiredCapability).Distinct();
        Assert.All(required, capability => Assert.True(capability == "experimental.sparseVhd" || !mapped.Contains(capability!)));
    }
}
