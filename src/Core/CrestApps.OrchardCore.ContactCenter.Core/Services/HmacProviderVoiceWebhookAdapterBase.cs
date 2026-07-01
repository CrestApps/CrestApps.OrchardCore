using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides a reusable base for provider webhook adapters that authenticate deliveries with an
/// HMAC-SHA256 signature computed over the raw request body. Subclasses supply the shared secret, the
/// signature header name, and the payload parsing.
/// </summary>
public abstract class HmacProviderVoiceWebhookAdapterBase : IProviderVoiceWebhookAdapter
{
    /// <inheritdoc/>
    public abstract string TechnicalName { get; }

    /// <summary>
    /// Gets the name of the header that carries the provider signature.
    /// </summary>
    protected abstract string SignatureHeaderName { get; }

    /// <summary>
    /// Gets the shared secret used to compute the expected signature, or <see langword="null"/> when the
    /// provider is not configured.
    /// </summary>
    protected abstract string SigningSecret { get; }

    /// <inheritdoc/>
    public virtual bool ValidateSignature(ProviderVoiceWebhookRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var secret = SigningSecret;

        if (string.IsNullOrEmpty(secret))
        {
            return false;
        }

        if (!request.Headers.TryGetValue(SignatureHeaderName, out var providedSignature) || string.IsNullOrEmpty(providedSignature))
        {
            return false;
        }

        var expected = ComputeSignature(secret, request.Body ?? string.Empty);

        var providedBytes = DecodeSignature(providedSignature);
        var expectedBytes = DecodeSignature(expected);

        if (providedBytes is null || expectedBytes is null || providedBytes.Length != expectedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    /// <inheritdoc/>
    public abstract IReadOnlyList<ProviderVoiceEvent> Parse(ProviderVoiceWebhookRequest request);

    /// <summary>
    /// Computes the lowercase hexadecimal HMAC-SHA256 signature of the payload using the supplied secret.
    /// </summary>
    /// <param name="secret">The shared signing secret.</param>
    /// <param name="payload">The raw payload to sign.</param>
    /// <returns>The lowercase hexadecimal signature.</returns>
    protected static string ComputeSignature(string secret, string payload)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(payload);

        var key = Encoding.UTF8.GetBytes(secret);
        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexStringLower(hash);
    }

    private static byte[] DecodeSignature(string signature)
    {
        var value = signature.Trim();

        var separatorIndex = value.IndexOf('=');

        if (separatorIndex >= 0 && separatorIndex < value.Length - 1)
        {
            value = value[(separatorIndex + 1)..];
        }

        try
        {
            return Convert.FromHexString(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
