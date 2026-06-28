namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Represents Twilio Lookup line type intelligence details.
/// </summary>
internal sealed class TwilioLineTypeIntelligence
{
    /// <summary>
    /// Gets or sets the reported line type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the carrier name.
    /// </summary>
    public string CarrierName { get; set; }

    /// <summary>
    /// Gets or sets the mobile country code.
    /// </summary>
    public string MobileCountryCode { get; set; }

    /// <summary>
    /// Gets or sets the mobile network code.
    /// </summary>
    public string MobileNetworkCode { get; set; }

    /// <summary>
    /// Gets or sets the provider error code.
    /// </summary>
    public int? ErrorCode { get; set; }
}
