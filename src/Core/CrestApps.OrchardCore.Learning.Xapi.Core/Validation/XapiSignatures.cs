using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Validation;

/// <summary>
/// Checks the JSON web signature (JWS, RFC 7515) of a signed statement, as xAPI 1.0.3 asks: compact serialization,
/// an RS256, RS384 or RS512 algorithm, a payload that is the statement (compared with the statement comparison
/// rules, without the attachments), and, when the header carries an X.509 certificate (<c>x5c</c>), a signature that
/// the certificate's key verifies. Without a certificate the LRS cannot know the signer's key, so only the form and
/// the payload are checked.
/// </summary>
public static class XapiSignatures
{
    /// <summary>
    /// Checks a signature.
    /// </summary>
    /// <param name="statement">The statement the signature is attached to.</param>
    /// <param name="signature">The content of the signature attachment.</param>
    /// <returns>Why the signature is not valid, or <see langword="null"/>.</returns>
    public static string Verify(JsonObject statement, byte[] signature)
    {
        ArgumentNullException.ThrowIfNull(statement);
        ArgumentNullException.ThrowIfNull(signature);

        var parts = Encoding.ASCII.GetString(signature).Trim().Split('.');

        if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty))
        {
            return "The signature is not a JWS in compact serialization.";
        }

        JsonObject header;
        JsonObject payload;
        byte[] signatureBytes;

        try
        {
            header = JsonNode.Parse(Base64UrlDecode(parts[0])) as JsonObject;
            payload = JsonNode.Parse(Base64UrlDecode(parts[1])) as JsonObject;
            signatureBytes = Base64UrlDecode(parts[2]);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return "The signature is not a valid JWS.";
        }

        if (header is null || payload is null)
        {
            return "The signature's header and payload must be JSON objects.";
        }

        var algorithm = header["alg"] is JsonValue alg && alg.GetValueKind() == JsonValueKind.String ? alg.GetValue<string>() : null;
        var hash = algorithm switch
        {
            "RS256" => HashAlgorithmName.SHA256,
            "RS384" => HashAlgorithmName.SHA384,
            "RS512" => HashAlgorithmName.SHA512,
            _ => default,
        };

        if (hash == default)
        {
            return "The signature must use RS256, RS384 or RS512.";
        }

        var signed = statement.DeepClone().AsObject();
        var claimed = payload.DeepClone().AsObject();

        signed.Remove("attachments");
        claimed.Remove("attachments");

        // A statement sent without an id gets one from the LRS; the signer could not have known it.
        if (claimed["id"] is null)
        {
            signed.Remove("id");
        }

        if (!XapiStatementNormalizer.AreEquivalent(signed, claimed) ||
            !string.Equals(XapiAgents.Text(signed["id"]), XapiAgents.Text(claimed["id"]), StringComparison.OrdinalIgnoreCase))
        {
            return "The signed payload is not the statement.";
        }

        if (header["x5c"] is not JsonArray chain || chain.Count == 0)
        {
            return null;
        }

        try
        {
            using var certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(XapiAgents.Text(chain[0]) ?? string.Empty));
            using var rsa = certificate.GetRSAPublicKey();

            if (rsa is null)
            {
                return "The certificate of the signature has no RSA key.";
            }

            var input = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);

            return rsa.VerifyData(input, signatureBytes, hash, RSASignaturePadding.Pkcs1)
                ? null
                : "The signature does not match the certificate in its header.";
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            return "The certificate of the signature is not valid.";
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var text = value.Replace('-', '+').Replace('_', '/');

        text += (text.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            0 => string.Empty,
            _ => throw new FormatException("Not base64url."),
        };

        return Convert.FromBase64String(text);
    }
}
