using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Represents the raw response returned by the Twilio Lookup endpoint.
/// </summary>
internal sealed class TwilioLookupResponse
{
    /// <summary>
    /// Gets or sets the international dialing prefix.
    /// </summary>
    [JsonPropertyName("calling_country_code")]
    public string CallingCountryCode { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    [JsonPropertyName("country_code")]
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the phone number in E.164 format.
    /// </summary>
    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the national phone number format.
    /// </summary>
    [JsonPropertyName("national_format")]
    public string NationalFormat { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is valid.
    /// </summary>
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    /// <summary>
    /// Gets or sets the validation errors returned for invalid numbers.
    /// </summary>
    [JsonPropertyName("validation_errors")]
    public string[] ValidationErrors { get; set; }

    /// <summary>
    /// Gets or sets the line type intelligence data package response.
    /// </summary>
    [JsonPropertyName("line_type_intelligence")]
    public TwilioLineTypeIntelligence LineTypeIntelligence { get; set; }

    /// <summary>
    /// Gets or sets the line status data package response.
    /// </summary>
    [JsonPropertyName("line_status")]
    public TwilioLineStatus LineStatus { get; set; }

    /// <summary>
    /// Gets or sets the SMS pumping risk data package response.
    /// </summary>
    [JsonPropertyName("sms_pumping_risk")]
    public TwilioSmsPumpingRisk SmsPumpingRisk { get; set; }

    /// <summary>
    /// Gets or sets the resource URL.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; set; }
}
