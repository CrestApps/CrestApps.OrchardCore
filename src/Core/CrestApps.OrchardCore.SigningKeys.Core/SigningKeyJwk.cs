using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.SigningKeys.Core;

/// <summary>
/// Makes key pairs and moves them between JSON Web Keys and the secrets of Orchard Core Secrets: an RS256 key is an
/// <see cref="RsaKeySecret"/> (PKCS #1, in base64, as Orchard Core's own RSA secret editor writes it), an ES256 key a
/// <see cref="TextSecret"/> holding its private JSON Web Key.
/// </summary>
public static class SigningKeyJwk
{
    private static readonly string[] _privateMembers = ["d", "p", "q", "dp", "dq", "qi", "oth", "k"];

    /// <summary>
    /// Makes a key pair.
    /// </summary>
    /// <param name="algorithm">The algorithm, one of <see cref="SigningKeyAlgorithms"/>.</param>
    /// <param name="kid">The key id.</param>
    /// <returns>The key pair as a JSON Web Key, with <c>kid</c>, <c>use</c> and <c>alg</c>.</returns>
    public static JsonObject Create(string algorithm, string kid)
    {
        ArgumentException.ThrowIfNullOrEmpty(kid);

        switch (algorithm)
        {
            case SigningKeyAlgorithms.RS256:
                using (var rsa = RSA.Create(2048))
                {
                    return FromRsa(rsa.ExportParameters(includePrivateParameters: true), kid);
                }

            case SigningKeyAlgorithms.ES256:
                using (var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256))
                {
                    return FromEc(ecdsa.ExportParameters(includePrivateParameters: true), kid);
                }

            default:
                throw new NotSupportedException($"The signing key algorithm '{algorithm}' is not supported.");
        }
    }

    /// <summary>
    /// Returns the public half of a JSON Web Key: every member but the private ones.
    /// </summary>
    /// <param name="jwk">The JSON Web Key.</param>
    /// <returns>A new object without <c>d</c>, <c>p</c>, <c>q</c>, <c>dp</c>, <c>dq</c>, <c>qi</c>, <c>oth</c> and <c>k</c>.</returns>
    public static JsonObject PublicOf(JsonObject jwk)
    {
        ArgumentNullException.ThrowIfNull(jwk);

        var result = new JsonObject();

        foreach (var (name, value) in jwk)
        {
            if (!_privateMembers.Contains(name, StringComparer.Ordinal))
            {
                result[name] = value?.DeepClone();
            }
        }

        return result;
    }

    /// <summary>
    /// Puts a key pair in the secret type that keeps it.
    /// </summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="privateJwk">The key pair as a JSON Web Key.</param>
    /// <returns>An <see cref="RsaKeySecret"/> for RS256, a <see cref="TextSecret"/> for ES256.</returns>
    public static ISecret ToSecret(string algorithm, JsonObject privateJwk)
    {
        ArgumentNullException.ThrowIfNull(privateJwk);

        switch (algorithm)
        {
            case SigningKeyAlgorithms.RS256:
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(ToRsaParameters(privateJwk));

                    return new RsaKeySecret
                    {
                        PublicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey()),
                        PrivateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey()),
                        IncludesPrivateKey = true,
                        KeySize = rsa.KeySize,
                    };
                }

            case SigningKeyAlgorithms.ES256:
                return new TextSecret
                {
                    Text = privateJwk.ToJsonString(),
                };

            default:
                throw new NotSupportedException($"The signing key algorithm '{algorithm}' is not supported.");
        }
    }

    /// <summary>
    /// Reads a key pair back from its secret.
    /// </summary>
    /// <param name="algorithm">The algorithm.</param>
    /// <param name="secret">The secret.</param>
    /// <param name="kid">The key id, set on the result.</param>
    /// <returns>The key pair as a JSON Web Key, or <see langword="null"/> when the secret holds no private key of the
    /// algorithm.</returns>
    public static JsonObject FromSecret(string algorithm, ISecret secret, string kid)
    {
        try
        {
            switch (algorithm)
            {
                case SigningKeyAlgorithms.RS256 when secret is RsaKeySecret { IncludesPrivateKey: true, PrivateKey: { Length: > 0 } privateKey }:
                    using (var rsa = RSA.Create())
                    {
                        rsa.ImportRSAPrivateKey(Convert.FromBase64String(privateKey), out _);

                        return FromRsa(rsa.ExportParameters(includePrivateParameters: true), kid);
                    }

                case SigningKeyAlgorithms.ES256 when secret is TextSecret { Text: { Length: > 0 } text }:
                    if (JsonNode.Parse(text) is JsonObject jwk && jwk["d"] is not null)
                    {
                        jwk["kid"] = kid;

                        return jwk;
                    }

                    return null;

                default:
                    return null;
            }
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static JsonObject FromRsa(RSAParameters parameters, string kid)
    {
        var jwk = new JsonObject
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["alg"] = SigningKeyAlgorithms.RS256,
            ["kid"] = kid,
            ["n"] = Base64Url.EncodeToString(parameters.Modulus),
            ["e"] = Base64Url.EncodeToString(parameters.Exponent),
        };

        if (parameters.D is not null)
        {
            jwk["d"] = Base64Url.EncodeToString(parameters.D);
            jwk["p"] = Base64Url.EncodeToString(parameters.P);
            jwk["q"] = Base64Url.EncodeToString(parameters.Q);
            jwk["dp"] = Base64Url.EncodeToString(parameters.DP);
            jwk["dq"] = Base64Url.EncodeToString(parameters.DQ);
            jwk["qi"] = Base64Url.EncodeToString(parameters.InverseQ);
        }

        return jwk;
    }

    private static JsonObject FromEc(ECParameters parameters, string kid)
        => new()
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64Url.EncodeToString(parameters.Q.X),
            ["y"] = Base64Url.EncodeToString(parameters.Q.Y),
            ["d"] = Base64Url.EncodeToString(parameters.D),
            ["kid"] = kid,
            ["use"] = "sig",
            ["alg"] = SigningKeyAlgorithms.ES256,
        };

    private static RSAParameters ToRsaParameters(JsonObject jwk)
        => new()
        {
            Modulus = Decode(jwk, "n"),
            Exponent = Decode(jwk, "e"),
            D = Decode(jwk, "d"),
            P = Decode(jwk, "p"),
            Q = Decode(jwk, "q"),
            DP = Decode(jwk, "dp"),
            DQ = Decode(jwk, "dq"),
            InverseQ = Decode(jwk, "qi"),
        };

    private static byte[] Decode(JsonObject jwk, string name)
        => jwk[name]?.GetValue<string>() is { Length: > 0 } value ? Base64Url.DecodeFromChars(value) : null;
}
