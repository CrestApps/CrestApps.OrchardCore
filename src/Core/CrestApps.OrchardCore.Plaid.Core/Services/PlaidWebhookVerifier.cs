using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Plaid.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Checks that a webhook came from Plaid: its <c>Plaid-Verification</c> header is a JWT signed with ES256 by a key Plaid
/// publishes, issued no more than five minutes ago, over the SHA-256 of the exact body.
/// </summary>
public sealed class PlaidWebhookVerifier
{
    /// <summary>
    /// How old a signature may be.
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    private readonly PlaidClient _client;
    private readonly IMemoryCache _cache;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaidWebhookVerifier"/> class.
    /// </summary>
    /// <param name="client">The Plaid client, to read signing keys.</param>
    /// <param name="cache">The cache of signing keys.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public PlaidWebhookVerifier(PlaidClient client, IMemoryCache cache, IClock clock, ILogger<PlaidWebhookVerifier> logger)
    {
        _client = client;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Returns whether a webhook is signed by Plaid.
    /// </summary>
    /// <param name="credentials">The credentials, to read the signing key.</param>
    /// <param name="signatureHeader">The <c>Plaid-Verification</c> header.</param>
    /// <param name="body">The exact body, as received.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the signature, its age and the body's hash are right.</returns>
    public async Task<bool> VerifyAsync(PlaidCredentials credentials, string signatureHeader, byte[] body, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(body);

        if (string.IsNullOrEmpty(signatureHeader))
        {
            return Refuse("no signature header");
        }

        var parts = signatureHeader.Split('.');

        if (parts.Length != 3)
        {
            return Refuse("the signature is not a JWT");
        }

        string keyId;

        try
        {
            using var header = JsonDocument.Parse(Base64UrlDecode(parts[0]));

            if (!header.RootElement.TryGetProperty("alg", out var alg) || alg.GetString() != "ES256")
            {
                return Refuse("the signature's algorithm is not ES256");
            }

            if (!header.RootElement.TryGetProperty("kid", out var kid) || string.IsNullOrEmpty(keyId = kid.GetString()))
            {
                return Refuse("the signature names no key");
            }
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
        {
            return Refuse("the signature's header can't be read");
        }

        var key = await GetKeyAsync(credentials, keyId, cancellationToken);

        if (key is null)
        {
            return Refuse("the signing key is unknown");
        }

        if (key.ExpiredAt is { } expiredAt && DateTimeOffset.FromUnixTimeSeconds(expiredAt) <= _clock.UtcNow)
        {
            return Refuse("the signing key has expired");
        }

        try
        {
            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = Base64UrlDecode(key.X),
                    Y = Base64UrlDecode(key.Y),
                },
            });

            var signedData = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);

            // A JWT's ES256 signature is r and s side by side (IEEE P1363), which is .NET's default format.
            if (!ecdsa.VerifyData(signedData, Base64UrlDecode(parts[2]), HashAlgorithmName.SHA256))
            {
                return Refuse("the signature does not match");
            }

            using var claims = JsonDocument.Parse(Base64UrlDecode(parts[1]));

            if (!claims.RootElement.TryGetProperty("iat", out var iat) || !iat.TryGetInt64(out var issuedAt))
            {
                return Refuse("the signature has no issue time");
            }

            var age = _clock.UtcNow - DateTimeOffset.FromUnixTimeSeconds(issuedAt).UtcDateTime;

            if (age > MaxAge || age < -MaxAge)
            {
                return Refuse("the signature is too old");
            }

            if (!claims.RootElement.TryGetProperty("request_body_sha256", out var hashClaim) || hashClaim.GetString() is not { } expectedHash)
            {
                return Refuse("the signature has no body hash");
            }

            var actualHash = Convert.ToHexStringLower(SHA256.HashData(body));

            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actualHash), Encoding.ASCII.GetBytes(expectedHash.ToLowerInvariant())))
            {
                return Refuse("the body's hash does not match");
            }
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException or InvalidOperationException)
        {
            return Refuse("the signature can't be checked");
        }

        return true;
    }

    private async Task<PlaidWebhookVerificationKey> GetKeyAsync(PlaidCredentials credentials, string keyId, CancellationToken cancellationToken)
    {
        var cacheKey = $"CrestApps.Plaid.WebhookKey:{credentials.Environment}:{credentials.ClientId}:{keyId}";

        if (_cache.TryGetValue(cacheKey, out PlaidWebhookVerificationKey cached))
        {
            return cached;
        }

        PlaidWebhookVerificationKey key;

        try
        {
            key = await _client.GetWebhookVerificationKeyAsync(credentials, keyId, cancellationToken);
        }
        catch (PlaidApiException exception)
        {
            _logger.LogWarning("Plaid refused to give the webhook signing key {KeyId}: {Error}", keyId.SanitizeLogValue(), exception.Message);

            return null;
        }

        if (key is not null && key.ExpiredAt is null)
        {
            _cache.Set(cacheKey, key, TimeSpan.FromHours(24));
        }

        return key;
    }

    private bool Refuse(string reason)
    {
        _logger.LogWarning("A Plaid webhook was refused: {Reason}.", reason);

        return false;
    }

    private static byte[] Base64UrlDecode(string value)
        => System.Buffers.Text.Base64Url.DecodeFromChars(value);
}
