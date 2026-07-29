using System.Text.RegularExpressions;
using DistroNexus.Core.Exceptions;

namespace DistroNexus.Tests.Exceptions;

public sealed class ErrorCodeDocumentationTests
{
    [Fact]
    public void ErrorCodeReference_ContainsEveryDefinedEnumValueWithItsStableNumber()
    {
        var root = FindRepositoryRoot();
        var reference = File.ReadAllText(Path.Combine(root, "docs", "development", "error-codes.md"));

        var missing = Enum.GetValues<DistroNexusErrorCode>()
            .Where(code => !Regex.IsMatch(reference,
                $@"\|\s*{(int)code}\s*\|\s*`{Regex.Escape(code.ToString())}`\s*\|",
                RegexOptions.CultureInvariant))
            .Select(code => $"{code}={(int)code}")
            .ToArray();

        Assert.True(missing.Length == 0,
            "docs/development/error-codes.md is missing enum entries: " + string.Join(", ", missing));
    }

    private static string FindRepositoryRoot()
    {
        var path = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(path, "AGENTS.md")))
            path = Directory.GetParent(path)?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
        return path;
    }
}
