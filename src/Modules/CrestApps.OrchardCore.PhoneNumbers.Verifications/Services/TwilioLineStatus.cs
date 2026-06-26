namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Represents Twilio Lookup line status details.
/// </summary>
internal sealed class TwilioLineStatus
{
    /// <summary>
    /// Gets or sets the provider-specific line status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the provider error code.
    /// </summary>
    public int? ErrorCode { get; set; }
}
