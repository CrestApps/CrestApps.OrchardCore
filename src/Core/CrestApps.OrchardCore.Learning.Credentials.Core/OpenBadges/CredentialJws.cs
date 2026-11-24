using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;

/// <summary>
/// The JSON Web Token proof format of Open Badges 3.0: a compact JWS signed with RS256 whose payload is the credential
/// with the registered claims <c>iss</c> (the issuer's id), <c>jti</c> (the credential's id), <c>nbf</c> (from
/// <c>validFrom</c>), <c>exp</c> (from <c>validUntil</c>, when there is one) and <c>sub</c> (the subject's id, when
/// there is one). The header names <c>alg</c>, <c>typ</c> and the <c>kid</c>, the address of the public key.
/// </summary>
public static class CredentialJws
{
    /// <summary>
    /// The only signing algorithm.
    /// </summary>
    public const string Algorithm = "RS256";

    /// <summary>
    /// Makes the JWT payload of a credential: the credential with the registered claims.
    /// </summary>
    /// <param name="credential">The credential.</param>
    /// <returns>The payload.</returns>
    public static JsonObject Payload(JsonObject credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        var payload = credential.DeepClone().AsObject();
        var issuer = credential["issuer"] is JsonObject profile ? profile["id"] : credential["issuer"];

        if (issuer is not null)
        {
            payload["iss"] = issuer.DeepClone();
        }

        if (credential["id"] is { } id)
        {
            payload["jti"] = id.DeepClone();
        }

        if (NumericDate(credential["validFrom"]) is { } notBefore)
        {
            payload["nbf"] = notBefore;
        }

        if (NumericDate(credential["validUntil"]) is { } expires)
        {
            payload["exp"] = expires;
        }

        if (credential["credentialSubject"] is JsonObject subject && subject["id"] is { } subjectId)
        {
            payload["sub"] = subjectId.DeepClone();
        }

        return payload;
    }

    /// <summary>
    /// Signs a payload.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <param name="privateJwk">The private RSA key as a JSON Web Key.</param>
    /// <param name="kid">The <c>kid</c> of the header.</param>
    /// <returns>The compact JWS.</returns>
    public static string Sign(JsonObject payload, JsonObject privateJwk, string kid)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(privateJwk);
        ArgumentException.ThrowIfNullOrEmpty(kid);

        var header = new JsonObject
        {
            ["alg"] = Algorithm,
            ["typ"] = "JWT",
            ["kid"] = kid,
        };

        var signingInput = Encode(header) + "." + Encode(payload);

        using var rsa = RSA.Create();
        rsa.ImportParameters(Parameters(privateJwk, includePrivate: true));

        var signature = rsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return signingInput + "." + Base64Url.EncodeToString(signature);
    }

    /// <summary>
    /// Reads a compact JWS without checking it.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The header and the payload, or <see langword="null"/> values when the token is not a JWS.</returns>
    public static (JsonObject Header, JsonObject Payload) Read(string token)
    {
        var parts = token?.Split('.');

        if (parts is not { Length: 3 })
        {
            return (null, null);
        }

        try
        {
            return (Decode(parts[0]), Decode(parts[1]));
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException)
        {
            return (null, null);
        }
    }

    /// <summary>
    /// Checks the RS256 signature of a compact JWS with a public key.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="publicJwk">The public RSA key as a JSON Web Key.</param>
    /// <returns><see langword="true"/> when the signature is valid.</returns>
    public static bool Verify(string token, JsonObject publicJwk)
    {
        if (publicJwk is null)
        {
            return false;
        }

        var parts = token?.Split('.');

        if (parts is not { Length: 3 })
        {
            return false;
        }

        try
        {
            if (Decode(parts[0])?["alg"]?.GetValue<string>() != Algorithm)
            {
                return false;
            }

            using var rsa = RSA.Create();
            rsa.ImportParameters(Parameters(publicJwk, includePrivate: false));

            return rsa.VerifyData(
                Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]),
                Base64Url.DecodeFromChars(parts[2]),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException or CryptographicException)
        {
            return false;
        }
    }

    private static long? NumericDate(JsonNode value)
    {
        if (value is null || value.GetValueKind() != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(value.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment)
            ? moment.ToUnixTimeSeconds()
            : null;
    }

    private static string Encode(JsonObject value)
        => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value.ToJsonString()));

    private static JsonObject Decode(string part)
        => JsonNode.Parse(Base64Url.DecodeFromChars(part)) as JsonObject;

    private static RSAParameters Parameters(JsonObject jwk, bool includePrivate)
    {
        var parameters = new RSAParameters
        {
            Modulus = Bytes(jwk, "n"),
            Exponent = Bytes(jwk, "e"),
        };

        if (includePrivate)
        {
            parameters.D = Bytes(jwk, "d");
            parameters.P = Bytes(jwk, "p");
            parameters.Q = Bytes(jwk, "q");
            parameters.DP = Bytes(jwk, "dp");
            parameters.DQ = Bytes(jwk, "dq");
            parameters.InverseQ = Bytes(jwk, "qi");
        }

        return parameters;
    }

    private static byte[] Bytes(JsonObject jwk, string name)
        => jwk[name]?.GetValue<string>() is { Length: > 0 } value ? Base64Url.DecodeFromChars(value) : null;
}
