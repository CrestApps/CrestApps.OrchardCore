namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Represents the raw response returned by the Twilio Lookup endpoint.
/// </summary>
internal sealed class TwilioLookupResponse
{
    /// <summary>
    /// Gets or sets the international dialing prefix.
    /// </summary>
    public string CallingCountryCode { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the phone number in E.164 format.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the national phone number format.
    /// </summary>
    public string NationalFormat { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is valid.
    /// </summary>
    public bool Valid { get; set; }

    /// <summary>
    /// Gets or sets the validation errors returned for invalid numbers.
    /// </summary>
    public string[] ValidationErrors { get; set; }

    /// <summary>
    /// Gets or sets the line type intelligence data package response.
    /// </summary>
    public TwilioLineTypeIntelligence LineTypeIntelligence { get; set; }

    /// <summary>
    /// Gets or sets the line status data package response.
    /// </summary>
    public TwilioLineStatus LineStatus { get; set; }

    /// <summary>
    /// Gets or sets the SMS pumping risk data package response.
    /// </summary>
    public TwilioSmsPumpingRisk SmsPumpingRisk { get; set; }

    /// <summary>
    /// Gets or sets the resource URL.
    /// </summary>
    public string Url { get; set; }
}
