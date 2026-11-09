using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.Webhooks.Core.Signatures;

/// <summary>
/// Validates an HMAC signature over the signed content, keyed with the shared secret. The algorithm, the encoding of
/// the digest and an optional prefix (<c>v1=</c>, <c>hmac-sha256-hex=</c>) describe a provider's scheme.
/// </summary>
public sealed class HmacWebhookSignatureValidator : IWebhookSignatureValidator
{
    /// <summary>
    /// Validates HMAC-SHA256 signatures sent as lowercase hex, with no prefix.
    /// </summary>
    public static readonly HmacWebhookSignatureValidator Sha256Hex = new("hmac-sha256-hex", HashAlgorithmName.SHA256, WebhookSignatureEncoding.Hex);

    /// <summary>
    /// Validates HMAC-SHA1 signatures sent as base64, the scheme of Twilio request validation.
    /// </summary>
    public static readonly HmacWebhookSignatureValidator Sha1Base64 = new("hmac-sha1-base64", HashAlgorithmName.SHA1, WebhookSignatureEncoding.Base64);

    private readonly HashAlgorithmName _algorithm;
    private readonly WebhookSignatureEncoding _encoding;
    private readonly string _prefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="HmacWebhookSignatureValidator"/> class.
    /// </summary>
    /// <param name="scheme">The stable name of the scheme.</param>
    /// <param name="algorithm">The hash algorithm: SHA1, SHA256, SHA384 or SHA512.</param>
    /// <param name="encoding">How the digest is written in the signature.</param>
    /// <param name="prefix">A prefix the signature starts with, compared without regard to case, or <see langword="null"/>.</param>
    public HmacWebhookSignatureValidator(string scheme, HashAlgorithmName algorithm, WebhookSignatureEncoding encoding, string prefix = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheme);

        if (algorithm != HashAlgorithmName.SHA1 && algorithm != HashAlgorithmName.SHA256 &&
            algorithm != HashAlgorithmName.SHA384 && algorithm != HashAlgorithmName.SHA512)
        {
            throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Only SHA1, SHA256, SHA384 and SHA512 are supported.");
        }

        Scheme = scheme;
        _algorithm = algorithm;
        _encoding = encoding;
        _prefix = prefix;
    }

    /// <inheritdoc/>
    public string Scheme { get; }

    /// <summary>
    /// Computes the signature a sender using this scheme puts on the content.
    /// </summary>
    /// <param name="secret">The shared secret.</param>
    /// <param name="signedContent">The content that is signed.</param>
    /// <returns>The signature, prefix included.</returns>
    public string Compute(string secret, string signedContent)
    {
        ArgumentNullException.ThrowIfNull(secret);

        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(signedContent ?? string.Empty);
        byte[] digest;

        if (_algorithm == HashAlgorithmName.SHA1)
        {
            // SHA1 is only ever chosen to match a provider that still signs with it (Twilio); it is not used to protect
            // anything this site produces.
#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            digest = HMACSHA1.HashData(key, data);
#pragma warning restore CA5350 // Do Not Use Weak Cryptographic Algorithms
        }
        else if (_algorithm == HashAlgorithmName.SHA384)
        {
            digest = HMACSHA384.HashData(key, data);
        }
        else if (_algorithm == HashAlgorithmName.SHA512)
        {
            digest = HMACSHA512.HashData(key, data);
        }
        else
        {
            digest = HMACSHA256.HashData(key, data);
        }

        var encoded = _encoding == WebhookSignatureEncoding.Base64 ? Convert.ToBase64String(digest) : Convert.ToHexStringLower(digest);

        return _prefix + encoded;
    }

    /// <inheritdoc/>
    public bool IsValid(WebhookSignatureValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrEmpty(context.Secret) || string.IsNullOrEmpty(context.Signature))
        {
            return false;
        }

        var signature = context.Signature.Trim();

        if (!string.IsNullOrEmpty(_prefix))
        {
            if (!signature.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            signature = _prefix + signature[_prefix.Length..];
        }

        if (_encoding == WebhookSignatureEncoding.Hex)
        {
            signature = signature.ToLowerInvariant();
        }

        return WebhookSignatures.FixedTimeEquals(Compute(context.Secret, context.SignedContent), signature);
    }
}
