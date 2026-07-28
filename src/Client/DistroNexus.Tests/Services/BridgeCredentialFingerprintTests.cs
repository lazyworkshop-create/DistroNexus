namespace DistroNexus.Tests.Services;

public sealed class BridgeCredentialFingerprintTests
{
    [Fact]
    public void Fingerprint_ChangesWhenRegisteredIdentityOrStateChanges()
    {
        var baseline = Details("Stopped", "{a}");
        var stateChanged = Details("Running", "{a}");
        var identityChanged = Details("Stopped", "{b}");

        Assert.NotEqual(Fingerprint(baseline), Fingerprint(stateChanged));
        Assert.NotEqual(Fingerprint(baseline), Fingerprint(identityChanged));
    }

    [Fact]
    public void Fingerprint_MalformedRegistrationFailsClosed()
    {
        var malformed = Details("Stopped", "", "", null);
        var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => Fingerprint(malformed));
        Assert.Equal("Lifecycle.CredentialStateChanged", error.InnerException!.Message);
    }
    private static object Details(string state,string guid,string path=@"C:\WSL\Ubuntu",DateTime? time = null) => Activator.CreateInstance(Type.GetType("DistroNexus.WorkspaceBridge.BridgeInstanceDetails, DistroNexus.WorkspaceBridge")!, "Ubuntu",state,2,path,0L,time ?? new DateTime(2025,1,1,0,0,0,DateTimeKind.Utc),"Ubuntu",guid,null,null)!;
    private static string Fingerprint(object value) => (string)Type.GetType("DistroNexus.WorkspaceBridge.BridgeWslManagerService, DistroNexus.WorkspaceBridge")!.GetMethod("Fingerprint",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.Invoke(null,[value])!;
}
