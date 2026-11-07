using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Webhooks.Core.Signatures;

namespace CrestApps.OrchardCore.Webhooks.Core.Services;

/// <summary>
/// Signs webhook bodies with <c>v1=</c> and the lowercase hex HMAC-SHA256 of <c>{timestamp}.{body}</c>, keyed with the
/// secret: the scheme every outbound webhook of the site uses.
/// </summary>
public sealed class HmacSha256WebhookSigner : IWebhookSigner
{
    /// <summary>
    /// The prefix that names the scheme's version.
    /// </summary>
    public const string VersionPrefix = "v1=";

    /// <inheritdoc/>
    public string Sign(string secret, long timestamp, string body)
        => VersionPrefix + Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret ?? string.Empty),
            Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + "." + (body ?? string.Empty))));

    /// <inheritdoc/>
    public bool Verify(string secret, long timestamp, string body, string signature)
        => !string.IsNullOrEmpty(secret) &&
            !string.IsNullOrEmpty(signature) &&
            WebhookSignatures.FixedTimeEquals(Sign(secret, timestamp, body), signature.Trim());
}
