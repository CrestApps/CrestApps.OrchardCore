using CrestApps.OrchardCore.DataConnectors.Drivers;
using CrestApps.OrchardCore.DataConnectors.Http;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

/// <summary>
/// SEC-A20 l: a stored API key, password, bearer token or client secret survived a change of the connection's host, so
/// someone who could edit the connection but not read its secret could point it at their own server and receive it.
/// </summary>
public sealed class HttpConnectionSecretScopeTests
{
    private static readonly HttpConnectionSettings _stored = new()
    {
        BaseUrl = "https://api.example.com/v1",
        Authentication = new HttpAuthenticationSettings { TokenUrl = "https://login.example.com/token" },
    };

    [Theory]
    [InlineData("https://api.example.com/v2", "https://login.example.com/token", false)]
    [InlineData("https://API.example.com/other", "https://login.example.com/token", false)]
    [InlineData("https://evil.example.net/v1", "https://login.example.com/token", true)]
    [InlineData("http://api.example.com/v1", "https://login.example.com/token", true)]
    [InlineData("https://api.example.com:8443/v1", "https://login.example.com/token", true)]
    [InlineData("https://api.example.com/v1", "https://evil.example.net/token", true)]
    public void SendsSecretsElsewhere_WhenTheOriginChanges(string baseUrl, string tokenUrl, bool expected)
    {
        Assert.Equal(expected, HttpConnectionDisplayDriver.SendsSecretsElsewhere(_stored, baseUrl, tokenUrl, authority: null));
    }

    [Fact]
    public void SendsSecretsElsewhere_ForANewConnection_IsFalse()
    {
        Assert.False(HttpConnectionDisplayDriver.SendsSecretsElsewhere(new HttpConnectionSettings(), "https://api.example.com", null, null));
    }
}
