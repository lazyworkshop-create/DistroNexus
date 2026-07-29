using System.Diagnostics;
using System.Security.AccessControl;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DistroNexus.Core.Services;

/// <summary>Resolves only a signed usbipd-win installation below Program Files. PATH is never a trust source.</summary>
public static class TrustedUsbIpdExecutable
{
    private const string ProductName = "usbipd-win";
    // The release pipeline pins this exact vendor identity alongside the helper signer.
    // A non-empty value is mandatory before any elevated executable is accepted.
    private static readonly string ExpectedPublisherThumbprint = typeof(TrustedUsbIpdExecutable).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(x => x.Key == "DistroNexus.UsbIpdPublisherThumbprint")?.Value ?? string.Empty;

    public static string? Resolve()
    {
        foreach (var root in InstallationRoots())
        {
            var candidate = Path.Combine(root, "usbipd-win", "usbipd.exe");
            if (IsTrustedForLaunch(candidate, root)) return candidate;
        }
        return null;
    }

    internal static IEnumerable<string> InstallationRoots()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        };
        return roots.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Validates the exact installed executable before an elevated helper may launch it.</summary>
    public static bool IsTrustedForLaunch(string candidate, string installationRoot)
    {
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installationRoot));
            var path = Path.GetFullPath(candidate);
            if (!Path.IsPathFullyQualified(path) || !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFileName(path), "usbipd.exe", StringComparison.OrdinalIgnoreCase) ||
                !HasNoReparsePointInExistingPath(root, path) || !File.Exists(path)) return false;

            if (string.IsNullOrWhiteSpace(ExpectedPublisherThumbprint) ||
                !AuthenticodeTrust.IsTrustedProduct(path, ProductName, ExpectedPublisherThumbprint)) return false;

            var owner = new FileInfo(path).GetAccessControl().GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
            if (owner is null || !IsMachineOwned(owner)) return false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or CryptographicException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>Rejects a candidate when any existing component from the approved root to the file is a reparse point.</summary>
    internal static bool HasNoReparsePointInExistingPath(string installationRoot, string candidate) =>
        HasNoReparsePointInExistingPath(installationRoot, candidate, GetExistingPathAttributes);

    internal static bool HasNoReparsePointInExistingPath(string installationRoot, string candidate, Func<string, FileAttributes?> getExistingPathAttributes)
    {
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installationRoot));
            var path = Path.GetFullPath(candidate);
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;

            if (IsReparsePoint(getExistingPathAttributes(root))) return false;
            var relativePath = Path.GetRelativePath(root, path);
            var current = root;
            foreach (var segment in relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if (IsReparsePoint(getExistingPathAttributes(current))) return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static FileAttributes? GetExistingPathAttributes(string path) =>
        File.Exists(path) || Directory.Exists(path) ? File.GetAttributes(path) : null;

    private static bool IsReparsePoint(FileAttributes? attributes) =>
        attributes.HasValue && attributes.Value.HasFlag(FileAttributes.ReparsePoint);

    private static bool IsMachineOwned(SecurityIdentifier owner) =>
        owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ||
        owner.IsWellKnown(WellKnownSidType.LocalSystemSid) ||
        owner.Value.Equals("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464", StringComparison.Ordinal); // TrustedInstaller
}

/// <summary>Small fail-closed Authenticode verifier shared by fixed elevated executable boundaries.</summary>
public static class AuthenticodeTrust
{
    private static readonly Guid WinTrustActionGenericVerifyV2 = new("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");

    public static bool IsTrustedProduct(string path, string expectedProductName, string expectedPublisherThumbprint)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(expectedProductName) || string.IsNullOrWhiteSpace(expectedPublisherThumbprint)) return false;
            var info = FileVersionInfo.GetVersionInfo(path);
            if (!string.Equals(info.ProductName, expectedProductName, StringComparison.Ordinal)) return false;
#pragma warning disable SYSLIB0057
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            if (!string.Equals(certificate.Thumbprint, expectedPublisherThumbprint, StringComparison.OrdinalIgnoreCase)) return false;
            return VerifyAuthenticode(path) == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or CryptographicException or PlatformNotSupportedException or DllNotFoundException)
        {
            return false;
        }
    }

    private static int VerifyAuthenticode(string path)
    {
        var file = new WinTrustFileInfo(path);
        var action = WinTrustActionGenericVerifyV2;
        var filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
        try
        {
            Marshal.StructureToPtr(file, filePointer, false);
            var data = new WinTrustData(filePointer);
            return WinVerifyTrust(IntPtr.Zero, ref action, ref data);
        }
        finally { Marshal.FreeHGlobal(filePointer); }
    }

    [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int WinVerifyTrust(IntPtr hwnd, [In] ref Guid pgActionID, ref WinTrustData pWVTData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint cbStruct;
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
        public WinTrustFileInfo(string path) { cbStruct = (uint)Marshal.SizeOf<WinTrustFileInfo>(); pcwszFilePath = path; hFile = IntPtr.Zero; pgKnownSubject = IntPtr.Zero; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
        public WinTrustData(IntPtr file) { cbStruct = (uint)Marshal.SizeOf<WinTrustData>(); pPolicyCallbackData = IntPtr.Zero; pSIPClientData = IntPtr.Zero; dwUIChoice = 2; fdwRevocationChecks = 0; dwUnionChoice = 1; pFile = file; dwStateAction = 0; hWVTStateData = IntPtr.Zero; pwszURLReference = IntPtr.Zero; dwProvFlags = 0x00001000; dwUIContext = 0; pSignatureSettings = IntPtr.Zero; }
    }
}
