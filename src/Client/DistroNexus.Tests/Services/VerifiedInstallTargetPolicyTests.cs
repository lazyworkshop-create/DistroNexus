using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using DistroNexus.Core.Interfaces;
using DistroNexus.Core.Models;
using DistroNexus.Core.Services;
using Moq;

namespace DistroNexus.Tests.Services;

public sealed class VerifiedInstallTargetPolicyTests
{
    [Fact]
    public async Task PreviewTarget_RejectsProtectedAndRootOnlyPathsWithoutIssuingAToken()
    {
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus.TargetPolicy", Guid.NewGuid().ToString("N"));
        var service = new VerifiedInstallService(Mock.Of<ICatalogService>(), Mock.Of<IProcessRunner>(), (_, _) => Task.FromResult(false), root);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewTargetAsync(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewTargetAsync(Path.GetPathRoot(Environment.CurrentDirectory)!));
    }

    [Fact]
    public async Task PreviewTarget_RejectsNonWritableParentWithoutIssuingAnEligibleToken()
    {
        var root = CreateRoot();
        var parent = Path.Combine(root, "read-only");
        Directory.CreateDirectory(parent);
        var original = File.GetAttributes(parent);
        File.SetAttributes(parent, original | FileAttributes.ReadOnly);
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(root).PreviewTargetAsync(Path.Combine(parent, "Distro")));

            Assert.Equal("Install.TargetUnavailable", error.Message);
        }
        finally
        {
            File.SetAttributes(parent, original);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task PreviewTarget_RejectsAncestorReparsePointWithoutIssuingAnEligibleToken()
    {
        var root = CreateRoot();
        var junction = Path.Combine(root, "junction");
        var target = Path.Combine(junction, "target");
        var backing = Path.Combine(root, "backing");
        Directory.CreateDirectory(backing);
        try
        {
            await CreateJunctionAsync(junction, backing);
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(root).PreviewTargetAsync(target));

            Assert.Equal("Install.TargetUnavailable", error.Message);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task TargetPreviewGrant_RevalidationRejectsEffectiveWriteRevocation()
    {
        var root = CreateRoot();
        var directory = new DirectoryInfo(root);
        using var identity = WindowsIdentity.GetCurrent();
        var deniedSid = identity.User!;
        try
        {
            var target = Path.Combine(root, "Distro");
            var bytes = Encoding.UTF8.GetBytes("verified package");
            var server = await StartPackageServerAsync(bytes);
            var cache = Path.Combine(root, "cache");
            var package = new DistroPackage { Id = "ubuntu", DownloadUrl = server.Url, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)), FileSize = bytes.Length };
            var catalog = new Mock<ICatalogService>(MockBehavior.Strict);
            catalog.Setup(x => x.GetDistributionByIdAsync("ubuntu", It.IsAny<CancellationToken>())).ReturnsAsync(package);
            catalog.Setup(x => x.GetPackageCachePath()).Returns(cache);
            var service = new VerifiedInstallService(catalog.Object, Mock.Of<IProcessRunner>(), (_, _) => Task.FromResult(false), Path.Combine(root, "grants"));
            var preview = await service.PreviewTargetAsync(target);
            Assert.True(preview.IsEligible);
            Assert.NotEmpty(preview.PreviewToken);
            var acquire = await service.AcquireAsync((await service.PreviewAcquireAsync("ubuntu")).PreviewToken);
            await server.Completion;

            directory.SetAccessControl(AddWriteDeny(directory.GetAccessControl(AccessControlSections.Access), deniedSid));
            Assert.Contains(directory.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)).OfType<FileSystemAccessRule>(), rule =>
                rule.AccessControlType == AccessControlType.Deny && rule.IdentityReference.Value == deniedSid.Value);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewInstallAsync(acquire.PackageReference, "Ubuntu", preview.PreviewToken, "root", "bash", null, false, null));

            Assert.Equal("Install.TargetStateChanged", exception.Message);
        }
        finally
        {
            RemoveWriteDeny(directory, deniedSid);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task PreviewTarget_RejectsInheritedEffectiveWriteDeny()
    {
        var root = CreateRoot();
        var parent = new DirectoryInfo(Path.Combine(root, "parent"));
        var child = Path.Combine(parent.FullName, "child");
        parent.Create();
        Directory.CreateDirectory(child);
        using var identity = WindowsIdentity.GetCurrent();
        var deniedSid = identity.User!;
        try
        {
            parent.SetAccessControl(AddWriteDeny(parent.GetAccessControl(AccessControlSections.Access), deniedSid, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit));
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(root).PreviewTargetAsync(Path.Combine(child, "Distro")));

            Assert.Equal("Install.TargetUnavailable", error.Message);
        }
        finally
        {
            RemoveWriteDeny(parent, deniedSid, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit);
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EffectiveAccess_DenyOnlyGroupParticipatesOnlyInDenyEvaluation()
    {
        var allowSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        var denyOnlySid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var rules = new[]
        {
            new FileSystemAccessRule(allowSid, FileSystemRights.FullControl, AccessControlType.Allow),
            new FileSystemAccessRule(denyOnlySid, FileSystemRights.FullControl, AccessControlType.Deny),
        };
        var allow = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { allowSid.Value };
        var deny = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { allowSid.Value, denyOnlySid.Value };
        var result = (bool)typeof(VerifiedInstallService).GetMethod("HasRequiredDirectoryAccess", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [rules, allow, deny])!;

        Assert.False(result);
    }

    private static VerifiedInstallService Service(string root) => new(Mock.Of<ICatalogService>(), Mock.Of<IProcessRunner>(), (_, _) => Task.FromResult(false), root);
    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "DistroNexus.TargetPolicy", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static async Task CreateJunctionAsync(string target, string backing)
    {
        var start = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("mklink");
        start.ArgumentList.Add("/J");
        start.ArgumentList.Add(target);
        start.ArgumentList.Add(backing);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to create test junction.");
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }

    private static DirectorySecurity AddWriteDeny(DirectorySecurity security, SecurityIdentifier sid, InheritanceFlags inheritance = InheritanceFlags.None)
    {
        security.AddAccessRule(new FileSystemAccessRule(sid,
            FileSystemRights.FullControl,
            inheritance, PropagationFlags.None, AccessControlType.Deny));
        return security;
    }

    private static void RemoveWriteDeny(DirectoryInfo directory, SecurityIdentifier sid, InheritanceFlags inheritance = InheritanceFlags.None)
    {
        var security = directory.GetAccessControl(AccessControlSections.Access);
        security.RemoveAccessRuleSpecific(new FileSystemAccessRule(sid, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Deny));
        directory.SetAccessControl(security);
    }

    private static async Task<(string Url, Task Completion)> StartPackageServerAsync(byte[] body)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var completion = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var request = new byte[4096]; _ = await stream.ReadAsync(request);
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header); await stream.WriteAsync(body); listener.Stop();
        });
        return ($"http://127.0.0.1:{port}/ubuntu.tar", completion);
    }
}
