using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Represents the raw response returned by the Veriphone verification endpoint.
/// </summary>
internal sealed class VeriphoneResponse
{
    /// <summary>
    /// Gets or sets the provider response status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the phone number echoed by the provider.
    /// </summary>
    [JsonPropertyName("phone")]
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is valid.
    /// </summary>
    [JsonPropertyName("phone_valid")]
    public bool PhoneValid { get; set; }

    /// <summary>
    /// Gets or sets the reported line type.
    /// </summary>
    [JsonPropertyName("phone_type")]
    public string PhoneType { get; set; }

    /// <summary>
    /// Gets or sets the provider-reported phone region.
    /// </summary>
    [JsonPropertyName("phone_region")]
    public string PhoneRegion { get; set; }

    /// <summary>
    /// Gets or sets the country name.
    /// </summary>
    [JsonPropertyName("country")]
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    [JsonPropertyName("country_code")]
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the country calling prefix.
    /// </summary>
    [JsonPropertyName("country_prefix")]
    public string CountryPrefix { get; set; }

    /// <summary>
    /// Gets or sets the provider-formatted international number.
    /// </summary>
    [JsonPropertyName("international_number")]
    public string InternationalNumber { get; set; }

    /// <summary>
    /// Gets or sets the provider-formatted local number.
    /// </summary>
    [JsonPropertyName("local_number")]
    public string LocalNumber { get; set; }

    /// <summary>
    /// Gets or sets the E.164 phone number.
    /// </summary>
    [JsonPropertyName("e164")]
    public string E164 { get; set; }

    /// <summary>
    /// Gets or sets the carrier associated with the phone number.
    /// </summary>
    [JsonPropertyName("carrier")]
    public string Carrier { get; set; }
}
