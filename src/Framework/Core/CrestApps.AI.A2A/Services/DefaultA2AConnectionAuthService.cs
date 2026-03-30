using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.AI.A2A.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CrestApps.AI.A2A.Services;

internal sealed class DefaultA2AConnectionAuthService : IA2AConnectionAuthService
{
    private const int ExpirationBufferSeconds = 60;

    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;

    public DefaultA2AConnectionAuthService(
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<DefaultA2AConnectionAuthService> logger)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> BuildHeadersAsync(
        A2AConnectionMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (metadata is null)
        {
            return headers;
        }

        var protector = _dataProtectionProvider.CreateProtector(A2AConstants.DataProtectionPurpose);

        switch (metadata.AuthenticationType)
        {
            case A2AClientAuthenticationType.ApiKey:
                BuildApiKeyHeaders(metadata, protector, headers);
                break;

            case A2AClientAuthenticationType.Basic:
                BuildBasicHeaders(metadata, protector, headers);
                break;

            case A2AClientAuthenticationType.OAuth2ClientCredentials:
                await BuildOAuth2ClientCredentialsHeadersAsync(metadata, protector, headers, cancellationToken);
                break;

            case A2AClientAuthenticationType.OAuth2PrivateKeyJwt:
                await BuildOAuth2PrivateKeyJwtHeadersAsync(metadata, protector, headers, cancellationToken);
                break;

            case A2AClientAuthenticationType.OAuth2Mtls:
                await BuildOAuth2MtlsHeadersAsync(metadata, protector, headers, cancellationToken);
                break;

            case A2AClientAuthenticationType.CustomHeaders:
                BuildCustomHeaders(metadata, headers);
                break;
        }

        return headers;
    }

    public async Task ConfigureHttpClientAsync(
        HttpClient httpClient,
        A2AConnectionMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var headers = await BuildHeadersAsync(metadata, cancellationToken);

        foreach (var header in headers)
        {
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private void BuildApiKeyHeaders(
        A2AConnectionMetadata metadata,
        IDataProtector protector,
        Dictionary<string, string> headers)
    {
        if (string.IsNullOrEmpty(metadata.ApiKey))
        {
            return;
        }

        var apiKey = Unprotect(protector, metadata.ApiKey);
        var headerName = string.IsNullOrWhiteSpace(metadata.ApiKeyHeaderName)
            ? "Authorization"
            : metadata.ApiKeyHeaderName;
        var value = !string.IsNullOrWhiteSpace(metadata.ApiKeyPrefix)
            ? $"{metadata.ApiKeyPrefix} {apiKey}"
            : apiKey;

        headers[headerName] = value;
    }

    private void BuildBasicHeaders(
        A2AConnectionMetadata metadata,
        IDataProtector protector,
        Dictionary<string, string> headers)
    {
        if (string.IsNullOrEmpty(metadata.BasicUsername))
        {
            return;
        }

        var password = !string.IsNullOrEmpty(metadata.BasicPassword)
            ? Unprotect(protector, metadata.BasicPassword)
            : string.Empty;
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{metadata.BasicUsername}:{password}"));

        headers["Authorization"] = $"Basic {credentials}";
    }

    private async Task BuildOAuth2ClientCredentialsHeadersAsync(
        A2AConnectionMetadata metadata,
        IDataProtector protector,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(metadata.OAuth2TokenEndpoint) ||
            string.IsNullOrEmpty(metadata.OAuth2ClientId) ||
            string.IsNullOrEmpty(metadata.OAuth2ClientSecret))
        {
            return;
        }

        var clientSecret = Unprotect(protector, metadata.OAuth2ClientSecret);

        try
        {
            var token = await AcquireTokenAsync(
                "cc",
                metadata.OAuth2TokenEndpoint,
                metadata.OAuth2ClientId,
                metadata.OAuth2Scopes,
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = metadata.OAuth2ClientId,
                    ["client_secret"] = clientSecret,
                },
                cancellationToken);

            headers["Authorization"] = $"Bearer {token}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire OAuth2 token from '{TokenEndpoint}'.", metadata.OAuth2TokenEndpoint);
            throw;
        }
    }

    private async Task BuildOAuth2PrivateKeyJwtHeadersAsync(
        A2AConnectionMetadata metadata,
        IDataProtector protector,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(metadata.OAuth2TokenEndpoint) ||
            string.IsNullOrEmpty(metadata.OAuth2ClientId) ||
            string.IsNullOrEmpty(metadata.OAuth2PrivateKey))
        {
            return;
        }

        var privateKey = Unprotect(protector, metadata.OAuth2PrivateKey);
        var assertion = CreateClientAssertion(
            metadata.OAuth2TokenEndpoint, metadata.OAuth2ClientId, privateKey, metadata.OAuth2KeyId);

        try
        {
            var token = await AcquireTokenAsync(
                "pkjwt",
                metadata.OAuth2TokenEndpoint,
                metadata.OAuth2ClientId,
                metadata.OAuth2Scopes,
                new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = metadata.OAuth2ClientId,
                    ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
                    ["client_assertion"] = assertion,
                },
                cancellationToken);

            headers["Authorization"] = $"Bearer {token}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire OAuth2 token via Private Key JWT from '{TokenEndpoint}'.", metadata.OAuth2TokenEndpoint);
            throw;
        }
    }

    private async Task BuildOAuth2MtlsHeadersAsync(
        A2AConnectionMetadata metadata,
        IDataProtector protector,
        Dictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(metadata.OAuth2TokenEndpoint) ||
            string.IsNullOrEmpty(metadata.OAuth2ClientId) ||
            string.IsNullOrEmpty(metadata.OAuth2ClientCertificate))
        {
            return;
        }

        var certBase64 = Unprotect(protector, metadata.OAuth2ClientCertificate);
        var certBytes = Convert.FromBase64String(certBase64);
        var certPassword = !string.IsNullOrEmpty(metadata.OAuth2ClientCertificatePassword)
            ? Unprotect(protector, metadata.OAuth2ClientCertificatePassword)
            : null;

        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = metadata.OAuth2ClientId,
            };

            if (!string.IsNullOrWhiteSpace(metadata.OAuth2Scopes))
            {
                parameters["scope"] = metadata.OAuth2Scopes;
            }

            var cacheKey = GetOAuth2CacheKey("mtls", metadata.OAuth2TokenEndpoint, metadata.OAuth2ClientId, metadata.OAuth2Scopes);

            if (_cache.TryGetValue(cacheKey, out string cachedToken))
            {
                headers["Authorization"] = $"Bearer {cachedToken}";
                return;
            }

            var cert = string.IsNullOrEmpty(certPassword)
                ? X509CertificateLoader.LoadPkcs12(certBytes, null)
                : X509CertificateLoader.LoadPkcs12(certBytes, certPassword);

            using (cert)
            {
                var handler = new HttpClientHandler();
                handler.ClientCertificates.Add(cert);

                using var httpClient = new HttpClient(handler);
                var token = await SendTokenRequestAsync(httpClient, metadata.OAuth2TokenEndpoint, parameters, cacheKey, cancellationToken);

                headers["Authorization"] = $"Bearer {token}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire OAuth2 token via mTLS from '{TokenEndpoint}'.", metadata.OAuth2TokenEndpoint);
            throw;
        }
    }

    private static void BuildCustomHeaders(
        A2AConnectionMetadata metadata,
        Dictionary<string, string> headers)
    {
        if (metadata.AdditionalHeaders is null)
        {
            return;
        }

        foreach (var header in metadata.AdditionalHeaders)
        {
            headers[header.Key] = header.Value;
        }
    }

    private async Task<string> AcquireTokenAsync(
        string grantType,
        string tokenEndpoint,
        string clientId,
        string scopes,
        Dictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        var cacheKey = GetOAuth2CacheKey(grantType, tokenEndpoint, clientId, scopes);

        if (_cache.TryGetValue(cacheKey, out string cachedToken))
        {
            return cachedToken;
        }

        if (!string.IsNullOrWhiteSpace(scopes) && !parameters.ContainsKey("scope"))
        {
            parameters["scope"] = scopes;
        }

        using var httpClient = _httpClientFactory.CreateClient(nameof(DefaultA2AConnectionAuthService));

        return await SendTokenRequestAsync(httpClient, tokenEndpoint, parameters, cacheKey, cancellationToken);
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

    private static string GetOAuth2CacheKey(string grantType, string tokenEndpoint, string clientId, string scopes)
        => $"a2a_oauth2_{grantType}_{tokenEndpoint}_{clientId}_{scopes}";

    private string Unprotect(IDataProtector protector, string value)
    {
        try
        {
            return protector.Unprotect(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect a credential value for A2A connection.");
            return value;
        }
    }

    private sealed class OAuth2TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
