using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Represents Twilio Lookup SMS pumping risk details.
/// </summary>
internal sealed class TwilioSmsPumpingRisk
{
    /// <summary>
    /// Gets or sets the carrier risk category.
    /// </summary>
    [JsonPropertyName("carrier_risk_category")]
    public string CarrierRiskCategory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the number is blocked by Twilio fraud controls.
    /// </summary>
    [JsonPropertyName("number_blocked")]
    public bool? NumberBlocked { get; set; }

    /// <summary>
    /// Gets or sets the SMS pumping risk score.
    /// </summary>
    [JsonPropertyName("sms_pumping_risk_score")]
    public double? SmsPumpingRiskScore { get; set; }
}
