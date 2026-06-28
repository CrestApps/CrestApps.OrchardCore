using System.Text.Json;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Represents the nested phone validation object returned by the AbstractAPI Phone Intelligence endpoint.
/// </summary>
internal sealed class AbstractApiPhoneValidation
{
    /// <summary>
    /// Gets or sets a value indicating whether the phone number is valid.
    /// </summary>
    public bool? Valid { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the phone number is valid when returned as <c>is_valid</c>.
    /// </summary>
    public bool? IsValid { get; set; }

    /// <summary>
    /// Gets or sets the international and local formatted phone number values.
    /// </summary>
    public AbstractApiFormat Format { get; set; }

    /// <summary>
    /// Gets or sets the country information.
    /// </summary>
    public AbstractApiCountry Country { get; set; }

    /// <summary>
    /// Gets or sets the location associated with the phone number.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets the reported line type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the carrier associated with the phone number.
    /// </summary>
    public string Carrier { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific line status.
    /// </summary>
    public string LineStatus { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific minimum observed line age.
    /// </summary>
    public JsonElement? MinimumAge { get; set; }
}
