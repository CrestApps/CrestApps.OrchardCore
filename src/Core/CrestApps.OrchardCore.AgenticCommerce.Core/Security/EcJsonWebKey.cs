using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// An elliptic-curve JSON Web Key (RFC 7517, RFC 7518 section 6.2) on the P-256, P-384 or P-521 curve.
/// </summary>
public sealed class EcJsonWebKey
{
    /// <summary>
    /// Gets or sets the key ID (<c>kid</c>).
    /// </summary>
    public string Kid { get; set; }

    /// <summary>
    /// Gets or sets the key type (<c>kty</c>), always <c>EC</c>.
    /// </summary>
    public string Kty { get; set; } = "EC";

    /// <summary>
    /// Gets or sets the curve (<c>crv</c>): <c>P-256</c>, <c>P-384</c> or <c>P-521</c>.
    /// </summary>
    public string Crv { get; set; }

    /// <summary>
    /// Gets or sets the base64url x coordinate (<c>x</c>).
    /// </summary>
    public string X { get; set; }

    /// <summary>
    /// Gets or sets the base64url y coordinate (<c>y</c>).
    /// </summary>
    public string Y { get; set; }

    /// <summary>
    /// Gets or sets the base64url private key (<c>d</c>), or <see langword="null"/> for a public key.
    /// </summary>
    public string D { get; set; }

    /// <summary>
    /// Gets or sets the intended use (<c>use</c>), such as <c>sig</c>.
    /// </summary>
    public string Use { get; set; }

    /// <summary>
    /// Gets or sets the algorithm (<c>alg</c>), such as <c>ES256</c>.
    /// </summary>
    public string Alg { get; set; }

    /// <summary>
    /// Gets a value indicating whether the key holds the private part.
    /// </summary>
    public bool HasPrivateKey => !string.IsNullOrEmpty(D);

    /// <summary>
    /// Creates a JWK from an ECDSA key.
    /// </summary>
    /// <param name="key">The ECDSA key on the P-256, P-384 or P-521 curve.</param>
    /// <param name="kid">The key ID to give the JWK.</param>
    /// <param name="includePrivateKey">Whether to export the private part (<c>d</c>).</param>
    /// <returns>The JWK.</returns>
    public static EcJsonWebKey FromECDsa(ECDsa key, string kid, bool includePrivateKey)
    {
        ArgumentNullException.ThrowIfNull(key);

        var curve = JwsAlgorithms.GetCurveForKeySize(key.KeySize)
            ?? throw new NotSupportedException($"An ECDSA key of {key.KeySize} bits is not supported.");
        var parameters = key.ExportParameters(includePrivateKey);

        return new EcJsonWebKey
        {
            Kid = kid,
            Crv = curve,
            X = Base64Url.EncodeToString(parameters.Q.X),
            Y = Base64Url.EncodeToString(parameters.Q.Y),
            D = includePrivateKey && parameters.D is not null
                ? Base64Url.EncodeToString(parameters.D)
                : null,
        };
    }

    /// <summary>
    /// Reads a JWK from its JSON form.
    /// </summary>
    /// <param name="json">The JWK JSON object.</param>
    /// <returns>The key, or <see langword="null"/> when the JSON is not a valid EC key on a supported curve.</returns>
    public static EcJsonWebKey FromJson(JsonObject json)
    {
        if (json is null || JoseJson.GetString(json, "kty") != "EC")
        {
            return null;
        }

        var curve = JoseJson.GetString(json, "crv");
        var size = JwsAlgorithms.GetCoordinateSize(curve);

        if (size == 0)
        {
            return null;
        }

        var x = JoseJson.GetString(json, "x");
        var y = JoseJson.GetString(json, "y");
        var d = JoseJson.GetString(json, "d");

        if (!HasLength(x, size) || !HasLength(y, size) || (d is not null && !HasLength(d, size)))
        {
            return null;
        }

        return new EcJsonWebKey
        {
            Kid = JoseJson.GetString(json, "kid"),
            Crv = curve,
            X = x,
            Y = y,
            D = d,
            Use = JoseJson.GetString(json, "use"),
            Alg = JoseJson.GetString(json, "alg"),
        };
    }

    /// <summary>
    /// Writes the JWK as JSON.
    /// </summary>
    /// <param name="includePrivateKey">Whether to include the private part (<c>d</c>) when the key has one.</param>
    /// <returns>The JWK JSON object.</returns>
    public JsonObject ToJson(bool includePrivateKey = false)
    {
        var json = new JsonObject
        {
            ["kty"] = Kty ?? "EC",
            ["crv"] = Crv,
        };

        if (!string.IsNullOrEmpty(Kid))
        {
            json["kid"] = Kid;
        }

        if (!string.IsNullOrEmpty(Use))
        {
            json["use"] = Use;
        }

        if (!string.IsNullOrEmpty(Alg))
        {
            json["alg"] = Alg;
        }

        json["x"] = X;
        json["y"] = Y;

        if (includePrivateKey && HasPrivateKey)
        {
            json["d"] = D;
        }

        return json;
    }

    /// <summary>
    /// Creates an ECDSA key from the JWK. The caller owns and disposes the returned key.
    /// </summary>
    /// <returns>The ECDSA key, with the private part when the JWK has one.</returns>
    public ECDsa CreateECDsa()
    {
        var size = JwsAlgorithms.GetCoordinateSize(Crv);

        if (size == 0)
        {
            throw new NotSupportedException($"The curve '{Crv}' is not supported.");
        }

        var parameters = new ECParameters
        {
            Curve = JwsAlgorithms.GetNamedCurve(Crv),
            Q = new ECPoint
            {
                X = Base64Url.DecodeFromChars(X),
                Y = Base64Url.DecodeFromChars(Y),
            },
            D = HasPrivateKey
                ? Base64Url.DecodeFromChars(D)
                : null,
        };

        return ECDsa.Create(parameters);
    }

    /// <summary>
    /// Computes the RFC 7638 JWK thumbprint: the base64url SHA-256 of the required members in lexicographic order.
    /// </summary>
    /// <returns>The base64url thumbprint.</returns>
    public string ComputeThumbprint()
    {
        var canonical = $"{{\"crv\":\"{Crv}\",\"kty\":\"EC\",\"x\":\"{X}\",\"y\":\"{Y}\"}}";

        return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// Gets the JWS algorithm of the key: <see cref="Alg"/> when it is set, otherwise ES256, ES384 or ES512 by curve.
    /// </summary>
    /// <returns>The algorithm name, or <see langword="null"/> when the curve is not supported.</returns>
    public string GetAlgorithm()
    {
        return !string.IsNullOrEmpty(Alg)
            ? Alg
            : JwsAlgorithms.GetAlgorithmForCurve(Crv);
    }

    private static bool HasLength(string value, int size)
    {
        return JoseJson.TryDecodeBase64Url(value, out var bytes) && bytes.Length == size;
    }
}
