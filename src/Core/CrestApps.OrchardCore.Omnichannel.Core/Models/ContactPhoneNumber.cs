namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// One of a contact's phone numbers, as it is stored on the contact.
/// </summary>
/// <remarks>
/// The number is carried as it was entered, with the region it was entered for. It is not normalized here: the
/// places that need an E.164 number or the national digits (the contact index, the time zone detection) derive them
/// in one place, so every source of contact methods is normalized the same way.
/// </remarks>
public sealed class ContactPhoneNumber
{
    /// <summary>
    /// Gets or sets the number as it was entered.
    /// </summary>
    public string PhoneNumber { get; set; }

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 region the number was entered for (for example "US"), when it is known.
    /// </summary>
    public string CountryCode { get; set; }

    /// <summary>
    /// Gets or sets the national number, when it was stored with the number.
    /// </summary>
    public string NationalNumber { get; set; }

    /// <summary>
    /// Gets or sets the kind of number, one of the <see cref="ContactPhoneTypes"/> when it is a known kind.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the extension dialed after the number is answered, when there is one.
    /// </summary>
    public string Extension { get; set; }
}
