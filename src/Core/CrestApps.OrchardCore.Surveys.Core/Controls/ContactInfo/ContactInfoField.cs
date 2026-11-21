namespace CrestApps.OrchardCore.Surveys.Core.Controls.ContactInfo;

/// <summary>
/// A field a contact information question asks for.
/// </summary>
public sealed class ContactInfoField
{
    /// <summary>
    /// Gets or sets the field.
    /// </summary>
    public ContactInfoFieldKind Kind { get; set; }

    /// <summary>
    /// Gets or sets whether the field is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the label, or <see langword="null"/> for the default.
    /// </summary>
    public string Label { get; set; }
}
