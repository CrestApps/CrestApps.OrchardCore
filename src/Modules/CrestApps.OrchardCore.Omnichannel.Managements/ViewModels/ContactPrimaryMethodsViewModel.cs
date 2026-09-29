namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for the primary contact methods shown in a contact's list header.
/// </summary>
public class ContactPrimaryMethodsViewModel
{
    /// <summary>
    /// Gets or sets the contact's primary contact methods, in the order they appear on the contact.
    /// </summary>
    public IReadOnlyList<ContactPrimaryMethod> Methods { get; set; } = [];
}

/// <summary>
/// Represents one primary contact method of a contact.
/// </summary>
public sealed class ContactPrimaryMethod
{
    /// <summary>
    /// Gets or sets whether the method is a phone number or an email address.
    /// </summary>
    public ContactPrimaryMethodKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the label of the method, such as the phone number type. Empty when the method has none.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the stored value: the phone number or the email address.
    /// </summary>
    public string Value { get; set; }
}

/// <summary>
/// The kinds of primary contact method.
/// </summary>
public enum ContactPrimaryMethodKind
{
    Phone,
    Email,
}
