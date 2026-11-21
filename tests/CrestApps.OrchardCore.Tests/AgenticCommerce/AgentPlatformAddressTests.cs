using CrestApps.OrchardCore.AgenticCommerce.Core.Services;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// G14 (fix verification): the parts of a profile address that are indexed. A host no DNS name can have (a Unicode host
/// whose punycode form is too long, which <see cref="Uri.IdnHost"/> refuses with an exception, or an ASCII host over 253
/// characters, which the 255-character column refuses on SQL Server and PostgreSQL) has no host or origin, rather than
/// failing every save and every re-index of the platform.
/// </summary>
public sealed class AgentPlatformAddressTests
{
    public static TheoryData<string> UnindexableAddresses => new()
    {
        "https://" + string.Join('.', Enumerable.Repeat(new string('ü', 50), 5)) + ".example/.well-known/ucp",
        "https://" + string.Join('.', Enumerable.Repeat(new string('a', 60), 5)) + ".example/.well-known/ucp",
    };

    [Theory]
    [MemberData(nameof(UnindexableAddresses))]
    public void AHostNoDnsNameCanHave_HasNoHostOrOrigin(string url)
    {
        Assert.Null(AgentPlatformAddress.GetHost(url));
        Assert.Null(AgentPlatformAddress.GetOrigin(url));
        Assert.Null(AgentPlatformAddress.GetOriginKey(url));
    }

    [Fact]
    public void AHostOfTheLongestDnsName_IsKept()
    {
        // Three labels of 63 characters and one of 61, with the dots between them: 253 characters.
        var host = string.Join('.', Enumerable.Repeat(new string('a', 63), 3)) + "." + new string('b', 61);

        Assert.Equal(253, host.Length);
        Assert.Equal(host, AgentPlatformAddress.GetHost($"https://{host.ToUpperInvariant()}./p"));
        Assert.Equal($"https://{host}:443", AgentPlatformAddress.GetOrigin($"https://{host}/p"));
    }

    [Fact]
    public void AUnicodeHost_IsKeptInPunycode()
    {
        Assert.Equal("xn--bcher-kva.example", AgentPlatformAddress.GetHost("https://Bücher.Example/.well-known/ucp"));
        Assert.Equal("https://xn--bcher-kva.example:443", AgentPlatformAddress.GetOrigin("https://Bücher.Example./.well-known/ucp"));
    }
}
