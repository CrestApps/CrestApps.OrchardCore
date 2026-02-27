using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

internal sealed class DefaultOAuth2TokenService : IOAuth2TokenService
{
    // Reserve 60 seconds to avoid using a token that's about to expire.
    private const int ExpirationBufferSeconds = 60;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;

    public DefaultOAuth2TokenService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<DefaultOAuth2TokenService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<string> AcquireTokenAsync(
        string tokenEndpoint,
        string clientId,
        string clientSecret,
        string scopes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tokenEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(clientId);
        ArgumentException.ThrowIfNullOrEmpty(clientSecret);

        var cacheKey = GetCacheKey("cc", tokenEndpoint, clientId, scopes);

        if (_cache.TryGetValue(cacheKey, out string cachedToken))
        {
            return cachedToken;
        }

        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        };

        if (!string.IsNullOrWhiteSpace(scopes))
        {
            parameters["scope"] = scopes;
        }

        using var httpClient = _httpClientFactory.CreateClient(nameof(DefaultOAuth2TokenService));

        return await SendTokenRequestAsync(httpClient, tokenEndpoint, parameters, cacheKey, cancellationToken);
    }

    public async Task<string> AcquireTokenWithPrivateKeyJwtAsync(
        string tokenEndpoint,
        string clientId,
        string privateKeyPem,
        string keyId = null,
        string scopes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tokenEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(clientId);
        ArgumentException.ThrowIfNullOrEmpty(privateKeyPem);

        var cacheKey = GetCacheKey("pkjwt", tokenEndpoint, clientId, scopes);

        if (_cache.TryGetValue(cacheKey, out string cachedToken))
        {
            return cachedToken;
        }

        var assertion = CreateClientAssertion(tokenEndpoint, clientId, privateKeyPem, keyId);

        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["client_assertion"] = assertion,
        };

        if (!string.IsNullOrWhiteSpace(scopes))
        {
            parameters["scope"] = scopes;
        }

        using var httpClient = _httpClientFactory.CreateClient(nameof(DefaultOAuth2TokenService));

        return await SendTokenRequestAsync(httpClient, tokenEndpoint, parameters, cacheKey, cancellationToken);
    }

    public async Task<string> AcquireTokenWithMtlsAsync(
        string tokenEndpoint,
        string clientId,
        byte[] clientCertificateBytes,
        string certificatePassword = null,
        string scopes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tokenEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(clientId);
        ArgumentNullException.ThrowIfNull(clientCertificateBytes);

        var cacheKey = GetCacheKey("mtls", tokenEndpoint, clientId, scopes);

        if (_cache.TryGetValue(cacheKey, out string cachedToken))
        {
            return cachedToken;
        }

        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
        };

        if (!string.IsNullOrWhiteSpace(scopes))
        {
            parameters["scope"] = scopes;
        }

        var cert = string.IsNullOrEmpty(certificatePassword)
            ? X509CertificateLoader.LoadPkcs12(clientCertificateBytes, null)
            : X509CertificateLoader.LoadPkcs12(clientCertificateBytes, certificatePassword);

        using (cert)
        {
            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(cert);

            using var httpClient = new HttpClient(handler);

            return await SendTokenRequestAsync(httpClient, tokenEndpoint, parameters, cacheKey, cancellationToken);
        }
    }

    private async Task<string> SendTokenRequestAsync(
        HttpClient httpClient,
        string tokenEndpoint,
        Dictionary<string, string> parameters,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(parameters),
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "OAuth2 token request to '{TokenEndpoint}' failed with status {StatusCode}: {ErrorBody}",
                tokenEndpoint,
                response.StatusCode,
                errorBody);

            throw new HttpRequestException(
                $"OAuth2 token request failed with status {response.StatusCode}.");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<OAuth2TokenResponse>(cancellationToken);

        if (string.IsNullOrEmpty(tokenResponse?.AccessToken))
        {
            throw new InvalidOperationException("OAuth2 token response did not contain an access token.");
        }

        var expiration = tokenResponse.ExpiresIn > ExpirationBufferSeconds
            ? TimeSpan.FromSeconds(tokenResponse.ExpiresIn - ExpirationBufferSeconds)
            : TimeSpan.FromMinutes(5);

        _cache.Set(cacheKey, tokenResponse.AccessToken, expiration);

        return tokenResponse.AccessToken;
    }

    private static string CreateClientAssertion(string tokenEndpoint, string clientId, string privateKeyPem, string keyId)
    {
        var now = DateTimeOffset.UtcNow;

        // Build JWT header.
        var headerObj = new Dictionary<string, string>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT",
        };

        if (!string.IsNullOrEmpty(keyId))
        {
            headerObj["kid"] = keyId;
        }

        var headerJson = JsonSerializer.Serialize(headerObj);
        var headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));

        // Build JWT payload.
        var payloadObj = new Dictionary<string, object>
        {
            ["iss"] = clientId,
            ["sub"] = clientId,
            ["aud"] = tokenEndpoint,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
        };

        var payloadJson = JsonSerializer.Serialize(payloadObj);
        var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        // Sign.
        var dataToSign = Encoding.UTF8.GetBytes($"{headerBase64}.{payloadBase64}");

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var signature = rsa.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureBase64 = Base64UrlEncode(signature);

        return $"{headerBase64}.{payloadBase64}.{signatureBase64}";
    }

    private static string Base64UrlEncode(byte[] input)
        => Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string GetCacheKey(string grantType, string tokenEndpoint, string clientId, string scopes)
        => $"mcp_oauth2_{grantType}_{tokenEndpoint}_{clientId}_{scopes}";

    private sealed class OAuth2TokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
