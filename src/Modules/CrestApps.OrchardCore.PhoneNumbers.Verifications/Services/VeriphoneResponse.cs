namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Represents the raw response returned by the Veriphone verification endpoint.
/// </summary>
internal sealed class VeriphoneResponse
{
    /// <summary>
    /// Gets or sets the provider response status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the phone number echoed by the provider.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is valid.
    /// </summary>
    public bool PhoneValid { get; set; }

    /// <summary>
    /// Gets or sets the reported line type.
    /// </summary>
    public string PhoneType { get; set; }

    /// <summary>
    /// Gets or sets the provider-reported phone region.
    /// </summary>
    public string PhoneRegion { get; set; }

    /// <summary>
    /// Gets or sets the country name.
    /// </summary>
    public string Country { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the country calling prefix.
    /// </summary>
    public string CountryPrefix { get; set; }

    /// <summary>
    /// Gets or sets the provider-formatted international number.
    /// </summary>
    public string InternationalNumber { get; set; }

    /// <summary>
    /// Gets or sets the provider-formatted local number.
    /// </summary>
    public string LocalNumber { get; set; }

    /// <summary>
    /// Gets or sets the E.164 phone number.
    /// </summary>
    public string E164 { get; set; }

    /// <summary>
    /// Gets or sets the carrier associated with the phone number.
    /// </summary>
    public string Carrier { get; set; }
}
