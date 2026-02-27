using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Mcp;

public sealed class DefaultOAuth2TokenServiceTests
{
    private const string TokenEndpoint = "https://auth.example.com/oauth2/token";
    private const string ClientId = "test-client-id";
    private const string ClientSecret = "test-client-secret";

    [Fact]
    public async Task AcquireTokenAsync_SuccessfulResponse_ReturnsAccessToken()
    {
        // Arrange
        var expectedToken = "access-token-abc123";
        var handler = CreateHandler(new TokenResponse(expectedToken, "Bearer", 3600));
        var service = CreateService(handler);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var token = await service.AcquireTokenAsync(TokenEndpoint, ClientId, ClientSecret, cancellationToken: ct);

        // Assert
        Assert.Equal(expectedToken, token);
    }

    [Fact]
    public async Task AcquireTokenAsync_SendsCorrectParameters()
    {
        // Arrange
        var scopes = "read write";
        Dictionary<string, string> capturedForm = null;
        var ct = TestContext.Current.CancellationToken;

        var handler = CreateHandler(new TokenResponse("token", "Bearer", 3600), request =>
        {
            capturedForm = ParseFormContent(request);
        });
        var service = CreateService(handler);

        // Act
        await service.AcquireTokenAsync(TokenEndpoint, ClientId, ClientSecret, scopes, ct);

        // Assert
        Assert.NotNull(capturedForm);
        Assert.Equal("client_credentials", capturedForm["grant_type"]);
        Assert.Equal(ClientId, capturedForm["client_id"]);
        Assert.Equal(ClientSecret, capturedForm["client_secret"]);
        Assert.Equal(scopes, capturedForm["scope"]);
    }

    [Fact]
    public async Task AcquireTokenAsync_NoScopes_DoesNotSendScopeParameter()
    {
        // Arrange
        Dictionary<string, string> capturedForm = null;
        var ct = TestContext.Current.CancellationToken;

        var handler = CreateHandler(new TokenResponse("token", "Bearer", 3600), request =>
        {
            capturedForm = ParseFormContent(request);
        });
        var service = CreateService(handler);

        // Act
        await service.AcquireTokenAsync(TokenEndpoint, ClientId, ClientSecret, cancellationToken: ct);

        // Assert
        Assert.NotNull(capturedForm);
        Assert.False(capturedForm.ContainsKey("scope"));
    }

    [Fact]
    public async Task AcquireTokenAsync_CachesToken_ReturnsCachedOnSecondCall()
    {
        // Arrange
        var callCount = 0;
        var handler = CreateHandler(new TokenResponse("cached-token", "Bearer", 3600), _ => callCount++);
        var service = CreateService(handler);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var token1 = await service.AcquireTokenAsync(TokenEndpoint, ClientId, ClientSecret, "scope1", ct);
        var token2 = await service.AcquireTokenAsync(TokenEndpoint, ClientId, ClientSecret, "scope1", ct);

        // Assert
        Assert.Equal("cached-token", token1);
        Assert.Equal("cached-token", token2);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task AcquireTokenAsync_DifferentScopes_NotCachedTogether()
    {
        // Arrange
        var callCount = 0;
        var handler = CreateHandler(new TokenResponse("token", "Bearer", 3600), _ => callCount++);
        var service = CreateService(handler);
        var ct = TestContext.Current.CancellationToken;

        // Act
        await service.AcquireTokenAsync(TokenEndpoint, ClientId, ClientSecret, "scope-a", ct);
        await service.AcquireTokenAsync(TokenEndpoint, ClientId, ClientSecret, "scope-b", ct);

        // Assert
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task AcquireTokenAsync_ServerReturnsError_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"invalid_client\"}"),
            }));
        var service = CreateService(handler);
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.AcquireTokenAsync(TokenEndpoint, ClientId, ClientSecret, cancellationToken: ct));
    }

    [Fact]
    public async Task AcquireTokenAsync_EmptyAccessToken_ThrowsInvalidOperationException()
    {
        // Arrange
        var handler = CreateHandler(new TokenResponse("", "Bearer", 3600));
        var service = CreateService(handler);
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AcquireTokenAsync(TokenEndpoint, ClientId, ClientSecret, cancellationToken: ct));
    }

    [Fact]
    public async Task AcquireTokenAsync_ThrowsOnNullTokenEndpoint()
    {
        // Arrange
        var handler = CreateHandler(new TokenResponse("token", "Bearer", 3600));
        var service = CreateService(handler);
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.AcquireTokenAsync(null, ClientId, ClientSecret, cancellationToken: ct));
    }

    [Fact]
    public async Task AcquireTokenAsync_ThrowsOnNullClientId()
    {
        // Arrange
        var handler = CreateHandler(new TokenResponse("token", "Bearer", 3600));
        var service = CreateService(handler);
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.AcquireTokenAsync(TokenEndpoint, null, ClientSecret, cancellationToken: ct));
    }

    [Fact]
    public async Task AcquireTokenAsync_ThrowsOnNullClientSecret()
    {
        // Arrange
        var handler = CreateHandler(new TokenResponse("token", "Bearer", 3600));
        var service = CreateService(handler);
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.AcquireTokenAsync(TokenEndpoint, ClientId, null, cancellationToken: ct));
    }

    [Fact]
    public async Task AcquireTokenWithPrivateKeyJwtAsync_SendsJwtClientAssertion()
    {
        // Arrange
        Dictionary<string, string> capturedForm = null;
        var privateKeyPem = GenerateTestRsaPrivateKeyPem();
        var ct = TestContext.Current.CancellationToken;

        var handler = CreateHandler(new TokenResponse("pkjwt-token", "Bearer", 3600), request =>
        {
            capturedForm = ParseFormContent(request);
        });
        var service = CreateService(handler);

        // Act
        var token = await service.AcquireTokenWithPrivateKeyJwtAsync(
            TokenEndpoint, ClientId, privateKeyPem, "key-001", "api", ct);

        // Assert
        Assert.Equal("pkjwt-token", token);
        Assert.NotNull(capturedForm);
        Assert.Equal("client_credentials", capturedForm["grant_type"]);
        Assert.Equal(ClientId, capturedForm["client_id"]);
        Assert.Equal("urn:ietf:params:oauth:client-assertion-type:jwt-bearer", capturedForm["client_assertion_type"]);
        Assert.True(capturedForm.ContainsKey("client_assertion"));

        // Verify the JWT has 3 parts (header.payload.signature).
        var jwtParts = capturedForm["client_assertion"].Split('.');
        Assert.Equal(3, jwtParts.Length);
    }

    [Fact]
    public async Task AcquireTokenWithPrivateKeyJwtAsync_JwtContainsCorrectClaims()
    {
        // Arrange
        Dictionary<string, string> capturedForm = null;
        var privateKeyPem = GenerateTestRsaPrivateKeyPem();
        var ct = TestContext.Current.CancellationToken;

        var handler = CreateHandler(new TokenResponse("pkjwt-token", "Bearer", 3600), request =>
        {
            capturedForm = ParseFormContent(request);
        });
        var service = CreateService(handler);

        // Act
        await service.AcquireTokenWithPrivateKeyJwtAsync(
            TokenEndpoint, ClientId, privateKeyPem, "key-001", "api", ct);

        // Assert
        var jwt = capturedForm["client_assertion"];
        var parts = jwt.Split('.');

        // Decode header.
        var headerJson = Base64UrlDecode(parts[0]);
        var header = JsonSerializer.Deserialize<Dictionary<string, string>>(headerJson);
        Assert.Equal("RS256", header["alg"]);
        Assert.Equal("JWT", header["typ"]);
        Assert.Equal("key-001", header["kid"]);

        // Decode payload.
        var payloadJson = Base64UrlDecode(parts[1]);
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);
        Assert.Equal(ClientId, payload["iss"].ToString());
        Assert.Equal(ClientId, payload["sub"].ToString());
        Assert.Equal(TokenEndpoint, payload["aud"].ToString());
        Assert.True(payload.ContainsKey("jti"));
        Assert.True(payload.ContainsKey("iat"));
        Assert.True(payload.ContainsKey("exp"));
    }

    [Fact]
    public async Task AcquireTokenWithPrivateKeyJwtAsync_ThrowsOnNullPrivateKey()
    {
        // Arrange
        var handler = CreateHandler(new TokenResponse("token", "Bearer", 3600));
        var service = CreateService(handler);
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.AcquireTokenWithPrivateKeyJwtAsync(TokenEndpoint, ClientId, null, cancellationToken: ct));
    }

    [Fact]
    public async Task AcquireTokenWithMtlsAsync_ThrowsOnNullCertBytes()
    {
        // Arrange
        var handler = CreateHandler(new TokenResponse("token", "Bearer", 3600));
        var service = CreateService(handler);
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.AcquireTokenWithMtlsAsync(TokenEndpoint, ClientId, null, cancellationToken: ct));
    }

    [Fact]
    public async Task AcquireTokenWithPrivateKeyJwtAsync_CachesToken()
    {
        // Arrange
        var callCount = 0;
        var privateKeyPem = GenerateTestRsaPrivateKeyPem();
        var handler = CreateHandler(new TokenResponse("pkjwt-cached", "Bearer", 3600), _ => callCount++);
        var service = CreateService(handler);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var token1 = await service.AcquireTokenWithPrivateKeyJwtAsync(
            TokenEndpoint, ClientId, privateKeyPem, "key-001", "scope", ct);
        var token2 = await service.AcquireTokenWithPrivateKeyJwtAsync(
            TokenEndpoint, ClientId, privateKeyPem, "key-001", "scope", ct);

        // Assert
        Assert.Equal("pkjwt-cached", token1);
        Assert.Equal("pkjwt-cached", token2);
        Assert.Equal(1, callCount);
    }

    private static DefaultOAuth2TokenService CreateService(MockHttpMessageHandler handler)
    {
        var factory = new MockHttpClientFactory(handler);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = NullLogger<DefaultOAuth2TokenService>.Instance;

        return new DefaultOAuth2TokenService(factory, cache, logger);
    }

    private static MockHttpMessageHandler CreateHandler(TokenResponse tokenResponse, Action<HttpRequestMessage> onRequest = null)
    {
        var json = JsonSerializer.Serialize(tokenResponse);

        return new MockHttpMessageHandler((request, _) =>
        {
            onRequest?.Invoke(request);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
        });
    }

    /// <summary>
    /// Synchronously parses form-encoded content from the request.
    /// </summary>
    private static Dictionary<string, string> ParseFormContent(HttpRequestMessage request)
    {
        var content = request.Content as FormUrlEncodedContent;
        var bytes = content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        var body = System.Text.Encoding.UTF8.GetString(bytes);

        return body.Split('&')
            .Select(p => p.Split('='))
            .ToDictionary(
                p => Uri.UnescapeDataString(p[0]),
                p => Uri.UnescapeDataString(p[1].Replace('+', ' ')));
    }

    private static string GenerateTestRsaPrivateKeyPem()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        return rsa.ExportRSAPrivateKeyPem();
    }

    private static string Base64UrlDecode(string input)
    {
        var padded = input
            .Replace('-', '+')
            .Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        var bytes = Convert.FromBase64String(padded);

        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private sealed class TokenResponse
    {
        public TokenResponse(string accessToken, string tokenType, int expiresIn)
        {
            AccessToken = accessToken;
            TokenType = tokenType;
            ExpiresIn = expiresIn;
        }

        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; }

        [System.Text.Json.Serialization.JsonPropertyName("token_type")]
        public string TokenType { get; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; }
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }

    private sealed class MockHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public MockHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler);
    }
}
