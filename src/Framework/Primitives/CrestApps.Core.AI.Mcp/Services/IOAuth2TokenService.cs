namespace CrestApps.Core.AI.Mcp.Services;

/// <summary>
/// Acquires OAuth 2.0 access tokens using various client authentication methods,
/// including client credentials, private key JWT assertions, and mutual TLS.
/// </summary>
public interface IOAuth2TokenService
{
    /// <summary>
    /// Acquires an access token using the OAuth 2.0 client credentials grant.
    /// </summary>
    /// <param name="tokenEndpoint">The URL of the token endpoint.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="clientSecret">The client secret.</param>
    /// <param name="scopes">Optional space-delimited scopes to request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The acquired access token string.</returns>
    Task<string> AcquireTokenAsync(string tokenEndpoint, string clientId, string clientSecret, string scopes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires an access token using the OAuth 2.0 client credentials grant with a private key JWT client assertion.
    /// </summary>
    /// <param name="tokenEndpoint">The URL of the token endpoint.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="privateKeyPem">The PEM-encoded private key used to sign the JWT assertion.</param>
    /// <param name="keyId">Optional key identifier to include in the JWT header.</param>
    /// <param name="scopes">Optional space-delimited scopes to request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The acquired access token string.</returns>
    Task<string> AcquireTokenWithPrivateKeyJwtAsync(string tokenEndpoint, string clientId, string privateKeyPem, string keyId = null, string scopes = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires an access token using the OAuth 2.0 client credentials grant with mutual TLS (mTLS) client authentication.
    /// </summary>
    /// <param name="tokenEndpoint">The URL of the token endpoint.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="clientCertificateBytes">The raw bytes of the client certificate.</param>
    /// <param name="certificatePassword">Optional password for the certificate.</param>
    /// <param name="scopes">Optional space-delimited scopes to request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The acquired access token string.</returns>
    Task<string> AcquireTokenWithMtlsAsync(string tokenEndpoint, string clientId, byte[] clientCertificateBytes, string certificatePassword = null, string scopes = null, CancellationToken cancellationToken = default);
}
