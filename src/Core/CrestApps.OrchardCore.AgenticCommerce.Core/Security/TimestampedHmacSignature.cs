using System.Globalization;
using CrestApps.OrchardCore.Webhooks.Core.Signatures;
using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// The timestamped HMAC signature ACP uses for webhooks (<c>Merchant-Signature: t=&lt;unix seconds&gt;,v1=&lt;hex&gt;</c>),
/// where <c>v1</c> is the lowercase hex HMAC-SHA256 of <c>t + "." + body</c> with the shared secret.
/// </summary>
public static class TimestampedHmacSignature
{
    private const int SignatureHexLength = 64;

    /// <summary>
    /// Signs a body.
    /// </summary>
    /// <param name="secret">The shared secret.</param>
    /// <param name="unixTimestamp">The signing time in Unix seconds.</param>
    /// <param name="body">The raw body, exactly as sent.</param>
    /// <returns>The header value <c>t=&lt;ts&gt;,v1=&lt;hex&gt;</c>.</returns>
    public static string Sign(string secret, long unixTimestamp, string body)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        var timestamp = unixTimestamp.ToString(CultureInfo.InvariantCulture);

        return "t=" + timestamp + ",v1=" + HmacWebhookSignatureValidator.Sha256Hex.Compute(secret, SignedContent(timestamp, body));
    }

    /// <summary>
    /// Verifies a header value against a body. Any <c>v1</c> member may match; the comparison runs in fixed time.
    /// </summary>
    /// <param name="header">The header value.</param>
    /// <param name="secret">The shared secret.</param>
    /// <param name="body">The raw body, exactly as received.</param>
    /// <param name="nowUnix">The current time in Unix seconds.</param>
    /// <param name="toleranceSeconds">How far the timestamp may be from <paramref name="nowUnix"/>, in either direction.</param>
    /// <returns><see langword="true"/> when the header is well formed, within the tolerance, and a signature matches.</returns>
    public static bool Verify(string header, string secret, string body, long nowUnix, long toleranceSeconds = 300)
    {
        if (string.IsNullOrWhiteSpace(header) || string.IsNullOrEmpty(secret))
        {
            return false;
        }

        string timestamp = null;
        var signatures = new List<string>();

        foreach (var part in header.Split(','))
        {
            var separator = part.IndexOf('=');

            if (separator <= 0)
            {
                return false;
            }

            var name = part.Substring(0, separator).Trim();
            var value = part.Substring(separator + 1).Trim();

            if (name == "t")
            {
                if (timestamp is not null || value.Length == 0 || !value.All(char.IsAsciiDigit))
                {
                    return false;
                }

                timestamp = value;
            }
            else if (name == "v1")
            {
                if (value.Length != SignatureHexLength || !value.All(char.IsAsciiHexDigit))
                {
                    return false;
                }

                signatures.Add(value);
            }
        }

        if (timestamp is null || signatures.Count == 0
            || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var signedAt))
        {
            return false;
        }

        if (Math.Abs(nowUnix - signedAt) > toleranceSeconds)
        {
            return false;
        }

        var signedContent = SignedContent(timestamp, body);
        var matched = false;

        // Every member is checked, so the time taken doesn't tell which one matched.
        foreach (var signature in signatures)
        {
            matched |= HmacWebhookSignatureValidator.Sha256Hex.IsValid(new WebhookSignatureValidationContext
            {
                Secret = secret,
                Signature = signature,
                SignedContent = signedContent,
            });
        }

        return matched;
    }

    private static string SignedContent(string timestamp, string body)
        => timestamp + "." + (body ?? string.Empty);
}
