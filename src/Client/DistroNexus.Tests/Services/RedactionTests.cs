using DistroNexus.Core.Services;

namespace DistroNexus.Tests.Services;

public class RedactionTests
{
    [Fact]
    public void Redact_RemovesCredentialsPrivateKeysAndUserNames()
    {
        var input = "token=abc password:xyz C:\\Users\\alice\\file -----BEGIN PRIVATE KEY-----secret-----END PRIVATE KEY-----";
        var result = SensitiveDataRedactor.Redact(input);
        Assert.DoesNotContain("abc", result);
        Assert.DoesNotContain("xyz", result);
        Assert.DoesNotContain("alice", result);
        Assert.DoesNotContain("secret", result);
    }
}
