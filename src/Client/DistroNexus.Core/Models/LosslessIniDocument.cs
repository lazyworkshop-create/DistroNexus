using System.Text;

namespace DistroNexus.Core.Models;

/// <summary>An INI token stream that retains every original character, line ending and UTF-8 BOM.</summary>
public sealed class LosslessIniDocument
{
    private readonly List<ConfigurationToken> _tokens;
    public IReadOnlyList<ConfigurationToken> Tokens => _tokens;
    public bool HasUtf8Bom { get; }

    private LosslessIniDocument(List<ConfigurationToken> tokens, bool bom) => (_tokens, HasUtf8Bom) = (tokens, bom);

    public static LosslessIniDocument Parse(ReadOnlySpan<byte> bytes)
    {
        var bom = bytes.StartsWith(Encoding.UTF8.Preamble);
        if (bom) bytes = bytes[Encoding.UTF8.Preamble.Length..];
        var text = new UTF8Encoding(false, true).GetString(bytes);
        var tokens = new List<ConfigurationToken>();
        var section = string.Empty;
        var pos = 0;
        var line = 1;
        while (pos < text.Length)
        {
            var end = text.IndexOfAny(['\r', '\n'], pos);
            string raw, ending;
            if (end < 0) { raw = text[pos..]; ending = string.Empty; pos = text.Length; }
            else
            {
                raw = text[pos..end];
                ending = text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? "\r\n" : text[end].ToString();
                pos = end + ending.Length;
            }
            var token = ParseLine(raw, ending, line++, section);
            tokens.Add(token);
            if (token.Kind == ConfigurationTokenKind.Section) section = token.Section!;
        }
        if (text.Length == 0) return new(tokens, bom);
        return new(tokens, bom);
    }

    public static LosslessIniDocument Empty() => new([], false);

    private static ConfigurationToken ParseLine(string raw, string ending, int line, string section)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return new(ConfigurationTokenKind.Blank, line, raw, ending, section);
        if (trimmed[0] is '#' or ';') return new(ConfigurationTokenKind.Comment, line, raw, ending, section);
        if (trimmed[0] == '[' && trimmed[^1] == ']' && trimmed.Length > 2)
            return new(ConfigurationTokenKind.Section, line, raw, ending, trimmed[1..^1].Trim());
        var equals = raw.IndexOf('=');
        if (equals < 0) return new(ConfigurationTokenKind.Malformed, line, raw, ending, section);
        var key = raw[..equals].Trim();
        if (key.Length == 0) return new(ConfigurationTokenKind.Malformed, line, raw, ending, section);
        var start = equals + 1;
        while (start < raw.Length && char.IsWhiteSpace(raw[start])) start++;
        var valueEnd = raw.Length;
        // Preserve whitespace before an inline comment and the comment itself.
        var supportsInlineComment = !((section.Equals("wsl2", StringComparison.OrdinalIgnoreCase) &&
            key.Equals("kernelCommandLine", StringComparison.OrdinalIgnoreCase)) ||
            (section.Equals("boot", StringComparison.OrdinalIgnoreCase) && key.Equals("command", StringComparison.OrdinalIgnoreCase)));
        if (supportsInlineComment)
            for (var i = start; i < raw.Length; i++)
                if (raw[i] is '#' or ';' && i > start && char.IsWhiteSpace(raw[i - 1])) { valueEnd = i; break; }
        while (valueEnd > start && char.IsWhiteSpace(raw[valueEnd - 1])) valueEnd--;
        return new(ConfigurationTokenKind.KeyValue, line, raw, ending, section, key,
            raw[start..valueEnd], start, valueEnd - start);
    }

    public string? GetLastValue(string section, string key) => _tokens.LastOrDefault(t =>
        t.Kind == ConfigurationTokenKind.KeyValue &&
        string.Equals(t.Section, section, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;

    public LosslessIniDocument WithValue(string section, string key, string value)
    {
        var copy = new List<ConfigurationToken>(_tokens);
        var index = copy.FindLastIndex(t => t.Kind == ConfigurationTokenKind.KeyValue &&
            string.Equals(t.Section, section, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            var old = copy[index];
            var raw = old.Raw.Remove(old.ValueStart, old.ValueLength).Insert(old.ValueStart, value);
            copy[index] = ParseLine(raw, old.LineEnding, old.Line, old.Section ?? string.Empty);
            return new(copy, HasUtf8Bom);
        }
        var sectionIndex = copy.FindIndex(t => t.Kind == ConfigurationTokenKind.Section &&
            string.Equals(t.Section, section, StringComparison.OrdinalIgnoreCase));
        var defaultEnding = copy.Select(t => t.LineEnding).FirstOrDefault(e => e.Length > 0) ?? Environment.NewLine;
        if (sectionIndex < 0)
        {
            if (copy.Count > 0 && copy[^1].LineEnding.Length == 0)
                copy[^1] = copy[^1] with { LineEnding = defaultEnding };
            copy.Add(ParseLine($"[{section}]", defaultEnding, copy.Count + 1, string.Empty));
            sectionIndex = copy.Count - 1;
        }
        var insert = sectionIndex + 1;
        while (insert < copy.Count && copy[insert].Kind != ConfigurationTokenKind.Section) insert++;
        // An appended record must not run into a final record that deliberately had no
        // terminator.  Give that existing record the document's established convention;
        // the newly appended token remains unterminated, preserving the original EOF style.
        if (insert == copy.Count && copy.Count > 0 && copy[^1].LineEnding.Length == 0)
            copy[^1] = copy[^1] with { LineEnding = defaultEnding };
        copy.Insert(insert, ParseLine($"{key}={value}", insert == copy.Count ? string.Empty : defaultEnding, insert + 1, section));
        for (var i = insert + 1; i < copy.Count; i++) copy[i] = copy[i] with { Line = i + 1 };
        return new(copy, HasUtf8Bom);
    }

    public LosslessIniDocument WithoutValue(string section, string key)
    {
        var copy = _tokens.Where(t => !(t.Kind == ConfigurationTokenKind.KeyValue &&
            string.Equals(t.Section, section, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase))).ToList();
        for (var i = 0; i < copy.Count; i++) copy[i] = copy[i] with { Line = i + 1 };
        return new(copy, HasUtf8Bom);
    }

    public byte[] ToBytes()
    {
        var body = Encoding.UTF8.GetBytes(string.Concat(_tokens.Select(t => t.Raw + t.LineEnding)));
        if (!HasUtf8Bom) return body;
        var bytes = new byte[Encoding.UTF8.Preamble.Length + body.Length];
        Encoding.UTF8.Preamble.CopyTo(bytes.AsSpan()); body.CopyTo(bytes.AsSpan(Encoding.UTF8.Preamble.Length));
        return bytes;
    }

    public override string ToString() => Encoding.UTF8.GetString(ToBytes().AsSpan(HasUtf8Bom ? 3 : 0));
}
