namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Represents the raw response returned by the AbstractAPI Phone Validation endpoint.
/// </summary>
internal sealed class AbstractApiResponse
{
    /// <summary>
    /// Gets or sets the phone number echoed by the provider.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is valid.
    /// </summary>
    public bool Valid { get; set; }

    /// <summary>
    /// Gets or sets the international (E.164) format of the phone number.
    /// </summary>
    public string InternationalFormat { get; set; }

    /// <summary>
    /// Gets or sets the local format of the phone number.
    /// </summary>
    public string LocalFormat { get; set; }

    /// <summary>
    /// Gets or sets the country information.
    /// </summary>
    public AbstractApiCountry Country { get; set; }

    /// <summary>
    /// Gets or sets the location associated with the phone number.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets the reported line type (e.g., mobile, landline, voip).
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the carrier associated with the phone number.
    /// </summary>
    public string Carrier { get; set; }
}
