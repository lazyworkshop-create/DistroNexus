namespace DistroNexus.Core.Services;

/// <summary>Bounded local cache for already validated, decoded icon bytes. Remote URLs are never accepted.</summary>
public sealed class WslgIconCache
{
    private readonly int _maxEntries; private readonly int _maxBytes; private readonly Dictionary<string, byte[]> _entries = new(StringComparer.Ordinal); private readonly LinkedList<string> _lru = []; private int _bytes;
    public WslgIconCache(int maxEntries = 256, int maxBytes = 16 * 1024 * 1024) { _maxEntries=maxEntries; _maxBytes=maxBytes; }
    /// <param name="cacheKey">Opaque caller key (normally instance plus path).</param>
    /// <param name="sourcePath">Original Linux path that is independently root-validated.</param>
    public bool TryAdd(string cacheKey, string sourcePath, ReadOnlySpan<byte> bytes)
    {
        if (!DesktopEntryParser.IsApprovedIconPath(sourcePath) || bytes.Length == 0 || bytes.Length > 1024 * 1024 || !IsSafelyDecoded(bytes)) return false;
        if (string.IsNullOrWhiteSpace(cacheKey)) return false;
        var key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(cacheKey))).ToLowerInvariant();
        if (_entries.ContainsKey(key)) return true;
        while (_entries.Count >= _maxEntries || _bytes + bytes.Length > _maxBytes) { if(_lru.Last is null) return false; var old=_lru.Last.Value; _bytes-=_entries[old].Length; _entries.Remove(old); _lru.RemoveLast(); }
        _entries[key]=bytes.ToArray(); _lru.AddFirst(key); _bytes+=bytes.Length; return true;
    }
    public bool TryGet(string cacheKey, out byte[]? bytes) { var key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(cacheKey))).ToLowerInvariant(); if(!_entries.TryGetValue(key,out var value)){bytes=null;return false;} var node=_lru.Find(key); if(node is not null){_lru.Remove(node);_lru.AddFirst(node);} bytes=value.ToArray();return true; }
    private static bool IsSafelyDecoded(ReadOnlySpan<byte> bytes)
    {
        // Header checks limit allocation before GDI+ is asked to decode the complete image.
        if (!WslgApplicationService.IsDecodableImage(bytes.ToArray())) return false;
        try
        {
            using var source = new MemoryStream(bytes.ToArray(), writable: false);
            using var image = System.Drawing.Image.FromStream(source, useEmbeddedColorManagement: false, validateImageData: true);
            if (image.Width is <= 0 or > 4096 || image.Height is <= 0 or > 4096 || (long)image.Width * image.Height > 16_000_000) return false;
            // Constructing a bitmap forces decode of image data, including compressed IDAT/JPEG segments.
            using var decoded = new System.Drawing.Bitmap(image);
            return decoded.Width == image.Width && decoded.Height == image.Height;
        }
        catch (ArgumentException) { return false; }
        catch (OutOfMemoryException) { return false; }
    }
}
