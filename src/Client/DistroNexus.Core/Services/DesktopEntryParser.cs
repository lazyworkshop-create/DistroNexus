using System.Text;
using DistroNexus.Core.Models;

namespace DistroNexus.Core.Services;

/// <summary>Strict, non-executing subset of the Desktop Entry specification used by WSLg.</summary>
public static class DesktopEntryParser
{
    public static WslgApplication? Parse(string instanceName, string path, string content)
    {
        if (string.IsNullOrWhiteSpace(instanceName) || !IsApprovedDesktopPath(path) || content.Length > 64 * 1024 || content.IndexOfAny(['\0', '\r']) >= 0) return null;
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var group = false;
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is '#' or ';') continue;
            if (line[0] == '[') { group = line == "[Desktop Entry]"; continue; }
            if (!group) continue;
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            fields[line[..separator]] = line[(separator + 1)..];
        }
        if (!fields.TryGetValue("Type", out var type) || type != "Application" ||
            !fields.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name) ||
            !fields.TryGetValue("Exec", out var exec) || IsTrue(fields, "Hidden") || IsTrue(fields, "NoDisplay") || IsTrue(fields, "Terminal")) return null;
        var icon = NormalizeIcon(fields.GetValueOrDefault("Icon"));
        var args = TokenizeExec(exec, name, path, icon);
        if (args is null || args.Count == 0 || !IsSafeExecutable(args[0])) return null;
        var categories = fields.GetValueOrDefault("Categories")?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        var id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(instanceName + "\n" + path))).ToLowerInvariant();
        return new WslgApplication(id, instanceName, name.Trim(), args[0], args.Skip(1).ToArray(), categories, path, icon);
    }
    private static string? NormalizeIcon(string? icon) => string.IsNullOrWhiteSpace(icon) || icon.Contains("://", StringComparison.Ordinal) || !IsApprovedIconPath(icon) ? null : icon;
    private static bool IsTrue(IReadOnlyDictionary<string,string> fields, string key) => fields.TryGetValue(key, out var value) && bool.TryParse(value, out var result) && result;
    public static bool IsApprovedDesktopPath(string path) => IsCanonicalLinuxPath(path) && (path.StartsWith("/usr/share/applications/", StringComparison.Ordinal) || path.StartsWith("/usr/local/share/applications/", StringComparison.Ordinal) || path.StartsWith("/home/", StringComparison.Ordinal) && path.Contains("/.local/share/applications/", StringComparison.Ordinal));
    public static bool IsApprovedIconPath(string path) => IsCanonicalLinuxPath(path) && (path.StartsWith("/usr/share/icons/", StringComparison.Ordinal) || path.StartsWith("/usr/share/pixmaps/", StringComparison.Ordinal) || path.StartsWith("/home/", StringComparison.Ordinal) && path.Contains("/.local/share/icons/", StringComparison.Ordinal));
    public static bool IsCanonicalLinuxPath(string path) => !string.IsNullOrWhiteSpace(path) && path.IndexOfAny(['\0','\n','\r']) < 0 && path.StartsWith("/", StringComparison.Ordinal) && !path.Contains('\\') && !path.Contains("//", StringComparison.Ordinal) && !path.Split('/').Any(x => x is "." or "..");
    public static bool IsSafeExecutable(string value)
    {
        if (!IsCanonicalLinuxPath(value) || value.StartsWith("/mnt/", StringComparison.OrdinalIgnoreCase) || value.IndexOfAny(['\0','\n','\r']) >= 0) return false;
        var name=value[(value.LastIndexOf('/')+1)..];
        return !name.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".bat",StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".cmd",StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".com",StringComparison.OrdinalIgnoreCase) && name is not "cmd" and not "powershell" and not "pwsh" and not "explorer" and not "wsl" and not "rundll32";
    }
    internal static List<string>? TokenizeExec(string value, string? appName = null, string? desktopPath = null, string? icon = null)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(['\0','\n','\r']) >= 0) return null;
        var output = new List<string>(); var current = new StringBuilder(); var quoted = false; var escape = false;
        foreach (var c in value)
        {
            if (escape) { current.Append(c); escape = false; continue; }
            if (c == '\\') { escape = true; continue; }
            if (c == '"') { quoted = !quoted; continue; }
            if (char.IsWhiteSpace(c) && !quoted) { if (current.Length > 0) { output.Add(current.ToString()); current.Clear(); } continue; }
            if (c == '%') { current.Append(c); continue; }
            current.Append(c);
        }
        if (quoted) return null;
        if (escape) return null;
        if (current.Length > 0) output.Add(current.ToString());
        // field-code expansion happens after lexical tokenization to retain argument boundaries.
        for (var i=0;i<output.Count;i++)
        {
            var token=output[i];
            if (!token.Contains('%')) continue;
            if (token.Contains("%i", StringComparison.Ordinal)) { if(token != "%i") return null; if(string.IsNullOrWhiteSpace(icon)) { output.RemoveAt(i--); continue; } output[i]="--icon"; output.Insert(++i,icon); continue; }
            var expanded=new StringBuilder();
            for(var p=0;p<token.Length;p++) { if(token[p]!='%'){expanded.Append(token[p]);continue;} if(++p==token.Length)return null; switch(token[p]) { case '%': expanded.Append('%'); break; case 'c': expanded.Append(appName ?? string.Empty); break; case 'k': expanded.Append(desktopPath ?? string.Empty); break; case 'f': case 'F': case 'u': case 'U': break; default:return null; } }
            output[i]=expanded.ToString();
        }
        output.RemoveAll(string.IsNullOrEmpty);
        return output.Any(x => x.Length > 4096) ? null : output;
    }
}
