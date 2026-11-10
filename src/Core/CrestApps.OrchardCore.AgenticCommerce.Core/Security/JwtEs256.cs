using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Creates and reads ES256 JSON Web Tokens with the header <c>{"alg":"ES256","typ":"JWT","kid":...}</c>.
/// </summary>
public static class JwtEs256
{
    /// <summary>
    /// Creates a compact ES256 JWT.
    /// </summary>
    /// <param name="claims">The claims (the JWT payload).</param>
    /// <param name="privateKey">The P-256 private key; its <see cref="EcJsonWebKey.Kid"/> becomes the <c>kid</c> header.</param>
    /// <returns>The compact JWT.</returns>
    public static string Create(JsonObject claims, EcJsonWebKey privateKey)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(privateKey);

        if (privateKey.Crv != JwsAlgorithms.CurveP256 || !privateKey.HasPrivateKey)
        {
            throw new ArgumentException("An ES256 JWT needs a P-256 private key.", nameof(privateKey));
        }

        var header = new JsonObject
        {
            ["alg"] = JwsAlgorithms.ES256,
            ["typ"] = "JWT",
        };

        if (!string.IsNullOrEmpty(privateKey.Kid))
        {
            header["kid"] = privateKey.Kid;
        }

        using var key = privateKey.CreateECDsa();

        return JwsSigner.SignCompact(header, JoseJson.SerializeToUtf8(claims), key);
    }

    /// <summary>
    /// Verifies a compact JWT signed with ES256, ES384 or ES512 and reads its claims.
    /// </summary>
    /// <param name="jwt">The compact JWT.</param>
    /// <param name="keyResolver">Returns the public key for the decoded header (usually by <c>kid</c>), or <see langword="null"/> when no key is known.</param>
    /// <param name="header">The decoded header when the JWT verifies.</param>
    /// <param name="claims">The decoded claims when the JWT verifies.</param>
    /// <returns><see langword="true"/> when the signature verifies and the payload is a JSON object.</returns>
    public static bool TryVerify(string jwt, Func<JsonObject, EcJsonWebKey> keyResolver, out JsonObject header, out JsonObject claims)
    {
        header = null;
        claims = null;

        if (keyResolver is null)
        {
            return false;
        }

        ECDsa key = null;

        try
        {
            if (!JwsSigner.TryVerifyCompact(
                jwt,
                h =>
                {
                    var jwk = keyResolver(h);

                    if (jwk is null || JwsAlgorithms.GetAlgorithmForCurve(jwk.Crv) != JoseJson.GetString(h, "alg"))
                    {
                        return null;
                    }

                    key = jwk.CreateECDsa();

                    return key;
                },
                out var verifiedHeader,
                out var payload))
            {
                return false;
            }

            if (!JoseJson.TryParseObject(payload, out var verifiedClaims))
            {
                return false;
            }

            header = verifiedHeader;
            claims = verifiedClaims;

            return true;
        }
        finally
        {
            key?.Dispose();
        }
    }
}
