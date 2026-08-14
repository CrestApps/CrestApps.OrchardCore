using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.ContentFields.Fields;

/// <summary>
/// A content field that stores a phone number with its country code for
/// correct flag display and E.164 formatting.
/// </summary>
public sealed class PhoneField : ContentField
{
    /// <summary>
    /// Gets or sets the phone number in E.164 format (e.g., "+14155552671").
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code (e.g., "US", "CA").
    /// This is stored separately so the correct country flag can be displayed
    /// even when multiple countries share the same calling code.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the national phone number without the country calling code
    /// (e.g., "4155552671").
    /// </summary>
    public string NationalNumber { get; set; }
}
