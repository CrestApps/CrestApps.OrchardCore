using System.Globalization;
using CrestApps.OrchardCore.Webhooks.Core.Signatures;
using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// Checks the <c>X-Mailchimp-Signature</c> header of a webhook that has a signing secret:
/// <c>t={unix seconds},v1={hex}</c>, where the hex is the HMAC-SHA256 of <c>{t}.{raw body}</c> keyed with the signing
/// secret. A signature more than <see cref="Tolerance"/> away from now is refused, so a captured request can't be
/// replayed later.
/// </summary>
public static class MailchimpWebhookSignature
{
    /// <summary>
    /// How far a signature's time may be from now.
    /// </summary>
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Computes the <c>v1</c> signature Mailchimp puts on a body at a time.
    /// </summary>
    /// <param name="signingSecret">The webhook's signing secret.</param>
    /// <param name="timestamp">The <c>t</c> value, as sent.</param>
    /// <param name="body">The raw body.</param>
    /// <returns>The signature in lowercase hex.</returns>
    public static string Compute(string signingSecret, string timestamp, string body)
    {
        ArgumentException.ThrowIfNullOrEmpty(signingSecret);
        ArgumentException.ThrowIfNullOrEmpty(timestamp);

        return HmacWebhookSignatureValidator.Sha256Hex.Compute(signingSecret, $"{timestamp}.{body}");
    }

    /// <summary>
    /// Checks a signature header against the raw body, before anything in the body is decoded.
    /// </summary>
    /// <param name="header">The <c>X-Mailchimp-Signature</c> header, or <see langword="null"/>.</param>
    /// <param name="body">The raw body.</param>
    /// <param name="signingSecret">The webhook's signing secret.</param>
    /// <param name="nowUtc">The current time (UTC).</param>
    /// <returns>Whether the signature is valid, and why not.</returns>
    public static MailchimpSignatureResult Verify(string header, string body, string signingSecret, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(signingSecret);

        if (string.IsNullOrWhiteSpace(header))
        {
            return MailchimpSignatureResult.Missing;
        }

        string timestamp = null;
        var signatures = new List<string>();

        foreach (var part in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equals = part.IndexOf('=');

            if (equals <= 0)
            {
                continue;
            }

            var name = part.Substring(0, equals).Trim();
            var value = part.Substring(equals + 1).Trim();

            if (string.Equals(name, "t", StringComparison.OrdinalIgnoreCase))
            {
                timestamp = value;
            }
            else if (string.Equals(name, "v1", StringComparison.OrdinalIgnoreCase) && value.Length > 0)
            {
                signatures.Add(value);
            }
        }

        if (string.IsNullOrEmpty(timestamp)
            || signatures.Count == 0
            || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds)
            || unixSeconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            return MailchimpSignatureResult.Malformed;
        }

        var signedContent = $"{timestamp}.{body}";
        var valid = signatures.Exists(signature => HmacWebhookSignatureValidator.Sha256Hex.IsValid(new WebhookSignatureValidationContext
        {
            Signature = signature,
            Secret = signingSecret,
            SignedContent = signedContent,
            Timestamp = timestamp,
        }));

        if (!valid)
        {
            return MailchimpSignatureResult.Invalid;
        }

        var signedUtc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        var now = nowUtc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc) : nowUtc.ToUniversalTime();

        return (now - signedUtc).Duration() > Tolerance
            ? MailchimpSignatureResult.Stale
            : MailchimpSignatureResult.Valid;
    }
}
