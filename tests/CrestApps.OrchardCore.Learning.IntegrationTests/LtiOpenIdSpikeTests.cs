using System.Net;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OrchardCore.OpenId.Abstractions.Descriptors;
using OrchardCore.OpenId.Abstractions.Managers;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// The spike of plan section 16 check 6 (LTI on the OpenID server), kept as characterization tests of what Orchard
/// Core's OpenID server does today with an LTI tool's requests, without any Learning code: whether its token endpoint
/// takes a <c>private_key_jwt</c> client assertion against a key set held by the OpenID application, in the shape LTI
/// tools send it and in the shape OpenIddict 7 asks for, and what its authorization endpoint answers to an LTI
/// third-party initiated login.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed partial class LtiOpenIdSpikeTests
{
    private const string Scope = "https://purl.imsglobal.org/spec/lti-ags/scope/score";
    private const string TokenClientId = "lti-spike-token";
    private const string LaunchClientId = "lti-spike-launch";
    private const string RedirectUri = "https://tool.example.com/lti/launch";

    private static readonly RSA _toolKey = RSA.Create(2048);

    private readonly LearningSiteFixture _fixture;

    public LtiOpenIdSpikeTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TokenEndpoint_LtiShapedClientAssertion_IsRefusedByOpenIddict()
    {
        // Arrange: what LTI libraries send (typ JWT, aud the token endpoint URL).
        await EnsureTokenClientAsync();
        using var http = await _fixture.CreateClientAsync();
        var tokenUrl = new Uri(_fixture.BaseAddress, "connect/token").AbsoluteUri;

        // Act
        var response = await http.PostAsync("connect/token", TokenRequest(Assertion("JWT", tokenUrl)), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_client", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TokenEndpoint_Rfc7523bisClientAssertion_WithTheApplicationsKeySet_GetsAnAccessToken()
    {
        // Arrange: the shape OpenIddict 7 asks for (typ client-authentication+jwt, aud the issuer).
        await EnsureTokenClientAsync();
        using var http = await _fixture.CreateClientAsync();

        // Act
        var response = await http.PostAsync("connect/token", TokenRequest(Assertion("client-authentication+jwt", _fixture.BaseAddress.AbsoluteUri)), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.False(string.IsNullOrEmpty(JsonNode.Parse(body)?["access_token"]?.GetValue<string>()));
    }

    [Fact]
    public async Task AuthorizationEndpoint_LtiLogin_IssuesAnIdTokenSignedByTheServerKey_WithTheUserIdAsSubject()
    {
        // Arrange
        await EnsureLaunchClientAsync();
        using var http = await _fixture.CreateClientAsync("learner1");
        var query = string.Join("&", new Dictionary<string, string>
        {
            ["scope"] = "openid",
            ["response_type"] = "id_token",
            ["response_mode"] = "form_post",
            ["prompt"] = "none",
            ["client_id"] = LaunchClientId,
            ["redirect_uri"] = RedirectUri,
            ["login_hint"] = "opaque-login-hint",
            ["lti_message_hint"] = "opaque-message-hint",
            ["state"] = "state-1",
            ["nonce"] = "nonce-1",
        }.Select(pair => pair.Key + "=" + Uri.EscapeDataString(pair.Value)));

        // Act
        var response = await http.GetAsync("connect/authorize?" + query, TestContext.Current.CancellationToken);

        // OpenIddict 7 keeps the request as a request token and redirects to itself with a request_uri.
        for (var hop = 0; hop < 3 && response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location is { } location; hop++)
        {
            response = await http.GetAsync(location, TestContext.Current.CancellationToken);
        }

        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {response.Headers.Location} {html}");

        var idToken = new JsonWebToken(WebUtility.HtmlDecode(IdTokenPattern().Match(html).Groups["token"].Value));

        Assert.Equal(_fixture.EnrolledUserId, idToken.Subject);
        Assert.Equal("nonce-1", idToken.GetClaim("nonce").Value);
        Assert.False(idToken.TryGetClaim("https://purl.imsglobal.org/spec/lti/claim/message_type", out _));
        Assert.Contains(RedirectUri, html, StringComparison.Ordinal);
    }

    private Task EnsureTokenClientAsync()
        => _fixture.InTenantAsync(async services =>
        {
            var scopes = services.GetRequiredService<IOpenIdScopeManager>();

            if (await scopes.FindByNameAsync(Scope, TestContext.Current.CancellationToken) is null)
            {
                await scopes.CreateAsync(new OpenIdScopeDescriptor { Name = Scope, DisplayName = "LTI score" }, TestContext.Current.CancellationToken);
            }

            var applications = services.GetRequiredService<IOpenIdApplicationManager>();

            if (await applications.FindByClientIdAsync(TokenClientId, TestContext.Current.CancellationToken) is not null)
            {
                return;
            }

            var key = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(_toolKey.ExportParameters(false)) { KeyId = "tool-1" });
            key.Use = "sig";
            key.Alg = SecurityAlgorithms.RsaSha256;

            var keySet = new JsonWebKeySet();
            keySet.Keys.Add(key);

            var descriptor = new OpenIdApplicationDescriptor
            {
                ClientId = TokenClientId,
                DisplayName = "LTI spike tool",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
                JsonWebKeySet = keySet,
            };

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + Scope);

            await applications.CreateAsync(descriptor, TestContext.Current.CancellationToken);
        });

    private Task EnsureLaunchClientAsync()
        => _fixture.InTenantAsync(async services =>
        {
            var applications = services.GetRequiredService<IOpenIdApplicationManager>();

            if (await applications.FindByClientIdAsync(LaunchClientId, TestContext.Current.CancellationToken) is not null)
            {
                return;
            }

            var descriptor = new OpenIdApplicationDescriptor
            {
                ClientId = LaunchClientId,
                DisplayName = "LTI spike launch",
                ClientType = OpenIddictConstants.ClientTypes.Public,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            };

            descriptor.RedirectUris.Add(new Uri(RedirectUri));
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.Implicit);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.IdToken);

            await applications.CreateAsync(descriptor, TestContext.Current.CancellationToken);
        });

    private static FormUrlEncodedContent TokenRequest(string assertion)
        => new(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["client_assertion"] = assertion,
            ["scope"] = Scope,
        });

    private static string Assertion(string type, string audience)
    {
        var now = DateTime.UtcNow;

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = TokenClientId,
            Audience = audience,
            Subject = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", TokenClientId)]),
            Claims = new Dictionary<string, object> { ["jti"] = Guid.NewGuid().ToString("n") },
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(5),
            TokenType = type,
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(_toolKey) { KeyId = "tool-1" }, SecurityAlgorithms.RsaSha256),
        });
    }

    [GeneratedRegex("name=\"id_token\"[^>]*value=\"(?<token>[^\"]+)\"")]
    private static partial Regex IdTokenPattern();
}
