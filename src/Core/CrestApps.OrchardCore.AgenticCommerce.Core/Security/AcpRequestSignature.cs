using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Signs and verifies ACP request signatures: the <c>Signature</c> header (a base64url detached signature) and the
/// <c>Timestamp</c> header (RFC 3339).
/// </summary>
/// <remarks>
/// ACP says a client signs "over canonical JSON" and sends a timestamp checked against a clock-skew window, and leaves
/// the algorithm, the canonical form and the key to an agreement with the merchant. This class settles them as
/// follows: the algorithm is ES256 (ECDSA on P-256 with SHA-256), whose signature is the 64-byte IEEE P1363
/// <c>r || s</c> form JOSE uses (a DER signature is accepted too). What is signed depends on the platform's signature
/// version: version 1 (<see cref="BodyOnlyVersion"/>, platforms set up before versions existed) signs only the RFC 8785
/// (JCS) canonical JSON of the request body, or nothing for a request without a body, and checks the timestamp against
/// the skew window; version 2 (<see cref="BoundVersion"/>) signs <c>timestamp.METHOD.path.</c> followed by that
/// canonical JSON, so a captured signature can't be sent with another timestamp or to another path.
/// </remarks>
public static partial class AcpRequestSignature
{
    /// <summary>
    /// The request header the signature comes in.
    /// </summary>
    public const string SignatureHeader = "Signature";

    /// <summary>
    /// The request header the time of signing comes in.
    /// </summary>
    public const string TimestampHeader = "Timestamp";

    /// <summary>
    /// ECDSA on P-256 with SHA-256.
    /// </summary>
    public const string ES256 = JwsAlgorithms.ES256;

    /// <summary>
    /// The clock skew allowed when a platform doesn't set its own.
    /// </summary>
    public static readonly TimeSpan DefaultClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The algorithms the store verifies. Ed25519 isn't one: .NET 10 has no Ed25519 signature API of its own.
    /// </summary>
    public static readonly IReadOnlyList<string> Algorithms = [ES256];

    /// <summary>
    /// The signature version that signs only the body: what platforms set up before versions existed sign.
    /// </summary>
    public const int BodyOnlyVersion = 1;

    /// <summary>
    /// The signature version that signs the timestamp, the method and the path with the body.
    /// </summary>
    public const int BoundVersion = 2;

    /// <summary>
    /// Returns the bytes a version 2 signature is made over: <c>timestamp.METHOD.path.</c> (UTF-8) followed by the
    /// signing input of the body.
    /// </summary>
    /// <param name="timestamp">The <c>Timestamp</c> header, as sent.</param>
    /// <param name="method">The HTTP method, such as <c>POST</c>; upper-cased.</param>
    /// <param name="path">The request's path, such as <c>/acp/checkout_sessions/cs_1/complete</c>.</param>
    /// <param name="body">The signing input of the body, from <see cref="TryGetSigningInput"/>.</param>
    public static byte[] GetBoundSigningInput(string timestamp, string method, string path, byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var prefix = System.Text.Encoding.UTF8.GetBytes($"{timestamp?.Trim()}.{method?.Trim().ToUpperInvariant()}.{path}.");
        var input = new byte[prefix.Length + body.Length];

        prefix.CopyTo(input, 0);
        body.CopyTo(input, prefix.Length);

        return input;
    }

    /// <summary>
    /// Returns the bytes a request body is signed over: its RFC 8785 canonical JSON, or nothing for an empty body.
    /// </summary>
    /// <param name="body">The raw body.</param>
    /// <param name="input">The signed bytes.</param>
    /// <returns><see langword="false"/> when the body isn't JSON, so it can't be canonicalized.</returns>
    public static bool TryGetSigningInput(byte[] body, out byte[] input)
    {
        if (body is not { Length: > 0 })
        {
            input = [];

            return true;
        }

        try
        {
            input = GetSigningInput(JsonNode.Parse(body));

            return true;
        }
        catch (JsonException)
        {
            input = null;

            return false;
        }
    }

    /// <summary>
    /// Returns the bytes a JSON value is signed over: its RFC 8785 canonical JSON, or nothing for no value.
    /// </summary>
    /// <param name="content">The request's JSON, or <see langword="null"/> for none.</param>
    public static byte[] GetSigningInput(JsonNode content)
        => content is null ? [] : JsonCanonicalizer.CanonicalizeToUtf8(content);

    /// <summary>
    /// Reads a platform's public key: an EC JWK on P-256 (<c>{"kty":"EC","crv":"P-256","x":...,"y":...}</c>), or a PEM
    /// <c>PUBLIC KEY</c> (SubjectPublicKeyInfo) on P-256. A private key is refused.
    /// </summary>
    /// <param name="text">The key as entered.</param>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the text is a P-256 public key.</returns>
    public static bool TryReadPublicKey(string text, out EcJsonWebKey key)
    {
        key = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();

        if (text.StartsWith('{'))
        {
            JsonObject json;

            try
            {
                json = JsonNode.Parse(text) as JsonObject;
            }
            catch (JsonException)
            {
                return false;
            }

            var jwk = EcJsonWebKey.FromJson(json);

            if (jwk is null || jwk.HasPrivateKey || jwk.Crv != JwsAlgorithms.CurveP256)
            {
                return false;
            }

            key = new EcJsonWebKey
            {
                Kid = jwk.Kid,
                Crv = jwk.Crv,
                X = jwk.X,
                Y = jwk.Y,
            };

            return true;
        }

        if (!PemEncoding.TryFind(text, out var fields) || text[fields.Label] is not "PUBLIC KEY")
        {
            return false;
        }

        try
        {
            using var ecdsa = ECDsa.Create();

            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(text[fields.Base64Data]), out _);

            if (ecdsa.KeySize != 256)
            {
                return false;
            }

            key = EcJsonWebKey.FromECDsa(ecdsa, kid: null, includePrivateKey: false);

            return true;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads an RFC 3339 date-time, which must have a time zone offset or <c>Z</c>.
    /// </summary>
    /// <param name="value">The header's value.</param>
    /// <param name="timestamp">The time.</param>
    public static bool TryParseTimestamp(string value, out DateTimeOffset timestamp)
    {
        timestamp = default;

        return !string.IsNullOrWhiteSpace(value) &&
            Rfc3339().IsMatch(value.Trim()) &&
            DateTimeOffset.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp);
    }

    /// <summary>
    /// Signs bytes with ES256 and returns the base64url P1363 signature, as a client puts it in <c>Signature</c>.
    /// </summary>
    /// <param name="input">The signed bytes, from <see cref="GetSigningInput(JsonNode)"/>.</param>
    /// <param name="privateKey">The P-256 private key.</param>
    public static string Sign(byte[] input, ECDsa privateKey)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(privateKey);

        return Base64Url.EncodeToString(privateKey.SignData(input, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    /// <summary>
    /// Verifies a request's signature and timestamp.
    /// </summary>
    /// <param name="input">The signed bytes, from <see cref="TryGetSigningInput"/> or <see cref="GetSigningInput(JsonNode)"/>.</param>
    /// <param name="signature">The <c>Signature</c> header.</param>
    /// <param name="timestamp">The <c>Timestamp</c> header.</param>
    /// <param name="key">The platform's P-256 public key.</param>
    /// <param name="maxSkew">How far the timestamp may be from now, either way.</param>
    /// <param name="now">The current time.</param>
    public static AcpSignatureVerification Verify(byte[] input, string signature, string timestamp, EcJsonWebKey key, TimeSpan maxSkew, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(timestamp))
        {
            return AcpSignatureVerification.MissingHeaders;
        }

        if (!TryParseTimestamp(timestamp, out var signedAt))
        {
            return AcpSignatureVerification.InvalidTimestamp;
        }

        if ((now - signedAt).Duration() > maxSkew)
        {
            return AcpSignatureVerification.StaleTimestamp;
        }

        if (input is null || !TryDecode(signature.Trim(), out var bytes))
        {
            return AcpSignatureVerification.InvalidSignature;
        }

        try
        {
            using var ecdsa = key.CreateECDsa();

            var format = bytes.Length == 64 ? DSASignatureFormat.IeeeP1363FixedFieldConcatenation : DSASignatureFormat.Rfc3279DerSequence;

            return ecdsa.VerifyData(input, bytes, HashAlgorithmName.SHA256, format)
                ? AcpSignatureVerification.Valid
                : AcpSignatureVerification.InvalidSignature;
        }
        catch (CryptographicException)
        {
            return AcpSignatureVerification.InvalidSignature;
        }
    }

    // base64url as ACP says, with or without padding.
    private static bool TryDecode(string value, out byte[] bytes)
    {
        bytes = null;

        var trimmed = value.TrimEnd('=');

        if (trimmed.Length == 0 || !Base64Url.IsValid(trimmed))
        {
            return false;
        }

        bytes = Base64Url.DecodeFromChars(trimmed);

        return true;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}[Tt ]\d{2}:\d{2}:\d{2}(\.\d{1,9})?([Zz]|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex Rfc3339();
}
