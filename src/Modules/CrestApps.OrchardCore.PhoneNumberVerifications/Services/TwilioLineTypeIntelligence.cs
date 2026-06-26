using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Represents Twilio Lookup line type intelligence details.
/// </summary>
internal sealed class TwilioLineTypeIntelligence
{
    /// <summary>
    /// Gets or sets the reported line type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the carrier name.
    /// </summary>
    [JsonPropertyName("carrier_name")]
    public string CarrierName { get; set; }

    /// <summary>
    /// Gets or sets the mobile country code.
    /// </summary>
    [JsonPropertyName("mobile_country_code")]
    public string MobileCountryCode { get; set; }

    /// <summary>
    /// Gets or sets the mobile network code.
    /// </summary>
    [JsonPropertyName("mobile_network_code")]
    public string MobileNetworkCode { get; set; }

    /// <summary>
    /// Gets or sets the provider error code.
    /// </summary>
    [JsonPropertyName("error_code")]
    public int? ErrorCode { get; set; }
}
