using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Models;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Services;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.EventGrid;

/// <summary>
/// SEC-P10a: any anonymous request with a bearer header made the Event Grid endpoint build a new OpenID configuration
/// manager, so every request fetched the metadata document and signing keys again. One manager is kept per metadata
/// address, the address must be HTTPS, and a token that isn't a JWT is refused before anything is fetched.
/// </summary>
public sealed class EventGridAadTokensTests
{
    private const string MetadataAddress = "https://login.example.com/tenant/v2.0/.well-known/openid-configuration";

    [Fact]
    public async Task TwoRequests_FetchTheMetadataOnce()
    {
        // Arrange
        var managers = new ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>(StringComparer.Ordinal);
        var retriever = new CountingRetriever();
        var token = CreateToken();

        // Act
        var first = await EventGridAadTokens.ValidateAsync(token, Options(), managers, _ => retriever, TestContext.Current.CancellationToken);
        var second = await EventGridAadTokens.ValidateAsync(token, Options(), managers, _ => retriever, TestContext.Current.CancellationToken);

        // Assert: the token is signed with a key the metadata doesn't list, so it's refused, but only one fetch was made.
        Assert.False(first);
        Assert.False(second);
        Assert.Equal(1, retriever.MetadataFetches);
    }

    [Theory]
    [InlineData("not-a-token")]
    [InlineData("a.b")]
    public async Task AMalformedToken_IsRefused_BeforeAnythingIsFetched(string token)
    {
        var managers = new ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>(StringComparer.Ordinal);
        var retriever = new CountingRetriever();

        var valid = await EventGridAadTokens.ValidateAsync(token, Options(), managers, _ => retriever, TestContext.Current.CancellationToken);

        Assert.False(valid);
        Assert.Equal(0, retriever.MetadataFetches);
    }

    [Fact]
    public async Task APlainHttpMetadataAddress_IsRefused_BeforeAnythingIsFetched()
    {
        var managers = new ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>(StringComparer.Ordinal);
        var retriever = new CountingRetriever();
        var options = Options();
        options.AADMetadataAddress = "http://login.example.com/tenant/v2.0/.well-known/openid-configuration";

        var valid = await EventGridAadTokens.ValidateAsync(CreateToken(), options, managers, _ => retriever, TestContext.Current.CancellationToken);

        Assert.False(valid);
        Assert.Equal(0, retriever.MetadataFetches);
    }

    private static EventGridOptions Options()
        => new()
        {
            AADIssuer = "https://login.example.com/tenant/v2.0",
            AADAudience = "api://eventgrid",
            AADMetadataAddress = MetadataAddress,
        };

    private static string CreateToken()
    {
        using var rsa = RSA.Create(2048);
        var credentials = new SigningCredentials(new RsaSecurityKey(rsa.ExportParameters(true)) { KeyId = "unknown" }, SecurityAlgorithms.RsaSha256);

        return new JwtSecurityTokenHandler().CreateEncodedJwt(
            "https://login.example.com/tenant/v2.0",
            "api://eventgrid",
            new ClaimsIdentity([new Claim("sub", "eventgrid")]),
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(10),
            DateTime.UtcNow.AddMinutes(-1),
            credentials);
    }

    private sealed class CountingRetriever : IDocumentRetriever
    {
        private int _metadataFetches;

        public int MetadataFetches => _metadataFetches;

        public Task<string> GetDocumentAsync(string address, CancellationToken cancel)
        {
            if (address == MetadataAddress)
            {
                Interlocked.Increment(ref _metadataFetches);

                return Task.FromResult("""{"issuer":"https://login.example.com/tenant/v2.0","jwks_uri":"https://login.example.com/tenant/discovery/v2.0/keys"}""");
            }

            return Task.FromResult("""{"keys":[]}""");
        }
    }
}
