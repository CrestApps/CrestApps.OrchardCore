using System.Security.Cryptography;
using System.Text;

namespace CrestApps.OrchardCore.DialPad.Services;

/// <summary>
/// Validates and decodes DialPad webhook payloads. DialPad delivers call-event webhooks as an HS256
/// signed JSON Web Token (JWT) whose payload is the event body. When no signing secret is configured,
/// an unsigned JSON body is accepted.
/// </summary>
public static class DialPadJwtValidator
{
    /// <summary>
    /// Validates the webhook body and extracts the event JSON payload.
    /// </summary>
    /// <param name="body">The raw webhook body (a signed JWT, or JSON when no secret is configured).</param>
    /// <param name="signingSecret">The configured DialPad webhook signing secret, or <see langword="null"/>.</param>
    /// <param name="payloadJson">When this method returns, contains the extracted event JSON payload.</param>
    /// <returns><see langword="true"/> when the body is valid; otherwise, <see langword="false"/>.</returns>
    public static bool TryValidateAndExtract(string body, string signingSecret, out string payloadJson)
    {
        payloadJson = null;

        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        var trimmed = body.Trim();
        var segments = trimmed.Split('.');

        // A body that is not a three-segment JWT is treated as a raw JSON payload, which is only
        // acceptable when no signing secret has been configured.
        if (segments.Length != 3)
        {
            if (!string.IsNullOrEmpty(signingSecret))
            {
                return false;
            }

            payloadJson = trimmed;

            return LooksLikeJson(payloadJson);
        }

        var payload = DecodeSegment(segments[1]);

        if (payload is null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(signingSecret))
        {
            payloadJson = payload;

            return true;
        }

        if (!VerifySignature(segments[0], segments[1], segments[2], signingSecret))
        {
            return false;
        }

        payloadJson = payload;

        return true;
    }

    private static bool VerifySignature(string headerSegment, string payloadSegment, string signatureSegment, string secret)
    {
        var signingInput = $"{headerSegment}.{payloadSegment}";
        var key = Encoding.UTF8.GetBytes(secret);
        var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(signingInput));

        var provided = DecodeSegmentBytes(signatureSegment);

        if (provided is null || provided.Length != expected.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    private static string DecodeSegment(string segment)
    {
        var bytes = DecodeSegmentBytes(segment);

        return bytes is null ? null : Encoding.UTF8.GetString(bytes);
    }

    private static byte[] DecodeSegmentBytes(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return null;
        }

        var value = segment.Replace('-', '+').Replace('_', '/');

        switch (value.Length % 4)
        {
            case 2:
                value += "==";
                break;
            case 3:
                value += "=";
                break;
        }

        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool LooksLikeJson(string value)
    {
        return value.StartsWith('{') && value.EndsWith('}');
    }
}
