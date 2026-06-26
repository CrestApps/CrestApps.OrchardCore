using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Represents the country information in an AbstractAPI response.
/// </summary>
internal sealed class AbstractApiCountry
{
    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the country name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the country calling prefix.
    /// </summary>
    [JsonPropertyName("prefix")]
    public string Prefix { get; set; }
}
