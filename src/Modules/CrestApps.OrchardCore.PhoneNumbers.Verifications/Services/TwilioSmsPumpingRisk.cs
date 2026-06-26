namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Represents Twilio Lookup SMS pumping risk details.
/// </summary>
internal sealed class TwilioSmsPumpingRisk
{
    /// <summary>
    /// Gets or sets the carrier risk category.
    /// </summary>
    public string CarrierRiskCategory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the number is blocked by Twilio fraud controls.
    /// </summary>
    public bool? NumberBlocked { get; set; }

    /// <summary>
    /// Gets or sets the SMS pumping risk score.
    /// </summary>
    public double? SmsPumpingRiskScore { get; set; }
}
