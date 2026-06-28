namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Represents the country information in an AbstractAPI response.
/// </summary>
internal sealed class AbstractApiCountry
{
    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the country name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the country calling prefix.
    /// </summary>
    public string Prefix { get; set; }
}
