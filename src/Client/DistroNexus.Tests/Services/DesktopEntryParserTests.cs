using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public sealed class DesktopEntryParserTests
{
    private const string Path = "/usr/share/applications/example.desktop";
    [Fact]
    public void ValidEntry_ProducesArgumentListWithoutExecutingAnything()
    {
        var app = DesktopEntryParser.Parse("Ubuntu", Path, "[Desktop Entry]\nType=Application\nName=Example\nExec=/usr/bin/example --profile \"two words\"\nCategories=Development;IDE;\nIcon=/usr/share/icons/example.png\n");
        Assert.NotNull(app); Assert.Equal("/usr/bin/example", app.Executable); Assert.Equal(["--profile", "two words"], app.Arguments); Assert.Equal(["Development", "IDE"], app.Categories);
    }
    [Theory]
    [InlineData("Exec=/usr/bin/example %z")]
    [InlineData("Exec=/mnt/c/Windows/System32/cmd.exe /c whoami")]
    [InlineData("Exec=/usr/bin/example\u0000bad")]
    [InlineData("Exec=/usr/bin/example\nTerminal=true")]
    public void UnsafeOrUnsupportedEntries_AreRejected(string fields)
    {
        var app = DesktopEntryParser.Parse("Ubuntu", Path, "[Desktop Entry]\nType=Application\nName=Example\n" + fields + "\n");
        Assert.Null(app);
    }
    [Fact]
    public void RemoteAndEscapedIcons_AreNotReturned()
    {
        var remote=DesktopEntryParser.Parse("Ubuntu",Path,"[Desktop Entry]\nType=Application\nName=X\nExec=/usr/bin/x\nIcon=https://host/icon.png\n");
        var escaped=DesktopEntryParser.Parse("Ubuntu",Path,"[Desktop Entry]\nType=Application\nName=X\nExec=/usr/bin/x\nIcon=/tmp/icon.png\n");
        Assert.Null(remote!.IconPath); Assert.Null(escaped!.IconPath);
    }
    [Fact]
    public void SupportedFieldCodes_ExpandWithoutShellSyntax()
    {
        var app=DesktopEntryParser.Parse("Ubuntu",Path,"[Desktop Entry]\nType=Application\nName=Example\nExec=/usr/bin/example --name=%c --entry=%k %% %f\n");
        Assert.NotNull(app); Assert.Contains("--name=Example",app.Arguments); Assert.Contains("--entry=" + Path,app.Arguments); Assert.Contains("%",app.Arguments); Assert.DoesNotContain(app.Arguments,x=>x.Contains("%f",StringComparison.Ordinal));
    }
    [Fact]
    public void CanonicalRootChecks_RejectTraversalAndBackslashPaths()
    {
        Assert.False(DesktopEntryParser.IsApprovedDesktopPath("/usr/share/applications/../evil.desktop"));
        Assert.False(DesktopEntryParser.IsApprovedDesktopPath("/usr/share/applications\\evil.desktop"));
        Assert.False(DesktopEntryParser.IsApprovedIconPath("/usr/share/icons/../tmp/icon.png"));
    }
    [Fact]
    public void IconCache_IsBoundedAndRejectsUnapprovedSources()
    {
        var cache=new WslgIconCache(2,16); Assert.False(cache.TryAdd("ubuntu:/tmp/a.png","/tmp/a.png",[1])); Assert.False(cache.TryAdd("ubuntu:/usr/share/icons/a.png","/usr/share/icons/a.png",new byte[8]));
    }
    [Fact]
    public void IconCache_RejectsHeaderOnlyAndMalformedImages()
    {
        var cache=new WslgIconCache();
        byte[] header=[137,80,78,71,13,10,26,10,0,0,0,13,(byte)'I',(byte)'H',(byte)'D',(byte)'R',0,0,0,1,0,0,0,1,8,6,0,0,0];
        byte[] malformedJpeg=[0xff,0xd8,0xff,0xc0,0,8,8,0,1,0,1];
        Assert.False(cache.TryAdd("a","/usr/share/icons/a.png",header)); Assert.False(cache.TryAdd("b","/usr/share/icons/b.jpg",malformedJpeg));
    }
    [Fact]
    public void UnsafeIcon_NeverFlowsIntoPercentIArguments()
    {
        var remote=DesktopEntryParser.Parse("Ubuntu",Path,"[Desktop Entry]\nType=Application\nName=X\nExec=/usr/bin/x %i\nIcon=https://host/icon.png\n");
        var escaped=DesktopEntryParser.Parse("Ubuntu",Path,"[Desktop Entry]\nType=Application\nName=X\nExec=/usr/bin/x %i\nIcon=/tmp/icon.png\n");
        Assert.NotNull(remote); Assert.NotNull(escaped); Assert.DoesNotContain(remote.Arguments,x=>x.Contains("http",StringComparison.Ordinal)); Assert.DoesNotContain(escaped.Arguments,x=>x.Contains("/tmp",StringComparison.Ordinal)); Assert.Empty(remote.Arguments); Assert.Empty(escaped.Arguments);
    }
}
