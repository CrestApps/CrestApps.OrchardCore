using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Represents the raw response returned by the AbstractAPI Phone Validation endpoint.
/// </summary>
internal sealed class AbstractApiResponse
{
    /// <summary>
    /// Gets or sets the phone number echoed by the provider.
    /// </summary>
    [JsonPropertyName("phone")]
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is valid.
    /// </summary>
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    /// <summary>
    /// Gets or sets the international (E.164) format of the phone number.
    /// </summary>
    [JsonPropertyName("international_format")]
    public string InternationalFormat { get; set; }

    /// <summary>
    /// Gets or sets the local format of the phone number.
    /// </summary>
    [JsonPropertyName("local_format")]
    public string LocalFormat { get; set; }

    /// <summary>
    /// Gets or sets the country information.
    /// </summary>
    [JsonPropertyName("country")]
    public AbstractApiCountry Country { get; set; }

    /// <summary>
    /// Gets or sets the location associated with the phone number.
    /// </summary>
    [JsonPropertyName("location")]
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets the reported line type (e.g., mobile, landline, voip).
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the carrier associated with the phone number.
    /// </summary>
    [JsonPropertyName("carrier")]
    public string Carrier { get; set; }
}
