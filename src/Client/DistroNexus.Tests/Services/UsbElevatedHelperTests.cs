using System.Text;
using System.Text.Json;
using DistroNexus.Core.Models;
using DistroNexus.UsbElevatedHelper;

namespace DistroNexus.Tests.Services;

public sealed class UsbElevatedHelperTests
{
    [Theory]
    [InlineData("2341:004")]
    [InlineData("2341:0043;whoami")]
    public async Task Helper_RejectsMalformedHardwareIdentityBeforePipeOrProcessUse(string hardwareId)
    {
        var exitCode = await Program.Main(["--usb-operation", Envelope(hardwareId)]);
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task Helper_RejectsOversizedHardwareIdentityBeforePipeOrProcessUse()
    {
        var exitCode = await Program.Main(["--usb-operation", Envelope(new string('A', 4097) + ":0043")]);
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task Helper_RejectsPreServerAuthenticationProtocol()
    {
        var request = Request("2341:0043");
        var legacy = new UsbElevatedHelperLaunchEnvelope(2, "DistroNexus.Usb.fixture", request,
            Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), Environment.ProcessId);
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(legacy)));
        Assert.Equal(2, await Program.Main(["--usb-operation", payload]));
    }

    [Theory]
    [InlineData(4, "{\"devices\":[{\"bus_id\":\"1-2\",\"vidPid\":\"2341:0043\",\"device\":\"Fixture\",\"status\":\"Not shared\"}]}")]
    [InlineData(5, "{\"devices\":[{\"busId\":\"1-2\",\"hardwareId\":\"2341:0043\",\"description\":\"Fixture\",\"state\":\"Not shared\"}]}")]
    [InlineData(4, "{not-json")]
    [InlineData(5, "{\"devices\":[{\"bus_id\":\"1-2\",\"vidPid\":\"2341:0043;whoami\",\"device\":\"Fixture\",\"status\":\"Not shared\"}]}")]
    public void Helper_RevalidationFailsClosedForAliasMalformedAndCrossMajorJson(int major, string json)
    {
        var request = Request("2341:0043");
        Assert.False(Program.MatchesExpectedDeviceJson(json, request, major));
    }

    [Fact]
    public void Helper_RevalidationAcceptsOnlyItsExactMajorShape()
    {
        var request = Request("2341:0043");
        const string v4 = "{\"devices\":[{\"busId\":\"1-2\",\"hardwareId\":\"2341:0043\",\"description\":\"Fixture\",\"state\":\"Not shared\"}]}";
        const string v5 = "{\"devices\":[{\"bus_id\":\"1-2\",\"vidPid\":\"2341:0043\",\"device\":\"Fixture\",\"status\":\"Not shared\"}]}";
        Assert.True(Program.MatchesExpectedDeviceJson(v4, request, 4));
        Assert.True(Program.MatchesExpectedDeviceJson(v5, request, 5));
    }

    [Fact]
    public void Helper_RejectsHostileSameUserPipeBeforeItCanIssueAuthorization()
    {
        // A pipe name/launch envelope is not a server credential. The helper must require the
        // Windows-reported server PID to be the expected signed desktop process.
        Assert.False(Program.IsDesktopServerAuthorized(4243, 4242, _ => true));
        Assert.False(Program.IsDesktopServerAuthorized(4242, 4242, _ => false));
        Assert.True(Program.IsDesktopServerAuthorized(4242, 4242, id => id == 4242));
    }

    private static string Envelope(string hardwareId)
    {
        var request = Request(hardwareId);
        var value = new UsbElevatedHelperLaunchEnvelope(3, "DistroNexus.Usb.fixture", request, Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), Environment.ProcessId);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));
    }

    private static UsbElevatedOperationRequest Request(string hardwareId) => new(Guid.NewGuid(), UsbDeviceAction.Bind, new UsbBusId("1-2"), hardwareId,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), "issuer", "S-1-5-21-test");
}
