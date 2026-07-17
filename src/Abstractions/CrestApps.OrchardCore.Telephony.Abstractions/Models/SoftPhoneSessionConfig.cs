using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Describes the browser media session associated with a short-lived registration.
/// </summary>
public sealed class SoftPhoneSessionConfig
{
    /// <summary>
    /// Gets or sets the interaction identifier bound to the credential.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the UTC expiration instant for the browser media session.
    /// </summary>
    [JsonPropertyName("expiresAtUtc")]
    public DateTime ExpiresAtUtc { get; set; }
}
