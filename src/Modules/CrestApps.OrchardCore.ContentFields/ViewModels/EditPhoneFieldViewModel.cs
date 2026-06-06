using CrestApps.OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentFields.ViewModels;

/// <summary>
/// View model used when editing a <see cref="PhoneField"/>.
/// </summary>
public class EditPhoneFieldViewModel
{
    /// <summary>
    /// Gets or sets the phone number in E.164 format.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the national phone number without the country calling code.
    /// </summary>
    public string NationalNumber { get; set; }

    /// <summary>
    /// Gets or sets the phone field instance being edited.
    /// </summary>
    public PhoneField Field { get; set; }

    /// <summary>
    /// Gets or sets the content part containing this field.
    /// </summary>
    public ContentPart Part { get; set; }

    /// <summary>
    /// Gets or sets the field definition metadata.
    /// </summary>
    public ContentPartFieldDefinition PartFieldDefinition { get; set; }
}
