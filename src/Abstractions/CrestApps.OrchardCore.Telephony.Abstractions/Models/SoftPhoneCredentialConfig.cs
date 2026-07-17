using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes the short-lived browser SIP credential.
/// </summary>
public sealed class SoftPhoneCredentialConfig
{
    /// <summary>
    /// Gets or sets the credential type. Supported values are <c>password</c> and <c>ephemeralToken</c>.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the credential value.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the UTC expiration instant for the credential.
    /// </summary>
    [JsonPropertyName("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }
}
