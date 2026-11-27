using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.AI.Core.Services;

/// <summary>
/// The OAuth2 token service of the AI connections. The client-secret and private-key grants go through the package's
/// <see cref="DefaultOAuth2TokenService"/>, whose named client <see cref="AIEgress.AddAIEgressGuards"/> guards. The
/// package sends the mTLS grant through a bare handler of its own, which reaches any address and follows redirects, so
/// that grant is sent here instead, through the egress guard with the client certificate (review 11, C13).
/// </summary>
internal sealed class EgressGuardedOAuth2TokenService : IOAuth2TokenService
{
    // The package's own expiry rules.
    private const int ExpirationBufferSeconds = 60;

    private readonly DefaultOAuth2TokenService _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;
    private readonly bool _isDevelopment;
    private readonly bool _allowPrivateNetwork;
    private readonly IShellConfiguration _shellConfiguration;

    public EgressGuardedOAuth2TokenService(
        DefaultOAuth2TokenService inner,
        IMemoryCache cache,
        IOptions<AIEgressOptions> options,
        ILogger<EgressGuardedOAuth2TokenService> logger,
        IHostEnvironment hostEnvironment = null,
        IShellConfiguration shellConfiguration = null)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
        _isDevelopment = hostEnvironment?.IsDevelopment() == true;
        _allowPrivateNetwork = options.Value.AllowPrivateNetwork;
        _shellConfiguration = shellConfiguration;
    }

    public Task<string> AcquireTokenAsync(string tokenEndpoint, string clientId, string clientSecret, string scopes = null, CancellationToken cancellationToken = default)
        => _inner.AcquireTokenAsync(tokenEndpoint, clientId, clientSecret, scopes, cancellationToken);

    public Task<string> AcquireTokenWithPrivateKeyJwtAsync(string tokenEndpoint, string clientId, string privateKeyPem, string keyId = null, string scopes = null, CancellationToken cancellationToken = default)
        => _inner.AcquireTokenWithPrivateKeyJwtAsync(tokenEndpoint, clientId, privateKeyPem, keyId, scopes, cancellationToken);

    public async Task<string> AcquireTokenWithMtlsAsync(string tokenEndpoint, string clientId, byte[] clientCertificateBytes, string certificatePassword = null, string scopes = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(tokenEndpoint);
        ArgumentException.ThrowIfNullOrEmpty(clientId);
        ArgumentNullException.ThrowIfNull(clientCertificateBytes);

        var cacheKey = GetMtlsCacheKey(tokenEndpoint, clientId, scopes, clientCertificateBytes, certificatePassword);

        if (_cache.TryGetValue<string>(cacheKey, out var cached))
        {
            return cached;
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

        using var certificate = X509CertificateLoader.LoadPkcs12(clientCertificateBytes, string.IsNullOrEmpty(certificatePassword) ? null : certificatePassword);

        // Every address is checked at connect, and a redirect is followed only as the other guarded clients follow it:
        // a redirect that would resend the form to another origin is not followed, so the certificate and the request
        // reach only the endpoint the connection names.
        var handler = EgressNetwork.CreateHandler(_isDevelopment, _allowPrivateNetwork, allowRedirects: false, EgressNetwork.GetProxy(_shellConfiguration));
        handler.SslOptions.ClientCertificates = [certificate];

        using var client = new HttpMessageInvoker(new CrossOriginRedirectHandler { InnerHandler = handler });
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(parameters),
        };

        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("An OAuth2 mTLS token request failed with status {StatusCode}.", response.StatusCode);

            throw new HttpRequestException($"OAuth2 token request failed with status {response.StatusCode}.");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

        if (string.IsNullOrEmpty(token?.AccessToken))
        {
            throw new InvalidOperationException("OAuth2 token response did not contain an access token.");
        }

        var lifetime = token.ExpiresIn > ExpirationBufferSeconds
            ? TimeSpan.FromSeconds(token.ExpiresIn - ExpirationBufferSeconds)
            : TimeSpan.FromMinutes(5);

        _cache.Set(cacheKey, token.AccessToken, lifetime);

        return token.AccessToken;
    }

    /// <summary>
    /// The cache key of an mTLS token: the endpoint, the client and the scopes, and a hash of the certificate and its
    /// password. The memory cache is shared by every tenant, and the endpoint and client are often public, so a key
    /// without the credential would hand one connection's token to another that names the same client with any
    /// certificate (review 3).
    /// </summary>
    /// <param name="tokenEndpoint">The token endpoint.</param>
    /// <param name="clientId">The client.</param>
    /// <param name="scopes">The scopes.</param>
    /// <param name="clientCertificateBytes">The client certificate.</param>
    /// <param name="certificatePassword">The certificate's password.</param>
    /// <returns>The key.</returns>
    internal static string GetMtlsCacheKey(string tokenEndpoint, string clientId, string scopes, byte[] clientCertificateBytes, string certificatePassword)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(clientCertificateBytes);
        hash.AppendData([0]);
        hash.AppendData(Encoding.UTF8.GetBytes(certificatePassword ?? string.Empty));

        return $"oauth2_mtls_{tokenEndpoint}_{clientId}_{scopes}_{Convert.ToHexString(hash.GetHashAndReset())}";
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
