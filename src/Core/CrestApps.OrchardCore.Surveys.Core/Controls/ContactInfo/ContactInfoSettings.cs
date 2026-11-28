namespace CrestApps.OrchardCore.Surveys.Core.Controls.ContactInfo;

/// <summary>
/// The settings of a contact information question.
/// </summary>
public sealed class ContactInfoSettings
{
    /// <summary>
    /// Gets or sets the fields asked for, in order.
    /// </summary>
    public List<ContactInfoField> Fields { get; set; } =
    [
        new ContactInfoField { Kind = ContactInfoFieldKind.Name, Required = true },
        new ContactInfoField { Kind = ContactInfoFieldKind.Email, Required = true },
        new ContactInfoField { Kind = ContactInfoFieldKind.Phone },
    ];
}
