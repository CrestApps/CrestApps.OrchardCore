using CrestApps.OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentFields.ViewModels;

/// <summary>
/// View model used when displaying a <see cref="PhoneField"/>.
/// </summary>
public class DisplayPhoneFieldViewModel
{
    /// <summary>
    /// Gets or sets the phone field instance being displayed.
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
