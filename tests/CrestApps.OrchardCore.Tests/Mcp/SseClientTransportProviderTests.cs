using System.Text;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Core.Services;
using CrestApps.OrchardCore.AI.Mcp.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using Moq;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Mcp;

public sealed class SseClientTransportProviderTests
{
    private const string TestEndpoint = "https://mcp.example.com/sse";

    [Fact]
    public void CanHandle_SseConnection_ReturnsTrue()
    {
        // Arrange
        var connection = new McpConnection { Source = McpConstants.TransportTypes.Sse };
        var provider = CreateProvider();

        // Act & Assert
        Assert.True(provider.CanHandle(connection));
    }

    [Fact]
    public void CanHandle_NonSseConnection_ReturnsFalse()
    {
        // Arrange
        var connection = new McpConnection { Source = McpConstants.TransportTypes.StdIo };
        var provider = CreateProvider();

        // Act & Assert
        Assert.False(provider.CanHandle(connection));
    }

    [Fact]
    public async Task GetAsync_Anonymous_ReturnsTransportWithNoAuthHeaders()
    {
        // Arrange
        var connection = CreateConnection(McpClientAuthenticationType.Anonymous);
        var provider = CreateProvider();

        // Act
        var headers = await GetHeadersAsync(provider, connection);

        // Assert
        Assert.Empty(headers);
    }

    [Theory]
    [InlineData("Authorization", "Bearer", "my-api-key", "Authorization", "Bearer my-api-key")]
    [InlineData("X-Api-Key", "", "my-api-key", "X-Api-Key", "my-api-key")]
    [InlineData("X-Api-Key", null, "my-api-key", "X-Api-Key", "my-api-key")]
    [InlineData("", "Bearer", "my-api-key", "Authorization", "Bearer my-api-key")]
    [InlineData(null, "Bearer", "my-api-key", "Authorization", "Bearer my-api-key")]
    [InlineData(null, null, "my-api-key", "Authorization", "my-api-key")]
    public async Task GetAsync_ApiKey_SetsCorrectHeader(
        string headerName,
        string prefix,
        string apiKey,
        string expectedHeaderName,
        string expectedHeaderValue)
    {
        // Arrange
        var connection = CreateConnection(McpClientAuthenticationType.ApiKey);
        var metadata = connection.As<SseMcpConnectionMetadata>();
        metadata.ApiKeyHeaderName = headerName;
        metadata.ApiKeyPrefix = prefix;
        metadata.ApiKey = apiKey;
        connection.Put(metadata);

        var provider = CreateProvider();

        // Act
        var headers = await GetHeadersAsync(provider, connection);

        // Assert
        Assert.True(headers.ContainsKey(expectedHeaderName));
        Assert.Equal(expectedHeaderValue, headers[expectedHeaderName]);
    }

    [Fact]
    public async Task GetAsync_Basic_SetsBase64AuthorizationHeader()
    {
        // Arrange
        var username = "testuser";
        var password = "testpass";
        var connection = CreateConnection(McpClientAuthenticationType.Basic);
        var metadata = connection.As<SseMcpConnectionMetadata>();
        metadata.BasicUsername = username;
        metadata.BasicPassword = password;
        connection.Put(metadata);

        var provider = CreateProvider();

        // Act
        var headers = await GetHeadersAsync(provider, connection);

        // Assert
        var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        Assert.True(headers.ContainsKey("Authorization"));
        Assert.Equal($"Basic {expectedCredentials}", headers["Authorization"]);
    }

    [Fact]
    public async Task GetAsync_Basic_WithEmptyPassword_SetsHeaderWithEmptyPassword()
    {
        // Arrange
        var connection = CreateConnection(McpClientAuthenticationType.Basic);
        var metadata = connection.As<SseMcpConnectionMetadata>();
        metadata.BasicUsername = "testuser";
        metadata.BasicPassword = null;
        connection.Put(metadata);

        var provider = CreateProvider();

        // Act
        var headers = await GetHeadersAsync(provider, connection);

        // Assert
        var expectedCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("testuser:"));
        Assert.True(headers.ContainsKey("Authorization"));
        Assert.Equal($"Basic {expectedCredentials}", headers["Authorization"]);
    }

    [Fact]
    public async Task GetAsync_OAuth2ClientCredentials_AcquiresTokenAndSetsBearerHeader()
    {
        // Arrange
        var expectedToken = "oauth2-access-token-123";
        var tokenEndpoint = "https://auth.example.com/token";
        var clientId = "my-client-id";
        var clientSecret = "my-client-secret";
        var scopes = "read write";

        var connection = CreateConnection(McpClientAuthenticationType.OAuth2ClientCredentials);
        var metadata = connection.As<SseMcpConnectionMetadata>();
        metadata.OAuth2TokenEndpoint = tokenEndpoint;
        metadata.OAuth2ClientId = clientId;
        metadata.OAuth2ClientSecret = clientSecret;
        metadata.OAuth2Scopes = scopes;
        connection.Put(metadata);

        var tokenService = new Mock<IOAuth2TokenService>();
        tokenService
            .Setup(x => x.AcquireTokenAsync(tokenEndpoint, clientId, clientSecret, scopes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedToken);

        var provider = CreateProvider(tokenService: tokenService.Object);

        // Act
        var headers = await GetHeadersAsync(provider, connection);

        // Assert
        Assert.True(headers.ContainsKey("Authorization"));
        Assert.Equal($"Bearer {expectedToken}", headers["Authorization"]);
        tokenService.Verify(x => x.AcquireTokenAsync(tokenEndpoint, clientId, clientSecret, scopes, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_OAuth2PrivateKeyJwt_AcquiresTokenAndSetsBearerHeader()
    {
        // Arrange
        var expectedToken = "pkjwt-access-token-456";
        var tokenEndpoint = "https://auth.example.com/token";
        var clientId = "my-client-id";
        var privateKey = "-----BEGIN RSA PRIVATE KEY-----\ntest\n-----END RSA PRIVATE KEY-----";
        var keyId = "key-001";
        var scopes = "api";

        var connection = CreateConnection(McpClientAuthenticationType.OAuth2PrivateKeyJwt);
        var metadata = connection.As<SseMcpConnectionMetadata>();
        metadata.OAuth2TokenEndpoint = tokenEndpoint;
        metadata.OAuth2ClientId = clientId;
        metadata.OAuth2PrivateKey = privateKey;
        metadata.OAuth2KeyId = keyId;
        metadata.OAuth2Scopes = scopes;
        connection.Put(metadata);

        var tokenService = new Mock<IOAuth2TokenService>();
        tokenService
            .Setup(x => x.AcquireTokenWithPrivateKeyJwtAsync(tokenEndpoint, clientId, privateKey, keyId, scopes, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedToken);

        var provider = CreateProvider(tokenService: tokenService.Object);

        // Act
        var headers = await GetHeadersAsync(provider, connection);

        // Assert
        Assert.True(headers.ContainsKey("Authorization"));
        Assert.Equal($"Bearer {expectedToken}", headers["Authorization"]);
        tokenService.Verify(x => x.AcquireTokenWithPrivateKeyJwtAsync(tokenEndpoint, clientId, privateKey, keyId, scopes, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_OAuth2Mtls_AcquiresTokenAndSetsBearerHeader()
    {
        // Arrange
        var expectedToken = "mtls-access-token-789";
        var tokenEndpoint = "https://auth.example.com/token";
        var clientId = "my-client-id";
        var rawCertBytes = new byte[] { 1, 2, 3, 4, 5 };
        var certBase64 = Convert.ToBase64String(rawCertBytes);
        var certPassword = "cert-password";
        var scopes = "admin";

        // Protect values like the display driver does before storing.
        var protector = new PassthroughDataProtectionProvider().CreateProtector("test");

        var connection = CreateConnection(McpClientAuthenticationType.OAuth2Mtls);
        var metadata = connection.As<SseMcpConnectionMetadata>();
        metadata.OAuth2TokenEndpoint = tokenEndpoint;
        metadata.OAuth2ClientId = clientId;
        metadata.OAuth2ClientCertificate = protector.Protect(certBase64);
        metadata.OAuth2ClientCertificatePassword = protector.Protect(certPassword);
        metadata.OAuth2Scopes = scopes;
        connection.Put(metadata);

        var tokenService = new Mock<IOAuth2TokenService>();
        tokenService
            .Setup(x => x.AcquireTokenWithMtlsAsync(
                tokenEndpoint,
                clientId,
                It.Is<byte[]>(b => b.SequenceEqual(rawCertBytes)),
                certPassword,
                scopes,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedToken);

        var provider = CreateProvider(tokenService: tokenService.Object);

        // Act
        var headers = await GetHeadersAsync(provider, connection);

        // Assert
        Assert.True(headers.ContainsKey("Authorization"));
        Assert.Equal($"Bearer {expectedToken}", headers["Authorization"]);
    }

    [Fact]
    public async Task GetAsync_CustomHeaders_PassesAllHeaders()
    {
        // Arrange
        var connection = CreateConnection(McpClientAuthenticationType.CustomHeaders);
        var metadata = connection.As<SseMcpConnectionMetadata>();
        metadata.AdditionalHeaders = new Dictionary<string, string>
        {
            ["X-Custom-Header"] = "custom-value",
            ["Authorization"] = "Bearer custom-token",
        };
        connection.Put(metadata);

        var provider = CreateProvider();

        // Act
        var headers = await GetHeadersAsync(provider, connection);

        // Assert
        Assert.Equal(2, headers.Count);
        Assert.Equal("custom-value", headers["X-Custom-Header"]);
        Assert.Equal("Bearer custom-token", headers["Authorization"]);
    }

    [Fact]
    public async Task GetAsync_CustomHeaders_WithNullHeaders_ReturnsEmptyHeaders()
    {
        // Arrange
        var connection = CreateConnection(McpClientAuthenticationType.CustomHeaders);
        var metadata = connection.As<SseMcpConnectionMetadata>();
        metadata.AdditionalHeaders = null;
        connection.Put(metadata);

        var provider = CreateProvider();

        // Act
        var headers = await GetHeadersAsync(provider, connection);

        // Assert
        Assert.Empty(headers);
    }

    [Fact]
    public async Task GetAsync_OAuth2ClientCredentials_WhenTokenAcquisitionFails_ThrowsException()
    {
        // Arrange
        var connection = CreateConnection(McpClientAuthenticationType.OAuth2ClientCredentials);
        var metadata = connection.As<SseMcpConnectionMetadata>();
        metadata.OAuth2TokenEndpoint = "https://auth.example.com/token";
        metadata.OAuth2ClientId = "client-id";
        metadata.OAuth2ClientSecret = "client-secret";
        connection.Put(metadata);

        var tokenService = new Mock<IOAuth2TokenService>();
        tokenService
            .Setup(x => x.AcquireTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Token request failed"));

        var provider = CreateProvider(tokenService: tokenService.Object);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetAsync(connection));
    }

    private static McpConnection CreateConnection(McpClientAuthenticationType authType)
    {
        var connection = new McpConnection
        {
            Source = McpConstants.TransportTypes.Sse,
        };

        var metadata = new SseMcpConnectionMetadata
        {
            Endpoint = new Uri(TestEndpoint),
            AuthenticationType = authType,
        };

        connection.Put(metadata);

        return connection;
    }

    private static SseClientTransportProvider CreateProvider(IOAuth2TokenService tokenService = null)
    {
        var dataProtectionProvider = new PassthroughDataProtectionProvider();
        tokenService ??= Mock.Of<IOAuth2TokenService>();
        var logger = NullLogger<SseClientTransportProvider>.Instance;

        return new SseClientTransportProvider(dataProtectionProvider, tokenService, logger);
    }

    private static async Task<Dictionary<string, string>> GetHeadersAsync(SseClientTransportProvider provider, McpConnection connection)
    {
        var transport = await provider.GetAsync(connection);

        // Extract headers via reflection since HttpClientTransport doesn't expose them directly.
        var optionsField = typeof(HttpClientTransport)
            .GetField("_options", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var options = optionsField?.GetValue(transport) as HttpClientTransportOptions;

        return options?.AdditionalHeaders as Dictionary<string, string>
            ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// A pass-through data protector that returns values unchanged.
    /// This simulates the behavior of decryption returning the original value.
    /// </summary>
    private sealed class PassthroughDataProtectionProvider : IDataProtectionProvider
    {
        public IDataProtector CreateProtector(string purpose) => new PassthroughDataProtector();
    }

    private sealed class PassthroughDataProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => plaintext;

        public byte[] Unprotect(byte[] protectedData) => protectedData;
    }
}
