using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Represents Twilio Lookup line status details.
/// </summary>
internal sealed class TwilioLineStatus
{
    /// <summary>
    /// Gets or sets the provider-specific line status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the provider error code.
    /// </summary>
    [JsonPropertyName("error_code")]
    public int? ErrorCode { get; set; }
}
