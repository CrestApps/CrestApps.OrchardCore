namespace CrestApps.OrchardCore.AI.Mcp.Core.Services;

public interface IOAuth2TokenService
{
    /// <summary>
    /// Acquires an access token using the OAuth 2.0 client credentials grant.
    /// </summary>
    Task<string> AcquireTokenAsync(string tokenEndpoint, string clientId, string clientSecret, string scopes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires an access token using the OAuth 2.0 client credentials grant with a private key JWT client assertion.
    /// </summary>
    Task<string> AcquireTokenWithPrivateKeyJwtAsync(string tokenEndpoint, string clientId, string privateKeyPem, string keyId = null, string scopes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires an access token using the OAuth 2.0 client credentials grant with mutual TLS (mTLS) client authentication.
    /// </summary>
    Task<string> AcquireTokenWithMtlsAsync(string tokenEndpoint, string clientId, byte[] clientCertificateBytes, string certificatePassword = null, string scopes = null, CancellationToken cancellationToken = default);
}
