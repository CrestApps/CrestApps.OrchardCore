using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.SigningKeys;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// The credentials key set of the shared signing keys: an RS256 key in Orchard Core Secrets that signs credentials and
/// status lists, rotated yearly. A retired key stays published for ten years, so credentials it signed keep verifying.
/// </summary>
public sealed class CredentialSigningKeys
{
    /// <summary>
    /// How long a key signs before a new one replaces it.
    /// </summary>
    public static readonly TimeSpan RotationAge = TimeSpan.FromDays(365);

    /// <summary>
    /// How long a retired key stays published.
    /// </summary>
    public static readonly TimeSpan RetiredLifetime = TimeSpan.FromDays(3650);

    private readonly ISigningKeyRing _ring;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialSigningKeys"/> class.
    /// </summary>
    /// <param name="ring">The shared signing keys.</param>
    public CredentialSigningKeys(ISigningKeyRing ring)
    {
        _ring = ring;
    }

    /// <summary>
    /// Configures the key set.
    /// </summary>
    /// <param name="options">The key set options.</param>
    public static void Configure(SigningKeySetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Algorithm = SigningKeyAlgorithms.RS256;
        options.RotationAge = RotationAge;
        options.RetiredLifetime = RetiredLifetime;
        options.KeyIdPrefix = "credentials";
        options.Description = "Digital credentials: the key that signs Open Badges credentials and status lists. Made and rotated by the site; do not edit.";
    }

    /// <summary>
    /// Signs a JWT payload with the current key. The <c>kid</c> is the address of the public key.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <param name="baseUrl">The site's base address, without a trailing slash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The compact JWS.</returns>
    public async Task<string> SignAsync(JsonObject payload, string baseUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var key = await _ring.GetSigningKeyAsync(CredentialsConstants.KeySet, cancellationToken);

        return CredentialJws.Sign(payload, key.PrivateJwk, CredentialUrls.Key(baseUrl, key.Kid));
    }

    /// <summary>
    /// Gets the published public keys.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The keys as JSON Web Keys.</returns>
    public Task<IReadOnlyList<JsonObject>> GetPublishedKeysAsync(CancellationToken cancellationToken = default)
        => _ring.GetPublishedKeysAsync(CredentialsConstants.KeySet, cancellationToken);

    /// <summary>
    /// Finds a published public key.
    /// </summary>
    /// <param name="kid">The key id, or the address of the key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The key, or <see langword="null"/>.</returns>
    public async Task<JsonObject> FindPublishedKeyAsync(string kid, CancellationToken cancellationToken = default)
    {
        var keyId = CredentialUrls.KeyIdOf(kid);

        if (string.IsNullOrEmpty(keyId))
        {
            return null;
        }

        return (await GetPublishedKeysAsync(cancellationToken))
            .FirstOrDefault(key => string.Equals(key["kid"]?.GetValue<string>(), keyId, StringComparison.Ordinal));
    }
}
