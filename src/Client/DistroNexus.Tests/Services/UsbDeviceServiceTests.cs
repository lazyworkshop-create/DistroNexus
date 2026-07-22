using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public sealed class UsbDeviceServiceTests
{
    [Fact]
    public void BusId_RejectsShellSyntax()
    {
        Assert.Throws<ArgumentException>(() => new UsbBusId("1-2;whoami"));
        Assert.Equal("1-A", new UsbBusId("1-a").Value);
    }
    [Fact]
    public void TrustedUsbIpdResolver_FailsClosedForRelativeAndOutsidePaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus-usbipd-root");
        Assert.False(TrustedUsbIpdExecutable.IsTrustedForLaunch("usbipd.exe", root));
        Assert.False(TrustedUsbIpdExecutable.IsTrustedForLaunch(Path.Combine(Path.GetTempPath(), "usbipd.exe"), root));
    }
    [Fact]
    public void TrustedUsbIpdResolver_RejectsDirectoryReparsePointBelowInstallationRoot()
    {
        var temporaryRoot = CreateTemporaryDirectory();
        try
        {
            var installationRoot = Path.Combine(temporaryRoot, "Program Files");
            var productDirectory = Path.Combine(installationRoot, "usbipd-win");
            Directory.CreateDirectory(installationRoot);
            Directory.CreateDirectory(productDirectory);
            var candidate = Path.Combine(productDirectory, "usbipd.exe");
            File.WriteAllText(candidate, "test");

            Assert.False(TrustedUsbIpdExecutable.HasNoReparsePointInExistingPath(installationRoot, candidate,
                path => path.Equals(productDirectory, StringComparison.OrdinalIgnoreCase) ? FileAttributes.Directory | FileAttributes.ReparsePoint : ExistingAttributes(path)));
        }
        finally { DeleteTemporaryDirectory(temporaryRoot); }
    }
    [Fact]
    public void TrustedUsbIpdResolver_RejectsFileReparsePointBelowInstallationRoot()
    {
        var temporaryRoot = CreateTemporaryDirectory();
        try
        {
            var installationRoot = Path.Combine(temporaryRoot, "Program Files");
            var productDirectory = Path.Combine(installationRoot, "usbipd-win");
            Directory.CreateDirectory(productDirectory);
            var candidate = Path.Combine(productDirectory, "usbipd.exe");
            File.WriteAllText(candidate, "test");

            Assert.False(TrustedUsbIpdExecutable.HasNoReparsePointInExistingPath(installationRoot, candidate,
                path => path.Equals(candidate, StringComparison.OrdinalIgnoreCase) ? FileAttributes.ReparsePoint : ExistingAttributes(path)));
        }
        finally { DeleteTemporaryDirectory(temporaryRoot); }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "DistroNexus-usbipd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    private static FileAttributes? ExistingAttributes(string path) =>
        File.Exists(path) || Directory.Exists(path) ? File.GetAttributes(path) : null;
    [Fact]
    public void Parser_UsesOnlyRecognizedRows()
    {
        var devices = UsbIpdAdapter.ParseTable("BUSID  VID:PID    DEVICE                                      STATE\n1-2    2341:0043   Arduino Uno                                  Shared\nbad ;  0000:0000   nope                                        Shared\n");
        var device = Assert.Single(devices);
        Assert.Equal("1-2", device.BusId.Value); Assert.True(device.IsShared);
    }
    [Fact]
    public void JsonParser_RejectsMalformedOutput()
    {
        Assert.False(UsbIpdAdapter.TryParseJson("{not json", out var values));
        Assert.Empty(values);
    }
    [Theory]
    [InlineData("usbipd-win 4.2.0", 4)]
    [InlineData("usbipd-win version unknown", null)]
    [InlineData("usbipd-win 99999999999.2.0", null)]
    public void VersionParser_HandlesKnownAndMalformedVersions(string text, int? major)
    {
        Assert.Equal(major, UsbIpdAdapter.ParseVersion(text)?.Major);
    }
    [Fact]
    public void VersionParser_FailsClosedForOversizedOutput()
    {
        Assert.Null(UsbIpdAdapter.ParseVersion("usbipd-win 4.2.0 " + new string('x', 4096)));
    }
    [Fact]
    public async Task Bind_RequiresCurrentSingleUsePreviewAndElevatedBoundary()
    {
        var adapter = new FakeAdapter(); adapter.Devices[0] = adapter.Devices[0] with { Availability = UsbDeviceAvailability.Available, IsShared = false }; var broker = new FakeBroker(); var service = new UsbDeviceService(adapter, broker);
        var preview = await service.PreviewAsync(UsbDeviceAction.Bind, new UsbBusId("1-2"));
        var first = await service.ExecuteAsync(preview);
        var second = await service.ExecuteAsync(preview);
        Assert.True(first.Succeeded); Assert.False(second.Succeeded); Assert.Equal(1, broker.Calls);
    }
    [Fact]
    public async Task Attach_RejectsUnsafeDistributionAndStaleDevice()
    {
        var adapter = new FakeAdapter(); var service = new UsbDeviceService(adapter, new FakeBroker());
        await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewAsync(UsbDeviceAction.Attach, new UsbBusId("1-2"), "Ubuntu\n--exec"));
        var preview = await service.PreviewAsync(UsbDeviceAction.Attach, new UsbBusId("1-2"), "Ubuntu");
        adapter.Devices.Clear();
        var result = await service.ExecuteAsync(preview);
        Assert.False(result.Succeeded); Assert.Equal("Usb.StaleBusId", result.OutcomeCode);
    }
    [Fact]
    public async Task UnknownVersion_DisablesMutationsButListsWhenInstalled()
    {
        var adapter = new FakeAdapter { Status = new(true, true, new Version(99, 0), false, "Usb.UnknownVersion") };
        var service = new UsbDeviceService(adapter, new FakeBroker());
        Assert.Single(await service.ListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewAsync(UsbDeviceAction.Detach, new UsbBusId("1-2")));
    }
    [Fact]
    public async Task StoppedService_DisablesEveryMutation()
    {
        var adapter = new FakeAdapter { Status = new(true, false, new Version(4, 2), true, "Usb.ServiceStopped") };
        var service = new UsbDeviceService(adapter, new FakeBroker());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewAsync(UsbDeviceAction.Detach, new UsbBusId("1-2")));
    }
    [Theory]
    [InlineData(UsbDeviceAction.Bind, UsbDeviceAvailability.Shared, true, false)]
    [InlineData(UsbDeviceAction.Unbind, UsbDeviceAvailability.Attached, true, true)]
    [InlineData(UsbDeviceAction.Attach, UsbDeviceAvailability.Available, false, false)]
    [InlineData(UsbDeviceAction.Detach, UsbDeviceAvailability.Shared, true, false)]
    public async Task Preview_RejectsIllegalStateTransitions(UsbDeviceAction action, UsbDeviceAvailability availability, bool shared, bool attached)
    {
        var adapter = new FakeAdapter();
        adapter.Devices[0] = adapter.Devices[0] with { Availability = availability, IsShared = shared, IsAttached = attached };
        var service = new UsbDeviceService(adapter, new FakeBroker());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewAsync(action, new UsbBusId("1-2"), action == UsbDeviceAction.Attach ? "Ubuntu" : null));
    }
    [Fact]
    public async Task Execute_RevalidatesStateToClosePreviewToOperationRace()
    {
        var adapter = new FakeAdapter(); var broker = new FakeBroker(); var service = new UsbDeviceService(adapter, broker);
        var preview = await service.PreviewAsync(UsbDeviceAction.Attach, new UsbBusId("1-2"), "Ubuntu");
        adapter.Devices[0] = adapter.Devices[0] with { Availability = UsbDeviceAvailability.Attached, IsAttached = true };
        var result = await service.ExecuteAsync(preview);
        Assert.False(result.Succeeded); Assert.Equal("Usb.StateChanged", result.OutcomeCode); Assert.Equal(0, broker.Calls);
    }
    [Fact]
    public async Task Execute_ReportsBoundedProductPhasesAndStructuredFailure()
    {
        var adapter = new FakeAdapter();
        var service = new UsbDeviceService(adapter, new FakeBroker());
        var preview = await service.PreviewAsync(UsbDeviceAction.Attach, new UsbBusId("1-2"), "Ubuntu");
        adapter.Devices.Clear();
        var phases = new List<UsbOperationProgress>();

        var result = await service.ExecuteAsync(preview, new CapturingProgress(phases));

        Assert.Equal("DN-8008", result.Diagnostic?.Code);
        Assert.Contains(phases, item => item.PhaseCode == "Usb.Phase.Validating");
        Assert.Contains(phases, item => item.PhaseCode == "Usb.Phase.Revalidating");
    }
    [Fact]
    public async Task Execute_RejectsReusedBusIdWhenHardwareIdentityChanged()
    {
        var adapter = new FakeAdapter(); var service = new UsbDeviceService(adapter, new FakeBroker());
        var preview = await service.PreviewAsync(UsbDeviceAction.Attach, new UsbBusId("1-2"), "Ubuntu");
        adapter.Devices[0] = adapter.Devices[0] with { HardwareId = "9999:0001", Description = "Replacement device" };

        var result = await service.ExecuteAsync(preview);

        Assert.False(result.Succeeded);
        Assert.Equal("Usb.HardwareChanged", result.OutcomeCode);
    }
    [Theory]
    [InlineData("Arduino Uno", "Devices_GuidanceArduino")]
    [InlineData("Android Composite ADB", "Devices_GuidanceAndroid")]
    [InlineData("Smart Card Reader", "Devices_GuidanceSmartcard")]
    [InlineData("FTDI serial adapter", "Devices_GuidanceSerial")]
    public void Parser_ClassifiesGuidance(string description, string guidance)
    {
        var item = Assert.Single(UsbIpdAdapter.ParseTable($"1-2    2341:0043   {description}                                  Shared"));
        Assert.Equal(guidance, item.GuidanceCode);
    }
    [Fact]
    public void Parser_OnlyClassifiesExplicitStorageDescriptors()
    {
        var storage = Assert.Single(UsbIpdAdapter.ParseTable("1-2    1234:5678   USB Mass Storage Device  Shared"));
        var generic = Assert.Single(UsbIpdAdapter.ParseTable("1-3    1234:5678   Generic USB Device  Shared"));
        Assert.True(storage.IsStorageClass); Assert.Equal("Devices_GuidanceStorage", storage.GuidanceCode);
        Assert.False(generic.IsStorageClass); Assert.Null(generic.GuidanceCode);
    }
    [Theory]
    [InlineData(UsbAttachmentVerification.ToolUnavailable)]
    [InlineData(UsbAttachmentVerification.NotPresent)]
    public async Task Attach_PreservesAdvisoryVerificationGuidance(UsbAttachmentVerification outcome)
    {
        var adapter = new FakeAdapter { Verification = new(outcome, outcome.ToString()) };
        var service = new UsbDeviceService(adapter, new FakeBroker());
        var preview = await service.PreviewAsync(UsbDeviceAction.Attach, new UsbBusId("1-2"), "Ubuntu");
        var result = await service.ExecuteAsync(preview);
        Assert.True(result.Succeeded); Assert.Equal(outcome.ToString(), result.Guidance);
    }
    [Fact]
    public async Task SignedBroker_FailsClosedAndNeverStartsAnElevatedProcess()
    {
        var broker = new SignedUsbElevatedOperationBroker();
        var result = await broker.ExecuteAsync(new UsbElevatedOperationRequest(Guid.NewGuid(), UsbDeviceAction.Bind, new UsbBusId("1-2"), "2341:0043", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1), "test", "test"));
        Assert.False(result.Succeeded);
        Assert.Equal("Usb.AllowListRejected", result.OutcomeCode);
    }
    [Fact]
    public void ElevatedIssuer_RejectsReplayExpiryAndWrongCaller()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var issuer = new UsbElevatedRequestIssuer(clock);
        var preview = new UsbDeviceActionPreview(Guid.NewGuid(), UsbDeviceAction.Bind, new UsbBusId("1-2"), "2341:0043", null, true, true, [], [], clock.GetUtcNow().AddMinutes(1));
        var request = issuer.Issue(preview, "S-1-5-21-test");
        Assert.False(issuer.Consume(request, "S-1-5-21-other"));
        Assert.True(issuer.Consume(request, "S-1-5-21-test"));
        Assert.False(issuer.Consume(request, "S-1-5-21-test"));
        var expired = issuer.Issue(preview with { Token = Guid.NewGuid(), ExpiresAt = clock.GetUtcNow().AddSeconds(1) }, "S-1-5-21-test");
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(issuer.Consume(expired, "S-1-5-21-test"));
    }
    [Fact]
    public async Task SignedBroker_OnlyLaunchesOneIssuedBindRequestForItsCaller()
    {
        var issuer = new UsbElevatedRequestIssuer();
        var caller = new StaticCaller("S-1-5-21-test");
        var launcher = new RecordingLauncher();
        var broker = new SignedUsbElevatedOperationBroker(issuer, caller, launcher);
        var request = issuer.Issue(new UsbDeviceActionPreview(Guid.NewGuid(), UsbDeviceAction.Bind, new UsbBusId("1-2"), "2341:0043", null, true, true, [], [], DateTimeOffset.UtcNow.AddMinutes(1)), caller.GetCallerIdentity());
        Assert.True((await broker.ExecuteAsync(request)).Succeeded);
        Assert.False((await broker.ExecuteAsync(request)).Succeeded);
        Assert.Equal(1, launcher.Calls);
        Assert.Equal(UsbDeviceAction.Bind, launcher.Request!.Action);
    }
    [Fact]
    public async Task SignedBroker_UsesAuthenticatedLauncherWithoutPrematurelyConsumingGrant()
    {
        var issuer = new UsbElevatedRequestIssuer();
        var caller = new StaticCaller("S-1-5-21-test");
        var launcher = new AuthenticatedRecordingLauncher();
        var broker = new SignedUsbElevatedOperationBroker(issuer, caller, launcher);
        var request = issuer.Issue(new UsbDeviceActionPreview(Guid.NewGuid(), UsbDeviceAction.Bind, new UsbBusId("1-2"), "2341:0043", null, true, true, [], [], DateTimeOffset.UtcNow.AddMinutes(1)), caller.GetCallerIdentity());

        Assert.True((await broker.ExecuteAsync(request)).Succeeded);
        Assert.Equal(1, launcher.Calls);
        Assert.True(launcher.GrantWasCurrent);
        Assert.True(issuer.Consume(request, caller.GetCallerIdentity()));
    }
    [Theory]
    [InlineData("S-1-5-21-caller", "S-1-5-21-caller", false, true)]
    [InlineData("S-1-5-21-admin", "S-1-5-21-caller", true, false)]
    [InlineData("S-1-5-21-other", "S-1-5-21-caller", false, false)]
    public void PipePeerGrant_RequiresExactInitiatingSid(string peer, string caller, bool administrator, bool expected) =>
        Assert.Equal(expected, SignedUsbElevatedHelperLauncher.IsPeerSidAuthorizedForGrant(peer, caller, administrator));
    [Fact]
    public void PipeHello_RejectsHostileServerEvenWhenItKnowsTheLaunchNonce()
    {
        const string nonce = "A0B1C2D3E4F5A6B7C8D9E0F1A2B3C4D5";
        Assert.True(SignedUsbElevatedHelperLauncher.IsHelperHelloAuthorized(new UsbElevatedHelperHello(3, 1234, nonce), 1234, nonce));
        Assert.False(SignedUsbElevatedHelperLauncher.IsHelperHelloAuthorized(new UsbElevatedHelperHello(3, 9876, nonce), 1234, nonce));
        Assert.False(SignedUsbElevatedHelperLauncher.IsHelperHelloAuthorized(new UsbElevatedHelperHello(1, 1234, nonce), 1234, nonce));
    }
    [Theory]
    [InlineData(4, "{\"devices\":[{\"busId\":\"1-2\",\"hardwareId\":\"2341:0043\",\"description\":\"Arduino\",\"state\":\"Shared\"}]}")]
    [InlineData(5, "[{\"bus_id\":\"1-2\",\"vidPid\":\"2341:0043\",\"device\":\"Arduino\",\"status\":\"Shared\"}]")]
    public void JsonParser_SelectsApprovedVersionShape(int major, string json)
    {
        Assert.True(UsbIpdAdapter.TryParseJson(json, major, out var devices));
        Assert.Equal("1-2", Assert.Single(devices).BusId.Value);
    }
    [Theory]
    [InlineData("{\"devices\":[{\"busId\":\"1-2\",\"vidPid\":\"2341:0043\",\"description\":\"Arduino\",\"state\":\"Shared\"}]}")]
    [InlineData("{\"devices\":[{\"busId\":\"1-2\",\"hardwareId\":\"2341:0043\",\"device\":\"Arduino\",\"state\":\"Shared\"}]}")]
    [InlineData("{\"devices\":[{\"busId\":\"1-2\",\"hardwareId\":\"2341:0043\",\"description\":\"Arduino\",\"status\":\"Shared\"}]}")]
    public void JsonParser_V4RejectsV5OnlyFields(string json)
    {
        Assert.False(UsbIpdAdapter.TryParseJson(json, 4, out var devices));
        Assert.Empty(devices);
    }
    [Theory]
    [InlineData("{\"devices\":[{\"busId\":\"1-2\",\"vidPid\":\"2341:0043\",\"device\":\"Fixture\",\"status\":\"Shared\"}]}")]
    [InlineData("{\"devices\":[{\"bus_id\":\"1-2\",\"hardwareId\":\"2341:0043\",\"device\":\"Fixture\",\"status\":\"Shared\"}]}")]
    [InlineData("{\"devices\":[{\"bus_id\":\"1-2\",\"vidPid\":\"2341:0043\",\"description\":\"Fixture\",\"status\":\"Shared\"}]}")]
    public void JsonParser_V5RejectsV4AndMixedAliases(string json)
    {
        Assert.False(UsbIpdAdapter.TryParseJson(json, 5, out var devices));
        Assert.Empty(devices);
    }
    [Theory]
    [InlineData(4, "{\"devices\":[{\"busId\":\"1-2\",\"hardwareId\":\"2341:004\",\"description\":\"Fixture\",\"state\":\"Shared\"}]}")]
    [InlineData(5, "{\"devices\":[{\"bus_id\":\"1-2\",\"vidPid\":\"2341:0043;whoami\",\"device\":\"Fixture\",\"status\":\"Shared\"}]}")]
    public void JsonParser_RejectsMalformedHardwareIdForEveryApprovedMajor(int major, string json)
    {
        Assert.False(UsbIpdAdapter.TryParseJson(json, major, out var devices));
        Assert.Empty(devices);
    }
    [Fact]
    public void JsonParser_FailsClosedForOversizedIdentity()
    {
        var json = "{\"devices\":[{\"busId\":\"1-2\",\"hardwareId\":\"" + new string('A', 4097) + ":0043\",\"description\":\"Fixture\",\"state\":\"Shared\"}]}";
        Assert.False(UsbIpdAdapter.TryParseJson(json, 4, out var devices));
        Assert.Empty(devices);
    }
    [Theory]
    [InlineData("Attached elsewhere")]
    [InlineData("Shared-ish")]
    [InlineData("not shared")]
    public void Parser_RejectsStateSubstringsAndCaseAliases(string state)
    {
        Assert.Empty(UsbIpdAdapter.ParseTable($"1-2  2341:0043  Fixture  {state}"));
        Assert.False(UsbIpdAdapter.TryParseJson($"{{\"devices\":[{{\"busId\":\"1-2\",\"hardwareId\":\"2341:0043\",\"description\":\"Fixture\",\"state\":\"{state}\"}}]}}", 4, out var parsed));
        Assert.Empty(parsed);
    }
    [Theory]
    [InlineData("usbipd-win 4.2.0", "json-v4")]
    [InlineData("usbipd-win 5.1.0", "json-v5")]
    [InlineData("usbipd-win 6.0.0", "table-unsupported")]
    [InlineData("unknown", "table-unknown")]
    public void VersionProfiles_AreExplicit(string text, string profile) => Assert.Equal(profile, UsbIpdAdapter.SelectParserProfile(UsbIpdAdapter.ParseVersion(text)));
    [Fact]
    public async Task Adapter_UsesJsonOnlyForApprovedVersionsAndFallsBackToTable()
    {
        var runner = new RecordingRunner(
            new ProcessResult(0, "usbipd-win 4.2.0", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "STATE : 4 RUNNING", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(1, "", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "1-2  2341:0043  Arduino Uno  Shared", "", TimeSpan.Zero, false, false, false, 1));
        var adapter = new UsbIpdAdapter(runner);
        var status = await adapter.GetStatusAsync();
        Assert.Equal("json-v4", status.ParserProfile);
        Assert.Single(await adapter.ListAsync(status));
        Assert.Equal(["--version", "query", "list", "list"], runner.Requests.Select(x => x.Arguments[0]).ToArray());
    }
    [Fact]
    public async Task Adapter_V4JsonWithV5OnlyFieldsFallsBackToTable()
    {
        var runner = new RecordingRunner(
            new ProcessResult(0, "usbipd-win 4.2.0", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "STATE : 4 RUNNING", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "{\"devices\":[{\"busId\":\"1-2\",\"vidPid\":\"2341:0043\",\"device\":\"Arduino\",\"status\":\"Shared\"}]}", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "1-2  2341:0043  Arduino Uno  Shared", "", TimeSpan.Zero, false, false, false, 1));
        var adapter = new UsbIpdAdapter(runner);

        var devices = await adapter.ListAsync(await adapter.GetStatusAsync());

        Assert.Equal("Arduino Uno", Assert.Single(devices).Description);
        Assert.Equal(["--version", "query", "list", "list"], runner.Requests.Select(x => x.Arguments[0]).ToArray());
        Assert.Equal("--json", runner.Requests[2].Arguments[1]);
    }
    [Fact]
    public async Task Adapter_UnknownVersionSkipsJsonAndPreservesDiagnostic()
    {
        var runner = new RecordingRunner(
            new ProcessResult(0, "usbipd experimental", "", TimeSpan.Zero, false, false, false, 1),
            new ProcessResult(0, "STATE : 4 RUNNING", "", TimeSpan.Zero, false, false, false, 1));
        var adapter = new UsbIpdAdapter(runner);
        var status = await adapter.GetStatusAsync();
        Assert.False(status.SupportsMutation); Assert.True(status.IsServiceRunning); Assert.Equal("Usb.VersionMalformed", status.ReasonCode); Assert.Equal("table-unknown", status.ParserProfile); Assert.Equal("usbipd experimental", status.RawVersionDiagnostic);
    }
    [Fact]
    public void DeviceWatcher_IsIdempotentlyDisposableWhenWindowsNotificationsAreUnavailable()
    {
        using var watcher = new UsbDeviceChangeWatcher();
        watcher.Stop();
        watcher.Dispose();
        watcher.Dispose();
    }
    private sealed class FakeAdapter : IUsbIpdAdapter
    {
        public UsbIpdStatus Status { get; set; } = new(true, true, new Version(4, 2), true, "Usb.Available");
        public List<UsbDeviceInfo> Devices { get; } = [new(new UsbBusId("1-2"), "2341:0043", "Arduino Uno", UsbDeviceAvailability.Shared, true, false, false)];
        public Task<UsbIpdStatus> GetStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(Status);
        public Task<IReadOnlyList<UsbDeviceInfo>> ListAsync(UsbIpdStatus status, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UsbDeviceInfo>>(Devices.ToArray());
        public Task<UsbDeviceActionResult> ExecuteUnelevatedAsync(UsbDeviceAction action, UsbBusId busId, string? distributionName, CancellationToken cancellationToken = default) => Task.FromResult(new UsbDeviceActionResult(true, "Usb.Succeeded"));
        public UsbAttachmentVerificationResult Verification { get; set; } = new(UsbAttachmentVerification.Present, "verified");
        public Task<UsbAttachmentVerificationResult> VerifyAttachmentAsync(UsbDeviceInfo device, string distributionName, CancellationToken cancellationToken = default) => Task.FromResult(Verification);
    }
    private sealed class FakeBroker : IUsbElevatedOperationBroker
    {
        public int Calls { get; private set; }
        public Task<UsbDeviceActionResult> ExecuteAsync(UsbElevatedOperationRequest request, CancellationToken cancellationToken = default) { Calls++; return Task.FromResult(new UsbDeviceActionResult(true, "Usb.Succeeded")); }
    }
    private sealed class FakeTimeProvider(DateTimeOffset value) : TimeProvider
    {
        private DateTimeOffset _now = value;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }
    private sealed class StaticCaller(string value) : IUsbCallerIdentityProvider { public string GetCallerIdentity() => value; }
    private sealed class RecordingLauncher : IUsbElevatedHelperLauncher
    {
        public int Calls { get; private set; }
        public UsbElevatedOperationRequest? Request { get; private set; }
        public Task<UsbDeviceActionResult> LaunchAsync(UsbElevatedOperationRequest request, CancellationToken cancellationToken = default)
        { Calls++; Request = request; return Task.FromResult(new UsbDeviceActionResult(true, "Usb.Succeeded")); }
    }
    private sealed class AuthenticatedRecordingLauncher : IUsbElevatedAuthenticatedHelperLauncher
    {
        public int Calls { get; private set; }
        public bool GrantWasCurrent { get; private set; }
        public Task<UsbDeviceActionResult> LaunchAsync(UsbElevatedOperationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UsbDeviceActionResult> LaunchAuthorizedAsync(UsbElevatedOperationRequest request, IUsbElevatedRequestIssuer issuer, string callerIdentity, CancellationToken cancellationToken = default)
        {
            Calls++; GrantWasCurrent = issuer.IsCurrent(request, callerIdentity);
            return Task.FromResult(new UsbDeviceActionResult(true, "Usb.Succeeded"));
        }
    }
    private sealed class CapturingProgress(List<UsbOperationProgress> values) : IProgress<UsbOperationProgress>
    {
        public void Report(UsbOperationProgress value) => values.Add(value);
    }
    private sealed class RecordingRunner(params ProcessResult[] values) : IProcessRunner
    {
        private readonly Queue<ProcessResult> _values = new(values);
        public List<ProcessRequest> Requests { get; } = [];
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        { Requests.Add(request); return Task.FromResult(_values.Dequeue()); }
    }
}
