using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Security;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Ap2;

/// <summary>
/// The merchant authorization of the UCP AP2 Mandates extension (<c>ap2.merchant_authorization</c>): a JWS with
/// detached content over the JCS (RFC 8785) form of the checkout without its top-level <c>ap2</c> member.
/// </summary>
public static class Ap2CheckoutAuthorization
{
    private const string Ap2Member = "ap2";

    /// <summary>
    /// Signs a checkout and returns its merchant authorization, <c>b64url(header)..b64url(signature)</c>, with the
    /// header <c>{"alg":...,"kid":...}</c>.
    /// </summary>
    /// <param name="checkout">The checkout response object. Its top-level <c>ap2</c> member is not signed.</param>
    /// <param name="key">The merchant's private signing key; its <see cref="EcJsonWebKey.Kid"/> becomes the <c>kid</c> header.</param>
    /// <returns>The detached JWS.</returns>
    public static string CreateMerchantAuthorization(JsonObject checkout, EcJsonWebKey key)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrEmpty(key.Kid, nameof(key));

        var header = new JsonObject
        {
            ["alg"] = JwsAlgorithms.GetAlgorithmForCurve(key.Crv),
            ["kid"] = key.Kid,
        };

        using var ecdsa = key.CreateECDsa();

        return JwsSigner.SignDetached(header, GetSigningPayload(checkout), ecdsa);
    }

    /// <summary>
    /// Verifies the merchant authorization of a checkout.
    /// </summary>
    /// <param name="checkout">The checkout object. Its top-level <c>ap2</c> member is ignored.</param>
    /// <param name="merchantAuthorization">The detached JWS.</param>
    /// <param name="keyResolver">Returns the merchant's public key for a <c>kid</c>, or <see langword="null"/> when no key is known.</param>
    /// <returns><see langword="true"/> when the header carries <c>alg</c> and <c>kid</c>, a key is known and the signature verifies.</returns>
    public static bool VerifyMerchantAuthorization(JsonObject checkout, string merchantAuthorization, Func<string, EcJsonWebKey> keyResolver)
    {
        if (checkout is null || string.IsNullOrEmpty(merchantAuthorization) || keyResolver is null)
        {
            return false;
        }

        ECDsa ecdsa = null;

        try
        {
            return JwsSigner.VerifyDetached(
                merchantAuthorization,
                GetSigningPayload(checkout),
                header =>
                {
                    var kid = JoseJson.GetString(header, "kid");
                    var jwk = string.IsNullOrEmpty(kid)
                        ? null
                        : keyResolver(kid);

                    if (jwk is null || JwsAlgorithms.GetAlgorithmForCurve(jwk.Crv) != JoseJson.GetString(header, "alg"))
                    {
                        return null;
                    }

                    ecdsa = jwk.CreateECDsa();

                    return ecdsa;
                },
                out _);
        }
        catch (CryptographicException)
        {
            return false;
        }
        finally
        {
            ecdsa?.Dispose();
        }
    }

    /// <summary>
    /// Turns a checkout and its merchant authorization into a compact checkout JWT for an AP2 mandate:
    /// <c>header + "." + b64url(JCS(checkout without ap2)) + "." + signature</c>, a valid compact JWS made from the
    /// same signature.
    /// </summary>
    /// <param name="checkout">The checkout object. Its top-level <c>ap2</c> member is left out.</param>
    /// <param name="merchantAuthorization">The detached JWS over the checkout.</param>
    /// <returns>The compact checkout JWT.</returns>
    public static string ToCompactCheckoutJwt(JsonObject checkout, string merchantAuthorization)
    {
        ArgumentNullException.ThrowIfNull(checkout);
        ArgumentException.ThrowIfNullOrEmpty(merchantAuthorization);

        var parts = merchantAuthorization.Split('.');

        if (parts.Length != 3 || parts[1].Length != 0)
        {
            throw new ArgumentException("The merchant authorization is not a detached JWS.", nameof(merchantAuthorization));
        }

        return parts[0] + "." + Base64Url.EncodeToString(GetSigningPayload(checkout)) + "." + parts[2];
    }

    private static byte[] GetSigningPayload(JsonObject checkout)
    {
        var copy = checkout.DeepClone().AsObject();
        copy.Remove(Ap2Member);

        return JsonCanonicalizer.CanonicalizeToUtf8(copy);
    }
}
