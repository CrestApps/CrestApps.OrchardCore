using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Signs and verifies JSON Web Signatures (RFC 7515) with ES256, ES384 and ES512, in the compact serialization and
/// with detached content (RFC 7515 Appendix F). Signatures are the raw <c>r||s</c> form (IEEE P1363) JWS requires.
/// </summary>
public static class JwsSigner
{
    /// <summary>
    /// Signs a payload as a compact JWS: <c>b64url(header).b64url(payload).b64url(signature)</c>.
    /// </summary>
    /// <param name="header">The protected header. It must carry an <c>alg</c> that matches the key's curve.</param>
    /// <param name="payload">The payload bytes.</param>
    /// <param name="key">The private ECDSA key.</param>
    /// <returns>The compact JWS.</returns>
    public static string SignCompact(JsonObject header, ReadOnlySpan<byte> payload, ECDsa key)
    {
        var headerSegment = EncodeHeader(header, key, out var hashAlgorithm);
        var payloadSegment = Base64Url.EncodeToString(payload);
        var signature = key.SignData(Encoding.ASCII.GetBytes(headerSegment + "." + payloadSegment), hashAlgorithm);

        return headerSegment + "." + payloadSegment + "." + Base64Url.EncodeToString(signature);
    }

    /// <summary>
    /// Signs a payload as a JWS with detached content: <c>b64url(header)..b64url(signature)</c>.
    /// </summary>
    /// <param name="header">The protected header. It must carry an <c>alg</c> that matches the key's curve.</param>
    /// <param name="payload">The payload bytes, which are signed but not included.</param>
    /// <param name="key">The private ECDSA key.</param>
    /// <returns>The detached JWS.</returns>
    public static string SignDetached(JsonObject header, ReadOnlySpan<byte> payload, ECDsa key)
    {
        var headerSegment = EncodeHeader(header, key, out var hashAlgorithm);
        var payloadSegment = Base64Url.EncodeToString(payload);
        var signature = key.SignData(Encoding.ASCII.GetBytes(headerSegment + "." + payloadSegment), hashAlgorithm);

        return headerSegment + ".." + Base64Url.EncodeToString(signature);
    }

    /// <summary>
    /// Verifies a compact JWS.
    /// </summary>
    /// <param name="jws">The compact JWS.</param>
    /// <param name="keyResolver">Returns the public key for the decoded header, or <see langword="null"/> when no key is known. The caller owns the returned key.</param>
    /// <param name="header">The decoded protected header when the signature is valid.</param>
    /// <param name="payload">The decoded payload when the signature is valid.</param>
    /// <returns><see langword="true"/> when the JWS is well formed, uses a supported algorithm and the signature verifies.</returns>
    public static bool TryVerifyCompact(string jws, Func<JsonObject, ECDsa> keyResolver, out JsonObject header, out byte[] payload)
    {
        header = null;
        payload = null;

        if (string.IsNullOrEmpty(jws) || keyResolver is null)
        {
            return false;
        }

        var parts = jws.Split('.');

        if (parts.Length != 3 || !JoseJson.TryDecodeBase64Url(parts[1], out var payloadBytes))
        {
            return false;
        }

        if (!TryVerify(parts[0], parts[1], parts[2], keyResolver, out var verifiedHeader))
        {
            return false;
        }

        header = verifiedHeader;
        payload = payloadBytes;

        return true;
    }

    /// <summary>
    /// Verifies a JWS with detached content against the payload it was computed over.
    /// </summary>
    /// <param name="detachedJws">The detached JWS, <c>b64url(header)..b64url(signature)</c>.</param>
    /// <param name="payload">The detached payload bytes.</param>
    /// <param name="keyResolver">Returns the public key for the decoded header, or <see langword="null"/> when no key is known. The caller owns the returned key.</param>
    /// <param name="header">The decoded protected header when the signature is valid.</param>
    /// <returns><see langword="true"/> when the JWS is well formed, uses a supported algorithm and the signature verifies.</returns>
    public static bool VerifyDetached(string detachedJws, ReadOnlySpan<byte> payload, Func<JsonObject, ECDsa> keyResolver, out JsonObject header)
    {
        header = null;

        if (string.IsNullOrEmpty(detachedJws) || keyResolver is null)
        {
            return false;
        }

        var parts = detachedJws.Split('.');

        if (parts.Length != 3 || parts[1].Length != 0)
        {
            return false;
        }

        return TryVerify(parts[0], Base64Url.EncodeToString(payload), parts[2], keyResolver, out header);
    }

    internal static bool TryVerify(
        string headerSegment,
        string payloadSegment,
        string signatureSegment,
        Func<JsonObject, ECDsa> keyResolver,
        out JsonObject header)
    {
        header = null;

        if (!JoseJson.TryDecodeObjectSegment(headerSegment, out var decodedHeader)
            || !JoseJson.TryDecodeBase64Url(signatureSegment, out var signature))
        {
            return false;
        }

        if (decodedHeader.ContainsKey("crit") || decodedHeader["b64"] is JsonValue)
        {
            return false;
        }

        var alg = JoseJson.GetString(decodedHeader, "alg");

        if (!JwsAlgorithms.TryGetAlgorithm(alg, out var hashAlgorithm, out var keySize))
        {
            return false;
        }

        var key = keyResolver(decodedHeader);

        if (key is null || key.KeySize != keySize || signature.Length != JwsAlgorithms.GetSignatureSize(keySize))
        {
            return false;
        }

        try
        {
            if (!key.VerifyData(Encoding.ASCII.GetBytes(headerSegment + "." + payloadSegment), signature, hashAlgorithm))
            {
                return false;
            }
        }
        catch (CryptographicException)
        {
            return false;
        }

        header = decodedHeader;

        return true;
    }

    private static string EncodeHeader(JsonObject header, ECDsa key, out HashAlgorithmName hashAlgorithm)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(key);

        var alg = JoseJson.GetString(header, "alg");

        if (!JwsAlgorithms.TryGetAlgorithm(alg, out hashAlgorithm, out var keySize))
        {
            throw new ArgumentException($"The JWS header must carry a supported 'alg' (ES256, ES384 or ES512), not '{alg}'.", nameof(header));
        }

        if (key.KeySize != keySize)
        {
            throw new ArgumentException($"The algorithm '{alg}' does not match a {key.KeySize}-bit key.", nameof(key));
        }

        return JoseJson.EncodeSegment(header);
    }
}
